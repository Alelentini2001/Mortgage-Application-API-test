# Phase 10: Documentation (`README.md`)

Status: **Merged** · Branch: `phase-10-docs` · PR: [#20](https://github.com/Alelentini2001/Mortgage-Application-API-test/pull/20) · Started: 2026-11-16 · Finished: 2026-11-16

## Goal

Replace the 5-line placeholder `README.md` with clear, accurate, reviewer-ready documentation satisfying `docs/spec/ORIGINAL_SPEC.md` ("Documentation" and "Design & Reflection", requirements `R19`, `R20`, `R34`–`R42`, `R44`) and all items carried from Phases 1–9 (`docs/PROGRESS.md` "Carry into later phases"):
- **Setup & running (`R34`, `R35`, `R46`):** prerequisites (`.NET 10 SDK` via `global.json`, Docker), running locally (`dotnet run --project src/MortgageApplications.Api`), running via Docker Compose (`docker compose up --build`), opt-in OpenTelemetry Aspire dashboard (`compose.observability.yaml`), resetting a `data` volume with wrong ownership (`docker compose down -v`), running outside Compose with `docker run --init` (ADR 0002), and a copy-pasteable `curl` walkthrough (`POST /dev/token` → `POST /api/applications` → `GET /api/applications/{id}` → `POST .../assessments` → poll `GET .../assessments/{assessmentId}`, plus `src/MortgageApplications.Api/requests.http` and Scalar `/scalar`).
- **Running tests & verification (`R36`):** `./scripts/verify.sh` (full gate), `./scripts/verify.sh --fast`, `dotnet test --solution MortgageApplications.slnx`, and xUnit v3 `--filter-class` / `--filter-method` flags on `Microsoft.Testing.Platform`.
- **Affordability logic & assumptions (`R19`, `R20`):** the 3 independent checks (LTV <= 90%, DTI <= 45% at a 6% annual stress rate, LTI <= 4.5x), the monthly payment and income-based `MaxBorrowing` formulas, full-precision `decimal` comparisons with single 2 dp `MidpointRounding.AwayFromZero` rounding at the end, `RulesVersion` auditability, and explicit assumptions (linking to `docs/spec/affordability-rules.md` and ADR 0001).
- **Design & Reflection (`R37`–`R42`, `R44`):**
  1. **Key Design Decisions (`R39`):** (a) Pure domain `Core` with `decimal` money/percentages and separate validation vs. underwriting decision (ADR 0001); (b) Save-before-enqueue background assessment pipeline (`202 Accepted`, bounded `Channel<Guid>` with `503` full-queue timeout, startup Pending sweep, ADR 0003); (c) Fail-closed ES256 JWT bearer authentication, owner scoping via `ICurrentUser` (`404` on cross-owner access), and a test-enforced zero-PII telemetry allowlist (ADR 0004).
  2. **Scaling Consideration (`R40`):** why SQLite's single writer and the in-process `Channel<Guid>` queue are the first bottleneck under load, and the first change to make: swap persistence to Azure SQL and `IAssessmentQueue` to Azure Service Bus, adding a `RowVersion` concurrency token and `AttemptCount` retry cap so multiple stateless API and worker instances scale out independently (ADR 0003, `P9-D3 A`, `PRODUCTION-LAYERS.md`).
  3. **Trade-offs (`R41`):** what was deliberately kept simple or left out (single applicant, fixed 6% stress rate rather than product catalogue, income-based `MaxBorrowing` not capped by property LTV, SQLite + in-process `Channel` with zero local cloud cost and production cost scaling with Azure SQL serverless + Service Bus tiers, no `Idempotency-Key` header on `POST .../assessments`, startup-only recovery sweep, dev token endpoint in `Development` instead of a live OIDC provider).
  4. **Next Steps (`R42`):** `Idempotency-Key` support, production OIDC / Microsoft Entra ID (`oid` + `tid` claims, `Jwt:SigningPublicKey` / authority), outbox + Service Bus + `RowVersion`/`AttemptCount`, EF Core migration bundle deployment job (ADR 0002), GDPR retention / deletion endpoint, and gateway rate limiting.
- **AI-Assisted Engineering & Cross-Environment Portability:** concise summary of the phase-gated workflow (`AGENTS.md`, `.claude/`, TDD + `security-auditor` + `dotnet-reviewer` + `spec-checker`, verbatim `docs/ai/PROMPTS.md`) and how the repository switched mid-project from **Claude Code (Claude Opus 5.5 / Sonnet 4.6)** to **Google Antigravity IDE (Gemini 4 Argon)** without modifying the rules, skills, or subagent prompts.

## Plan (approved at Checkpoint A)

Approved by Alessandro on 2026-11-16 (`P10-02`), with Option A on both `P10-D1` and `P10-D2`.

1. Branch `phase-10-docs` from `main` (done).
2. Stop at **Checkpoint A** for Alessandro's approval on `P10-D1` and `P10-D2` (done).
3. Write `README.md` covering all sections above with clickable links to `docs/spec/affordability-rules.md`, `docs/adr/0001`–`0004`, `docs/layers/PRODUCTION-LAYERS.md`, `docs/PROGRESS.md`, and `docs/ai/PROMPTS.md`.
4. Verify every command and `curl` snippet in `README.md` and run `./scripts/verify.sh` (full).
5. Run `security-auditor`, `dotnet-reviewer`, and `spec-checker` subagents; fix any real findings.
6. Update `docs/progress/phase-10-docs.md`, `docs/PROGRESS.md` (updating `R19`, `R20`, `R34`–`R42`, `R44` in the traceability table), `docs/ai/PROMPTS.md`, and `docs/ai/DESIGN-JOURNAL.md`; push, open PR, wait for CI, and stop at **Checkpoint B**.

## Decisions

### P10-D1 · Structure and scope of `README.md`
- **Options:**
  - **A (chosen):** Keep all required documentation in a single self-contained root `README.md` (~255 lines) with clear sections (Overview & Architecture, Prerequisites & Setup, Running the Application, Quick-Start API Walkthrough, Running the Tests, Affordability Logic & Assumptions, Design & Reflection 1–4, and AI-Assisted Engineering & Cross-IDE Portability), linking to deeper docs (`docs/spec/affordability-rules.md`, `docs/adr/`, `docs/layers/PRODUCTION-LAYERS.md`, `docs/PROGRESS.md`).
  - **B:** Keep `README.md` minimal (< 100 lines) and split Setup, API usage, and Design & Reflection into separate files under `docs/`.
- **Trade-offs:** Option A matches `ORIGINAL_SPEC.md` ("Please include a README with: Setup instructions, How to run the application, How to run tests, A brief explanation of your design decisions" and "Please include a short section in your README addressing the following: 1. Key Design Decisions, 2. Scaling Consideration, 3. Trade-offs, 4. Next Steps") so an interviewer can read everything in one place on the GitHub landing page.
- **Decision:** **Option A**, approved by Alessandro (`P10-02`).

### P10-D2 · Verification of `README.md` (`test-designer` / tests)
- **Options:**
  - **A (chosen):** Treat Phase 10 as a documentation-only phase (like Phase 01 and H1) with no new C# tests, verify every documented command + `curl` flow and `./scripts/verify.sh` (full, 586 tests + 113 hook cases), and run `security-auditor`, `dotnet-reviewer`, and `spec-checker` to verify every spec requirement (`R19`, `R20`, `R34`–`R42`, `R44`).
  - **B:** Run `test-designer` and add C# tests asserting markdown headings in `README.md`.
- **Trade-offs:** Option A avoids brittle string assertions on prose while still running the full verification, security, code-review, and spec-compliance gates.
- **Decision:** **Option A**, approved by Alessandro (`P10-02`).

## Docs verified (links)
- `docs/spec/ORIGINAL_SPEC.md` ("Documentation", "Design & Reflection", "Time Expectation")
- `docs/spec/affordability-rules.md` (§1–§7)
- `docs/layers/PRODUCTION-LAYERS.md` (all 14 layers)
- `docs/adr/0001-affordability-assessment-model.md`
- `docs/adr/0002-container-runtime-and-startup-migrations.md`
- `docs/adr/0003-background-assessment-queue.md`
- `docs/adr/0004-authentication-and-authorisation.md`

## Orchestration used
- **Skill:** `/phase 10 docs` (`.claude/skills/phase/SKILL.md`).
- **Subagents (in Google Antigravity IDE · Gemini 4 Argon):**
  - `security-auditor` (`P10-03`): ran Gitleaks (`183` commits + working tree), Semgrep (`p/csharp` + `p/secrets`), and `dotnet list package --vulnerable`, and checked `README.md` for accidental secret/token leakage and accurate `POST /dev/token` / `127.0.0.1` / `Jwt:SigningPublicKey` security warnings (**PASS**, plus 2 informational accuracy notes).
  - `dotnet-reviewer` (`P10-04`): fresh-context accuracy check of `README.md` against `Program.cs`, `ApplicationEndpoints.cs`, `DevTokenEndpoint.cs`, `AssessmentTelemetry.cs`, `EfMortgageApplicationRepository.cs`, `MortgageApplicationsDbContext.cs`, `scripts/verify.sh`, `compose.yaml`, `compose.observability.yaml`, `requests.http`, and ADRs `0001`–`0004` (8 factual findings `R1`–`R8`, all fixed).
  - `spec-checker` (`P10-05`): mapped `R19`, `R20`, `R34`–`R42`, `R44`, and all 6 carried items (`docker compose down -v`, `docker run --init`, `Jwt:SigningPublicKey` / Entra ID, `RowVersion` + `AttemptCount`, production cost, and cross-IDE portability) to line ranges in `README.md`, advancing overall traceability from `31 met · 10 partial · 14 missing` to **`43 met · 4 partial · 8 missing`**.
- **Hooks that fired:** `.githooks/pre-commit` (Gitleaks on every commit).

## Prompts

### P10-01 · human→agent (Antigravity · Gemini 4 Argon) · 2026-11-16
- **Prompt (verbatim):**
  ```
  Approved
  ```
- **Purpose:** Approve Phase 09 Checkpoint B (#19) and proceed to Phase 10 (`README.md`) Checkpoint A plan.
- **Outcome:** PR #19 squash-merged (`ff53123`); `phase-10-docs` branch and Phase 10 plan created; stopped at Checkpoint A for approval.

### P10-02 · human→agent (Antigravity · Gemini 4 Argon) · 2026-11-16
- **Prompt (verbatim):**
  ```
  Approved with all options A
  ```
- **Purpose:** Checkpoint A approval for Phase 10 (`P10-D1` and `P10-D2` Option A).
- **Outcome:** Wrote `README.md` (`f6b8fc9`), ran `./scripts/verify.sh`, and ran the three Phase 10 gate subagents (`P10-03`–`P10-05`).

### P10-03 · agent→subagent (`security-auditor` in Antigravity · Gemini 4 Argon) · 2026-11-16
- **Prompt (verbatim):**
  ```
  Security audit of Phase 10 (Documentation / README.md) in /Users/ale_ssd/Developer/Mortgage-Application-API-test, branch phase-10-docs. Read only: do not edit files.

  Read:
  - `README.md`
  - `docs/progress/phase-10-docs.md`
  - `docs/spec/affordability-rules.md`
  - `docs/adr/0002-container-runtime-and-startup-migrations.md` and `docs/adr/0004-authentication-and-authorisation.md`

  Run:
  1. `gitleaks git --no-banner --redact -v` and `gitleaks dir --no-banner --redact .`
  2. `semgrep scan --config p/csharp --config p/secrets --error --quiet`
  3. `dotnet list MortgageApplications.slnx package --vulnerable --include-transitive`

  Also verify:
  - No real secrets, tokens, or private keys appear in `README.md` or `docs/progress/phase-10-docs.md`.
  - The security warnings around `POST /dev/token`, `127.0.0.1:8080:8080` loopback binding, and production `Jwt:SigningPublicKey` are accurately documented in `README.md`.

  Output a concise security gate verdict (PASS/FAIL) and any findings.
  ```
- **Purpose:** Security gate for Phase 10.
- **Outcome:** **PASS** (0 leaks in 183 commits + working tree, 0 Semgrep findings, 0 vulnerable packages; 2 informational accuracy notes on `GET /health` and `DevAuth:Enabled=true` + `Jwt:SigningPublicKey` fixed).

### P10-04 · agent→subagent (`dotnet-reviewer` in Antigravity · Gemini 4 Argon) · 2026-11-16
- **Prompt (verbatim):**
  ```
  Fresh-context diff review of Phase 10 (Documentation / README.md) in /Users/ale_ssd/Developer/Mortgage-Application-API-test, branch phase-10-docs. Read only: do not edit files, do not build or run tests.

  Get the diff with `git diff main...HEAD`. Read `README.md` in full, and cross-check every route, status code, port, command, file path, configuration key, formula, and ADR claim against:
  - `src/MortgageApplications.Api/Program.cs`
  - `src/MortgageApplications.Api/Applications/ApplicationEndpoints.cs`
  - `src/MortgageApplications.Api/Identity/DevTokenEndpoint.cs`
  - `src/MortgageApplications.Api/Properties/launchSettings.json`
  - `src/MortgageApplications.Api/appsettings.json`
  - `src/MortgageApplications.Api/requests.http`
  - `compose.yaml` and `compose.observability.yaml`
  - `docs/spec/ORIGINAL_SPEC.md` and `docs/spec/affordability-rules.md`
  - `docs/adr/0001-affordability-assessment-model.md`, `0002-container-runtime-and-startup-migrations.md`, `0003-background-assessment-queue.md`, `0004-authentication-and-authorisation.md`

  Output only a list, most severe first, one finding per line:
  `file:line | severity (critical/high/medium/low) | why it is wrong | concrete fix`
  If you find nothing real, output `No findings.` and the list of files you reviewed. State plainly that you did not build or run tests.
  ```
- **Purpose:** Fresh-context review of `README.md` against the codebase.
- **Outcome:** 8 findings (`R1`–`R8`), all fixed in `README.md` and `docs/progress/phase-10-docs.md`.

### P10-05 · agent→subagent (`spec-checker` in Antigravity · Gemini 4 Argon) · 2026-11-16
- **Prompt (verbatim):**
  ```
  Requirement traceability check at the end of Phase 10 (Documentation / README.md) in /Users/ale_ssd/Developer/Mortgage-Application-API-test, branch phase-10-docs. Read only: do not edit files.

  Read:
  - `README.md`
  - `docs/spec/ORIGINAL_SPEC.md` and `docs/spec/affordability-rules.md`
  - `docs/layers/PRODUCTION-LAYERS.md`
  - `docs/PROGRESS.md` (the R1-R55 traceability table and "Carry into later phases")
  - `docs/progress/phase-10-docs.md`

  Do:
  1. Map every Phase 10 spec requirement (`R19`, `R20`, `R34`, `R35`, `R36`, `R37`, `R38`, `R39`, `R40`, `R41`, `R42`, `R44`) and every carried item (`docker compose down -v`, `docker run --init`, `Jwt:SigningPublicKey` / Entra ID, multi-instance worker `RowVersion` + `AttemptCount`, production cost, cross-IDE/cross-model portability) to the exact line range in `README.md`.
  2. State how the R1–R55 traceability table in `docs/PROGRESS.md` changes after Phase 10 (which rows move from `missing` or `partial` to `met`, and the new overall summary counts of `met / partial / missing`).
  3. Output a Markdown table for the Phase 10 items and at most 5 short notes.
  ```
- **Purpose:** Requirement traceability at the end of Phase 10.
- **Outcome:** 12 requirements (`R19`, `R20`, `R34`–`R42`, `R44`) moved to **met**, bringing overall totals to **43 met · 4 partial · 8 missing**; all 5 factual notes resolved in `README.md`.

### P10-06 · human→agent (Antigravity · Gemini 4 Argon) · 2026-11-16
- **Prompt (verbatim):**
  ```
  Approved
  ```
- **Purpose:** Checkpoint B approval for PR #20 (`phase-10-docs`).
- **Outcome:** PR #20 approved and squash-merged into `main`.

## What was built (files)

- `README.md`: complete project documentation (Architecture & Endpoints, Prerequisites & Setup, How to Run the Application locally/Docker/Aspire + `curl` walkthrough + Production Auth, How to Run Tests, Affordability Assessment Logic & Assumptions, Design & Reflection 1–4, and AI-Assisted Engineering & Cross-Environment Portability).
- `docs/progress/phase-10-docs.md`, `docs/PROGRESS.md`, `docs/ai/PROMPTS.md`, and `docs/ai/DESIGN-JOURNAL.md`.

## Tests

Documentation-only phase (`P10-D2 A`). `./scripts/verify.sh` (full) passed:
- `0` warnings, `0` errors
- **586 tests** (`275` unit + `311` integration) passed (`17.4 s`)
- **113 hook cases** passed (`0` failed)
- `dotnet format --verify-no-changes` clean

## Security gate

`security-auditor` (`P10-03`, Antigravity IDE · Gemini 4 Argon). **Verdict: PASS.**
- `gitleaks git --no-banner --redact -v` (`183` commits) and `gitleaks dir --no-banner --redact .`: `0` leaks.
- `semgrep scan --config p/csharp --config p/secrets --error --quiet`: `0` findings.
- `dotnet list MortgageApplications.slnx package --vulnerable --include-transitive`: `0` vulnerable packages.
- Manual check: no real tokens or keys in `README.md`; two informational accuracy notes (`GET /health` description and `DevAuth:Enabled=true` + `Jwt:SigningPublicKey` startup fail-closed check) fixed in `README.md`.

## Review gate

1. **`dotnet-reviewer` (`P10-04`, Antigravity IDE · Gemini 4 Argon, read-only):**

| # | Finding | Severity | Status |
|---|---|---|---|
| R1 | `README.md:32`: `GET /health` was described as returning `200 OK, 503` with a SQLite connectivity check, whereas `Program.cs:150` maps `() => TypedResults.Ok("Healthy")` (DB readiness is a production next step in `PRODUCTION-LAYERS.md:12`). | Medium | Fixed: updated `GET /health` row to `200 OK` and liveness check description. |
| R2 | `README.md:88`: named the custom OTel metrics `mortgage_applications.assessments` and `mortgage_applications.assessment.duration` instead of `mortgage.assessments.processed` and `mortgage.assessment.duration` (`AssessmentTelemetry.cs:37,40`). | Medium | Fixed: updated to `mortgage.assessments.processed` and `mortgage.assessment.duration`. |
| R3 | `README.md:43,135`: stated `./scripts/verify.sh` runs `gitleaks`, `semgrep`, and `NuGet audit`, whereas `scripts/verify.sh` runs restore, build, unit + integration tests, hook tests, and `dotnet format --verify-no-changes` (`gitleaks` runs in `.githooks/pre-commit`, and `gitleaks`/`semgrep`/`dotnet list package --vulnerable` run in the security gate). | Medium | Fixed: updated lines 43 and 135 of `README.md`. |
| R4 | `README.md:215`: stated `EfMortgageApplicationRepository.FindForProcessingAsync` uses `AsNoTracking`, whereas it runs a tracked query projecting the 5 scalar application inputs alongside the tracked `AffordabilityAssessment` entity. | Low | Fixed: reworded to match `EfMortgageApplicationRepository.cs:43-61`. |
| R5 | `README.md:225`: wrote `MortgageApplicationDbContext` (singular) instead of `MortgageApplicationsDbContext` (plural). | Low | Fixed. |
| R6 | `README.md:33`: wrote `404 outside Development` for `POST /dev/token`, whereas an anonymous caller gets `401` from the fallback policy and an authenticated caller gets `404` when not registered (`DevTokenTests.cs:65-89`). | Low | Fixed: updated to `401 anonymous / 404 authenticated when not registered`. |
| R7 | `README.md:102-121`: omitted the `GET /api/applications/$APP_ID` step from the `curl` walkthrough. | Low | Fixed: added step 3 (`GET /api/applications/$APP_ID`) to the `curl` walkthrough. |
| R8 | `docs/progress/phase-10-docs.md:8`: wrote `/scalar/v1` instead of `/scalar`. | Low | Fixed. |

2. **`spec-checker` (`P10-05`, Antigravity IDE · Gemini 4 Argon, read-only):**
   - `R19`, `R20`, `R34`, `R35`, `R36`, `R37`, `R38`, `R39`, `R40`, `R41`, `R42`, and `R44` all move to **met** (**43 met · 4 partial · 8 missing**; all remaining 12 items belong to Phase 11 live rehearsal, demo script, and 3-slide deck).
   - Also noted the production cost detail (`PRODUCTION-LAYERS.md:19`), which was added to `README.md:235`.

## AI output corrected or rejected (REAL cases only)

- **Three factual inaccuracies in the first draft of `README.md` (`f6b8fc9`) caught by `dotnet-reviewer` (`P10-04`) and `spec-checker` (`P10-05`):**
  1. `GET /health` was described as probing SQLite (`200 OK, 503`), carried over from the Phase 9 plan notes rather than `src/MortgageApplications.Api/Program.cs:150` (`() => TypedResults.Ok("Healthy")`).
  2. Custom metric instrument names in `README.md:88` were written as `mortgage_applications.assessments` / `mortgage_applications.assessment.duration` instead of `mortgage.assessments.processed` / `mortgage.assessment.duration` (`AssessmentTelemetry.cs:37,40`).
  3. `MortgageApplicationsDbContext` was spelled in the singular (`MortgageApplicationDbContext`) and `FindForProcessingAsync` was described with `(AsNoTracking)` instead of scalar projection alongside the tracked `AffordabilityAssessment`.
  All were fixed before opening the PR.

## Optimisations considered

- N/A (documentation-only phase).

## Open issues

- None for Phase 10. Phase 11 covers the fresh-clone rehearsal, `DEMO-SCRIPT.md`, and `docs/presentation/SLIDES.md` (`R43`, `R45`–`R55`).

## Time spent (approx.)

~15 min (`2026-11-16 03:18 UTC` to `03:32 UTC`, in Google Antigravity IDE · Gemini 4 Argon).

## Human review (Alessandro writes this; the agent leaves it empty)
