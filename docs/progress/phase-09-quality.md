# Phase 09: Optimisation + quality

Status: **Merged** · Branch: `phase-09-quality` · PR: [#19](https://github.com/Alelentini2001/Mortgage-Application-API-test/pull/19) · Started: 2026-11-16 · Finished: 2026-11-16

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
- **Outcome:** failing tests written first (`dbc93ad`), then implementation (`31b6b89`, `807ed6a`, `0f4fcb1`, `0db33e8`, `4f152ee`).

### P09-05 · agent→subagent (`security-auditor` in Antigravity · Gemini 4 Argon) · 2026-11-16
- **Prompt (verbatim):**
  ```
  Security gate for Phase 9 (optimisation + quality) of a .NET 10 mortgage-application API in /Users/ale_ssd/Developer/Mortgage-Application-API-test, branch phase-09-quality. Read only: do not edit files or switch branches.

  Read first: .claude/rules/security.md, AGENTS.md, docs/progress/phase-09-quality.md (goal, plan, decisions P9-D1 to P9-D4, approved test cases P9-T1 to P9-T9).

  Do:
  1. Run `gitleaks git --no-banner --redact -v` and `gitleaks dir --no-banner --redact .`
  2. Run `semgrep scan --config p/csharp --config p/secrets --error --quiet` (and with `--baseline-commit main`).
  3. Run `dotnet list MortgageApplications.slnx package --vulnerable --include-transitive`.
  4. Review the diff (`git diff main...HEAD`) against OWASP Top 10:2025 and the repo security rules:
     - `AssessmentProcessingResult.RulesVersion` is an allowlisted field (rules section 6) and carries no applicant data.
     - `EfMortgageApplicationRepository.FindForProcessingAsync` and `UpdateAssessmentAsync` keep the same worker-only boundaries and parameterized LINQ queries (`DatabaseQueryPlanTests` uses `query.CreateDbCommand()`, no raw user SQL).
     - `Program.cs` `OTEL_RESOURCE_ATTRIBUTES` parsing cannot throw or leak data.
     - `.claude/rules/backend.md` and `tests/CLAUDE.md` updates keep all security rules intact.

  Output:
  - Exact commands run and trimmed outputs.
  - A findings table: `tool/check | finding (file:line) | severity | real or false positive | reason | fix`.
  - A one-line verdict: `PASS` or `FAIL`.
  ```
- **Purpose:** security gate on `phase-09-quality`.
- **Outcome:** **PASS**. 0 leaks (169 commits + working tree), 0 Semgrep findings, 0 vulnerable packages, 0 OWASP findings.

### P09-06 · agent→subagent (`dotnet-reviewer` in Antigravity · Gemini 4 Argon) · 2026-11-16
- **Prompt (verbatim):**
  ```
  Fresh-context diff review of Phase 9 (optimisation + quality) in /Users/ale_ssd/Developer/Mortgage-Application-API-test, branch phase-09-quality. Read only: do not edit files, do not build or run tests.

  Get the diff with `git diff main...HEAD`. Read the touched files in full (`src/MortgageApplications.Core/Applications/AssessmentProcessingResult.cs`, `src/MortgageApplications.Core/Applications/MortgageApplicationService.cs`, `src/MortgageApplications.Infrastructure/Persistence/EfMortgageApplicationRepository.cs`, `src/MortgageApplications.Api/Program.cs`, `src/MortgageApplications.Api/Observability/AssessmentTelemetry.cs`, `src/MortgageApplications.Api/Assessments/AssessmentWorker.cs`, `.claude/rules/backend.md`, `tests/CLAUDE.md`, `tests/MortgageApplications.UnitTests/Applications/MortgageApplicationServiceTests.cs`, `tests/MortgageApplications.IntegrationTests/AssessmentRepositoryTests.cs`, `tests/MortgageApplications.IntegrationTests/DatabaseQueryPlanTests.cs`, `tests/MortgageApplications.IntegrationTests/OtlpExportTests.cs`, `tests/MortgageApplications.IntegrationTests/CapturingLoggerProvider.cs`, `tests/MortgageApplications.IntegrationTests/AssessmentWorkerTests.cs`, `tests/MortgageApplications.IntegrationTests/JsonStrictnessTests.cs`), plus `docs/progress/phase-09-quality.md` and `docs/progress/phase-09/test-proposal.md`.

  Check in priority order:
  1. Correctness of the telemetry, repository, and `OTEL_RESOURCE_ATTRIBUTES` changes (`P9-D1`).
  2. Test coverage and quality: whether every approved case (`P9-T1`–`P9-T9`) is implemented and can fail if the behaviour under test breaks.
  3. Clean and readable code (`R25`).

  Output only a list, most severe first, one finding per line:
  `file:line | severity (critical/high/medium/low) | why it is wrong | concrete fix`
  If you find nothing real, output `No findings.` and the list of files you reviewed. State plainly that you did not build or run tests.
  ```
- **Purpose:** fresh-context review of `phase-09-quality`.
- **Outcome:** 5 findings (`R1`–`R5`), all fixed test-first in `0f4fcb1`, `0db33e8`, and `4f152ee`.

### P09-07 · agent→subagent (`spec-checker` in Antigravity · Gemini 4 Argon) · 2026-11-16
- **Prompt (verbatim):**
  ```
  Requirement traceability check at the end of Phase 9 (optimisation + quality) in /Users/ale_ssd/Developer/Mortgage-Application-API-test, branch phase-09-quality. Read only: do not edit files.

  Read:
  - `docs/spec/ORIGINAL_SPEC.md` and `docs/spec/affordability-rules.md`
  - `docs/layers/PRODUCTION-LAYERS.md` ("Database", "Caching", "Scaling")
  - `docs/PROGRESS.md` (the R1-R55 traceability table)
  - `docs/progress/phase-09-quality.md` and `docs/progress/phase-09/test-proposal.md`
  - The Phase 9 code and tests changed in `git diff main...HEAD`

  Do:
  1. Map every Phase 9 goal, decision (`P9-D1` to `P9-D4`), and approved test case (`P9-T1` to `P9-T9`) to the file:line and test name that meet it.
  2. Check how the R1–R55 traceability table in `docs/PROGRESS.md` changes after Phase 9 (specifically R25 "Clean and readable code" moving from `missing` to `met`, R27 test count moving to `584/584 + 113 hook cases`, and the updated overall summary counts).
  3. Check which rows of `docs/layers/PRODUCTION-LAYERS.md` ("Database", "Caching") need wording updates now that Phase 9 is complete.
  4. Output a Markdown table for the Phase 9 items and at most 5 short notes.
  ```
- **Purpose:** requirement traceability at the end of Phase 9.
- **Outcome:** **R25 (`Clean and readable code`)** moves from `missing` to **met**, bringing overall totals to **31 met · 10 partial · 14 missing**; all `P9-D1`–`P9-D4` and `P9-T1`–`P9-T9` met after `0f4fcb1` and `0db33e8`.

### P09-08 · human→agent (Antigravity · Gemini 4 Argon) · 2026-11-16
- **Prompt (verbatim):**
  ```
  Approved
  ```
- **Purpose:** Checkpoint B approval for PR #19 (`phase-09-quality`).
- **Outcome:** PR #19 approved and squash-merged into `main`.

## What was built (files)

Implemented in commits `dbc93ad` (failing tests), `31b6b89` (core/infra/api/rules implementation), `807ed6a` (`DetectChanges` in `P9-T2`), `0f4fcb1` (gate tests), `0db33e8` (`DatabaseMigration` WAL + `AssessmentProcessingResult` XML summary), and `4f152ee` (`OpenConnectionAsync` in `DatabaseQueryPlanTests`):

- `src/MortgageApplications.Core/Applications/AssessmentProcessingResult.cs` and `MortgageApplicationService.cs`:
  - `AssessmentProcessingResult` carries `string? RulesVersion = null` (populated from `result.RulesVersion` on `Completed`, `null` on `Failed`/`Skipped`/`NotFound`).
- `src/MortgageApplications.Infrastructure/Persistence/EfMortgageApplicationRepository.cs` and `DatabaseMigration.cs`:
  - `FindForProcessingAsync` projects the 5 scalar inputs (`AnnualIncome`, `MonthlyExpenses`, `DepositAmount`, `PropertyValue`, `TermYears`) instead of materializing/tracking the parent `MortgageApplication`.
  - `UpdateAssessmentAsync` calls `db.Assessments.Update(assessment)` only when `db.Entry(assessment).State == EntityState.Detached`, so for an already-tracked assessment EF Core writes only the modified columns (`Status`, `Result`, `AssessedAt`/`FailedAt`).
  - `DatabaseMigration.MigrateDatabaseAsync` executes `PRAGMA journal_mode=WAL;` after `MigrateAsync` so existing file-backed SQLite databases in `DELETE` mode are switched to `WAL`.
- `src/MortgageApplications.Api/Program.cs`, `Observability/AssessmentTelemetry.cs`, and `Assessments/AssessmentWorker.cs`:
  - `Program.cs` splits `OTEL_RESOURCE_ATTRIBUTES` on `,` (`RemoveEmptyEntries | TrimEntries`) and checks `kv.StartsWith("service.name=", StringComparison.Ordinal)` with a non-whitespace value.
  - `AssessmentTelemetry.RecordProcessed` sets `OutcomeTag` on `activity` once (removing the duplicate loop write), and `AssessmentWorker` passes `result.RulesVersion ?? affordabilityOptions.Value.RulesVersion!`.
- `.claude/rules/backend.md` and `tests/CLAUDE.md`:
  - Updated to reflect `P3-D1` (DTO mapping at the API boundary) and `P9-D4` (`AffordabilityCalculatorPropertyTests` exception).
- Tests (`tests/MortgageApplications.UnitTests/` and `tests/MortgageApplications.IntegrationTests/`):
  - `MortgageApplicationServiceTests.cs` (`P9-T1`, `P9-T7`), `AssessmentRepositoryTests.cs` (`P9-T2`, `P9-T8`), `DatabaseQueryPlanTests.cs` (new, `P9-T3`, `P9-T4`), `OtlpExportTests.cs` (`P9-T5`), `CapturingLoggerProvider.cs` and `AssessmentWorkerTests.cs` (`P9-T6` / `RR5`), `AssessmentSpanTests.cs` (`P9-T1`, `P9-T7`), and `JsonStrictnessTests.cs` (`P9-T9`).

## Tests

### Proposal (`test-designer`, P09-03)

Full proposal, verbatim: [`phase-09/test-proposal.md`](phase-09/test-proposal.md).

**Recommended selection:** all 9 cases (`P9-T1`–`P9-T7` `must`, plus `P9-T8` detached-update fallback and `P9-T9` shared `Production` host in `JsonStrictnessTests`).

### Results

- **Red first (`dbc93ad`, before implementation):**
  - `MortgageApplicationServiceTests`: 2 failed (`P9-T1` E1 and E2: `RulesVersion` was `null` instead of `"1.0"`).
  - `AssessmentRepositoryTests`: 1 failed (`P9-T2`: `db.ChangeTracker.Entries<MortgageApplication>()` was not empty).
  - `OtlpExportTests`: 2 failed (`P9-T5`: `"custom.service.name=other"` and `"deployment.environment=demo,service.name=   "` suppressed the default `"mortgage-applications"` service name).
- **Red then green on the gate findings (`0f4fcb1` → `0db33e8` + `4f152ee`):**
  - `DatabaseQueryPlanTests.MigrateDatabaseAsync_enables_wal_mode_on_file_database` (`0f4fcb1`): failed red with `Expected: "wal", Actual: "delete"` when the file-backed SQLite database was initialized in `DELETE` mode prior to `MigrateDatabaseAsync`. Passed after `0db33e8` added `await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;", cancellationToken);`.
- **Green (`4f152ee`):** `./scripts/verify.sh` full → 0 warnings, 0 errors, **586 tests** (`275` unit + `311` integration), **113 hook cases**, `VERIFY PASSED (full)`.
- **Integration tests 3 times in a row (after `4f152ee`):** run 1: `311/311` (17.5 s); run 2: `311/311` (17.2 s); run 3: `311/311` (17.4 s).

## Security gate

`security-auditor` (`P09-05`, Antigravity IDE · Gemini 4 Argon). **Verdict: PASS.**
- `gitleaks git --no-banner --redact -v` (169 commits) and `gitleaks dir --no-banner --redact .`: 0 leaks.
- `semgrep scan --config p/csharp --config p/secrets --error --quiet` (full and `--baseline-commit main`): 0 findings.
- `dotnet list MortgageApplications.slnx package --vulnerable --include-transitive`: 0 vulnerable packages.
- Manual OWASP Top 10:2025 review: 0 findings (`AssessmentProcessingResult.RulesVersion` is allowlisted in rules §6; `EfMortgageApplicationRepository` and `DatabaseQueryPlanTests` use parameterized EF Core commands; `OTEL_RESOURCE_ATTRIBUTES` parsing cannot throw or leak data).

## Review gate

1. **`dotnet-reviewer` (`P09-06`, Antigravity IDE · Gemini 4 Argon, read-only):**

| # | Finding | Severity | Status |
|---|---|---|---|
| R1 | `DatabaseQueryPlanTests.cs:36`: `P9-T3` explained hand-written `IQueryable` expressions instead of capturing the SQL commands executed by `EfMortgageApplicationRepository`, and omitted `IX_AffordabilityAssessments_ApplicationId`. | High | Fixed, `0f4fcb1` + `4f152ee`: `RecordingCommandInterceptor` (`DbCommandInterceptor`) captures the real commands from `FindAsync`, `FindAssessmentAsync`, `FindForProcessingAsync`, and the `OwnerId` and `ApplicationId` index queries, asserting both `IX_MortgageApplications_OwnerId` and `IX_AffordabilityAssessments_ApplicationId`. |
| R2 | `MortgageApplicationServiceTests.cs:354` & `AssessmentSpanTests.cs`: `P9-T1` lacked the custom `RulesVersion = "2.1"` unit test and a worker telemetry test where the calculator's stored `RulesVersion` (`"2.1"`) differs from `IOptions<AffordabilityOptions>.Value.RulesVersion` (`"1.0"`). | High | Fixed, `0f4fcb1`: added `ProcessAssessmentAsync_completed_reports_custom_rules_version_from_result` (`275` unit tests) and `Completed_span_uses_stored_rules_version_while_failed_falls_back_to_options` + single-`OutcomeTag` assertion in `AssessmentSpanTests`. |
| R3 | `DatabaseMigration.cs:13`: `MigrateDatabaseAsync` relied on EF Core's `Create()` default for WAL on new files, leaving an existing file in `DELETE` journal mode unchanged. | Medium | Fixed, `0f4fcb1` (test initializes file in `DELETE` mode, failed red) + `0db33e8` (`ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;")` in `MigrateDatabaseAsync`). |
| R4 | `DatabaseQueryPlanTests.cs:1`: unused `using` directives before `DbCommandInterceptor` was added. | Low | Fixed, `0f4fcb1` (both namespaces used by `RecordingCommandInterceptor`). |
| R5 | `AssessmentProcessingResult.cs:6`: XML `<summary>` omitted `RulesVersion`. | Low | Fixed, `0db33e8`. |

2. **`spec-checker` (`P09-07`, Antigravity IDE · Gemini 4 Argon, read-only):**
   - **R25 (`Clean and readable code`):** `missing` → **met** (overall totals: **31 met · 10 partial · 14 missing**; all remaining 24 items belong to Phase 10 `README.md` or Phase 11 rehearsal/slides).
   - All `P9-D1`–`P9-D4` and `P9-T1`–`P9-T9` verified as **met**.

## AI output corrected or rejected (REAL cases only)

- **EF Core `DetectChanges()` timing in `P9-T2` (`dbc93ad`):** `AssessmentRepositoryTests.FindForProcessingAsync_does_not_track_application_and_update_modifies_only_changed_columns` initially read `entry.Properties.Where(p => p.IsModified)` right after `work.Assessment.Complete(...)` without calling `db.ChangeTracker.DetectChanges()`, returning `[]` because snapshot change tracking runs on `DetectChanges()` / `SaveChangesAsync()`. Fixed in `807ed6a`.
- **`DatabaseQueryPlanTests` SQLite `.ToQueryString()` and `PRAGMA journal_mode=WAL` (`dbc93ad`):**
  - `.ToQueryString()` prepends SQLite CLI `.param set` lines, which failed `EXPLAIN QUERY PLAN`; replaced with `RecordingCommandInterceptor` (`0f4fcb1`, `R1`).
  - `MigrateDatabaseAsync_enables_wal_mode_on_file_database` initially passed without modifying `DatabaseMigration.cs` because EF Core creates brand-new SQLite files in WAL mode; caught by `dotnet-reviewer` (`R3`), proven red by initializing the temp file in `DELETE` mode (`0f4fcb1`), and fixed in `DatabaseMigration.cs` (`0db33e8`).

## Optimisations considered

- **Scalar projection + tracked-only `UpdateAssessmentAsync` (`EfMortgageApplicationRepository.cs`, `P9-D1 A`):** `FindForProcessingAsync` no longer materializes or tracks the parent `MortgageApplication` entity on every background assessment, and `UpdateAssessmentAsync` writes only the 3 modified columns (`Status`, `Result`, `AssessedAt`/`FailedAt`) instead of all 7 columns (`P9-T2`).
- **Shared `Production` host in `JsonStrictnessTests` (`P9-D1 A`, `P9-T9`):** reuses one `Production` `WebApplicationFactory<Program>` across the class; measured `JsonStrictnessTests` duration is **2.03 s** for all 13 cases (`dotnet test --filter-class "*JsonStrictnessTests"`).
- **Index & caching verification (`P9-D2 A`, `DatabaseQueryPlanTests`):** all 5 repository/index queries execute in `< 1 ms` in-process using SQLite index/primary-key `SEARCH` plans (`IX_MortgageApplications_OwnerId`, `IX_AffordabilityAssessments_ApplicationId`, and `PK_MortgageApplications` / `PK_AffordabilityAssessments`) with `0` `SCAN` lines, and all responses are per-owner (`Cache-Control: no-store`), confirming output caching is unnecessary.

## Open issues

- None for Phase 9. Multi-instance worker scaling items (`RowVersion` concurrency token and `AttemptCount` retry cap, `P9-D3 A`) are documented in `PRODUCTION-LAYERS.md` ("Scaling") and carried into Phase 10 (`README.md`).

## Time spent (approx.)

~30 min (`2026-11-16 02:35 UTC` to `03:05 UTC`, commits `6983b6f`–`4f152ee`, in Google Antigravity IDE · Gemini 4 Argon).

## Human review (Alessandro writes this; the agent leaves it empty)
