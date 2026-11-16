# Follow-Up Discussion & Live Demo Runbook (30-Minute Session)

Prepared for the **Canada Life** follow-up discussion (`docs/spec/ORIGINAL_SPEC.md` "Follow-Up Discussion", requirements `R45`–`R55`).
Companion 3-slide presentation: [`docs/presentation/SLIDES.md`](SLIDES.md).

---

## 1. Session Time Budget (`≤ 30 minutes`, `R50`)

| Window | Duration | Segment | Goal |
|---|---|---|---|
| `00:00–03:00` | `3 min` | **1. Fresh Clone, Build & Start (`R45`, `R46`)** | Clone repo into a clean folder, run `./scripts/verify.sh --fast` (or `docker compose up --build`), and start the API |
| `03:00–09:00` | `6 min` | **2. Live API Demo (`R47`)** | Walk through `src/MortgageApplications.Api/requests.http` (or `curl` + `/scalar`): auth, `Approved` E1, `Declined` E2, `400` validation, and `404` owner isolation |
| `09:00–14:00` | `5 min` | **3. Codebase Walkthrough (`R48`)** | Tour `Core` → `Infrastructure` → `Api` → `Tests` using the file & line pointers below |
| `14:00–20:00` | `6 min` | **4. 3-Slide Presentation (`R49`–`R55`)** | Present [`docs/presentation/SLIDES.md`](SLIDES.md) (~2 min per slide) after the demo to kick-start the architectural discussion |
| `20:00–30:00` | `10 min` | **5. Follow-Up Q&A & Deep Dive** | Discuss trade-offs, scaling (`Azure SQL` + `Service Bus` + `RowVersion`), auth (`Entra ID`), and AI-assisted workflow |

---

## 2. Recorded Fresh-Clone Rehearsals (`R45`, `R46`)

Executed on 2026-11-16 (`macOS arm64`, `.NET SDK 10.0.100`, Docker Compose) against `https://github.com/Alelentini2001/Mortgage-Application-API-test.git`:

| Step | Rehearsal 1: `.NET 10` CLI + Full `verify.sh` | Rehearsal 2: `docker compose up -d --build` |
|---|---|---|
| `git clone` | `1.08 s` | `1.02 s` |
| Build / Verify (`./scripts/verify.sh` full: 586 tests + 113 hook cases + format check, or `docker compose up -d --build`) | `44.14 s` | `11.00 s` |
| Startup to `GET /health` → `200 OK` | `4.49 s` (`http://localhost:5227`) | `0.91 s` (`http://127.0.0.1:8080`) |
| 8-step live API demo (`401`, `alice`/`bob` tokens, E1 `Approved`, E2 `Declined`, `400` validation, `404` cross-user) | `0.39 s` | `0.40 s` |
| **Total wall-clock time** | **`50.09 s`** | **`13.33 s`** |

---

## 3. Step-by-Step Live Demo Commands (`00:00–09:00`, `R45`–`R47`)

### Step A — Fresh Clone & Start (`00:00–03:00`)

```bash
# 1. Fresh clone into a clean directory
git clone https://github.com/Alelentini2001/Mortgage-Application-API-test.git mortgage-demo
cd mortgage-demo

# 2. Fast build + 275 unit tests (~3 s), or full ./scripts/verify.sh (~44 s for all 586 tests + 113 hook cases)
./scripts/verify.sh --fast

# 3a. Start via .NET CLI (http://localhost:5227, Scalar UI at http://localhost:5227/scalar)
dotnet run --project src/MortgageApplications.Api

# --- OR ---
# 3b. Start via Docker Compose (http://127.0.0.1:8080, or add -f compose.observability.yaml for Aspire at :18888)
docker compose up --build
```

### Step B — Live API Requests (`03:00–09:00`)

You can click **"Send Request"** on each block in [`src/MortgageApplications.Api/requests.http`](../../src/MortgageApplications.Api/requests.http) (VS Code REST Client, JetBrains Rider, or Visual Studio), or run the terminal snippet below in a second shell (`BASE_URL=http://localhost:5227` for `dotnet run`, or `BASE_URL=http://127.0.0.1:8080` for Docker Compose):

```bash
BASE_URL=http://localhost:5227

# 1. Liveness check (200 OK "Healthy") and unauthenticated call (401 Unauthorized + WWW-Authenticate: Bearer)
curl -i "$BASE_URL/health"
curl -i -X POST "$BASE_URL/api/applications" -H "Content-Type: application/json" -d '{}'

# 2. Mint 15-minute ES256 dev tokens for "alice" and "bob" (Development + DevAuth:Enabled=true only)
ALICE=$(curl -s -X POST "$BASE_URL/dev/token" -H "Content-Type: application/json" -d '{"subject":"alice"}' | jq -r '.token')
BOB=$(curl -s -X POST "$BASE_URL/dev/token" -H "Content-Type: application/json" -d '{"subject":"bob"}' | jq -r '.token')

# 3. Create application E1 as Alice -> 201 Created + Location header
APP1=$(curl -s -X POST "$BASE_URL/api/applications" \
  -H "Authorization: Bearer $ALICE" \
  -H "Content-Type: application/json" \
  -d '{"annualIncome":85000.00,"monthlyExpenses":900.00,"depositAmount":60000.00,"propertyValue":300000.00,"termYears":30}' \
  | jq -r '.id')

# 4. Retrieve application E1 -> 200 OK
curl -s "$BASE_URL/api/applications/$APP1" -H "Authorization: Bearer $ALICE" | jq .

# 5. Trigger asynchronous affordability assessment -> 202 Accepted (status: Pending) + Location header
ASSESS1=$(curl -s -X POST "$BASE_URL/api/applications/$APP1/assessments" \
  -H "Authorization: Bearer $ALICE" | jq -r '.id')

# 6. Poll the assessment -> 200 OK (status: Completed, decision: Approved, LTV 80.00%, DTI 33.02%, LTI 2.82x, MaxBorrowing 381535.82)
curl -s "$BASE_URL/api/applications/$APP1/assessments/$ASSESS1" \
  -H "Authorization: Bearer $ALICE" | jq .

# 7. High-LTV application E2 (deposit 20,000 -> LTV 93.33% > 90.00%) -> 200 OK with decision: Declined (not an HTTP error)
APP2=$(curl -s -X POST "$BASE_URL/api/applications" \
  -H "Authorization: Bearer $ALICE" \
  -H "Content-Type: application/json" \
  -d '{"annualIncome":85000.00,"monthlyExpenses":900.00,"depositAmount":20000.00,"propertyValue":300000.00,"termYears":30}' \
  | jq -r '.id')
ASSESS2=$(curl -s -X POST "$BASE_URL/api/applications/$APP2/assessments" -H "Authorization: Bearer $ALICE" | jq -r '.id')
sleep 0.2
curl -s "$BASE_URL/api/applications/$APP2/assessments/$ASSESS2" -H "Authorization: Bearer $ALICE" | jq .

# 8. Input validation (termYears: 50 -> 400 ValidationProblemDetails) & cross-user isolation (Bob reading Alice's app -> 404 Not Found)
curl -i -X POST "$BASE_URL/api/applications" \
  -H "Authorization: Bearer $ALICE" \
  -H "Content-Type: application/json" \
  -d '{"annualIncome":85000.00,"monthlyExpenses":900.00,"depositAmount":60000.00,"propertyValue":300000.00,"termYears":50}'
curl -i "$BASE_URL/api/applications/$APP1" -H "Authorization: Bearer $BOB"
```

---

## 4. Codebase Walkthrough Pointers (`09:00–14:00`, `R48`)

Open these 6 files in order to tell the story from pure domain logic to HTTP, background processing, persistence, and verification:

1. **Pure Domain & Calculator (`MortgageApplications.Core`):**
   - [`src/MortgageApplications.Core/Affordability/AffordabilityCalculator.cs`](../../src/MortgageApplications.Core/Affordability/AffordabilityCalculator.cs): pure `decimal` arithmetic, exponentiation by squaring (`Pow`), 3 independent checks (`LTV`, `DTI`, `LTI`), income-based `MaxBorrowing`, unrounded comparison, and single end-rounding (`Math.Round(..., 2, MidpointRounding.AwayFromZero) + 0.00m`).
   - [`src/MortgageApplications.Core/Applications/ApplicationInputRules.cs`](../../src/MortgageApplications.Core/Applications/ApplicationInputRules.cs): shared input preconditions (`AnnualIncome`, `MonthlyExpenses`, `DepositAmount`, `PropertyValue`, `TermYears`, `<= 2 dp`, minimum loan `1,000`).
2. **Use Cases & Save-Before-Enqueue (`MortgageApplications.Core`):**
   - [`src/MortgageApplications.Core/Applications/MortgageApplicationService.cs`](../../src/MortgageApplications.Core/Applications/MortgageApplicationService.cs): `CreateAsync`, `GetAsync`, `AssessAsync` (persists `Pending` row first, then calls `IAssessmentQueue.EnqueueAsync`), `GetAssessmentAsync`, and worker-only `ProcessAssessmentAsync`.
3. **Persistence & Query Plans (`MortgageApplications.Infrastructure`):**
   - [`src/MortgageApplications.Infrastructure/Persistence/EfMortgageApplicationRepository.cs`](../../src/MortgageApplications.Infrastructure/Persistence/EfMortgageApplicationRepository.cs): owner-scoped queries (`Id == id && OwnerId == ownerId`), scalar projection in `FindForProcessingAsync`, and minimal-column updates on tracked assessments.
   - [`src/MortgageApplications.Infrastructure/DatabaseMigration.cs`](../../src/MortgageApplications.Infrastructure/DatabaseMigration.cs): `Development`-only startup migration + explicit `PRAGMA journal_mode=WAL;`.
4. **HTTP Boundary, Auth, Rate Limiting & Background Worker (`MortgageApplications.Api`):**
   - [`src/MortgageApplications.Api/Applications/ApplicationEndpoints.cs`](../../src/MortgageApplications.Api/Applications/ApplicationEndpoints.cs): Minimal API `MapGroup("/api/applications")` + `TypedResults` + `503` full-queue timeout handling.
   - [`src/MortgageApplications.Api/Assessments/ChannelAssessmentQueue.cs`](../../src/MortgageApplications.Api/Assessments/ChannelAssessmentQueue.cs) & [`AssessmentWorker.cs`](../../src/MortgageApplications.Api/Assessments/AssessmentWorker.cs): bounded `Channel<Guid>` (`TryWrite` first, then bounded `WaitToWriteAsync`) and `BackgroundService` with startup `Pending` sweep + retry loop.
   - [`src/MortgageApplications.Api/Identity/AuthenticationSetup.cs`](../../src/MortgageApplications.Api/Identity/AuthenticationSetup.cs) & [`DevTokenEndpoint.cs`](../../src/MortgageApplications.Api/Identity/DevTokenEndpoint.cs): ES256 validation, `RequireAuthenticatedUser` fallback policy, `SubjectClaim` duplicate-key check, and fail-closed `POST /dev/token`.
5. **High-Signal Verification Tests (`tests/`):**
   - [`tests/MortgageApplications.UnitTests/Affordability/AffordabilityCalculatorTests.cs`](../../tests/MortgageApplications.UnitTests/Affordability/AffordabilityCalculatorTests.cs): 18 golden examples (`E1`–`E18`) + boundary & culture tests.
   - [`tests/MortgageApplications.IntegrationTests/TelemetryAllowlistTests.cs`](../../tests/MortgageApplications.IntegrationTests/TelemetryAllowlistTests.cs) & [`DatabaseQueryPlanTests.cs`](../../tests/MortgageApplications.IntegrationTests/DatabaseQueryPlanTests.cs): negative PII/financial leak scan across logs/spans/metrics, and SQLite `EXPLAIN QUERY PLAN` index verification.

---

## 5. Likely Follow-Up Questions & Concise Answers (`20:00–30:00`, `R48`)

1. **"Why three checks (`LTV`, `DTI`, `LTI`) instead of checking `Loan <= MaxBorrowing`?"**
   - Because $\text{MaxBorrowing} = \max(0, \min(\text{LtiCap}, \text{DtiCap}))$, `Loan <= MaxBorrowing` fails if and only if `DTI` or `LTI` fails. Making `LTV`, `DTI`, and `LTI` the three checks gives every failure a single, non-duplicated explanation while preserving $\text{Approved} \implies \text{Loan} \le \text{MaxBorrowing}$ ([ADR 0001](../adr/0001-affordability-assessment-model.md)).
2. **"Why doesn't `MaxBorrowing` cap at 90% of the property value?"**
   - It is deliberately defined and documented in OpenAPI as *income-based* borrower capacity. When an application is declined on LTV (`E2`), showing both the failed `LTV 93.33% > 90.00%` check and `MaxBorrowing = 381,535.82` tells the applicant that their income supports the loan and only the deposit/property ratio failed ([ADR 0001](../adr/0001-affordability-assessment-model.md)).
3. **"What happens if the process crashes between saving the `Pending` assessment and enqueueing it, or while the queue is full?"**
   - `AssessAsync` saves the `Pending` row to SQLite **before** calling `queue.EnqueueAsync`. If the process crashes or if a full queue times out after `10 s` (`503 Service Unavailable`), the `Pending` row is already on disk and `AssessmentWorker`'s startup sweep re-enqueues it on restart ([ADR 0003](../adr/0003-background-assessment-queue.md)).
4. **"What breaks first if you run 5 container replicas today, and how do you fix it?"**
   - Two things: SQLite's single-writer lock and the in-process `Channel<Guid>` queue (and `AssessmentWorker` has no concurrency token on `AffordabilityAssessment`). We swap `MortgageApplicationsDbContext` to Azure SQL, swap `IAssessmentQueue` to Azure Service Bus, and add a `RowVersion` concurrency token + `AttemptCount` retry cap so competing workers process each assessment atomically ([ADR 0003](../adr/0003-background-assessment-queue.md)).
5. **"How did switching from Claude Code to Google Antigravity IDE mid-project work?"**
   - All rules (`AGENTS.md`, `.claude/rules/`), skills (`.claude/skills/`), subagent roles (`.claude/agents/`), and phase state (`docs/PROGRESS.md`, `docs/progress/phase-*.md`) live in version-controlled Markdown. Switching from Claude Opus/Sonnet in Claude Code to Gemini 4 Argon in Antigravity IDE at Phase 07 required zero changes to those files, and the second model's fresh-context `dotnet-reviewer` immediately caught two subtle issues (`DevTokenRequest`/`DevTokenResponse` `ToString()` redaction and duplicate `"sub"` JSON keys in `SubjectClaim`).
