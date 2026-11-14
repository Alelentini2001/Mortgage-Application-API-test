---
name: verify-all
description: Run every gate (verify script, security scanners, review subagents) and print one summary table with evidence.
disable-model-invocation: true
---

Run each gate and record the exact command, exit code and a trimmed output:

1. `./scripts/verify.sh` (full).
2. `gitleaks dir --no-banner --redact .` and `gitleaks git --no-banner --redact`.
3. `semgrep scan --config p/csharp --config p/secrets --error --quiet`.
4. `dotnet list MortgageApplications.slnx package --vulnerable --include-transitive`.
5. `dotnet-reviewer` subagent on `git diff main...HEAD`.
6. `spec-checker` subagent.

Print one table: `gate | command | result (pass/fail/findings) | evidence (trimmed output or link)`.
Then a one-line overall verdict. Do not fix anything in this skill; list what needs fixing.
