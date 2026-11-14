# Phase 01: Spec + production layers

Status: **Merged** (squash `d1c096a`) · Branch: `phase-01-spec` · PR: [#3](https://github.com/Alelentini2001/Mortgage-Application-API-test/pull/3) · Started: 2026-11-14 · Finished: 2026-11-14

## Goal

Write the affordability rules every later phase codes against (`docs/spec/affordability-rules.md`), fill in the production-layers checklist for this API (`docs/layers/PRODUCTION-LAYERS.md`), have the `dotnet-reviewer` critique the rules before any code exists, and map `docs/spec/ORIGINAL_SPEC.md` into the traceability section of `docs/PROGRESS.md` with the `spec-checker`. No production code changes in this phase.

## How this phase was run

Alessandro started it with `/phase 01 spec` plus "Also proceed automatically, completing all, testing, analyzing, etc..." (P01-01). This overrides the Checkpoint A stop in `CLAUDE.md` for this phase. So:
- Every decision below takes the recommended option and is marked **decided by: agent (auto-proceed)**. They are all open to change at Checkpoint B.
- The agent still stops before the squash-merge (Checkpoint B), because these are business rules that land on `main`.

## Plan (Checkpoint A, auto-approved under P01-01)

1. Re-check the B4 worked examples with real `System.Decimal` arithmetic (throwaway .NET 10 file-based app in the session scratchpad, not in the repo).
2. Write `docs/spec/affordability-rules.md` from B4 with the decisions below applied: inputs, preconditions, formulas, checks, rounding, outputs, explanation format, assumptions, worked examples.
3. Write ADR 0001 for the assessment model.
4. Fill in `docs/layers/PRODUCTION-LAYERS.md` (all 14 layers, `N/A + reason` where they don't apply).
5. `dotnet-reviewer` critiques the rules (fresh context). Apply real findings, record each one.
6. `spec-checker` maps `ORIGINAL_SPEC.md` → files/phases. The result goes into `docs/PROGRESS.md` "Requirement traceability".
7. Gates: `./scripts/verify.sh` full, `security-auditor`, evidence, PR, local `/code-review` + `/security-review`, CI. Stop at Checkpoint B.

Not in scope: the test-first step (no code this phase; the `test-designer` runs in Phase 2 against these rules) and input validation code (Phase 3).

## Decisions (option chosen · alternatives · reason · decided by) → [ADR 0001](../adr/0001-affordability-assessment-model.md)

| # | Question | Options | Chosen | Decided by |
|---|---|---|---|---|
| D1 | Which interest rate drives the payment? The spec has no rate input. | (a) One fixed **assessment rate of 6%** from `AffordabilityOptions`. (b) Add an interest-rate field to the application: the spec lists exactly five fields. (c) Product rate + 3 pp stress buffer: needs a product rate the spec doesn't give. | **(a)**. Simple, explainable, configurable. | agent (auto-proceed); approved by Alessandro at Checkpoint B |
| D2 | Which checks decide the result? B4 has LTV ≤ 90%, DTI ≤ 45% and Loan ≤ MaxBorrowing. | (a) B4 as written. But MaxBorrowing = min(4.5 × income, the loan at which DTI is exactly 45%), so the third check repeats the DTI check: E3 and E5 fail it for the same reason twice. (b) **Three independent checks: LTV ≤ 90%, DTI ≤ 45%, loan-to-income (LTI) ≤ 4.5×.** MaxBorrowing is still reported, but it isn't a separate check. (c) Two checks: LTV and Loan ≤ MaxBorrowing; this hides the DTI reason. | **(b)**. Each failure has exactly one reason in the explanation. Same decisions as B4 on all 18 worked examples. Invariant: Approved ⇒ Loan ≤ MaxBorrowing (0 violations in 200,000 random samples). | agent (auto-proceed); approved by Alessandro at Checkpoint B |
| D3 | Do the checks compare rounded or unrounded values? | (a) **Compare full-precision `decimal` values; round only the outputs.** (b) Round each metric to 2 dp, then compare. | **(a)**. It follows the "round once at the end" rule and keeps the D2 invariant exact. Cost: at a limit a value like 45.000128% shows as "45.00%" but fails. The summary then says "before rounding" (E11, E13). | agent (auto-proceed); approved by Alessandro at Checkpoint B |
| D4 | What does "maximum borrowing" cover? | (a) **Income-based only**: min(LTI cap, DTI cap), floored at 0. (b) Also cap at 90% of the property value (`dotnet-reviewer` F5). | **(a), now labelled "income-based; ignores property value and deposit"**. (b) is a reasonable alternative, but it changes B4's own E1 (max 381,535.82 → 270,000.00). **Open for Alessandro.** | agent (auto-proceed); approved by Alessandro at Checkpoint B |
| D5 | Threshold values | (a) **B4's values: LTV 90%, DTI 45%, LTI 4.5×, rate 6%**. (b) Stricter (85% / 40% / 4.0×). | **(a)**. Common UK-style limits; the spec says realism is not assessed. | agent (auto-proceed); approved by Alessandro at Checkpoint B |
| D6 | How is the rule set versioned? | (a) **A `RulesVersion` string in `AffordabilityOptions`, stamped on every assessment; bumped by hand whenever an option changes.** (b) A hash of the thresholds. | **(a)**, plus the rate and limits stored with each assessment (F7), so a forgotten bump doesn't make old results unexplainable. | agent (auto-proceed); approved by Alessandro at Checkpoint B |
| D7 | Explanation format | (a) **A structured list of checks (name, value, limit, passed) plus one summary sentence with exact templates.** (b) One free-text string. | **(a)**. Testable, and API clients can render it. The summary sentence meets "a short explanation". | agent (auto-proceed); approved by Alessandro at Checkpoint B |
| D8 | Input limits the calculator can assume (enforced by validation in Phase 3) | (a) **Income 1–10,000,000; expenses 0–1,000,000 per month; property value 1,000–100,000,000 (raised from 1 when D9 was added; `security-auditor` S11 noticed an early draft of this row still said 1); term 5–40 whole years; money ≤ 2 dp.** (b) Validate only "positive" and deposit < property. | **(a)**. Clear 400s instead of absurd results, and well inside `decimal` range. These are our assumptions, not the spec's; the rules doc says so. | agent (auto-proceed); approved by Alessandro at Checkpoint B |
| D9 | Minimum loan (`dotnet-reviewer` F11) | (a) **Loan ≥ 1,000, so deposit ≤ property − 1,000 (validation → 400).** (b) Allow any loan > 0 and list tiny loans as an assumption. | **(a)**. Without it, a 0.01 loan is "Approved" with DTI 0.00% and LTI 0.00×, which reads as nonsense. | agent (auto-proceed); approved by Alessandro at Checkpoint B |

## Checkpoint B outcome (2026-11-14)

Alessandro: "Proceed" (P01-06).
- D1–D9 were approved as recommended. D4 stays income-based: the LTV cap (`dotnet-reviewer` F5) was not adopted.
- ADR 0001 is Accepted. The rules doc status is accepted.
- No answer was given on `security-auditor` S10 or the `spec-checker` questions, so they stay open (see "Open issues").
- CI "Build, test, format" passed on the PR head `ef22157`. The PR was squash-merged as `d1c096a`.

## Docs verified (links)

- `Math.Round(decimal d, int decimals, MidpointRounding mode)`: https://learn.microsoft.com/en-us/dotnet/api/system.math.round?view=net-10.0
  - Exists in .NET 10. The page warns that the default overloads use banker's rounding (`ToEven`), so the mode must be passed explicitly.
- `MidpointRounding.AwayFromZero`: https://learn.microsoft.com/en-us/dotnet/api/system.midpointrounding?view=net-10.0 (linked from the `Math.Round` page)
- `System.Decimal`: https://learn.microsoft.com/en-us/dotnet/api/system.decimal?view=net-10.0
  - Range ±79,228,162,514,264,337,593,543,950,335.
  - There is no `Decimal.Pow` on the page, so `(1 + r)^n` uses exponentiation by squaring on `decimal`.
- Behaviour measured, not taken from the docs (`calc3.cs` below):
  - `Math.Round(85.005m, 2)` gives `85.00` (banker's rounding).
  - `AwayFromZero` gives `85.01`.
  - `+ 0.00m` after rounding gives scale 2 (`80.0m` → `80.00`).

## Orchestration used

- **Lead agent:**
  - Explored the spec, B4 and the docs.
  - Ran three throwaway `System.Decimal` scripts (`calc.cs`, `calc2.cs`, `calc3.cs`) as .NET 10 file-based apps in the session scratchpad, so nothing extra lands in the repo.
  - Wrote the rules, the ADR, the layers checklist and the evidence.
  - Checked the Learn pages with WebFetch.
- **Subagents** (the real `.claude/agents/` definitions, registered in this session; `dotnet-reviewer` and `spec-checker` ran in parallel, both read-only):
  - `dotnet-reviewer` (P01-02): critiqued the rules before any code. It found **12 findings, no maths errors**, and recomputed E1–E9 independently in Python `decimal`.
  - `spec-checker` (P01-03): split the spec into 55 requirements and mapped them to files and phases.
  - `security-auditor` (P01-04): security gate.
- **Skills:**
  - `/phase` (started by Alessandro).
  - `affordability-rules` was updated: it loads automatically when Core or unit-test files are touched in Phase 2.
- **Hooks that fired:**
  - The Gitleaks pre-commit hook fired on every commit ("no leaks found").
  - `verify-on-stop.sh`: its outcome wasn't observed in this session. By design it skips the build when only docs change, and it is still proven by script tests only (Phase 0).
- **MCP servers:** none.

## Prompts

`docs/ai/PROMPTS.md` entries P01-01 to P01-06.

## What was built

- `docs/spec/affordability-rules.md`: rules v1.0, covering inputs and preconditions, formulas, checks, rounding and formatting, outputs, exact explanation templates, assumptions, 18 worked examples with exact summaries, and the changes from B4.
- `docs/adr/0001-affordability-assessment-model.md`: the first ADR (status: Proposed).
- `docs/layers/PRODUCTION-LAYERS.md`: all 14 layers filled in.
- `docs/PROGRESS.md`: "Requirement traceability", 55 requirements.
- `.claude/skills/affordability-rules/SKILL.md`: non-negotiables updated (scale 2, invariant culture, a pure calculator, never log `Summary`/`Checks`).
- `docs/ai/PROMPTS.md`, `docs/ai/DESIGN-JOURNAL.md`: Phase 1 entries.

## Tests

- No code tests this phase. The 18 worked examples are the golden tests for Phase 2.
- `./scripts/verify.sh` (full), 2026-11-14, on `phase-01-spec` after commit `317c612`:
  ```
  ==> hook regression tests
  hook tests: 91 passed, 0 failed

  ==> format check

  VERIFY PASSED (full)
  ```
  The earlier part of the same run reported `total: 2, failed: 0, succeeded: 2` (the placeholder smoke tests).

## Worked-example check

Final run: `dotnet run calc3.cs` in the session scratchpad, not committed. The current culture was set to `it-IT` to prove the output doesn't depend on the machine culture. The numbers in the raw prints use a comma because of that.
- "B4" is the decision under B4's original three checks.
- The script's labels E11b to E17 are the rules doc's E12 to E18.

```
scale check: R2(80.0m)=80,00 R2(240000m)=240000,00 R2(5m)=5,00 R2(85.005m)=85,01 ToEven(85.005m)=85,00
E1 | 85000/900/60000/300000/30y | loan 240,000.00 | LTV 80.00 | pay 1,438.92 | DTI 33.02 (raw 33,020065) | LTI 2.82 | max 381,535.82 | Approved | B4 Approved | Approved: all 3 checks passed (LTV 80.00% ≤ 90.00%, DTI 33.02% ≤ 45.00%, LTI 2.82× ≤ 4.50×).
E2 | 85000/900/20000/300000/30y | loan 280,000.00 | LTV 93.33 | pay 1,678.74 | DTI 36.41 (raw 36,405762) | LTI 3.29 | max 381,535.82 | Declined | B4 Declined | Declined: LTV 93.33% is above the 90.00% limit.
E3 | 45000/1500/60000/300000/25y | loan 240,000.00 | LTV 80.00 | pay 1,546.32 | DTI 81.24 (raw 81,235290) | LTI 5.33 | max 29,101.29 | Declined | B4 Declined | Declined: DTI 81.24% is above the 45.00% limit; LTI 5.33× is above the 4.50× limit.
E4 | 60000/0/30000/200000/25y | loan 170,000.00 | LTV 85.00 | pay 1,095.31 | DTI 21.91 (raw 21,906248) | LTI 2.83 | max 270,000.00 | Approved | B4 Approved | Approved: all 3 checks passed (LTV 85.00% ≤ 90.00%, DTI 21.91% ≤ 45.00%, LTI 2.83× ≤ 4.50×).
E5 | 30000/1200/50000/200000/25y | loan 150,000.00 | LTV 75.00 | pay 966.45 | DTI 86.66 (raw 86,658084) | LTI 5.00 | max 0.00 | Declined | B4 Declined | Declined: DTI 86.66% is above the 45.00% limit; LTI 5.00× is above the 4.50× limit.
E6 | 50000/0/60000/300000/35y | loan 240,000.00 | LTV 80.00 | pay 1,368.46 | DTI 32.84 (raw 32,842927) | LTI 4.80 | max 225,000.00 | Declined | B4 Declined | Declined: LTI 4.80× is above the 4.50× limit.
E7 | 70000/500/30000/300000/30y | loan 270,000.00 | LTV 90.00 | pay 1,618.79 | DTI 36.32 (raw 36,322053) | LTI 3.86 | max 315,000.00 | Approved | B4 Approved | Approved: all 3 checks passed (LTV 90.00% ≤ 90.00%, DTI 36.32% ≤ 45.00%, LTI 3.86× ≤ 4.50×).
E8 | 60000/500/30000/300000/30y | loan 270,000.00 | LTV 90.00 | pay 1,618.79 | DTI 42.38 (raw 42,375728) | LTI 4.50 | max 270,000.00 | Approved | B4 Approved | Approved: all 3 checks passed (LTV 90.00% ≤ 90.00%, DTI 42.38% ≤ 45.00%, LTI 4.50× ≤ 4.50×).
E9 | 80000/1000/0/200000/30y | loan 200,000.00 | LTV 100.00 | pay 1,199.10 | DTI 32.99 (raw 32,986516) | LTI 2.50 | max 333,583.23 | Declined | B4 Declined | Declined: LTV 100.00% is above the 90.00% limit.
E10 | 60000/0/29990/200000/25y | loan 170,010.00 | LTV 85.01 | pay 1,095.38 | DTI 21.91 (raw 21,907536) | LTI 2.83 | max 270,000.00 | Approved | B4 Approved | Approved: all 3 checks passed (LTV 85.01% ≤ 90.00%, DTI 21.91% ≤ 45.00%, LTI 2.83× ≤ 4.50×).
E11 | 60000/631,22/30000/300000/30y | loan 270,000.00 | LTV 90.00 | pay 1,618.79 | DTI 45.00 (raw 45,000128) | LTI 4.50 | max 269,998.93 | Declined | B4 Declined | Declined: DTI 45.00% is above the 45.00% limit before rounding.
E11b | 60000/631,21/30000/300000/30y | loan 270,000.00 | LTV 90.00 | pay 1,618.79 | DTI 45.00 (raw 44,999928) | LTI 4.50 | max 270,000.00 | Approved | B4 Approved | Approved: all 3 checks passed (LTV 90.00% ≤ 90.00%, DTI 45.00% ≤ 45.00%, LTI 4.50× ≤ 4.50×).
E12 | 60000/500/29988/300000/30y | loan 270,012.00 | LTV 90.00 | pay 1,618.86 | DTI 42.38 (raw 42,377167) | LTI 4.50 | max 270,000.00 | Declined | B4 Declined | Declined: LTV 90.00% is above the 90.00% limit before rounding; LTI 4.50× is above the 4.50× limit before rounding.
E13 | 60000/1625/30000/300000/30y | loan 270,000.00 | LTV 90.00 | pay 1,618.79 | DTI 64.88 (raw 64,875728) | LTI 4.50 | max 104,244.76 | Declined | B4 Declined | Declined: DTI 64.88% is above the 45.00% limit.
E14 | 60000/300/50000/200000/5y | loan 150,000.00 | LTV 75.00 | pay 2,899.92 | DTI 64.00 (raw 63,998405) | LTI 2.50 | max 100,864.84 | Declined | B4 Declined | Declined: DTI 64.00% is above the 45.00% limit.
E15 | 40000/200/20000/180000/40y | loan 160,000.00 | LTV 88.89 | pay 880.34 | DTI 32.41 (raw 32,410255) | LTI 4.00 | max 180,000.00 | Approved | B4 Approved | Approved: all 3 checks passed (LTV 88.89% ≤ 90.00%, DTI 32.41% ≤ 45.00%, LTI 4.00× ≤ 4.50×).
E16 | 10000000/1000000/0/100000000/40y | loan 100,000,000.00 | LTV 100.00 | pay 550,213.64 | DTI 186.03 (raw 186,025637) | LTI 10.00 | max 0.00 | Declined | B4 Declined | Declined: LTV 100.00% is above the 90.00% limit; DTI 186.03% is above the 45.00% limit; LTI 10.00× is above the 4.50× limit.
E17 | 1/1000000/0/100000000/5y | loan 100,000,000.00 | LTV 100.00 | pay 1,933,280.15 | DTI 3519936183.53 (raw 3519936183,531350) | LTI 100000000.00 | max 0.00 | Declined | B4 Declined | Declined: LTV 100.00% is above the 90.00% limit; DTI 3519936183.53% is above the 45.00% limit; LTI 100000000.00× is above the 4.50× limit.
```

Earlier runs (same scratchpad):
- `calc.cs` checked B4's own three checks on E1–E6.
- `calc2.cs` checked the D2 checks on E1–E9 and ran a 200,000-sample random test: `random: 200000 samples, approved 99156, approved with loan > max: 0`.

## Security gate

**`security-auditor` (P01-04): PASS.** It reported exit codes read directly and result counts parsed from JSON.

| Tool | Command | Result |
|---|---|---|
| Gitleaks, history | `gitleaks git --no-banner --redact -v` | 24 commits scanned, no leaks found, exit 0 |
| Gitleaks, working tree | `gitleaks dir --no-banner --redact .` | no leaks found, exit 0 |
| Semgrep, full | `semgrep scan --config p/csharp --config p/secrets --metrics=off --error --quiet --json` | exit 0; 0 results, 0 errors, 59 files |
| Semgrep, diff | same + `--baseline-commit main` | exit 0; 0 results, 0 errors, 8 files |
| NuGet vulnerabilities | `dotnet list MortgageApplications.slnx package --vulnerable --include-transitive` | no vulnerable packages (5 projects) |

The scanners can only prove "no secrets" for a Markdown diff. The findings come from the auditor's design-stage review against OWASP Top 10:2025:

| ID | Sev | Finding | Resolution |
|---|---|---|---|
| S1 | medium | Phase 3 endpoints would exist without auth or owner scoping until Phase 7; the source of `OwnerId` wasn't stated | **Fixed in the design.** Owner-scoped queries from Phase 3 via `ICurrentUser` (fixed dev owner until Phase 7). `OwnerId` never comes from the body. `FallbackPolicy = RequireAuthenticatedUser` in Phase 7. **Accepted exception:** pre-Phase-7 builds aren't deployed or submitted |
| S2 | medium | The dev token endpoint was gated only by the environment name, and the same image is promoted to production | **Fixed in the design.** Registered only for `Development`/`Docker` **and** `DevAuth:Enabled=true`. Startup throws if it's enabled in `Production`. No default key; key ≥ 256 bits. Test: 404 under `Production` |
| S3 | medium | Logging rules were inconsistent: the denylist missed derived values and span attributes | **Fixed.** One allowlist in rules §6 and in the Observability row, covering logs, spans and metric tags. No body logging, no `EnableSensitiveDataLogging`, no submitted values in messages. The Phase 5 test checks raw and derived numbers |
| S4 | low | The skill said only "never log Summary/Checks" | **Fixed.** The skill now states the allowlist |
| S5 | low | OpenAPI/Scalar exposure and CORS weren't specified | **Fixed.** Development only; no CORS policy |
| S6 | low | No per-user limit; a bounded channel could silently drop work | **Fixed.** `FullMode = Wait` + timeout → 503, never drop. No rate limiting = accepted risk recorded in Phase 8 |
| S7 | low | JWT hardening details missing (algorithms, skew, signed tokens; Entra `sub` is pairwise) | **Fixed.** HS256 pinned, `RequireSignedTokens`, small skew; production owner = `oid`+`tid` |
| S8 | low | Division-by-zero safety relied only on API validation; the worker is a second path in | **Fixed.** Calculator guard clauses (`ArgumentOutOfRangeException`), rules §3 |
| S9 | info | Retention/deletion/encryption at rest are production-only | Accepted; already disclosed |
| S10 | info | Verbatim prompts include the local path `/Users/ale_ssd/...` (OS username visible once public) | **For Alessandro:** accept, or redact to `<repo>` before the repo goes public (Phase 11) |
| S11 | info | The phase file's D8 said property value from 1; rules §1 says 1,000 | **Fixed** (D8 row) |

## Review gate

**`dotnet-reviewer` (P01-02), before any code.** It found no maths errors, and its Python `decimal` recomputation (28 and 50 digits) matches E1–E9 at 2 dp. 12 findings: 0 high, 6 medium, 6 low.

| ID | Sev | Finding | Resolution |
|---|---|---|---|
| F1 | medium | No example lands on a rounding midpoint, so banker's rounding would pass every golden test | **Fixed.** E10 (raw LTV 85.005 → 85.01) |
| F2 | medium | The "before rounding" wording had no trigger rule and no example | **Fixed.** Rule: failed **and** rounded value = rounded limit, for any check. E11, E13 with exact strings |
| F3 | medium | Summary templates incomplete; culture not fixed (`81,24%` under it-IT) | **Fixed.** Literal templates for all checks, `InvariantCulture` + `"0.00"`; checked under it-IT |
| F4 | medium | Rounding keeps the scale (`80.0`, `240000`), so JSON wouldn't show 2 dp | **Fixed.** `+ 0.00m` after rounding → scale 2 (measured) |
| F5 | medium | MaxBorrowing ignores the property; E2 is declined on LTV but shows 381,535.82 | **Partly adopted.** Labelled "income-based; ignores property value and deposit". The LTV cap (option b) is left to Alessandro, because it changes B4's E1 (D4) |
| F6 | medium | Golden gaps: no DTI-only case, terms 5/40 unused, no limit inputs | **Fixed.** E14 (DTI only), E15 (5y), E16 (40y), E17 (max inputs), E18 (extreme ratios) |
| F7 | low | The assessment rate isn't an output, so results can't be recomputed | **Fixed.** `AssessmentRate` is stored and returned |
| F8 | low | `AssessedAt` would make a "pure" calculator read the clock | **Fixed.** The use case stamps it via `TimeProvider`; the calculator is pure |
| F9 | low | `AssessmentRate > 0` allows an underflowing rate → `DivideByZeroException` | **Fixed.** Rate 0.001–0.25 (≤ 4 dp); thresholds ≤ 2 dp |
| F10 | low | "Keeps money out of logs" could be read as "Summary is safe to log" | **Fixed.** Never log `Summary` or `Checks`; log only IDs, decision, version |
| F11 | low | A 0.01 loan is Approved with DTI/LTI 0.00 | **Fixed.** Minimum loan of 1,000 (D9) |
| F12 | low | "Same decisions as B4 on all 9 examples" rested on an E1–E6 run | **Fixed.** `calc3.cs` prints the B4 decision for all 18 examples: all match |

**`spec-checker` (P01-03).**
- 55 atomic requirements (R1–R55), written into `docs/PROGRESS.md`: 0 met, 27 partial, 28 missing. That's expected at Phase 1: "met" needs code or a test.
- Requirement 3 coverage: the rules define every output the spec asks for (LTV, DTI, decision, max borrowing, short explanation) plus the assumptions.
- Its points acted on in this phase:
  - The input limits are labelled as our assumptions.
  - Currency isn't in the spec, so the rules say the maths is currency-agnostic and the docs assume GBP.
- Its points left for Alessandro: see "Open issues".

**PR AI reviews on PR #3** (local, Alessandro's subscription):
- **`/code-review` (high, inline): 8 findings, all fixed.**
  - §8 overclaimed that the reviewer recomputed all 18 examples (it did E1–E14).
  - An invented "14:10" end time in "Time spent".
  - A traceability evidence pointer that led nowhere.
  - Two logging allowlists that disagreed (status codes and timings).
  - A stale prompt range.
  - No scale rule for `Checks.Limit`.
  - A stale "D1–D8".
  - An unobserved claim about `verify-on-stop.sh`.
- **`/security-review` (finder sub-task P01-05): no findings.**
  - Its below-threshold note was applied: guard-clause exceptions must not carry the value (`ThrowIf*` helpers embed it). See rules §3 and the skill.

## AI output corrected or rejected

Real cases only:
1. **The lead agent wrote traceability totals without counting.** The first draft of `docs/PROGRESS.md` said "30 partial · 25 missing". A count from the table gave 27 and 28. Fixed before commit, and the line now says "(counted from the table)".
2. **The lead agent's own spec had testability gaps, found by the fresh-context reviewer.** The first rules draft had no rounding-midpoint example. That means an implementation using banker's rounding, the exact mistake the draft warned about, would have passed every golden test (F1). The draft also had no exact summary templates and no culture rule (F3), and no scale rule for JSON (F4).
3. **An overstated claim.** The first draft of D2 said "same outcomes as B4 for all 9 worked examples", but the recorded run covered only E1–E6 (F12). Now backed by a run over all 18.
4. **More overclaims and an invented number in the lead agent's evidence, caught by the local `/code-review` on PR #3.**
   - The rules said every example was recomputed by the reviewer, but E15–E18 were not.
   - "Time spent" gave an end time ("about 14:10") that no clock reading backed.
   - Both were fixed before Checkpoint B.

## Optimisations considered

None. A docs-only phase.

## Open issues

Resolved at Checkpoint B: D1–D9 approved, D4 kept income-based, ADR 0001 Accepted.

Still open for Alessandro:
- **spec-checker questions:**
  - Keep "partial" for designed-not-built (or split it out)?
  - Do the constraint rows R44 and R50 stay in the map or move to the Phase 11 checklist?
  - Final tag or release for submission (R43)?
  - Do the optional items (R30–R33) all stay in scope?

- **`security-auditor` S10:** keep the local absolute paths in the verbatim prompt logs, or redact them before the repo goes public?

Carried into later phase plans (from the security gate):
- Phase 3: owner-scoped queries via `ICurrentUser`.
- Phase 5: allowlist log test.
- Phase 6: no silent drops.
- Phase 7: secure default policy and a fail-closed dev token endpoint.
- Phase 8: the rate-limit decision or an accepted risk.

Other:
- Branch protection on `main` (from Phase 0).

## Time spent (approx.)

From commit and shell timestamps: branch created 13:45, last evidence commit 13:56 (`ff6814c`), PR #3 opened right after; the review fixes followed (see the PR). The Checkpoint B time is in the PR's last comment. Subagent wall time: `dotnet-reviewer` about 3.3 min, `spec-checker` about 1 min.

## Human review (Alessandro writes this; the agent leaves it empty)
