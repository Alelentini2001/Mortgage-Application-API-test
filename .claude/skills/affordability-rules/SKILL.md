---
name: affordability-rules
description: Mortgage affordability formulas, thresholds, assumptions and worked examples (LTV, stressed payment, DTI, max borrowing, decision). Use when writing or reviewing the AffordabilityCalculator, its options, or its tests.
user-invocable: false
paths:
  - "src/MortgageApplications.Core/**"
  - "tests/MortgageApplications.UnitTests/**"
---

The authoritative rules are in `docs/spec/affordability-rules.md`. Read that file before writing or reviewing calculator code or tests, and follow it exactly.

Non-negotiables:
- `decimal` only; `(1 + r)^n` via exponentiation by squaring on `decimal` (no `Math.Pow`).
- Compare unrounded values. Round each output once at the end: 2 dp, `MidpointRounding.AwayFromZero`, then `+ 0.00m` for scale 2.
- Summary strings use `CultureInfo.InvariantCulture` and the exact templates in section 6.
- The calculator is pure: no clock (the use case stamps `AssessedAt` via `TimeProvider`), no config reads, no I/O.
- Logs and spans carry only application ID, assessment ID, decision and RulesVersion: never an input or any output value (including `Summary`/`Checks`).
- Guard clauses: throw `ArgumentOutOfRangeException` when a section 1 precondition is broken.
- Evaluate every rule; the explanation lists every check with its value and threshold.
- Thresholds come from `AffordabilityOptions` and are stamped on the assessment as `RulesVersion`.
