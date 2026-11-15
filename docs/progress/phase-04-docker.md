# Phase 04: Docker

Status: **In progress: implementation, gates, LOCAL container checks and ADR 0002 draft done; spec check and PR next** · Branch: `phase-04-docker` (worktree, parallel with Phase 6) · PR: — · Started: 2026-11-14 · Finished: —

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

### P04-06 · agent→subagent (`parallel-implementer`) · 2026-11-15
- **Prompt (verbatim):**
  ```
  Implement Phase 4 (Docker) of this .NET 10 mortgage-application API, exactly as approved.

  Branch: switch to the existing branch phase-04-docker (it holds one commit with the approved plan). Do not create another branch. Never push.

  Scope: docs/progress/phase-04-docker.md. The sections "Parallel safety with Phase 6", "Plan (approved at Checkpoint A)", "Decisions" (P4-D1 to P4-D4), "Docs verified" and "Tests > Proposal" (the recommended checks C1-C11, C13, C14, C16 and the answers to ambiguities 1-6, all approved) are your scope. Read AGENTS.md, CLAUDE.md, .claude/rules/infra.md, .claude/rules/security.md first.

  Key points from the approved answers (the phase file is the source of truth if anything differs):
  - Dockerfile: sdk:10.0 build stage, aspnet:10.0 runtime (tags, not digests). Restore and publish src/MortgageApplications.Api/MortgageApplications.Api.csproj only, after copying the three src .csproj files, Directory.Build.props, Directory.Packages.props and global.json (the .slnx lists test projects that .dockerignore excludes). Runtime: create /app/data owned by $APP_UID, USER $APP_UID, EXPOSE 8080. No secrets, no ENV with connection strings.
  - compose.yaml: service api, build ".", ports "127.0.0.1:8080:8080", ASPNETCORE_ENVIRONMENT=Development, ConnectionStrings__MortgageApplications=Data Source=/app/data/mortgage-applications.db, named volume data:/app/data. No Aspire dashboard.
  - CI: new job "docker" in .github/workflows/ci.yml: job-level permissions contents: read, the same pinned actions/checkout SHA as the existing job with persist-credentials: false, timeout-minutes, `docker compose up -d --build`, a poll script on GET /health (60 s timeout, fails non-zero and prints `docker compose logs` on timeout or if the container exits), then the C4/C5 POST + GET round trip, and a final `if: always()` step running `docker compose down -v`. No secrets, no login, no push.
  - No .NET code changes. Do not edit src/, tests/, Program.cs, docs/PROGRESS.md, docs/ai/*, or any ADR file (ADR 0002 is written later by the lead agent). Only the phase file under docs/.

  Process:
  1. Write the files in small conventional commits (scopes infra, ci). End each commit message with: Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
  2. Run ./scripts/verify.sh (full) and keep the final output (it must still pass).
  3. Check whether Docker is reachable with `docker version`. If it is: run the approved LOCAL checks in a fresh clone of this branch in a new empty directory under /private/tmp/claude-501/ (clone from this repository's path, check out phase-04-docker), and record each check's command and result (C1-C11, C14, with the C9 planted files created only in that clone). Clean up containers, images you built and volumes afterwards. Never print request bodies containing amounts into the phase file beyond what the check needs. If Docker is NOT reachable: do not try to start it, do not wait for it; mark every LOCAL check as "not run: Docker daemon not available" and continue.
  4. Do the C13 static review of the workflow yourself and record it.
  5. In the phase file, fill "What was built (files)", "Tests" > a new "Results" subsection (one row per check: run / not run, command, result) and "Open issues". Commit that.

  Report: files changed, commits, trimmed verify output, the result of each check (or "not run" and why), every place you deviated from the phase file and why. If the plan is ambiguous or a step fails three times, stop and report instead of guessing.
  ```
- **Purpose:** implement the approved Phase 4 plan in its own worktree.
- **Outcome:** 4 commits (`7e83532`, `aeb932d`, `ee9c8ef`, `ce46e8f`); full verify passed; LOCAL container checks marked not run (no Docker daemon). See "Results".

### P04-07 · agent→subagent (`security-auditor`) · 2026-11-15
- **Prompt (verbatim):**
  ```
  Security gate for Phase 4 (Docker) of a .NET 10 mortgage-application API. Review only the branch diff: run `git diff main...phase-04-docker` from the repository root (the branch is checked out in another worktree; do not switch branches here). Files: Dockerfile, compose.yaml, .github/workflows/ci.yml (new `docker` job), docs/progress/phase-04-docker.md.

  Read first: .claude/rules/security.md, .claude/rules/infra.md, .dockerignore, docs/progress/phase-04-docker.md (plan, decisions P4-D1 to P4-D4, approved answers to ambiguities 1-6).

  Check at least: the image runs as non-root (`USER $APP_UID` after every root step) and nothing writes outside /app/data at run time; no secrets, connection strings or keys in any layer, ENV, ARG or build context (`COPY src/ src/` scope against .dockerignore: *.db, .env*, *.pem, bin/obj); compose binds to 127.0.0.1 only and the ADR-to-be says Development in compose exposes Scalar/OpenAPI without auth until Phase 7; the CI job: least-privilege permissions, pinned actions, persist-credentials false, no `${{ }}` expressions inside `run:` blocks (script injection), no secrets, no push/login, cleanup with `if: always()`, request bodies with amounts not printed to logs; supply chain of base images (tags not digests, P4-D4 accepted for now: say whether that acceptance is reasonable or should be raised now). Run Gitleaks on the branch commits (`gitleaks git --log-opts="main..phase-04-docker"` or the closest available form) and Semgrep on the changed files if the tools are installed; say which ran and which were not available. Do not install anything.

  Use your output format: each finding with severity, file:line, evidence, real or false positive, and a proposed fix or justification. Do not edit files.
  ```
- **Purpose:** security gate on the Phase 4 diff.
- **Outcome:** pass, 11 rows (S1–S11). See "Security gate".

### P04-08 · agent→subagent (`dotnet-reviewer`) · 2026-11-15
- **Prompt (verbatim):**
  ```
  Fresh-context review of Phase 4 (Docker) of a .NET 10 mortgage-application API. Review only the branch diff: run `git diff main...phase-04-docker` from the repository root (the branch is checked out in another worktree; do not switch branches here). Files: Dockerfile, compose.yaml, .github/workflows/ci.yml (new `docker` job), docs/progress/phase-04-docker.md.

  Read first: AGENTS.md, .claude/rules/infra.md, docs/progress/phase-04-docker.md (goal, plan, decisions P4-D1 to P4-D4, "Docs verified", approved checks and answers), src/MortgageApplications.Api/Program.cs, src/MortgageApplications.Api/appsettings.json, src/MortgageApplications.Infrastructure/DatabaseMigration.cs, global.json, Directory.Build.props, .dockerignore.

  Flag only correctness, security and requirement gaps, for example: will `dotnet restore` of the Api csproj alone succeed with central package management and Directory.Build.props as copied (anything else the build reads, such as a NuGet.config, analyzer config or .editorconfig with warnings-as-errors, missing from the build context so the image build behaves differently from verify.sh?); does `chown "$APP_UID"` expand in that RUN; does the named volume inherit /app/data ownership on first use; does the compose environment make the app migrate on startup and find the database; does the CI poll script really fail on every failure path (curl errors, container exit, timeout) and never exit 0 early; is the POST body valid for the API's strict JSON and validation rules; does the phase file's "Results" section claim anything that was not run (the Docker daemon was not available, so the LOCAL checks must be marked not run). Report each finding with severity, file:line, evidence and a proposed fix. Do not edit files.
  ```
- **Purpose:** fresh-context review of the Phase 4 diff.
- **Outcome:** 9 findings (R1–R9), none critical or high. See "Review gate".

### P04-09 · human→agent · 2026-11-15
- **Prompt (verbatim):**
  > I hit my usage limit while you were working, but it has reset now. Please continue from where you left off.
- **Purpose:** resume after the usage limit stopped the Phase 6 fix run (P06-06).
- **Outcome:** the fix run was restarted (P06-07); the lead agent checked its commits, re-ran the full verify (376 tests) and presented seven open decisions plus the C14 CI-break question.

### P04-10 · human→agent · 2026-11-15
- **Prompt (verbatim):**
  > Proceed, also I have a question, does this also mind when two users do the same at the same time? Like security stuff like these...
- **Purpose:** approve the recommended answer to all seven open decisions (Phase 6: accept S1 until Phase 8, fix the clock (R4), retry the startup sweep (R3), record the missing concurrency token in ADR 0003, accept the worker-only methods with comments, keep the Q5 catch; both phases: the agent drafts ADR 0002 and ADR 0003) and the deliberate CI break for C14; plus a question on concurrent users.
- **Outcome:** R3 and R4 fixed on `phase-06-background`; ADR 0002 and ADR 0003 drafted; the question answered in chat.

## What was built (files)

Written by `parallel-implementer` on 2026-11-15 (P04-06). No .NET code, test project, `.dockerignore` or ADR file was changed; ADR 0002 is written later by the lead agent.

| File | What |
|---|---|
| `Dockerfile` (new) | `sdk:10.0` build stage: copies `global.json`, `Directory.Build.props`, `Directory.Packages.props` and the three `src` `.csproj` files, restores `src/MortgageApplications.Api/MortgageApplications.Api.csproj` only (ambiguity 1), then copies `src/` and runs `dotnet publish -c Release -o /app/publish --no-restore`. `aspnet:10.0` runtime stage: `mkdir -p /app/data && chown "$APP_UID" /app/data`, copies the publish output, `USER $APP_UID`, `EXPOSE 8080`, `ENTRYPOINT ["dotnet", "MortgageApplications.Api.dll"]`. No `ENV`, no secrets. |
| `compose.yaml` (new) | Service `api`, `build: .`, port `127.0.0.1:8080:8080` (ambiguity 4), `ASPNETCORE_ENVIRONMENT: Development` (P4-D1), `ConnectionStrings__MortgageApplications: Data Source=/app/data/mortgage-applications.db`, named volume `data:/app/data`. No Aspire dashboard (P4-D2). |
| `.github/workflows/ci.yml` | New `docker` job (P4-D3, ambiguities 2 and 3): job-level `permissions: contents: read`, `timeout-minutes: 15`, the same pinned `actions/checkout` SHA as `verify` with `persist-credentials: false`; `docker compose up -d --build`; a poll of `GET /health` (60 s, fails and prints `docker compose logs` on timeout or if the `api` container is not running); `POST /api/applications` with the golden E1 body from `Http.cs` (ambiguity 5), expects 201 and a `Location` header, then `GET` of that path expects 200 and a body equal to the POST response (compared with `jq -S`); final `if: always()` step `docker compose down -v`. Bodies are written to files and never echoed. |
| `docs/progress/phase-04-docker.md` | This section, "Results" and "Open issues". |
| `docs/adr/0002-container-runtime-and-startup-migrations.md` (new, lead agent) | Draft ADR: startup migration only in Development, production uses a migration bundle, connection string from the environment, compose exposes Scalar, OpenAPI and every endpoint without auth until Phase 7 behind the `127.0.0.1` bind (security S3, review R1). |

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
6. The runtime image is the default `aspnet:10.0` (it has `sh` and `id`, which C2 and C9 use). Chiseled images are a Phase 8 topic. Correction (review gate, R8): since .NET 10 the default tags are Ubuntu 24.04, not Debian ([Learn: default .NET container tags now use Ubuntu](https://learn.microsoft.com/en-us/dotnet/core/compatibility/containers/10.0/default-images-use-ubuntu)); this answer first said Debian.

### Results (parallel-implementer, 2026-11-15)

The Docker daemon was not available on the implementer's machine: `docker version` printed the client (29.3.1, `desktop-linux` context) and then `failed to connect to the docker API at unix:///Users/ale_ssd/.docker/run/docker.sock ... no such file or directory`. As instructed, the daemon was not started and no LOCAL check was run. The CI rows run for the first time when the PR's CI runs.

| ID | Run? | Command | Result |
|---|---|---|---|
| C1 | not run | `docker compose up --build -d` in a fresh clone | Not run: Docker daemon not available |
| C2 | not run (LOCAL); CI pending | `docker inspect -f '{{.Config.User}}'`, `docker compose exec api id -u` | Not run locally: Docker daemon not available. The CI job does not check the UID directly; it only proves the non-root container can write `/app/data` (C5). |
| C3 | not run (LOCAL); CI pending | `curl http://127.0.0.1:8080/health` | Not run locally: Docker daemon not available. CI step "Wait for GET /health (60 s)" covers it once CI runs. |
| C4 | not run (LOCAL); CI pending | `POST /api/applications` with the E1 body | Not run locally: Docker daemon not available. CI step "POST then GET an application" covers it. |
| C5 | not run (LOCAL); CI pending | `GET` of the returned `Location` | Not run locally: Docker daemon not available. Same CI step. |
| C6 | not run | `docker compose down` then `up -d`, `GET` the old ID | Not run: Docker daemon not available |
| C7 | not run | `docker compose down -v` then `up -d`, `GET` the old ID, new `POST` | Not run: Docker daemon not available |
| C8 | not run | `docker compose exec api stat -c '%u' /app/data` | Not run: Docker daemon not available |
| C9 | not run | planted `*.db`, `.env`, `.env.local`, `*.pem`, `docs/`, `tests/` files in the clone, then `find` in the image | Not run: Docker daemon not available (no planted files were created) |
| C10 | not run | `docker history --no-trunc`, `docker inspect -f '{{.Config.Env}}'` | Not run: Docker daemon not available |
| C11 | not run | `docker compose exec api dotnet --list-sdks`, `docker image ls` | Not run: Docker daemon not available |
| C13 | run (review) | static review of `.github/workflows/ci.yml` | Pass, see below |
| C14 | not run | compose with `Affordability__AssessmentRate` set to an invalid value, then the poll step | Not run locally: Docker daemon not available. The "one deliberate CI break" part is not done either (it needs a pushed branch, and pushing is out of the implementer's scope). |
| C16 | CI pending | final `if: always()` step `docker compose down -v` | Present in the workflow (C13); runs for the first time with the PR's CI. |

**C13 static review** of the `docker` job (`grep -n -E 'secrets|login|push|GITHUB_TOKEN|\$\{\{|uses:|permissions|timeout|persist|always' .github/workflows/ci.yml` and a read of the file):
- Job-level `permissions: contents: read` (lines 36-37), on top of the workflow-level `contents: read`.
- `actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1 # v7.0.1`, the same SHA as the `verify` job, with `persist-credentials: false`. No other action is used.
- `timeout-minutes: 15`.
- No `secrets.` reference, no `GITHUB_TOKEN`, no `docker login`, no `docker push`. The only `push` match is the workflow trigger (line 4).
- No `${{ }}` expression inside any `run:` block, so no script injection from event data.
- The final step has `if: always()` and runs `docker compose down -v`.
- Request and response bodies go to files (`created.json`, `fetched.json`) and are not printed; on failure only status codes, the `Location` path and `docker compose logs` are printed. The app does not log amounts (AGENTS.md rule), so the logs hold no money values.
- The `docker` job does not depend on `verify`; the two run in parallel.

**Supporting checks that are not container checks** (done because Docker was not available; they test the scripts and the build commands, not the image):
- The workflow parses as YAML (`ruby -ryaml`: jobs `verify` and `docker`, steps as listed above) and every `run:` block passes `bash -n`.
- The two CI scripts were run with `bash -e` against the API started with `dotnet run --no-launch-profile` on `http://127.0.0.1:8080` (`ASPNETCORE_ENVIRONMENT=Development`, a scratchpad SQLite file). Poll: `GET /health returned 200.`, exit 0. Round trip: `POST returned 201 and GET /api/applications/<id> returned the same application.`, exit 0. The `docker compose ps` / `logs` branches were not reached.
- The Dockerfile's restore and publish commands were run on the host in a copy of the build context (only `global.json`, `Directory.Build.props`, `Directory.Packages.props` and `src/` without `bin/` and `obj/`): `dotnet restore src/MortgageApplications.Api/MortgageApplications.Api.csproj` restored the three projects, and `dotnet publish ... -c Release -o publish --no-restore` succeeded (output includes `MortgageApplications.Api.dll`, `appsettings*.json` and the SQLite native `runtimes/`). This supports ambiguity 1 but is not the Linux build.
- `./scripts/verify.sh` (full): build 0 warnings, 0 errors; tests 347 passed, 0 failed; hook tests 91 passed; format check passed; `VERIFY PASSED (full)`.

### Results (lead agent, with Docker, 2026-11-15)

Docker Desktop came up after the implementer had finished (server 29.3.1, arm64). The lead agent ran the LOCAL checks in a fresh clone of `phase-04-docker` at `274365b` in the scratchpad (`git clone --branch phase-04-docker <repo> p4-clone`), with compose project name `p4check`.

| ID | Result | Evidence |
|---|---|---|
| C1 | Pass | `docker compose up --build -d` exit 0 in 44 s wall time (base images already pulled; 0 cached build steps) |
| C2 | Pass | `Config.User=1654`; `id -u` → `1654`; `$APP_UID` → `1654`; `whoami` → `app` |
| C3 | Pass | `GET /health` → `"Healthy" 200` |
| C4 | Pass | `POST /api/applications` (E1 body) → 201, `Location: /api/applications/<id>` |
| C5 | Pass | `GET` of that path → 200, body equal to the POST body (`jq` slurp compare) |
| C6 | Pass | `down`, `up -d`, `GET` the same ID → 200, same body |
| C7 | Pass | `down -v`, `up -d`, `GET` the old ID → 404; new `POST` → 201 |
| C8 | Pass | `/app/data` owner `1654 app 755`; it holds `mortgage-applications.db`, `-shm`, `-wal` |
| C9 | Pass | Planted untracked files at the root (`planted-root.db`, `.env`, `.env.local`, `planted-root.pem`, `docs/planted.md`, `tests/planted.txt`) and nested under `src/MortgageApplications.Api/` (`planted.db`, `.env`, `.env.local`, `planted.pem`, `secrets/planted.txt`, `PLANTED.md`). `find` for planted names, `.env*`, `*.pem`, `*.db`, `secrets` found nothing in the final image (`/app`) **or** the build stage (`docker build --target build`, `/src`), so the recursive `.dockerignore` patterns (S2) work. `/app` has no `tests`, `docs`, `.git`, `.claude`. `.editorconfig` is present in the build stage (R5) |
| C10 | Pass | Image `Env` holds only `PATH`, `APP_UID=1654`, `ASPNETCORE_HTTP_PORTS=8080`, `DOTNET_RUNNING_IN_CONTAINER`, `DOTNET_VERSION=10.0.15`, `ASPNET_VERSION=10.0.15`; `docker history --no-trunc` has no secret, token, key or connection-string match |
| C11 | Pass | `dotnet --list-sdks` → empty; `/usr/share/dotnet/sdk` does not exist; image 305 MB; OS `Ubuntu 24.04.5 LTS` (confirms R8) |
| C14 | **Failed first, then fixed** | With `Affordability__AssessmentRate=0.987` (compose override), the extracted CI poll script exited 1 after 60 s with `::error::GET /health did not return 200 within 60 s.` and printed the logs (`OptionsValidationException: AssessmentRate must be between 0.001 and 0.25 …`; no amounts in the 129 log lines). But the container never exited: `dotnet` stayed at 99% CPU (`docker top`) and `Up 40 seconds` after the unhandled exception. Cause: `dotnet` was PID 1, and PID 1 ignores the abort signal the runtime raises on an unhandled exception. With `init: true` the same container exits at once with code 134. Fix: `init: true` in `compose.yaml`. Re-run: poll exit 1 after 3 s with `::error::The api container is not running.` (this also covers nice-to-have C15) |
| CI steps | Pass | All four `docker` job scripts, extracted from `ci.yml` and run against the fixed compose: `GET /health returned 200.`, `The api container runs as uid 1654, which owns /app/data.`, `POST returned 201 and GET /api/applications/<id> returned the same application.` |

Not done: C14's deliberate CI break (needs a pushed failing commit; waiting for Alessandro). The containers, the `p4check` volume and the test images were removed afterwards.

## Security gate

`security-auditor` (prompt P04-07) on `git diff main...phase-04-docker` at `ce46e8f`, 2026-11-15. Verdict: pass, no high or critical findings. The file and scanner review only: the image had not been built (no Docker daemon).

Tools: Gitleaks 8.30.1 on the branch commits (`5 commits scanned ... no leaks found`) and the full history (`63 commits scanned ... no leaks found`); Semgrep 1.185.0 with `p/csharp`, `p/secrets`, `p/dockerfile`, `p/github-actions` on the three changed files (`Ran 55 rules on 3 files: 0 findings`); `dotnet list ... package --vulnerable --include-transitive` (no vulnerable packages). hadolint, actionlint, zizmor and trivy are not installed and were not run.

| # | Finding | Severity | Resolution |
|---|---|---|---|
| S1 | The image has never been built or run, so non-root, layer contents and `/app/data` ownership are checked only on paper | Medium (verification gap) | Open: LOCAL checks C1–C11 and C14 must run before Checkpoint B, and the CI `docker` job must pass |
| S2 | `.dockerignore` patterns `.env`, `.env.*`, `*.pem`, `*.md` match only at the context root, so `COPY src/ src/` would copy nested ones into the build stage | Low (latent; no such files exist today) | Fixed: patterns made recursive (`**/.env`, `**/.env.*`, `**/*.pem`, `**/*.md`) and `**/secrets/` added |
| S3 | Compose runs as Development: Scalar, OpenAPI and every endpoint are reachable without authentication until Phase 7 | Low, accepted (P4-D1) | Mitigated by the `127.0.0.1` bind. ADR 0002 (draft) states it and that the bind must not be widened before Phase 7 |
| S4 | Developer exception page in Development | None | False positive: `ProblemExceptionHandler` handles every 500 first and never writes exception text |
| S5 | Base images by tag, not digest | Low, accepted (P4-D4) | Phase 8: pin digests and add a Dependabot `docker` entry |
| S6 | Non-root user | None | Pass on paper; CI now checks it (see R2) |
| S7 | Secrets in layers, `ENV` or `ARG` | None | Pass: none |
| S8 | Workflow permissions, pinning, script injection, cleanup | None | Pass |
| S9 | `$location` echoed in a `::error::` line | Info | False positive: the header comes from this repository's own code and the job has a read-only token and no secrets |
| S10 | The E1 body appears in the Actions log | Info | False positive: synthetic test values already in `Http.cs`; a comment in the step now says so |
| S11 | `runs-on: ubuntu-latest` floats | Info | Same as the `verify` job; revisit in Phase 8 with digest pinning |

## Review gate

`dotnet-reviewer` (prompt P04-08) on the same diff, 2026-11-15. No critical or high findings.

| # | Finding | Severity | Resolution |
|---|---|---|---|
| R1 | ADR 0002 is missing, and the comments in `Program.cs:54` and `DatabaseMigration.cs:11` point to it | Medium | Fixed: ADR 0002 drafted by the lead agent (Alessandro chose this, P04-10). The two code comments are not touched here because `Program.cs` belongs to Phase 6's file set; they are changed to "ADR 0002" on `main` after both merges |
| R2 | C2 was approved as LOCAL + CI, but the CI job never checked the UID | Medium | Fixed: new CI step "Check the container user" fails if `id -u` is empty or 0, or if `/app/data` is not owned by that UID (also covers C8) |
| R3 | `diff <(jq …) <(jq …)` passes when both bodies are empty or not JSON | Low | Fixed: `jq -e` with `--slurpfile` checks exactly one JSON body, equality, and two submitted values |
| R4 | A curl transport error ends the step before `docker compose logs` runs | Low | Fixed: `|| status="curl-exit-$?"` so the status branch prints the logs |
| R5 | `.editorconfig` is not in the image build, so its analyzer settings differ from `verify.sh` | Low | Fixed: copied in the restore layer |
| R6 | `sdk:10.0` can move to a newer SDK band than `verify`, adding analyzer warnings (warnings as errors) | Low | Accepted: recorded under "Open issues" with the Phase 8 digest pinning |
| R7 | Phase file status line was stale | Low | Fixed |
| R8 | Ambiguity 6 said the runtime is Debian | Low | Fixed: Learn confirms .NET 10 default tags are Ubuntu 24.04; the answer is corrected in place |
| R9 | `.claude/rules/infra.md` globs do not match `compose.yaml` | Low | Fixed: `compose*.yaml` and `.dockerignore` added to its `paths` |

The reviewer reproduced the restore and publish of the Api project from the Docker build context on the host (macOS, SDK 10.0.404): both succeeded. It also confirmed that `$APP_UID` expands in `RUN` and `USER`, that a new named volume takes the owner of `/app/data`, and that the POST body passes the strict JSON and validation rules.

## AI output corrected or rejected (REAL cases only)

- **Lead agent, wrong base OS.** The lead agent's answer to ambiguity 6 (approved in P04-05) said the runtime image is "the default Debian `aspnet:10.0`". The `dotnet-reviewer` flagged it from memory (R8); the lead agent checked Learn, which says .NET 10 default tags are Ubuntu 24.04 and no Debian images ship. Corrected in place. No code depended on it.
- **Implementer, approved check left out.** The approved selection lists C2 (non-root UID) as "LOCAL + CI". The `parallel-implementer` did not add it to CI and recorded it as "outside the approved plan". The `dotnet-reviewer` caught the contradiction (R2); the lead agent added the CI step.

## Optimisations considered

## Open issues

- Image digest pinning: Learn calls it a best practice; P4-D4 chose tags for now. Revisit in Phase 8, with a Dependabot `docker` entry (security S5). Also covers review R6: a floating `sdk:10.0` can bring a newer SDK band than the `verify` job and fail on new analyzer warnings.
- ADR 0002 is a draft for Alessandro to accept or change at Checkpoint B.
- The "ADR in Phase 4" comments in `Program.cs` and `DatabaseMigration.cs` are updated to "ADR 0002" after both merges.
- C14 in CI (a deliberately broken startup must fail the `docker` job): approved in P04-10, run on the PR branch as a temporary commit that is reverted.
- X1 (no startup migration outside Development, as an xUnit test): deferred until Phase 4 and Phase 6 are both merged.
- LOCAL checks: not run by the implementer (no Docker daemon); run afterwards by the lead agent, all pass after the `init: true` fix (see "Results (lead agent, with Docker)"). The CI `docker` job has not run yet: it runs with the PR.
- C14's "one deliberate CI break" is not done: it needs a pushed commit that breaks the container, then a revert. Needs Alessandro's go-ahead, since it adds a red CI run to the PR.
- C2 and C8 in CI: resolved after the review gate (R2); C2 was approved as LOCAL + CI, so the CI step is in scope.

## Time spent (approx.)

## Human review (Alessandro writes this; the agent leaves it empty)
