# Design journal

Chronological record of ideas, options, rejected ideas, who decided, and what changed and why.

## 2026-11-13 · Phase 0 planning

**First reading.** The brief asks for a small, clean ASP.NET 10 API with tests, and for AI to be used visibly to both code and review. So the reviewers will judge the code quality and the engineering process in equal measure. The harness (rules, hooks, reviewer agents, evidence files) is therefore part of the deliverable, not overhead.

**Environment findings that changed the plan.**
- Only the .NET 7 SDK was installed. The brew cask installed SDK 11 (there is no 10 cask). Options were the official pkg, the brew formula, or container-only builds. **Alessandro installed the official .NET 10 pkg.**
- The GitHub repo is private. **Alessandro decided** to keep it private while building and make it public in Phase 11, after a history scan.
- Semgrep and Gitleaks were missing. **Alessandro approved** a brew install. The Microsoft Learn MCP was **not** added (Alessandro's choice), so API checks use WebFetch on learn.microsoft.com.

**Harness decisions (D1–D9, proposed by the agent, approved by Alessandro on 2026-11-14).**
- D1: an initial commit on `main`, then one branch per phase, because a PR needs a base branch.
- D2: block every `.env*` edit, including `.env.example`. Config goes through appsettings and compose environment variables instead.
- D3: block force push only (`--force`, `-f`), not every `--force`. Otherwise harmless commands like `dotnet new --force` would be blocked too.
- D4: whitespace-only format on each edit (~1 s). The full `dotnet format --verify-no-changes` runs in `verify.sh`.
- D5: `.slnx` solution, the .NET 10 default.
- D6: central package management, so package versions are audited in one file.
- D7: AI review workflows are created now and skip until the API key secret exists. **Superseded at Checkpoint B:** Alessandro wants AI reviews run on his subscription, so the workflows were removed and the reviews run locally (`/code-review`, `/security-review`), posted as PR comments.
- D8: repo artefacts stay in normal English regardless of the chat style. `docs/PROGRESS.md` is the only progress tracker.
- D9: the official .NET 10 SDK pkg.

## 2026-11-14 · Phase 0 implementation

- **xUnit v3 packaging.** Checked the xUnit docs: `xunit.v3` 4.x defaults to Microsoft Testing Platform v2, and with the .NET 10 SDK the `global.json` `test.runner` setting replaces `TestingPlatformDotnetTestSupport`. Chose the plain `xunit.v3` package, with no `Microsoft.NET.Test.Sdk`, because the project is MTP-only.
- **Stop hook scope.** The first version ran verify whenever anything under `src/` or `tests/` changed, including nested `CLAUDE.md` files. Narrowed it to code and build files (`*.cs`, `*.csproj`, `*.props`, `*.slnx`, `global.json`), so doc edits never trigger a build.
- **Dangerous-bash hook rewrite.** The agent's first version had two bugs, both caught by its own hook tests (see the phase file): a plain `rm file.txt` exited with code 1, and flags were read from the whole command line. The rewrite parses each command segment separately.

## 2026-11-14 · Phase 0 gates

- **The fresh-context review justified itself on day one.** The agent's own 18-case hook test passed, yet the independent reviewer found 20+ bypasses and a false positive in the Bash hook.
- **Idea → options → choice for the Bash hook.**
  - (1) Keep patching regexes. Rejected: whitespace splitting can't model quoting or nesting.
  - (2) Write a real shell parser. Rejected: too heavy for a harness.
  - (3) Use Python's `shlex` tokenizer with `punctuation_chars`, plus a regression table. **Chosen by the agent, pending Alessandro's review at Checkpoint B.**
  - Also documented explicitly: the hook is a best-effort speed bump, and permission rules are the second layer.
- **Permission narrowing (D10), harness self-protection (D11) and CI supply-chain controls (D12)** were raised by both gates. They change what Alessandro has approved to run without prompting, so they're left for his decision rather than applied by the agent.
- **Honesty correction.** The lead agent reported "Semgrep: no findings" from a run that had actually aborted (nonexistent `p/bash` ruleset; `$?` read after a pipe). The security-auditor caught it, and the corrected run is recorded in the phase file.

## 2026-11-14 · Phase 1: affordability rules

- **Re-checking B4 with real decimals.** All three B4 worked examples match `System.Decimal` to the cent. The check also showed that B4's third check, "Loan ≤ MaxBorrowing", fails exactly when DTI or the 4.5× income multiple fails. The explanation would give the same reason twice.
- **Idea → options → choice for the checks (D2).**
  - (1) Keep B4.
  - (2) Three independent checks: LTV, DTI and loan-to-income. **Chosen.** MaxBorrowing stays as an output.
  - (3) Two checks.
  - Chosen by the agent under Alessandro's "proceed automatically" instruction. It gives the same decisions as B4 on every example. A 200,000-sample random run found no approved case with Loan > MaxBorrowing.
- **Rounding (D3).** Compare unrounded values, round outputs once. The alternative, rounding before comparing, would break the invariant above by up to a rounding step. The cost, a value like 45.004% showing as "45.00%" but failing, is handled by an explicit "before rounding" explanation.
- **No rate field (D1).** The spec's five fields have no interest rate, so the payment uses one configurable 6% assessment rate rather than inventing a product rate.
- **Production layers.** Filled in for all 14 layers. Caching and frontend are `N/A` with reasons. The scaling answer for the README is fixed now: SQLite and the in-process queue are the first bottleneck, so the first change is Azure SQL plus a durable queue.
- **Checkpoint B (Alessandro: "Proceed").** All nine agent decisions were approved. MaxBorrowing stays income-based, so B4's own example 1 keeps its 381,535.82, rather than taking the reviewer's LTV-capped alternative. ADR 0001 is Accepted.

## 2026-11-14 · Phase 2: core domain + calculator

- **Shape of the domain (P2-D1 to P2-D6, Alessandro approved the recommendations).** A sealed calculator built once from validated options, an immutable result record held by the assessment (null while Pending), options validation as a pure method in Core, one shared input guard, version 7 IDs, and timestamps passed in by the caller. The idea throughout: Core stays a pure function of (inputs, options) so it can be tested exhaustively without a host.
- **Test-first, honestly.** Stubs that throw made all 205 tests compile and fail; the implementation then turned them green on the first full run. Two deliberate bugs (banker's rounding; comparing rounded values) were each caught by 6 and 8 tests. That is the evidence that the golden set guards the two subtle rules, not just the happy path.
- **The test-designer added a case the rules didn't have.** E1–E18 never mix a normal failure with a "before rounding" failure in one summary. E19 does; the lead agent recomputed it with `System.Decimal` before it went into the golden set.
- **What the fresh reviewers found.** No maths errors (the `dotnet-reviewer` recomputed everything at 80 digits). The real findings were about what the code will leak or allow once it is wired up: record `ToString` printing income (found by both reviewers independently), a pre-1970 timestamp that made `Guid.CreateVersion7` put a value in an exception message, and an assessment that would accept a hand-built inconsistent result. All fixed before the PR. The fixes were written before their tests, so the new tests were run against the old code to show they fail (7 of 8 did; the eighth guards code that was already correct).
- **Deferred on purpose.** EF Core can't use the entity's private constructor as written. That stays for Phase 3, where the persistence abstraction is Alessandro's decision, rather than shaping the domain around a mapping choice not yet made.

## 2026-11-14 · Phase 3: API + persistence + validation

- **The abstraction choice (P3-D1, Alessandro chose the recommendation).** A use-case service in Core over one small repository interface whose every read takes the owner. The alternatives were endpoints talking to the `DbContext` directly (fewer files, but HTTP handlers own the orchestration and Phase 6 would have to pull it out) and an `IQueryable`-style store in Core (leaks EF semantics). Putting the owner in the method signature makes "another user's data is a 404" structural, not a convention.
- **One source of input bounds (P3-D2).** The rules section 1 bounds now live in one public Core rule set. The API's `IValidatableObject` and Phase 2's `InputGuard` both call it, and a parity theory runs the same inputs through both. While wiring it up, the test-designer asked whether a bad property value should also produce a "loan below 1,000" error (A6). It shouldn't: one mistake, one error.
- **What the docs check changed before any code.** Three framework facts shaped the tests: in .NET 10 Development a malformed body throws and becomes a 500 unless the handler keeps its status; handled exceptions no longer log by default; validation metadata that is missing fails silently, so a test must prove validation runs. The check also corrected the plan's claim that SQLite can't compare decimals.
- **Where the framework disagreed with the plan.** EF Core 10 rejected the recommended complex-type mapping in three steps (constructor binding, nested JSON collection, `IList<T>` requirement). The approved fallback, one JSON column via a value converter, was used rather than making the domain's checks mutable. `AddValidation` named errors in PascalCase; Alessandro chose camelCase everywhere (A1), done centrally in the ProblemDetails customisation.
- **What the gates found.** No high or critical findings. The real value was in tests that passed without proving their claim: the assessment round trip compared JSON by value, so a dropped trailing zero would never fail (reviewer F1); the JSON-strictness tests only ran on the Development error path (security L2, reviewer F5, found independently). Both were tightened, along with redacted `ToString` on the API records and rejecting duplicate JSON keys. Eight new tests failed before the fixes and pass after.
- **Tooling friction, recorded honestly.** The project's permission rules stop the agent installing tools, so `dotnet-ef` waited for Alessandro. The generated migration failed the build on a style rule; the fix marks migrations as generated code rather than editing generated files.

## 2026-11-15 · Phase 4: Docker (in parallel with Phase 6)

- **Schema in the container (P4-D1, Alessandro chose the recommendation).** Compose runs the API as Development, so the existing Development-only startup migration creates the schema. The alternatives were a config switch in `Program.cs` (a conflict with Phase 6, and a switch that could be left on in production) and a separate migration-bundle container (closest to production, more moving parts). The cost, Scalar, OpenAPI and every endpoint reachable without auth, is contained by binding the port to `127.0.0.1` only. ADR 0002.
- **The check that found a real bug.** C14 (a broken startup must fail CI) showed the container never exited: `dotnet` as PID 1 ignored its own abort signal and spun at 99% CPU. `init: true` in compose fixed it; the CI poll then failed in about 3 s instead of 60 s. The PR `/code-review` pointed out the fix lives in compose, not the image, so ADR 0002 says any other runner needs an init process too.
- **What the gates found.** The implementer left out an approved CI check (the non-root UID); the reviewer caught it. The lead agent's own answer said the runtime image is Debian; Learn says Ubuntu 24.04. Both corrected. The deliberate CI break (C14) ran on the PR and failed the job as intended.

## 2026-11-15 · Phase 6: background assessments (in parallel with Phase 4)

- **Queue shape (P6-D1, P6-D2).** A Core port (`IAssessmentQueue`) with a bounded in-process channel that waits when full, and one `BackgroundService` reader. Save Pending first, enqueue second, so a crash leaves a row the startup sweep finds. The alternatives were the database as the queue (polling) and a broker (new infrastructure and a secret). ADR 0003 records the single-worker limits: no concurrency token, so a second worker needs one first.
- **What the reviewer found that tests had missed.** A client that disconnects right after the insert left the row Pending and never queued, even with room, because `WriteAsync` returns cancelled at once for a cancelled token. `TryWrite` first fixed it, with a test that failed before. Other tests passed without proving their claim (a held worker that was never holding an assessment, a "full queue" check that was already true); they were rewritten, and two were checked by breaking the code on purpose.
- **Decisions Alessandro took after the gates.** Retry a failed startup sweep instead of waiting for a restart; finish an assessment at its creation time if the clock moved back; accept the open-POST denial-of-service risk until Phase 8; keep the worker-only methods on the shared service with comments.
- **Rebase cost.** Merging Phase 4 first and rebasing Phase 6 changed every commit hash the phase file cited. The file now has an old-to-new table; the logged prompts stay verbatim.
- **Working rule after this phase (P06-12).** Alessandro asked to go phase by phase from now on: no more parallel worktrees.

## 2026-11-15 · Phase 5: logging, tracing and metrics

- **Telemetry stack (P5-D1 to P5-D5, Alessandro chose all recommendations).** The OpenTelemetry SDK (3 packages at 1.20.1, the newest two weeks old) with OTLP export switched on only by an endpoint setting, so tests, CI and a plain run never connect. The Aspire dashboard is an opt-in compose override; the default run and CI did not change. The worker's span is a separate trace linked to the POST by the assessment ID, so the queue port stayed a plain ID.
- **The test that matters most is a negative one.** Every log, span and metric tag is scanned for every form of the E1 and E2 numbers (raw, rounded, grouped, comma-decimal, unrounded ratios). The reviewer found the first version missed longer-precision leaks such as an unrounded LTI; the fix made separator forms match as prefixes and added the unrounded values, and the old helper failed 7 of 18 new self-tests.
- **Tests that could not fail, again.** The root-span test passed without the line it guarded; the instrument set and the span event tags were not checked. Each was fixed and proven by a temporary mutation.
- **A Linux-only CI failure.** The export tests relied on the flush at host shutdown; on the CI runner the hosts shut down in milliseconds and sent nothing. Not reproduced locally or in a Linux container; fixed by exporting every 100 ms and waiting for the data before shutdown (cause inferred, not proven).
- **Manual evidence.** The dashboard check (O30) confirmed traces, logs and metrics arrive with only the allowlisted tags. Rules section 6 now names the HTTP span keys the framework adds; `user_agent.original` is removed.

## 2026-11-15 · Harness H1: usage awareness and model choice

- **What was asked and what was built first.** Alessandro asked for agents that know the project's state and the usage limits. The lead's design (session-start and subagent context hooks, a status-line usage snapshot, compaction checkpoints, generated decision indexes, handovers, about 2,000 lines with tests) was approved and built.
- **Why most of it was removed.** In use it added approval prompts and context output to his chat. He said he only meant being aware of usage and choosing models sensibly. The lead removed every hook and script; H1 kept an agent registry and short rules in `CLAUDE.md`.
- **What is actually measurable.** Only what Claude Code shows the user: `/usage`, `/context`, an optional status line, and the limit message with its reset time. Plan name, remaining allowance and per-model quotas are not available to scripts.
- **Lesson for later phases.** Ask what "aware" means in practice before building machinery; a smaller first version would have saved a day's usage. The review also found that shell commands can edit protected hook files without the "ask" prompt (open for Phase 8).

## 2026-11-16 · Phase 7: authentication, authorisation and cross-IDE portability

- **Asymmetric ES256 and a fail-closed dev endpoint (P7-D1 to P7-D4, ADR 0004).** Instead of an HS256 shared secret in config, `POST /dev/token` generates an ECDSA P-256 key pair in memory when `Development` and `DevAuth:Enabled=true` are both set, and startup fails if `DevAuth:Enabled` is on outside `Development` or together with a configured `Jwt:SigningPublicKey`. Trust comes only from `Jwt:*` (`Authority` and `MetadataAddress` on the scheme are cleared).
- **Static `CryptoProviderFactory` cache trap.** Going green, valid tokens intermittently failed with 401 when integration tests ran together: IdentityModel's default static `CryptoProviderFactory` caches signature providers by key thumbprint, so a disposed host's `TokenKeys` disposed the `ECDsa` still used by another host's cached provider. Giving each key its own `CryptoProviderFactory { CacheSignatureProviders = false }` fixed it.
- **Replacing framework auth logs with an app-owned `TokenRejected` event.** Even with IdentityModel's default PII hiding enabled, `JwtBearerHandler` logged `IDX10205` with the token's issuer in clear at `Information` (caught by AU92). Pinning `Microsoft.AspNetCore.Authentication` at `Warning` in `appsettings.json` and logging an app-owned `TokenRejected` event (`FailureType` only: exception class name or `InvalidSubject`) keeps rejection observability without leaking token contents.
- **Cross-IDE and cross-model portability (Claude Code → Antigravity IDE, Claude Opus/Sonnet → Gemini 4 Argon).** Mid-way through Phase 7 (`3320a82`), we switched from Claude Code to Google Antigravity IDE (Gemini 4 Argon) to verify that the repository's instructions (`AGENTS.md`, `CLAUDE.md`, `.claude/rules/`), skills (`.claude/skills/`), subagent definitions (`.claude/agents/`) and repo-based progress tracking work seamlessly across different environments and model families. Defining and running `security-auditor`, `dotnet-reviewer` and `spec-checker` on Gemini 4 Argon not only worked without changing any harness file, it also caught two real issues that the first Claude Opus gate pass had missed: `DevTokenRequest` and `DevTokenResponse` did not override `ToString()` (printing `Subject` and `Token`), and `SubjectClaim.IsJsonString` used `TryGetProperty("sub")`, allowing a JWT with duplicate `"sub"` JSON properties to pass validation and return `201 Created` (`83b2bd3`, `8f86bce`).

## 2026-11-16 · Phase 8: security hardening

- **Rate limiting and queue backpressure (`P8-D1`, `P8-D2`).** Added ASP.NET Core's built-in `AddRateLimiter` (`RateLimitingSetup`) with a shared `"write"` fixed-window policy on `POST /api/applications`, `POST /api/applications/{id}/assessments` and `POST /dev/token`, running after `UseAuthentication()` and `UseAuthorization()` so anonymous callers get `401` and cannot exhaust a subject's permits. Both `security-auditor` and `dotnet-reviewer` caught that sending a `Bearer` token to the `AllowAnonymous` `POST /dev/token` endpoint would partition by `sub` instead of `"dev-token"`, and that an unprefixed `sub == "dev-token"` would collide with it; fixed by ignoring `sub` on `IAllowAnonymous` endpoints and prefixing subject partitions as `"sub:{sub}"`. Full-queue waits in `ChannelAssessmentQueue` are bounded by `Assessments:EnqueueTimeoutSeconds` (10 s default), returning `503 ProblemDetails` while keeping the saved `Pending` row in SQLite for startup recovery.
- **Security headers, HSTS and `AllowedHosts` (`P8-D3`).** `SecurityHeadersMiddleware` sets `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: no-referrer` and `Cache-Control: no-store` on all responses, `UseHsts()` runs outside `Development`, and `AllowedHosts` is `"localhost;127.0.0.1"`.
- **Supply-chain locks and digest pins (`P8-D4`, `P8-D5`).** Added `nuget.config` (`<clear />`, `nuget.org` over HTTPS, `<packageSourceMapping>` `*`), `<RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>` with 5 committed `packages.lock.json` files and `--locked-mode` restores across `verify.sh`, `Dockerfile` and CI, plus multi-arch `@sha256` digests in `Dockerfile` and `compose.observability.yaml` and `.github/dependabot.yml`.
- **Harness self-protection (`P8-D6`, H1 review R4).** Extended `block-dangerous-bash.sh` to block shell writes and inline interpreter access to `.claude/hooks` and `.claude/settings*.json` (including `mv` source arguments, `rm` and `chmod`, caught by the Phase 8 gates), expanding `test-hooks.sh` to 113 cases.



