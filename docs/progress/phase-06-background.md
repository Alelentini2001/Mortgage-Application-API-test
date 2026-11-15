# Phase 06: Background assessments

Status: **In progress: implementation, security and review gates done; ADR 0003 drafted; waiting for the Phase 4 merge, rebase and PR** · Branch: `phase-06-background` (worktree, parallel with Phase 4) · PR: — · Started: 2026-11-14 · Finished: —

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
| P6-D2 | A: bounded channel, `BoundedChannelFullMode.Wait` + `WriteAsync`, capacity from config (default 100) | B `TryWrite` + 503; C unbounded | No silent drops; a saved Pending item is recovered by the startup re-enqueue (ADR 0003) | Alessandro |
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
- Not re-checked before implementation: EF Core change tracking for updating a detached entity. Flagged for the `dotnet-reviewer` (it found no problem) and checked afterwards, below.

Checked by the lead agent on 2026-11-15, after implementation (the implementer had no web access):
- [BackgroundService.ExecuteTask](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.hosting.backgroundservice.executetask?view=net-10.0): the task of the long-running operation, null until `StartAsync` runs. Used by W-6.
- [ChannelReader<T>.Count](https://learn.microsoft.com/en-us/dotnet/api/system.threading.channels.channelreader-1.count?view=net-10.0): current number of items; a bounded channel supports it (`CanCount`). Used by tests only.
- [ValidateDataAnnotations](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.dependencyinjection.optionsbuilderdataannotationsextensions.validatedataannotations?view=net-10.0): registers DataAnnotations validation for the options type. Used with `ValidateOnStart`.
- [SqliteConnection.ClearPool](https://learn.microsoft.com/en-us/dotnet/api/microsoft.data.sqlite.sqliteconnection.clearpool?view=msqlite-10.0): empties the pool for that connection string. Used by `ApiFactory` on dispose.
- [Explicit tracking (EF Core)](https://learn.microsoft.com/en-us/ef/core/change-tracking/explicit-tracking): `Update` marks the entity Modified whether or not it is tracked. The processing load is tracked anyway.
- [Task.Delay(TimeSpan, TimeProvider, CancellationToken)](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.task.delay?view=net-10.0): available from .NET 8; throws `OperationCanceledException` (stored in the task) when the token is cancelled; delays above 4,294,967,294 ms are rejected (the option's maximum is 3,600 s). Used by the sweep retry (R3).

## Orchestration used

- Ran in parallel with Phase 4, each phase in its own git worktree (`.claude/worktrees/agent-acd40a009ddf8a3cd` for this one), with no shared files (see "Parallel safety").
- `test-designer` proposed the cases (P06-01). `parallel-implementer` wrote the code and tests (P06-02), was resumed once after its 60-turn limit (P06-03) and twice for the review-gate fixes (P06-06, P06-07).
- `security-auditor` (P06-04) and `dotnet-reviewer` (P06-05) ran in fresh contexts on the diff at `850a3a8`.
- The lead agent made the two fixes Alessandro approved on 2026-11-15 (R3 and R4) and wrote ADR 0003.

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

### P06-02 · agent→subagent (`parallel-implementer`) · 2026-11-15
- **Prompt (verbatim):**
  ```
  Implement Phase 6 (background assessments) of this .NET 10 mortgage-application API, exactly as approved.

  Branch: switch to the existing branch phase-06-background (it holds one commit with the approved plan). Do not create another branch. Never push.

  Scope: docs/progress/phase-06-background.md. The sections "Plan (approved at Checkpoint A)", "Decisions" (P6-D1 to P6-D4), "Docs verified" and "Tests > Proposal" (the recommended selection and the answers to Q1-Q12, all approved) are your scope. Read AGENTS.md, CLAUDE.md, .claude/rules/*.md, tests/CLAUDE.md and src/MortgageApplications.Core/CLAUDE.md first.

  Key points from the approved answers (the phase file is the source of truth if anything differs):
  - IAssessmentQueue in Core; ChannelAssessmentQueue and AssessmentWorker in Api. Bounded channel, BoundedChannelFullMode.Wait, WriteAsync with the request's cancellation token. Capacity from `Assessments:QueueCapacity` (default 100, validated at startup 1 to 10,000, like AffordabilityOptions).
  - AssessAsync saves Pending, then enqueues (in that order). ProcessAssessmentAsync(Guid) loads the assessment plus the application's AffordabilityInputs without an owner filter, returns unless Pending, catches only the calculation (Fail on a calculation error), saves. A failure while saving propagates to the worker, which logs it and leaves the row Pending. No new calculator interface (Q1).
  - The worker re-enqueues all Pending IDs at startup, then reads one item at a time with a new async DI scope per item, catches and logs per item (assessment ID only, never amounts or owner ID), and treats cancellation on the stopping token as shutdown, never as Failed. An exception escaping ExecuteAsync stops the host (Learn link in the phase file), so nothing but shutdown may leave the loop.
  - POST /api/applications/{id}/assessments → 202 Accepted (TypedResults.Accepted<AssessmentResponse>) + Location of GET …/assessments/{assessmentId}. GET is always 200. AssessmentResponse gains nullable failedAt; no failure reason.
  - ApiFactory: named shared-cache in-memory SQLite (`Data Source=<guid>;Mode=Memory;Cache=Shared`), one keep-alive connection, one connection per DbContext. The worker runs in the factory's default host; WithWebHostBuilder-derived hosts remove it unless a test opts in.
  - If ListPending needs an index, add a new migration; never edit InitialCreate (a hook blocks it).
  - No new packages. No logging of income, expenses, deposit, property value, tokens or the owner ID.

  Do not edit: Dockerfile, compose.yaml, .dockerignore, .github/, docs/PROGRESS.md, docs/ai/*, any ADR file (ADR 0003 is written later by the lead agent). Only the phase file under docs/.

  Process:
  1. Test-first: write the approved tests and the changes to existing tests; run them and confirm the new ones fail for the right reason. Keep that output.
  2. Implement until green. Small conventional commits (AGENTS.md "Commits and PRs"; scopes core, api, infra, tests). End each commit message with: Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
  3. Run ./scripts/verify.sh (full) and keep the final output. Then run the integration tests 5 times in a row (dotnet test --project tests/MortgageApplications.IntegrationTests) and record pass/fail for each run. If any run flakes on SQLite locking, stop and report (Q10 says that goes back to Alessandro); do not switch to a temp-file database yourself.
  4. In the phase file, fill "What was built (files)", "Tests" > a new "Results" subsection (added/total/passing, the red-then-green evidence, the 5-run result) and "Open issues" for anything you could not do. Commit that.

  Report: files changed, commits, trimmed verify output, the 5-run result, every place you deviated from the phase file and why, and anything you could not do. If the plan is ambiguous or a step fails three times, stop and report instead of guessing.
  ```
- **Purpose:** implement the approved Phase 6 plan, test-first, in its own worktree.
- **Outcome:** stopped at its 60-turn limit after 4 commits (`4a8ed68`, `e531691`, `d249652`, `7215f8d`) with a clean tree.

### P06-03 · agent→subagent (`parallel-implementer`, resumed) · 2026-11-15
- **Prompt (verbatim):**
  ```
  Finish Phase 6: you hit the turn limit after 4 commits (4a8ed68, e531691, d249652, 7215f8d) and a clean tree. Do only the remaining steps from your original instructions, with no new features:
  1. If any approved must-case from the phase file is still missing, list it (do not write it now unless it is a few lines).
  2. Run ./scripts/verify.sh (full) and keep the final output.
  3. Run the integration tests 5 times in a row and record pass/fail for each run. If a run flakes on SQLite locking, stop and report.
  4. Fill the phase file: "What was built (files)", "Tests" > "Results" (added/total/passing, red-then-green evidence if you kept it, otherwise say it was not kept, plus the 5-run result), and "Open issues". Commit that.
  5. Deliver your report: files changed, commits, trimmed verify output, the 5-run result, every deviation from the phase file and why, and anything not done. Keep tool use tight; you have limited turns.
  ```
- **Purpose:** finish the remaining steps after the turn limit, with no new features.
- **Outcome:** full verify passed (373 tests); integration tests passed 5 runs in a row; phase-file sections "What was built", "Results" and "Open issues" written (commit `850a3a8`).

### P06-04 · agent→subagent (`security-auditor`) · 2026-11-15
- **Prompt (verbatim):**
  ```
  Security gate for Phase 6 (background assessments) of a .NET 10 mortgage-application API. Review only the branch diff: run `git diff main...phase-06-background` from the repository root (the branch is checked out in another worktree; do not switch branches here; read branch files with `git show phase-06-background:<path>`).

  Read first: .claude/rules/security.md, AGENTS.md, docs/progress/phase-06-background.md on the branch (plan, decisions P6-D1 to P6-D4, approved answers Q1-Q12, "Open issues" with the implementer's deviations).

  Check at least: the non-owner-scoped processing load (`ProcessAssessmentAsync` and the repository method behind it) is reachable only from `AssessmentWorker`, never from an endpoint or with a caller-supplied ID; POST and GET keep owner scoping and return identical 404s for unknown, other-owner and malformed IDs, with nothing saved or queued on 404; no log message, log state, exception message or ProblemDetails carries income, expenses, deposit, property value or the owner ID (check every `[LoggerMessage]` and every logged exception path); `AssessmentResponse.failedAt` exposes no failure reason; the bounded queue cannot grow without limit and `Assessments:QueueCapacity` is validated at startup; a POST waiting on a full queue cannot be used to exhaust resources beyond what the capacity and request cancellation allow (say what the remaining denial-of-service exposure is and whether it belongs to Phase 8 rate limiting); the worker cannot be stopped by one bad item; shutdown never marks Failed; any `CA1711` or other analyzer suppressions are justified. Run Gitleaks on the branch commits (`gitleaks git --log-opts="main..phase-06-background"` or closest form), Semgrep (`p/csharp`, `p/secrets`) on the changed `.cs` files from the branch worktree at /Users/ale_ssd/Developer/Mortgage-Application-API-test/.claude/worktrees/agent-acd40a009ddf8a3cd, and `dotnet list package --vulnerable --include-transitive` there; say which tools ran and which were not available. Do not install anything.

  Use your output format: each finding with severity, file:line, evidence, real or false positive, and a proposed fix or justification. Do not edit files.
  ```
- **Purpose:** security gate on the branch diff.
- **Outcome:** PASS. One medium finding (S1), two low and three info notes. See "Security gate".

### P06-05 · agent→subagent (`dotnet-reviewer`) · 2026-11-15
- **Prompt (verbatim):**
  ```
  Fresh-context review of Phase 6 (background assessments) of a .NET 10 mortgage-application API. Review only the branch diff: run `git diff main...phase-06-background` from the repository root (the branch is checked out in the worktree /Users/ale_ssd/Developer/Mortgage-Application-API-test/.claude/worktrees/agent-acd40a009ddf8a3cd; read files there; do not switch branches or edit anything).

  Read first: AGENTS.md, .claude/rules/backend.md, .claude/rules/tests.md, tests/CLAUDE.md, and docs/progress/phase-06-background.md on the branch (goal, plan, decisions P6-D1 to P6-D4, "Docs verified", approved tests and answers Q1-Q12, "Results" and "Open issues" with the implementer's six deviations).

  Flag only correctness, security and requirement gaps, for example: EF Core change tracking in the update path (is the loaded assessment tracked, or attached, so `SaveChangesAsync` writes Status, Result, AssessedAt and FailedAt; what the result value comparer costs); whether save-then-enqueue really leaves a recoverable Pending row on every failure path, including a POST cancelled while `WriteAsync` waits; the re-enqueue running alongside the read loop (deviation 5): can it double-process or reorder in a way that breaks idempotency, and is the duplicate skip correct; whether `ProcessAssessmentAsync` catches only the calculation and lets save failures propagate as approved (Q5); whether the worker can ever leave its loop except on shutdown; the clock-before-CreatedAt bug the implementer found (both `Complete` and `Fail` throw): confirm it, and say how likely it is in practice; the `ApiFactory.WithWebHostBuilder` `new` hiding (deviation 4): can a test silently get the wrong host type through the base-class reference; tests that can be timing-flaky despite the 5 clean runs (fixed sleeps, negative waits, races with the worker), and the reduced `[Fact(Timeout)]` coverage (deviation 2); whether every approved must-case from the phase file really has a test that would fail if the behaviour broke; whether the 202 response, `Location` and `failedAt` match P6-D4 and Q11; the `CA1711` suppressions. Report each finding with severity, file:line, evidence and a proposed fix.
  ```
- **Purpose:** fresh-context review gate on the branch diff.
- **Outcome:** no critical or high findings; three medium and seven low (R1–R10). See "Review gate".

### P06-06 · agent→subagent (`parallel-implementer`, resumed) · 2026-11-15
- **Prompt (verbatim):**
  ```
  Phase 6 review gate: fix these five findings on phase-06-background, test-first where a behaviour changes, then run ./scripts/verify.sh (full) and the integration tests 3 times. Do nothing else: no other refactors, no clock fix, no sweep, no concurrency token (those wait for Alessandro).
  R1 (medium): no test drives an exception into AssessmentWorker's per-item catch (AssessmentWorker.cs ~:95). Add an integration test with a repository wrapper whose UpdateAssessmentAsync throws once: the row stays Pending, a ProcessingError log entry has the assessment ID and no amounts or owner ID, and a following POST in the same host still completes.
  R2 (medium, real bug): ChannelAssessmentQueue.EnqueueAsync passes the request token to WriteAsync, which returns cancelled at once if the token is already cancelled, even with free capacity, stranding a saved Pending row. Change to `TryWrite` first, `WriteAsync(id, ct)` only when full. Add a ChannelAssessmentQueueTests case: already-cancelled token, empty queue → the ID is queued.
  R5 (low): I-11 holds the worker before FindForProcessingAsync, so nothing is in memory at shutdown. Add a gate point after Complete has run in memory and before the save (in the update path), stop the host there, assert the row is Pending with AssessedAt and FailedAt null and nothing logged at Error.
  R9 (low): AssessmentWorkerTests ~:116 `Assert.False(third.IsCompleted)` does not prove the POST reached WriteAsync. Also assert the queue's Reader.Count == 1 via a bounded EventuallyAsync.
  R10 (low): U-P10 must also assert exactly one update attempt and that Fail was never called (count attempts in the fake before throwing).
  Commit in small conventional commits (scope tests or api) ending with: Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>. Do not edit the phase file. Report: commits, which new tests failed before the fix (R2 at least), trimmed verify output, the 3-run result.
  ```
- **Purpose:** fix the review findings that needed no decision (R1, R2, R5, R9, R10).
- **Outcome:** the run stopped on the user's usage limit before making any change.

### P06-07 · agent→subagent (`parallel-implementer`, resumed) · 2026-11-15
- **Prompt (verbatim):**
  ```
  Your last run stopped on a rate limit before any change (worktree clean at 850a3a8). Please do the five fixes from my previous message now (R1, R2, R5, R9, R10), exactly as specified there: test-first where behaviour changes, small commits, full verify, integration tests 3 times, no phase-file edits, then report. Keep tool use tight.
  ```
- **Purpose:** restart the P06-06 fixes after the usage limit reset.
- **Outcome:** commits `8339c95` (R10), `37b28b2` (R2) and `b505133` (R1, R5, R9). Only the R2 test failed before its fix (`TaskCanceledException`). Full verify passed (376 tests); integration tests passed 3 runs in a row.

### P06-08 · agent→subagent (`spec-checker`) · 2026-11-15
- **Prompt (verbatim):**
  ```
  Spec check for Phase 6 (background assessments) of a .NET 10 mortgage-application API. Read only; do not edit anything.

  Read the branch files in the worktree /Users/ale_ssd/Developer/Mortgage-Application-API-test/.claude/worktrees/agent-acd40a009ddf8a3cd (branch phase-06-background): docs/spec/ORIGINAL_SPEC.md, docs/spec/affordability-rules.md, docs/progress/phase-06-background.md (goal, plan, decisions P6-D1 to P6-D4, the approved test list and answers Q1-Q12, the security and review gates), docs/adr/0003-background-assessment-queue.md, the traceability notes in docs/progress/phase-01-spec.md and phase-03-api.md for the requirement IDs, and the code under src/ and tests/ that the phase file names.

  Map every spec requirement and every Phase 6 goal and approved must-case this phase touches (assess endpoint now 202 + Location, polling GET, Pending/Completed/Failed, no silent drops, startup recovery, owner scoping and 404s, no personal data in logs, validation at the boundary unchanged) to the file and test that meet it. Mark each met / partial / missing with a one-line reason and the file:line or test name. Check that the spec's original wording about the assessment endpoint is still satisfied by a 202 + polling flow, and say plainly if it is not. List anything the phase file claims that you could not find. Keep it short: one table plus at most 5 notes.
  ```
- **Purpose:** requirement traceability before the PR.
- **Outcome:** see "Review gate", spec check.

The human prompts for this phase (Checkpoint A approval, test approval, the usage-limit resume and the approval of the review-gate decisions) are in `phase-04-docker.md`, "Prompts".

## What was built (files)

Implemented by the `parallel-implementer` in commits `4a8ed68` (core), `e531691` (infra), `d249652` (api) and `7215f8d` (tests).

- **Core** (`src/MortgageApplications.Core/Applications/`):
  - `IAssessmentQueue.cs` (new): the port, `EnqueueAsync(Guid, CancellationToken)`.
  - `AssessmentWorkItem.cs` (new): the assessment plus its application's stored `AffordabilityInputs` (Q1).
  - `AssessmentProcessingOutcome.cs` (new): `NotFound`, `Skipped`, `Completed`, `Failed`, so the worker can log what happened.
  - `IMortgageApplicationRepository.cs`: `FindForProcessingAsync`, `UpdateAssessmentAsync`, `ListPendingAssessmentIdsAsync` (not owner-scoped, worker only).
  - `MortgageApplicationService.cs`: takes `IAssessmentQueue`; `AssessAsync` saves Pending, then enqueues; new `ProcessAssessmentAsync` (catches only the calculator call, Q5) and `ListPendingAssessmentIdsAsync`.
- **Infrastructure**: `Persistence/EfMortgageApplicationRepository.cs`: one tracked join query for the processing load, `Update` + `SaveChangesAsync`, Pending IDs ordered by ID (version 7, so oldest first). No index on `Status` and no new migration: the list query runs once per startup.
- **Api**:
  - `Assessments/AssessmentQueueOptions.cs` (new): `Assessments:QueueCapacity`, default 100, `[Range(1, 10_000)]`.
  - `Assessments/ChannelAssessmentQueue.cs` (new): `Channel.CreateBounded`, `BoundedChannelFullMode.Wait`, `SingleReader`, `WriteAsync` with the caller's token.
  - `Assessments/AssessmentWorker.cs` (new): `BackgroundService`; the startup re-enqueue runs next to the read loop; one item at a time, `CreateAsyncScope` per item; `[LoggerMessage]` logs with the assessment ID only.
  - `Program.cs`: options with `ValidateDataAnnotations().ValidateOnStart()`, queue singleton (also registered as `IAssessmentQueue`), `AddHostedService<AssessmentWorker>()`.
  - `Applications/ApplicationEndpoints.cs`: POST returns `Results<Accepted<AssessmentResponse>, NotFound>`; OpenAPI summaries updated.
  - `Applications/Responses.cs`: `AssessmentResponse` gains nullable `FailedAt` (Q11).
  - `appsettings.json` (`Assessments` section) and `requests.http` (202 and poll steps).
- **Tests**:
  - Unit: `Applications/Fakes.cs` (new fake methods, `FakeAssessmentQueue`, settable `FixedTimeProvider`), `Applications/MortgageApplicationServiceTests.cs`.
  - Integration, new: `AssessmentWorkerTests.cs`, `AssessmentRepositoryTests.cs`, `ChannelAssessmentQueueTests.cs`, `WorkerGate.cs` (holds the worker at its processing load), `CapturingLoggerProvider.cs`.
  - Integration, changed: `ApiFactory.cs` (P6-D3 database, migrations before any host starts, worker only in the default host), `Http.cs` (polling helpers), `AssessmentTests.cs`, `NotFoundAndOwnerTests.cs`, `StartupTests.cs`, `ErrorHandlingTests.cs`, `ApiRecordsTests.cs`.

A manual smoke run (`dotnet run`, Development, file database in the scratchpad) returned `HTTP/1.1 202 Accepted` with a `Location`; a GET 0.3 s later returned `"status":"Completed"`; the log showed `Re-enqueued 0 pending assessments at startup.`

After the review gate:
- `ChannelAssessmentQueue.EnqueueAsync`: `TryWrite` first, `WriteAsync` only when full (R2).
- `MortgageApplicationService.ProcessAssessmentAsync`: finish time is the later of now and `CreatedAt` (R4).
- `AssessmentWorker`: the startup sweep retries every `Assessments:RequeueRetryDelaySeconds` (new option, default 30 s, `[Range(1, 3600)]`, in `appsettings.json`) until it succeeds or the host stops (R3).
- Tests: `WorkerGate.cs` gains `BeforeSave`, `FailFirstSave` and `FailFirstSweep`; new tests listed under "Review gate".
- `docs/adr/0003-background-assessment-queue.md` (new, draft).

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

### Results

**Counts.** Before (plan commit `bc3ab50`, measured in a temporary checkout): 347 tests (258 unit + 89 integration). After: **373 tests (267 unit + 106 integration), 373 passing**, so 26 more. Every case in the recommended selection has a test:

| Case | Test |
|---|---|
| U-A1, U-A2 | `AssessAsync_saves_pending_assessment_and_enqueues_its_id`, `AssessAsync_saves_before_it_enqueues` |
| U-S1 | `AssessAsync_other_owner_…_queues_nothing`, `AssessAsync_unknown_application_…_queues_nothing` (existing tests, now also check the queue) |
| U-P1, U-P2, U-P4 | `ProcessAssessmentAsync_E1_completes_with_golden_values_and_clock_time`, `…_E2_completes_as_declined_not_failed`, `…_calculation_error_saves_failed_without_throwing` |
| U-P5, U-P6, U-P7 | `…_completed_assessment_is_skipped_unchanged`, `…_failed_assessment_is_skipped_unchanged`, `…_unknown_id_changes_nothing`, `…_assessment_without_application_changes_nothing` |
| U-P10, U-P11 | `ProcessAssessmentAsync_save_failure_propagates`, `ListPendingAssessmentIdsAsync_returns_only_pending_ids` |
| I-R1 to I-R3 | `AssessmentRepositoryTests` (3 tests) |
| I-1, I-2, I-3 | `Post_assessment_returns_202_with_location_and_pending_body`, `Post_assessment_E1_completes_approved_with_golden_values`, `Post_assessment_E2_completes_declined_on_ltv` |
| I-4, I-7, I-8 | `Get_assessment_while_pending_returns_200_with_pending_status`, `Startup_completes_pending_assessments_and_leaves_finished_ones`, `Same_id_queued_twice_is_processed_once` |
| I-5, I-6 | `Post_assessment_for_unknown_malformed_or_other_owner_application_queues_nothing` (new); `Other_owner_gets_the_same_404_as_an_unknown_id` (existing I35) |
| I-9, I-10, I-11 | `Full_queue_makes_the_post_wait_and_drops_nothing`, `Cancelled_waiting_post_leaves_pending_row_that_a_restart_completes`, `Shutdown_while_processing_leaves_the_assessment_pending` |
| I-13, I-14 | `Failing_item_becomes_failed_the_next_completes_and_logs_hold_no_personal_data` |
| W-5, W-6 | `ChannelAssessmentQueueTests` (2 tests), `Stopping_an_idle_worker_returns_within_5_seconds_without_errors` |
| Q7 | `Startup_with_invalid_queue_capacity_fails` (0 and 10,001) |

Changed existing tests: I4–I7 and F3 in `AssessmentTests` now expect 202 and poll; `NotFoundAndOwnerTests` I33, I35, I37 use the 202 flow; `ApiRecordsTests` passes the new `failedAt` argument; `ErrorHandlingTests.ThrowingRepository` implements the new repository methods.

**Red, then green.** The tests were written against stubs (new methods threw `NotImplementedException`; the endpoint still returned 201; the worker did nothing). Outputs were kept in the session scratchpad, not in the repo:
- Unit, `MortgageApplicationServiceTests`: 19 total, **11 failed**, 8 passed. Failures were `NotImplementedException` from `ProcessAssessmentAsync` / `ListPendingAssessmentIdsAsync`, `Expected: Pending, Actual: Completed` (U-A1) and an empty enqueue list (U-A2).
- Integration: 106 total, **16 failed**, 90 passed. Failures were `Expected: Accepted` (I-1, I6, I7), `NotImplementedException` (queue, repository and I-7 tests), `Timed out after 10 s: the worker's startup re-enqueue` (gated worker tests, I-8, W-6) and a `NullReferenceException` in I-13.
- Green before the implementation, and why: the E1, E2 and F3 polling tests (the synchronous 201 body was already Completed, so the poll ended at once; the 202 is asserted by I-1), I-5 (the 404 path did not change) and Q7 (the options registration was added together with the stubs).
- After the implementation: unit 267 / 267 and integration 106 / 106 passing on the first run.

**Full verify** (`./scripts/verify.sh`, after commit `7215f8d`), trimmed:
```
==> build (Release, warnings as errors)
    0 Warning(s)
    0 Error(s)
==> all tests
  total: 373
  failed: 0
  succeeded: 373
  skipped: 0
  duration: 4s 919ms
==> hook regression tests
hook tests: 91 passed, 0 failed
==> format check
VERIFY PASSED (full)
```

**Flakiness check (plan step 5).** `dotnet test --project tests/MortgageApplications.IntegrationTests`, 5 runs in a row: all 5 passed, 106 / 106 each (5.4 s, 4.9 s, 4.9 s, 5.0 s, 4.9 s). No SQLite locking errors, so Q10 stays with P6-D3.

**After the review gate** (lead agent, 2026-11-15). The P06-07 fixes added 3 tests (376 in total, 3 clean integration runs). Then the two fixes Alessandro approved:
- R4, commit `e164567`: the 2 new unit tests failed first with `System.ArgumentException : Must not be before the assessment was created. (Parameter 'assessedAt')` and `(Parameter 'failedAt')`; after the fix, unit 269 / 269.
- R3, commit `e4215a9`: the new integration test failed first with `Timed out after 10 s: assessment still Pending` (the log showed `Simulated sweep failure.`); after the fix it passed. Plus 2 startup-validation cases for the new option (0 and 3,601 s).

Full verify (`./scripts/verify.sh`, run with the R3 change in the working tree, just before commit `e4215a9`), trimmed:
```
  total: 381
  failed: 0
  succeeded: 381
  skipped: 0
==> hook regression tests
hook tests: 90 passed, 0 failed
==> format check
VERIFY PASSED (full)
```
The hook count is 90 rather than 91 because one case (the no-change skip of `verify-on-stop`) runs only on a clean tree. After the commit, `.claude/hooks/tests/test-hooks.sh` printed `hook tests: 91 passed, 0 failed`.

Integration tests after `e4215a9`, 3 runs in a row: 112 / 112 each, no failures.

## Security gate

`security-auditor` (P06-04) on the diff at `850a3a8`. **Verdict: PASS.** Tools: Gitleaks 8.30.1 (branch commits and working tree: no leaks), Semgrep 1.185.0 with `p/csharp` and `p/secrets` (27 changed files and a `--baseline-commit main` run: 0 findings), `dotnet list package --vulnerable --include-transitive` (no vulnerable packages), and a manual OWASP Top 10:2025 review. `Directory.Packages.props`, `global.json` and `.github/` are unchanged.

| # | Finding | Severity | Status |
|---|---|---|---|
| S1 | Denial of service: POSTs waiting on a full queue are not limited. Each saves a Pending row first and keeps a request open; a "POST then disconnect" loop grows the database and the next startup backlog. No rate limit or request timeout exists. | Medium | Accepted until Phase 8 (Alessandro, 2026-11-15): rate limiter on `POST …/assessments` (per owner after Phase 7) and a request timeout. Recorded in ADR 0003. |
| S2 | `ProcessAssessmentAsync` and `ListPendingAssessmentIdsAsync` are public on the shared service and not owner-scoped. Only the worker calls them; no endpoint can pass an ID. | Low | Accepted with the existing "worker only" comments (Alessandro, 2026-11-15). A separate `AssessmentProcessor` is the later option. Recorded in ADR 0003. |
| S3 | `catch (Exception)` around the calculator discards the exception, so the log cannot say which rule failed, and a calculator bug becomes a permanent Failed. | Low | Kept as approved in Q5 (Alessandro, 2026-11-15). |
| S4 | `ChannelAssessmentQueue.Reader` is public. | Info | Accepted: only the worker and one test use it. |
| S5 | Three cases leave rows Pending until restart: a failed startup re-enqueue, a POST cancelled after the save, a clock before `CreatedAt`. | Info | The first and third are fixed (R3, R4). The second was narrowed by R2: it now happens only when the queue is full and the client gives up. |
| S6 | `CA1711` suppressions on the two queue types. | Info | False positive: naming only, scoped and justified. |

Checks that passed: worker logs hold only `Count`, `AssessmentId` and `Outcome`; logged exceptions carry no data values; ProblemDetails unchanged; 404s identical for unknown, malformed and other-owner IDs, with nothing saved or queued; queue capacity validated at startup; one bad item cannot stop the worker; shutdown never marks Failed; LINQ only, no raw SQL.

The R3 and R4 fixes came after this gate. They add no endpoint, package or log field (the `RequeueFailed` message gains the retry delay in seconds). The gate was not re-run on them; it is re-run after the rebase on `main` (plan step 9).

## Review gate

`dotnet-reviewer` (P06-05) on the diff at `850a3a8`. It read the code and did not build or run tests.

| # | Finding | Severity | Status |
|---|---|---|---|
| R1 | No test sends an exception into the worker's per-item `catch`; deleting it would stop the host on the first save failure and every test would pass. | Medium | Fixed, `b505133`: `Failed_save_is_logged_leaves_the_row_pending_and_the_worker_continues`. |
| R2 | A client that disconnects after the insert leaves a Pending row that is never queued, even with free room: `WriteAsync` returns cancelled at once for a cancelled token. | Medium | Fixed, `37b28b2`: `TryWrite` first, then `WriteAsync`. Test `EnqueueAsync_with_cancelled_token_and_free_capacity_queues_the_id` failed before the fix. |
| R3 | The startup sweep runs once; if it fails (for example the database is not migrated yet), Pending rows stay Pending for the process lifetime. | Medium/Low | Fixed, `e4215a9` (Alessandro chose "retry with a delay until it succeeds"). A periodic sweep is later work (ADR 0003). |
| R4 | A clock reading before `CreatedAt` makes `Complete` and `Fail` throw; the row stays Pending. | Low | Fixed, `e164567`: finish at the later of now and `CreatedAt` (Alessandro). |
| R5 | I-11 holds the worker before the load, so it never tests "shutdown while an assessment is in memory". | Low | Fixed, `b505133`: `Shutdown_before_the_save_leaves_the_assessment_pending` (gate point `BeforeSave`). |
| R6 | No concurrency token: two processors on one database would both see Pending and the last write would win. | Low | Not fixed (Alessandro): safe with one reader; recorded in ADR 0003 as a requirement before scaling out. |
| R7 | `ApiFactory.WithWebHostBuilder` hides the base method with `new`; a helper typed as `WebApplicationFactory<Program>` would bind to the base and start a second worker. | Low | Open, not fixed: every call site uses `ApiFactory` today (reviewer grepped them). See "Open issues". |
| R8 | I-10 assumes TestServer cancels `RequestAborted` synchronously before `gate.Open()`. | Low | Open, not verified: the test passed in every run so far (5 + 3 + 3 + 3). See "Open issues". |
| R9 | `Assert.False(third.IsCompleted)` does not prove the third POST reached `WriteAsync`. | Low | Fixed, `b505133`: waits for `Reader.Count == 1` first. |
| R10 | U-P10 only checks that the exception propagates; U-P11 tests the fake's filter. | Low | U-P10 fixed, `8339c95` (exactly one save attempt, never Failed). U-P11 kept: I-R1 covers the real query. |

Checked and found no problem: EF Core tracking of the processing load, the Q5 catch scope, leaving the worker loop only on shutdown, save-then-enqueue order, the 202/`Location`/`failedAt` shapes, timeout coverage of the polling tests, the CA1711 suppressions, the approved must-cases and the log fields.

**`spec-checker` (P06-08)** at `cce8f49`, read-only, 2026-11-15. It did not build, run tests or open every test body. Spec requirement 3 (assess endpoint) and R30 (optional background processing): **met**. All 17 Phase 6 goals and must-cases it listed are met, except "the non-owner-scoped load is worker-only", which is **partial**: nothing in the code enforces it (accepted, S2 and ADR 0003). Its notes:
- **202 and polling against the spec wording.** The spec says only "Provide an endpoint to assess affordability for a given application" and does not require a synchronous call, so the 202 + `Location` + polling flow meets it. The caveat: the POST response no longer holds the result, only a Pending body. Background processing is an optional enhancement in the spec.
- It did not find the R3 test by grep. The test is `Startup_sweep_failure_is_retried_and_completes_pending_assessment` in `AssessmentWorkerTests.cs`.
- The security gate was not re-run after R3 and R4 (already noted above; it is re-run after the rebase).
- R7 and R8 stay open, so the tests that depend on them are only as sound as those assumptions.
- `docs/PROGRESS.md` (Phase 6 "Not started", R13 as 201, R30 "missing") is updated on `main` after the merge.

## AI output corrected or rejected (REAL cases only)

- **`test-designer` Q10 (P06-01)** said a busy timeout does not cover shared-cache locks. Learn's "Database errors" page says Microsoft.Data.Sqlite retries both busy and locked errors until the command timeout. Rejected; P6-D3 kept.
- **`parallel-implementer` (P06-02)** wrote `EnqueueAsync` as a plain `WriteAsync(id, cancellationToken)`, which strands a saved row when the client has already disconnected (R2). Caught by `dotnet-reviewer`, fixed in `37b28b2`.
- **`parallel-implementer` (P06-02)** wrote I-11 so that it could not fail on the behaviour it names (R5), and U-P10 so that it could not catch a second save (R10). Caught by `dotnet-reviewer`, fixed in `b505133` and `8339c95`.

## Optimisations considered

- No index on `Status` for the startup sweep: it runs once per start (plus retries on failure), so a table scan is acceptable at this size.


## Open issues

Implementation notes and deviations, for the reviewers:
- **`AssessmentProcessingOutcome` (not in the plan).** `ProcessAssessmentAsync` returns an outcome instead of nothing, so the worker can log Failed and NotFound at Warning (assessment ID only) without Core logging.
- **Test timeouts.** The plan says `[Fact(Timeout = …)]` on polling tests. xUnit analyzer `xUnit1069` (an error under warnings-as-errors) needs a timed test to reference `TestContext.Current.CancellationToken` directly. Timeouts are kept only on the 4 tests that hold the worker (I-4, I-9, I-10, I-11). The other polling tests rely on the bounded poll helper (10 s).
- **`CA1711` suppressed** on `IAssessmentQueue` and `ChannelAssessmentQueue` (the analyzer rejects names ending in `Queue`). The approved names were kept, each suppression with a justification.
- **`ApiFactory.WithWebHostBuilder` hides the base method** (`new`), so that every existing call site gets a host without the worker (Q2). An overload with `runWorker: true` opts in. Calling the base method through a `WebApplicationFactory<Program>` reference would skip this.
- **The startup re-enqueue runs next to the first requests.** A POST that arrives before the re-enqueue reads the database can be queued twice; the second run is skipped (idempotent). Tests that need an exact queue wait for the `PendingRequeued` log event first.
- **EF Core change tracking** (flagged in "Docs verified"): the processing load is tracked, and `UpdateAssessmentAsync` calls `Update` before `SaveChangesAsync`. I-R2 and I-R3 prove the change survives a reload. Checked on Learn afterwards by the lead agent (see "Docs verified").
- **APIs not checked on Learn by the implementer** (no web access): `ValidateDataAnnotations`, `ChannelReader<T>.Count`, `BackgroundService.ExecuteTask` and `SqliteConnection.ClearPool` were checked afterwards by the lead agent (see "Docs verified"). `ExecuteUpdateAsync` (tests only) was not checked.
- **Clock going backwards:** fixed after the review gate (R4, `e164567`).
- **Not done yet:** the rebase on `main` after Phase 4 merges, the gates re-run on the rebased branch (plan step 9), the spec check, the PR and Checkpoint B.

Still open after the gates, for Alessandro:
- **S1, accepted until Phase 8:** rate limiter and request timeout on `POST …/assessments`.
- **R6:** no concurrency token; required before a second worker or instance (ADR 0003).
- **R7:** the `new` hiding in `ApiFactory`. Possible fix: decide in `ConfigureWebHost` from a setting, or a test that the base method gives a host without the worker.
- **R8:** I-10's reliance on TestServer aborting the request synchronously is not verified against TestServer's source or docs.
- **ADR 0003** is a draft for Alessandro to accept or change at Checkpoint B.
- **Concurrency today:** `DevelopmentCurrentUser` gives every caller the same owner (`dev-owner-0001`) until Phase 7, so per-user isolation under real load is first tested then.

## Time spent (approx.)

## Human review (Alessandro writes this; the agent leaves it empty)
