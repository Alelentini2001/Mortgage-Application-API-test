# Harness H1: context and usage awareness

Status: **In progress: plan and tests approved (2026-11-15), implementation next** · Branch: `harness-01-context` · PR: — · Started: 2026-11-15 · Finished: —

## Goal

Make every agent start from the project's real state (phase, decisions, open items, other agents' work) and make the lead agent aware of context-window and subscription-usage limits, using only documented Claude Code interfaces. Source: Alessandro's brief "Project-wide agent context intelligence & subscription-aware orchestration" (prompt H-00). Not a phase of the mortgage API: a harness phase run between Phase 5 and Phase 7 (H-D5).
Status: **Plan approved at Checkpoint A (2026-11-15): Alessandro chose A on H-D1 to H-D5 ("Approved, all A").** Implementation starts on branch `harness-01-context` after Phase 5 merges (H-D5 A). Test design runs now (read-only). Written 2026-11-15 by the lead agent from a read of the repo and the Claude Code docs (prompt H-01 to the `claude-code-guide` agent; facts it marked "summary" rather than quoted are flagged below).

## 1. Discovery report

### What exists today
| Part | What it does for context |
|---|---|
| `CLAUDE.md` → `@AGENTS.md`, nested `CLAUDE.md` (docs, tests, Core), `.claude/rules/*.md` (path-scoped) | Loaded automatically into the main agent and into every non-fork subagent (docs: subagents load the CLAUDE.md hierarchy). |
| `docs/PROGRESS.md` "Resume here" | The one hand-written state summary. Read at session start because `CLAUDE.md` says so (advice, not enforced). |
| `reinject-context.sh` (SessionStart, matcher `compact` only) | Prints "Resume here" and the branch after a compaction. Nothing runs on `startup`, `resume` or `clear`. |
| `CLAUDE.md` "Compaction" section | Asks the summariser to keep phase, step, files, tests, open decisions. Advice only; no `PreCompact` hook. |
| Phase files `docs/progress/phase-NN-*.md` (250–577 lines each) | Full record per phase: plan, decisions, prompts, tests, gates, open issues. Large. |
| `docs/ai/PROMPTS.md` (803 lines, 65 entries), `DESIGN-JOURNAL.md`, `ORCHESTRATION.md`, ADRs 0001–0003 | History. No index; an agent must read whole files to find a past decision. |
| 5 agents in `.claude/agents/` | Fixed roles, models (opus: implementer, reviewer, security; sonnet: spec-checker, test-designer), tool limits. No shared handover format. |
| Skills `phase`, `progress`, `verify-all`, `adr`, `retro` | The phase loop and evidence rules. |

### Answers to the eight discovery questions
1. **Context available to each agent.** Main agent: CLAUDE.md hierarchy, rules, the conversation, plus "Resume here" only after compaction. Subagents: CLAUDE.md hierarchy and a git status snapshot, plus whatever the lead writes in the prompt. Nothing else (docs: they "do not see" the parent conversation, invoked skills or files already read).
2. **Lost in a fresh context.** Decisions, what other agents found, which approaches failed, the current step, unless the lead restates them. In this session the lead re-briefed every subagent by hand (prompts P04-xx, P05-xx, P06-xx are long for that reason) and twice had to restate after usage-limit stops.
3. **On compaction.** The summary is model-written; `reinject-context.sh` adds "Resume here" and the branch. The current step, files under investigation and test commands survive only if the summariser keeps them. No checkpoint file is written.
4. **Subagents and decision history.** Only through the prompt or by reading whole phase files. No decision index.
5. **How findings travel.** Subagent final report → lead's context → lead copies it into the phase file. Prompts are logged; outcomes are summarised.
6. **Seeing other agents' work.** Not directly. A reviewer sees the diff, not the implementer's reasoning; a re-run reviewer does not see the first reviewer's findings unless the lead pastes them.
7. **Context-window reporting (documented).** `/context` (interactive). The status line JSON has `context_window.context_window_size`, `used_percentage`, `remaining_percentage`, `current_usage.*`, `exceeds_200k_tokens` (`used_percentage` counts input tokens only and is null right after `/compact` until the next call). `PreCompact` (`trigger` manual or auto) and `PostCompact` (`compact_summary`) hooks exist. Hooks receive no context-usage field.
8. **Usage and subscription data (documented).** The **only** machine-readable source is the status line JSON `rate_limits.five_hour` and `rate_limits.seven_day` (`used_percentage`, `resets_at` as Unix seconds), present only for Pro, Max and Ultra subscribers after the first API response, each window possibly absent. `/usage` is interactive only. A usage-limit hit surfaces as the `StopFailure` hook (`error`, matcher `rate_limit`) and messages such as "You've hit your session limit" / "…weekly limit" / "…Opus limit". **Not exposed anywhere:** plan name, remaining allowance in tokens or messages, per-model quotas as numbers, API billing for a subscription login (`cost.total_cost_usd` is a list-price estimate, not a bill). These stay `unknown`.

Also relevant: the subagent `memory` frontmatter field exists (`project` → `.claude/agent-memory/<name>/MEMORY.md`, first 200 lines loaded, Read/Write/Edit enabled). `SubagentStart` can add `additionalContext` to a subagent (docs; the example placement was "summary", to be confirmed on 2.1.283 in implementation). Installed Claude Code is 2.1.283; features documented for later builds (e.g. subagent `autoCompactWindow`, v2.1.296) are not used.

## 2. Proposed architecture (smallest robust version)

**Principle:** the repo docs stay the authority. New files are *indexes and handovers* that point to them, plus *local volatile state* (usage, checkpoint) that is never committed.

| Component | Purpose | Committed? |
|---|---|---|
| `docs/ai/context/CONTEXT-INDEX.md` | One page: where each kind of fact lives (PROGRESS, phase files, ADRs, journal, rules) and which sections to read for which task. Agents read this first instead of whole files. | yes |
| `docs/ai/context/DECISIONS.md` | Index of every decision (ID, one line, who decided, link to ADR or phase-file row). **Generated** by a script from the phase files' "Decisions" tables and ADR headers, so it never drifts or duplicates. | yes (generated) |
| `docs/ai/context/OPEN-ITEMS.md` | Index of open issues and carry-overs (generated from phase files' "Open issues" and PROGRESS carry-overs). | yes (generated) |
| `docs/ai/context/AGENT-REGISTRY.md` | Each agent: responsibility, boundaries, model and why, which context it needs, which decisions are Alessandro's only. | yes |
| `docs/ai/context/handovers/` + `HANDOVER-TEMPLATE.md` | One structured handover per delegated task (task ID, responsibility, context refs, work done, files, discoveries, concerns, verification, next action). The lead writes it from the agent's report (read-only agents cannot write). | yes |
| `scripts/context/brief.sh <agent> [phase]` | Prints a short, role-specific brief: phase, branch, resume section, decisions and open items relevant to the role, recent handovers, usage status. Used by hooks and by the lead when delegating. | yes |
| `scripts/context/index.sh` | Regenerates `DECISIONS.md` and `OPEN-ITEMS.md`; `verify.sh` checks they are current (fails if stale). | yes |
| `.claude/statusline.sh` | Shows model, context %, 5h/7d usage % and reset; writes a snapshot `.claude/state/usage.json` (measured values + timestamp; `unknown` when absent). Never stores tokens or account data. | script yes, state no |
| Hooks (new) | `session-context.sh` on SessionStart `startup|resume|clear|compact` (replaces `reinject-context.sh`'s job, keeps its output); `subagent-context.sh` on SubagentStart (adds the role brief); `pre-compact.sh` on PreCompact (writes `.claude/state/checkpoint.md` from git, last verify, open PR, `CURRENT-WORK` notes, and prints keep-instructions for the summary); `usage-stop.sh` on StopFailure (records the limit hit, reset time if known, and an exact resume instruction in the checkpoint). | yes; state no |
| `docs/ai/context/CURRENT-WORK.md` | Short lead-maintained "now": phase, step, files in play, next exact action. The checkpoint copies it. | yes |
| Usage-aware rules | A section in `CLAUDE.md` / `ORCHESTRATION.md`: thresholds (normal < 75%, approaching ≥ 75%, exhausted = 100% or a `StopFailure` rate-limit), what the lead does in each, and "unknown" handling. No model switching without verification. | yes |

Not proposed: an external database or memory service; the subagent `memory` field (see decision H-D1); changing any agent's model; parallel phases.

## 3. Decisions for Alessandro (options · trade-offs · recommendation)

**H-D1. Where agent memory lives.**
- **A. Committed indexes and handovers in `docs/ai/context/` + local gitignored `.claude/state/` for usage and checkpoint** (recommended). Reviewable, evidence-linked, survives restarts and machines; no uncontrolled notes.
- B. The built-in subagent `memory: project` field (`.claude/agent-memory/<agent>/MEMORY.md`). Automatic, but each agent writes free-form notes the lead does not review, and read-only agents gain Write/Edit (breaks the reviewer's read-only rule).
- C. Only extend `PROGRESS.md`. Smallest, but it is already 107 lines and would become the transcript dump the brief warns against.

**H-D2. Decision and open-item indexes.**
- **A. Generated by a script from phase files and ADRs, checked by `verify.sh`** (recommended): one source of truth, no drift.
- B. Hand-maintained index: simpler, but duplicates and drifts.

**H-D3. Status line and usage snapshot.**
- **A. Project `statusLine` in `.claude/settings.json`** (recommended): the snapshot exists for everyone working in the repo. Cost: replaces a personal status line inside this project.
- B. Personal `.claude/settings.local.json`: keeps your own status line choice; usage awareness works only where it is set.

**H-D4. Subagent context injection.**
- **A. Both: a `SubagentStart` hook adds the role brief automatically, and the lead's prompt references the handover/brief** (recommended). If the hook output does not reach the subagent on 2.1.283 (to be checked in implementation), the explicit brief still covers it.
- B. Explicit brief in the prompt only (today's practice, templated).

**H-D5. Sequencing and branch.**
- **A. Run this as harness phase `H1` on branch `harness-01-context`, after Phase 5 merges and before Phase 7** (recommended): one phase at a time (your rule), and later phases benefit.
- B. After Phase 11 (the project is finished first; the harness work then helps only the retrospective).

Model assignments: **no change proposed.** Opus stays on implementer, reviewer and security auditor (correctness and security), Sonnet on spec-checker and test-designer (structured reading). The registry records the reasons and a rule: when usage is "approaching", cut extra delegation and context loading first, never downgrade a security or review model to save usage.

## 4. Tests (plan)
Hook regression cases in `.claude/hooks/tests/test-hooks.sh` with JSON fixtures: SessionStart for each source; missing `PROGRESS.md`, missing phase file, malformed resume section; SubagentStart for each agent type and an unknown one (brief contains its responsibility and the "Alessandro decides" rule); PreCompact writes a checkpoint and never edits authoritative files; StopFailure `rate_limit` with and without reset info; status line with full `rate_limits`, one window absent, none at all, `null` context usage (prints and stores `unknown`, never a guess); model change between snapshots; concurrent writes to the state file (atomic rename); malformed handover file is reported, not crash. `index.sh` round-trip and stale-index failure in `verify.sh`. A manual check on 2.1.283 that `SubagentStart` context reaches a subagent. No test touches real account usage.

## 5. Gates and evidence
Test-designer before tests; `security-auditor` (state files hold no secrets; hooks handle hostile JSON; no credential reads); `dotnet-reviewer` replaced by a fresh-context reviewer suited to shell and docs (the `dotnet-reviewer` agent can review shell too); `spec-checker` against this brief's Parts 1–9; local `/code-review` and `/security-review`; Checkpoint B.

## 6. Demonstrations promised for the PR
1. A fresh subagent started with only "continue the open item X" gets its brief from the hook and finds the decision and handover without the previous conversation.
2. With the usage snapshot removed or stale, every brief says `usage: unknown (no status-line data since …)`; with a fixture `StopFailure` rate-limit input, the checkpoint gets the blocker, the reset time if present, and the resume instruction.

## 7. Test proposal (`test-designer`, prompt H-02; approved by Alessandro 2026-11-15: recommended set + manual check + U10, answers A1–A15 as recommended)

About 110 cases in nine groups, all bash with fixtures in temp repos (`CLAUDE_PROJECT_DIR` points at the fixture), a fixed clock (`HARNESS_NOW`), no network and no real account data; jq-less fallback tested the existing way (PATH without jq). Groups: SessionStart (S1–S16), SubagentStart and brief (B1–B10), PreCompact (C1–C10), status line and usage states (U1–U25), StopFailure (E1–E11), index and stale check (I1–I15), handovers (H1–H8), cross-cutting (X1–X9), and one manual check on 2.1.283 (a unique marker in the test-designer brief; a fresh subagent must echo it without reading files; repeated with spec-checker; recorded in the phase file and PROMPTS.md).

The full proposal, verbatim: [`harness-01/test-proposal.md`](harness-01/test-proposal.md).

**Recommended selection:** the designer's "minimum set" (about 70 cases: S1–S7, S12–S14, S16; B1–B5, B8–B9; C1, C4–C6, C8; U1, U2, U5–U8, U11–U13, U15–U16, U19–U22; E1–E2, E4–E5, E8–E9; I1, I3, I5–I8, I12–I13, I15; H1–H4, H6; X1–X4, X6–X7) plus the manual check. Riskiest areas it names: BSD vs GNU `date` (U10, add it), partial writes of `usage.json` (U15–U16), a timestamp inside generated files (I3), copying secrets into state (U21, C6, E4–E5).

**Questions and recommended answers (lead agent):**
- A1 Malformed phase file: `index.sh --check` fails (blocks verify); hooks warn and continue.
- A2 Size cap: injected context (resume section + brief) under about 100 lines, with a "truncated, see …" line.
- A3 Reviewer-type agents do **not** get earlier reviewers' findings by default (independence); the registry says so.
- A4 `brief.sh nonsense` (CLI) exits non-zero with usage; the hook always gives the generic brief.
- A5 PreCompact stdout vs a manual `/compact` instruction: not confirmed in the docs read. Keep-instructions are written as additions ("Also keep …"); behaviour checked manually once and recorded.
- A6 `usage.json` schema, allowlist only: `updated_at`, `model_id`, `model_name`, `context_used_pct`, `five_hour.{used_pct,resets_at}`, `seven_day.{used_pct,resets_at}`, `state`.
- A7 Negative or non-numeric → unknown for that window; ≥ 100 → exhausted.
- A8 Stale after 15 minutes, by the `updated_at` field (never file mtime).
- A9 `usage-stop.sh` writes its own `.claude/state/blocker.md` (one writer per file); the SessionStart hook shows it.
- A10 Only `rate_limit` sets `exhausted`; other `StopFailure` errors are recorded as a blocker type without changing the usage state; no billing or account details stored.
- A11 `index.sh --check` also validates handover headings; handovers are named `H<phase>-<NNN>-<agent>.md` and sorted by name.
- A12 Without jq: print and store `unknown`, never parse numbers with sed.
- A13 Keep `reinject-context.sh` as a thin shim that calls the new hook, so its existing test stays unchanged.
- A14 Resume wording: "Start a new session after <reset or 'the reset'>; Resume here, CURRENT-WORK and the checkpoint load automatically. Next action: <from CURRENT-WORK>."
- A15 Do not rely on `StopFailure` stdout; the hook works only through its file write.

## Prompts

### H-00 · human→agent · 2026-11-15
- **Prompt:** the brief "FOLLOW-UP TASK: PROJECT-WIDE AGENT CONTEXT INTELLIGENCE & SUBSCRIPTION-AWARE ORCHESTRATION" (Parts 1–9, execution rules, final objective), pasted in full. Not copied verbatim here because of its length; its headings and requirements are reflected in "Goal", the plan and the tests. Kept in the session transcript.
- **Purpose:** new scope of work.
- **Outcome:** discovery, plan, Checkpoint A.

### H-01 · agent→subagent (`claude-code-guide`) · 2026-11-15
- **Prompt (verbatim):**
  ```
  I am designing context and usage awareness for a Claude Code project harness (installed Claude Code 2.1.283, macOS, Pro/Max subscription login, no API key). I need verified facts from the official Claude Code documentation only; for each answer give the doc URL and quote the exact field or event names. If something is not documented, say "not documented" rather than guessing.

  1. Hooks: the full list of hook events supported today. For each of SessionStart (and its matcher sources: startup, resume, clear, compact), PreCompact, PostCompact (does it exist?), SubagentStart (does it exist?), SubagentStop, Stop, UserPromptSubmit, Notification, SessionEnd: the JSON input fields (e.g. session_id, transcript_path, cwd, trigger, custom_instructions, agent_id, agent_type, stop_hook_active), and which events can inject context into the model (hookSpecificOutput.additionalContext or plain stdout) versus only block or log. Can a SubagentStart hook add context to the subagent? Does PreCompact allow adding instructions to the compaction summary?
  2. Subagents (.claude/agents/*.md): the supported frontmatter fields today (name, description, tools, disallowedTools, model, maxTurns, isolation, skills, memory, hooks, permissionMode, initialPrompt, effort, etc.). Is there a documented persistent "memory" field for subagents and how does it work (where is it stored)? Do subagents load CLAUDE.md / project memory automatically? Do they see the parent conversation?
  3. Status line: the JSON object passed to a statusLine command on stdin: every documented field, in particular model (id, display_name), context window size and current usage (e.g. context_window, exceeds_200k_tokens, current usage percentages), cost (total_cost_usd, durations), and any rate-limit or subscription-usage fields (e.g. rate_limits, five-hour or weekly usage, reset times). Say exactly which ones exist.
  4. Usage and limits: which documented commands or interfaces report subscription usage, remaining allowance and reset times for a subscription user (/usage, /status, /cost, /context, others), and whether any of these are available to hooks or scripts (not just the interactive UI). How does Claude Code surface a usage-limit or rate-limit hit (message text, hook event, exit status)?
  5. Context window: how a user or script can read current context usage (/context, status line fields), the context-window size per model, and when auto-compaction triggers.
  6. Environment variables documented as available inside hook and status-line commands (e.g. CLAUDE_PROJECT_DIR, CLAUDE_ENV_FILE, CLAUDE_CODE_*). Only documented ones.

  Keep the answer structured by question number. Mark clearly anything that may depend on version and which version the docs describe.
  ```
- **Purpose:** verify hook events, status-line JSON, usage reporting and subagent options in the official docs.
- **Outcome:** 33 hook events incl. PreCompact, PostCompact, SubagentStart, StopFailure; status-line `context_window` and `rate_limits` fields; no scriptable plan name, allowance or per-model quota. Items it marked "summary" (not quoted) are flagged in the discovery report.

### H-02 · human→agent · 2026-11-15
- **Prompt (verbatim):**
  > Approved, all A
- **Purpose:** Checkpoint A approval (H-D1 to H-D5 all option A).
- **Outcome:** test design started.

### H-03 · agent→subagent (`test-designer`) · 2026-11-15
- **Prompt (verbatim):**
  ```
  Harness phase H1 (context and usage awareness for the Claude Code harness) of a .NET 10 mortgage-application API repo. Propose test cases for the approved plan; do not write code or edit files.

  Read first: the approved plan /private/tmp/claude-501/-Users-ale-ssd-Developer-Mortgage-Application-API-test/38287261-0d8e-41a1-a4c6-05aca4d87104/scratchpad/harness/H-discovery-and-plan.md (all decisions H-D1 to H-D5 are option A), then in /Users/ale_ssd/Developer/Mortgage-Application-API-test: .claude/settings.json, .claude/hooks/*.sh, .claude/hooks/tests/test-hooks.sh (how hook cases are written today: JSON piped to the hook, exit code checked), .claude/agents/*.md, scripts/verify.sh, CLAUDE.md, AGENTS.md, docs/PROGRESS.md (its "Resume here" section format), one phase file (docs/progress/phase-06-background.md: "Decisions" table and "Open issues" format), docs/adr/0003-background-assessment-queue.md (ADR header format).

  Documented Claude Code facts the design relies on (installed version 2.1.283): SessionStart input has source = startup | resume | clear | compact (| fork) and hooks can return hookSpecificOutput.additionalContext or plain stdout; SubagentStart input has agent_id and agent_type and can return hookSpecificOutput.additionalContext; PreCompact input has trigger (manual | auto) and custom_instructions, and plain stdout on exit 0 becomes compaction instructions; StopFailure input has error (matcher rate_limit, overloaded, ...), optional error_details and last_assistant_message; the status line command receives JSON on stdin with model.id/display_name, context_window.context_window_size/used_percentage/remaining_percentage/current_usage (current_usage may be null), and rate_limits.five_hour and rate_limits.seven_day each with used_percentage and resets_at (Unix seconds), each window possibly absent, the whole rate_limits object absent for non-subscribers or before the first response.

  What will be built: scripts/context/brief.sh <agent> (role-specific brief), scripts/context/index.sh (generates docs/ai/context/DECISIONS.md and OPEN-ITEMS.md from phase files and ADRs; verify.sh fails when they are stale), docs/ai/context/{CONTEXT-INDEX.md, AGENT-REGISTRY.md, CURRENT-WORK.md, HANDOVER-TEMPLATE.md, handovers/}, .claude/statusline.sh (prints model, context %, usage; writes .claude/state/usage.json atomically, gitignored), hooks session-context.sh (SessionStart all sources; keeps what reinject-context.sh prints today), subagent-context.sh (SubagentStart), pre-compact.sh (PreCompact: writes .claude/state/checkpoint.md, prints keep-instructions), usage-stop.sh (StopFailure: records the blocker, reset time if known, exact resume instruction). Usage states: normal < 75%, approaching >= 75%, exhausted at 100% or a rate-limit StopFailure, unknown when no data or stale.

  Cover at least the brief's Part 7 list: new-session context loading for each SessionStart source; context after compaction; missing PROGRESS.md; missing or malformed phase files; invalid or incomplete context; context for fresh subagents (each of the five agent types and an unknown one: the brief names its responsibility, boundaries and the "decisions reserved for Alessandro" rule); retrieval of previous decisions (index contents, stale-index failure); no duplication of authoritative records (index rows link to sources, generated files are marked generated); handover creation, retrieval and malformed handovers; preservation of existing context (hooks never write PROGRESS.md, phase files, ADRs or PROMPTS.md); concurrent writes to the state files; usage available, unavailable, partially absent, null values, stale snapshot, model change, rate limit with and without reset info, exhaustion; human checkpoints and author/reviewer independence stated in every brief; no secrets or account data written to state files; hostile or malformed JSON on stdin; existing hook cases still pass. Say how each case is run (bash with fixtures, no network, no real account data), which need jq and how the jq-less fallback is tested, and how to keep tests deterministic (fixed clock via an env var or file timestamp, temp dirs). Name the one check that can only be done manually on 2.1.283 (SubagentStart context reaching the subagent) and how to record it.

  Use your agent's output format. Mark each case must or nice, keep the list tight, and list ambiguities in the plan for Alessandro to settle.
  ```
- **Purpose:** propose H1 test cases.
- **Outcome:** about 110 cases and 15 questions; see section 7.

### H-04 · human→agent · 2026-11-15
- **Prompt (verbatim):**
  > Approved
- **Purpose:** approval of the recommended test set and answers A1–A15.
- **Outcome:** recorded; implementation waited for the Phase 5 merge (H-D5).

### H-05 · human→agent · 2026-11-15
- **Prompt (verbatim):**
  > Approved, merge here
- **Purpose:** Phase 5 Checkpoint B (recorded in `phase-05-observability.md`, P05-11); it also unblocked H1.
- **Outcome:** PR #9 merged as `352c6c8`; branch `harness-01-context` created from `cd9f7be`.

## What was built (files)

## Tests (results)

## Security gate

## Review gate

## AI output corrected or rejected (REAL cases only)

## Open issues

## Time spent (approx.)

## Human review (Alessandro writes this; the agent leaves it empty)
