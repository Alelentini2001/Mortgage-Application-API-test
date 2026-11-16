---
paths:
  - "src/**/*.cs"
---
# Backend rules
- Minimal APIs: group endpoints with `MapGroup`, return `TypedResults`, declare result union types (`Results<Ok<T>, NotFound>`).
- Request/response DTOs are public `record`s in the Api project. Never expose entities.
- Validation via `AddValidation()` + DataAnnotations + `IValidatableObject`. Invalid input → 400 `ValidationProblemDetails`.
- Errors return ProblemDetails (`AddProblemDetails` + `IExceptionHandler`), with the trace ID.
- DI lifetimes: `DbContext` is scoped. Singletons (e.g. `BackgroundService`) create a scope per work item.
- Async all the way; accept and pass `CancellationToken`.
- Read queries use `AsNoTracking()` and map to DTOs at the API boundary (`P3-D1`).
- Log with `[LoggerMessage]` source-generated methods. Never log monetary values or personal data.
- Configuration via the options pattern (`IOptions<T>`), validated at startup.
