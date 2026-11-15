# Phase 05: Logging + instrumentation

Status: **In progress: plan approved (2026-11-15), test design next** · Branch: `phase-05-observability` · PR: — · Started: 2026-11-15 · Finished: —

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

## What was built (files)

## Tests

## Security gate

## Review gate

## AI output corrected or rejected (REAL cases only)

## Optimisations considered

## Open issues

- The dashboard image tag to pin and its publish date (P4-D4 still uses tags, not digests; digest pinning is Phase 8).

## Time spent (approx.)

## Human review (Alessandro writes this; the agent leaves it empty)
