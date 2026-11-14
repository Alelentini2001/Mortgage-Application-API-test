---
name: adr
description: Create a numbered Architecture Decision Record in docs/adr.
argument-hint: "<title>"
disable-model-invocation: true
---

1. Find the next number: highest `docs/adr/NNNN-*.md` + 1 (start at 0001).
2. Create `docs/adr/NNNN-<kebab-case of "$ARGUMENTS">.md` from `docs/adr/_TEMPLATE.md`.
3. Fill context, options considered (with trade-offs), decision, consequences and **decided by** from this conversation. If Alessandro has not decided yet, set status `Proposed` and stop.
4. Link the ADR from the current phase file's "Decisions" section.
