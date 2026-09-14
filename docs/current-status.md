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
- synthetic portal pages for login, dashboard, records, challenges, iframe, submit success, session expiry, rate limit, and uncertain commit readback
- CI build/test workflow

## Latest milestone status

The branch remains draft. Binary artifacts are intentionally not published yet because the project is still core/synthetic-backend stage. Release artifacts should wait until browser-provider and packaging gates exist and pass.
