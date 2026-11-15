# Current work

Lead-maintained "now". Keep it short; update it when the step changes. The PreCompact checkpoint copies it, and the brief and the StopFailure resume sentence read the "Next action" line.

- **Phase:** H1, harness: context and usage awareness (`docs/progress/harness-01-context.md`)
- **Step:** test-first implementation (phase loop step 5), then verify and the gates
- **Branch:** `harness-01-context`
- **Files in play:** `scripts/context/`, `.claude/hooks/` (session-context, subagent-context, pre-compact, usage-stop, tests), `.claude/statusline.sh`, `docs/ai/context/`, `.claude/settings.json`, `scripts/verify.sh`, `.gitignore`
- **Next action:** run the manual SubagentStart delivery check on 2.1.283, then the security and review gates for H1.
