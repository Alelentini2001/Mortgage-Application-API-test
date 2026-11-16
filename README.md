# Mortgage Application API

ASP.NET Core (.NET 10) JSON API for submitting mortgage applications, retrieving applications, and running asynchronous affordability assessments (loan-to-value, debt-to-income, loan-to-income, decision, maximum borrowing estimate, and structured explanation).

Built as a backend-only technical exercise for **Canada Life**. Engineer of record: **Alessandro Lentini**.

---

## Architecture & Project Layout

```text
MortgageApplications.slnx
├── src/
│   ├── MortgageApplications.Core/           # Domain entities, AffordabilityCalculator, ApplicationInputRules,
│   │                                        # MortgageApplicationService, ports (no ASP.NET Core or EF Core refs)
│   ├── MortgageApplications.Infrastructure/ # EF Core 10 + SQLite persistence, configurations, migrations
│   └── MortgageApplications.Api/            # Minimal APIs, ES256 JWT auth, background worker, rate limiting,
│                                            # security headers, OpenTelemetry, request/response DTOs
└── tests/
    ├── MortgageApplications.UnitTests/        # 275 unit + property tests (golden examples E1–E19, boundaries)
    └── MortgageApplications.IntegrationTests/ # 311 WebApplicationFactory + SQLite + OTLP + query-plan tests
```

### HTTP Endpoints

| Method & Route | Auth | Status Codes | Purpose |
|---|---|---|---|
| `POST /api/applications` | Bearer (`sub`) | `201 Created` (`Location`), `400`, `401`, `429` | Validate and persist a new mortgage application owned by the caller's `sub` claim |
| `GET /api/applications/{id:guid}` | Bearer (`sub`) | `200 OK`, `401`, `404` | Retrieve an application owned by the caller (another user's ID returns `404`) |
| `POST /api/applications/{id:guid}/assessments` | Bearer (`sub`) | `202 Accepted` (`Location`), `401`, `404`, `429`, `503` | Persist a `Pending` assessment and enqueue it for background calculation |
| `GET /api/applications/{id:guid}/assessments/{assessmentId:guid}` | Bearer (`sub`) | `200 OK`, `401`, `404` | Poll assessment state (`Pending`, `Completed` with `result`, or `Failed` with `failedAt`) |
| `GET /health` | Anonymous | `200 OK`, `503` | Readiness/liveness check (includes SQLite connectivity check) |
| `POST /dev/token` | Anonymous (`Development` + `DevAuth:Enabled=true` only) | `200 OK`, `400`, `429` (`404` outside `Development`) | Issue a 15-minute ES256 JWT signed by an ephemeral in-memory key pair for local runs and demos |

In `Development` only, the OpenAPI 3.1 document is served at `/openapi/v1.json` and the interactive **Scalar** API reference UI at `/scalar`.

---

## Prerequisites & Setup

- **.NET 10 SDK** (`10.0.100` or newer; pinned in [`global.json`](global.json) with `rollForward: latestFeature`).
- **Docker & Docker Compose** (optional; required only for containerised runs and the opt-in Aspire observability dashboard).
- **Optional security CLI tools** for the full local verification script (`./scripts/verify.sh`): [`gitleaks`](https://github.com/gitleaks/gitleaks) and [`semgrep`](https://semgrep.dev/).

Clone the repository and restore locked dependencies:

```bash
git clone https://github.com/Alelentini2001/Mortgage-Application-API-test.git
cd Mortgage-Application-API-test
dotnet restore MortgageApplications.slnx --locked-mode
```

---

## How to Run the Application

### Option 1: Local `.NET` CLI (`http://localhost:5227`)

```bash
dotnet run --project src/MortgageApplications.Api
```

- Uses the `http` launch profile in [`src/MortgageApplications.Api/Properties/launchSettings.json`](src/MortgageApplications.Api/Properties/launchSettings.json), which sets `ASPNETCORE_ENVIRONMENT=Development` and `DevAuth__Enabled=true`.
- On startup in `Development`, EF Core applies migrations automatically (`DatabaseMigration.MigrateDatabaseAsync`) and enables `PRAGMA journal_mode=WAL;` on the local SQLite file (`mortgage-applications.db`).
- Interactive API docs: `http://localhost:5227/scalar` (OpenAPI spec at `http://localhost:5227/openapi/v1.json`).

### Option 2: Docker Compose (`http://127.0.0.1:8080`)

```bash
docker compose up --build
```

- Builds the multi-stage [`Dockerfile`](Dockerfile) (`sdk:10.0` → `aspnet:10.0` pinned by multi-arch `@sha256` digest, running as non-root `USER $APP_UID` with `init: true`) and mounts a named volume `data` at `/app/data`.
- Bound to loopback only (`127.0.0.1:8080:8080`) because `DevAuth__Enabled=true` is enabled for the demo ([ADR 0002](docs/adr/0002-container-runtime-and-startup-migrations.md), [ADR 0004](docs/adr/0004-authentication-and-authorisation.md)).
- **Resetting the SQLite volume:** if an older container created the `data` volume with a different UID/owner, remove the volume with:
  ```bash
  docker compose down -v
  ```
- **Running the image outside Compose:** always pass `--init` (`docker run --init ...`) so an init process runs as PID 1; without it, `.NET` as PID 1 ignores its own abort signal on an unhandled startup exception and hangs ([ADR 0002](docs/adr/0002-container-runtime-and-startup-migrations.md)).

### Option 3: Docker Compose + OpenTelemetry Aspire Dashboard (`http://127.0.0.1:18888`)

```bash
docker compose -f compose.yaml -f compose.observability.yaml up --build
```

- Starts the API alongside `mcr.microsoft.com/aspire/dashboard:13.6.2` and sets `OTEL_EXPORTER_OTLP_ENDPOINT=http://aspire-dashboard:18889`.
- Open the `http://localhost:18888/login?t=...` URL printed in the `aspire-dashboard` container logs to inspect structured logs, distributed traces (including the custom `assessment.process` span), and metrics (`mortgage_applications.assessments` counter and `mortgage_applications.assessment.duration` histogram).

### Quick-Start API Walkthrough (`curl`)

Set `BASE_URL=http://localhost:5227` (for `dotnet run`) or `BASE_URL=http://127.0.0.1:8080` (for `docker compose up`), or run the ready-made requests in [`src/MortgageApplications.Api/requests.http`](src/MortgageApplications.Api/requests.http) from VS Code REST Client, JetBrains Rider, or Visual Studio:

```bash
BASE_URL=http://localhost:5227

# 1. Mint a 15-minute development ES256 bearer token for subject "alice"
TOKEN=$(curl -s -X POST "$BASE_URL/dev/token" \
  -H "Content-Type: application/json" \
  -d '{"subject":"alice"}' | jq -r '.token')

# 2. Create a mortgage application (golden example E1 -> 201 Created)
APP_ID=$(curl -s -X POST "$BASE_URL/api/applications" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "annualIncome": 85000.00,
    "monthlyExpenses": 900.00,
    "depositAmount": 60000.00,
    "propertyValue": 300000.00,
    "termYears": 30
  }' | jq -r '.id')

# 3. Request an affordability assessment (202 Accepted, status = Pending)
ASSESSMENT_ID=$(curl -s -X POST "$BASE_URL/api/applications/$APP_ID/assessments" \
  -H "Authorization: Bearer $TOKEN" | jq -r '.id')

# 4. Poll the assessment (200 OK with status = Completed and result.decision = Approved)
curl -s "$BASE_URL/api/applications/$APP_ID/assessments/$ASSESSMENT_ID" \
  -H "Authorization: Bearer $TOKEN" | jq .
```

### Production Authentication Configuration

Outside `Development`, `POST /dev/token` is not registered (and startup fails if `DevAuth:Enabled=true` is set or if `Jwt:SigningPublicKey` is missing). Supply the issuer's ECDSA P-256 (`ES256`) public key in PEM format via environment variables (`Jwt__SigningPublicKey`, `Jwt__Issuer`, `Jwt__Audience`). See [ADR 0004](docs/adr/0004-authentication-and-authorisation.md) for how an external identity provider such as Microsoft Entra ID plugs in.

---

## How to Run Tests

Tests run on **Microsoft.Testing.Platform** (configured in [`global.json`](global.json)) using **xUnit v3**:

```bash
# Full verification gate: locked restore, build (warnings as errors),
# all unit + integration tests, dotnet format check, hook tests, gitleaks, semgrep, NuGet audit
./scripts/verify.sh

# Fast gate: build + unit tests only (~3 seconds)
./scripts/verify.sh --fast

# Run all 586 tests (275 unit + 311 integration) directly via dotnet test
dotnet test --solution MortgageApplications.slnx

# Run a single test class or method (xUnit v3 on Microsoft.Testing.Platform uses --filter-class / --filter-method)
dotnet test --project tests/MortgageApplications.UnitTests -- --filter-class "*AffordabilityCalculatorTests"
dotnet test --project tests/MortgageApplications.IntegrationTests -- --filter-class "*DatabaseQueryPlanTests"
```

---

## Affordability Assessment Logic & Assumptions

Full specification and 18 worked golden examples (`E1`–`E18`): [`docs/spec/affordability-rules.md`](docs/spec/affordability-rules.md) and [ADR 0001](docs/adr/0001-affordability-assessment-model.md).

### Input Preconditions (HTTP `400 ValidationProblemDetails` vs. `Declined`)

Input validation is strictly separated from the underwriting decision. Invalid inputs are rejected at the HTTP boundary with `400 ValidationProblemDetails` before reaching the calculator (`ApplicationInputRules` + `AffordabilityCalculator` guard clauses); a `Declined` outcome is a valid `200 OK` assessment result, not an error:

| Field | Validated Range | Precision |
|---|---|---|
| `annualIncome` | `1.00` to `10,000,000.00` | At most 2 decimal places |
| `monthlyExpenses` | `0.00` to `1,000,000.00` | At most 2 decimal places |
| `depositAmount` | `>= 0.00` and **`propertyValue - depositAmount >= 1,000.00`** (minimum loan `1,000`) | At most 2 decimal places |
| `propertyValue` | `1,000.00` to `100,000,000.00` | At most 2 decimal places |
| `termYears` | `5` to `40` whole years | Integer |

### Formulas (`decimal` Arithmetic Only)

All calculations use `System.Decimal` (never `double`), with exponentiation by squaring for $(1 + r)^n$:

- **Loan amount:** $\text{Loan} = \text{PropertyValue} - \text{DepositAmount}$
- **Loan-to-value (LTV %):** $\text{LTV} = \frac{\text{Loan}}{\text{PropertyValue}} \times 100$
- **Monthly repayment (`MonthlyPayment`):** standard capital-and-interest annuity at the configured annual assessment rate (`AssessmentRate = 0.06`, i.e. $6\%$, so $r = 0.06 / 12$ and $n = \text{TermYears} \times 12$):
  $$\text{Annuity} = 1 - (1 + r)^{-n}, \qquad \text{MonthlyPayment} = \frac{\text{Loan} \times r}{\text{Annuity}}$$
- **Debt-to-income (DTI %):** $\text{DTI} = \frac{\text{MonthlyExpenses} + \text{MonthlyPayment}}{\text{AnnualIncome} / 12} \times 100$
- **Loan-to-income multiple (LTI):** $\text{LTI} = \frac{\text{Loan}}{\text{AnnualIncome}}$
- **Income-based maximum borrowing (`MaxBorrowing`):**
  $$\text{LtiCap} = 4.5 \times \text{AnnualIncome}, \qquad \text{AffordablePay} = 0.45 \times \frac{\text{AnnualIncome}}{12} - \text{MonthlyExpenses}$$
  $$\text{DtiCap} = \frac{\text{AffordablePay} \times \text{Annuity}}{r}, \qquad \text{MaxBorrowing} = \max\bigl(0,\ \min(\text{LtiCap},\ \text{DtiCap})\bigr)$$

### Three Independent Checks & Rounding Rules

Every assessment evaluates and reports all three checks in order (`LTV`, `DTI`, `LTI`), even when an earlier check fails:

| Check | Rule (v1.0) | Default Limit |
|---|---|---|
| `LTV` | $\text{LTV \%} \le \text{MaxLoanToValuePercent}$ | `90.00%` |
| `DTI` | $\text{DTI \%} \le \text{MaxDebtToIncomePercent}$ | `45.00%` |
| `LTI` | $\text{LTI} \le \text{MaxLoanToIncomeMultiple}$ | `4.50×` |

- **Decision:** `Approved` if all three checks pass (limits are inclusive); otherwise `Declined`.
- **Full-precision comparison, single end-rounding:** comparisons run on unrounded `decimal` values so the invariant $\text{Approved} \implies \text{Loan} \le \text{MaxBorrowing}$ holds exactly. Output numbers are rounded once at the end to `2 dp` using `MidpointRounding.AwayFromZero` and normalised to scale `2` (`80.00`). If a check fails before rounding but rounds to its limit (e.g. raw DTI `45.000128%` → `45.00%`), the explanation appends `before rounding`.

### Explicit Assumptions

1. **Single applicant, gross annual income**, and currency-agnostic arithmetic (examples use `GBP`).
2. **Repayment mortgage** (capital + interest) at a single fixed $6\%$ annual assessment rate over the full term (no product rate catalogue or introductory period).
3. **`MonthlyExpenses` represent existing monthly commitments** excluding the new mortgage payment; DTI adds the new mortgage payment to them.
4. **`MaxBorrowing` is income-based** (how much the applicant's income and expenses support at the $45\%$ DTI and $4.5\times$ LTI limits, independent of the property's LTV cap).
5. **Minimum loan of `1,000`** (`PropertyValue - DepositAmount >= 1,000`) to prevent degenerate micro-loan approvals.
6. **Auditability:** each persisted assessment stores the `RulesVersion` (`"1.0"`), `AssessmentRate`, and check limits used to compute it.

---

## Design & Reflection

### 1. Key Design Decisions

1. **Pure, deterministic domain core (`MortgageApplications.Core`) with `decimal` arithmetic and separate validation vs. decision ([ADR 0001](docs/adr/0001-affordability-assessment-model.md)):**
   - `MortgageApplications.Core` has zero references to ASP.NET Core or EF Core. `AffordabilityCalculator` is a pure function of `(applicationInputs, options)` with no clock, database, or configuration access, making it trivially testable across 18 worked golden examples (`E1`–`E18`) and property-based invariant checks.
   - Replacing a composite `Loan <= MaxBorrowing` check with three orthogonal checks (`LTV`, `DTI`, `LTI`) eliminated duplicate DTI failure reasons while preserving identical `Approved`/`Declined` outcomes and the $\text{Approved} \implies \text{Loan} \le \text{MaxBorrowing}$ invariant.
2. **Save-before-enqueue asynchronous assessment pipeline with bounded backpressure and startup recovery ([ADR 0003](docs/adr/0003-background-assessment-queue.md)):**
   - `POST /api/applications/{id}/assessments` saves the `AffordabilityAssessment` row in `Pending` status **before** writing the assessment ID to the bounded `System.Threading.Channels` queue (`IAssessmentQueue`) and returning `202 Accepted`.
   - If the queue is full (`Assessments:QueueCapacity`, default `100`), `ChannelAssessmentQueue` waits up to `Assessments:EnqueueTimeoutSeconds` (default `10 s`) before returning `503 Service Unavailable`; even then, or if the process crashes mid-flight, the `Pending` row is already durable in SQLite and `AssessmentWorker`'s startup sweep re-enqueues it on restart (never silently dropping work).
   - `EfMortgageApplicationRepository.FindForProcessingAsync` projects only the 5 scalar application inputs (`AsNoTracking`) and updates only the modified columns on the tracked `AffordabilityAssessment` entity.
3. **Fail-closed ES256 authentication, owner-scoped persistence, and test-enforced zero-PII telemetry ([ADR 0004](docs/adr/0004-authentication-and-authorisation.md)):**
   - A global `RequireAuthenticatedUser` fallback policy protects all endpoints by default, validating ES256 JWT signatures, issuer, audience, and expiry. Every application and assessment query is scoped by `Id = @id AND OwnerId = @owner` where `OwnerId` comes exclusively from `ICurrentUser` (`sub` claim); cross-user access returns the same `404` as an unknown ID so existence is never leaked.
   - Because financial figures are personal data, logs, OpenTelemetry span attributes, and metric tags follow a strict allowlist (application ID, assessment ID, decision, `RulesVersion`, HTTP status codes, and timings). [`TelemetryAllowlistTests`](tests/MortgageApplications.IntegrationTests/TelemetryAllowlistTests.cs) and [`OtlpExportTests`](tests/MortgageApplications.IntegrationTests/OtlpExportTests.cs) assert that no raw or formatted input/output numbers ever appear in logs, spans, or OTLP protobuf exports.

### 2. Scaling Consideration

**First bottleneck under significantly higher load:** SQLite's single-writer lock and the in-process `Channel<Guid>` assessment queue (`ChannelAssessmentQueue` + `AssessmentWorker`), both of which bind write throughput and background processing to a single container instance.

**First change to make and why:**
1. **Swap SQLite for Azure SQL (or PostgreSQL) and swap `IAssessmentQueue` for a durable broker (Azure Service Bus):** `MortgageApplicationDbContext` and `IAssessmentQueue` were designed as clean boundaries (`Infrastructure` and a `Core` port), so neither `MortgageApplications.Core` nor the HTTP endpoints change.
2. **Add multi-worker concurrency guards (`RowVersion` + `AttemptCount`):** with multiple stateless API and worker replicas competing on a shared queue (`Azure Container Apps`), add an optimistic concurrency token (`RowVersion`) on `AffordabilityAssessment` so `Status = Pending → Completed/Failed` is atomic across competing consumers, plus an `AttemptCount` column / dead-letter policy so a poison message cannot loop indefinitely ([ADR 0003](docs/adr/0003-background-assessment-queue.md), [`PRODUCTION-LAYERS.md`](docs/layers/PRODUCTION-LAYERS.md) "Scaling").

### 3. Trade-offs (Kept Simple or Left Out)

- **Single-applicant, fixed-rate underwriting model:** one applicant, one fixed $6\%$ stress rate, no credit bureau check, no product catalogue, and an income-based `MaxBorrowing` that deliberately ignores property LTV so the property check (`LTV`) and borrower capacity (`MaxBorrowing`) remain distinct signals ([ADR 0001](docs/adr/0001-affordability-assessment-model.md)).
- **SQLite + in-process `Channel<Guid>` instead of external infrastructure:** keeps local `dotnet run`, `docker compose up --build`, and CI completely self-contained with zero cloud dependencies or paid services (`PRODUCTION-LAYERS.md` "Cost").
- **Startup-only recovery sweep and no `Idempotency-Key` header:** each `POST /api/applications/{id}/assessments` creates a new assessment snapshot, and transient DB failures during worker processing wait for the next startup sweep rather than running a periodic background polling timer.
- **Development token issuer (`POST /dev/token`) instead of a live OIDC tenant:** lets interviewers run the full authenticated flow locally with zero external account setup while keeping the production `Jwt:SigningPublicKey` validation path tested and fail-closed ([ADR 0004](docs/adr/0004-authentication-and-authorisation.md)).

### 4. Next Steps (With More Time)

1. **`Idempotency-Key` on `POST /api/applications` and `POST .../assessments`** stored with a short TTL so client network retries never create duplicate applications or assessments.
2. **Transactional outbox + Azure Service Bus + `RowVersion` / `AttemptCount`** for horizontal worker scale-out and dead-letter handling.
3. **External OIDC authority integration (Microsoft Entra ID)** keying ownership on `oid` + `tid` (immutable object + tenant IDs rather than pairwise `sub`) and rotating signing keys via OIDC discovery.
4. **EF Core migration bundle deployment job** (`dotnet ef migrations bundle`) executed as an init/release step before container rollout instead of runtime migrations ([ADR 0002](docs/adr/0002-container-runtime-and-startup-migrations.md)).
5. **Data retention & GDPR erasure endpoint** (`DELETE /api/applications/{id}` + scheduled retention purge + Azure SQL TDE at rest).

---

## AI-Assisted Engineering & Cross-Environment Portability

This repository was built using a structured, human-gated AI engineering harness documented in [`AGENTS.md`](AGENTS.md), [`docs/layers/PRODUCTION-LAYERS.md`](docs/layers/PRODUCTION-LAYERS.md), [`docs/PROGRESS.md`](docs/PROGRESS.md), [`docs/ai/DESIGN-JOURNAL.md`](docs/ai/DESIGN-JOURNAL.md), and the verbatim prompt log [`docs/ai/PROMPTS.md`](docs/ai/PROMPTS.md):

- **Phase loop with human checkpoints:** every phase (`Phase 00`–`Phase 11`) stopped at **Checkpoint A** (plan & trade-off approval) and **Checkpoint B** (PR review & squash-merge approval) with test-first development (`test-designer`) and three mandatory read-only verification subagents (`security-auditor`, `dotnet-reviewer`, and `spec-checker`).
- **Cross-IDE & cross-model portability showcase:** Phases `00`–`07` (implementation) were executed in **Claude Code (Claude Opus 5.5 / Sonnet 4.6)**, and from Phase `07`'s verification gates onward (`Phases 07`–`11`) development was intentionally switched to **Google Antigravity IDE (Gemini 4 Argon)**. Because all project context, architectural boundaries, rules (`.claude/rules/`), skills (`.claude/skills/`), and subagent prompts (`.claude/agents/`) live in version-controlled Markdown in the repository, the second IDE and model picked up mid-phase and caught additional edge cases (`DevTokenRequest`/`DevTokenResponse` `ToString()` redaction and duplicate JSON `"sub"` keys in `P07-10`) without changing a single rule or workflow file.
