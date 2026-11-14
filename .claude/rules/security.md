# Security rules (always loaded)
- No real secrets anywhere in the repo or its history. Dev signing keys are clearly marked dev-only.
- Never read or edit `.env*`, `*.pem` or `**/secrets/**`.
- Never log income, expenses, deposit, property value, tokens or personal data.
- Every endpoint except `/health` and the dev token endpoint requires authorization.
- Resources are scoped to the caller's `OwnerId`; another user's resource returns 404 (no existence leak).
- Validate all input at the boundary: ranges, decimal places, deposit < property value.
- Ask Alessandro before adding any package, MCP server, skill, permission or workflow secret.
