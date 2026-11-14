# Progress

## Resume here
- **Phase:** 00 (harness + skeleton) **merged** to `main` via PR #1 (squash `b20c823`).
- **Live hook proof done** (2026-11-14) in [#2](https://github.com/Alelentini2001/Mortgage-Application-API-test/pull/2) , squash-merged as `945d3c5`: `protect-files.sh`, `block-dangerous-bash.sh` and `format-on-edit.sh` fired live; all 5 subagents registered. Evidence: `docs/progress/phase-00-harness.md`, "Live proof".
- **Next action:** Alessandro types `/phase 01 spec` (the skill can only be started by a human). Phase 1 then runs up to Checkpoint A. `docs/spec/ORIGINAL_SPEC.md` was added 2026-11-14.
- **Open decisions / blockers for Alessandro:**
  - Later: branch protection on `main` (require the CI check).
- **Last verify:** `./scripts/verify.sh` full → PASSED (2 tests, 91 hook cases) on `chore/phase-00-live-proof`, 2026-11-14. CI "Build, test, format" passed on PR #2.

## Phases

| # | Phase | Status | Branch | PR | Tests | Date |
|---|---|---|---|---|---|---|
| 00 | Harness + skeleton | Merged | `phase-00-harness` | [#1](https://github.com/Alelentini2001/Mortgage-Application-API-test/pull/1) | 2/2 + 91 hook cases | 2026-11-14 |
| 01 | Spec + production layers | Ready | — | — | — | — |
| 02 | Core domain + calculator (TDD) | Not started | — | — | — | — |
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

Snapshot at the end of Phase 1 (2026-11-14), produced by the `spec-checker` subagent (prompt P01-03) from `docs/spec/ORIGINAL_SPEC.md`. Re-run at the end of every phase.

**Status key.** "Met" needs code or a test. Design written down but not built is "partial".
**Totals now:** 0 met · 27 partial · 28 missing (counted from the table).

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
| R28 | Tests for core business logic | Testing | missing | Phase 2 (TDD; golden cases from rules §8) |
| R29 | Tests for critical validation rules | Testing | missing | Phase 3 |
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

The spec-checker's evidence for each row (the file and line it opened) is in its report, summarised in `docs/progress/phase-01-spec.md`, "Review gate".
