# Runtime Model

GitHub is source control, documentation, CI/CD, and release distribution only. GitHub is not the runtime and is not required during normal operation.

## Desktop mode

```text
Chrome Extension
  -> Native Messaging or secure localhost transport
  -> DEVOS Runtime
  -> DEVOS Core
  -> Capability Router
  -> Chrome/CDP, Playwright, Firecrawl, BrowserAct, or Native UI
  -> Verification
  -> Checkpoint
```

Development endpoint: `http://127.0.0.1:8787`.

Planned endpoints:

- `POST /tasks`
- `GET /tasks/{id}`
- `POST /tasks/{id}/approve`
- `POST /tasks/{id}/decline`
- `POST /tasks/{id}/pause`
- `POST /tasks/{id}/resume`
- `POST /tasks/{id}/cancel`
- `GET /status`
- `GET /browser/status`
- `GET /ai/status`
- `GET /capabilities`
- `GET /tasks/{id}/evidence`

## Cloud mode

Cloud mode runs `Devos.Host.Server`, using Playwright and Firecrawl by default with Ollama Cloud or self-hosted Ollama. Local Chrome sessions are never copied automatically into cloud mode.
