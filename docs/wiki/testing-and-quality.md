# Testing and quality

This document explains how the current solution is validated, what each test suite covers, and what kinds of regressions the repository is already protecting against.

## Testing strategy summary

The repository uses multiple test levels so the current control-plane behavior is validated from unit level up to browser-driven flows.

## Test projects

| Project | Test type | Focus |
| --- | --- | --- |
| `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests` | Unit | Retry timing, schedule evaluation, auth-state behavior, and application logic. |
| `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests` | Unit | Configuration persistence, secret protection, notification providers, redaction, and retention behavior. |
| `test/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.UnitTests` | Unit | AppHost settings parsing, provider-branch environment wiring, infrastructure/project registration, and focused composition-topology smoke coverage. |
| `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests` | Unit | API auth-configuration behavior, configuration validation, and auth-audit summary resolution without distributed runtime startup. |
| `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests` | Integration | API contracts, real AppHost-backed service behavior with one shared AppHost-plus-Keycloak runtime for the retained real-token auth slice, and the isolated synthetic-token negatives that still require controlled invalid JWT and claim-shape inputs. |
| `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests` | Unit | Web authentication policy registration, claim mapping, direct `PlatformApiClient` boundary behavior, and bUnit component coverage for the refreshed Blazor shell and key operator pages. |
| `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests` | Functional | Requirement-driven redirect, sign-out, CSRF, and rendered HTML outcomes with one shared real AppHost-plus-Keycloak runtime per auth collection. |
| `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests` | End-to-end | One retained browser smoke that proves the real AppHost-plus-Keycloak sign-in path from runtime listener discovery through the protected UI surface. |

## AppHost-backed distributed validation model

The distributed validation model now follows the delivered Aspire topology rather than a substitute path.

- `src/TNC.Trading.Platform.AppHost/AppHost.cs` is now a thin composition root.
- Infrastructure registration, project registration, and shared environment wiring live in focused AppHost support files.
- AppHost-focused unit tests now cover `AppHostSettings`, provider-parity environment wiring, infrastructure/project registration, and a resource-model composition smoke that checks preserved resource names, waits, endpoint registrations, and operator-facing links before the higher-cost distributed suites run.
- AppHost-backed integration, functional, and end-to-end suites validate the real Aspire-managed runtime with Docker-backed infrastructure, SQL Server, Mailpit, and Keycloak.
- The only supported AppHost override is the narrow API-authentication switch used by the synthetic bearer-token integration slice; it keeps the Web runtime on Keycloak while allowing API-only invalid-token and claim-shape negatives to reach the protected boundary.
- Shared real-runtime helpers discover listener URLs from AppHost startup output so the suites validate the delivered listener set instead of fixed launch-settings assumptions.
- The real-token API authentication integration suite now reuses one AppHost-plus-Keycloak process per xUnit collection, while the synthetic-token API negatives stay isolated in their own AppHost-backed collection because they still require the API-only test-provider override.
- The Web functional and Web end-to-end auth suites now each reuse one AppHost-plus-Keycloak process per xUnit collection so the retained distributed coverage proves the delivered topology without repeatedly paying startup cost for every test case.

The only retained synthetic auth negatives are the isolated API bearer-token cases that need intentionally invalid issuer, audience, expiry, unsupported audit payloads, or controlled display-name claim shapes. Those tests are kept in a dedicated integration-test slice because the real provider path does not expose a stable way to mint those selectively invalid or incomplete JWTs on demand.

## What the tests already cover

### Application behavior

The unit tests cover:

- retry-delay calculation and backoff caps
- missing-credential degraded behavior
- retry-state visibility rules
- blocked-live safety behavior
- notification suppression and retry-cycle updates
- schedule evaluation

The application unit suites now prefer direct compile-time access to internal coordinator, schedule, and IG sanitization types instead of generic string-based reflection helpers. The only supporting seam added for this hardening was an internal visibility expansion for the test projects together with making the targeted retry-cycle helper callable as an internal member, so renamed members now fail at compile time rather than surfacing as runtime reflection errors.

### Infrastructure behavior

The unit tests cover:

- SQL-backed configuration seeding and update behavior
- restart-required behavior for startup-fixed changes
- protected credential storage and rotation
- secret redaction in audits and operational data
- notification provider fallback and dispatch recording
- retention cleanup for operational records

The infrastructure unit suites now instantiate internal persistence, notification, and configuration components directly through compile-time references. The remaining infrastructure test helper is limited to typed in-memory `PlatformDbContext` and data-protection setup so the tests no longer rely on string-based constructor, method, enum, or property lookup.

The infrastructure notification tests now also cover the `NotificationDispatcher` runtime branches for unconfigured recipients, missing provider registrations, and handled provider exceptions. This keeps notification routing, failed dispatch persistence, and secret-safe failure shaping in the low-cost unit layer instead of relying on broader runtime scenarios.

### API behavior

The API tests cover:

- health endpoints
- anonymous `401` behavior for protected endpoints
- invalid issuer, invalid audience, invalid signature, expired, and no-role bearer-token fail-closed behavior
- viewer, operator, and administrator bearer-token access behavior across status, configuration, manual-retry, events, and administrator auth-summary endpoints
- current `/api/platform/status` contract coverage for IG login current-state detail and the latest stored non-secret login payload embedded in the existing response
- persisted operator auth audit-event recording through the protected API boundary for sign-in, sign-out, access-denied, and token-acquisition-failure outcomes
- validation-problem payloads for unsupported or malformed auth-audit event submissions
- display-name fallback behavior for auth-audit summaries when `preferred_username`, `name`, or both claims are absent
- validation-problem payload shape for invalid protected configuration updates
- secret-safe responses
- role-boundary enforcement across protected API routes

The API unit suite now uses direct compile-time access to internal authentication and configuration-validation types instead of the former generic `ApiReflection` helper. A small internal auth-audit resolver seam now owns the summary, severity, and display-name fallback rules so supported audit events, unsupported event rejection, and username fallback order can be validated cheaply before the higher-cost integration tests persist events through the protected runtime boundary.

### UI behavior

The Web unit, functional, and end-to-end tests cover:

- shared authorization policy registration for viewer, operator, and administrator routes
- anonymous, no-role, and elevated-role operator-context mapping
- theme-mode parsing and Radzen Software theme selection for the shared UI shell
- delegated-scope token evaluation and navigation recovery decisions for protected UI flows
- lower-level protected-route redirect decisions and auth-audit helper behavior
- direct `PlatformApiClient` success and failure-path handling for status, configuration, events, manual retry, and auth-administration requests
- bUnit coverage for the refreshed `MainLayout`, `Home`, `Status`, and `Configuration` surfaces, including signed-in versus signed-out rendering, degraded warnings, manual-retry affordances, current IG login-state labels, latest payload details, configuration save-state behavior, and access-denied routing
- public landing-page behavior
- local sign-in surface behavior in lightweight automated runs
- unit-level route-first anonymous challenge behavior for protected status, configuration, and administrator surfaces
- lightweight functional checks for public entry, tampered external return targets, and the hardened GET-versus-POST sign-out boundary
- one retained functional sign-out smoke that proves a real Keycloak-backed sign-out forces the next protected navigation back to sign-in
- one retained functional insufficient-role smoke that proves a signed-in viewer is denied from the operator-only configuration route through the real runtime
- one retained functional CSRF negative that proves the real sign-out POST rejects requests without the antiforgery token
- one retained real Keycloak browser smoke that discovers the live Web listener from AppHost startup output and reaches the protected UI without fixed-port assumptions

Focused manual validation still complements the automated suite for the refreshed shared shell, theme switching, remembered browser preference, header presentation, and narrower-width layout behavior.

## Coverage map

The current suite ownership is intentionally pyramid-shaped so the repository keeps most auth and UI confidence in cheaper tests and retains only a narrow real-runtime smoke set.

| Area or risk | Primary suites | Retained high-cost coverage |
| --- | --- | --- |
| Route-first anonymous challenge and return-url normalization | Web unit tests, Web functional tests, API integration tests | No retained real-runtime protected-route matrix; only the lightweight public-entry and external-return functional checks remain |
| UI role boundaries and access-denied routing | Web unit tests, Web functional tests | One real-runtime functional viewer-to-configuration denial smoke |
| Sign-out wiring, fail-closed post-sign-out behavior, and OIDC logout participation | Web unit tests, API integration tests, Web functional tests | One real-runtime functional post-sign-out protected-route smoke |
| Sign-out CSRF hardening | Web functional tests | One real-runtime functional missing-antiforgery negative |
| Browser-provider parity for the delivered local auth path | Web unit tests, Web functional tests | One real-runtime E2E sign-in smoke from AppHost listener discovery to protected content |
| Refreshed Blazor shell and operator-page rendering | Web unit tests with bUnit | Manual responsive and theme checks only |

The current real-runtime auth matrix is intentionally narrow: one browser sign-in smoke, one functional sign-out smoke, one functional insufficient-role smoke, and one functional CSRF negative. Broader route matrices, role-policy checks, API-boundary checks, and rendered-component checks now live in lower-level suites so the distributed layer stays small and evidence-driven.

## Refreshed quality evidence for work package 005

The latest mitigation reruns for `docs/005-refactor-app-host/` are recorded in [Quality evidence after test mitigation](../005-refactor-app-host/004-quality-evidence-after-test-mitigation.md).

### Latest Coverlet evidence

| Test project | Line coverage | Branch coverage | Material outcome |
| --- | --- | --- | --- |
| `AppHost.UnitTests` | `94.69%` | `100.00%` | New direct AppHost coverage now protects the refactored support units with a strong low-cost regression net. |
| `Application.UnitTests` | `55.11%` | `42.81%` | Stable versus the baseline review. |
| `Infrastructure.UnitTests` | `36.51%` | `24.46%` | Modest improvement over the baseline review. |
| `Api.UnitTests` | `5.87%` | `7.23%` | Improved over the baseline review, but still the weakest lower-level coverage area. |
| `Web.UnitTests` | `34.17%` | `26.16%` | Stable versus the baseline review. |

### Latest Stryker evidence

| Test project | Mutation score | Material outcome |
| --- | --- | --- |
| `AppHost.UnitTests` | `77.86%` | New strong mutation signal for AppHost settings, wiring, and composition support behavior. |
| `Application.UnitTests` | `18.80%` | Unchanged from the baseline review; future gains should come from application orchestration scenarios rather than extra percentage-only tests. |
| `Infrastructure.UnitTests` | `27.83%` | Slight improvement over the baseline review. |
| `Api.UnitTests` | `26.56%` | Material improvement over the baseline review, but still below the stronger AppHost signal. |
| `Web.UnitTests` | Not available | The rerun still fails inside Stryker when mutating `src/TNC.Trading.Platform.Web/Program.cs`, so Web mutation evidence remains blocked pending a Stryker-compatible fix or isolation strategy. |

The refreshed evidence shows that the mitigation materially improved the AppHost and API fast-feedback story without broadening the distributed suites. The remaining low-signal areas are still better treated as future prioritization input than as a reason to add brittle tests.

## Quality characteristics currently protected

The automated suite already checks important non-functional expectations:

- environment safety for Test versus Live selection
- write-only secret handling
- redaction of sensitive values from records and responses
- persisted auth audit history for sign-in, sign-out, denied access, and token-acquisition failures without exposing tokens
- stable health endpoints for orchestration
- restart-required behavior for startup-fixed configuration changes
- durable operational history when SQL-backed persistence is used
- degraded-state visibility instead of silent failure

## Test naming and traceability

The repository uses descriptive test names and requirement comments.

In the current suites, tests trace directly to the requirement sources that introduced or refined the delivered behavior:

- `docs/003-authentication-and-authorisation/requirements.md` for sign-in, sign-out, role-boundary, and local Keycloak behavior
- `docs/004-ui-update-and-refactor/requirements.md` for the refreshed Blazor shell and operator-page rendering behavior
- `docs/005-refactor-app-host/requirements.md` and `docs/005-refactor-app-host/plans/003-work-package-test-mitigation-plan.md` for test-pyramid hardening, fixture reuse, and reduced distributed-suite scope

This keeps the traceability view aligned with the current repository state instead of only the older environment-foundation package.

## Running the tests

From the repository root:

```powershell
 dotnet test
```

If a local environment hits intermittent MSBuild child-node exits during the repository-wide run, use serialized execution:

```powershell
 dotnet test -m:1
```

To build first:

```powershell
 dotnet build
 dotnet test
```

## Local test behavior notes

Many integration, functional, and end-to-end tests run through the Aspire AppHost.

The auth-focused distributed suites now run against the real Aspire-managed AppHost and Keycloak local runtime.

The Web auth suites may still use `Authentication__Test__EnableInteractiveSignIn=true` where a browser-driven helper surface is required, but that setting no longer changes AppHost composition or switches the runtime into a synthetic mode.

This keeps the suites aligned with the delivered local runtime while still exercising the distributed application shape. Distributed validation now uses the supported Docker plus Keycloak local runtime rather than an in-memory substitute path.

For Web auth scenarios, the shared real-runtime helpers start the AppHost as a real process, discover listener URLs from runtime output, and establish authenticated browser sessions before copying the resulting platform cookie into the functional client container.

API integration tests now prefer real Keycloak-issued bearer tokens for protected-route coverage and reuse one shared real AppHost-plus-Keycloak runtime per collection for the retained real-token auth matrix. Only the isolated invalid-issuer, invalid-audience, expired-token, audit-validation, and display-name-fallback negatives remain synthetic, and those tests use a separate AppHost-scoped API provider override so the API can validate controlled JWT and claim-shape inputs without changing the default Keycloak-backed runtime.

For protected-route and sign-out functional coverage, the test suites prefer deterministic cookie-container control and redirect assertions instead of arbitrary waits or a substitute runtime.

The retained real-infrastructure auth smoke stays intentionally narrow. It reuses one AppHost-plus-Keycloak runtime per E2E collection, discovers the AppHost-started Web UI endpoint from runtime listener output instead of `launchSettings.json`, and then exercises one real Keycloak sign-in journey to the protected `/status` surface.

The functional auth collection also reuses one AppHost-plus-Keycloak runtime and keeps only the real-runtime cases that still add unique value above the lower-level suites: post-sign-out fail-closed behavior, one insufficient-role route denial, and the CSRF-negative sign-out boundary.

## Manual validation areas still worth checking

Automated tests cover a large part of the current control plane, but manual review is still useful for:

- starting the AppHost and checking dashboard links for the Web UI, API, Keycloak, Mailpit, and Scalar surfaces together with service startup output
- signing in through the live Keycloak local realm and confirming the protected operator Web experience loads through the real AppHost-managed route flow
- calling the public API health endpoints and one protected API route through the AppHost-exposed listener set
- checking local Mailpit behavior when infrastructure containers are enabled
- checking Scalar UI in development
- reviewing generated logs during degraded and recovery scenarios

The distributed auth suites should now be interpreted as real-runtime validation of the AppHost topology rather than as a synthetic fallback path.

The refreshed quality evidence also confirms that the cheapest new AppHost suite now carries most of the AppHost refactor confidence, while the distributed auth suites remain intentionally narrow and focused on the unique real-runtime behaviors that unit tests cannot prove.

## Quality checklist for future changes

When extending the application, keep these areas protected:

- do not expose stored secrets in UI, API, logs, or records
- preserve Test-platform live safety rules
- keep degraded-state UI available
- keep health endpoints stable
- maintain environment tagging in events and notifications
- keep retry behavior observable and deterministic

## Related documents

- [Application overview](application-overview.md)
- [Local development guide](local-development.md)
- [Runtime behavior](runtime-behavior.md)
