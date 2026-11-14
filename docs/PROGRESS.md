# Progress

## Resume here
- **Phase:** 00 (harness + skeleton). Implementation and gates done on `phase-00-harness`; PR open.
- **Next action:** squash-merge PR #1 once CI is green (approved at Checkpoint B), then `/clear`. Phase 1 needs `docs/spec/ORIGINAL_SPEC.md`.
- **Open decisions / blockers for Alessandro:**
  - Add `docs/spec/ORIGINAL_SPEC.md` (blocks Phase 1).
  - Live hook proof needs one interactive Claude Code session started in the repo (accept the trust dialog; `claude` CLI login had expired).
  - Later: branch protection on `main` (require the CI check).
- **Last verify:** `./scripts/verify.sh` full → PASSED (2 tests, 91 hook cases), 2026-11-14.

## Phases

| # | Phase | Status | Branch | PR | Tests | Date |
|---|---|---|---|---|---|---|
| 00 | Harness + skeleton | Approved, merging | `phase-00-harness` | [#1](https://github.com/Alelentini2001/Mortgage-Application-API-test/pull/1) | 2/2 + 91 hook cases | 2026-11-14 |
| 01 | Spec + production layers | Blocked (spec file) | — | — | — | — |
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
