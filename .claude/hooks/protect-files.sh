#!/usr/bin/env bash
# PreToolUse (Edit|Write): block edits to secrets, migrations already merged to main, and git internals.
# Exit 2 blocks the tool call; stderr is shown to Claude as the reason.
# Regression cases: .claude/hooks/tests/test-hooks.sh
set -euo pipefail

command -v jq >/dev/null || { echo "protect-files: jq is required" >&2; exit 2; }

file_path="$(jq -r '.tool_input.file_path // empty')" || { echo "protect-files: could not parse hook input" >&2; exit 2; }
[[ -z "$file_path" ]] && exit 0

name="$(basename "$file_path")"

block() { echo "Blocked by protect-files.sh: $1 ($file_path)" >&2; exit 2; }

# Shared with block-dangerous-bash.sh: one list of protected paths.
protected_regex="$(cat "$(dirname "$0")/protected-paths.regex")" ||
  { echo "protect-files: protected-paths.regex missing" >&2; exit 2; }
if grep -Eq "$protected_regex" <<<"$file_path"; then
  block "secrets, key files and git internals are never edited by the agent"
fi

# Migrations merged to main are immutable: add a new migration instead.
# Migrations created on the current branch can still be fixed.
if [[ "$file_path" == */Migrations/*.cs ]]; then
  root="$(git -C "$(dirname "$file_path")" rev-parse --show-toplevel 2>/dev/null || true)"
  if [[ -n "$root" ]] && git -C "$root" cat-file -e "main:${file_path#"$root"/}" 2>/dev/null; then
    block "migrations merged to main are immutable; create a new migration with 'dotnet ef migrations add'"
  fi
fi

exit 0
