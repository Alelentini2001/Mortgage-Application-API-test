# Design journal

Chronological record of ideas, options, rejected ideas, who decided, and what changed and why.

## 2026-11-13 · Phase 0 planning

**First reading.** The brief asks for a small, clean ASP.NET 10 API with tests, and for AI to be used visibly to both code and review. So the reviewers will judge the code quality and the engineering process in equal measure. The harness (rules, hooks, reviewer agents, evidence files) is therefore part of the deliverable, not overhead.

**Environment findings that changed the plan.**
- Only the .NET 7 SDK was installed. The brew cask installed SDK 11 (there is no 10 cask). Options were the official pkg, the brew formula, or container-only builds. **Alessandro installed the official .NET 10 pkg.**
- The GitHub repo is private. **Alessandro decided** to keep it private while building and make it public in Phase 11, after a history scan.
- Semgrep and Gitleaks were missing. **Alessandro approved** a brew install. The Microsoft Learn MCP was **not** added (Alessandro's choice), so API checks use WebFetch on learn.microsoft.com.

**Harness decisions (D1–D9, proposed by the agent, approved by Alessandro on 2026-11-14).**
- D1: an initial commit on `main`, then one branch per phase, because a PR needs a base branch.
- D2: block every `.env*` edit, including `.env.example`. Config goes through appsettings and compose environment variables instead.
- D3: block force push only (`--force`, `-f`), not every `--force`. Otherwise harmless commands like `dotnet new --force` would be blocked too.
- D4: whitespace-only format on each edit (~1 s). The full `dotnet format --verify-no-changes` runs in `verify.sh`.
- D5: `.slnx` solution, the .NET 10 default.
- D6: central package management, so package versions are audited in one file.
- D7: AI review workflows are created now and skip until the API key secret exists. **Superseded at Checkpoint B:** Alessandro wants AI reviews run on his subscription, so the workflows were removed and the reviews run locally (`/code-review`, `/security-review`), posted as PR comments.
- D8: repo artefacts stay in normal English regardless of the chat style. `docs/PROGRESS.md` is the only progress tracker.
- D9: the official .NET 10 SDK pkg.

## 2026-11-14 · Phase 0 implementation

- **xUnit v3 packaging.** Checked the xUnit docs: `xunit.v3` 4.x defaults to Microsoft Testing Platform v2, and with the .NET 10 SDK the `global.json` `test.runner` setting replaces `TestingPlatformDotnetTestSupport`. Chose the plain `xunit.v3` package, with no `Microsoft.NET.Test.Sdk`, because the project is MTP-only.
- **Stop hook scope.** The first version ran verify whenever anything under `src/` or `tests/` changed, including nested `CLAUDE.md` files. Narrowed it to code and build files (`*.cs`, `*.csproj`, `*.props`, `*.slnx`, `global.json`), so doc edits never trigger a build.
- **Dangerous-bash hook rewrite.** The agent's first version had two bugs, both caught by its own hook tests (see the phase file): a plain `rm file.txt` exited with code 1, and flags were read from the whole command line. The rewrite parses each command segment separately.

## 2026-11-14 · Phase 0 gates

- **The fresh-context review justified itself on day one.** The agent's own 18-case hook test passed, yet the independent reviewer found 20+ bypasses and a false positive in the Bash hook.
- **Idea → options → choice for the Bash hook.**
  - (1) Keep patching regexes. Rejected: whitespace splitting can't model quoting or nesting.
  - (2) Write a real shell parser. Rejected: too heavy for a harness.
  - (3) Use Python's `shlex` tokenizer with `punctuation_chars`, plus a regression table. **Chosen by the agent, pending Alessandro's review at Checkpoint B.**
  - Also documented explicitly: the hook is a best-effort speed bump, and permission rules are the second layer.
- **Permission narrowing (D10), harness self-protection (D11) and CI supply-chain controls (D12)** were raised by both gates. They change what Alessandro has approved to run without prompting, so they're left for his decision rather than applied by the agent.
- **Honesty correction.** The lead agent reported "Semgrep: no findings" from a run that had actually aborted (nonexistent `p/bash` ruleset; `$?` read after a pipe). The security-auditor caught it, and the corrected run is recorded in the phase file.

## 2026-11-14 · Phase 1: affordability rules

- **Re-checking B4 with real decimals.** All three B4 worked examples match `System.Decimal` to the cent. The check also showed that B4's third check, "Loan ≤ MaxBorrowing", fails exactly when DTI or the 4.5× income multiple fails. The explanation would give the same reason twice.
- **Idea → options → choice for the checks (D2).**
  - (1) Keep B4.
  - (2) Three independent checks: LTV, DTI and loan-to-income. **Chosen.** MaxBorrowing stays as an output.
  - (3) Two checks.
  - Chosen by the agent under Alessandro's "proceed automatically" instruction. It gives the same decisions as B4 on every example. A 200,000-sample random run found no approved case with Loan > MaxBorrowing.
- **Rounding (D3).** Compare unrounded values, round outputs once. The alternative, rounding before comparing, would break the invariant above by up to a rounding step. The cost, a value like 45.004% showing as "45.00%" but failing, is handled by an explicit "before rounding" explanation.
- **No rate field (D1).** The spec's five fields have no interest rate, so the payment uses one configurable 6% assessment rate rather than inventing a product rate.
- **Production layers.** Filled in for all 14 layers. Caching and frontend are `N/A` with reasons. The scaling answer for the README is fixed now: SQLite and the in-process queue are the first bottleneck, so the first change is Azure SQL plus a durable queue.
- **Checkpoint B (Alessandro: "Proceed").** All nine agent decisions were approved. MaxBorrowing stays income-based, so B4's own example 1 keeps its 381,535.82, rather than taking the reviewer's LTV-capped alternative. ADR 0001 is Accepted.
