# Authentication and Authorisation Test Review Report

This report reviews the current automated testing for work package `docs/003-authentication-and-authorisation/` after delivery of `plans/001-delivery-plan.md`.

## Review scope

- **Work package**: `./docs/003-authentication-and-authorisation/`
- **Review depth**: `standard`
- **Reviewer perspective**: `Senior Test Architect`
- **Reviewed artifacts**:
  - `docs/003-authentication-and-authorisation/requirements.md`
  - `docs/003-authentication-and-authorisation/technical-specification.md`
  - `docs/003-authentication-and-authorisation/plans/001-delivery-plan.md`
  - `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/PlatformWebAuthenticationServiceCollectionExtensionsTests.cs`
  - `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/PlatformOperatorContextAccessorTests.cs`
  - `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/PlatformAccessTokenProviderTests.cs`
  - `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/PlatformAuthAuditClientTests.cs`
  - `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/PlatformNavigationAccessCoordinatorTests.cs`
  - `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/PlatformAuthorizationRedirectResolverTests.cs`
  - `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/Authentication/PlatformAuthenticationFunctionalTests.cs`
  - `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/Authentication/PlatformProtectedRouteFunctionalTests.cs`
  - `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/Authentication/PlatformAuthenticationE2ETests.cs`
  - `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/Authentication/PlatformDashboardAuthenticationE2ETests.cs`
  - `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/PlatformApiAuthenticationServiceCollectionExtensionsTests.cs`
  - `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/PlatformAuthenticationIntegrationTests.cs`

## Executive summary

- **Overall test confidence**: `medium`
- **Overall coverage assessment**: `partial`
- **Top concerns**:
  1. `F1` Real identity-provider coverage is thin compared with the extensive test-provider coverage; most lifecycle tests run against the local test provider, with only one retained real Keycloak sign-in smoke.
  2. `F2` Authenticated UI-state coverage is narrower than the requirement set; the suite proves access control well, but it does not directly assert display-name-first landing-page rendering, role-aware navigation, or access-denied guidance quality.
  3. `F3` Observability and provider-parity confidence is incomplete; audit persistence and some redaction are covered, but structured logs, retention behavior, and valid Entra/Keycloak runtime branches are not exercised deeply.

Current execution state is healthy: `run_tests` completed `80` relevant tests with `80 passed, 0 failed, 0 skipped` across Web unit (`23`), Web functional (`27`), Web E2E (`4`), API unit (`8`), and API integration (`18`) coverage.

## Requirement coverage matrix

| Requirement / area | Existing coverage | Evidence | Gap assessment | Recommendation |
| --- | --- | --- | --- | --- |
| `FR1` sign-in, sign-out, session recovery | Partial | `PlatformAuthenticationFunctionalTests.SignOut_ShouldReturnLandingPageContent_WhenSignedInOperatorEndsPlatformSession`; `ConfigurationRoute_ShouldRedirectToSignIn_WhenOperatorRequestsProtectedRouteAfterSignOut`; `ConfigurationPage_ShouldRequireReauthentication_WhenPlatformSessionIsLost`; `PlatformDashboardAuthenticationE2ETests.OperatorUi_ShouldRenderPlatformStatus_WhenSeededViewerSignsInFromAspireDashboard` | Platform-session lifecycle is well covered against the test provider, but real Keycloak sign-out, callback failure, and real-session recovery are not automated. `F1` | Add real-provider E2E lifecycle coverage for sign-out, re-authentication, and failed callback handling. |
| `FR2` public entry and public surfaces | Covered | `PlatformAuthenticationFunctionalTests.LandingPage_ShouldReturnPublicEntryContent_WhenAnonymousUserRequestsRoot`; `SignInPage_ShouldListSeededLocalUsers_WhenTestProviderIsActive`; `PlatformAuthenticationIntegrationTests.HealthEndpoints_ShouldReturnOk_WhenRequestedAnonymously` | Public entry and health/readiness behavior are directly exercised. | Keep this baseline and add a small endpoint inventory assertion if more anonymous endpoints are introduced. |
| `FR3` pre-provisioned users and no-role denial | Partial | `PlatformOperatorContextAccessorTests.GetCurrentAsync_ShouldReturnAuthenticatedNoRoleContext_WhenPrincipalHasNoPlatformRole`; `PlatformProtectedRouteFunctionalTests.StatusRoute_ShouldRedirectToAccessDenied_WhenNoRoleUserRequestsProtectedRoute`; `PlatformAuthenticationE2ETests.LandingPage_ShouldRouteNoRoleUserToAccessDenied_WhenNoRoleUserSignsIn`; `PlatformAuthenticationIntegrationTests.StatusEndpoint_ShouldReturnForbidden_WhenAuthenticatedCallerHasNoPlatformRole` | No-role handling is covered, but there is no automated evidence for rejection of unknown or non-seeded users through the real provider. `F1` | Add E2E coverage for non-seeded login failure in the Keycloak path or document why it must remain provider-owned and manually validated. |
| `FR4` expose authentication state to the Blazor UI | Partial | `PlatformOperatorContextAccessorTests.GetCurrentAsync_ShouldReturnAnonymousContext_WhenUserIsNotAuthenticated`; `GetCurrentAsync_ShouldUseFallbackDisplayName_WhenPrimaryDisplayNameClaimIsMissing`; `PlatformNavigationAccessCoordinatorTests.EnsureRequiredScopesAsync_ShouldReturnTrue_WhenRequiredScopeIsAlreadyGranted` | Lower-level mapping is covered, but UI rendering of authenticated context is not directly asserted on the landing page or navigation. `F2` | Add functional tests for authenticated landing-page display name, anonymous state, and role-aware navigation links. |
| `FR5` protect operator-only Blazor routes | Covered | `PlatformProtectedRouteFunctionalTests.StatusRoute_ShouldRedirectToSignIn_WhenAnonymousUserRequestsProtectedRoute`; `ConfigurationRoute_ShouldRedirectToSignIn_WhenAnonymousUserRequestsProtectedRoute`; `AuthenticationAdministrationRoute_ShouldRedirectToSignIn_WhenAnonymousUserRequestsProtectedRoute`; route matrix theory | Strong anonymous-route and role-boundary coverage on representative protected routes. | Extend only when new protected routes are added. |
| `FR6` protect operator-only API endpoints | Covered | `PlatformAuthenticationIntegrationTests.StatusEndpoint_ShouldReturnUnauthorized_WhenAnonymousCallerRequestsProtectedSurface`; `StatusEndpoint_ShouldReturnOk_WhenViewerBearerTokenIsProvided`; `ConfigurationEndpoint_ShouldReturnForbidden_WhenViewerTokenLacksOperatorRole`; invalid issuer/audience/signature/expiry tests | API protection and fail-closed behavior are strongly covered on representative endpoints. | Add endpoint-inventory traceability to keep future protected endpoints from escaping coverage. |
| `FR7` named roles applied consistently | Covered | `PlatformWebAuthenticationServiceCollectionExtensionsTests.AddPlatformWebAuthentication_ShouldRegisterExpectedRolePolicies_WhenConfiguredForTests`; Web route matrix theory; API role tests for viewer/operator/admin endpoints | Shared role policy setup and role boundaries are exercised across Web and API. | Keep using shared-role tests as the first line of regression defense. |
| `FR8` minimal authenticated user context | Partial | `PlatformOperatorContextAccessorTests.GetCurrentAsync_ShouldUseFallbackDisplayName_WhenPrimaryDisplayNameClaimIsMissing`; `GetCurrentAsync_ShouldHonorConfiguredClaimTypes_WhenCustomRoleAndDisplayNameClaimsAreUsed` | Mapping logic is covered, but there is no functional assertion that only the intended minimal context is rendered to the UI. `F2` | Add functional assertions for displayed name, absent extra claims, and anonymous UI state. |
| `FR9` administrator/operator/viewer role boundaries | Covered | Web route matrix theory; `PlatformAuthenticationIntegrationTests.ConfigurationEndpoint_ShouldReturnOk_WhenOperatorUpdatesConfiguration`; `AuthAdministrationEndpoint_ShouldReturnOk_WhenAdministratorTokenIsProvided`; `AuthAdministrationEndpoint_ShouldReturnForbidden_WhenOperatorTokenLacksAdministratorRole` | Representative role matrix coverage is strong across Web and API. | Add new matrix rows whenever privileged surfaces are introduced. |
| `FR10` dedicated access-denied experience | Partial | `PlatformAuthorizationRedirectResolverTests.CreateDecision_ShouldReturnAccessDeniedDestination_WhenUserIsAuthenticated`; Web no-role route tests; `PlatformAuthenticationE2ETests.LandingPage_ShouldRouteNoRoleUserToAccessDenied_WhenNoRoleUserSignsIn`; API `403` tests | Routing to denied experiences is covered, but the denied page guidance and safe-content assertions are shallow. `F2`, `F5` | Strengthen functional assertions for denial explanation text, guidance, and non-disclosure of sensitive authorization detail. |
| `NF1` standards compatibility | Partial | Web and API auth registration unit tests for provider selection and configuration validation; one real Keycloak E2E smoke | The suite proves the test provider and some configuration guards, but not valid Entra runtime behavior and only a narrow real Keycloak path. `F1`, `F3` | Add provider-parity startup/request tests for valid Keycloak and configuration-binding coverage for Entra. |
| `NF2` fail-closed reliability | Partial | Web sign-out/session-loss tests; access-token expiry/not-yet-valid tests; API invalid token tests; anonymous-route redirects | Fail-closed behavior is a strength, but invalid OIDC callback handling and real provider session expiry are not automated. `F1`, `F6` | Add callback-failure and real-session-expiry coverage at functional or E2E level. |
| `NF3` externalized configuration | Partial | `PlatformWebAuthenticationServiceCollectionExtensionsTests` and `PlatformApiAuthenticationServiceCollectionExtensionsTests` validate missing/unsupported provider settings | Coverage stops at startup validation and does not prove valid non-test provider runtime paths in automation. `F3` | Add integration coverage for valid provider binding and startup for supported non-test branches where practical. |
| `NF4` observability without secret leakage | Partial | `PlatformAuthAuditClientTests`; Web audit-rendering functional tests; API audit persistence tests | Audit flows are well covered, but structured logs, metrics/traces, and retention semantics are not directly asserted. `F3` | Add tests for log event shape/redaction and retention behavior in the auth event store. |
| `NF5` understandable UX | Partial | Landing page public content, sign-in choices, sign-out return, access-denied heading | Headings and route outcomes are covered, but detailed operator guidance and authenticated landing-page UX are not. `F2`, `F5` | Add richer content assertions for signed-in landing and denied states. |
| `SR1` authenticated and role-based protection | Covered | Web route protection tests; API `401`/`403`/authorized tests; role-policy registration tests | Strong evidence across Web and API. | Maintain with endpoint and route inventory checks. |
| `SR2` no secret leakage to client/logs/reports | Partial | `PlatformAuthAuditClientTests.RecordTokenAcquisitionFailedAsync_ShouldExcludeAccessTokenFromAuditPayload_WhenAccessTokenIsSupplied`; audit persistence tests assert signing key and token prefix are absent | Good targeted redaction checks, but no direct automated checks of structured logs or broader rendered UI output beyond selected pages. `F3` | Add log-capture tests and a small browser-focused assertion set for sensitive text absence on auth pages. |
| `SR3` secrets externalized and rotatable | Partial | Web/API configuration validation unit tests | Evidence is limited to validation behavior rather than runtime secret-source behavior. `F3` | Add configuration-source tests or a documented manual validation checklist reference in automation notes. |
| `SR4` invalid/tampered/expired auth rejected safely | Partial | API invalid issuer/audience/signature/expiry tests; `SignInEndpoint_ShouldRedirectToLandingPage_WhenExternalReturnUrlIsSupplied`; access-token invalidity unit tests | Token and return-url failures are covered, but invalid OIDC callback/state/correlation failures are not. `F6` | Add functional tests around invalid callback inputs or middleware-level failure handling. |
| `DR1` auth audit retention | Partial | API audit persistence tests; Web audit history rendering tests | Persistence is covered, but 90-day retention behavior is not exercised. `F3` | Add lower-level retention tests around auth event queries/cleanup semantics. |
| `IR1` local IdP integration | Partial | `PlatformDashboardAuthenticationE2ETests.OperatorUi_ShouldRenderPlatformStatus_WhenSeededViewerSignsInFromAspireDashboard` | Only one real Keycloak browser flow is retained. Sign-out, denial, and admin flows on the real provider are not covered. `F1` | Expand the real-provider smoke into a small lifecycle suite. |
| `IR2` delegated Web-to-API propagation | Partial | `PlatformAccessTokenProviderTests`; `PlatformNavigationAccessCoordinatorTests`; API integration tests on bearer tokens; Web functional privileged-scope preservation test | Delegated token handling is well covered with the test provider, but real elevated-scope acquisition and renewal behavior are not. `F1`, `F6` | Add targeted tests for elevated-scope acquisition and refusal behavior beyond preserved query parameters. |
| `TR1` anonymous versus authenticated access | Covered | Web protected-route tests; API anonymous versus authenticated tests; Web functional sign-in flows | Strong cross-layer evidence exists. | Preserve this split as the baseline regression suite. |
| `TR2` named role coverage | Covered | Web role matrix theory; API admin/operator/viewer tests; no-role tests | Strong role-matrix evidence across representative Web and API surfaces. | Keep matrix coverage aligned to new protected surfaces. |
| `TR3` local validation for sign-in/sign-out/recovery | Partial | Functional sign-out and re-auth tests; E2E Keycloak sign-in smoke | Most local validation is automated only against the test provider; the real AppHost plus Keycloak path is narrower. `F1` | Add a minimal real-provider lifecycle pack for sign-in, sign-out, and recovery. |

## Existing test strengths

- The suite spans four useful levels: unit, integration, functional, and E2E, which is appropriate for this authentication-heavy work package.
- Web route protection is well covered through deterministic functional tests and a compact role matrix in `PlatformProtectedRouteFunctionalTests`.
- API protection is a strong area: anonymous `401`, authenticated `403`, authorized success, and invalid issuer/audience/signature/expiry are all exercised in `PlatformAuthenticationIntegrationTests`.
- Traceability comments are consistently present in the auth-focused tests, making requirement intent and test purpose easy to audit.
- Secret-safety assertions are already present in several places, especially around audit payloads and persisted event content.

## Gaps in testing

### Missing coverage

- `F1` Real identity-provider automation is too narrow. The suite has one real Keycloak sign-in smoke in `PlatformDashboardAuthenticationE2ETests`, but the broader lifecycle remains test-provider-driven: no real-provider sign-out, session-expiry recovery, admin flow, or elevated-scope denial path is automated.
- `F2` Authenticated UX coverage is incomplete. The suite proves route outcomes and headings, but does not directly verify the authenticated landing page, display-name-first rendering, role-aware navigation, or access-denied guidance promised by `FR4`, `FR8`, and `NF5`.
- `F3` Provider-parity and observability coverage is incomplete. Valid Keycloak and Entra runtime branches are not exercised deeply, and log/retention behavior is not directly tested even though audit persistence is.
- `F6` Invalid OIDC callback and middleware-driven failure paths are not evidenced by current automated tests, leaving a documented `SR4`/`NF2` scenario unverified.

### Weak or fragile tests

- `F5` Several functional and E2E tests assert only path or heading presence. That is useful for routing confidence, but it is too weak to catch regressions in operator guidance, role-aware navigation, or accidental sensitive-detail disclosure.
- `F7` The current suite relies heavily on representative endpoint tests rather than an explicit protected-endpoint inventory. That keeps the suite lean, but it increases the chance that a newly added protected surface could miss equivalent authorization coverage.

### Risks not adequately tested

- `F8` Unknown-user or real-provider authentication failure behavior is not proven in automation. No current test demonstrates that a non-seeded or misconfigured external login fails safely in the real Keycloak path.
- `F9` Audit retention is documented for 90 days in `DR1`, but no automated evidence currently verifies retention semantics, pruning, or query-boundary behavior.
- `F10` Elevated delegated-scope behavior is only partially tested. The suite proves preserved scope requests and fail-closed token checks, but not a real interactive elevation or refusal flow against the real provider.

## Recommendations to strengthen existing tests

1. Strengthen Web functional and E2E assertions around authenticated content (`F2`, `F5`) by checking display name, role-appropriate links, denied-page guidance, and absence of sensitive detail instead of only headings and final routes.
2. Strengthen observability coverage (`F3`, `F9`) by capturing auth log output or lower-level log entries and by adding tests that verify retention/query semantics for persisted auth events.
3. Strengthen provider-parity confidence (`F1`, `F3`, `F6`) by expanding the current real Keycloak smoke into a small lifecycle pack and by adding valid-configuration startup/request-path tests for supported non-test branches where infrastructure permits.

## Recommendations for new tests

| Priority | Area | Test level | Recommendation | Reason |
| --- | --- | --- | --- | --- |
| High | Real Keycloak lifecycle | E2E | Add a real-provider suite that covers sign-in, platform sign-out, denied re-entry to a protected route, and successful re-authentication. | Closes `F1` and raises confidence in `FR1`, `IR1`, and `TR3` on the actual local path. |
| High | OIDC callback failure handling | Functional / E2E | Add tests for invalid or tampered callback inputs, correlation failure, and safe fallback to an anonymous or denied experience. | Closes `F6` and directly addresses `NF2` and `SR4`. |
| High | Authenticated landing page and navigation | Functional | Add tests that assert display-name-first rendering, role-aware links, anonymous absence of operator context, and concise denied-page guidance. | Closes `F2` and hardens `FR4`, `FR8`, `FR10`, and `NF5`. |
| Medium | Provider parity | Integration / E2E | Add valid Keycloak branch startup coverage and, where feasible, configuration-binding tests for the Entra branch. | Closes `F3` and reduces risk that only the test provider is truly exercised. |
| Medium | Audit retention | Unit / Integration | Add tests for auth-event retention boundaries, date filtering, and cleanup behavior against the event store/query path. | Closes `F9` and gives real evidence for `DR1`. |
| Medium | Protected surface inventory | Unit / Integration | Add a lightweight inventory test or convention test that enumerates protected Web/API surfaces and asserts expected auth metadata or policy assignment. | Closes `F7` and helps prevent silent gaps when new protected features are added. |
| Medium | Elevated-scope refusal and recovery | Functional / E2E | Add tests for privileged-area elevation refusal, admin-scope recovery prompts, and controlled operator messaging when elevated access cannot be acquired. | Closes `F10` and better covers `IR2`, `FR9`, and `SR4`. |

## Hardening recommendations

- Keep the existing requirement-traceability comments; they are a strength of the suite and should be extended to any new auth tests.
- Prefer lower-level retention, logging, and inventory tests before adding more expensive browser coverage.
- Separate test-provider coverage from real-provider coverage explicitly in naming or test traits so failures show whether the regression is in app logic or infrastructure integration.
- For functional and E2E tests, assert meaningful page content and safe error messaging, not only headings and final paths.
- Consider adding a small protected-surface checklist to CI so new routes or endpoints cannot be introduced without explicit auth-test intent.

## Assumptions and missing information

- This review is based on the current repository documentation and the currently discoverable automated tests; it does not include a line-coverage report.
- The review assumes the real Entra branch is not currently available for automated environment-backed execution in this workspace.
- The assessment focuses on auth-related tests for this work package and not on unrelated application tests in the solution.
- The report treats the current `run_tests` result (`80/80` passing) as the current execution state for the reviewed auth suites.

## Suggested next steps

1. Implement the high-priority real-provider lifecycle and callback-failure tests first (`F1`, `F6`, `F8`) so the actual Keycloak path is no longer a single-smoke dependency.
2. Add authenticated landing-page, denied-page, and role-aware navigation assertions next (`F2`, `F5`) to close the most visible UX and disclosure gaps.
3. Add retention, logging, and protected-surface inventory tests after that (`F3`, `F7`, `F9`, `F10`) to harden long-term regression confidence without overloading the browser suite.
