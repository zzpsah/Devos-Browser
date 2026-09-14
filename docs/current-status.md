# Current Status

## 2026-09-14

Repository selected: `zzpsah/Devos-Browser`.

Development branch: `feat/devos-browser-runtime-core`.

Current milestone: Phase 0-3 foundation is CI-backed; Phase 5 Chrome/CDP bridge work has started as an incremental slice. Phase 4 synthetic-browser fixtures already exist for core contract testing, but the full browser fixture application is not yet complete.

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
- Chrome/CDP bridge handshake, capability grants, loopback enforcement, and fail-closed adapter skeleton
- Manifest V3 DEVOS Chrome extension skeleton
- extension connection-state popup and reconnect flow
- versioned extension/runtime bridge envelope with schema validation
- loopback WebSocket endpoint at `ws://127.0.0.1:8787/bridge`
- runtime-side bridge session with hello/helloAck negotiation and ping/pong
- explicit Chrome debugger attach/detach for runtime-selected tabs
- bounded observation of an explicitly attached tab through CDP `Runtime.evaluate`
- correlated runtime command broker with bounded command kinds, per-command timeout, correlation cleanup, disconnect failure propagation, and no blind replay
- structured action execution remains fail-closed until the governed executor slice is implemented
- CI restore/build/test workflow

## Latest verified CI milestone

Commit: `6b84d14a995618f3575dc10b45c0b5783d5dad70`

Pull-request workflow run: `34843456751`

Result:

- restore: PASS
- build: PASS
- tests: PASS
- test count: 70 passed / 0 failed
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
7. Extension can attach only the requested tab and perform a bounded observation.
8. Correlated extension event/error responses can complete the matching broker command without blind retry.
9. Arbitrary action execution is rejected until a governed command executor is wired through the runtime.

Still missing before Chrome acceptance can be claimed:

- WebSocket command broker binding/pump from runtime callers to the active extension connection
- normalized DOM/accessibility element refs (`d1`, `d2`, ...)
- governed navigation/click/type/read/wait execution
- result/readback mapping into `BrowserActionResult`
- multi-tab and frame contract completion
- downloads/screenshots/network events
- authenticated localhost bridge or native-messaging hardening
- real-machine Chrome acceptance tests

## Next slice

Bind the command broker to the active WebSocket connection and implement the minimal governed read-only browser path (`attach -> observe -> normalized observation`). After that, add controlled navigation/click/type/read/wait execution behind existing governance.

Keep the PR draft. Do not publish binaries and do not merge `main` until explicit user approval and separate real-machine validation are complete.
