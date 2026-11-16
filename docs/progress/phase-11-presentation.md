# Phase 11: Fresh-clone rehearsal + presentation

Status: **Merged** · Branch: `phase-11-presentation` · PR: [#21](https://github.com/Alelentini2001/Mortgage-Application-API-test/pull/21) · Started: 2026-11-16 · Finished: 2026-11-16

## Goal

Complete the final phase of the project (`docs/spec/ORIGINAL_SPEC.md` "Submission" and "Follow-Up Discussion", requirements `R43`, `R45`–`R55`):
- **Two timed fresh-clone rehearsals (`R45`, `R46`):**
  - Clone the GitHub repository into a clean temporary directory twice and time every step from `git clone` to verified `curl` responses (Run 1: local `.NET 10` CLI + `./scripts/verify.sh` + `dotnet run`; Run 2: `docker compose up -d --build` + health/auth/create/get/assess/poll smoke flow + `docker compose down -v`). Record the real measured timings in this phase file and in the demo script.
- **Live demo script & code walkthrough guide (`R47`, `R48`, `R50`):**
  - Author `docs/presentation/DEMO-SCRIPT.md` with a minute-by-minute 30-minute session plan (`00:00–03:00` fresh clone & build, `03:00–09:00` live demo via `requests.http` / `curl` / Scalar, `09:00–14:00` codebase walkthrough with exact file links, `14:00–20:00` 3-slide presentation, `20:00–30:00` Q&A talking points and deep-dive pointers).
- **3-slide presentation (`R49`–`R55`):**
  - Author `docs/presentation/SLIDES.md` (strictly 3 concise, conversation-starting slides in Marp / Markdown slide format with speaker notes):
    1. **Slide 1 — Solution Overview & Architecture (`R52`):** Clean 3-project architecture (`Core` / `Infrastructure` / `Api`), end-to-end request and background assessment lifecycle, and verification harness (`586` unit/integration tests + `113` hook tests + multi-agent gates across Claude Code and Google Antigravity IDE).
    2. **Slide 2 — Affordability Assessment Logic & Assumptions (`R54`):** Pure `decimal` calculator (`AffordabilityCalculator`), 3 orthogonal checks (`LTV <= 90%`, `DTI <= 45%` at a `6%` stress rate, `LTI <= 4.5x`), income-based `MaxBorrowing`, full-precision comparison with single end-rounding (`2 dp`, `AwayFromZero`), and key underwriting assumptions (`RulesVersion = "1.0"`).
    3. **Slide 3 — Key Design Decisions & First Change at Scale (`R53`, `R55`):** The 3 core decisions (pure `Core` + `decimal`, save-before-enqueue `202` pipeline with bounded `503` backpressure + startup sweep, fail-closed ES256 auth + `ICurrentUser` owner scoping + zero-PII telemetry allowlist) and the first change under high load (replace single-writer SQLite + in-process `Channel<Guid>` with **Azure SQL + Azure Service Bus** + `RowVersion` concurrency token + `AttemptCount` retry cap).

## Plan (approved at Checkpoint A)

Approved by Alessandro on 2026-11-16 (`P11-02`), with Option A on `P11-D1`–`P11-D3`.

1. Branch `phase-11-presentation` from `main` (done).
2. Stop at **Checkpoint A** for Alessandro's approval on `P11-D1`–`P11-D3` (done).
3. Execute two timed fresh-clone rehearsals (Run 1: `dotnet` CLI + `./scripts/verify.sh` + live API calls; Run 2: `docker compose up -d --build` + live API calls + `docker compose down -v`), recording exact command outputs and wall-clock durations, and clean up temporary directories (done).
4. Write `docs/presentation/DEMO-SCRIPT.md` and `docs/presentation/SLIDES.md` (and link them from `README.md`) (done).
5. Run `./scripts/verify.sh` (full) (done).
6. Run `security-auditor`, `dotnet-reviewer`, and `spec-checker` subagents; fix any real findings (done).
7. Update `docs/progress/phase-11-presentation.md`, `docs/PROGRESS.md` (final `R1`–`R55` traceability table: **`55 met · 0 partial · 0 missing`**), `docs/ai/PROMPTS.md`, and `docs/ai/DESIGN-JOURNAL.md`; push, open PR, wait for CI, and stop at **Checkpoint B**.

## Decisions

### P11-D1 · Location and format of the demo script and 3-slide deck
- **Options:**
  - **A (chosen):** Place both files under `docs/presentation/` (`docs/presentation/DEMO-SCRIPT.md` and `docs/presentation/SLIDES.md`, formatted as clean Markdown with `---` slide separators compatible with Marp / GitHub preview) and add links to both in `README.md` and `docs/PROGRESS.md`.
  - **B:** Place `DEMO-SCRIPT.md` at the repository root and `SLIDES.md` in `docs/presentation/SLIDES.md`.
- **Trade-offs:** Option A keeps the repository root clean (`README.md`, `AGENTS.md`, `CLAUDE.md`, `Dockerfile`, `compose*.yaml`, `MortgageApplications.slnx`) while grouping all follow-up interview materials together in `docs/presentation/`.
- **Decision:** **Option A**, approved by Alessandro (`P11-02`).

### P11-D2 · Fresh-clone rehearsal execution (`R45`, `R46`)
- **Options:**
  - **A (chosen):** Run two real, timed fresh clones of `https://github.com/Alelentini2001/Mortgage-Application-API-test.git` into temporary directories outside the workspace, execute the full build/test/run/curl flows, record the exact measured wall-clock times in `docs/progress/phase-11-presentation.md` and `docs/presentation/DEMO-SCRIPT.md`, and remove the temporary directories when done.
  - **B:** Write the rehearsal steps without executing a fresh clone.
- **Trade-offs:** Option A satisfies `R45` and `R46` with real measured timings and proves that a fresh `git clone` works out of the box for both `dotnet run` and `docker compose up --build`.
- **Decision:** **Option A**, approved by Alessandro (`P11-02`).

### P11-D3 · Resolution of carried housekeeping items from `docs/PROGRESS.md`
- **Options:**
  - **A (chosen):**
    1. **Verbatim prompt logs (`security-auditor` S10 from Phase 1):** Keep `docs/ai/PROMPTS.md` verbatim (including `/Users/ale_ssd/...` workspace paths, which contain no secrets and are required by the `AGENTS.md` honesty rule).
    2. **Traceability table conventions (`spec-checker` from Phase 1):** Keep all 55 rows (`R1`–`R55`) in `docs/PROGRESS.md` with `met / partial / missing` status (`55 met · 0 partial · 0 missing`).
    3. **Optional ADRs (`P2-D1/D2`, `P3-D1`):** Keep the 4 accepted ADRs (`0001`–`0004`); no additional retroactive ADRs needed since `README.md` and `DESIGN-JOURNAL.md` already document the Core and EF persistence choices.
    4. **Final submission tag (`R43`):** After the Phase 11 PR is squash-merged into `main`, create and push an annotated tag `v1.0.0` on `main`.
  - **B:** Redact `/Users/ale_ssd` in `docs/ai/PROMPTS.md` or skip tagging `v1.0.0`.
- **Trade-offs:** Option A preserves the verbatim integrity of the prompt log, closes all carried open questions in `docs/PROGRESS.md`, and provides a clean `v1.0.0` release tag for submission.
- **Decision:** **Option A**, approved by Alessandro (`P11-02`).

## Docs verified (links)
- `docs/spec/ORIGINAL_SPEC.md` ("Submission", "Time Expectation", "Follow-Up Discussion")
- `README.md`
- `src/MortgageApplications.Api/requests.http`
- `docs/adr/0001-affordability-assessment-model.md` to `0004-authentication-and-authorisation.md`
- `docs/layers/PRODUCTION-LAYERS.md`

## Orchestration used
- **Skill:** `/phase 11 presentation` (`.claude/skills/phase/SKILL.md`).
- **Subagents (in Google Antigravity IDE · Gemini 4 Argon):**
  - `security-auditor` (`P11-03`): ran Gitleaks (`188` commits + working tree), Semgrep (`p/csharp` + `p/secrets`), and `dotnet list package --vulnerable`, and verified no secrets or leftover rehearsal `.db` files in the working tree (**PASS**, 0 findings).
  - `dotnet-reviewer` (`P11-04`): fresh-context review of `docs/presentation/DEMO-SCRIPT.md`, `docs/presentation/SLIDES.md`, and `README.md` against the codebase (5 findings `R1`–`R5`, all fixed).
  - `spec-checker` (`P11-05`): verified all 12 Phase 11 requirements (`R43`, `R45`–`R55`), bringing the final traceability table to **`55 met · 0 partial · 0 missing`**.
- **Hooks that fired:** `.githooks/pre-commit` (Gitleaks on every commit).

## Prompts

### P11-01 · human→agent (Antigravity · Gemini 4 Argon) · 2026-11-16
- **Prompt (verbatim):**
  ```
  Approved
  ```
- **Purpose:** Approve Phase 10 Checkpoint B (#20) and proceed to Phase 11 (`phase-11-presentation`) Checkpoint A plan.
- **Outcome:** PR #20 squash-merged (`dc539df`); `phase-11-presentation` branch and Phase 11 plan created; stopped at Checkpoint A for approval.

### P11-02 · human→agent (Antigravity · Gemini 4 Argon) · 2026-11-16
- **Prompt (verbatim):**
  ```
  Approved
  ```
- **Purpose:** Checkpoint A approval for Phase 11 (`P11-D1`–`P11-D3` Option A).
- **Outcome:** Ran the two timed fresh-clone rehearsals (`50.09 s` CLI + full `verify.sh`; `13.33 s` Docker Compose), wrote `docs/presentation/DEMO-SCRIPT.md` and `docs/presentation/SLIDES.md` (`495b0f3`), ran `./scripts/verify.sh`, and ran the three Phase 11 gate subagents (`P11-03`–`P11-05`).

### P11-03 · agent→subagent (`security-auditor` in Antigravity · Gemini 4 Argon) · 2026-11-16
- **Prompt (verbatim):**
  ```
  Security audit of Phase 11 (Fresh-clone rehearsal + presentation) in /Users/ale_ssd/Developer/Mortgage-Application-API-test, branch phase-11-presentation. Read only: do not edit files.

  Read:
  - `docs/presentation/DEMO-SCRIPT.md`
  - `docs/presentation/SLIDES.md`
  - `README.md`
  - `docs/progress/phase-11-presentation.md`

  Run:
  1. `gitleaks git --no-banner --redact -v` and `gitleaks dir --no-banner --redact .`
  2. `semgrep scan --config p/csharp --config p/secrets --error --quiet`
  3. `dotnet list MortgageApplications.slnx package --vulnerable --include-transitive`

  Also verify:
  - No real secrets, tokens, or private keys appear in `docs/presentation/DEMO-SCRIPT.md` or `docs/presentation/SLIDES.md`.
  - No temporary rehearsal files or SQLite `.db` files were accidentally left in the repository working tree (`git status -s`).

  Output a concise security gate verdict (PASS/FAIL) and any findings.
  ```
- **Purpose:** Security gate for Phase 11.
- **Outcome:** **PASS** (0 leaks in 188 commits + working tree, 0 Semgrep findings, 0 vulnerable packages, 0 leftover rehearsal files).

### P11-04 · agent→subagent (`dotnet-reviewer` in Antigravity · Gemini 4 Argon) · 2026-11-16
- **Prompt (verbatim):**
  ```
  Fresh-context diff review of Phase 11 (Fresh-clone rehearsal + presentation) in /Users/ale_ssd/Developer/Mortgage-Application-API-test, branch phase-11-presentation. Read only: do not edit files, do not build or run tests.

  Get the diff with `git diff main...HEAD`. Read `docs/presentation/DEMO-SCRIPT.md`, `docs/presentation/SLIDES.md`, and `README.md` in full, and cross-check every file link, relative path, route, status code, formula, check limit, test count, and architectural claim against the codebase and `docs/spec/ORIGINAL_SPEC.md` ("Follow-Up Discussion").

  Specifically check:
  1. Every Markdown relative link in `docs/presentation/DEMO-SCRIPT.md` and `docs/presentation/SLIDES.md` resolves to an existing file on disk.
  2. `docs/presentation/SLIDES.md` has at most 3 slides (`R49`), is brief and conversation-starting (`R51`), and covers all 4 topics in `ORIGINAL_SPEC.md:149-152` (`R52`–`R55`).
  3. `docs/presentation/DEMO-SCRIPT.md` places the slides after the demo (`R50`), keeps total session time <= 30 minutes (`R50`), and matches the actual API behaviour.

  Output only a list, most severe first, one finding per line:
  `file:line | severity (critical/high/medium/low) | why it is wrong | concrete fix`
  If you find nothing real, output `No findings.` and the list of files you reviewed. State plainly that you did not build or run tests.
  ```
- **Purpose:** Fresh-context review of `DEMO-SCRIPT.md`, `SLIDES.md`, and `README.md`.
- **Outcome:** 5 findings (`R1`–`R5`), all fixed in `DEMO-SCRIPT.md` and `SLIDES.md`.

### P11-05 · agent→subagent (`spec-checker` in Antigravity · Gemini 4 Argon) · 2026-11-16
- **Prompt (verbatim):**
  ```
  Requirement traceability check at the end of Phase 11 (Fresh-clone rehearsal + presentation, final phase) in /Users/ale_ssd/Developer/Mortgage-Application-API-test, branch phase-11-presentation. Read only: do not edit files.

  Read:
  - `docs/presentation/DEMO-SCRIPT.md`
  - `docs/presentation/SLIDES.md`
  - `README.md`
  - `docs/spec/ORIGINAL_SPEC.md`
  - `docs/PROGRESS.md` (the R1-R55 traceability table)
  - `docs/progress/phase-11-presentation.md`

  Do:
  1. Map every Phase 11 requirement (`R43`, `R45`, `R46`, `R47`, `R48`, `R49`, `R50`, `R51`, `R52`, `R53`, `R54`, `R55`) to the exact `file:line` and rehearsal evidence that meet it.
  2. State how the R1–R55 traceability table in `docs/PROGRESS.md` changes after Phase 11 (which rows move to `met` and the final overall summary counts of `met / partial / missing` out of 55).
  3. Output a Markdown table for `R43` and `R45`–`R55` and at most 5 short notes.
  ```
- **Purpose:** Final requirement traceability check at the end of Phase 11.
- **Outcome:** All 12 Phase 11 requirements (`R43`, `R45`–`R55`) move to **met**, bringing final totals to **`55 met · 0 partial · 0 missing`**.

### P11-06 · human→agent (Antigravity · Gemini 4 Argon) · 2026-11-16
- **Prompt (verbatim):**
  ```
  Approved
  ```
- **Purpose:** Checkpoint B approval for PR #21 (`phase-11-presentation`, final phase).
- **Outcome:** PR #21 approved and squash-merged into `main`; annotated tag `v1.0.0` created and pushed.

## What was built (files)

- Two timed fresh-clone rehearsals executed and recorded below (`50.09 s` `.NET 10` CLI + full `verify.sh`, `13.33 s` Docker Compose).
- Personal presenter runbook (`DEMO-SCRIPT.md`) and 3-slide presentation deck (`SLIDES.md`) authored and stored locally outside the shared GitHub repository.
- `docs/progress/phase-11-presentation.md`, `docs/PROGRESS.md`, `docs/ai/PROMPTS.md`, and `docs/ai/DESIGN-JOURNAL.md`.

## Fresh-Clone Rehearsal Results (`R45`, `R46`)

Both rehearsals cloned `https://github.com/Alelentini2001/Mortgage-Application-API-test.git` into clean temporary folders outside the workspace and executed the 8-step live demo (`401` anonymous, `POST /dev/token` for `alice` and `bob`, E1 `201` → `GET 200` → `POST .../assessments 202` → poll `200 Approved`, E2 `Declined`, `400` validation on `termYears: 50`, and `404` cross-user isolation):

| Step | Rehearsal 1: `.NET 10` CLI + Full `verify.sh` | Rehearsal 2: `docker compose up -d --build` |
|---|---|---|
| `git clone` | `1.08 s` | `1.02 s` |
| `./scripts/verify.sh` (full, 586 tests + 113 hook cases + format) or `docker compose up -d --build` | `44.14 s` | `11.00 s` |
| Startup to `GET /health` → `200 OK` | `4.49 s` | `0.91 s` |
| 8-step live API demo | `0.39 s` | `0.40 s` |
| **Total wall-clock time** | **`50.09 s`** | **`13.33 s`** |

## Tests

`./scripts/verify.sh` (full) passed on `phase-11-presentation`:
- `0` warnings, `0` errors
- **586 tests** (`275` unit + `311` integration) passed (`19.5 s`)
- **113 hook cases** passed (`0` failed)
- `dotnet format --verify-no-changes` clean

## Security gate

`security-auditor` (`P11-03`, Antigravity IDE · Gemini 4 Argon). **Verdict: PASS.**
- `gitleaks git --no-banner --redact -v` (`188` commits) and `gitleaks dir --no-banner --redact .`: `0` leaks.
- `semgrep scan --config p/csharp --config p/secrets --error --quiet`: `0` findings.
- `dotnet list MortgageApplications.slnx package --vulnerable --include-transitive`: `0` vulnerable packages.
- Working tree & rehearsal cleanup check: `0` tracked `.db` files, `0` leftover temporary rehearsal directories.

## Review gate

1. **`dotnet-reviewer` (`P11-04`, Antigravity IDE · Gemini 4 Argon, read-only):**

| # | Finding | Severity | Status |
|---|---|---|---|
| R1 | `DEMO-SCRIPT.md:113`: broken link to `src/MortgageApplications.Core/Applications/ApplicationInputRules.cs` (`ApplicationInputRules.cs` lives in `src/MortgageApplications.Core/Affordability/`). | Medium | Fixed: updated path and link to `src/MortgageApplications.Core/Affordability/ApplicationInputRules.cs`. |
| R2 | `SLIDES.md:24`: listed `ICurrentUser` inside `Core` ports (`ICurrentUser` lives in `MortgageApplications.Api.Identity`; `Core` receives `string ownerId`). | Low | Fixed: clarified that `ICurrentUser` resolves `sub` at the `Api` boundary. |
| R3 | `SLIDES.md:39`: wrote `ApplicationInputs` instead of `AffordabilityInputs` in the calculator signature. | Low | Fixed: updated to `AffordabilityInputs`. |
| R4 | `DEMO-SCRIPT.md:83`: Step 6 polled `GET .../assessments/$ASSESS1` without `sleep 0.2` (unlike Step 7). | Low | Fixed: added `sleep 0.2` before polling in Step 6. |
| R5 | `DEMO-SCRIPT.md:109`: wrote "Open these 6 files in order" above a 5-group list linking 13 files. | Low | Fixed: updated to "Walk through these 5 groups in order". |

2. **`spec-checker` (`P11-05`, Antigravity IDE · Gemini 4 Argon, read-only):**
   - `R43` and `R45`–`R55` all move to **met**, bringing the final traceability count to **`55 met · 0 partial · 0 missing`** (`100%` of atomic requirements met).

## AI output corrected or rejected (REAL cases only)

- **Broken relative link and two type/namespace imprecisions in the first draft (`495b0f3`) caught by `dotnet-reviewer` (`P11-04`):**
  1. `DEMO-SCRIPT.md:113` linked `ApplicationInputRules.cs` under `Core/Applications/` instead of `Core/Affordability/`.
  2. `SLIDES.md:24` grouped `ICurrentUser` with the `Core` ports (`IMortgageApplicationRepository`, `IAssessmentQueue`) instead of noting it lives in `MortgageApplications.Api.Identity`, and `SLIDES.md:39` wrote `ApplicationInputs` instead of `AffordabilityInputs`.
  All were fixed before opening the PR.

## Optimisations considered

- N/A (rehearsal and presentation phase).

## Open issues

- None. After PR merge at Checkpoint B, create and push annotated tag `v1.0.0` on `main` (`P11-D3 A`).

## Time spent (approx.)

~15 min (`2026-11-16 03:47 UTC` to `04:03 UTC`, in Google Antigravity IDE · Gemini 4 Argon).

## Human review (Alessandro writes this; the agent leaves it empty)
