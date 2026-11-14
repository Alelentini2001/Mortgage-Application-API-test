# Core project rules
- Pure domain: no references to ASP.NET Core, EF Core, logging frameworks or HTTP types.
- Money and percentages are `decimal`; no `double`/`Math.Pow`. Use the decimal power helper for `(1 + r)^n`.
- Round only at the end: `Math.Round(value, 2, MidpointRounding.AwayFromZero)`.
- The calculator evaluates **every** rule and returns every check in the explanation, even after one fails.
- Thresholds come from `AffordabilityOptions`; each assessment stores the `RulesVersion` it used.
- Entities protect their invariants (private setters, factory methods). No public mutable state.
- Formulas and worked examples: `docs/spec/affordability-rules.md`.
