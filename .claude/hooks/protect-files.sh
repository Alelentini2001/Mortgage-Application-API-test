#!/usr/bin/env bash
# PreToolUse (Edit|Write): block edits to secrets, existing EF Core migrations and git internals.
# Exit 2 blocks the tool call; stderr is shown to Claude as the reason.
set -euo pipefail

command -v jq >/dev/null || { echo "protect-files: jq is required" >&2; exit 2; }

file_path="$(jq -r '.tool_input.file_path // empty')"
[[ -z "$file_path" ]] && exit 0

name="$(basename "$file_path")"

block() { echo "Blocked by protect-files.sh: $1 ($file_path)" >&2; exit 2; }

case "$name" in
  .env|.env.*) block "environment files may contain secrets" ;;
  *.pem)       block "certificate/key files are never edited by the agent" ;;
esac

case "$file_path" in
  */.git/*) block "git internals are off limits" ;;
esac

# Existing migrations are immutable: add a new migration instead.
if [[ "$file_path" == */Migrations/*.cs && -f "$file_path" ]]; then
  block "existing migrations are immutable; create a new migration with 'dotnet ef migrations add'"
fi

exit 0
