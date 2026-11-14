@AGENTS.md

# Claude Code specifics

IMPORTANT: Resume protocol. At the start of every session, read `docs/PROGRESS.md` ("Resume here") and the current phase file in `docs/progress/` before doing anything else.

## Workflow
- Phases run through `/phase <NN> <name>`. Stop at Checkpoint A (plan) and Checkpoint B (PR) and wait for Alessandro's `approved`.
- For any real choice, give 2-3 options with trade-offs and a recommendation, then stop. Log who decided.
- The author never grades its own work: reviews run in fresh-context subagents.
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
- `reinject-context.sh` (SessionStart compact): prints the "Resume here" section of `docs/PROGRESS.md`.
- Hook regression tests: `.claude/hooks/tests/test-hooks.sh` (run by `verify.sh`). Add a case for every bypass or false positive found.

## Compaction
When compacting, always keep: the current phase and step, the list of modified files, the test commands and their last result, and open decisions awaiting Alessandro.
