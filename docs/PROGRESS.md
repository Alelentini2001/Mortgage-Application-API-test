# Progress

## Resume here
- **Phase:** 00 (harness + skeleton). Implementation done on `phase-00-harness`; gates and PR in progress.
- **Next action:** finish the Phase 0 gates, open the PR, then **Checkpoint B**.
- **Open decisions / blockers for Alessandro:**
  - Add `docs/spec/ORIGINAL_SPEC.md` (blocks Phase 1).
  - Live hook proof needs one interactive Claude Code session started in the repo (accept the trust dialog; `claude` CLI login had expired).
  - Later: `/install-github-app`, `ANTHROPIC_API_KEY` repo secret, branch protection on `main`.
- **Last verify:** `./scripts/verify.sh` full → PASSED (2 tests), 2026-11-14.

## Phases

| # | Phase | Status | Branch | PR | Tests | Date |
|---|---|---|---|---|---|---|
| 00 | Harness + skeleton | In review | `phase-00-harness` | — | 2/2 (placeholders) | 2026-11-14 |
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
