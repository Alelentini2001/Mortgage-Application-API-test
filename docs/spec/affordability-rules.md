# Affordability rules

Rules version: **1.0** · Status: proposed in Phase 1 (decisions D1–D8 in `docs/progress/phase-01-spec.md`, [ADR 0001](../adr/0001-affordability-assessment-model.md)) · Source: master prompt B4, changed as recorded below.

The spec says the logic "does not need to be realistic or comprehensive" but must be "simple, reasonable" and "clearly explained". These rules are deliberately simple. Every number in an assessment can be recomputed by hand from this page.

## 1. Inputs

From the mortgage application (the five fields in the spec):

| Field | Unit | Calculator precondition (enforced by input validation, Phase 3) |
|---|---|---|
| `AnnualIncome` | GBP per year, gross | 1 to 10,000,000, at most 2 dp |
| `MonthlyExpenses` | GBP per month | 0 to 1,000,000, at most 2 dp |
| `DepositAmount` | GBP | 0 or more, **strictly below** `PropertyValue`, at most 2 dp |
| `PropertyValue` | GBP | 1 to 100,000,000, at most 2 dp |
| `TermYears` | whole years | 5 to 40 |

Invalid input never reaches the calculator. It's rejected with **400 `ValidationProblemDetails`**. A **Declined** assessment is a valid result, not an error.

From configuration (`AffordabilityOptions`, validated at startup):

| Option | Value in rules version 1.0 | Startup validation |
|---|---|---|
| `AssessmentRate` (annual) | 0.06 (6%) | > 0 and ≤ 0.25 |
| `MaxLoanToValuePercent` | 90 | > 0 and ≤ 100 |
| `MaxDebtToIncomePercent` | 45 | > 0 and ≤ 100 |
| `MaxLoanToIncomeMultiple` | 4.5 | > 0 and ≤ 10 |
| `RulesVersion` | "1.0" | required, not blank |

Any change to a threshold **must** bump `RulesVersion`. Every assessment stores the version it used, so old assessments stay explainable.

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
- `MonthlyPayment` uses the single assessment rate (decision D1). There's no separate "product rate" because the application has no rate field.
- `MonthlyExpenses` are the applicant's existing monthly commitments. They exclude the new mortgage, so DTI adds the new payment to them.
- `MaxBorrowing` is income-based (D4). It doesn't depend on the property. The property side is covered by the LTV check.
- If expenses alone are at or above the DTI limit, `AffordablePay ≤ 0`, so `MaxBorrowing = 0` (worked example E5).

## 3. Checks and decision

Three independent checks (D2). **Every check is always evaluated** and reported, even after one fails.

| Check | Passes when | Limit (v1.0) |
|---|---|---|
| `LoanToValue` | LTV % ≤ `MaxLoanToValuePercent` | 90% |
| `DebtToIncome` | DTI % ≤ `MaxDebtToIncomePercent` | 45% |
| `LoanToIncome` | LTI ≤ `MaxLoanToIncomeMultiple` | 4.5× |

**Decision:** `Approved` if all three checks pass, otherwise `Declined`. Limits are inclusive: a value exactly at the limit passes (E8).

**Invariant:** Approved ⇒ Loan ≤ MaxBorrowing. When DTI and LTI both pass, the loan is within both caps that define MaxBorrowing. Phase 2 tests this as a property.

## 4. Rounding

- Checks compare **full-precision** values (D3). Nothing is rounded before a comparison.
- Outputs are rounded **once**, at the end: `Math.Round(value, 2, MidpointRounding.AwayFromZero)`. The default `Math.Round` uses banker's rounding, so the mode is always passed explicitly.
- Money is shown to 2 dp. Percentages and the LTI multiple are also shown to 2 dp.
- Edge case: a value just over a limit can round to the limit (for example DTI 45.004% shows as 45.00%) and still fail. The explanation then says "above the limit before rounding", so the result never looks contradictory.

## 5. Outputs (an assessment)

| Field | Meaning |
|---|---|
| `LoanAmount` | `Loan`, 2 dp |
| `LoanToValuePercent` | LTV %, 2 dp |
| `MonthlyPayment` | payment at the assessment rate, 2 dp |
| `DebtToIncomePercent` | DTI %, 2 dp |
| `LoanToIncomeMultiple` | LTI, 2 dp |
| `MaxBorrowing` | 2 dp |
| `Decision` | `Approved` or `Declined` |
| `Checks` | one entry per check: `Name`, `Value`, `Limit`, `Passed` (values 2 dp) |
| `Summary` | one sentence (see below) |
| `RulesVersion` | for example "1.0" |
| `AssessedAt` | UTC timestamp |

## 6. Explanation

Structured checks plus one summary sentence (D7). Format:

- Approved: `Approved: all 3 checks passed (LTV 80.00% ≤ 90.00%, DTI 33.02% ≤ 45.00%, LTI 2.82× ≤ 4.50×).`
- Declined: `Declined: LTV 93.33% is above the 90.00% limit.` If more than one check fails, list each failed check, separated by `; `, in the table order: LTV, DTI, LTI.
- Rounding edge (section 4): `Declined: DTI 45.00% is above the 45.00% limit before rounding.`

The explanation never contains the applicant's raw income, expenses, deposit or property value. It is part of the API response, and the same rule keeps money out of logs (see `AGENTS.md`).

## 7. Assumptions

- One applicant. Gross annual income. GBP only.
- A repayment (capital and interest) mortgage, monthly payments, a fixed rate for the whole term.
- `MonthlyExpenses` already include the applicant's other debts and committed living costs, excluding the new mortgage.
- No credit score, fees, insurance, interest-only, help-to-buy or affordability stress beyond the single assessment rate.
- The result is an estimate for this exercise, not a lending decision.

## 8. Worked examples

Inputs are income / monthly expenses / deposit / property value / term. Each was computed with `System.Decimal` using exactly the formulas above (evidence: `docs/progress/phase-01-spec.md`, "Worked-example check"). These become golden tests in Phase 2.

| # | Inputs | Loan | LTV % | Payment | DTI % | LTI | MaxBorrowing | Decision | Why |
|---|---|---|---|---|---|---|---|---|---|
| E1 | 85,000 / 900 / 60,000 / 300,000 / 30y | 240,000.00 | 80.00 | 1,438.92 | 33.02 | 2.82 | 381,535.82 | **Approved** | all pass |
| E2 | 85,000 / 900 / 20,000 / 300,000 / 30y | 280,000.00 | 93.33 | 1,678.74 | 36.41 | 3.29 | 381,535.82 | **Declined** | LTV |
| E3 | 45,000 / 1,500 / 60,000 / 300,000 / 25y | 240,000.00 | 80.00 | 1,546.32 | 81.24 | 5.33 | 29,101.29 | **Declined** | DTI, LTI |
| E4 | 60,000 / 0 / 30,000 / 200,000 / 25y | 170,000.00 | 85.00 | 1,095.31 | 21.91 | 2.83 | 270,000.00 | **Approved** | zero expenses; LTI cap binds MaxBorrowing |
| E5 | 30,000 / 1,200 / 50,000 / 200,000 / 25y | 150,000.00 | 75.00 | 966.45 | 86.66 | 5.00 | 0.00 | **Declined** | DTI, LTI; expenses above the DTI limit → MaxBorrowing 0 |
| E6 | 50,000 / 0 / 60,000 / 300,000 / 35y | 240,000.00 | 80.00 | 1,368.46 | 32.84 | 4.80 | 225,000.00 | **Declined** | LTI only (DTI passes) |
| E7 | 70,000 / 500 / 30,000 / 300,000 / 30y | 270,000.00 | 90.00 | 1,618.79 | 36.32 | 3.86 | 315,000.00 | **Approved** | LTV exactly at the limit |
| E8 | 60,000 / 500 / 30,000 / 300,000 / 30y | 270,000.00 | 90.00 | 1,618.79 | 42.38 | 4.50 | 270,000.00 | **Approved** | LTV and LTI both exactly at their limits |
| E9 | 80,000 / 1,000 / 0 / 200,000 / 30y | 200,000.00 | 100.00 | 1,199.10 | 32.99 | 2.50 | 333,583.23 | **Declined** | zero deposit → LTV 100% |

## 9. Changes from master prompt B4

| B4 | This version | Why |
|---|---|---|
| "Stressed payment at 6%" | One 6% **assessment rate** | No product rate exists to stress (D1) |
| Checks: LTV, DTI, Loan ≤ MaxBorrowing | Checks: LTV, DTI, **LTI** | B4's third check repeated the DTI check; LTI was the only new part (D2) |
| Rounding point not stated for comparisons | Compare unrounded, round outputs once | Keeps the invariant exact (D3) |
| Explanation "lists every check" | Structured checks + one summary sentence | Testable and short (D7) |
| Input limits not stated | Section 1 preconditions | Phase 3 validation needs exact limits (D8) |

The B4 worked examples (E1–E3) are unchanged: same values, same decisions.
