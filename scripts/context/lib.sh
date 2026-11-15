#!/usr/bin/env bash
# Shared helpers for scripts/context/*.sh, the H1 hooks and .claude/statusline.sh. Sourced, never run.
# Code comes from this repo; data comes from CLAUDE_PROJECT_DIR (the fixture in tests).
# The clock is HARNESS_NOW (Unix seconds) when set, else `date +%s`. File content is data: it is
# printed or parsed, never executed.
set -uo pipefail

CTX_STALE_SECONDS=900 # A8: a status-line snapshot older than 15 minutes is stale
CTX_HANDOVER_HEADINGS="## Task ID|## Responsibility|## Context references|## Work done|## Files|## Discoveries|## Concerns|## Verification|## Next action"

ctx_init() { # sets CTX_ROOT, CTX_NOW, CTX_STATE
  local lib_repo
  lib_repo="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
  CTX_ROOT="${CLAUDE_PROJECT_DIR:-$lib_repo}"
  [[ -d "$CTX_ROOT" ]] || CTX_ROOT="$lib_repo"
  if [[ "${HARNESS_NOW:-}" =~ ^[0-9]{1,12}$ ]]; then CTX_NOW=$((10#$HARNESS_NOW)); else CTX_NOW="$(date +%s)"; fi
  [[ "$CTX_NOW" =~ ^[0-9]+$ ]] || CTX_NOW=0
  CTX_STATE="$CTX_ROOT/.claude/state"
}

ctx_has_jq() { command -v jq >/dev/null 2>&1; }

ctx_read_stdin() { # hook input, capped at 2 MB; never blocks on a terminal
  [[ -t 0 ]] && return 0
  head -c 2097152 2>/dev/null
}

ctx_clean() { tr -d '\000-\010\013-\037\177'; } # drop control characters, keep tab and newline

ctx_fmt_utc() { # Unix seconds -> "YYYY-MM-DD HH:MM UTC". Pure arithmetic, so BSD and GNU date do not matter.
  local t=${1:-} d s z era doe yoe y doy mp m dd
  [[ "$t" =~ ^[0-9]{1,12}$ ]] || { printf 'unknown'; return 0; }
  t=$((10#$t)); d=$((t / 86400)); s=$((t % 86400))
  z=$((d + 719468)); era=$((z / 146097)); doe=$((z - era * 146097))
  yoe=$(((doe - doe / 1460 + doe / 36524 - doe / 146096) / 365))
  y=$((yoe + era * 400)); doy=$((doe - (365 * yoe + yoe / 4 - yoe / 100)))
  mp=$(((5 * doy + 2) / 153)); dd=$((doy - (153 * mp + 2) / 5 + 1))
  if ((mp < 10)); then m=$((mp + 3)); else m=$((mp - 9)); fi
  ((m <= 2)) && y=$((y + 1))
  printf '%04d-%02d-%02d %02d:%02d UTC' "$y" "$m" "$dd" $((s / 3600)) $((s % 3600 / 60))
}

ctx_write_atomic() { # ctx_write_atomic <dest>: stdin -> temp file in the same dir -> mv
  local dest=$1 dir tmp
  dir="$(dirname "$dest")"
  mkdir -p "$dir" 2>/dev/null || return 1
  tmp="$(mktemp "$dir/.$(basename "$dest").XXXXXX" 2>/dev/null)" || return 1
  if cat >"$tmp" && mv -f "$tmp" "$dest"; then return 0; fi
  rm -f "$tmp"
  return 1
}

ctx_field() { # ctx_field <file> <label>: value of a "- **Label:** value" line, cleaned, capped
  [[ -f "$1" ]] || return 0
  awk -v p="- **$2:**" 'index($0, p) == 1 { print substr($0, length(p) + 1); exit }' "$1" |
    ctx_clean | sed 's/^ *//' | cut -c1-300
}

ctx_branch() {
  local b
  b="$(git -C "$CTX_ROOT" branch --show-current 2>/dev/null)"
  printf '%s' "${b:-unknown}" | ctx_clean
}

ctx_next_action() { # the CURRENT-WORK "Next action" line, or a pointer when it is missing
  local n
  n="$(ctx_field "$CTX_ROOT/docs/ai/context/CURRENT-WORK.md" "Next action")"
  printf '%s' "${n:-see docs/ai/context/CURRENT-WORK.md and docs/PROGRESS.md, Resume here.}"
}

ctx_handover_problem() { # prints why a handover file is malformed; prints nothing when it is fine
  local f=$1 name missing="" h rest
  name="$(basename "$f")"
  if ! [[ "$name" =~ ^H[0-9]+-[0-9]{3}-[a-z0-9-]+\.md$ ]]; then printf 'bad name (use H<phase>-<NNN>-<agent>.md)'; return 0; fi
  if ! grep -Iq . "$f" 2>/dev/null; then printf 'empty or not text'; return 0; fi
  rest="$CTX_HANDOVER_HEADINGS"
  while [[ -n "$rest" ]]; do
    h="${rest%%|*}"; [[ "$rest" == *"|"* ]] && rest="${rest#*|}" || rest=""
    grep -qxF -- "$h" "$f" || missing="${missing:+$missing, }\"$h\""
  done
  [[ -n "$missing" ]] && printf 'missing %s' "$missing"
  return 0
}

ctx_handover_files() { # handover files, newest first: H<phase>-<NNN>, both numeric
  local dir="$CTX_ROOT/docs/ai/context/handovers" f
  [[ -d "$dir" ]] || return 0
  for f in "$dir"/*; do
    [[ -f "$f" ]] || continue
    [[ "$(basename "$f")" == README.md ]] && continue
    printf '%s\n' "$(basename "$f")"
  done | LC_ALL=C sort -t- -k1.2,1nr -k2,2nr -k3,3r
}

ctx_blocker_line() { # an active rate-limit blocker, given the snapshot time ($1, may be empty)
  local b="$CTX_STATE/blocker.md" err at rs
  [[ -f "$b" ]] || return 0
  err="$(sed -n 's/^- error: \([a-z_]*\)$/\1/p' "$b" | head -n 1)"
  at="$(sed -n 's/^- at_epoch: \([0-9]\{1,12\}\)$/\1/p' "$b" | head -n 1)"
  rs="$(sed -n 's/^- reset_epoch: \([0-9]\{1,12\}\)$/\1/p' "$b" | head -n 1)"
  [[ "$err" == rate_limit && -n "$at" ]] || return 0
  [[ -n "${1:-}" ]] && (($1 > at)) && return 0 # newer status-line data wins (E8)
  [[ -n "$rs" ]] && ((rs <= CTX_NOW)) && return 0
  if [[ -n "$rs" ]]; then
    printf 'usage: exhausted (rate limit at %s; resets %s)' "$(ctx_fmt_utc "$at")" "$(ctx_fmt_utc "$rs")"
  else
    printf 'usage: exhausted (rate limit at %s; reset time unknown)' "$(ctx_fmt_utc "$at")"
  fi
}

ctx_usage_status() { # one line: the usage state for briefs and SessionStart
  local f="$CTX_STATE/usage.json" row upd st p5 p7 rs blk
  if [[ ! -f "$f" ]]; then
    blk="$(ctx_blocker_line "")"; printf '%s\n' "${blk:-usage: unknown (no status-line data)}"; return 0
  fi
  if ! ctx_has_jq; then printf 'usage: unknown (jq not installed)\n'; return 0; fi
  row="$(jq -r --argjson now "$CTX_NOW" '
    def pct(w): ([w | objects | .used_pct | numbers | select(. >= 0) | (. * 10 | round) / 10] | first) // "?";
    def rs(w): [w | objects | .resets_at | numbers | select(. > $now) | floor];
    if type == "object" and (.updated_at | type) == "number" then
      [ (.updated_at | floor),
        ((.state | strings | select(. == "normal" or . == "approaching" or . == "exhausted" or . == "unknown")) // "unknown"),
        pct(.five_hour), pct(.seven_day),
        ((rs(.five_hour) + rs(.seven_day)) | min) // "none" ] | @tsv
    else empty end' "$f" 2>/dev/null | head -n 1)"
  if [[ -z "$row" ]]; then
    blk="$(ctx_blocker_line "")"; printf '%s\n' "${blk:-usage: unknown (status-line snapshot unreadable)}"; return 0
  fi
  IFS=$'\t' read -r upd st p5 p7 rs <<<"$row"
  [[ "$upd" =~ ^[0-9]{1,12}$ ]] || { printf 'usage: unknown (status-line snapshot unreadable)\n'; return 0; }
  blk="$(ctx_blocker_line "$upd")"
  if [[ -n "$blk" ]]; then printf '%s\n' "$blk"; return 0; fi
  if ((CTX_NOW - upd > CTX_STALE_SECONDS)); then
    printf 'usage: unknown (no status-line data since %s)\n' "$(ctx_fmt_utc "$upd")"; return 0
  fi
  [[ "$p5" == "?" ]] || p5="$p5%"
  [[ "$p7" == "?" ]] || p7="$p7%"
  if [[ "$rs" == none ]]; then rs="unknown"; else rs="$(ctx_fmt_utc "$rs")"; fi
  printf 'usage: %s (5h %s, 7d %s, earliest reset %s; status line at %s)\n' "$st" "$p5" "$p7" "$rs" "$(ctx_fmt_utc "$upd")"
}
