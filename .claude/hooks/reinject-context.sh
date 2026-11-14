#!/usr/bin/env bash
# SessionStart (compact): print the "Resume here" section of docs/PROGRESS.md so it survives compaction.
set -uo pipefail

root="${CLAUDE_PROJECT_DIR:-$(git rev-parse --show-toplevel 2>/dev/null)}"
progress="$root/docs/PROGRESS.md"
[[ -f "$progress" ]] || exit 0

echo "Context re-injected after compaction (from docs/PROGRESS.md):"
awk '/^## Resume here/{on=1; print; next} on && /^## /{exit} on{print}' "$progress"
echo "Current branch: $(git -C "$root" branch --show-current 2>/dev/null)"
exit 0
