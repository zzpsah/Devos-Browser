import {
  BridgeMessageKind,
  DEVOS_PROTOCOL_VERSION,
  createEnvelope,
  validateEnvelope
} from "./bridge-protocol.js";

const BRIDGE_URL = "ws://127.0.0.1:8787/bridge";
const RECONNECT_DELAY_MS = 3000;
const MAX_VISIBLE_TEXT = 20000;

let socket = null;
let reconnectTimer = null;
const attachedTabs = new Set();

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
          "browser.read",
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
      send(BridgeMessageKind.ERROR, {
        correlationId: message.messageId,
        tabId: message.tabId,
        payload: {
          code: "ACTION_EXECUTOR_NOT_IMPLEMENTED",
          message: "Structured Chrome/CDP action execution remains fail-closed until the governed executor slice is implemented."
        }
      });
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

  try {
    await chrome.debugger.attach({ tabId }, "1.3");
    attachedTabs.add(tabId);
    send(BridgeMessageKind.EVENT, {
      correlationId: message.messageId,
      tabId: String(tabId),
      payload: { event: "tabAttached" }
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
        expression: `(() => ({
          url: location.href,
          title: document.title,
          visibleText: (document.body?.innerText || "").slice(0, ${MAX_VISIBLE_TEXT}),
          networkState: document.readyState
        }))()`,
        returnByValue: true
      }
    );

    send(BridgeMessageKind.EVENT, {
      correlationId: message.messageId,
      tabId: String(tabId),
      payload: {
        event: "observation",
        observation: evaluation?.result?.value ?? null
      }
    });
  } catch (error) {
    sendBridgeError(message, "OBSERVE_FAILED", String(error));
  }
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
    send(BridgeMessageKind.EVENT, {
      tabId: String(source.tabId),
      payload: { event: "debuggerDetached" }
    });
  }
});

chrome.tabs.onRemoved.addListener(tabId => {
  attachedTabs.delete(tabId);
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
