# Testing Strategy

## Required test groups

- Unit tests
- Protocol tests
- Capability routing tests
- Integration tests
- Browser tests
- Security tests
- Synthetic challenge tests
- Recovery tests
- Packaging smoke

## Synthetic portal

Use only synthetic data. The portal must include fake records, pagination, forms, download/upload, iframe, delayed load, network failure, session expiry, mock CAPTCHA, mock OTP, mock MFA, mock Turnstile, mock rate-limit, and uncertain commit simulation.

## Release rule

No artifact publication before all mandatory gates pass.
