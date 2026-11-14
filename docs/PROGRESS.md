# Progress

## Resume here
- **Phase:** 00 (harness + skeleton) **merged** to `main` via PR #1 (squash `b20c823`).
- **Live hook proof done** (2026-11-14) on branch `chore/phase-00-live-proof`: `protect-files.sh`, `block-dangerous-bash.sh` and `format-on-edit.sh` fired live; all 5 subagents registered. Evidence: `docs/progress/phase-00-harness.md`, "Live proof".
- **Next action:** Alessandro reviews and merges the live-proof PR, then types `/phase 01 spec` (the skill can only be started by a human) and Phase 1 runs up to Checkpoint A. `docs/spec/ORIGINAL_SPEC.md` was added 2026-11-14.
- **Open decisions / blockers for Alessandro:**
  - Approve and merge the live-proof PR.
  - Later: branch protection on `main` (require the CI check).
- **Last verify:** `./scripts/verify.sh` full → PASSED (2 tests, 91 hook cases) on `chore/phase-00-live-proof`, 2026-11-14.

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

_Filled from Phase 1 onward by the `spec-checker` subagent once `docs/spec/ORIGINAL_SPEC.md` exists._
