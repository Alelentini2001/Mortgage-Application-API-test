#!/usr/bin/env bash
# Stop: if source files changed since the last commit, run the fast verify.
# Exit 2 keeps Claude working and shows the failure. Never blocks twice in a row.
set -uo pipefail

input="$(cat)"

# Loop guard: Claude is already continuing because of a stop hook.
# grep fallback keeps the guard working even if jq is missing.
if command -v jq >/dev/null; then
  [[ "$(jq -r '.stop_hook_active // false' <<<"$input" 2>/dev/null)" == "true" ]] && exit 0
else
  grep -Eq '"stop_hook_active"[[:space:]]*:[[:space:]]*true' <<<"$input" && exit 0
fi

root="${CLAUDE_PROJECT_DIR:-$(git rev-parse --show-toplevel 2>/dev/null)}"
cd "$root" || exit 0

# Only code and build files count; docs and CLAUDE.md edits never trigger a build.
changed="$(git status --porcelain --untracked-files=all -- '*.cs' '*.csproj' '*.props' '*.slnx' global.json .editorconfig 2>/dev/null)"
[[ -z "$changed" ]] && exit 0

if ! output="$(./scripts/verify.sh --fast 2>&1)"; then
  {
    echo "verify-on-stop.sh: ./scripts/verify.sh --fast FAILED on uncommitted changes. Fix before finishing."
    echo "---- last 40 lines ----"
    tail -n 40 <<<"$output"
  } >&2
  exit 2
fi

exit 0
