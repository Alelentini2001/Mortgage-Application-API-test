# Phase 07: Authentication + authorisation

Status: **In progress: plan approved (2026-11-15), test design next** · Branch: `phase-07-auth` · PR: — · Started: 2026-11-15 · Finished: —

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

Planning: lead agent only.

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

## What was built (files)

## Tests

### Proposal (`test-designer`, P07-03; waiting for Alessandro)

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

## Security gate

## Review gate

## AI output corrected or rejected (REAL cases only)

## Optimisations considered

## Open issues

- This session still has the agent definitions loaded before the H1 cut (H1 review R3). Restart Claude Code before the `test-designer` step.

## Time spent (approx.)

## Human review (Alessandro writes this; the agent leaves it empty)
