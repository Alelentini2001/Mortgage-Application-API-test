@AGENTS.md

# Claude Code specifics

IMPORTANT: Resume protocol. At the start of every session, read `docs/PROGRESS.md` ("Resume here") and the current phase file in `docs/progress/` before doing anything else.

## Workflow
- Phases run through `/phase <NN> <name>`. Stop at Checkpoint A (plan) and Checkpoint B (PR) and wait for Alessandro's `approved`.
- For any real choice, give 2-3 options with trade-offs and a recommendation, then stop. Log who decided.
- The author never grades its own work: reviews run in fresh-context subagents, plus `/code-review` and `/security-review` locally on every PR (posted as a PR comment). No AI review runs in CI.
- Verify .NET / ASP.NET Core APIs against Microsoft Learn (learn.microsoft.com) before using them, and record the links in the phase file.
- Log every subagent prompt verbatim in `docs/ai/PROMPTS.md`.
- Repo docs, commits and PR text are written in plain, normal English.
- Progress lives in `docs/PROGRESS.md` and `docs/progress/`, not in chat or external memory tools.

## Skills (`.claude/skills/`)
- `/phase <NN> <name>`: runs the phase loop for one phase (explore, plan, test-first, gates, PR).
- `/verify-all`: verify script + security gate + review gates, one summary table.
- `/progress`: updates `docs/PROGRESS.md` and the phase file from real git/test/gate data.
- `/adr <title>`: new Architecture Decision Record in `docs/adr/`.
- `/retro`: end-of-project retrospective (Part C).
- `affordability-rules` (auto): loads the affordability formulas when working on the calculator or its tests.

## Subagents (`.claude/agents/`)
- `dotnet-reviewer`: fresh-context diff review (maths, security, framework usage, test gaps). Read-only.
- `spec-checker`: requirement → file/test traceability, met/partial/missing. Read-only.
- `security-auditor`: consolidates Gitleaks, Semgrep, dependency scan and OWASP checklist. Read-only.
- `test-designer`: proposes test cases as a list; never writes code.
- `parallel-implementer`: implements one approved plan in its own worktree (parallel phases only).

## Hooks (`.claude/hooks/`, enforced, not advice)
- `protect-files.sh` (PreToolUse Edit|Write): blocks `.env*`, `*.pem`, existing migrations, `.git/`.
- `block-dangerous-bash.sh` (PreToolUse Bash): blocks force push, `--no-verify`, `rm -rf`, `reset --hard`, pipe-to-shell.
- `format-on-edit.sh` (PostToolUse Edit|Write): whitespace-formats the edited `.cs` file.
- `verify-on-stop.sh` (Stop): runs `verify.sh --fast` when source changed; guarded by `stop_hook_active`.
- `session-context.sh` (SessionStart startup|resume|clear|fork): prints "Resume here" (capped), the branch, the CURRENT-WORK pointer, the usage status and the blocker if any.
- `reinject-context.sh` (SessionStart compact): shim that runs `session-context.sh` for compact; it also prints the PreCompact checkpoint.
- `subagent-context.sh` (SubagentStart): adds `scripts/context/brief.sh <agent_type>` as `additionalContext`.
- `pre-compact.sh` (PreCompact): writes `.claude/state/checkpoint.md`; prints "Also keep ..." instructions.
- `usage-stop.sh` (StopFailure): writes `.claude/state/blocker.md` (error type, reset time if known, resume sentence).
- `.claude/statusline.sh` (status line): model, context %, 5h and 7d usage; writes `.claude/state/usage.json`.
- Hook regression tests: `.claude/hooks/tests/test-hooks.sh` (run by `verify.sh`; sources `test-context.sh` for the H1 cases). Add a case for every bypass or false positive found.

## Context and usage awareness
- Context lives in `docs/ai/context/`: start from `CONTEXT-INDEX.md`; keep `CURRENT-WORK.md` current (phase, step, files in play, next action); write a handover in `handovers/` for each delegated task; run `scripts/context/index.sh` after changing decisions, open issues or carry-overs.
- Usage states come from the status-line snapshot `.claude/state/usage.json` (5-hour and 7-day windows; stale after 15 minutes) and from `.claude/state/blocker.md`:
  - **normal** (< 75%): work as usual.
  - **approaching** (≥ 75% in either window): fewer subagents and less context loading; finish the current step and update CURRENT-WORK.
  - **exhausted** (≥ 100%, or a `rate_limit` StopFailure): stop retrying, keep progress in the repo, and make sure CURRENT-WORK and the blocker's resume sentence say exactly what to do next.
  - **unknown** (no data, stale or unreadable): report it as unknown; never guess a number.
- Never switch or downgrade a model to save usage without verifying the change with Alessandro; never downgrade a security or review model.
- Not measurable from scripts: plan name, remaining allowance, per-model quotas as numbers, real billing.

## Compaction
When compacting, always keep: the current phase and step, the list of modified files, the test commands and their last result, and open decisions awaiting Alessandro.
