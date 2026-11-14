---
name: progress
description: Update docs/PROGRESS.md and the current phase file from real git, test and gate data.
disable-model-invocation: true
---

Collect real data only:
- `git log --oneline main..HEAD` and `git branch --show-current`
- `gh pr list --head "$(git branch --show-current)" --json number,url,state`
- the last `./scripts/verify.sh` result (re-run `--fast` if unknown) and the test totals it printed

Then:
1. Update the phase row in `docs/PROGRESS.md` (status, branch, PR, tests, date) and rewrite its "Resume here" section (phase, step, next action, open decisions).
2. Fill the matching sections of the current phase file (What was built, Tests, gates).
3. Never fill "Human review". Never write a number you did not observe; write "not measured" instead.
