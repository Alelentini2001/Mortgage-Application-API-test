#!/usr/bin/env bash
# SubagentStart: adds the role brief (scripts/context/brief.sh) to the subagent as
# hookSpecificOutput.additionalContext. An unknown, empty or hostile agent_type gets the generic brief.
# The JSON is built by jq, so every value is escaped. Never exits non-zero.
set -uo pipefail

here="$(cd "$(dirname "$0")" && pwd)"
# shellcheck source=../../scripts/context/lib.sh
. "$here/../../scripts/context/lib.sh"
ctx_init
brief_sh="$here/../../scripts/context/brief.sh"

if ! ctx_has_jq; then
  # Fixed text only: nothing from the input or the repo goes into hand-written JSON.
  printf '%s\n' '{"hookSpecificOutput":{"hookEventName":"SubagentStart","additionalContext":"Context brief unavailable (jq not installed). Start from docs/ai/context/CONTEXT-INDEX.md and docs/ai/context/CURRENT-WORK.md. Checkpoint A (plan) and Checkpoint B (PR): wait for Alessandro'"'"'s approval. The author never grades its own work. Decisions reserved for Alessandro are never made by an agent."}}'
  exit 0
fi

input="$(ctx_read_stdin)"
agent="$(jq -r '.agent_type | strings' <<<"$input" 2>/dev/null | head -n 1)"
brief=""
if [[ "$agent" =~ ^[a-z0-9][a-z0-9-]{0,63}$ ]]; then
  brief="$(CLAUDE_PROJECT_DIR="$CTX_ROOT" "$brief_sh" "$agent" 2>/dev/null)" || brief=""
fi
[[ -n "$brief" ]] || brief="$(CLAUDE_PROJECT_DIR="$CTX_ROOT" "$brief_sh" --generic 2>/dev/null)"
[[ -n "$brief" ]] || brief="Context brief unavailable. Start from docs/ai/context/CONTEXT-INDEX.md."

jq -n --arg ctx "$brief" '{hookSpecificOutput: {hookEventName: "SubagentStart", additionalContext: $ctx}}' 2>/dev/null ||
  printf '%s\n' '{"hookSpecificOutput":{"hookEventName":"SubagentStart","additionalContext":"Context brief unavailable. Start from docs/ai/context/CONTEXT-INDEX.md."}}'
exit 0
