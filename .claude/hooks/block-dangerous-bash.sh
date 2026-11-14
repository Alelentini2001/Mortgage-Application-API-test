#!/usr/bin/env bash
# PreToolUse (Bash): block destructive or verification-skipping commands.
# Exit 2 blocks the command; stderr is shown to Claude as the reason.
# Each command segment (split on ; && || | and newlines) is checked on its own,
# so flags from one command never trigger a rule for another.
set -euo pipefail

command -v jq >/dev/null || { echo "block-dangerous-bash: jq is required" >&2; exit 2; }

cmd="$(jq -r '.tool_input.command // empty')"
[[ -z "$cmd" ]] && exit 0

block() { echo "Blocked by block-dangerous-bash.sh: $1" >&2; exit 2; }

# Whole-command checks (patterns that span a pipe).
if grep -Eq '(curl|wget)[^|]*\|[[:space:]]*(sudo[[:space:]]+)?(ba|z)?sh([[:space:]]|$)' <<<"$cmd"; then
  block "piping a download into a shell is not allowed"
fi
if grep -Eq -- '--no-verify' <<<"$cmd"; then
  block "--no-verify skips the safety hooks"
fi

# Per-segment checks.
while IFS= read -r segment; do
  # Trim leading whitespace and an optional sudo/env prefix.
  segment="$(sed -E 's/^[[:space:]]+//; s/^(sudo|command|env)[[:space:]]+//' <<<"$segment")"
  read -r -a words <<<"$segment" || true
  [[ ${#words[@]} -eq 0 ]] && continue

  case "${words[0]}" in
    git)
      if [[ " $segment " == *" push "* ]]; then
        for w in "${words[@]}"; do
          [[ "$w" == "--force" || "$w" == "-f" ]] &&
            block "force push is not allowed (--force-with-lease only with Alessandro's approval)"
        done
      fi
      if [[ " $segment " == *" reset "* && " $segment " == *" --hard"* ]]; then
        block "git reset --hard discards work"
      fi
      ;;
    rm)
      flags=""
      for w in "${words[@]:1}"; do
        [[ "$w" == --recursive ]] && flags+="r"
        [[ "$w" == --force ]] && flags+="f"
        [[ "$w" =~ ^-[a-zA-Z]+$ ]] && flags+="${w#-}"
      done
      if [[ "$flags" == *[rR]* && "$flags" == *f* ]]; then
        block "rm -rf is not allowed; delete specific paths or use 'dotnet clean'"
      fi
      ;;
  esac
done < <(sed -E 's/(\&\&|\|\||;|\|)/\n/g' <<<"$cmd")

exit 0
