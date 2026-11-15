---
name: phase
description: Run the phase loop (Definition of Done) for one project phase, stopping at Checkpoint A and Checkpoint B.
argument-hint: "<NN> <name>"
arguments: [number, name]
disable-model-invocation: true
---

Run phase $number (`$name`) through the loop below. Phase file: `docs/progress/phase-$number-$name.md` (copy `docs/progress/_TEMPLATE.md` if missing).

1. **Resume.** Read `docs/PROGRESS.md` ("Resume here"), `docs/ai/context/CONTEXT-INDEX.md`, `docs/ai/context/CURRENT-WORK.md` and the last phase file (only the sections the index points to). Say in 3 lines where we are, including the usage status line.
2. **Explore and plan.** Explore with scoped subagents if needed. Write the plan into the phase file, list every decision that needs Alessandro (2-3 options, trade-offs, recommendation). → **CHECKPOINT A: stop and wait for `approved`.**
3. **Branch.** `git switch -c phase-$number-$name` (or a worktree via `parallel-implementer`, only for approved parallel phases).
4. **Docs check.** Verify every .NET API you will use on learn.microsoft.com; record the links in the phase file.
5. **Test-first.** Ask the `test-designer` subagent for cases → Alessandro approves → write failing tests, show them failing → implement → green. Small conventional commits.
6. **Verify.** `./scripts/verify.sh` (full). Show the output.
7. **Security gate.** `security-auditor` subagent (+ `/security-review` if useful). Fix or justify every finding.
8. **Review gate.** `dotnet-reviewer` subagent on `git diff main...HEAD`, then `spec-checker`. Fix real gaps only.
9. **Evidence.** Complete the phase file; append verbatim subagent prompts to `docs/ai/PROMPTS.md`; update `docs/ai/DESIGN-JOURNAL.md`; write ADRs with `/adr`; run `scripts/context/index.sh` and commit `DECISIONS.md` and `OPEN-ITEMS.md`; update `docs/ai/context/CURRENT-WORK.md`.

Delegation (any step): reference the agent's brief (`scripts/context/brief.sh <agent> [phase]`) in the prompt, update CURRENT-WORK first, and save the agent's report as a handover in `docs/ai/context/handovers/` (`H<phase>-<NNN>-<agent>.md`, headings from `HANDOVER-TEMPLATE.md`). Give reviewers the diff and the spec, not earlier reviewers' findings, unless the task is a re-check.
10. **PR.** Push, `gh pr create` (body = phase summary). Run the AI reviews **locally on Alessandro's subscription** (no API key in CI): `/code-review` and `/security-review` on the PR branch. Post one summary comment to the PR with `gh pr comment` (findings + resolution), fix real findings, wait for CI, summarise. → **CHECKPOINT B: stop and wait for `approved`.** Then squash-merge with a written message (`AGENTS.md`, "Commits and PRs"), update `docs/PROGRESS.md`, and tell Alessandro to `/clear`.

Stop conditions: same step fails 3 times → stop and ask. Corrected twice on the same issue → propose `/clear` and a better prompt.
