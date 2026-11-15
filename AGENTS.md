# AGENTS.md

Instructions for any coding agent (Claude Code, Codex, Copilot) working in this repo.

## Purpose
ASP.NET Core (.NET 10) JSON API for mortgage applications: create, retrieve, and assess affordability (LTV, DTI, decision, max borrowing, explanation).
Backend only, no UI. Technical test for Canada Life; the human engineer of record is Alessandro.
Source of truth for requirements: `docs/spec/ORIGINAL_SPEC.md`. Affordability rules: `docs/spec/affordability-rules.md`.

## Commands
```bash
./scripts/verify.sh            # full gate: restore, build (warnings as errors), all tests, format check
./scripts/verify.sh --fast     # build + unit tests only
dotnet run --project src/MortgageApplications.Api
dotnet test --solution MortgageApplications.slnx
dotnet test --project tests/MortgageApplications.UnitTests -- --filter-class "*CalculatorTests"
dotnet format MortgageApplications.slnx
```
Tests run on Microsoft.Testing.Platform (set in `global.json`). xUnit v3 filters are `--filter-class` / `--filter-method`.

## Architecture boundaries
- `src/MortgageApplications.Core`: domain entities, `AffordabilityCalculator`, use cases. **No ASP.NET Core or EF Core references.**
- `src/MortgageApplications.Infrastructure`: EF Core (SQLite) persistence. References Core only.
- `src/MortgageApplications.Api`: HTTP (Minimal APIs), auth, hosting, background worker. Request/response DTOs live here and are never entities.
- `tests/MortgageApplications.UnitTests` (Core) and `tests/MortgageApplications.IntegrationTests` (`WebApplicationFactory`).

## Agents and models
Who does what, with which model and why: `docs/ai/context/AGENT-REGISTRY.md`. Subagents do not see the lead's conversation; the lead's prompt carries the task, the files to read and the limits.

## Coding standards (only where they differ from .NET defaults)
- Money and percentages are `decimal`. Never `double`. Round only at the end: 2 dp, `MidpointRounding.AwayFromZero`.
- Minimal APIs with `MapGroup` + `TypedResults`. Public request/response records.
- Input validation (400 `ValidationProblemDetails`) is separate from the affordability decision. A Declined result is valid output, not an error.
- **Never log income, expenses, deposit, property value or any personal data.**
- Package versions live only in `Directory.Packages.props`. Adding a package needs Alessandro's approval.
- Small classes, one responsibility, no speculative abstractions.

## Git etiquette
- One phase = one branch (`phase-NN-<name>`) = one PR, squash-merged.
- Conventional commits with a scope; format in "Commits and PRs" below.
- Never force-push, never `--no-verify`, never commit secrets.

## Commits and PRs: easy to read, still technical
- Subject: `type(scope): what changed`, imperative, at most 72 characters. Types: `feat`, `fix`, `test`, `build`, `docs`, `chore`, `refactor`, `ci`. Scopes: `core`, `api`, `infra`, `tests`, `docs`, `harness`, `ci`. Example: `feat(core): add calculator with loan-to-value and debt-to-income checks`.
- Body: one line on **why**, then at most 5 bullets on **what changed**. One idea per bullet. Wrap at 72 columns, continuing a bullet with a two-space indent.
- Name real things in backticks (`AffordabilityCalculator`, `POST /api/applications`). Give numbers with units (`45%`, `2 dp`, `1,000 GBP`).
- No bare internal IDs. Write "reviewer: max borrowing ignored the property (F5)", not "fix F5". Spell out an acronym the first time it appears in a message (loan-to-value (LTV)).
- One `Co-Authored-By` trailer, at the end.
- PR descriptions follow `.github/pull_request_template.md`.
- Squash-merge with a written message, not GitHub's list of every branch commit: `gh pr merge <n> --squash --subject '<PR title> (#<n>)' --body-file <file>` (single quotes, so the shell never expands the title), where the file holds the PR's "What changed" bullets. Write that file in the session scratchpad, never in the repo.

## Definition of done
1. Tests written first where there is logic; `./scripts/verify.sh` passes and its output is shown.
2. Security gate (Gitleaks, Semgrep, security review) and review gate (fresh-context reviewer, spec check) are run; findings are fixed or justified.
3. Phase file in `docs/progress/` is complete, and `docs/PROGRESS.md` is updated.

## Honesty rule
Never invent prompts, findings, test results, timings or "AI mistakes caught". If something was not done, write that it was not done.
