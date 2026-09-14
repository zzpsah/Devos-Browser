# Capability Routing

Capability routing chooses the best provider for an action.

Examples:

- Existing logged-in Chrome task -> Chrome/CDP.
- Cloud isolated browser task -> Playwright.
- Large website extraction -> Firecrawl.
- Windows file picker -> Native UI Automation.
- Agent-centric browsing experiment -> BrowserAct/open-browser-use.

The router must choose a compatible provider or fail clearly. It must not force every task through one backend.
