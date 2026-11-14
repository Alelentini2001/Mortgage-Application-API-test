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
