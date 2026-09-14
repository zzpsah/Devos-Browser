# DEVOS Chrome Extension

Thin Manifest V3 bridge for controlling an existing logged-in Chrome session from DEVOS Runtime.

## Responsibilities

- connect to DEVOS Runtime through loopback localhost transport or, later, native messaging
- negotiate DEVOS Browser Capability Protocol compatibility
- attach only to tabs explicitly approved by the runtime
- expose structured bridge messages instead of arbitrary code execution
- inspect approved tab state through Chrome DevTools Protocol
- report connection and debugger lifecycle events
- reconnect safely after service-worker/runtime disconnects
- surface bridge status in a small popup

The full DEVOS brain does not live in the extension. Planning, governance, approval, verification, checkpoints and recovery stay in DEVOS Runtime. The extension must never fetch live behavior definitions from GitHub or bypass runtime governance.

## Current development slice

Implemented:

- `manifest.json` with `chrome.debugger`, tabs, storage, scripting and downloads permissions
- loopback WebSocket client targeting `ws://127.0.0.1:8787/bridge`
- versioned message envelope shared conceptually with the runtime-side schema
- hello/helloAck handshake flow
- connection-state persistence
- explicit attach/detach of a runtime-selected tab
- bounded observation of an attached tab using CDP `Runtime.evaluate`
- ping/pong and bridge event reporting
- fail-closed structured-action path until the governed action executor is implemented

Not yet implemented:

- click/type/select/navigation action execution
- normalized DOM/Accessibility element references
- download/upload handling
- multi-frame observation
- native messaging transport
- production authentication of the localhost bridge

## Load unpacked for development

1. Open `chrome://extensions`.
2. Enable **Developer mode**.
3. Choose **Load unpacked**.
4. Select this `extension/devos-chrome-extension` directory.
5. Start DEVOS Runtime bridge on loopback port `8787` before expecting a connected state.

This is a development bridge only. Do not publish or treat it as production-ready until the runtime bridge, authentication and Chrome acceptance tests are complete.
