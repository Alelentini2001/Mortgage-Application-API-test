---
paths:
  - "tests/**/*.cs"
---
# Test rules
- Each new test proves one behaviour named in the phase file; list what it proves there.
- Boundaries: test the exact limit (e.g. LTV = 90.00%), just below and just above.
- Integration tests assert status code, `Location` header and the ProblemDetails shape, not just success.
- Security cases are tests too: 401 without token, 404 for another user's data, no money values in logs.
- Polling tests (background work) use a bounded timeout, never a fixed `Task.Delay`.
