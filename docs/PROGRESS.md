# Progress

## Resume here
- **Phase:** 03 (API + persistence + validation, the minimum submittable product). PR [#6](https://github.com/Alelentini2001/Mortgage-Application-API-test/pull/6) is open on `phase-03-api`, **waiting for Alessandro at Checkpoint B**. Phase file: `docs/progress/phase-03-api.md`.
- **Built:** four endpoints under `/api/applications` (create 201 + `Location`, get, synchronous assess 201, get assessment), validation at the boundary over one Core rule set, strict JSON, owner-scoped EF Core SQLite persistence with an initial migration, ProblemDetails without exception text, startup options validation, OpenAPI + Scalar in Development, `requests.http`; 258 unit + 89 integration tests.
- **Next action:** Alessandro reviews PR #6 and replies `approved` (or asks for changes). Then: squash-merge with a written message, mark Phase 3 merged here, `/clear`, and start Phase 4 ∥ Phase 6 (master prompt B5 parallel plan).
- **Carry into later phases:**
  - Phase 4: connection string location and the startup-migration ADR (security I1).
  - Phase 5: `[LoggerMessage]` with allowlisted fields only + log-capture test (S1 part b); real 500s are logged by the framework today and must be covered.
  - Phase 6: no silent queue drops and check `Status` before `Complete`; the POST becomes 202; the result comparer's cost once the worker tracks assessments (`/code-review` 6).
  - Phase 7: replace `DevelopmentCurrentUser`, add `RequireAuthorization` (reviewer F6); secure default policy and a fail-closed dev token endpoint.
  - Phase 8: rate-limit decision or an accepted risk; NuGet lock files and package-source mapping (security L3); NuGet signature verification on macOS.
  - Phase 9: `/code-review` 1, 5 and 7 (one bad-request error path, one 2 dp mechanism, faster strictness tests).
- **Open decisions / blockers for Alessandro:**
  - Phase 3: accept `DevelopmentCurrentUser` until Phase 7 (recommended: yes); `.claude/rules/backend.md` says reads "project to DTOs" but P3-D1 returns entities through the Core port: reword the rule or accept the exception (`/code-review` 3); optional ADR for the persistence choice (`/adr` is user-invoked).
  - Optional ADR for the Core domain shape (P2-D1/D2): `/adr` is user-invoked only.
  - Whether to write the property-test exception into `tests/CLAUDE.md` ("No logic in tests beyond setup"; Phase 2 `/code-review` finding 7).
  - `security-auditor` S10 (Phase 1): keep or redact local paths in the verbatim prompt logs before the repo goes public.
  - `spec-checker` questions (status wording, constraint rows R44/R50, final tag, optional scope R30–R33): see `docs/progress/phase-01-spec.md`, "Open issues".
  - Later: branch protection on `main` (require the CI check).
- **Last verify:** `./scripts/verify.sh` full → PASSED (347 tests, 91 hook cases, clean tree) on `phase-03-api` at `acbb482`, 2026-11-14. CI on PR #6: see the PR.

## Phases

| # | Phase | Status | Branch | PR | Tests | Date |
|---|---|---|---|---|---|---|
| 00 | Harness + skeleton | Merged | `phase-00-harness` | [#1](https://github.com/Alelentini2001/Mortgage-Application-API-test/pull/1) | 2/2 + 91 hook cases | 2026-11-14 |
| 01 | Spec + production layers | Merged | `phase-01-spec` | [#3](https://github.com/Alelentini2001/Mortgage-Application-API-test/pull/3) | docs only (18 golden examples for Phase 2) | 2026-11-14 |
| 02 | Core domain + calculator (TDD) | Merged | `phase-02-core` | [#5](https://github.com/Alelentini2001/Mortgage-Application-API-test/pull/5) | 215 unit + 1 integration placeholder, 216/216 | 2026-11-14 |
| 03 | API + persistence + validation (MVP) | PR open (Checkpoint B) | `phase-03-api` | [#6](https://github.com/Alelentini2001/Mortgage-Application-API-test/pull/6) | 258 unit + 89 integration, 347/347 | 2026-11-14 |
| 04 | Docker | Not started | — | — | — | — |
| 05 | Logging + instrumentation | Not started | — | — | — | — |
| 06 | Background assessments | Not started | — | — | — | — |
| 07 | Authentication + authorisation | Not started | — | — | — | — |
| 08 | Security hardening | Not started | — | — | — | — |
| 09 | Optimisation + quality | Not started | — | — | — | — |
| 10 | Documentation | Not started | — | — | — | — |
| 11 | Fresh-clone rehearsal + presentation | Not started | — | — | — | — |

## Requirement traceability

Snapshot at the end of Phase 3 (2026-11-14), produced by the `spec-checker` subagent (prompt P03-09) at `a446c64` from `docs/spec/ORIGINAL_SPEC.md`. Re-run at the end of every phase.

**Status key.** "Met" needs code or a test. Design written down but not built is "partial".
**Totals now:** 26 met · 9 partial · 20 missing (Phase 2 was 1 / 28 / 26).

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
| R13 | Endpoint to assess affordability | Req 3 | met | `POST /api/applications/{id}/assessments` (synchronous, 201 + `Location`) and `GET …/assessments/{assessmentId}`; `AssessmentTests`. Phase 6 makes it 202 |
| R14 | Assessment includes LTV | Req 3 | met | `AssessmentResultResponse.LoanToValuePercent`; `AssessmentTests`; unit golden tests |
| R15 | Assessment includes debt-to-income | Req 3 | met | `DebtToIncomePercent`; `AssessmentTests` |
| R16 | Decision: approved or declined | Req 3 | met | `Decision`; `AssessmentTests` (E1 Approved, E2 Declined) |
| R17 | Maximum borrowing estimate | Req 3 | met | `MaxBorrowing` (OpenAPI description: income-based); `AssessmentTests`, `StartupTests` (OpenAPI) |
| R18 | Short explanation for the decision | Req 3 | met | `Summary` + `Checks`; exact text asserted in `AssessmentTests` |
| R19 | Logic reasonable and clearly explained | Req 3 | partial | Rules doc and the response's `summary`/`checks`; README in Phase 10; slides in Phase 11 |
| R20 | State assumptions clearly | Req 3 | partial | Rules §7; the response carries the rate and rules version; README in Phase 10 |
| R21 | ASP.NET 10 app, good framework use | Tech | met (judgement) | `net10.0`; `MapGroup`, `TypedResults`, `AddValidation`, `AddProblemDetails`, `IExceptionHandler`, `ValidateOnStart`, `TimeProvider`, EF Core (`Program.cs`). The spec-checker flags this as subjective |
| R22 | Clear model structure and relationships | Tech | met | One-to-many application → assessments via FK, index, cascade delete (`AffordabilityAssessmentConfiguration`); entities in `Core/Applications/`. No navigation properties and no direct cascade test (no delete endpoint) |
| R23 | Use of validations | Tech | met | Same as R10, plus `AffordabilityOptionsValidator` (`StartupTests`) and strict JSON |
| R24 | Thoughtful organisation of business logic | Tech | met | Core: calculator, `ApplicationInputRules`, `MortgageApplicationService`; Infrastructure: repository; thin Api handlers |
| R25 | Clean and readable code | Tech | missing | Not judged by tests; Phase 9 clean-up pass |
| R26 | Exposes a JSON API | Tech | met | `TypedResults` JSON bodies; integration tests parse them |
| R27 | Automated tests are required | Testing | met | `tests/MortgageApplications.UnitTests/`, `tests/MortgageApplications.IntegrationTests/` (`WebApplicationFactory`); 347/347 |
| R28 | Tests for core business logic | Testing | met | `tests/MortgageApplications.UnitTests/Affordability/` (golden E1–E19, boundaries, rounding, culture, property tests) |
| R29 | Tests for critical validation rules | Testing | met | `ValidationTests` (HTTP 400 per field and bound); unit `ApplicationInputRulesTests`, `InputGuardTests` |
| R30 | Optional: background processing | Optional | missing | Phase 6 |
| R31 | Optional: authentication | Optional | missing | Phase 7 (`DevelopmentCurrentUser` only, a fixed owner) |
| R32 | Optional: Docker support | Optional | missing | Phase 4 |
| R33 | Optional: logging or instrumentation | Optional | missing | Phase 5 |
| R34 | README: setup instructions | Docs | missing | Phase 10 |
| R35 | README: how to run the app | Docs | missing | Phase 10 |
| R36 | README: how to run tests | Docs | missing | Phase 10 |
| R37 | README: design decisions explained | Docs | missing | Phase 10 |
| R38 | README: Design & Reflection section | Design | missing | Phase 10 |
| R39 | 2-3 key design decisions and why | Design Q1 | missing | Phase 10 (inputs: ADR 0001, `DESIGN-JOURNAL.md`); Alessandro edits |
| R40 | Scaling: first change under load | Design Q2 | partial | Draft in `PRODUCTION-LAYERS.md`, "Scaling"; README in Phase 10 |
| R41 | Trade-offs: kept simple or left out | Design Q3 | partial | Notes in `PRODUCTION-LAYERS.md` and rules §7; README in Phase 10 |
| R42 | Next steps with more time | Design Q4 | partial | Notes in `PRODUCTION-LAYERS.md`; README in Phase 10 |
| R43 | Submit as GitHub repo or archive | Submission | partial | The GitHub repo exists; final state in Phase 11 |
| R44 | Clarity over production-readiness | Time | partial | Expectation only; Phases 9-10 |
| R45 | Fresh clone in the session | Follow-up | missing | Phase 11 (two timed clean runs) |
| R46 | Build and run from the clone | Follow-up | missing | Phases 4, 10, 11 |
| R47 | Short live demo | Follow-up | missing | `requests.http` exists (Phase 3); `DEMO-SCRIPT.md` in Phase 11 |
| R48 | Talk through code and choices | Follow-up | missing | Phase 11 (`DEMO-SCRIPT.md`, likely questions) |
| R49 | At most 3 slides | Follow-up | missing | Phase 11 (`docs/presentation/SLIDES.md`) |
| R50 | Slides after demo; session ≤ 30 min | Follow-up | missing | Phase 11 (minute-by-minute plan) |
| R51 | Slides brief, start a conversation | Follow-up | missing | Phase 11 |
| R52 | Topic: solution overview and structure | Follow-up | missing | Phase 11, slide 1 |
| R53 | Topic: key design decisions | Follow-up | missing | Phase 11, slide 3 |
| R54 | Topic: assessment logic and assumptions | Follow-up | partial | Logic in code and the rules doc; slide 2 in Phase 11 |
| R55 | Topic: first change at scale | Follow-up | partial | Content in `PRODUCTION-LAYERS.md`, "Scaling"; slide in Phase 11 |

The spec-checker's full report (with file and line evidence for each row) is summarised in `docs/progress/phase-03-api.md`, "Review gate". It reported `requests.http` missing because it was added after its snapshot (`eb21d5f`). Re-run the `spec-checker` (prompt P03-09) to regenerate the evidence column.
