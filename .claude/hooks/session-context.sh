#!/usr/bin/env bash
# SessionStart (startup, resume, clear, compact, fork): prints the project context as plain stdout.
# Prints: the "Resume here" section of docs/PROGRESS.md (verbatim, capped), the branch, a pointer to
# CURRENT-WORK, the usage status, the blocker if any and, after compaction, the checkpoint.
# Missing or malformed inputs give a one-line warning. Writes nothing. Never exits non-zero.
set -uo pipefail

here="$(cd "$(dirname "$0")" && pwd)"
# shellcheck source=../../scripts/context/lib.sh
. "$here/../../scripts/context/lib.sh"
ctx_init

RESUME_CAP=50
CHECKPOINT_CAP=30

input="$(ctx_read_stdin)"
source_name=""
if [[ "${1:-}" == --source && -n "${2:-}" ]]; then
  source_name="$2"
elif ctx_has_jq; then
  source_name="$(jq -r '.source | strings' <<<"$input" 2>/dev/null | head -n 1)"
else
  # Without jq only the fixed word "compact" is recognised; nothing from the input is printed.
  grep -Eq '"source"[[:space:]]*:[[:space:]]*"compact"' <<<"$input" && source_name=compact
fi
case "$source_name" in
  startup | resume | clear | compact | fork) ;;
  *) source_name=unknown ;;
esac

progress="$CTX_ROOT/docs/PROGRESS.md"
if [[ "$source_name" == compact ]]; then
  echo "Context re-injected after compaction (from docs/PROGRESS.md):"
else
  echo "Project context at session start ($source_name), from docs/PROGRESS.md:"
fi

if [[ ! -f "$progress" ]]; then
  echo "warning: docs/PROGRESS.md not found"
elif ! grep -q '^## Resume here' "$progress"; then
  echo "warning: no Resume here section in docs/PROGRESS.md"
else
  awk -v cap="$RESUME_CAP" '
    /^## Resume here/ { on = 1; print; n = 1; next }
    on && /^## / { exit }
    on { if (n >= cap) { print "(truncated, see docs/PROGRESS.md)"; exit } print; n++ }' "$progress"
fi

echo "Current branch: $(ctx_branch)"
if [[ -f "$CTX_ROOT/docs/ai/context/CURRENT-WORK.md" ]]; then
  echo "Current work: docs/ai/context/CURRENT-WORK.md (phase, step, files in play, next action). Read it before acting."
else
  echo "warning: docs/ai/context/CURRENT-WORK.md not found"
fi
echo "Context index: docs/ai/context/CONTEXT-INDEX.md (where each fact lives; read only what your task needs)."
ctx_usage_status

if [[ -f "$CTX_STATE/blocker.md" ]]; then
  echo "Blocker from the last session (.claude/state/blocker.md):"
  grep -E '^- (error|at|reset|resume):' "$CTX_STATE/blocker.md" | ctx_clean | head -n 6
fi

if [[ "$source_name" == compact && -f "$CTX_STATE/checkpoint.md" ]]; then
  echo "Checkpoint written before compaction (.claude/state/checkpoint.md):"
  ctx_clean <"$CTX_STATE/checkpoint.md" | awk -v cap="$CHECKPOINT_CAP" 'NR > cap { print "(truncated, see .claude/state/checkpoint.md)"; exit } { print }'
fi
exit 0
