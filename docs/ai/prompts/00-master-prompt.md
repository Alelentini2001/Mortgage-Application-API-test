# Master prompt v2 (verbatim, human→agent, 2026-11-13)

Sent by Alessandro as the opening instruction for this project, prefixed with: "Before proceeding show me your plan and the things you will do".

---

# Master prompt v2: agentic engineering (reusable harness + .NET technical test)

## How to use this (for you, Alessandro; not part of the prompt)

- **Part A (generic harness bootstrap)** is reusable on every project. **Part B** is this assessment; for future projects, replace Part B with that project's spec and phases. **Part C** is the retro, which promotes lessons into your personal rules and the playbook.
- This prompt is long on purpose, but it's **a one-time bootstrap**. It turns itself into small files (CLAUDE.md under 200 lines, path-scoped rules, on-demand skills, subagents, hooks). After Phase 0, sessions only load the lean harness. That's context engineering applied to the prompt itself.
- Steps:
  1. Create an empty public GitHub repo, clone it, and `cd` in.
  2. Install the toolkit: dotnet/skills plugins, Microsoft Learn MCP, `gh` CLI (logged in) or GitHub MCP, NuGet MCP, Semgrep, Gitleaks, the OWASP skill, `jq`, and `/install-github-app`.
  3. Copy the test PDF text into `docs/spec/ORIGINAL_SPEC.md` yourself.
  4. Start Claude Code with `claude --permission-mode plan`, paste everything below the line, and name the session (`/rename mortgage-api-test`).
  5. At every **CHECKPOINT**, read the diff and the phase file, write your notes in the "Human review" section, and reply `approved` or give corrections.
  6. Run `/clear` after each merged phase. The harness resumes from `docs/PROGRESS.md`.
- Rule for you: if you can't explain a merged line in the interview, it shouldn't have been merged.

---------------------------------------------------------------- PROMPT STARTS BELOW

# OPERATING MODE: AGENTIC ENGINEERING

You are my lead engineering agent. I'm Alessandro, the engineer of record. We practise **agentic engineering, not vibe coding**: you do most of the implementation, and I own the architecture, quality and correctness.

Principles (apply them to everything):
1. **Every task has a check you can run** (tests, build, linter, script). Show evidence (the command + its output), don't just assert success.
2. **Explore → plan → implement → commit.** Use plan mode for anything that touches several files or has an uncertain approach.
3. **Context is the budget.** Delegate research to subagents, scope every investigation, load skills only when needed, summarise long outputs, and keep durable state in files (`docs/PROGRESS.md`), not in the chat.
4. **Advice vs enforcement:** CLAUDE.md and rules are advice; hooks and permissions are enforcement. Anything that must always happen becomes a hook.
5. **The author never grades its own work.** Reviews run in fresh-context subagents. Reviewers flag only correctness, security and requirement gaps, not style preferences, so we don't over-engineer.
6. **Small units:** one phase = one branch = one PR, with conventional commits.
7. **I decide.** For any real choice, give 2-3 options with trade-offs and a recommendation, then stop for my answer. Log who decided.
8. **Test-first** wherever there's logic.
9. **Least power:** stay inside this repo; no real secrets; no production data; ask before adding packages, MCP servers, skills or permissions.
10. **Honest evidence only.** Never invent prompts, findings, test results, timings or "AI mistakes caught". If something wasn't done, write that it wasn't done.

Stop conditions: at every `CHECKPOINT`, wait for `approved`. If the same step fails 3 times, stop and ask. If you've been corrected twice on the same issue, propose that I `/clear` and restart the step with a better prompt.

====================================================================
# PART A: GENERIC HARNESS BOOTSTRAP (reusable for any project)
====================================================================

Run Part A as **Phase 0**. Verify every Claude Code feature against the official docs before configuring it (use WebFetch on `https://code.claude.com/docs/llms.txt` to find the right pages: memory, settings/permissions, hooks, sub-agents, skills). If a field or syntax differs from what's written here, **the docs win**. Note the difference in the phase file.

## A1. Toolkit check (report only, install nothing)
List what's available: plugins/skills, MCP servers (`claude mcp list`), CLIs (`dotnet --version`, `docker --version`, `gh auth status`, `jq --version`, `semgrep --version`, `gitleaks version`). Produce a table: tool → available? → used for → fallback if missing.

## A2. Memory files (instructions)

1. **`AGENTS.md`** (tool-agnostic, so Codex/Copilot read it too): project purpose in 3 lines, build/test/run commands, architecture boundaries, coding standards that differ from defaults, git etiquette, the definition of done, and the honesty rule.
2. **`CLAUDE.md`** (root, **under 200 lines**): starts with `@AGENTS.md`, then only Claude-specific content:
   - which skills, subagents and hooks exist, and when to use each (one line each)
   - "Verify .NET/ASP.NET APIs with the Microsoft Learn MCP before using them"
   - "Resume protocol: read docs/PROGRESS.md first"
   - a compaction instruction: "When compacting, always keep: the current phase and step, the list of modified files, the test commands and their last result, and open decisions awaiting Alessandro"
   - mark at most **one or two** lines as IMPORTANT.
3. **Nested `CLAUDE.md`** files (loaded only when Claude works in that folder), each 5-15 lines: `src/<Core project>/CLAUDE.md` (domain purity rules), `tests/CLAUDE.md` (test conventions), `docs/CLAUDE.md` (evidence and honesty rules, templates).
4. **`CLAUDE.local.md`**: a short placeholder for my personal notes. Add it to `.gitignore`.
5. **`.claude/rules/`**, path-scoped with `paths:` frontmatter, one topic per file:
   - `backend.md` → `paths: ["src/**/*.cs"]`
   - `tests.md` → `paths: ["tests/**/*.cs"]`
   - `infra.md` → `paths: ["Dockerfile", "docker-compose*.yml", ".github/**"]`
   - `security.md` → no `paths` (always loaded, max 15 lines: secrets, PII in logs, authZ, input validation)
6. **`docs/ai/proposed-user-claude-md.md`**: a *proposal* for my personal `~/.claude/CLAUDE.md` and `~/.claude/rules/` (habits that apply to all my projects). **Don't write to my home directory.** I'll copy what I want.

After writing them, run `/context` (or explain how I can) to confirm what loads, and report the total line counts.

## A3. Subagents (`.claude/agents/*.md`)
All are read-only unless stated. Use the documented frontmatter (`name`, `description`, `tools`, `disallowedTools`, `model`, `permissionMode`, `maxTurns`, `skills`, `isolation`, `memory`), checked against the docs.

| Agent | Purpose | Key settings |
|---|---|---|
| `dotnet-reviewer` | Fresh-context review of a diff. Priorities: 1) correctness of the maths and rounding; 2) security (authZ/IDOR, secrets, PII in logs); 3) framework usage (DI lifetimes, a scoped DbContext in singletons, validation, status codes, async/cancellation); 4) test gaps; 5) over-engineering. Output only `file:line, severity, why, fix`. **Flag only correctness, security and requirement gaps.** | `tools: Read, Grep, Glob, Bash`; `disallowedTools: Write, Edit`; `model: opus`; preload the .NET code-review skill if installed |
| `spec-checker` | Traceability: requirement → where met (file/test) → met / partial / missing | read-only, `model: sonnet` |
| `security-auditor` | Consolidates `/security-review`, Semgrep, Gitleaks, dependency checks and the OWASP Top 10:2025 checklist for the diff or the repo; separates real findings from false positives (with reasons) | read-only, `model: opus`, preload the OWASP skill if installed |
| `test-designer` | From the spec and the code, proposes test cases (boundaries, failure paths, security cases) as a list. **Doesn't write code**; I pick the cases | read-only |
| `parallel-implementer` | Implements one approved phase plan in isolation, for the parallel phases only | `isolation: worktree`; tools include Edit/Write/Bash; `maxTurns` bounded |

Log every subagent invocation's prompt verbatim in `docs/ai/PROMPTS.md`.

## A4. Skills (`.claude/skills/<name>/SKILL.md`): workflows load only when invoked
Workflows with side effects use `disable-model-invocation: true`, so they only run when I type them.

- `/phase <NN> <name>`: runs the **phase loop** (A7) for one phase, step by step, stopping at both checkpoints.
- `/verify-all`: runs `scripts/verify.sh`, the security gate and the review gates, and prints one summary table with evidence.
- `/progress`: updates `docs/PROGRESS.md` and the current phase file from git log, test results and gate outputs (real data only).
- `/adr <title>`: creates `docs/adr/NNNN-<title>.md` (context, options, decision, consequences, decided by).
- `/retro`: the Part C retrospective.
- A model-invocable knowledge skill for the domain (here: `affordability-rules`), with a short description that points to `docs/spec/affordability-rules.md`, so the rules load only when relevant.

## A5. Settings, permissions and hooks (`.claude/settings.json`, committed)
- **Permissions:** allow `dotnet *`, `git status/diff/log/add/commit/switch/branch/worktree *`, `gh pr *`, `gh run *`, `docker *`, `./scripts/verify.sh`. **Deny:** reading `.env*`, `**/secrets/**` and `*.pem`; `git push --force*`; `git reset --hard*`; `rm -rf *`; any `--no-verify`. Keep personal tweaks in `.claude/settings.local.json` (gitignored).
- **Hooks**, each as a script in `.claude/hooks/` called via `"$CLAUDE_PROJECT_DIR"/.claude/hooks/<script>.sh`, reading the JSON event from stdin with `jq`:
  - `PreToolUse` (matcher `Edit|Write`) → `protect-files.sh`: exit 2 with a reason if the target is `.env*`, `*.pem`, an existing migration file, or `.git/**`.
  - `PreToolUse` (matcher `Bash`) → `block-dangerous-bash.sh`: exit 2 on `rm -rf`, `--force`, `--no-verify`, `reset --hard`, or `curl … | sh`.
  - `PostToolUse` (matcher `Edit|Write`) → `format-on-edit.sh`: if the file is `*.cs`, run `dotnet format` scoped to that file; never fail the edit.
  - `Stop` → `verify-on-stop.sh`: only if tracked source files changed since the last commit, run `scripts/verify.sh --fast` (build + unit tests). Exit 2 with the failure summary if it fails. **Read the documented stop-hook input field that signals a repeated stop, and don't block twice in a row**, so it can't loop.
  - `SessionStart` (matcher `compact`) → `reinject-context.sh`: print the "Resume here" section of `docs/PROGRESS.md`, so context survives compaction.
- Shape (verify the exact syntax against the hooks docs, then adapt):
```json
{
  "hooks": {
    "PreToolUse": [
      { "matcher": "Edit|Write", "hooks": [{ "type": "command", "command": "\"$CLAUDE_PROJECT_DIR\"/.claude/hooks/protect-files.sh" }] },
      { "matcher": "Bash", "hooks": [{ "type": "command", "command": "\"$CLAUDE_PROJECT_DIR\"/.claude/hooks/block-dangerous-bash.sh" }] }
    ],
    "PostToolUse": [
      { "matcher": "Edit|Write", "hooks": [{ "type": "command", "command": "\"$CLAUDE_PROJECT_DIR\"/.claude/hooks/format-on-edit.sh" }] }
    ],
    "Stop": [ { "hooks": [{ "type": "command", "command": "\"$CLAUDE_PROJECT_DIR\"/.claude/hooks/verify-on-stop.sh" }] } ],
    "SessionStart": [ { "matcher": "compact", "hooks": [{ "type": "command", "command": "\"$CLAUDE_PROJECT_DIR\"/.claude/hooks/reinject-context.sh" }] } ]
  }
}
```
- **Test the harness and record the evidence:** try to edit `.env.example`, which should be blocked; run a blocked Bash command; edit a `.cs` file and confirm it was formatted; break a test and confirm the Stop hook catches it, then revert. Paste the outputs into the phase file.

## A6. Single verification command + CI + security tooling
- `scripts/verify.sh [--fast]`: restore, build (warnings as errors), unit tests (`--fast`), or all tests + `dotnet format --verify-no-changes` (full). Clear pass/fail output, non-zero exit on failure.
- Gitleaks pre-commit hook (or documented install steps if Gitleaks is missing).
- `.github/workflows/ci.yml`: runs `scripts/verify.sh` (+ `docker build` later) on push and PR.
- **Separate** workflow files for the AI code review (`anthropics/claude-code-action` + code-review plugin) and the AI security review (`anthropics/claude-code-security-review`), with least-privilege `permissions:` and same-repo PRs only. **Ask me to add the API-key secret; never create it yourself.** CI must not depend on those secrets.

## A7. The phase loop (Definition of Done), used by `/phase`
1. **Resume:** read `docs/PROGRESS.md` and the last phase file. Say in 3 lines where we are.
2. **Explore** with subagents (scoped), then **plan** into `docs/progress/phase-NN-<name>.md`. List the decisions that need my choice. → **CHECKPOINT A**
3. `git switch -c phase-NN-<name>` (or a worktree for the approved parallel phases).
4. **Docs check:** verify every API you'll use (Learn MCP / Context7), and record the links.
5. **Test-first:** `test-designer` proposes cases → I approve → failing tests → implement → green. Commit small.
6. `scripts/verify.sh` (full). Show the output.
7. **Security gate:** `/security-review`, Semgrep, Gitleaks, the `security-auditor`. Fix or justify each finding.
8. **Review gate:** `dotnet-reviewer` on the diff (plus `/code-review` if available), then the `spec-checker`. Fix real gaps only.
9. **Evidence:** complete the phase file and append to `PROMPTS.md`, `DESIGN-JOURNAL.md` and the ADRs (`/progress`, `/adr`).
10. **PR:** push, open a PR (body = phase summary), wait for CI and the AI reviews, resolve their comments, then summarise for me. → **CHECKPOINT B** → squash-merge → update `PROGRESS.md` → tell me to `/clear`.

## A8. Evidence system (created in Phase 0)
```
docs/
├─ PROGRESS.md                  # phase table (status, branch, PR, tests, date) + "Resume here" + requirement traceability
├─ progress/phase-NN-<name>.md  # one per phase (template below)
├─ spec/ORIGINAL_SPEC.md        # provided by Alessandro
├─ adr/NNNN-<title>.md
├─ layers/PRODUCTION-LAYERS.md  # the production-layers checklist for this project (A9)
└─ ai/
   ├─ ORCHESTRATION.md          # how agents, skills, MCP servers, hooks and gates work together (Mermaid diagram) + what ran in parallel
   ├─ PROMPTS.md                # verbatim prompts: human→agent and agent→subagent, with purpose and outcome
   ├─ DESIGN-JOURNAL.md         # the thinking: ideas, options, rejected ideas, who decided, what changed and why
   ├─ AI-USAGE.md               # 1-2 page reviewer summary
   └─ proposed-user-claude-md.md
```
Phase file template:
```markdown
# Phase NN: <name>
Status · Branch · PR · Started/Finished
## Goal
## Plan (approved at Checkpoint A)
## Decisions (option chosen · alternatives · reason · decided by) → ADR links
## Docs verified (links)
## Orchestration used (skills · MCP servers · subagents · hooks that fired) and what each contributed
## Prompts (links to PROMPTS.md entries)
## What was built (files)
## Tests (added / total / passing; what each new test proves)
## Security gate (tool → finding → fix / false positive + reason)
## Review gate (reviewer + PR AI review → resolution)
## AI output corrected or rejected (REAL cases only)
## Optimisations considered (measured? applied? effect)
## Open issues
## Time spent (approx.)
## Human review (Alessandro writes this; the agent leaves it empty)
```

## A9. Production-layers checklist (fill in during the planning phase)
For each layer, record: applies? → decision → how it's enforced (rule / test / hook / CI) → phase.
Frontend (minified builds, private source maps, no secrets in the bundle) · Database (versioned migrations, tenant scoping / RLS, indexes, backups) · Auth & permissions (real IdP in production, roles, least privilege) · Version control (PRs, protected main, no secrets in history) · APIs (validation, ProblemDetails, OpenAPI, idempotency) · Hosting/deploy (containers, env-scoped config, build-once-promote, rollback, health checks) · Security (OWASP, dependency and secret scanning) · Rate limiting · Caching (only where measured) · Scaling (stateless tier, durable queues, the first bottleneck) · Observability (logs, traces, metrics, no PII) · Data/compliance (GDPR retention, audit) · Cost · AI features (evals, guardrails, human approval, prompt versioning).
Mark layers that don't apply as `N/A + reason`. Never skip one silently.

## A10. Parallelism rules (generic)
Only parallelise phases with **no shared files**, each in its own worktree and branch (via `parallel-implementer` with `isolation: worktree`). Merge one at a time, rebase the next, and re-run all gates. Never run two agents on the same branch. Record what ran in parallel and why it was safe in `ORCHESTRATION.md`.

====================================================================
# PART B: THIS PROJECT (.NET technical test)
====================================================================

## B1. Assignment summary (source of truth: docs/spec/ORIGINAL_SPEC.md)
An **ASP.NET 10** backend exposing a **JSON API** for mortgage applications:
1. **Create** an application (annual income, monthly expenses, deposit amount, property value, term in years), with validation and persistence.
2. **Retrieve** an application.
3. **Assess affordability:** LTV, DTI, approved/declined decision, max borrowing estimate, short explanation. Simple, reasonable, clearly explained logic with stated assumptions.
- Expectations: good use of the framework, clear models and relationships, validation, organised business logic, clean code. **Tests** for the business logic and the critical validation. **Backend only, no UI.**
- Preferred extras: **background processing for assessments, authentication, Docker, logging/instrumentation.**
- README: setup, run, tests, design decisions, plus **Design & Reflection** (2-3 key decisions; the first change for high load; what was kept simple or left out; next steps).
- Follow-up: fresh clone → build → run → demo → code walkthrough; **max 3 slides**; 30 minutes.
- Recruiter: **use AI to code AND to review**, visibly.

## B2. Project engineering rules (go into AGENTS.md / rules)
- `net10.0`, nullable on, warnings as errors, analyzers on, `Directory.Build.props`, `global.json` (SDK pin + `"test": { "runner": "Microsoft.Testing.Platform" }`).
- **Money and percentages are `decimal`**; round only at the end with `MidpointRounding.AwayFromZero` (2dp).
- Projects: `src/MortgageApplications.Api` (HTTP, auth, hosting, worker), `src/MortgageApplications.Core` (domain + calculator + use cases; **no ASP.NET/EF references**), `src/MortgageApplications.Infrastructure` (EF Core SQLite), `tests/MortgageApplications.UnitTests`, `tests/MortgageApplications.IntegrationTests`.
- Minimal APIs with `MapGroup`, `TypedResults`, **public** request/response DTO records in the Api project (never entities).
- Validation (400 `ValidationProblemDetails`) is separate from the affordability decision (Declined is a valid result).
- **Never log income, expenses, deposit, property value or personal data.**
- Approved packages: `Microsoft.AspNetCore.OpenApi`, `Scalar.AspNetCore`, `Microsoft.EntityFrameworkCore.Sqlite`, `Microsoft.EntityFrameworkCore.Design`, `Microsoft.AspNetCore.Authentication.JwtBearer`, `OpenTelemetry.Extensions.Hosting`, `OpenTelemetry.Instrumentation.AspNetCore`, `OpenTelemetry.Instrumentation.Http`, `OpenTelemetry.Exporter.OpenTelemetryProtocol`, `xunit.v3`, `Microsoft.AspNetCore.Mvc.Testing`. Anything else: ask, with a reason.

## B3. Verified technical notes (re-verify with the Learn MCP)
- `builder.Services.AddValidation()` + DataAnnotations + `IValidatableObject`. Request types must be **public**. Add an integration test proving that an invalid request returns 400.
- `AddOpenApi()`/`MapOpenApi()` (OpenAPI 3.1 by default) + `MapScalarApiReference()`. Bearer scheme via a document transformer.
- xUnit v3 on Microsoft.Testing.Platform; xUnit v3 filters use `--filter-class` / `--filter-method`. `[InlineData]` can't hold `decimal`; use `TheoryData<>` / `[MemberData]`.
- OpenTelemetry: `AddOpenTelemetry().WithTracing().WithMetrics().WithLogging().UseOtlpExporter()`; endpoint from `OTEL_EXPORTER_OTLP_ENDPOINT`.
- Aspire dashboard (dev only): `mcr.microsoft.com/aspire/dashboard`, UI 18888, OTLP `4317→18889`; the login token is in the container logs.
- A `BackgroundService` creates a **DI scope per work item** (scoped DbContext).

## B4. Affordability rules (the starting point for Phase 1; I may change them)
- Loan = PropertyValue − Deposit; LTV = Loan / PropertyValue × 100.
- Stressed payment at **6%** (r = 0.06/12, n = term × 12): Payment = Loan × r / (1 − (1 + r)^−n).
- DTI = (MonthlyExpenses + Payment) / (AnnualIncome / 12) × 100.
- MaxBorrowing = min(4.5 × AnnualIncome, (0.45 × AnnualIncome/12 − MonthlyExpenses) × (1 − (1 + r)^−n) / r), floored at 0.
- **Approved** only if LTV ≤ 90%, DTI ≤ 45% and Loan ≤ MaxBorrowing. The explanation lists every check. Thresholds live in `AffordabilityOptions` and are stamped on each assessment as `RulesVersion`.
- Assumptions: single applicant, GBP, repayment mortgage, gross income, expenses exclude the new mortgage, no credit score or fees.
- Worked examples (re-check with the implementation's rounding): 85,000/900/60,000/300,000/30y → LTV 80.00%, payment ≈ 1,438.92, DTI ≈ 33.02%, max ≈ 381,535.82 → **Approved** · 85,000/900/20,000/300,000/30y → LTV ≈ 93.33% → **Declined** · 45,000/1,500/60,000/300,000/25y → DTI ≈ 81.24%, max ≈ 29,101.29 → **Declined**.

## B5. Phases (each runs through the A7 loop)
- **Phase 0: harness + skeleton (~1 h).** All of Part A, plus the solution and projects, `global.json`, `Directory.Build.props`, `.editorconfig`, `.gitignore`, `.dockerignore`, `scripts/verify.sh`, CI, evidence docs, `ORCHESTRATION.md` v1 (Mermaid), and the harness test evidence. Done when the empty solution passes verify and CI, the hooks are proven, and the toolkit report is written.
- **Phase 1: spec + production layers (~30 min).** `docs/spec/affordability-rules.md` (from B4) + `docs/layers/PRODUCTION-LAYERS.md` filled in for this API (frontend N/A; DB: SQLite now, Azure SQL later; auth: JWT dev / Entra ID production; rate limiting, caching and scaling decisions). The `dotnet-reviewer` **critiques the spec before any code**; I decide the changes.
- **Phase 2: core domain + calculator, TDD (~1.5 h).** Entities (immutable application; assessment with status, metrics, decision, explanation, RulesVersion, timestamps), `AffordabilityOptions`, a pure decimal `AffordabilityCalculator` (no `double`, or a documented justification) that evaluates **all** rules, and a full explanation. The `test-designer` proposes cases (worked examples, limit-exact boundaries, zero expenses, expenses above the DTI cap → max 0, rounding); I approve them.
- **Phase 3: API + persistence + validation (~1.5 h). This is the minimum submittable product.** EF Core SQLite (`HasPrecision(18,2)`, enums as strings, one-to-many, indexes on OwnerId/ApplicationId, initial migration); DTOs; `AddValidation` + `IValidatableObject` (deposit < property, term 5-40, positive money, ≤ 2dp, sensible maximums); `AddProblemDetails`; an `IExceptionHandler` with a trace ID; OpenAPI + Scalar; `/health`. Endpoints: `POST /api/applications` (201 + Location), `GET /api/applications/{id}`, `POST /api/applications/{id}/assessments` (**synchronous** for now). Present the service/persistence abstraction trade-off for me to choose. Unit tests for the validation rules; integration tests with `WebApplicationFactory`; `requests.http`.
- **Phase 4: Docker (~45 min), parallel with Phase 6 allowed.** A multi-stage Dockerfile (sdk:10.0 → aspnet:10.0), non-root `USER $APP_UID`, port 8080; compose with the api + a SQLite volume + the aspire-dashboard; dev-only migrations on startup (ADR); `docker build` in CI. Done when `git clone && docker compose up --build` works in a clean folder.
- **Phase 5: logging + instrumentation (~45 min).** Structured `ILogger` + `[LoggerMessage]`; a **test asserting that no monetary values appear in logs**; OTel tracing + a custom ActivitySource span per assessment + a Meter (counter by decision, duration histogram); a trace ID in ProblemDetails; a DB health check.
- **Phase 6: background assessments (~1-1.5 h), parallel with Phase 4 allowed.** `IAssessmentQueue` + a bounded `Channel<Guid>`; `AssessmentWorker : BackgroundService` (scope per item, idempotent on Pending, Failed on exception, metrics); re-enqueue Pending items on startup; `POST …/assessments` → **202** + Location; `GET …/assessments/{assessmentId}`. Tests: the worker logic, end to end with polling and a timeout, the failure path, idempotency. ADR: the queue behind an interface → Azure Service Bus at scale.
- **Phase 7: authentication + authorisation (~1 h).** JWT bearer (issuer, audience, lifetime, signing key ≥ 256 bits from config); `RequireAuthorization()` on the group; a dev-only `POST /auth/token` (Development/Docker only; the README says production = Entra ID/OIDC); `OwnerId` from `sub`; another user's data → **404**; a Scalar bearer transformer. Tests: 401 without a token, 401 with an invalid/expired token, cross-user 404, a test auth handler.
- **Phase 8: security hardening (~45 min).** A full-repo `/security-review`, Semgrep, a Gitleaks history scan, `dotnet list package --vulnerable --include-transitive`, the NuGet MCP check, the OWASP Top 10:2025 mapping with evidence, and the `agentic-actions-auditor` on `.github/workflows/*`. Optional (with my approval): `AddRateLimiter` on POST endpoints, with a 429 test.
- **Phase 9: optimisation + quality (~45 min).** **Measure first** (k6 via Docker or a small script; record the numbers). Apply only justified changes (`AsNoTracking` + projections, indexes, SQLite WAL, channel capacity), with before/after numbers. A clean-up pass with no behaviour changes.
- **Phase 10: documentation (~45 min).** The README (Mermaid architecture, Docker + `dotnet run` paths, tokens, the demo with `requests.http`, the rules and assumptions, Design & Reflection drafted in my voice for me to edit); finalise `ORCHESTRATION.md`, `PROMPTS.md`, `DESIGN-JOURNAL.md`, `AI-USAGE.md`. The `spec-checker` must show every requirement as met.
- **Phase 11: fresh-clone rehearsal + presentation (~45 min).** Two timed clean runs (Docker, then `dotnet run`); `docs/presentation/SLIDES.md` (exactly 3 slides: solution and structure; assessment logic and assumptions; decisions, trade-offs, scaling and the AI workflow with one real caught issue); `docs/presentation/DEMO-SCRIPT.md` (a minute-by-minute plan + 15 likely questions with answers grounded in the repo).

Parallel plan for this project: phases 0-3 sequential → after Phase 3 is merged, **4 ∥ 6** in worktrees (they mustn't share files; if both need `Program.cs` or compose, serialise them) → merge 4, rebase 6, re-run the gates, merge 6 → 5, 7, 8, 9, 10, 11 sequential.

## B6. Explanation documents (Phase 10) and final briefing
- `DESIGN-JOURNAL.md`: chronological thinking (first reading of the spec and what the reviewers want; each idea → options → choice → who decided; what changed and why).
- `ORCHESTRATION.md`: a Mermaid diagram (me → lead agent → skills / MCP servers → subagents → hooks → gates → PR → CI + AI reviews → my approval → merge); for each tool and agent, why it was chosen and concrete contributions; what was **not** installed and why (lean context, skill supply-chain risk); guardrails; parallel runs.
- `PROMPTS.md`: every prompt, verbatim, by phase, tagged `human→agent` or `agent→subagent`, with purpose and outcome.
- `AI-USAGE.md` (1-2 pages): what the AI did versus what I decided, **real** corrections with links to the PRs, metrics (phases, PRs, tests, findings fixed), and how I'd run this workflow in a team (shared CLAUDE.md/AGENTS.md, required human + AI review, an approved tool list, data rules).
- **Final briefing** (your last message): the architecture in 5 sentences, 3 key decisions, the assessment logic, the scaling answer, trade-offs, security posture, the 5 key files and what to say about each, and the **weakest parts, honestly**, with how I should talk about them.

====================================================================
# PART C: RETRO AND LEARNING LOOP (run with `/retro` after Phase 11)
====================================================================
1. `docs/ai/RETRO.md`: what worked, what didn't, where the agents struggled, which hooks fired and saved us, which rules were ignored (a sign CLAUDE.md is too long), and the time per phase versus the estimate.
2. **Prune the harness:** propose cuts to CLAUDE.md/rules (anything Claude already does correctly without the rule → delete it, or turn it into a hook).
3. **Promote lessons:** write `docs/ai/playbook-updates.md` with (a) proposed additions to my personal `~/.claude/CLAUDE.md` / `~/.claude/rules/`, (b) proposed updates to my Agentic Engineering Playbook, and (c) any reusable skill or agent worth extracting into a personal plugin. **Propose only; I apply them.**

# START
Begin with **Phase 0 / A1 (toolkit check)** and **A7 step 1-2** (resume + plan). Then stop at **CHECKPOINT A**.
