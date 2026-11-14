# Phase 02: Core domain + calculator (TDD)

Status: **Merged** (squash `7a579a0`) · Branch: `phase-02-core` · PR: [#5](https://github.com/Alelentini2001/Mortgage-Application-API-test/pull/5) · Started: 2026-11-14 · Finished: 2026-11-14

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

Planned files (Core): `AffordabilityOptions.cs`, `AffordabilityCalculator.cs`, `AffordabilityInputs.cs`, `AffordabilityResult.cs`, `CheckResult.cs`, `Decision.cs`, `AssessmentStatus.cs`, `MortgageApplication.cs`, `AffordabilityAssessment.cs`, `Internal/DecimalMath.cs`, `Internal/InputGuard.cs`, `Internal/SummaryBuilder.cs`. Tests: one test class per production class. Exact names may change during TDD; the final list goes under "What was built". (Built differently: the internal helpers sit next to the calculator in `Affordability/`, so the namespace matches the folder.)

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

## Checkpoint B outcome (2026-11-14)

Alessandro: "approved" (P02-09).
- CI "Build, test, format" passed on the PR head `871ffb0`. The PR was squash-merged as `7a579a0` with a written message.
- No answer was given on the optional ADR for P2-D1/D2 or on the `tests/CLAUDE.md` property-test exception (`/code-review` finding 7), so both stay open.

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

- **Lead agent:** plan, docs check (WebFetch on Learn), stubs, tests, implementation, mutation check, fixes, evidence. Recomputed every new expected number with a throwaway `System.Decimal` script in the session scratchpad (`check.cs`, not committed).
- **Subagents** (the real `.claude/agents/` definitions):
  - `test-designer` (P02-03): proposed about 60 cases and 17 ambiguities. Alessandro approved the selection (P02-04).
  - `security-auditor` (P02-05), `dotnet-reviewer` (P02-06) and `spec-checker` (P02-07) ran in parallel, all read-only.
- **Skills:**
  - `/phase` (started by Alessandro).
  - `affordability-rules` is path-scoped to Core and unit-test files; I read `docs/spec/affordability-rules.md` directly either way.
  - `/adr`: not run. It is user-invoked only (see "Open issues").
- **Hooks that fired:**
  - The Gitleaks pre-commit hook on every commit ("no leaks found").
  - `format-on-edit.sh` and `verify-on-stop.sh`: their outcome wasn't shown in this session's tool output, so no claim is made.
  - `.claude/hooks/tests/test-hooks.sh` (via `verify.sh`): 91 cases on a clean tree, 90 with uncommitted `.cs` changes. One case (`verify-on-stop.sh`, "no source changes") only runs when the tree is clean. That's by design (`test-hooks.sh:116`).
- **MCP servers:** none.

## Prompts

`docs/ai/PROMPTS.md` entries P02-01 onwards.

## What was built

Commits on `phase-02-core`: `3947624` (failing tests + stubs), `9989eb6` (calculator), `932f529` (entities), `39e0595` (review and security fixes), `d439c3e` (evidence), `08d86d3` (`/code-review` fixes).

- `src/MortgageApplications.Core/Affordability/`
  - `AffordabilityCalculator.cs`: the pure calculator (rules §2–§6).
  - `AffordabilityOptions.cs`: settable options + `Validate()`.
  - `AffordabilityInputs.cs`, `AffordabilityResult.cs`, `CheckResult.cs`, `Decision.cs`: records and enum, with redacted `ToString`.
  - `InputGuard.cs` (internal): rules §1 preconditions; the exception names the field, never the value.
  - `SummaryBuilder.cs` (internal): exact summary templates, invariant culture.
  - `DecimalMath.cs` (internal): `Pow` by squaring, `Round2`, `HasAtMostDecimals`.
- `src/MortgageApplications.Core/Applications/`
  - `MortgageApplication.cs`: immutable entity, `Create` + `ToInputs`.
  - `AffordabilityAssessment.cs`: Pending → Completed or Failed, once.
  - `AssessmentStatus.cs`, `Timestamps.cs` (internal UTC + epoch check).
- `src/MortgageApplications.Core/MortgageApplications.Core.csproj`: `InternalsVisibleTo` the unit test project.
- `tests/MortgageApplications.UnitTests/`: `TestData.cs`, `Affordability/*Tests.cs`, `Affordability/GoldenCases.cs`, `Applications/*Tests.cs`. `HarnessSmokeTests.cs` removed.

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

**Red** (commit `3947624`, public API stubs that throw `NotImplementedException`): `dotnet test --project tests/MortgageApplications.UnitTests`
```
Test run summary: Failed!
  total: 205
  failed: 205
  succeeded: 0
```
These failures come from the stubs throwing, not from assertions. That is the "compile, then fail" red agreed in the plan (step 4).

**Green** (commits `9989eb6` calculator, `932f529` entities): 205 of 205 unit tests pass on the first full run. Full verify is in the next subsection.

Added / total / passing: 215 unit tests at the end (205 at the red commit, 8 from the gate fixes, 2 from `/code-review`) (the Phase 0 placeholder `HarnessSmokeTests` in the unit project was removed); 216 in the solution with the integration placeholder; 216 passing.

What each test class proves:

| Class | Cases | Proves |
|---|---|---|
| `AffordabilityCalculatorTests` | G1 (×3 theories over E1–E19), G2/G3/E19 summaries, B4–B8, O4, C1, X2 | Golden outputs, checks and scale 2; exact summaries; boundary behaviour; options drive limits and are copied; culture-independent summary; thread safety |
| `InputGuardTests` | V1–V7, S1–S2 | Every rules §1 bound (just below, at, just above); minimum loan; same guard in `Create`; exceptions name the field and never carry the value; null and invalid-options paths |
| `AffordabilityOptionsTests` | O1–O3 | Each option bound; valid options give no errors; all five errors reported together |
| `DecimalMathTests` | D1–D3, R1–R2 | Exact powers; agreement with a `double` oracle (test only) within 1e-12; negative exponent throws; AwayFromZero; scale 2 |
| `AffordabilityCalculatorPropertyTests` | P1–P3 | 10,000 seeded samples: Approved ⇒ Loan ≤ MaxBorrowing; never throws, scale 2, MaxBorrowing ≥ 0, loan ≥ 1,000; Approved exactly when all checks pass |
| `MortgageApplicationTests` | M1, M2, M4 + UTC | Keeps inputs and owner; version 7 ID; unique IDs; blank or null owner rejected; non-UTC time rejected |
| `AffordabilityAssessmentTests` | A1–A5 + UTC/order | Pending → Completed or Failed once; second change throws and keeps the first outcome; null result; UTC and "not before creation" |

### Verify

Final run: `./scripts/verify.sh` (full), 2026-11-14, on `phase-02-core` at `08d86d3` (clean tree, after the `/code-review` fixes):
```
    0 Warning(s)
    0 Error(s)
  total: 216
  failed: 0
  succeeded: 216
  skipped: 0
  duration: 989ms
hook tests: 91 passed, 0 failed
VERIFY PASSED (full)
```
216 = 215 unit tests + 1 integration placeholder. (At `39e0595`, before the `/code-review` fixes: 214 passed, 91 hook cases.)

First run, before the review fixes, at `932f529`:
```
==> build (Release, warnings as errors)
Build succeeded.
    0 Warning(s)
    0 Error(s)
==> all tests
Test run summary: Passed!
  total: 206
  failed: 0
  succeeded: 206
  skipped: 0
  duration: 1s 389ms
==> hook regression tests
hook tests: 91 passed, 0 failed
==> format check
VERIFY PASSED (full)
```

**Property test calibration (P1).** The floor was set after a real run. I temporarily raised `_minimumApproved` to 999,999 and read the failure message: `Only 1103 of 10000 samples were approved.` (the `dotnet-reviewer` reproduced 1103 independently). After the review fix R5 (property also biased to its bounds) the same check gave `Only 960 of 10000 samples were approved.` The committed floor is 500, about half the measured count, so a generator change that stops producing approvals fails the test.

**Mutation check (lead agent, not committed).** Two deliberate bugs, each applied, run, then reverted (`git status` clean afterwards, 0 failures again):

| Mutation | Unit tests failed |
|---|---|
| `Round2` uses `MidpointRounding.ToEven` | 6 |
| Checks compare the rounded value (`Round2(value) <= limit`) | 8 |

Both bugs the rules warn about are caught.

## Security gate

**`security-auditor` (P02-05): PASS.** No high or critical findings.

| Tool | Command | Result |
|---|---|---|
| Gitleaks, history | `gitleaks git --no-banner --redact -v` | 35 commits scanned, no leaks found, exit 0 |
| Gitleaks, working tree | `gitleaks dir --no-banner --redact .` | no leaks found, exit 0 |
| Semgrep, full | `semgrep scan --config p/csharp --config p/secrets --metrics=off --error --quiet --json` | exit 0; 0 results, 0 errors, 74 files |
| Semgrep, diff | same + `--baseline-commit main` | exit 0; 0 results, 0 errors, 16 files |
| NuGet vulnerabilities | `dotnet list MortgageApplications.slnx package --vulnerable --include-transitive` | no vulnerable packages (5 projects) |
| Runtime probe | a scratch console app against Core (scratchpad, not the repo) | `decimal.MaxValue`/`MinValue`, all 8 rate/term/income corners, `int.MaxValue` terms: clean results or a value-free `ArgumentOutOfRangeException` |

| Id | Severity | Finding | Resolution |
|---|---|---|---|
| S1 | Low (real in Phase 5) | Record `ToString` on `AffordabilityInputs`, `AffordabilityResult`, `CheckResult` prints inputs and derived values | **Fixed** in `39e0595`: redacted `ToString` + 2 tests. The auditor's point (b) stays for Phase 5: structured logging and serialisers read properties, not `ToString`, so the real control is `[LoggerMessage]` with only allowlisted fields plus a log-capture test. |
| S2 | Info (Phase 3, A01) | `AffordabilityAssessment` has no `OwnerId`; looking one up by ID alone would be an IDOR | **Carried to Phase 3**: every assessment query joins through the application's `OwnerId`; cross-user 404 test. |
| S3 | Info | `Complete` accepts any public `AffordabilityResult` | **Partly fixed** in `39e0595` (same as review R3): `Complete` rejects a result whose decision doesn't match its three checks. Results are still only built by the calculator via the use case (Phase 3). |
| S4 | Info (Phase 3) | Bad options fail only when the calculator is built | **Carried to Phase 3**: `IValidateOptions<AffordabilityOptions>` calling `Validate()` + `ValidateOnStart()`. |
| S5 | Info (Phase 3) | Guard messages are safe to map to a 400; other exceptions must go through ProblemDetails | **Carried to Phase 3**: test that a 500 has no stack trace or exception text. |
| S6 | Info | Version 7 IDs reveal the creation time (74 random bits, not enumerable) | **Accepted**: IDs are owner-scoped with 404s. To mention in the Phase 3 notes. |

False positives (from the auditor): no deposit upper bound (the minimum-loan rule bounds it), `Pow` overflow or loop, entity bypass, `Summary` holding derived values (allowed in the response, banned from logs), `InternalsVisibleTo`, synthetic money values in tests.

## Review gate

**`dotnet-reviewer` (P02-06).** It ran the 205 tests, recomputed E1–E19 and B4–B6 in Python `decimal` at 80 digits (all match `GoldenCases.cs`), and re-ran the property generator (1103 approvals, smallest MaxBorrowing − Loan gap 1.18). No high or medium findings.

| Id | Finding | Resolution |
|---|---|---|
| R1 | `default(DateTimeOffset)` passes the UTC check, then `Guid.CreateVersion7` throws with ParamName `timestamp` and "Actual value was -62135596800000" | **Fixed** (`39e0595`): reject times before the Unix epoch; 2 tests |
| R2 | Record `ToString` prints values (same as S1) | **Fixed** (`39e0595`) |
| R3 | `Complete` accepts an inconsistent hand-built result | **Fixed** (`39e0595`): consistency check in `Complete`; 2 tests. The record constructor was left public: Phase 3 may need it. |
| R4 | EF Core can't bind `MortgageApplication`'s private constructor (it takes `AffordabilityInputs`) and doesn't map get-only properties by convention | **Deferred to Phase 3** on purpose: the mapping choice is part of the Phase 3 persistence decision. Noted in "Open issues". |
| R5 | Property generator never hit PropertyValue 100,000,000; a third of samples clamp to 1,000 | **Fixed** (`39e0595`): property biased to both bounds, comment corrected; recalibrated (960 approvals) |
| R6 | O4 only proved LTV and version are copied | **Fixed** (`39e0595`): the test now changes all five options |
| N4 | No test that option errors leave out the value | **Fixed** (`39e0595`): 1 test (passes on old code too: a regression guard) |
| N5 | `AffordabilityResult` comment said every decimal has scale 2 (`AssessmentRate` doesn't) | **Fixed** (`39e0595`) |
| N6 | `100.500m` stored with scale 3 | **Fixed** (`39e0595`): `Create` stores money with scale 2; 1 test |
| N1 | No raw DTI exactly at 45% (not reachable with 2 dp inputs) | No change: all three checks share `Check()`, pinned by E7/E8 |
| N2 | P3 re-checks the implementation's own expression | No change: kept as a regression guard |
| N3 | No exact summaries for E4, E7, E10, E16 | No change: dropped as G4 at selection (P02-04) |

**Test-first for the fixes.** The fixes were written before their tests. To check the tests really test them, I stashed the `src` changes and ran the new tests against the old code: 7 of 8 new tests failed. The eighth, N4, passes on both versions because the code was already correct. Then I restored the fixes: 213 of 213 pass.

**`/code-review` (high effort, inline in the lead agent, on `main...phase-02-core` at `d439c3e`).** 8 findings, reported with the review tool:

| # | Finding | Outcome |
|---|---|---|
| 1 | `Complete` stored the caller's `Checks` list by reference; it could be changed after validation | **Fixed** (`08d86d3`): store a read-only copy and validate the copy; new test fails on the old code |
| 2 | "Approved exactly when all checks pass" exists in three places | **Partly fixed** by #5 (the summary now takes the calculator's decision). The check in `Complete` stays on purpose: it guards against hand-built results. Making `Decision` a computed property would change the record Phase 3 maps, so it is left for that phase. |
| 3 | `RulesVersion` isn't trimmed or length-limited | **Skipped**: rules §1 says only "required, not blank". A length limit belongs with the Phase 3 column. |
| 4 | `Complete` checks the argument before the state | **No change**: argument-first is the .NET convention; Phase 6 should check `Status` before calling `Complete`. |
| 5 | `SummaryBuilder` re-derived the decision and picked the unit by name | **Fixed** (`08d86d3`): takes `Decision`; units follow the fixed check order. Golden summaries unchanged. |
| 6 | `OwnerId` has no length bound or trimming | **Skipped**: belongs with Phase 7 (`sub` claim) and the Phase 3 column. |
| 7 | `tests/CLAUDE.md` "No logic in tests beyond setup" vs the loops in the property and concurrency tests | **Skipped, for Alessandro**: P2-D4 approved these tests; whether to write the exception into `tests/CLAUDE.md` is your call. |
| 8 | `AddPercentError` also checks the LTI multiple | **Fixed** (`08d86d3`): renamed `AddLimitError` |

**`/security-review` (P02-08).** The finder sub-task reported **no findings** at confidence 8 or above, so no false-positive filter sub-tasks ran. It rejected, with reasons: value leaks in exceptions, `ToString` leaks, `Summary`, entity bypass, hand-built results (S3), v7 IDs (S6), assessment IDOR (S2, Phase 3), entity serialisation, overflow, culture, `InternalsVisibleTo`, deposit bound.

**`spec-checker` (P02-07).** Two rows changed: **R28 missing → met** (Core business-logic tests) and **R29 missing → partial** (validation rules tested at domain level; the 400 `ValidationProblemDetails` tests come in Phase 3). Totals: **1 met, 28 partial, 26 missing** (was 0 / 27 / 28). It read the phase file before the results were written, so its "not run yet" note is out of date: `verify.sh` results are above. Its open questions: whether domain-level guard tests count for R29 (it said partial; so do I), R22/R24 depend on the Phase 3 abstraction choice, and R25 waits for Phase 9.

## AI output corrected or rejected

Real cases only:
- **Lead agent, caught by the build:** the first `InputGuard` passed field names (`DepositAmount`) as `paramName`, and analyzer CA2208 failed the build. Fixed by building the exception in one helper with a comment, not by suppressing the rule.
- **Lead agent, caught by `dotnet format`:** private `const` fields broke the `_camelCase` naming rule. Renamed.
- **Lead agent, caught by the `dotnet-reviewer`:** R1 (a pre-1970 timestamp leaked a value through `Guid.CreateVersion7`), R3, R5 and R6 above. R5 is a case where the lead agent's own comment claimed more than the generator did.
- **`test-designer` output not used as is:** its V1 row "PropertyValue 999.99" was flagged ambiguous by the designer itself. It was kept after fixing the order of checks (ranges before the minimum-loan rule), so the row reports `PropertyValue`.

## Optimisations considered

None applied. The full unit suite runs in under 1 s (`duration: 889ms` for 214 tests), so nothing was worth measuring further. `Pow` skips the unneeded final squaring.

## Open issues

- **ADR for P2-D1/P2-D2 (domain shape):** not written. `/adr` is user-invoked only, so the agent can't run it. If you want one: `/adr Core domain shape: pure calculator and entities`. The decisions and reasons are in this file.
- **For Phase 3:** owner scoping through the application for assessments (S2); `ValidateOnStart` for options (S4); 500 without exception text (S5); EF Core mapping for `MortgageApplication` (R4: a scalar constructor or explicit property mapping); R29 needs the HTTP 400 tests.
- **For Phase 5:** `[LoggerMessage]` with allowlisted fields only, plus a log-capture test (S1 part b).
- Carried from Phase 1: `security-auditor` S10 (local paths in prompt logs) and the `spec-checker` questions are still open for Alessandro.

## Time spent (approx.)

Not measured. No timer was kept, so no number is given.

## Human review

