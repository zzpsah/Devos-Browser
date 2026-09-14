# Current Status

## 2026-09-14

Repository selected: `zzpsah/Devos-Browser`.

Development branch: `feat/devos-browser-runtime-core`.

Current milestone: Phase 0-3 foundation is CI-backed; Phase 5 Chrome/CDP bridge now has a CI-backed loopback connection, correlated runtime command path, explicit tab attach, and normalized read-only observation slice. Phase 4 synthetic-browser fixtures already exist for core contract testing, but the full browser fixture application is not yet complete.

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
- correlated runtime command broker with bounded command kinds, per-command timeout, correlation cleanup, disconnect failure propagation, and no blind replay
- active WebSocket command connection binding with stale-binding protection
- idempotent explicit tab attach from the extension
- bounded normalized page observation through CDP `Runtime.evaluate`
- snapshot-scoped element refs (`d1`, `d2`, ...), form refs, table refs, and frame refs
- observation mapping into the provider-independent `BrowserObservation` contract
- read-only runtime endpoint: `GET /browser/tabs/{tabId}/observation`
- observation failures mapped to bounded HTTP failure states instead of blind retry
- structured mutation execution remains fail-closed until the governed executor slice is implemented
- CI restore/build/test workflow

## Latest verified CI milestone

Commit: `84d1f4aee53963f75efeb1aa423f9ae83529b6db`

Push workflow run: `34863674312`

Result:

- restore: PASS
- build: PASS
- tests: PASS
- test count: 78 passed / 0 failed
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
12. Arbitrary action execution is still rejected until the governed command executor is wired through the runtime.

Still missing before Chrome acceptance can be claimed:

- governed navigation/read/wait execution through the bridge
- controlled click/type/select execution using fresh element refs
- positive result/readback mapping into `BrowserActionResult`
- observation freshness/staleness token enforcement for mutation actions
- multi-tab and frame contract completion
- downloads/screenshots/network events
- authenticated localhost bridge or native-messaging hardening
- real-machine Chrome acceptance tests

## Next slice

Implement the first governed Chrome action executor for low-risk actions (`navigate`, `read`, `wait`) with bounded payloads, fresh observation/readback, and tests. Keep click/type/select fail-closed until the observation-ref freshness boundary is enforced. Then add controlled mutations behind existing governance and verification.

Keep the PR draft. Do not publish binaries and do not merge `main` until explicit user approval and separate real-machine validation are complete.
