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
