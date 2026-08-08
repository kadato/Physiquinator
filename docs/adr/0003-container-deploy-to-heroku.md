# ADR 0003: Container deployment to Heroku

## Status

Accepted (2026-08-08)

## Context

The project is a .NET stack with a pinned preview SDK (`11.0.100-preview.6`).
Heroku has no official .NET buildpack that supports arbitrary SDK versions, but it
accepts Docker images through its container registry.

## Decision

Ship the web host as a multi-stage Docker image (`Dockerfile` at the repository
root) and deploy through the container registry:

- The build stage uses the SDK image matching `global.json`, restores with project
  files only for layer caching, then publishes with `PublishReadyToRun` for faster
  cold starts.
- Kestrel binds the `PORT` env var injected by the platform; `X-Forwarded-*` headers
  are trusted because the platform terminates TLS.
- The deployment workflow (`.github/workflows/deploy-heroku.yml`) runs the test suite
  first, builds the image, scans it with Trivy (CRITICAL severity gates the deploy),
  pushes to `registry.heroku.com/{app}/web`, and releases through the Heroku
  Platform API (`PATCH /apps/{app}/formation`), then polls `/healthz`.

## Consequences

- One workflow covers test, scan, ship, and readiness verification, and it is
  triggered by every push to `main`.
- The image build does a full restore plus ReadyToRun compilation, so pushes take a
  few minutes; acceptable for a portfolio project.
- Trivy must not block on unfixed issues in the base .NET images, hence
  `ignore-unfixed: true`; upgrades to the base images are watched via Dependabot.
