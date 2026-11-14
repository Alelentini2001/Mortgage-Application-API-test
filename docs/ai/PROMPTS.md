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

### P00-09 · human→agent · 2026-11-14
- **Prompt (verbatim):**
  > Resume this project. Follow CLAUDE.md's resume protocol: read docs/PROGRESS.md ("Resume here") and docs/progress/phase-00-harness.md first, then tell me in 3 lines where we are.
  >
  > Before Phase 1, close the one open Phase 0 item: prove the harness LIVE in this session (the authoring session never loaded the repo's hooks, so they are only proven by script tests). Do each step with the real tool, quote the real output, and do not retry a step that is blocked:
  > 1. Use the Write tool to create .env.example with "X=1". Expected: blocked by protect-files.sh.
  > 2. Use the Bash tool to run `rm -r -f ./harness-probe-dir`. Expected: blocked by block-dangerous-bash.sh.
  > 3. Use the Write tool to create src/MortgageApplications.Core/HarnessProbe.cs with badly formatted code, then show the file. Expected: whitespace formatted by format-on-edit.sh. Then delete the file.
  > 4. Confirm the five project subagents (dotnet-reviewer, spec-checker, security-auditor, test-designer, parallel-implementer) are registered, and tell me how to check what /context loads.
  > If any hook does not fire, stop and tell me; don't work around it.
  > Record the live evidence in docs/progress/phase-00-harness.md ("Harness proof" + "Open issues"), update docs/PROGRESS.md, commit on a branch `chore/phase-00-live-proof`, and open a small PR. Run the local /code-review and /security-review on it per the phase skill, then stop for my approval.
  >
  > After that PR is merged, start Phase 1 with the phase loop: /phase 01 spec
  > Phase 1 scope: docs/spec/affordability-rules.md (starting from master prompt B4 in docs/ai/prompts/00-master-prompt.md, re-checking the worked examples) and docs/layers/PRODUCTION-LAYERS.md filled in for this API. Have the dotnet-reviewer subagent critique the rules before any code, and have the spec-checker map docs/spec/ORIGINAL_SPEC.md into the traceability section of docs/PROGRESS.md. Stop at CHECKPOINT A with the decisions I need to make.
- **Purpose:** prove the hooks live, then queue Phase 1.
- **Outcome:** steps 2, 3 and 4 passed live. Step 1 was blocked by the `Read(**/.env*)` deny rule before the hook ran, so the agent stopped and asked how to prove `protect-files.sh`.

### P00-10 · human→agent · 2026-11-14
- **Prompt (verbatim):**
  > Probe a .key file
- **Purpose:** choose a live test for `protect-files.sh` on a path that only the hook protects.
- **Outcome:** a `Write` to `certs/harness-probe.key` was blocked by `protect-files.sh`. Evidence recorded in the phase 0 file ("Live proof").

### P00-11 · agent→subagent (`/security-review` skill, finder sub-task, general-purpose) · 2026-11-14
- **Prompt:** the bundled `/security-review` instructions, shortened for a docs-only PR. Full text, verbatim:
  ```
  You are a senior security engineer doing a focused security review of a pull request. Read-only: do not write or edit any file, do not run destructive commands.

  Repository: /Users/ale_ssd/Developer/Mortgage-Application-API-test (branch chore/phase-00-live-proof). Get the diff with `git -C /Users/ale_ssd/Developer/Mortgage-Application-API-test diff main...HEAD` plus uncommitted changes with `git -C /Users/ale_ssd/Developer/Mortgage-Application-API-test diff HEAD`. That combined diff is the full scope.

  Context: the PR is docs only. It records live evidence that the repo's Claude Code hooks fire (protect-files.sh, block-dangerous-bash.sh, format-on-edit.sh) in docs/progress/phase-00-harness.md, updates docs/PROGRESS.md, and logs prompts verbatim in docs/ai/PROMPTS.md. No code, config, hook, workflow or permission file changes. The quoted outputs include an absolute local path and hook error messages.

  OBJECTIVE: identify HIGH-CONFIDENCE security vulnerabilities newly added by this PR that have real exploitation potential. Not a general review. Do not comment on existing concerns.
  - Minimise false positives: only flag issues where you are >80% confident of actual exploitability. Skip theoretical issues, style, low impact.
  - Exclusions: DoS; secrets or sensitive data stored on disk (handled elsewhere); rate limiting / resource exhaustion; insecure documentation (findings in documentation files such as markdown are excluded).
  - Categories: input validation (SQL/command/XXE/template/NoSQL injection, path traversal); authn/authz (bypass, privilege escalation, sessions, JWT); crypto and secrets (hardcoded keys, passwords, tokens, weak crypto, key storage, randomness, cert validation); injection and code execution (deserialization, pickle, YAML, eval, XSS); data exposure (sensitive data logging, PII, endpoint leakage, debug info).
  - Method: research repo context (security model, existing patterns), compare the changes against it, trace data flows, and assess each modified file. In particular check whether the new text discloses a real secret or token, or documents a working bypass of the hooks or permission rules that wasn't already in the repo.
  - Severity: HIGH = directly exploitable (RCE, data breach, auth bypass); MEDIUM = significant impact under specific conditions; LOW = defense in depth. Report HIGH and MEDIUM only. Confidence 0.0-1.0; do not report below 0.7.

  Output: markdown. For each finding: file, line, severity, category, description, exploit scenario, recommendation, confidence. If there are none, say "No findings" and give a 2-4 sentence rationale listing what you checked.
  ```
- **Purpose:** local AI security review of PR #2 on Alessandro's subscription.
- **Outcome:** no findings. The sub-task checked for secrets, documented hook or permission bypasses, and injection through the logged prompts. No false-positive filtering sub-tasks were needed.
- `/code-review` (high effort) ran inline in the lead agent, with no subagent. It reported 6 findings on the docs, all fixed (see the phase file, "Local AI reviews on PR #2").

## Phase 01: spec + production layers

### P01-01 · human→agent · 2026-11-14
- **Prompt (verbatim):** `/phase 01 spec` with the arguments:
  > 01 spec
  >
  > Also proceed automatically, completing all, testing, analyzing, etc...
- **Purpose:** run Phase 1 end to end without stopping at Checkpoint A. The scope comes from P00-09.
- **Outcome:** the agent applied the recommended option for every decision (D1–D8, marked "decided by: agent (auto-proceed)") and still stopped before the squash-merge (Checkpoint B).

### P01-02 · agent→subagent (`dotnet-reviewer`) · 2026-11-14
- **Prompt (verbatim):**
  ```
  Critique the affordability spec for Phase 1 of the repo at /Users/ale_ssd/Developer/Mortgage-Application-API-test (branch phase-01-spec). No code exists yet: this review happens before any code is written, so your job is to find problems in the rules that would cause wrong, untestable or unexplainable behaviour once implemented.

  Read: docs/spec/ORIGINAL_SPEC.md (requirements, source of truth), docs/spec/affordability-rules.md (the rules under review), docs/adr/0001-affordability-assessment-model.md, the decisions table and "Worked-example check" in docs/progress/phase-01-spec.md, and AGENTS.md plus src/MortgageApplications.Core/CLAUDE.md (engineering rules: decimal only, round once at the end with AwayFromZero, evaluate every rule, thresholds in AffordabilityOptions stamped as RulesVersion).

  Check in particular:
  1. Maths: are the formulas correct (annuity payment, DTI, the DTI-based max borrowing inversion, LTI)? Recompute at least E1, E3, E5 and E8 independently (by hand or a short script with Python's decimal module at 28+ digits of precision; do not modify any repo file). Report any value that differs at 2 dp.
  2. Consistency: do the checks, the invariant "Approved ⇒ Loan ≤ MaxBorrowing", the rounding rules and the explanation formats agree with each other? Any case where the explanation could contradict the shown numbers?
  3. Edge cases and decimal behaviour: zero expenses, zero deposit, deposit near property value, expenses above the DTI limit, minimum/maximum inputs from section 1, term 5 and 40, precision loss or overflow in (1+r)^n with decimal, division by zero.
  4. Spec fit: does the output cover everything ORIGINAL_SPEC.md requirement 3 asks for? Are assumptions stated clearly enough for "clearly explained"?
  5. Testability: can Phase 2 write deterministic golden tests from section 8? Anything ambiguous an implementer would have to guess?
  6. Anything in the rules that conflicts with AGENTS.md or the Core CLAUDE.md.

  Output: a table of findings (id, severity high/medium/low, section, problem, concrete failing example, suggested fix), then your recomputed values for the examples you checked. Flag only correctness, requirement and testability gaps; no style comments.
  ```
- **Purpose:** fresh-context critique of the rules before any code (B5, Phase 1).
- **Outcome:** see the phase 1 file, "Review gate".

### P01-03 · agent→subagent (`spec-checker`) · 2026-11-14
- **Prompt (verbatim):**
  ```
  Build the requirement traceability map for the repo at /Users/ale_ssd/Developer/Mortgage-Application-API-test (branch phase-01-spec). We are at the end of Phase 1 of 12; almost nothing is implemented yet, so most requirements will be "missing" or "planned". That's expected: the goal is a complete map that later phases fill in.

  Source of truth: docs/spec/ORIGINAL_SPEC.md. Split it into atomic, numbered requirements (R1, R2, ...) covering every section: Scenario, Requirements 1-3 (each listed field and each "You should" bullet, each assessment output), Technical Expectations, Testing, Optional Enhancements, Documentation, Design & Reflection (each of the 4 questions), Submission, Follow-Up Discussion (fresh clone/build/run/demo, max 3 slides, the suggested topics).

  For each requirement give:
  - Status now: met / partial / missing.
  - Where it is met or will be met: the existing file(s) that address it today (for example docs/spec/affordability-rules.md, docs/layers/PRODUCTION-LAYERS.md, src/..., tests/...), and the planned phase from the phase list in docs/PROGRESS.md and the plan in docs/ai/prompts/00-master-prompt.md section B5.
  - Evidence: for "met" or "partial", the specific file and section (or test name) you checked. Never mark something met without having opened the file.

  Also read docs/spec/affordability-rules.md and check Requirement 3 specifically: does the rules document define every assessment output the spec asks for (LTV, debt-to-income, approved/declined decision, max borrowing estimate, short explanation) and state its assumptions? Report gaps.

  Output: one Markdown table with columns `ID | Requirement (short) | Spec section | Status | Where (file / planned phase) | Evidence`, followed by a short list of gaps or ambiguities in the spec mapping that need a human decision. Keep requirement text short (under 12 words each).
  ```
- **Purpose:** map `ORIGINAL_SPEC.md` into the traceability section of `docs/PROGRESS.md`.
- **Outcome:** see the phase 1 file, "Review gate", and `docs/PROGRESS.md`, "Requirement traceability".
