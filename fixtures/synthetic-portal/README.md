# Synthetic Portal

This fixture is the local, synthetic-only portal for DEVOS browser automation.

Implemented in this slice as `Devos.Browser.Synthetic`:

- login page
- dashboard page
- 100 fake student records
- records table metadata
- pagination route
- mock CAPTCHA marker
- mock OTP marker
- mock MFA marker
- mock Turnstile marker
- mock reCAPTCHA marker
- session-expired route
- rate-limit route
- iframe metadata route
- submit-success route
- uncertain commit simulator with readback route

All data is synthetic. The fixture must never use real school, student, credential, payment, or portal data.
