# Phase 05: Logging + instrumentation

Status: **Checkpoint B: PR open, waiting for Alessandro** · Branch: `phase-05-observability` · PR: [#9](https://github.com/Alelentini2001/Mortgage-Application-API-test/pull/9) · Started: 2026-11-15 · Finished: —

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

## Checkpoint B outcome (2026-11-15)

Alessandro: "proceed", then "Approved, merge here" (P05-11).
- Rules section 6 (`docs/spec/affordability-rules.md`) now names the HTTP server-span keys (review R4).
- The four PR `/code-review` findings go to Phase 9.
- The CI fix (`2ca41ee`) was made in a separate session started from this one; this session committed its phase-file note and merged.

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

### P05-05 · agent→subagent (`general-purpose`, implementer) · 2026-11-15
- **Prompt (verbatim):**
  ```
  Implement Phase 5 (logging + instrumentation) of this .NET 10 mortgage-application API, exactly as approved, test-first.

  Where: the main checkout /Users/ale_ssd/Developer/Mortgage-Application-API-test, branch phase-05-observability (already checked out; phases now run one at a time, so there is no worktree). Do not switch or create branches. Never push. Never force anything, never --no-verify.

  Scope and source of truth: docs/progress/phase-05-observability.md. Read "Goal", "Plan (approved at Checkpoint A)", "Decisions" (P5-D1 to P5-D5, all option A), "Docs verified" and "Tests > Proposal" (the approved selection and the approved answers to Q1-Q13) first. Also read AGENTS.md, CLAUDE.md, .claude/rules/backend.md, .claude/rules/tests.md, .claude/rules/security.md, .claude/rules/infra.md, tests/CLAUDE.md, src/MortgageApplications.Core/CLAUDE.md, docs/spec/affordability-rules.md sections 6 and 8.

  What to build (summary; the phase file is binding where it is more precise):
  1. Packages, approved: in Directory.Packages.props add OpenTelemetry.Extensions.Hosting 1.20.1, OpenTelemetry.Instrumentation.AspNetCore 1.20.1, OpenTelemetry.Exporter.OpenTelemetryProtocol 1.20.1; reference them from src/MortgageApplications.Api only. No other package, and no package in the test projects.
  2. src/MortgageApplications.Api/Observability/AssessmentTelemetry.cs: an ActivitySource named "MortgageApplications" and a Meter created through IMeterFactory with the same name; counter mortgage.assessments.processed (long) and histogram mortgage.assessment.duration (double, unit "s"). Tags: assessment.outcome, assessment.decision (only when Completed), assessment.rules_version.
  3. AssessmentWorker: span "assessment.process" (kind Internal) per item with assessment.id and application.id (application.id only when known), then assessment.outcome = Completed | Failed | NotFound | Skipped, assessment.decision when Completed, assessment.rules_version (configured value) for Completed and Failed. Status: Unset for Completed, Failed, NotFound, Skipped; Error for a processing error (an exception reaching the worker's per-item catch) with the exception TYPE name only as description, no message, no exception event, no outcome tag. Counter and histogram only for Completed and Failed. Duration measured with the injected TimeProvider (GetTimestamp / GetElapsedTime). The span is a root span (P5-D3 A: separate traces). The worker needs application.id and the rules version: get them without changing what Core exposes more than needed (for example extend AssessmentProcessingOutcome into a small result record returned by ProcessAssessmentAsync, or read them from the work item); keep Core free of telemetry, logging and ASP.NET Core references.
  4. ApplicationEndpoints: after a successful queue, tag the current request activity (the ASP.NET Core server span) with assessment.id, and log a new [LoggerMessage] event "AssessmentQueued" (Information) with AssessmentId and ApplicationId only. Nothing for 404s.
  5. Program.cs: AddOpenTelemetry() with ConfigureResource(AddService("mortgage-applications") unless OTEL_SERVICE_NAME is set), WithTracing (AddAspNetCoreInstrumentation, AddSource("MortgageApplications")), WithMetrics (AddAspNetCoreInstrumentation, AddMeter("MortgageApplications"), AddMeter("Microsoft.AspNetCore.Hosting"), AddMeter("Microsoft.AspNetCore.Server.Kestrel")), WithLogging or builder.Logging.AddOpenTelemetry. Add the OTLP exporter ONLY when builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"] is set (read from IConfiguration, never Environment). Register AssessmentTelemetry as a singleton.
  6. appsettings.json: "Microsoft.EntityFrameworkCore.Database.Command": "Warning" under Logging:LogLevel.
  7. compose.observability.yaml (new): service aspire-dashboard, image mcr.microsoft.com/aspire/dashboard with a pinned version tag (not latest; pick a stable 13.x tag and record in the phase file that its publish date is to be checked by the lead agent), ports only "127.0.0.1:18888:18888", no host mapping for 18889/18890, no ASPIRE_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS; and on api: OTEL_EXPORTER_OTLP_ENDPOINT: http://aspire-dashboard:18889 plus depends_on aspire-dashboard. compose.yaml must not change.
  8. Carry-overs: the comments "ADR in Phase 4" in src/MortgageApplications.Api/Program.cs and src/MortgageApplications.Infrastructure/DatabaseMigration.cs become "ADR 0002".
  9. Check, before relying on it, how OpenTelemetry.Instrumentation.AspNetCore 1.20.x handles url.query (redaction) from its README in the NuGet package you restore (~/.nuget/packages/opentelemetry.instrumentation.aspnetcore/1.20.1/README.md if present); record what you found and where.

  Tests (integration tests in tests/MortgageApplications.IntegrationTests; follow the approved cases O1-O3, O5-O18, O20-O28 and the nice Q12 query-string test, with the IDs in a comment above each test):
  - Extend CapturingLoggerProvider to keep structured state (key/value pairs), scope values and Activity.Current's trace ID at log time, without breaking existing tests.
  - A small TelemetryCapture helper (ActivityListener + MeterListener, IDisposable) that copies data at ActivityStopped / measurement time into concurrent collections. Put every class that uses it in one [Collection] with DisableParallelization = true. Filter spans by the test's own assessment IDs, and metrics by the host's IMeterFactory scope.
  - A forbidden-number helper producing every form listed in the phase file (raw, trimmed, F2, N2/N0, hand-built NumberFormatInfo comma-decimal, dot-grouping, U+00A0 and U+202F space grouping), with GUIDs and the RulesVersion stripped before the boundary-regex search, 5+ digit integers only for bare integers, plus the Summary fragments. Include a self-test of the helper.
  - Every wait is bounded (Http.EventuallyAsync), never Task.Delay. Set config per host with AddInMemoryCollection, never Environment.SetEnvironmentVariable.
  - O24/O25/O26: a loopback HttpListener or TcpListener on a free port; set OTEL_EXPORTER_OTLP_ENDPOINT to it (and OTEL_EXPORTER_OTLP_PROTOCOL http/protobuf if that makes the listener simpler; record what you did); dispose the host to flush.
  - O27 (X1): Production host and a Development control on a fresh named shared-cache in-memory SQLite database the factory did not migrate, as described in the phase file.
  - O28: a file-text test of compose.observability.yaml and compose.yaml.

  Order of work: tests first. Write the tests against minimal stubs so they compile, run them and keep the red output (save it to /private/tmp/claude-501/-Users-ale-ssd-Developer-Mortgage-Application-API-test/38287261-0d8e-41a1-a4c6-05aca4d87104/scratchpad/p5-red.txt), then implement until green. Small conventional commits (AGENTS.md "Commits and PRs"), each ending with the line: Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>. Then run ./scripts/verify.sh (full) and the integration tests 3 times in a row, and keep the outputs.

  Phase file: fill in only "What was built (files)", "Tests" (add a "Results" subsection under the Proposal: red counts and why, green counts, verify output trimmed, the 3 runs) and "Open issues" (deviations, anything not done, anything you could not check). Do not edit other sections, docs/PROGRESS.md or docs/ai/*.

  Honesty rule: never claim a test failed or passed without having run it; if something was not done, write that it was not done and why. Stop and report if the same step fails 3 times. Keep tool use tight (you have a turn limit): batch edits, avoid re-reading files you just wrote.

  Report at the end: commits, files, red/green evidence, verify output, the 3 runs, the url.query finding, the dashboard tag chosen, and every deviation from the plan.
  ```
- **Purpose:** implement the approved plan test-first. The `parallel-implementer` agent is reserved for parallel phases, so a general-purpose agent got the same kind of brief.
- **Outcome:** commits `019cffa` to `b791a5d`; red 4 unit + 21 integration failures for the expected reasons, then green; 431 tests; 3 clean integration runs. Deviations listed in "Open issues".
- **Follow-up message from the lead agent (verbatim), sent while it ran:**
  > Dashboard image tag for compose.observability.yaml: use mcr.microsoft.com/aspire/dashboard:13.6.2. The lead agent checked the MCR catalog on 2026-11-15: 13.6.2 was published 2026-10-29, the newest tag at least two weeks old (13.6.4 is 2026-11-10, 13.6.5 is 2026-11-11). Record that in the phase file instead of "to be checked". Everything else in your instructions is unchanged.

### P05-06 · agent→subagent (`security-auditor`) · 2026-11-15
- **Prompt (verbatim):**
  ```
  Security gate for Phase 5 (logging + instrumentation) of a .NET 10 mortgage-application API. Read only: do not edit files or switch branches.

  Repository: /Users/ale_ssd/Developer/Mortgage-Application-API-test, branch phase-05-observability (checked out). Review the branch diff: git diff main...HEAD. Read first: .claude/rules/security.md, AGENTS.md, docs/spec/affordability-rules.md section 6 (logging allowlist), docs/progress/phase-05-observability.md (plan, decisions P5-D1 to P5-D5, the approved answers Q1-Q13, "Open issues", and the O30 manual dashboard check).

  Do:
  1. Gitleaks on the branch commits (--log-opts="main..HEAD") and the working tree; Semgrep (p/csharp, p/secrets) on the changed files; dotnet list package --vulnerable --include-transitive (three OpenTelemetry packages are new). Say which tools ran; do not install anything.
  2. OWASP Top 10:2025 review of the diff with focus on: personal data or amounts reaching any log, span attribute, span status description or metric tag (inputs, outputs, Summary, Checks, owner ID, exception messages); framework telemetry (ASP.NET Core server spans: url.query handling, headers, route values); the OpenTelemetry log provider exporting structured state and scopes; the OTLP exporter being enabled only by OTEL_EXPORTER_OTLP_ENDPOINT and what an attacker who controls configuration could do with it (not a finding if config is trusted, but say so); compose.observability.yaml (loopback-only UI, unpublished OTLP ports, login token, no anonymous mode, the dashboard's unauthenticated OTLP endpoint on the compose network, the pinned image tag); supply chain of the three new packages and their transitive packages.
  3. Check that the tests would catch a regression: do the allowlist tests scan every telemetry path that leaves the process (logs incl. structured state and exceptions, spans incl. status, metric tags, the OTLP bytes)? Name any path they miss.

  Output: a findings table (tool/check, finding with file:line, severity, real or false positive, reason, fix) and a one-word verdict (PASS or FAIL). Keep it short.
  ```
- **Purpose:** security gate.
- **Outcome:** PASS; Low and Info findings only. See "Security gate".

### P05-07 · agent→subagent (`dotnet-reviewer`) · 2026-11-15
- **Prompt (verbatim):**
  ```
  Fresh-context review of Phase 5 (logging + instrumentation) of a .NET 10 mortgage-application API. Read only: do not edit files or switch branches.

  Repository: /Users/ale_ssd/Developer/Mortgage-Application-API-test, branch phase-05-observability (checked out). Review the branch diff: git diff main...HEAD. Read first: AGENTS.md, .claude/rules/backend.md, .claude/rules/tests.md, tests/CLAUDE.md, src/MortgageApplications.Core/CLAUDE.md, docs/progress/phase-05-observability.md (goal, plan, decisions, "Docs verified", the approved test selection and answers Q1-Q13, "Results", "Open issues").

  Check at least:
  - AssessmentWorker / AssessmentTelemetry: span lifecycle per item (root span via Activity.Current = null in an async method; started before the scope, disposed after logging), tags and status per outcome (Completed, Failed, NotFound, Skipped, processing error, shutdown OCE), counter and histogram only for Completed and Failed, duration from TimeProvider, histogram buckets in seconds, IMeterFactory use, ActivitySource lifetime and disposal.
  - Core change: AssessmentProcessingResult replacing the enum return; Core stays free of telemetry, logging and ASP.NET Core; the existing unit tests still prove what they proved.
  - ApplicationEndpoints: tagging the server span through IHttpActivityFeature, the AssessmentQueued [LoggerMessage] (logger created per request from ILoggerFactory: fine or wasteful?), nothing on 404.
  - Program.cs: OpenTelemetry registration, the conditional OTLP exporter read from builder.Configuration before Build (the implementer found WebApplicationFactory's ConfigureAppConfiguration is not visible there and switched the tests to UseSetting: is the production behaviour still right for environment variables, appsettings and command line?), OTEL_SERVICE_NAME handling, WithLogging and the existing console logging.
  - Tests: whether each approved case (O1-O3, O5-O18, O20-O28, Q12) would fail if its behaviour broke; listener isolation (one non-parallel collection, IMeterFactory scope filter); bounded waits; the ForbiddenNumbers helper (forms, boundary regex, stripping GUIDs and hex IDs: could the stripping hide a real leak?); O24's fixed port 4318 and its skip path; flakiness risks.
  - Anything in the diff that contradicts the plan or the decisions.

  Output: findings, most severe first, each as file:line | severity | problem | fix. Then a list of what you checked and found fine. Say plainly whether you built or ran tests.
  ```
- **Purpose:** fresh-context review gate.
- **Outcome:** one medium and eight low findings. See "Review gate".

### P05-08 · agent→subagent (implementer, resumed) · 2026-11-15
- **Prompt (verbatim):**
  ```
  Phase 5 gate findings: fix the items below on phase-05-observability, test-first where behaviour changes (show the new or strengthened test failing first, or prove it with a temporary mutation that you revert, and record which). Then run ./scripts/verify.sh (full) and the integration tests 3 times. Same rules as before: no new package, no push, small conventional commits with the Co-Authored-By line, keep tool use tight.

  From the review gate (dotnet-reviewer):
  1. (medium) ForbiddenNumbers misses longer-precision leaks. Forms containing a decimal separator must match as prefixes (drop the trailing (?![0-9]) for them). Add, per output value, a 1 dp form and a 2 dp truncated form, and add the unrounded E1/E2 values as extra forbidden values: LTV, DTI and LTI to at least 4 dp, the LTV and DTI as fractions (for example 0.8, 0.3302), and the unrounded monthly payment to 4 dp. Compute them with System.Decimal from the rules formulas (docs/spec/affordability-rules.md), not by hand, and say in a comment how. Move "2.825" from CleanTexts to Leaks in the self-test. Re-check that nothing legitimate (IDs, rules version, timestamps, durations) now matches; if a duration or timestamp could collide, strip it the same way IDs are stripped and say so.
  2. O24 depends on the fixed default port 4318 and skips when it is taken. The reviewer suggested pointing the signal-specific endpoint keys at a free port, but item 3 makes those keys enable export, so that design no longer works. Keep O24 on the default port unless you find a way, without a new package and without the fixed port, for O24 to fail if the exporter were registered unconditionally. Either way, make a skip visible (an explicit xUnit skip with the reason "port 4318 in use"), never a silent pass, and explain the design in the test comment.
  3. Program.cs: also turn the OTLP exporter on when any of OTEL_EXPORTER_OTLP_TRACES_ENDPOINT, OTEL_EXPORTER_OTLP_METRICS_ENDPOINT or OTEL_EXPORTER_OTLP_LOGS_ENDPOINT is set. Add a test (O25b) that setting only the traces key sends trace data to the fake collector.
  4. user_agent.original on ASP.NET Core server spans is client-controlled and not in the rules section 6 allowlist: remove it in AddAspNetCoreInstrumentation (EnrichWithHttpRequest setting the tag to null, or the documented option if the package README has one). Then add the skipped O4 as an explicit framework span tag-key allowlist: the server span may carry only the HTTP semantic-convention keys it carries today minus user_agent.original (list them exactly in the test), plus assessment.id. Record in the phase file that rules section 6 does not yet name these HTTP keys; Alessandro decides at Checkpoint B whether to amend the rules wording.
  5. O14 cannot fail if Activity.Current = null is removed. Start the worker's host while a test-only ActivitySource activity is current (with a listener that samples it), so ExecuteAsync captures it as a would-be parent; keep the assertion that the worker span has no parent. Prove it fails with the line removed (temporary mutation, reverted).
  6. O17: assert the histogram's bucket advice (InstrumentAdvice boundaries in seconds) from the published instrument, if the advice is readable through a public API on .NET 10; if it is not public, say so and skip this item.
  7. Program.cs: AddService("mortgage-applications") also overrides a service.name from OTEL_RESOURCE_ATTRIBUTES. Skip AddService when OTEL_RESOURCE_ATTRIBUTES contains "service.name=" as well, and comment it. No new test needed beyond what is cheap.

  From the security gate (security-auditor):
  8. TelemetryCapture copies only span event names: include span event tags and link tags in the scanned span text.
  9. Metric instrument set: assert that the MortgageApplications meter publishes exactly the two allowed instruments (names, types, units), from InstrumentPublished, so a new instrument recording an amount cannot slip through.
  10. O6: also scan the 500 request's server span (tags and status description) for the sentinels and the owner ID, filtered by the traced trace ID.

  Phase file (docs/progress/phase-05-observability.md): fix the contradictions the reviewer found (status line still says "implementation next"; "Open issues" still says O30 and the Learn checks were not done: the lead agent did both, see the O30 section and "Docs verified"). Record the shutdown-cancellation span behaviour as a known, intended gap: on shutdown the span ends Unset with assessment.id only (no outcome), consistent with "shutdown is never a failure"; no test. Add a "Results after the gates" paragraph under Tests > Results with red/mutation evidence, verify output and the 3 runs. Do not fill the Security gate or Review gate sections (the lead agent writes those). Do not edit docs/PROGRESS.md or docs/ai/*.

  Report: commits, what failed first or which mutation proved each strengthened test, the exact framework key list for O4, verify output, the 3 runs, and anything you could not do.
  ```
- **Purpose:** fix the gate findings that need no decision.
- **Outcome:** commits `0eccb16`, `f25c203`, `bcd3028`; 442 tests; 3 clean integration runs (168 / 168).

### P05-09 · agent→subagent (`spec-checker`) · 2026-11-15
- **Prompt (verbatim):**
  ```
  Spec check for Phase 5 (logging + instrumentation) of a .NET 10 mortgage-application API. Read only; do not edit anything.

  Repository: /Users/ale_ssd/Developer/Mortgage-Application-API-test, branch phase-05-observability (checked out). Read: docs/spec/ORIGINAL_SPEC.md, docs/spec/affordability-rules.md section 6, docs/layers/PRODUCTION-LAYERS.md (the Observability row), docs/progress/phase-05-observability.md (goal, plan, decisions, approved tests and answers, gates, open issues), the requirement table in docs/PROGRESS.md, and the code and tests the phase file names.

  Map every spec requirement this phase touches (the spec's logging, instrumentation or observability wording if any, the README "design decisions" expectations it feeds, and the security constraints on personal data), every Phase 5 goal (allowlisted [LoggerMessage] logs, OpenTelemetry tracing with one span per assessment, the two metrics, OTLP export only when configured, the opt-in dashboard, trace ID in ProblemDetails, the allowlist test over logs, spans and metric tags, the carry-overs: ADR 0002 comments and test X1) and the Observability row's "evidence" column to the file and test that meet it. Mark each met / partial / missing with a one-line reason and the file or test name. List any requirement-table rows in docs/PROGRESS.md whose status should change after this phase, and anything the phase file claims that you could not find. Keep it short: one table plus at most 5 notes.
  ```
- **Purpose:** requirement traceability before the PR.
- **Outcome:** see "Review gate", spec check.

### P05-10 · agent→subagent (`general-purpose`, from the `/security-review` skill) · 2026-11-15
- **Prompt (verbatim):**
  ```
  Security review sub-task for PR #9 (Phase 5, logging and instrumentation) of a .NET 10 mortgage-application API. Read only: do not edit files and do not run Docker.

  Repository: /Users/ale_ssd/Developer/Mortgage-Application-API-test (branch phase-05-observability, checked out). The diff to review: run `git diff main...HEAD` there (OpenTelemetry tracing, metrics and log export via OTLP when configured; a worker span and two metrics; a new log event; an opt-in compose override with the Aspire dashboard; tests). Accepted and documented, do not report: exception messages and stack traces in error logs leaving the process when OTLP is on (P5-D4), the dashboard's OTLP endpoint without an API key on the compose network (S7), image tag not digest (Phase 8), the fixed DevelopmentCurrentUser until Phase 7. Context to read: docs/progress/phase-05-observability.md ("Security gate"), docs/spec/affordability-rules.md section 6, .claude/rules/security.md, AGENTS.md.

  OBJECTIVE: identify HIGH-CONFIDENCE security vulnerabilities newly added by this PR that could have real exploitation potential. Not a general code review. Do not comment on existing security concerns.
  1. Minimise false positives: only flag issues where you are >80% confident of actual exploitability.
  2. Skip theoretical issues, style concerns and low-impact findings.
  3. Prioritise vulnerabilities that could lead to unauthorized access, data breaches or system compromise.
  4. Do NOT report: denial of service; secrets or sensitive data stored on disk; rate limiting or resource exhaustion.

  Categories to examine: input validation (SQL/command/XXE/template/NoSQL injection, path traversal); authentication and authorization (bypass, privilege escalation, sessions, JWT); crypto and secrets (hardcoded keys, weak crypto, key storage, randomness, certificate validation); injection and code execution (deserialization, YAML, eval, XSS); data exposure (sensitive data logging or storage, PII, endpoint data leakage, debug information exposure). Even if only exploitable from the local network, it can be HIGH.

  Method: (1) research the repository's security model and existing patterns; (2) compare the new changes against them and look for new attack surface; (3) assess each modified file, trace data from inputs to sensitive operations, look for privilege boundaries crossed unsafely.

  For each candidate give: file, line, severity (HIGH / MEDIUM / LOW), category slug, description, exploit scenario, fix recommendation, and a confidence from 1 to 10. Focus on HIGH and MEDIUM; MEDIUM only if obvious and concrete. If there are no candidates, say so plainly. Keep the report short.
  ```
- **Purpose:** the find step of the local `/security-review` on PR #9.
- **Outcome:** no HIGH or MEDIUM candidates, so the skill's false-positive filter step had nothing to run on.

### P05-11 · human→agent · 2026-11-15
- **Prompts (verbatim), in order:**
  > proceed

  > Approved, merge here
- **Purpose:** Checkpoint B approval of PR #9. After "proceed", the lead agent asked: "Do you approve PR #9, with my two recommendations: name the extra HTTP span keys in rules section 6, and move the four low code-review findings to Phase 9?", and whether to merge here or in the Phase 5 session. "Approved, merge here" answers both.
- **Outcome:** rules section 6 amended in this PR; the four PR `/code-review` findings carried to Phase 9; PR #9 squash-merged by the lead agent after green CI.

## What was built (files)

Implementation run by a delegated implementer agent on 2026-11-15, test-first, on `phase-05-observability` (no worktree).

Source:
- `src/MortgageApplications.Core/Applications/AssessmentProcessingResult.cs` (new): what `ProcessAssessmentAsync` did (`Outcome`), the `ApplicationId` (Completed, Failed, Skipped) and the `Decision` (Completed only). No input or output value. Core stays free of telemetry, logging and ASP.NET Core.
- `src/MortgageApplications.Core/Applications/MortgageApplicationService.cs`: `ProcessAssessmentAsync` returns `AssessmentProcessingResult` instead of the bare `AssessmentProcessingOutcome` enum (kept as the `Outcome` field).
- `src/MortgageApplications.Api/Observability/AssessmentTelemetry.cs` (new, singleton): `ActivitySource` and `IMeterFactory` meter named `MortgageApplications`; counter `mortgage.assessments.processed` (long, unit `{assessment}`) and histogram `mortgage.assessment.duration` (double, unit `s`, bucket advice in seconds); span `assessment.process` (Internal). Tags: `assessment.id`, `application.id` (span only), `assessment.outcome`, `assessment.decision` (Completed only), `assessment.rules_version` (Completed and Failed). Counter and histogram only for Completed and Failed. A processing error sets status Error with the exception type name only.
- `src/MortgageApplications.Api/Assessments/AssessmentWorker.cs`: one root span per item (`Activity.Current` cleared first, P5-D3 A), duration from the injected `TimeProvider` (`GetTimestamp`/`GetElapsedTime`), rules version from `AffordabilityOptions`. The six log messages are unchanged.
- `src/MortgageApplications.Api/Applications/ApplicationEndpoints.cs`: after a successful queue, tags the request's server span (`IHttpActivityFeature`) with `assessment.id` and logs `AssessmentQueued` (EventId 7, Information, `AssessmentId` and `ApplicationId` only). Nothing for a 404.
- `src/MortgageApplications.Api/Program.cs`: `AddOpenTelemetry()` with `ConfigureResource` (`AddService("mortgage-applications")` only when neither `OTEL_SERVICE_NAME` nor a `service.name=` in `OTEL_RESOURCE_ATTRIBUTES` is set; after the gates), `WithTracing` (ASP.NET Core instrumentation with `user_agent.original` removed through `EnrichWithHttpRequest`, after the gates; `AddSource("MortgageApplications")`), `WithMetrics` (ASP.NET Core instrumentation, `MortgageApplications`, `Microsoft.AspNetCore.Hosting`, `Microsoft.AspNetCore.Server.Kestrel`), `WithLogging()`; `UseOtlpExporter()` only when `OTEL_EXPORTER_OTLP_ENDPOINT` or one of the `..._TRACES_ENDPOINT`, `..._METRICS_ENDPOINT`, `..._LOGS_ENDPOINT` keys is set in `builder.Configuration` (the signal keys after the gates). Comment "ADR in Phase 4" becomes "ADR 0002".
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

**Results after the gates** (review gate items 1 to 7, security gate items 8 to 10; commits `0eccb16` tests, `f25c203` fix; outputs kept in the session scratchpad as `p5-gates-*.txt`):
- Red with the strengthened tests, before the `Program.cs` fix: integration 168 total, 4 failed. O4 failed on `user_agent.original`. O25b (traces key only) received no request. The `OTEL_RESOURCE_ATTRIBUTES` service-name case exported `mortgage-applications`. `FormsOf_produces_every_listed_form` failed on the new precision forms, so its expected list was updated (test, not helper).
- Item 1, `ForbiddenNumbers`: the new self-tests were also run against the previous helper (restored temporarily from git, then put back). 7 of 18 failed: `LTI 2.825` (moved from the clean texts to the leaks), the 5 unrounded E1 cases (`ltv=0.8`, `dti=0.3302`, `lti=2.8235`, `ltv 80.0%`, `payment 1438.9`), and the forms list. With the new helper, O1, O2, O5, O6, O8 and O26 still pass, so no ID, rules version or other legitimate text matches the new forms. Timestamps and durations are not stripped because no scanned text carries one (the class comment says why).
- Item 5, O14: proved by a temporary mutation (reverted). With `Activity.Current = null` removed from the worker, O14 failed: the worker span joined the trace of the test activity that was current when the host started.
- Item 8, span event and link tags: proved by a temporary mutation (reverted). A span event with tag `loan=240000.00` made O1 (E1) fail. E2 passed, which is right: 240000.00 is not an E2 number.
- Item 9, instrument set: proved by a temporary mutation (reverted). An extra counter `mortgage.mutation.extra` on our meter made the new instrument test fail.
- Item 6, bucket advice: `Instrument<T>.Advice` is public, so the instrument test also asserts the 14 boundaries. Proved by a temporary mutation (reverted): with the advice removed, the test failed. A first attempt at this mutation did not compile; that run used stale binaries and is discarded.
- Item 10, O6: the 500 request now posts the sentinel body. Its server span (tags, status description, events) and the logs are scanned for the sentinels and the owner ID. It passed at once; no mutation was run for it.
- Item 2, O24: still on the default port 4318 (the reason is in the test comment). The skip is explicit: `Assert.Skip("port 4318 in use")`. It did not skip in any run.
- Item 4, O4 key list, exact (ASP.NET Core instrumentation 1.20.1 on these endpoints: 201, 202 with a query string, 200, 404, 500): `error.type`, `http.request.method`, `http.response.status_code`, `http.route`, `network.protocol.version`, `server.address`, `url.path`, `url.query`, `url.scheme`, plus our `assessment.id`. `user_agent.original` was there before the fix and is removed now.
- `./scripts/verify.sh` (full), trimmed:
  ```
  ==> restore
  ==> build (Release, warnings as errors)
  ==> all tests
    total: 442
    failed: 0
    succeeded: 442
    skipped: 0
    duration: 13s 372ms
  ==> hook regression tests
  hook tests: 91 passed, 0 failed
  ==> format check
  VERIFY PASSED (full)
  ```
- Integration tests 3 times in a row (`-c Release --no-build`): run 1: 168 passed, 0 failed, 0 skipped (14 s); run 2: 168 passed (14 s); run 3: 168 passed (13 s).

## Security gate

`security-auditor` (P05-06) on `git diff main...HEAD` at `4c7442c` (15 commits). **Verdict: PASS.** Tools: Gitleaks (branch commits, working tree, full history: no leaks), Semgrep `p/csharp` + `p/secrets` (9 changed source files; `.semgrepignore` skips tests: 0 findings), `dotnet list package --vulnerable --include-transitive` (none). New transitive packages: only `OpenTelemetry`, `OpenTelemetry.Api`, `OpenTelemetry.Api.ProviderBuilderExtensions`, all 1.20.1. `dotnet nuget verify` (package signatures): **not run**, the permission system denied it.

| # | Finding | Severity | Status |
|---|---|---|---|
| S1 | The allowlist scan skipped span event tags and link tags. | Low (test gap) | Fixed, `0eccb16`. Mutation: a span event tagged `loan=240000.00` made O1 (E1) fail. |
| S2 | Metric values and the set of instruments were not checked; a new instrument recording an amount would pass. | Low (test gap) | Fixed, `0eccb16`: the meter must publish exactly the two instruments (names, types, units, bucket advice). Mutation: an extra counter made the test fail. |
| S3 | O6 scanned only logs on the 500 path, not the 500 request's server span. | Low (test gap) | Fixed, `0eccb16` (no mutation run). |
| S4 | Meters created outside `IMeterFactory`, and numeric OTLP attributes, are not seen by the tests. | Info | Accepted: the code uses the factory; S2 pins the instrument set. |
| S5 | With OTLP on, exception messages and stack traces from the framework 500 log and `ProcessingError` leave the process. | Info | Accepted design (P5-D4, Q11); O5 and O6 prove no sentinel or owner ID appears. Revisit before any non-local backend. |
| S6 | Whoever controls configuration can redirect OTLP, add headers or disable query redaction. | Info | Not a finding: configuration is inside the trust boundary. README (Phase 10): use an `https` endpoint in production. |
| S7 | The dashboard's OTLP endpoint has no API key; any container on the compose network could send it telemetry. | Low | Accepted for local use (also seen in O30). An API key would need a dev-only secret: ask Alessandro first. |
| S8 | Dashboard image pinned by tag, not digest. | Low | Accepted until Phase 8 (P4-D4). |
| S9 | No `packages.lock.json` or source mapping. | Low | Pre-existing; Phase 8 (security L3 from Phase 3). |


## Review gate

`dotnet-reviewer` (P05-07), static review (it did not build or run tests). Nothing critical or high.

| # | Finding | Severity | Status |
|---|---|---|---|
| R1 | `ForbiddenNumbers` matched fixed-scale forms with a trailing digit boundary, so a longer-precision leak (unrounded LTI `2.8235…`, `33.020`, ratios like `0.8`) passed. | Medium | Fixed, `0eccb16`: separator forms match as prefixes; F1, truncated F2 and F4 forms; unrounded LTV, DTI, LTI, payment and the two fractions computed with `System.Decimal` at test time. The new self-tests failed 7 of 18 against the old helper. |
| R2 | O24 depends on the fixed port 4318 and skipped silently when it was taken. | Low | Partly fixed: the skip is now explicit (`Assert.Skip("port 4318 in use")`); the fixed port stays, because after R3 any endpoint key turns the exporter on, so only the default port can catch an unconditional exporter. It did not skip in any run. |
| R3 | Only the generic `OTEL_EXPORTER_OTLP_ENDPOINT` turned export on, not the signal-specific keys. | Low | Fixed, `f25c203`; O25b (traces key only) failed first. Side effect, recorded in "Open issues": the other two signals then try the default localhost endpoint. |
| R4 | `user_agent.original` (client-controlled) was on every server span, and no test limited framework span keys (O4 had been skipped). | Low | Fixed, `f25c203`: removed with `EnrichWithHttpRequest`; O4 added as a key allowlist and failed first. **Open for Alessandro:** rules section 6 does not name the remaining HTTP keys (`error.type`, `http.request.method`, `http.response.status_code`, `http.route`, `network.protocol.version`, `server.address`, `url.path`, `url.query` (values redacted), `url.scheme`). |
| R5 | O14 could not fail if `Activity.Current = null` were removed. | Low | Fixed, `0eccb16`: the host starts under a test activity. Mutation: without the line, the worker span joined that trace and the test failed. |
| R6 | On shutdown the span ends Unset with only `assessment.id`. | Low | Recorded as an intended gap ("shutdown is never a failure"), no test. |
| R7 | The seconds bucket advice was untested. | Low | Fixed, `0eccb16`: `Instrument<T>.Advice` is public on .NET 10; mutation (advice removed) failed the test. |
| R8 | `AddService` overrode a `service.name` from `OTEL_RESOURCE_ATTRIBUTES`. | Low | Fixed, `f25c203`; the resource-attributes test failed first. |
| R9 | Phase file status line and "Open issues" contradicted the O30 and docs records. | Low | Fixed, `bcd3028`. |

Checked and found fine by the reviewer: span lifecycle and tags per outcome (Q1–Q5), metrics only for Completed and Failed with bounded tags and seconds, meter and source lifetimes, Core free of telemetry, the endpoint tag and log (per-request `CreateLogger` is cached by category), the production configuration path for the exporter, the EF log level, query redaction, compose, test isolation and waits, the ID stripping in `ForbiddenNumbers` (cannot hide a real leak), O27.

After the fixes: `./scripts/verify.sh` full → `0 Warning(s)`, `total: 442, failed: 0, skipped: 0`, `hook tests: 91 passed`, `VERIFY PASSED (full)` (lead agent, at `bcd3028`).


**Local PR reviews** on PR #9 at `0e0a1f0`, posted as one PR comment.
- `/code-review` (high effort, run by the lead agent in its own context): 4 low findings, none fixed here: (1) the `OTEL_RESOURCE_ATTRIBUTES` check is a loose substring match; (2) the rules-version tag comes from configuration, not the stored result (same value today); (3) the outcome tag is set twice on the span; (4) a single signal-specific endpoint key makes the other signals retry against localhost. Proposed: Phase 9 (4 also in the README).
- `/security-review`: no findings (P05-10).

**`spec-checker` (P05-09)**, static, at `f0bb7b5`. The spec's only related line is the optional enhancement "Logging or instrumentation" (R33): **met**. All Phase 5 goals and carry-overs met (logs, tracing, one span per assessment, the two metrics, conditional OTLP export, opt-in dashboard, trace ID in ProblemDetails, the allowlist tests, ADR 0002 comments, X1). Two rows partial against the layers table wording:
- "no EF `EnableSensitiveDataLogging`": no test for it. The lead agent grepped `src/`: it appears nowhere.
- The layers row names "HTTP" client instrumentation, which P5-D5 left out. The API makes no outbound HTTP calls (no `HttpClient` in `src/`, lead agent grep), so there is nothing to instrument; the layers row wording is updated in Phase 10 with the README.
Rows to update in `docs/PROGRESS.md` after the merge: R33 to met, the Phase 5 table row, the resume section and its carry-overs.

**CI failure on PR #9 and fix** (2026-11-15). CI "Build, test, format" failed on `0e0a1f0` (run 43722308183) and `cd849d3` (run 43722941889): 442 tests, 3 failed, all in `OtlpExportTests` — `With_an_endpoint_traces_metrics_and_logs_are_exported` (paths `[]`), `Traces_endpoint_alone_exports_traces` (no `/v1/traces` request) and `Exported_data_has_our_span_and_instruments_and_no_assessment_numbers` (empty body). The fake collector received nothing, and each test took only 79–271 ms. "Docker image build and smoke test" passed.
- **Not reproduced locally:** the same tests passed on macOS, in a Linux arm64 `sdk:10.0` container for the class alone (6 / 6, 31 s) and for the full integration suite in Release (168 / 168). There, each export test took seconds, because the host's shutdown flushes the exporters.
- **Cause (inferred, not proven):** the tests depended only on that flush at host shutdown. On the CI runner the hosts were disposed in milliseconds and nothing reached the collector.
- **Fix** (`2ca41ee`, tests only): each exporting host sets `OTEL_BSP_SCHEDULE_DELAY`, `OTEL_BLRP_SCHEDULE_DELAY` and `OTEL_METRIC_EXPORT_INTERVAL` to 100 ms (read through `IConfiguration`, OTLP exporter README "Environment Variables"), and each test waits, bounded at 10 s, for the expected data while the host is running, then disposes it. The assertions are unchanged. Mutation check: with `UseOtlpExporter()` commented out, all five positive tests failed with "received 0 request(s)" and O24 still passed. `./scripts/verify.sh` full: 442 passed, `VERIFY PASSED (full)`.

## AI output corrected or rejected (REAL cases only)

- **Lead agent, invented commit reference.** When writing the security gate section, the lead agent typed a placeholder hash (`ab0e6b1`-era) instead of the real commit the gates ran at. Caught on re-reading the output of its own edit and replaced with `4c7442c` in the next commit.
- **Implementer, OTLP tests could not have passed as first written.** They set the endpoint with `ConfigureAppConfiguration`, which `WebApplicationFactory` applies only at `Build()`, after `Program` reads the key. Found by the implementer's own first green run (O25/O26 still red); fixed with `UseSetting`.
- **Implementer, export tests relied on the shutdown flush.** The OTLP export tests passed locally but received no data on the Linux CI runner (see "CI failure on PR #9 and fix" under Tests). Caught by CI, fixed in `2ca41ee`.
- **Implementer, leak search too narrow.** `ForbiddenNumbers` matched fixed-scale forms only, so an unrounded LTI `2.8235` passed (its own self-test listed `2.825` as clean). Caught by `dotnet-reviewer` (R1), fixed in `0eccb16`.

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
- The Learn checks for the implementation-time APIs and the manual dashboard check (O30) were done by the lead agent: see "Docs verified" and the O30 paragraph under "Results".
- Rules section 6 names the allowed fields (application ID, assessment ID, decision, `RulesVersion`, status codes, timings) but not the HTTP semantic-convention keys of the framework server spans (O4 list under "Results after the gates": method, route, path, redacted query, scheme, status code, protocol version, server address, error type). Alessandro decides at Checkpoint B whether to amend the rules wording.
- Known, intended gap (no test): when the host shuts down while the worker is processing an item, the `assessment.process` span ends with status Unset and only `assessment.id` (no outcome, no count). This matches "shutdown is never a failure": the item stays Pending and is processed at the next start.
- With only a signal-specific endpoint key set (for example `OTEL_EXPORTER_OTLP_TRACES_ENDPOINT`), `UseOtlpExporter` still turns on all three signals, and the other two go to the exporter's default endpoint on localhost, where they fail. Set the base key, or all three signal keys.
- O24 depends on the fixed default port 4318: any endpoint key pointing at a free port would also turn the exporter on, so only the default port can catch an exporter registered unconditionally. It skips, with the reason, when 4318 is taken.

## Time spent (approx.)

## Human review (Alessandro writes this; the agent leaves it empty)
