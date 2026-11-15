# Phase 06: Background assessments

Status: **In progress: plan and tests approved (2026-11-15), implementation running** · Branch: `phase-06-background` (worktree, parallel with Phase 4) · PR: — · Started: 2026-11-14 · Finished: —

## Goal

Assessments run in the background (master prompt B5, Phase 6; R30).
- `POST /api/applications/{id}/assessments` saves a Pending assessment and returns **202 Accepted** + `Location` of `GET …/assessments/{assessmentId}`.
- A queue behind an interface (`IAssessmentQueue`) on a bounded `Channel<Guid>`; an `AssessmentWorker : BackgroundService` processes one item at a time, with a DI scope per item.
- Idempotent: an item whose assessment is no longer Pending is skipped. An exception marks the assessment Failed.
- On startup, every Pending assessment is put back on the queue (nothing is lost on a restart).
- ADR: the queue behind an interface, replaced by Azure Service Bus (or similar) at scale.

Carried in from Phase 3 (`docs/PROGRESS.md`): no silent queue drops; check `Status` before `Complete`; the POST becomes 202 (`AssessmentResponse.Result` is already null-able for Pending); the cost of the result comparer once the worker tracks assessments (`/code-review` 6).

Not in scope: metrics and the custom span (Phase 5 adds the `Meter` and `ActivitySource`), retries of Failed assessments, authentication (Phase 7), more than one worker or more than one API instance.

## Parallel safety with Phase 4 (master prompt A10, B5)

Phase 4 touches only `Dockerfile`, `compose.yaml`, `.dockerignore`, `.github/workflows/ci.yml` and ADR 0002 (if Phase 4's P4-D1 option A is approved). This phase touches only `src/`, `tests/`, ADR **0003** and this file. If Phase 4 picks P4-D1 option B (a `Program.cs` switch), the two phases must run one after the other.

Shared evidence files (`docs/PROGRESS.md`, `docs/ai/PROMPTS.md`, `DESIGN-JOURNAL.md`, `ORCHESTRATION.md`) are not edited in the worktree; prompts are logged here and copied into `PROMPTS.md` on `main` after the merge. Phase 4 merges first; this branch then rebases on `main` and re-runs all gates.

## Plan (approved at Checkpoint A)

1. **Branch** `phase-06-background` in its own worktree (`parallel-implementer`).
2. **Docs check** on learn.microsoft.com, links recorded below: `BackgroundService` and hosted services (startup order, `ExecuteAsync`, exceptions in .NET 8+ `BackgroundServiceExceptionBehavior`), consuming scoped services from a background task (`IServiceScopeFactory.CreateAsyncScope`), `System.Threading.Channels` (`Channel.CreateBounded`, `BoundedChannelFullMode`, `WriteAsync`/`TryWrite`, `ReadAllAsync`), `TypedResults.Accepted` / `AcceptedAtRoute`, EF Core change tracking for updates (`Update`, attach, `SaveChangesAsync`), SQLite concurrency (one writer, busy timeout) and in-memory shared-cache databases.
3. **Test design.** `test-designer` proposes cases. Draft: POST returns 202 + `Location` + body with `status: Pending` and `result: null`; polling the `Location` reaches Completed with the golden E1 values (timeout 10 s, so a hang fails the test); Declined still completes (not Failed); unknown or another owner's application → 404 and nothing queued; the worker skips an assessment that is not Pending (idempotency: the same ID queued twice is processed once); an exception in the calculation marks it Failed and the worker keeps going with the next item; Pending assessments in the database at startup are processed; a full queue behaves as P6-D2 says (no lost item); cancellation on shutdown leaves the item Pending, not Failed. **Alessandro picks the cases.**
4. **Red, then green**, in small commits:
   - Core: `IAssessmentQueue` (port, P6-D1); `MortgageApplicationService.AssessAsync` saves Pending and enqueues; a new use case `ProcessAssessmentAsync(Guid assessmentId)` that loads the assessment and its application inputs (internal, not owner-scoped: the ID comes from our own queue), returns early unless Pending, calls `Complete`, or `Fail` on an exception, and saves; repository additions (`FindForProcessingAsync`, `UpdateAssessmentAsync`, `ListPendingAssessmentIdsAsync`).
   - Infrastructure: the repository methods. Check whether `ListPending…` needs an index on `Status` (a new migration, never an edit of `InitialCreate`; the hook blocks that anyway).
   - Api: `ChannelAssessmentQueue` (singleton, bounded capacity from config, P6-D2), `AssessmentWorker` (startup re-enqueue, then read the channel, scope per item, catch and log per item without personal data, honour the stopping token), registration in `Program.cs`, the endpoint returns 202, `AssessmentResponse` gains a nullable `failedAt` (Q11; no failure reason), OpenAPI summary updated, `requests.http` updated with a poll step.
   - Tests: the test factory change from P6-D3; existing `AssessmentTests` move from 201 to 202 + polling; unit tests for `ProcessAssessmentAsync` with fakes.
   - ADR 0003 (the queue choice), via `/adr` or written by the agent if Alessandro says so.
5. **Verify.** `./scripts/verify.sh` (full), output in this file. Run the integration tests 5 times in a row to check for timing flakiness; record the result.
6. **Security gate.** `security-auditor` on the diff (no personal data in worker logs, the internal non-owner-scoped lookup is reachable only from the worker, no unbounded memory).
7. **Review gate.** `dotnet-reviewer` on `git diff main...HEAD`, then `spec-checker` (R30 should move to met; R13 updated for 202).
8. **Evidence.** This file; prompts copied to `PROMPTS.md` after merge.
9. **Rebase** on `main` after Phase 4 merges, re-run steps 5-7, then **PR**, local `/code-review` + `/security-review`, one PR comment, CI. **Checkpoint B.**

## Decisions for Alessandro (2-3 options · trade-offs · recommendation)

**P6-D1. Where `IAssessmentQueue` lives.**
- **A. Interface in Core, channel implementation and worker in Api** (recommended). The use case enqueues without knowing the transport; the worker is hosting code, which `AGENTS.md` puts in Api. Swapping to Azure Service Bus means one new implementation in Infrastructure or Api.
- **B. Interface and implementation both in Api**; the endpoint enqueues after the service saves. Core stays unchanged, but "save Pending, then enqueue" is split across two layers, so a future caller of the use case could forget the second step.

**P6-D2. What happens when the bounded queue is full** (Phase 3 carry-over: no silent drops).
- **A. `BoundedChannelFullMode.Wait` + `WriteAsync`** (recommended). The POST waits until there is room (back-pressure), bounded by the request's cancellation token. If the client gives up, the assessment is already saved as Pending and the next startup re-enqueue picks it up. Capacity from config (default 100).
- **B. `TryWrite`; when full, return 503 + `Retry-After`** and mark the saved assessment Failed (or never save it). The client learns about overload straight away; more code and a new error path to test.
- **C. Unbounded channel.** Simplest, never blocks; memory can grow without limit under load. Not recommended.

**P6-D3. Integration test database once a worker runs on another thread.** Today `ApiFactory` shares **one open `SqliteConnection`** across all contexts. A connection must not be used by two threads at once, and the worker and a request would now do that.
- **A. Named in-memory database with shared cache** (`Data Source=<guid>;Mode=Memory;Cache=Shared`, the form on Learn), one connection kept open to keep it alive, each `DbContext` opening its own connection (recommended). Still fast and isolated per factory; matches the real app's one-connection-per-context behaviour.
- **B. A temporary database file per factory**, deleted on dispose. Closest to production SQLite (including WAL and locking); slower and leaves files behind if a test run is killed.

**P6-D4. HTTP status of `GET …/assessments/{assessmentId}` while Pending.**
- **A. Always 200**, with `status` telling the client Pending / Completed / Failed (recommended). Simple to poll; the response shape is already built for it.
- **B. 202 while Pending, 200 when finished, with `Retry-After`.** A common async pattern, but two status codes for one resource and more client code.

## Decisions (option chosen · alternatives · reason · decided by) → ADR links

Alessandro approved the plan with "Proceed" on 2026-11-15, after the agent recommended option A for each decision. All four take the recommended option.

| ID | Chosen | Alternatives | Reason | Decided by |
|---|---|---|---|---|
| P6-D1 | A: `IAssessmentQueue` in Core; channel implementation and worker in Api | B everything in Api | "Save Pending, then enqueue" stays in one use case; hosting code stays in Api | Alessandro |
| P6-D2 | A: bounded channel, `BoundedChannelFullMode.Wait` + `WriteAsync`, capacity from config (default 100) | B `TryWrite` + 503; C unbounded | No silent drops; a saved Pending item is recovered by the startup re-enqueue | Alessandro |
| P6-D3 | A: named shared-cache in-memory SQLite per factory, one keep-alive connection, one connection per `DbContext` | B temporary file per factory | A SQLite connection must not be used by two threads; stays fast and isolated | Alessandro |
| P6-D4 | A: `GET …/assessments/{assessmentId}` always 200, `status` field tells the state | B 202 while Pending | Simple to poll; response shape already supports it | Alessandro |

## Docs verified (links)

Checked by the lead agent on 2026-11-15, before implementation.

- [Background tasks with hosted services in ASP.NET Core (.NET 10)](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services?view=aspnetcore-10.0):
  - In .NET 10, `ExecuteAsync` "is called on the thread pool"; the host waits for it in `StopAsync`. The stopping token fires on shutdown (default timeout 30 s).
  - A hosted service is a singleton, so it creates a scope to use scoped services such as a `DbContext`.
  - The queued-task sample uses `Channel.CreateBounded` with `BoundedChannelFullMode.Wait`, capacity from config (`QueueCapacity`, default 100), and its web sample returns 202 after enqueueing: "If the bounded queue is full, the request waits until space is available". This matches P6-D2.
  - The sample's loop catches each work item's exception and logs it, so one failure does not stop the loop.
- [.NET 6 breaking change: exception handling in hosting](https://learn.microsoft.com/en-us/dotnet/core/compatibility/core-libraries/6.0/hosting-exception-handling): an exception escaping `ExecuteAsync` is logged and **stops the host** by default. So the worker must catch per item; only cancellation on shutdown may leave the loop.
- [Channels](https://learn.microsoft.com/en-us/dotnet/core/extensions/channels): `Wait` is the default full mode; "Calls to `WriteAsync` wait for space"; `TryWrite` returns false at once. The `Drop*` modes drop items, so they are ruled out. `ReadAllAsync` returns an `IAsyncEnumerable<T>`.
- [TypedResults.Accepted](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.http.typedresults.accepted?view=aspnetcore-10.0): `Accepted<TValue>(string? uri, TValue? value)` returns `Accepted<TValue>`, so the endpoint stays typed (`Results<Accepted<AssessmentResponse>, NotFound>`).
- [In-memory databases (Microsoft.Data.Sqlite)](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/in-memory-databases): a shared in-memory database uses `Data Source=<name>;Mode=Memory;Cache=Shared`; it lives while at least one connection is open. P6-D3 uses this form (the plan's `file:` URI form is replaced by it).
- [Database errors (Microsoft.Data.Sqlite)](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/database-errors): `SqliteConnection`, `SqliteCommand` and `SqliteDataReader` "cannot be shared and used concurrently from multiple threads". This confirms the P6-D3 risk. On a busy or locked error the library retries until the command timeout (default 30 s), so short write contention between the worker and a request is retried rather than failed.
- Not re-checked here: EF Core change tracking for updating a detached entity. The implementer must load the assessment tracked (or attach it) before `SaveChangesAsync`. Flagged for the `dotnet-reviewer`.

## Orchestration used

## Prompts

Logged here during the parallel run and copied to `docs/ai/PROMPTS.md` on `main` after the merge. The human prompts for planning and approval (P04-01 to P04-03) are in `phase-04-docker.md`, "Prompts".

### P06-01 · agent→subagent (`test-designer`) · 2026-11-15
- **Prompt (verbatim):**
  ```
  Phase 6 (background assessments) of a .NET 10 mortgage-application API (Minimal APIs + EF Core SQLite). Propose test cases for the approved plan; do not write code.

  Read first: docs/progress/phase-06-background.md (goal, plan, approved decisions P6-D1 to P6-D4), docs/spec/affordability-rules.md (section 8 golden examples), .claude/rules/tests.md, tests/CLAUDE.md, .claude/rules/security.md, and the current code and tests: src/MortgageApplications.Core/Applications/ (MortgageApplicationService, AffordabilityAssessment, IMortgageApplicationRepository), src/MortgageApplications.Infrastructure/Persistence/EfMortgageApplicationRepository.cs, src/MortgageApplications.Api/Applications/ApplicationEndpoints.cs, src/MortgageApplications.Api/Program.cs, tests/MortgageApplications.IntegrationTests/ (ApiFactory, AssessmentTests, NotFoundAndOwnerTests) and tests/MortgageApplications.UnitTests/Applications/ (Fakes, MortgageApplicationServiceTests, AffordabilityAssessmentTests). Phase 2 and 3 already cover the calculator, the entity state machine and the synchronous endpoint: don't repeat those, but say which existing tests must change.

  What will be built (not written yet):
  - Core: IAssessmentQueue (port) with an async enqueue of an assessment ID; MortgageApplicationService.AssessAsync saves a Pending assessment, then enqueues its ID, and returns the Pending assessment; a new use case ProcessAssessmentAsync(Guid assessmentId) that loads the assessment and its application's inputs without an owner filter (the ID comes from our own queue), returns without change unless the assessment is Pending, otherwise calls Complete with the calculator result, or Fail when the calculation throws, and saves; repository additions for that load, an update, and listing Pending assessment IDs.
  - Api: ChannelAssessmentQueue (singleton, Channel.CreateBounded with BoundedChannelFullMode.Wait, WriteAsync honouring the request's cancellation token, capacity from configuration, default 100); AssessmentWorker : BackgroundService that first re-enqueues every Pending assessment from the database, then reads the channel one item at a time with a new DI scope per item, catches and logs per-item exceptions without personal data and keeps going, and stops on the stopping token; POST /api/applications/{id}/assessments returns 202 Accepted + Location of GET /api/applications/{id}/assessments/{assessmentId} with body status Pending and result null; GET of an assessment is always 200 with a status field (P6-D4).
  - Tests: ApiFactory moves to a named shared-cache in-memory SQLite database per factory with one keep-alive connection and one connection per DbContext (P6-D3); integration tests poll the Location with a timeout.

  Cover at least: 202 + Location + Pending body; polling reaches Completed with golden E1 values within a timeout; a Declined case completes (not Failed); unknown, malformed or another owner's application id → 404 and nothing queued and nothing saved; idempotency (the same ID processed twice, or an already Completed or Failed assessment, is not changed); calculation exception → Failed with FailedAt, and the worker processes the next item afterwards; Pending rows present at startup are processed; enqueue order (save before enqueue, so a crash between them leaves a recoverable Pending row); full queue: WriteAsync waits and nothing is dropped; cancellation of the POST while waiting leaves the saved row Pending; shutdown during processing does not mark the item Failed; ProcessAssessmentAsync with an unknown ID does nothing and does not throw; no income, expenses, deposit or property value in worker log messages; tests are not timing-flaky (say how: polling helper, TimeProvider, deterministic fakes). Say which cases belong in unit tests (Core with fakes) versus integration tests.

  Use the agent's output format. Mark each case must or nice, keep the list tight (no redundant cases), and list any ambiguities in the plan you need Alessandro to settle.
  ```
- **Purpose:** propose Phase 6 test cases before any test is written.
- **Outcome:** 17 unit, 4 repository, 15 integration and 6 worker-level cases, a list of existing tests that must change, and 12 questions (Q1–Q12). The lead agent checked Q8, Q10 and Q11 against the code and Learn (see "Tests", "Proposal"). Q10's claim that a busy timeout does not cover shared-cache locks is contradicted by Learn: Microsoft.Data.Sqlite retries both busy and locked errors until the command timeout.

## What was built (files)

## Tests

### Proposal (test-designer P06-01, approved by Alessandro P04-05: recommended selection and answers, including `failedAt` in the response)

The full list is in the subagent's report. Golden values are copied from rules §8 and the existing `AssessmentTests`, not recomputed: E1 = 240,000.00 / 80.00 / 1,438.92 / 33.02 / 2.82 / 381,535.82 / Approved; E2 = 280,000.00 / 93.33 / 1,678.74 / 36.41 / 3.29 / 381,535.82 / Declined.

**Existing tests that change.** `AssessmentTests` I4–I7 and F3 move from 201 to 202, then poll to Completed. `NotFoundAndOwnerTests` keep their 404 checks against the new flow. `MortgageApplicationServiceTests` U2/U3 split into "AssessAsync saves Pending and enqueues" and "ProcessAssessmentAsync completes with the golden values"; U6 also asserts that nothing was enqueued. `Fakes.cs` gains the new repository methods and a recording `FakeAssessmentQueue`.

**Recommended selection.**

| ID | Level | Case |
|---|---|---|
| U-A1 | unit | `AssessAsync` returns Pending (`result`, `assessedAt`, `failedAt` null), one row saved, the queue holds exactly its ID |
| U-A2 | unit | Call order is save, then enqueue (a crash in between leaves a recoverable Pending row) |
| U-S1 | unit | Another owner's or an unknown application → null, nothing saved, nothing queued |
| U-P1 / U-P2 | unit | `ProcessAssessmentAsync` completes E1 (golden values, `AssessedAt` from the clock) and E2 (Declined, not Failed) |
| U-P4 | unit | Calculation throws → Failed with `FailedAt`, no exception escapes |
| U-P5 / U-P6 | unit | Already Completed or Failed → unchanged, no update call, calculator not called |
| U-P7 | unit | Unknown ID (or no application for it, Q4) → no change, no throw |
| U-P10 | unit | Save after `Complete` throws → the exception reaches the worker; the row stays Pending (Q5) |
| U-P11 / I-R1 | unit + SQLite | `ListPendingAssessmentIdsAsync` returns only Pending IDs (the real query checks the string enum mapping) |
| I-R2 / I-R3 | SQLite | Completed and Failed updates survive a reload in a fresh scope |
| I-1 | HTTP | POST → 202, exact `Location`, body `status: Pending`, `"result":null` in the raw JSON |
| I-2 / I-3 | HTTP | Polling reaches Completed with the E1 golden values and raw 2 dp text; E2 completes as Declined |
| I-4 | HTTP | GET while Pending (worker held by a gate) → 200, never 202 (P6-D4) |
| I-5 / I-6 | HTTP | Unknown, other-owner and malformed IDs → identical 404s, no row added, nothing enqueued; other owner's assessment GET → 404 |
| I-7 | HTTP | Pending rows seeded before the host starts are completed; a seeded Completed row is unchanged |
| I-8 | HTTP | The same ID queued twice is processed once, with no error logged |
| I-9 / I-10 | HTTP | Capacity 1 and a held worker: the third POST waits (checked by `Task.IsCompleted` once the queue is known full, not by a sleep); after release all three complete. Cancelling the waiting POST leaves its row Pending, and a restarted host completes it |
| I-11 | HTTP | Shutdown while processing does not mark anything Failed |
| I-13 / I-14 | HTTP | One failing item becomes Failed (`failedAt` set) and the next item still completes; the captured logs contain no input amounts and no owner ID |
| W-5 | integration project | `ChannelAssessmentQueue`: the second write waits at capacity 1; a cancelled wait throws and adds nothing |
| W-6 | integration project | Stopping an idle worker returns within 5 s with no error logged |
| Q7 | HTTP | `Assessments:QueueCapacity` = 0 fails at startup |

Dropped as covered elsewhere: W-1 and W-2 (one reader loop and a scope per item are checked by review), W-3 (I-13), W-4 (I-7), U-P3 (I6 covers E18 end to end), U-P9 (folded into Q5), I-12 (I-10 already restarts a host), U-S2 and I-R4 (documentation only; the `security-auditor` checks that the unscoped load is reachable only from the worker). Kept as nice: I-15 (no amounts in success-path logs) and a 20-request concurrent POST test, both run by hand a few times.

**Flakiness.** One polling helper (25 ms interval, 10 s timeout, last body in the failure message), a gate that holds the worker wherever Pending must be observed, `[Fact(Timeout = …)]` on polling tests, and the 5-run check from plan step 5.

**Questions, with the recommended answer** (checked against the code where marked):
- **Q1. Making the calculation throw.** No new interface. The repository's processing load returns the assessment plus the application's `AffordabilityInputs` (a public record), so a unit fake can return invalid inputs and the real calculator throws (`InputGuard`). Integration tests (I-13) insert an invalid application row directly with SQL, the real "row written before a rules change" failure.
- **Q2. Several hosts per factory.** `WithWebHostBuilder` hosts would each start a worker on the shared database. → The worker runs in the factory's default host; derived hosts remove it unless a test opts in. Two workers on one database stay out of scope (one worker, one instance) and go into ADR 0003 as a scale item.
- **Q3. Seeding before the worker starts (I-7).** → Build a first host without the worker, seed, then start a second host with the worker on the same database. This also models a restart.
- **Q4. Orphan Pending row.** The foreign key cascades, so it should not happen; covered by U-P7 (no change, no throw).
- **Q5. A failure while saving or failing.** → Leave the row Pending and let the worker log it (the next startup re-enqueue retries it). The use case catches only the calculation; it marks Failed only for a calculation error.
- **Q6. Unscoped load reachable only from the worker.** → Checked by the `security-auditor`; no architecture-test package.
- **Q7. Capacity setting.** → `Assessments:QueueCapacity`, default 100, validated at startup (1 to 10,000), like `AffordabilityOptions`.
- **Q8. Inputs in exception messages.** Checked: `InputGuard` builds `ArgumentOutOfRangeException(field, message)` and never puts the value in the message (`src/MortgageApplications.Core/Affordability/InputGuard.cs`). I-14 still tests the logs.
- **Q9. Where worker tests live.** → Integration project (`tests.md`: unit tests are Core only). No new project reference.
- **Q10. SQLite locking with a shared cache.** Learn says the library retries busy and locked errors until the command timeout (30 s). → Keep P6-D3. If the 5-run check flakes, stop and come back to Alessandro with option B (temporary file).
- **Q11. `failedAt` in the response.** Checked: `AssessmentResponse` has no `FailedAt` today (`src/MortgageApplications.Api/Applications/Responses.cs`); the column exists in `InitialCreate`. → Add `failedAt` (nullable) to the response. No failure reason is exposed.
- **Q12.** Each POST still creates a new assessment; no change.

## Security gate

## Review gate

## AI output corrected or rejected (REAL cases only)

## Optimisations considered

## Open issues

## Time spent (approx.)

## Human review (Alessandro writes this; the agent leaves it empty)
