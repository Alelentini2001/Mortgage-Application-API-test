# Phase 8 Test Design — Security Hardening (`P8-D1` to `P8-D6`)

Proposed test cases for [phase-08-security.md](file:///Users/ale_ssd/Developer/Mortgage-Application-API-test/docs/progress/phase-08-security.md), grouped by category. Nothing is assumed approved until Alessandro selects the cases.

---

## 1. Happy path

1. **`S8-01` (`must`)** | **Given** default `ApiFactory` (`Environment = Development`, `Host = localhost`, authenticated client for `test-owner-0001`, default `RateLimiting:PermitLimit = 60`, `RateLimiting:WindowSeconds = 60`, `Assessments:EnqueueTimeoutSeconds = 10`) | **When** `POST /api/applications` is called with `Http.BaseBody` (`annualIncome = 85000.00`, `monthlyExpenses = 900.00`, `depositAmount = 60000.00`, `propertyValue = 300000.00`, `termYears = 30`) | **Then** `201 Created` with `Location: /api/applications/{id}` and response headers `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: no-referrer`, and `Cache-Control: no-store` | **Why it matters:** proves normal application creation works under all new security middlewares and carries the required security and anti-caching headers.

2. **`S8-02` (`must`)** | **Given** default `ApiFactory` (`Host = localhost`) and also a request with `Host: 127.0.0.1:8080` | **When** anonymous `GET /health` is called on each host | **Then** both return `200 OK` (`"Healthy"`) with `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, and `Referrer-Policy: no-referrer` (and no `Strict-Transport-Security` in `Development`) | **Why it matters:** proves both hosts in `AllowedHosts = "localhost;127.0.0.1"` (`WebApplicationFactory` and Docker/CI loopback) succeed and `/health` receives the baseline security headers.

3. **`S8-03` (`must`)** | **Given** a `Development` host with `DevAuth:Enabled = true` and `Jwt:SigningPublicKey = ""` | **When** anonymous `POST /dev/token` is called with `{"subject":"alice"}` | **Then** `200 OK` with `{token, expiresAt}` and headers `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: no-referrer`, and `Cache-Control: no-store` | **Why it matters:** proves issued bearer tokens carry `Cache-Control: no-store` so intermediaries never cache tokens (`P8-D3`).

---

## 2. Boundaries (exact limit, just below, just above)

### Rate-limit request count boundary (`P8-D1`)

4. **`S8-04` (`must`)** | **Given** a host with `RateLimiting:PermitLimit = 2` and `RateLimiting:WindowSeconds = 60`, and an authenticated client for `"alice"` | **When** `"alice"` sends 3 consecutive `POST /api/applications` requests with `Http.BaseBody` within the same 60 s window | **Then** request #1 (below limit: 1 < 2) returns `201 Created`; request #2 (exact limit: 2 = 2) returns `201 Created`; request #3 (just above: 3 > 2) returns `429 Too Many Requests` (`application/problem+json` with `status = 429`, non-empty `title`, non-empty `traceId`), header `Retry-After` is an integer in 1..60, and total applications in SQLite is exactly 2 (the 3rd was not saved) | **Why it matters:** tests just below (1), at (2), and just above (3) `PermitLimit` on `POST /api/applications` and proves rejected requests write nothing to SQLite.

5. **`S8-05` (`must`)** | **Given** a host with `RateLimiting:PermitLimit = 2` and `RateLimiting:WindowSeconds = 60`, and an existing application `{id}` seeded for `"alice"` before the test host's limiter is used | **When** `"alice"` sends 3 consecutive `POST /api/applications/{id}/assessments` requests | **Then** request #1 (1 < 2) returns `202 Accepted`; request #2 (2 = 2) returns `202 Accepted`; request #3 (3 > 2) returns `429 Too Many Requests` (`ProblemDetails` + `Retry-After` in 1..60), and total assessments in SQLite for `{id}` is exactly 2 | **Why it matters:** proves the exact rate-limit boundary on `POST /api/applications/{id}/assessments` and that a `429` neither saves a `Pending` row nor enqueues work.

6. **`S8-06` (`must`)** | **Given** a `Development` host with `DevAuth:Enabled = true`, `Jwt:SigningPublicKey = ""`, `RateLimiting:PermitLimit = 2`, and `RateLimiting:WindowSeconds = 60` | **When** an anonymous client sends 3 consecutive `POST /dev/token` requests with `{"subject":"alice"}` | **Then** request #1 (1 < 2) returns `200 OK`; request #2 (2 = 2) returns `200 OK`; request #3 (3 > 2) returns `429 Too Many Requests` (`ProblemDetails` + `Retry-After` in 1..60, body does not contain `"token"`) | **Why it matters:** proves the `"dev-token"` partition enforces `PermitLimit` at the exact threshold and issues no token on `429`.

7. **`S8-07` (`nice`)** | **Given** a host with `RateLimiting:PermitLimit = 1` and `RateLimiting:WindowSeconds = 1` | **When** `"alice"` sends `POST /api/applications` (#1 -> `201`), immediately sends #2 (-> `429`), and polls/retries until the 1 s fixed window rolls over (bounded wait <= 3 s) | **Then** the post-window request returns `201 Created` | **Why it matters:** proves the fixed window replenishes permits after `WindowSeconds` elapses.

### Startup options validation boundaries (`P8-D1`, `P8-D2`)

8. **`S8-08` (`must`)** | **Given** `RateLimiting:PermitLimit` (`[Range(1, 10_000)]`) set to `"1"` (exact min) or `"10000"` (exact max) | **When** the host starts and `GET /health` is called | **Then** `200 OK` | **Why it matters:** proves both exact boundaries (1 and 10,000) of `RateLimiting:PermitLimit` are accepted at startup.

9. **`S8-09` (`must`)** | **Given** `RateLimiting:PermitLimit` set to `"0"` (just below min: 0), `"-1"`, or `"10001"` (just above max: 10,001) | **When** the host starts (`CreateClient()`) | **Then** startup throws `OptionsValidationException` whose message contains `"PermitLimit"` | **Why it matters:** proves startup fails closed just outside `[1, 10_000]`.

10. **`S8-10` (`must`)** | **Given** `RateLimiting:WindowSeconds` (`[Range(1, 3600)]`) set to `"1"` (exact min) or `"3600"` (exact max) | **When** the host starts and `GET /health` is called | **Then** `200 OK` | **Why it matters:** proves both exact boundaries (1 s and 3600 s) of `RateLimiting:WindowSeconds` are accepted at startup.

11. **`S8-11` (`must`)** | **Given** `RateLimiting:WindowSeconds` set to `"0"` (just below min: 0), `"-1"`, or `"3601"` (just above max: 3601) | **When** the host starts (`CreateClient()`) | **Then** startup throws `OptionsValidationException` whose message contains `"WindowSeconds"` | **Why it matters:** proves startup fails closed just outside `[1, 3600]`.

12. **`S8-12` (`must`)** | **Given** `Assessments:EnqueueTimeoutSeconds` (`[Range(1, 300)]`) set to `"1"` (exact min) or `"300"` (exact max) | **When** the host starts and `GET /health` is called | **Then** `200 OK` | **Why it matters:** proves both exact boundaries (1 s and 300 s) of `Assessments:EnqueueTimeoutSeconds` are accepted at startup.

13. **`S8-13` (`must`)** | **Given** `Assessments:EnqueueTimeoutSeconds` set to `"0"` (just below min: 0), `"-1"`, or `"301"` (just above max: 301) | **When** the host starts (`CreateClient()`) | **Then** startup throws `OptionsValidationException` whose message contains `"EnqueueTimeoutSeconds"` | **Why it matters:** proves startup fails closed just outside `[1, 300]`.

---

## 3. Failure & validation

14. **`S8-14` (`must`)** | **Given** default `ApiFactory` (`AllowedHosts = "localhost;127.0.0.1"`) | **When** a request is sent with `Host: evil.example` (or `Host: evil.example:8080`) to `GET /health` and to `POST /api/applications` | **Then** `400 Bad Request` | **Why it matters:** proves ASP.NET Core Host Filtering rejects untrusted `Host` headers (`P8-D3`).

---

## 4. Security

### Rate-limit isolation, read exemptions, and auth ordering (`P8-D1`)

15. **`S8-15` (`must`)** | **Given** a host with `RateLimiting:PermitLimit = 1`, `RateLimiting:WindowSeconds = 60`, and two authenticated clients (`"alice"` and `"bob"`) | **When** `"alice"` calls `POST /api/applications` twice (#1 -> `201`, #2 -> `429`), and then `"bob"` calls `POST /api/applications` once | **Then** `"bob"` receives `201 Created` | **Why it matters:** proves rate-limit partitions are isolated per `sub` so one noisy user cannot starve another user (`P8-D1`).

16. **`S8-16` (`must`)** | **Given** a host with `RateLimiting:PermitLimit = 2`, `RateLimiting:WindowSeconds = 60`, where `"alice"` calls `POST /api/applications` (#1 -> `201`, creating `{id}`) and `POST /api/applications/{id}/assessments` (#2 -> `202`, creating `{assessmentId}`), and then a 3rd `POST` returns `429` (exhausting `"alice"`'s write budget) | **When** `"alice"` calls `GET /api/applications/{id}`, `GET /api/applications/{id}/assessments/{assessmentId}`, and `GET /health` | **Then** all three `GET` requests return `200 OK` | **Why it matters:** proves read endpoints (`GET`) and `/health` are not throttled when a caller's write budget is exhausted, so polling an assessment `Location` continues to work (`P8-D1`).

17. **`S8-17` (`must`)** | **Given** a host with `RateLimiting:PermitLimit = 1`, `RateLimiting:WindowSeconds = 60` | **When** an anonymous client (or a client with a bad/expired bearer token) sends 3 `POST /api/applications` requests, and then authenticated `"alice"` sends 1 `POST /api/applications` request | **Then** all 3 unauthenticated requests return `401 Unauthorized` (never `429`), and `"alice"`'s request returns `201 Created` | **Why it matters:** proves authentication/authorization runs before rate limiting on `/api/*`, so unauthenticated traffic gets `401` and cannot burn permits.

### Security headers & HSTS (`P8-D3`)

18. **`S8-18` (`must`)** | **Given** default `ApiFactory` | **When** `GET /api/applications/{id}` (`200`), `GET /api/applications/{unknownId}` (`404 ProblemDetails`), and anonymous `POST /api/applications` (`401 ProblemDetails`) are called | **Then** every response includes `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: no-referrer`, and `Cache-Control: no-store` | **Why it matters:** proves security headers and `Cache-Control: no-store` are present on both successful and error (`ProblemDetails`) responses under `/api/*`.

19. **`S8-19` (`must`)** | **Given** a host running in `Production` (and `Staging`) with an HTTPS base address (`https://...`, see Ambiguity A2 regarding `HstsOptions.ExcludedHosts` vs `AllowedHosts`) vs a host running in `Development` with an HTTPS base address | **When** `GET /health` is called over HTTPS on each | **Then** `Production` and `Staging` include the `Strict-Transport-Security` header (`max-age=...`), whereas `Development` does not include `Strict-Transport-Security` | **Why it matters:** proves `UseHsts()` is enabled outside `Development` only (`P8-D3`).

### Hook self-protection (`P8-D6`)

20. **`S8-20` (`must`)** | **Given** `.claude/hooks/block-dangerous-bash.sh` invoked via `.claude/hooks/tests/test-hooks.sh` | **When** a Bash command attempts to write to `.claude/hooks/**` or `.claude/settings*.json` via shell redirect (`>`, `>>`, `>|`, `&>`, `&>>`), `tee`, `sed -i` / `--in-place`, `cp`, `mv`, `ln`, `bash -c` / `sh -c`, `eval`, or inline interpreter (`python3 -c`, `python -c`, `node -e`, `perl -e`, `ruby -e`) | **Then** every case exits with code `2` (blocked) | **Why it matters:** closes H1 review R4 so a Bash command cannot overwrite or disable the safety hooks or permission settings (`P8-D6`).

21. **`S8-21` (`must`)** | **Given** `.claude/hooks/block-dangerous-bash.sh` and `.claude/hooks/protect-files.sh` | **When** safe read/test commands run in Bash (`.claude/hooks/tests/test-hooks.sh`, `git diff .claude/hooks/block-dangerous-bash.sh`, `git status .claude/settings.json`) and when `protect-files.sh` checks `.claude/settings.json` or `.claude/hooks/block-dangerous-bash.sh` | **Then** all exit with code `0` (allowed) | **Why it matters:** proves running hook tests or inspecting `.claude/` in git is not false-positively blocked, and direct `Edit`/`Write` on `.claude/` remains governed by the `ask` permission rule in `.claude/settings.json` (`P8-D6`).

---

## 5. Concurrency & idempotency (queue saturation timeout + recovery, `P8-D2`)

22. **`S8-22` (`must`)** | **Given** a host with `Assessments:QueueCapacity = 1`, `Assessments:EnqueueTimeoutSeconds = 1`, and the background worker held at `WorkerGate`; an application `{id}` created with sentinel financial values (`annualIncome = 85123.45`, `monthlyExpenses = 912.34`, `depositAmount = 61234.56`, `propertyValue = 301234.56`, `termYears = 30`); assessment #1 (`202`) held inside the worker (`gate.Entered`) and assessment #2 (`202`) filling the 1-slot channel | **When** assessment #3 is posted (`POST /api/applications/{id}/assessments`) while the worker gate stays closed | **Then**:
    1. Assessment #3 returns `503 Service Unavailable` after ~1 s (`application/problem+json` with `status = 503`, non-empty `title`, non-empty `traceId`, and `Cache-Control: no-store`).
    2. Neither the `503` response body nor any log entry contains `OperationCanceledException`, stack traces, the owner ID (`test-owner-0001`), or any sentinel number (`85123`, `912.34`, `61234`, `301234`, `240000`).
    3. SQLite contains all 3 assessment rows, and the 3rd row has `Status == AssessmentStatus.Pending`.
    4. After `gate.Open()`, assessments #1 and #2 finish (`Completed`), while #3 remains `Pending` until a restarted host (`runWorker: true`) runs its startup sweep, which re-enqueues and completes #3 (`Status == "Completed"`).
    | **Why it matters:** proves full-queue backpressure times out cleanly with `503 ProblemDetails`, leaks no internals or PII, preserves the `Pending` row in SQLite, and recovers on startup (`P8-D2`, Phase 6 S1).

23. **`S8-23` (`must`)** | **Given** a host with `Assessments:QueueCapacity = 1`, `Assessments:EnqueueTimeoutSeconds = 30`, and the worker held at `WorkerGate` (with #1 in the worker and #2 filling the channel) | **When** the client cancels its own request token (`RequestAborted`) while assessment #3 is waiting on the full queue (before the 30 s enqueue timeout elapses) | **Then** the request is cancelled (`OperationCanceledException` on the client side) rather than returning `503`, logs no error, and leaves the saved row `Pending` in SQLite for startup recovery (existing `I-10`) | **Why it matters:** proves `AssessAsync` distinguishes an enqueue timeout (`503`) from a caller disconnect (`RequestAborted` -> cancelled).

---

## 6. Supply chain & configuration files (`P8-D4`, `P8-D5`)

24. **`S8-24` (`must`)** | **Given** the repository root | **When** `nuget.config`, `Directory.Build.props`, and the 5 project directories (`src/MortgageApplications.Core`, `src/MortgageApplications.Infrastructure`, `src/MortgageApplications.Api`, `tests/MortgageApplications.UnitTests`, `tests/MortgageApplications.IntegrationTests`) are inspected | **Then**:
    - `nuget.config` has `<packageSources>` with `<clear />` and only `https://api.nuget.org/v3/index.json` (no `http://` sources), plus `<packageSourceMapping>` mapping `<package pattern="*" />` to `nuget.org`;
    - `Directory.Build.props` contains `<RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>`;
    - all 5 `packages.lock.json` files exist and are non-empty JSON with `"version": 1` (or `2`);
    - `scripts/verify.sh` and `Dockerfile` run `dotnet restore` with `--locked-mode` (and `Dockerfile` copies `nuget.config` and the `src/` `packages.lock.json` files before `dotnet restore`).
    | **Why it matters:** verifies deterministic, hash-locked NuGet restores and source mapping across local runs, Docker builds, and CI (`P8-D4`).

25. **`S8-25` (`must`)** | **Given** `Dockerfile`, `compose.observability.yaml`, and `.github/dependabot.yml` | **When** their contents are checked | **Then**:
    - `Dockerfile` pins both `FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:[0-9a-f]{64}` and `FROM mcr.microsoft.com/dotnet/aspnet:10.0@sha256:[0-9a-f]{64}`;
    - `compose.observability.yaml` pins `image: mcr.microsoft.com/aspire/dashboard:13.6.2@sha256:[0-9a-f]{64}` (updating `ComposeFileTests.Observability_override_adds_a_pinned_loopback_dashboard_and_the_api_endpoint`);
    - `.github/dependabot.yml` configures `version: 2` and weekly updates for all 3 ecosystems: `nuget`, `docker`, and `github-actions`.
    | **Why it matters:** prevents floating tag drift (`sdk:10.0` analyzer band changes mid-PR) and ensures automated PRs for NuGet, Docker digests, and GitHub Actions (`P8-D5`).

---

## Ambiguities & implementation notes for Alessandro to settle

- **A1. Shared vs per-endpoint rate-limit bucket for authenticated writes (`P8-D1`):** Should `POST /api/applications` and `POST /api/applications/{id}/assessments` share a single per-`sub` fixed-window bucket (`PermitLimit = 60` total write operations per minute per owner, plus the separate `"dev-token"` partition for `POST /dev/token`), or have separate buckets per route? *(Assumed in `S8-16`: one shared `"write"` policy partitioned by `sub`, with `"dev-token"` as its own partition key.)*
- **A2. `UseHsts()` default `ExcludedHosts` vs `AllowedHosts = "localhost;127.0.0.1"` (`P8-D3`):** ASP.NET Core's `HstsMiddleware` excludes `localhost`, `127.0.0.1`, and `[::1]` by default (`HstsOptions.ExcludedHosts`), while `appsettings.json` sets `AllowedHosts = "localhost;127.0.0.1"`. That means an HTTPS request to `https://localhost/...` in `Production` will **not** emit `Strict-Transport-Security` unless either:
  1. `S8-19` configures `AllowedHosts = "localhost;127.0.0.1;example.com"` on that test host and sends the HTTPS request to `https://example.com/health` (keeping ASP.NET Core's default `ExcludedHosts` intact so `localhost` never sends HSTS), **or**
  2. `AddHsts(o => o.ExcludedHosts.Clear())` is called in `Program.cs`.
  *(Recommendation: Option 1 — keep ASP.NET Core's default `ExcludedHosts` so browsers on `localhost` aren't HSTS-pinned, and use `https://example.com/health` with `AllowedHosts` in `S8-19`.)*
- **A3. `Cache-Control: no-store` scope (`P8-D3`):** P8-D3 specifies `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, and `Referrer-Policy: no-referrer` on every response, and `Cache-Control: no-store` on `/api/*` and `/dev/token`. Should `Cache-Control: no-store` be applied only to `/api/*` and `/dev/token` (as written in P8-D3), or globally to every response? *(Test cases `S8-01`, `S8-03`, `S8-18` assert it on `/api/*` and `/dev/token` without forbidding it on `/health`.)*
