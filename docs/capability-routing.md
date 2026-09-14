# Capability Routing

Capability routing chooses the best provider for an action. DEVOS Core asks for a capability, not for a technology-specific implementation.

## Provider selection

The router evaluates every registered provider and records a candidate score. A candidate is eligible only when:

- the provider is available
- the provider advertises the requested capability
- any exact provider constraint matches
- experimental providers are allowed when the provider is marked experimental

The selected provider is returned with a confidence value and candidate diagnostics. The older `Select(...)` API remains as a compatibility wrapper over the richer `SelectRoute(...)` result.

## Constraint examples

- Existing logged-in Chrome task -> prefer Chrome/CDP.
- Cloud isolated browser task -> prefer Playwright.
- Large website extraction -> prefer Firecrawl.
- Windows file picker -> prefer Native UI Automation.
- Agent-centric browsing experiment -> BrowserAct/open-browser-use only when experimental providers are enabled.

Supported routing hints include `provider`, `preferredProvider`, `mode`, `workload`, `requiresExistingSession`, and `requiresIsolation`.

The router must choose a compatible provider or fail clearly. It must not force every task through one backend.
