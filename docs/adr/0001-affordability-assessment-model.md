# 0001. Affordability assessment model

- **Status:** Proposed (applied in Phase 1; awaiting Alessandro at Checkpoint B)
- **Date:** 2026-11-14
- **Decided by:** the agent's recommendation, applied under Alessandro's "proceed automatically" instruction for Phase 1 (P01-01). Alessandro confirms or changes it at Checkpoint B.

## Context

The spec asks for LTV, a debt-to-income ratio, an approved/declined decision, a maximum borrowing estimate and a short explanation. The logic must be "simple, reasonable" and "clearly explained", and realism is not assessed. The application has five fields and no interest rate. Master prompt B4 proposed a starting point with three checks: LTV ≤ 90%, DTI ≤ 45% and Loan ≤ MaxBorrowing, where MaxBorrowing = min(4.5 × income, the DTI-based loan).

Re-checking B4 with `System.Decimal` showed that "Loan ≤ MaxBorrowing" fails exactly when DTI or loan-to-income fails. For example, E3 and E5 were declined twice for the same reason.

## Options considered

1. **B4 as written.** Familiar, but the explanation repeats the DTI failure, and the third check's only new rule (4.5× income) is hidden inside "MaxBorrowing".
2. **Three independent checks (LTV, DTI, LTI), with MaxBorrowing reported but not checked.** Each failure has one reason. MaxBorrowing stays a useful output, and Approved ⇒ Loan ≤ MaxBorrowing still holds.
3. **Two checks (LTV, Loan ≤ MaxBorrowing).** Shortest, but the explanation can't say whether income multiple or affordability was the problem.

Supporting choices: one fixed assessment rate (6%) instead of a product rate plus a buffer, because there is no rate input. Comparisons use full-precision `decimal`, and outputs are rounded once to 2 dp with `AwayFromZero`. The thresholds and a `RulesVersion` live in `AffordabilityOptions`, and every assessment stores the version it used.

## Decision

Option 2, with the supporting choices above. Full rules: `docs/spec/affordability-rules.md`, rules version 1.0.

## Consequences

- The calculator is a pure function of (application, options). It is easy to unit test, and the explanation maps one-to-one to the checks.
- Decisions match B4 for all its worked examples, so nothing in the original intent is lost.
- Comparing unrounded values can produce "45.00% is above the 45.00% limit before rounding" at a boundary. The explanation states this explicitly.
- Changing a threshold is a config change plus a `RulesVersion` bump. Old assessments keep their version.
- Revisit if a product rate, joint applicants or credit data are ever added. All three would change the model, not just the thresholds.
