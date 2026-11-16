# Phase 09: Optimisation + quality

Status: **In progress: plan and tests approved (2026-11-16), implementation next** · Branch: `phase-09-quality` · PR: — · Started: 2026-11-16 · Finished: —

## Goal

Complete the Phase 9 quality, clean-up, and measurement pass (`docs/PROGRESS.md` R25 "Clean and readable code", "Carry into later phases", and `docs/layers/PRODUCTION-LAYERS.md` "Database", "Caching", "Scaling"):
- **Carried code-review and quality fixes (`P9-D1`):**
  - **PR #9 (Phase 5) `/code-review` 1–3:** exact key matching for `service.name` in `OTEL_RESOURCE_ATTRIBUTES` (`Program.cs`); remove the duplicate `OutcomeTag` `SetTag` call in `AssessmentTelemetry.RecordProcessed`; pass `RulesVersion` from the stored `AffordabilityResult` on `Completed` assessments (`AssessmentProcessingResult`).
  - **PR #8 (Phase 6) `/code-review` 3 (processing load tracking):** project the application's 5 scalar inputs in `EfMortgageApplicationRepository.FindForProcessingAsync` so EF Core tracks only the `AffordabilityAssessment` row (not the parent `MortgageApplication`), and in `UpdateAssessmentAsync` call `Update(assessment)` only when the entry is not already tracked (`EntityState.Detached`), so EF Core's `UPDATE` statement writes only the changed columns (`Status`, `Result`, `AssessedAt`/`FailedAt`).
  - **Phase 6 `RR5` (sweep retry delay proof):** record a timestamp on `CapturingLoggerProvider` log entries (from `TimeProvider`) and assert in `Startup_sweep_failure_is_retried_and_completes_pending_assessment` that the retry waited at least the configured delay.
  - **Phase 3 `/code-review` 7 (`JsonStrictnessTests` host reuse):** share one `Production` host in `JsonStrictnessTests` instead of building and disposing a new `WebApplicationFactory` host on every `[Theory]` row.
- **Database index & query-plan verification and latency measurements (`P9-D2`):**
  - Add `DatabaseQueryPlanTests` using SQLite `EXPLAIN QUERY PLAN` on the repository queries (`FindAsync`, `FindAssessmentAsync`, `FindForProcessingAsync`) to prove each uses primary-key / foreign-key indexes (`SEARCH ... USING INDEX`) rather than full table scans (`SCAN`), and measure/record local request and worker latencies in this phase file (`PRODUCTION-LAYERS.md` "Database", "Caching", "Scaling").
- **Schema / single-worker scope (`P9-D3`)** and **rule wording alignment (`P9-D4`)** below.

No new NuGet packages are needed.

## Plan (approved at Checkpoint A)

Approved by Alessandro on 2026-11-16 (P09-02), with Option A on every decision (`P9-D1` to `P9-D4`).

1. Branch `phase-09-quality` from `main` (done).
2. Docs check (links below).
3. Ask `test-designer` for the Phase 9 test cases (`OTEL_RESOURCE_ATTRIBUTES` key boundary, stored `RulesVersion` tag on `assessment.process`, `FindForProcessingAsync` untracked application + minimal `UpdateAssessmentAsync`, `RR5` retry-delay elapsed check, and SQLite `EXPLAIN QUERY PLAN` index checks) → Alessandro approves → write failing tests first.
4. Implement the approved Phase 9 improvements in small conventional commits.
5. Run `./scripts/verify.sh` (full) and 3 integration test runs, plus measure and record before/after integration suite time and endpoint/worker latencies.
6. Run `security-auditor`, `dotnet-reviewer`, and `spec-checker` subagents; fix real findings test-first.
7. Update `docs/layers/PRODUCTION-LAYERS.md`, `docs/progress/phase-09-quality.md`, `docs/PROGRESS.md` (marking R25 `met`), `docs/ai/PROMPTS.md`, and `docs/ai/DESIGN-JOURNAL.md`; push and open the PR; Checkpoint B.

## Decisions for Alessandro (2-3 options · trade-offs · recommendation)

**P9-D1. Carried code-review and quality fixes (`Program.cs`, `AssessmentTelemetry`, `EfMortgageApplicationRepository`, `CapturingLoggerProvider`, `JsonStrictnessTests`).**
- **A. Apply all five clean-ups above test-first** (recommended): exact `service.name` parsing in `OTEL_RESOURCE_ATTRIBUTES`, single `OutcomeTag` set and stored `RulesVersion` in `AssessmentTelemetry`, scalar projection + tracked-aware `UpdateAssessmentAsync` in `EfMortgageApplicationRepository`, timestamped `CapturingLoggerProvider` for `RR5`, and shared `Production` host in `JsonStrictnessTests`.
- **B. Telemetry and repository fixes only, leaving the test suite untouched.**

**P9-D2. Database query plans, SQLite WAL, and caching measurements (`PRODUCTION-LAYERS.md` "Database", "Caching", "Scaling").**
- **A. Verify index usage (`EXPLAIN QUERY PLAN`) in `DatabaseQueryPlanTests`, enable `Journal Mode=Wal` on file-based SQLite connection strings (keeping `:memory:` unchanged), and record in-process latency measurements showing why output caching is not needed** (recommended). Proves indexes are used by test, enables WAL for concurrent reads during writes on file SQLite, and answers the `PRODUCTION-LAYERS.md` "Caching" and "Database" Phase 9 items with real numbers.
- **B. `EXPLAIN QUERY PLAN` tests and latency measurements only, without changing `Journal Mode`.**

**P9-D3. Items that would require a new database migration (PR #8 `/code-review` 1 retry counter, Phase 6 R6 concurrency token).**
- **A. Keep the single-migration schema (`InitialCreate`) unchanged and document both in `PRODUCTION-LAYERS.md` / Phase 10 `README.md` as multi-instance scale items (alongside ADR 0003)** (recommended). Matches `ORIGINAL_SPEC.md` ("Clarity over production-readiness") and ADR 0003 (single worker instance in this exercise).
- **B. Add a second EF Core migration (`RowVersion` / `AttemptCount`) now.**

**P9-D4. Aligning `.claude/rules/backend.md` and `tests/CLAUDE.md` with the approved Phase 2 and Phase 3 decisions (`docs/PROGRESS.md` open items).**
- **A. Update `.claude/rules/backend.md` ("Reads use `AsNoTracking` and map to DTOs at the API boundary") and `tests/CLAUDE.md` ("No logic in tests beyond setup, except the approved property-based invariants in `AffordabilityCalculatorPropertyTests`")** (recommended). Resolves both open items in `docs/PROGRESS.md` so the written rules match `P3-D1` and Phase 2 `/code-review` 7.
- **B. Leave both files unchanged and keep the exceptions noted in `docs/PROGRESS.md`.**

## Decisions (option chosen · alternatives · reason · decided by) → ADR links

| # | Option chosen | Alternatives | Reason | Decided by |
|---|---|---|---|---|
| P9-D1 | A: Apply all five carried quality/review fixes (`OTEL_RESOURCE_ATTRIBUTES` exact key check, stored `RulesVersion` & single `OutcomeTag` in `AssessmentTelemetry`, scalar projection + tracked-aware `UpdateAssessmentAsync` in `EfMortgageApplicationRepository`, timestamped `CapturingLoggerProvider` for `RR5`, shared `Production` host in `JsonStrictnessTests`) | B telemetry and repository fixes only | Resolves carried items from Phases 3, 5 and 6 and satisfies R25 | Alessandro |
| P9-D2 | A: `EXPLAIN QUERY PLAN` tests (`DatabaseQueryPlanTests`), `PRAGMA journal_mode=WAL;` on file SQLite databases, and recorded latency measurements confirming why output caching is not needed | B query plan tests and measurements only | Proves index usage by test, enables concurrent reads on file SQLite, and answers `PRODUCTION-LAYERS.md` Phase 9 items with real numbers | Alessandro |
| P9-D3 | A: Keep the single-migration schema (`InitialCreate`) unchanged; document retry counter and concurrency token as multi-instance scale items alongside ADR 0003 | B add a second EF Core migration now | Matches `ORIGINAL_SPEC.md` ("Clarity over production-readiness") and single-worker scope (ADR 0003) | Alessandro |
| P9-D4 | A: Update `.claude/rules/backend.md` and `tests/CLAUDE.md` to match the approved Phase 2 and Phase 3 decisions | B keep exceptions noted in `docs/PROGRESS.md` | Clears both open items in `docs/PROGRESS.md` | Alessandro |

## Docs verified (links)

Checked on 2026-11-16 before planning:
- [Efficient Querying - EF Core (Tracking, no-tracking and projections)](https://learn.microsoft.com/en-us/ef/core/performance/efficient-querying): projecting only needed columns avoids materializing and tracking unneeded entities; `SaveChangesAsync` on an already-tracked entity writes only modified properties, whereas `DbSet.Update` marks all properties modified.
- [SQLite Write-Ahead Logging (Microsoft.Data.Sqlite / SQLite)](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/connection-strings): `PRAGMA journal_mode=WAL;` allows concurrent readers with a writer on file databases.
- [OpenTelemetry Environment Variable Specification (`OTEL_RESOURCE_ATTRIBUTES`)](https://opentelemetry.io/docs/specs/otel/configuration/sdk-environment-variables/): comma-separated `key=value` pairs.

## Orchestration used

Planning and orchestration in Google Antigravity IDE (Gemini 4 Argon), continuing the cross-IDE / cross-model workflow started in Phase 7.

## Prompts

### P09-01 · human→agent (Antigravity IDE · Gemini 4 Argon) · 2026-11-16
- **Prompt (verbatim):**
  > Approved
- **Purpose:** approve Phase 8 Checkpoint B, merge PR #12, and start Phase 9 planning.
- **Outcome:** PR #12 merged (`f1a21ea`); Phase 9 plan written (`docs/progress/phase-09-quality.md`).

### P09-02 · human→agent (Antigravity IDE · Gemini 4 Argon) · 2026-11-16
- **Prompt (verbatim):**
  > Approved options A on all
- **Purpose:** Checkpoint A approval of the Phase 9 plan and decisions `P9-D1` to `P9-D4` (all Option A).
- **Outcome:** decisions recorded; `test-designer` invoked (`P09-03`).

### P09-03 · agent→subagent (`test-designer` in Antigravity · Gemini 4 Argon) · 2026-11-16
- **Prompt (verbatim):**
  ```
  Phase 9 (optimisation + quality) of a .NET 10 mortgage-application API in /Users/ale_ssd/Developer/Mortgage-Application-API-test (branch phase-09-quality). Propose test cases for the approved plan; do not write code.

  Read first:
  - `docs/progress/phase-09-quality.md` (goal, plan, approved decisions P9-D1 to P9-D4, all Option A)
  - `docs/layers/PRODUCTION-LAYERS.md` ("Database", "Caching", "Scaling")
  - `src/MortgageApplications.Api/Program.cs`, `src/MortgageApplications.Api/Observability/AssessmentTelemetry.cs`, `src/MortgageApplications.Api/Assessments/AssessmentWorker.cs`
  - `src/MortgageApplications.Core/Applications/AssessmentProcessingOutcome.cs`, `src/MortgageApplications.Core/Applications/MortgageApplicationService.cs`
  - `src/MortgageApplications.Infrastructure/Persistence/EfMortgageApplicationRepository.cs`, `src/MortgageApplications.Infrastructure/Persistence/DatabaseMigration.cs`, `src/MortgageApplications.Infrastructure/DependencyInjection.cs`
  - `tests/MortgageApplications.IntegrationTests/` (`OtlpExportTests.cs`, `AssessmentSpanTests.cs`, `AssessmentRepositoryTests.cs`, `AssessmentWorkerTests.cs`, `CapturingLoggerProvider.cs`, `JsonStrictnessTests.cs`)
  - `tests/MortgageApplications.UnitTests/Applications/MortgageApplicationServiceTests.cs`
  - `.claude/rules/backend.md`, `tests/CLAUDE.md`

  What will be built (P9-D1 to P9-D4, all Option A):
  1. OTEL_RESOURCE_ATTRIBUTES exact key matching (P9-D1, PR #9 /code-review 1): In Program.cs, OTEL_RESOURCE_ATTRIBUTES is split on , and each segment is trimmed so only an attribute whose key is "service.name" (kv.TrimStart().StartsWith("service.name=", StringComparison.Ordinal) with non-whitespace value) suppresses the default "mortgage-applications" service name. A key like "custom.service.name=other" must NOT suppress the default "mortgage-applications" service name, while "deployment.environment=demo, service.name=svc-trimmed" MUST keep "svc-trimmed".
  2. Stored RulesVersion in AssessmentProcessingResult & single OutcomeTag set (P9-D1, PR #9 /code-review 2-3): AssessmentProcessingResult carries string? RulesVersion (from result.RulesVersion when Completed, null otherwise), and AssessmentWorker passes result.RulesVersion ?? affordabilityOptions.Value.RulesVersion! to AssessmentTelemetry.RecordProcessed. In AssessmentTelemetry.RecordProcessed, OutcomeTag is set on activity once (not duplicated in the loop).
  3. EfMortgageApplicationRepository.FindForProcessingAsync & UpdateAssessmentAsync (P9-D1, PR #8 /code-review 3): FindForProcessingAsync projects the 5 scalar inputs (AnnualIncome, MonthlyExpenses, DepositAmount, PropertyValue, TermYears) instead of materializing/tracking the MortgageApplication entity (so db.ChangeTracker.Entries<MortgageApplication>() is empty after FindForProcessingAsync, while db.ChangeTracker.Entries<AffordabilityAssessment>() has the 1 tracked assessment in EntityState.Unchanged). UpdateAssessmentAsync calls db.Assessments.Update(assessment) only when db.Entry(assessment).State == EntityState.Detached, so for a tracked assessment only the modified properties (Status, Result, AssessedAt or FailedAt) are marked IsModified = true (not Id, ApplicationId, CreatedAt).
  4. Timestamped CapturingLoggerProvider for RR5 (P9-D1): CapturedLog records a timestamp (Stopwatch.GetTimestamp() or DateTimeOffset.UtcNow), and Startup_sweep_failure_is_retried_and_completes_pending_assessment asserts that the elapsed time between the RequeueFailed log entry and the PendingRequeued log entry is at least 800 ms (for the 1 s configured retry delay, proving the retry actually waited).
  5. SQLite EXPLAIN QUERY PLAN & WAL mode on file databases (P9-D2): DatabaseQueryPlanTests runs EXPLAIN QUERY PLAN against the SQLite connection for the repository's point/foreign-key lookups (FindAsync by Id + OwnerId, FindAssessmentAsync by Id + ApplicationId + OwnerId, FindForProcessingAsync join by assessment Id and application Id, and Applications lookup by OwnerId) and asserts every plan uses SEARCH with an index/primary key and never a full-table SCAN; and DatabaseMigration.MigrateDatabaseAsync sets PRAGMA journal_mode=WAL; when the SQLite database is file-backed (verified by a file-backed SQLite test checking PRAGMA journal_mode; returns "wal").

  Use your output format (ID | given | when | then | why it matters), mark each case must or nice, keep the list tight and high-value, and list any ambiguities for Alessandro to settle.
  ```
- **Purpose:** propose Phase 9 test cases before writing any tests.
- **Outcome:** 9 test cases (`P9-T1` to `P9-T9`); full text in [`phase-09/test-proposal.md`](phase-09/test-proposal.md).

### P09-04 · human→agent (Antigravity IDE · Gemini 4 Argon) · 2026-11-16
- **Prompt (verbatim):**
  > Approved
- **Purpose:** approval of the Phase 9 test cases (`P9-T1`–`P9-T9`).
- **Outcome:** failing tests written first (`P9-T1`–`P9-T9`), then implementation.

## What was built (files)

## Tests

### Proposal (`test-designer`, P09-03)

Full proposal, verbatim: [`phase-09/test-proposal.md`](phase-09/test-proposal.md).

**Recommended selection:** all 9 cases (`P9-T1`–`P9-T7` `must`, plus `P9-T8` detached-update fallback and `P9-T9` shared `Production` host in `JsonStrictnessTests`).

## Security gate

## Review gate

## AI output corrected or rejected (REAL cases only)

## Optimisations considered

## Open issues

## Time spent (approx.)

## Human review (Alessandro writes this; the agent leaves it empty)
