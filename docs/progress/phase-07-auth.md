# Phase 07: Authentication + authorisation

Status: **Merged** · Branch: `phase-07-auth` · PR: [#11](https://github.com/Alelentini2001/Mortgage-Application-API-test/pull/11) · Started: 2026-11-15 · Finished: 2026-11-16

## Goal

Replace the fixed development owner with real JWT bearer authentication, secure by default, as designed in `docs/layers/PRODUCTION-LAYERS.md` ("Auth & permissions") and `.claude/rules/security.md`:
- Every endpoint requires an authenticated caller, except `/health` and the dev token endpoint (fallback policy).
- `OwnerId` comes from the token's `sub` claim through `ICurrentUser`, never from the request. Another user's data stays a 404.
- A dev token endpoint for local runs, demos and CI that **fails closed**: registered only in `Development` with `DevAuth:Enabled=true`; startup throws if the flag is on in any other environment.
- Tests for 401 (no token, bad signature, expired, wrong issuer or audience, unsigned), cross-user 404, and the dev endpoint's gating.

Spec: "Authentication" is an optional enhancement (R31, today "missing"). Carried in: Phase 3 reviewer F6 (`RequireAuthorization`), Phase 1 security S1, S2, S7; ADR 0002 (compose exposes everything without auth until now).

Not in scope: a real identity provider (Entra ID / OIDC) beyond documenting how it plugs in (P7-D2), roles or admin users, rate limiting (Phase 8).

## Plan (approved at Checkpoint A)

Approved by Alessandro on 2026-11-15 (P07-02), with option A on every decision and the JwtBearer package (P7-D9 A).

1. Branch `phase-07-auth` from `main`.
2. Docs check (links below; more at implementation: `JsonWebTokenHandler`, `ECDsaSecurityKey`, OpenAPI document transformers for the security scheme, `IHttpContextAccessor` lifetime).
3. Package (needs approval, P7-D9): `Microsoft.AspNetCore.Authentication.JwtBearer` in `Directory.Packages.props`, Api project only.
4. `Program.cs` / `Identity/`: `AddAuthentication().AddJwtBearer(...)` with issuer, audience, lifetime and signature validated, the algorithm pinned (P7-D1), `MapInboundClaims = false`, a small `ClockSkew` (30 s); `AddAuthorizationBuilder().SetFallbackPolicy(RequireAuthenticatedUser)`; `.AllowAnonymous()` on `/health` and the dev token endpoint only. `ClaimsCurrentUser` (replaces `DevelopmentCurrentUser`) reads `sub`; a missing, empty or over-long (> 128, the column size) `sub` is rejected as 401 when the token is validated.
5. Options `Jwt` (issuer, audience, token lifetime) and `DevAuth` (enabled), validated at startup. Outside `Development`, startup fails if no validation key or authority is configured, and if `DevAuth:Enabled` is true.
6. Dev token endpoint `POST /dev/token` with `{ "subject": "alice" }` (pattern `^[a-z0-9-]{1,64}$`) returning `{ token, expiresAt }` (15 min). No password, no user store: it exists only to make tokens for local testing, as Learn allows ("Limit creating access tokens to test scenarios").
7. OpenAPI (Development): a bearer security scheme, so Scalar can send the token.
8. Compose: `DevAuth__Enabled: "true"` (compose runs as Development, ADR 0002). CI `docker` job: get a token from `/dev/token` first, then the existing POST/GET and 202 steps with it.
9. Tests: `ApiFactory` creates its own key and sends a token for a default test owner on every request, so existing tests keep their meaning; the existing `ICurrentUser` overrides become tokens for other subjects. New tests come from `test-designer` (step 10).
10. Test-first (`test-designer` → Alessandro approves → failing tests → implementation), verify, security gate, review gate, spec check, ADR 0004 (auth design), PR, Checkpoint B.

## Decisions for Alessandro (2-3 options · trade-offs · recommendation)

**P7-D1. Token signing.**
- **A. Asymmetric ES256 (ECDSA P-256), with a dev key pair generated in memory at startup when the dev endpoint is on** (recommended). Learn: "Asymmetric keys should **always** be used when creating access tokens." No key or secret in the repo or in config for dev; tokens die with the process (fine for dev and tests). Production validates with the identity provider's public keys (P7-D2).
- B. HS256 with a ≥ 256-bit shared key from configuration (the Phase 1 design). Simpler, but a shared secret has to be provisioned and kept out of the repo, and Learn advises against symmetric keys for token creation.
- C. `dotnet user-jwts` (SDK command): tokens from user secrets on the developer's machine. No endpoint, but it does not work in compose or CI without extra steps.

**P7-D2. Production identity provider.**
- **A. Document only (ADR 0004 + README): how `Jwt:Authority` / audience would point at Entra ID, owner keyed on `oid`+`tid`; no untested code path now** (recommended). Outside Development the app fails closed until it is configured.
- B. Add `Authority`-based validation now. Real code that no test can exercise without a tenant.

**P7-D3. Dev token subject.**
- **A. The caller picks the subject (`alice`, `bob`), validated by a strict pattern** (recommended): the demo and CI can show that another user's application is a 404.
- B. One fixed subject. Simpler, but cross-user behaviour cannot be demonstrated without code.

**P7-D4. How tests get tokens.**
- **A. `ApiFactory` owns a test key and mints tokens directly; separate tests cover the dev endpoint itself** (recommended): fast, no extra HTTP call per test, and the validation path is the real one.
- B. Every test calls `/dev/token`. Exercises the endpoint everywhere, but couples all tests to it.

**P7-D9. Package version.**
- **A. `Microsoft.AspNetCore.Authentication.JwtBearer` 10.0.15** (published 2026-11-10, 5 days ago) to match the other Microsoft packages and the installed runtime, as at Phase 3 (P3-D6) (recommended).
- B. 10.0.14 (2026-10-25), the newest at least two weeks old, one patch behind the rest.

## Decisions (option chosen · alternatives · reason · decided by) → ADR links

| # | Option chosen | Alternatives | Reason | Decided by |
|---|---|---|---|---|
| P7-D1 | A: ES256 (ECDSA P-256); dev key pair generated in memory when the dev endpoint is on | B HS256 shared key; C `dotnet user-jwts` | Learn: asymmetric keys for token creation; no secret in repo or config | Alessandro |
| P7-D2 | A: production identity provider documented only (ADR 0004, README); fail closed until configured | B authority-based validation now | No untested code path | Alessandro |
| P7-D3 | A: caller-chosen dev subject, strict pattern | B one fixed subject | Cross-user 404 can be demonstrated | Alessandro |
| P7-D4 | A: tests mint tokens with the factory's own key; separate tests for `/dev/token` | B every test calls `/dev/token` | Fast; real validation path | Alessandro |
| P7-D9 | A: `Microsoft.AspNetCore.Authentication.JwtBearer` 10.0.15 | B 10.0.14 | Matches the runtime and the other Microsoft packages (P3-D6 precedent) | Alessandro |

ADR 0004 records the auth design (written at the end of the phase).

## Docs verified (links)

Checked before planning, 2026-11-15:
- [Configure JWT bearer authentication (ASP.NET Core 10)](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/configure-jwt-bearer-authentication?view=aspnetcore-10.0): package `Microsoft.AspNetCore.Authentication.JwtBearer`; validate signature, issuer, audience and expiry, 401 otherwise ("MUST send a `WWW-Authenticate` header"); `MapInboundClaims = false`; `SetFallbackPolicy` with `RequireAuthenticatedUser` "to require an authenticated user"; "Asymmetric keys should **always** be used when creating access tokens"; "Never create an access token from a username/password request"; "Limit creating access tokens to test scenarios"; test tokens via `dotnet user-jwts`; "Don't deploy insecure security test flows to production"; 404 is allowed for not-authorised resources to avoid leaking existence.
- [Secure data / fallback policy](https://learn.microsoft.com/en-us/aspnet/core/security/authorization/secure-data?view=aspnetcore-10.0): "The fallback policy is selected when the authorization middleware can't build a policy from authorization metadata"; apply `AllowAnonymous` to intentionally public endpoints; the fallback policy "is the preferred way to require all users to be authenticated". Minimal-API specifics (`.AllowAnonymous()` on a route) to be checked at implementation.
- NuGet registration API, 2026-11-15: JwtBearer 10.0.14 (2026-10-25), 10.0.15 (2026-11-10).

## Orchestration used

- **Planning, test design, initial implementation and first gate pass (`e30de47`–`3320a82`):** run in Claude Code (lead: Claude Opus 5.5; `test-designer`: Sonnet 4.6; implementer and first gates: Opus 5.5).
- **Cross-IDE and cross-model portability check (`3320a82`–HEAD):** after `3320a82`, we intentionally switched IDE and AI model to **Google Antigravity IDE (Gemini 4 Argon)** to demonstrate that the repository's harness (`AGENTS.md`, `.claude/rules/`, `.claude/skills/`, `.claude/agents/`, and git/progress state) works without modification across different environments and model families. In Antigravity, the same `.claude/agents/` definitions (`security-auditor`, `dotnet-reviewer`, `spec-checker`) were defined and invoked in fresh contexts (P07-09 to P07-11), catching two additional real issues and three test gaps (`83b2bd3`, `8f86bce`) before opening the PR.

## Prompts

### P07-01 · human→agent · 2026-11-15
- **Prompt (verbatim):**
  > continue with phase 7
- **Purpose:** start Phase 7.
- **Outcome:** this plan; Checkpoint A.

### P07-02 · human→agent · 2026-11-15
- **Prompt (verbatim):**
  > Approved, all A
- **Purpose:** Checkpoint A approval (P7-D1 to P7-D4 and P7-D9 all option A, package approved).
- **Outcome:** decisions recorded; branch created; `test-designer` run (P07-03).

### P07-03 · agent→subagent (`test-designer`) · 2026-11-15
- **Prompt (verbatim):**
  ```
  Phase 7 (authentication + authorisation) of a .NET 10 mortgage-application API (Minimal APIs, EF Core SQLite, background assessment worker, OpenTelemetry). Propose test cases for the approved plan; do not write code. If your agent instructions mention scripts/context/brief.sh, CONTEXT-INDEX.md or a handover template, ignore that: those files were removed.

  Read first in /Users/ale_ssd/Developer/Mortgage-Application-API-test (branch phase-07-auth): docs/progress/phase-07-auth.md (goal, plan, decisions P7-D1 to P7-D4 and P7-D9, all option A), docs/layers/PRODUCTION-LAYERS.md ("Auth & permissions" row), .claude/rules/security.md, .claude/rules/tests.md, tests/CLAUDE.md, and the code and tests: src/MortgageApplications.Api/Program.cs, src/MortgageApplications.Api/Identity/*, src/MortgageApplications.Api/Applications/ApplicationEndpoints.cs, tests/MortgageApplications.IntegrationTests/ (ApiFactory, Http, NotFoundAndOwnerTests, StartupTests, ErrorHandlingTests, ObservabilityLogTests, TelemetryAllowlistTests), compose.yaml, .github/workflows/ci.yml.

  What will be built (not written yet): JWT bearer authentication with ES256 (ECDSA P-256), issuer, audience, lifetime and signature validated, algorithm pinned, MapInboundClaims = false, ClockSkew 30 s; fallback authorization policy RequireAuthenticatedUser with AllowAnonymous only on /health and POST /dev/token; ClaimsCurrentUser reading sub (missing, empty or longer than 128 characters rejected as 401); options Jwt (issuer, audience, lifetime) and DevAuth (enabled) validated at startup; outside Development startup fails if DevAuth:Enabled is true or no validation key or authority is configured; POST /dev/token {"subject":"alice"} (pattern ^[a-z0-9-]{1,64}$) returns {token, expiresAt} (15 min), registered only in Development with DevAuth:Enabled=true, signed with a key pair generated in memory at startup; an OpenAPI bearer security scheme in Development; compose sets DevAuth__Enabled=true; the CI docker job gets a token first; ApiFactory owns its own test key and sends a token for a default test owner on every request, and existing ICurrentUser overrides become tokens for other subjects.

  Cover at least: 401 with no token, a malformed token, a bad signature, a token signed with another key, expired (just past the 30 s skew and just inside it), not yet valid, wrong issuer, wrong audience, alg none / unsigned, HS256 with the public key as secret (algorithm confusion), a token without sub, an empty sub, a 129-character sub (and 128 accepted); 401 responses are ProblemDetails with a WWW-Authenticate header and leak no token or exception text; every application endpoint requires auth (enumerate them from the endpoint data source so a new endpoint cannot slip through) and /health does not; cross-user: another subject's application and assessment are 404 with the same body as an unknown ID, and nothing is created or queued for them; owner comes only from sub (a body or header claiming another owner has no effect); dev endpoint: 404 in Production and Staging even with the flag, 404 in Development without the flag, works in Development with the flag, subject pattern boundaries (1 and 64 characters, 65, uppercase, symbols, empty, missing, extra properties), tokens it issues are accepted, expiresAt about 15 minutes, a token from one app instance is rejected by another (in-memory key); startup fails when DevAuth:Enabled=true outside Development and when nothing to validate tokens is configured outside Development; the background worker still processes items (it has no user); telemetry and logs never contain the token, the sub value (owner ID) or the Authorization header (extend the Phase 5 allowlist tests); the OpenAPI document declares the bearer scheme in Development; existing tests keep their meaning. Say which are unit vs integration tests, how tests create tokens without new packages (the JwtBearer package brings Microsoft.IdentityModel.JsonWebTokens transitively through the Api project reference), how to control time for expiry tests without sleeping, and which existing tests must change.

  Use your agent's output format. Mark each case must or nice, keep the list tight, and list ambiguities in the plan for Alessandro to settle.
  ```
- **Purpose:** propose Phase 7 test cases before any test is written.
- **Outcome:** about 70 cases (AU01–AU112) and 14 questions (A1–A14); full text in [`phase-07/test-proposal.md`](phase-07/test-proposal.md).

### P07-04 · human→agent · 2026-11-15
- **Prompt (verbatim):**
  > Approved
- **Purpose:** approval of the recommended test selection and the answers A1–A14.
- **Outcome:** implementation delegated, test-first (P07-05).

### P07-05 to P07-07 · agent→subagent (implementer and first gates in Claude Code) · 2026-11-15 to 2026-11-16
- **Prompt:** not recorded verbatim before the session switch from Claude Code to Antigravity IDE (honesty rule: not reconstructed).
- **Purpose:** implement Phase 7 test-first (`9b259ee`–`cf6dad0`), draft ADR 0004 (`5430b17`), run the first security and review gates (`G1`–`G10`), and fix `G1`–`G10` test-first (`828573e`, `7b681a5`, `64b90e0`, `e0ba9d2`, `3320a82`).
- **Outcome:** 537 tests passing at `3320a82`; see "What was built" and "Tests > Results after the gates".

### P07-08 · human→agent (Antigravity IDE · Gemini 4 Argon) · 2026-11-16
- **Prompt (verbatim):**
  > ok so continue where it stopped, and when tracking down tasks and what we did, state that we tried also to switch to different AI models and IDEs to showcase that these skills, agents and instructions can be used seamessly in different environments
- **Purpose:** resume Phase 7 from `3320a82` in Antigravity IDE (Gemini 4 Argon), run the `.claude/agents/` gate subagents, fix any findings, update phase and progress records, and open the PR.
- **Outcome:** `security-auditor` (P07-09), `dotnet-reviewer` (P07-10) and `spec-checker` (P07-11) defined from `.claude/agents/*.md` and run; 5 findings from P07-10 fixed test-first (`83b2bd3`, `8f86bce`).

### P07-09 · agent→subagent (`security-auditor` in Antigravity · Gemini 4 Argon) · 2026-11-16
- **Prompt (verbatim):**
  ```
  Security gate for Phase 7 (authentication + authorisation) of a .NET 10 mortgage-application API in /Users/ale_ssd/Developer/Mortgage-Application-API-test, branch phase-07-auth (HEAD 3320a82). Read only: do not edit files or switch branches.

  Read first: .claude/rules/security.md, AGENTS.md, docs/progress/phase-07-auth.md (goal, plan, decisions P7-D1 to P7-D4 and P7-D9, approved answers A1-A14, "What was built", "Tests > Results after the gates", "Open issues"), docs/adr/0004-authentication-and-authorisation.md.

  Do:
  1. Run `gitleaks git --no-banner --redact -v` (or `--log-opts="main..HEAD"`) and `gitleaks dir --no-banner --redact .`
  2. Run `semgrep scan --config p/csharp --config p/secrets --error --quiet` (and with `--baseline-commit main`).
  3. Run `dotnet list MortgageApplications.slnx package --vulnerable --include-transitive`.
  4. Review the diff (`git diff main...HEAD`) against OWASP Top 10:2025 and the Phase 7 security requirements:
     - ES256 (ECDSA P-256) pinned via `ValidAlgorithms`, `RequireSignedTokens`, `RequireExpirationTime`, `ClockSkew` 30 s, `MapInboundClaims = false`, `IncludeErrorDetails = false`.
     - `Authority` and `MetadataAddress` cleared so `Authentication:Schemes:Bearer:*` cannot add trusted keys; header `kid`, `jwk`, `jku` do not resolve keys.
     - `SigningPublicKey` accepts only a `PUBLIC KEY` PEM on P-256; outside Development startup fails if `Jwt:SigningPublicKey` is missing or if `DevAuth:Enabled` is true; startup also fails if `DevAuth:Enabled` is true while `Jwt:SigningPublicKey` is set.
     - `POST /dev/token` is mapped only in `Development` with `DevAuth:Enabled=true`, validates `subject` with `^[a-z0-9-]{1,64}\z`, issues 15-minute tokens with an in-memory key pair.
     - Fallback authorization policy `RequireAuthenticatedUser`; `AllowAnonymous` only on `/health`, `/dev/token` (when registered), and in Development OpenAPI and Scalar.
     - `SubjectClaim` / `ClaimsCurrentUser` requires a single JSON string `sub`, non-blank, <= 128 chars; owner comes only from `sub`; another user's application or assessment returns an identical 404.
     - No token, `Authorization` header, `sub` (owner ID), issuer or exception text leaks in logs, spans, metrics, ProblemDetails or `WWW-Authenticate`; `Microsoft.AspNetCore.Authentication` is at `Warning` and app-owned `TokenRejected` logs only `FailureType`.
     - CI `.github/workflows/ci.yml` masks the dev token (`::add-mask::`) and passes it via `@auth-header.txt`.

  Output:
  - Exact commands run and trimmed outputs.
  - A findings table: `tool/check | finding (file:line) | severity | real or false positive | reason | fix`.
  - A one-line verdict: `PASS` or `FAIL`.
  ```
- **Purpose:** security gate on `phase-07-auth` at `3320a82`.
- **Outcome:** **PASS**. Gitleaks (146 commits + working tree): 0 leaks; Semgrep (`p/csharp` + `p/secrets`, full and `--baseline-commit main`): 0 findings; `dotnet list package --vulnerable --include-transitive`: 0 vulnerable packages; OWASP Top 10:2025: 1 low/info item (`FailureType` log key not yet in `TelemetryAllowlistTests._logKeys`).

### P07-10 · agent→subagent (`dotnet-reviewer` in Antigravity · Gemini 4 Argon) · 2026-11-16
- **Prompt (verbatim):**
  ```
  Fresh-context diff review of Phase 7 (authentication + authorisation) in /Users/ale_ssd/Developer/Mortgage-Application-API-test, branch phase-07-auth (HEAD 3320a82). Read only: do not edit files, do not build or run tests.

  Get the diff with `git diff main...HEAD`. Read the touched files in full (`src/MortgageApplications.Api/Program.cs`, `src/MortgageApplications.Api/Identity/*`, `src/MortgageApplications.Api/appsettings.json`, `src/MortgageApplications.Api/Properties/launchSettings.json`, `src/MortgageApplications.Api/requests.http`, `compose.yaml`, `.github/workflows/ci.yml`, `Directory.Packages.props`, `src/MortgageApplications.Api/MortgageApplications.Api.csproj`, `tests/MortgageApplications.IntegrationTests/*`), plus `AGENTS.md`, `.claude/rules/backend.md`, `.claude/rules/security.md`, `.claude/rules/tests.md`, `tests/CLAUDE.md`, `docs/progress/phase-07-auth.md` (plan, decisions, approved test cases and answers A1-A14, "Tests > Results after the gates", "Open issues"), and `docs/adr/0004-authentication-and-authorisation.md`.

  Check in priority order:
  1. Security & auth correctness: ES256 validation, key handling (`TokenKeys`, `SigningPublicKey`, `CryptoProviderFactory` lifetime and disposal), `SubjectClaim` JSON payload parsing and claim validation, `ClaimsCurrentUser` lifetime and fallback behaviour, `DevTokenEndpoint` / `DevTokenIssuer` gating and Startup validation (`JwtOptionsValidator`), `OnChallenge` header formatting, OpenAPI `BearerSecurityTransformer`.
  2. Framework usage: DI lifetimes (`TokenKeys` singleton vs options validation, `IHttpContextAccessor` + scoped `ICurrentUser`), middleware order (`UseExceptionHandler`, `UseStatusCodePages`, `UseAuthentication`, `UseAuthorization`), endpoint metadata.
  3. Test gaps: whether the tests in `AuthenticationTests`, `AuthorizationTests`, `DevTokenTests`, `AuthTelemetryTests`, `StartupTests`, `LocalRunFileTests`, `ComposeFileTests` can fail when the behaviour under test breaks, and whether any approved must-case in `docs/progress/phase-07-auth.md` is missing or weak.
  4. Over-engineering or dead code.

  Output only a list, most severe first, one finding per line:
  `file:line | severity (critical/high/medium/low) | why it is wrong | concrete fix`
  If you find nothing real, output `No findings.` and the list of files you reviewed. State plainly that you did not build or run tests.
  ```
- **Purpose:** second fresh-context review of `phase-07-auth` at `3320a82`.
- **Outcome:** 1 medium and 4 low findings (R2-1 to R2-5), all fixed test-first in `83b2bd3` and `8f86bce`. See "Review gate".

### P07-11 · agent→subagent (`spec-checker` in Antigravity · Gemini 4 Argon) · 2026-11-16
- **Prompt (verbatim):**
  ```
  Requirement traceability check at the end of Phase 7 (authentication + authorisation) in /Users/ale_ssd/Developer/Mortgage-Application-API-test, branch phase-07-auth (HEAD 3320a82). Read only: do not edit files.

  Read:
  - `docs/spec/ORIGINAL_SPEC.md` and `docs/spec/affordability-rules.md`
  - `docs/PROGRESS.md` (the existing R1-R55 traceability table)
  - `docs/progress/phase-07-auth.md` (goal, plan, decisions, what was built, tests, open issues)
  - `docs/adr/0004-authentication-and-authorisation.md`
  - The Phase 7 code and tests under `src/MortgageApplications.Api/` and `tests/MortgageApplications.IntegrationTests/`

  Do:
  1. Check every Phase 7 goal and approved requirement (R31 optional authentication, owner scoping from `sub`, 401 on missing/invalid tokens with `WWW-Authenticate` and ProblemDetails, cross-user 404, fail-closed `POST /dev/token`, no token or `sub` in logs/spans/metrics, OpenAPI bearer scheme in Development, Docker compose + CI smoke test with dev token) and map each to the file:line and test name that meet it.
  2. Check how the R1-R55 table in `docs/PROGRESS.md` changes after Phase 7 (specifically R31 and the overall met / partial / missing totals, plus whether R6/section 6 of `docs/spec/affordability-rules.md` is affected by the new `TokenRejected` log event's `FailureType` field).
  3. Output a Markdown table for the Phase 7 requirements and the updated R1-R55 summary counts, plus at most 5 short notes on any open items or discrepancies.
  ```
- **Purpose:** requirement traceability at the end of Phase 7.
- **Outcome:** R31 is **met** (all four Optional Enhancements R30–R33 are now met); overall totals move to **30 met · 10 partial · 15 missing**. See "Review gate".

### P07-12 · human→agent (Antigravity IDE · Gemini 4 Argon) · 2026-11-16
- **Prompt (verbatim):**
  > Proceed with option a for both of them and continue
- **Purpose:** Checkpoint B approval on PR #11 (Option A for anonymous unknown paths returning `401`, Option A for adding `FailureType` to rules §6 and `TelemetryAllowlistTests._logKeys`, ADR 0004 Accepted, merge PR #11 and continue to Phase 8).
- **Outcome:** `FailureType` added to `docs/spec/affordability-rules.md` §6 and `TelemetryAllowlistTests._logKeys`, ADR 0004 marked `Accepted`, PR #11 squash-merged.

## What was built (files)

Implementation delegated to a subagent (P07-05). Commits: `9b259ee` (package), `151fa7f` (failing tests), `b28b2c1` (implementation), `0b996c4` (three test expectations corrected), `1d0944f` (compose and CI).

- `Directory.Packages.props`, `src/MortgageApplications.Api/MortgageApplications.Api.csproj`: `Microsoft.AspNetCore.Authentication.JwtBearer` 10.0.15, Api project only (P7-D9 A).
- `src/MortgageApplications.Api/Identity/`:
  - `AuthenticationSetup.cs`: options, `AddAuthentication().AddJwtBearer()`, validation parameters (issuer, audience, lifetime, signature, `ValidAlgorithms` = ES256, `RequireSignedTokens`, `RequireExpirationTime`, `ClockSkew` 30 s, `MapInboundClaims = false`, `IncludeErrorDetails = false`), `OnTokenValidated` (rejects a missing, repeated, blank or > 128-character `sub`), `OnChallenge` (sets `error="invalid_token"` on a rejected token, no description), fallback policy `RequireAuthenticatedUser`.
  - `JwtOptions.cs`, `DevAuthOptions.cs`, `JwtOptionsValidator.cs` (both validators; `ValidateOnStart`, messages name the setting only), `SigningPublicKey.cs` (accepts only a `PUBLIC KEY` PEM on P-256).
  - `TokenKeys.cs`: validation keys (configured key and, when the dev endpoint is on, an in-memory P-256 pair); each key has its own uncached `CryptoProviderFactory` (see Tests > Results, "Bug found going green").
  - `DevTokenEndpoint.cs` (`POST /dev/token`, `DevTokenRequest` with `[Required]` + `^[a-z0-9-]{1,64}\z`, `DevTokenResponse`), `DevTokenIssuer.cs` (ES256, `sub`, `iss`, `aud`, `iat`, `nbf`, `exp`, `jti`; whole seconds so `expiresAt` equals `exp`).
  - `ClaimsCurrentUser.cs`, `SubjectClaim.cs`; `ICurrentUser.cs` comment updated; `DevelopmentCurrentUser.cs` deleted.
  - `BearerSecurityTransformer.cs`: document transformer (http bearer JWT scheme) and operation transformer (requirement on every operation without `IAllowAnonymous`), from [Customize OpenAPI documents](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/openapi/customize-openapi?view=aspnetcore-10.0) (checked 2026-11-16: `BearerSecuritySchemeTransformer`, "Conditionally applying security requirements", `Document` on the operation transformer context in .NET 10).
- `src/MortgageApplications.Api/Program.cs`: `AddJwtAuthentication()`, transformers, `UseAuthentication`/`UseAuthorization` after `UseStatusCodePages` (so 401s get the ProblemDetails body), `AllowAnonymous` on `/health` and, in Development, OpenAPI and Scalar; `/dev/token` mapped only in Development with `DevAuth:Enabled`.
- `src/MortgageApplications.Api/appsettings.json`: `Jwt` (issuer, audience, 15 min), `DevAuth:Enabled=false`, and `Microsoft.AspNetCore.Authentication` at `Warning` (A12, see AU92).
- `docs/layers/PRODUCTION-LAYERS.md`: "Auth & permissions" row now describes what was built (the old text said "until Phase 7" and HS256).
- `compose.yaml`: `DevAuth__Enabled: "true"`; port comment rewritten (A13); bind unchanged, `127.0.0.1:8080:8080`.
- `.github/workflows/ci.yml` (docker job): new step "Anonymous POST /api/applications returns 401" (checks `WWW-Authenticate: Bearer`); new step "Get a dev token" (`::add-mask::`, token written to a header file and sent with `curl -H @auth-header.txt`, never printed); the POST, GET, 202 and polling calls send it.
- Tests (`tests/MortgageApplications.IntegrationTests/`): `TestTokens.cs` (new), `AuthenticationTests.cs`, `AuthorizationTests.cs`, `DevTokenTests.cs`, `AuthTelemetryTests.cs` (new); `ApiFactory.cs`, `StartupTests.cs`, `ComposeFileTests.cs`, `NotFoundAndOwnerTests.cs`, `CreateApplicationTests.cs`, `AssessmentWorkerTests.cs`, `ObservabilityLogTests.cs`, `TelemetryAllowlistTests.cs` changed (list below).

## Tests

### Proposal (`test-designer`, P07-03; approved by Alessandro, P07-04)

Full proposal, verbatim: [`phase-07/test-proposal.md`](phase-07/test-proposal.md). All are integration tests (`tests/CLAUDE.md`: unit tests cover Core only; the auth code is in the Api project). Tokens are made in tests with `Microsoft.IdentityModel.JsonWebTokens` (comes with the approved package) or by hand for the malformed ones; no new test package.

**Recommended selection:** the designer's priority list (AU01, AU03, AU04, AU06, AU10, AU11, AU13–AU16, AU18, AU20, AU21, AU23, AU24, AU26, AU27, AU29, AU31, AU32, AU40, AU42, AU50, AU51, AU54, AU60, AU61, AU64, AU67, AU71, AU80, AU82, AU90, AU92, AU100, AU110) plus AU02, AU28 (whitespace sub), AU52, AU65, AU66, AU68, AU69, AU83, AU85, AU87 and AU101; AU111 (CI with a token) checked by the CI run itself.

**Questions and the lead agent's recommended answers:**
- **A1** Key seam: config `Jwt:SigningPublicKey` (PEM) for validation. Tests set the factory's own public key; production supplies the identity provider's key (or authority later, P7-D2). With `DevAuth` on and no key configured, the app generates its own key pair in memory and validates with it, so AU61 and AU71 use the app's real key.
- **A2** Time: real clock, no new seam; "just inside" and "just outside" the 30 s skew are tested at 25 s and 35 s (5 s slack). The exact 30.0 s edge is not tested.
- **A3** `/openapi/v1.json` and `/scalar` are anonymous in Development only (they are mapped only there and hold no data); AU41's allowlist includes them for Development.
- **A4** Whitespace-only `sub` → 401.
- **A5** `IncludeErrorDetails = false`: the `WWW-Authenticate` header stays `Bearer` with `error="invalid_token"` but no `error_description`, so no exception text leaves the API.
- **A6** All integration tests; the unit-test project keeps its Core-only boundary.
- **A7** A body with `ownerId` is a 400 (strict JSON) and creates nothing; the owner can only come from `sub`.
- **A8** The subject check matches the whole string (`\z` or a character check), so `"alice\n"` is a 400.
- **A9** Startup failures are `OptionsValidationException` through `ValidateOnStart`, like the existing options.
- **A10** Development with no key and `DevAuth` off starts, and every protected endpoint is 401.
- **A11** `Jwt:TokenLifetimeMinutes`, 1 to 60, default 15.
- **A12** Keep IdentityModel's PII hiding on (its default); AU92 checks the logs with sentinel issuer, audience and subject values. If anything leaks, raise the `Microsoft.AspNetCore.Authentication` log level to `Warning`.
- **A13** ADR 0004 states that the compose demo lets anyone on the machine mint a token for any subject, which is acceptable only because the port is bound to `127.0.0.1`.
- **A14** Production-host tests configure `Jwt:SigningPublicKey` with a test public key.

### Results

**Red** (commit `151fa7f`: package and tests only, `DevelopmentCurrentUser` still in place; `dotnet test --project tests/MortgageApplications.IntegrationTests -c Release --no-build`): 242 total, **67 failed**, 175 passed. Saved locally (scratchpad `p7-red.txt`, not in the repo).
- 64 of the 74 new test cases failed, as expected: no token is checked, no `/dev/token`, no `Jwt`/`DevAuth` validation, no bearer scheme, compose unchanged.
- 10 new cases passed before any auth code, because they describe behaviour that already held without auth (they guard against regressions, not new behaviour): AU02, AU03, AU06, AU16 (any token is ignored), AU64 Staging and Production and AU65 (`/dev/token` does not exist yet), AU101 x3 (Development-only paths are 404 like any unknown path).
- 3 existing tests failed, as expected: I35, I-5 and O21 now use a token for another subject instead of an `ICurrentUser` override, and the app still ignored tokens, so the "other owner" was the same owner.

**Going green.** First run after the implementation: 16 failures, then 3, then 2, then 0. What changed on the way:
- **Bug found going green (implementation).** Valid tokens were rejected with 401 on some hosts when tests ran together, never alone. Cause: IdentityModel's default `CryptoProviderFactory` is static and caches signature providers by key thumbprint; several hosts of one factory import the same public key, and a disposed host's `TokenKeys` disposed the `ECDsa` the cache still used. Fix: each key gets its own `CryptoProviderFactory` with `CacheSignatureProviders = false` (`TokenKeys.cs`).
- **Three test expectations corrected** (commit `0b996c4`; each now pins verified framework behaviour, none was relaxed to hide an app bug):
  - AU40: the allowlist was written before the framework's real endpoints were known. Scalar also maps `GET /scalar/favicon.svg`, `/scalar/scalar.js` and `/scalar/scalar.aspnetcore.js` (Development only, A3), and the create route's raw pattern is `POST /api/applications/` (group prefix plus `""`).
  - AU68 (wrong content type): an anonymous `text/plain` POST to `/dev/token` is a 401, not 400/415. Routing sends a content type the endpoint does not accept to the framework's 415 endpoint, which is not `AllowAnonymous`, so the fallback policy answers first. No token is issued. An authenticated caller still gets 415 (JsonStrictnessTests I26).
  - AU92 (A12): with `Microsoft.AspNetCore.Authentication` at Debug, JwtBearer's Information log "Failed to validate the token." carried IdentityModel's exception text with the token's issuer in clear (`IDX10205: Issuer validation failed. Issuer: 'sentinel-issuer-41'`), even with PII hiding at its default. As A12 says, `Microsoft.AspNetCore.Authentication` is now pinned at `Warning` in `appsettings.json`, and the test checks the configured levels, with the Debug host as a control that the framework does log these failures.
- A compose test (AU110) stayed red until `compose.yaml` changed (commit `1d0944f`).

**Green.** `./scripts/verify.sh` (full), after commit `1d0944f`:
```
==> restore
==> build (Release, warnings as errors)
    0 Warning(s)
    0 Error(s)
==> all tests
  total: 516
  failed: 0
  succeeded: 516
  skipped: 0
==> hook regression tests
hook tests: 91 passed, 0 failed
==> format check
VERIFY PASSED (full)
```
Integration tests 3 more times (`dotnet test --project tests/MortgageApplications.IntegrationTests -c Release --no-build`): run 1: 242 total, 0 failed, 14.7 s; run 2: 242, 0 failed, 14.9 s; run 3: 242, 0 failed, 16.0 s.

**Local docker run** of the CI docker job (run blocks extracted from `ci.yml` with ruby, each run with `bash -e`, `COMPOSE_PROJECT_NAME=p7-local`, then `docker compose down -v --rmi local`): all 7 steps passed: build and start; `/health` 200; container uid 1654 owns `/app/data`; anonymous POST 401 with `WWW-Authenticate: Bearer`; `POST /dev/token` returned a token; POST 201 then GET returned the same application; POST assessment 202 and it completed as Approved. Cleanup removed the container, image, network and volume. Locally the `::add-mask::` line prints the token (only the GitHub runner consumes it), so the saved local log has it redacted; masking on GitHub is checked only by a real CI run (AU111, not done here).

**Existing tests changed, and why** (meaning kept in each case):
- `ApiFactory`: owns an ECDSA P-256 key, sets `Jwt:SigningPublicKey` with `UseSetting`, and every client (also from `WithWebHostBuilder` hosts and `CreateDefaultClient(handlers)`) sends a token for `ApiFactory.DefaultOwner`; `CreateAnonymousClient()`, `TokenFor(subject)`, `Sign(spec)` added.
- `NotFoundAndOwnerTests` I35 (= AU50) and I-5 (= AU51): the other owner is a token for `other-owner` on a host without the worker (as before), not an `ICurrentUser` override. I37: owner is `ApiFactory.DefaultOwner`.
- `ObservabilityLogTests` O21: the foreign POST uses a token for `other-owner` on the default host instead of a second host with an `ICurrentUser` override. O6: owner is `ApiFactory.DefaultOwner`.
- `CreateApplicationTests` (I3 + I37), `AssessmentWorkerTests` (the failed-save and failing-item tests), `TelemetryAllowlistTests` O5: owner is `ApiFactory.DefaultOwner`. Reason for all owner lookups: `ICurrentUser` is now scoped to a request and cannot be resolved from the root provider.
- `StartupTests` I43: the `Jwt` settings are put back with the connection string, so the failure still comes only from the missing Affordability section (review F4).
- `ComposeFileTests`: AU110 added; existing O28 tests unchanged.

**Cases implemented** (ID in a comment above each): AU01–AU04, AU06, AU10, AU11, AU13–AU16, AU18, AU20, AU21, AU23, AU24 (DER and PEM secrets), AU26–AU29, AU31 and AU32 (checked on every bad-token case), AU40, AU42, AU50, AU51, AU52, AU54, AU60, AU61, AU64 (Staging, Production), AU65 (plus a test for A10), AU66–AU69, AU71, AU80 (Production; Staging = AU81), AU82, AU83, AU85 (including an invalid PEM), AU87, AU90, AU92, AU100, AU101, AU110. AU111 is the CI run itself (not done here).

### Results after the gates

The ten gate findings were fixed test-first on the branch (commits `828573e` tests, `7b681a5` and `64b90e0` fixes, `e0ba9d2` ADR 0004 and production layers). How each was proved (red run and mutation run saved locally in the scratchpad as `p7-gates-red.txt` and `p7-gates-mutation.txt`, not in the repo):

- **Red first** (`828573e`, before any app change: 263 integration tests, 6 failed): G1 launch profiles (`Development_launch_profiles_enable_dev_auth`) and requests.http (`Requests_file_sends_a_token_on_every_api_request`); G2 dev auth with a configured key (`Startup_with_dev_auth_and_a_signing_key_fails`, no exception was thrown); G3 bearer `Authority` setting (`Bearer_scheme_authority_setting_is_ignored`, `Authority` was the configured URL); G5 numeric `sub` (created, 201); G10 `TokenRejected` event (none written).
- **Temporary mutation, then reverted** (all at once on the pre-fix code; `git checkout` restored the files): `ClockSkew` 31 s, `RequireExpirationTime = false`, `ValidAlgorithms` removed, the token read from `?access_token=`, the single-`sub` check relaxed to "at least one", `SigningPublicKey` reading any PEM with `ImportFromPem` and no curve check, and AU40 sending `not-a-guid` as the route values. 11 of 74 tests in the three classes failed. Caught: the options guard (G4); "no exp claim" (G4); the query-string token (G4); P-384 and both private-key PEMs (G6); AU40's new endpoint check (G7: an unmatched path is a 401 too, so the old check would have passed). The other failures were the G2, G3 and G5-numeric red cases above.
- **Passed first, and no mutation changed them**, recorded as they are: the ES384 header (with `ValidAlgorithms` removed it is still a 401, because the ES256 signature does not verify as ES384); the two-value `sub` array and the object `sub` (IdentityModel rejects them when it reads the token, before our check; relaxing our count check changed nothing); the RSA public PEM (cannot be imported as ECDSA either way); another key with an unknown `kid`, its own `jwk` or a `jku` (IdentityModel does not use these headers to find keys; no mutation was tried, as it would mean writing a key resolver); a trusted-key token with an unknown `kid` is accepted (pinned: `kid` is not a trust anchor); lowercase `bearer` is accepted (the handler matches the scheme name case-insensitively); the AU92 control now asserts the Debug host's logs contain `sentinel-issuer-41`, which they do, so the main assertion can fail (G9).
- **G8** (AU16 robustness, no behaviour change): a warm-up request before the near-expiry token, and margins of 15 s inside and 45 s outside the 30 s skew (AU15 and AU16). There is no nbf pair (only AU18, 5 minutes ahead), so nothing else changed.
- **Fix found going green:** the `ClaimValueTypes.String` check alone did not reject a numeric `sub`, because IdentityModel turns `12345` into a claim of type String. `SubjectClaim` now also checks that `sub` is a JSON string in the token's payload.
- **Verify** (`./scripts/verify.sh`, full, after `e0ba9d2`): build 0 warnings, 0 errors; 537 tests, 0 failed; hook tests 91 passed; format check passed; `VERIFY PASSED (full)`.
- **Integration tests 3 times:** 263 total, 0 failed in each run (15.8 s, 16.1 s, 15.9 s).
- Not done: `dotnet run` with the launch profile and requests.http were not run by hand (the file tests check the settings; the compose run already showed `/dev/token` working in Development); the docker smoke test was not re-run after these fixes (compose has no `Jwt:SigningPublicKey`, so the new startup rule does not apply to it).
- **Second review pass in Antigravity IDE (`83b2bd3` tests, `8f86bce` fixes):**
  - **Red first** (`83b2bd3`, before `8f86bce`): in the 5 filtered integration test classes (110 tests), **2 failed**: `ApiRecordsTests.Dev_token_records_ToString_hide_the_subject_and_token` (`DevTokenRequest { Subject = 123456 }`) and `AuthenticationTests.Post_with_bad_token_returns_401_problem_and_leaks_nothing(badToken: "G5 duplicate sub properties")` (`Expected: Unauthorized, Actual: Created`). The 3 new guard tests (`TokenRejected` for `InvalidSubject`, `Jwt:TokenLifetimeMinutes` limits `1` and `60`, and `/dev/token` log check) passed.
  - **Green** (`./scripts/verify.sh`, full, after `8f86bce`): build 0 warnings, 0 errors; **542 tests** (274 unit + 268 integration), 0 failed; hook tests 91 passed on the clean tree; format check passed; `VERIFY PASSED (full)`.

## Security gate

1. **First security gate (Claude Code, before `828573e`):** raised G2 (`DevAuth:Enabled` + `Jwt:SigningPublicKey` together), G3 (`Authentication:Schemes:Bearer` `Authority`/`MetadataAddress`), G4 (options guard, no `exp`, header `alg`/`kid`/`jwk`/`jku`, query-string token), G5 (non-string or repeated `sub`), G6 (key curve/type check in `SigningPublicKey`), G9 (AU92 control assertion), and G10 (app-owned `TokenRejected` log event). All fixed test-first in `828573e` and `64b90e0`.
2. **Full `security-auditor` run (P07-09, Antigravity IDE · Gemini 4 Argon, at `3320a82`):** **Verdict: PASS.**
   - `gitleaks git --no-banner --redact -v` (146 commits) and `gitleaks dir --no-banner --redact .`: 0 leaks.
   - `semgrep scan --config p/csharp --config p/secrets --error --quiet` (full and `--baseline-commit main`): 0 findings.
   - `dotnet list MortgageApplications.slnx package --vulnerable --include-transitive`: 0 vulnerable packages (`Microsoft.AspNetCore.Authentication.JwtBearer` `10.0.15` + transitive dependencies).
   - Manual OWASP Top 10:2025 review of `git diff main...HEAD`:

| Check | Finding | Severity | Status |
|---|---|---|---|
| A01–A08, A10 | None: fallback `RequireAuthenticatedUser`, owner from `sub` only, identical 404 across owners, ES256 P-256 pinned, `Authority`/`MetadataAddress` cleared, `/dev/token` gated to Development + `DevAuth:Enabled=true`, CI token masked with `::add-mask::`. | None | Pass |
| A09 Logging & Alerting | `TelemetryAllowlistTests.cs:22-23`: `TokenRejected` logs `FailureType` (an exception class name or `InvalidSubject`, no PII or token), which is not yet listed in rules §6 or `_logKeys`. | Low (Info) | Open for Alessandro (see "Open issues") |

## Review gate

1. **First review gate (Claude Code, before `828573e`):** raised G1 (`launchSettings.json` and `requests.http` broke after auth was added), G7 (AU40 needed `EndpointRecorder` to prove routes matched rather than 401ing as unknown paths), and G8 (AU16 warm-up and 15 s / 45 s skew margins). All fixed test-first in `828573e` and `7b681a5`.
2. **Second fresh-context `dotnet-reviewer` run (P07-10, Antigravity IDE · Gemini 4 Argon, at `3320a82`, read-only):** caught 1 medium and 4 low findings, all fixed test-first in `83b2bd3` and `8f86bce`:

| # | Finding | Severity | Status |
|---|---|---|---|
| R2-1 | `DevTokenEndpoint.cs:28,37`: `DevTokenRequest` and `DevTokenResponse` did not override `ToString()`, so record formatting printed `Subject` (owner ID) and the raw JWT `Token` in clear, unlike the other API records. | Medium | Fixed, `8f86bce` (`ToString()` redacted on both records) + `ApiRecordsTests.Dev_token_records_ToString_hide_the_subject_and_token` (`83b2bd3`, failed red first). |
| R2-2 | `SubjectClaim.cs:40` & `AuthenticationTests.cs:295`: `TryGetProperty("sub")` inspected only the last `"sub"` property in the JSON payload, and `JsonWebToken` overwrote earlier `"sub"` claims, so a payload with duplicate `"sub"` properties (`{"sub":"sentinel-owner-7","sub":"bob"}`) bypassed `SubjectClaim` and returned `201 Created`. | Low (real bug) | Fixed, `8f86bce` (`SubjectClaim.IsJsonString` requires `EnumerateObject()` to have exactly one `"sub"` property) + `"G5 duplicate sub properties"` in `AuthenticationTests` (`83b2bd3`, failed red first with `201 Created`). |
| R2-3 | `AuthTelemetryTests.cs:90`: `Rejected_token_writes_a_token_rejected_event_with_the_failure_type_only` tested only `OnAuthenticationFailed` (tampered signature), leaving `TokenRejected(context.HttpContext, "InvalidSubject")` in `OnTokenValidated` (`AuthenticationSetup.cs:70`) untested. | Low | Fixed, `83b2bd3`: parameterized as a `[Theory]` covering both `"tampered signature"` and `"invalid subject"` (`FailureType == "InvalidSubject"`). |
| R2-4 | `StartupTests.cs:125`: `Jwt:TokenLifetimeMinutes` was tested at `0`, `-1`, `15` and `61`, but not at the exact valid limits `1` and `60` (`JwtOptionsValidator.cs:24`). | Low | Fixed, `83b2bd3`: `Startup_with_token_lifetime_at_a_limit_starts` (`"1"` and `"60"`). |
| R2-5 | `DevTokenTests.cs:21`: no test checked `factory.Logs.Entries` after calling `POST /dev/token` to ensure neither the requested subject nor the issued token was logged. | Low | Fixed, `83b2bd3`: `Dev_token_returns_an_es256_token_for_the_subject` asserts `factory.Logs.Entries` holds neither `"alice"` nor the token or its segments. |

3. **`spec-checker` (P07-11, Antigravity IDE · Gemini 4 Argon, at `3320a82`, read-only):**
   - **R31 (`Optional: authentication`):** `missing` → **met** (all four Optional Enhancements R30–R33 are now met).
   - **Totals across R1–R55:** **30 met · 10 partial · 15 missing** (up from 29 / 10 / 16).
   - All 10 Phase 7 goals and requirements are met (P7-7 and P7-10 partial only on the open `FailureType` allowlist decision for Alessandro and the Phase 10 README section).

## AI output corrected or rejected (REAL cases only)

- **Implementer (`b28b2c1`):** IdentityModel's default static `CryptoProviderFactory` cached signature providers across hosts and used an `ECDsa` instance after its owning host disposed it, causing intermittent 401s when integration tests ran together. Fixed in `TokenKeys.cs` by giving each key its own `CryptoProviderFactory { CacheSignatureProviders = false }`.
- **Implementer (`b28b2c1` / `64b90e0`):** left `DevTokenRequest` and `DevTokenResponse` with compiler-generated `ToString()` (exposing `Subject` and `Token`) and used `JsonElement.TryGetProperty("sub")` in `SubjectClaim.IsJsonString`, which accepted a JWT payload with duplicate `"sub"` JSON properties (`201 Created`). Caught by the second fresh-context `dotnet-reviewer` pass (P07-10, Gemini 4 Argon in Antigravity) after the first Claude Opus gate pass missed both; fixed test-first in `83b2bd3` and `8f86bce`.
- **First gate pass (`828573e`):** wrote `"G5 array of two subs"` as a single JSON array property (rejected by IdentityModel before `OnTokenValidated`) instead of duplicate `"sub"` properties, and left the `"InvalidSubject"` branch of `TokenRejected` untested. Caught by `dotnet-reviewer` (P07-10) and fixed in `83b2bd3`.

## Optimisations considered

- **`CacheSignatureProviders = false` per `ECDsaSecurityKey` (`TokenKeys.cs`):** avoids static thumbprint cache collisions when multiple `WebApplicationFactory` hosts dispose their keys; negligible overhead for a single P-256 verification per request.
- **In-process token minting in `ApiFactory` (`P7-D4 A`):** `ApiFactory` signs tokens directly with its own P-256 key instead of calling `POST /dev/token` before every test, keeping the 268-test integration suite around 14–16 s while exercising the full `JwtBearerHandler` validation pipeline.

## Open issues

- This session still has the agent definitions loaded before the H1 cut (H1 review R3). Restart Claude Code before the `test-designer` step.
- **Resolved at Checkpoint B (P07-12, Option A):** anonymous unknown paths returning `401` under the fallback policy is accepted (reveals no routes; documented in ADR 0004).
- **Resolved at Checkpoint B (P07-12, Option A):** `FailureType` (from `TokenRejected`) added to `docs/spec/affordability-rules.md` section 6 and `TelemetryAllowlistTests._logKeys`, and O22 now also triggers a `TokenRejected` event.
- **CI (`AU111`):** both GitHub Actions jobs (`Build, test, format` and `Docker image build and smoke test`) passed on PR #11, with `0` occurrences of `eyJ` in the workflow logs (`::add-mask::` verified).
- **ADR 0004:** marked `Accepted` at Checkpoint B (P07-12).
- Settings under `Authentication:Schemes:Bearer:SigningKeys` (or other `TokenValidationParameters` bound by the framework) are replaced by the app's own parameters; only `Authority` and `MetadataAddress` are covered by a test (G3).
- Default token: `ApiFactory` sets it in `ConfigureClient` (the client's default Authorization header), not in a `DelegatingHandler`. `CreateDefaultClient` is not virtual, so a handler cannot be added to every client; `ConfigureClient` runs for every client, after any handlers a test passes, and a request's own Authorization header wins.
- If anyone lowers `Microsoft.AspNetCore.Authentication` below `Warning`, token issuer (and other IdentityModel non-PII values) reach the logs again (AU92 control).
- The `ci.yml` change took three edits, not one (the remaining `curl -H @auth-header.txt` additions were missed in the first edit).
- Commit `b28b2c1` alone does not pass every test: the corrected expectations are in `0b996c4` and AU110 passes only after `1d0944f`. The branch tip passes.
- Not done here (lead's later steps): README (Phase 10). Live GitHub Actions CI run (`AU111`, including that `::add-mask::` hides the token on GitHub) runs on PR #11. ADR 0004 was updated for the gate fixes (`e0ba9d2`); its status is Proposed until Checkpoint B approval.

## Time spent (approx.)

~1 h 40 min across two sessions (`2026-11-15 23:49 UTC` to `2026-11-16 01:28 UTC`, commits `e30de47`–`8f86bce`, including the IDE/model switch from Claude Code to Antigravity IDE and the second gate pass).

## Human review (Alessandro writes this; the agent leaves it empty)
