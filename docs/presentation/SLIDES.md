---
marp: true
theme: default
paginate: true
title: Mortgage Application API — Follow-Up Discussion (3 Slides)
author: Alessandro Lentini
---

# Slide 1 · Solution Overview & Architecture

**ASP.NET Core (.NET 10) JSON API — 3-Project Clean Boundary**

```text
[ HTTP Client ] ──► MortgageApplications.Api (Minimal APIs, ES256 JWT, Rate Limiter, OTel)
                         │                              │
                         ▼                              ▼ (IAssessmentQueue: Channel<Guid>)
             MortgageApplications.Core         AssessmentWorker (BackgroundService)
             (AffordabilityCalculator,                  │
              ApplicationInputRules,                    ▼
              MortgageApplicationService) ◄──── MortgageApplications.Infrastructure
                                                (EF Core 10 + SQLite WAL, Owner-Scoped)
```

- **`Core` has zero framework dependencies** — pure C# domain entities, `decimal` calculator, and ports (`IMortgageApplicationRepository`, `IAssessmentQueue`; `ICurrentUser` resolves `sub` at the `Api` boundary).
- **4 Optional Enhancements built & verified:** Background assessments (`202 Accepted` + polling), ES256 JWT authentication + owner isolation, Docker (`@sha256` digest-pinned, non-root `init: true`), and OpenTelemetry (traces, metrics, structured `[LoggerMessage]` logs, opt-in Aspire dashboard).
- **586 automated tests** (`275` unit + `311` integration) + **113 hook tests**, built phase-by-phase with TDD and fresh-context AI review gates across **Claude Code** and **Google Antigravity IDE**.

<!--
Speaker notes (~2 min):
- After the live demo, this slide summarises how the three projects fit together.
- Highlight the dependency direction: Core depends on nothing; Infrastructure and Api depend on Core.
- Invite discussion on any of the four optional enhancements we just exercised in the demo.
-->

---

# Slide 2 · Affordability Logic & Assumptions

**Pure `decimal` Function: `(AffordabilityInputs, AffordabilityOptions) → AffordabilityResult`**

| Check | Formula (`System.Decimal`, full precision) | v1.0 Limit |
|---|---|---|
| **1. Loan-to-Value (`LTV`)** | $\text{Loan} / \text{PropertyValue} \times 100$, where $\text{Loan} = \text{PropertyValue} - \text{DepositAmount}$ | $\le 90.00\%$ |
| **2. Debt-to-Income (`DTI`)** | $(\text{MonthlyExpenses} + \text{MonthlyPayment}) / (\text{AnnualIncome} / 12) \times 100$ at $6\%$ annual rate | $\le 45.00\%$ |
| **3. Loan-to-Income (`LTI`)** | $\text{Loan} / \text{AnnualIncome}$ | $\le 4.50\times$ |

- **Decision & Invariant:** `Approved` iff all 3 independent checks pass ($\text{Approved} \implies \text{Loan} \le \text{MaxBorrowing}$).
- **Income-Based `MaxBorrowing`:** $\max\bigl(0,\ \min(4.5 \times \text{Income},\ \text{DtiCap})\bigr)$ — reports borrower capacity independently of the property's LTV cap.
- **Unrounded comparison, single end-rounding:** comparisons use full-precision `decimal`; outputs are rounded once at the end (`2 dp`, `MidpointRounding.AwayFromZero`, scale `2`). Boundary failures append `"before rounding"`.
- **Assumptions:** single applicant, gross income, capital-and-interest repayment over `5–40` years, fixed $6\%$ stress rate, existing `MonthlyExpenses` exclude the new mortgage, minimum loan `1,000`, and `RulesVersion` (`"1.0"`) persisted per assessment.

<!--
Speaker notes (~2 min):
- Point out why we used 3 independent checks (LTV, DTI, LTI) instead of a composite "Loan <= MaxBorrowing" check: in B4, "Loan <= MaxBorrowing" failed whenever DTI or LTI failed, repeating the DTI failure reason twice in the explanation (ADR 0001).
- Mention that invalid inputs (e.g. deposit >= propertyValue or term outside 5..40) are rejected at the HTTP boundary with 400 ValidationProblemDetails, whereas Declined is a valid 200 OK business result.
-->

---

# Slide 3 · Key Design Decisions & First Change at Scale

### 3 Key Design Decisions
1. **Pure Domain `Core` + `decimal` Precision ([ADR 0001](../adr/0001-affordability-assessment-model.md)):** No clock, DB, or framework in `AffordabilityCalculator`; 18 golden examples (`E1`–`E18`) + property tests verify exact rounding and invariants.
2. **Save-Before-Enqueue `202` Pipeline ([ADR 0003](../adr/0003-background-assessment-queue.md)):** `Pending` row is committed to SQLite before queueing to bounded `Channel<Guid>`; full queues wait up to `10 s` (`503` backpressure) and `AssessmentWorker`'s startup sweep recovers `Pending` rows on restart — **zero silent drops**.
3. **Fail-Closed ES256 Auth, Owner Scoping & Zero-PII Telemetry ([ADR 0004](../adr/0004-authentication-and-authorisation.md)):** Global `RequireAuthenticatedUser` policy, every query filtered by `Id AND OwnerId` (`sub` → `404` on cross-owner access), and an automated allowlist test proving no income/loan/ratio figures ever appear in logs, spans, or OTLP exports.

### First Change Under Significantly Higher Load
- **First Bottleneck:** SQLite's single-writer lock and the in-process `Channel<Guid>` queue (both tied to a single instance).
- **First Change:** Swap `MortgageApplicationsDbContext` to **Azure SQL** and `IAssessmentQueue` to **Azure Service Bus**, adding a **`RowVersion`** concurrency token and **`AttemptCount`** retry cap / dead-letter policy so multiple stateless API and worker replicas scale out horizontally.

<!--
Speaker notes (~2 min):
- These three decisions and the scaling path are designed to kick-start our discussion:
  - Would we add a transactional outbox when moving to Service Bus?
  - How would we plug in Microsoft Entra ID (`oid` + `tid` claims) and `Idempotency-Key` headers?
-->
