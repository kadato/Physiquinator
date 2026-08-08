# ADR 0001: Browser IndexedDB as the persistence layer for the web host

## Status

Accepted (2026-08-08)

## Context

The web host (`Physiquinator.Web`) runs on hosts with an ephemeral filesystem:
restarts and redeploys wipe the SQLite databases stored under the temp directory.
The data layer (`sqlite-net-pcl`) reads
and writes local files and cannot be pointed at a managed database without a full
repository rewrite.

## Decision

Keep SQLite as the storage engine on the server, and mirror the database files to the
browser's IndexedDB:

- **Restore**: a script in `App.razor` runs before `Blazor.start()`, reads every
  `physiquinator*.db3` copy from IndexedDB, and POSTs them to `/api/db/restore`. The
  files therefore exist on disk before the first circuit opens, so the normal open-
  then-seed flow is untouched and the demo seeder's idempotency checks handle
  restored data.
- **Save**: a web-only component (`DbSyncHost`) awaits app initialization, then every
  15 seconds checkpoints each database (WAL `TRUNCATE` via a short-lived connection)
  and pushes the bytes to IndexedDB through JS interop.

## Consequences

- Data survives server restarts and redeploys with no changes to the Core data layer.
- Data is bound to a browser and an account, not to a device-independent server
  store; two devices do not see each other's changes.
- Up to 15 seconds of edits can be lost on abrupt tab close.
- If the project ever needs true multi-device sync, the repository layer must be
  abstracted and backed by Postgres or an object store (see ADR 0002 for the
  account-isolation groundwork).
