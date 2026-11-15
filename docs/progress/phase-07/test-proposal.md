# Phase 7 test proposal (test-designer, P07-03), verbatim report

PHASE 7 TEST PROPOSAL (auth). Nothing here is approved; Alessandro picks. I read the phase file, Program.cs, ApplicationEndpoints, DevelopmentCurrentUser, ApiFactory, NotFoundAndOwnerTests, StartupTests, TelemetryAllowlistTests, Http.cs, compose.yaml, ci.yml and grepped the tests for ICurrentUser. I did not open ErrorHandlingTests, ObservabilityLogTests (only grep hits), ComposeFileTests (grep found nothing relevant), PRODUCTION-LAYERS.md or .claude/rules/security.md beyond the copy in the session context.

## 0. Method (how to test without new packages or sleeping)

- Tokens: use Microsoft.IdentityModel.JsonWebTokens (JsonWebTokenHandler + SecurityTokenDescriptor + ECDsaSecurityKey over ECDsa.Create(ECCurve.NamedCurves.nistP256)). It comes transitively through the Api project reference, so Directory.Packages.props stays untouched. Raw tokens (alg none, HS256 confusion, tampered signature) can be built by hand: base64url(header).base64url(payload).signature using System.Text.Json and System.Security.Cryptography, no library needed. Put this in one test helper `TestTokens` (mint valid, mint with overrides, mint raw).
- ApiFactory (P7-D4 A): owns an ECDsa test key, exposes `TokenFor(subject)` and a default test owner. Configures the app to validate against its public key (via the same Jwt options and key-source seam the app uses; see ambiguity A1). Its client sends the default token by default through a DelegatingHandler. Auth tests need clients with NO token, so give the factory a `CreateAnonymousClient()` (or pass a flag), since CreateClient() will carry the default token.
- Time: the plan does not say TimeProvider is used by JwtBearer. TokenValidationParameters has `LifetimeValidator` and the handler takes `TimeProvider` in TokenHandler on .NET 10 only via custom validators, so do NOT assume the app clock is controllable. Cheapest sleep-free way: mint tokens with explicit `NotBefore`/`Expires` relative to real UtcNow (expired 31 s ago, expired 29 s ago, not-before 31 s ahead, 29 s ahead). Margins of about 1 s are flaky on a slow CI; use 31 s / 29 s only if the app clock cannot be injected, otherwise prefer injecting a FakeTimeProvider-style test double (hand-written, no package) into the options (ambiguity A2). The exact-limit boundary (30 s to the second) is a must for unit-level only if the app exposes a seam; otherwise assert just inside/just outside with a 2 s margin.
- Unit vs integration: tests/CLAUDE.md says unit tests cover Core only, and all auth code is in the Api project. So everything below is an integration test (WebApplicationFactory), except options validation and the pattern rule, which can be integration tests via startup failure (as StartupTests does). Flag for Alessandro: if he wants pure unit tests of `ClaimsCurrentUser` or the options validators, the UnitTests project would need an Api reference, which breaks the stated boundary (ambiguity A6).

## 1. Existing tests that must change

- ApiFactory: owns the key, default-owner token handler, anonymous client option, plus config for Jwt/DevAuth so it still starts in Development.
- NotFoundAndOwnerTests: `OtherOwnerFactory()` and `TestUser` override ICurrentUser. Replace with a client sending `TokenFor("other-owner")` from the same factory (no second host needed; the "other host without worker" trick for queue counting still needs a host without worker, so that test keeps WithWebHostBuilder but sets the token header instead of ICurrentUser). `I37` and the `owner` lookups via `GetRequiredService<ICurrentUser>()` (NotFoundAndOwnerTests:133, CreateApplicationTests:67, AssessmentWorkerTests:253 and :295, ObservabilityLogTests:87, TelemetryAllowlistTests:102) must read the factory's default owner constant instead: ICurrentUser will be scoped/per-request and cannot be resolved from the root provider.
- ObservabilityLogTests:55 and :153 (OtherOwner class): same conversion to a token for another subject.
- TelemetryAllowlistTests: `Server_span_tag_keys_are_in_the_allowlist` exact-set assertion (`Assert.Empty(_serverSpanKeys.Except(keys))`) stays valid only if authentication adds no new span tags. Check it still passes; if the auth middleware adds tags, the allowlist change needs Alessandro's decision. Also the `throwing` host and `CreateDefaultClient(handler)` clients bypass any ApiFactory default-token handler; they must get the token explicitly (the handler must be composable with TraceparentHandler).
- StartupTests: `Development_only_path_returns_404_in_production` and `Startup_migrates_the_database_only_in_development` start a Production host; with the new fail-closed rule that host throws unless a validation key is configured. Production hosts in these tests need a key configured (or the expectation changes). Also `/openapi/v1.json` and `/health` calls must still be anonymous-reachable: OpenAPI and Scalar are mapped only in Development; confirm they have AllowAnonymous or the fallback policy returns 401 on them (ambiguity A3, and the plan lists only /health and POST /dev/token as anonymous).
- ComposeFileTests: if it asserts compose content, it needs the DevAuth__Enabled line (I could not confirm what it asserts).
- Any test that posts without going through the factory client (e.g. via CreateDefaultClient) loses its token.

## 2. Cases

Notation: ID | given | when | then | why. "Anon" = factory client with no Authorization header. Valid token = ES256, correct iss/aud, exp 15 min ahead, sub "alice".

### Happy path

AU01 must | valid token for sub "alice" | POST /api/applications with BaseBody | 201, Location /api/applications/{id}; row's OwnerId == "alice" (read via DbContext) | proves sub is the owner and the real validation path works
AU02 must | token for alice creates app and assessment | GET both with alice's token | 200, 200; POST assessment returns 202 with Location | existing behaviour preserved end to end
AU03 must | no token | GET /health | 200 | health stays public
AU04 must | sub is exactly 128 chars ('a' x 128) | POST /api/applications | 201; OwnerId has length 128 | exact upper limit (column size)
AU05 nice | token has extra claims (role, email, name) and sub "alice" | GET own app | 200, and none of the extra values appear in response | extra claims ignored; MapInboundClaims=false means "sub" is read as "sub" not NameIdentifier
AU06 must | worker running (default host), alice creates app and POST assessment | poll location with alice's token (PollUntilFinishedAsync) | status reaches Completed within 10 s | worker has no user and still works

### Failure: 401 (Authentication). All use POST /api/applications unless noted, plus GET /api/applications/{id}. Each asserts 401, `WWW-Authenticate` starts with "Bearer", body is ProblemDetails (application/problem+json, status 401, title, traceId), and nothing is created.

AU10 must | no Authorization header | POST /api/applications and GET /api/applications/{newGuid} | 401 on both (auth runs before the 404 route logic); DB application count unchanged | basic gate, and unknown ID does not become 404 for anon
AU11 must | `Authorization: Bearer not-a-jwt` | request | 401; WWW-Authenticate contains `error="invalid_token"` | malformed
AU12 nice | `Authorization: Basic YWxpY2U6eA==` ; `Authorization: Bearer` (empty) | request | 401 | wrong scheme / empty
AU13 must | valid token with last signature char altered | request | 401 | bad signature
AU14 must | well-formed ES256 token signed with a different ECDsa key (correct iss/aud/exp) | request | 401 | wrong key
AU15 must | exp = now - 31 s (just past 30 s skew), nbf earlier | request | 401, WWW-Authenticate has `error="invalid_token"` and `error_description` that does not contain the token | expired beyond skew
AU16 must | exp = now - 29 s | request | 201/200 (accepted inside skew) | skew exactly honoured; flaky if margins under ~2 s with real clock (see A2)
AU17 nice | exp = now - 1 h | request | 401 | plain expired
AU18 must | nbf = now + 5 min (iat/exp consistent) | request | 401 | not yet valid
AU19 nice | nbf = now + 29 s | request | accepted (skew applies both ways) | symmetric skew; same clock caveat
AU20 must | iss = "https://evil.example" | request | 401 | wrong issuer
AU21 must | aud = "other-api" | request | 401 | wrong audience
AU22 nice | no iss claim ; no aud claim ; no exp claim | request | each 401 (RequireExpirationTime must be true) | token without expiry must not live forever
AU23 must | alg none: header {"alg":"none","typ":"JWT"}, valid payload, empty signature (`h.p.`) | request | 401 | unsigned
AU24 must | HS256 token, HMAC key = the public key bytes (SubjectPublicKeyInfo DER, and also the raw public-key string/PEM as secret), valid claims | request | 401 | algorithm confusion; proves algorithm is pinned
AU25 must | ES256 header alg replaced by ES384 / RS256 with a validly signed token under the right key where possible, else just header changed | request | 401 | pinned to ES256 only (ES384 signed by same P-256 key is not constructible; use header-swap only and say so)
AU26 must | token with no sub | request | 401 (not 201, not 500), no app created | missing sub
AU27 must | sub = "" | request | 401 | empty
AU28 must | sub = " " (whitespace only) | request | 401 | nice to pin (plan says missing/empty/over-long; whitespace is not specified, A4); mark nice
AU29 must | sub of 129 chars | request | 401; DB row count unchanged | exact limit +1
AU30 nice | sub is a number (`"sub":123`) or array (`["a","b"]`) | request | 401, never 500 | wrong claim type must not throw
AU31 must | for AU10-AU30 responses | inspect body and headers | none of: the token string, any base64url segment of it, "IDX" exception codes/text (e.g. "IDX10"), stack trace, "Microsoft.IdentityModel", the sub value (use a distinctive sub like "sentinel-owner-7") | no leak. Note: JwtBearer puts `error_description` (e.g. "The token expired at ...") in WWW-Authenticate by default; decide if that is acceptable (A5)
AU32 must | any 401 | inspect | status code pages / ProblemDetails shape matches ErrorHandlingTests conventions: application/problem+json, status 401, non-empty traceId | consistent error shape (JwtBearer default 401 has an empty body; app must write ProblemDetails via the status code pages already used)

### Authorization (fallback policy)

AU40 must | enumerate `EndpointDataSource` endpoints (RouteEndpoint) from factory.Services | for each endpoint not in the explicit anonymous allowlist {GET /health, POST /dev/token}: assert endpoint metadata contains an IAuthorizeData or fallback applies (i.e. `IAllowAnonymous` metadata absent), AND, behaviourally, call each with Anon and assert 401 (substitute a random Guid for route params) | so a new endpoint cannot slip through. The allowlist is a literal list in the test; adding any other AllowAnonymous endpoint fails the test. Also assert the four application endpoints are present (guards against the test enumerating nothing)
AU41 must | endpoint with AllowAnonymous metadata | enumerate | exactly {GET /health} in a default Development host with DevAuth off, and {GET /health, POST /dev/token} with it on. Fails if any other endpoint (including /openapi, /scalar in Development) is anonymous unexpectedly | see A3
AU42 must | the four application endpoints | Anon, each: POST /api/applications, GET /api/applications/{guid}, POST .../assessments, GET .../assessments/{guid} | all 401; POST assessments creates nothing and queues nothing (queue.Reader.Count == 0 on a host without worker) | named explicit list as a second line of defence
AU43 nice | OPTIONS/HEAD on a protected path anonymous | 401 or 405, never 200 with data | low value; skip if tight

### Cross-user (404 behaviour)

AU50 must | alice creates application A and assessment S | bob (token sub "bob") GETs /api/applications/A, POSTs /api/applications/A/assessments, GETs /api/applications/A/assessments/S | all 404; each body, after stripping traceId and instance, equals the 404 for a random unknown Guid (reuse Strip from NotFoundAndOwnerTests); assessment count unchanged; alice still gets 200 | replaces I35 using tokens
AU51 must | same as AU50 on a host without worker | bob POSTs assessment for A and for a malformed ID | queue.Reader.Count == 0 and no assessment row | nothing queued for another subject (replaces I-5)
AU52 must | bob creates an application | alice GETs it | 404 | symmetric
AU53 must | body of POST /api/applications includes `"ownerId":"bob"` | alice posts | 400 (JsonStrictness: unknown property rejected) and nothing created; ALSO a second variant with a body that is otherwise valid cannot be used since unknown props are 400s, so owner via body is rejected not ignored | the plan says "no effect"; strict JSON makes it a 400. Confirm expectation (A7)
AU54 must | alice's token plus headers `X-Owner-Id: bob`, `X-User-Id: bob`, `X-Forwarded-User: bob`, and query `?ownerId=bob` | POST then GET | created row OwnerId == "alice"; bob's GET of it is 404 | owner only from sub
AU55 nice | subjects "Alice" and "alice" (case) , and "alice" vs "alice " | create as one, read as other | 404 | owner match is exact; no case folding. Only if sub charset allows; uppercase subs are valid in a real JWT even though dev endpoint forbids them
AU56 must | existing I37 (no owner in bodies) | with token sub "sentinel-owner-7" | no response body contains the sub or "ownerId" | keeps meaning

### Dev token endpoint

Hosts: Development+DevAuth on = "devOn" (factory config `DevAuth:Enabled=true`); the other combos below.

AU60 must | devOn | POST /dev/token {"subject":"alice"} anonymous | 200, JSON has exactly `token` and `expiresAt`; the token is 3 segments; header alg "ES256"; claims sub "alice", iss/aud equal the configured Jwt values; exp - iat == 900 s; `expiresAt` parses as a UTC timestamp within 900 s ± 5 s of now and equals the token exp (to the second) | contract
AU61 must | devOn | take the issued token | call POST /api/applications and GET it | 201 then 200; so the issued token is accepted by the real validation path | closes the loop (this needs a factory that does NOT override the key: the endpoint signs with the in-memory key, so this test must use the app's own key. The ApiFactory test key therefore cannot be the only mode; see A1)
AU62 must | devOn, two subjects alice and bob via endpoint | cross user | bob gets 404 for alice's application | the demo scenario from P7-D3
AU63 must | Production, DevAuth:Enabled=true | startup | host build throws (see AU80) and, if somehow built, POST /dev/token is 404. Primary assertion is the throw | fail closed
AU64 must | Staging (and Production) with a validation key configured and DevAuth:Enabled absent/false | POST /dev/token | 404 (ProblemDetails or empty, but same as an unknown route) | not registered
AU65 must | Development, DevAuth:Enabled false and Development with the key missing | POST /dev/token | 404 | needs flag
AU66 must | devOn | subject boundaries: "a" (1) ; 64 x 'a' ; "a-1-b" with digits and hyphen ; "-" ; "0" | 200 each | valid pattern edges
AU67 must | devOn | 65 x 'a' ; "" ; "Alice" ; "al ice" ; "alice!" ; "alice\n" (trailing newline: `$` in .NET regex matches before a final \n, so `^[a-z0-9-]{1,64}$` accepts "alice\n" unless the attribute uses \z) ; "../x" ; "ali_ce" ; "alicé" ; "alice@x" | 400 ValidationProblemDetails with error key `subject` (JSON name), never 500 and no token | **the trailing-newline case is a likely real bug** in the pattern as written in the plan; confirm exact expected value, I am not sure how AddValidation/RegularExpression behaves (A8)
AU68 must | devOn | body `{}` ; `{"subject":null}` ; empty body ; body `null` ; wrong content type ; `{"subject":123}` | 400 each, no token | missing
AU69 must | devOn | `{"subject":"alice","role":"admin"}` ; duplicate "subject" | 400 | strict JSON (the app already disallows unmapped members and duplicates)
AU70 must | devOn | two separate subject requests for "alice" | tokens differ (jti or iat) or at least both valid; assert both accepted | nice variant: tokens need not be unique; mark nice
AU71 must | two app instances (two ApiFactory-less hosts, both devOn, no test key override) | take a token from instance 1 and send to instance 2 | 401 | key generated in memory per process; proves no shared key and no key on disk
AU72 nice | token from devOn instance, wait nothing | decode and check no `kid` leaks a path, and that `typ` is JWT | skip if tight
AU73 must | devOn | GET /dev/token | 404 or 405 (not 200) | method restriction
AU74 must | devOn | endpoint response and logs | the token is not in captured logs (factory.Logs) or span text | see AU90

### Startup / configuration

AU80 must | Production, DevAuth:Enabled=true, valid Jwt options and a key | build host (CreateClient) | throws; message names the option `DevAuth` / `Enabled`, not a secret or value of a key. Use FindOptionsFailure style (OptionsValidationException) or whatever type the plan uses (A9) | fail closed
AU81 must | Staging, DevAuth:Enabled=true | same | throws | any non-Development
AU82 must | Production, DevAuth off, no validation key and no authority | startup | throws, message names the missing setting | fail closed when unconfigured
AU83 must | Production, DevAuth off, validation key configured | startup then GET /health | 200, and POST /api/applications anon is 401 | production happy path
AU84 must | Development, DevAuth off, no key configured | startup | succeeds? Or fails? The plan says the failure rule applies "outside Development"; so Development without any key starts and every protected call is 401 | pin the behaviour (A10)
AU85 must | Jwt options invalid: empty issuer, empty audience, lifetime 0, lifetime negative, lifetime absurd (for example above 24 h; the plan gives no maximum) | startup | throws naming the option, not the value | options validation (like I42). Boundary lifetime values depend on the validator, so I can only assert 0/negative/empty confidently; the upper limit is A11
AU86 nice | Jwt:Lifetime 1 minute | issue via /dev/token | exp - iat == 60 | the lifetime option is honoured (plan says 15 min default)
AU87 must | no `Jwt` section at all (as I43 does for Affordability) | startup | throws | no default-open
AU88 must | the key material | grep repo-independent: assert the factory host and the dev host's configuration contain no private key (IConfiguration has no key-like value; the in-memory key never reaches logs) | nice; the repo-secret part is Gitleaks' job

### Telemetry and logs

AU90 must | alice with sentinel sub "sentinel-owner-7" does create + assess + GET + one 401 (bad token) and a 404; capture logs (factory.Logs), spans (TelemetryCapture), metric tags | scan all texts, including tags, scopes, state | none contains the token (full string, each segment), the sub value, "Bearer ", or the Authorization header value; control: the sub does appear in the DB owner column | extends O5 (which already checks the owner)
AU91 must | server span tags | after AU90 | `_serverSpanKeys` unchanged (no `enduser.id`, no `user.id` etc.); see change list above | ASP.NET instrumentation does not add user tags by default, but auth could add events/status; assert none added
AU92 must | failed validation (expired/bad-signature token) | inspect logs | the framework's JwtBearer logs (Microsoft.AspNetCore.Authentication.JwtBearer, "Failed to validate the token", exception text containing the token's claims such as issuer or the sub in IDX messages) must not contain the token or sub. JwtBearer logs the exception (with `IDX10223: Lifetime validation failed. The token is expired. ValidTo (UTC): ..., Current time` and for audience/issuer mismatch includes the token's audience/issuer values) at Information/Debug. Test: sentinel aud in token, assert it not in logs, OR if it is found there then the app must filter that category (A12). I am not sure the sub itself appears, flag as uncertain | real leak path
AU93 nice | OTLP/telemetry for 401 requests | the server span has `http.response.status_code=401` and no extra keys | covered by AU91

### OpenAPI

AU100 must | Development | GET /openapi/v1.json anon (OpenAPI is mapped in Development) | 200; `components.securitySchemes` contains a scheme with type `http`, scheme `bearer`, bearerFormat `JWT`; protected operations carry a `security` requirement; /health and /dev/token operations do not | scheme declared (the A3 question: is /openapi anonymous?)
AU101 must | Production | GET /openapi/v1.json | 404 (existing test I46 stays; make sure it is 404 not 401 for anon, since fallback policy would 401 an unmapped route? An unmapped route is a plain 404 because the fallback policy only applies to matched endpoints: confirm) | no change in meaning
AU102 nice | document contents | contains no private key, no token example | trivial

### Compose / CI (infra)

AU110 must | compose.yaml | parse text (as ComposeFileTests probably does) | contains `DevAuth__Enabled: "true"` and ASPNETCORE_ENVIRONMENT Development; the "no authentication until Phase 7" comment is updated; ports still loopback `127.0.0.1:8080:8080` | DevAuth must be loopback only
AU111 must | ci.yml docker job | text check or manual run | requests `/dev/token`, parses `token`, sends `Authorization: Bearer ...` on POST/GET/assess; does not echo the token (no `set -x`, no printing the JSON, add `::add-mask::`); one extra step asserts anonymous POST /api/applications returns 401 | CI proves auth works in the container. This can only be verified by running CI; mark as manual verification, not an xUnit test, unless ComposeFileTests already parses ci.yml
AU112 nice | docker job | anonymous GET of another subject's app returns 404 | demo of cross-user in CI; adds time

## 3. Ambiguities in the plan for Alessandro

A1. How does the ApiFactory test key reach validation, and how does AU61/AU71 use the app's own in-memory key? Needs a seam (options object with a signing/validation key source). Two modes are required: the factory test key (most tests) and the real dev key (the dev-endpoint tests). The plan only mentions the first.
A2. Time control: JwtBearer validates against the system clock unless a validator/TimeProvider is injected. Do we accept real-clock tests with 31 s / 29 s margins (flaky-prone, no sleeping), or add a TimeProvider seam to the validation parameters? Exact 30.0 s boundary only testable with the seam.
A3. Are /openapi/v1.json, /scalar and /scalar/v1 anonymous in Development? The plan says AllowAnonymous only on /health and /dev/token, which would put OpenAPI behind a 401 and break the Scalar page and the existing OpenApi test. Decide.
A4. Whitespace-only sub: valid or 401? Plan lists missing, empty, over 128 only.
A5. 401 body and WWW-Authenticate: default JwtBearer sends `error="invalid_token", error_description="The token expired at ..."`. Is the description acceptable (RFC 6750 permits it) or must it be stripped to `Bearer`? The plan says "leak no token or exception text"; an expiry timestamp is exception text.
A6. Unit tests cover Core only (tests/CLAUDE.md) but all auth code is in Api. Confirm that everything is an integration test, or allow an Api reference in the unit test project.
A7. A body with `ownerId`: the plan says "no effect"; strict JSON makes it a 400. Which is intended? (I assumed 400 and nothing created.)
A8. Pattern `^[a-z0-9-]{1,64}$` with .NET `$` accepts a trailing "\n". Needs `\z` or a character check. Confirm the expected result for "alice\n" is 400.
A9. Which exception type do startup failures throw (OptionsValidationException vs InvalidOperationException)? Tests will assert on type and that the message names the setting but not values; settle the type.
A10. Development with no validation key and DevAuth off: starts (all protected endpoints 401) or fails?
A11. Upper bound on Jwt token lifetime, and units (minutes? seconds?) are unspecified.
A12. Framework JwtBearer logs may include token-derived text (issuer/audience values, expiry times). Filter the category, or accept? Also whether the exception in logs may include the sub (I did not verify).
A13. Compose runs Development and is bound to 127.0.0.1 only; with DevAuth on, anyone on the machine can mint any subject. Fine for demo; ADR 0004 should state it.
A14. A Production host test now needs a configured key; confirm how tests configure it (in-memory config key name is unknown to me: I could not see the options names).

## 4. Priority picks if the list must be cut to about 25

AU01, AU03, AU06, AU10, AU11, AU13, AU14, AU15, AU16, AU18, AU20, AU21, AU23, AU24, AU26, AU27, AU29, AU04, AU31, AU32, AU40, AU42, AU50, AU51, AU54, AU60, AU61, AU64, AU67, AU71, AU80, AU82, AU90, AU92, AU100, AU110.

Files referenced (all under /Users/ale_ssd/Developer/Mortgage-Application-API-test): docs/progress/phase-07-auth.md, src/MortgageApplications.Api/Program.cs, src/MortgageApplications.Api/Applications/ApplicationEndpoints.cs, src/MortgageApplications.Api/Identity/DevelopmentCurrentUser.cs, tests/MortgageApplications.IntegrationTests/{ApiFactory,Http,NotFoundAndOwnerTests,StartupTests,TelemetryAllowlistTests,ObservabilityLogTests,AssessmentWorkerTests,CreateApplicationTests}.cs, compose.yaml, .github/workflows/ci.yml.
