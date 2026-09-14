# Current Status

## 2026-09-14

Repository selected: `zzpsah/Devos-Browser`.

Development branch: `feat/devos-browser-runtime-core`.

Current milestone: Phase 0-3 foundation is CI-backed; Phase 5 Chrome/CDP bridge now has a CI-backed loopback connection, correlated runtime command path, explicit tab attach, normalized read-only observation, and the first bounded governed action executor for navigation and selector waits. Phase 4 synthetic-browser fixtures already exist for core contract testing, but the full browser fixture application is not yet complete.

## Confirmed product direction

DEVOS Browser Runtime is not a custom browser shell. It is a browser-independent runtime that can control real browsers and cloud browser backends through replaceable capability providers.

Core rule:

> DEVOS may use Chrome/CDP, Playwright, Firecrawl, BrowserAct, Edge, and other browser technologies deeply, but only through dedicated adapters/capability modules. DEVOS Core must remain independent of any single implementation.

## Implemented so far

- repository skeleton and .NET solution
- architecture/runtime/protocol/security/provider-contract docs
- capability abstractions and router
- capability route scoring, confidence, diagnostics, constraints, and experimental-provider blocking
- browser/extraction/AI abstractions
- deterministic bounded-command planner
- governance classification with semantic commit detection
- protocol versioning and negotiation
- security-challenge detection and fail-closed synthetic challenge policy
- action verifier
- in-memory checkpoint store
- one-step governed task runner
- runtime task registry
- minimal server API
- sensitive-value redactor and observation sanitizer
- synthetic browser adapter and reusable browser-adapter contract harness
- synthetic portal contract routes for login, dashboard, records, challenge pages, iframe, session expiry, rate limit, submit success, and uncertain readback
- Chrome/CDP bridge handshake, capability grants, loopback enforcement, and fail-closed legacy adapter skeleton
- Manifest V3 DEVOS Chrome extension skeleton
- extension connection-state popup and reconnect flow
- versioned extension/runtime bridge envelope with schema validation
- loopback WebSocket endpoint at `ws://127.0.0.1:8787/bridge`
- runtime-side bridge session with hello/helloAck negotiation and ping/pong
- explicit Chrome debugger attach/detach for runtime-selected tabs
- correlated runtime command broker with bounded command kinds, per-command timeout, correlation cleanup, disconnect failure propagation, and no blind replay
- active WebSocket command connection binding with stale-binding protection
- idempotent explicit tab attach from the extension
- bounded normalized page observation through CDP `Runtime.evaluate`
- snapshot-scoped element refs (`d1`, `d2`, ...), form refs, table refs, and frame refs
- observation mapping into the provider-independent `BrowserObservation` contract
- read-only runtime endpoint: `GET /browser/tabs/{tabId}/observation`
- observation failures mapped to bounded HTTP failure states instead of blind retry
- tab-scoped `ChromeCdpTabBrowserAdapter` exposing only implemented capabilities
- bounded `navigate` execution restricted to absolute HTTP/HTTPS URLs
- bounded `wait` execution restricted to a CSS selector with an 8-second upper bound
- correlated action result/error mapping into `BrowserActionResult`
- click/type/select/submit remain fail-closed in the real Chrome adapter
- CI now syntax-checks extension JavaScript in addition to .NET restore/build/test

## Latest verified CI milestone

Commit: `860e15c7f0a642fc509729ef31887c6a02cfbf1f`

Push workflow run: `34864270908`

Result:

- extension JavaScript syntax: PASS
- restore: PASS
- build: PASS
- tests: PASS
- test count: 88 passed / 0 failed
- build warnings: 0
- build errors: 0

This is CI evidence only. A real local Chrome extension/runtime acceptance test has **not** yet been performed and must be tracked separately.

No binary/release artifact has been published.

## Current Chrome bridge boundary

Working in code/CI:

1. Runtime listens on loopback `127.0.0.1:8787` by default.
2. Extension connects to the `/bridge` WebSocket endpoint.
3. Extension sends a versioned hello payload.
4. Runtime validates protocol, transport, endpoint and requested capabilities.
5. Runtime returns a bounded capability grant.
6. Runtime command broker creates correlated attach/detach/observe/action/ping commands with timeout and disconnect handling.
7. The active WebSocket session is bound to the command broker and rejects a second concurrent bridge.
8. Extension attaches only a runtime-selected Chrome tab; repeated attach for the same tab is idempotent.
9. Extension emits a bounded observation containing URL, title, visible text, interactive/semantic elements, forms, tables, frames, and document readiness.
10. Runtime maps the observation into provider-independent protocol models and exposes it through a read-only endpoint.
11. Element refs are snapshot-scoped and must be treated as stale after navigation or major DOM changes until a fresh observation is taken.
12. The tab-scoped adapter can issue only the currently implemented low-risk executor actions: `navigate` and selector `wait`.
13. Navigation rejects non-HTTP(S) schemes; wait rejects empty/invalid selectors and is time-bounded.
14. Higher-risk element mutations remain rejected before they reach the extension executor.

Still missing before Chrome acceptance can be claimed:

- observation freshness/staleness token enforcement for element-targeted actions
- controlled click/type/select execution using fresh element refs
- submit/upload approval path wired to real Chrome execution and positive readback
- richer navigation completion/readback semantics beyond initial `Page.navigate` acceptance
- multi-tab and frame contract completion
- downloads/screenshots/network events
- authenticated localhost bridge or native-messaging hardening
- real-machine Chrome acceptance tests

## Next slice

Add a snapshot freshness token to normalized observations and enforce it for element-targeted commands. After that, enable controlled `click`, `type`, and `select` only when a fresh snapshot token and valid element ref match, then rely on the existing governance and verification layers for post-action readback. Submit/upload must remain approval-gated.

Keep the PR draft. Do not publish binaries and do not merge `main` until explicit user approval and separate real-machine validation are complete.
