import {
  BridgeMessageKind,
  DEVOS_PROTOCOL_VERSION,
  createEnvelope,
  validateEnvelope
} from "./bridge-protocol.js";

const BRIDGE_URL = "ws://127.0.0.1:8787/bridge";
const RECONNECT_DELAY_MS = 3000;
const MAX_VISIBLE_TEXT = 20000;
const MAX_ELEMENTS = 250;
const MAX_FORMS = 50;
const MAX_TABLES = 50;
const MAX_FRAMES = 50;
const MAX_WAIT_MS = 8000;

let socket = null;
let reconnectTimer = null;
const attachedTabs = new Set();
const tabSnapshots = new Map();

async function setConnectionState(state, detail = null) {
  await chrome.storage.local.set({
    devosBridgeState: state,
    devosBridgeDetail: detail,
    devosBridgeProtocolVersion: DEVOS_PROTOCOL_VERSION,
    devosBridgeUpdatedAt: new Date().toISOString()
  });
}

function send(kind, options = {}) {
  if (!socket || socket.readyState !== WebSocket.OPEN) {
    return false;
  }

  socket.send(JSON.stringify(createEnvelope(kind, options)));
  return true;
}

function scheduleReconnect() {
  if (reconnectTimer) {
    return;
  }

  reconnectTimer = setTimeout(() => {
    reconnectTimer = null;
    connect();
  }, RECONNECT_DELAY_MS);
}

function connect() {
  if (socket && (socket.readyState === WebSocket.OPEN || socket.readyState === WebSocket.CONNECTING)) {
    return;
  }

  setConnectionState("connecting", BRIDGE_URL);

  try {
    socket = new WebSocket(BRIDGE_URL);
  } catch (error) {
    setConnectionState("disconnected", String(error));
    scheduleReconnect();
    return;
  }

  socket.addEventListener("open", async () => {
    await setConnectionState("handshaking", BRIDGE_URL);
    const manifest = chrome.runtime.getManifest();

    send(BridgeMessageKind.HELLO, {
      payload: {
        extensionId: chrome.runtime.id,
        extensionVersion: manifest.version,
        protocolVersion: DEVOS_PROTOCOL_VERSION,
        transportMode: "Localhost",
        runtimeEndpoint: "http://127.0.0.1:8787",
        requestedCapabilities: [
          "browser.navigate",
          "browser.back",
          "browser.forward",
          "browser.reload",
          "browser.observe",
          "browser.click",
          "browser.type",
          "browser.select",
          "browser.read",
          "browser.wait.selector",
          "browser.tabs",
          "browser.network",
          "browser.accessibility",
          "browser.download",
          "browser.screenshot"
        ]
      }
    });
  });

  socket.addEventListener("message", async event => {
    await handleRuntimeMessage(event.data);
  });

  socket.addEventListener("close", async event => {
    socket = null;
    attachedTabs.clear();
    tabSnapshots.clear();
    await setConnectionState("disconnected", `socket closed (${event.code})`);
    scheduleReconnect();
  });

  socket.addEventListener("error", async () => {
    await setConnectionState("error", "runtime bridge connection error");
  });
}

async function handleRuntimeMessage(raw) {
  let message;

  try {
    message = JSON.parse(raw);
  } catch {
    send(BridgeMessageKind.ERROR, { payload: { code: "INVALID_JSON" } });
    return;
  }

  const validation = validateEnvelope(message);
  if (!validation.valid) {
    send(BridgeMessageKind.ERROR, {
      correlationId: message?.messageId ?? null,
      payload: { code: "INVALID_MESSAGE", message: validation.error }
    });
    return;
  }

  switch (message.kind) {
    case BridgeMessageKind.HELLO_ACK:
      if (message.payload?.state === "CONNECTED") {
        await setConnectionState("connected", message.payload?.message ?? null);
      } else {
        await setConnectionState("rejected", message.payload?.reason ?? "Bridge handshake rejected.");
      }
      break;

    case BridgeMessageKind.PING:
      send(BridgeMessageKind.PONG, { correlationId: message.messageId });
      break;

    case BridgeMessageKind.ATTACH_TAB:
      await attachTab(message);
      break;

    case BridgeMessageKind.DETACH_TAB:
      await detachTab(message);
      break;

    case BridgeMessageKind.OBSERVE:
      await observeTab(message);
      break;

    case BridgeMessageKind.EXECUTE_ACTION:
      await executeAction(message);
      break;

    default:
      break;
  }
}

async function attachTab(message) {
  const tabId = Number(message.tabId);
  if (!Number.isInteger(tabId) || tabId <= 0) {
    sendBridgeError(message, "INVALID_TAB", "Tab id must be a positive integer.");
    return;
  }

  if (attachedTabs.has(tabId)) {
    send(BridgeMessageKind.EVENT, {
      correlationId: message.messageId,
      tabId: String(tabId),
      payload: { event: "tabAttached", alreadyAttached: true }
    });
    return;
  }

  try {
    await chrome.debugger.attach({ tabId }, "1.3");
    attachedTabs.add(tabId);
    invalidateSnapshot(tabId);
    send(BridgeMessageKind.EVENT, {
      correlationId: message.messageId,
      tabId: String(tabId),
      payload: { event: "tabAttached", alreadyAttached: false }
    });
  } catch (error) {
    sendBridgeError(message, "ATTACH_FAILED", String(error));
  }
}

async function detachTab(message) {
  const tabId = Number(message.tabId);
  if (!Number.isInteger(tabId) || tabId <= 0) {
    sendBridgeError(message, "INVALID_TAB", "Tab id must be a positive integer.");
    return;
  }

  try {
    if (attachedTabs.has(tabId)) {
      await chrome.debugger.detach({ tabId });
    }
    attachedTabs.delete(tabId);
    invalidateSnapshot(tabId);
    send(BridgeMessageKind.EVENT, {
      correlationId: message.messageId,
      tabId: String(tabId),
      payload: { event: "tabDetached" }
    });
  } catch (error) {
    sendBridgeError(message, "DETACH_FAILED", String(error));
  }
}

async function observeTab(message) {
  const tabId = Number(message.tabId);
  if (!attachedTabs.has(tabId)) {
    sendBridgeError(message, "TAB_NOT_ATTACHED", "DEVOS may observe only tabs explicitly attached through the bridge.");
    return;
  }

  try {
    const evaluation = await chrome.debugger.sendCommand(
      { tabId },
      "Runtime.evaluate",
      {
        expression: `(() => {
          const normalizeText = value => String(value || "").replace(/\\s+/g, " ").trim();
          const isVisible = element => {
            if (!(element instanceof Element)) return false;
            const style = getComputedStyle(element);
            if (style.display === "none" || style.visibility === "hidden" || Number(style.opacity) === 0) return false;
            const rect = element.getBoundingClientRect();
            return rect.width > 0 && rect.height > 0;
          };
          const inferRole = element => {
            const explicitRole = element.getAttribute("role");
            if (explicitRole) return explicitRole;
            const tag = element.tagName.toLowerCase();
            if (tag === "a" && element.hasAttribute("href")) return "link";
            if (tag === "button" || tag === "summary") return "button";
            if (tag === "select") return "combobox";
            if (tag === "textarea") return "textbox";
            if (tag === "input") {
              const type = (element.getAttribute("type") || "text").toLowerCase();
              if (type === "checkbox") return "checkbox";
              if (type === "radio") return "radio";
              if (["button", "submit", "reset", "image"].includes(type)) return "button";
              return "textbox";
            }
            if (element.isContentEditable) return "textbox";
            return null;
          };
          const safeLabel = element => {
            const tag = element.tagName.toLowerCase();
            const aria = normalizeText(element.getAttribute("aria-label"));
            if (aria) return aria.slice(0, 500);
            const title = normalizeText(element.getAttribute("title"));
            const placeholder = normalizeText(element.getAttribute("placeholder"));
            const name = normalizeText(element.getAttribute("name"));
            const inner = tag === "input" ? "" : normalizeText(element.innerText || element.textContent);
            return (inner || placeholder || title || name || "").slice(0, 500) || null;
          };

          const candidates = Array.from(document.querySelectorAll(
            'a[href],button,input,select,textarea,summary,[role],[contenteditable="true"]'
          ));
          const elements = [];
          const refByElement = new Map();

          for (const element of candidates) {
            if (elements.length >= ${MAX_ELEMENTS}) break;
            if (!isVisible(element)) continue;

            const ref = "d" + (elements.length + 1);
            refByElement.set(element, ref);
            elements.push({
              ref,
              role: inferRole(element),
              text: safeLabel(element),
              type: (element.getAttribute("type") || element.tagName.toLowerCase()).toLowerCase(),
              href: element instanceof HTMLAnchorElement ? element.href : null,
              visible: true
            });
          }

          const forms = Array.from(document.forms).slice(0, ${MAX_FORMS}).map((form, index) => ({
            ref: "f" + (index + 1),
            elementRefs: Array.from(form.elements)
              .map(element => refByElement.get(element))
              .filter(Boolean)
          }));

          const tables = Array.from(document.querySelectorAll("table")).slice(0, ${MAX_TABLES}).map((table, index) => {
            const rows = Array.from(table.rows || []);
            const columnCount = rows.reduce((max, row) => Math.max(max, row.cells?.length || 0), 0);
            return {
              ref: "t" + (index + 1),
              rowCount: rows.length,
              columnCount
            };
          });

          const frames = Array.from(document.querySelectorAll("iframe,frame")).slice(0, ${MAX_FRAMES}).map((frame, index) => ({
            ref: "fr" + (index + 1),
            url: frame.getAttribute("src") || null,
            title: frame.getAttribute("title") || null
          }));

          return {
            url: location.href,
            title: document.title,
            visibleText: (document.body?.innerText || "").slice(0, ${MAX_VISIBLE_TEXT}),
            elements,
            forms,
            tables,
            frames,
            networkState: document.readyState
          };
        })()`,
        returnByValue: true
      }
    );

    const observation = evaluation?.result?.value;
    if (!observation || typeof observation !== "object") {
      sendBridgeError(message, "OBSERVE_FAILED", "Chrome did not return a normalized observation object.");
      return;
    }

    const snapshotToken = createSnapshotToken();
    observation.snapshotToken = snapshotToken;
    tabSnapshots.set(tabId, {
      token: snapshotToken,
      elements: Array.isArray(observation.elements) ? observation.elements.map(element => ({ ...element })) : []
    });

    send(BridgeMessageKind.EVENT, {
      correlationId: message.messageId,
      tabId: String(tabId),
      payload: {
        event: "observation",
        observation
      }
    });
  } catch (error) {
    sendBridgeError(message, "OBSERVE_FAILED", String(error));
  }
}

async function executeAction(message) {
  const tabId = Number(message.tabId);
  if (!attachedTabs.has(tabId)) {
    sendBridgeError(message, "TAB_NOT_ATTACHED", "DEVOS may execute actions only on explicitly attached tabs.");
    return;
  }

  const action = message.payload?.action;
  const kind = String(action?.kind || "").toLowerCase();

  if (kind === "navigate") {
    const targetUrl = String(action?.value || action?.target || "").trim();
    let parsedUrl;

    try {
      parsedUrl = new URL(targetUrl);
    } catch {
      sendBridgeError(message, "INVALID_URL", "Navigate requires a valid absolute URL.");
      return;
    }

    if (!["http:", "https:"].includes(parsedUrl.protocol)) {
      sendBridgeError(message, "UNSAFE_URL_SCHEME", "DEVOS Chrome navigation is limited to HTTP and HTTPS URLs.");
      return;
    }

    try {
      invalidateSnapshot(tabId);
      const result = await chrome.debugger.sendCommand({ tabId }, "Page.navigate", { url: parsedUrl.href });
      if (result?.errorText) {
        sendBridgeError(message, "NAVIGATE_FAILED", result.errorText);
        return;
      }

      sendActionResult(message, tabId, {
        action: "navigate",
        success: true,
        url: parsedUrl.href
      });
    } catch (error) {
      sendBridgeError(message, "NAVIGATE_FAILED", String(error));
    }
    return;
  }

  if (kind === "wait") {
    const selector = String(action?.target || "").trim();
    if (!selector) {
      sendBridgeError(message, "INVALID_WAIT_TARGET", "Wait requires a CSS selector target.");
      return;
    }

    const deadline = Date.now() + MAX_WAIT_MS;
    while (Date.now() < deadline) {
      try {
        const evaluation = await chrome.debugger.sendCommand(
          { tabId },
          "Runtime.evaluate",
          {
            expression: `(() => { try { return document.querySelector(${JSON.stringify(selector)}) !== null; } catch { return "__DEVOS_INVALID_SELECTOR__"; } })()`,
            returnByValue: true
          }
        );

        const value = evaluation?.result?.value;
        if (value === "__DEVOS_INVALID_SELECTOR__") {
          sendBridgeError(message, "INVALID_SELECTOR", "Wait target is not a valid CSS selector.");
          return;
        }

        if (value === true) {
          sendActionResult(message, tabId, {
            action: "wait",
            success: true,
            target: selector
          });
          return;
        }
      } catch (error) {
        sendBridgeError(message, "WAIT_FAILED", String(error));
        return;
      }

      await new Promise(resolve => setTimeout(resolve, 100));
    }

    sendBridgeError(message, "WAIT_TIMEOUT", `Selector was not found within ${MAX_WAIT_MS} ms.`);
    return;
  }

  if (["click", "type", "select"].includes(kind)) {
    await executeFreshElementAction(message, tabId, action, kind);
    return;
  }

  sendBridgeError(
    message,
    "ACTION_NOT_ENABLED",
    `Chrome action '${kind || "unknown"}' is not enabled by the current DEVOS bridge executor.`
  );
}

async function executeFreshElementAction(message, tabId, action, kind) {
  const targetRef = String(action?.target || "").trim();
  const snapshotToken = String(action?.snapshotToken || "").trim();

  if (!/^d[1-9]\d*$/.test(targetRef)) {
    sendBridgeError(message, "INVALID_ELEMENT_REF", "Element action requires a normalized DEVOS element ref such as d1.");
    return;
  }

  if (!snapshotToken) {
    sendBridgeError(message, "SNAPSHOT_REQUIRED", "Element action requires the snapshot token from the observation that produced the ref.");
    return;
  }

  const snapshot = tabSnapshots.get(tabId);
  if (!snapshot || snapshot.token !== snapshotToken) {
    sendBridgeError(message, "STALE_SNAPSHOT", "Element ref snapshot is missing or stale. Take a fresh observation before acting.");
    return;
  }

  const elementSnapshot = snapshot.elements.find(element => element.ref === targetRef);
  if (!elementSnapshot) {
    sendBridgeError(message, "UNKNOWN_ELEMENT_REF", `Element ref '${targetRef}' is not present in the active observation snapshot.`);
    return;
  }

  if (kind === "type" && typeof action?.value !== "string") {
    sendBridgeError(message, "VALUE_REQUIRED", "Type action requires a string value.");
    return;
  }

  if (kind === "select" && typeof action?.value !== "string") {
    sendBridgeError(message, "VALUE_REQUIRED", "Select action requires a string option value or text.");
    return;
  }

  const refIndex = Number(targetRef.slice(1)) - 1;
  const expected = {
    role: elementSnapshot.role ?? null,
    text: elementSnapshot.text ?? null,
    type: elementSnapshot.type ?? null,
    href: elementSnapshot.href ?? null
  };
  const value = typeof action?.value === "string" ? action.value : null;

  try {
    const evaluation = await chrome.debugger.sendCommand(
      { tabId },
      "Runtime.evaluate",
      {
        expression: `(() => {
          const normalizeText = value => String(value || "").replace(/\\s+/g, " ").trim();
          const isVisible = element => {
            if (!(element instanceof Element)) return false;
            const style = getComputedStyle(element);
            if (style.display === "none" || style.visibility === "hidden" || Number(style.opacity) === 0) return false;
            const rect = element.getBoundingClientRect();
            return rect.width > 0 && rect.height > 0;
          };
          const inferRole = element => {
            const explicitRole = element.getAttribute("role");
            if (explicitRole) return explicitRole;
            const tag = element.tagName.toLowerCase();
            if (tag === "a" && element.hasAttribute("href")) return "link";
            if (tag === "button" || tag === "summary") return "button";
            if (tag === "select") return "combobox";
            if (tag === "textarea") return "textbox";
            if (tag === "input") {
              const type = (element.getAttribute("type") || "text").toLowerCase();
              if (type === "checkbox") return "checkbox";
              if (type === "radio") return "radio";
              if (["button", "submit", "reset", "image"].includes(type)) return "button";
              return "textbox";
            }
            if (element.isContentEditable) return "textbox";
            return null;
          };
          const safeLabel = element => {
            const tag = element.tagName.toLowerCase();
            const aria = normalizeText(element.getAttribute("aria-label"));
            if (aria) return aria.slice(0, 500);
            const title = normalizeText(element.getAttribute("title"));
            const placeholder = normalizeText(element.getAttribute("placeholder"));
            const name = normalizeText(element.getAttribute("name"));
            const inner = tag === "input" ? "" : normalizeText(element.innerText || element.textContent);
            return (inner || placeholder || title || name || "").slice(0, 500) || null;
          };
          const currentFingerprint = element => ({
            role: inferRole(element),
            text: safeLabel(element),
            type: (element.getAttribute("type") || element.tagName.toLowerCase()).toLowerCase(),
            href: element instanceof HTMLAnchorElement ? element.href : null
          });
          const equalFingerprint = (left, right) =>
            left.role === right.role && left.text === right.text && left.type === right.type && left.href === right.href;

          const candidates = Array.from(document.querySelectorAll(
            'a[href],button,input,select,textarea,summary,[role],[contenteditable="true"]'
          )).filter(isVisible).slice(0, ${MAX_ELEMENTS});
          const element = candidates[${refIndex}];
          if (!element) return { ok: false, code: "STALE_ELEMENT", message: "Element ref no longer resolves." };

          const expected = ${JSON.stringify(expected)};
          if (!equalFingerprint(currentFingerprint(element), expected)) {
            return { ok: false, code: "STALE_ELEMENT", message: "Element fingerprint changed after observation." };
          }

          const kind = ${JSON.stringify(kind)};
          const value = ${JSON.stringify(value)};

          if (kind === "click") {
            element.focus?.();
            element.click();
            return { ok: true };
          }

          if (kind === "type") {
            if (element instanceof HTMLInputElement || element instanceof HTMLTextAreaElement) {
              element.focus();
              const prototype = element instanceof HTMLTextAreaElement ? HTMLTextAreaElement.prototype : HTMLInputElement.prototype;
              const descriptor = Object.getOwnPropertyDescriptor(prototype, "value");
              if (descriptor?.set) descriptor.set.call(element, value);
              else element.value = value;
              element.dispatchEvent(new InputEvent("input", { bubbles: true, inputType: "insertText", data: value }));
              element.dispatchEvent(new Event("change", { bubbles: true }));
              return { ok: true };
            }

            if (element.isContentEditable) {
              element.focus();
              element.textContent = value;
              element.dispatchEvent(new InputEvent("input", { bubbles: true, inputType: "insertText", data: value }));
              return { ok: true };
            }

            return { ok: false, code: "TARGET_NOT_EDITABLE", message: "Target element is not editable." };
          }

          if (kind === "select") {
            if (!(element instanceof HTMLSelectElement)) {
              return { ok: false, code: "TARGET_NOT_SELECT", message: "Target element is not a select control." };
            }

            const option = Array.from(element.options).find(candidate =>
              candidate.value === value || normalizeText(candidate.textContent) === normalizeText(value));
            if (!option) {
              return { ok: false, code: "OPTION_NOT_FOUND", message: "Requested option was not found." };
            }

            element.value = option.value;
            element.dispatchEvent(new Event("input", { bubbles: true }));
            element.dispatchEvent(new Event("change", { bubbles: true }));
            return { ok: true };
          }

          return { ok: false, code: "ACTION_NOT_ENABLED", message: "Element action is not enabled." };
        })()`,
        returnByValue: true
      }
    );

    const result = evaluation?.result?.value;
    if (!result?.ok) {
      sendBridgeError(message, result?.code || "ELEMENT_ACTION_FAILED", result?.message || "Element action failed.");
      return;
    }

    invalidateSnapshot(tabId);
    sendActionResult(message, tabId, {
      action: kind,
      success: true,
      target: targetRef,
      snapshotToken
    });
  } catch (error) {
    sendBridgeError(message, "ELEMENT_ACTION_FAILED", String(error));
  }
}

function sendActionResult(message, tabId, result) {
  send(BridgeMessageKind.EVENT, {
    correlationId: message.messageId,
    tabId: String(tabId),
    payload: {
      event: "actionResult",
      ...result
    }
  });
}

function createSnapshotToken() {
  if (typeof crypto?.randomUUID === "function") {
    return crypto.randomUUID();
  }

  return `snapshot-${Date.now()}-${Math.random().toString(16).slice(2)}`;
}

function invalidateSnapshot(tabId) {
  tabSnapshots.delete(Number(tabId));
}

function sendBridgeError(message, code, detail) {
  send(BridgeMessageKind.ERROR, {
    correlationId: message?.messageId ?? null,
    tabId: message?.tabId ?? null,
    payload: { code, message: detail }
  });
}

chrome.debugger.onDetach.addListener(async source => {
  if (source.tabId) {
    attachedTabs.delete(source.tabId);
    invalidateSnapshot(source.tabId);
    send(BridgeMessageKind.EVENT, {
      tabId: String(source.tabId),
      payload: { event: "debuggerDetached" }
    });
  }
});

chrome.tabs.onRemoved.addListener(tabId => {
  attachedTabs.delete(tabId);
  invalidateSnapshot(tabId);
});

chrome.tabs.onUpdated.addListener((tabId, changeInfo) => {
  if (changeInfo.status === "loading" || typeof changeInfo.url === "string") {
    invalidateSnapshot(tabId);
  }
});

chrome.runtime.onInstalled.addListener(() => {
  setConnectionState("disconnected", "extension installed");
  connect();
});

chrome.runtime.onStartup.addListener(() => {
  connect();
});

chrome.runtime.onMessage.addListener((message, _sender, sendResponse) => {
  if (message?.type === "devos.bridge.reconnect") {
    connect();
    sendResponse({ ok: true });
    return true;
  }

  if (message?.type === "devos.bridge.status") {
    chrome.storage.local.get([
      "devosBridgeState",
      "devosBridgeDetail",
      "devosBridgeProtocolVersion",
      "devosBridgeUpdatedAt"
    ]).then(sendResponse);
    return true;
  }

  return false;
});

connect();
