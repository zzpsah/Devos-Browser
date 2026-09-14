# Architecture

## Goal

DEVOS Browser Runtime is a governed browser automation runtime. It accepts user goals, plans safe browser actions, selects the best provider, verifies outcomes, checkpoints progress, and recovers safely.

## Core rule

DEVOS may use Chrome/CDP, Playwright, Firecrawl, BrowserAct, Edge, and other browser technologies deeply, but only through dedicated adapters/capability modules. DEVOS Core must remain independent of any single implementation.

## High-level flow

```text
User Goal
  -> DEVOS Runtime
  -> DEVOS Core
  -> Planner
  -> Governance
  -> Capability Router
  -> Selected Provider
  -> Execution
  -> Verification
  -> Checkpoint
  -> Next Step
```

## Capability providers

- Chrome/CDP: primary local existing-browser control.
- Playwright: isolated and cloud browser automation.
- Firecrawl: crawling, scraping, and structured extraction.
- BrowserAct/open-browser-use: optional agent browser adapters.
- Native UI: desktop dialogs that are outside web DOM.
- Ollama Local/Cloud: AI planning providers.
- Deterministic provider: non-AI fallback.

## Boundary

DEVOS Core owns policy, verification, checkpoints, recovery, and provider selection. Provider modules own implementation-specific details.
