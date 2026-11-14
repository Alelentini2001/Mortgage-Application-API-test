# Phase 00: Harness + skeleton

Status: **Planning — awaiting Checkpoint A** · Branch: `phase-00-harness` (not created yet) · PR: — · Started: 2026-11-13 · Finished: —

## Goal

A lean, enforced agent harness (memory files, rules, subagents, skills, hooks, permissions) plus an empty .NET 10 solution that passes `scripts/verify.sh` locally and in CI. Hooks are proven with recorded evidence. Toolkit report written.

## A1. Toolkit report (checked 2026-11-13, read-only)

| Tool | Available? | Used for | Fallback if missing |
|---|---|---|---|
| .NET SDK | 7.0.306 + **11.0.100** (the brew `dotnet-sdk` cask installed 11, not 10; there's no `dotnet-sdk@10` cask). Runtimes: 7.0.9, 11.0.0. **.NET 10 SDK/runtime still missing** → decision D9 | build, test, format | `mcr.microsoft.com/dotnet/sdk:10.0` container |
| Docker | CLI 29.3.1 yes; **daemon not running** | Phase 4 image, compose, k6 | Alessandro starts Docker Desktop |
| gh CLI | 2.92.0, logged in (scopes: repo, read:org, gist) | PRs, CI runs | — |
| jq | 1.7.1 | hook scripts parse stdin JSON | — |
| git | 2.50.1 | — | — |
| Claude Code | 2.1.283 | harness | — |
| Semgrep | 1.185.0 (installed via brew, approved) | security gate (SAST) | `semgrep/semgrep` Docker image |
| Gitleaks | 8.30.1 (installed via brew, approved) | pre-commit + history scan | `zricethezav/gitleaks` Docker image |
| Microsoft Learn MCP | **not installed** (Alessandro chose not to add it) | verify .NET APIs | WebFetch of learn.microsoft.com, links recorded per phase |
| NuGet MCP | **not installed** | package/vuln checks | `dotnet list package --vulnerable --include-transitive`, `dotnet package search` |
| GitHub MCP | not installed | — | `gh` CLI |
| Context7 | not installed | — | WebFetch |
| dotnet/skills plugins, OWASP skill | **not installed** | reviewer/auditor preload | Reviewers carry their own checklists in the agent prompt; OWASP Top 10:2025 checklist written into `security-auditor` |
| Built-in `/code-review`, `/security-review` | yes (bundled skills) | review + security gates | — |
| MCP servers present | claude-mem, blender, meshy (unrelated); supabase (needs auth); taski (failing, 402) | none used by this project | — |

## Docs verified (Claude Code, 2026-11-13)

- Hooks — https://code.claude.com/docs/en/hooks
  - Shape `hooks → Event → [{matcher, hooks:[{type:"command", command}]}]` matches the prompt.
  - `SessionStart` matcher `compact` is valid; plain stdout is added to context.
  - `tool_input.file_path` is always absolute for Edit/Write; Bash uses `tool_input.command`.
  - Exit 2: PreToolUse blocks (stderr → Claude); PostToolUse cannot block; Stop continues the turn.
  - Stop input has **`stop_hook_active`** (true when already continuing because of a stop hook). Built-in cap: 8 consecutive continuations.
  - **Difference from prompt:** docs use `${CLAUDE_PROJECT_DIR}`; the prompt's `"$CLAUDE_PROJECT_DIR"` form also works in a shell command. I'll use the quoted form `"$CLAUDE_PROJECT_DIR"/...` so paths with spaces are safe.
- Sub-agents — https://code.claude.com/docs/en/sub-agents
  - All fields in the prompt exist: `name`, `description`, `tools`, `disallowedTools`, `model`, `permissionMode`, `maxTurns`, `skills`, `isolation` (exactly `worktree`), `memory`.
  - **Addition:** `effort` and `color` also exist. Not needed.
- Skills — https://code.claude.com/docs/en/skills
  - Live in `.claude/skills/<name>/SKILL.md`; `disable-model-invocation: true` confirmed; args via `$ARGUMENTS`, `$0`, `$1`, or named `arguments:`.
- Memory — https://code.claude.com/docs/en/memory
  - `@AGENTS.md` import is the documented way to share AGENTS.md.
  - Nested CLAUDE.md files load on demand when Claude touches a file in that folder.
  - Rules: `paths:` is a YAML list of quoted globs.
  - Target under 200 lines per file.
  - Root CLAUDE.md is re-read after `/compact`.
- Permissions — https://code.claude.com/docs/en/permissions
  - Deny always wins over allow.
  - Write `Bash(git push --force *)` (space-star form). `:*` is an equivalent trailing form.
  - Read rules use gitignore-style paths, e.g. `Read(./.env*)`, `Read(**/secrets/**)`.
  - **Difference from prompt:** a broad `Bash(dotnet *)` allow also allows `dotnet nuget push` etc. I'll keep it, as the prompt asks; hooks plus deny rules cover the dangerous cases.

## Plan (for approval)

**Step 0: bootstrap `main`.** The repo is empty, so a PR needs a base branch. Make one commit on `main` (README stub + `.gitignore`) and push it. Then `git switch -c phase-00-harness`.

**Step 1: skeleton.** All of this needs the .NET 10 SDK.
- `global.json`: SDK pin with `rollForward: latestFeature`, plus `"test": {"runner": "Microsoft.Testing.Platform"}`.
- `Directory.Build.props`: net10.0, nullable, `TreatWarningsAsErrors`, `AnalysisLevel latest-recommended`, `EnforceCodeStyleInBuild`.
- `Directory.Packages.props`: central package management, so versions live in one place.
- `.editorconfig`, `.gitignore`, `.dockerignore`.
- Solution `MortgageApplications.slnx`, the .NET 10 default format.
- Projects:
  - `src/MortgageApplications.Api`
  - `src/MortgageApplications.Core` (no ASP.NET/EF refs)
  - `src/MortgageApplications.Infrastructure`
  - `tests/MortgageApplications.UnitTests` (xunit.v3)
  - `tests/MortgageApplications.IntegrationTests` (xunit.v3 + Mvc.Testing)
- One placeholder test per test project, so the pipeline proves it runs tests.

**Step 2: memory files (A2).**
- `AGENTS.md`.
- `CLAUDE.md`: under 200 lines, starts with `@AGENTS.md`.
- Nested `CLAUDE.md` in `src/MortgageApplications.Core/`, `tests/` and `docs/`.
- `CLAUDE.local.md` (gitignored).
- `.claude/rules/`: `backend.md`, `tests.md`, `infra.md`, `security.md`.
- `docs/ai/proposed-user-claude-md.md`: a proposal only. Nothing is written to `~/.claude`.

**Step 3: subagents (A3).** `.claude/agents/`: `dotnet-reviewer` (opus), `spec-checker` (sonnet), `security-auditor` (opus), `test-designer`, `parallel-implementer` (`isolation: worktree`, bounded `maxTurns`).

**Step 4: skills (A4).** `/phase`, `/verify-all`, `/progress`, `/adr`, `/retro` (all `disable-model-invocation: true`), plus the `affordability-rules` knowledge skill, which points at `docs/spec/affordability-rules.md` (that file is written in Phase 1).

**Step 5: settings + hooks (A5).** `.claude/settings.json` holds permissions and the 5 hooks. The scripts in `.claude/hooks/*.sh` use `jq` and `set -euo pipefail`.

**Step 6: verification + CI (A6).**
- `scripts/verify.sh [--fast]`.
- Gitleaks pre-commit: native `.githooks/pre-commit` + `git config core.hooksPath .githooks` (no Python pre-commit framework).
- `.github/workflows/ci.yml`.
- `ai-code-review.yml` and `ai-security-review.yml`: least-privilege `permissions:`, same-repo PRs only. They skip cleanly until **you** add the `ANTHROPIC_API_KEY` secret. CI never depends on them.

**Step 7: evidence system (A8).**
- `docs/PROGRESS.md`
- `docs/adr/`
- `docs/layers/PRODUCTION-LAYERS.md` (skeleton; filled in Phase 1)
- `docs/ai/ORCHESTRATION.md` v1 (Mermaid)
- `docs/ai/PROMPTS.md`
- `docs/ai/DESIGN-JOURNAL.md`
- `docs/ai/AI-USAGE.md` (stub)

**Step 8: prove the harness** and paste the real output here:
- (a) Edit `.env.example` → blocked by `protect-files.sh`.
- (b) `git push --force` → blocked by `block-dangerous-bash.sh`.
- (c) Write a badly formatted `.cs` file → formatted by `format-on-edit.sh`.
- (d) Break the placeholder test → the Stop hook reports the failure → revert.
- (e) Show that `stop_hook_active` prevents a second block.

**Step 9: gates.**
- `scripts/verify.sh` (full).
- Gitleaks + Semgrep on the repo.
- `security-auditor` + `dotnet-reviewer` on the diff (fresh context).
- `/progress`, then push, PR, CI → **Checkpoint B**.

## Decisions awaiting Alessandro (Checkpoint A)

| # | Decision | Options | Recommendation |
|---|---|---|---|
| D1 | Empty repo bootstrap | (a) one initial commit pushed to `main`, then branch · (b) do everything on `main` | **(a)**: keeps one phase = one PR |
| D2 | `.env*` edit block | (a) block all `.env*` incl. `.env.example`, configure via appsettings + compose env · (b) exempt `*.example` | **(a)**: nothing in this project needs an env file |
| D3 | Dangerous-Bash scope | (a) block `git push --force`/`-f` (allow `--force-with-lease`), `--no-verify`, `rm -rf`, `git reset --hard`, `curl\|wget … \| sh` · (b) block every `--force` | **(a)**: (b) also blocks `dotnet new --force` |
| D4 | Format-on-edit | (a) `dotnet format whitespace --folder --include <file>` (fast, no build) and full `dotnet format --verify-no-changes` in `verify.sh` · (b) full `dotnet format --include` per edit (several seconds each) | **(a)** |
| D5 | Solution format | (a) `.slnx` (.NET 10 default) · (b) classic `.sln` | **(a)** |
| D6 | Package versions | (a) central `Directory.Packages.props` · (b) versions in each csproj | **(a)**: one place to audit |
| D7 | AI review workflows | (a) create in Phase 0, skip until the secret exists · (b) defer to Phase 8 | **(a)**: AI review visible on every PR from Phase 1 on |
| D9 | Getting .NET 10 (only SDK 11 installed; `net10.0` tests need the 10.x runtime) | (a) you run Microsoft's official .NET 10 SDK `.pkg` installer, side by side with 11, and `global.json` pins 10.0.x · (b) `brew install dotnet@10` formula (separate prefix, clashes with the cask's `dotnet` link) · (c) build/test only in the `sdk:10.0` container | **(a)** |
| D8 | Global setup clash | Caveman mode, graphify and claude-mem are active globally. Repo docs, commits and PRs stay in normal English; `docs/PROGRESS.md` is the only progress source of truth (not claude-mem work-state) | Confirm |

## Prerequisites from Alessandro

- [ ] **Add `docs/spec/ORIGINAL_SPEC.md`** (the PDF text). Not in the repo yet. Phase 0 can run without it; **Phase 1 is blocked until it exists.**
- [ ] Start Docker Desktop. Needed in Phase 4; also needed sooner if the brew SDK install fails.
- [ ] Later: `/install-github-app`, the `ANTHROPIC_API_KEY` repo secret, and branch protection on `main`.

## Decisions (option chosen · alternatives · reason · decided by)
_Filled after Checkpoint A._

## Orchestration used
- A1 + docs check: lead agent only (Bash, WebFetch). No subagents yet.

## Prompts
- See `docs/ai/PROMPTS.md` (created in Step 7; the master prompt is logged there verbatim as entry #1).

## What was built
_None yet._

## Tests
_None yet._

## Security gate
_Not run yet._

## Review gate
_Not run yet._

## AI output corrected or rejected
_None yet._

## Optimisations considered
_None yet._

## Open issues
- The brew cask installed .NET SDK 11.0.100, not 10. The .NET 10 SDK is still needed (D9).

## Time spent (approx.)
- A1 + docs + plan: ~25 min (including the folder-picker timeout).

## Human review (Alessandro writes this; the agent leaves it empty)
