# Phase 08: Security hardening

Status: **In progress: awaiting test proposal approval** · Branch: `phase-08-security` · PR: — · Started: 2026-11-16 · Finished: —

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

## What was built (files)

## Tests

### Proposal (`test-designer`, P08-03)

Full proposal, verbatim: [`phase-08/test-proposal.md`](phase-08/test-proposal.md).

**Recommended selection:** all 24 `must` cases (`S8-01`–`S8-06`, `S8-08`–`S8-25`) plus `S8-07` (`nice`, 1 s fixed-window replenishment).

**Questions and recommended answers:**
- **A1 (Shared vs per-endpoint write bucket, `P8-D1`):** One shared `"write"` policy partitioned by authenticated `sub` across `POST /api/applications` and `POST /api/applications/{id}/assessments` (and `"dev-token"` as its own partition key on `POST /dev/token`).
- **A2 (`UseHsts()` default `ExcludedHosts` vs `AllowedHosts`, `P8-D3`):** Keep ASP.NET Core's default `HstsOptions.ExcludedHosts` intact (so `localhost` browsers are never HSTS-pinned) and in `S8-19` configure `AllowedHosts = "localhost;127.0.0.1;example.com"` on the test host and send `https://example.com/health`.
- **A3 (`Cache-Control: no-store` scope, `P8-D3`):** Set `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: no-referrer`, and `Cache-Control: no-store` on all responses from our middleware (an API-only service has no cacheable static assets; setting `no-store` across responses is simple and uniform, and satisfies `S8-01`, `S8-03`, and `S8-18`).

## Security gate

## Review gate

## AI output corrected or rejected (REAL cases only)

## Optimisations considered

## Open issues

## Time spent (approx.)

## Human review (Alessandro writes this; the agent leaves it empty)
