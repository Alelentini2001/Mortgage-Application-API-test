#!/usr/bin/env bash
# Status line: prints one line (model, context %, 5h and 7d usage %, earliest reset, usage state) and
# writes .claude/state/usage.json atomically with allowlisted fields only (A6). Never exits non-zero.
# States: normal < 75%, approaching >= 75%, exhausted >= 100%, unknown when absent, null, negative,
# non-numeric or past its reset (A7). Without jq: prints and stores unknown, never parses numbers (A12).
set -uo pipefail

here="$(cd "$(dirname "$0")" && pwd)"
# shellcheck source=../scripts/context/lib.sh
. "$here/../scripts/context/lib.sh"
ctx_init

input="$(ctx_read_stdin)"
snapshot=""
line=""

if ctx_has_jq; then
  # One jq program builds the snapshot from an allowlist; anything else in the input is dropped.
  snapshot="$(jq -c --argjson now "$CTX_NOW" '
    def pct: if type == "number" and . >= 0 then . else null end;
    def ts: if type == "number" and . >= 0 and . < 1e12 then floor else null end;
    def text(n): if type == "string" then explode | map(select(. >= 32 and (. < 127 or . > 159))) | implode | .[0:n] else null end;
    def window(w): (w | if type == "object" then {used_pct: (.used_percentage | pct), resets_at: (.resets_at | ts)} else {used_pct: null, resets_at: null} end);
    def wstate: if .used_pct == null then "unknown"
                elif .resets_at != null and .resets_at <= $now then "unknown"
                elif .used_pct >= 100 then "exhausted"
                elif .used_pct >= 75 then "approaching"
                else "normal" end;
    if type != "object" then error("not an object") else . end
    | (.rate_limits | if type == "object" then . else {} end) as $rl
    | window($rl.five_hour) as $f | window($rl.seven_day) as $s
    | ([$f, $s] | map(wstate) | map(select(. != "unknown"))) as $known
    | {
        updated_at: $now,
        model_id: (.model | if type == "object" then .id | text(80) else null end),
        model_name: (.model | if type == "object" then .display_name | text(40) else null end),
        context_used_pct: (.context_window | if type == "object" then .used_percentage | pct else null end),
        five_hour: $f,
        seven_day: $s,
        state: (if ($known | index("exhausted")) != null then "exhausted"
                elif ($known | index("approaching")) != null then "approaching"
                elif ($known | length) > 0 then "normal"
                else "unknown" end)
      }' <<<"$input" 2>/dev/null)"
fi

if [[ -n "$snapshot" ]]; then
  line="$(jq -r --argjson now "$CTX_NOW" '
    def p: (. * 10 | round) / 10 | tostring;
    def win(tag): if .used_pct == null then "\(tag) ?"
                    elif .resets_at != null and .resets_at <= $now then "\(tag) ? (reset passed)"
                    else "\(tag) \(.used_pct | p)%" end;
    [ (.model_name // "?"),
      (if .context_used_pct == null then "ctx ?" else "ctx \(.context_used_pct | p)%" end),
      "\(.five_hour | win("5h")), \(.seven_day | win("7d"))",
      ([.five_hour, .seven_day] | map(.resets_at | numbers | select(. > $now)) | min // "none" | tostring),
      ([.five_hour, .seven_day] | map(.resets_at | numbers | select(. <= $now)) | length | tostring),
      .state,
      ([.five_hour, .seven_day] | map(select(.used_pct == null)) | length | tostring) ] | @tsv' <<<"$snapshot" 2>/dev/null)"
fi

if [[ -n "$line" ]]; then
  IFS=$'\t' read -r name ctxp windows reset passed state missing <<<"$line"
  if [[ "$reset" =~ ^[0-9]+$ ]]; then reset="reset $(ctx_fmt_utc "$reset")"
  elif [[ "$passed" != 0 ]]; then reset="reset passed"
  else reset="reset ?"; fi
  label="usage: $state"
  [[ "$state" != unknown && "$missing" != 0 ]] && label="$label (partial)"
  printf '%s | %s | %s | %s | %s\n' "$(printf '%s' "$name" | ctx_clean | tr '\n\t' '  ')" "$ctxp" "$windows" "$reset" "$label"
  printf '%s\n' "$snapshot" | ctx_write_atomic "$CTX_STATE/usage.json"
else
  printf '? | ctx ? | 5h ?, 7d ? | reset ? | usage: unknown\n'
  printf '{"updated_at":%s,"model_id":null,"model_name":null,"context_used_pct":null,"five_hour":{"used_pct":null,"resets_at":null},"seven_day":{"used_pct":null,"resets_at":null},"state":"unknown"}\n' "$CTX_NOW" |
    ctx_write_atomic "$CTX_STATE/usage.json"
fi
exit 0
