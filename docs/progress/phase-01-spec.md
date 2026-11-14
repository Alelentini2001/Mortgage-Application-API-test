# Phase 01: Spec + production layers

Status: **In progress** · Branch: `phase-01-spec` · PR: — · Started: 2026-11-14 · Finished: —

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
| D1 | Which interest rate drives the payment? The spec has no rate input. | (a) One fixed **assessment rate of 6%** from `AffordabilityOptions`. (b) Add an interest-rate field to the application: the spec lists exactly five fields. (c) Product rate + 3 pp stress buffer: needs a product rate the spec doesn't give. | **(a)**. Simple, explainable, configurable. | agent (auto-proceed) |
| D2 | Which checks decide the result? B4 has LTV ≤ 90%, DTI ≤ 45% and Loan ≤ MaxBorrowing. | (a) B4 as written. But MaxBorrowing = min(4.5 × income, the loan at which DTI is exactly 45%), so the third check repeats the DTI check: E3 and E5 fail it for the same reason twice. (b) **Three independent checks: LTV ≤ 90%, DTI ≤ 45%, loan-to-income (LTI) ≤ 4.5×.** MaxBorrowing is still reported, but it isn't a separate check. (c) Two checks: LTV and Loan ≤ MaxBorrowing; this hides the DTI reason. | **(b)**. Each failure has exactly one reason in the explanation. Same outcomes as B4 for all 9 worked examples. Invariant: Approved ⇒ Loan ≤ MaxBorrowing (0 violations in 200,000 random samples). | agent (auto-proceed) |
| D3 | Do the checks compare rounded or unrounded values? | (a) **Compare full-precision `decimal` values; round only the outputs.** (b) Round each metric to 2 dp, then compare. | **(a)**. It follows the "round once at the end" rule and keeps the D2 invariant exact. Cost: at a limit a value like 45.004% shows as "45.00%" but fails; the explanation shows enough to explain it (see the rules, "Rounding"). | agent (auto-proceed) |
| D4 | What does "maximum borrowing" cover? | (a) **Income-based only**: min(LTI cap, DTI cap), floored at 0. (b) Also cap at 90% of the property value. | **(a)**. It answers "how much can this applicant borrow", independent of the property. The LTV check covers the property side. | agent (auto-proceed) |
| D5 | Threshold values | (a) **B4's values: LTV 90%, DTI 45%, LTI 4.5×, rate 6%**. (b) Stricter (85% / 40% / 4.0×). | **(a)**. Common UK-style limits; the spec says realism is not assessed. | agent (auto-proceed) |
| D6 | How is the rule set versioned? | (a) **A `RulesVersion` string in `AffordabilityOptions`, stamped on every assessment; bumped by hand whenever a threshold changes.** (b) A hash of the thresholds. | **(a)**. Readable, and old assessments stay explainable. | agent (auto-proceed) |
| D7 | Explanation format | (a) **A structured list of checks (name, value, limit, passed) plus one summary sentence.** (b) One free-text string. | **(a)**. Testable, and API clients can render it. The summary sentence meets "a short explanation". | agent (auto-proceed) |
| D8 | Input limits the calculator can assume (enforced by validation in Phase 3) | (a) **Income 1–10,000,000; expenses 0–1,000,000 per month; property value 1–100,000,000; deposit 0 to below property value; term 5–40 whole years; money ≤ 2 dp.** (b) Validate only "positive" and deposit < property. | **(a)**. Clear 400s instead of absurd results, and well inside `decimal` range. A deposit of 0 is allowed (LTV 100% → Declined, a valid result). | agent (auto-proceed) |

## Docs verified (links)

- `Math.Round(decimal d, int decimals, MidpointRounding mode)`: https://learn.microsoft.com/en-us/dotnet/api/system.math.round?view=net-10.0
  - Exists in .NET 10. The page warns that the default overloads use banker's rounding (`ToEven`), so the mode must be passed explicitly.
- `MidpointRounding.AwayFromZero`: https://learn.microsoft.com/en-us/dotnet/api/system.midpointrounding?view=net-10.0 (linked from the `Math.Round` page)
- `System.Decimal`: https://learn.microsoft.com/en-us/dotnet/api/system.decimal?view=net-10.0
  - Range ±79,228,162,514,264,337,593,543,950,335.
  - There is no `Decimal.Pow` on the page, so `(1 + r)^n` uses exponentiation by squaring on `decimal`.

## Orchestration used

_Filled in as the phase runs._

## Prompts

_Filled in as the phase runs._ See `docs/ai/PROMPTS.md`, Phase 01.

## What was built

_Filled in as the phase runs._

## Tests

No code tests this phase. The worked examples were computed with `System.Decimal` (see "Worked-example check" below); they become the golden tests in Phase 2.

## Worked-example check

Throwaway .NET 10 file-based app (`dotnet run calc2.cs`, session scratchpad, not committed), using the formulas in `docs/spec/affordability-rules.md`:

```
E1 | 85000/900/60000/300000/30y | loan 240000 LTV 80.0 pass | pay 1438.92 | DTI 33.02 pass | LTI 2.82 pass | max 381535.82 | Approved
E2 | 85000/900/20000/300000/30y | loan 280000 LTV 93.33 FAIL | pay 1678.74 | DTI 36.41 pass | LTI 3.29 pass | max 381535.82 | Declined
E3 | 45000/1500/60000/300000/25y | loan 240000 LTV 80.0 pass | pay 1546.32 | DTI 81.24 FAIL | LTI 5.33 FAIL | max 29101.29 | Declined
E4 | 60000/0/30000/200000/25y | loan 170000 LTV 85.00 pass | pay 1095.31 | DTI 21.91 pass | LTI 2.83 pass | max 270000.0 | Approved
E5 | 30000/1200/50000/200000/25y | loan 150000 LTV 75.00 pass | pay 966.45 | DTI 86.66 FAIL | LTI 5 FAIL | max 0 | Declined
E6 | 50000/0/60000/300000/35y | loan 240000 LTV 80.0 pass | pay 1368.46 | DTI 32.84 pass | LTI 4.8 FAIL | max 225000.0 | Declined
E7 | 70000/500/30000/300000/30y | loan 270000 LTV 90.0 pass | pay 1618.79 | DTI 36.32 pass | LTI 3.86 pass | max 315000.0 | Approved
E8 | 60000/500/30000/300000/30y | loan 270000 LTV 90.0 pass | pay 1618.79 | DTI 42.38 pass | LTI 4.5 pass | max 270000.0 | Approved
E9 | 80000/1000/0/200000/30y | loan 200000 LTV 100 FAIL | pay 1199.10 | DTI 32.99 pass | LTI 2.5 pass | max 333583.23 | Declined
random: 200000 samples, approved 99156, approved with loan > max: 0
```

- E1–E3 match every value B4 states (payment 1,438.92, DTI 33.02% / 81.24%, max 381,535.82 / 29,101.29, LTV 93.33%).
- An earlier run with B4's own three checks gave the same decisions for E1–E6. It also showed that E3 and E5 fail B4's "Loan ≤ MaxBorrowing" check for the same reason as the DTI check (the basis for D2).
- Values like `80.0` and `270000.0` are `decimal` scale artefacts in the raw print. The API formats every output to 2 dp.

## Security gate

_Filled in as the phase runs._

## Review gate

_Filled in as the phase runs._

## AI output corrected or rejected

_Filled in as the phase runs. Real cases only._

## Optimisations considered

None. A docs-only phase.

## Open issues

_Filled in as the phase runs._

## Time spent (approx.)

_Filled in at the end._

## Human review (Alessandro writes this; the agent leaves it empty)
