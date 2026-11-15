# Agent registry

Who does what, and why each agent has its model. The tools and the model are set in `.claude/agents/<name>.md`; this page explains them and does not change them. `scripts/context/brief.sh` reads the Kind, Responsibility, Boundaries and Model lines below, so keep those labels and one `## <agent>` heading per agent file.

Rules for every agent:
- Never downgrade a security or review model to save usage. When usage is "approaching", the lead cuts extra delegation and context loading first.
- Reviewers (`dotnet-reviewer`, `security-auditor`, `spec-checker`) do not get earlier reviewers' findings by default. They review the diff and the spec on their own. The lead passes earlier findings only when the task is a re-check of named fixes, and says so in the prompt.
- Decisions reserved for Alessandro are never made by an agent: plan approval (Checkpoint A), merge approval (Checkpoint B), scope changes, new packages, tools, MCP servers, permissions or secrets, accepting a risk, and any choice listed as "for Alessandro" in a phase file.
- Every agent ends its report with the headings in [HANDOVER-TEMPLATE.md](HANDOVER-TEMPLATE.md). Read-only agents cannot write files, so the lead saves the handover.

## dotnet-reviewer
- **Kind:** reviewer
- **Responsibility:** fresh-context diff review: correctness of the maths and rounding, security, framework usage, test gaps, over-engineering.
- **Boundaries:** read-only (Write, Edit and NotebookEdit are disallowed); reviews only the diff or range it is given; reports findings and never fixes them; no style comments.
- **Model:** opus. Reason: a missed correctness or security bug costs more than the usage; review needs the strongest reasoning.
- **Context it needs:** the diff (`git diff main...HEAD`), the touched files, `AGENTS.md`, `docs/spec/affordability-rules.md` when the maths changes. Not the implementer's reasoning or report.
- **Reserved for Alessandro:** whether a finding is accepted as a risk or deferred.

## parallel-implementer
- **Kind:** author
- **Responsibility:** implements one approved plan in its own git worktree, test-first, in small conventional commits.
- **Boundaries:** may use Write and Edit, only inside the approved plan's scope; never pushes, never force-pushes, never uses `--no-verify`; stops after the same step fails 3 times.
- **Model:** opus. Reason: it writes production code and tests; mistakes here are expensive to find later.
- **Context it needs:** the phase file ("Plan", "Decisions", the approved test proposal), `AGENTS.md`, `CLAUDE.md`, `docs/ai/context/CURRENT-WORK.md`.
- **Reserved for Alessandro:** any change to the approved plan, any new package, the merge.

## security-auditor
- **Kind:** reviewer
- **Responsibility:** the security gate: runs Gitleaks, Semgrep and the dependency scan, reviews the diff against OWASP Top 10:2025, and separates real findings from false positives.
- **Boundaries:** read-only (Write, Edit and NotebookEdit are disallowed); runs scanners and reads; never changes files; every false positive needs a concrete reason.
- **Model:** opus. Reason: security triage; a wrong "false positive" call is the costly failure.
- **Context it needs:** the diff, `.claude/rules/security.md`, the scanner output. Not earlier reviewers' findings.
- **Reserved for Alessandro:** accepting a security risk, adding a permission or a secret.

## spec-checker
- **Kind:** reviewer
- **Responsibility:** requirement traceability: maps every requirement in `docs/spec/ORIGINAL_SPEC.md` to the file, line or test that meets it (met, partial, missing).
- **Boundaries:** read-only (Read, Grep and Glob only; no Bash, Write or Edit); evidence or it is not "met"; no style opinions.
- **Model:** sonnet. Reason: structured reading and matching against a fixed list; the strongest model adds little here.
- **Context it needs:** `docs/spec/ORIGINAL_SPEC.md`, `docs/spec/affordability-rules.md`, the code and tests, the traceability table in `docs/PROGRESS.md`.
- **Reserved for Alessandro:** what is in or out of scope, how a requirement is read when the spec is ambiguous.

## test-designer
- **Kind:** designer
- **Responsibility:** proposes test cases (boundaries, failure paths, security cases) before implementation; never writes code.
- **Boundaries:** read-only (Read, Grep and Glob only; no Bash, Write or Edit); proposes, does not decide which cases run.
- **Model:** sonnet. Reason: structured reading of the plan and spec into a list of cases; the arithmetic is shown so it can be checked.
- **Context it needs:** the phase "Plan", the spec, the code under test if it exists.
- **Reserved for Alessandro:** which cases are approved.
