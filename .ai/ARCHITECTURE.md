# Architecture

No browser/runtime architecture is treated as verified yet.

This project is intended to become a DevOS browser/runtime integration project. Component boundaries, browser providers, execution model, authentication handling, safety controls, and external integrations must be derived from current source and explicit project decisions before being recorded as architecture truth.

## Current verified boundary
- Repository-local DevOS context is the durable project continuity layer.
- Browser/runtime implementation claims remain unverified until source and tests provide evidence.
