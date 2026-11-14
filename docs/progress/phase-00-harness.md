# Phase 00: Harness + skeleton

Status: **Merged** (squash `b20c823`); live hook proof added afterwards in [#2](https://github.com/Alelentini2001/Mortgage-Application-API-test/pull/2) (`chore/phase-00-live-proof`) · Branch: `phase-00-harness` · PR: [#1](https://github.com/Alelentini2001/Mortgage-Application-API-test/pull/1) · Started: 2026-11-13 · Finished: 2026-11-14

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

## Docs verified (.NET / tooling, 2026-11-14)

- xUnit v3 + MTP — https://xunit.net/docs/getting-started/v3/microsoft-testing-platform
  - `xunit.v3` 4.x defaults to MTP v2.
  - With the .NET 10 SDK, the `global.json` test runner replaces `TestingPlatformDotnetTestSupport`.
  - `Microsoft.NET.Test.Sdk` isn't needed when running only on MTP.
- `dotnet test` MTP mode — https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-with-dotnet-test
  - Exact `global.json` `"test": {"runner": "Microsoft.Testing.Platform"}`; requires MTP ≥ 1.7.
  - Use `--solution` / `--project` instead of positional paths.
- claude-code-security-review — https://github.com/anthropics/claude-code-security-review
  - Inputs `claude-api-key` and `comment-pr`; needs `pull-requests: write`.
  - "not hardened against prompt injection": trusted (same-repo) PRs only.
- claude-code-action — https://github.com/anthropics/claude-code-action (`docs/solutions.md`, "Automatic PR Code Review")
  - Inputs `anthropic_api_key`, `prompt`, `claude_args`, `track_progress`.
  - **Difference from the master prompt:** the docs don't show a "code-review plugin" input, so the workflow uses a review `prompt` instead.

## Plan (approved at Checkpoint A on 2026-11-14: "i installed the 10 pkg, you can proceed")

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

## Decisions proposed at Checkpoint A

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

## Decisions for Checkpoint B (raised by the security and review gates)

| # | Decision | Options | Recommendation |
|---|---|---|---|
| D10 | Narrow the auto-approve permissions in `.claude/settings.json` | (a) Replace `dotnet *` with `dotnet build/test/restore/format/run/clean/ef/new/sln *`. Replace `gh pr *` with `gh pr view/diff/checks/list/create *`. Replace `docker *` with `docker build *` + `docker compose *`. Add deny rules for `dotnet add package *`, `dotnet package *`, `dotnet tool *`, `dotnet nuget *`, `gh pr merge *`, `git branch -D *`, `git worktree remove --force *`. Remove the message-matching `Bash(* --no-verify *)` deny (the hook covers it precisely). · (b) Keep the broad allows; rely on hooks + prompts | **(a)**: removed allows fall back to a prompt, they aren't blocked |
| D11 | Protect the harness from the agent | (a) `protect-files.sh` blocks Edit/Write to `.claude/settings*.json`, `.claude/hooks/**`, `.githooks/**`, `.github/workflows/**`; harness changes then need you to approve a one-off edit · (b) leave editable; reviews + PR diff catch changes | **(a)** after Phase 0 merges, so later phases can't silently weaken guardrails |
| D12 | Server-side supply chain + secrets | (a) Add a SHA-pinned Gitleaks job to CI, `nuget.config` (nuget.org only + source mapping), `RestorePackagesWithLockFile` + `--locked-mode` in CI, and `dependabot.yml` for actions + NuGet · (b) defer to Phase 8 | **(a) in Phase 8**: keeps Phase 0 focused. Phase 8 is the security phase |

## Checkpoint B outcome (2026-11-14)

Alessandro: "Ok proceed, and about the AI reviews, they should be done here with my subscription!"

- **D10 applied.** The auto-approve list was narrowed to specific `dotnet`/`git`/`gh pr`/`docker` verbs (30 allow rules). Deny rules were added for `dotnet add package`/`package`/`tool`/`nuget`, `git branch -D` and `git worktree remove --force`. The message-matching `Bash(* --no-verify *)` deny was removed; the hook covers it precisely.
- **D11 applied as permission `ask` rules, not a hard block.** Edits to `.claude/settings*.json`, `.claude/hooks/**`, `.githooks/**`, `.github/workflows/**` and `scripts/verify.sh` now always prompt Alessandro. `gh pr merge` and `gh pr comment` also prompt. A hook hard-block would have left no way to approve legitimate harness changes.
- **D12 deferred to Phase 8.**
- **AI reviews moved from CI to local.** `ai-code-review.yml` and `ai-security-review.yml` were deleted. No API key or secret exists anywhere. Every PR now gets local `/code-review` + `/security-review` runs on Alessandro's subscription, with the summary posted as a PR comment (phase skill step 10). Earlier mentions of those workflows in this file are kept as history.

## Prerequisites from Alessandro

- [x] **Add `docs/spec/ORIGINAL_SPEC.md`** (the PDF text). Added on `main` in `67977f2`.
- [ ] Start Docker Desktop. Needed in Phase 4; also needed sooner if the brew SDK install fails.
- [ ] Later: branch protection on `main`. (The API-key secret is no longer needed: AI reviews run locally; see Checkpoint B outcome.)

## Decisions (option chosen · alternatives · reason · decided by)
All of D1–D9 were approved by Alessandro with the recommended option (a). See the table above and `docs/ai/DESIGN-JOURNAL.md`.
- D9 route: **Alessandro installed the official .NET 10 SDK pkg** himself (SDK 10.0.404, runtime 10.0.15).
- No ADR this phase. These are harness/tooling choices, not architecture; ADRs start in Phase 1.

## Orchestration used
- A1 + docs check: lead agent only (Bash, WebFetch on code.claude.com, xunit.net, learn.microsoft.com, github.com).
- Skills: none invoked; the project skills were created this phase.
- MCP servers: none used.
- Subagents: `dotnet-reviewer` and `security-auditor` roles. They ran as `general-purpose` (opus) told to load the repo's agent files, because the project agents weren't registered in the authoring session (P00-05, P00-06). They are registered in a session started in the repo (see "Live proof", step 4).
- Hooks that fired:
  - Gitleaks pre-commit fired on all 5 harness commits ("no leaks found" each time).
  - The Claude Code hooks were proven by direct script tests (evidence below). They did **not** fire live in the authoring session.
  - Later, in a session started in the repo, `protect-files.sh`, `block-dangerous-bash.sh` and `format-on-edit.sh` fired live. See "Live proof" below.

## Prompts
- `docs/ai/PROMPTS.md` entries P00-01 to P00-11 (P00-09 to P00-11 are the live-proof session). The master prompt is verbatim in `docs/ai/prompts/00-master-prompt.md`.

## What was built
- **Skeleton:**
  - `global.json` (SDK 10.0.100 + latestFeature roll-forward → 10.0.404; MTP test runner)
  - `Directory.Build.props`, `Directory.Packages.props`, `.editorconfig`, `.gitignore`, `.dockerignore`
  - `MortgageApplications.slnx` with 3 src + 2 test projects
  - `Program.cs` with `/health` and `public partial class Program`
- **Memory:**
  - `AGENTS.md` (46 lines), `CLAUDE.md` (39 lines)
  - Nested `CLAUDE.md` in Core (8), tests (8), docs (7)
  - `.claude/rules/` backend (14), tests (10), infra (12), security (8)
  - `CLAUDE.local.md` (gitignored)
  - `docs/ai/proposed-user-claude-md.md`
  - Lines loaded at session start: CLAUDE.md + AGENTS.md + security rule = 93. Path-scoped and nested files load on demand.
- **Subagents:** `.claude/agents/` dotnet-reviewer, spec-checker, security-auditor, test-designer, parallel-implementer.
- **Skills:** `.claude/skills/` phase, verify-all, progress, adr, retro, affordability-rules.
- **Enforcement:**
  - `.claude/settings.json` (13 allow, 8 deny, 5 hooks)
  - `.claude/hooks/*.sh` (5 scripts)
  - `.githooks/pre-commit` (Gitleaks; enabled with `git config core.hooksPath .githooks`)
- **Verification/CI:**
  - `scripts/verify.sh [--fast]`
  - `.github/workflows/ci.yml`
  - `ai-code-review.yml`, `ai-security-review.yml`
  - All actions are pinned by commit SHA.
- **Evidence:**
  - `docs/PROGRESS.md`
  - `docs/progress/_TEMPLATE.md`
  - `docs/adr/_TEMPLATE.md`
  - `docs/layers/PRODUCTION-LAYERS.md` (skeleton)
  - `docs/ai/` ORCHESTRATION (Mermaid v1), PROMPTS, DESIGN-JOURNAL, AI-USAGE (stub)

**How to check what loads (`/context`).** I can't run `/context` myself. Start `claude` in the repo, accept the trust dialog, and run `/context`. Expect `CLAUDE.md`, `AGENTS.md` (via `@AGENTS.md`) and `.claude/rules/security.md` under memory files. The other rules and nested `CLAUDE.md` files appear only after Claude touches matching files.

## Tests
- Added 2 / total 2 / passing 2. These are placeholders: `HarnessSmokeTests` in each test project proves the MTP pipeline discovers and runs tests. They're removed in Phase 2 and Phase 3.
- `./scripts/verify.sh` (full), 2026-11-14:
  ```
  ==> restore
  ==> build (Release, warnings as errors)
  ==> all tests
    total: 2
    failed: 0
    succeeded: 2
  ==> format check
  VERIFY PASSED (full)
  ```

## Harness proof (evidence)

**How it was tested.** Each hook script was fed the same JSON Claude Code sends on stdin (fields verified in the hooks docs).

**Why not live at first.** A live check in the authoring session wasn't possible: that session started outside the repo, so the repo's hooks weren't loaded. A deliberate `Write` to `.env.example` **went through** (the file was then deleted). A headless `claude -p` run in the repo also failed (`OAuth session expired`, workspace not trusted). The live proof was done later; see "Live proof" below.

### Live proof (2026-11-14, Claude Desktop Code session started in the repo)

Each step used the real tool. Outputs are quoted exactly. No blocked step was retried.

| # | Action (real tool) | Expected | Real result |
|---|---|---|---|
| 1 | `Write` `.env.example` with `X=1` | blocked by `protect-files.sh` | **Blocked, but by the permission deny rule `Read(**/.env*)`, not by the hook.** The tool returned a permission error, not a hook message, so the hook was not reached. **Not verified that no file was created:** `.env.*` is gitignored, so `git status` can't show it, and `ls -la .env.example` was denied by the same rule. Alessandro can check with `ls -la .env.example`. |
| 1b | `Write` `certs/harness-probe.key` (`*.key` is protected by the hook only; no deny rule covers it). Added with Alessandro's approval after step 1. | blocked by `protect-files.sh` | **Blocked by the hook.** No file or `certs/` folder was created. |
| 2 | `Bash` `rm -r -f ./harness-probe-dir` | blocked by `block-dangerous-bash.sh` | **Blocked by the hook.** |
| 3 | `Write` `src/MortgageApplications.Core/HarnessProbe.cs` with bad formatting | reformatted by `format-on-edit.sh` | **Reformatted by the hook.** The file was then deleted with a plain `rm`; `git status` was clean. |
| 4 | Agent types listed in the session | 5 project subagents registered | **All 5 registered:** `dotnet-reviewer`, `spec-checker`, `security-auditor`, `test-designer`, `parallel-implementer`. |

Step 1, tool error:
```
File is covered by a Read deny rule in your permission settings and cannot be written.
```

Step 1b, hook output:
```
PreToolUse:Write hook error: [command hook 3]: Blocked by protect-files.sh: secrets, key files and git internals are never edited by the agent (/Users/ale_ssd/Developer/Mortgage-Application-API-test/certs/harness-probe.key)
```

Step 2, hook output:
```
PreToolUse:Bash hook error: [command hook 4]: Blocked by block-dangerous-bash.sh: rm -rf is not allowed; delete specific paths or use 'dotnet clean'
```

Step 3, input written (tab and trailing spaces included):
```
namespace MortgageApplications.Core;
        public static class HarnessProbe
{
            public static int   Add( int a,int b )   =>a+b;   
	    }
```
Claude Code reported "PostToolUse hook modified ... HarnessProbe.cs after your edit". File after the hook (`cat -e`, `$` marks line ends):
```
namespace MortgageApplications.Core;$
$
public static class HarnessProbe$
{$
    public static int Add(int a, int b) => a + b;$
}$
```

Other live observations from the same session:
- Writing a file under `src/MortgageApplications.Core/` loaded `src/MortgageApplications.Core/CLAUDE.md`, `.claude/rules/backend.md` and the path-scoped `affordability-rules` skill on demand.
- `phase`, `verify-all`, `progress`, `adr` and `retro` don't appear in the agent's skill list. This is expected: they use `disable-model-invocation: true`, so only Alessandro can start them (for example by typing `/phase 01 spec`).
- Not proven live: `verify-on-stop.sh` (Stop) and `reinject-context.sh` (SessionStart on compact). Both are still covered only by the script tests in (d) and (e).

**(a) protect-files.sh**
```
exit=2 | {"file_path":".../.env.example"} | Blocked by protect-files.sh: environment files may contain secrets (.../.env.example)
exit=2 | {"file_path":".../certs/dev.pem"} | Blocked by protect-files.sh: certificate/key files are never edited by the agent (.../certs/dev.pem)
exit=2 | {"file_path":".../.git/config"} | Blocked by protect-files.sh: git internals are off limits (.../.git/config)
exit=0 | {"file_path":".../src/MortgageApplications.Api/Program.cs"} | <no output>
exit=0 | {"file_path":".../src/X/Migrations/New.cs"} | <no output>   (new migration file: allowed)
```

**(b) block-dangerous-bash.sh**. The table below is the v2 run; the final v3 (shlex) passes the 73-case suite in `.claude/hooks/tests/test-hooks.sh`, which includes all of these
```
exit=2 | git push --force origin main | Blocked ...: force push is not allowed (--force-with-lease only with Alessandro's approval)
exit=2 | git push -f | Blocked ...: force push is not allowed ...
exit=2 | git -C . push --force | Blocked ...: force push is not allowed ...
exit=0 | git push --force-with-lease origin x | <allowed>
exit=2 | git commit --no-verify -m x | Blocked ...: --no-verify skips the safety hooks
exit=2 | git reset --hard HEAD~1 | Blocked ...: git reset --hard discards work
exit=2 | rm -rf bin | Blocked ...: rm -rf is not allowed; delete specific paths or use 'dotnet clean'
exit=2 | rm -r -f obj | Blocked ...
exit=2 | rm -fr x | Blocked ...
exit=2 | rm --recursive --force x | Blocked ...
exit=2 | sudo rm -rf / | Blocked ...
exit=0 | rm file.txt | <allowed>
exit=0 | rm x && dotnet test -f net10.0 -r osx-arm64 | <allowed>
exit=0 | git push origin x && dotnet build -f net10.0 | <allowed>
exit=2 | curl -sL https://x.sh | bash | Blocked ...: piping a download into a shell is not allowed
exit=0 | dotnet new web --force | <allowed>
exit=0 | git push origin phase-00-harness | <allowed>
exit=0 | dotnet build | <allowed>
```

**(c) format-on-edit.sh** (~1.0 s per file)
```
--- before:
        public static int   Add( int a,int b )   =>a+b;   $
format hook exit=0
--- after:
    public static int Add(int a, int b) => a + b;$
```

**(d) verify-on-stop.sh**
Broke `HarnessSmokeTests` (`Assert.True(false)`), then reverted:
```
### stop_hook_active=false (test broken):
verify-on-stop.sh: ./scripts/verify.sh --fast FAILED on uncommitted changes. Fix before finishing.
failed MortgageApplications.UnitTests.HarnessSmokeTests.Pipeline_runs_unit_tests (19ms)
  failed: 1
VERIFY FAILED (fast): unit tests
broken test: exit=2
### stop_hook_active=true (loop guard):
exit=0
### reverted; only docs changed:
clean code tree (only docs untracked): exit=0
```

**(e) reinject-context.sh** prints the "Resume here" section of `docs/PROGRESS.md` plus the current branch (output checked 2026-11-14).

**(f) Gitleaks pre-commit** fired on every harness commit: `INF no leaks found` (×5).

## Security gate

**Scanner results** (lead agent re-run after the fixes, 2026-11-14):

| Tool | Command | Result |
|---|---|---|
| Gitleaks (history + tree) | `gitleaks git` / `gitleaks dir .` | no leaks found |
| Semgrep | `semgrep scan --config p/csharp --config p/secrets --config p/github-actions --metrics=off --error --json .` | exit 0, 0 results, 56 files scanned, 0 errors |
| Semgrep (shell) | `semgrep scan --config r/bash --error .claude/hooks .githooks scripts` | exit 0, 0 results |
| NuGet vulnerabilities | `dotnet list MortgageApplications.slnx package --vulnerable --include-transitive` | no vulnerable packages (5 projects) |
| Action pins | `git ls-remote` (auditor) | all 4 SHAs match their tags/branch |

**Security-auditor verdict: PASS** (no high or critical findings). Its findings and how each was handled:

| ID | Finding | Severity | Resolution |
|---|---|---|---|
| S1 | `git commit -n` (short form of `--no-verify`) and `git -c core.hooksPath=…` passed `block-dangerous-bash.sh`, so the Gitleaks pre-commit hook could be skipped | Medium | **Fixed** (the reviewer found the same issue, plus prefix forms; see Review gate). Covered by regression cases in `.claude/hooks/tests/test-hooks.sh`. |
| S2 | `Bash(docker *)` allow means `docker run -v /:/h` auto-approves host-wide file access, getting around the `.env`/`.pem` deny rules | Medium | **Decision for Alessandro (D10).** This changes permission settings. |
| S3 | `protect-files.sh` doesn't protect the harness itself (`.claude/settings*.json`, `.claude/hooks/`, `.githooks/`, workflows) | Low | **Decision for Alessandro (D11).** |
| S4 | `Bash(dotnet *)` also allows `dotnet nuget push` / `dotnet tool install --add-source` | Low | **Decision for Alessandro (D10).** |
| S5 | Secret scanning is client-side only (opt-in `core.hooksPath`); no Gitleaks step in CI | Low | **Proposed (D12):** add a pinned Gitleaks CI job. |
| S6 | No `packages.lock.json` / `nuget.config` source mapping / Dependabot | Low | **Proposed (D12):** lock files + `dependabot.yml`. |
| S7 | AI review actions read PR-author content (prompt injection) | Low, mitigated | Accepted. Same-repo guard, `pull_request` only (never `pull_request_target`), minimal permissions, `persist-credentials: false`. AI review stays advisory, never a required check. |
| S8 | `id-token: write` in `ai-code-review.yml` | Info | False positive: claude-code-action needs it for the OIDC → GitHub App token exchange. |
| S9 | `${{ }}` interpolation in workflows | Info | False positive: only `github.repository` and the PR number, neither attacker-controlled. |
| S10 | Hooks fail open if `jq` can't parse the input | Info | Accepted: Claude Code always sends valid JSON, and a missing `jq` already fails closed. |
| S11 | `AllowedHosts: *`, no HSTS/headers | Info | Deferred to Phase 3/8, when real endpoints exist. |
| S12 | Semgrep `p/bash` returns 404; semicolon-bodied `public partial class Program;` doesn't parse | Info | **Fixed.** Use `r/bash`; `Program` now uses `{ }` so Semgrep parses the whole file. |

## Review gate

**`dotnet-reviewer`** (fresh context, opus; prompt P00-05) returned 17 findings: 1 high, 8 medium, 8 low. `spec-checker` was **not run**, because `docs/spec/ORIGINAL_SPEC.md` doesn't exist yet. No PR AI review yet (no API key secret).

**Root-cause fix.** The v1/v2 Bash hook split shell text on whitespace, which can't handle quoting, wrappers or nesting. The core is now a Python `shlex` tokenizer (`python3 -I`), with the script path unchanged so `settings.json` needs no edit. A regression suite, `.claude/hooks/tests/test-hooks.sh` (73 cases), now runs inside `verify.sh` full mode and therefore in CI.

| Finding (reviewer) | Severity | Resolution |
|---|---|---|
| `git commit -n`, `--no-verif` (prefix), `git -c core.hooksPath=…` bypass the pre-commit Gitleaks hook | High | **Fixed.** Blocked `-n` clusters, any `--no-v…` prefix, hooksPath overrides, and `git config core.hooksPath` to anything except `.githooks`. Tests added. |
| `git push origin +main`, `--mirror`, `GIT_DIR=x git push -f`, `git reset --har` | Medium | **Fixed.** Env-prefix stripping, `+refspec`, `--mirror`, `--forc…` prefixes, `--har…` prefixes. Tests added. |
| `/bin/rm`, `\rm`, `"rm"`, `(…)`, `{ …; }`, `&`, `time`/`nice`/`xargs`/`eval`/`FOO=1` wrappers, `bash -c "rm -rf"`, `find -delete` | Medium | **Fixed.** Quote-aware tokens, basename of the command, more separators, wrapper stripping, recursive check of `-c` and `eval` payloads, `find -delete` / `-exec rm`. Tests added. |
| `bash <(curl …)`, `sh -c "$(curl …)"`, `curl … \| python3` | Medium | **Fixed.** Process substitution, command substitution and pipes into python/node/perl/ruby. Tests added. |
| `protect-files.sh` misses `*.key`, `*.pfx`, `secrets/`; shell writes (`>`, `tee`, `sed -i`, `cp`) to protected paths bypass it | Medium | **Fixed** for secrets paths in both hooks. Tests added. Protecting the harness itself → **D11**. |
| `Bash(dotnet *)` auto-approves `dotnet add package` / `tool install` / `nuget push` | Medium | **D10** (permission change). |
| `Bash(gh pr *)` auto-approves `gh pr merge --admin`, against Checkpoint B | Medium | **D10.** |
| `Bash(docker *)` auto-approves `docker run --privileged -v /:/host` | Medium | **D10** (same as auditor S2). |
| No server-side secret scan in CI | Medium | **D12.** |
| `git switch -f` / `--discard-changes` / `git branch -D` / `git worktree remove --force` | Low | **Fixed** in the hook: `switch`/`checkout` force, `--discard-changes`, `git clean -f`. Deny rules for `branch -D` / `worktree remove --force` → **D10**. |
| Commit messages mentioning `-f` or `--no-verify` blocked (false positive) | Low | **Fixed.** `-m`/`-F` values are ignored. Tests added. Note: the settings deny rule `Bash(* --no-verify *)` still matches message text → **D10**. |
| Stop hook loop guard needs `jq` | Low | **Fixed.** `grep` fallback. |
| Stop hook ignores `.editorconfig`; doesn't re-verify already-committed breakage | Low | `.editorconfig` **added**. Committed breakage is caught by `verify.sh` in CI and the review gate; not added to the hook (it would build on every stop). |
| `protect-files.sh` blocks current-branch migrations too | Low | **Fixed.** It only blocks migrations that exist on `main` (`git cat-file -e main:<path>`). |
| `format-on-edit.sh` uses the main repo root for worktree files | Low | **Fixed.** The root is resolved from the edited file's own repo. |
| `ci.yml` cancels in-progress runs on `main` | Low | **Fixed.** Cancel only for PRs. |
| No `nuget.config` source mapping / lock files | Low | **D12.** |

**Reviewer conduct note.** The reviewer reported that one of its test commands accidentally ran `git -C <repo> reset --har`. The auto-mode classifier denied it, so no git state changed. It was running as `general-purpose`, so its read-only limit was an instruction, not enforced by `disallowedTools`. This is one more reason to run the real `.claude/agents/` definitions from a session started in the repo.

## Local AI reviews on PR #1 (Alessandro's subscription)

**`/code-review` (high effort, inline).** 7 findings, all **fixed** and covered by new regression cases (91 total):

| # | Finding | Fix |
|---|---|---|
| 1 | `bash -lc "rm -rf x"` / `sh -ec` skipped the nested-payload check | `-c` detected inside combined flags |
| 2 | `curl x \| tee f \| bash` passed (interpreter not the next pipe stage) | regex matches any later pipe stage |
| 3 | Fallback tokenizer (unbalanced quotes) didn't split `(`, `)`, `&`, `\|` → `echo it's; (rm -rf x)` passed | fallback pads punctuation before splitting |
| 4 | `git branch --delete --force`, `-df`, `git worktree remove -f` passed the narrow deny strings | hook checks branch/worktree force-delete; deny rules added |
| 5 | Deny `dotnet package *` also blocked the documented read-only `dotnet package search` | deny narrowed to `dotnet package add/remove` |
| 6 | Protected-path list duplicated in two hooks (drift risk) | single `.claude/hooks/protected-paths.regex` read by both |
| 7 | No tests for the verify-on-stop / format-on-edit / reinject-context hooks | added: loop guard, jq-less fallback, no-change skip, Resume section, formatting |

**`/security-review`.** **No findings** (confidence ≥ 8). Non-security note: hooks failed open (exit 1) if the regex file was missing. **Fixed:** both now exit 2 (checked by moving the file away: exit 2 from both).

## Local AI reviews on PR #2 (live proof)

**`/code-review` (high effort, inline).** 6 findings on the docs, all **fixed**:

| # | Finding | Fix |
|---|---|---|
| 1 | Step 1 said "No file was created" for `.env.example`, but nothing checked it (`.env.*` is gitignored, so `git status` can't show it) | Marked "not verified". `ls -la .env.example` was denied by the same deny rule, so the agent didn't work around it. |
| 2 | The rewritten open issue dropped the still-open "re-login the `claude` CLI" item | Restored as its own open issue |
| 3 | Open issue claimed `*.pem` behaves like `.env*` and stated the deny/hook order as fact, with no evidence | Reworded to what was observed; `*.pem` marked "not tested live" |
| 4 | "Prompts" section still said P00-01 to P00-06 | Now P00-01 to P00-11 |
| 5 | "Resume here" and the status line named the branch but not the PR | Linked [#2](https://github.com/Alelentini2001/Mortgage-Application-API-test/pull/2) |
| 6 | Orchestration line still said agents "weren't registered in this session" (ambiguous) | Now "the authoring session", with a pointer to Live proof step 4 |

**`/security-review`.** **No findings** (finder sub-task P00-11). Docs-only change: no secrets, no new hook or permission bypass documented.

## AI output corrected or rejected
Real cases only:
1. **`block-dangerous-bash.sh` v1 bugs, caught by the agent's own hook tests before commit.**
   - A plain `rm file.txt` exited with code 1: `grep -Eo` found no flags, and `pipefail` with `set -e` aborted the script.
   - Flags were collected from the whole command line, so `rm x && dotnet test -f net10.0 -r osx-arm64` would have been blocked as `rm -rf`.
   - Fix: parse each command segment separately (split on `; && || |`).
2. **`verify-on-stop.sh` v1 scope too wide.** It treated any change under `src/` or `tests/`, including nested `CLAUDE.md` files, as a reason to build. Narrowed to `*.cs`, `*.csproj`, `*.props`, `*.slnx`, `global.json`.
4. **False "Semgrep: no findings" claim by the lead agent, caught by the security-auditor.**
   - The lead agent's first Semgrep run included `--config p/bash`, which doesn't exist (HTTP 404), so Semgrep aborted.
   - The agent then printed `$?` after `| tail`, which reported `tail`'s exit code (0), not Semgrep's.
   - The agent reported "no findings" in chat without seeing a real Semgrep result.
   - Fixed: re-ran with real exit codes (above). Lesson: never read `$?` after a pipe; use `--json` and count the results.
5. **S1 hook bypass (`git commit -n`), found by the security-auditor.** Fixed (see Security gate).
6. **The lead agent's regex-based Bash hook (v2) was bypassable in 20+ ways**, found by the fresh-context `dotnet-reviewer`. Examples: `/bin/rm -rf`, `bash -c "rm -rf"`, `git push origin +main`, `git reset --har`. It also blocked legitimate commit messages. The agent's own 18-case test had passed, so author-written tests weren't enough. Fixed with a quote-aware tokenizer and a 73-case regression suite now run in CI. This is the clearest Phase 0 example of "the author never grades its own work".
3. **Test projects didn't compile at first** (`CS0246: 'Fact' could not be found`). Unlike the template, `xunit.v3` doesn't add a global `using Xunit`. Fixed with `<Using Include="Xunit" />` in both test projects.

## Optimisations considered
- Format-on-edit uses `dotnet format whitespace --folder` (measured ~1.0 s) instead of a full `dotnet format` per edit (it loads the workspace and runs analyzers; not measured). Full formatting is enforced by `verify.sh`.

## Open issues
- ~~Live hook proof is pending.~~ Resolved 2026-11-14 in [#2](https://github.com/Alelentini2001/Mortgage-Application-API-test/pull/2): `protect-files.sh`, `block-dangerous-bash.sh` and `format-on-edit.sh` fired live (see "Live proof"). `verify-on-stop.sh` and `reinject-context.sh` are still proven by script tests only.
- **Re-login the `claude` CLI** (`OAuth session expired` on headless `claude -p`). Still open; not checked in the live-proof session.
- **The `.env*` deny rule hides the hook.** The live `Write` to `.env.example` returned the deny-rule error, not the hook message (Live proof, step 1). That's fine (two layers), but a live check of the hook has to use a path only the hook covers, such as `*.key`. `*.pem` also has a `Read` deny rule, so it probably behaves the same way; not tested live.
- ~~`docs/spec/ORIGINAL_SPEC.md` is missing.~~ Added on `main` in `67977f2`.
- ~~AI review workflows skip until the API key secret exists~~. Resolved at Checkpoint B: the workflows were removed and reviews run locally.
- **Branch protection on `main`** (require CI) is for Alessandro to enable.
- **The brew cask also installed .NET SDK 11.0.100** (unused). `global.json` pins 10.x, so builds use 10.0.404.

## Time spent (approx.)
- A1 + docs + plan: ~25 min (including the 5-min folder-picker timeout).
- Implementation + harness tests + gates: ~1 h 10 min (approximate, from session timestamps).

## Human review (Alessandro writes this; the agent leaves it empty)
