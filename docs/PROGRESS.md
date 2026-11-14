# Progress

## Resume here
- **Phase:** 02 (core domain + calculator, TDD) on branch `phase-02-core`, PR open, **waiting for Alessandro's Checkpoint B**. Phase file: `docs/progress/phase-02-core.md`.
- **Built:** pure `AffordabilityCalculator` (rules v1.0), `AffordabilityOptions.Validate()`, input guard, `MortgageApplication` and `AffordabilityAssessment` entities; 215 unit tests (golden E1–E19, boundaries, guard, culture, property, entities).
- **Next action:** Alessandro reviews the PR and replies `approved` → squash-merge → `/clear` → `/phase 03 api`.
- **Carry into later phases:**
  - Phase 3: owner scoping for assessments through the application (security S2); `ValidateOnStart` for `AffordabilityOptions` (S4); 500s without exception text (S5); EF Core mapping for `MortgageApplication` (review R4); HTTP 400 validation tests (R29).
  - Phase 5: `[LoggerMessage]` with allowlisted fields only + log-capture test (S1 part b, from Phase 1 too).
  - Phase 6 no silent queue drops; Phase 7 secure default policy and a fail-closed dev token endpoint; Phase 8 the rate-limit decision or an accepted risk (all from Phase 1).
- **Open decisions / blockers for Alessandro:**
  - Optional ADR for the Core domain shape (P2-D1/D2): `/adr` is user-invoked only.
  - Whether to write the property-test exception into `tests/CLAUDE.md` ("No logic in tests beyond setup"; `/code-review` finding 7).
  - `security-auditor` S10 (Phase 1): keep or redact local paths in the verbatim prompt logs before the repo goes public.
  - `spec-checker` questions (status wording, constraint rows R44/R50, final tag, optional scope R30–R33): see `docs/progress/phase-01-spec.md`, "Open issues".
  - Later: branch protection on `main` (require the CI check).
- **Last verify:** `./scripts/verify.sh` full → PASSED (216 tests, 91 hook cases) on `phase-02-core` at `08d86d3`, 2026-11-14.

## Phases

| # | Phase | Status | Branch | PR | Tests | Date |
|---|---|---|---|---|---|---|
| 00 | Harness + skeleton | Merged | `phase-00-harness` | [#1](https://github.com/Alelentini2001/Mortgage-Application-API-test/pull/1) | 2/2 + 91 hook cases | 2026-11-14 |
| 01 | Spec + production layers | Merged | `phase-01-spec` | [#3](https://github.com/Alelentini2001/Mortgage-Application-API-test/pull/3) | docs only (18 golden examples for Phase 2) | 2026-11-14 |
| 02 | Core domain + calculator (TDD) | PR open (Checkpoint B) | `phase-02-core` | [#5](https://github.com/Alelentini2001/Mortgage-Application-API-test/pull/5) | 215 unit + 1 integration placeholder, 216/216 | 2026-11-14 |
| 03 | API + persistence + validation (MVP) | Not started | — | — | — | — |
| 04 | Docker | Not started | — | — | — | — |
| 05 | Logging + instrumentation | Not started | — | — | — | — |
| 06 | Background assessments | Not started | — | — | — | — |
| 07 | Authentication + authorisation | Not started | — | — | — | — |
| 08 | Security hardening | Not started | — | — | — | — |
| 09 | Optimisation + quality | Not started | — | — | — | — |
| 10 | Documentation | Not started | — | — | — | — |
| 11 | Fresh-clone rehearsal + presentation | Not started | — | — | — | — |

## Requirement traceability

Snapshot at the end of Phase 2 (2026-11-14), produced by the `spec-checker` subagent (prompt P02-07) from `docs/spec/ORIGINAL_SPEC.md`. Phase 2 changed R28 and R29 only. Re-run at the end of every phase.

**Status key.** "Met" needs code or a test. Design written down but not built is "partial".
**Totals now:** 1 met · 28 partial · 26 missing (counted from the table; Phase 1 was 0 / 27 / 28).

| ID | Requirement (short) | Spec section | Status | Where (file / planned phase) |
|---|---|---|---|---|
| R1 | Submit a mortgage application | Scenario | missing | Phase 3 |
| R2 | Retrieve an existing application | Scenario | missing | Phase 3 |
| R3 | Basic affordability assessment | Scenario | partial | Rules: `docs/spec/affordability-rules.md`. Code: Phase 2 (calculator), Phase 3 (endpoint) |
| R4 | Endpoint to create an application | Req 1 | missing | Phase 3 (`POST /api/applications`) |
| R5 | Field: annual income | Req 1 | partial | Rules §1. Code: Phases 2-3 |
| R6 | Field: monthly expenses | Req 1 | partial | Rules §1. Code: Phases 2-3 |
| R7 | Field: deposit amount | Req 1 | partial | Rules §1. Code: Phases 2-3 |
| R8 | Field: property value | Req 1 | partial | Rules §1. Code: Phases 2-3 |
| R9 | Field: term in years | Req 1 | partial | Rules §1. Code: Phases 2-3 |
| R10 | Validate inputs appropriately | Req 1 | partial | Limits: rules §1. Code: Phase 3 (`AddValidation`, `IValidatableObject`) |
| R11 | Persist the application | Req 1 | missing | Phase 3 (EF Core SQLite + migration); design in `PRODUCTION-LAYERS.md`, "Database" |
| R12 | Endpoint to retrieve an application | Req 2 | missing | Phase 3 (`GET /api/applications/{id}`) |
| R13 | Endpoint to assess affordability | Req 3 | missing | Phase 3 (synchronous); Phase 6 (202 + Location) |
| R14 | Assessment includes LTV | Req 3 | partial | Rules §2, §5. Code: Phase 2 |
| R15 | Assessment includes debt-to-income | Req 3 | partial | Rules §2, §5. Code: Phase 2 |
| R16 | Decision: approved or declined | Req 3 | partial | Rules §3. Code: Phase 2 |
| R17 | Maximum borrowing estimate | Req 3 | partial | Rules §2, §5. Code: Phase 2 |
| R18 | Short explanation for the decision | Req 3 | partial | Rules §6. Code: Phase 2 |
| R19 | Logic reasonable and clearly explained | Req 3 | partial | Rules doc (§8: 9 worked examples); README in Phase 10; slides in Phase 11 |
| R20 | State assumptions clearly | Req 3 | partial | Rules §7; README in Phase 10 |
| R21 | ASP.NET 10 app, good framework use | Tech | partial | Phase 0 skeleton (`net10.0`); real use in Phases 3-7 |
| R22 | Clear model structure and relationships | Tech | partial | Design: `PRODUCTION-LAYERS.md`, "Database" (one-to-many). Code: Phases 2-3 |
| R23 | Use of validations | Tech | partial | Rules §1. Code: Phase 3 |
| R24 | Thoughtful organisation of business logic | Tech | partial | Boundaries in `AGENTS.md`; code in Phase 2 |
| R25 | Clean and readable code | Tech | missing | Phases 2-9 (clean-up pass in Phase 9) |
| R26 | Exposes a JSON API | Tech | partial | `/health` only; endpoints in Phase 3 |
| R27 | Automated tests are required | Testing | partial | Placeholder smoke tests only; real tests in Phases 2-3 |
| R28 | Tests for core business logic | Testing | met | `tests/MortgageApplications.UnitTests/Affordability/` (golden E1–E19, boundaries, rounding, culture, property tests) |
| R29 | Tests for critical validation rules | Testing | partial | Domain guard and options tested (`InputGuardTests`, `AffordabilityOptionsTests`); HTTP 400 tests in Phase 3 |
| R30 | Optional: background processing | Optional | missing | Phase 6 |
| R31 | Optional: authentication | Optional | missing | Phase 7 |
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
| R47 | Short live demo | Follow-up | missing | Phase 10 (`requests.http`), Phase 11 (`DEMO-SCRIPT.md`) |
| R48 | Talk through code and choices | Follow-up | missing | Phase 11 (`DEMO-SCRIPT.md`, likely questions) |
| R49 | At most 3 slides | Follow-up | missing | Phase 11 (`docs/presentation/SLIDES.md`) |
| R50 | Slides after demo; session ≤ 30 min | Follow-up | missing | Phase 11 (minute-by-minute plan) |
| R51 | Slides brief, start a conversation | Follow-up | missing | Phase 11 |
| R52 | Topic: solution overview and structure | Follow-up | missing | Phase 11, slide 1 |
| R53 | Topic: key design decisions | Follow-up | missing | Phase 11, slide 3 |
| R54 | Topic: assessment logic and assumptions | Follow-up | partial | Content in the rules doc; slide 2 in Phase 11 |
| R55 | Topic: first change at scale | Follow-up | partial | Content in `PRODUCTION-LAYERS.md`, "Scaling"; slide in Phase 11 |

The spec-checker's report also had an evidence column for each row (the file and line it opened, for example `Program.cs:6` for the missing endpoints). That column was not committed. Re-run the `spec-checker` (prompt P01-03) to regenerate it. The summary is in `docs/progress/phase-01-spec.md`, "Review gate".
