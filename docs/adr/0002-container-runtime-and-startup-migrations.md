# 0002. Container runtime and startup migrations

- **Status:** Accepted (2026-11-15, by Alessandro: "Yes and proceed" at Phase 6 Checkpoint B, answering whether ADR 0002 and ADR 0003 can be marked Accepted)
- **Date:** 2026-11-15
- **Decided by:** Alessandro (on the agent's recommendation): P4-D1 option A at Phase 4 Checkpoint A ("Proceed"), and "the agent writes ADR 0002 as a draft" on 2026-11-15 ("Proceed").

## Context

Phase 4 adds a Docker image and `compose.yaml` for a local demo. The container needs a SQLite schema before the first request.

Since Phase 3, `Program.cs` applies EF Core migrations at startup only when the environment is `Development`. Security finding I1 (Phase 3) asked for this rule, and where the connection string comes from, to be written down.

EF Core's guidance ([Applying migrations](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying)) is:
- Runtime `Migrate()`/`MigrateAsync()` is fine for apps that can tolerate startup migration behaviour, but not for every replica of a production service.
- For containers, build a migration bundle and run it once as a deployment job before the new version starts.
- On SQLite, a process killed during a migration can leave the migration lock behind and block later runs.

Phase 4 and Phase 6 ran in parallel. Phase 6 owns `Program.cs`, so Phase 4 could not change startup code without a file conflict.

## Options considered

1. **Compose runs the API as `Development`.** No code change. The existing Development-only startup migration creates the schema. Scalar and the OpenAPI document are available in the demo. Cost: the container is not a production-like run, and Development exposes the API reference pages.
2. **A configuration switch in `Program.cs`** (for example `Database:MigrateOnStartup`). Production-like environment in compose. Cost: a `Program.cs` change that conflicts with Phase 6, and a second way to turn on startup migrations that could be left on in production.
3. **A separate migration container** running an EF Core bundle (`dotnet ef migrations bundle`) before the API starts (`depends_on: condition: service_completed_successfully`). Closest to production. Cost: more moving parts, a slower build, and the bundle tool version must match `dotnet-tools.json`.

## Decision

Option 1.

- **Startup migration runs only in `Development`.** That covers local `dotnet run` and local compose. There is one rule and no switch to leave on by mistake.
- **Production applies migrations as a separate step**: an EF Core migration bundle, built with the image and run once as a deployment job, with `ASPNETCORE_ENVIRONMENT` set explicitly, before the new version starts. This is not built in this project; it is the documented path.
- **Connection string:** `ConnectionStrings:MortgageApplications`. `appsettings.json` holds a local-file default. Compose overrides it with the environment variable `ConnectionStrings__MortgageApplications` (`/app/data/mortgage-applications.db` on the named volume `data`). Production supplies it the same way, from the environment. No secret is involved: SQLite has no credentials.
- **Container:** multi-stage `sdk:10.0` → `aspnet:10.0` (Ubuntu 24.04), non-root `USER $APP_UID`, port 8080, `/app/data` owned by that user. Compose sets `init: true`, because dotnet as PID 1 hung at full CPU after an unhandled startup exception (check C14).

## Consequences

- **Security, accepted until Phase 7:** in compose, Scalar, the OpenAPI document and every endpoint are reachable without authentication. The port is bound to `127.0.0.1` only. That bind must not be widened (for example to `0.0.0.0` or a bare `8080:8080`) before Phase 7 adds authentication.
- The compose run is not a production-like configuration. A production-like smoke test would need option 3 or a real deployment.
- If a future change needs a production-like container locally, option 3 is the upgrade path, with no change to the startup rule.
- **Run the image with an init process.** The fix for the PID 1 hang is `init: true` in compose, not in the image. Any other runner must do the same (`docker run --init`, or the orchestrator's equivalent); otherwise a failed startup hangs instead of exiting. An init inside the image (for example tini) would add an OS package and needs approval (PR #7 `/code-review` finding 1).
- Base images are referenced by tag, not digest (P4-D4). Digest pinning is revisited in Phase 8.
- The comments in `Program.cs` and `DatabaseMigration.cs` that say "ADR in Phase 4" point here. They are updated to "ADR 0002" after Phase 6 merges, because Phase 6 owns `Program.cs`.
