#!/usr/bin/env bash
# Generates docs/ai/context/DECISIONS.md and docs/ai/context/OPEN-ITEMS.md from the "Decisions" tables
# and "Open issues" lists in docs/progress/*.md, the ADR headers in docs/adr/ and the carry-overs in
# docs/PROGRESS.md. Rows are short pointers with links; the linked file stays the record.
#   scripts/context/index.sh          regenerate both files
#   scripts/context/index.sh --check  write nothing; exit 1 when a file is stale, or a phase file,
#                                     ADR or handover is malformed (A1, A11)
# Output is stable: numeric phase order, document order inside a file, no timestamp.
set -uo pipefail

here="$(cd "$(dirname "$0")" && pwd)"
# shellcheck source=lib.sh
. "$here/lib.sh"
ctx_init
export LC_ALL=C

mode=write
case "${1:-}" in
  --check) mode=check ;;
  "") ;;
  *) printf 'usage: scripts/context/index.sh [--check]\n' >&2; exit 2 ;;
esac

out_dir="$CTX_ROOT/docs/ai/context"
work="$(mktemp -d 2>/dev/null)" || { printf 'index.sh: cannot create a temp dir\n' >&2; exit 1; }
trap 'rm -f "$work"/*; rmdir "$work" 2>/dev/null' EXIT
problems="$work/problems"
: >"$problems"
tab="$(printf '\t')"

# Shared awk helpers: split a table row (a "\|" stays inside its cell), strip links and bold, cap length.
AWK_LIB='
function trim(s) { sub(/^[ \t]+/, "", s); sub(/[ \t]+$/, "", s); return s }
function cells(line, arr,   n, i, j, m, parts, s) {
  gsub(/\\[|]/, "\001", line)
  sub(/^[ \t]*[|]/, "", line); sub(/[|][ \t]*$/, "", line)
  n = split(line, arr, "|")
  for (i = 1; i <= n; i++) {
    m = split(arr[i], parts, "\001"); s = parts[1]
    for (j = 2; j <= m; j++) s = s "\\|" parts[j]
    arr[i] = trim(s)
  }
  return n
}
function unlink(s,   out, t) {
  out = ""
  while (match(s, /\[[^]]*\]\([^)]*\)/)) {
    t = substr(s, RSTART + 1, RLENGTH - 1); t = substr(t, 1, index(t, "](") - 1)
    out = out substr(s, 1, RSTART - 1) t; s = substr(s, RSTART + RLENGTH)
  }
  return out s
}
function short(s, n,   t) {
  s = unlink(s); gsub(/\*\*/, "", s); gsub(/[ \t]+/, " ", s); s = trim(s)
  if (length(s) <= n) return s
  s = substr(s, 1, n)
  while (length(s) > 0 && substr(s, length(s), 1) > "\177") s = substr(s, 1, length(s) - 1)
  t = s; if (gsub(/`/, "", t) % 2) s = s "`"
  return trim(s) "..."
}
{ sub(/\r$/, "") }
'

phase_files() { # phase-NN-*.md by number, then harness-NN-*.md by number; templates and others skipped
  local f b n kind
  for f in "$CTX_ROOT"/docs/progress/*.md; do
    [[ -f "$f" ]] || continue
    b="$(basename "$f")"
    case "$b" in
      phase-[0-9]*-*.md) kind=1 ;;
      harness-[0-9]*-*.md) kind=2 ;;
      *) continue ;;
    esac
    n="${b#*-}"; n="${n%%-*}"
    [[ "$n" =~ ^[0-9]{1,4}$ ]] || continue
    printf '%s\t%s\t%s\n' "$kind" "$((10#$n))" "$b"
  done | sort -t "$tab" -k1,1n -k2,2n -k3,3 | cut -f3
}

decisions() { # decisions <file>: "ID<TAB>summary<TAB>decided by" rows, or "!<TAB>problem"
  awk "$AWK_LIB"'
    /^## / { insec = ($0 == "## Decisions" || index($0, "## Decisions (") == 1); intable = 0; next }
    !insec { next }
    /^[ \t]*[|]/ {
      if (!intable) {
        intable = 1; hn = cells($0, h); idc = 0; sc = 2; sep = 1
        for (i = 1; i <= hn; i++) { t = tolower(h[i]); if (index(t, "decided by")) idc = i; if (t == "chosen" || t == "option chosen") sc = i }
        if (!idc) print "!\tline " NR ": the Decisions table has no \"Decided by\" column"
        next
      }
      if (sep) { sep = 0; if ($0 ~ /^[ \t]*[|][ \t:|-]*$/) next }
      n = cells($0, c)
      if (n != hn) { print "!\tline " NR ": a Decisions row has " n " cells, the header has " hn; next }
      if (!idc) next
      print short(c[1], 20) "\t" short(c[sc], 100) "\t" short(c[idc], 60)
      next
    }
    { intable = 0 }' "$1"
}

open_issues() { # open_issues <file>: top-level open items; struck-through (resolved) ones skipped
  awk "$AWK_LIB"'
    /^## / { insec = (index($0, "## Open issues") == 1); next }
    insec && /^- / && !/^- ~~/ { print short(substr($0, 3), 140) }' "$1"
}

adr_row() { # adr_row <file>: "ID<TAB>title<TAB>decided by", or "!<TAB>problem"
  awk "$AWK_LIB"'
    /^# / && title == "" { title = substr($0, 3); sub(/^[0-9]+[.] */, "", title) }
    index($0, "- **Decided by:**") == 1 && by == "" { by = substr($0, 18) }
    END {
      if (title == "" || by == "") { print "!\tmissing the \"# NNNN. Title\" line or the \"- **Decided by:**\" line"; exit }
      print "ADR " id "\t" short(title, 100) "\t" short(by, 60)
    }' id="$2" "$1"
}

carry_overs() { # "Phase N<TAB>item" from the PROGRESS.md "Carry into later phases" list
  local p="$CTX_ROOT/docs/PROGRESS.md"
  [[ -f "$p" ]] || return 0
  awk "$AWK_LIB"'
    /^## / { insec = ($0 == "## Resume here"); on = 0; next }
    !insec { next }
    /^- / { on = (index($0, "Carry into later phases") > 0); next }
    on && /^  - / {
      s = substr($0, 5); label = "-"
      if (match(s, /^Phase [0-9]+:/)) { label = substr(s, 1, RLENGTH - 1); s = substr(s, RLENGTH + 1) }
      print label "\t" short(s, 140)
    }' "$p"
}

note() { printf '%s\n' "$1" >>"$problems"; }

# --- DECISIONS.md ---
dec="$work/DECISIONS.md"
open="$work/OPEN-ITEMS.md"
{
  printf '# Decisions index\n\n'
  printf '> GENERATED by `scripts/context/index.sh`. Do not edit: change the source file, then run `scripts/context/index.sh`.\n\n'
  printf 'One row per decision, from the "Decisions" tables in `docs/progress/` and the ADR headers in `docs/adr/`. The summary is cut short on purpose; the linked file is the record.\n\n'
  printf '| ID | Decision (short) | Decided by | Source |\n|---|---|---|---|\n'
} >"$dec"
{
  printf '# Open items index\n\n'
  printf '> GENERATED by `scripts/context/index.sh`. Do not edit: change the source file, then run `scripts/context/index.sh`.\n\n'
  printf 'Open issues from the phase files ("Open issues" sections, resolved items left out) and the carry-overs in `docs/PROGRESS.md`. The linked file is the record.\n\n'
  printf '| Source | Item (short) |\n|---|---|\n'
} >"$open"

while IFS= read -r b; do
  [[ -z "$b" ]] && continue
  f="$CTX_ROOT/docs/progress/$b"; label="${b%.md}"
  while IFS="$tab" read -r id summary by; do
    [[ -z "$id" ]] && continue
    if [[ "$id" == "!" ]]; then note "malformed docs/progress/$b: $summary. Fix the table, then run scripts/context/index.sh."; continue; fi
    printf '| %s | %s | %s | [%s](../../progress/%s) |\n' "$id" "$summary" "$by" "$label" "$b" >>"$dec"
  done <<<"$(decisions "$f")"
  while IFS= read -r item; do
    [[ -z "$item" ]] && continue
    printf '| [%s](../../progress/%s) | %s |\n' "$label" "$b" "$item" >>"$open"
  done <<<"$(open_issues "$f")"
done <<<"$(phase_files)"

printf '\n## ADRs\n\n| ID | Title | Decided by | Source |\n|---|---|---|---|\n' >>"$dec"
for f in "$CTX_ROOT"/docs/adr/[0-9][0-9][0-9][0-9]-*.md; do
  [[ -f "$f" ]] || continue
  b="$(basename "$f")"
  IFS="$tab" read -r id summary by <<<"$(adr_row "$f" "${b%%-*}")"
  if [[ "$id" == "!" ]]; then note "malformed docs/adr/$b: $summary. Fix the header, then run scripts/context/index.sh."; continue; fi
  printf '| %s | %s | %s | [%s](../../adr/%s) |\n' "$id" "$summary" "$by" "${b%.md}" "$b" >>"$dec"
done

printf '\n## Carry-overs (docs/PROGRESS.md, "Resume here")\n\n| Phase | Item (short) | Source |\n|---|---|---|\n' >>"$open"
while IFS="$tab" read -r label item; do
  [[ -z "$label" ]] && continue
  printf '| %s | %s | [PROGRESS.md](../../PROGRESS.md) |\n' "$label" "$item" >>"$open"
done <<<"$(carry_overs)"

# --- handovers (A11): names and headings ---
while IFS= read -r name; do
  [[ -z "$name" ]] && continue
  problem="$(ctx_handover_problem "$CTX_ROOT/docs/ai/context/handovers/$name")"
  [[ -n "$problem" ]] && note "malformed handover docs/ai/context/handovers/$(printf '%s' "$name" | ctx_clean): $problem. Fix: follow docs/ai/context/HANDOVER-TEMPLATE.md."
done <<<"$(ctx_handover_files)"

status=0
if [[ "$mode" == check ]]; then
  for g in DECISIONS.md OPEN-ITEMS.md; do
    if ! cmp -s "$work/$g" "$out_dir/$g"; then
      note "docs/ai/context/$g is stale or missing. Fix: run scripts/context/index.sh and commit the result."
    fi
  done
else
  for g in DECISIONS.md OPEN-ITEMS.md; do
    ctx_write_atomic "$out_dir/$g" <"$work/$g" || note "could not write docs/ai/context/$g"
  done
  printf 'index.sh: wrote docs/ai/context/DECISIONS.md (%s rows) and docs/ai/context/OPEN-ITEMS.md (%s rows)\n' \
    "$(grep -c '](\.\./\.\./' "$dec")" "$(grep -c '](\.\./\.\./' "$open")"
fi
if [[ -s "$problems" ]]; then
  sed 's/^/index.sh: /' "$problems" >&2
  status=1
fi
exit "$status"
