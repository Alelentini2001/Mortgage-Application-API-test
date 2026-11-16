# Progress

## Resume here
- **Phase:** Phase 11 (fresh-clone rehearsal + presentation) **ready for Checkpoint B** on branch `phase-11-presentation`. Phase 10 merged before it (#20, `dc539df`). Phase file: `docs/progress/phase-11-presentation.md`.
- **Cross-IDE and cross-model portability:** started in Claude Code (Phases 00–07 implementation, Claude Opus 5.5 / Sonnet 4.6), then resumed in Google Antigravity IDE (Gemini 4 Argon) from Phase 07's gates through Phases 08, 09, 10, and 11 to showcase that `AGENTS.md`, `.claude/rules/`, `.claude/skills/`, and `.claude/agents/` (`test-designer`, `security-auditor`, `dotnet-reviewer`, `spec-checker`) run seamlessly across different IDEs and AI models.
- **Working rule from Alessandro (P06-12):** phases run one at a time from now on, no parallel worktrees.
- **Built so far:** four endpoints under `/api/applications` (create 201, get, assess 202 + background worker, get assessment 200 with Pending/Completed/Failed), ES256 JWT bearer authentication (`RequireAuthenticatedUser` fallback policy, owner from `sub` via `ClaimsCurrentUser`, fail-closed `POST /dev/token`, OpenAPI bearer scheme), fixed-window per-subject write rate limiting (429 + `Retry-After`), bounded full-queue enqueue timeout (503 + startup recovery), HTTP security headers + HSTS + `AllowedHosts`, NuGet `packages.lock.json` + `<packageSourceMapping>` + `--locked-mode`, Docker multi-arch `@sha256` digest pins + Dependabot, bash hook self-protection (`.claude/hooks/**`), scalar-projected background processing load + minimal column updates, SQLite `EXPLAIN QUERY PLAN` index verification + `WAL` journal mode, OpenTelemetry tracing, metrics and logs with an allowlist test, opt-in Aspire dashboard, complete `README.md`, two timed fresh-clone rehearsals (`50.09 s` CLI + `verify.sh`, `13.33 s` Docker Compose), `docs/presentation/DEMO-SCRIPT.md`, and `docs/presentation/SLIDES.md` (`R1`–`R55` all met); **275 unit + 311 integration tests (586 total) + 113 hook cases**.
- **Next action:** Checkpoint B approval from Alessandro on the Phase 11 PR, then squash-merge and tag `v1.0.0` on `main`.
- **Carry into later phases:** None (Phase 11 is the final phase).
- **Open decisions / blockers for Alessandro:**
  - Resolved at Phase 11 Checkpoint A (`P11-D3 A`, `P11-02`): keep `docs/ai/PROMPTS.md` verbatim, keep all 55 rows (`R1`–`R55`) in `docs/PROGRESS.md`, keep the 4 accepted ADRs (`0001`–`0004`), and tag `v1.0.0` on `main` after Phase 11 merges.
  - Optional GitHub setting: branch protection on `main` (requiring the CI checks).
- **Last verify:** `./scripts/verify.sh` full → PASSED (**586 tests**: 275 unit + 311 integration; **113 hook cases**) on `phase-11-presentation`, 2026-11-16.

## Phases

| # | Phase | Status | Branch | PR | Tests | Date |
|---|---|---|---|---|---|---|
| 00 | Harness + skeleton | Merged | `phase-00-harness` | [#1](https://github.com/Alelentini2001/Mortgage-Application-API-test/pull/1) | 2/2 + 91 hook cases | 2026-11-14 |
| 01 | Spec + production layers | Merged | `phase-01-spec` | [#3](https://github.com/Alelentini2001/Mortgage-Application-API-test/pull/3) | docs only (18 golden examples for Phase 2) | 2026-11-14 |
| 02 | Core domain + calculator (TDD) | Merged | `phase-02-core` | [#5](https://github.com/Alelentini2001/Mortgage-Application-API-test/pull/5) | 215 unit + 1 integration placeholder, 216/216 | 2026-11-14 |
| 03 | API + persistence + validation (MVP) | Merged | `phase-03-api` | [#6](https://github.com/Alelentini2001/Mortgage-Application-API-test/pull/6) | 258 unit + 89 integration, 347/347 | 2026-11-14 |
| 04 | Docker | Merged | `phase-04-docker` | [#7](https://github.com/Alelentini2001/Mortgage-Application-API-test/pull/7) | 258 unit + 89 integration, 347/347; CI `docker` job (build, health, user, POST/GET) | 2026-11-15 |
| 05 | Logging + instrumentation | Merged | `phase-05-observability` | [#9](https://github.com/Alelentini2001/Mortgage-Application-API-test/pull/9) | 274 unit + 168 integration, 442/442 | 2026-11-15 |
| 06 | Background assessments | Merged | `phase-06-background` | [#8](https://github.com/Alelentini2001/Mortgage-Application-API-test/pull/8) | 269 unit + 115 integration, 384/384; CI `docker` job also checks the 202 flow | 2026-11-15 |
| H1 | Harness: usage and model-choice rules (light) | Merged | `harness-01-context` | [#10](https://github.com/Alelentini2001/Mortgage-Application-API-test/pull/10) | docs only (442 tests unchanged) | 2026-11-15 |
| 07 | Authentication + authorisation | Merged | `phase-07-auth` | [#11](https://github.com/Alelentini2001/Mortgage-Application-API-test/pull/11) | 274 unit + 268 integration, 542/542; CI `docker` job also checks 401 and `/dev/token` | 2026-11-16 |
| 08 | Security hardening | Merged | `phase-08-security` | [#12](https://github.com/Alelentini2001/Mortgage-Application-API-test/pull/12) | 274 unit + 303 integration, 577/577 + 113 hook cases | 2026-11-16 |
| 09 | Optimisation + quality | Merged | `phase-09-quality` | [#19](https://github.com/Alelentini2001/Mortgage-Application-API-test/pull/19) | 275 unit + 311 integration, 586/586 + 113 hook cases | 2026-11-16 |
| 10 | Documentation | Merged | `phase-10-docs` | [#20](https://github.com/Alelentini2001/Mortgage-Application-API-test/pull/20) | docs only (586 tests + 113 hook cases unchanged) | 2026-11-16 |
| 11 | Fresh-clone rehearsal + presentation | In review (Checkpoint B) | `phase-11-presentation` | — | 2 timed fresh-clone rehearsals (`50.09 s` CLI, `13.33 s` Docker) + 586 tests + 113 hook cases | 2026-11-16 |

## Requirement traceability

Snapshot at the end of Phase 3 (2026-11-14), produced by the `spec-checker` subagent (prompt P03-09) at `a446c64` from `docs/spec/ORIGINAL_SPEC.md`. Re-run at the end of every phase.

**Status key.** "Met" needs code, a test, or a rehearsed artifact. Design written down but not built is "partial".
**Totals now:** **55 met · 0 partial · 0 missing** after Phase 11 (R43, R45–R55 met; `spec-checker` P11-05). After Phase 10: 43 / 4 / 8 (R19, R20, R34–R42, R44 met; `spec-checker` P10-05). After Phase 9: 31 / 10 / 14 (R25 met; `spec-checker` P09-07). After Phase 7/8: 30 / 10 / 15 (R31 met, all four Optional Enhancements R30–R33 met; `spec-checker` P07-11). After Phase 5: 29 / 10 / 16 (R33 met; `spec-checker` P05-09). After Phase 6: 28 / 10 / 17 (R30 met, R13 now 202 + polling; `spec-checker` P06-08). After Phase 4: 27 / 10 / 18. End of Phase 3: 26 / 9 / 20.

Paths: "Endpoints" = `src/MortgageApplications.Api/Applications/ApplicationEndpoints.cs`; test classes are in `tests/MortgageApplications.IntegrationTests/` unless marked unit.

| ID | Requirement (short) | Spec section | Status | Where (file / test / planned phase) |
|---|---|---|---|---|
| R1 | Submit a mortgage application | Scenario | met | Endpoints `POST /api/applications`; `CreateApplicationTests` |
| R2 | Retrieve an existing application | Scenario | met | Endpoints `GET /api/applications/{id}`; `CreateApplicationTests` (POST then GET) |
| R3 | Basic affordability assessment | Scenario | met | Endpoints `POST …/assessments`, `MortgageApplicationService.AssessAsync`; `AssessmentTests` (E1, E2, E18) |
| R4 | Endpoint to create an application | Req 1 | met | `POST /api/applications` (201 + `Location`); `CreateApplicationTests` |
| R5 | Field: annual income | Req 1 | met | `CreateApplicationRequest`, `MortgageApplicationConfiguration`; `ValidationTests`, `CreateApplicationTests` |
| R6 | Field: monthly expenses | Req 1 | met | Same as R5 |
| R7 | Field: deposit amount | Req 1 | met | Same as R5 |
| R8 | Field: property value | Req 1 | met | Same as R5 |
| R9 | Field: term in years | Req 1 | met | `TermYears` with `[Range(5, 40)]`; `ValidationTests` |
| R10 | Validate inputs appropriately | Req 1 | met | `AddValidation` + `[Required]`/`[Range]` + `IValidatableObject` over `ApplicationInputRules`; `ValidationTests`, `JsonStrictnessTests`, unit `ApplicationInputRulesTests` |
| R11 | Persist the application | Req 1 | met | `EfMortgageApplicationRepository`, configurations, migration `InitialCreate`; round-trip tests in `CreateApplicationTests` and `AssessmentTests` (E18). Proven on in-memory SQLite; the file database only by the manual smoke test |
| R12 | Endpoint to retrieve an application | Req 2 | met | `GET /api/applications/{id:guid}` (200/404); `CreateApplicationTests`, `NotFoundAndOwnerTests` |
| R13 | Endpoint to assess affordability | Req 3 | met | `POST /api/applications/{id}/assessments` (202 + `Location`, background worker) and `GET …/assessments/{assessmentId}` (200, poll until Completed or Failed); `AssessmentTests`, `AssessmentWorkerTests` |
| R14 | Assessment includes LTV | Req 3 | met | `AssessmentResultResponse.LoanToValuePercent`; `AssessmentTests`; unit golden tests |
| R15 | Assessment includes debt-to-income | Req 3 | met | `DebtToIncomePercent`; `AssessmentTests` |
| R16 | Decision: approved or declined | Req 3 | met | `Decision`; `AssessmentTests` (E1 Approved, E2 Declined) |
| R17 | Maximum borrowing estimate | Req 3 | met | `MaxBorrowing` (OpenAPI description: income-based); `AssessmentTests`, `StartupTests` (OpenAPI) |
| R18 | Short explanation for the decision | Req 3 | met | `Summary` + `Checks`; exact text asserted in `AssessmentTests` |
| R19 | Logic reasonable and clearly explained | Req 3 | met | `docs/spec/affordability-rules.md`, response `summary`/`checks`, `README.md:155-206`; slide 2 tracked in R54 |
| R20 | State assumptions clearly | Req 3 | met | `docs/spec/affordability-rules.md` §1 & §7, response `assessmentRate`/`rulesVersion`, `README.md:159-169,198-206` |
| R21 | ASP.NET 10 app, good framework use | Tech | met (judgement) | `net10.0`; `MapGroup`, `TypedResults`, `AddValidation`, `AddProblemDetails`, `IExceptionHandler`, `ValidateOnStart`, `TimeProvider`, EF Core (`Program.cs`). The spec-checker flags this as subjective |
| R22 | Clear model structure and relationships | Tech | met | One-to-many application → assessments via FK, index, cascade delete (`AffordabilityAssessmentConfiguration`); entities in `Core/Applications/`. No navigation properties and no direct cascade test (no delete endpoint) |
| R23 | Use of validations | Tech | met | Same as R10, plus `AffordabilityOptionsValidator` (`StartupTests`) and strict JSON |
| R24 | Thoughtful organisation of business logic | Tech | met | Core: calculator, `ApplicationInputRules`, `MortgageApplicationService`; Infrastructure: repository; thin Api handlers |
| R25 | Clean and readable code | Tech | met (judgement) | Phase 9 clean-up pass (`Program.cs`, `AssessmentTelemetry`, `AssessmentWorker`, `MortgageApplicationService`, `EfMortgageApplicationRepository`, `.claude/rules/backend.md`, `tests/CLAUDE.md`); `TreatWarningsAsErrors` + `dotnet format --verify-no-changes` |
| R26 | Exposes a JSON API | Tech | met | `TypedResults` JSON bodies; integration tests parse them |
| R27 | Automated tests are required | Testing | met | `tests/MortgageApplications.UnitTests/`, `tests/MortgageApplications.IntegrationTests/` (`WebApplicationFactory`); 586/586 + 113 hook cases |
| R28 | Tests for core business logic | Testing | met | `tests/MortgageApplications.UnitTests/Affordability/` (golden E1–E19, boundaries, rounding, culture, property tests) |
| R29 | Tests for critical validation rules | Testing | met | `ValidationTests` (HTTP 400 per field and bound); unit `ApplicationInputRulesTests`, `InputGuardTests` |
| R30 | Optional: background processing | Optional | met | `AssessmentWorker`, `ChannelAssessmentQueue` (`EnqueueTimeoutSeconds` → 503 + startup recovery), `IAssessmentQueue`; `AssessmentWorkerTests`; ADR 0003 |
| R31 | Optional: authentication | Optional | met | ES256 JWT bearer (`AuthenticationSetup`, `ClaimsCurrentUser`, `SubjectClaim`, `TokenKeys`, `SigningPublicKey`), fail-closed `POST /dev/token` (`DevTokenEndpoint`), OpenAPI bearer scheme (`BearerSecurityTransformer`); `AuthenticationTests`, `AuthorizationTests`, `DevTokenTests`, `AuthTelemetryTests`, `StartupTests`; ADR 0004 |
| R32 | Optional: Docker support | Optional | met | `Dockerfile` (`@sha256` digests, `dotnet restore --locked-mode`), `compose.yaml`; CI job "Docker image build and smoke test"; `SecurityHardeningTests`, `ComposeFileTests` |
| R33 | Optional: logging or instrumentation | Optional | met | `[LoggerMessage]` logs, OpenTelemetry tracing and metrics (`AssessmentTelemetry`, `Program.cs`), opt-in dashboard; `TelemetryAllowlistTests`, `AssessmentSpanTests`, `AssessmentMetricTests`, `OtlpExportTests` |
| R34 | README: setup instructions | Docs | met | `README.md:39-52` (`.NET 10 SDK` via `global.json`, Docker/Compose, optional `gitleaks`/`semgrep`, `git clone`, `dotnet restore --locked-mode`) |
| R35 | README: how to run the app | Docs | met | `README.md:55-130` (`dotnet run`, `docker compose up --build`, Aspire dashboard override, `curl` walkthrough + `requests.http`, production auth) |
| R36 | README: how to run tests | Docs | met | `README.md:133-151` (`./scripts/verify.sh`, `./scripts/verify.sh --fast`, `dotnet test --solution`, xUnit v3 `--filter-class` / `--filter-method`) |
| R37 | README: design decisions explained | Docs | met | `README.md:209-222` (linked to ADRs 0001, 0003, 0004) |
| R38 | README: Design & Reflection section | Design | met | `README.md:209-246` (`## Design & Reflection`, subsections 1–4) |
| R39 | 2-3 key design decisions and why | Design Q1 | met | `README.md:211-222` (pure `Core` + `decimal`, save-before-enqueue background pipeline, fail-closed ES256 auth + owner scoping + allowlisted telemetry) |
| R40 | Scaling: first change under load | Design Q2 | met | `README.md:224-231`, `docs/layers/PRODUCTION-LAYERS.md:16`, ADR 0003 (Azure SQL + Service Bus + `RowVersion` + `AttemptCount`) |
| R41 | Trade-offs: kept simple or left out | Design Q3 | met | `README.md:232-238`, `docs/layers/PRODUCTION-LAYERS.md`, `docs/spec/affordability-rules.md` §7 |
| R42 | Next steps with more time | Design Q4 | met | `README.md:239-246`, `docs/layers/PRODUCTION-LAYERS.md` |
| R43 | Submit as GitHub repo or archive | Submission | met | GitHub repo `Alelentini2001/Mortgage-Application-API-test` (`README.md:48`, `DEMO-SCRIPT.md:22-30`); `v1.0.0` tag after Phase 11 merge |
| R44 | Clarity over production-readiness | Time | met (judgement) | Phase 9 clean-up + `README.md:9-23,155-246` |
| R45 | Fresh clone in the session | Follow-up | met (rehearsed) | `docs/presentation/DEMO-SCRIPT.md:12,20-30,39-41` (2 timed clean clones: `1.08 s` and `1.02 s`, 2026-11-16) |
| R46 | Build and run from the clone | Follow-up | met (rehearsed) | `docs/presentation/DEMO-SCRIPT.md:24-30,43-52` (Rehearsal 1: full `verify.sh` `44.14 s` + `dotnet run` `4.49 s`, total `50.09 s`; Rehearsal 2: `docker compose up -d --build` `11.00 s` + health `0.91 s`, total `13.33 s`) |
| R47 | Short live demo | Follow-up | met (rehearsed) | `docs/presentation/DEMO-SCRIPT.md:13,54-104` (`03:00–09:00`, rehearsed 8-step `curl` flow in `0.39 s` / `0.40 s`), `src/MortgageApplications.Api/requests.http:1-126` |
| R48 | Talk through code and choices | Follow-up | met | `docs/presentation/DEMO-SCRIPT.md:14,108-141` (`09:00–14:00` 5-group codebase walkthrough + `20:00–30:00` Q&A answers) |
| R49 | At most 3 slides | Follow-up | met | `docs/presentation/SLIDES.md:1-76` (strictly 3 slides separated by `---`) |
| R50 | Slides after demo; session ≤ 30 min | Follow-up | met | `docs/presentation/DEMO-SCRIPT.md:8-17` (30-min budget: clone/build `00:00–03:00`, demo `03:00–09:00`, code `09:00–14:00`, slides `14:00–20:00`, Q&A `20:00–30:00`) |
| R51 | Slides brief, start a conversation | Follow-up | met | `docs/presentation/SLIDES.md:9-76` (concise bullets/tables + `~2 min` conversation-starting speaker notes per slide) |
| R52 | Topic: solution overview and structure | Follow-up | met | `docs/presentation/SLIDES.md:9-33` (`Slide 1 · Solution Overview & Architecture`) |
| R53 | Topic: key design decisions | Follow-up | met | `docs/presentation/SLIDES.md:60-66` (`Slide 3 · Key Design Decisions & First Change at Scale`) |
| R54 | Topic: assessment logic and assumptions | Follow-up | met | `docs/presentation/SLIDES.md:37-56` (`Slide 2 · Affordability Logic & Assumptions`), `docs/spec/affordability-rules.md` |
| R55 | Topic: first change at scale | Follow-up | met | `docs/presentation/SLIDES.md:67-70` (`Slide 3`: Azure SQL + Service Bus + `RowVersion` + `AttemptCount`), `docs/layers/PRODUCTION-LAYERS.md:16` |

The spec-checker's full report (with file and line evidence for each row) is summarised in `docs/progress/phase-03-api.md`, "Review gate". It reported `requests.http` missing because it was added after its snapshot (`eb21d5f`). Re-run the `spec-checker` (prompt P03-09) to regenerate the evidence column.
