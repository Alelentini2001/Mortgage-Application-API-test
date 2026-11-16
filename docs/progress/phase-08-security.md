# Phase 08: Security hardening

Status: **Ready for Checkpoint B (PR review)** · Branch: `phase-08-security` · PR: [#12](https://github.com/Alelentini2001/Mortgage-Application-API-test/pull/12) · Started: 2026-11-16 · Finished: —

## Goal

Complete the security and supply-chain hardening deferred from Phases 0, 1, 3, 4, 6 and H1, as designed in `docs/layers/PRODUCTION-LAYERS.md` ("Security", "Rate limiting", "Scaling") and `docs/PROGRESS.md` ("Carry into later phases"):
- **Rate limiting** on write endpoints (`POST /api/applications`, `POST /api/applications/{id}/assessments`, and `POST /dev/token`), partitioned per authenticated owner (`sub`), returning `429 Too Many Requests` (`ProblemDetails`) with `Retry-After` (P8-D1).
- **Bounded enqueue timeout** on `POST /api/applications/{id}/assessments` when the bounded background queue is full, returning `503 Service Unavailable` (`ProblemDetails`) instead of waiting indefinitely, while keeping the saved `Pending` row in SQLite for the worker/startup recovery (Phase 6 security S1; `PRODUCTION-LAYERS.md` "Scaling"; P8-D2).
- **HTTP security headers, HSTS and `AllowedHosts`** (`X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: no-referrer`, `Cache-Control: no-store` on `/api/*` and `/dev/token`, `UseHsts()` outside `Development`, and `AllowedHosts` host filtering; P8-D3).
- **NuGet supply-chain hardening:** `nuget.config` with `<packageSourceMapping>` (`nuget.org` only), `<RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>`, committed `packages.lock.json` files, and `dotnet restore --locked-mode` in `scripts/verify.sh`, `Dockerfile`, and CI (Phase 0 D12, Phase 3 security L3; P8-D4).
- **Container digest pinning and Dependabot:** multi-arch `@sha256:...` digests on the base and dashboard images in `Dockerfile` and `compose.observability.yaml`, plus `.github/dependabot.yml` covering `nuget`, `docker`, and `github-actions` (Phase 4 P4-D4, R6, S5; P8-D5).
- **Harness hook hardening:** block shell writes (`>`, `tee`, `sed -i`, `cp`/`mv`/`ln`, inline `python`/`sh -c`) to `.claude/hooks/**` and `.claude/settings*.json` in `block-dangerous-bash.sh`, with regression cases in `.claude/hooks/tests/test-hooks.sh` (H1 review R4; P8-D6).

No new NuGet packages are needed (`Microsoft.AspNetCore.RateLimiting` and `HostFiltering` ship in the ASP.NET Core 10 shared framework).

## Plan (approved at Checkpoint A)

Approved by Alessandro on 2026-11-16 (P08-02), with Option A on every decision (`P8-D1` to `P8-D6`).

1. Branch `phase-08-security` from `main`.
2. Docs check (links below).
3. Ask `test-designer` for the Phase 8 test cases (rate limiting 429 + per-owner isolation + `Retry-After` + ProblemDetails, full-queue enqueue timeout 503 with Pending row preserved, security headers + HSTS + `AllowedHosts`, `nuget.config` / lock files / `dependabot.yml` verification, and hook regression cases) → Alessandro approves → write failing tests first.
4. Implement rate limiting, enqueue timeout, security headers/HSTS/`AllowedHosts`, NuGet lock files + `nuget.config`, image digest pins + `.github/dependabot.yml`, and `block-dangerous-bash.sh` hardening in small conventional commits.
5. Run `./scripts/verify.sh` (full), 3 integration test runs, and local Docker smoke check.
6. Run `security-auditor`, `dotnet-reviewer`, and `spec-checker` subagents; fix real findings test-first.
7. Update `docs/layers/PRODUCTION-LAYERS.md`, `docs/progress/phase-08-security.md`, `docs/PROGRESS.md`, `docs/ai/PROMPTS.md`, and `docs/ai/DESIGN-JOURNAL.md`; push and open the PR; Checkpoint B.

## Decisions for Alessandro (2-3 options · trade-offs · recommendation)

**P8-D1. Rate limiting on POST endpoints (`PRODUCTION-LAYERS.md` "Rate limiting", Phase 6 S1).**
- **A. Built-in `AddRateLimiter` with a configurable fixed-window policy on `POST /api/applications`, `POST /api/applications/{id}/assessments`, and `POST /dev/token`** (recommended). Partitioned by the authenticated user's `sub` claim (and a separate `"dev-token"` partition for `/dev/token`), `QueueLimit = 0`, `429 Too Many Requests` with `ProblemDetails` and `Retry-After` (seconds). Options `RateLimiting:PermitLimit` (default `60`) and `RateLimiting:WindowSeconds` (default `60`), `[Range(1, 10_000)]` / `[Range(1, 3600)]`, validated at startup. Existing tests stay well below `60/min`; rate-limit tests lower `PermitLimit` via `UseSetting`. No new NuGet package.
- **B. Do not add in-process rate limiting; record it as an accepted risk handled at the API gateway in production** (`PRODUCTION-LAYERS.md` fallback). Simpler, but leaves Phase 6 S1 open in local/container runs.

**P8-D2. Timeout when `POST /api/applications/{id}/assessments` waits on a full queue (`PRODUCTION-LAYERS.md` "Scaling", Phase 6 S1).**
- **A. Configurable enqueue wait timeout (`Assessments:EnqueueTimeoutSeconds`, default `10` s, `[Range(1, 300)]`, validated at startup) around `IAssessmentQueue.EnqueueAsync`, returning `503 Service Unavailable` (`ProblemDetails`) when the queue stays full past the timeout** (recommended). Matches `PRODUCTION-LAYERS.md` ("Scaling": "`FullMode = Wait` with a timeout, then 503. The assessment stays Pending and is picked up again on restart"), works even when a debugger is attached, and distinguishes queue saturation (`503`) from a client disconnect (`RequestAborted` → cancelled).
- **B. ASP.NET Core `AddRequestTimeouts` / `UseRequestTimeouts` middleware** (returns `504 Gateway Timeout` by default, and Learn notes it does not trigger when a debugger is attached).

**P8-D3. Security headers, HSTS and `AllowedHosts` (`PRODUCTION-LAYERS.md` "Security").**
- **A. Add response headers (`X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: no-referrer`, and `Cache-Control: no-store` on `/api/*` and `/dev/token`), enable `UseHsts()` outside `Development`, and set `AllowedHosts` in `appsettings.json` to `"localhost;127.0.0.1"`** (recommended). `WebApplicationFactory` uses `localhost`, Docker Compose and CI use `127.0.0.1`, and requests with an untrusted `Host` header get `400 Bad Request` from ASP.NET Core's Host Filtering middleware.
- **B. Add the response headers and `UseHsts()` outside `Development`, but keep `"AllowedHosts": "*"`** (documenting that host filtering is handled at the ingress/gateway in production).

**P8-D4. NuGet lock files, package-source mapping, and signature verification (Phase 0 D12, Phase 3 L3).**
- **A. Add `nuget.config` (`<clear />`, `nuget.org` over HTTPS, `<packageSourceMapping>` mapping `*` to `nuget.org`), set `<RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>` in `Directory.Build.props`, commit the 5 `packages.lock.json` files, and run `dotnet restore --locked-mode` in `scripts/verify.sh`, `Dockerfile`, and `.github/workflows/ci.yml`** (recommended). Note: `dotnet nuget verify --all` is not supported on macOS by the .NET SDK (root store limitation), so we document that honestly and keep locked-mode restore (which verifies SHA-512 package content hashes on every platform).
- **B. Add `nuget.config` with `<packageSourceMapping>` only, without committing `packages.lock.json` files.**

**P8-D5. Container base-image digest pinning and Dependabot (Phase 4 P4-D4, R6, S5).**
- **A. Pin the multi-arch manifest digests (`tag@sha256:...`) on `mcr.microsoft.com/dotnet/sdk:10.0` and `mcr.microsoft.com/dotnet/aspnet:10.0` in `Dockerfile` and `mcr.microsoft.com/aspire/dashboard:13.6.2` in `compose.observability.yaml`, and add `.github/dependabot.yml` for `nuget`, `docker`, and `github-actions` (weekly)** (recommended). Prevents a floating `sdk:10.0` tag from introducing a new SDK analyzer band mid-PR (Phase 4 R6), while Dependabot opens PRs for digest updates.
- **B. Add `.github/dependabot.yml` (`nuget`, `docker`, `github-actions`), but keep tag-only image references in `Dockerfile` and `compose.observability.yaml`.**

**P8-D6. Blocking shell writes to `.claude/hooks/**` and `.claude/settings*.json` (H1 review R4).**
- **A. Extend `block-dangerous-bash.sh` so shell writes (redirects, `tee`, `sed -i`, `cp`/`mv`/`ln`, and inline `python`/`sh -c` file writes targeting `.claude/hooks/` or `.claude/settings*.json`) are blocked with exit code 2, and add regression tests in `.claude/hooks/tests/test-hooks.sh`** (recommended). Keeps `protect-files.sh` unchanged so direct `Edit`/`Write` tool calls on `.claude/` still use the `ask` permission prompt rather than a hard block.
- **B. Accept the risk and rely on agent instructions.**

## Decisions (option chosen · alternatives · reason · decided by) → ADR links

| # | Option chosen | Alternatives | Reason | Decided by |
|---|---|---|---|---|
| P8-D1 | A: Built-in `AddRateLimiter` fixed-window policy on `POST /api/applications`, `POST …/assessments`, and `POST /dev/token`, partitioned per `sub` (and `"dev-token"`), `QueueLimit = 0`, 429 `ProblemDetails` + `Retry-After` | B no in-process limiter (accepted risk) | Closes Phase 6 S1 locally and in containers without new packages | Alessandro |
| P8-D2 | A: Configurable `Assessments:EnqueueTimeoutSeconds` (default 10 s, `1..300`) around `IAssessmentQueue.EnqueueAsync`, returning 503 `ProblemDetails` while keeping the `Pending` row in SQLite | B `AddRequestTimeouts` / `UseRequestTimeouts` (504, disabled under debugger) | Matches `PRODUCTION-LAYERS.md` ("Scaling": wait with a timeout, then 503); deterministic in tests and under a debugger | Alessandro |
| P8-D3 | A: Response headers (`X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: no-referrer`, `Cache-Control: no-store` on `/api/*` and `/dev/token`), `UseHsts()` outside Development, `AllowedHosts` = `"localhost;127.0.0.1"` | B keep `AllowedHosts: "*"` | Enforces host filtering and prevents intermediary caching of financial data and tokens | Alessandro |
| P8-D4 | A: `nuget.config` (`<clear />`, `nuget.org` over HTTPS, `<packageSourceMapping>` `*`), `<RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>`, 5 `packages.lock.json` files committed, `dotnet restore --locked-mode` in `verify.sh`, `Dockerfile` and CI | B source mapping only | Deterministic, hash-verified restores across local, Docker and CI (Phase 0 D12, Phase 3 L3) | Alessandro |
| P8-D5 | A: Multi-arch `@sha256:...` digests pinned in `Dockerfile` and `compose.observability.yaml`, plus `.github/dependabot.yml` (`nuget`, `docker`, `github-actions` weekly) | B Dependabot without digest pins | Prevents floating `sdk:10.0` analyzer drift (Phase 4 R6) and automates digest updates | Alessandro |
| P8-D6 | A: Block shell writes to `.claude/hooks/**` and `.claude/settings*.json` in `block-dangerous-bash.sh` + regression tests in `test-hooks.sh` | B rely on instructions only | Closes H1 review R4 while keeping `Edit`/`Write` on the `ask` permission list | Alessandro |

## Docs verified (links)

Checked on 2026-11-16 before planning:
- [Rate limiting middleware in ASP.NET Core (.NET 10)](https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit?view=aspnetcore-10.0): `AddRateLimiter`, `UseRateLimiter` (after routing/authentication when partitioning by user), `RateLimitPartition.GetFixedWindowLimiter`, `RejectionStatusCode = StatusCodes.Status429TooManyRequests`, `OnRejected` callback (`OnRejectedContext.Lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan retryAfter)`), `RequireRateLimiting`.
- [Request timeouts middleware in ASP.NET Core (.NET 10)](https://learn.microsoft.com/en-us/aspnet/core/performance/request-timeouts?view=aspnetcore-10.0): cooperative cancellation via `HttpContext.RequestAborted`, returns 504 by default, and does not trigger when a debugger is attached.
- [Enable repeatable package restores using a lock file](https://learn.microsoft.com/en-us/nuget/consume-packages/package-references-in-project-files#locking-dependencies) and [Package Source Mapping](https://learn.microsoft.com/en-us/nuget/consume-packages/package-source-mapping): `RestorePackagesWithLockFile`, `dotnet restore --locked-mode`, `<packageSourceMapping>` with `<package pattern="*" />`.

## Orchestration used

Planning and orchestration in Google Antigravity IDE (Gemini 4 Argon), continuing the cross-IDE / cross-model workflow started in Phase 7.

## Prompts

### P08-01 · human→agent (Antigravity IDE · Gemini 4 Argon) · 2026-11-16
- **Prompt (verbatim):**
  > Proceed with option a for both of them and continue
- **Purpose:** approve Phase 7 Checkpoint B, merge PR #11, and start Phase 8 planning.
- **Outcome:** PR #11 merged (`e1c841d`); Phase 8 plan written (`docs/progress/phase-08-security.md`).

### P08-02 · human→agent (Antigravity IDE · Gemini 4 Argon) · 2026-11-16
- **Prompt (verbatim):**
  > Approved
- **Purpose:** Checkpoint A approval of the Phase 8 plan and decisions `P8-D1` to `P8-D6` (all Option A).
- **Outcome:** decisions recorded; `test-designer` invoked (`P08-03`).

### P08-03 · agent→subagent (`test-designer` in Antigravity · Gemini 4 Argon) · 2026-11-16
- **Prompt (verbatim):**
  ```
  Phase 8 (security hardening) of a .NET 10 mortgage-application API in /Users/ale_ssd/Developer/Mortgage-Application-API-test (branch phase-08-security). Propose test cases for the approved plan; do not write code.

  Read first:
  - `docs/progress/phase-08-security.md` (goal, plan, approved decisions P8-D1 to P8-D6, all Option A)
  - `docs/layers/PRODUCTION-LAYERS.md` ("Security", "Rate limiting", "Scaling")
  - `.claude/rules/security.md`, `.claude/rules/tests.md`, `tests/CLAUDE.md`
  - `src/MortgageApplications.Api/Program.cs`, `src/MortgageApplications.Api/Applications/ApplicationEndpoints.cs`, `src/MortgageApplications.Api/Assessments/*`, `src/MortgageApplications.Api/Identity/DevTokenEndpoint.cs`, `src/MortgageApplications.Api/appsettings.json`
  - `tests/MortgageApplications.IntegrationTests/` (`ApiFactory.cs`, `AssessmentWorkerTests.cs`, `StartupTests.cs`, `ComposeFileTests.cs`, `LocalRunFileTests.cs`)
  - `.claude/hooks/block-dangerous-bash.sh`, `.claude/hooks/tests/test-hooks.sh`
  - `Dockerfile`, `compose.observability.yaml`, `scripts/verify.sh`, `.github/workflows/ci.yml`

  What will be built (P8-D1 to P8-D6, all Option A):
  1. Rate limiting (P8-D1): Built-in AddRateLimiter fixed-window policy on POST /api/applications, POST /api/applications/{id}/assessments, and POST /dev/token, partitioned by authenticated owner sub (and a separate "dev-token" partition for /dev/token), QueueLimit = 0, returning 429 Too Many Requests (ProblemDetails: status, title, traceId) + Retry-After header (seconds), creating or queueing nothing on 429. GET endpoints and /health are not rate-limited by this write policy. Options RateLimiting:PermitLimit ([Range(1, 10_000)], default 60) and RateLimiting:WindowSeconds ([Range(1, 3600)], default 60), validated at startup. Unauthenticated requests to /api/* still get 401 before consuming a rate-limit permit.
  2. Bounded enqueue timeout (P8-D2): Configurable Assessments:EnqueueTimeoutSeconds ([Range(1, 300)], default 10 s, validated at startup) around IAssessmentQueue.EnqueueAsync when POST /api/applications/{id}/assessments waits on a full queue (QueueCapacity = 1, worker held). When the enqueue times out, the endpoint returns 503 Service Unavailable (ProblemDetails, no internal exception details leaked), while the saved Pending assessment row remains in SQLite so the worker/startup recovery completes it once released. A client disconnect (RequestAborted) still cancels without returning 503.
  3. Security headers, HSTS, and AllowedHosts (P8-D3): Every response includes X-Content-Type-Options: nosniff, X-Frame-Options: DENY, and Referrer-Policy: no-referrer; /api/* and /dev/token responses also include Cache-Control: no-store. Outside Development, UseHsts() is enabled (sending Strict-Transport-Security over HTTPS requests). AllowedHosts in appsettings.json is "localhost;127.0.0.1", so an untrusted Host header (e.g. Host: evil.example) returns 400 Bad Request, while localhost and 127.0.0.1 succeed.
  4. NuGet lock files & source mapping (P8-D4): nuget.config with <clear />, nuget.org (https://api.nuget.org/v3/index.json), and <packageSourceMapping> (* -> nuget.org); <RestorePackagesWithLockFile>true</RestorePackagesWithLockFile> in Directory.Build.props; 5 packages.lock.json files committed; dotnet restore --locked-mode in scripts/verify.sh, Dockerfile, and .github/workflows/ci.yml.
  5. Container digest pinning & Dependabot (P8-D5): Multi-arch @sha256:... digests on mcr.microsoft.com/dotnet/sdk:10.0 and mcr.microsoft.com/dotnet/aspnet:10.0 in Dockerfile and mcr.microsoft.com/aspire/dashboard:13.6.2 in compose.observability.yaml; .github/dependabot.yml covering nuget, docker, and github-actions (weekly).
  6. Bash hook self-protection (P8-D6): block-dangerous-bash.sh blocks shell writes (redirects >/>>, tee, sed -i, cp/mv/ln, and inline python/sh -c writes) targeting .claude/hooks/** or .claude/settings*.json with exit code 2, tested in .claude/hooks/tests/test-hooks.sh.

  Use your output format (ID | given | when | then | why it matters), mark each case must or nice, keep the list tight and high-value, and list any ambiguities for Alessandro to settle.
  ```
- **Purpose:** propose Phase 8 test cases before writing any tests.
- **Outcome:** 25 test cases (`S8-01` to `S8-25`) and 3 questions (`A1`–`A3`); full text in [`phase-08/test-proposal.md`](phase-08/test-proposal.md).

### P08-04 · human→agent (Antigravity IDE · Gemini 4 Argon) · 2026-11-16
- **Prompt (verbatim):**
  > Approved
- **Purpose:** approval of the Phase 8 test cases (`S8-01`–`S8-25`) and recommended answers `A1`–`A3`.
- **Outcome:** failing tests written first (`317d493`), then implementation (`1b64d2c`, `2065277`, `80f5159`).

### P08-05 · agent→subagent (`security-auditor` in Antigravity · Gemini 4 Argon) · 2026-11-16
- **Prompt (verbatim):**
  ```
  Security gate for Phase 8 (security hardening) of a .NET 10 mortgage-application API in /Users/ale_ssd/Developer/Mortgage-Application-API-test, branch phase-08-security (HEAD 80f5159). Read only: do not edit files or switch branches.

  Read first: .claude/rules/security.md, AGENTS.md, docs/progress/phase-08-security.md (goal, plan, decisions P8-D1 to P8-D6, approved test proposal S8-01 to S8-25 and answers A1-A3), docs/layers/PRODUCTION-LAYERS.md ("Security", "Rate limiting", "Scaling").

  Do:
  1. Run `gitleaks git --no-banner --redact -v` and `gitleaks dir --no-banner --redact .`
  2. Run `semgrep scan --config p/csharp --config p/secrets --error --quiet` (and with `--baseline-commit main`).
  3. Run `dotnet list MortgageApplications.slnx package --vulnerable --include-transitive`.
  4. Review the diff (`git diff main...HEAD`) against OWASP Top 10:2025 and the Phase 8 security requirements:
     - `RateLimitingSetup` (`src/MortgageApplications.Api/Security/RateLimitingSetup.cs`) and `RateLimitingOptions`: fixed-window limiter on `POST /api/applications`, `POST /api/applications/{id}/assessments`, and `POST /dev/token`, partitioned per authenticated `sub` (and `"dev-token"`), `QueueLimit = 0`, 429 `ProblemDetails` + `Retry-After`; `UseAuthentication()` and `UseAuthorization()` run before `UseRateLimiter()` so unauthenticated traffic gets 401 and cannot exhaust a subject's permits; any partition eviction or memory growth considerations.
     - `ChannelAssessmentQueue` (`src/MortgageApplications.Api/Assessments/ChannelAssessmentQueue.cs`) and `ApplicationEndpoints.AssessAsync`: `Assessments:EnqueueTimeoutSeconds` (1..300, default 10 s), returns 503 `ProblemDetails` on queue saturation without leaking internal exception messages or PII while preserving the `Pending` row in SQLite for startup recovery.
     - `SecurityHeadersMiddleware` (`src/MortgageApplications.Api/Security/SecurityHeadersMiddleware.cs`), `UseHsts()` outside `Development`, and `AllowedHosts = "localhost;127.0.0.1"`.
     - `nuget.config` (`<clear />`, HTTPS `nuget.org`, `<packageSourceMapping>` `*`), `<RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>`, 5 `packages.lock.json` files, `--locked-mode` in `scripts/verify.sh`, `Dockerfile`, and `.github/workflows/ci.yml`.
     - Multi-arch `@sha256:...` digests in `Dockerfile` and `compose.observability.yaml`, and `.github/dependabot.yml`.
     - `.claude/hooks/block-dangerous-bash.sh` blocking shell writes and inline interpreter access to `.claude/hooks/**` and `.claude/settings*.json` (and whether any obvious bypass remains).

  Output:
  - Exact commands run and trimmed outputs.
  - A findings table: `tool/check | finding (file:line) | severity | real or false positive | reason | fix`.
  - A one-line verdict: `PASS` or `FAIL`.
  ```
- **Purpose:** security gate on `phase-08-security` at `80f5159`.
- **Outcome:** **PASS**. 0 leaks (158 commits + working tree), 0 Semgrep findings, 0 vulnerable packages; 2 low findings (`S1` `/dev/token` partition key with Bearer header / `"dev-token"` subject prefix, `S2` `block-dangerous-bash.sh` `.claude/hooks` without trailing slash and `mv`/`rm`/`chmod`), both fixed test-first in `7282960` and `2a74405`.

### P08-06 · agent→subagent (`dotnet-reviewer` in Antigravity · Gemini 4 Argon) · 2026-11-16
- **Prompt (verbatim):**
  ```
  Fresh-context diff review of Phase 8 (security hardening) in /Users/ale_ssd/Developer/Mortgage-Application-API-test, branch phase-08-security (HEAD 80f5159). Read only: do not edit files, do not build or run tests.

  Get the diff with `git diff main...HEAD`. Read the touched files in full (`src/MortgageApplications.Api/Program.cs`, `src/MortgageApplications.Api/Security/*`, `src/MortgageApplications.Api/Assessments/*`, `src/MortgageApplications.Api/Applications/ApplicationEndpoints.cs`, `src/MortgageApplications.Api/Identity/DevTokenEndpoint.cs`, `src/MortgageApplications.Api/appsettings.json`, `Directory.Build.props`, `nuget.config`, `Dockerfile`, `compose.observability.yaml`, `scripts/verify.sh`, `.github/workflows/ci.yml`, `.github/dependabot.yml`, `.claude/hooks/block-dangerous-bash.sh`, `.claude/hooks/tests/test-hooks.sh`, `tests/MortgageApplications.IntegrationTests/*`), plus `AGENTS.md`, `.claude/rules/backend.md`, `.claude/rules/security.md`, `.claude/rules/tests.md`, `tests/CLAUDE.md`, and `docs/progress/phase-08-security.md`.

  Check in priority order:
  1. Security & correctness: `RateLimitingSetup` (partitioning, `OnRejected` `Retry-After` calculation, middleware order with `UseStatusCodePages` / `UseAuthentication` / `UseAuthorization`), `ChannelAssessmentQueue.EnqueueSlowAsync` (`CancellationTokenSource` disposal, race between `timeout.Token` and `cancellationToken`, `TimeoutException` handling in `ApplicationEndpoints.AssessAsync`), `SecurityHeadersMiddleware`, `UseHsts()`, `AllowedHosts`, `block-dangerous-bash.sh` regexes, `Dockerfile` layer caching and `--locked-mode`.
  2. Framework usage: DI lifetimes, options validation (`ValidateDataAnnotations().ValidateOnStart()`), OpenAPI metadata (`ProducesProblem` for 429 and 503).
  3. Test gaps: whether every approved case (`S8-01`–`S8-25`) in `docs/progress/phase-08/test-proposal.md` has a test in `RateLimitingTests`, `SecurityHardeningTests`, `StartupTests`, `AssessmentWorkerTests`, `ComposeFileTests`, or `test-hooks.sh` that would fail if the behaviour broke.
  4. Over-engineering or dead code.

  Output only a list, most severe first, one finding per line:
  `file:line | severity (critical/high/medium/low) | why it is wrong | concrete fix`
  If you find nothing real, output `No findings.` and the list of files you reviewed. State plainly that you did not build or run tests.
  ```
- **Purpose:** fresh-context review of `phase-08-security` at `80f5159`.
- **Outcome:** 2 medium (`R1`, `R2`, matching `security-auditor` `S1`/`S2`) and 3 low (`R3`–`R5`) findings, all fixed test-first in `7282960` and `2a74405`.

### P08-07 · agent→subagent (`spec-checker` in Antigravity · Gemini 4 Argon) · 2026-11-16
- **Prompt (verbatim):**
  ```
  Requirement traceability check at the end of Phase 8 (security hardening) in /Users/ale_ssd/Developer/Mortgage-Application-API-test, branch phase-08-security (HEAD 80f5159). Read only: do not edit files.

  Read:
  - `docs/spec/ORIGINAL_SPEC.md` and `docs/spec/affordability-rules.md`
  - `docs/layers/PRODUCTION-LAYERS.md` ("Security", "Rate limiting", "Scaling")
  - `docs/PROGRESS.md` (the R1-R55 traceability table)
  - `docs/progress/phase-08-security.md` and `docs/progress/phase-08/test-proposal.md`
  - The Phase 8 code, build files, hooks, and tests changed in `git diff main...HEAD`

  Do:
  1. Map every Phase 8 goal, decision (`P8-D1` to `P8-D6`), and approved test case (`S8-01` to `S8-25`) to the file:line and test name that meet it.
  2. Check which rows of `docs/layers/PRODUCTION-LAYERS.md` ("Security", "Rate limiting", "Scaling") need wording updates to reflect that Phase 8 is now built rather than planned for Phase 8.
  3. Check whether any R1–R55 row in `docs/PROGRESS.md` changes status or evidence after Phase 8 (e.g. R23 validations, R27 test count 576/576 + 105 hook cases, R30 background processing 503 timeout).
  4. Output a Markdown table for the Phase 8 goals and at most 5 short notes on any open items or doc updates needed.
  ```
- **Purpose:** requirement traceability at the end of Phase 8.
- **Outcome:** all 6 decisions (`P8-D1`–`P8-D6`) and all 25 test cases (`S8-01`–`S8-25`) are **met**.

## What was built (files)

Implemented in commits `317d493` (failing tests), `1b64d2c` (API security), `2065277` (NuGet lock files, Docker digests, Dependabot), `80f5159` (bash hook hardening), `7282960` (gate tests), and `2a74405` (gate fixes):

- `src/MortgageApplications.Api/Security/`:
  - `RateLimitingOptions.cs`: `RateLimiting:PermitLimit` (`[Range(1, 10_000)]`, default 60) and `RateLimiting:WindowSeconds` (`[Range(1, 3_600)]`, default 60).
  - `RateLimitingSetup.cs`: `AddWriteRateLimiting()`, fixed-window `"write"` policy (`QueueLimit = 0`, `AutoReplenishment = true`), partitioned by `"sub:{sub}"` on authenticated endpoints and `"dev-token"` on `IAllowAnonymous` endpoints (`POST /dev/token`), returning `429 Too Many Requests` (`ProblemDetails`) with `Retry-After` in whole seconds.
  - `SecurityHeadersMiddleware.cs`: `UseSecurityHeaders()`, setting `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: no-referrer`, and `Cache-Control: no-store` on every response.
- `src/MortgageApplications.Api/Assessments/AssessmentQueueOptions.cs` and `ChannelAssessmentQueue.cs`:
  - `Assessments:EnqueueTimeoutSeconds` (`[Range(1, 300)]`, default 10 s), validated at startup; `EnqueueSlowAsync` links the request's `CancellationToken` with a timeout `CancellationTokenSource` and throws `TimeoutException` only when the timeout expires before the caller cancels.
- `src/MortgageApplications.Api/Applications/ApplicationEndpoints.cs` and `Identity/DevTokenEndpoint.cs`:
  - `.RequireRateLimiting(RateLimitingSetup.WritePolicyName)` and `.ProducesProblem(StatusCodes.Status429TooManyRequests)` on `POST /api/applications`, `POST /api/applications/{id}/assessments`, and `POST /dev/token`.
  - `AssessAsync` catches `TimeoutException` and returns `TypedResults.StatusCode(StatusCodes.Status503ServiceUnavailable)` (`503 ProblemDetails` via `UseStatusCodePages()`), leaving the saved `Pending` row in SQLite for startup recovery.
- `src/MortgageApplications.Api/Program.cs` and `appsettings.json`:
  - `UseHsts()` outside `Development`, `UseSecurityHeaders()`, `UseRateLimiter()` after `UseAuthentication()` and `UseAuthorization()`; `AllowedHosts` set to `"localhost;127.0.0.1"`.
- Supply chain & Docker (`P8-D4`, `P8-D5`):
  - `nuget.config` (`<clear />`, `nuget.org` over HTTPS, `<packageSourceMapping>` `*`), `Directory.Build.props` (`<RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>`), 5 committed `packages.lock.json` files, `--locked-mode` in `scripts/verify.sh`, `Dockerfile`, and `.github/workflows/ci.yml`.
  - Multi-arch `@sha256:...` digests pinned in `Dockerfile` (`sdk:10.0`, `aspnet:10.0`) and `compose.observability.yaml` (`aspire/dashboard:13.6.2`), plus `.github/dependabot.yml` (`nuget`, `docker`, `github-actions` weekly).
- `.claude/hooks/block-dangerous-bash.sh`: blocks redirects, `tee`, `sed -i`, `cp`, `mv`, `ln`, `rm`, `unlink`, `chmod`, `truncate`, `install`, and inline interpreter commands targeting `.claude/hooks` or `.claude/settings*.json`.
- Tests (`tests/MortgageApplications.IntegrationTests/` and `.claude/hooks/tests/test-hooks.sh`): `RateLimitingTests.cs` (new), `SecurityHardeningTests.cs` (new), `StartupTests.cs`, `AssessmentWorkerTests.cs`, `ComposeFileTests.cs`, `OtlpExportTests.cs`, and `test-hooks.sh`.

## Tests

### Proposal (`test-designer`, P08-03)

Full proposal, verbatim: [`phase-08/test-proposal.md`](phase-08/test-proposal.md).

**Recommended selection:** all 24 `must` cases (`S8-01`–`S8-06`, `S8-08`–`S8-25`) plus `S8-07` (`nice`, 1 s fixed-window replenishment).

**Questions and recommended answers:**
- **A1 (Shared vs per-endpoint write bucket, `P8-D1`):** One shared `"write"` policy partitioned by authenticated `sub` across `POST /api/applications` and `POST /api/applications/{id}/assessments` (and `"dev-token"` as its own partition key on `POST /dev/token`).
- **A2 (`UseHsts()` default `ExcludedHosts` vs `AllowedHosts`, `P8-D3`):** Keep ASP.NET Core's default `HstsOptions.ExcludedHosts` intact (so `localhost` browsers are never HSTS-pinned) and in `S8-19` configure `AllowedHosts = "localhost;127.0.0.1;example.com"` on the test host and send `https://example.com/health`.
- **A3 (`Cache-Control: no-store` scope, `P8-D3`):** Set `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: no-referrer`, and `Cache-Control: no-store` on all responses from our middleware (an API-only service has no cacheable static assets; setting `no-store` across responses is simple and uniform, and satisfies `S8-01`, `S8-03`, and `S8-18`).

### Results

- **Red first (`317d493`, before implementation):**
  - Integration tests in the 5 touched classes (`RateLimitingTests`, `SecurityHardeningTests`, `StartupTests`, `ComposeFileTests`, `AssessmentWorkerTests`): 86 total, **27 failed**, 59 passed.
  - `.claude/hooks/tests/test-hooks.sh`: 104 total, **11 failed** (all 11 `S8-20` blocked cases), 93 passed.
- **Green after initial implementation (`80f5159`):** `./scripts/verify.sh` full → 0 warnings, 0 errors, **576 tests** (274 unit + 302 integration), **105 hook cases**, `VERIFY PASSED (full)`.
- **Red then green on the gate findings (`7282960` tests → `2a74405` fixes):**
  - Red (`7282960`): `RateLimitingTests.Dev_token_ignores_bearer_sub_and_does_not_collide_with_sub_named_dev_token` failed (`Expected: Created, Actual: TooManyRequests`), and all 6 new `test-hooks.sh` cases (`cp /tmp/x .claude/hooks`, `mv` source, `rm`, `chmod`, `shutil.rmtree('.claude/hooks')`) failed (`106 passed, 6 failed`).
  - Green (`2a74405`): `./scripts/verify.sh` full → 0 warnings, 0 errors, **577 tests** (274 unit + 303 integration), **113 hook cases**, `VERIFY PASSED (full)`.
- **Integration tests 3 times in a row (after `2a74405`):** run 1: `303/303` (16.6 s); run 2: `303/303` (17.1 s); run 3: `303/303` (17.0 s).
- **Local Docker image build:** `docker build -t mortgage-applications-p8-test .` (with `@sha256` base-image digests, `nuget.config`, `packages.lock.json` and `dotnet restore --locked-mode`) succeeded in 17.0 s and was cleaned up (`docker rmi`).

## Security gate

`security-auditor` (`P08-05`, Antigravity IDE · Gemini 4 Argon, at `80f5159`). **Verdict: PASS.**
- `gitleaks git --no-banner --redact -v` (158 commits) and `gitleaks dir --no-banner --redact .`: 0 leaks.
- `semgrep scan --config p/csharp --config p/secrets --error --quiet` (full and `--baseline-commit main`): 0 findings.
- `dotnet list MortgageApplications.slnx package --vulnerable --include-transitive`: 0 vulnerable packages across all 5 projects.
- Manual OWASP Top 10:2025 review:

| # | Finding | Severity | Status |
|---|---|---|---|
| S1 (= R1) | `RateLimitingSetup.cs:43`: `POST /dev/token` used `sub` when a `Bearer` header was sent (allowing a caller in dev mode to rotate minted tokens across fresh partitions) and shared an unprefixed keyspace with `sub == "dev-token"`. | Low/Medium | Fixed, `2a74405`: `IAllowAnonymous` endpoints always use `"dev-token"`, and authenticated subjects are prefixed as `"sub:{sub}"`. Pinned by `Dev_token_ignores_bearer_sub_and_does_not_collide_with_sub_named_dev_token` (`7282960`). |
| S2 (= R2) | `block-dangerous-bash.sh:37,170-187`: `PROTECTED` required a trailing `/` on `.claude/hooks/`, only checked the last argument of `mv`, and did not check `rm`, `unlink`, `chmod`, `truncate`, or `install` against `PROTECTED`. | Low/Medium | Fixed, `2a74405`: matches `hooks(/|$)` and checks all non-flag arguments of `mv`, `rm`, `unlink`, `chmod`, `truncate`, and `install` (+6 cases in `test-hooks.sh`, `7282960`). |

## Review gate

1. **`dotnet-reviewer` (`P08-06`, Antigravity IDE · Gemini 4 Argon, at `80f5159`, read-only):**

| # | Finding | Severity | Status |
|---|---|---|---|
| R1 (= S1) | `RateLimitingSetup.cs:43`: `POST /dev/token` partitioned by `sub` when a Bearer header was sent and used an unprefixed keyspace. | Medium | Fixed, `2a74405` (test in `7282960`). |
| R2 (= S2) | `block-dangerous-bash.sh:37,179`: `.claude/hooks` without trailing `/` and `mv` source arguments were not matched. | Medium | Fixed, `2a74405` (6 tests in `7282960`). |
| R3 | `AssessmentWorkerTests.cs:209`: `S8-22` checked `"Pending"` on the timed-out 3rd assessment before `gate.Open()`, rather than after `first`, `second`, and a `marker` assessment completed, and omitted `"240000"` from `forbidden`. | Low | Fixed, `7282960`: checks `"Pending"` after `gate.Open()`, `first`, `second`, and `marker`, and includes `"240000"`. |
| R4 | `AssessmentWorkerTests.cs:147`: `I-10` (`S8-23`) waited for `CountAssessmentsAsync == 3` rather than `SignalWhenEnqueueWaits` and did not call `AssertNoWorkerWarningsOrErrors`. | Low | Fixed, `7282960`. |
| R5 | `test-hooks.sh:123`: `S8-21` omitted `edit_file 0` checks on `protect-files.sh` for `.claude/settings.json` and `.claude/hooks/block-dangerous-bash.sh`. | Low | Fixed, `7282960`. |

2. **`spec-checker` (`P08-07`, Antigravity IDE · Gemini 4 Argon, at `80f5159`, read-only):**
   - All 6 Phase 8 decisions (`P8-D1`–`P8-D6`) and all 25 test cases (`S8-01`–`S8-25`) are **met**.
   - `docs/layers/PRODUCTION-LAYERS.md` ("Security", "Rate limiting", "Scaling") and `docs/PROGRESS.md` updated to reflect Phase 8 as built.

## AI output corrected or rejected (REAL cases only)

- **Rate-limit partition key on `POST /dev/token` (`1b64d2c`):** initial `RateLimitingSetup` used `httpContext.User.FindFirst(SubjectClaim.Type)?.Value ?? "dev-token"`, which allowed a caller sending a `Bearer` token to `POST /dev/token` to use that `sub`'s bucket instead of `"dev-token"` and left `sub == "dev-token"` unprefixed. Caught by both `security-auditor` (`P08-05`) and `dotnet-reviewer` (`P08-06`); fixed test-first in `7282960` and `2a74405`.
- **Bash hook path and command coverage (`80f5159`):** initial `block-dangerous-bash.sh` regex required a trailing `/` on `.claude/hooks/` and did not check `mv` source arguments or `rm`/`chmod` targets against `PROTECTED`. Caught by `security-auditor` and `dotnet-reviewer`; fixed test-first in `7282960` and `2a74405`.
- **Binary protobuf `"×"` collision in Phase 5 `OtlpExportTests`:** during the 3-run integration check, `OtlpExportTests.Exported_data_has_our_span_and_instruments_and_no_assessment_numbers` failed once because the 2-byte UTF-8 sequence `0xC3 0x97` (`"×"`) appeared by chance inside a random binary protobuf trace/span ID. Fixed in `2a74405` by excluding the 1-character `"×"` from the raw binary protobuf scan while keeping it on all text logs, span tags, and metric tags in `TelemetryAllowlistTests`.

## Optimisations considered

- **Fast-path `TryWrite` in `ChannelAssessmentQueue.EnqueueAsync`:** keeps the synchronous `ValueTask.CompletedTask` return when the bounded channel has room, allocating the timeout `CancellationTokenSource` only on the slow path (`EnqueueSlowAsync`) when the channel is full.

## Open issues

- **NuGet signature verification on macOS (`P8-D4 A`):** `dotnet nuget verify --all` is not supported on macOS due to the .NET SDK root store limitation; SHA-512 package content hashes are verified on every restore via `packages.lock.json` and `dotnet restore --locked-mode`.
- **In-memory rate-limit partitions:** `PartitionedRateLimiter` holds one fixed-window counter per active `sub` in process memory; acceptable for a single-instance exercise (distributed rate limiting at the API gateway is documented in `PRODUCTION-LAYERS.md` for production).

## Time spent (approx.)

~40 min (`2026-11-16 01:42 UTC` to `02:22 UTC`, commits `9c690ea`–`2a74405`, in Google Antigravity IDE · Gemini 4 Argon).

## Human review (Alessandro writes this; the agent leaves it empty)
