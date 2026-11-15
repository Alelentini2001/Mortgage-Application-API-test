# Phase 04: Docker

Status: **In progress: plan and tests approved (2026-11-15), implementation next** · Branch: `phase-04-docker` (worktree, parallel with Phase 6) · PR: — · Started: 2026-11-14 · Finished: —

## Goal

`git clone && docker compose up --build` runs the API in a clean folder (master prompt B5, Phase 4; R32, R46).
- Multi-stage `Dockerfile` (`sdk:10.0` → `aspnet:10.0`), non-root `USER $APP_UID`, port 8080, no secrets baked in (`.claude/rules/infra.md`).
- `compose.yaml` with the API and a named volume for the SQLite file.
- Migrations on startup decided and written down in an ADR (carried from Phase 3: security I1, "connection string location and the startup-migration ADR").
- CI builds the image (and smoke-tests it, if P4-D3 option A is approved). CI still needs no secret.

Not in scope: the Aspire dashboard and any OpenTelemetry wiring (see P4-D2), README text (Phase 10), image digest pinning and image scanning (Phase 8).

## Parallel safety with Phase 6 (master prompt A10, B5)

Phase 6 changes `Program.cs`, `ApplicationEndpoints.cs`, Core, Infrastructure and the tests. This plan touches none of them **if P4-D1 option A is approved**. Files this phase may touch:

| File | Change |
|---|---|
| `Dockerfile` (new) | multi-stage build |
| `compose.yaml` (new) | api service + `data` volume |
| `.dockerignore` | only if the build context needs it (it already excludes `bin/`, `obj/`, `*.db`, `tests/`, `docs/`, `.env*`, `*.pem`) |
| `.github/workflows/ci.yml` | new `docker` job |
| `docs/adr/0002-database-migrations-on-startup.md` (new) | ADR number 0002 is reserved for this phase; Phase 6 takes 0003 |
| `docs/progress/phase-04-docker.md` | this file |

Shared evidence files (`docs/PROGRESS.md`, `docs/ai/PROMPTS.md`, `DESIGN-JOURNAL.md`, `ORCHESTRATION.md`) are **not** edited in the worktree. Prompts are logged in this file during the phase and copied into `PROMPTS.md` on `main` after the merge, so the two branches cannot conflict there.

## Plan (approved at Checkpoint A)

1. **Branch** `phase-04-docker` in its own worktree (`parallel-implementer`), from `main` at `96f8720` or later.
2. **Docs check** on learn.microsoft.com, links recorded below: the .NET container images (`APP_UID`, default port 8080, `ASPNETCORE_HTTP_PORTS`), "Containerize a .NET app" multi-stage pattern, `dotnet publish` options for containers, EF Core "Applying migrations" (runtime `Migrate()` caveats and migration bundles), configuration from environment variables (`ConnectionStrings__MortgageApplications`), environments (`ASPNETCORE_ENVIRONMENT`). Docker docs for compose named volumes.
3. **Test design.** `test-designer` proposes checks for the container path (no .NET test project change expected). Draft: image builds; container starts as a non-root user; `GET /health` returns 200 on port 8080; `POST` then `GET /api/applications/{id}` works; data survives `docker compose down` + `up` (volume); no `.db`, `.env` or `*.pem` inside the image. **Alessandro picks the cases.**
4. **Build**, small commits:
   - `Dockerfile`: restore with only the `*.csproj`, `Directory.*.props`, `global.json` and `.slnx` copied first (layer cache), then publish `src/MortgageApplications.Api` in Release; runtime stage `mcr.microsoft.com/dotnet/aspnet:10.0`, create `/app/data` owned by `$APP_UID`, `USER $APP_UID`, `EXPOSE 8080`.
   - `compose.yaml`: service `api`, build from `.`, port `8080:8080`, `ASPNETCORE_ENVIRONMENT` per P4-D1, `ConnectionStrings__MortgageApplications=Data Source=/app/data/mortgage-applications.db`, named volume `data:/app/data`.
   - CI `docker` job per P4-D3: `permissions: contents: read`, pinned `actions/checkout`, `docker build`, and (option A) run the container and poll `/health` with a timeout.
   - ADR 0002 (via `/adr`, which Alessandro runs, or written by the agent if Alessandro says so).
5. **Verify.** `./scripts/verify.sh` (full) still passes; run the approved container checks locally with `docker compose up --build` in a fresh clone in the scratchpad; record the output and the time.
6. **Security gate.** `security-auditor` on the diff (Dockerfile: root user, secrets, `COPY . .` scope; workflow: permissions, pinning). Gitleaks + Semgrep as before.
7. **Review gate.** `dotnet-reviewer` on `git diff main...HEAD`, then `spec-checker` (R32 should move to met; R46 partial).
8. **Evidence.** This file; prompts logged here and copied to `PROMPTS.md` after merge.
9. **PR**, local `/code-review` + `/security-review`, one PR comment, CI. **Checkpoint B.** Phase 4 merges first; Phase 6 then rebases and re-runs its gates.

## Decisions for Alessandro (2-3 options · trade-offs · recommendation)

**P4-D1. How the container database gets its schema.** Today `Program.cs` migrates on startup only when the environment is Development.
- **A. Compose runs the API as `Development`** (recommended). No code change, so no conflict with Phase 6; Scalar and the OpenAPI document are available in the demo; Phase 7's dev token endpoint ("Development/Docker only") fits the same rule. Cost: the container is not a production-like run. The ADR says: startup migration is allowed only in Development (local `dotnet run` and local compose); production applies migrations as a separate step (an EF Core migration bundle) before the new version starts.
- **B. A config switch** (`Database:MigrateOnStartup`, false by default, true in compose) with the container in `Production`. More realistic, but it edits `Program.cs`, which Phase 6 also edits: Phase 4 and 6 would have to run one after the other (master prompt B5), or accept a merge conflict.
- **C. A separate migration container** running an EF Core bundle (`dotnet ef migrations bundle`) before the API starts (`depends_on: condition: service_completed_successfully`). Closest to production; more moving parts, a slower build, and the bundle tool version must match `dotnet-tools.json` (10.0.15). Also no `Program.cs` change.

**P4-D2. The Aspire dashboard in compose.**
- **A. Add it in Phase 5** (recommended), together with the OpenTelemetry exporter. Until Phase 5 the API sends no telemetry, so the dashboard would be empty.
- **B. Add it now** as an empty service, so Phase 5 only sets the exporter endpoint. Adds a container that shows nothing in this phase.

**P4-D3. What CI does with the image.**
- **A. Build, then run it and poll `GET /health`** with a 60 s timeout (recommended). Proves the image starts, not only that it compiles; about 1-2 minutes more CI.
- **B. Build only** (`docker build`), as `.claude/rules/infra.md` says at minimum. Faster; a startup failure would only show in the fresh-clone run.

**P4-D4. Base image references.**
- **A. Tags** (`sdk:10.0`, `aspnet:10.0`) now, digest pinning revisited in Phase 8 (recommended). Readable, picks up patched images.
- **B. Digest-pinned** now. Reproducible, but needs a manual bump for every security patch.

## Decisions (option chosen · alternatives · reason · decided by) → ADR links

Alessandro approved the plan with "Proceed" on 2026-11-15, after the agent recommended option A for each decision. All four take the recommended option.

| ID | Chosen | Alternatives | Reason | Decided by |
|---|---|---|---|---|
| P4-D1 | A: compose runs the API as `Development`; startup migration stays Development-only; production uses a migration bundle (ADR 0002) | B config switch in `Program.cs`; C separate migration container | No `Program.cs` change, so Phase 4 and 6 can run in parallel; one simple rule for startup migrations | Alessandro |
| P4-D2 | A: Aspire dashboard added in Phase 5 | B empty service now | No telemetry exists until Phase 5 | Alessandro |
| P4-D3 | A: CI builds the image, runs it and polls `GET /health` (60 s timeout) | B build only | Proves the image starts, not only that it builds | Alessandro |
| P4-D4 | A: image tags now, digest pinning revisited in Phase 8 | B digest pins now | Readable; picks up patched images | Alessandro |

## Docs verified (links)

Checked by the lead agent on 2026-11-15, before implementation.

- [Run an ASP.NET Core app in Docker containers](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/docker/building-net-docker-images?view=aspnetcore-10.0): the .NET 10 sample is multi-stage (`sdk:10.0` → `aspnet:10.0`), copies the project files and restores as a separate layer for the build cache, then `dotnet publish -c release -o /app --no-restore`; it maps host ports to container port 8080.
- [Breaking change: default ASP.NET Core port changed from 80 to 8080](https://learn.microsoft.com/en-us/dotnet/core/compatibility/containers/8.0/aspnet-port): since .NET 8 the images listen on 8080; `ASPNETCORE_HTTP_PORTS` takes a semicolon-separated list of ports. The reason given is running as a non-root user, which needs a non-privileged port.
- `APP_UID` is **not documented on Learn** (searched the container-images, Docker tutorial and port pages). Source used instead: the .NET team blog, [Secure your .NET cloud apps with rootless Linux Containers](https://devblogs.microsoft.com/dotnet/securing-containers-with-rootless/): the images define a non-root `app` user, `$APP_UID` is its UID (1654), and `USER $APP_UID` is called a best practice.
- [Containerize an app with Docker](https://learn.microsoft.com/en-us/dotnet/core/docker/build-container): calls a digest after the image tag a best practice. P4-D4 chose tags for now; this is recorded as an open issue for Phase 8.
- [Applying migrations (EF Core)](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying): for containers, "generate the bundle during the build and run it as a one-shot deployment job", and do not make every replica run migrations from its entrypoint. Runtime `MigrateAsync` is acceptable for apps "that can tolerate startup migration behavior"; EF Core 9+ takes a migration lock, but on SQLite an abandoned lock (process killed mid-migration) can block later migrations. Set `ASPNETCORE_ENVIRONMENT` explicitly when building and running a bundle. This is the basis for ADR 0002.
- Configuration from environment variables (`ConnectionStrings__MortgageApplications`, double underscore for `:`): already used in Phase 3 tests; not re-checked.

## Orchestration used

## Prompts

Logged here during the parallel run and copied to `docs/ai/PROMPTS.md` on `main` after the merge (see "Parallel safety"). The human prompts below cover both Phase 4 and Phase 6; the Phase 6 file links here for them.

### P04-01 · human→agent · 2026-11-14
- **Prompt (verbatim):**
  > continue with next steps
- **Purpose:** start the next step from `docs/PROGRESS.md` ("Resume here": Phase 4 and Phase 6 in parallel).
- **Outcome:** the agent read the progress files and the code, checked which files each phase would touch, and wrote both plans with their decisions (Checkpoint A).

### P04-02 · human→agent · 2026-11-15
- **Prompt (verbatim):**
  > Try again
- **Purpose:** resume after the previous turn stopped before the Phase 6 plan was written.
- **Outcome:** the Phase 6 plan was written; both plans presented at Checkpoint A.

### P04-03 · human→agent · 2026-11-15
- **Prompt (verbatim):**
  > Proceed
- **Purpose:** Checkpoint A approval of both plans with the recommended options (P4-D1 to P4-D4, P6-D1 to P6-D4).
- **Outcome:** decisions recorded; docs check; `test-designer` run for both phases (P04-04, P06-01).

### P04-04 · agent→subagent (`test-designer`) · 2026-11-15
- **Prompt (verbatim):**
  ```
  Phase 4 (Docker) of a .NET 10 mortgage-application API (Minimal APIs + EF Core SQLite). Propose test cases and checks for the approved plan; do not write code.

  Read first: docs/progress/phase-04-docker.md (goal, plan, approved decisions P4-D1 to P4-D4), .claude/rules/infra.md, .claude/rules/security.md, .claude/rules/tests.md, tests/CLAUDE.md, .dockerignore, .github/workflows/ci.yml, src/MortgageApplications.Api/Program.cs, src/MortgageApplications.Api/appsettings.json, src/MortgageApplications.Infrastructure/DatabaseMigration.cs, and tests/MortgageApplications.IntegrationTests/StartupTests.cs (what startup behaviour is already covered).

  What will be built (not written yet):
  - A multi-stage Dockerfile (mcr.microsoft.com/dotnet/sdk:10.0 → aspnet:10.0, image tags not digests), restore layer from the csproj/props/global.json/slnx files, publish the Api project in Release, /app/data created and owned by $APP_UID, USER $APP_UID, EXPOSE 8080.
  - compose.yaml: one service "api" built from ".", port 8080:8080, ASPNETCORE_ENVIRONMENT=Development (so the existing Development-only startup migration runs, decision P4-D1), ConnectionStrings__MortgageApplications=Data Source=/app/data/mortgage-applications.db, named volume data:/app/data. No Aspire dashboard (Phase 5).
  - A new CI job "docker": permissions contents: read, pinned actions/checkout, docker build, run the container, poll GET /health with a 60 s timeout, fail the job otherwise, always stop the container.
  - No .NET code change is planned. The checks are mostly shell/CI checks, not xUnit tests.

  Cover at least: the image builds from a clean clone; the container runs as a non-root user (uid check); /health returns 200 on 8080; a POST then GET /api/applications/{id} round trip works inside the container (schema exists, so the startup migration ran); data survives docker compose down + up but not down -v; no *.db, .env*, *.pem, tests/ or docs/ in the image; the CI job fails if the container never becomes healthy and cleans up either way; the published app does not need the SDK at runtime. Also say whether any xUnit test is worth adding (for example a test that the startup migration does not run outside Development, if not already covered) and why.

  Use the agent's output format. Mark each case must or nice, keep the list tight (no redundant cases), say for each where it runs (local fresh-clone run, CI job, or xUnit), and list any ambiguities in the plan you need Alessandro to settle.
  ```
- **Purpose:** propose Phase 4 checks before anything is built.
- **Approved:** see P04-05.
- **Outcome:** 16 container/CI checks (C1–C16; C17 folded into ambiguity 2), 1 optional xUnit test (X1) and 6 ambiguities. The lead agent checked ambiguity 1 against the code (`MortgageApplications.slnx` lists both test projects; `.dockerignore` excludes `tests/`). Recommended selection and answers are in "Tests", "Proposal".

### P04-05 · human→agent · 2026-11-15
- **Prompt (verbatim):**
  > Ok add it and proceed (yes)
- **Purpose:** approval of the recommended test selections and answers for Phase 4 and Phase 6; "add it" is read as adding `failedAt` to `AssessmentResponse` (Phase 6, Q11), the last item the agent recommended adding.
- **Outcome:** both phase branches created with their plan files; `parallel-implementer` started for each (P04-06, P06-02).

## What was built (files)

## Tests

### Proposal (test-designer P04-04, approved by Alessandro P04-05: recommended selection and answers)

Where: LOCAL = fresh clone in the scratchpad, then `docker compose up --build`; CI = the new `docker` job.

Recommended selection (all "must" rows plus C8):

| ID | Where | Check |
|---|---|---|
| C1 | LOCAL | Fresh clone builds and starts with `docker compose up --build -d`; wall time recorded |
| C2 | LOCAL + CI | `docker inspect` user is not empty or root; `id -u` in the container is the `APP_UID` value, not 0 |
| C3 | LOCAL + CI | `GET /health` on 8080 → 200 `Healthy` |
| C4 | LOCAL + CI | `POST /api/applications` on an empty volume → 201 + `Location` (proves the startup migration ran) |
| C5 | LOCAL + CI | `GET` of that ID → 200 with the same values (proves `/app/data` is writable by the non-root user) |
| C6 | LOCAL | `compose down` then `up` → the application is still there (named volume) |
| C7 | LOCAL | `compose down -v` then `up` → old ID is 404 (not 500), new POST is 201 |
| C8 | LOCAL | `/app/data` is owned by the app UID (names the cause if C4 fails) |
| C9 | LOCAL | Planted untracked `*.db`, `.env`, `.env.local`, `*.pem`, `docs/`, `tests/` files are not in the image; `/app` has no `tests/`, `docs/`, `.git/`, `.claude/` |
| C10 | LOCAL | `docker history` and the image `Env` hold no connection string, key or token |
| C11 | LOCAL | `dotnet --list-sdks` in the image lists nothing (runtime only); image size recorded |
| C13 | review | Workflow: job-level `permissions: contents: read`, `actions/checkout` pinned by SHA with `persist-credentials: false`, `timeout-minutes`, no secrets, no push or login |
| C14 | LOCAL + one deliberate CI break | A container that never becomes healthy (invalid `Affordability__AssessmentRate`) makes the poll step fail within about 60 s and print the logs; it never exits 0 |
| C16 | CI | A final `if: always()` step removes the containers and the volume |

Not recommended now: C12 and C15 (nice, small value). **X1** (an xUnit test that the startup migration does not run outside Development) is worth having but should wait until both phases are merged: Phase 6 adds a worker that reads the database at startup, so a test host on an unmigrated database written now would break after the rebase. Recorded under "Open issues".

Ambiguities, with the recommended answer:
1. **Restore layer.** The `.slnx` lists the test projects, which `.dockerignore` excludes, so restoring the solution would fail. → Restore and publish `src/MortgageApplications.Api/MortgageApplications.Api.csproj` only, after copying the three `src` `.csproj` files, `Directory.Build.props`, `Directory.Packages.props` and `global.json`.
2. **`/health` does not touch the database**, so a `/health`-only CI poll passes with a broken schema or an unwritable volume. → CI also runs C4 and C5 (POST then GET). This extends P4-D3 a little and costs a few seconds.
3. **CI must use the same environment as compose.** → CI runs `docker compose up -d --build`, so compose is the one source of truth.
4. **Port binding.** `8080:8080` listens on all interfaces, and until Phase 7 there is no authentication. → Bind to `127.0.0.1:8080:8080`; the README (Phase 10) says how to change it.
5. A valid POST body for C4 is taken from the existing integration tests (`Http.cs`).
6. The runtime image is the default Debian `aspnet:10.0` (it has `sh` and `id`, which C2 and C9 use). Chiseled images are a Phase 8 topic.

## Security gate

## Review gate

## AI output corrected or rejected (REAL cases only)

## Optimisations considered

## Open issues

- Image digest pinning: Learn calls it a best practice; P4-D4 chose tags for now. Revisit in Phase 8.
- X1 (no startup migration outside Development, as an xUnit test): deferred until Phase 4 and Phase 6 are both merged.

## Time spent (approx.)

## Human review (Alessandro writes this; the agent leaves it empty)
