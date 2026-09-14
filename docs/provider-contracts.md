# Provider Contracts

Provider modules are replaceable capability providers. A provider may deeply use Chrome/CDP, Playwright, Firecrawl, BrowserAct, Edge, native desktop APIs, or another backend, but DEVOS Core must see only normalized capabilities and protocol models.

## Browser adapter contract

Every `IBrowserAdapter` must:

- expose a stable `ProviderId`
- advertise its supported capabilities
- report availability honestly
- return normalized `BrowserObservation` objects
- use stable per-observation element refs such as `d1`, `d2`, `d3`
- return `BrowserActionResult` for every attempted action
- never throw for ordinary page state differences that should be represented as action failure
- preserve the distinction between execution success and DEVOS verification

## Contract harness

The unit test suite includes a reusable browser-adapter contract harness. The synthetic adapter is the first provider tested against it. Future Chrome/CDP, Playwright, Edge, and BrowserAct/open-browser-use adapters must pass the same harness before they are trusted by the router.

## Security boundary

Real CAPTCHA, OTP, MFA, biometric checks, hardware keys, and account-recovery confirmations are human-only. Synthetic challenge auto-completion is permitted only in verified test fixtures under test mode.
