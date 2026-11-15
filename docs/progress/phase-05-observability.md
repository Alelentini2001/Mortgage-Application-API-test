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

Checked by the lead agent after implementation, 2026-11-15 (the implementer read only the package READMEs):
- [Creating metrics](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/metrics-instrumentation): in DI apps "create the Meter object using IMeterFactory"; the factory keeps meters "in different service collections isolated from each other even if they use an identical name", "especially useful for testing". Durations: "prefer units of seconds recorded as a floating point". `InstrumentAdvice<T>` sets recommended histogram buckets and is supported by the OpenTelemetry .NET SDK from 1.10.0. Tag names: lowercase dotted, and avoid unbounded tag combinations (so the IDs stay off the metrics).
- [Add distributed tracing instrumentation](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/distributed-tracing-instrumentation-walkthroughs): `StartActivity` returns null when nobody listens (hence `activity?.`), the default kind is Internal, and `SetStatus(ActivityStatusCode.Error, description)` sets the span status. It recommends one long-lived `ActivitySource`; ours lives in the `AssessmentTelemetry` singleton.
- [IHttpActivityFeature](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.http.features.ihttpactivityfeature?view=aspnetcore-10.0): "Feature to access the Activity associated with a request"; used to tag the POST's server span.
- Not on Learn: `UseOtlpExporter` and the `url.query` redaction are OpenTelemetry package features, checked in the package READMEs (see "Open issues").

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

Implementation run by a delegated implementer agent on 2026-11-15, test-first, on `phase-05-observability` (no worktree).

Source:
- `src/MortgageApplications.Core/Applications/AssessmentProcessingResult.cs` (new): what `ProcessAssessmentAsync` did (`Outcome`), the `ApplicationId` (Completed, Failed, Skipped) and the `Decision` (Completed only). No input or output value. Core stays free of telemetry, logging and ASP.NET Core.
- `src/MortgageApplications.Core/Applications/MortgageApplicationService.cs`: `ProcessAssessmentAsync` returns `AssessmentProcessingResult` instead of the bare `AssessmentProcessingOutcome` enum (kept as the `Outcome` field).
- `src/MortgageApplications.Api/Observability/AssessmentTelemetry.cs` (new, singleton): `ActivitySource` and `IMeterFactory` meter named `MortgageApplications`; counter `mortgage.assessments.processed` (long, unit `{assessment}`) and histogram `mortgage.assessment.duration` (double, unit `s`, bucket advice in seconds); span `assessment.process` (Internal). Tags: `assessment.id`, `application.id` (span only), `assessment.outcome`, `assessment.decision` (Completed only), `assessment.rules_version` (Completed and Failed). Counter and histogram only for Completed and Failed. A processing error sets status Error with the exception type name only.
- `src/MortgageApplications.Api/Assessments/AssessmentWorker.cs`: one root span per item (`Activity.Current` cleared first, P5-D3 A), duration from the injected `TimeProvider` (`GetTimestamp`/`GetElapsedTime`), rules version from `AffordabilityOptions`. The six log messages are unchanged.
- `src/MortgageApplications.Api/Applications/ApplicationEndpoints.cs`: after a successful queue, tags the request's server span (`IHttpActivityFeature`) with `assessment.id` and logs `AssessmentQueued` (EventId 7, Information, `AssessmentId` and `ApplicationId` only). Nothing for a 404.
- `src/MortgageApplications.Api/Program.cs`: `AddOpenTelemetry()` with `ConfigureResource` (`AddService("mortgage-applications")` only when `OTEL_SERVICE_NAME` is not set), `WithTracing` (ASP.NET Core instrumentation, `AddSource("MortgageApplications")`), `WithMetrics` (ASP.NET Core instrumentation, `MortgageApplications`, `Microsoft.AspNetCore.Hosting`, `Microsoft.AspNetCore.Server.Kestrel`), `WithLogging()`; `UseOtlpExporter()` only when `builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]` is set. Comment "ADR in Phase 4" becomes "ADR 0002".
- `src/MortgageApplications.Api/appsettings.json`: `Microsoft.EntityFrameworkCore.Database.Command` at `Warning` (P5-D5 A).
- `src/MortgageApplications.Infrastructure/DatabaseMigration.cs`: comment "ADR in Phase 4" becomes "ADR 0002".
- `Directory.Packages.props`, `src/MortgageApplications.Api/MortgageApplications.Api.csproj`: the 3 approved packages at 1.20.1 (transitive: `OpenTelemetry`, `OpenTelemetry.Api`, `OpenTelemetry.Api.ProviderBuilderExtensions`, all 1.20.1). `dotnet list package --vulnerable --include-transitive`: none.
- `compose.observability.yaml` (new): `aspire-dashboard` on `mcr.microsoft.com/aspire/dashboard:13.6.2`, ports only `"127.0.0.1:18888:18888"`, no `ASPIRE_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS`; `api` gets `OTEL_EXPORTER_OTLP_ENDPOINT: http://aspire-dashboard:18889` and `depends_on: aspire-dashboard`. `compose.yaml` unchanged.

Tests (`tests/MortgageApplications.IntegrationTests` unless noted):
- `CapturingLoggerProvider.cs`: now also keeps structured state, scope values (`ISupportExternalScope`) and `Activity.Current`'s trace ID at log time. `Text` adds state and scopes, but not the activity-tracking scope IDs (random hex).
- `TelemetryCapture.cs` (new): `ActivityListener` (sources `MortgageApplications`, `Microsoft.AspNetCore`) and `MeterListener` (instruments whose `Meter.Scope` is the host's `IMeterFactory`), copied at stop/record time into concurrent queues; the `Telemetry` collection (`DisableParallelization = true`); `TraceparentHandler` (fixed W3C trace ID per test client).
- `ForbiddenNumbers.cs` (new) and `ForbiddenNumbersTests.cs` (new, self-test); `Telemetry.cs` (new, shared steps).
- `TelemetryAllowlistTests.cs`, `AssessmentSpanTests.cs`, `AssessmentMetricTests.cs`, `ObservabilityLogTests.cs`, `OtlpExportTests.cs`, `ComposeFileTests.cs` (new); `StartupTests.cs` (O27); `AssessmentWorkerTests.cs` (one line: `.Outcome`).
- `tests/MortgageApplications.UnitTests/Applications/MortgageApplicationServiceTests.cs`: asserts read `.Outcome`; 5 new cases for the IDs and the decision in the result.

Commits: `019cffa` (red tests), `2b53c42` (core), `b4371e4` (packages), `3efacda` (api), `9c9e652` (compose), `4a22c4e` (ADR comment), `fcfc2db` (test settings fix).

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

### Results

**Case to test map** (ID in a comment above each test):

| Case | Test |
|---|---|
| O1, O2 | `TelemetryAllowlistTests.Assessment_numbers_reach_no_log_span_or_metric_tag` (E1, E2) |
| O3 | `TelemetryAllowlistTests.Our_span_and_metric_tag_keys_are_in_the_allowlist` |
| O5 | `TelemetryAllowlistTests.Failed_and_errored_items_leave_no_inputs_or_owner_in_telemetry` |
| O22 | `TelemetryAllowlistTests.Our_log_field_names_are_in_the_allowlist` |
| Q12 | `TelemetryAllowlistTests.Query_string_value_reaches_no_span` |
| O9, O10, O11, O12, O13, O14, O15 | `AssessmentSpanTests` (one test each) |
| O16, O17, O18 | `AssessmentMetricTests` (O18 also covers the Q5 NotFound and Skipped spans) |
| O20, O21, O6, O7 (404 and 400), O8, O23 | `ObservabilityLogTests` |
| O24, O25, O26 | `OtlpExportTests` |
| O27 (X1) | `StartupTests.Startup_migrates_the_database_only_in_development` (Production, Development) |
| O28 | `ComposeFileTests` (override file, default file) |
| helper | `ForbiddenNumbersTests` (12 cases) |
| Core | 5 new unit cases in `MortgageApplicationServiceTests` (`*_reports_*`) |

**Red** (commit `019cffa`, tests against a Core stub that returned the outcome with null ID and decision; output kept in the session scratchpad, `p5-red.txt` and `p5-red-full.txt`):
- Unit: 274 total, **4 failed**, 270 passed. The 4 are the new Completed (E1, E2), Failed and Skipped result cases (stub returned no application ID or decision). The new NotFound case passed: null and null is the right answer for it.
- Integration: 157 total, **21 failed**, 136 passed. Failed for the expected reasons: 14 timed out after 10 s waiting for an `assessment.process` span, a counter or histogram measurement, or a server span tagged `assessment.id`; 3 found no `AssessmentQueued` entry (O20, O21 control, O22 control); O23 found EF command entries at Information; O25 and O26 received no OTLP request; O28 (override) found no `compose.observability.yaml`.
- Of the 42 new integration cases, 21 passed while red, because they guard existing or not-yet-broken behaviour: the 12 `ForbiddenNumbersTests` cases, O6, O7 (2), O8 (trace ID and 500 handling exist since Phase 3), O24 (no exporter existed yet), Q12 (no query value reached a span), O27 (2, Development-only migration exists since Phase 3) and the O28 default-file case.

**Green** (after the implementation commits):
- First run: unit 274 passed; integration 155 of 157 passed, O25 and O26 failed (no OTLP request). Cause: `Program` reads `OTEL_EXPORTER_OTLP_ENDPOINT` before `Build()`, and `WebApplicationFactory` applies `ConfigureAppConfiguration` sources only at `Build()`; a temporary debug line (removed) showed the value empty at that point. Fix in the tests only: the OTLP settings go in per host with `UseSetting` (commit `fcfc2db`). Then all 3 `OtlpExportTests` passed.
- `./scripts/verify.sh` (full), trimmed:
  ```
  ==> restore
  ==> build (Release, warnings as errors)
  ==> all tests
    total: 431
    failed: 0
    succeeded: 431
    skipped: 0
    duration: 16s 321ms
  ==> hook regression tests
  hook tests: 90 passed, 0 failed
  ==> format check
  VERIFY PASSED (full)
  ```
- Integration tests 3 times in a row (`dotnet test --project tests/MortgageApplications.IntegrationTests -c Release --no-build`): run 1: 157 passed, 0 failed, 0 skipped (14 s); run 2: 157 passed, 0 failed, 0 skipped (14 s); run 3: 157 passed, 0 failed, 0 skipped (13 s). O24 did not skip in any run (port 4318 was free).
- Verify steps: O29 `docker compose -f compose.yaml -f compose.observability.yaml config` renders (dashboard `13.6.2`, only `127.0.0.1:18888` published, `api` has the endpoint and `depends_on`). O31: `grep "ADR in Phase 4"` in `Program.cs` and `DatabaseMigration.cs` finds nothing.

**How the tests work** (details the proposal left open):
- `ForbiddenNumbers` forms per value: raw, trimmed, F2, N2, N0, comma decimal, dot grouping (N2 and N0), U+00A0 and U+202F grouping (N2 and N0); bare integers only with 5+ digits. Before the search it removes GUIDs, the rules version and also 32- and 16-character lowercase hex IDs (W3C trace and span IDs), which the log scopes and `traceparent` values carry. Summary: the exact E1 or E2 summary plus `checks passed`, `is above the`, `≤`, `×`.
- O1/O2 scan the spans of the test's own trace (a fixed `traceparent` on every request, so the create POST with the inputs is included) plus the worker span, all metric tags of the host's meter factory, and every log entry of the factory.
- O24: the protocol is set to `http/protobuf` and no endpoint, so a wrongly registered exporter would send to `localhost:4318`; the test listens there on 127.0.0.1 and ::1 with `TcpListener` and checks no connection is pending after the host is disposed (skips if 4318 is taken). O25/O26: an `HttpListener` on a free 127.0.0.1 port with `OTEL_EXPORTER_OTLP_PROTOCOL=http/protobuf`; disposing the host flushes; O25 expects `/v1/traces`, `/v1/metrics` and `/v1/logs`, O26 searches the protobuf bytes as text.
- O17: a `TimeProvider` whose `GetTimestamp` steps 1.5 s per call, so the histogram records exactly `1.5`.
- O27: a fresh named shared-cache in-memory database that `ApiFactory` did not migrate, swapped in with `ConfigureTestServices`; the check is whether `__EFMigrationsHistory` exists after startup (0 in Production, 1 in Development).
- Q12, `url.query`: checked in `~/.nuget/packages/opentelemetry.instrumentation.aspnetcore/1.20.1/README.md` (lines 84 to 88): by default the instrumentation replaces every query value with `Redacted` (`?key1=Redacted`); `OTEL_DOTNET_EXPERIMENTAL_ASPNETCORE_DISABLE_URL_QUERY_REDACTION=true` turns that off. Not set anywhere in this repo. The Q12 test proves `85123` in a query reaches no span.

**O30, manual dashboard check** (lead agent, 2026-11-15). `docker compose -f compose.yaml -f compose.observability.yaml up -d --build` (project `p5obs`): `/health` 200; `docker compose ps` shows `api` on `127.0.0.1:8080` and `aspire-dashboard` on `127.0.0.1:18888` only (no OTLP port on the host). E1 completed as Approved and E2 as Declined. In the dashboard (built-in browser, login link from the dashboard container's log):
- **Traces:** 12 traces, including two `assessment.process` root traces separate from their `POST …/assessments` traces (P5-D3 A). The E1 span has exactly: `application.id`, `assessment.decision=Approved`, `assessment.id`, `assessment.outcome=Completed`, `assessment.rules_version=1.0`; kind Internal; 0 events, 0 links; resource `service.name=mortgage-applications`, SDK 1.20.1.
- **Structured logs:** 9 entries, including two `Assessment {AssessmentId} of application {ApplicationId} queued.` linked to their traces. A script over the page text found none of `85000`, `85,000`, `60000`, `20000`, `300000`, `240000`, `280000`, `1438.92`, `1678.74`, `33.02`, `36.41`, `2.82`, `3.29`, `381535`, `93.33`, `80.00`, `900.00`.
- **Metrics:** meter `MortgageApplications` with `mortgage.assessments.processed` = 2 (tag values: decision Approved and Declined, outcome Completed, rules version 1.0) and `mortgage.assessment.duration`; plus the `Microsoft.AspNetCore.Hosting`, `Routing` and `Server.Kestrel` meters.
- The dashboard shows "Endpoint is unsecured: untrusted apps can send telemetry to the dashboard": its OTLP endpoint has no API key. Accepted for local use, because the OTLP ports are only on the compose network (see "Open issues").
- Cleaned up with `down -v --rmi local`.

## Security gate

## Review gate

## AI output corrected or rejected (REAL cases only)

## Optimisations considered

## Open issues

- **Dashboard OTLP endpoint without an API key** (seen in O30): any container on the compose network could send telemetry to the dashboard. Local demo only, OTLP ports not published; the dashboard documents an OTLP API-key mode, not checked or configured here.

- Dashboard image: `mcr.microsoft.com/aspire/dashboard:13.6.2`. The lead agent checked the MCR catalog on 2026-11-15: 13.6.2 was published 2026-10-29, the newest tag at least two weeks old (13.6.4 is 2026-11-10, 13.6.5 is 2026-11-11). Still a tag, not a digest (P4-D4); digest pinning is Phase 8.
- Deviation: `ProcessAssessmentAsync` now returns `AssessmentProcessingResult` (outcome, application ID, decision) instead of the enum; existing unit asserts changed from `outcome` to `outcome.Outcome` (no assert weakened).
- Deviation: `OtlpExportTests` pass the `OTEL_EXPORTER_OTLP_*` keys with `UseSetting`, not `AddInMemoryCollection`, because `Program` reads them before `Build()` (see "Results"). Still per host, never an environment variable. Side effect worth knowing: a value for these keys that only comes from a configuration source added after `WebApplication.CreateBuilder` (for example in a test's `ConfigureAppConfiguration`) does not turn the exporter on. Environment variables, `appsettings*.json` and command-line arguments do.
- Addition not in the plan: the duration histogram has bucket boundary advice in seconds (`InstrumentAdvice`, the same boundaries as `http.server.request.duration`); the SDK's default buckets are for milliseconds.
- Addition not in the plan: `ForbiddenNumbers` also strips 32- and 16-hex trace and span IDs before searching, not only GUIDs and the rules version.
- The worker's Skipped span carries `application.id` (the row exists); the plan said "only when known", which it is.
- `CapturingLoggerProvider.Text` leaves out the activity-tracking scope values (`TraceId`, `SpanId`, `ParentId`), so random hex cannot make the existing sentinel tests (I-13, I-14) flaky; the trace ID is kept in its own field.
- Not done by the implementer: Learn checks for the implementation-time APIs listed in plan step 2 (`ActivitySource`, `Meter`/`InstrumentAdvice`, `IMeterFactory` scope, `MeterListener`/`ActivityListener`, `UseOtlpExporter`, `IHttpActivityFeature`). Only the package READMEs in the NuGet cache were read (`UseOtlpExporter` behaviour, `IConfiguration` for `OTEL_*` keys, `url.query` redaction). The lead agent should add the Learn links to "Docs verified".
- Not done: the manual dashboard check (O30, plan step 10) and the security and review gates; they are the lead agent's steps.

## Time spent (approx.)

## Human review (Alessandro writes this; the agent leaves it empty)
