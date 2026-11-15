# 0003. Background assessment queue

- **Status:** Accepted (2026-11-15, by Alessandro: "Yes and proceed" at Phase 6 Checkpoint B, answering whether ADR 0002 and ADR 0003 can be marked Accepted)
- **Date:** 2026-11-15
- **Decided by:** Alessandro (on the agent's recommendation): P6-D1 to P6-D4 at Phase 6 Checkpoint A ("Proceed"); the review-gate follow-ups and "the agent writes ADR 0003 as a draft" on 2026-11-15 ("Proceed").

## Context

Until Phase 3, `POST /api/applications/{id}/assessments` ran the calculator inside the request and returned 201. Phase 6 moves the work to a background worker: the POST saves a Pending assessment and returns 202 with a `Location`, and the client polls `GET …/assessments/{assessmentId}` until the status is Completed or Failed.

Constraints:
- No silent drops (Phase 3 carry-over): an accepted assessment must finish or stay visibly Pending, never disappear.
- One process, SQLite, no message broker in this project.
- The calculator is pure; only the hosting code knows about threads and queues.

## Options considered

1. **In-process bounded `System.Threading.Channels` queue behind a Core interface (`IAssessmentQueue`), one `BackgroundService` reader.** No new package or infrastructure. Lost on a process stop, so a startup sweep re-enqueues Pending rows from the database.
2. **The database as the queue** (the worker polls for Pending rows). Survives restarts with no sweep. Cost: polling delay or a tight loop, and more database load for every idle second.
3. **A message broker (for example Azure Service Bus).** Durable, scales to many workers. Cost: new infrastructure, a package and a secret, out of scope for a technical test.

Full-queue behaviour (P6-D2): wait for room (`BoundedChannelFullMode.Wait`), return 503 at once, or use an unbounded queue.

## Decision

Option 1, with these rules:

- **Order: save Pending, then enqueue** (`MortgageApplicationService.AssessAsync`). If the process stops in between, the row is Pending and the startup sweep finds it.
- **Bounded queue, waits when full** (P6-D2 A). Capacity `Assessments:QueueCapacity` (default 100, 1 to 10,000), validated at startup. The write tries `TryWrite` first, so a request whose client already disconnected still queues its ID when there is room (review R2).
- **Startup sweep:** the worker lists Pending IDs (oldest first) and enqueues them next to its read loop. If the sweep fails, it is retried every `Assessments:RequeueRetryDelaySeconds` (default 30 s, 1 to 3,600) until it succeeds or the host stops (review R3).
- **Idempotent processing:** an assessment that is no longer Pending is skipped, so an ID queued twice is processed once.
- **One DI scope per item;** every item error is caught and logged, because an exception escaping `ExecuteAsync` stops the host. Shutdown leaves an unfinished item Pending.
- **Failures:** only the calculation is caught and marks the row Failed. A load or save failure leaves the row Pending until the next start.
- **Clock:** the finish time is the later of now and `CreatedAt`, so a clock that moved back cannot make the item throw (review R4).
- **Logs** carry the assessment ID, a count or the outcome only: never amounts, inputs or the owner ID.
- **`GET` of an assessment is always 200** while it exists (P6-D4); the `status` field tells the state.

## Consequences

- **Single worker, single process.** There is no concurrency token on the assessment row (review R6). With one reader this is safe: the sweep and the requests feed the same channel, and the one reader processes items one at a time. Running two API instances against one database would need a concurrency token (for example `Status` as a concurrency check, which needs a new migration) or a broker with competing consumers.
- **Accepted risk until Phase 8 (security gate, Medium):** a client can send many POSTs while the queue is full. Each saves a Pending row and holds a request open. Phase 8 adds a rate limiter and a request timeout.
- **Recovery is at startup only** for rows whose processing failed on a load or save. A periodic sweep is later work if that turns out to matter.
- **Worker-only methods are public on the shared service** (`ProcessAssessmentAsync`, `ListPendingAssessmentIdsAsync`) and are not owner-scoped. They are marked "worker only" in code. No endpoint calls them, and the IDs come only from the worker's own queue. Splitting them into a separate class is possible later.
- **Scaling path:** `IAssessmentQueue` is a Core port. A broker such as Azure Service Bus can replace the channel without changing the use case; the sweep then becomes a dead-letter or outbox concern.
