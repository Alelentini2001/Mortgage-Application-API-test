# 0004. Authentication and authorisation

- **Status:** Proposed (draft written by the agent; Alessandro accepts or changes it at Phase 7 Checkpoint B)
- **Date:** 2026-11-16
- **Decided by:** Alessandro (on the agent's recommendation): P7-D1 to P7-D4 and P7-D9 at Phase 7 Checkpoint A ("Approved, all A"), and the test answers A1–A14 ("Approved").

## Context

Until Phase 7 every request ran as one fixed development owner, and every endpoint was open (accepted in Phase 3 and in ADR 0002 because builds were not deployed and compose binds to `127.0.0.1`). The spec lists authentication as an optional enhancement. The Phase 1 design (`docs/layers/PRODUCTION-LAYERS.md`) planned JWT bearer with HS256.

Microsoft Learn ("Configure JWT bearer authentication") says asymmetric keys should always be used when creating access tokens, that token creation should be limited to test scenarios, and that an API should validate signature, issuer, audience and expiry.

## Options considered

1. **ES256 (ECDSA P-256) with the public key from configuration; in Development a key pair generated in memory for a dev token endpoint.** No secret in the repo or config; dev tokens die with the process.
2. **HS256 with a shared key from configuration** (the Phase 1 design). Simpler, but a secret to provision and guard, and against the Learn guidance.
3. **`dotnet user-jwts`**. No endpoint, but awkward in compose and CI.

For the production identity provider: build authority-based (OIDC) validation now, or document it and fail closed until configured.

## Decision

Option 1, and the production identity provider is documented only.

- **Secure by default:** a fallback policy requires an authenticated user. Anonymous: `GET /health`, `POST /dev/token` (when registered) and, in Development only, the OpenAPI document and Scalar. Because the fallback policy also applies when no route matches, an anonymous request to an unknown path is a 401, not a 404.
- **Validation:** ES256 only, signed tokens and an expiry required, issuer and audience checked, 30 s clock skew, inbound claims not remapped, no error details in `WWW-Authenticate`.
- **Owner:** the `sub` claim, required, not blank, at most 128 characters (the column size), exactly one. It reaches the code only through `ICurrentUser`. Another user's data is a 404, as before.
- **Dev token endpoint:** `POST /dev/token {"subject": "alice"}` returns a 15-minute token. Registered only in `Development` with `DevAuth:Enabled=true`; outside Development, startup fails if the flag is on or if no `Jwt:SigningPublicKey` is configured. No password and no user store: it exists for local runs, demos and CI.
- **Production identity provider (not built):** an identity provider such as Microsoft Entra ID issues the tokens; the API is given its public key (or, later, its authority and audience). With Entra, the owner should be keyed on `oid` + `tid` rather than `sub`, which is pairwise per application.

## Consequences

- The compose demo lets anyone on the machine mint a token for any subject. That is acceptable only because the port is bound to `127.0.0.1` (ADR 0002); that bind must still not be widened.
- Tests make their own key and tokens, so no key is ever committed.
- IdentityModel logs the token's issuer in clear at Information level when validation fails, even with its personal-data hiding on. The `Microsoft.AspNetCore.Authentication` log category is therefore set to `Warning`.
- IdentityModel caches signature providers globally by key thumbprint; each key gets its own uncached provider factory so that disposing one key cannot break another host.
- Moving to a real identity provider means configuring its key (or adding authority-based validation) and changing the owner claim; the rest of the code depends only on `ICurrentUser`.
