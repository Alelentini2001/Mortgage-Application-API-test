# Phase 02: Core domain + calculator (TDD)

Status: **In progress (Checkpoint A approved)** · Branch: `phase-02-core` · PR: — · Started: 2026-11-14 · Finished: —

## Goal

Build the pure domain in `src/MortgageApplications.Core`, test-first:
- the mortgage application entity (immutable, five inputs, owner, created time);
- the assessment entity (status, metrics, decision, explanation, `RulesVersion`, rate and limits used, timestamps);
- `AffordabilityOptions` and its validation;
- a pure `decimal` `AffordabilityCalculator` that evaluates all three checks and builds the explanation exactly as in `docs/spec/affordability-rules.md` (rules v1.0).

No HTTP, no EF Core, no persistence. Those are Phase 3.

## Plan (approved at Checkpoint A)

1. **Branch** `phase-02-core` from `main`.
2. **Docs check.** Verify on learn.microsoft.com every .NET API used (list under "Docs verified") and record the links.
3. **Test design.** The `test-designer` subagent proposes cases from rules v1.0. Alessandro picks the cases. Expected starting set:
   - golden E1–E18 from rules §8: every output field, the decision and the exact summary (`TheoryData` + `[MemberData]`, since `[InlineData]` can't hold `decimal`);
   - limit-exact boundaries, just below and just above (E7, E8, E11–E13);
   - rounding: E10 catches banker's rounding; every output has scale 2 (`decimal.Scale == 2`);
   - culture: the summary is the same under a culture with `,` as decimal separator. `InvariantGlobalization` is on in `Directory.Build.props`, so the test clones the invariant culture and changes its separators (named cultures like `it-IT` aren't available in invariant mode);
   - guard clauses: each broken section 1 precondition throws `ArgumentOutOfRangeException` with the parameter name, and the message holds no input value;
   - options validation: each bound in rules §1 (rate, three limits, `RulesVersion`);
   - entity state: Pending → Completed / Failed, no second completion, timestamps set from the caller;
   - the invariant "Approved ⇒ Loan ≤ MaxBorrowing" as a property test (see P2-D4).
4. **Red.** Write the approved tests, show `dotnet test` failing (compile errors count as red only for the first commit; then real assertion failures).
5. **Green.** Implement in small commits:
   - `DecimalMath.Pow` (exponentiation by squaring) and the 2 dp rounding helper;
   - `AffordabilityOptions` + validation;
   - input guard (section 1 preconditions);
   - `AffordabilityCalculator` + `AffordabilityResult` + `CheckResult` + summary builder;
   - `MortgageApplication` and `AffordabilityAssessment` entities.
   Remove the placeholder `HarnessSmokeTests` from the unit test project (Phase 0 said it goes in Phase 2).
6. **Verify.** `./scripts/verify.sh` (full), output in this file.
7. **Security gate.** `security-auditor` (+ `/security-review` on the PR).
8. **Review gate.** `dotnet-reviewer` on `git diff main...HEAD`, then `spec-checker`.
9. **Evidence.** This file, `docs/ai/PROMPTS.md`, `docs/ai/DESIGN-JOURNAL.md`, `docs/PROGRESS.md`. ADR only if a decision below is architectural (P2-D1/D2 likely go into one short ADR).
10. **PR**, local `/code-review` + `/security-review`, one PR comment, CI. **Checkpoint B.**

Planned files (Core): `AffordabilityOptions.cs`, `AffordabilityCalculator.cs`, `AffordabilityInputs.cs`, `AffordabilityResult.cs`, `CheckResult.cs`, `Decision.cs`, `AssessmentStatus.cs`, `MortgageApplication.cs`, `AffordabilityAssessment.cs`, `Internal/DecimalMath.cs`, `Internal/InputGuard.cs`, `Internal/SummaryBuilder.cs`. Tests: one test class per production class. Exact names may change during TDD; the final list goes under "What was built".

Not in scope: use cases and repository interfaces (Phase 3, where Alessandro chooses the service/persistence abstraction, master prompt B5), EF mapping, validation attributes on DTOs, logging.

## Decisions for Alessandro (2-3 options · trade-offs · recommendation)

| # | Question | Options | Recommendation |
|---|---|---|---|
| P2-D1 | Calculator shape | (a) **`sealed class AffordabilityCalculator(AffordabilityOptions options)`** with `Assess(AffordabilityInputs)`. Options validated once in the constructor; registered as a singleton in Phase 3. (b) `static Assess(inputs, options)`: no state at all, but options are re-validated on every call or not at all. (c) (a) plus an `IAffordabilityCalculator` interface: easy to mock, but nothing needs a mock (it's pure and fast), so it's a speculative abstraction. | **(a)**. Still a pure function of (inputs, options); no interface until something needs one. |
| P2-D2 | How the assessment holds the result | (a) **The entity holds an immutable `AffordabilityResult` record (null while Pending)**, with `IReadOnlyList<CheckResult> Checks`. The domain stays clean; Phase 3 decides how to map it (complex type / JSON column / flattened columns). (b) Flatten every output onto the entity now (`LoanAmount`, `LtvPercent`, …, `Checks` as JSON string). Easier EF mapping later, but persistence shapes the domain. | **(a)**. Mapping is a Phase 3 concern; the calculator's return type and the stored result are the same thing. |
| P2-D3 | Where `AffordabilityOptions` is validated | (a) **A pure `AffordabilityOptions.Validate()` in Core returning a list of error messages** (unit-tested here). Phase 3 wraps it in an `IValidateOptions<T>` + `ValidateOnStart` in the Api project. The calculator constructor also throws if it's invalid. (b) DataAnnotations `[Range]` on the properties: can't express "at most 2 dp" or "rate at most 4 dp" without custom attributes, and `[Range]` on `decimal` goes through `double`/string conversion. | **(a)**. All rules §1 option bounds in one tested place; Core keeps no ASP.NET reference. |
| P2-D4 | Property test for "Approved ⇒ Loan ≤ MaxBorrowing" | (a) **Seeded `System.Random` loop in a normal xUnit test** (fixed seed, about 10,000 samples inside the section 1 bounds, 2 dp inputs). Deterministic, no new package. (b) FsCheck or CsCheck: shrinking and better generators, but a new package needs your approval and adds supply-chain surface. | **(a)**. Phase 1 already ran 200,000 random samples with 0 violations; this keeps it as a repeatable test. |
| P2-D5 | Who enforces the section 1 preconditions in Core | (a) **One internal `InputGuard` used by both `MortgageApplication.Create` and the calculator.** Entities protect their invariants (`src/MortgageApplications.Core/CLAUDE.md`), and the calculator still guards other paths such as the Phase 6 worker (rules §3). (b) Only the calculator guards; the entity trusts Phase 3 validation. Less code, but an invalid application could then be built and stored. | **(a)**. One rule set, two callers, no duplication. |
| P2-D6 | Identity and time on entities | (a) **`Guid.CreateVersion7()` for IDs (time-ordered, good for the Phase 3 indexes); timestamps passed in as `DateTimeOffset` by the caller** (the Phase 3 use case reads `TimeProvider`). (b) The entity takes a `TimeProvider` itself: entities then depend on a service. (c) `Guid.NewGuid()`: random v4 IDs fragment the index. | **(a)**. |

## Decisions (option chosen · alternatives · reason · decided by)

Checkpoint A, 2026-11-14: Alessandro replied "approved" (P02-02) to the plan and the recommended option for every decision.

| # | Chosen | Decided by |
|---|---|---|
| P2-D1 | (a) sealed `AffordabilityCalculator(AffordabilityOptions)` with `Assess(inputs)`, no interface | Alessandro (agent's recommendation) |
| P2-D2 | (a) immutable `AffordabilityResult` record on the assessment, null while Pending | Alessandro (agent's recommendation) |
| P2-D3 | (a) pure `AffordabilityOptions.Validate()` in Core; Phase 3 wires startup validation | Alessandro (agent's recommendation) |
| P2-D4 | (a) seeded `System.Random` property test, no new package | Alessandro (agent's recommendation) |
| P2-D5 | (a) one internal `InputGuard` used by the entity and the calculator | Alessandro (agent's recommendation) |
| P2-D6 | (a) `Guid.CreateVersion7()` IDs; timestamps passed in by the caller | Alessandro (agent's recommendation) |

## Docs verified (links)

Checked with WebFetch on 2026-11-14 (net-10.0 view):
- `Math.Round(decimal, int, MidpointRounding)` and `MidpointRounding.AwayFromZero`: checked in Phase 1 (links in `phase-01-spec.md`). Not re-fetched.
- `Decimal.Scale`: https://learn.microsoft.com/en-us/dotnet/api/system.decimal.scale?view=net-10.0
  - `public byte Scale { get; }`, available from .NET 7. Used to assert scale 2 on every output.
- `Decimal.ToString(string, IFormatProvider)`: https://learn.microsoft.com/en-us/dotnet/api/system.decimal.tostring?view=net-10.0
  - Uses the `NumberFormatInfo` of the provider; with `null` it uses the current culture, so the summary always passes `CultureInfo.InvariantCulture`. The summary only formats values that are already rounded to scale 2, so the format string never rounds.
- `CultureInfo.Clone()`: https://learn.microsoft.com/en-us/dotnet/api/system.globalization.cultureinfo.clone?view=net-10.0
  - "The clone is writable even if the original CultureInfo is read-only", and `NumberFormat` is copied. The culture test clones the invariant culture and sets `NumberDecimalSeparator = ","`.
- Invariant globalization: https://learn.microsoft.com/en-us/dotnet/core/runtime-config/globalization
  - `PredefinedCulturesOnly` defaults to `true` in invariant mode: only the invariant culture can be created. Confirms why the test can't use `it-IT`.
- `ArgumentOutOfRangeException` constructors: https://learn.microsoft.com/en-us/dotnet/api/system.argumentoutofrangeexception.-ctor?view=net-10.0
  - `(string paramName, string message)`: `Message` is the given message, `ParamName` the given name; no value.
  - `(string paramName, object actualValue, string message)`: the value's string "is appended to the message". The guard never uses this overload.
- `Guid.CreateVersion7()`: https://learn.microsoft.com/en-us/dotnet/api/system.guid.createversion7?view=net-10.0
  - Available from .NET 9. RFC 9562 version 7; the parameterless overload uses `DateTimeOffset.UtcNow`, and there is a `CreateVersion7(DateTimeOffset)` overload. The entity uses `CreateVersion7(createdAt)`, so the ID's time matches the passed-in timestamp.
- `Random(int Seed)`: https://learn.microsoft.com/en-us/dotnet/api/system.random.-ctor?view=net-10.0
  - "Providing an identical seed value to different Random objects causes each instance to produce identical sequences". The page doesn't promise the sequence is stable across .NET versions, so the property test must not depend on specific generated values, only on the invariant.
- xUnit v3 `TheoryData<...>` + `[MemberData]`: xunit.net docs, not Learn; already used per `tests/CLAUDE.md`. Not re-fetched.

## Orchestration used

To fill in.

## Prompts

`docs/ai/PROMPTS.md` entries P02-01 onwards.

## What was built

Not started.

## Tests

### Selection (test-designer P02-03, approved by Alessandro P02-04)

Picked from the `test-designer` proposal, with the lead agent's recommendations:
- **Golden:** G1 (E1–E18: every output, check, `Passed` flag, scale 2), G2 (the 8 summaries printed in rules §8), G3 (6 more summaries derived from the templates).
- **Boundaries:** B1–B8, plus a new **E19** (60,000 / 500 / 19,988 / 290,000 / 30y → `Declined: LTV 93.11% is above the 90.00% limit; LTI 4.50× is above the 4.50× limit before rounding.`): a normal and a "before rounding" failure in one summary.
- **Rounding:** R1–R3. **Guard:** V1–V7. **Security:** S1–S2. **Options:** O1–O4. **Culture:** C1. **Pow:** D1–D3. **Property:** P1–P3. **Entities:** M1, M2, M4, A1–A5. **Concurrency:** X2.
- **Dropped:** G4 (repeats G2/G3 templates), M3 (reflection, brittle), X1 (covered by X2), S3 (covered by G2), A6 (replaced by the UTC rule below).

New expected numbers checked by the lead agent with `System.Decimal` (`check.cs` in the session scratchpad, not committed). All match the `test-designer`'s hand working:
```
B4 | loan 269999.00 | LTV 90.00 (raw 89,99966666666666666666666667) | pay 1618.78 | DTI 42.38 | LTI 4.50 (raw 4,4999833333333333333333333333) | max 270000.00 | Approved
B5 | loan 1000.00 | LTV 0.33 | pay 6.00 | DTI 12.79 | LTI 0.01 | max 381535.82 | Approved
B6 | loan 270000.00 | LTV 90.00 | pay 1618.79 | DTI 77.38 | LTI 4.50 | max 0.00 | Declined | Declined: DTI 77.38% is above the 45.00% limit.
B7 | loan 270000.00 | LTV 90.00 | ... | max 315000.00 | Declined | Declined: LTV 90.00% is above the 89.99% limit.
B8 | loan 240000.00 | ... | max 290594.63 | Approved | Approved: all 3 checks passed (LTV 80.00% ≤ 85.00%, DTI 35.25% ≤ 40.00%, LTI 2.82× ≤ 4.00×).
E19 | loan 270012.00 | LTV 93.11 | pay 1618.86 | DTI 42.38 | LTI 4.50 (raw 4,5002) | max 270000.00 | Declined | Declined: LTV 93.11% is above the 90.00% limit; LTI 4.50× is above the 4.50× limit before rounding.
Pow(1.5,10)=57,6650390625 Pow(1.005,360)=6,0225752122632161840540468160 Math.Pow=6,0225752122629865
```
(The raw values print with `,` because the scratch script ran under the machine culture; the script itself formats the summary with the invariant culture.)

### Implementation answers to the `test-designer` ambiguities (approved with P02-04)

1. Internal helpers are tested through `InternalsVisibleTo` for the unit test project (csproj item, no package).
2. `ParamName` is the PascalCase field name (`AnnualIncome`) on both paths.
3. "At most 2 dp" is by value (`Math.Round(x, 2) == x`), so `100.500m` is accepted.
4. Range checks run first, then the minimum-loan rule, which reports `DepositAmount`.
5. Invalid options → `ArgumentException` (option names, never values). A second state change → `InvalidOperationException`.
6. `AssessedAt` is on the assessment entity, not on the result.
7. Tests compare results field by field (a record holding a list compares the list by reference).
8. The summary uses `"0.00"` with no thousands separator (E18 shows `3519936183.53`); the commas in the rules table are for reading only.
9. Entity timestamps must be UTC (offset zero) or the entity throws; a finish time can't be before the creation time.
10. `AffordabilityOptions` has settable properties (config binding in Phase 3) and no defaults in code; the calculator copies the values in its constructor.
11. A Failed assessment stores only the status and the time; Phase 6 decides on a reason code.
12. `OwnerId` is a non-blank string (it will come from the `sub` claim in Phase 7).
13. Formulas are computed in the order rules §2 lists them.

### Results

Not run yet.

## Security gate

Not run yet.

## Review gate

Not run yet.

## AI output corrected or rejected

None yet.

## Optimisations considered

None yet.

## Open issues

- Carried from Phase 1: `security-auditor` S10 (local paths in prompt logs) and the `spec-checker` questions are still open for Alessandro.

## Time spent (approx.)

To fill in.

## Human review

