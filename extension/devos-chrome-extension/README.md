# DEVOS Chrome Extension

Thin bridge for existing logged-in Chrome.

Responsibilities:

- connect to DEVOS Runtime through secure localhost or native messaging
- negotiate browser capability protocol version
- attach only to approved tabs
- inspect page state and emit normalized observations
- execute structured actions from DEVOS Runtime only after governance approval
- report browser events, navigation, downloads, frames, and reconnect state
- reconnect safely after Chrome restart

The full DEVOS brain does not live in the extension. The extension must never fetch live behavior definitions from GitHub or bypass runtime governance.
