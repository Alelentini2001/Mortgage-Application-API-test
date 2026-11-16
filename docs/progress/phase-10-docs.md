# Phase 10: Documentation (`README.md`)

Status: **At Checkpoint A (awaiting plan approval)** · Branch: `phase-10-docs` · PR: — · Started: 2026-11-16 · Finished: —

## Goal

Replace the 5-line placeholder `README.md` with clear, accurate, reviewer-ready documentation satisfying `docs/spec/ORIGINAL_SPEC.md` ("Documentation" and "Design & Reflection", requirements `R19`, `R20`, `R34`–`R42`, `R44`) and all items carried from Phases 1–9 (`docs/PROGRESS.md` "Carry into later phases"):
- **Setup & running (`R34`, `R35`, `R46`):** prerequisites (`.NET 10 SDK` via `global.json`, Docker), running locally (`dotnet run --project src/MortgageApplications.Api`), running via Docker Compose (`docker compose up --build`), opt-in OpenTelemetry Aspire dashboard (`compose.observability.yaml`), resetting a `data` volume with wrong ownership (`docker compose down -v`), running outside Compose with `docker run --init` (ADR 0002), and a copy-pasteable `curl` walkthrough (`POST /dev/token` → `POST /api/applications` → `POST .../assessments` → poll `GET .../assessments/{assessmentId}` → `GET /api/applications/{id}`, plus `requests.http` and Scalar `/scalar/v1`).
- **Running tests & verification (`R36`):** `./scripts/verify.sh` (full gate), `./scripts/verify.sh --fast`, `dotnet test --solution MortgageApplications.slnx`, and xUnit v3 `--filter-class` / `--filter-method` flags on `Microsoft.Testing.Platform`.
- **Affordability logic & assumptions (`R19`, `R20`):** the 3 independent checks (LTV <= 90%, DTI <= 45% at a 6% annual stress rate, LTI <= 4.5x), the monthly payment and income-based `MaxBorrowing` formulas, full-precision `decimal` comparisons with single 2 dp `MidpointRounding.AwayFromZero` rounding at the end, `RulesVersion` auditability, and explicit assumptions (linking to `docs/spec/affordability-rules.md` and ADR 0001).
- **Design & Reflection (`R37`–`R42`, `R44`):**
  1. **Key Design Decisions (`R39`):** (a) Pure domain `Core` with `decimal` money/percentages and separate validation vs. underwriting decision (ADR 0001); (b) Save-before-enqueue background assessment pipeline (`202 Accepted`, bounded `Channel<Guid>` with `503` full-queue timeout, startup Pending sweep, ADR 0003); (c) Fail-closed ES256 JWT bearer authentication, owner scoping via `ICurrentUser` (`404` on cross-owner access), and a test-enforced zero-PII telemetry allowlist (ADR 0004).
  2. **Scaling Consideration (`R40`):** why SQLite's single writer and the in-process `Channel<Guid>` queue are the first bottleneck under load, and the first change to make: swap persistence to Azure SQL and `IAssessmentQueue` to Azure Service Bus, adding a `RowVersion` concurrency token and `AttemptCount` retry cap so multiple stateless API and worker instances scale out independently (ADR 0003, `P9-D3 A`, `PRODUCTION-LAYERS.md`).
  3. **Trade-offs (`R41`):** what was deliberately kept simple or left out (single applicant, fixed 6% stress rate rather than product catalogue, income-based `MaxBorrowing` not capped by property LTV, SQLite + in-process `Channel`, no `Idempotency-Key` header on `POST .../assessments`, startup-only recovery sweep, dev token endpoint in `Development` instead of a live OIDC provider).
  4. **Next Steps (`R42`):** `Idempotency-Key` support, production OIDC / Microsoft Entra ID (`oid` + `tid` claims, `Jwt:SigningPublicKey` / authority), outbox + Service Bus + `RowVersion`/`AttemptCount`, EF Core migration bundle deployment job (ADR 0002), GDPR retention / deletion endpoint, and gateway rate limiting.
- **AI-Assisted Engineering & Cross-Environment Portability:** concise summary of the phase-gated workflow (`AGENTS.md`, `.claude/`, TDD + `security-auditor` + `dotnet-reviewer` + `spec-checker`, verbatim `docs/ai/PROMPTS.md`) and how the repository switched mid-project from **Claude Code (Claude Opus 5.5 / Sonnet 4.6)** to **Google Antigravity IDE (Gemini 4 Argon)** without modifying the rules, skills, or subagent prompts.

## Plan (awaiting Checkpoint A approval)

1. Branch `phase-10-docs` from `main` (done).
2. Stop at **Checkpoint A** for Alessandro's approval on `P10-D1` and `P10-D2`.
3. Write `README.md` covering all sections above with clickable links to `docs/spec/affordability-rules.md`, `docs/adr/0001`–`0004`, `docs/layers/PRODUCTION-LAYERS.md`, `docs/PROGRESS.md`, and `docs/ai/PROMPTS.md`.
4. Verify every command and `curl` snippet in `README.md` and run `./scripts/verify.sh` (full).
5. Run `security-auditor`, `dotnet-reviewer`, and `spec-checker` subagents; fix any real findings.
6. Update `docs/progress/phase-10-docs.md`, `docs/PROGRESS.md` (updating `R19`, `R20`, `R34`–`R42`, `R44` in the traceability table), `docs/ai/PROMPTS.md`, and `docs/ai/DESIGN-JOURNAL.md`; push, open PR, wait for CI, and stop at **Checkpoint B**.

## Decisions

### P10-D1 · Structure and scope of `README.md`
- **Options:**
  - **A (recommended):** Keep all required documentation in a single self-contained root `README.md` (~250–320 lines) with clear sections (Overview & Architecture, Prerequisites & Setup, Running the Application, Quick-Start API Walkthrough, Running the Tests, Affordability Logic & Assumptions, Design & Reflection 1–4, Production Readiness & Security Summary, and AI-Assisted Engineering & Cross-IDE Portability), linking to deeper docs (`docs/spec/affordability-rules.md`, `docs/adr/`, `docs/layers/PRODUCTION-LAYERS.md`, `docs/PROGRESS.md`).
  - **B:** Keep `README.md` minimal (< 100 lines) and split Setup, API usage, and Design & Reflection into separate files under `docs/`.
- **Trade-offs:** Option A matches `ORIGINAL_SPEC.md` ("Please include a README with: Setup instructions, How to run the application, How to run tests, A brief explanation of your design decisions" and "Please include a short section in your README addressing the following: 1. Key Design Decisions, 2. Scaling Consideration, 3. Trade-offs, 4. Next Steps") so an interviewer can read everything in one place on the GitHub landing page.
- **Decision:** Pending Alessandro's approval at Checkpoint A.

### P10-D2 · Verification of `README.md` (`test-designer` / tests)
- **Options:**
  - **A (recommended):** Treat Phase 10 as a documentation-only phase (like Phase 01 and H1) with no new C# tests, verify every documented command + `curl` flow and `./scripts/verify.sh` (full, 586 tests + 113 hook cases), and run `security-auditor`, `dotnet-reviewer`, and `spec-checker` to verify every spec requirement (`R19`, `R20`, `R34`–`R42`, `R44`).
  - **B:** Run `test-designer` and add C# tests asserting markdown headings in `README.md`.
- **Trade-offs:** Option A avoids brittle string assertions on prose while still running the full verification, security, code-review, and spec-compliance gates.
- **Decision:** Pending Alessandro's approval at Checkpoint A.

## Docs verified (links)
- `docs/spec/ORIGINAL_SPEC.md` ("Documentation", "Design & Reflection", "Time Expectation")
- `docs/spec/affordability-rules.md` (§1–§7)
- `docs/layers/PRODUCTION-LAYERS.md` (all 14 layers)
- `docs/adr/0001-affordability-assessment-model.md`
- `docs/adr/0002-container-runtime-and-startup-migrations.md`
- `docs/adr/0003-background-assessment-queue.md`
- `docs/adr/0004-authentication-and-authorisation.md`

## Orchestration used
- *Pending execution after Checkpoint A.*

## Prompts

### P10-01 · human→agent (Antigravity · Gemini 4 Argon) · 2026-11-16
- **Prompt (verbatim):**
  ```
  Approved
  ```
- **Purpose:** Approve Phase 09 Checkpoint B (#19) and proceed to Phase 10 (`README.md`) Checkpoint A plan.
- **Outcome:** PR #19 squash-merged (`ff53123`); `phase-10-docs` branch and Phase 10 plan created; stopped at Checkpoint A for approval.
