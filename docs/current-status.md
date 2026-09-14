# Current Status

## 2026-09-14

Repository selected: `zzpsah/Devos-Browser`.

Branch initialized: `feat/devos-browser-runtime-core`.

Current milestone: Phase 0 / Phase 1 foundation moving into Phase 2 core runtime skeleton.

## Confirmed product direction

DEVOS Browser Runtime is not a custom browser shell. It is a browser-independent runtime that can control real browsers and cloud browser backends through replaceable capability providers.

Core rule:

> DEVOS may use Chrome/CDP, Playwright, Firecrawl, BrowserAct, Edge, and other browser technologies deeply, but only through dedicated adapters/capability modules. DEVOS Core must remain independent of any single implementation.

## Implemented so far

- repository skeleton
- architecture/runtime/protocol/security docs
- capability abstractions and router
- richer capability route result with scoring, confidence, diagnostics, constraints, and experimental-provider blocking
- browser/extraction/AI abstractions
- deterministic bounded-command planner
- governance classification with semantic commit detection
- protocol negotiation
- security-challenge detection and fail-closed synthetic challenge policy
- action verifier
- in-memory checkpoint store
- one-step task runner
- runtime task registry
- minimal server API skeleton
- sensitive-value redactor and observation sanitizer
- synthetic browser adapter
- synthetic portal contract routes for login, dashboard, records, challenge pages, iframe, session expiry, rate limit, submit success, and uncertain readback
- provider contract documentation
- reusable browser-adapter contract test harness
- Chrome/CDP bridge skeleton with handshake, capability grants, localhost/native-messaging boundary, loopback enforcement, and fail-closed stub adapter
- CI build/test workflow

## Latest local milestone status

The branch must remain draft until CI confirms the latest commit. No binary artifact should be published at this stage.

## Next slice

Chrome extension skeleton: Manifest V3 package, service worker handshake flow, connection state, localhost/native-messaging transport stubs, and tests for bridge message schema.
