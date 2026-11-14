# Prompts log

Every prompt, verbatim, by phase. Tags: `human→agent` (Alessandro to the lead agent) and `agent→subagent` (lead agent to a subagent or another Claude process). Each entry has its purpose and outcome.

## Phase 00: harness + skeleton

### P00-01 · human→agent · 2026-11-13
- **Prompt:** "Before proceeding show me your plan and the things you will do", followed by the master prompt. Full text, verbatim: [prompts/00-master-prompt.md](prompts/00-master-prompt.md).
- **Purpose:** bootstrap the harness and run Phase 0 up to Checkpoint A.
- **Outcome:** the agent replied with a plan only (no files touched). It flagged the missing repo folder, the missing spec file, missing toolkit items, and 8 harness issues to decide.

### P00-02 · human→agent · 2026-11-13
- **Prompt (verbatim):**
  > Proceed, this is the GitHub repo: https://github.com/Alelentini2001/Mortgage-Application-API-test.git
  >
  > Also remember to maintain the project structure and code clean!
- **Purpose:** start Phase 0.
- **Outcome:** the folder picker timed out. Alessandro then answered four multiple-choice questions: clone to `~/Developer`, Claude runs brew for the .NET SDK, repo public at the end, brew-install Semgrep + Gitleaks (no Learn MCP). The agent cloned the repo, ran the A1 toolkit check and the docs check, wrote the Phase 0 plan, and stopped at Checkpoint A.

### P00-03 · human→agent · 2026-11-14
- **Prompt (verbatim):**
  > i installed the 10 pkg, you can proceed
- **Purpose:** resolve D9 (.NET 10 SDK) and approve the Phase 0 plan.
- **Outcome:** treated as approval of D1–D9 with the recommended options. Phase 0 was implemented.

### P00-04 · agent→headless Claude (`claude -p`, haiku) · 2026-11-14 · **did not run**
- **Prompt (verbatim):**
  ```
  Harness test. Do exactly these steps, one tool call each, and do not retry a step that is blocked.
  1) Use the Write tool to create the file .env.example with the content: X=1
  2) Use the Bash tool to run exactly: rm -r -f ./harness-probe-dir
  3) Use the Write tool to create src/MortgageApplications.Core/HarnessProbe.cs with exactly this content:
  namespace MortgageApplications.Core;
  public static class HarnessProbe
  {
          public static int   Add( int a,int b )   =>a+b;
  }
  4) Use the Bash tool to run exactly: cat src/MortgageApplications.Core/HarnessProbe.cs
  Then report, for each step, whether it succeeded or was blocked, quoting any block message verbatim.
  ```
- **Purpose:** prove the hooks fire in a real Claude Code session started inside the repo. The lead session can't do this, because it started in another folder and doesn't load the repo's hooks.
- **Outcome:** **not run.** The CLI reported `Failed to authenticate: OAuth session expired` and that the workspace is not trusted. Live proof is deferred to the next interactive session in the repo (see phase file, "Open issues").

### P00-05 · agent→subagent (`dotnet-reviewer` role, run as general-purpose/opus) · 2026-11-14
- **Note:** the repo's `.claude/agents/` definitions weren't registered in this session (it started outside the repo). The reviewer ran as a `general-purpose` subagent told to load and follow `.claude/agents/dotnet-reviewer.md`, so its read-only limit was instructed, not enforced by frontmatter.
- **Prompt (verbatim):**
  ```
  You are acting as the `dotnet-reviewer` subagent. First read /Users/ale_ssd/Developer/Mortgage-Application-API-test/.claude/agents/dotnet-reviewer.md and follow its instructions and output format exactly. You are read-only: never use Write, Edit or any command that modifies files or git state.

  Review the Phase 0 diff of the repo at /Users/ale_ssd/Developer/Mortgage-Application-API-test: `git -C /Users/ale_ssd/Developer/Mortgage-Application-API-test diff main...phase-00-harness`. Phase 0 is a harness + empty .NET 10 solution skeleton (no business logic yet). Focus on: (1) correctness of the hook scripts in .claude/hooks/ against their stated purpose (can a dangerous command or protected file slip through? can a hook wrongly block normal work? can the Stop hook loop?), (2) .claude/settings.json permission rules and hook wiring, (3) scripts/verify.sh failing correctly (non-zero exit on any failure), (4) the GitHub workflows' security (permissions, fork PRs, secret handling, pinned actions), (5) build configuration (global.json, Directory.Build.props, Directory.Packages.props, test projects on Microsoft.Testing.Platform), (6) any requirement in AGENTS.md that the skeleton already violates. You may run read-only commands and you may pipe sample JSON into the hook scripts to test them (protect-files.sh and block-dangerous-bash.sh only read stdin; do not run verify-on-stop.sh or format-on-edit.sh). Output only the finding list in your required format, or "No findings." with the files reviewed.
  ```
- **Purpose:** fresh-context review gate for Phase 0.
- **Outcome:** see the phase 0 file, "Review gate".

### P00-06 · agent→subagent (`security-auditor` role, run as general-purpose/opus) · 2026-11-14
- **Note:** same registration limit as P00-05.
- **Prompt (verbatim):**
  ```
  You are acting as the `security-auditor` subagent. First read /Users/ale_ssd/Developer/Mortgage-Application-API-test/.claude/agents/security-auditor.md and follow its instructions and output format exactly. You are read-only: never use Write, Edit or any command that modifies files or git state.

  Run the security gate for Phase 0 of the repo at /Users/ale_ssd/Developer/Mortgage-Application-API-test (branch phase-00-harness, diff against main). cd into that directory first. Run your scanners (gitleaks history + working tree, semgrep with p/csharp, p/secrets, p/github-actions and p/bash using --metrics=off, dotnet vulnerable-package scan) and review the diff against the OWASP Top 10:2025 checklist. Phase 0 contains no business logic: the relevant attack surface is the GitHub Actions workflows (.github/workflows/), the Claude Code hooks and permission settings (.claude/), the git pre-commit hook (.githooks/), scripts/verify.sh, and the minimal Program.cs. Pay particular attention to A08 (workflow permissions, untrusted PR code, prompt injection into the AI review actions) and A03 (pinned actions and packages). Output your table and verdict.
  ```
- **Purpose:** security gate for Phase 0.
- **Outcome:** see the phase 0 file, "Security gate".

### P00-07 · human→agent · 2026-11-14
- **Prompt (verbatim):**
  > Ok proceed, and about the AI reviews, they should be done here with my subscription!
- **Purpose:** Checkpoint B approval, and redirect the AI reviews from CI (API key) to local runs.
- **Outcome:**
  - D10 and D11 applied, D12 deferred to Phase 8.
  - The AI review workflows were removed.
  - Local `/code-review` (high effort) and `/security-review` ran on PR #1 (P00-08) and their summary was posted as a PR comment.

### P00-08 · agent→subagent (`/security-review` skill, finder sub-task, general-purpose) · 2026-11-14
- **Prompt:** the bundled `/security-review` instructions, adapted for this repo. The repo context paragraph, verbatim:
  ```
  Context: the PR is a Phase 0 harness + empty .NET 10 skeleton: Claude Code hooks (.claude/hooks/*.sh, one embeds a Python tokenizer), permission settings (.claude/settings.json), a gitleaks git pre-commit hook (.githooks/pre-commit), a CI workflow (.github/workflows/ci.yml, pull_request + push to main, contents: read, actions pinned by SHA), scripts/verify.sh, and a minimal ASP.NET Program.cs with a /health endpoint. No business logic, no auth, no data access yet. Earlier gates already raised: broad auto-approve permissions (now narrowed), harness self-protection (now `ask` rules), no server-side gitleaks in CI and no NuGet lock files (deferred to Phase 8).
  ```
  The rest follows the skill's own text: objective, exclusions, categories, method and output format.
- **Purpose:** local AI security review on Alessandro's subscription.
- **Outcome:** no findings at confidence ≥ 8, so no false-positive filtering sub-tasks were needed. One non-security note: if `protected-paths.regex` were missing, the hooks would fail open (exit 1). Fixed: they now exit 2.
- `/code-review` (high effort) ran inline in the lead agent, with no subagent. It reported 7 findings, all fixed (see the phase file).
