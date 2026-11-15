#!/usr/bin/env bash
# PreCompact: writes .claude/state/checkpoint.md atomically (branch, changed file paths only, last
# verify line, CURRENT-WORK, UTC time) and prints keep-instructions for the summary. The instructions
# are written as additions ("Also keep ...") so they do not replace a manual /compact instruction (A5).
# custom_instructions and file contents are never copied. Never exits non-zero.
set -uo pipefail

here="$(cd "$(dirname "$0")" && pwd)"
# shellcheck source=../../scripts/context/lib.sh
. "$here/../../scripts/context/lib.sh"
ctx_init

input="$(ctx_read_stdin)"
trigger=unknown
if ctx_has_jq; then
  t="$(jq -r '.trigger | strings' <<<"$input" 2>/dev/null | head -n 1)"
  [[ "$t" == manual || "$t" == auto ]] && trigger="$t"
fi

current="$CTX_ROOT/docs/ai/context/CURRENT-WORK.md"
progress="$CTX_ROOT/docs/PROGRESS.md"
{
  echo "# Checkpoint (written by .claude/hooks/pre-compact.sh before compaction)"
  echo "- written: $(ctx_fmt_utc "$CTX_NOW")"
  echo "- trigger: $trigger"
  echo "- branch: $(ctx_branch)"
  echo
  echo "## Changed files (paths only)"
  changed="$(git -C "$CTX_ROOT" --no-optional-locks status --porcelain --untracked-files=all 2>/dev/null | cut -c4- | ctx_clean)"
  if [[ -z "$changed" ]]; then
    echo "- none, or not a git repository"
  else
    printf '%s\n' "$changed" | head -n 50 | sed 's/^/- /'
    total="$(printf '%s\n' "$changed" | wc -l | tr -d ' ')"
    ((total > 50)) && echo "- ($((total - 50)) more)"
  fi
  echo
  echo "## Last verify (docs/PROGRESS.md)"
  last="$(ctx_field "$progress" "Last verify")"
  echo "${last:-not available}"
  echo
  echo "## Current work (docs/ai/context/CURRENT-WORK.md)"
  if [[ -f "$current" ]]; then
    ctx_clean <"$current" | grep -v '^#' | grep -v '^[[:space:]]*$' | head -n 20
  else
    echo "not available"
  fi
} | ctx_write_atomic "$CTX_STATE/checkpoint.md" || echo "warning: could not write .claude/state/checkpoint.md" >&2

cat <<'EOF'
Also keep, in addition to any instructions already given for this compaction:
- the current phase and step (docs/ai/context/CURRENT-WORK.md),
- the list of modified files,
- the test commands and their last result,
- the open decisions awaiting Alessandro.
A checkpoint with the branch, changed files, last verify and current work was written to .claude/state/checkpoint.md; it is shown again after compaction.
EOF
exit 0
