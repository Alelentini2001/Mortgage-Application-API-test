#!/usr/bin/env bash
# Harness H1 cases: context briefs, indexes, handovers, checkpoints and usage awareness.
# Sourced by test-hooks.sh (shares its pass/fail counters; runs from the repo root).
# Case IDs are from docs/progress/harness-01/test-proposal.md (approved set).
# Every case runs against a fixture repo in a temp dir: CLAUDE_PROJECT_DIR points at it and
# HARNESS_NOW fixes the clock. No network, no real account data, nothing outside the fixture is written.

repo="$PWD"
ctx="$repo/scripts/context"
statusline="$repo/.claude/statusline.sh"
NOW=1794744000 # 2026-11-15 12:00 UTC
tmp_root="$(mktemp -d)"
cleanup_context_tests() { [[ -n "$tmp_root" && -d "$tmp_root" ]] && rm -rf -- "$tmp_root"; }
trap cleanup_context_tests EXIT
agents="dotnet-reviewer spec-checker security-auditor test-designer parallel-implementer"
new_scripts="scripts/context/lib.sh scripts/context/brief.sh scripts/context/index.sh .claude/statusline.sh
.claude/hooks/session-context.sh .claude/hooks/subagent-context.sh .claude/hooks/pre-compact.sh .claude/hooks/usage-stop.sh"
hook_scripts=".claude/statusline.sh .claude/hooks/session-context.sh .claude/hooks/subagent-context.sh
.claude/hooks/pre-compact.sh .claude/hooks/usage-stop.sh"

# --- helpers ---
ok_if() { # ok_if <case-id> <what> <test command...>
  local id=$1 what=$2; shift 2
  if "$@"; then pass=$((pass + 1)); else fail=$((fail + 1)); echo "FAIL [$id] $what"; fi
}
has() { [[ "$1" == *"$2"* ]]; }
hasnt() { [[ "$1" != *"$2"* ]]; }
eq() { [[ "$1" == "$2" ]]; }
jq_ok() { jq -e "$2" >/dev/null 2>&1 <<<"$1"; }
lines_le() { [[ "$(printf '%s\n' "$1" | wc -l | tr -d ' ')" -le "$2" ]]; }

run() { # run <stdin> <command> [args...]: sets out, err, rc. Uses $FX and $NOW_AT (default $NOW).
  out="$(printf '%s' "$1" | CLAUDE_PROJECT_DIR="$FX" HARNESS_NOW="${NOW_AT:-$NOW}" "${@:2}" 2>"$tmp_root/stderr")"
  rc=$?
  err="$(cat "$tmp_root/stderr")"
}
run_nojq() { # same as run, with jq hidden from PATH
  out="$(printf '%s' "$1" | PATH="$nojq_bin" CLAUDE_PROJECT_DIR="$FX" HARNESS_NOW="${NOW_AT:-$NOW}" "${@:2}" 2>"$tmp_root/stderr")"
  rc=$?
  err="$(cat "$tmp_root/stderr")"
}
tree_sums() { # checksums of every file in the fixture except .git and .claude/state
  (cd "$FX" && find . -type f ! -path './.git/*' ! -path './.claude/state/*' | LC_ALL=C sort | while IFS= read -r f; do cksum "$f"; done)
}
state_files() { (cd "$FX/.claude/state" 2>/dev/null && ls -A | LC_ALL=C sort | tr '\n' ' '); }
brief() { run "" "$ctx/brief.sh" "$@"; }

nojq_bin="$tmp_root/nojq"
mkdir -p "$nojq_bin"
for b in bash sh cat head tail mkdir mktemp mv rm cp tr date git grep sed awk dirname basename wc sort cut ls env printf; do
  p="$(command -v "$b" 2>/dev/null)" && [[ "$p" == /* ]] && ln -s "$p" "$nojq_bin/$b"
done

fx_new() { # fx_new: build a fixture repo and set FX to it
  FX="$(mktemp -d "$tmp_root/fx.XXXXXX")"
  mkdir -p "$FX/docs/progress" "$FX/docs/adr" "$FX/docs/ai/context/handovers" "$FX/.claude/agents" "$FX/.claude/state"
  cp "$repo"/.claude/agents/*.md "$FX/.claude/agents/"
  for f in CONTEXT-INDEX.md AGENT-REGISTRY.md HANDOVER-TEMPLATE.md; do cp "$repo/docs/ai/context/$f" "$FX/docs/ai/context/" 2>/dev/null; done
  cp "$repo/docs/ai/context/handovers/README.md" "$FX/docs/ai/context/handovers/" 2>/dev/null
  cp "$repo/docs/progress/_TEMPLATE.md" "$FX/docs/progress/"
  cp "$repo/docs/adr/_TEMPLATE.md" "$FX/docs/adr/"
  printf '.claude/state/\n' >"$FX/.gitignore"
  cat >"$FX/docs/PROGRESS.md" <<'EOF'
# Progress

## Resume here
- **Phase:** fixture phase H1. RESUME-MARKER-1.
- **Carry into later phases:**
  - Phase 7: carry item seven CARRY-7.
  - Phase 6: carry item six CARRY-6.
- **Open decisions / blockers for Alessandro:**
  - fixture decision OPEN-DEC-1.
- **Last verify:** `./scripts/verify.sh` full → PASSED (LAST-VERIFY-1).

## Phases
PHASES-MARKER must never be injected.
EOF
  cat >"$FX/docs/progress/phase-06-background.md" <<'EOF'
# Phase 06: background (fixture)

## Plan (approved at Checkpoint A)
PHASE-BODY-MARKER plan text.

## Decisions (option chosen · alternatives · reason · decided by) → ADR links

| ID | Chosen | Alternatives | Reason | Decided by |
|---|---|---|---|---|
| P6-D1 | A: queue port in Core | B everything in Api | reason one | Alessandro |
| P6-D2 | A: bounded channel with a \| pipe | B TryWrite | reason two | Alessandro |
| P6-D3 | A: shared-cache SQLite | B file per factory | reason three | Alessandro |
| P6-D4 | A: always 200 | B 202 while Pending | reason four | Alessandro |

## Open issues
- **R7:** fixture open item OPEN-6A.
- ~~resolved item~~ RESOLVED-MARKER.
- second open item OPEN-6B.

## Human review
EOF
  cat >"$FX/docs/progress/phase-02-core.md" <<'EOF'
# Phase 02: core (fixture)

## Decisions (option chosen · alternatives · reason · decided by)

| # | Chosen | Decided by |
|---|---|---|
| P2-D1 | (a) sealed calculator | Alessandro (agent's recommendation) |
| P2-D2 | (a) immutable result | Alessandro (agent's recommendation) |

## Open issues
- phase two open item OPEN-2A.
EOF
  cat >"$FX/docs/progress/phase-10-docs.md" <<'EOF'
# Phase 10: docs (fixture)

## Decisions (option chosen · alternatives · reason · decided by)

| ID | Chosen | Decided by |
|---|---|---|
| P10-D1 | A: one README | Alessandro |
EOF
  cat >"$FX/docs/adr/0003-background-assessment-queue.md" <<'EOF'
# 0003. Background assessment queue

- **Status:** Accepted (fixture)
- **Date:** 2026-11-15
- **Decided by:** Alessandro (on the agent's recommendation)

## Context
fixture
EOF
  cat >"$FX/docs/ai/context/CURRENT-WORK.md" <<'EOF'
# Current work

- **Phase:** H1 fixture phase
- **Step:** step five
- **Files in play:** `scripts/context/`
- **Next action:** run the fixture NEXT-ACTION-1.
EOF
  git -C "$FX" init -q -b h-test
  git -C "$FX" add -A
  git -C "$FX" -c user.name=fixture -c user.email=fixture@example.invalid commit -q -m fixture
  CLAUDE_PROJECT_DIR="$FX" "$ctx/index.sh" >/dev/null 2>&1
  git -C "$FX" add -A
  git -C "$FX" -c user.name=fixture -c user.email=fixture@example.invalid commit -q -m index
}
sl() { # sl <context %> <rate_limits body>: a status-line input with fake values
  printf '{"session_id":"sess-FAKE-123","transcript_path":"/Users/someone/t.jsonl","cwd":"/Users/someone/repo","workspace":{"current_dir":"/Users/someone/repo"},"cost":{"total_cost_usd":1.23},"email":"someone@example.com","model":{"id":"m1","display_name":"Model One"},"context_window":{"context_window_size":200000,"used_percentage":%s,"remaining_percentage":60,"current_usage":null},"rate_limits":{%s}}' "$1" "$2"
}
win() { printf '"%s":{"used_percentage":%s,"resets_at":%s}' "$1" "$2" "$3"; }
usage_json() { cat "$FX/.claude/state/usage.json" 2>/dev/null; }
put_usage() { # put_usage <updated_at> <state> <5h%> <5h reset>: write a snapshot fixture directly
  printf '{"updated_at":%s,"model_id":"m1","model_name":"Model One","context_used_pct":40,"five_hour":{"used_pct":%s,"resets_at":%s},"seven_day":{"used_pct":10,"resets_at":%s},"state":"%s"}' \
    "$1" "$3" "$4" "$((NOW + 86400))" "$2" >"$FX/.claude/state/usage.json"
}

# =====================================================================
# A. SessionStart (session-context.sh)
# =====================================================================
fx_new
resume_section="$(awk '/^## Resume here/{on=1; print; next} on && /^## /{exit} on{print}' "$FX/docs/PROGRESS.md")"

# S1
run '{"hook_event_name":"SessionStart","source":"startup"}' "$repo/.claude/hooks/session-context.sh"
s1_out="$out"
ok_if S1 "startup exits 0" eq "$rc" 0
ok_if S1 "startup prints Resume here verbatim" has "$out" "$resume_section"
ok_if S1 "startup prints the branch" has "$out" "Current branch: h-test"
ok_if S1 "startup points at CURRENT-WORK" has "$out" "docs/ai/context/CURRENT-WORK.md"
ok_if S1 "startup prints a usage line" has "$out" "usage: "
ok_if S1 "startup does not dump other PROGRESS sections" hasnt "$out" "PHASES-MARKER"

# S2
run '{"hook_event_name":"SessionStart","source":"resume"}' "$repo/.claude/hooks/session-context.sh"
ok_if S2 "resume gives the same content as startup" eq "$(tail -n +2 <<<"$out")" "$(tail -n +2 <<<"$s1_out")"

# S3
run '{"hook_event_name":"SessionStart","source":"clear"}' "$repo/.claude/hooks/session-context.sh"
ok_if S3 "clear gives the same content as startup" eq "$(tail -n +2 <<<"$out")" "$(tail -n +2 <<<"$s1_out")"

# S4
run '{"hook_event_name":"SessionStart","source":"compact"}' "$repo/.claude/hooks/session-context.sh"
ok_if S4 "compact keeps the Resume here section" has "$out" "$resume_section"
ok_if S4 "compact keeps the branch line" has "$out" "Current branch: h-test"
ok_if S4 "compact keeps the old intro line" has "$out" "Context re-injected after compaction"

# S5
for src in '{"source":"fork"}' '{}' '{"source":""}' '{"source":"weird"}' '{"source":42}'; do
  run "$src" "$repo/.claude/hooks/session-context.sh"
  ok_if S5 "source $src exits 0" eq "$rc" 0
  ok_if S5 "source $src gives the default content" eq "$(tail -n +2 <<<"$out")" "$(tail -n +2 <<<"$s1_out")"
done
run '{"source":"weird"}' "$repo/.claude/hooks/session-context.sh"
ok_if S5 "an unknown source value is not echoed" hasnt "$out" "weird"

# S6
mv "$FX/docs/PROGRESS.md" "$tmp_root/PROGRESS.bak"
for src in startup resume clear compact; do
  run "{\"source\":\"$src\"}" "$repo/.claude/hooks/session-context.sh"
  ok_if S6 "missing PROGRESS.md ($src) exits 0" eq "$rc" 0
  ok_if S6 "missing PROGRESS.md ($src) is reported" has "$out" "docs/PROGRESS.md not found"
  ok_if S6 "missing PROGRESS.md ($src) still prints the branch" has "$out" "Current branch: h-test"
  ok_if S6 "missing PROGRESS.md ($src) still prints usage" has "$out" "usage: "
  ok_if S6 "missing PROGRESS.md ($src) has no shell error" hasnt "$err" "unbound variable"
done
mv "$tmp_root/PROGRESS.bak" "$FX/docs/PROGRESS.md"

# S7
cp "$FX/docs/PROGRESS.md" "$tmp_root/PROGRESS.bak"
printf '# Progress\n\n## Phases\nPHASES-MARKER\n' >"$FX/docs/PROGRESS.md"
run '{"source":"startup"}' "$repo/.claude/hooks/session-context.sh"
ok_if S7 "no Resume here heading exits 0" eq "$rc" 0
ok_if S7 "no Resume here heading is reported" has "$out" "no Resume here section in docs/PROGRESS.md"
ok_if S7 "nothing is guessed from other sections" hasnt "$out" "PHASES-MARKER"
cp "$tmp_root/PROGRESS.bak" "$FX/docs/PROGRESS.md"

# S11 (not in the approved set; covers the A2 size cap from build item 4)
{ printf '# Progress\n\n## Resume here\n'; for i in $(seq 1 500); do printf -- '- long resume line %s\n' "$i"; done; printf '\n## Phases\n'; } >"$FX/docs/PROGRESS.md"
run '{"source":"startup"}' "$repo/.claude/hooks/session-context.sh"
ok_if S11 "a 500-line Resume here is truncated" has "$out" "truncated, see docs/PROGRESS.md"
ok_if S11 "injected context stays under 100 lines" lines_le "$out" 100
cp "$tmp_root/PROGRESS.bak" "$FX/docs/PROGRESS.md"

# S12
big="{\"source\":\"startup\",\"x\":\"$(head -c 1048576 /dev/zero | tr '\0' 'a')\"}"
for input in '' 'not json' '{' "$big"; do
  run "$input" "$repo/.claude/hooks/session-context.sh"
  ok_if S12 "malformed stdin (${#input} bytes) exits 0" eq "$rc" 0
  ok_if S12 "malformed stdin (${#input} bytes) gives the default content" has "$out" "$resume_section"
  run_nojq "$input" "$repo/.claude/hooks/session-context.sh"
  ok_if S12 "[nojq] malformed stdin (${#input} bytes) exits 0" eq "$rc" 0
  ok_if S12 "[nojq] malformed stdin (${#input} bytes) gives the default content" has "$out" "$resume_section"
done
run_nojq '{"source":"compact"}' "$repo/.claude/hooks/session-context.sh"
ok_if S12 "[nojq] compact still prints Resume here" has "$out" "$resume_section"

# S13
cat >"$FX/docs/PROGRESS.md" <<'EOF'
# Progress

## Resume here
- injection $(touch PWNED) and `touch PWNED2` and $HOME stay literal.

## Phases
EOF
run '{"source":"startup"}' "$repo/.claude/hooks/session-context.sh"
ok_if S13 "file content is printed literally" has "$out" '$(touch PWNED) and `touch PWNED2` and $HOME'
ok_if S13 "file content is never executed" eval '[[ ! -e "$FX/PWNED" && ! -e "$FX/PWNED2" && ! -e PWNED && ! -e PWNED2 ]]'
cp "$tmp_root/PROGRESS.bak" "$FX/docs/PROGRESS.md"

# S14
before_sums="$(tree_sums)"; before_status="$(git -C "$FX" status --porcelain)"; before_state="$(state_files)"
run '{"source":"startup"}' "$repo/.claude/hooks/session-context.sh"; first="$out"
run '{"source":"startup"}' "$repo/.claude/hooks/session-context.sh"
ok_if S14 "two runs give identical output" eq "$out" "$first"
ok_if S14 "no tracked or untracked file changed" eq "$(git -C "$FX" status --porcelain)" "$before_status"
ok_if S14 "no file content changed" eq "$(tree_sums)" "$before_sums"
ok_if S14 "no state file was created" eq "$(state_files)" "$before_state"

# S16
out="$(CLAUDE_PROJECT_DIR="$FX" HARNESS_NOW="$NOW" "$repo/.claude/hooks/reinject-context.sh" </dev/null)"
ok_if S16 "the reinject-context.sh shim still prints Resume here" has "$out" "## Resume here"
ok_if S16 "the shim still prints the branch" has "$out" "Current branch: h-test"
ok_if S16 "the existing reinject-context case is unchanged in test-hooks.sh" grep -qF 'reinject_out="$(CLAUDE_PROJECT_DIR="$PWD" "$hooks/reinject-context.sh")"' "$repo/.claude/hooks/tests/test-hooks.sh"

# =====================================================================
# B. SubagentStart (subagent-context.sh) and brief.sh
# =====================================================================
fx_new
reserved="Decisions reserved for Alessandro are never made by an agent"
kw_dotnet_reviewer="diff review"; kw_spec_checker="requirement traceability"; kw_security_auditor="security gate"
kw_test_designer="never writes code"; kw_parallel_implementer="implements one approved plan"
keyword() { local v="kw_${1//-/_}"; printf '%s' "${!v}"; }

# B1
for a in $agents; do
  run "{\"hook_event_name\":\"SubagentStart\",\"agent_id\":\"a1\",\"agent_type\":\"$a\"}" "$repo/.claude/hooks/subagent-context.sh"
  ok_if B1 "$a: exits 0" eq "$rc" 0
  ok_if B1 "$a: output is SubagentStart JSON" jq_ok "$out" '.hookSpecificOutput.hookEventName == "SubagentStart"'
  ctx_text="$(jq -r '.hookSpecificOutput.additionalContext // empty' <<<"$out" 2>/dev/null)"
  ok_if B1 "$a: additionalContext is not empty" test -n "$ctx_text"
  ok_if B1 "$a: names the agent" has "$ctx_text" "Agent: $a"
  ok_if B1 "$a: states its responsibility" has "$ctx_text" "$(keyword "$a")"
  if [[ "$a" == parallel-implementer ]]; then
    ok_if B1 "$a: boundaries allow Write and Edit" has "$ctx_text" "Write and Edit"
  else
    ok_if B1 "$a: boundaries say read-only" has "$ctx_text" "read-only"
  fi
  ok_if B1 "$a: has the reserved-decisions sentence" has "$ctx_text" "$reserved"
done

# B2
for input in '{"agent_type":"general-purpose"}' '{"agent_type":""}' '{"agent_id":"a1"}' '{"agent_type":"../../etc/passwd"}'; do
  run "$input" "$repo/.claude/hooks/subagent-context.sh"
  ctx_text="$(jq -r '.hookSpecificOutput.additionalContext // empty' <<<"$out" 2>/dev/null)"
  ok_if B2 "$input: exits 0" eq "$rc" 0
  ok_if B2 "$input: generic brief" has "$ctx_text" "unknown agent type"
  ok_if B2 "$input: keeps the reserved-decisions rule" has "$ctx_text" "$reserved"
  ok_if B2 "$input: keeps the checkpoints" has "$ctx_text" "Checkpoint A (plan)"
  ok_if B2 "$input: the raw value is not used" hasnt "$ctx_text" "passwd"
done

# B3
for input in '{"agent_type":"test-designer\nX"}' '{"agent_type":"a\"b"}' '{"agent_type":"$(touch PWNED3)"}' 'not json'; do
  run "$input" "$repo/.claude/hooks/subagent-context.sh"
  ok_if B3 "$input: exits 0" eq "$rc" 0
  ok_if B3 "$input: output is valid JSON" jq_ok "$out" '.hookSpecificOutput.hookEventName == "SubagentStart"'
done
ok_if B3 "no command ran from agent_type" eval '[[ ! -e "$FX/PWNED3" && ! -e PWNED3 ]]'

# B4
for a in $agents --generic; do
  brief "$a"
  ok_if B4 "$a: Checkpoint A (plan)" has "$out" "Checkpoint A (plan)"
  ok_if B4 "$a: Checkpoint B (PR)" has "$out" "Checkpoint B (PR)"
  ok_if B4 "$a: waits for approval" has "$out" "wait for Alessandro's approval"
  ok_if B4 "$a: author/reviewer independence" has "$out" "the author never grades its own work"
  ok_if B4 "$a: reserved decisions" has "$out" "$reserved"
done
ok_if B4 "the shared rules are one constant in brief.sh" eval '[[ "$(grep -c "the author never grades its own work" "$ctx/brief.sh")" == 1 ]]'

# B5
for i in 1 2 3 4 5 6 7; do cp "$repo/docs/ai/context/HANDOVER-TEMPLATE.md" "$FX/docs/ai/context/handovers/H1-00$i-test-designer.md"; done
brief test-designer 06
ok_if B5 "brief exits 0" eq "$rc" 0
ok_if B5 "brief names the phase file" has "$out" "docs/progress/phase-06-background.md"
ok_if B5 "brief names the branch" has "$out" "Branch: h-test"
ok_if B5 "brief points at CURRENT-WORK" has "$out" "docs/ai/context/CURRENT-WORK.md"
ok_if B5 "brief shows the next action" has "$out" "NEXT-ACTION-1"
ok_if B5 "brief lists relevant open items" has "$out" "OPEN-6A"
ok_if B5 "brief leaves out other phases' open items" hasnt "$out" "OPEN-2A"
ok_if B5 "brief has the usage status" has "$out" "usage: "
ok_if B5 "brief points at the context index" has "$out" "docs/ai/context/CONTEXT-INDEX.md"
ok_if B5 "brief shows at most 5 handovers" eval '[[ "$(grep -c "docs/ai/context/handovers/H" <<<"$out")" -le 5 ]]'
ok_if B5 "brief does not dump the phase file" hasnt "$out" "PHASE-BODY-MARKER"
ok_if B5 "brief is under 100 lines" lines_le "$out" 100
rm -f "$FX"/docs/ai/context/handovers/H1-00*.md

# B8
brief test-designer 06
ok_if B8 "phase 06 selects phase-06-*.md" has "$out" "docs/progress/phase-06-background.md"
brief test-designer 99
ok_if B8 "phase 99 is reported missing" has "$out" "phase 99 not found"
for bad in '../x' '6;id' '' 'abc' '0001234'; do
  brief test-designer "$bad"
  ok_if B8 "phase '$bad' is rejected" test "$rc" -ne 0
done
# B8 / A4 (build item 3): unknown agent on the CLI exits non-zero with usage
brief nonsense
ok_if B8 "unknown agent exits non-zero" test "$rc" -ne 0
ok_if B8 "unknown agent prints usage" has "$err" "usage:"
brief
ok_if B8 "no agent exits non-zero" test "$rc" -ne 0

# B9
reg_names="$(grep -E '^## [a-z0-9-]+$' "$repo/docs/ai/context/AGENT-REGISTRY.md" 2>/dev/null | cut -c4- | LC_ALL=C sort | tr '\n' ' ')"
file_names="$(cd "$repo/.claude/agents" && ls -- *.md | sed 's/\.md$//' | LC_ALL=C sort | tr '\n' ' ')"
ok_if B9 "registry agents match .claude/agents" eq "$reg_names" "$file_names"
for a in $agents; do
  fm_model="$(sed -n 's/^model: *//p' "$repo/.claude/agents/$a.md")"
  reg_model="$(awk -v n="## $a" '$0 == n {on=1; next} on && /^## /{exit} on && /^- \*\*Model:\*\*/ {sub(/^- \*\*Model:\*\* */, ""); sub(/[.].*$/, ""); print; exit}' "$repo/docs/ai/context/AGENT-REGISTRY.md")"
  ok_if B9 "$a: registry model matches frontmatter ($fm_model)" eq "$reg_model" "$fm_model"
  brief "$a"
  ok_if B9 "$a: brief.sh knows the agent" eq "$rc" 0
done

# =====================================================================
# C. PreCompact (pre-compact.sh)
# =====================================================================
fx_new
printf 'secret sk-FAKE-SECRET-123\n' >"$FX/docs/leak.txt"
printf '\nchanged\n' >>"$FX/docs/progress/phase-02-core.md"
pre_sums="$(tree_sums)"

# C1
run '{"hook_event_name":"PreCompact","trigger":"auto","custom_instructions":""}' "$repo/.claude/hooks/pre-compact.sh"
cp_text="$(cat "$FX/.claude/state/checkpoint.md" 2>/dev/null)"
ok_if C1 "exits 0" eq "$rc" 0
ok_if C1 "checkpoint has the branch" has "$cp_text" "h-test"
ok_if C1 "checkpoint lists a modified file" has "$cp_text" "docs/progress/phase-02-core.md"
ok_if C1 "checkpoint lists an untracked file" has "$cp_text" "docs/leak.txt"
ok_if C1 "checkpoint has the last verify line" has "$cp_text" "LAST-VERIFY-1"
ok_if C1 "checkpoint has CURRENT-WORK" has "$cp_text" "NEXT-ACTION-1"
ok_if C1 "checkpoint time comes from HARNESS_NOW" has "$cp_text" "2026-11-15 12:00 UTC"
ok_if C1 "keep-instructions are additions" has "$out" "Also keep"
ok_if C1 "keep: phase and step" has "$out" "phase and step"
ok_if C1 "keep: modified files" has "$out" "modified files"
ok_if C1 "keep: test commands and last result" has "$out" "test commands and their last result"
ok_if C1 "keep: open decisions" has "$out" "open decisions awaiting Alessandro"

# C4
run '{"trigger":"auto"}' "$repo/.claude/hooks/pre-compact.sh"
ok_if C4 "second run gives the same checkpoint" eq "$(cat "$FX/.claude/state/checkpoint.md")" "$cp_text"
ok_if C4 "only checkpoint.md is left, no temp files" eq "$(state_files)" "checkpoint.md "

# C5
ok_if C5 "no authoritative file changed" eq "$(tree_sums)" "$pre_sums"

# C6
ok_if C6 "the secret is not in stdout" hasnt "$out" "sk-FAKE-SECRET-123"
ok_if C6 "the secret is not in the state dir" eval '! grep -rq "sk-FAKE-SECRET-123" "$FX/.claude/state"'

# C8
run '{"source":"compact"}' "$repo/.claude/hooks/session-context.sh"
ok_if C8 "SessionStart compact shows the checkpoint" has "$out" "NEXT-ACTION-1"
ok_if C8 "SessionStart compact names the checkpoint file" has "$out" ".claude/state/checkpoint.md"

# =====================================================================
# D. Status line and usage states (statusline.sh)
# =====================================================================
fx_new

# U1
run "$(sl 40 "$(win five_hour 20 $((NOW + 3600))),$(win seven_day 10 $((NOW + 86400)))")" "$statusline"
ok_if U1 "exits 0" eq "$rc" 0
ok_if U1 "one line" eq "$(printf '%s\n' "$out" | wc -l | tr -d ' ')" 1
ok_if U1 "model name" has "$out" "Model One"
ok_if U1 "context %" has "$out" "ctx 40%"
ok_if U1 "5h %" has "$out" "5h 20%"
ok_if U1 "7d %" has "$out" "7d 10%"
ok_if U1 "earliest reset" has "$out" "reset 2026-11-15 13:00 UTC"
ok_if U1 "state normal" has "$out" "usage: normal"
ok_if U1 "snapshot values" jq_ok "$(usage_json)" ".updated_at == $NOW and .model_id == \"m1\" and .model_name == \"Model One\" and .context_used_pct == 40 and .five_hour.used_pct == 20 and .five_hour.resets_at == $((NOW + 3600)) and .seven_day.used_pct == 10 and .state == \"normal\""

# U2
for w in five_hour seven_day; do
  other=seven_day; [[ "$w" == seven_day ]] && other=five_hour
  for pair in 74:normal 74.9:normal 75:approaching 99.9:approaching 100:exhausted; do
    v="${pair%%:*}"; want="${pair#*:}"
    run "$(sl 40 "$(win "$w" "$v" $((NOW + 3600))),$(win "$other" 1 $((NOW + 7200)))")" "$statusline"
    ok_if U2 "$w $v% is $want" has "$out" "usage: $want"
    ok_if U2 "$w $v% stored as $want" jq_ok "$(usage_json)" ".state == \"$want\""
  done
done

# U5
run '{"model":{"id":"m1","display_name":"Model One"},"context_window":{"used_percentage":40}}' "$statusline"
ok_if U5 "no rate_limits: usage unknown" has "$out" "usage: unknown"
ok_if U5 "no rate_limits: no usage number" eval '[[ "$out" != *"5h "[0-9]* && "$out" != *"7d "[0-9]* ]]'
ok_if U5 "no rate_limits: context still shown" has "$out" "ctx 40%"
ok_if U5 "no rate_limits: stored unknown" jq_ok "$(usage_json)" '.state == "unknown" and .five_hour.used_pct == null and .seven_day.used_pct == null'

# U6
run "$(sl 40 "$(win five_hour 20 $((NOW + 3600)))")" "$statusline"
ok_if U6 "only 5h: shows 5h" has "$out" "5h 20%"
ok_if U6 "only 5h: 7d is ? not 0" has "$out" "7d ?"
ok_if U6 "only 5h: state from what exists, marked partial" has "$out" "usage: normal (partial)"
ok_if U6 "only 5h: 7d stored as null" jq_ok "$(usage_json)" '.seven_day.used_pct == null and .five_hour.used_pct == 20'
run "$(sl 40 "$(win seven_day 80 $((NOW + 3600)))")" "$statusline"
ok_if U6 "only 7d: 5h is ?" has "$out" "5h ?"
ok_if U6 "only 7d: approaching, partial" has "$out" "usage: approaching (partial)"

# U7
for v in null '"abc"' -5; do
  run "$(sl 40 "$(win five_hour "$v" $((NOW + 3600))),$(win seven_day 10 $((NOW + 86400)))")" "$statusline"
  ok_if U7 "5h used_percentage $v: exits 0" eq "$rc" 0
  ok_if U7 "5h used_percentage $v: shown as ?" has "$out" "5h ?"
  ok_if U7 "5h used_percentage $v: stored null" jq_ok "$(usage_json)" '.five_hour.used_pct == null'
  ok_if U7 "5h used_percentage $v: no arithmetic error" hasnt "$err" "expression"
done
run "$(sl 40 "$(win five_hour 150 $((NOW + 3600))),$(win seven_day 10 $((NOW + 86400)))")" "$statusline"
ok_if U7 "150% is exhausted" has "$out" "usage: exhausted"
run "$(sl 40 "$(win five_hour 20 null),$(win seven_day 10 '"abc"')")" "$statusline"
ok_if U7 "null or non-numeric resets_at: exits 0" eq "$rc" 0
ok_if U7 "null or non-numeric resets_at: stored null" jq_ok "$(usage_json)" '.five_hour.resets_at == null and .seven_day.resets_at == null'
ok_if U7 "null or non-numeric resets_at: reset shown as ?" has "$out" "reset ?"

# U8
run '{"model":{"id":"m1","display_name":"Model One"},"context_window":{"context_window_size":200000,"used_percentage":null,"current_usage":null},"rate_limits":{"five_hour":{"used_percentage":20,"resets_at":'"$((NOW + 3600))"'}}}' "$statusline"
ok_if U8 "null context shows ctx ?" has "$out" "ctx ?"
ok_if U8 "null context stored as null, not 0" jq_ok "$(usage_json)" '.context_used_pct == null'
ok_if U8 "usage still works" has "$out" "5h 20%"

# U10
run "$(sl 40 "$(win five_hour 100 "$NOW")")" "$statusline"
ok_if U10 "reset equal to now: reset passed" has "$out" "reset passed"
ok_if U10 "reset equal to now: not exhausted" hasnt "$out" "exhausted"
run "$(sl 40 "$(win five_hour 100 $((NOW - 1)))")" "$statusline"
ok_if U10 "reset in the past: reset passed" has "$out" "reset passed"
ok_if U10 "reset in the past: stale, not exhausted" has "$out" "usage: unknown"
run "$(sl 40 "$(win five_hour 20 99999999999)")" "$statusline"
ok_if U10 "huge reset: exits 0" eq "$rc" 0
ok_if U10 "huge reset: formatted" has "$out" "reset 5138-11-16 09:46 UTC"
fake_date="$tmp_root/fake-date"; mkdir -p "$fake_date"
printf '#!/bin/sh\n[ "$*" = "+%%s" ] && exec %s +%%s\nexit 1\n' "$(command -v date)" >"$fake_date/date"; chmod +x "$fake_date/date"
out="$(printf '%s' "$(sl 40 "$(win five_hour 20 $((NOW + 3600)))")" | PATH="$fake_date:$PATH" CLAUDE_PROJECT_DIR="$FX" HARNESS_NOW="$NOW" "$statusline" 2>/dev/null)"
ok_if U10 "reset time needs neither BSD date -r nor GNU date -d" has "$out" "reset 2026-11-15 13:00 UTC"
out="$(printf '%s' "$(sl 40 "$(win five_hour 20 $((NOW + 3600)))")" | PATH="$fake_date:$PATH" CLAUDE_PROJECT_DIR="$FX" "$statusline" 2>/dev/null)"
ok_if U10 "without HARNESS_NOW the clock comes from date +%s" jq_ok "$(usage_json)" '.updated_at > 1700000000'

# U11
for pair in 899:fresh 900:fresh 901:stale; do
  age="${pair%%:*}"; want="${pair#*:}"
  put_usage $((NOW - age)) approaching 80 $((NOW + 3600))
  brief test-designer
  if [[ "$want" == fresh ]]; then
    ok_if U11 "snapshot $age s old is used" has "$out" "usage: approaching"
  else
    ok_if U11 "snapshot $age s old is stale" has "$out" "usage: unknown (no status-line data since 2026-11-15 11:44 UTC)"
  fi
done

# U12
rm -f "$FX/.claude/state/usage.json"
brief test-designer
ok_if U12 "no snapshot: unknown" has "$out" "usage: unknown (no status-line data)"

# U13
for junk in '' '{"state":' "$(printf '\001\002\377')" '[1,2]' '{"updated_at":"x","state":"normal"}'; do
  printf '%s' "$junk" >"$FX/.claude/state/usage.json"
  brief test-designer
  ok_if U13 "corrupt snapshot (${#junk} bytes): brief says unknown" has "$out" "usage: unknown"
  ok_if U13 "corrupt snapshot (${#junk} bytes): brief exits 0" eq "$rc" 0
  run '{"source":"startup"}' "$repo/.claude/hooks/session-context.sh"
  ok_if U13 "corrupt snapshot (${#junk} bytes): session hook says unknown" has "$out" "usage: unknown"
done
run "$(sl 40 "$(win five_hour 20 $((NOW + 3600)))")" "$statusline"
ok_if U13 "next status line run replaces the corrupt file" jq_ok "$(usage_json)" '.five_hour.used_pct == 20'

# U15
run "$(sl 40 "$(win five_hour 1 $((NOW + 3600)))")" "$statusline"
( for i in $(seq 1 30); do sl 40 "$(win five_hour "$i" $((NOW + 3600)))" | CLAUDE_PROJECT_DIR="$FX" HARNESS_NOW="$NOW" "$statusline" >/dev/null 2>&1; done ) &
writer=$!
reader_bad=0
for i in $(seq 1 60); do jq -e . "$FX/.claude/state/usage.json" >/dev/null 2>&1 || reader_bad=$((reader_bad + 1)); done
wait "$writer"
ok_if U15 "a reader never sees a partial snapshot" eq "$reader_bad" 0
ok_if U15 "no temp files are left" eq "$(state_files)" "usage.json "

# U16
pids=""
for i in $(seq 1 20); do
  (sl 40 "$(win five_hour "$i" $((NOW + 3600))),$(win seven_day $((i + 50)) $((NOW + 86400)))" | CLAUDE_PROJECT_DIR="$FX" HARNESS_NOW="$NOW" "$statusline" >/dev/null 2>&1) &
  pids="$pids $!"
done
bad_exit=0; for p in $pids; do wait "$p" || bad_exit=$((bad_exit + 1)); done
ok_if U16 "20 parallel runs all exit 0" eq "$bad_exit" 0
ok_if U16 "final snapshot is one whole input, not a mix" jq_ok "$(usage_json)" '.seven_day.used_pct == .five_hour.used_pct + 50'
ok_if U16 "no temp files are left" eq "$(state_files)" "usage.json "

# U19
nested="$(printf '%.0s[' $(seq 1 20000))"
for input in '' 'not json' "$big" "$nested" '{"model":{"id":"m1","display_name":"$(touch PWNED4)\u001b[31mRed\u0007"}}'; do
  run "$input" "$statusline"
  ok_if U19 "hostile input (${#input} bytes): exits 0" eq "$rc" 0
  ok_if U19 "hostile input (${#input} bytes): one line" eq "$(printf '%s\n' "$out" | wc -l | tr -d ' ')" 1
  ok_if U19 "hostile input (${#input} bytes): usage unknown" has "$out" "usage: unknown"
  ok_if U19 "hostile input (${#input} bytes): no escape characters" eval '[[ "$out" != *$'"'"'\033'"'"'* && "$out" != *$'"'"'\a'"'"'* ]]'
done
ok_if U19 "no command ran from the model name" eval '[[ ! -e "$FX/PWNED4" && ! -e PWNED4 ]]'

# U20
run_nojq "$(sl 40 "$(win five_hour 20 $((NOW + 3600)))")" "$statusline"
ok_if U20 "[nojq] exits 0" eq "$rc" 0
ok_if U20 "[nojq] prints usage unknown" has "$out" "usage: unknown"
ok_if U20 "[nojq] never prints a guessed number" hasnt "$out" "20%"
ok_if U20 "[nojq] stores state unknown" jq_ok "$(usage_json)" '.state == "unknown" and .five_hour.used_pct == null'
ok_if U20 "[nojq] no temp files are left" eq "$(state_files)" "usage.json "

# U21
run "$(sl 40 "$(win five_hour 20 $((NOW + 3600))),$(win seven_day 10 $((NOW + 86400)))")" "$statusline"
ok_if U21 "snapshot has only the allowlisted keys" jq_ok "$(usage_json)" 'keys == ["context_used_pct","five_hour","model_id","model_name","seven_day","state","updated_at"] and (.five_hour | keys) == ["resets_at","used_pct"] and (.seven_day | keys) == ["resets_at","used_pct"]'
for s in sess-FAKE-123 /Users/someone someone@example.com total_cost_usd current_dir; do
  ok_if U21 "snapshot does not contain $s" eval '! grep -qF -- "$s" "$FX/.claude/state/usage.json"'
done

# U22
ok_if U22 ".gitignore lists .claude/state/" grep -qxF '.claude/state/' "$repo/.gitignore"
ok_if U22 "usage.json is ignored by git" git -C "$repo" check-ignore -q .claude/state/usage.json

# =====================================================================
# E. StopFailure (usage-stop.sh)
# =====================================================================
fx_new
stop_input='{"hook_event_name":"StopFailure","error":"rate_limit","error_details":"account someone@example.com on Max plan, request req_FAKE123","last_assistant_message":"here is sk-FAKE-SECRET-123 from /Users/someone/x"}'

# E1
put_usage $((NOW - 60)) exhausted 100 $((NOW + 1800))
run "$stop_input" "$repo/.claude/hooks/usage-stop.sh"
blk="$(cat "$FX/.claude/state/blocker.md" 2>/dev/null)"
ok_if E1 "exits 0" eq "$rc" 0
ok_if E1 "blocker records rate_limit" has "$blk" "error: rate_limit"
ok_if E1 "blocker has the reset time" has "$blk" "2026-11-15 12:30 UTC"
ok_if E1 "blocker has the resume sentence" has "$blk" "Start a new session after 2026-11-15 12:30 UTC; Resume here, CURRENT-WORK and the checkpoint load automatically. Next action: run the fixture NEXT-ACTION-1."
brief test-designer
ok_if E1 "usage state becomes exhausted" has "$out" "usage: exhausted"
run '{"source":"startup"}' "$repo/.claude/hooks/session-context.sh"
ok_if E1 "SessionStart shows the blocker" has "$out" "Start a new session after 2026-11-15 12:30 UTC"
# E7 (not in the approved set; covers build item 7 "repeated calls replace, not append")
run "$stop_input" "$repo/.claude/hooks/usage-stop.sh"
ok_if E7 "a repeated call replaces the blocker" eq "$(grep -c '^- error:' "$FX/.claude/state/blocker.md")" 1
ok_if E7 "no temp files are left" eq "$(state_files)" "blocker.md usage.json "

# E2
rm -f "$FX/.claude/state/usage.json" "$FX/.claude/state/blocker.md"
run "$stop_input" "$repo/.claude/hooks/usage-stop.sh"
blk="$(cat "$FX/.claude/state/blocker.md" 2>/dev/null)"
ok_if E2 "no snapshot: reset time unknown" has "$blk" "reset time unknown"
ok_if E2 "no snapshot: the resume sentence is complete" has "$blk" "Start a new session after the reset; Resume here, CURRENT-WORK and the checkpoint load automatically. Next action: run the fixture NEXT-ACTION-1."
brief test-designer
ok_if E2 "no snapshot: still exhausted" has "$out" "usage: exhausted"
put_usage $((NOW - 60)) normal 20 $((NOW - 10))
run "$stop_input" "$repo/.claude/hooks/usage-stop.sh"
ok_if E2 "a reset in the past is not used" hasnt "$(cat "$FX/.claude/state/blocker.md")" "2026-11-15 11:59 UTC"
ok_if E2 "the future 7d reset is used instead" has "$(cat "$FX/.claude/state/blocker.md")" "reset: 2026-11-16 12:00 UTC"
run '{"error":"overloaded"}' "$repo/.claude/hooks/usage-stop.sh"
ok_if E2 "overloaded is recorded as a blocker type" has "$(cat "$FX/.claude/state/blocker.md")" "error: overloaded"
brief test-designer
ok_if E2 "overloaded does not set exhausted (A10)" hasnt "$out" "usage: exhausted"
run '{"error":"something_new"}' "$repo/.claude/hooks/usage-stop.sh"
ok_if E2 "an unlisted error is stored as unknown" has "$(cat "$FX/.claude/state/blocker.md")" "error: unknown"

# E4 and E5
run "$stop_input" "$repo/.claude/hooks/usage-stop.sh"
for s in sk-FAKE-SECRET-123 /Users/someone someone@example.com "Max plan" req_FAKE123; do
  ok_if E4 "state files do not contain '$s'" eval '! grep -rqF -- "$s" "$FX/.claude/state"'
done

# E8
put_usage $((NOW - 60)) exhausted 100 $((NOW + 1800))
run "$stop_input" "$repo/.claude/hooks/usage-stop.sh"
NOW_AT=$((NOW + 60)) run "$(sl 40 "$(win five_hour 10 $((NOW - 100))),$(win seven_day 5 $((NOW + 86400)))")" "$statusline"
NOW_AT=$((NOW + 60)) brief test-designer
ok_if E8 "fresh status-line data after the reset clears exhausted" has "$out" "usage: normal"
ok_if E8 "the status line itself shows normal" eval 'NOW_AT=$((NOW + 60)) run "$(sl 40 "$(win five_hour 10 $((NOW - 100))),$(win seven_day 5 $((NOW + 86400)))")" "$statusline"; has "$out" "usage: normal"'

# E9
pre_sums="$(tree_sums)"
run "$stop_input" "$repo/.claude/hooks/usage-stop.sh"
run '{"error":"overloaded"}' "$repo/.claude/hooks/usage-stop.sh"
ok_if E9 "no authoritative file changed" eq "$(tree_sums)" "$pre_sums"

# =====================================================================
# F. Index and stale check (index.sh)
# =====================================================================
fx_new
dec="$(cat "$FX/docs/ai/context/DECISIONS.md" 2>/dev/null)"
open="$(cat "$FX/docs/ai/context/OPEN-ITEMS.md" 2>/dev/null)"

# I1
ok_if I1 "4 phase-06 rows" eq "$(grep -c '^| P6-D' <<<"$dec")" "$(grep -c '^| P6-D' "$FX/docs/progress/phase-06-background.md")"
ok_if I1 "phase rows link to their source" has "$dec" "(../../progress/phase-06-background.md)"
ok_if I1 "a pipe inside a cell stays in one cell" has "$dec" 'bounded channel with a \| pipe'
ok_if I1 "rows carry who decided" eval 'grep "^| P6-D1 " <<<"$dec" | grep -q "| Alessandro |"'
ok_if I1 "one ADR row with a link" eq "$(grep -c '(../../adr/0003-background-assessment-queue.md)' <<<"$dec")" 1
ok_if I1 "the ADR row has its title" has "$dec" "Background assessment queue"
ok_if I1 "templates are not indexed" eval '! grep -q "_TEMPLATE" <<<"$dec$open"'
ok_if I1 "numeric phase order (02, 06, 10)" eval '[[ "$(grep -n "^| P2-D1 " <<<"$dec" | cut -d: -f1)" -lt "$(grep -n "^| P6-D1 " <<<"$dec" | cut -d: -f1)" && "$(grep -n "^| P6-D1 " <<<"$dec" | cut -d: -f1)" -lt "$(grep -n "^| P10-D1 " <<<"$dec" | cut -d: -f1)" ]]'
ok_if I1 "open items listed with links" eval 'grep "OPEN-6A" <<<"$open" | grep -qF "(../../progress/phase-06-background.md)"'
ok_if I1 "resolved items are left out" hasnt "$open" "RESOLVED-MARKER"
ok_if I1 "carry-overs listed once with a link" eval '[[ "$(grep -c "CARRY-7" <<<"$open")" == 1 ]] && grep "CARRY-7" <<<"$open" | grep -qF "(../../PROGRESS.md)"'

# I3
first_dec="$(cksum <"$FX/docs/ai/context/DECISIONS.md")"; first_open="$(cksum <"$FX/docs/ai/context/OPEN-ITEMS.md")"
CLAUDE_PROJECT_DIR="$FX" HARNESS_NOW=$((NOW + 999999)) "$ctx/index.sh" >/dev/null 2>&1
ok_if I3 "second run (other clock) gives identical DECISIONS.md" eq "$(cksum <"$FX/docs/ai/context/DECISIONS.md")" "$first_dec"
ok_if I3 "second run gives identical OPEN-ITEMS.md" eq "$(cksum <"$FX/docs/ai/context/OPEN-ITEMS.md")" "$first_open"
ok_if I3 "second run changes nothing in git" git -C "$FX" diff --quiet

# I5
run "" "$ctx/index.sh" --check
ok_if I5 "current index passes --check" eq "$rc" 0
sed 's/| reason three | Alessandro |/| reason three | the agent |/' "$FX/docs/progress/phase-06-background.md" >"$tmp_root/p6" && cp "$tmp_root/p6" "$FX/docs/progress/phase-06-background.md"
i5_sums="$(tree_sums)"
run "" "$ctx/index.sh" --check
ok_if I5 "edited Decided by: --check exits 1" eq "$rc" 1
ok_if I5 "message names the stale file" has "$err" "docs/ai/context/DECISIONS.md"
ok_if I5 "message names the fix command" has "$err" "scripts/context/index.sh"
# I8
ok_if I8 "--check on a stale index writes nothing" eq "$(tree_sums)" "$i5_sums"

# I6
run "" "$ctx/index.sh"
run "" "$ctx/index.sh" --check
ok_if I6 "regenerated index passes --check" eq "$rc" 0
# I8
i8_sums="$(tree_sums)"
run "" "$ctx/index.sh" --check
ok_if I8 "--check on a current index writes nothing" eq "$(tree_sums)" "$i8_sums"

# I7
cp "$FX/docs/adr/0003-background-assessment-queue.md" "$FX/docs/adr/0004-new-decision.md"
run "" "$ctx/index.sh" --check
ok_if I7 "a new ADR makes the index stale" eq "$rc" 1
run "" "$ctx/index.sh"
rm "$FX/docs/progress/phase-10-docs.md"
run "" "$ctx/index.sh" --check
ok_if I7 "a deleted phase file makes the index stale" eq "$rc" 1
run "" "$ctx/index.sh"

# I9 (subset; build item 2 and A1: a malformed phase file fails --check and names the file)
printf '\n## Decisions (fixture)\n\n| ID | Chosen | Decided by |\n|---|---|---|\n| P11-D1 | only two cells |\n' >"$FX/docs/progress/phase-11-bad.md"
run "" "$ctx/index.sh" --check
ok_if I9 "malformed Decisions table fails --check" eq "$rc" 1
ok_if I9 "message names the malformed file" has "$err" "docs/progress/phase-11-bad.md"
run '{"source":"startup"}' "$repo/.claude/hooks/session-context.sh"
ok_if I9 "hooks still exit 0 with a malformed phase file" eq "$rc" 0
rm "$FX/docs/progress/phase-11-bad.md"

# I12
verify_full="$(awk '/^else$/{on=1} on' "$repo/scripts/verify.sh")"
ok_if I12 "verify.sh full mode runs the index check" has "$verify_full" "scripts/context/index.sh --check"
ok_if I12 "a failed index check fails verify" eval 'grep -F "scripts/context/index.sh --check" "$repo/scripts/verify.sh" | grep -qF "|| fail"'
ok_if I12 "fast mode does not run it" eval '! awk "/MODE\" == \"fast\"/{on=1} /^else\$/{exit} on" "$repo/scripts/verify.sh" | grep -qF "index.sh"'

# I13
for gen in "$repo/docs/ai/context/DECISIONS.md" "$repo/docs/ai/context/OPEN-ITEMS.md" "$FX/docs/ai/context/DECISIONS.md" "$FX/docs/ai/context/OPEN-ITEMS.md"; do
  broken=0; dir="$(dirname "$gen")"
  links="$(grep -o '](\([^)]*\))' "$gen" 2>/dev/null | sed 's/^](//; s/)$//' | sort -u)"
  while IFS= read -r l; do [[ -z "$l" ]] && continue; [[ -e "$dir/${l%%#*}" ]] || broken=$((broken + 1)); done <<<"$links"
  ok_if I13 "${gen#"$tmp_root"/}: has links" test -n "$links"
  ok_if I13 "${gen#"$tmp_root"/}: every link resolves" eq "$broken" 0
done

# I15
for gen in DECISIONS.md OPEN-ITEMS.md; do
  ok_if I15 "$gen is marked generated in its first 5 lines" eval 'head -n 5 "$repo/docs/ai/context/$gen" | grep -q "GENERATED by .*scripts/context/index.sh.*[Dd]o not edit"'
done
ok_if I15 "the real index is current" eval 'CLAUDE_PROJECT_DIR="$repo" "$ctx/index.sh" --check >/dev/null 2>&1'

# =====================================================================
# G. Handovers
# =====================================================================
fx_new
template="$repo/docs/ai/context/HANDOVER-TEMPLATE.md"
handover() { # handover <name> <task line>: a valid handover from the template
  awk -v t="$2" '{print} $0 == "## Task ID" {print t}' "$template" >"$FX/docs/ai/context/handovers/$1"
}

# H1
for h in "## Task ID" "## Responsibility" "## Context references" "## Work done" "## Files" "## Discoveries" "## Concerns" "## Verification" "## Next action"; do
  ok_if H1 "template has '$h'" grep -qxF "$h" "$template"
done

# H6
brief spec-checker
ok_if H6 "empty handovers dir: no handovers" has "$out" "no handovers"
mv "$FX/docs/ai/context/handovers" "$tmp_root/handovers.bak"
brief spec-checker
ok_if H6 "missing handovers dir: exits 0" eq "$rc" 0
ok_if H6 "missing handovers dir: no handovers" has "$out" "no handovers"
mv "$tmp_root/handovers.bak" "$FX/docs/ai/context/handovers"

# H2
handover H1-001-test-designer.md "H1-001: design the fixture tests TASK-LINE-1"
brief spec-checker
ok_if H2 "brief lists the handover path" has "$out" "docs/ai/context/handovers/H1-001-test-designer.md"
ok_if H2 "brief shows its task line" has "$out" "TASK-LINE-1"
run "" "$ctx/index.sh" --check
ok_if H2 "a valid handover passes --check" eq "$rc" 0

# H3
for i in 02 03 04 05 06 07 08 09 10 11 12; do handover "H1-0$i-test-designer.md" "H1-0$i: task number $i"; done
brief spec-checker
ok_if H3 "only the newest 5 are listed" eq "$(grep -c 'docs/ai/context/handovers/H1-' <<<"$out")" 5
ok_if H3 "newest first" eval '[[ "$(grep -n "H1-012-" <<<"$out" | head -1 | cut -d: -f1)" -lt "$(grep -n "H1-008-" <<<"$out" | head -1 | cut -d: -f1)" ]]'
ok_if H3 "older ones are left out" hasnt "$out" "H1-007-"
handover H2-001-spec-checker.md "H2-001: later phase"; handover H10-001-spec-checker.md "H10-001: much later phase"
brief spec-checker
ok_if H3 "phase numbers sort numerically (H10 before H2)" eval '[[ "$(grep -n "H10-001-" <<<"$out" | cut -d: -f1)" -lt "$(grep -n "H2-001-" <<<"$out" | cut -d: -f1)" ]]'
rm -f "$FX"/docs/ai/context/handovers/H*.md

# H4
handover H1-001-test-designer.md "H1-001: good one GOOD-TASK"
printf '# Handover\n## Task ID\nH1-002: bad\nBODY-MARKER-XYZ\n' >"$FX/docs/ai/context/handovers/H1-002-spec-checker.md"
: >"$FX/docs/ai/context/handovers/H1-003-spec-checker.md"
printf '\000\001\002' >"$FX/docs/ai/context/handovers/H1-004-spec-checker.md"
{ handover_body="$(cat "$template")"; printf '%s\n' "$handover_body"; for i in $(seq 1 5000); do printf 'BIG-BODY line %s\n' "$i"; done; } >"$FX/docs/ai/context/handovers/H1-005-spec-checker.md"
brief spec-checker
ok_if H4 "exits 0" eq "$rc" 0
ok_if H4 "missing headings are reported" has "$out" "H1-002-spec-checker.md is malformed: missing"
ok_if H4 "an empty handover is reported" has "$out" "H1-003-spec-checker.md is malformed"
ok_if H4 "a binary handover is reported" has "$out" "H1-004-spec-checker.md is malformed"
ok_if H4 "bodies are not dumped" eval '[[ "$out" != *BODY-MARKER-XYZ* && "$out" != *"BIG-BODY"* ]]'
ok_if H4 "the good handover is still shown" has "$out" "GOOD-TASK"
ok_if H4 "brief stays under 100 lines" lines_le "$out" 100
run "" "$ctx/index.sh" --check
ok_if H4 "--check fails on a malformed handover (A11)" eq "$rc" 1
ok_if H4 "--check names the handover" has "$err" "H1-002-spec-checker.md"
run '{"agent_type":"spec-checker"}' "$repo/.claude/hooks/subagent-context.sh"
ok_if H4 "the SubagentStart hook still exits 0 with valid JSON" jq_ok "$out" '.hookSpecificOutput.hookEventName == "SubagentStart"'

# =====================================================================
# H. Cross-cutting
# =====================================================================
fx_new

# X1
case_lines() { grep -cE '^[[:space:]]*(bash_cmd|edit_file|expect) [0-9]' ; }
ok_if X1 "every case line from main is still in test-hooks.sh" eq "$(case_lines <"$repo/.claude/hooks/tests/test-hooks.sh")" "$(git -C "$repo" show main:.claude/hooks/tests/test-hooks.sh | case_lines)"
ok_if X1 "the main case lines are unchanged" eval '[[ -z "$(git -C "$repo" show main:.claude/hooks/tests/test-hooks.sh | grep -E "^[[:space:]]*(bash_cmd|edit_file|expect) [0-9]" | grep -vxFf "$repo/.claude/hooks/tests/test-hooks.sh")" ]]'

# X2
for s in $new_scripts .claude/hooks/reinject-context.sh .claude/hooks/tests/test-context.sh; do
  ok_if X2 "$s is executable" test -x "$repo/$s"
  mode="$(git -C "$repo" ls-files -s -- "$s" | cut -d' ' -f1)"
  [[ -n "$mode" ]] && ok_if X2 "$s has git mode 100755" eq "$mode" 100755
done

# X3
for s in $new_scripts; do
  ok_if X3 "$s: no network command" eval '! grep -Eq "(^|[^a-z_])(curl|wget|nc|ssh|scp|telnet)([^a-z_-]|$)" "$repo/$s"'
  ok_if X3 "$s: no read of home config, env files, keys or credentials" eval '! grep -Eq "~/\.claude|HOME/\.claude|\.env|\.pem|secrets/|credentials" "$repo/$s"'
  ok_if X3 "$s: no eval or bash -c" eval '! grep -Eq "(^|[^a-z_])eval([^a-z_]|$)|bash -c|sh -c" "$repo/$s"'
done
if command -v shellcheck >/dev/null; then
  for s in $new_scripts; do ok_if X3 "$s: shellcheck" shellcheck -x "$repo/$s"; done
fi

# X4
for f in .claude/state/usage.json .claude/state/checkpoint.md .claude/state/blocker.md docs/ai/context/handovers/H1-001-test-designer.md docs/ai/context/CURRENT-WORK.md; do
  edit_file 0 "$f"
done

# X6
for pair in session-context.sh:'{"source":"startup"}' subagent-context.sh:'{"agent_type":"test-designer"}' pre-compact.sh:'{"trigger":"auto"}' usage-stop.sh:'{"error":"overloaded"}'; do
  h="${pair%%:*}"; input="${pair#*:}"
  t0="$(date +%s)"; run "$input" "$repo/.claude/hooks/$h"; t1="$(date +%s)"
  ok_if X6 "$h finishes within 2 s" test $((t1 - t0)) -le 2
done
t0="$(date +%s)"; run "$(sl 40 "$(win five_hour 20 $((NOW + 3600)))")" "$statusline"; t1="$(date +%s)"
ok_if X6 "statusline.sh finishes within 1 s" test $((t1 - t0)) -le 1

# X7
for s in $new_scripts; do
  ok_if X7 "$s: set -uo pipefail" grep -qx 'set -uo pipefail' "$repo/$s"
  ok_if X7 "$s: no set -e" eval '! grep -Eq "^set -[a-z]*e" "$repo/$s"'
done
for s in $hook_scripts; do
  ok_if X7 "$s: ends with exit 0" eq "$(grep -v '^[[:space:]]*$' "$repo/$s" | tail -n 1)" "exit 0"
  ok_if X7 "$s: never exits non-zero" eval '! grep -Eq "exit [1-9]" "$repo/$s"'
done
