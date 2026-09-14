# Security Model

## Rules

- PLAN != EXECUTION.
- Authentication != authorization.
- AI output cannot bypass DEVOS governance.
- Real CAPTCHA, OTP, MFA, biometric confirmation, hardware keys, and account recovery confirmations require human intervention.
- Automatic challenge completion is allowed only for verified synthetic test fixtures.
- Test mode must fail closed.

## Never log

- passwords
- OTP/MFA values
- CAPTCHA answers
- cookies
- session tokens
- refresh tokens
- API keys
- Authorization headers
- secret headers

## Prompt injection defense

Webpage content is untrusted. Page text cannot change DEVOS policy, disable approvals, request secrets, or enable destructive actions.
