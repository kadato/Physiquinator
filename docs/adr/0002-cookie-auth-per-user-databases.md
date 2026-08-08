# ADR 0002: Cookie authentication with per-account databases

## Status

Accepted (2026-08-08)

## Context

The web host was a single-tenant demo: every visitor's circuit opened the same SQLite
file, so users overwrote each other's data, and there was no way to protect the agent
API beyond an optional shared key.

## Decision

Add ASP.NET Core cookie authentication with a lightweight account store:

- **Accounts**: `WebUserStore` keeps users in a small SQLite database with PBKDF2
  (100k iterations, SHA-256) password hashes. A demo account is seeded from
  `AUTH_DEMO_USERNAME` / `AUTH_DEMO_PASSWORD` (default `demo` / `demo1234`).
- **Session**: a SameSite=Lax, HTTPS-aware cookie, 30-day sliding expiration. Auth
  endpoints are JSON-only with no CORS, which blocks cross-origin form-based CSRF;
  login is rate limited.
- **Isolation**: `WebUserDatabasePathProvider` derives the database file name from
  the authenticated account id (`physiquinator_{userId}.db3`), registered after
  Core's path provider so per-circuit resolution picks it up. MCP tool calls, which
  create their own scopes, fall back to an isolated `mcp-agent` tenant.
- **Gating**: `AuthGate` is the interactive root component. It captures the auth
  state before any app service resolves (so the path provider already knows the
  account), then renders either the login panel or the app.

## Consequences

- Users are isolated per account; the IndexedDB sync stores only the current
  account's files in a browser.
- The `/mcp` endpoint remains key-authenticated (required in production) and is
  independent of browser sessions.
- Registration is open to anyone; suitable for a demo, but production would want
  email verification or an invite flow.
- Auth cookies are encrypted with ASP.NET DataProtection keys stored on the
  ephemeral dyno filesystem, so a dyno restart invalidates existing sessions and
  signs everyone out once; the data itself is untouched. Persisting the key ring
  (e.g. in object storage) is the fix for a production deployment.
- Account rows are not yet linked to an external identity provider; adding OAuth
  later only requires a second authentication handler and mapping the external id.
