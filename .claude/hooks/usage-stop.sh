#!/usr/bin/env bash
# StopFailure: records why the turn stopped in .claude/state/blocker.md (its own file, A9), atomically,
# replacing any earlier record. Stores only the error type from an allowlist, the time, the reset time
# (from the status-line snapshot, only when it is in the future) and the resume sentence (A14).
# Only rate_limit marks usage as exhausted (A10). Message text and error details are never copied.
# Claude Code ignores this hook's output (A15), so it works only through the file. Never exits non-zero.
set -uo pipefail

here="$(cd "$(dirname "$0")" && pwd)"
# shellcheck source=../../scripts/context/lib.sh
. "$here/../../scripts/context/lib.sh"
ctx_init

input="$(ctx_read_stdin)"
error=unknown
if ctx_has_jq; then
  e="$(jq -r '.error | strings' <<<"$input" 2>/dev/null | head -n 1)"
else
  e="$(grep -Eo '"error"[[:space:]]*:[[:space:]]*"(rate_limit|overloaded)"' <<<"$input" | head -n 1 | grep -Eo 'rate_limit|overloaded')"
fi
case "$e" in
  rate_limit | overloaded) error="$e" ;;
esac

# Reset time: an exhausted window's reset if any (the latest of them), else the earliest future reset.
reset=""
if ctx_has_jq && [[ -f "$CTX_STATE/usage.json" ]]; then
  reset="$(jq -r --argjson now "$CTX_NOW" '
    [.five_hour, .seven_day] | map(objects | select((.resets_at | type) == "number" and .resets_at > $now)) as $w
    | (($w | map(select((.used_pct | type) == "number" and .used_pct >= 100) | .resets_at) | max)
       // ($w | map(.resets_at) | min)) // empty | floor' "$CTX_STATE/usage.json" 2>/dev/null | head -n 1)"
  [[ "$reset" =~ ^[0-9]{1,12}$ ]] || reset=""
fi

if [[ -n "$reset" ]]; then after="$(ctx_fmt_utc "$reset")"; reset_text="$after"; else after="the reset"; reset_text="reset time unknown"; fi
state=unchanged
[[ "$error" == rate_limit ]] && state=exhausted
next="$(ctx_next_action)"

{
  echo "# Blocker (written by .claude/hooks/usage-stop.sh on StopFailure)"
  echo "- error: $error"
  echo "- at: $(ctx_fmt_utc "$CTX_NOW")"
  echo "- at_epoch: $CTX_NOW"
  echo "- reset: $reset_text"
  echo "- reset_epoch: ${reset:-unknown}"
  echo "- usage state: $state"
  echo "- resume: Start a new session after $after; Resume here, CURRENT-WORK and the checkpoint load automatically. Next action: $next"
} | ctx_write_atomic "$CTX_STATE/blocker.md"
exit 0
