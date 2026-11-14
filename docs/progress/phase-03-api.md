# Phase 03: API + persistence + validation (MVP)

Status: **Merged** (squash `a187e29`) · Branch: `phase-03-api` · PR: [#6](https://github.com/Alelentini2001/Mortgage-Application-API-test/pull/6) · Started: 2026-11-14 · Finished: 2026-11-14

## Goal

The minimum submittable product (master prompt B5, Phase 3). On top of the Phase 2 Core:
- EF Core + SQLite persistence: applications and their assessments (one-to-many), with an initial migration.
- Request/response DTOs in the Api project; entities are never exposed.
- Input validation at the boundary: 400 `ValidationProblemDetails` for every rules §1 precondition.
- Endpoints (Minimal APIs, `MapGroup`, `TypedResults`):
  - `POST /api/applications` → 201 + `Location`
  - `GET /api/applications/{id}` → 200 / 404
  - `POST /api/applications/{id}/assessments` → **synchronous** assessment (Phase 6 makes it 202)
  - `GET /health` (exists)
- `AddProblemDetails` + an `IExceptionHandler`: trace ID on every error, no exception text outside Development.
- OpenAPI document (+ Scalar UI if approved, P3-D6) in Development only; `requests.http` for the demo.
- Integration tests with `WebApplicationFactory<Program>` on a real SQLite database.

Carried in from Phase 2 (all in scope here):
- **S2** owner scoping for assessments through the application's `OwnerId`, with a cross-owner 404 test.
- **S4** `AffordabilityOptions` validated at startup (`IValidateOptions<T>` calling `Validate()` + `ValidateOnStart()`).
- **S5** a 500 carries no stack trace or exception text (test).
- **R4** EF Core mapping for `MortgageApplication` (private scalar constructor EF can bind; get-only properties mapped explicitly).
- **R29** HTTP 400 validation tests.
- `/code-review` 3 and 6: column lengths for `RulesVersion` and `OwnerId` (see step 5).

Not in scope: background processing (Phase 6), JWT auth (Phase 7: until then `ICurrentUser` returns one fixed development owner, per `PRODUCTION-LAYERS.md`), logging/OTel (Phase 5), Docker (Phase 4), rate limiting (Phase 8).

## Plan (approved at Checkpoint A)

1. **Branch** `phase-03-api` from `main`.
2. **Packages** (only after P3-D6 is approved): add the approved versions to `Directory.Packages.props`, reference them, `dotnet restore`, `verify.sh --fast` still green. Separate `build(infra)` commit.
3. **Docs check** on learn.microsoft.com, links recorded below. At least:
   - Minimal API validation in .NET 10 (`AddValidation`, DataAnnotations + `IValidatableObject`, the 400 shape, nullable members and `[Required]`);
   - `MapGroup`, `TypedResults`, `Results<…>`, `CreatedAtRoute`/`Created` + `Location`;
   - `AddProblemDetails`, `IExceptionHandler`, `UseExceptionHandler`, `UseStatusCodePages`, where the `traceId` extension comes from;
   - `AddOpenApi` / `MapOpenApi` (+ Scalar's own docs for `MapScalarApiReference`);
   - EF Core 10: complex types (optional complex types, complex collections mapped to JSON), constructor binding, backing fields for get-only properties, `HasConversion<string>()` for enums, `HasPrecision`;
   - EF Core SQLite provider limitations: `decimal` and `DateTimeOffset` are stored as TEXT and can't be compared or ordered in SQL;
   - migrations (`dotnet ef migrations add`, `Database.MigrateAsync`), `dotnet-ef` as a local tool;
   - options: `BindConfiguration`, `IValidateOptions<T>`, `ValidateOnStart`;
   - `WebApplicationFactory<TEntryPoint>`, `ConfigureTestServices`, in-memory SQLite with an open connection;
   - `TimeProvider`, `System.Text.Json` `JsonUnmappedMemberHandling` and `JsonNumberHandling` (P3-D8).
4. **Test design.** `test-designer` proposes cases (400 per field and bound, missing/null fields, deposit rule, 201 + `Location`, 404s, cross-owner 404, Declined is 201 not an error, assessment response values match golden E1, scale 2 in JSON, ProblemDetails shape + `traceId`, 500 without exception text, startup fails with bad options). **Alessandro picks the cases.**
5. **Red, then green**, in small commits:
   - Core: the validation rules shape from P3-D2; private scalar constructors for EF binding (R4); the use case + repository interface from P3-D1; `RulesVersion` length bound (max 16) added to `AffordabilityOptions.Validate()` so it always fits its column.
   - Infrastructure: `MortgageApplicationsDbContext`, one `IEntityTypeConfiguration` per entity (`HasPrecision(18,2)` on money, enums as strings, `OwnerId` max 128, `RulesVersion` max 16, index on `OwnerId`, FK + index on `ApplicationId`, cascade delete), the result mapping from P3-D3, the repository (every read filters on `Id` **and** owner; assessments join through the application), the initial migration (P3-D5).
   - Api: DTOs, validation, endpoint group, `ICurrentUser` + `DevelopmentCurrentUser`, `AffordabilityOptions` bound from `"Affordability"` in `appsettings.json` with `ValidateOnStart`, calculator registered as a singleton, `TimeProvider.System`, ProblemDetails + exception handler, OpenAPI (+ Scalar) in Development only, migrate on startup in Development only (an ADR on startup migrations is Phase 4's), `requests.http`.
   - Tests: integration tests replace the placeholder `HarnessSmokeTests`; unit tests for the validation rules and the use case.
6. **Verify.** `./scripts/verify.sh` (full), output in this file.
7. **Security gate.** `security-auditor` + `/security-review` on the PR.
8. **Review gate.** `dotnet-reviewer` on `git diff main...HEAD`, then `spec-checker` (R1, R2, R4, R10–R13, R23, R26, R29 should move to met).
9. **Evidence.** This file, `PROMPTS.md`, `DESIGN-JOURNAL.md`, `PROGRESS.md`. ADR candidate: P3-D1 (if you want one, run `/adr`).
10. **PR**, local `/code-review` + `/security-review`, one PR comment, CI. **Checkpoint B.**

Response shapes (draft; the field names follow rules §5):
- `ApplicationResponse`: `id`, `annualIncome`, `monthlyExpenses`, `depositAmount`, `propertyValue`, `termYears`, `createdAt`. No `ownerId`.
- `AssessmentResponse`: `id`, `applicationId`, `status`, `createdAt`, `assessedAt`, and `result` (null unless Completed, so Phase 6 can return Pending with the same shape): the 11 rules §5 outputs + `checks[]`. `maxBorrowing` carries the OpenAPI description "income-based maximum borrowing; ignores property value and deposit" (rules §2).

Known SQLite limits we accept: `DateTimeOffset` supports only equality in SQL (no comparison or ordering), so any list would sort by the version 7 `Id`. Money is `decimal`; EF translates `decimal` comparison and ordering on SQLite through its own functions (corrected after the docs check: the plan first said it couldn't). Nothing in this phase filters or sorts on money. `HasPrecision(18,2)` documents the Azure SQL type (`PRODUCTION-LAYERS.md`, "Database").

## Decisions for Alessandro (2-3 options · trade-offs · recommendation)

| # | Question | Options | Recommendation |
|---|---|---|---|
| P3-D1 | **Service/persistence abstraction** (the B5 choice) | (a) **A use-case service in Core** (`MortgageApplicationService`: create, get, assess, get assessment) **and one small repository interface in Core** (`IMortgageApplicationRepository`), implemented with EF in Infrastructure. Every read method takes the owner, so owner scoping (S2) is in the signature. Core stays free of EF; the Phase 6 worker reuses the same assess path; the use case is unit-testable with a hand-written fake. Cost: about 3 more files, and reads return entities (mapped to DTOs in Api) instead of projecting in SQL. (b) **Endpoints use the `DbContext` directly** and call the calculator. Fewest files, reads can project straight to DTOs, but the orchestration (owner check, assess, complete, save) lives in HTTP handlers, Api depends on EF, and Phase 6 has to extract it anyway. (c) (a) without the repository: the Core service gets an `IQueryable`-style store or the DbContext through an interface. Leaks EF query semantics into Core. | **(a)**. It matches the `AGENTS.md` boundaries ("use cases" in Core, Api never shapes the domain) and gives Phase 6 a ready-made path. The repository is small and has two real implementations (EF, test fake), so it isn't speculative. |
| P3-D2 | **Where the rules §1 input bounds live for the 400 response** | (a) **One public pure rule set in Core** (`ApplicationInputRules.Validate(inputs)` → list of (field, message)), which `InputGuard` also uses. The request DTO has nullable properties with `[Required]` (so a missing field is a 400, not a silent 0) and `[Range(5, 40)]` on `TermYears`, and its `IValidatableObject.Validate` maps the Core errors to field errors. One source for the bounds; exact `decimal` comparisons. Cost: a small refactor of Phase 2's `InputGuard` (its tests stay as they are). (b) **DataAnnotations only in Api**: `[Range(typeof(decimal), …)]` + a custom `[MaxDecimalPlaces(2)]` attribute, `IValidatableObject` only for the minimum-loan rule. Looks the most "framework", but the bounds are written twice (Api and Core) and can drift; `[Range]` with `typeof(decimal)` parses its bounds from strings. | **(a)**. Same bounds everywhere, still uses `AddValidation` + DataAnnotations + `IValidatableObject` as the spec and B5 ask. |
| P3-D3 | **How the assessment result is stored** | (a) **EF 10 optional complex type** for `Result`: the scalar outputs become columns on `AffordabilityAssessments` (decision queryable, `decimal(18,2)` in Azure SQL), and `Checks` is a complex collection stored as one JSON column. No domain change. Depends on EF 10 features I will confirm in the docs check; if they don't fit the record as it is, I fall back to (b) and tell you. (b) **The whole result as one JSON column** with a value converter. Least mapping risk, but nothing in it is queryable and it needs a hand-written value comparer. (c) **Flat columns + a separate `AssessmentChecks` table.** Most relational, but more code and tables for three fixed rows. | **(a)**, with (b) as the stated fallback. |
| P3-D4 | **Assessment endpoint response** | (a) **201 Created + `Location: /api/applications/{id}/assessments/{assessmentId}`**, with the assessment in the body, **and add `GET …/assessments/{assessmentId}` now** so the `Location` resolves. Declined is still 201 (a valid result). Phase 6 only changes the POST to 202. (b) 200 OK with the body, no `Location`; the GET waits for Phase 6. Smaller now, but the POST creates a resource without saying where it is, and Phase 6 changes both status and shape. | **(a)**. Each POST creates a new assessment row (not idempotent; `PRODUCTION-LAYERS.md` "APIs" already records `Idempotency-Key` as a production next step). |
| P3-D5 | **Schema creation** | (a) **EF migrations**: initial migration in Infrastructure, `dotnet-ef` as a repo-local tool (`.config/dotnet-tools.json`, pinned), `Microsoft.EntityFrameworkCore.Design` with `PrivateAssets=all`. The API applies migrations at startup in Development only; tests apply them per test class. Merged migrations are protected by `protect-files.sh`. (b) `EnsureCreated()`: no tool, no Design package, but no schema history, it can't evolve a database, and B5 asks for an initial migration. | **(a)**. |
| P3-D6 | **Packages to add** (each needs your approval; versions are the latest .NET 10 patch, matching the installed runtime 10.0.15; .NET 11 packages are out because we target `net10.0`) | `Microsoft.EntityFrameworkCore.Sqlite` 10.0.15 (Infrastructure) · `Microsoft.EntityFrameworkCore.Design` 10.0.15 (Infrastructure, `PrivateAssets=all`) · `dotnet-ef` 10.0.15 (local tool) · `Microsoft.AspNetCore.OpenApi` 10.0.15 (Api) · `Microsoft.AspNetCore.Mvc.Testing` 10.0.15 (IntegrationTests) · `Scalar.AspNetCore` (Api, Development only; I'd pin the newest version that is at least two weeks old, checked at install: the latest are 2.19.0, 2.20.0 and 2.21.0). Alternatives for the docs UI: (a) **Scalar** as B5 says: a browsable UI for the live demo, one third-party package. (b) No UI: only `/openapi/v1.json` + `requests.http`; one fewer dependency, a less friendly demo. | **Approve all, with Scalar (a)**. Microsoft packages are first-party; Scalar is mapped in Development only, so it never ships in a Production pipeline. |
| P3-D7 | **Integration test database** | (a) **In-memory SQLite** (`Data Source=:memory:`) with one connection held open by the factory for the test class; migrations applied on it. Real SQLite engine, fast, nothing left on disk. (b) A temp SQLite file per test class, deleted afterwards. Closer to the running app, slower, needs clean-up. | **(a)**. Matches `tests/CLAUDE.md` ("isolated SQLite database per test class"). |
| P3-D8 | **JSON strictness on requests** | (a) **Strict:** the create DTO rejects unknown properties (`JsonUnmappedMemberHandling.Disallow`) and numbers sent as strings → 400. A client trying `"ownerId": "x"` gets a 400, not a silent ignore. (b) ASP.NET defaults: unknown properties are ignored; `"85000"` as a string is accepted (web defaults allow reading numbers from strings). More forgiving clients. | **(a)**. Small, and it makes mass-assignment attempts visible. Any JSON error is a 400 ProblemDetails that never echoes the submitted value. |

## Decisions (option chosen · alternatives · reason · decided by)

Checkpoint A, 2026-11-14: Alessandro replied "approved" (P03-02) to the plan and the recommended option for every decision.

| # | Chosen | Decided by |
|---|---|---|
| P3-D1 | (a) use-case service + one repository interface in Core, EF implementation in Infrastructure | Alessandro (agent's recommendation) |
| P3-D2 | (a) one public rule set in Core, used by the DTO's `IValidatableObject` and by `InputGuard` | Alessandro (agent's recommendation) |
| P3-D3 | (a) EF 10 optional complex type, checks as a JSON column; fallback (b) one JSON column | Alessandro (agent's recommendation) |
| P3-D4 | (a) 201 + `Location`, plus `GET …/assessments/{assessmentId}` now | Alessandro (agent's recommendation) |
| P3-D5 | (a) EF migrations, `dotnet-ef` as a pinned local tool, migrate at startup in Development only | Alessandro (agent's recommendation) |
| P3-D6 | All packages approved, with Scalar | Alessandro (agent's recommendation) |
| P3-D7 | (a) in-memory SQLite per test class, connection held open | Alessandro (agent's recommendation) |
| P3-D8 | (a) strict request JSON | Alessandro (agent's recommendation) |

Versions installed (P3-D6), checked on the NuGet registration API on 2026-11-14:
- Microsoft packages and `dotnet-ef`: **10.0.15**, published 2026-11-10. This is 4 days old, but it is the servicing patch that matches the installed runtime (`Microsoft.AspNetCore.App 10.0.15`), as stated in the approved plan. 10.0.14 (2026-10-25) is the previous patch.
- `Scalar.AspNetCore`: **2.18.7**, published 2026-10-30: the newest version at least two weeks old (2.19.0 is from 2026-11-05).

## Checkpoint B outcome (2026-11-14)

Alessandro: "Proceed" (P03-12), after "Ok, what now?" (P03-11).
- CI "Build, test, format" passed on PR #6; the PR was squash-merged as `a187e29` with a written message.
- No answer was given on the two open questions (keep `DevelopmentCurrentUser` until Phase 7; reword `.claude/rules/backend.md` "project to DTOs" or accept the exception) or on an optional ADR, so they stay open in `docs/PROGRESS.md`.

## Docs verified (links)

Checked with WebFetch on 2026-11-14 (ASP.NET Core 10 view where the page has versions). What each page settles for this phase:

- **Validation:** https://learn.microsoft.com/en-us/aspnet/core/fundamentals/validation?view=aspnetcore-10.0
  - `builder.Services.AddValidation()`. A source generator creates metadata "in the assembly where `AddValidation` is called", and Minimal API parameter types "are discovered from endpoint handler signatures". So the request DTOs live in the Api project; no `[ValidatableType]` needed (that attribute is experimental in .NET 10, warning ASP0029).
  - Order: property attributes → type attributes → `IValidatableObject.Validate`. "Class-level validation runs after property validation and only if property validation succeeds." So `[Required]` errors come first, and the Core rules run only on a complete request.
  - "Missing metadata doesn't produce a runtime exception or log entry": an integration test must prove validation actually runs.
  - `AddValidation` "doesn't automatically validate or reject unmapped JSON properties"; set `UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow` (P3-D8).
  - Known .NET 10 limit: nullable value types are not validated **as endpoint parameters** (aspnetcore #67033). Our nullable members are properties of a body DTO, not parameters; a test covers it.
- **Minimal API validation summary and `DisableValidation`:** https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis?view=aspnetcore-10.0 ("If validation fails, the runtime returns a `400 - Bad Request` response").
- **Errors:** https://learn.microsoft.com/en-us/aspnet/core/fundamentals/error-handling-api?view=aspnetcore-10.0 and https://learn.microsoft.com/en-us/aspnet/core/fundamentals/error-handling?view=aspnetcore-10.0
  - `AddProblemDetails()` + `UseExceptionHandler()` + `UseStatusCodePages()` give ProblemDetails for empty 4xx/5xx responses.
  - `IExceptionHandler.TryHandleAsync(HttpContext, Exception, CancellationToken)` → `ValueTask<bool>`; registered with `AddExceptionHandler<T>()` (singleton); "If `UseExceptionHandler` isn't called, the registered `IExceptionHandler` implementations are never called."
  - **`ThrowOnBadRequest` defaults to `true` in Development**: malformed JSON throws `BadHttpRequestException`, and "In ASP.NET Core 10 and earlier, unhandled exceptions default to `500 Internal Server Error` unless the handler explicitly preserves `BadHttpRequestException.StatusCode`". Our handler must map it to its own status code (400/415). Integration tests run in Development, so this is tested.
  - **.NET 10 change:** "the default behavior is to suppress emission of diagnostics such as logs and metrics for handled exceptions (when `TryHandleAsync` returns `true`)". The handler must not hide real 500s; Phase 5 adds the allowlisted log. Noted for Phase 5.
  - "Don't share detailed exception information publicly when the app runs in production." The production sample shows a `traceId`, but the page doesn't say where it comes from: an integration test asserts it is present.
- **Responses, `TypedResults`, `IProblemDetailsService` customisation:** https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/responses?view=aspnetcore-10.0
- **Body binding and JSON options:** https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/parameter-binding?view=aspnetcore-10.0
  - `builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions…)`. "Failure to deserialize JSON body" → 400; wrong content type → 415. The page doesn't document number-from-string handling, so a test pins it (P3-D8).
- **OpenAPI:** https://learn.microsoft.com/en-us/aspnet/core/fundamentals/openapi/aspnetcore-openapi?view=aspnetcore-10.0
  - `AddOpenApi()` / `MapOpenApi()` at `/openapi/{documentName}.json`; "In .NET 10, the default is 3.1"; mapped inside `if (app.Environment.IsDevelopment())`. "Interactive UIs such as Swagger UI or Scalar are not included by default and must be added separately." Scalar's own docs are not on Learn; checked at implementation.
- **EF Core complex types:** https://learn.microsoft.com/en-us/ef/core/modeling/complex-types
  - Optional complex properties are EF 10+ and need "at least one **required** property" (our `Decision` enum is non-nullable, so it qualifies).
  - Complex collections are EF 10+ and "must be mapped to a single JSON column using `ToJson`".
  - "Unlike table splitting, JSON mapping allows collections *within* the mapped type." **Risk for P3-D3 (a):** a JSON collection (`Checks`) inside a table-split complex property may not be allowed. If the model build rejects it, the approved fallback applies: `ComplexProperty(a => a.Result, b => b.ToJson())` (the whole result in one JSON column, still a complex type, not a value converter). I'll report which one was used.
  - Records are supported as complex types (examples use positional records). The page doesn't say whether `IReadOnlyList<T>` works as the collection type; checked when the model builds.
- **EF Core constructor binding:** https://learn.microsoft.com/en-us/ef/core/modeling/constructors
  - "The parameter types and names must match property types and names, except that properties can be Pascal-cased while the parameters are camel-cased"; the constructor "can be public, private, or have any other accessibility".
  - "Properties without setters are not mapped by convention." So `MortgageApplication` gets a private scalar constructor (R4) and its get-only properties are mapped explicitly with `Property(…)`. No public setters.
- **EF Core SQLite limitations:** https://learn.microsoft.com/en-us/ef/core/providers/sqlite/limitations
  - `DateTimeOffset`: only equality is translated; comparison and ordering run on the client.
  - `decimal`: "EF Core translates comparison, ordering, arithmetic and aggregate operations on `decimal` values to functions and a collation that it registers on the connection". (This corrected the plan.)
  - Migrations: no idempotent scripts on SQLite; EF 9+ uses a `__EFMigrationsLock` table.
- **Integration tests:** https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests?view=aspnetcore-10.0
  - "If the SUT's environment isn't set, the environment defaults to `Development`."
  - .NET 10 pattern: remove the `IDbContextOptionsConfiguration<TContext>` and `DbConnection` descriptors, register an open `SqliteConnection("DataSource=:memory:")` as a singleton, re-add the context with `UseSqlite(connection)`. "the SQLite provider is the recommended choice for in-memory testing".
  - `WithWebHostBuilder(b => b.ConfigureTestServices(…))` to replace a service in one test (used for the cross-owner `ICurrentUser`). `IClassFixture<…>` per test class.
- **Options validation:** https://learn.microsoft.com/en-us/dotnet/core/extensions/options
  - `IValidateOptions<T>.Validate(string? name, T options)` → `ValidateOptionsResult.Fail(…)` / `Success`; registered with `TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<T>, …>())`; `.ValidateOnStart()` on `AddOptions<T>().Bind(…)` moves validation to startup.
- **Carried from Phase 2** (not re-fetched): `Guid.CreateVersion7`, `ArgumentOutOfRangeException` constructors, `Math.Round` with `MidpointRounding.AwayFromZero`.
- **`JsonSerializerOptions.AllowDuplicateProperties`** (added after the security gate, I2): https://learn.microsoft.com/en-us/dotnet/api/system.text.json.jsonserializeroptions.allowduplicateproperties?view=net-10.0
  - Available in .NET 10; "The default is `true`"; "If set to `false`, JsonException is thrown when a duplicate property name is encountered during deserialization."
- **Not done:** `TimeProvider` (standard BCL type, planned in Phase 2) and Scalar's `MapScalarApiReference` (third-party, not on Learn) were not checked against docs in this phase. Both are used as in their standard samples, and tests cover the behaviour (fixed clock in unit tests; Scalar 302 in Development, 404 in Production).

## Orchestration used

- **Lead agent:** plan, docs check (WebFetch on Learn), NuGet version check (registration API, saved to the scratchpad), stubs, tests, implementation, smoke test, gate fixes, evidence.
- **Subagents** (the real `.claude/agents/` definitions):
  - `test-designer` (P03-04): 19 unit and 46 integration rows plus 12 ambiguities; Alessandro approved the selection (P03-05).
  - `security-auditor` (P03-07), `dotnet-reviewer` (P03-08) and `spec-checker` (P03-09) ran in parallel, all read-only.
- **Skills:** `/phase` (started by Alessandro). `/adr` not run (user-invoked only).
- **Hooks that fired:**
  - `block-dangerous-bash.sh` blocked a `curl … | python3` pipe during the NuGet version check (pipe-to-interpreter rule); redone by saving to the scratchpad first.
  - Permission rules denied `dotnet tool *` (and the auto-mode classifier refused writing the tool manifest by hand), so Alessandro installed `dotnet-ef`. They also denied `dotnet ef migrations has-pending-model-changes` for the `dotnet-reviewer`.
  - The Gitleaks pre-commit hook on every commit ("no leaks found").
  - `.claude/hooks/tests/test-hooks.sh` via `verify.sh`: 91 cases on a clean tree, 90 with uncommitted `.cs` changes (by design).
  - `format-on-edit.sh` and `verify-on-stop.sh`: their output wasn't shown in this session, so no claim is made.
- **MCP servers:** none.

## Prompts

`docs/ai/PROMPTS.md` entries P03-01 onwards.

## What was built

Commits on `phase-03-api`: `a0daa7c` (packages), `e604a56` (red), `90136a9` (Core), `14a6c9c` (Infrastructure), `654281b` (Api), `371d9f9` (naming), `bf2920c` (migration), `a446c64` (docs), `eb21d5f` (`requests.http`), `8a2e80e` + `bb24cff` + `5f91df6` (gate fixes), then evidence.

- **Core** (`src/MortgageApplications.Core/`)
  - `Affordability/ApplicationInputRules.cs`: public rules §1 rule set (`InputError` field + message, never the value); `InputGuard` now throws its first error.
  - `Applications/MortgageApplicationService.cs`, `IMortgageApplicationRepository.cs`: the use case and its owner-scoped port.
  - `MortgageApplication` scalar constructor for EF (R4); `AffordabilityOptions` 16-character `RulesVersion` bound.
- **Infrastructure** (`src/MortgageApplications.Infrastructure/`)
  - `Persistence/MortgageApplicationsDbContext.cs`, `MortgageApplicationConfiguration.cs`, `AffordabilityAssessmentConfiguration.cs`, `EfMortgageApplicationRepository.cs`.
  - `AffordabilityResultJson.cs` (result as one JSON column, P3-D3 fallback b), `MinimumScaleTwoConverter.cs` (2 dp after SQLite).
  - `DesignTimeDbContextFactory.cs`, `Migrations/` (`InitialCreate`), `DependencyInjection.cs`, `DatabaseMigration.cs`.
- **Api** (`src/MortgageApplications.Api/`)
  - `Program.cs`; `Applications/ApplicationEndpoints.cs`, `CreateApplicationRequest.cs`, `Responses.cs`; `Errors/ProblemExceptionHandler.cs`, `ValidationErrorKeys.cs`; `Configuration/AffordabilityOptionsValidator.cs`; `Identity/ICurrentUser.cs`, `DevelopmentCurrentUser.cs`; `appsettings.json` (connection string, `Affordability` v1.0); `requests.http`.
- **Build:** `Directory.Packages.props` (5 packages), `dotnet-tools.json` (`dotnet-ef` 10.0.15), `.editorconfig` (migrations are generated code).
- **Tests:** `tests/MortgageApplications.UnitTests/Affordability/ApplicationInputRulesTests.cs`, `Applications/MortgageApplicationServiceTests.cs`, `Applications/Fakes.cs`, 2 tests in `AffordabilityOptionsTests.cs`; `tests/MortgageApplications.IntegrationTests/` (`ApiFactory`, `Http`, 9 test classes). `HarnessSmokeTests.cs` removed.

## Tests

### Proposal (test-designer P03-04, approved by Alessandro P03-05)

Alessandro replied "approved" (P03-05) to the recommended selection and the answers to A1–A12 below.

The `test-designer` proposed 19 unit rows (U1–U19) and 46 integration rows (I1–I46; I41 is a "not tested on purpose" note). Full list in its report (P03-04). The lead agent checked its golden numbers against rules §8: E1, E2 and E18 match exactly. It did not open the Phase 2 test files, so overlap was judged by the lead agent.

**Recommended selection** (all `must`, plus the `nice` rows marked +):
- **Unit (Core):** U1, U2, U3 (use case: owner and time from the caller/clock, E1 and E2 stored, Declined not an error); U5, U6, U7, U8 (owner scoping and wrong-application lookups return not found, nothing saved); U10–U15 (rule set: baseline, bound parity with `InputGuard` in one theory, minimum-loan limit, all errors at once, no echoed values); U17 (`RulesVersion` 16 vs 17 characters).
- **Integration:** I1–I6 (create, scale 2 in raw JSON, round trip without `ownerId`, E1 / E2 / E18 assessments end to end); + I7 (two POSTs = two assessments); I9–I15 as theories (every rules §1 bound and the minimum loan); I16, I17, I18 (`{}` only) (missing and null fields); I20, I21 (Core rules don't run while an attribute error exists); I22, I23, I24, I26 (malformed JSON stays 400, unknown property, number as string, 415); I28 (no echo); I29–I33, I35, I37 (404s, cross-owner with equal 404 bodies, no owner in any body); I38, I39 (500 without exception text in Development and Production); I40 (one shared ProblemDetails assert helper, not extra tests); I42 + I43 (only "section missing") (startup fails on bad options); + I44 (`/health`, replaces `HarnessSmokeTests`); + I45, I46 (OpenAPI documents MaxBorrowing; OpenAPI and Scalar return 404 in Production).
- **Dropped:** U4, U9, U16, U18, U19, I8, I19, I25, I27, I34, I36, I18's empty-body and `null` rows. Reason: each repeats a kept case or a Phase 2 test (U16 = Phase 2 O1; U18 = Phase 2 entity guard tests), or is low value (I27).

About 30 unit test methods and 35 integration test methods once theories are counted as one each.

**Ambiguities (A1–A12) with the lead agent's recommendation:**

| # | Question | Recommendation |
|---|---|---|
| A1 | Error key casing | camelCase (`annualIncome`), the same as the JSON. The `IValidatableObject` side uses the same names; I16 + I9–I15 fail if the two sources disagree. If `AddValidation` turns out to emit PascalCase, I'll stop and tell you rather than bend the tests. |
| A2 | Malformed ID | `{id:guid}` route constraint, so `not-a-guid` gives 404 (no route), not 400. |
| A3 | JSON-level failures | Plain ProblemDetails (no `errors` dictionary). Tests assert status, content type and `traceId` only. |
| A4 | 500 body in Development | Generic in every environment (I38 and I39). Nothing is lost: details go to logs in Phase 5. |
| A5 | Core rules skipped while an attribute error exists | Accept the documented framework order. |
| A6 | Loan rule when property or deposit is itself invalid | Skip the minimum-loan rule unless both fields are individually valid. The client then gets one error per real problem. `InputGuard` keeps throwing as today. |
| A7 | Enum and rate JSON | Enums as strings (`"Approved"`, `"Completed"`) via `JsonStringEnumConverter`; check names `"LTV"`, `"DTI"`, `"LTI"` as in Phase 2; `assessmentRate` asserted by value (0.06), not text. |
| A8 | Use-case outcomes | Not found = `null`. `Create` relies on the entity guard throwing (the API validates first). |
| A9 | Empty body / `null` | Dropped from the selection (framework behaviour). |
| A10 | `Location` absolute or relative | Relative; tests assert the path. |
| A11 | Test clock | A small hand-written `TimeProvider` subclass, **no new package** (`FakeTimeProvider` would need approval). |
| A12 | Shared in-memory database | One open connection per test class, shared with the derived factory used for the other owner (I35), so the cross-owner test can't pass on an empty database. |

### Results

**Red** (commit `e604a56`, stubs throw `NotImplementedException`; integration endpoints not mapped yet):
- unit `dotnet test --project tests/MortgageApplications.UnitTests`: total 258, failed 42, succeeded 216;
- integration `dotnet test --project tests/MortgageApplications.IntegrationTests`: total 74, failed 70, succeeded 4.
The 4 integration passes were `/health` (exists since Phase 0) and the three "404 in Production" rows (nothing mapped yet). The 16-character `RulesVersion` unit test also passed already (no length rule existed). Every other new test failed on a real assertion or a `NotImplementedException`.

**Green:** Core `90136a9` (unit 258/258), Infrastructure `14a6c9c`, Api `654281b`, naming fix `371d9f9`, migration `bf2920c`. Then all 332 pass (258 unit + 74 integration).

**Verify** (`./scripts/verify.sh`, full, 2026-11-14, on `phase-03-api` at `bf2920c`; only docs uncommitted):
```
==> build (Release, warnings as errors)
Build succeeded.
    0 Warning(s)
    0 Error(s)
==> all tests
Test run summary: Passed!
  total: 332
  failed: 0
  succeeded: 332
  skipped: 0
  duration: 3s 361ms
==> hook regression tests
hook tests: 91 passed, 0 failed
==> format check
VERIFY PASSED (full)
```

**After the gate fixes** (commits `8a2e80e` tests, `bb24cff` fixes, `5f91df6` naming): the tightened and new tests were run against the code before the fixes: integration total 89, failed 8 (3 × `ToString` redaction, 2 × C# names in messages, 1 × read-only checks after a load, 2 × duplicate JSON property). The tightened F1, F2, F4, Production-path and HTML-client tests passed already: the behaviour was right, the old tests just didn't prove it. After the fixes: 347 of 347 (258 unit + 89 integration). Final full verify: see "Verify (final)" below.

What each new test class proves:

| Class | Cases | Proves |
|---|---|---|
| `ApplicationInputRulesTests` (unit) | U10–U15, A6 | Every rules §1 bound, with the same field as `InputGuard`; minimum loan exactly 1,000 passes; one error per mistake; all errors at once; no value in any message |
| `MortgageApplicationServiceTests` (unit) | U1–U3, U5–U8 | Owner and time come from the caller and the clock; E1/E2 stored with rules version and rate; another owner or the wrong application gives `null` and saves nothing |
| `AffordabilityOptionsTests` (unit, 2 added) | U17 | `RulesVersion` 16 characters passes, 17 fails without the value |
| `CreateApplicationTests` | I1–I3, I37 | 201 + `Location` + body; GET returns the same body; 2 dp in raw JSON after the SQLite round trip; no owner in any body |
| `ValidationTests` | I9–I18, I20, I21, I28 + messages | Each bound as a 400 naming exactly one camelCase field; limits accepted; missing/null/`{}`; Core rules wait for attribute errors; no echoed value; attribute messages don't name the C# property |
| `JsonStrictnessTests` | I22–I24, I26 + duplicate key | Malformed JSON, unknown property, number as string and a duplicate property are 400 ProblemDetails without echo, and 415 for the wrong content type, in Development **and** Production; nothing saved |
| `AssessmentTests` | I4–I7 + F3 | Golden E1, E2 and E18 end to end (every output, checks, summary, rate, version, scale 2); the fetched assessment is byte-for-byte the POST body; two POSTs = two assessments; loaded checks are read-only |
| `NotFoundAndOwnerTests` | I29–I33, I35, I37 | Unknown, malformed, wrong-application and other-owner IDs all give the same 404 ProblemDetails; the other owner's host provably shares the database and its POST saves nothing; the owner still gets 200 |
| `ErrorHandlingTests` | I38, I39 + HTML client | A 500 in Development and Production (and for an HTML client) has no exception type, message, namespace or stack |
| `StartupTests` | I42–I46 | Invalid options, or a missing `Affordability` section with the rest of the configuration present, stop startup (message names the option, not the value); `/health`; OpenAPI documents MaxBorrowing; OpenAPI and Scalar are 404 in Production |
| `ApiRecordsTests` | security L1 | The request and response records' `ToString` print no input or output value |

**Manual smoke test** (2026-11-14, `dotnet run --project src/MortgageApplications.Api --launch-profile http`, Development, file database): the startup migration created the SQLite file; `POST /api/applications` (E1) → 201 with `Location`; `POST …/assessments` → 201 with the E1 golden values and `"limit":4.50`; `termYears` 50 → 400 with key `termYears`; unknown ID → 404 ProblemDetails with `traceId`; `/openapi/v1.json` → 200; `/scalar` → 302 (redirect to its UI). Run before the gate fixes, so the 400 message then still read "The field TermYears must be between 5 and 40." (fixed in `bb24cff`).

## Implementation notes

- **P3-D3 outcome: fallback (b) used.** Three real EF Core 10 errors, in order:
  1. `No suitable constructor was found for the type 'AffordabilityAssessment.Result#AffordabilityResult'` (the record's constructor takes the `Checks` collection);
  2. `Complex property '…Result#AffordabilityResult.Checks' is mapped to JSON but its containing type … is not. Map the root complex type to JSON. See https://github.com/dotnet/efcore/issues/36558.` (the docs-check risk, confirmed);
  3. after mapping the whole result to JSON: `The complex collection '…Checks' is of type 'IReadOnlyList<CheckResult>' which does not implement 'IList<CheckResult>'.`
  Changing Core's `Checks` to `IList<T>` would make the domain mutable for persistence's sake, so the result is now one JSON column through a value converter + value comparer (`AffordabilityResultJson`), as the approved fallback describes. Commit `14a6c9c`.
- **A1 outcome: camelCase, decided by Alessandro (P03-06, "camelCase (keep fix)").** `AddValidation` in .NET 10 names attribute errors by the C# property (`TermYears`, `AnnualIncome`); our Core-rule errors were camelCase. `ValidationErrorKeys` renames every `errors` key to its JSON name through `AddProblemDetails(o => o.CustomizeProblemDetails = …)`; with it, all 400 key tests pass. Alternative rejected: PascalCase everywhere (keys would not match the JSON the client sent).
- **Migration.** `dotnet-ef` was missing at first (P03-02); until the migration existed, database tests failed with `SQLite Error 1: 'no such table: MortgageApplications'`. Alessandro then installed it (repo-root `dotnet-tools.json`, 10.0.15). `InitialCreate` was generated with `dotnet ef migrations add InitialCreate --project src/MortgageApplications.Infrastructure --startup-project src/MortgageApplications.Infrastructure --output-dir Persistence/Migrations` (the design-time factory means the API doesn't have to start). The generated file failed the build on IDE0161 (block namespace), so `.editorconfig` now marks `src/**/Migrations/*.cs` as generated code; the generated files were not edited.
- **.NET 10 diagnostics.** `UseExceptionHandler` is configured with `SuppressDiagnosticsCallback = context => context.Exception is BadHttpRequestException`, so real 500s are still logged by the framework (Phase 5 adds the allowlisted log).

## Security gate

**`security-auditor` (P03-07): PASS.** No high or critical findings. Diff at `a446c64`.

| Tool | Command | Result |
|---|---|---|
| Gitleaks (history) | `gitleaks git --no-banner --redact -v` | 49 commits, no leaks, exit 0 |
| Gitleaks (tree) | `gitleaks dir --no-banner --redact .` | no leaks, exit 0; local `*.db` files are gitignored and untracked |
| Semgrep 1.185.0 | `semgrep scan --config p/csharp --config p/secrets --error --quiet --metrics=off --json` (whole repo, and with `--baseline-commit main`) | 0 results each (counted from the JSON), exit 0; one `PartialParsing` warning on the primary constructor in `MortgageApplicationService.cs:10` (parser limit; file reviewed by hand) |
| Vulnerable packages | `dotnet list MortgageApplications.slnx package --vulnerable --include-transitive` | none in all 5 projects (nuget.org only) |

| # | Finding | Severity | Resolution |
|---|---|---|---|
| L1 | API request/response records print every input in the default `ToString` | Low (real once Phase 5 logs) | **Fixed** in `bb24cff`: allowlist-only `ToString`; `ApiRecordsTests` (3 tests, red before the fix) |
| L2 | JSON-strictness and 415 tests ran only in Development (Production writes the body through another path) | Low | **Fixed** in `8a2e80e`: every case runs in both environments (passed already; the gap was coverage) |
| L3 | No NuGet lock files or package-source mapping | Low (existed before) | **Deferred to Phase 8** (already planned as D12); it changes CI and config, so it needs Alessandro's approval |
| I1 | Connection string in the base `appsettings.json` | Info | **Deferred to Phase 4** (deploy/migrations ADR). Fails closed today: outside Development nothing creates tables |
| I2 | Duplicate JSON keys accepted (last wins; validation still runs on the final value) | Info | **Fixed** in `bb24cff`: `AllowDuplicateProperties = false` (checked on Learn: exists in .NET 10, default `true`, `false` throws `JsonException`); 2 tests, red before |
| I3 | No test with `Accept: text/html` on a 500 | Info | **Test added** in `8a2e80e`: empty 500, no exception text (passed already) |
| — | `DevelopmentCurrentUser` in every environment, no `RequireAuthorization` | Accepted (Phase 7) | Builds before Phase 7 are not deployed or submitted (`PRODUCTION-LAYERS.md`). Same as reviewer F6 |

Also noted for Phase 8 (from Alessandro's own `dotnet tool install` output, 2026-11-14): "Skipping NuGet package signature verification." Not investigated; to be checked with the lock-file work.

`/security-review` on the PR: see "Review gate" and the PR comment.

## Review gate

**`dotnet-reviewer` (P03-08):** 1 medium, 6 low, no critical or high. It ran both test projects (74/74 and 258/258 at the time) and a throwaway probe on the real `DbContext` (in its scratchpad, not committed): `HasPendingModelChanges()` was false; money columns are stored as `"85000.0"` and read back as `85000.00`; the result JSON keeps scale 2; `decimal` comparison and `OrderBy` still translate with the converter; a tracked load stays `Unchanged`. It could not run `dotnet ef migrations has-pending-model-changes` (refused by the permission rules); the probe covered the same question.

| # | Finding | Resolution |
|---|---|---|
| F1 (medium) | The assessment round trip compared JSON by value (`JsonNode.DeepEquals` treats `240000.0` = `240000.00`), so a dropped trailing zero after storage would pass every test | **Fixed** in `8a2e80e`: raw-text equality of POST and GET bodies; raw E18 values checked after the reload |
| F2 | Cross-owner test didn't prove the other host shares the database, nor that the foreign POST saves nothing against EF | **Fixed** in `8a2e80e` |
| F3 | A loaded result's `Checks` was a mutable `List`, while the comparer assumes immutability | **Fixed** in `bb24cff`: read-only copy on load; test red before |
| F4 | The "missing section" test cleared all configuration | **Fixed** in `8a2e80e`: clears, then re-adds the connection string |
| F5 | Same as security L2 | **Fixed** (see above) |
| F6 | `DevelopmentCurrentUser` in every environment | **Accepted until Phase 7**, recorded in the PR's security section |
| F7 | With the JSON-column fallback, `RulesVersion` has no column, so "fits its column" is no longer the reason for the 16-character bound | **Fixed** in `bb24cff`: documented as a sanity bound; the rule and U17 stay |

**`spec-checker` (P03-09):** at `a446c64`, **26 met, 9 partial, 20 missing** (Phase 2: 1 / 28 / 26). Moved to met: R1–R18, R21–R24, R26, R27, R29. No disagreement between code and spec. Its notes: R21 ("good use of the framework") is a judgement call; R22's one-to-many has a foreign key and cascade delete but no navigation properties and no direct test; it did not run the suite. It reported `requests.http` missing: it was added after its snapshot, in `eb21d5f`. The new table is in `docs/PROGRESS.md`.

**`/security-review` (P03-10) on PR #6:** the finder sub-task reported **no findings** at confidence 8 or above, so no false-positive filter sub-tasks ran. It checked injection, owner scoping, mass assignment, validation, deserialization, error exposure, personal data in logs, secrets and dependencies.

**`/code-review` (high, inline) on PR #6:** 7 findings, all low; none is a correctness bug. None changed in this PR, each for a stated reason:

| # | Finding | Resolution |
|---|---|---|
| 1 (altitude) | `ProblemExceptionHandler` special-cases `BadHttpRequestException`; `RouteHandlerOptions.ThrowOnBadRequest = false` everywhere would give one error path | **Not changed:** both paths are now tested in both environments; changing the framework default also changes Development diagnostics. Candidate for the Phase 9 clean-up |
| 2 (reuse) | `[Range(5, 40)]` repeats the Core term bounds | **Not changed:** the attribute is part of the approved P3-D2; the HTTP bound tests (I13) catch a drift at the boundary |
| 3 (conventions) | `.claude/rules/backend.md` says reads "project to DTOs"; the repository returns entities | **Decision for Alessandro:** P3-D1 chose entities through a Core port (DTOs live in Api). Either reword the rule or accept the exception |
| 4 (simplification) | camelCase conversion in both `Validate` and `ValidationErrorKeys` | **Not changed:** harmless; the request stays correct on its own if the hook ever changes |
| 5 (simplification) | 2 dp restored both in the entity constructor and by `MinimumScaleTwoConverter` | **Not changed:** Phase 9 clean-up (removing the converter needs a model-snapshot check) |
| 6 (efficiency) | The result comparer serialises on every compare | **Not changed:** reads are no-tracking today; revisit in Phase 6, where the worker tracks assessments |
| 7 (efficiency) | `JsonStrictnessTests` builds a host per theory row (suite ~3 s → ~8 s) | **Not changed:** Phase 9 measures first |

## AI output corrected or rejected (REAL cases only)

- The plan said SQLite can't compare or sort `decimal`. The docs check showed EF translates those operations (`ef_compare`); the plan was corrected before any code.
- P3-D3 option (a) was recommended as likely to work. EF 10 rejected it (errors above), and the approved fallback was used.
- The lead agent's assessment test claimed to prove scale 2 after the round trip but compared numbers by value. The `dotnet-reviewer` caught it (F1); fixed.
- The lead agent ran the JSON-strictness tests only in Development, the path its own exception handler covers. Both the `security-auditor` (L2) and the `dotnet-reviewer` (F5) caught it independently; fixed.
- The lead agent's first `sed` rename used `\b`, which BSD `sed` on macOS ignores; the format check caught it and the rename was redone with `[[:<:]]…[[:>:]]`.

## Optimisations considered

None measured in this phase (Phase 9 measures first). Noted only: reads use `AsNoTracking`; there is an index on `OwnerId` and on `ApplicationId`.

## Open issues

- `security-auditor` S6 (Phase 2): version 7 IDs reveal their creation time. Accepted: every lookup is owner-scoped and returns 404, and the 74 random bits make IDs impossible to enumerate. To mention in the README (Phase 10).
- Phase 4: connection string location and the startup-migration ADR (security I1).
- Phase 5: `[LoggerMessage]` with the allowlist; real 500s are logged by the framework today (`SuppressDiagnosticsCallback`), and the log-capture test must cover them.
- Phase 6: the POST becomes 202; `AssessmentResponse.Result` is already null-able for Pending.
- Phase 7: replace `DevelopmentCurrentUser`, add `RequireAuthorization` (reviewer F6).
- Phase 8: NuGet lock files and source mapping (security L3); check NuGet package signature verification on macOS.
- Docs check not done for `TimeProvider` and Scalar's `MapScalarApiReference` (both used as in their standard samples; Scalar's docs are not on Learn).
- R22 has no direct test of cascade delete (no delete endpoint exists); left as is.

## Time spent (approx.)

One working session on 2026-11-14: about 1 hour of wall-clock time, from the first scratchpad file (NuGet version check, 16:29) to the evidence commit (about 17:30), including waits for Alessandro. Not measured more precisely.

## Human review (Alessandro writes this; the agent leaves it empty)
