# H1 brief from Alessandro (prompt H-00), verbatim

````
FOLLOW-UP TASK: PROJECT-WIDE AGENT CONTEXT INTELLIGENCE & SUBSCRIPTION-AWARE ORCHESTRATION
You are working inside:
`Alelentini2001/Mortgage-Application-API-test`
Your task is to enhance the existing Claude Code multi-agent orchestration system by implementing two major capabilities:

1. Persistent Project Context Awareness
2. Model, Context-Window, Subscription and Usage Awareness

This is an enhancement to the existing engineering harness, NOT a replacement.
The goal is to make every agent operate as part of one coordinated engineering intelligence system, rather than as isolated agents that repeatedly rediscover information, lose decisions, duplicate work or exhaust available subscription usage.
You must inspect the existing architecture before making changes.
PART 1 — DEEP REPOSITORY DISCOVERY
Before implementation, inspect:

* `AGENTS.md`
* `CLAUDE.md`
* All nested `CLAUDE.md` files
* `.claude/settings.json`
* `.claude/agents/`
* `.claude/hooks/`
* `.claude/skills/`
* `docs/PROGRESS.md`
* `docs/progress/`
* `docs/ai/ORCHESTRATION.md`
* `docs/ai/PROMPTS.md`
* `docs/ai/DESIGN-JOURNAL.md`
* Existing phase workflows
* Existing verification and security scripts

Understand how context currently enters an agent, how subagents are started, how work is delegated, and how context survives compaction and session restarts.
Identify:

1. What context is currently available to each agent.
2. What information is lost when an agent starts in a fresh context.
3. What happens when the main agent compacts its conversation.
4. Whether subagents have access to the project's decision history.
5. How agents communicate findings and implementation progress.
6. Whether agents can see the work performed by other agents.
7. How Claude Code reports context-window and usage information.
8. Which usage/subscription information is actually accessible through supported mechanisms.

Produce a discovery report before implementing anything.
Do not redesign working components unnecessarily.
PART 2 — PROJECT CONTEXT INTELLIGENCE
Build a persistent, structured project-memory system.
2.1 Shared Project Context
Create a central project context that gives agents a reliable understanding of:

* Project purpose and objectives.
* Current development phase.
* Current branch.
* Completed phases.
* Current implementation status.
* Previous architecture decisions.
* Outstanding decisions.
* Accepted requirements.
* Outstanding requirements.
* Important technical constraints.
* Existing bugs and known limitations.
* Previously encountered problems and their solutions.
* Test and verification status.
* Security findings.
* Current implementation priorities.
* Dependencies between features and phases.

Use existing documentation wherever possible.
Suggested structure (adapt to existing conventions):
`docs/ai/context/`

* `PROJECT-CONTEXT.md`
* `ARCHITECTURE-CONTEXT.md`
* `DECISIONS.md`
* `KNOWN-ISSUES.md`
* `AGENT-REGISTRY.md`
* `CURRENT-WORK.md`
* `CONTEXT-INDEX.md`

Avoid duplicating information that already exists in `docs/PROGRESS.md`, ADRs or the design journal. Establish clear ownership and cross-reference existing sources.
2.2 Agent Context Injection
Every agent must receive the relevant context before beginning work.
Implement an appropriate context-loading mechanism that:

1. Reads the global project instructions.
2. Reads the current development phase.
3. Reads relevant previous decisions.
4. Identifies outstanding tasks.
5. Identifies relevant architecture constraints.
6. Identifies related work performed by other agents.
7. Loads only the information relevant to the agent's responsibility.

Context injection must work for:

* Main Claude Code agent.
* `parallel-implementer`.
* `dotnet-reviewer`.
* `spec-checker`.
* `security-auditor`.
* `test-designer`.
* Any future specialised agents.

Do not assume that subagents automatically inherit the complete parent conversation.
Where supported, pass an explicit, concise context brief when delegating work.
2.3 Persistent Agent Memory
Agents must be able to preserve relevant information beyond their current conversation.
Implement a controlled memory workflow:
Before work:

* Retrieve relevant context.
* Check existing decisions.
* Check outstanding tasks.
* Check whether another agent has already investigated the issue.

During work:

* Record important discoveries.
* Record decisions and their rationale.
* Record blockers.
* Record important failed approaches and their explanations.
* Record files modified.
* Record test results.

After work:

* Produce a structured handover.
* Update the appropriate project documentation.
* Make discoveries available to subsequent agents.
* Update outstanding work and dependencies.

Do not turn memory into an uncontrolled collection of chat transcripts.
Use concise, structured and evidence-backed records.
Never treat unverified agent assumptions as established project facts.
2.4 Cross-Agent Awareness
Create a mechanism for agents to understand what other agents are doing.
For example:

* Test Designer identifies missing test coverage.
* Implementer receives those approved test requirements.
* Reviewer receives the implementation context and intended behaviour.
* Security Auditor receives relevant implementation and architectural changes.
* Lead Agent consolidates findings.

Agents must understand their responsibility, the boundaries of their task and the work already completed by other agents.
Each agent should produce a handover containing:

* Task identifier.
* Responsibility.
* Context references.
* Work completed.
* Files inspected or modified.
* Important discoveries.
* Outstanding concerns.
* Verification performed.
* Recommended next action.

Do not allow agents to make decisions reserved for Alessandro.
PART 3 — CONTEXT-WINDOW INTELLIGENCE
Implement intelligent context-window management.
The system should distinguish:

* Model context-window capacity.
* Context currently consumed.
* Estimated remaining context.
* Compaction events.
* Subscription usage limits.
* API billing and token usage, where applicable.

These are different measurements and must never be conflated.
3.1 Context Monitoring
Investigate Claude Code's currently supported mechanisms for reporting context-window usage.
Use documented functionality, hooks, status information or other supported interfaces.
Do not invent environment variables, commands, JSON fields or APIs.
Track, where available:

* Active model.
* Context-window capacity.
* Current context usage.
* Remaining context.
* Estimated consumption rate.
* Recent compaction events.

Clearly label estimated versus measured data.
3.2 Intelligent Compaction Preparation
Before context is exhausted, preserve the information needed to continue working.
Create an appropriate checkpoint containing:

1. Current task and phase.
2. Approved implementation plan.
3. Current development step.
4. Decisions already made.
5. Files changed.
6. Files currently being investigated.
7. Test commands and results.
8. Outstanding issues.
9. Subagent activity.
10. Next exact action.

The checkpoint must be durable and automatically available after compaction or session restart.
Do not allow context-management automation to overwrite authoritative project decisions.
3.3 Context Efficiency
Introduce intelligent context-loading strategies.
Agents should:

* Load only relevant files.
* Avoid repeatedly reading enormous documentation files.
* Prefer indexes and targeted sections.
* Reuse previously established findings.
* Avoid repeating research already performed.
* Summarise completed investigations.
* Preserve precise technical details when summarising.
* Avoid excessive delegation when one agent can complete a task efficiently.

Do not sacrifice correctness or security to reduce token consumption.
PART 4 — SUBSCRIPTION AND USAGE AWARENESS
This is a particularly important feature.
I want the orchestration system to be aware of the Claude subscription and usage constraints under which the project is being developed.
Investigate what information Claude Code actually exposes in the installed environment.
Distinguish:

1. Claude Code session context usage.
2. Model token consumption.
3. Subscription usage limits.
4. Rate limits.
5. Model-specific usage restrictions.
6. API usage and billing, if API credentials are being used.

Do not confuse subscription usage with API billing.
4.1 Usage Intelligence
Where supported and accessible, collect:

* Active model.
* Current subscription or authentication mode, if exposed.
* Current usage limit.
* Remaining usage allowance.
* Reset time.
* Model-specific restrictions.
* Rate-limit responses.
* Available alternative models.

Use official and currently supported interfaces.
Where live subscription data is inaccessible, report that status as `unknown` rather than pretending it is available.
Do not access or store authentication tokens, credentials, private account data or browser session information.
4.2 Usage-Aware Agent Orchestration
The main agent must account for usage constraints when planning work.
For example:
Normal usage:

* Proceed with the approved workflow.
* Delegate only where delegation adds value.
* Maintain quality and independent reviews.

Usage approaching a limit:

* Prioritise critical work.
* Avoid unnecessary subagent launches.
* Reduce redundant context loading.
* Save durable checkpoints.
* Consolidate work into fewer, more efficient interactions.

Usage exhausted or rate-limited:

* Do not repeatedly retry doomed requests.
* Preserve all progress.
* Record the blocker.
* Identify the earliest known reset time, when available.
* Prepare an exact resume instruction.
* Report what was completed and what remains.

Never claim that another model or subscription will be available unless this has been verified.
4.3 Model Selection Awareness
Create an agent configuration strategy that considers:

* Task complexity.
* Required reasoning depth.
* Security sensitivity.
* Review independence.
* Current model availability.
* Usage constraints.

For example, security reviews and critical financial calculations should not automatically be delegated to a cheaper model solely to conserve usage.
Model selection must preserve the existing intentional choices in `.claude/agents/`.
Any changes to model assignments require an explicit explanation and trade-off analysis.
PART 5 — INTELLIGENT DEVELOPMENT ORCHESTRATION
Enhance the current phase workflow without removing the human approval checkpoints.
The lead agent should understand the entire project's current state before starting a new phase.
For every phase:

1. Retrieve the current project context.
2. Retrieve previous relevant decisions.
3. Identify dependencies.
4. Identify previous implementation work.
5. Check available context and usage information.
6. Estimate the complexity of the phase.
7. Develop an efficient execution plan.
8. Delegate to specialised agents only where beneficial.
9. Preserve agent handovers.
10. Consolidate results.
11. Update persistent context.
12. Prepare for the next phase.

Keep both existing human checkpoints:

* Checkpoint A: Alessandro approves the implementation plan.
* Checkpoint B: Alessandro approves the pull request.

Never bypass these checkpoints.
Preserve the rule that authors do not review their own work.
Do not reintroduce parallel phase execution without explicit approval. The existing orchestration decision is sequential development.
PART 6 — IMPLEMENTATION AND TOOLING
Implement the system using the existing project's conventions.
Potential components include:

* Context-loading scripts.
* Context indexing.
* Context checkpoint generation.
* Agent handover templates.
* Session hooks.
* Usage-status collection.
* Status-line integration.
* Agent-specific context instructions.
* Usage-aware orchestration instructions.

These are implementation options, not mandatory files. Choose the smallest robust architecture.
Investigate Claude Code's supported hooks and status interfaces before implementation.
Pay particular attention to:

* SessionStart.
* PreCompact.
* PostCompact, if supported.
* SubagentStart and SubagentStop, if supported.
* Stop.
* Status-line functionality.
* Supported hook input/output contracts.

Use only verified events and schemas.
Maintain compatibility with the installed Claude Code version.
Do not break existing hooks:

* `protect-files.sh`
* `block-dangerous-bash.sh`
* `format-on-edit.sh`
* `verify-on-stop.sh`
* `reinject-context.sh`

Update hook regression tests for new functionality.
Do not add external dependencies without Alessandro's approval.
Do not introduce an external database or remote memory service unless there is a compelling, approved reason. Local version-controlled project documentation should remain the authoritative project memory.
PART 7 — TESTING
Create meaningful tests covering:
Context awareness

* New session context loading.
* Context after compaction.
* Missing progress files.
* Missing phase files.
* Invalid or incomplete context.
* Context loading for fresh subagents.
* Retrieval of previous decisions.
* No duplication of authoritative records.

Persistent memory

* Handover creation.
* Handover retrieval.
* Concurrent context updates, where applicable.
* Preservation of existing context.
* Handling malformed records.

Usage awareness

* Usage information available.
* Usage information unavailable.
* Unknown values.
* Model changes.
* Rate limiting.
* Usage exhaustion.
* Reset information unavailable.

Orchestration

* Agent receives its responsibility.
* Agent receives relevant project context.
* Human checkpoints remain mandatory.
* Author/reviewer independence remains intact.

Tests must not depend on real subscription exhaustion or disclose actual account usage.
Use fixtures and mocked usage responses where necessary.
Run the existing verification and hook regression suites.
PART 8 — DOCUMENTATION
Update:

* `AGENTS.md`
* `CLAUDE.md`
* `docs/ai/ORCHESTRATION.md`
* `docs/PROGRESS.md`
* Relevant agent definitions.
* Relevant skills.
* Relevant hook documentation.

Document:

1. How shared project context works.
2. How agents retrieve context.
3. How handovers work.
4. How context checkpoints are created.
5. How subscription awareness works.
6. What usage information is measurable.
7. What information remains inaccessible.
8. How to recover after usage exhaustion.
9. How developers can inspect the current system.

The documentation must reflect the implementation, not aspirations.
PART 9 — REQUIRED DELIVERABLES
At completion, provide:

1. Architecture discovery report.
2. Proposed architecture and trade-offs.
3. List of files created or modified.
4. Context intelligence implementation.
5. Subscription and usage-awareness implementation.
6. Updated agent instructions.
7. Updated hook system.
8. Automated tests.
9. Test and verification results.
10. Updated architecture documentation.
11. Remaining limitations and unsupported capabilities.
12. Recommended next improvements.

Include a concise demonstration showing how a fresh agent can retrieve project context and continue work without relying on the previous agent's conversation.
Also demonstrate how the system responds when usage information is unavailable or a rate limit is encountered.
EXECUTION RULES

* Follow the existing `CLAUDE.md` and `AGENTS.md`.
* Start by reading `docs/PROGRESS.md` and the current phase file.
* This is a new scope of work: do not assume approval from another phase automatically covers it.
* Follow Checkpoint A before implementation.
* Do not start implementation before Alessandro approves the plan.
* Do not silently replace existing architecture.
* Never invent usage metrics or claim API access that does not exist.
* Never expose secrets or subscription credentials.
* Keep all progress and decisions in the repository.
* Log every subagent prompt verbatim in `docs/ai/PROMPTS.md`.
* Use fresh-context reviewers.
* Run security and specification reviews.
* Stop at Checkpoint B and wait for Alessandro's approval.

FINAL OBJECTIVE
Transform the existing Claude Code agent setup into a context-aware, usage-aware, persistent and coordinated multi-agent engineering environment.
Every agent should know:

* What we are building.
* Why we are building it.
* What has already been done.
* What decisions have been made.
* What other agents have discovered.
* What it is responsible for.
* What needs to happen next.
* How much context it has available.
* What subscription and usage constraints are actually known.

The system should be able to recover intelligently after context compaction, session restarts and usage interruptions, without losing important engineering knowledge or violating the existing human approval workflow.
````
