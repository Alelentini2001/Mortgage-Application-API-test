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
- **Outcome:** the agent applied the recommended option for every decision (D1–D9, marked "decided by: agent (auto-proceed)") and still stopped before the squash-merge (Checkpoint B).

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

### P01-04 · agent→subagent (`security-auditor`) · 2026-11-14
- **Prompt (verbatim):**
  ```
  Run the security gate for Phase 1 of the repo at /Users/ale_ssd/Developer/Mortgage-Application-API-test (branch phase-01-spec, diff against main: `git -C /Users/ale_ssd/Developer/Mortgage-Application-API-test diff main...HEAD`). cd into that directory first.

  Phase 1 is docs only plus one skill file: docs/spec/affordability-rules.md, docs/adr/0001-affordability-assessment-model.md, docs/layers/PRODUCTION-LAYERS.md, docs/progress/phase-01-spec.md, docs/PROGRESS.md, docs/ai/PROMPTS.md, docs/ai/DESIGN-JOURNAL.md and .claude/skills/affordability-rules/SKILL.md. No code, config, hook, workflow or permission changes.

  Run your scanners: gitleaks on history and working tree, semgrep with p/secrets (--metrics=off, --error, --json; count results from the JSON, never read $? after a pipe), and the dotnet vulnerable-package scan. Then review the diff as a design-stage security review against OWASP Top 10:2025: the rules and layers documents now fix security-relevant design choices that Phases 3-8 will implement (input limits and validation, the minimum loan, no PII or derived ratios in logs, owner scoping with 404 for other users, JWT dev / Entra ID production, the dev token endpoint, rate limiting marked optional, data minimisation and retention). Flag design gaps that would lead to a real vulnerability once built (for example a missing authorization requirement, an unsafe default, or a data-exposure path such as returning or logging personal financial data), and anything in the skill file that would steer future code into an insecure pattern.

  Output your table (tool / command / result) and findings table (id, severity, finding, file, recommendation), separating real findings from false positives, and a PASS/FAIL verdict.
  ```
- **Purpose:** the Phase 1 security gate.
- **Outcome:** see the phase 1 file, "Security gate".

### P01-05 · agent→subagent (`/security-review` skill, finder sub-task, general-purpose) · 2026-11-14
- **Prompt:** the bundled `/security-review` instructions, shortened for a docs-only PR. It has the same structure as P00-11, with this context paragraph, verbatim:
  ```
  Context: Phase 1 of a .NET 10 mortgage-application API. The PR is docs only plus one Claude Code skill file: docs/spec/affordability-rules.md (calculation rules), docs/adr/0001-affordability-assessment-model.md, docs/layers/PRODUCTION-LAYERS.md (security/auth/logging design for later phases), docs/progress/phase-01-spec.md, docs/PROGRESS.md, docs/ai/PROMPTS.md, docs/ai/DESIGN-JOURNAL.md and .claude/skills/affordability-rules/SKILL.md (instructions that steer future code generation). No code, config, hook, workflow or permission changes. A security-auditor already reviewed the design (findings S1-S11 in the phase file, "Security gate").
  ```
  The method line also asked it to check "whether the skill file (which is loaded automatically when future code is written) would steer code into an exploitable pattern". The rest (objective, categories, exclusions, severity, confidence ≥ 0.7, output format) is word for word P00-11's text.
- **Purpose:** local AI security review of PR #3 on Alessandro's subscription.
- **Outcome:** no findings, so no false-positive filtering sub-tasks were needed. Its below-threshold note (`ThrowIf*` helpers put values in exception messages) was applied to rules §3 and the skill.
- `/code-review` (high effort) ran inline in the lead agent, with no subagent. It reported 8 findings, all fixed (see the phase file, "Review gate").

### P01-06 · human→agent · 2026-11-14
- **Prompt (verbatim):**
  > Proceed
- **Purpose:** Checkpoint B approval of Phase 1.
- **Outcome:**
  - D1–D9 approved as recommended (D4 stays income-based), ADR 0001 Accepted.
  - PR #3 squash-merged as `d1c096a` after CI passed.
  - `security-auditor` S10 and the `spec-checker` questions were not answered, so they stay open.

### P01-07 · human→agent · 2026-11-14
- **Prompt (verbatim):**
  > Also make the commits and stuff done easier to read when pushing!! Easy but technical too
- **Purpose:** make commit messages, PR descriptions and squash-merge messages easier to read.
- **Outcome:**
  - New "Commits and PRs" rules in `AGENTS.md`.
  - A PR template (`.github/pull_request_template.md`).
  - The phase skill now squash-merges with a written message.
  - It applies from now on. Commits already on `main` were not rewritten: that would need a force push, which the repo forbids.

### P01-08 · agent→subagent (`/security-review` skill, finder sub-task, general-purpose) · 2026-11-14
- **Prompt:** same structure as P01-05. The context paragraph, verbatim:
  ```
  Context: a small harness/docs change in a .NET 10 mortgage-application API repo. It adds commit and PR writing rules to AGENTS.md (instructions every coding agent loads), a GitHub PR description template (.github/pull_request_template.md), one sentence in the /phase Claude Code skill (.claude/skills/phase/SKILL.md) about squash-merging with a written message via `gh pr merge <n> --squash --subject ... --body-file <file>` (file kept in the session scratchpad), and progress/prompt-log notes. No code, workflow, hook, settings or permission changes.
  ```
  The method line asked it to check whether the new agent instructions would weaken a guardrail: shell injection when building the `gh pr merge` command from PR text, bypassing the `ask` permission on `gh pr merge`, or leaking secrets into commit or PR text.
- **Purpose:** local AI security review of PR #4.
- **Outcome:**
  - No findings.
  - Its below-threshold note was applied: the `--subject` value is now single-quoted in `AGENTS.md`, so a title containing `$(...)` is never expanded.
- `/code-review` (high effort) ran inline in the lead agent: 6 findings, all fixed in `b7a06cf`.

## Phase 02: core domain + calculator

### P02-01 · human→agent · 2026-11-14
- **Prompt (verbatim):** `/phase 02 core` with the arguments:
  > 02 core
- **Purpose:** run Phase 2 (core domain + calculator, test-first) through the phase loop, stopping at Checkpoint A.
- **Outcome:** plan and decisions P2-D1 to P2-D6 written to `docs/progress/phase-02-core.md`; waiting for Alessandro at Checkpoint A.

### P02-02 · human→agent · 2026-11-14
- **Prompt (verbatim):**
  > approved
- **Purpose:** Checkpoint A approval of the Phase 2 plan.
- **Outcome:** recommended options taken for P2-D1 to P2-D6; branch `phase-02-core` created.

### P02-03 · agent→subagent (`test-designer`) · 2026-11-14
- **Prompt (verbatim):**
  ```
  Propose the unit test cases for Phase 2 of the repo at /Users/ale_ssd/Developer/Mortgage-Application-API-test (branch phase-02-core). No production code exists yet; this is the test-first step.

  Read first:
  - docs/progress/phase-02-core.md (the approved plan and decisions P2-D1 to P2-D6, and the planned Core types)
  - docs/spec/affordability-rules.md (rules v1.0: the only source for formulas, limits, rounding, templates and the golden examples E1–E18)
  - src/MortgageApplications.Core/CLAUDE.md and tests/CLAUDE.md (conventions)

  Scope: Core only.
  - AffordabilityCalculator.Assess(inputs) built with AffordabilityOptions; AffordabilityResult with every section 5 output, Checks and Summary.
  - AffordabilityOptions.Validate() returning error messages for each section 1 option bound.
  - The internal input guard (section 1 preconditions, ArgumentOutOfRangeException naming the parameter only, no value in the message), used by MortgageApplication.Create and by the calculator.
  - MortgageApplication entity (immutable; Guid v7 id; owner id; created time passed in).
  - AffordabilityAssessment entity (Pending -> Completed with a result, or Failed; timestamps passed in; no second completion; stores RulesVersion, rate and limits through the result).
  - DecimalMath.Pow (exponentiation by squaring on decimal) and the 2 dp AwayFromZero rounding + scale 2.
  - The property "Approved => Loan <= MaxBorrowing" as a seeded System.Random loop (P2-D4): propose the input generator bounds and sample count.
  - Culture independence: InvariantGlobalization is on in this repo, so the test must use a clone of the invariant culture with ',' as decimal separator, not a named culture.

  Use E1–E18 as the golden set; do not recompute them, cite them by ID and list which output fields each must assert. Only compute new expected numbers where you propose a case outside E1–E18, and show the working; flag any value you are not sure of. Mark each case must or nice, keep the list tight (no redundant cases), and group by category as your instructions say. End with a short list of anything in the rules that is ambiguous for an implementer.
  ```
- **Purpose:** test-first step: propose the Phase 2 unit test cases.
- **Outcome:** about 60 cases in 11 groups plus 17 ambiguities. The lead agent recomputed every new expected number it gave (B4–B8) with `System.Decimal` in the session scratchpad: all matched. Selection by Alessandro recorded in the phase file, "Tests".

### P02-04 · human→agent · 2026-11-14
- **Prompt (verbatim):**
  > approved
- **Purpose:** approve the recommended test selection (including the new E19) and the answers to the `test-designer` ambiguities.
- **Outcome:** recorded in the phase file, "Tests"; tests written next.

### P02-05 · agent→subagent (`security-auditor`) · 2026-11-14
- **Prompt (verbatim):**
  ```
  Run the security gate for Phase 2 of the repo at /Users/ale_ssd/Developer/Mortgage-Application-API-test (branch phase-02-core, diff against main: `git -C /Users/ale_ssd/Developer/Mortgage-Application-API-test diff main...HEAD`). cd into that directory first.

  Phase 2 adds the pure domain in src/MortgageApplications.Core (affordability calculator, options validation, input guard, summary builder, decimal maths, MortgageApplication and AffordabilityAssessment entities), an InternalsVisibleTo item in the Core csproj, unit tests in tests/MortgageApplications.UnitTests, and docs (docs/progress/phase-02-core.md, docs/ai/PROMPTS.md). No HTTP, persistence, auth, logging, packages, workflow, hook or permission changes.

  Run your scanners: gitleaks on history and working tree, semgrep with p/csharp and p/secrets (--metrics=off, --error, --json; count results from the JSON, never read $? after a pipe; add --baseline-commit main for a diff run), and the dotnet vulnerable-package scan. Then review the diff against OWASP Top 10:2025 and the repo rules in .claude/rules/security.md and docs/spec/affordability-rules.md sections 1, 3 and 6. Focus on what this code will expose once Phase 3 wires it to HTTP: whether any exception message, ToString, record print or summary can carry an applicant's input or a derived value (the guard must never include the value; rules section 6 logging allowlist); whether invalid or extreme input can crash, overflow or loop (decimal overflow, division by zero, Pow); whether the options validation fails closed; and whether entity invariants can be bypassed (public setters, state transitions). Note that records generate a ToString that prints every property: say whether that is a real risk for Phase 5 logging and what to do about it.

  Output your table (tool / command / result) and findings table (id, severity, finding, file:line, recommendation), separating real findings from false positives, and a PASS/FAIL verdict.
  ```
- **Purpose:** the Phase 2 security gate.
- **Outcome:** see the phase 2 file, "Security gate".

### P02-06 · agent→subagent (`dotnet-reviewer`) · 2026-11-14
- **Prompt (verbatim):**
  ```
  Review the Phase 2 diff of the repo at /Users/ale_ssd/Developer/Mortgage-Application-API-test: `git -C /Users/ale_ssd/Developer/Mortgage-Application-API-test diff main...HEAD` (branch phase-02-core). You have fresh context; do not trust the author's claims, check them.

  What the phase is: the pure domain in src/MortgageApplications.Core, test-first. Authoritative rules: docs/spec/affordability-rules.md (rules v1.0). Approved plan, decisions P2-D1 to P2-D6, test selection and the 13 implementation answers: docs/progress/phase-02-core.md. Conventions: AGENTS.md, src/MortgageApplications.Core/CLAUDE.md, tests/CLAUDE.md, .claude/rules/tests.md.

  Check, in this order:
  1. Maths: does AffordabilityCalculator follow rules section 2 exactly (formulas, order, decimal only), section 3 (full-precision inclusive comparisons, every check evaluated), section 4 (round once, AwayFromZero, scale 2, invariant culture) and section 6 (exact summary templates, "before rounding" rule)? Recompute at least E1, E11, E13 and E19 independently (Python decimal is fine) and compare with tests/MortgageApplications.UnitTests/Affordability/GoldenCases.cs.
  2. Guard and options: do InputGuard and AffordabilityOptions.Validate match the section 1 bounds exactly, including inclusive/exclusive edges, "at most 2 dp" by value, and the minimum loan of 1,000? Can any exception message contain an input value?
  3. Entities: can MortgageApplication or AffordabilityAssessment reach an invalid state? Are the state transitions and UTC rules sound? Anything Phase 3 (EF Core mapping) will need that this design makes hard, without speculating?
  4. Framework usage: Guid.CreateVersion7, Math.Round, decimal.Scale, CultureInfo, ArgumentOutOfRangeException constructors, records holding IReadOnlyList (equality, ToString).
  5. Tests: do they prove what docs/progress/phase-02-core.md "Tests" says? Any weak assertion, vacuous property test, culture test that could pass vacuously, or missing boundary from the approved selection? Is the seeded generator really inside the section 1 bounds?

  Report only correctness, security and requirement gaps, plus test gaps that matter. One line per finding: id, severity (high/medium/low), file:line, problem, suggested fix. Separate "real" from "nit / taste". Do not edit files.
  ```
- **Purpose:** fresh-context review gate for the Phase 2 diff.
- **Outcome:** see the phase 2 file, "Review gate".

### P02-07 · agent→subagent (`spec-checker`) · 2026-11-14
- **Prompt (verbatim):**
  ```
  Re-run the requirement traceability for the repo at /Users/ale_ssd/Developer/Mortgage-Application-API-test at the end of Phase 2 (branch phase-02-core; the working tree is the branch head).

  Inputs: docs/spec/ORIGINAL_SPEC.md (source of truth), the current table in docs/PROGRESS.md "Requirement traceability" (R1–R55, snapshot from Phase 1), docs/spec/affordability-rules.md, and docs/progress/phase-02-core.md (what Phase 2 claims to have built).

  Phase 2 added the Core domain and calculator with unit tests: src/MortgageApplications.Core/Affordability/*.cs, src/MortgageApplications.Core/Applications/*.cs, tests/MortgageApplications.UnitTests/**. No API, persistence, validation endpoint, README or Docker work.

  For every requirement R1–R55, give the status now (met / partial / missing) using the same key as docs/PROGRESS.md: "met" needs code or a test that actually does it; design alone is "partial"; a domain class that exists but is not reachable through the API is at most "partial" for an API requirement. Open the files and cite file:line or test name as evidence. Only change a row's status if Phase 2 changed it, and say why.

  Output: one Markdown table with columns `ID | Requirement (short) | Status | Where (file / planned phase) | Evidence`, then the totals (met / partial / missing), then a short list of gaps or ambiguities that need a human decision. Keep requirement text under 12 words.
  ```
- **Purpose:** requirement traceability at the end of Phase 2.
- **Outcome:** see the phase 2 file, "Review gate", and `docs/PROGRESS.md`, "Requirement traceability".

### P02-08 · agent→subagent (`/security-review` skill, finder sub-task, general-purpose) · 2026-11-14
- **Prompt:** the bundled `/security-review` instructions (objective, categories, methodology, severity, confidence, hard exclusions and precedents), condensed into one prompt with the same content. The context paragraph and method line, verbatim:
  ```
  Context: Phase 2 of a .NET 10 mortgage-application API. The PR adds the pure domain in src/MortgageApplications.Core (AffordabilityCalculator, AffordabilityOptions.Validate, InputGuard, SummaryBuilder, DecimalMath, the MortgageApplication and AffordabilityAssessment entities, records AffordabilityInputs/AffordabilityResult/CheckResult with redacted ToString), an InternalsVisibleTo item for the unit test project, unit tests, and docs. There is no HTTP endpoint, persistence, auth or logging code yet: the API only exposes /health. A security-auditor already reviewed this diff (findings S1–S6 in docs/progress/phase-02-core.md, "Security gate"), and a dotnet-reviewer and a /code-review pass ran; their fixes are in the diff. Rules: docs/spec/affordability-rules.md (sections 1, 3, 6), .claude/rules/security.md, AGENTS.md. Method: also check whether anything in this Core code would become exploitable once Phase 3 maps it to HTTP (for example an exception message or ToString carrying personal financial data, an entity invariant that can be bypassed, an ID that leaks or can be guessed).
  ```
- **Purpose:** local AI security review of PR #5 on Alessandro's subscription.
- **Outcome:** no findings at confidence 8 or above, so no false-positive filtering sub-tasks were needed.
- `/code-review` (high effort) ran inline in the lead agent, with no subagent: 8 findings, 3 fixed in `08d86d3` (see the phase file, "Review gate").

### P02-09 · human→agent · 2026-11-14
- **Prompt (verbatim):**
  > approved
- **Purpose:** Checkpoint B approval of Phase 2.
- **Outcome:**
  - CI passed on PR #5 head `871ffb0`; squash-merged as `7a579a0` with a written message.
  - The optional ADR and the `tests/CLAUDE.md` question were not answered, so they stay open.

## Phase 03: API + persistence + validation

### P03-01 · human→agent · 2026-11-14
- **Prompt (verbatim):** `/phase 03 api` with the arguments:
  > 03 api
- **Purpose:** run Phase 3 (the minimum submittable product) through the phase loop, stopping at Checkpoint A.
- **Outcome:** plan and decisions P3-D1 to P3-D8 written to `docs/progress/phase-03-api.md`; waited at Checkpoint A.

### P03-02 · human→agent · 2026-11-14
- **Prompt (verbatim):**
  > approved
- **Purpose:** Checkpoint A approval of the Phase 3 plan and every recommended option (P3-D1 to P3-D8).
- **Outcome:**
  - Branch `phase-03-api` created; packages added in `a0daa7c`.
  - The `dotnet-ef` local tool was **not** installed: the project's permission rules deny `dotnet tool *` and the auto-mode classifier refused writing `.config/dotnet-tools.json` by hand. Left for Alessandro.
  - Docs check done (phase file, "Docs verified").

### P03-03 · human→agent · 2026-11-14
- **Prompt (verbatim):**
  > Try again
- **Purpose:** retry after a WebFetch call returned a usage-limit error during the docs check.
- **Outcome:** the retried page (ASP.NET Core error handling) loaded; the docs check finished.

### P03-04 · agent→subagent (`test-designer`) · 2026-11-14
- **Prompt (verbatim):**
  ```
  Phase 3 of a .NET 10 mortgage-application API (Minimal APIs + EF Core SQLite). Propose test cases for the approved plan; do not write code.

  Read first: docs/progress/phase-03-api.md (plan, approved decisions P3-D1 to P3-D8, and "Docs verified": the framework facts there are confirmed and affect tests), docs/spec/affordability-rules.md (section 1 input bounds, sections 5-6 outputs and summary, section 8 golden examples), docs/layers/PRODUCTION-LAYERS.md ("APIs", "Auth & permissions"), .claude/rules/tests.md, tests/CLAUDE.md, .claude/rules/security.md, and the existing Core code and tests under src/MortgageApplications.Core and tests/MortgageApplications.UnitTests (Phase 2 already covers the calculator, InputGuard and entities: don't repeat those).

  What will be built (not written yet):
  - Core: a public pure rule set (ApplicationInputRules.Validate(inputs) returning (field, message) pairs) that InputGuard also uses; a MortgageApplicationService use case (create, get, assess, get assessment) over an IMortgageApplicationRepository whose read methods take the owner; RulesVersion max length 16 added to AffordabilityOptions.Validate().
  - Api: POST /api/applications (201 + Location /api/applications/{id}), GET /api/applications/{id} (200/404), POST /api/applications/{id}/assessments (synchronous, 201 + Location /api/applications/{id}/assessments/{assessmentId}; Declined is also 201), GET /api/applications/{id}/assessments/{assessmentId} (200/404). Request DTO with nullable properties, [Required], [Range(5,40)] on TermYears, IValidatableObject mapping the Core rule errors; strict JSON (unknown properties and numbers-as-strings rejected). ICurrentUser returns one fixed development owner until Phase 7; another owner's application or assessment returns 404. AddProblemDetails + IExceptionHandler (traceId, no exception text; BadHttpRequestException keeps its own 4xx status). AffordabilityOptions bound from configuration with ValidateOnStart. OpenAPI + Scalar in Development only.
  - Tests: unit tests (Core only: the rule set and the use case with a hand-written fake repository) and integration tests (WebApplicationFactory<Program>, in-memory SQLite per test class with migrations applied, Development environment by default).

  Cover at least: 400 per field and bound (exact limit, just inside, just outside) with the expected error key per field; missing field and explicit null; deposit rule (loan exactly 1,000 vs 999.99); more than 2 dp; malformed JSON, unknown property, number as string, wrong content type; IValidatableObject not running when a [Required] error exists; 201 + Location + body for create; GET round trip including scale 2 for money in JSON; assessment response values for golden E1 and one Declined golden case (use the numbers in rules section 8, don't recompute new ones unless needed and then show the working); assessment Location resolves; 404 for an unknown id, a malformed id, an assessment under the wrong application, and another owner (S2, using ConfigureTestServices to swap ICurrentUser); ProblemDetails shape with traceId on 400, 404 and 500, and no stack trace or exception message on a 500 (S5); startup fails with invalid Affordability options (S4); response never contains ownerId; no submitted value echoed in a 400 message; the use case stamps AssessedAt from TimeProvider and stores RulesVersion.

  Use the agent's output format. Mark each case must or nice, keep the list tight (no redundant cases), and list any ambiguities in the plan you need Alessandro to settle.
  ```
- **Purpose:** propose Phase 3 test cases before any test is written.
- **Outcome:** 19 unit rows and 46 integration rows, plus 12 ambiguities (A1–A12). Golden numbers checked against rules §8 by the lead agent (E1, E2, E18 match). The recommended selection and answers are in the phase file ("Tests", "Proposal"); approved in P03-05.

### P03-05 · human→agent · 2026-11-14
- **Prompt (verbatim):**
  > approved
- **Purpose:** approval of the recommended test selection and the answers to A1–A12.
- **Outcome:** red tests, then implementation (phase file, "Tests", "Results").

### P03-06 · human→agent · 2026-11-14
- **Prompt (verbatim):**
  > camelCase (keep fix)
- **Purpose:** decision on ambiguity A1, after `AddValidation` turned out to emit PascalCase keys.
- **Outcome:** `ValidationErrorKeys` kept; Api committed. Alessandro then installed `dotnet-ef` (repo-root `dotnet-tools.json`); migration generated, all 332 tests pass, full verify passed at `bf2920c`.

### P03-07 · agent→subagent (`security-auditor`) · 2026-11-14
- **Prompt (verbatim):**
  ```
  Security gate for Phase 3 of a .NET 10 mortgage-application API. Review the diff `git diff main...phase-03-api` (branch phase-03-api, HEAD a446c64). It adds: EF Core SQLite persistence (src/MortgageApplications.Infrastructure: DbContext, configurations, owner-scoped repository, AffordabilityResultJson value converter, MinimumScaleTwoConverter, initial migration, design-time factory), the Core use case MortgageApplicationService and ApplicationInputRules, and the HTTP API (src/MortgageApplications.Api: four endpoints under /api/applications, CreateApplicationRequest with AddValidation + IValidatableObject, strict JSON options, ProblemExceptionHandler, ValidationErrorKeys, AffordabilityOptionsValidator with ValidateOnStart, DevelopmentCurrentUser, OpenAPI + Scalar in Development only, startup migration in Development only), new packages in Directory.Packages.props, a repo-root dotnet-tools.json, and tests.

  Run your scanners: gitleaks on history and working tree, semgrep with p/csharp and p/secrets (--metrics=off, --error, --json; count results from the JSON, never read $? after a pipe; add --baseline-commit main for the diff run), and the dotnet vulnerable-package scan including transitive packages. Then review the diff against OWASP Top 10:2025 and the repo rules in .claude/rules/security.md, AGENTS.md and docs/spec/affordability-rules.md sections 1 and 6.

  Focus on: broken access control (every read scoped to the owner, assessments only through the owner's application, identical 404 bodies for unknown and foreign IDs, owner never taken from or returned in a body); mass assignment and input handling (strict JSON, numbers as strings, decimal overflow or extreme values reaching the calculator, the minimum-loan rule); information leaks (exception text, stack traces or submitted values in 400/404/415/500 bodies in Development and Production; ToString or JSON serialisation of entities; anything personal in logs, noting Phase 5 owns logging); configuration (options fail closed at startup, connection string, OpenAPI/Scalar exposure outside Development, startup migrations outside Development); the BadHttpRequestException status mapping and the SuppressDiagnosticsCallback choice; supply chain (the five new packages and their versions, the dotnet-ef tool manifest). Known accepted items, don't re-raise unless the code makes them worse: DevelopmentCurrentUser is a fixed owner until Phase 7 (builds before Phase 7 are not deployed), no rate limiting until Phase 8 (security-auditor S6 accepted risk), version 7 IDs reveal creation time (Phase 2 S6).

  Report each finding with severity, file:line, evidence, and whether it's real or a false positive with the reason. Say plainly which scanners ran and which did not.
  ```
- **Purpose:** Phase 3 security gate.
- **Outcome:** PASS, no high or critical findings; 3 low (L1–L3) and 3 info (I1–I3). L1, L2 and I2 fixed, I3 test added, L3 and I1 deferred (phase file, "Security gate").

### P03-08 · agent→subagent (`dotnet-reviewer`) · 2026-11-14
- **Prompt (verbatim):**
  ```
  Fresh-context review of Phase 3 of a .NET 10 mortgage-application API: `git diff main...phase-03-api` (HEAD a446c64). Read docs/progress/phase-03-api.md first (plan, approved decisions P3-D1 to P3-D8, the A1–A12 answers, "Docs verified", and "Implementation notes", which explain why the assessment result is a JSON value-converter column instead of an EF complex type). Rules: AGENTS.md, .claude/rules/backend.md, .claude/rules/tests.md, tests/CLAUDE.md, src/MortgageApplications.Core/CLAUDE.md, docs/spec/affordability-rules.md.

  Flag only correctness, security, framework-misuse and requirement gaps, plus test gaps where an approved case isn't really proven. Specifically check:
  1. EF Core: the value converter and comparer for AffordabilityResult (null handling, decimal scale and enum round trip, change tracking), MinimumScaleTwoConverter applied to every decimal (any value it could change, any query it could break), constructor binding of MortgageApplication, the repository's owner filter and the assessment subquery, AsNoTracking use, cascade delete, the migration vs the configurations, DateTimeOffset on SQLite.
  2. Minimal APIs: AddValidation behaviour with nullable value-type properties, IValidatableObject running only after attribute validation, the ValidationErrorKeys rename (does it hit every validation response, any key collision), TypedResults and Results unions, Created Location values, the {id:guid} constraint, ProblemExceptionHandler (BadHttpRequestException status, nothing leaked, the response already started case), UseStatusCodePages ordering, SuppressDiagnosticsCallback, strict JSON options and their effect on OpenAPI or other bodies, DI lifetimes (calculator singleton from IOptions, scoped service and DbContext), ValidateOnStart wiring.
  3. Core: ApplicationInputRules vs InputGuard parity and the A6 rule, the service reading the clock once, any way an invalid or foreign request can create rows.
  4. Tests: does each approved case (phase file "Proposal" selection) actually prove what it claims? In particular the cross-owner test sharing one in-memory database across WithWebHostBuilder hosts, the startup-failure tests (does Sources.Clear really remove the Affordability section, or do they pass for another reason?), the raw-JSON scale asserts, and the 500 tests in both environments.
  Re-run any arithmetic or behaviour you doubt rather than trusting comments. You may run `dotnet test` with filters and `git` read commands. Report findings as ID | severity | file:line | problem | suggested fix, and say which concerns you checked and found fine.
  ```
- **Purpose:** Phase 3 review gate.
- **Outcome:** 1 medium (F1, a test that compared JSON by value) and 6 low (F2–F7). F1–F5 and F7 fixed; F6 accepted until Phase 7 (phase file, "Review gate").

### P03-09 · agent→subagent (`spec-checker`) · 2026-11-14
- **Prompt (verbatim):**
  ```
  Requirement traceability at the end of Phase 3 (branch phase-03-api, HEAD a446c64). Source of truth: docs/spec/ORIGINAL_SPEC.md. Previous snapshot: the "Requirement traceability" table in docs/PROGRESS.md (R1–R55, end of Phase 2: 1 met, 28 partial, 26 missing). Phase 3 plan and results: docs/progress/phase-03-api.md.

  Re-check every row against the repository as it is now (src/, tests/, docs/). Status key: "met" needs code or a test; design written down but not built is "partial". For each row give ID | status | where (file and test that prove it) | change since Phase 2. Rows expected to move are R1, R2, R3, R4, R5–R9, R10, R11, R12, R13, R21, R22, R23, R24, R26, R27, R29, but judge each one yourself and don't promote a row on the plan's word alone. Then give the new totals, and list any requirement where the code and the spec disagree or where you're unsure.
  ```
- **Purpose:** Phase 3 requirement traceability.
- **Outcome:** 26 met, 9 partial, 20 missing (Phase 2: 1 / 28 / 26). New table in `docs/PROGRESS.md`.

### P03-10 · agent→subagent (`/security-review` skill, finder sub-task, general-purpose) · 2026-11-14
- **Prompt:** the bundled `/security-review` instructions (objective, critical instructions, categories, methodology, output, confidence scoring), condensed into one prompt with the same content. The context paragraph, verbatim:
  ```
  Context: Phase 3 of a .NET 10 mortgage-application API. The PR adds EF Core SQLite persistence (src/MortgageApplications.Infrastructure: DbContext, configurations, owner-scoped repository, a JSON value converter for the assessment result, a decimal scale converter, the initial migration, a design-time factory), the Core use case MortgageApplicationService and ApplicationInputRules, and the HTTP API (src/MortgageApplications.Api: POST/GET /api/applications, POST/GET /api/applications/{id}/assessments, a request record with AddValidation + IValidatableObject, strict JSON options, ProblemExceptionHandler, ValidationErrorKeys, startup options validation, DevelopmentCurrentUser as a fixed owner until Phase 7, OpenAPI + Scalar and startup migrations in Development only), packages, and tests. A security-auditor subagent and a dotnet-reviewer already reviewed this branch (docs/progress/phase-03-api.md, "Security gate" and "Review gate"); their fixes are in the diff. Accepted and documented, do not report: the fixed DevelopmentCurrentUser with no RequireAuthorization until Phase 7 (builds before Phase 7 are not deployed), no rate limiting until Phase 8, version 7 IDs revealing creation time. Rules: .claude/rules/security.md, AGENTS.md, docs/spec/affordability-rules.md section 6.
  ```
- **Purpose:** local AI security review of PR #6 on Alessandro's subscription.
- **Outcome:** no findings at confidence 8 or above (it listed 9 attack surfaces checked and why each holds), so no false-positive filter sub-tasks ran.
- `/code-review` (high effort) on PR #6 ran inline in the lead agent, with no subagent: 7 findings, all low and none a correctness bug (1 altitude, 1 reuse, 1 conventions, 2 simplification, 2 efficiency). Resolutions in the phase file, "Review gate".

### P03-11 · human→agent · 2026-11-14
- **Prompt (verbatim):**
  > Ok, what now?
- **Purpose:** status question at Checkpoint B.
- **Outcome:** CI had passed on PR #6; the agent listed the review, the open decisions and the `approved` step.

### P03-12 · human→agent · 2026-11-14
- **Prompt (verbatim):**
  > Proceed
- **Purpose:** Checkpoint B approval of Phase 3 (treated as `approved`, as "Proceed" was in Phase 1).
- **Outcome:**
  - PR #6 squash-merged as `a187e29` with a written message.
  - The open decisions (`DevelopmentCurrentUser` until Phase 7, the `backend.md` DTO-projection rule, an optional ADR) were not answered, so they stay open.

## Phase 04: Docker (run in parallel with Phase 6)

Copied from `docs/progress/phase-04-docker.md` after the merge. The human prompts P04-01 to P04-13 cover both Phase 4 and Phase 6; the Phase 6 subagent prompts (P06-01 to P06-08) are copied after the Phase 6 merge.

### P04-01 · human→agent · 2026-11-14
- **Prompt (verbatim):**
  > continue with next steps
- **Purpose:** start the next step from `docs/PROGRESS.md` ("Resume here": Phase 4 and Phase 6 in parallel).
- **Outcome:** the agent read the progress files and the code, checked which files each phase would touch, and wrote both plans with their decisions (Checkpoint A).

### P04-02 · human→agent · 2026-11-15
- **Prompt (verbatim):**
  > Try again
- **Purpose:** resume after the previous turn stopped before the Phase 6 plan was written.
- **Outcome:** the Phase 6 plan was written; both plans presented at Checkpoint A.

### P04-03 · human→agent · 2026-11-15
- **Prompt (verbatim):**
  > Proceed
- **Purpose:** Checkpoint A approval of both plans with the recommended options (P4-D1 to P4-D4, P6-D1 to P6-D4).
- **Outcome:** decisions recorded; docs check; `test-designer` run for both phases (P04-04, P06-01).

### P04-04 · agent→subagent (`test-designer`) · 2026-11-15
- **Prompt (verbatim):**
  ```
  Phase 4 (Docker) of a .NET 10 mortgage-application API (Minimal APIs + EF Core SQLite). Propose test cases and checks for the approved plan; do not write code.

  Read first: docs/progress/phase-04-docker.md (goal, plan, approved decisions P4-D1 to P4-D4), .claude/rules/infra.md, .claude/rules/security.md, .claude/rules/tests.md, tests/CLAUDE.md, .dockerignore, .github/workflows/ci.yml, src/MortgageApplications.Api/Program.cs, src/MortgageApplications.Api/appsettings.json, src/MortgageApplications.Infrastructure/DatabaseMigration.cs, and tests/MortgageApplications.IntegrationTests/StartupTests.cs (what startup behaviour is already covered).

  What will be built (not written yet):
  - A multi-stage Dockerfile (mcr.microsoft.com/dotnet/sdk:10.0 → aspnet:10.0, image tags not digests), restore layer from the csproj/props/global.json/slnx files, publish the Api project in Release, /app/data created and owned by $APP_UID, USER $APP_UID, EXPOSE 8080.
  - compose.yaml: one service "api" built from ".", port 8080:8080, ASPNETCORE_ENVIRONMENT=Development (so the existing Development-only startup migration runs, decision P4-D1), ConnectionStrings__MortgageApplications=Data Source=/app/data/mortgage-applications.db, named volume data:/app/data. No Aspire dashboard (Phase 5).
  - A new CI job "docker": permissions contents: read, pinned actions/checkout, docker build, run the container, poll GET /health with a 60 s timeout, fail the job otherwise, always stop the container.
  - No .NET code change is planned. The checks are mostly shell/CI checks, not xUnit tests.

  Cover at least: the image builds from a clean clone; the container runs as a non-root user (uid check); /health returns 200 on 8080; a POST then GET /api/applications/{id} round trip works inside the container (schema exists, so the startup migration ran); data survives docker compose down + up but not down -v; no *.db, .env*, *.pem, tests/ or docs/ in the image; the CI job fails if the container never becomes healthy and cleans up either way; the published app does not need the SDK at runtime. Also say whether any xUnit test is worth adding (for example a test that the startup migration does not run outside Development, if not already covered) and why.

  Use the agent's output format. Mark each case must or nice, keep the list tight (no redundant cases), say for each where it runs (local fresh-clone run, CI job, or xUnit), and list any ambiguities in the plan you need Alessandro to settle.
  ```
- **Purpose:** propose Phase 4 checks before anything is built.
- **Approved:** see P04-05.
- **Outcome:** 16 container/CI checks (C1–C16; C17 folded into ambiguity 2), 1 optional xUnit test (X1) and 6 ambiguities. The lead agent checked ambiguity 1 against the code (`MortgageApplications.slnx` lists both test projects; `.dockerignore` excludes `tests/`). Recommended selection and answers are in "Tests", "Proposal".

### P04-05 · human→agent · 2026-11-15
- **Prompt (verbatim):**
  > Ok add it and proceed (yes)
- **Purpose:** approval of the recommended test selections and answers for Phase 4 and Phase 6; "add it" is read as adding `failedAt` to `AssessmentResponse` (Phase 6, Q11), the last item the agent recommended adding.
- **Outcome:** both phase branches created with their plan files; `parallel-implementer` started for each (P04-06, P06-02).

### P04-06 · agent→subagent (`parallel-implementer`) · 2026-11-15
- **Prompt (verbatim):**
  ```
  Implement Phase 4 (Docker) of this .NET 10 mortgage-application API, exactly as approved.

  Branch: switch to the existing branch phase-04-docker (it holds one commit with the approved plan). Do not create another branch. Never push.

  Scope: docs/progress/phase-04-docker.md. The sections "Parallel safety with Phase 6", "Plan (approved at Checkpoint A)", "Decisions" (P4-D1 to P4-D4), "Docs verified" and "Tests > Proposal" (the recommended checks C1-C11, C13, C14, C16 and the answers to ambiguities 1-6, all approved) are your scope. Read AGENTS.md, CLAUDE.md, .claude/rules/infra.md, .claude/rules/security.md first.

  Key points from the approved answers (the phase file is the source of truth if anything differs):
  - Dockerfile: sdk:10.0 build stage, aspnet:10.0 runtime (tags, not digests). Restore and publish src/MortgageApplications.Api/MortgageApplications.Api.csproj only, after copying the three src .csproj files, Directory.Build.props, Directory.Packages.props and global.json (the .slnx lists test projects that .dockerignore excludes). Runtime: create /app/data owned by $APP_UID, USER $APP_UID, EXPOSE 8080. No secrets, no ENV with connection strings.
  - compose.yaml: service api, build ".", ports "127.0.0.1:8080:8080", ASPNETCORE_ENVIRONMENT=Development, ConnectionStrings__MortgageApplications=Data Source=/app/data/mortgage-applications.db, named volume data:/app/data. No Aspire dashboard.
  - CI: new job "docker" in .github/workflows/ci.yml: job-level permissions contents: read, the same pinned actions/checkout SHA as the existing job with persist-credentials: false, timeout-minutes, `docker compose up -d --build`, a poll script on GET /health (60 s timeout, fails non-zero and prints `docker compose logs` on timeout or if the container exits), then the C4/C5 POST + GET round trip, and a final `if: always()` step running `docker compose down -v`. No secrets, no login, no push.
  - No .NET code changes. Do not edit src/, tests/, Program.cs, docs/PROGRESS.md, docs/ai/*, or any ADR file (ADR 0002 is written later by the lead agent). Only the phase file under docs/.

  Process:
  1. Write the files in small conventional commits (scopes infra, ci). End each commit message with: Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
  2. Run ./scripts/verify.sh (full) and keep the final output (it must still pass).
  3. Check whether Docker is reachable with `docker version`. If it is: run the approved LOCAL checks in a fresh clone of this branch in a new empty directory under /private/tmp/claude-501/ (clone from this repository's path, check out phase-04-docker), and record each check's command and result (C1-C11, C14, with the C9 planted files created only in that clone). Clean up containers, images you built and volumes afterwards. Never print request bodies containing amounts into the phase file beyond what the check needs. If Docker is NOT reachable: do not try to start it, do not wait for it; mark every LOCAL check as "not run: Docker daemon not available" and continue.
  4. Do the C13 static review of the workflow yourself and record it.
  5. In the phase file, fill "What was built (files)", "Tests" > a new "Results" subsection (one row per check: run / not run, command, result) and "Open issues". Commit that.

  Report: files changed, commits, trimmed verify output, the result of each check (or "not run" and why), every place you deviated from the phase file and why. If the plan is ambiguous or a step fails three times, stop and report instead of guessing.
  ```
- **Purpose:** implement the approved Phase 4 plan in its own worktree.
- **Outcome:** 4 commits (`7e83532`, `aeb932d`, `ee9c8ef`, `ce46e8f`); full verify passed; LOCAL container checks marked not run (no Docker daemon). See "Results".

### P04-07 · agent→subagent (`security-auditor`) · 2026-11-15
- **Prompt (verbatim):**
  ```
  Security gate for Phase 4 (Docker) of a .NET 10 mortgage-application API. Review only the branch diff: run `git diff main...phase-04-docker` from the repository root (the branch is checked out in another worktree; do not switch branches here). Files: Dockerfile, compose.yaml, .github/workflows/ci.yml (new `docker` job), docs/progress/phase-04-docker.md.

  Read first: .claude/rules/security.md, .claude/rules/infra.md, .dockerignore, docs/progress/phase-04-docker.md (plan, decisions P4-D1 to P4-D4, approved answers to ambiguities 1-6).

  Check at least: the image runs as non-root (`USER $APP_UID` after every root step) and nothing writes outside /app/data at run time; no secrets, connection strings or keys in any layer, ENV, ARG or build context (`COPY src/ src/` scope against .dockerignore: *.db, .env*, *.pem, bin/obj); compose binds to 127.0.0.1 only and the ADR-to-be says Development in compose exposes Scalar/OpenAPI without auth until Phase 7; the CI job: least-privilege permissions, pinned actions, persist-credentials false, no `${{ }}` expressions inside `run:` blocks (script injection), no secrets, no push/login, cleanup with `if: always()`, request bodies with amounts not printed to logs; supply chain of base images (tags not digests, P4-D4 accepted for now: say whether that acceptance is reasonable or should be raised now). Run Gitleaks on the branch commits (`gitleaks git --log-opts="main..phase-04-docker"` or the closest available form) and Semgrep on the changed files if the tools are installed; say which ran and which were not available. Do not install anything.

  Use your output format: each finding with severity, file:line, evidence, real or false positive, and a proposed fix or justification. Do not edit files.
  ```
- **Purpose:** security gate on the Phase 4 diff.
- **Outcome:** pass, 11 rows (S1–S11). See "Security gate".

### P04-08 · agent→subagent (`dotnet-reviewer`) · 2026-11-15
- **Prompt (verbatim):**
  ```
  Fresh-context review of Phase 4 (Docker) of a .NET 10 mortgage-application API. Review only the branch diff: run `git diff main...phase-04-docker` from the repository root (the branch is checked out in another worktree; do not switch branches here). Files: Dockerfile, compose.yaml, .github/workflows/ci.yml (new `docker` job), docs/progress/phase-04-docker.md.

  Read first: AGENTS.md, .claude/rules/infra.md, docs/progress/phase-04-docker.md (goal, plan, decisions P4-D1 to P4-D4, "Docs verified", approved checks and answers), src/MortgageApplications.Api/Program.cs, src/MortgageApplications.Api/appsettings.json, src/MortgageApplications.Infrastructure/DatabaseMigration.cs, global.json, Directory.Build.props, .dockerignore.

  Flag only correctness, security and requirement gaps, for example: will `dotnet restore` of the Api csproj alone succeed with central package management and Directory.Build.props as copied (anything else the build reads, such as a NuGet.config, analyzer config or .editorconfig with warnings-as-errors, missing from the build context so the image build behaves differently from verify.sh?); does `chown "$APP_UID"` expand in that RUN; does the named volume inherit /app/data ownership on first use; does the compose environment make the app migrate on startup and find the database; does the CI poll script really fail on every failure path (curl errors, container exit, timeout) and never exit 0 early; is the POST body valid for the API's strict JSON and validation rules; does the phase file's "Results" section claim anything that was not run (the Docker daemon was not available, so the LOCAL checks must be marked not run). Report each finding with severity, file:line, evidence and a proposed fix. Do not edit files.
  ```
- **Purpose:** fresh-context review of the Phase 4 diff.
- **Outcome:** 9 findings (R1–R9), none critical or high. See "Review gate".

### P04-09 · human→agent · 2026-11-15
- **Prompt (verbatim):**
  > I hit my usage limit while you were working, but it has reset now. Please continue from where you left off.
- **Purpose:** resume after the usage limit stopped the Phase 6 fix run (P06-06).
- **Outcome:** the fix run was restarted (P06-07); the lead agent checked its commits, re-ran the full verify (376 tests) and presented seven open decisions plus the C14 CI-break question.

### P04-10 · human→agent · 2026-11-15
- **Prompt (verbatim):**
  > Proceed, also I have a question, does this also mind when two users do the same at the same time? Like security stuff like these...
- **Purpose:** approve the recommended answer to all seven open decisions (Phase 6: accept S1 until Phase 8, fix the clock (R4), retry the startup sweep (R3), record the missing concurrency token in ADR 0003, accept the worker-only methods with comments, keep the Q5 catch; both phases: the agent drafts ADR 0002 and ADR 0003) and the deliberate CI break for C14; plus a question on concurrent users.
- **Outcome:** R3 and R4 fixed on `phase-06-background`; ADR 0002 and ADR 0003 drafted; the question answered in chat.

### P04-11 · agent→subagent (`spec-checker`) · 2026-11-15
- **Prompt (verbatim):**
  ```
  Spec check for Phase 4 (Docker) of a .NET 10 mortgage-application API. Read only; do not edit anything.

  Read the branch files in the worktree /Users/ale_ssd/Developer/Mortgage-Application-API-test/.claude/worktrees/agent-afcd22232b922d159 (branch phase-04-docker): docs/spec/ORIGINAL_SPEC.md, docs/progress/phase-04-docker.md (goal, plan, decisions P4-D1 to P4-D4, approved checks C1-C16 and answers), docs/adr/0002-container-runtime-and-startup-migrations.md, Dockerfile, compose.yaml, .dockerignore, .github/workflows/ci.yml, .claude/rules/infra.md, .claude/rules/security.md, and the traceability notes in docs/progress/phase-01-spec.md for the requirement IDs.

  Map every spec requirement and every Phase 4 goal and approved check that this phase touches (containerisation, configuration, startup migrations, CI, security constraints such as non-root, no secrets, loopback bind) to the file or test or CI step where it is met. Mark each met / partial / missing, with a one-line reason and the file path. Say which checks are LOCAL only (run by the lead agent, recorded in the phase file) and which run in CI. List anything the phase file claims that you could not find in the files. Keep it short: one table plus at most 5 notes.
  ```
- **Purpose:** requirement traceability before the PR.
- **Outcome:** see "Review gate", spec check.

### P04-12 · agent→subagent (`general-purpose`, from the `/security-review` skill) · 2026-11-15
- **Prompt (verbatim):**
  ```
  Security review sub-task for PR #7 (Phase 4, Docker) of a .NET 10 mortgage-application API. Read only: do not edit files and do not run Docker.

  Repository worktree: /Users/ale_ssd/Developer/Mortgage-Application-API-test/.claude/worktrees/agent-afcd22232b922d159 (branch phase-04-docker). The diff to review: run `git diff main...HEAD` there. Changed files: .claude/rules/infra.md, .dockerignore, .github/workflows/ci.yml, Dockerfile, compose.yaml, docs/adr/0002-container-runtime-and-startup-migrations.md, docs/progress/phase-04-docker.md. Context to read: src/MortgageApplications.Api/Program.cs, src/MortgageApplications.Api/appsettings*.json, .claude/rules/security.md, AGENTS.md.

  OBJECTIVE: identify HIGH-CONFIDENCE security vulnerabilities newly added by this PR that could have real exploitation potential. Not a general code review. Do not comment on existing security concerns.
  1. Minimise false positives: only flag issues where you are >80% confident of actual exploitability.
  2. Skip theoretical issues, style concerns and low-impact findings.
  3. Prioritise vulnerabilities that could lead to unauthorized access, data breaches or system compromise.
  4. Do NOT report: denial of service; secrets or sensitive data stored on disk; rate limiting or resource exhaustion.

  Categories to examine: input validation (SQL/command/XXE/template/NoSQL injection, path traversal); authentication and authorization (bypass, privilege escalation, sessions, JWT); crypto and secrets (hardcoded keys, weak crypto, key storage, randomness, certificate validation); injection and code execution (deserialization, YAML, eval, XSS); data exposure (sensitive data logging or storage, PII, endpoint data leakage, debug information exposure). Even if only exploitable from the local network, it can be HIGH.

  Method: (1) research the repository's security model and existing patterns; (2) compare the new changes against them and look for new attack surface; (3) assess each modified file, trace data from inputs to sensitive operations, look for privilege boundaries crossed unsafely.

  For each candidate give: file, line, severity (HIGH / MEDIUM / LOW), category slug, description, exploit scenario, fix recommendation, and a confidence from 1 to 10. Focus on HIGH and MEDIUM; MEDIUM only if obvious and concrete. If there are no candidates, say so plainly. Keep the report short.
  ```
- **Purpose:** the find step of the local `/security-review` on PR #7.
- **Outcome:** no HIGH or MEDIUM candidates, so the skill's false-positive filter step had nothing to run on.

### P04-13 · human→agent · 2026-11-15
- **Prompt (verbatim):**
  > Yes proceed! What about phase 5?
- **Purpose:** Checkpoint B approval of Phase 4, and a question on when Phase 5 runs.
- **Outcome:** PR #7 squash-merged as `ede3605` after both CI jobs passed on `4651e34`; Phase 6 is rebased next. Phase 5 answered in chat.

## Phase 06: Background assessments (run in parallel with Phase 4)

Copied from `docs/progress/phase-06-background.md` after the merge. The human prompts for planning and approval are P04-01 to P04-13 above.

### P06-01 · agent→subagent (`test-designer`) · 2026-11-15
- **Prompt (verbatim):**
  ```
  Phase 6 (background assessments) of a .NET 10 mortgage-application API (Minimal APIs + EF Core SQLite). Propose test cases for the approved plan; do not write code.

  Read first: docs/progress/phase-06-background.md (goal, plan, approved decisions P6-D1 to P6-D4), docs/spec/affordability-rules.md (section 8 golden examples), .claude/rules/tests.md, tests/CLAUDE.md, .claude/rules/security.md, and the current code and tests: src/MortgageApplications.Core/Applications/ (MortgageApplicationService, AffordabilityAssessment, IMortgageApplicationRepository), src/MortgageApplications.Infrastructure/Persistence/EfMortgageApplicationRepository.cs, src/MortgageApplications.Api/Applications/ApplicationEndpoints.cs, src/MortgageApplications.Api/Program.cs, tests/MortgageApplications.IntegrationTests/ (ApiFactory, AssessmentTests, NotFoundAndOwnerTests) and tests/MortgageApplications.UnitTests/Applications/ (Fakes, MortgageApplicationServiceTests, AffordabilityAssessmentTests). Phase 2 and 3 already cover the calculator, the entity state machine and the synchronous endpoint: don't repeat those, but say which existing tests must change.

  What will be built (not written yet):
  - Core: IAssessmentQueue (port) with an async enqueue of an assessment ID; MortgageApplicationService.AssessAsync saves a Pending assessment, then enqueues its ID, and returns the Pending assessment; a new use case ProcessAssessmentAsync(Guid assessmentId) that loads the assessment and its application's inputs without an owner filter (the ID comes from our own queue), returns without change unless the assessment is Pending, otherwise calls Complete with the calculator result, or Fail when the calculation throws, and saves; repository additions for that load, an update, and listing Pending assessment IDs.
  - Api: ChannelAssessmentQueue (singleton, Channel.CreateBounded with BoundedChannelFullMode.Wait, WriteAsync honouring the request's cancellation token, capacity from configuration, default 100); AssessmentWorker : BackgroundService that first re-enqueues every Pending assessment from the database, then reads the channel one item at a time with a new DI scope per item, catches and logs per-item exceptions without personal data and keeps going, and stops on the stopping token; POST /api/applications/{id}/assessments returns 202 Accepted + Location of GET /api/applications/{id}/assessments/{assessmentId} with body status Pending and result null; GET of an assessment is always 200 with a status field (P6-D4).
  - Tests: ApiFactory moves to a named shared-cache in-memory SQLite database per factory with one keep-alive connection and one connection per DbContext (P6-D3); integration tests poll the Location with a timeout.

  Cover at least: 202 + Location + Pending body; polling reaches Completed with golden E1 values within a timeout; a Declined case completes (not Failed); unknown, malformed or another owner's application id → 404 and nothing queued and nothing saved; idempotency (the same ID processed twice, or an already Completed or Failed assessment, is not changed); calculation exception → Failed with FailedAt, and the worker processes the next item afterwards; Pending rows present at startup are processed; enqueue order (save before enqueue, so a crash between them leaves a recoverable Pending row); full queue: WriteAsync waits and nothing is dropped; cancellation of the POST while waiting leaves the saved row Pending; shutdown during processing does not mark the item Failed; ProcessAssessmentAsync with an unknown ID does nothing and does not throw; no income, expenses, deposit or property value in worker log messages; tests are not timing-flaky (say how: polling helper, TimeProvider, deterministic fakes). Say which cases belong in unit tests (Core with fakes) versus integration tests.

  Use the agent's output format. Mark each case must or nice, keep the list tight (no redundant cases), and list any ambiguities in the plan you need Alessandro to settle.
  ```
- **Purpose:** propose Phase 6 test cases before any test is written.
- **Outcome:** 17 unit, 4 repository, 15 integration and 6 worker-level cases, a list of existing tests that must change, and 12 questions (Q1–Q12). The lead agent checked Q8, Q10 and Q11 against the code and Learn (see "Tests", "Proposal"). Q10's claim that a busy timeout does not cover shared-cache locks is contradicted by Learn: Microsoft.Data.Sqlite retries both busy and locked errors until the command timeout.

### P06-02 · agent→subagent (`parallel-implementer`) · 2026-11-15
- **Prompt (verbatim):**
  ```
  Implement Phase 6 (background assessments) of this .NET 10 mortgage-application API, exactly as approved.

  Branch: switch to the existing branch phase-06-background (it holds one commit with the approved plan). Do not create another branch. Never push.

  Scope: docs/progress/phase-06-background.md. The sections "Plan (approved at Checkpoint A)", "Decisions" (P6-D1 to P6-D4), "Docs verified" and "Tests > Proposal" (the recommended selection and the answers to Q1-Q12, all approved) are your scope. Read AGENTS.md, CLAUDE.md, .claude/rules/*.md, tests/CLAUDE.md and src/MortgageApplications.Core/CLAUDE.md first.

  Key points from the approved answers (the phase file is the source of truth if anything differs):
  - IAssessmentQueue in Core; ChannelAssessmentQueue and AssessmentWorker in Api. Bounded channel, BoundedChannelFullMode.Wait, WriteAsync with the request's cancellation token. Capacity from `Assessments:QueueCapacity` (default 100, validated at startup 1 to 10,000, like AffordabilityOptions).
  - AssessAsync saves Pending, then enqueues (in that order). ProcessAssessmentAsync(Guid) loads the assessment plus the application's AffordabilityInputs without an owner filter, returns unless Pending, catches only the calculation (Fail on a calculation error), saves. A failure while saving propagates to the worker, which logs it and leaves the row Pending. No new calculator interface (Q1).
  - The worker re-enqueues all Pending IDs at startup, then reads one item at a time with a new async DI scope per item, catches and logs per item (assessment ID only, never amounts or owner ID), and treats cancellation on the stopping token as shutdown, never as Failed. An exception escaping ExecuteAsync stops the host (Learn link in the phase file), so nothing but shutdown may leave the loop.
  - POST /api/applications/{id}/assessments → 202 Accepted (TypedResults.Accepted<AssessmentResponse>) + Location of GET …/assessments/{assessmentId}. GET is always 200. AssessmentResponse gains nullable failedAt; no failure reason.
  - ApiFactory: named shared-cache in-memory SQLite (`Data Source=<guid>;Mode=Memory;Cache=Shared`), one keep-alive connection, one connection per DbContext. The worker runs in the factory's default host; WithWebHostBuilder-derived hosts remove it unless a test opts in.
  - If ListPending needs an index, add a new migration; never edit InitialCreate (a hook blocks it).
  - No new packages. No logging of income, expenses, deposit, property value, tokens or the owner ID.

  Do not edit: Dockerfile, compose.yaml, .dockerignore, .github/, docs/PROGRESS.md, docs/ai/*, any ADR file (ADR 0003 is written later by the lead agent). Only the phase file under docs/.

  Process:
  1. Test-first: write the approved tests and the changes to existing tests; run them and confirm the new ones fail for the right reason. Keep that output.
  2. Implement until green. Small conventional commits (AGENTS.md "Commits and PRs"; scopes core, api, infra, tests). End each commit message with: Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
  3. Run ./scripts/verify.sh (full) and keep the final output. Then run the integration tests 5 times in a row (dotnet test --project tests/MortgageApplications.IntegrationTests) and record pass/fail for each run. If any run flakes on SQLite locking, stop and report (Q10 says that goes back to Alessandro); do not switch to a temp-file database yourself.
  4. In the phase file, fill "What was built (files)", "Tests" > a new "Results" subsection (added/total/passing, the red-then-green evidence, the 5-run result) and "Open issues" for anything you could not do. Commit that.

  Report: files changed, commits, trimmed verify output, the 5-run result, every place you deviated from the phase file and why, and anything you could not do. If the plan is ambiguous or a step fails three times, stop and report instead of guessing.
  ```
- **Purpose:** implement the approved Phase 6 plan, test-first, in its own worktree.
- **Outcome:** stopped at its 60-turn limit after 4 commits (`4a8ed68`, `e531691`, `d249652`, `7215f8d`) with a clean tree.

### P06-03 · agent→subagent (`parallel-implementer`, resumed) · 2026-11-15
- **Prompt (verbatim):**
  ```
  Finish Phase 6: you hit the turn limit after 4 commits (4a8ed68, e531691, d249652, 7215f8d) and a clean tree. Do only the remaining steps from your original instructions, with no new features:
  1. If any approved must-case from the phase file is still missing, list it (do not write it now unless it is a few lines).
  2. Run ./scripts/verify.sh (full) and keep the final output.
  3. Run the integration tests 5 times in a row and record pass/fail for each run. If a run flakes on SQLite locking, stop and report.
  4. Fill the phase file: "What was built (files)", "Tests" > "Results" (added/total/passing, red-then-green evidence if you kept it, otherwise say it was not kept, plus the 5-run result), and "Open issues". Commit that.
  5. Deliver your report: files changed, commits, trimmed verify output, the 5-run result, every deviation from the phase file and why, and anything not done. Keep tool use tight; you have limited turns.
  ```
- **Purpose:** finish the remaining steps after the turn limit, with no new features.
- **Outcome:** full verify passed (373 tests); integration tests passed 5 runs in a row; phase-file sections "What was built", "Results" and "Open issues" written (commit `850a3a8`).

### P06-04 · agent→subagent (`security-auditor`) · 2026-11-15
- **Prompt (verbatim):**
  ```
  Security gate for Phase 6 (background assessments) of a .NET 10 mortgage-application API. Review only the branch diff: run `git diff main...phase-06-background` from the repository root (the branch is checked out in another worktree; do not switch branches here; read branch files with `git show phase-06-background:<path>`).

  Read first: .claude/rules/security.md, AGENTS.md, docs/progress/phase-06-background.md on the branch (plan, decisions P6-D1 to P6-D4, approved answers Q1-Q12, "Open issues" with the implementer's deviations).

  Check at least: the non-owner-scoped processing load (`ProcessAssessmentAsync` and the repository method behind it) is reachable only from `AssessmentWorker`, never from an endpoint or with a caller-supplied ID; POST and GET keep owner scoping and return identical 404s for unknown, other-owner and malformed IDs, with nothing saved or queued on 404; no log message, log state, exception message or ProblemDetails carries income, expenses, deposit, property value or the owner ID (check every `[LoggerMessage]` and every logged exception path); `AssessmentResponse.failedAt` exposes no failure reason; the bounded queue cannot grow without limit and `Assessments:QueueCapacity` is validated at startup; a POST waiting on a full queue cannot be used to exhaust resources beyond what the capacity and request cancellation allow (say what the remaining denial-of-service exposure is and whether it belongs to Phase 8 rate limiting); the worker cannot be stopped by one bad item; shutdown never marks Failed; any `CA1711` or other analyzer suppressions are justified. Run Gitleaks on the branch commits (`gitleaks git --log-opts="main..phase-06-background"` or closest form), Semgrep (`p/csharp`, `p/secrets`) on the changed `.cs` files from the branch worktree at /Users/ale_ssd/Developer/Mortgage-Application-API-test/.claude/worktrees/agent-acd40a009ddf8a3cd, and `dotnet list package --vulnerable --include-transitive` there; say which tools ran and which were not available. Do not install anything.

  Use your output format: each finding with severity, file:line, evidence, real or false positive, and a proposed fix or justification. Do not edit files.
  ```
- **Purpose:** security gate on the branch diff.
- **Outcome:** PASS. One medium finding (S1), two low and three info notes. See "Security gate".

### P06-05 · agent→subagent (`dotnet-reviewer`) · 2026-11-15
- **Prompt (verbatim):**
  ```
  Fresh-context review of Phase 6 (background assessments) of a .NET 10 mortgage-application API. Review only the branch diff: run `git diff main...phase-06-background` from the repository root (the branch is checked out in the worktree /Users/ale_ssd/Developer/Mortgage-Application-API-test/.claude/worktrees/agent-acd40a009ddf8a3cd; read files there; do not switch branches or edit anything).

  Read first: AGENTS.md, .claude/rules/backend.md, .claude/rules/tests.md, tests/CLAUDE.md, and docs/progress/phase-06-background.md on the branch (goal, plan, decisions P6-D1 to P6-D4, "Docs verified", approved tests and answers Q1-Q12, "Results" and "Open issues" with the implementer's six deviations).

  Flag only correctness, security and requirement gaps, for example: EF Core change tracking in the update path (is the loaded assessment tracked, or attached, so `SaveChangesAsync` writes Status, Result, AssessedAt and FailedAt; what the result value comparer costs); whether save-then-enqueue really leaves a recoverable Pending row on every failure path, including a POST cancelled while `WriteAsync` waits; the re-enqueue running alongside the read loop (deviation 5): can it double-process or reorder in a way that breaks idempotency, and is the duplicate skip correct; whether `ProcessAssessmentAsync` catches only the calculation and lets save failures propagate as approved (Q5); whether the worker can ever leave its loop except on shutdown; the clock-before-CreatedAt bug the implementer found (both `Complete` and `Fail` throw): confirm it, and say how likely it is in practice; the `ApiFactory.WithWebHostBuilder` `new` hiding (deviation 4): can a test silently get the wrong host type through the base-class reference; tests that can be timing-flaky despite the 5 clean runs (fixed sleeps, negative waits, races with the worker), and the reduced `[Fact(Timeout)]` coverage (deviation 2); whether every approved must-case from the phase file really has a test that would fail if the behaviour broke; whether the 202 response, `Location` and `failedAt` match P6-D4 and Q11; the `CA1711` suppressions. Report each finding with severity, file:line, evidence and a proposed fix.
  ```
- **Purpose:** fresh-context review gate on the branch diff.
- **Outcome:** no critical or high findings; three medium and seven low (R1–R10). See "Review gate".

### P06-06 · agent→subagent (`parallel-implementer`, resumed) · 2026-11-15
- **Prompt (verbatim):**
  ```
  Phase 6 review gate: fix these five findings on phase-06-background, test-first where a behaviour changes, then run ./scripts/verify.sh (full) and the integration tests 3 times. Do nothing else: no other refactors, no clock fix, no sweep, no concurrency token (those wait for Alessandro).
  R1 (medium): no test drives an exception into AssessmentWorker's per-item catch (AssessmentWorker.cs ~:95). Add an integration test with a repository wrapper whose UpdateAssessmentAsync throws once: the row stays Pending, a ProcessingError log entry has the assessment ID and no amounts or owner ID, and a following POST in the same host still completes.
  R2 (medium, real bug): ChannelAssessmentQueue.EnqueueAsync passes the request token to WriteAsync, which returns cancelled at once if the token is already cancelled, even with free capacity, stranding a saved Pending row. Change to `TryWrite` first, `WriteAsync(id, ct)` only when full. Add a ChannelAssessmentQueueTests case: already-cancelled token, empty queue → the ID is queued.
  R5 (low): I-11 holds the worker before FindForProcessingAsync, so nothing is in memory at shutdown. Add a gate point after Complete has run in memory and before the save (in the update path), stop the host there, assert the row is Pending with AssessedAt and FailedAt null and nothing logged at Error.
  R9 (low): AssessmentWorkerTests ~:116 `Assert.False(third.IsCompleted)` does not prove the POST reached WriteAsync. Also assert the queue's Reader.Count == 1 via a bounded EventuallyAsync.
  R10 (low): U-P10 must also assert exactly one update attempt and that Fail was never called (count attempts in the fake before throwing).
  Commit in small conventional commits (scope tests or api) ending with: Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>. Do not edit the phase file. Report: commits, which new tests failed before the fix (R2 at least), trimmed verify output, the 3-run result.
  ```
- **Purpose:** fix the review findings that needed no decision (R1, R2, R5, R9, R10).
- **Outcome:** the run stopped on the user's usage limit before making any change.

### P06-07 · agent→subagent (`parallel-implementer`, resumed) · 2026-11-15
- **Prompt (verbatim):**
  ```
  Your last run stopped on a rate limit before any change (worktree clean at 850a3a8). Please do the five fixes from my previous message now (R1, R2, R5, R9, R10), exactly as specified there: test-first where behaviour changes, small commits, full verify, integration tests 3 times, no phase-file edits, then report. Keep tool use tight.
  ```
- **Purpose:** restart the P06-06 fixes after the usage limit reset.
- **Outcome:** commits `8339c95` (R10), `37b28b2` (R2) and `b505133` (R1, R5, R9). Only the R2 test failed before its fix (`TaskCanceledException`). Full verify passed (376 tests); integration tests passed 3 runs in a row.

### P06-08 · agent→subagent (`spec-checker`) · 2026-11-15
- **Prompt (verbatim):**
  ```
  Spec check for Phase 6 (background assessments) of a .NET 10 mortgage-application API. Read only; do not edit anything.

  Read the branch files in the worktree /Users/ale_ssd/Developer/Mortgage-Application-API-test/.claude/worktrees/agent-acd40a009ddf8a3cd (branch phase-06-background): docs/spec/ORIGINAL_SPEC.md, docs/spec/affordability-rules.md, docs/progress/phase-06-background.md (goal, plan, decisions P6-D1 to P6-D4, the approved test list and answers Q1-Q12, the security and review gates), docs/adr/0003-background-assessment-queue.md, the traceability notes in docs/progress/phase-01-spec.md and phase-03-api.md for the requirement IDs, and the code under src/ and tests/ that the phase file names.

  Map every spec requirement and every Phase 6 goal and approved must-case this phase touches (assess endpoint now 202 + Location, polling GET, Pending/Completed/Failed, no silent drops, startup recovery, owner scoping and 404s, no personal data in logs, validation at the boundary unchanged) to the file and test that meet it. Mark each met / partial / missing with a one-line reason and the file:line or test name. Check that the spec's original wording about the assessment endpoint is still satisfied by a 202 + polling flow, and say plainly if it is not. List anything the phase file claims that you could not find. Keep it short: one table plus at most 5 notes.
  ```
- **Purpose:** requirement traceability before the PR.
- **Outcome:** see "Review gate", spec check.

### P06-09 · agent→subagent (`security-auditor`) · 2026-11-15
- **Prompt (verbatim):**
  ```
  Security gate re-run for Phase 6 (background assessments) after the rebase on main. Read only: do not edit files or switch branches.

  Worktree: /Users/ale_ssd/Developer/Mortgage-Application-API-test/.claude/worktrees/agent-acd40a009ddf8a3cd (branch phase-06-background, HEAD 45537a2, rebased on origin/main ede3605 + 6859276, which now include Phase 4: Dockerfile, compose.yaml, CI docker job). The first gate (docs/progress/phase-06-background.md, "Security gate", S1-S6) reviewed the diff at the old 850a3a8. Since then: a56a2e3 (TryWrite before WriteAsync), b234be6 and c352351 (tests), 9eae1f1 (clock: finish at the later of now and CreatedAt), 0773b2b (startup re-enqueue retried every Assessments:RequeueRetryDelaySeconds, new validated option, injected TimeProvider and IOptions, changed RequeueFailed log message), and docs.

  Do:
  1. Gitleaks on the branch commits (git log range origin/main..HEAD) and Semgrep (p/csharp, p/secrets) on the files changed in that range; dependency scan with dotnet list package --vulnerable --include-transitive. Say which tools ran; do not install anything.
  2. Review the new commits for: personal data or amounts in any log message or exception that reaches a log (AGENTS.md: never log income, expenses, deposit, property value, tokens or personal data); the retry loop (can it spin, hide errors, or run after shutdown); the new option's validation; owner scoping unchanged; and how Phase 6 behaves in the Phase 4 container (compose runs as Development; the worker runs there; anything in Program.cs that changes what compose exposes).
  3. Re-state S1-S6 only if their status changed.

  Output: a findings table (tool/check, finding with file:line, severity, real or false positive, reason, fix) and a one-word verdict (PASS or FAIL). Keep it short.
  ```
- **Purpose:** security gate re-run after the rebase on `main` (plan step 9).
- **Outcome:** PASS. No new finding above Info; the phase file cited pre-rebase hashes (fixed with the table under "Tests").

### P06-10 · agent→subagent (`dotnet-reviewer`) · 2026-11-15
- **Prompt (verbatim):**
  ```
  Fresh-context review re-run for Phase 6 (background assessments) after the rebase on main. Read only: do not edit files or switch branches.

  Worktree: /Users/ale_ssd/Developer/Mortgage-Application-API-test/.claude/worktrees/agent-acd40a009ddf8a3cd (branch phase-06-background, HEAD 45537a2, rebased on origin/main, which now includes Phase 4: Dockerfile, compose.yaml, CI docker job). The first review (docs/progress/phase-06-background.md, "Review gate", R1-R10) covered the diff at the old 850a3a8. Review only what changed since: a56a2e3, b234be6, c352351, 9eae1f1, 0773b2b (run git show for each), plus any interaction between Phase 6 and the Phase 4 files now on main.

  Check at least:
  - 9eae1f1: the later-of(now, CreatedAt) finish time in MortgageApplicationService.ProcessAssessmentAsync and its two unit tests.
  - 0773b2b: AssessmentWorker.RequeuePendingAsync / TryRequeuePendingAsync retry loop: shutdown during the delay or during a retry, exceptions that escape, duplicates queued by a retry, Task.Delay(TimeSpan, TimeProvider, CancellationToken) use, the option's [Range(1, 3600)] and its startup test, and the integration test Startup_sweep_failure_is_retried_and_completes_pending_assessment (could it pass without the retry? is it timing-flaky?).
  - That the R1, R2, R5, R9, R10 fixes do what the phase file says.
  - Whether the rebased branch still matches the Phase 4 smoke test in .github/workflows/ci.yml (the CI job only POSTs and GETs an application; say whether it should also cover the 202 assessment flow).

  Output: findings, most severe first, each as file:line | severity | problem | fix. Then a list of what you checked and found fine. Say plainly that you did not build or run tests, if you did not.
  ```
- **Purpose:** review gate re-run after the rebase, on the commits made after the first review.
- **Outcome:** no critical, high or medium findings; six low (RR1–RR6). See "Review gate".

### P06-11 · agent→subagent (`general-purpose`, from the `/security-review` skill) · 2026-11-15
- **Prompt (verbatim):**
  ```
  Security review sub-task for PR #8 (Phase 6, background assessments) of a .NET 10 mortgage-application API. Read only: do not edit files and do not run Docker.

  Repository worktree: /Users/ale_ssd/Developer/Mortgage-Application-API-test/.claude/worktrees/agent-acd40a009ddf8a3cd (branch phase-06-background). The diff to review: run `git diff origin/main...HEAD` there (31 files: a background worker, a bounded in-process queue, a 202 assess endpoint, repository methods for the worker that are not owner-scoped, tests, the CI docker job, docs). Accepted and documented, do not report: no rate limit or request timeout until Phase 8 (security S1), the fixed DevelopmentCurrentUser until Phase 7, the worker-only methods being public on the shared service (S2). Context to read: docs/progress/phase-06-background.md ("Security gate"), .claude/rules/security.md, AGENTS.md.

  OBJECTIVE: identify HIGH-CONFIDENCE security vulnerabilities newly added by this PR that could have real exploitation potential. Not a general code review. Do not comment on existing security concerns.
  1. Minimise false positives: only flag issues where you are >80% confident of actual exploitability.
  2. Skip theoretical issues, style concerns and low-impact findings.
  3. Prioritise vulnerabilities that could lead to unauthorized access, data breaches or system compromise.
  4. Do NOT report: denial of service; secrets or sensitive data stored on disk; rate limiting or resource exhaustion.

  Categories to examine: input validation (SQL/command/XXE/template/NoSQL injection, path traversal); authentication and authorization (bypass, privilege escalation, sessions, JWT); crypto and secrets (hardcoded keys, weak crypto, key storage, randomness, certificate validation); injection and code execution (deserialization, YAML, eval, XSS); data exposure (sensitive data logging or storage, PII, endpoint data leakage, debug information exposure). Even if only exploitable from the local network, it can be HIGH.

  Method: (1) research the repository's security model and existing patterns; (2) compare the new changes against them and look for new attack surface; (3) assess each modified file, trace data from inputs to sensitive operations, look for privilege boundaries crossed unsafely.

  For each candidate give: file, line, severity (HIGH / MEDIUM / LOW), category slug, description, exploit scenario, fix recommendation, and a confidence from 1 to 10. Focus on HIGH and MEDIUM; MEDIUM only if obvious and concrete. If there are no candidates, say so plainly. Keep the report short.
  ```
- **Purpose:** the find step of the local `/security-review` on PR #8.
- **Outcome:** no HIGH or MEDIUM candidates (hand review only), so the skill's false-positive filter step had nothing to run on.

### P06-12 · human→agent · 2026-11-15
- **Prompt (verbatim):**
  > Yes and proceed... but we should do phase by phase...
- **Purpose:** Checkpoint B approval of Phase 6. The agent had asked three things in one message: merge PR #8, mark ADR 0002 and ADR 0003 Accepted, and move the five PR `/code-review` findings to Phase 9. "Yes" is read as yes to all three. "Phase by phase" is read as: no more parallel phases; one phase at a time from now on.
- **Outcome:** PR #8 squash-merged as `4141af1` after both CI jobs passed on `552d410`; ADR 0002 and 0003 marked Accepted; the five findings carried to Phase 9 (4, `Retry-After`, skipped for now); Phase 5 started on its own.

## Phase 05: Logging + instrumentation

Copied from `docs/progress/phase-05-observability.md` after the merge. The CI fix on PR #9 was made in a separate session started from the lead session; its own prompts are in that session, and its result is recorded in the phase file.

### P05-01 · human→agent · 2026-11-15
- **Prompt (verbatim):**
  > Yes and proceed... but we should do phase by phase...
- **Purpose:** after the Phase 6 merge (P06-12), go on to the next phase, one phase at a time.
- **Outcome:** Phase 5 explored and planned in this file; Checkpoint A.

### P05-02 · human→agent · 2026-11-15
- **Prompt (verbatim):**
  > Yes continue
- **Purpose:** Checkpoint A approval. The agent had asked: "Do you approve the plan with A on all five, including adding the 3 OpenTelemetry packages?"
- **Outcome:** decisions recorded (all A), packages approved; branch created; `test-designer` run (P05-03).

### P05-03 · agent→subagent (`test-designer`) · 2026-11-15
- **Prompt (verbatim):**
  ```
  Phase 5 (logging + instrumentation) of a .NET 10 mortgage-application API (Minimal APIs, EF Core SQLite, a background assessment worker). Propose test cases for the approved plan; do not write code.

  Read first: docs/progress/phase-05-observability.md (goal, plan, approved decisions P5-D1 to P5-D5), docs/spec/affordability-rules.md (section 6 logging allowlist, section 8 golden examples E1 and E2), docs/layers/PRODUCTION-LAYERS.md (Observability row), .claude/rules/tests.md, tests/CLAUDE.md, .claude/rules/security.md, AGENTS.md, and the current code and tests: src/MortgageApplications.Api/Program.cs, src/MortgageApplications.Api/Assessments/AssessmentWorker.cs, src/MortgageApplications.Api/Applications/ApplicationEndpoints.cs, src/MortgageApplications.Api/Errors/ProblemExceptionHandler.cs, src/MortgageApplications.Api/appsettings.json, src/MortgageApplications.Infrastructure/DatabaseMigration.cs, tests/MortgageApplications.IntegrationTests/ (ApiFactory, CapturingLoggerProvider, AssessmentWorkerTests, ErrorHandlingTests, StartupTests, Http). Existing tests already cover the six worker log messages without amounts (I-14) and a non-empty traceId in every ProblemDetails (I40): don't repeat them, but say which ones should be extended.

  What will be built (not written yet):
  - AssessmentTelemetry in the Api project: ActivitySource and Meter both named "MortgageApplications"; counter mortgage.assessments.processed and histogram mortgage.assessment.duration (seconds), tags assessment.outcome (Completed or Failed), assessment.decision (Approved or Declined, only when Completed), assessment.rules_version.
  - AssessmentWorker starts span "assessment.process" per item with tags assessment.id, application.id, then outcome, decision, rules_version; status Error on a processing error with the exception type only, no message.
  - Program.cs: AddOpenTelemetry with service name, ASP.NET Core tracing and metrics, our source and meter, the Microsoft.AspNetCore.Hosting and Microsoft.AspNetCore.Server.Kestrel meters, and the OpenTelemetry log provider; the OTLP exporter only when OTEL_EXPORTER_OTLP_ENDPOINT is set.
  - A new [LoggerMessage] when an assessment is queued (assessment ID and application ID only). The framework's 500 error log stays (P5-D4). Microsoft.EntityFrameworkCore.Database.Command at Warning in appsettings.json (P5-D5).
  - compose.observability.yaml adds the Aspire dashboard (UI on 127.0.0.1:18888, OTLP only on the compose network) and sets OTEL_EXPORTER_OTLP_ENDPOINT on api; plain compose and CI unchanged.
  - Carry-overs: "ADR in Phase 4" comments become "ADR 0002"; test X1: outside Development the app does not run migrations at startup (note ApiFactory migrates its own database before any host starts, so X1 needs a database that the factory did not migrate).
  - Tests must not add packages: listen with ActivityListener and MeterListener (built into .NET).

  Cover at least: the allowlist test the layer table asks for (none of an assessment's input or output numbers, raw or formatted, in captured log text including exception text, span tag values, or metric tag values) for an Approved (E1) and a Declined (E2) case, and say exactly which number forms to check (raw decimal, 2 dp, thousands separators, invariant and current culture); span and metric tag KEYS limited to an allowlist; exactly one assessment.process span per processed item with the expected tags and the right status for Completed, Failed and a processing error; the counter and the histogram per outcome and decision; the new queued log; a real 500 is logged by the framework with no input values and its ProblemDetails traceId matches the request's trace ID; the OTLP exporter is not registered when the endpoint is not set (and how to prove that without a network); EF command logs no longer at Information; X1; the compose override (what can be checked in CI or locally without the dashboard UI). Say which cases are unit tests and which are integration tests, how to keep listener tests isolated when xUnit runs test classes in parallel (ActivityListener and MeterListener are process-wide), and how to avoid timing flakiness.

  Use the agent's output format. Mark each case must or nice, keep the list tight (no redundant cases), and list any ambiguities in the plan you need Alessandro to settle.
  ```
- **Purpose:** propose Phase 5 test cases before any test is written.
- **Outcome:** 31 cases (O1–O31) and 13 questions (Q1–Q13). See "Tests", "Proposal".

### P05-04 · human→agent · 2026-11-15
- **Prompt (verbatim):**
  > Yes proceed
- **Purpose:** approval of the recommended test selection (all must cases plus O7, O8, O26, O28; O29 and O31 as verify steps; O4 and O19 skipped) and of the recommended answers to Q1–Q13.
- **Outcome:** implementation delegated, test-first (P05-05).

### P05-05 · agent→subagent (`general-purpose`, implementer) · 2026-11-15
- **Prompt (verbatim):**
  ```
  Implement Phase 5 (logging + instrumentation) of this .NET 10 mortgage-application API, exactly as approved, test-first.

  Where: the main checkout /Users/ale_ssd/Developer/Mortgage-Application-API-test, branch phase-05-observability (already checked out; phases now run one at a time, so there is no worktree). Do not switch or create branches. Never push. Never force anything, never --no-verify.

  Scope and source of truth: docs/progress/phase-05-observability.md. Read "Goal", "Plan (approved at Checkpoint A)", "Decisions" (P5-D1 to P5-D5, all option A), "Docs verified" and "Tests > Proposal" (the approved selection and the approved answers to Q1-Q13) first. Also read AGENTS.md, CLAUDE.md, .claude/rules/backend.md, .claude/rules/tests.md, .claude/rules/security.md, .claude/rules/infra.md, tests/CLAUDE.md, src/MortgageApplications.Core/CLAUDE.md, docs/spec/affordability-rules.md sections 6 and 8.

  What to build (summary; the phase file is binding where it is more precise):
  1. Packages, approved: in Directory.Packages.props add OpenTelemetry.Extensions.Hosting 1.20.1, OpenTelemetry.Instrumentation.AspNetCore 1.20.1, OpenTelemetry.Exporter.OpenTelemetryProtocol 1.20.1; reference them from src/MortgageApplications.Api only. No other package, and no package in the test projects.
  2. src/MortgageApplications.Api/Observability/AssessmentTelemetry.cs: an ActivitySource named "MortgageApplications" and a Meter created through IMeterFactory with the same name; counter mortgage.assessments.processed (long) and histogram mortgage.assessment.duration (double, unit "s"). Tags: assessment.outcome, assessment.decision (only when Completed), assessment.rules_version.
  3. AssessmentWorker: span "assessment.process" (kind Internal) per item with assessment.id and application.id (application.id only when known), then assessment.outcome = Completed | Failed | NotFound | Skipped, assessment.decision when Completed, assessment.rules_version (configured value) for Completed and Failed. Status: Unset for Completed, Failed, NotFound, Skipped; Error for a processing error (an exception reaching the worker's per-item catch) with the exception TYPE name only as description, no message, no exception event, no outcome tag. Counter and histogram only for Completed and Failed. Duration measured with the injected TimeProvider (GetTimestamp / GetElapsedTime). The span is a root span (P5-D3 A: separate traces). The worker needs application.id and the rules version: get them without changing what Core exposes more than needed (for example extend AssessmentProcessingOutcome into a small result record returned by ProcessAssessmentAsync, or read them from the work item); keep Core free of telemetry, logging and ASP.NET Core references.
  4. ApplicationEndpoints: after a successful queue, tag the current request activity (the ASP.NET Core server span) with assessment.id, and log a new [LoggerMessage] event "AssessmentQueued" (Information) with AssessmentId and ApplicationId only. Nothing for 404s.
  5. Program.cs: AddOpenTelemetry() with ConfigureResource(AddService("mortgage-applications") unless OTEL_SERVICE_NAME is set), WithTracing (AddAspNetCoreInstrumentation, AddSource("MortgageApplications")), WithMetrics (AddAspNetCoreInstrumentation, AddMeter("MortgageApplications"), AddMeter("Microsoft.AspNetCore.Hosting"), AddMeter("Microsoft.AspNetCore.Server.Kestrel")), WithLogging or builder.Logging.AddOpenTelemetry. Add the OTLP exporter ONLY when builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"] is set (read from IConfiguration, never Environment). Register AssessmentTelemetry as a singleton.
  6. appsettings.json: "Microsoft.EntityFrameworkCore.Database.Command": "Warning" under Logging:LogLevel.
  7. compose.observability.yaml (new): service aspire-dashboard, image mcr.microsoft.com/aspire/dashboard with a pinned version tag (not latest; pick a stable 13.x tag and record in the phase file that its publish date is to be checked by the lead agent), ports only "127.0.0.1:18888:18888", no host mapping for 18889/18890, no ASPIRE_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS; and on api: OTEL_EXPORTER_OTLP_ENDPOINT: http://aspire-dashboard:18889 plus depends_on aspire-dashboard. compose.yaml must not change.
  8. Carry-overs: the comments "ADR in Phase 4" in src/MortgageApplications.Api/Program.cs and src/MortgageApplications.Infrastructure/DatabaseMigration.cs become "ADR 0002".
  9. Check, before relying on it, how OpenTelemetry.Instrumentation.AspNetCore 1.20.x handles url.query (redaction) from its README in the NuGet package you restore (~/.nuget/packages/opentelemetry.instrumentation.aspnetcore/1.20.1/README.md if present); record what you found and where.

  Tests (integration tests in tests/MortgageApplications.IntegrationTests; follow the approved cases O1-O3, O5-O18, O20-O28 and the nice Q12 query-string test, with the IDs in a comment above each test):
  - Extend CapturingLoggerProvider to keep structured state (key/value pairs), scope values and Activity.Current's trace ID at log time, without breaking existing tests.
  - A small TelemetryCapture helper (ActivityListener + MeterListener, IDisposable) that copies data at ActivityStopped / measurement time into concurrent collections. Put every class that uses it in one [Collection] with DisableParallelization = true. Filter spans by the test's own assessment IDs, and metrics by the host's IMeterFactory scope.
  - A forbidden-number helper producing every form listed in the phase file (raw, trimmed, F2, N2/N0, hand-built NumberFormatInfo comma-decimal, dot-grouping, U+00A0 and U+202F space grouping), with GUIDs and the RulesVersion stripped before the boundary-regex search, 5+ digit integers only for bare integers, plus the Summary fragments. Include a self-test of the helper.
  - Every wait is bounded (Http.EventuallyAsync), never Task.Delay. Set config per host with AddInMemoryCollection, never Environment.SetEnvironmentVariable.
  - O24/O25/O26: a loopback HttpListener or TcpListener on a free port; set OTEL_EXPORTER_OTLP_ENDPOINT to it (and OTEL_EXPORTER_OTLP_PROTOCOL http/protobuf if that makes the listener simpler; record what you did); dispose the host to flush.
  - O27 (X1): Production host and a Development control on a fresh named shared-cache in-memory SQLite database the factory did not migrate, as described in the phase file.
  - O28: a file-text test of compose.observability.yaml and compose.yaml.

  Order of work: tests first. Write the tests against minimal stubs so they compile, run them and keep the red output (save it to /private/tmp/claude-501/-Users-ale-ssd-Developer-Mortgage-Application-API-test/38287261-0d8e-41a1-a4c6-05aca4d87104/scratchpad/p5-red.txt), then implement until green. Small conventional commits (AGENTS.md "Commits and PRs"), each ending with the line: Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>. Then run ./scripts/verify.sh (full) and the integration tests 3 times in a row, and keep the outputs.

  Phase file: fill in only "What was built (files)", "Tests" (add a "Results" subsection under the Proposal: red counts and why, green counts, verify output trimmed, the 3 runs) and "Open issues" (deviations, anything not done, anything you could not check). Do not edit other sections, docs/PROGRESS.md or docs/ai/*.

  Honesty rule: never claim a test failed or passed without having run it; if something was not done, write that it was not done and why. Stop and report if the same step fails 3 times. Keep tool use tight (you have a turn limit): batch edits, avoid re-reading files you just wrote.

  Report at the end: commits, files, red/green evidence, verify output, the 3 runs, the url.query finding, the dashboard tag chosen, and every deviation from the plan.
  ```
- **Purpose:** implement the approved plan test-first. The `parallel-implementer` agent is reserved for parallel phases, so a general-purpose agent got the same kind of brief.
- **Outcome:** commits `019cffa` to `b791a5d`; red 4 unit + 21 integration failures for the expected reasons, then green; 431 tests; 3 clean integration runs. Deviations listed in "Open issues".
- **Follow-up message from the lead agent (verbatim), sent while it ran:**
  > Dashboard image tag for compose.observability.yaml: use mcr.microsoft.com/aspire/dashboard:13.6.2. The lead agent checked the MCR catalog on 2026-11-15: 13.6.2 was published 2026-10-29, the newest tag at least two weeks old (13.6.4 is 2026-11-10, 13.6.5 is 2026-11-11). Record that in the phase file instead of "to be checked". Everything else in your instructions is unchanged.

### P05-06 · agent→subagent (`security-auditor`) · 2026-11-15
- **Prompt (verbatim):**
  ```
  Security gate for Phase 5 (logging + instrumentation) of a .NET 10 mortgage-application API. Read only: do not edit files or switch branches.

  Repository: /Users/ale_ssd/Developer/Mortgage-Application-API-test, branch phase-05-observability (checked out). Review the branch diff: git diff main...HEAD. Read first: .claude/rules/security.md, AGENTS.md, docs/spec/affordability-rules.md section 6 (logging allowlist), docs/progress/phase-05-observability.md (plan, decisions P5-D1 to P5-D5, the approved answers Q1-Q13, "Open issues", and the O30 manual dashboard check).

  Do:
  1. Gitleaks on the branch commits (--log-opts="main..HEAD") and the working tree; Semgrep (p/csharp, p/secrets) on the changed files; dotnet list package --vulnerable --include-transitive (three OpenTelemetry packages are new). Say which tools ran; do not install anything.
  2. OWASP Top 10:2025 review of the diff with focus on: personal data or amounts reaching any log, span attribute, span status description or metric tag (inputs, outputs, Summary, Checks, owner ID, exception messages); framework telemetry (ASP.NET Core server spans: url.query handling, headers, route values); the OpenTelemetry log provider exporting structured state and scopes; the OTLP exporter being enabled only by OTEL_EXPORTER_OTLP_ENDPOINT and what an attacker who controls configuration could do with it (not a finding if config is trusted, but say so); compose.observability.yaml (loopback-only UI, unpublished OTLP ports, login token, no anonymous mode, the dashboard's unauthenticated OTLP endpoint on the compose network, the pinned image tag); supply chain of the three new packages and their transitive packages.
  3. Check that the tests would catch a regression: do the allowlist tests scan every telemetry path that leaves the process (logs incl. structured state and exceptions, spans incl. status, metric tags, the OTLP bytes)? Name any path they miss.

  Output: a findings table (tool/check, finding with file:line, severity, real or false positive, reason, fix) and a one-word verdict (PASS or FAIL). Keep it short.
  ```
- **Purpose:** security gate.
- **Outcome:** PASS; Low and Info findings only. See "Security gate".

### P05-07 · agent→subagent (`dotnet-reviewer`) · 2026-11-15
- **Prompt (verbatim):**
  ```
  Fresh-context review of Phase 5 (logging + instrumentation) of a .NET 10 mortgage-application API. Read only: do not edit files or switch branches.

  Repository: /Users/ale_ssd/Developer/Mortgage-Application-API-test, branch phase-05-observability (checked out). Review the branch diff: git diff main...HEAD. Read first: AGENTS.md, .claude/rules/backend.md, .claude/rules/tests.md, tests/CLAUDE.md, src/MortgageApplications.Core/CLAUDE.md, docs/progress/phase-05-observability.md (goal, plan, decisions, "Docs verified", the approved test selection and answers Q1-Q13, "Results", "Open issues").

  Check at least:
  - AssessmentWorker / AssessmentTelemetry: span lifecycle per item (root span via Activity.Current = null in an async method; started before the scope, disposed after logging), tags and status per outcome (Completed, Failed, NotFound, Skipped, processing error, shutdown OCE), counter and histogram only for Completed and Failed, duration from TimeProvider, histogram buckets in seconds, IMeterFactory use, ActivitySource lifetime and disposal.
  - Core change: AssessmentProcessingResult replacing the enum return; Core stays free of telemetry, logging and ASP.NET Core; the existing unit tests still prove what they proved.
  - ApplicationEndpoints: tagging the server span through IHttpActivityFeature, the AssessmentQueued [LoggerMessage] (logger created per request from ILoggerFactory: fine or wasteful?), nothing on 404.
  - Program.cs: OpenTelemetry registration, the conditional OTLP exporter read from builder.Configuration before Build (the implementer found WebApplicationFactory's ConfigureAppConfiguration is not visible there and switched the tests to UseSetting: is the production behaviour still right for environment variables, appsettings and command line?), OTEL_SERVICE_NAME handling, WithLogging and the existing console logging.
  - Tests: whether each approved case (O1-O3, O5-O18, O20-O28, Q12) would fail if its behaviour broke; listener isolation (one non-parallel collection, IMeterFactory scope filter); bounded waits; the ForbiddenNumbers helper (forms, boundary regex, stripping GUIDs and hex IDs: could the stripping hide a real leak?); O24's fixed port 4318 and its skip path; flakiness risks.
  - Anything in the diff that contradicts the plan or the decisions.

  Output: findings, most severe first, each as file:line | severity | problem | fix. Then a list of what you checked and found fine. Say plainly whether you built or ran tests.
  ```
- **Purpose:** fresh-context review gate.
- **Outcome:** one medium and eight low findings. See "Review gate".

### P05-08 · agent→subagent (implementer, resumed) · 2026-11-15
- **Prompt (verbatim):**
  ```
  Phase 5 gate findings: fix the items below on phase-05-observability, test-first where behaviour changes (show the new or strengthened test failing first, or prove it with a temporary mutation that you revert, and record which). Then run ./scripts/verify.sh (full) and the integration tests 3 times. Same rules as before: no new package, no push, small conventional commits with the Co-Authored-By line, keep tool use tight.

  From the review gate (dotnet-reviewer):
  1. (medium) ForbiddenNumbers misses longer-precision leaks. Forms containing a decimal separator must match as prefixes (drop the trailing (?![0-9]) for them). Add, per output value, a 1 dp form and a 2 dp truncated form, and add the unrounded E1/E2 values as extra forbidden values: LTV, DTI and LTI to at least 4 dp, the LTV and DTI as fractions (for example 0.8, 0.3302), and the unrounded monthly payment to 4 dp. Compute them with System.Decimal from the rules formulas (docs/spec/affordability-rules.md), not by hand, and say in a comment how. Move "2.825" from CleanTexts to Leaks in the self-test. Re-check that nothing legitimate (IDs, rules version, timestamps, durations) now matches; if a duration or timestamp could collide, strip it the same way IDs are stripped and say so.
  2. O24 depends on the fixed default port 4318 and skips when it is taken. The reviewer suggested pointing the signal-specific endpoint keys at a free port, but item 3 makes those keys enable export, so that design no longer works. Keep O24 on the default port unless you find a way, without a new package and without the fixed port, for O24 to fail if the exporter were registered unconditionally. Either way, make a skip visible (an explicit xUnit skip with the reason "port 4318 in use"), never a silent pass, and explain the design in the test comment.
  3. Program.cs: also turn the OTLP exporter on when any of OTEL_EXPORTER_OTLP_TRACES_ENDPOINT, OTEL_EXPORTER_OTLP_METRICS_ENDPOINT or OTEL_EXPORTER_OTLP_LOGS_ENDPOINT is set. Add a test (O25b) that setting only the traces key sends trace data to the fake collector.
  4. user_agent.original on ASP.NET Core server spans is client-controlled and not in the rules section 6 allowlist: remove it in AddAspNetCoreInstrumentation (EnrichWithHttpRequest setting the tag to null, or the documented option if the package README has one). Then add the skipped O4 as an explicit framework span tag-key allowlist: the server span may carry only the HTTP semantic-convention keys it carries today minus user_agent.original (list them exactly in the test), plus assessment.id. Record in the phase file that rules section 6 does not yet name these HTTP keys; Alessandro decides at Checkpoint B whether to amend the rules wording.
  5. O14 cannot fail if Activity.Current = null is removed. Start the worker's host while a test-only ActivitySource activity is current (with a listener that samples it), so ExecuteAsync captures it as a would-be parent; keep the assertion that the worker span has no parent. Prove it fails with the line removed (temporary mutation, reverted).
  6. O17: assert the histogram's bucket advice (InstrumentAdvice boundaries in seconds) from the published instrument, if the advice is readable through a public API on .NET 10; if it is not public, say so and skip this item.
  7. Program.cs: AddService("mortgage-applications") also overrides a service.name from OTEL_RESOURCE_ATTRIBUTES. Skip AddService when OTEL_RESOURCE_ATTRIBUTES contains "service.name=" as well, and comment it. No new test needed beyond what is cheap.

  From the security gate (security-auditor):
  8. TelemetryCapture copies only span event names: include span event tags and link tags in the scanned span text.
  9. Metric instrument set: assert that the MortgageApplications meter publishes exactly the two allowed instruments (names, types, units), from InstrumentPublished, so a new instrument recording an amount cannot slip through.
  10. O6: also scan the 500 request's server span (tags and status description) for the sentinels and the owner ID, filtered by the traced trace ID.

  Phase file (docs/progress/phase-05-observability.md): fix the contradictions the reviewer found (status line still says "implementation next"; "Open issues" still says O30 and the Learn checks were not done: the lead agent did both, see the O30 section and "Docs verified"). Record the shutdown-cancellation span behaviour as a known, intended gap: on shutdown the span ends Unset with assessment.id only (no outcome), consistent with "shutdown is never a failure"; no test. Add a "Results after the gates" paragraph under Tests > Results with red/mutation evidence, verify output and the 3 runs. Do not fill the Security gate or Review gate sections (the lead agent writes those). Do not edit docs/PROGRESS.md or docs/ai/*.

  Report: commits, what failed first or which mutation proved each strengthened test, the exact framework key list for O4, verify output, the 3 runs, and anything you could not do.
  ```
- **Purpose:** fix the gate findings that need no decision.
- **Outcome:** commits `0eccb16`, `f25c203`, `bcd3028`; 442 tests; 3 clean integration runs (168 / 168).

### P05-09 · agent→subagent (`spec-checker`) · 2026-11-15
- **Prompt (verbatim):**
  ```
  Spec check for Phase 5 (logging + instrumentation) of a .NET 10 mortgage-application API. Read only; do not edit anything.

  Repository: /Users/ale_ssd/Developer/Mortgage-Application-API-test, branch phase-05-observability (checked out). Read: docs/spec/ORIGINAL_SPEC.md, docs/spec/affordability-rules.md section 6, docs/layers/PRODUCTION-LAYERS.md (the Observability row), docs/progress/phase-05-observability.md (goal, plan, decisions, approved tests and answers, gates, open issues), the requirement table in docs/PROGRESS.md, and the code and tests the phase file names.

  Map every spec requirement this phase touches (the spec's logging, instrumentation or observability wording if any, the README "design decisions" expectations it feeds, and the security constraints on personal data), every Phase 5 goal (allowlisted [LoggerMessage] logs, OpenTelemetry tracing with one span per assessment, the two metrics, OTLP export only when configured, the opt-in dashboard, trace ID in ProblemDetails, the allowlist test over logs, spans and metric tags, the carry-overs: ADR 0002 comments and test X1) and the Observability row's "evidence" column to the file and test that meet it. Mark each met / partial / missing with a one-line reason and the file or test name. List any requirement-table rows in docs/PROGRESS.md whose status should change after this phase, and anything the phase file claims that you could not find. Keep it short: one table plus at most 5 notes.
  ```
- **Purpose:** requirement traceability before the PR.
- **Outcome:** see "Review gate", spec check.

### P05-10 · agent→subagent (`general-purpose`, from the `/security-review` skill) · 2026-11-15
- **Prompt (verbatim):**
  ```
  Security review sub-task for PR #9 (Phase 5, logging and instrumentation) of a .NET 10 mortgage-application API. Read only: do not edit files and do not run Docker.

  Repository: /Users/ale_ssd/Developer/Mortgage-Application-API-test (branch phase-05-observability, checked out). The diff to review: run `git diff main...HEAD` there (OpenTelemetry tracing, metrics and log export via OTLP when configured; a worker span and two metrics; a new log event; an opt-in compose override with the Aspire dashboard; tests). Accepted and documented, do not report: exception messages and stack traces in error logs leaving the process when OTLP is on (P5-D4), the dashboard's OTLP endpoint without an API key on the compose network (S7), image tag not digest (Phase 8), the fixed DevelopmentCurrentUser until Phase 7. Context to read: docs/progress/phase-05-observability.md ("Security gate"), docs/spec/affordability-rules.md section 6, .claude/rules/security.md, AGENTS.md.

  OBJECTIVE: identify HIGH-CONFIDENCE security vulnerabilities newly added by this PR that could have real exploitation potential. Not a general code review. Do not comment on existing security concerns.
  1. Minimise false positives: only flag issues where you are >80% confident of actual exploitability.
  2. Skip theoretical issues, style concerns and low-impact findings.
  3. Prioritise vulnerabilities that could lead to unauthorized access, data breaches or system compromise.
  4. Do NOT report: denial of service; secrets or sensitive data stored on disk; rate limiting or resource exhaustion.

  Categories to examine: input validation (SQL/command/XXE/template/NoSQL injection, path traversal); authentication and authorization (bypass, privilege escalation, sessions, JWT); crypto and secrets (hardcoded keys, weak crypto, key storage, randomness, certificate validation); injection and code execution (deserialization, YAML, eval, XSS); data exposure (sensitive data logging or storage, PII, endpoint data leakage, debug information exposure). Even if only exploitable from the local network, it can be HIGH.

  Method: (1) research the repository's security model and existing patterns; (2) compare the new changes against them and look for new attack surface; (3) assess each modified file, trace data from inputs to sensitive operations, look for privilege boundaries crossed unsafely.

  For each candidate give: file, line, severity (HIGH / MEDIUM / LOW), category slug, description, exploit scenario, fix recommendation, and a confidence from 1 to 10. Focus on HIGH and MEDIUM; MEDIUM only if obvious and concrete. If there are no candidates, say so plainly. Keep the report short.
  ```
- **Purpose:** the find step of the local `/security-review` on PR #9.
- **Outcome:** no HIGH or MEDIUM candidates, so the skill's false-positive filter step had nothing to run on.

### P05-11 · human→agent · 2026-11-15
- **Prompts (verbatim), in order:**
  > proceed

  > Approved, merge here
- **Purpose:** Checkpoint B approval of PR #9. After "proceed", the lead agent asked: "Do you approve PR #9, with my two recommendations: name the extra HTTP span keys in rules section 6, and move the four low code-review findings to Phase 9?", and whether to merge here or in the Phase 5 session. "Approved, merge here" answers both.
- **Outcome:** rules section 6 amended in this PR; the four PR `/code-review` findings carried to Phase 9; PR #9 squash-merged by the lead agent as `352c6c8` after both CI jobs passed on `6644435`.

## Harness H1: usage awareness and model choice

Copied from `docs/progress/harness-01-context.md` after the merge. H-00 (Alessandro's brief) is kept verbatim in `docs/progress/harness-01/brief-H-00.md`.

### H-00 · human→agent · 2026-11-15
- **Prompt (verbatim):** in [`harness-01/brief-H-00.md`](../progress/harness-01/brief-H-00.md) (460 lines, kept as a separate file because of its length).
- **Purpose:** new scope of work.
- **Outcome:** discovery, plan, Checkpoint A.

### H-01 · agent→subagent (`claude-code-guide`) · 2026-11-15
- **Prompt (verbatim):**
  ```
  I am designing context and usage awareness for a Claude Code project harness (installed Claude Code 2.1.283, macOS, Pro/Max subscription login, no API key). I need verified facts from the official Claude Code documentation only; for each answer give the doc URL and quote the exact field or event names. If something is not documented, say "not documented" rather than guessing.

  1. Hooks: the full list of hook events supported today. For each of SessionStart (and its matcher sources: startup, resume, clear, compact), PreCompact, PostCompact (does it exist?), SubagentStart (does it exist?), SubagentStop, Stop, UserPromptSubmit, Notification, SessionEnd: the JSON input fields (e.g. session_id, transcript_path, cwd, trigger, custom_instructions, agent_id, agent_type, stop_hook_active), and which events can inject context into the model (hookSpecificOutput.additionalContext or plain stdout) versus only block or log. Can a SubagentStart hook add context to the subagent? Does PreCompact allow adding instructions to the compaction summary?
  2. Subagents (.claude/agents/*.md): the supported frontmatter fields today (name, description, tools, disallowedTools, model, maxTurns, isolation, skills, memory, hooks, permissionMode, initialPrompt, effort, etc.). Is there a documented persistent "memory" field for subagents and how does it work (where is it stored)? Do subagents load CLAUDE.md / project memory automatically? Do they see the parent conversation?
  3. Status line: the JSON object passed to a statusLine command on stdin: every documented field, in particular model (id, display_name), context window size and current usage (e.g. context_window, exceeds_200k_tokens, current usage percentages), cost (total_cost_usd, durations), and any rate-limit or subscription-usage fields (e.g. rate_limits, five-hour or weekly usage, reset times). Say exactly which ones exist.
  4. Usage and limits: which documented commands or interfaces report subscription usage, remaining allowance and reset times for a subscription user (/usage, /status, /cost, /context, others), and whether any of these are available to hooks or scripts (not just the interactive UI). How does Claude Code surface a usage-limit or rate-limit hit (message text, hook event, exit status)?
  5. Context window: how a user or script can read current context usage (/context, status line fields), the context-window size per model, and when auto-compaction triggers.
  6. Environment variables documented as available inside hook and status-line commands (e.g. CLAUDE_PROJECT_DIR, CLAUDE_ENV_FILE, CLAUDE_CODE_*). Only documented ones.

  Keep the answer structured by question number. Mark clearly anything that may depend on version and which version the docs describe.
  ```
- **Purpose:** verify hook events, status-line JSON, usage reporting and subagent options in the official docs.
- **Outcome:** 33 hook events incl. PreCompact, PostCompact, SubagentStart, StopFailure; status-line `context_window` and `rate_limits` fields; no scriptable plan name, allowance or per-model quota. Items it marked "summary" (not quoted) are flagged in the discovery report.

### H-02 · human→agent · 2026-11-15
- **Prompt (verbatim):**
  > Approved, all A
- **Purpose:** Checkpoint A approval (H-D1 to H-D5 all option A).
- **Outcome:** test design started.

### H-03 · agent→subagent (`test-designer`) · 2026-11-15
- **Prompt (verbatim):**
  ```
  Harness phase H1 (context and usage awareness for the Claude Code harness) of a .NET 10 mortgage-application API repo. Propose test cases for the approved plan; do not write code or edit files.

  Read first: the approved plan /private/tmp/claude-501/-Users-ale-ssd-Developer-Mortgage-Application-API-test/38287261-0d8e-41a1-a4c6-05aca4d87104/scratchpad/harness/H-discovery-and-plan.md (all decisions H-D1 to H-D5 are option A), then in /Users/ale_ssd/Developer/Mortgage-Application-API-test: .claude/settings.json, .claude/hooks/*.sh, .claude/hooks/tests/test-hooks.sh (how hook cases are written today: JSON piped to the hook, exit code checked), .claude/agents/*.md, scripts/verify.sh, CLAUDE.md, AGENTS.md, docs/PROGRESS.md (its "Resume here" section format), one phase file (docs/progress/phase-06-background.md: "Decisions" table and "Open issues" format), docs/adr/0003-background-assessment-queue.md (ADR header format).

  Documented Claude Code facts the design relies on (installed version 2.1.283): SessionStart input has source = startup | resume | clear | compact (| fork) and hooks can return hookSpecificOutput.additionalContext or plain stdout; SubagentStart input has agent_id and agent_type and can return hookSpecificOutput.additionalContext; PreCompact input has trigger (manual | auto) and custom_instructions, and plain stdout on exit 0 becomes compaction instructions; StopFailure input has error (matcher rate_limit, overloaded, ...), optional error_details and last_assistant_message; the status line command receives JSON on stdin with model.id/display_name, context_window.context_window_size/used_percentage/remaining_percentage/current_usage (current_usage may be null), and rate_limits.five_hour and rate_limits.seven_day each with used_percentage and resets_at (Unix seconds), each window possibly absent, the whole rate_limits object absent for non-subscribers or before the first response.

  What will be built: scripts/context/brief.sh <agent> (role-specific brief), scripts/context/index.sh (generates docs/ai/context/DECISIONS.md and OPEN-ITEMS.md from phase files and ADRs; verify.sh fails when they are stale), docs/ai/context/{CONTEXT-INDEX.md, AGENT-REGISTRY.md, CURRENT-WORK.md, HANDOVER-TEMPLATE.md, handovers/}, .claude/statusline.sh (prints model, context %, usage; writes .claude/state/usage.json atomically, gitignored), hooks session-context.sh (SessionStart all sources; keeps what reinject-context.sh prints today), subagent-context.sh (SubagentStart), pre-compact.sh (PreCompact: writes .claude/state/checkpoint.md, prints keep-instructions), usage-stop.sh (StopFailure: records the blocker, reset time if known, exact resume instruction). Usage states: normal < 75%, approaching >= 75%, exhausted at 100% or a rate-limit StopFailure, unknown when no data or stale.

  Cover at least the brief's Part 7 list: new-session context loading for each SessionStart source; context after compaction; missing PROGRESS.md; missing or malformed phase files; invalid or incomplete context; context for fresh subagents (each of the five agent types and an unknown one: the brief names its responsibility, boundaries and the "decisions reserved for Alessandro" rule); retrieval of previous decisions (index contents, stale-index failure); no duplication of authoritative records (index rows link to sources, generated files are marked generated); handover creation, retrieval and malformed handovers; preservation of existing context (hooks never write PROGRESS.md, phase files, ADRs or PROMPTS.md); concurrent writes to the state files; usage available, unavailable, partially absent, null values, stale snapshot, model change, rate limit with and without reset info, exhaustion; human checkpoints and author/reviewer independence stated in every brief; no secrets or account data written to state files; hostile or malformed JSON on stdin; existing hook cases still pass. Say how each case is run (bash with fixtures, no network, no real account data), which need jq and how the jq-less fallback is tested, and how to keep tests deterministic (fixed clock via an env var or file timestamp, temp dirs). Name the one check that can only be done manually on 2.1.283 (SubagentStart context reaching the subagent) and how to record it.

  Use your agent's output format. Mark each case must or nice, keep the list tight, and list ambiguities in the plan for Alessandro to settle.
  ```
- **Purpose:** propose H1 test cases.
- **Outcome:** about 110 cases and 15 questions; see section 7.

### H-04 · human→agent · 2026-11-15
- **Prompt (verbatim):**
  > Approved
- **Purpose:** approval of the recommended test set and answers A1–A15.
- **Outcome:** recorded; implementation waited for the Phase 5 merge (H-D5).

### H-05 · human→agent · 2026-11-15
- **Prompt (verbatim):**
  > Approved, merge here
- **Purpose:** Phase 5 Checkpoint B (recorded in `phase-05-observability.md`, P05-11); it also unblocked H1.
- **Outcome:** PR #9 merged as `352c6c8`; branch `harness-01-context` created from `cd9f7be`.

### H-06 · agent→subagent (`general-purpose`, implementer) · 2026-11-15
- **Prompt (verbatim):**
  ```
  Implement harness phase H1 (context and usage awareness for the Claude Code agent harness) in /Users/ale_ssd/Developer/Mortgage-Application-API-test, exactly as approved, test-first.

  Where: the main checkout, branch harness-01-context (already checked out). Do not switch or create branches. Never push, never force, never --no-verify. No new tool or package (jq is already used; shellcheck may be used only if it is installed, never installed).

  Source of truth, read first:
  - docs/progress/harness-01-context.md: sections 1 (discovery), 2 (architecture), 3 (decisions H-D1 to H-D5, all option A), 4 to 6, and 7 (the APPROVED test selection and the approved answers A1 to A15, which are binding).
  - docs/progress/harness-01/test-proposal.md: the full test cases. Implement the approved set: S1-S7, S12-S14, S16; B1-B5, B8-B9; C1, C4-C6, C8; U1, U2, U5-U8, U10, U11-U13, U15-U16, U19-U22; E1-E2, E4-E5, E8-E9; I1, I3, I5-I8, I12-I13, I15; H1-H4, H6; X1-X4, X6-X7. Put the case ID in a comment above each case.
  - AGENTS.md, CLAUDE.md, .claude/settings.json, .claude/hooks/*.sh and .claude/hooks/tests/test-hooks.sh (keep its style and counters), .claude/agents/*.md, .claude/skills/*/SKILL.md, scripts/verify.sh, docs/ai/ORCHESTRATION.md, docs/PROGRESS.md, docs/CLAUDE.md, .claude/rules/security.md.

  Documented Claude Code facts (2.1.283) you may rely on: SessionStart input source = startup|resume|clear|compact|fork, output hookSpecificOutput.additionalContext or plain stdout; SubagentStart input agent_id, agent_type, output hookSpecificOutput.additionalContext (hookEventName "SubagentStart"); PreCompact input trigger (manual|auto), custom_instructions, plain stdout on exit 0 becomes compaction instructions; StopFailure input error (e.g. rate_limit, overloaded), optional error_details, last_assistant_message, output ignored (work only through files, A15); status line stdin JSON: model.id, model.display_name, context_window.context_window_size, used_percentage, remaining_percentage, current_usage (may be null), rate_limits.five_hour / seven_day each with used_percentage and resets_at (Unix seconds), each possibly absent, the whole object absent for non-subscribers or before the first response. Do not rely on anything else; never invent fields, variables or commands.

  Build (smallest robust version; names as in the plan):
  1. docs/ai/context/: CONTEXT-INDEX.md (where each kind of fact lives and which sections to read for which task; links, not copies), AGENT-REGISTRY.md (each of the 5 agents: responsibility, boundaries, model and the reason for it, context it needs, reserved decisions, the rule "never downgrade a security or review model to save usage"; reviewers do not get earlier reviewers' findings by default, A3), CURRENT-WORK.md (short lead-maintained "now": phase, step, files in play, next exact action; fill it for H1 now), HANDOVER-TEMPLATE.md (the plan's headings), handovers/ (with a README line), and the generated DECISIONS.md and OPEN-ITEMS.md.
  2. scripts/context/index.sh: generates DECISIONS.md (phase-file "Decisions" tables and ADR headers: ID, short summary capped, who decided, relative link) and OPEN-ITEMS.md (phase files' "Open issues" items and PROGRESS.md carry-overs, with links), "GENERATED ... do not edit" header, stable sort (numeric phase order), no timestamp inside; ignores _TEMPLATE.md; `--check` mode writes nothing and exits 1 when stale or when a phase file or handover is malformed (A1, A11), naming the file and the fix command. Wire `scripts/context/index.sh --check` into scripts/verify.sh full mode.
  3. scripts/context/brief.sh <agent> [phase]: the role brief (agent name, responsibility, boundaries, phase, branch, CURRENT-WORK pointer, relevant open items, usage status line, newest 5 handovers by filename showing path and task line only, never bodies), plus one shared constant block with the human checkpoints (A: plan, B: PR, wait for Alessandro's approval), author/reviewer independence and "decisions reserved for Alessandro are never made by an agent". Under about 100 lines (A2). Unknown agent on the CLI: non-zero with usage (A4). Phase argument validated (digits only, path-safe).
  4. .claude/hooks/session-context.sh (SessionStart, all sources): Resume here section verbatim (capped with a "truncated, see docs/PROGRESS.md" line), current branch, CURRENT-WORK pointer, usage status, blocker file if any, checkpoint if source is compact; visible one-line warnings for missing or malformed inputs; always exit 0; never eval file content. Keep .claude/hooks/reinject-context.sh as a thin shim that calls it so the existing test passes unchanged (A13).
  5. .claude/hooks/subagent-context.sh (SubagentStart): valid JSON with hookSpecificOutput.hookEventName "SubagentStart" and additionalContext = brief.sh output for agent_type; generic brief for unknown, empty or hostile agent_type; JSON-escape everything (use jq to build it); always exit 0.
  6. .claude/hooks/pre-compact.sh (PreCompact): writes .claude/state/checkpoint.md atomically (temp file in the same dir + mv): branch, changed file PATHS only, last verify line from PROGRESS.md, CURRENT-WORK content, UTC time from HARNESS_NOW or date; prints keep-instructions written as additions ("Also keep ...", A5) covering phase and step, modified files, test commands and last result, open decisions awaiting Alessandro; always exit 0.
  7. .claude/hooks/usage-stop.sh (StopFailure): writes .claude/state/blocker.md (its own file, A9) atomically: error type from an allowlist (unknown otherwise), time, reset time from usage.json if present and in the future (else "reset time unknown"), and the resume sentence from A14; only rate_limit sets the usage state to exhausted (A10); never copies last_assistant_message or error_details; repeated calls replace, not append; always exit 0.
  8. .claude/statusline.sh: one line (model display name sanitised of control characters, ctx %, 5h and 7d %, earliest reset, state); writes .claude/state/usage.json atomically with ONLY the A6 fields (allowlist copy, never session_id, transcript_path, cwd, cost, workspace or anything else); states per the plan (normal < 75, approaching >= 75, exhausted >= 100, unknown for absent, null, negative or non-numeric, A7); stale after 15 minutes by updated_at (A8); without jq prints and stores unknown (A12); BSD and GNU date both work (U10); a past reset clears an exhausted state written by usage-stop.sh (E8).
  9. .gitignore: .claude/state/. .claude/settings.json: add SessionStart (session-context.sh; keep the existing compact entry only through the shim so it is not called twice), SubagentStart, PreCompact, StopFailure entries and a statusLine command entry, all via "$CLAUDE_PROJECT_DIR". Do not change permissions.
  10. Every new script honours CLAUDE_PROJECT_DIR for the root and HARNESS_NOW for the clock, uses set -uo pipefail (not -e), quotes every expansion, has no network command, never reads ~/.claude, .env*, *.pem, secrets/ or credentials, and is executable (git mode 100755).
  11. Agent definitions (.claude/agents/*.md): add a short "Context and handover" section to each: start from the brief and docs/ai/context/CONTEXT-INDEX.md, read only the sections it points to, and end the report with the handover headings from HANDOVER-TEMPLATE.md. Do NOT change name, description, tools, disallowedTools, model, maxTurns or isolation.
  12. Docs (Part 8 of the brief): AGENTS.md and CLAUDE.md (where context lives, the usage states and what the lead does in each: normal, approaching = fewer subagents and less context loading, exhausted or rate-limited = stop retrying, keep progress, write the resume instruction; unknown is reported as unknown; no model switching without verification), docs/ai/ORCHESTRATION.md (new section with how context, handovers, checkpoints and usage awareness work, what is measurable, what is not: plan name, remaining allowance, per-model quotas as numbers, real billing; recovery after exhaustion; how to inspect: cat .claude/state/*.json|md, scripts/context/brief.sh <agent>), .claude/skills/phase/SKILL.md (step 1 reads CONTEXT-INDEX and CURRENT-WORK; delegation writes a handover; step 9 runs index.sh), and hook documentation where it lives today. Do not edit docs/PROGRESS.md or docs/ai/PROMPTS.md (the lead does at merge). Keep docs to what is implemented.

  Order: tests first. Add the new cases to the hook test harness (a separate file sourced or called from .claude/hooks/tests/test-hooks.sh is fine) against stubs, run them, save the red output to /private/tmp/claude-501/-Users-ale-ssd-Developer-Mortgage-Application-API-test/38287261-0d8e-41a1-a4c6-05aca4d87104/scratchpad/harness/h1-red.txt, then implement until green. Small conventional commits (AGENTS.md "Commits and PRs"; scopes harness, docs, tests), each ending with: Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>. Then run ./scripts/verify.sh (full) and the hook tests 3 times, and keep the outputs.

  Note: .claude/settings.json, .claude/hooks/** and scripts/verify.sh are on the "ask" permission list, so Alessandro is prompted for those edits; batch your edits to those files to keep prompts few. If an edit is denied, stop and report; do not work around it.

  Do not run the manual SubagentStart delivery check (the lead does it). Phase file docs/progress/harness-01-context.md: fill only "What was built (files)", "Tests (results)" (red counts and why, green counts, verify output trimmed, the 3 runs) and "Open issues" (deviations, anything not done or not checked). Honesty rule: never claim a result you did not see; write "not done" with the reason. Stop and report if the same step fails 3 times. Keep tool use tight.

  Report at the end: commits, files, red and green evidence, verify output, the 3 runs, and every deviation from the plan.
  ```
- **Purpose:** build the approved full design test-first.
- **Outcome:** stopped by a usage limit before writing anything, then resumed (H-07); built and committed the full design; stopped by the lead after H-08.

### H-07 · agent→subagent (implementer, resumed) · 2026-11-15
- **Prompt (verbatim):**
  ```
  Your run stopped on a usage limit after you started .claude/hooks/tests/test-context.sh (untracked, not committed; branch harness-01-context, nothing else changed). The limit has reset. Continue from there with the same instructions as before: tests first against stubs, save the red output, implement until green, small commits, full verify, hook tests 3 times, phase-file sections, then report. Keep tool use tight.
  ```
- **Purpose:** resume after the usage limit.
- **Outcome:** commits `d85a25b`, `e51a48e`, `f7fe43b`, `7d45ced`, `83f6193`; stopped by the lead (H-08) while adding one more test.

### H-08 · human→agent · 2026-11-15
- **Prompt (verbatim):**
  > also its continously asking me to approve commands about the session context here... fix it, what i meant was just to be "aware" of our usages and smartly use AI models to complete the job making sure all its done correctly
- **Purpose:** stop the approval prompts and narrow the scope.
- **Outcome:** the implementer was stopped; the lead asked two questions (H-09).

### H-09 · human→agent (answers to the lead's questions) · 2026-11-15
- **Questions and answers (verbatim):** "What should H1 keep?" → "Light (Recommended)". "May I add these two entries to .claude/settings.json so they stop: Bash(.claude/hooks/tests/*) and Bash(scripts/context/*)?" → "Yes, add both (Recommended)".
- **Outcome:** cut to the light version; the allow entries were later not needed (see "Scope change").

### H-10 · human→agent · 2026-11-15
- **Prompt (verbatim):**
  > its still doing the same... do not check the context for now on the chat anymore, just continue as before
- **Purpose:** remove the session-context output from the chat entirely.
- **Outcome:** settings, hooks, `verify.sh` and `.gitignore` restored to `main`; the status line and the session-start hook removed as well; H1 is now written rules and the agent registry only.

### H-11 · agent→subagent (`dotnet-reviewer`) · 2026-11-15
- **Prompt (verbatim):**
  ```
  Fresh-context review of harness phase H1 (light version) in /Users/ale_ssd/Developer/Mortgage-Application-API-test, branch harness-01-context. Read only. The diff is docs only: run git diff main...HEAD (AGENTS.md, CLAUDE.md, docs/ai/ORCHESTRATION.md, docs/ai/context/AGENT-REGISTRY.md, docs/progress/harness-01-context.md, docs/progress/harness-01/test-proposal.md).

  Check: (1) every claim about Claude Code behaviour or usage reporting is either sourced in the phase file's discovery report or stated as not available, nothing invented; (2) the agent registry matches the real frontmatter in .claude/agents/*.md (models, tools, maxTurns) and changes no agent; (3) the rules keep the human checkpoints, author/reviewer independence and "decisions reserved for Alessandro"; (4) the phase file honestly records the scope change, what was removed and why, and that .claude/settings.json, .claude/hooks/, scripts/verify.sh and .gitignore equal main (check with git diff main -- those paths); (5) plain English per docs/CLAUDE.md, no contradictions between CLAUDE.md, AGENTS.md, ORCHESTRATION.md and the registry. Output: findings, most severe first, as file:line | severity | problem | fix, then what you checked and found fine.
  ```
- **Purpose:** fresh-context review of the light version (docs-only diff).
- **Outcome:** no critical or high findings; 5 medium and 10 low, see "Review gate".

### H-12 · human→agent · 2026-11-15
- **Prompt (verbatim):**
  > Approved, merge
- **Purpose:** Checkpoint B approval of PR #10.
- **Outcome:** squash-merged as `1d7ca9e` after both CI jobs passed on `5db990c`.

---

## Phase 07: Authentication + authorisation

### P07-01 · human→agent · 2026-11-15
- **Prompt (verbatim):**
  > continue with phase 7
- **Purpose:** start Phase 7.
- **Outcome:** Phase 7 plan and Checkpoint A (`docs/progress/phase-07-auth.md`).

### P07-02 · human→agent · 2026-11-15
- **Prompt (verbatim):**
  > Approved, all A
- **Purpose:** Checkpoint A approval (P7-D1 to P7-D4 and P7-D9 all option A, package approved).
- **Outcome:** decisions recorded; branch `phase-07-auth` created; `test-designer` run (P07-03).

### P07-03 · agent→subagent (`test-designer`) · 2026-11-15
- **Prompt (verbatim):**
  ```
  Phase 7 (authentication + authorisation) of a .NET 10 mortgage-application API (Minimal APIs, EF Core SQLite, background assessment worker, OpenTelemetry). Propose test cases for the approved plan; do not write code. If your agent instructions mention scripts/context/brief.sh, CONTEXT-INDEX.md or a handover template, ignore that: those files were removed.

  Read first in /Users/ale_ssd/Developer/Mortgage-Application-API-test (branch phase-07-auth): docs/progress/phase-07-auth.md (goal, plan, decisions P7-D1 to P7-D4 and P7-D9, all option A), docs/layers/PRODUCTION-LAYERS.md ("Auth & permissions" row), .claude/rules/security.md, .claude/rules/tests.md, tests/CLAUDE.md, and the code and tests: src/MortgageApplications.Api/Program.cs, src/MortgageApplications.Api/Identity/*, src/MortgageApplications.Api/Applications/ApplicationEndpoints.cs, tests/MortgageApplications.IntegrationTests/ (ApiFactory, Http, NotFoundAndOwnerTests, StartupTests, ErrorHandlingTests, ObservabilityLogTests, TelemetryAllowlistTests), compose.yaml, .github/workflows/ci.yml.

  What will be built (not written yet): JWT bearer authentication with ES256 (ECDSA P-256), issuer, audience, lifetime and signature validated, algorithm pinned, MapInboundClaims = false, ClockSkew 30 s; fallback authorization policy RequireAuthenticatedUser with AllowAnonymous only on /health and POST /dev/token; ClaimsCurrentUser reading sub (missing, empty or longer than 128 characters rejected as 401); options Jwt (issuer, audience, lifetime) and DevAuth (enabled) validated at startup; outside Development startup fails if DevAuth:Enabled is true or no validation key or authority is configured; POST /dev/token {"subject":"alice"} (pattern ^[a-z0-9-]{1,64}$) returns {token, expiresAt} (15 min), registered only in Development with DevAuth:Enabled=true, signed with a key pair generated in memory at startup; an OpenAPI bearer security scheme in Development; compose sets DevAuth__Enabled=true; the CI docker job gets a token first; ApiFactory owns its own test key and sends a token for a default test owner on every request, and existing ICurrentUser overrides become tokens for other subjects.

  Cover at least: 401 with no token, a malformed token, a bad signature, a token signed with another key, expired (just past the 30 s skew and just inside it), not yet valid, wrong issuer, wrong audience, alg none / unsigned, HS256 with the public key as secret (algorithm confusion), a token without sub, an empty sub, a 129-character sub (and 128 accepted); 401 responses are ProblemDetails with a WWW-Authenticate header and leak no token or exception text; every application endpoint requires auth (enumerate them from the endpoint data source so a new endpoint cannot slip through) and /health does not; cross-user: another subject's application and assessment are 404 with the same body as an unknown ID, and nothing is created or queued for them; owner comes only from sub (a body or header claiming another owner has no effect); dev endpoint: 404 in Production and Staging even with the flag, 404 in Development without the flag, works in Development with the flag, subject pattern boundaries (1 and 64 characters, 65, uppercase, symbols, empty, missing, extra properties), tokens it issues are accepted, expiresAt about 15 minutes, a token from one app instance is rejected by another (in-memory key); startup fails when DevAuth:Enabled=true outside Development and when nothing to validate tokens is configured outside Development; the background worker still processes items (it has no user); telemetry and logs never contain the token, the sub value (owner ID) or the Authorization header (extend the Phase 5 allowlist tests); the OpenAPI document declares the bearer scheme in Development; existing tests keep their meaning. Say which are unit vs integration tests, how tests create tokens without new packages (the JwtBearer package brings Microsoft.IdentityModel.JsonWebTokens transitively through the Api project reference), how to control time for expiry tests without sleeping, and which existing tests must change.

  Use your agent's output format. Mark each case must or nice, keep the list tight, and list ambiguities in the plan for Alessandro to settle.
  ```
- **Purpose:** propose Phase 7 test cases before any test is written.
- **Outcome:** about 70 cases (AU01–AU112) and 14 questions (A1–A14); full text in `docs/progress/phase-07/test-proposal.md`.

### P07-04 · human→agent · 2026-11-15
- **Prompt (verbatim):**
  > Approved
- **Purpose:** approval of the recommended test selection and the answers A1–A14.
- **Outcome:** implementation delegated, test-first (P07-05).

### P07-05 to P07-07 · agent→subagent (implementer and first gates in Claude Code) · 2026-11-15 to 2026-11-16
- **Prompt:** not recorded verbatim before the session switch from Claude Code to Antigravity IDE (honesty rule: not reconstructed).
- **Purpose:** implement Phase 7 test-first (`9b259ee`–`cf6dad0`), draft ADR 0004 (`5430b17`), run the first security and review gates (`G1`–`G10`), and fix `G1`–`G10` test-first (`828573e`, `7b681a5`, `64b90e0`, `e0ba9d2`, `3320a82`).
- **Outcome:** 537 tests passing at `3320a82`; see `docs/progress/phase-07-auth.md`.

### P07-08 · human→agent (Antigravity IDE · Gemini 4 Argon) · 2026-11-16
- **Prompt (verbatim):**
  > ok so continue where it stopped, and when tracking down tasks and what we did, state that we tried also to switch to different AI models and IDEs to showcase that these skills, agents and instructions can be used seamessly in different environments
- **Purpose:** resume Phase 7 from `3320a82` in Antigravity IDE (Gemini 4 Argon), run the `.claude/agents/` gate subagents, fix any findings, update phase and progress records, and open the PR.
- **Outcome:** `security-auditor` (P07-09), `dotnet-reviewer` (P07-10) and `spec-checker` (P07-11) defined from `.claude/agents/*.md` and run; 5 findings from P07-10 fixed test-first (`83b2bd3`, `8f86bce`).

### P07-09 · agent→subagent (`security-auditor` in Antigravity · Gemini 4 Argon) · 2026-11-16
- **Prompt (verbatim):**
  ```
  Security gate for Phase 7 (authentication + authorisation) of a .NET 10 mortgage-application API in /Users/ale_ssd/Developer/Mortgage-Application-API-test, branch phase-07-auth (HEAD 3320a82). Read only: do not edit files or switch branches.

  Read first: .claude/rules/security.md, AGENTS.md, docs/progress/phase-07-auth.md (goal, plan, decisions P7-D1 to P7-D4 and P7-D9, approved answers A1-A14, "What was built", "Tests > Results after the gates", "Open issues"), docs/adr/0004-authentication-and-authorisation.md.

  Do:
  1. Run `gitleaks git --no-banner --redact -v` (or `--log-opts="main..HEAD"`) and `gitleaks dir --no-banner --redact .`
  2. Run `semgrep scan --config p/csharp --config p/secrets --error --quiet` (and with `--baseline-commit main`).
  3. Run `dotnet list MortgageApplications.slnx package --vulnerable --include-transitive`.
  4. Review the diff (`git diff main...HEAD`) against OWASP Top 10:2025 and the Phase 7 security requirements:
     - ES256 (ECDSA P-256) pinned via `ValidAlgorithms`, `RequireSignedTokens`, `RequireExpirationTime`, `ClockSkew` 30 s, `MapInboundClaims = false`, `IncludeErrorDetails = false`.
     - `Authority` and `MetadataAddress` cleared so `Authentication:Schemes:Bearer:*` cannot add trusted keys; header `kid`, `jwk`, `jku` do not resolve keys.
     - `SigningPublicKey` accepts only a `PUBLIC KEY` PEM on P-256; outside Development startup fails if `Jwt:SigningPublicKey` is missing or if `DevAuth:Enabled` is true; startup also fails if `DevAuth:Enabled` is true while `Jwt:SigningPublicKey` is set.
     - `POST /dev/token` is mapped only in `Development` with `DevAuth:Enabled=true`, validates `subject` with `^[a-z0-9-]{1,64}\z`, issues 15-minute tokens with an in-memory key pair.
     - Fallback authorization policy `RequireAuthenticatedUser`; `AllowAnonymous` only on `/health`, `/dev/token` (when registered), and in Development OpenAPI and Scalar.
     - `SubjectClaim` / `ClaimsCurrentUser` requires a single JSON string `sub`, non-blank, <= 128 chars; owner comes only from `sub`; another user's application or assessment returns an identical 404.
     - No token, `Authorization` header, `sub` (owner ID), issuer or exception text leaks in logs, spans, metrics, ProblemDetails or `WWW-Authenticate`; `Microsoft.AspNetCore.Authentication` is at `Warning` and app-owned `TokenRejected` logs only `FailureType`.
     - CI `.github/workflows/ci.yml` masks the dev token (`::add-mask::`) and passes it via `@auth-header.txt`.

  Output:
  - Exact commands run and trimmed outputs.
  - A findings table: `tool/check | finding (file:line) | severity | real or false positive | reason | fix`.
  - A one-line verdict: `PASS` or `FAIL`.
  ```
- **Purpose:** security gate on `phase-07-auth` at `3320a82`.
- **Outcome:** **PASS**. Gitleaks (146 commits + working tree): 0 leaks; Semgrep (`p/csharp` + `p/secrets`, full and `--baseline-commit main`): 0 findings; `dotnet list package --vulnerable --include-transitive`: 0 vulnerable packages; OWASP Top 10:2025: 1 low/info item (`FailureType` log key not yet in `TelemetryAllowlistTests._logKeys`).

### P07-10 · agent→subagent (`dotnet-reviewer` in Antigravity · Gemini 4 Argon) · 2026-11-16
- **Prompt (verbatim):**
  ```
  Fresh-context diff review of Phase 7 (authentication + authorisation) in /Users/ale_ssd/Developer/Mortgage-Application-API-test, branch phase-07-auth (HEAD 3320a82). Read only: do not edit files, do not build or run tests.

  Get the diff with `git diff main...HEAD`. Read the touched files in full (`src/MortgageApplications.Api/Program.cs`, `src/MortgageApplications.Api/Identity/*`, `src/MortgageApplications.Api/appsettings.json`, `src/MortgageApplications.Api/Properties/launchSettings.json`, `src/MortgageApplications.Api/requests.http`, `compose.yaml`, `.github/workflows/ci.yml`, `Directory.Packages.props`, `src/MortgageApplications.Api/MortgageApplications.Api.csproj`, `tests/MortgageApplications.IntegrationTests/*`), plus `AGENTS.md`, `.claude/rules/backend.md`, `.claude/rules/security.md`, `.claude/rules/tests.md`, `tests/CLAUDE.md`, `docs/progress/phase-07-auth.md` (plan, decisions, approved test cases and answers A1-A14, "Tests > Results after the gates", "Open issues"), and `docs/adr/0004-authentication-and-authorisation.md`.

  Check in priority order:
  1. Security & auth correctness: ES256 validation, key handling (`TokenKeys`, `SigningPublicKey`, `CryptoProviderFactory` lifetime and disposal), `SubjectClaim` JSON payload parsing and claim validation, `ClaimsCurrentUser` lifetime and fallback behaviour, `DevTokenEndpoint` / `DevTokenIssuer` gating and Startup validation (`JwtOptionsValidator`), `OnChallenge` header formatting, OpenAPI `BearerSecurityTransformer`.
  2. Framework usage: DI lifetimes (`TokenKeys` singleton vs options validation, `IHttpContextAccessor` + scoped `ICurrentUser`), middleware order (`UseExceptionHandler`, `UseStatusCodePages`, `UseAuthentication`, `UseAuthorization`), endpoint metadata.
  3. Test gaps: whether the tests in `AuthenticationTests`, `AuthorizationTests`, `DevTokenTests`, `AuthTelemetryTests`, `StartupTests`, `LocalRunFileTests`, `ComposeFileTests` can fail when the behaviour under test breaks, and whether any approved must-case in `docs/progress/phase-07-auth.md` is missing or weak.
  4. Over-engineering or dead code.

  Output only a list, most severe first, one finding per line:
  `file:line | severity (critical/high/medium/low) | why it is wrong | concrete fix`
  If you find nothing real, output `No findings.` and the list of files you reviewed. State plainly that you did not build or run tests.
  ```
- **Purpose:** second fresh-context review of `phase-07-auth` at `3320a82`.
- **Outcome:** 1 medium and 4 low findings (R2-1 to R2-5), all fixed test-first in `83b2bd3` and `8f86bce`.

### P07-11 · agent→subagent (`spec-checker` in Antigravity · Gemini 4 Argon) · 2026-11-16
- **Prompt (verbatim):**
  ```
  Requirement traceability check at the end of Phase 7 (authentication + authorisation) in /Users/ale_ssd/Developer/Mortgage-Application-API-test, branch phase-07-auth (HEAD 3320a82). Read only: do not edit files.

  Read:
  - `docs/spec/ORIGINAL_SPEC.md` and `docs/spec/affordability-rules.md`
  - `docs/PROGRESS.md` (the existing R1-R55 traceability table)
  - `docs/progress/phase-07-auth.md` (goal, plan, decisions, what was built, tests, open issues)
  - `docs/adr/0004-authentication-and-authorisation.md`
  - The Phase 7 code and tests under `src/MortgageApplications.Api/` and `tests/MortgageApplications.IntegrationTests/`

  Do:
  1. Check every Phase 7 goal and approved requirement (R31 optional authentication, owner scoping from `sub`, 401 on missing/invalid tokens with `WWW-Authenticate` and ProblemDetails, cross-user 404, fail-closed `POST /dev/token`, no token or `sub` in logs/spans/metrics, OpenAPI bearer scheme in Development, Docker compose + CI smoke test with dev token) and map each to the file:line and test name that meet it.
  2. Check how the R1-R55 table in `docs/PROGRESS.md` changes after Phase 7 (specifically R31 and the overall met / partial / missing totals, plus whether R6/section 6 of `docs/spec/affordability-rules.md` is affected by the new `TokenRejected` log event's `FailureType` field).
  3. Output a Markdown table for the Phase 7 requirements and the updated R1-R55 summary counts, plus at most 5 short notes on any open items or discrepancies.
  ```
- **Purpose:** requirement traceability at the end of Phase 7.
- **Outcome:** R31 is **met** (all four Optional Enhancements R30–R33 are now met); overall totals move to **30 met · 10 partial · 15 missing**.

### P07-12 · human→agent (Antigravity IDE · Gemini 4 Argon) · 2026-11-16
- **Prompt (verbatim):**
  > Proceed with option a for both of them and continue
- **Purpose:** Checkpoint B approval on PR #11 (Option A for anonymous unknown paths returning `401`, Option A for adding `FailureType` to rules §6 and `TelemetryAllowlistTests._logKeys`, ADR 0004 Accepted, merge PR #11 and continue to Phase 8).
- **Outcome:** `FailureType` added to `docs/spec/affordability-rules.md` §6 and `TelemetryAllowlistTests._logKeys`, ADR 0004 marked `Accepted`, PR #11 squash-merged.
