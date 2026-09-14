export const DEVOS_PROTOCOL_VERSION = "1.0";

export const BridgeMessageKind = Object.freeze({
  HELLO: "hello",
  HELLO_ACK: "helloAck",
  ATTACH_TAB: "attachTab",
  DETACH_TAB: "detachTab",
  OBSERVE: "observe",
  EXECUTE_ACTION: "executeAction",
  EVENT: "event",
  PING: "ping",
  PONG: "pong",
  ERROR: "error"
});

const ALLOWED_KINDS = new Set(Object.values(BridgeMessageKind));
const TAB_SCOPED_KINDS = new Set([
  BridgeMessageKind.ATTACH_TAB,
  BridgeMessageKind.DETACH_TAB,
  BridgeMessageKind.OBSERVE,
  BridgeMessageKind.EXECUTE_ACTION
]);

export function createEnvelope(kind, { correlationId = null, tabId = null, payload = null } = {}) {
  if (!ALLOWED_KINDS.has(kind)) {
    throw new Error(`Unsupported bridge message kind '${kind}'.`);
  }

  return {
    protocolVersion: DEVOS_PROTOCOL_VERSION,
    messageId: crypto.randomUUID(),
    kind,
    timestamp: new Date().toISOString(),
    correlationId,
    tabId,
    payload
  };
}

export function validateEnvelope(message) {
  if (!message || typeof message !== "object") {
    return { valid: false, error: "Bridge message is required." };
  }

  if (typeof message.messageId !== "string" || message.messageId.trim() === "") {
    return { valid: false, error: "Bridge message id is required." };
  }

  if (!ALLOWED_KINDS.has(message.kind)) {
    return { valid: false, error: `Unsupported bridge message kind '${message.kind}'.` };
  }

  if (!/^\d+\.\d+$/.test(message.protocolVersion ?? "")) {
    return { valid: false, error: "Invalid DEVOS protocol version." };
  }

  if (!message.timestamp || Number.isNaN(Date.parse(message.timestamp))) {
    return { valid: false, error: "Bridge message timestamp is required." };
  }

  if (TAB_SCOPED_KINDS.has(message.kind) && (message.tabId === null || message.tabId === undefined || String(message.tabId).trim() === "")) {
    return { valid: false, error: `Bridge message kind '${message.kind}' requires a tab id.` };
  }

  return { valid: true, error: null };
}
