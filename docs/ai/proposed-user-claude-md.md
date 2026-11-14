# Proposal: personal `~/.claude/CLAUDE.md` and `~/.claude/rules/`

**Proposal only.** Nothing here has been written to your home directory. Copy what you want.

These are habits from this project that apply to every project, so they belong at user level rather than in each repo.

## Proposed additions to `~/.claude/CLAUDE.md`

```markdown
# Working agreement (all projects)
- I am the engineer of record. For real choices, give 2-3 options with trade-offs and a recommendation, then stop.
- Every task has a runnable check. Show the command and its output; don't just say it passed.
- Explore → plan → implement → commit. Use plan mode for multi-file or uncertain work.
- Reviews of your own code run in a fresh-context subagent, focused on correctness, security and requirement gaps.
- Never invent results, findings, timings or corrections. If something wasn't done, say so.
- Ask before adding packages, MCP servers, skills, permissions or secrets.
- Repo docs, commit messages and PR text are written in plain, normal English, whatever chat style is active.
```

## Proposed `~/.claude/rules/git.md` (no `paths`, always loaded)

```markdown
# Git habits
- One unit of work = one branch = one PR. Conventional commits.
- Never force-push, never `--no-verify`, never `git reset --hard` without asking.
- Branch before the first edit when on the default branch.
```

## Proposed `~/.claude/rules/dotnet.md`

```markdown
---
paths:
  - "**/*.cs"
  - "**/*.csproj"
---
# .NET habits
- Verify APIs against Microsoft Learn before using them; prefer current-SDK patterns.
- `decimal` for money; round once at the end with an explicit `MidpointRounding`.
- Warnings as errors, nullable on, central package management.
- Never log personal or financial data.
```

## Not proposed (and why)

- Hook scripts: hooks are project-specific enforcement and are already versioned in the repo's `.claude/hooks/`.
- Your current global entries (graphify trigger, caveman mode, claude-mem): left as they are. Note that caveman mode changes chat style only; the repo rules above keep written artefacts in normal English.
