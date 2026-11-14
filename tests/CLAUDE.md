# Test conventions
- xUnit v3 on Microsoft.Testing.Platform. Filter with `--filter-class` / `--filter-method`.
- Test names: `Method_scenario_expected_result` (underscores allowed in tests only).
- `[InlineData]` cannot hold `decimal`: use `TheoryData<...>` with `[MemberData]`.
- Arrange / Act / Assert, one behaviour per test. No logic in tests beyond setup.
- Unit tests cover Core only. Integration tests use `WebApplicationFactory<Program>` and an isolated SQLite database per test class.
- Test-first: write the failing test, show it failing, then implement.
- Never weaken or delete a failing test to make the build pass; fix the code or ask Alessandro.
