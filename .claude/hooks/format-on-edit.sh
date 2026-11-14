#!/usr/bin/env bash
# PostToolUse (Edit|Write): whitespace-format the edited C# file. Never fails the edit.
# Full style/analyzer formatting is checked by scripts/verify.sh.
set -uo pipefail

file_path="$(jq -r '.tool_input.file_path // empty' 2>/dev/null)"
[[ "$file_path" == *.cs && -f "$file_path" ]] || exit 0

# Resolve the repo that contains the file, so worktrees format correctly too.
root="$(git -C "$(dirname "$file_path")" rev-parse --show-toplevel 2>/dev/null)" || exit 0
rel="${file_path#"$root"/}"

cd "$root" && dotnet format whitespace --folder --include "$rel" >/dev/null 2>&1 || true
exit 0
