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

case "$name" in
  .env|.env.*)       block "environment files may contain secrets" ;;
  *.pem|*.key|*.pfx) block "certificate/key files are never edited by the agent" ;;
esac

case "$file_path" in
  */.git/*)    block "git internals are off limits" ;;
  */secrets/*) block "secrets folders are off limits" ;;
esac

# Migrations merged to main are immutable: add a new migration instead.
# Migrations created on the current branch can still be fixed.
if [[ "$file_path" == */Migrations/*.cs ]]; then
  root="$(git -C "$(dirname "$file_path")" rev-parse --show-toplevel 2>/dev/null || true)"
  if [[ -n "$root" ]] && git -C "$root" cat-file -e "main:${file_path#"$root"/}" 2>/dev/null; then
    block "migrations merged to main are immutable; create a new migration with 'dotnet ef migrations add'"
  fi
fi

exit 0
