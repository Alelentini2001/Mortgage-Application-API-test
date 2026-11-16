# Phase 11: Fresh-clone rehearsal + presentation

Status: **At Checkpoint A (awaiting plan approval)** · Branch: `phase-11-presentation` · PR: — · Started: 2026-11-16 · Finished: —

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

## Plan (awaiting Checkpoint A approval)

1. Branch `phase-11-presentation` from `main` (done).
2. Stop at **Checkpoint A** for Alessandro's approval on `P11-D1`–`P11-D3`.
3. Execute two timed fresh-clone rehearsals in `/tmp` (Run 1: `dotnet` CLI + `./scripts/verify.sh` + live API calls; Run 2: `docker compose up -d --build` + live API calls + `docker compose down -v`), recording exact command outputs and wall-clock durations, and clean up `/tmp` directories.
4. Write `docs/presentation/DEMO-SCRIPT.md` and `docs/presentation/SLIDES.md` (and link them from `README.md`).
5. Run `./scripts/verify.sh` (full).
6. Run `security-auditor`, `dotnet-reviewer`, and `spec-checker` subagents; fix any real findings.
7. Update `docs/progress/phase-11-presentation.md`, `docs/PROGRESS.md` (final `R1`–`R55` traceability table), `docs/ai/PROMPTS.md`, and `docs/ai/DESIGN-JOURNAL.md`; push, open PR, wait for CI, and stop at **Checkpoint B**.

## Decisions

### P11-D1 · Location and format of the demo script and 3-slide deck
- **Options:**
  - **A (recommended):** Place both files under `docs/presentation/` (`docs/presentation/DEMO-SCRIPT.md` and `docs/presentation/SLIDES.md`, formatted as clean Markdown with `---` slide separators compatible with Marp / GitHub preview) and add links to both in `README.md` and `docs/PROGRESS.md`.
  - **B:** Place `DEMO-SCRIPT.md` at the repository root and `SLIDES.md` in `docs/presentation/SLIDES.md`.
- **Trade-offs:** Option A keeps the repository root clean (`README.md`, `AGENTS.md`, `CLAUDE.md`, `Dockerfile`, `compose*.yaml`, `MortgageApplications.slnx`) while grouping all follow-up interview materials together in `docs/presentation/`.
- **Decision:** Pending Alessandro's approval at Checkpoint A.

### P11-D2 · Fresh-clone rehearsal execution (`R45`, `R46`)
- **Options:**
  - **A (recommended):** Run two real, timed fresh clones of `https://github.com/Alelentini2001/Mortgage-Application-API-test.git` into temporary directories outside the workspace (`/tmp/mortgage-rehearsal-dotnet` and `/tmp/mortgage-rehearsal-docker`), execute the full build/test/run/curl flows, record the exact measured wall-clock times in `docs/progress/phase-11-presentation.md` and `docs/presentation/DEMO-SCRIPT.md`, and remove the `/tmp` directories when done.
  - **B:** Write the rehearsal steps without executing a fresh clone.
- **Trade-offs:** Option A satisfies `R45` and `R46` with real measured timings and proves that a fresh `git clone` works out of the box for both `dotnet run` and `docker compose up --build`.
- **Decision:** Pending Alessandro's approval at Checkpoint A.

### P11-D3 · Resolution of carried housekeeping items from `docs/PROGRESS.md`
- **Options:**
  - **A (recommended):**
    1. **Verbatim prompt logs (`security-auditor` S10 from Phase 1):** Keep `docs/ai/PROMPTS.md` verbatim (including `/Users/ale_ssd/...` workspace paths, which contain no secrets and are required by the `AGENTS.md` honesty rule).
    2. **Traceability table conventions (`spec-checker` from Phase 1):** Keep all 55 rows (`R1`–`R55`) in `docs/PROGRESS.md` with `met / partial / missing` status (where `R45`–`R55` become `met` in repo artifacts / `met (rehearsed, live session pending)` as appropriate).
    3. **Optional ADRs (`P2-D1/D2`, `P3-D1`):** Keep the 4 accepted ADRs (`0001`–`0004`); no additional retroactive ADRs needed since `README.md` and `DESIGN-JOURNAL.md` already document the Core and EF persistence choices.
    4. **Final submission tag (`R43`):** After the Phase 11 PR is squash-merged into `main`, create and push an annotated tag `v1.0.0` on `main`.
  - **B:** Redact `/Users/ale_ssd` in `docs/ai/PROMPTS.md` or skip tagging `v1.0.0`.
- **Trade-offs:** Option A preserves the verbatim integrity of the prompt log, closes all carried open questions in `docs/PROGRESS.md`, and provides a clean `v1.0.0` release tag for submission.
- **Decision:** Pending Alessandro's approval at Checkpoint A.

## Docs verified (links)
- `docs/spec/ORIGINAL_SPEC.md` ("Submission", "Time Expectation", "Follow-Up Discussion")
- `README.md`
- `src/MortgageApplications.Api/requests.http`
- `docs/adr/0001-affordability-assessment-model.md` to `0004-authentication-and-authorisation.md`
- `docs/layers/PRODUCTION-LAYERS.md`

## Orchestration used
- *Pending execution after Checkpoint A.*

## Prompts

### P11-01 · human→agent (Antigravity · Gemini 4 Argon) · 2026-11-16
- **Prompt (verbatim):**
  ```
  Approved
  ```
- **Purpose:** Approve Phase 10 Checkpoint B (#20) and proceed to Phase 11 (`phase-11-presentation`) Checkpoint A plan.
- **Outcome:** PR #20 squash-merged (`dc539df`); `phase-11-presentation` branch and Phase 11 plan created; stopped at Checkpoint A for approval.
