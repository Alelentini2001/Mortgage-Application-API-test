# Handover H<phase>-<NNN>-<agent>

One handover per delegated task. Save it as `handovers/H<phase>-<NNN>-<agent>.md` (for example `H1-001-test-designer.md`). Agents end their final report with the headings below; the lead saves the file, because read-only agents cannot write. Keep every heading (write "none" under an empty one): `scripts/context/index.sh --check` fails when one is missing. Link to files and commits instead of copying them. Never put secrets, tokens or personal data in a handover.

## Task ID
H<phase>-<NNN>: the task in one line.

## Responsibility
Which agent, and what it was responsible for (and not responsible for).

## Context references
Files and sections read, for example `docs/progress/phase-NN-name.md` "Plan".

## Work done
What was done, in a few bullets.

## Files
Files created or changed, or "none" for a read-only agent.

## Discoveries
Facts found that the next agent needs.

## Concerns
Risks, open questions, anything not done and why.

## Verification
Commands run and their result, or "not run" with the reason.

## Next action
The exact next step and who takes it.
