# Affordability rules

Rules version: **1.0** · Status: proposed in Phase 1 (decisions D1–D9 in `docs/progress/phase-01-spec.md`, [ADR 0001](../adr/0001-affordability-assessment-model.md)) · Source: master prompt B4, changed as recorded in section 9. Reviewed before any code by the `dotnet-reviewer` (findings F1–F12 in the phase file).

The spec says the logic "does not need to be realistic or comprehensive" but must be "simple, reasonable" and "clearly explained". These rules are deliberately simple. Every number in an assessment can be recomputed by hand from this page and the values stored with the assessment.

## 1. Inputs

From the mortgage application (the five fields in the spec). The limits are **our assumptions, not the spec's** (decision D8). They keep the results sensible and well inside `decimal` range.

| Field | Unit | Precondition (enforced by input validation, Phase 3) |
|---|---|---|
| `AnnualIncome` | currency per year, gross | 1 to 10,000,000, at most 2 dp |
| `MonthlyExpenses` | currency per month | 0 to 1,000,000, at most 2 dp |
| `DepositAmount` | currency | 0 or more, at most 2 dp, and **`PropertyValue − DepositAmount ≥ 1,000`** (minimum loan) |
| `PropertyValue` | currency | 1,000 to 100,000,000, at most 2 dp |
| `TermYears` | whole years | 5 to 40 |

Invalid input never reaches the calculator. It's rejected with **400 `ValidationProblemDetails`**. A **Declined** assessment is a valid result, not an error.

From configuration (`AffordabilityOptions`, validated at startup; the app doesn't start with invalid options):

| Option | Value in rules version 1.0 | Startup validation |
|---|---|---|
| `AssessmentRate` (annual) | 0.06 (6%) | 0.001 to 0.25, at most 4 dp |
| `MaxLoanToValuePercent` | 90 | > 0 and ≤ 100, at most 2 dp |
| `MaxDebtToIncomePercent` | 45 | > 0 and ≤ 100, at most 2 dp |
| `MaxLoanToIncomeMultiple` | 4.5 | > 0 and ≤ 10, at most 2 dp |
| `RulesVersion` | "1.0" | required, not blank |

- The thresholds have at most 2 dp, so the `Limit` shown in an explanation always equals the configured value.
- The lower bound on the rate keeps `r` and `Annuity` well away from zero (no `DivideByZeroException`, no loss of precision).
- **Any change to an option must bump `RulesVersion`.** Every assessment also stores the rate and limits it used (section 5), so it stays explainable even if someone forgets the bump.

## 2. Formulas

All arithmetic is `decimal`, never `double`. Values are kept at full precision and computed in the order shown, so tests are reproducible.

```
Loan            = PropertyValue − DepositAmount
LTV %           = Loan / PropertyValue × 100

r               = AssessmentRate / 12                      (monthly rate)
n               = TermYears × 12                           (number of payments)
Growth          = (1 + r)^n                                 exponentiation by squaring on decimal (no Math.Pow)
Annuity         = 1 − 1 / Growth                            = 1 − (1 + r)^−n
MonthlyPayment  = Loan × r / Annuity                        repayment mortgage at the assessment rate

MonthlyIncome   = AnnualIncome / 12
DTI %           = (MonthlyExpenses + MonthlyPayment) / MonthlyIncome × 100
LTI             = Loan / AnnualIncome

LtiCap          = MaxLoanToIncomeMultiple × AnnualIncome
AffordablePay   = MaxDebtToIncomePercent / 100 × MonthlyIncome − MonthlyExpenses
DtiCap          = AffordablePay × Annuity / r               the loan whose payment makes DTI exactly the limit
MaxBorrowing    = max(0, min(LtiCap, DtiCap))
```

Notes:
- `MonthlyPayment` uses the single assessment rate (D1). There's no separate "product rate" because the application has no rate field.
- `MonthlyExpenses` are the applicant's existing monthly commitments. They exclude the new mortgage, so DTI adds the new payment to them.
- **`MaxBorrowing` is income-based** (D4): how much this applicant's income supports, whatever the property. It doesn't apply the LTV limit. When an application is declined on LTV, MaxBorrowing can be above the loan the property allows (E2, E9). The LTV check and its limit are shown next to it, so the explanation stays complete. The API documents the field as "income-based maximum borrowing; ignores property value and deposit".
- If expenses alone are at or above the DTI limit, `AffordablePay ≤ 0`, so `MaxBorrowing = 0` (E5).
- Inside the section 1 bounds there is no division by zero (income ≥ 1, property ≥ 1,000, r ≥ 0.001/12) and no overflow (`Growth` is at most about 2×10⁴).

## 3. Checks and decision

Three independent checks (D2). **Every check is always evaluated** and reported, even after one fails.

| Check | Passes when | Limit (v1.0) |
|---|---|---|
| `LTV` | LTV % ≤ `MaxLoanToValuePercent` | 90% |
| `DTI` | DTI % ≤ `MaxDebtToIncomePercent` | 45% |
| `LTI` | LTI ≤ `MaxLoanToIncomeMultiple` | 4.5× |

**Decision:** `Approved` if all three checks pass, otherwise `Declined`. Limits are inclusive: a value exactly at the limit passes (E8).

**Invariant:** Approved ⇒ Loan ≤ MaxBorrowing. When DTI and LTI both pass, the loan is within both caps that define MaxBorrowing. Phase 2 tests this as a property.

**Purity:** the calculator is a pure function of (application inputs, options). It doesn't read the clock, config or the database. The use case adds `AssessedAt` from an injected `TimeProvider`.

**Guard clauses:** the calculator re-checks the section 1 preconditions and throws `ArgumentOutOfRangeException` if one is broken. Validation at the API is the first layer. The guard covers other paths into the calculator, such as the Phase 6 worker re-processing stored Pending assessments (`security-auditor` S8). The exception handler returns ProblemDetails with a trace ID and no exception details outside Development.

## 4. Rounding and formatting

- Checks compare **full-precision** values (D3). Nothing is rounded before a comparison.
- Outputs are rounded **once**, at the end: `Math.Round(value, 2, MidpointRounding.AwayFromZero)`. The default `Math.Round` uses banker's rounding (`ToEven`), so the mode is always passed explicitly. E10 has a raw LTV of exactly 85.005, so it catches the wrong mode: AwayFromZero gives 85.01, `ToEven` gives 85.00.
- Every rounded output then has **scale 2**: add `0.00m` after rounding (`80.0m` → `80.00`, `240000m` → `240000.00`). JSON then shows every output with 2 dp (`80.00`, not `80.0` or `80`).
- Strings (the summary) use `CultureInfo.InvariantCulture` and the format `"0.00"`. The output is the same on any machine culture (checked under `it-IT`).
- Money, percentages and the LTI multiple are all 2 dp.

## 5. Outputs (an assessment)

| Field | Meaning |
|---|---|
| `LoanAmount` | `Loan`, 2 dp |
| `LoanToValuePercent` | LTV %, 2 dp |
| `MonthlyPayment` | payment at the assessment rate, 2 dp |
| `DebtToIncomePercent` | DTI %, 2 dp |
| `LoanToIncomeMultiple` | LTI, 2 dp |
| `MaxBorrowing` | income-based maximum borrowing (section 2), 2 dp |
| `Decision` | `Approved` or `Declined` |
| `Checks` | one entry per check, in the order LTV, DTI, LTI: `Name`, `Value` (2 dp), `Limit`, `Passed` |
| `Summary` | one sentence (section 6) |
| `AssessmentRate` | the annual rate used, for example 0.06 |
| `RulesVersion` | for example "1.0" |
| `AssessedAt` | UTC timestamp, set by the use case (section 3, "Purity") |

## 6. Explanation

Structured `Checks` plus one `Summary` sentence (D7). Exact templates, with `{v}` and `{limit}` formatted as in section 4:

| Case | Template |
|---|---|
| Approved | `Approved: all 3 checks passed (LTV {v}% ≤ {limit}%, DTI {v}% ≤ {limit}%, LTI {v}× ≤ {limit}×).` |
| Declined | `Declined: ` + the failed checks joined with `; `, in the order LTV, DTI, LTI, + `.` |
| One failed LTV check | `LTV {v}% is above the {limit}% limit` |
| One failed DTI check | `DTI {v}% is above the {limit}% limit` |
| One failed LTI check | `LTI {v}× is above the {limit}× limit` |

- **Rounding edge.** If a check fails **and** its rounded value equals its rounded limit, append ` before rounding` to that check's text. This applies to any of the three checks (E11, E13). A passing value can never display above its limit: rounding is monotone and the limits have at most 2 dp.
- Exact characters: `≤` is U+2264 and `×` is U+00D7.
- The summary never contains the applicant's raw income, expenses, deposit or property value.
- **Logging allowlist.** Logs, span attributes and metric tags carry only the application ID, assessment ID, decision and `RulesVersion`. They never carry an input, an output value (loan, LTV, DTI, LTI, payment, max borrowing) or the `Summary`/`Checks`: derived values reveal income, deposit and property value (`AGENTS.md`, "Never log … personal data"; `docs/layers/PRODUCTION-LAYERS.md`, "Observability").

## 7. Assumptions

- One applicant. Gross annual income.
- **Currency:** the spec doesn't name one. The maths is currency-agnostic. Docs and examples assume GBP (pound sterling).
- A repayment (capital and interest) mortgage, monthly payments, one fixed rate for the whole term.
- `MonthlyExpenses` already include the applicant's other debts and committed living costs, excluding the new mortgage.
- No credit score, fees, insurance, interest-only, help-to-buy, or stress test beyond the single assessment rate.
- A minimum loan of 1,000, so tiny loans don't produce meaningless approvals (`dotnet-reviewer` F11).
- The result is an estimate for this exercise, not a lending decision.

## 8. Worked examples (golden tests for Phase 2)

Inputs are income / monthly expenses / deposit / property value / term. Each row was computed with `System.Decimal` using exactly the formulas above, and independently recomputed by the `dotnet-reviewer` with Python `decimal` at 28 and 50 digits (evidence: `docs/progress/phase-01-spec.md`, "Worked-example check" and "Review gate").

| # | Inputs | Loan | LTV % | Payment | DTI % | LTI | MaxBorrowing | Decision | What it tests |
|---|---|---|---|---|---|---|---|---|---|
| E1 | 85,000 / 900 / 60,000 / 300,000 / 30y | 240,000.00 | 80.00 | 1,438.92 | 33.02 | 2.82 | 381,535.82 | **Approved** | B4 example 1 |
| E2 | 85,000 / 900 / 20,000 / 300,000 / 30y | 280,000.00 | 93.33 | 1,678.74 | 36.41 | 3.29 | 381,535.82 | **Declined** | B4 example 2; LTV only |
| E3 | 45,000 / 1,500 / 60,000 / 300,000 / 25y | 240,000.00 | 80.00 | 1,546.32 | 81.24 | 5.33 | 29,101.29 | **Declined** | B4 example 3; DTI and LTI |
| E4 | 60,000 / 0 / 30,000 / 200,000 / 25y | 170,000.00 | 85.00 | 1,095.31 | 21.91 | 2.83 | 270,000.00 | **Approved** | zero expenses; LTI cap binds MaxBorrowing |
| E5 | 30,000 / 1,200 / 50,000 / 200,000 / 25y | 150,000.00 | 75.00 | 966.45 | 86.66 | 5.00 | 0.00 | **Declined** | expenses above the DTI limit → MaxBorrowing 0 |
| E6 | 50,000 / 0 / 60,000 / 300,000 / 35y | 240,000.00 | 80.00 | 1,368.46 | 32.84 | 4.80 | 225,000.00 | **Declined** | LTI only |
| E7 | 70,000 / 500 / 30,000 / 300,000 / 30y | 270,000.00 | 90.00 | 1,618.79 | 36.32 | 3.86 | 315,000.00 | **Approved** | LTV exactly at the limit |
| E8 | 60,000 / 500 / 30,000 / 300,000 / 30y | 270,000.00 | 90.00 | 1,618.79 | 42.38 | 4.50 | 270,000.00 | **Approved** | LTV and LTI exactly at their limits |
| E9 | 80,000 / 1,000 / 0 / 200,000 / 30y | 200,000.00 | 100.00 | 1,199.10 | 32.99 | 2.50 | 333,583.23 | **Declined** | zero deposit → LTV 100% |
| E10 | 60,000 / 0 / 29,990 / 200,000 / 25y | 170,010.00 | 85.01 | 1,095.38 | 21.91 | 2.83 | 270,000.00 | **Approved** | raw LTV exactly 85.005: catches banker's rounding |
| E11 | 60,000 / 631.22 / 30,000 / 300,000 / 30y | 270,000.00 | 90.00 | 1,618.79 | 45.00 | 4.50 | 269,998.93 | **Declined** | raw DTI 45.000128: fails, "before rounding" |
| E12 | 60,000 / 631.21 / 30,000 / 300,000 / 30y | 270,000.00 | 90.00 | 1,618.79 | 45.00 | 4.50 | 270,000.00 | **Approved** | raw DTI 44.999928: passes, all three at the limit |
| E13 | 60,000 / 500 / 29,988 / 300,000 / 30y | 270,012.00 | 90.00 | 1,618.86 | 42.38 | 4.50 | 270,000.00 | **Declined** | raw LTV 90.004 and LTI 4.5002: two "before rounding" failures |
| E14 | 60,000 / 1,625 / 30,000 / 300,000 / 30y | 270,000.00 | 90.00 | 1,618.79 | 64.88 | 4.50 | 104,244.76 | **Declined** | DTI only |
| E15 | 60,000 / 300 / 50,000 / 200,000 / 5y | 150,000.00 | 75.00 | 2,899.92 | 64.00 | 2.50 | 100,864.84 | **Declined** | minimum term (5y) |
| E16 | 40,000 / 200 / 20,000 / 180,000 / 40y | 160,000.00 | 88.89 | 880.34 | 32.41 | 4.00 | 180,000.00 | **Approved** | maximum term (40y) |
| E17 | 10,000,000 / 1,000,000 / 0 / 100,000,000 / 40y | 100,000,000.00 | 100.00 | 550,213.64 | 186.03 | 10.00 | 0.00 | **Declined** | maximum inputs; all three fail |
| E18 | 1 / 1,000,000 / 0 / 100,000,000 / 5y | 100,000,000.00 | 100.00 | 1,933,280.15 | 3,519,936,183.53 | 100,000,000.00 | 0.00 | **Declined** | extreme ratios: storage must hold 10+ integer digits |

Exact summaries for the golden tests:

| # | `Summary` |
|---|---|
| E1 | `Approved: all 3 checks passed (LTV 80.00% ≤ 90.00%, DTI 33.02% ≤ 45.00%, LTI 2.82× ≤ 4.50×).` |
| E2 | `Declined: LTV 93.33% is above the 90.00% limit.` |
| E3 | `Declined: DTI 81.24% is above the 45.00% limit; LTI 5.33× is above the 4.50× limit.` |
| E6 | `Declined: LTI 4.80× is above the 4.50× limit.` |
| E8 | `Approved: all 3 checks passed (LTV 90.00% ≤ 90.00%, DTI 42.38% ≤ 45.00%, LTI 4.50× ≤ 4.50×).` |
| E11 | `Declined: DTI 45.00% is above the 45.00% limit before rounding.` |
| E13 | `Declined: LTV 90.00% is above the 90.00% limit before rounding; LTI 4.50× is above the 4.50× limit before rounding.` |
| E17 | `Declined: LTV 100.00% is above the 90.00% limit; DTI 186.03% is above the 45.00% limit; LTI 10.00× is above the 4.50× limit.` |

The summary for every other row follows the same templates. The full list printed by the check script is in the phase file.

## 9. Changes from master prompt B4

| B4 | This version | Why |
|---|---|---|
| "Stressed payment at 6%" | One 6% **assessment rate** | No product rate exists to stress (D1) |
| Checks: LTV, DTI, Loan ≤ MaxBorrowing | Checks: LTV, DTI, **LTI** | B4's third check repeated the DTI check; LTI was its only new part (D2). Same decisions as B4 on all 18 examples |
| Rounding point for comparisons not stated | Compare unrounded; round outputs once to scale 2 | Keeps the invariant exact (D3); stable JSON (F4) |
| Explanation "lists every check" | Structured checks + one summary sentence with exact templates | Testable and short (D7, F2, F3) |
| Input limits not stated | Section 1 preconditions, including a minimum loan of 1,000 | Phase 3 validation needs exact limits (D8, F11) |
| — | The rate and limits are stored with each assessment | Old assessments stay explainable (F7) |

The B4 worked examples (E1–E3) are unchanged: same values, same decisions.
