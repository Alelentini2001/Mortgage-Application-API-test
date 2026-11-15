# Phase 05: Logging + instrumentation

Status: **In progress: plan and tests approved (2026-11-15), implementation next** · Branch: `phase-05-observability` · PR: — · Started: 2026-11-15 · Finished: —

## Goal

The Observability row of `docs/layers/PRODUCTION-LAYERS.md`, built and tested:
- Structured logs with `[LoggerMessage]`, carrying only the allowlisted fields (rules section 6): application ID, assessment ID, decision, `RulesVersion`, status codes and timings. Never an input, an output value (loan, LTV, DTI, LTI, payment, max borrowing), the `Summary` or the `Checks`.
- OpenTelemetry tracing: ASP.NET Core server spans plus one custom span per processed assessment.
- Metrics: assessments by decision, and a processing-duration histogram, plus the built-in ASP.NET Core meters.
- OTLP export to the Aspire dashboard for local runs. The trace ID in every ProblemDetails (already true since Phase 3, I40; kept under test).
- The test the layer table asks for: none of an assessment's input or output numbers, raw or formatted, appear in captured logs, span attributes or metric tags.

Carried in from earlier phases:
- Phase 3 security S1 (part b): allowlisted `[LoggerMessage]` and a log-capture test that also covers real 500s (the framework logs them today; `SuppressDiagnosticsCallback` keeps them).
- Phase 4 P4-D2: the Aspire dashboard in compose.
- Phase 4: the "ADR in Phase 4" comments in `Program.cs` and `DatabaseMigration.cs` become "ADR 0002"; test X1 (no startup migration outside Development).

Not in scope: authentication (Phase 7), rate limiting (Phase 8), production monitoring backends (Azure Monitor and so on; the README in Phase 10 says how the OTLP export would point at one), EF Core or `HttpClient` instrumentation (see P5-D5).

## Plan (approved at Checkpoint A)

Approved by Alessandro on 2026-11-15 (P05-02), with option A on every decision and the 3 packages in step 3.

1. **Branch** `phase-05-observability` from `main` (`b6dca6d`). Phases run one at a time (Alessandro, P06-12), so no worktree.
2. **Docs check**, links recorded below. Done before planning for the main pages; at implementation: `ActivitySource`/`Activity` tags, `Meter`/`Counter<T>`/`Histogram<T>`, `MeterListener` and `ActivityListener` (for tests, no package), the `UseOtlpExporter` extension, and the ASP.NET Core built-in meters.
3. **Packages** (needs Alessandro's approval, P5-D1). In `Directory.Packages.props`, Api project only:
   - `OpenTelemetry.Extensions.Hosting` 1.20.1 (published 2026-10-20)
   - `OpenTelemetry.Instrumentation.AspNetCore` 1.20.1 (2026-10-21)
   - `OpenTelemetry.Exporter.OpenTelemetryProtocol` 1.20.1 (2026-10-20)

   Each is the newest stable version at least two weeks old (1.21.0 is from 2026-11-11; AspNetCore 1.20.2 from 2026-11-06). No test package: tests listen with the built-in `ActivityListener` and `MeterListener`.
4. **Telemetry wiring** (`src/MortgageApplications.Api/Observability/`, new):
   - `AssessmentTelemetry`: one `ActivitySource` and one `Meter`, both named `MortgageApplications`. Counter `mortgage.assessments.processed` (tags: `assessment.outcome` = Completed or Failed, `assessment.decision` = Approved or Declined when Completed, `assessment.rules_version`). Histogram `mortgage.assessment.duration` (seconds, the worker's processing time, same tags).
   - `AssessmentWorker` starts span `assessment.process` per item with tags `assessment.id`, `application.id`, and then `assessment.outcome`, `assessment.decision`, `assessment.rules_version`; status Error on a processing error, with the exception type only (no message).
   - `Program.cs`: `AddOpenTelemetry()` with the service name, ASP.NET Core tracing and metrics, our source and meter, the `Microsoft.AspNetCore.Hosting` and `Microsoft.AspNetCore.Server.Kestrel` meters, and the OpenTelemetry log provider. The OTLP exporter is added **only when `OTEL_EXPORTER_OTLP_ENDPOINT` is set**, so tests, CI and a plain `dotnet run` never try to connect.
5. **Logs:** a new `[LoggerMessage]` when an assessment is queued (assessment ID and application ID). Keep the six worker messages. The framework's error log for a real 500 stays (P5-D4). `Microsoft.EntityFrameworkCore.Database.Command` set to `Warning` in `appsettings.json` (P5-D5).
6. **Compose** (P5-D2): `compose.observability.yaml`, an override file that adds the `aspire-dashboard` service (UI on `127.0.0.1:18888`, OTLP reachable only inside the compose network, default login token) and sets `OTEL_EXPORTER_OTLP_ENDPOINT` on `api`. Run with `docker compose -f compose.yaml -f compose.observability.yaml up --build`. The plain `docker compose up` and the CI job do not change.
7. **Carry-overs:** the two "ADR in Phase 4" comments → "ADR 0002"; test X1.
8. **Test-first.** `test-designer` proposes cases; Alessandro approves; failing tests first. Expected areas: the allowlist test over logs, spans and metric tags for E1 and E2 (raw and formatted numbers: 85000, 85,000.00, 240000, 80.00, 1438.92, 33.02, 381535.82 and so on); span and metric tag keys limited to the allowlist; one span per processed assessment with the expected tags; counter and histogram per outcome; a 500 is logged without values and its ProblemDetails trace ID matches the request's trace; exporter not registered without an endpoint; X1.
9. **Verify**, **security gate** (`security-auditor`), **review gate** (`dotnet-reviewer`, then `spec-checker`), **evidence**, **PR** with local `/code-review` and `/security-review`, **Checkpoint B**.
10. **Local check** with Docker: run the observability override, POST an assessment, and confirm in the dashboard (through its HTTP API or a screenshot in the built-in browser) that the trace, metrics and logs arrive, and that no input or output number appears in them.

## Decisions for Alessandro (2-3 options · trade-offs · recommendation)

**P5-D1. How to produce and export telemetry.**
- **A. OpenTelemetry SDK, 3 packages** (recommended). The Learn sample for .NET and the Aspire dashboard uses exactly these. Standard OTLP export, so the same code later points at Azure Monitor or any OTLP backend through environment variables. Cost: 3 new packages (plus their transitive dependencies) to keep updated.
- **B. Built-in APIs only, no packages.** `ILogger`, `ActivitySource` and `Meter` are part of .NET, and tests can listen to them. Cost: nothing leaves the process, so there is no dashboard and no trace view; only `dotnet-counters`/`dotnet-trace` on the host.
- **C. The Aspire ServiceDefaults project.** One call sets everything up. Cost: a new project and more packages (HTTP resilience, service discovery, health checks) that this API doesn't need.

**P5-D2. Where the Aspire dashboard runs.**
- **A. An override file `compose.observability.yaml`** (recommended). Opt-in: `docker compose -f compose.yaml -f compose.observability.yaml up`. The default run and CI stay as they are, and the API only gets an OTLP endpoint when the dashboard exists. UI on loopback only, default token login (the token is printed in the dashboard container's log).
- **B. Always in `compose.yaml`.** One command shows everything. Cost: every run and the CI job pull and start the dashboard image.
- **C. No compose service.** The README gives the `docker run` command for the dashboard. Simplest; P4-D2 is then only documented.

**P5-D3. Linking the worker's span to the POST that queued it.**
- **A. Separate traces, linked by the `assessment.id` tag** (recommended). The POST span and the `assessment.process` span both carry the assessment ID, so the dashboard can find one from the other. No change to the queue or the Core port.
- **B. Carry the trace context through the queue** (an `ActivityLink` from the worker span to the POST span). Nicer trace view. Cost: the queue item stops being a plain `Guid`, `IAssessmentQueue` changes, and the startup sweep has no context to carry anyway.

**P5-D4. Logging real 500s.**
- **A. Keep the framework's error log** (recommended). `ExceptionHandlerMiddleware` already logs the exception with its stack trace. Our exception messages never contain input values (`InputGuard`, entity messages); the allowlist test proves it for every log entry, including 500s.
- **B. Suppress it and log our own `[LoggerMessage]` with the exception type only.** Nothing unplanned can reach a log. Cost: no stack trace for real bugs.

**P5-D5. Database telemetry.**
- **A. No EF Core instrumentation; EF command logs at `Warning`** (recommended). EF Core's OpenTelemetry instrumentation is still a prerelease package, and SQL text adds little here. Without `EnableSensitiveDataLogging`, EF never logs parameter values, but at `Information` every query would also be exported to the dashboard as noise.
- **B. Add the EF Core instrumentation (prerelease).** Database spans in the trace view. Cost: a prerelease package, against the "stable versions only" practice.
- **C. Leave EF logs at `Information`.** More detail locally, more noise exported.

## Decisions (option chosen · alternatives · reason · decided by) → ADR links

| # | Option chosen | Alternatives | Reason | Decided by |
|---|---|---|---|---|
| P5-D1 | A: OpenTelemetry SDK, 3 packages at 1.20.1 | B built-in APIs only; C Aspire ServiceDefaults | Standard OTLP export, same code points at any backend later | Alessandro |
| P5-D2 | A: opt-in `compose.observability.yaml` with the Aspire dashboard | B always in `compose.yaml`; C README only | Default run and CI unchanged; the API only exports when the dashboard exists | Alessandro |
| P5-D3 | A: separate traces, linked by the `assessment.id` tag | B trace context through the queue | No change to the Core port or the queue | Alessandro |
| P5-D4 | A: keep the framework's 500 error log, covered by the allowlist test | B own log with the exception type only | Keeps stack traces for real bugs | Alessandro |
| P5-D5 | A: no EF Core instrumentation; EF command logs at `Warning` | B prerelease EF instrumentation; C EF logs at `Information` | Stable packages only; less noise | Alessandro |

No ADR: none of the chosen options changes the architecture (an ADR was proposed only for P5-D1 C or P5-D3 B).

## Docs verified (links)

Checked before planning, 2026-11-15:
- [.NET observability with OpenTelemetry](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/observability-with-otel): .NET provides `ILogger`, `Meter` and `ActivitySource` in the framework; the OpenTelemetry packages collect and export them. Lists `OpenTelemetry`, `OpenTelemetry.Instrumentation.AspNetCore` and `OpenTelemetry.Exporter.OpenTelemetryProtocol`.
- [Use OpenTelemetry with OTLP and the Aspire Dashboard](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/observability-otlp-example): the packages (`OpenTelemetry.Exporter.OpenTelemetryProtocol`, `OpenTelemetry.Extensions.Hosting`, `OpenTelemetry.Instrumentation.AspNetCore`), the registration (`builder.Logging.AddOpenTelemetry`, `AddOpenTelemetry().ConfigureResource/WithMetrics/WithTracing`, `AddMeter("Microsoft.AspNetCore.Hosting")`, `AddMeter("Microsoft.AspNetCore.Server.Kestrel")`), `OTEL_EXPORTER_OTLP_ENDPOINT` and `OTEL_SERVICE_NAME`. It says the dashboard "is a developer visualization tool, not a production monitoring tool", and that dashboard data "can be sensitive", so a login token is required by default.
- [Aspire dashboard standalone](https://aspire.dev/dashboard/standalone/) (the Learn URL redirects here): image `mcr.microsoft.com/aspire/dashboard`, UI port 18888, OTLP/gRPC 18889 and OTLP/HTTP 18890 inside the container; the login token is in the container log (`login?t=`); `ASPIRE_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS=true` turns login off and is for local development only (not planned).
- NuGet registration API, 2026-11-15: the versions and dates in plan step 3. MCR tag list for `aspire/dashboard`: newest tags `13.6.x`; the publish date of the tag to pin is checked at implementation (two-week rule).

## Orchestration used

Planning: lead agent only (no subagent yet).

## Prompts

### P05-01 · human→agent · 2026-11-15
- **Prompt (verbatim):**
  > Yes and proceed... but we should do phase by phase...
- **Purpose:** after the Phase 6 merge (P06-12), go on to the next phase, one phase at a time.
- **Outcome:** Phase 5 explored and planned in this file; Checkpoint A.

### P05-02 · human→agent · 2026-11-15
- **Prompt (verbatim):**
  > Yes continue
- **Purpose:** Checkpoint A approval. The agent had asked: "Do you approve the plan with A on all five, including adding the 3 OpenTelemetry packages?"
- **Outcome:** decisions recorded (all A), packages approved; branch created; `test-designer` run (P05-03).

### P05-03 · agent→subagent (`test-designer`) · 2026-11-15
- **Prompt (verbatim):**
  ```
  Phase 5 (logging + instrumentation) of a .NET 10 mortgage-application API (Minimal APIs, EF Core SQLite, a background assessment worker). Propose test cases for the approved plan; do not write code.

  Read first: docs/progress/phase-05-observability.md (goal, plan, approved decisions P5-D1 to P5-D5), docs/spec/affordability-rules.md (section 6 logging allowlist, section 8 golden examples E1 and E2), docs/layers/PRODUCTION-LAYERS.md (Observability row), .claude/rules/tests.md, tests/CLAUDE.md, .claude/rules/security.md, AGENTS.md, and the current code and tests: src/MortgageApplications.Api/Program.cs, src/MortgageApplications.Api/Assessments/AssessmentWorker.cs, src/MortgageApplications.Api/Applications/ApplicationEndpoints.cs, src/MortgageApplications.Api/Errors/ProblemExceptionHandler.cs, src/MortgageApplications.Api/appsettings.json, src/MortgageApplications.Infrastructure/DatabaseMigration.cs, tests/MortgageApplications.IntegrationTests/ (ApiFactory, CapturingLoggerProvider, AssessmentWorkerTests, ErrorHandlingTests, StartupTests, Http). Existing tests already cover the six worker log messages without amounts (I-14) and a non-empty traceId in every ProblemDetails (I40): don't repeat them, but say which ones should be extended.

  What will be built (not written yet):
  - AssessmentTelemetry in the Api project: ActivitySource and Meter both named "MortgageApplications"; counter mortgage.assessments.processed and histogram mortgage.assessment.duration (seconds), tags assessment.outcome (Completed or Failed), assessment.decision (Approved or Declined, only when Completed), assessment.rules_version.
  - AssessmentWorker starts span "assessment.process" per item with tags assessment.id, application.id, then outcome, decision, rules_version; status Error on a processing error with the exception type only, no message.
  - Program.cs: AddOpenTelemetry with service name, ASP.NET Core tracing and metrics, our source and meter, the Microsoft.AspNetCore.Hosting and Microsoft.AspNetCore.Server.Kestrel meters, and the OpenTelemetry log provider; the OTLP exporter only when OTEL_EXPORTER_OTLP_ENDPOINT is set.
  - A new [LoggerMessage] when an assessment is queued (assessment ID and application ID only). The framework's 500 error log stays (P5-D4). Microsoft.EntityFrameworkCore.Database.Command at Warning in appsettings.json (P5-D5).
  - compose.observability.yaml adds the Aspire dashboard (UI on 127.0.0.1:18888, OTLP only on the compose network) and sets OTEL_EXPORTER_OTLP_ENDPOINT on api; plain compose and CI unchanged.
  - Carry-overs: "ADR in Phase 4" comments become "ADR 0002"; test X1: outside Development the app does not run migrations at startup (note ApiFactory migrates its own database before any host starts, so X1 needs a database that the factory did not migrate).
  - Tests must not add packages: listen with ActivityListener and MeterListener (built into .NET).

  Cover at least: the allowlist test the layer table asks for (none of an assessment's input or output numbers, raw or formatted, in captured log text including exception text, span tag values, or metric tag values) for an Approved (E1) and a Declined (E2) case, and say exactly which number forms to check (raw decimal, 2 dp, thousands separators, invariant and current culture); span and metric tag KEYS limited to an allowlist; exactly one assessment.process span per processed item with the expected tags and the right status for Completed, Failed and a processing error; the counter and the histogram per outcome and decision; the new queued log; a real 500 is logged by the framework with no input values and its ProblemDetails traceId matches the request's trace ID; the OTLP exporter is not registered when the endpoint is not set (and how to prove that without a network); EF command logs no longer at Information; X1; the compose override (what can be checked in CI or locally without the dashboard UI). Say which cases are unit tests and which are integration tests, how to keep listener tests isolated when xUnit runs test classes in parallel (ActivityListener and MeterListener are process-wide), and how to avoid timing flakiness.

  Use the agent's output format. Mark each case must or nice, keep the list tight (no redundant cases), and list any ambiguities in the plan you need Alessandro to settle.
  ```
- **Purpose:** propose Phase 5 test cases before any test is written.
- **Outcome:** 31 cases (O1–O31) and 13 questions (Q1–Q13). See "Tests", "Proposal".

### P05-04 · human→agent · 2026-11-15
- **Prompt (verbatim):**
  > Yes proceed
- **Purpose:** approval of the recommended test selection (all must cases plus O7, O8, O26, O28; O29 and O31 as verify steps; O4 and O19 skipped) and of the recommended answers to Q1–Q13.
- **Outcome:** implementation delegated, test-first (P05-05).

## What was built (files)

## Tests

### Proposal (`test-designer`, P05-03; approved by Alessandro, P05-04)

All new cases are integration tests: `tests/CLAUDE.md` limits unit tests to Core, and the new code is in the Api project. No test package: `ActivityListener` and `MeterListener` are built in.

Facts it found that shape the tests (checked by the lead agent where marked):
- `InvariantGlobalization=true`, so culture forms (`85.000,00`, `85 000,00`) are built with hand-made `NumberFormatInfo`, not named cultures.
- `CapturingLoggerProvider` keeps only the formatted message and exception text; it must also keep structured state, scopes and the current trace ID, which the OpenTelemetry log provider exports.
- `ThrowingRepository` in `ErrorHandlingTests` puts `SECRET-MARKER-123` in its message, so the 500-log test needs its own thrower with no value in the message.
- The worker saves before it ends the span and records the metric, so every telemetry assertion waits (bounded) on the captured span or measurement itself.
- Listeners are process-wide: telemetry test classes go in one collection with parallelisation off, spans are filtered by the test's own assessment IDs, and the `Meter` comes from `IMeterFactory` (lead agent: Learn confirms factory meters are isolated per service collection, "especially useful for testing").
- The ProblemDetails `traceId` format (bare trace ID or the W3C `00-…` string) was not confirmed on Learn; O6 compares only the 32-hex trace-ID part.

Recommended selection (lead agent):

| Group | Cases | Recommended |
|---|---|---|
| Allowlist and security | O1 E1 scan of logs, spans and metric tags for every forbidden number form, with a control that the GET body has the numbers; O2 the same for E2; O3 our span and metric tag keys limited to the allowlist; O5 extend I-13/I-14 to spans, metrics and structured state; O6 a 500 is logged by the framework with no input values and its `traceId` matches a sent `traceparent`; O9 a processing error sets span status Error with the type only | all must |
| | O7 `traceparent` correlation for 404 and 400 (extend I40); O8 a malformed body logs no Error and no value | nice, include |
| | O4 deny-pattern on framework tag keys | skip: O1 already scans every source |
| Spans | O10 Completed/Approved; O11 Declined is not an error; O12 Failed has no decision tag; O13 processing error, worker continues; O14 one span per item, separate traces from the POST (P5-D3); O15 the POST server span carries `assessment.id` | all must |
| Metrics | O16 counter per outcome and decision; O17 histogram in seconds, exact value with a fake `TimeProvider`; O18 a re-queued ID is not counted twice | all must |
| | O19 counter on a processing error | skip: settled by Q3 (not counted) |
| Logs | O20 the new queued log; O21 no queued log for an unknown or another owner's application; O22 log field keys limited to the allowlist; O23 EF command logs below Warning are gone, with a control host at Information | all must |
| Export and startup | O24 no OTLP traffic without an endpoint (loopback listener); O25 traffic with one (positive control); O27 X1, no startup migration outside Development, with a Development control | all must |
| | O26 the exported bytes contain our span and meter names and none of the forbidden numbers | nice, include (only proof that `AddSource`/`AddMeter` are registered) |
| Compose and carry-overs | O30 manual dashboard check (plan step 10) | must (manual) |
| | O28 file-text check of the override; O29 `docker compose config` locally; O31 grep for "ADR in Phase 4" | nice, include (O29 and O31 as verify steps, not tests) |

Questions and the lead agent's recommended answers:
- **Q1** Failed outcome (calculation threw, row marked Failed): span status **Unset** with `assessment.outcome=Failed`; the worker did its job.
- **Q2** POST span: **build it**, the endpoint tags its server span with `assessment.id` (P5-D3 says both spans carry it).
- **Q3** Processing error (row stays Pending): span status **Error**, type only; **no counter or histogram** measurement; no `assessment.outcome` tag.
- **Q4** `assessment.rules_version` on Failed: **the configured value** (from the options).
- **Q5** NotFound and Skipped: a span with `assessment.outcome` = `NotFound` or `Skipped`; **no counter or histogram**, so a re-queued item is never counted twice.
- **Q6** **`IMeterFactory`**. Whether OpenTelemetry's `AddMeter` picks up a factory meter is proved by O25/O26.
- **Q7** Event `AssessmentQueued`, logged by `ApplicationEndpoints` after a successful queue (Core has no logger).
- **Q8** Read `OTEL_EXPORTER_OTLP_ENDPOINT` from **`builder.Configuration`** (environment variables are part of it), so tests set it per host.
- **Q9** Config constants (rate, limits) are **not** in the forbidden set: rules section 6 does not list them.
- **Q10** **OTLP/gRPC on 18889** (the exporter's default); no protocol variable.
- **Q11** Keep the full exception on `ProcessingError` (P5-D4: stack traces for real bugs). EF never logs parameter values without `EnableSensitiveDataLogging`; accepted.
- **Q12** The API has no query parameters. Check the ASP.NET Core instrumentation's query handling on its docs at implementation, and add one nice test that a query value like `?x=85123` does not reach a span.
- **Q13** Time the worker with the injected **`TimeProvider`** (already there), so O17 asserts an exact value.

## Security gate

## Review gate

## AI output corrected or rejected (REAL cases only)

## Optimisations considered

## Open issues

- The dashboard image tag to pin and its publish date (P4-D4 still uses tags, not digests; digest pinning is Phase 8).

## Time spent (approx.)

## Human review (Alessandro writes this; the agent leaves it empty)
