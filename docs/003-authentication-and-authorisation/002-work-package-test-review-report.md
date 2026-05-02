# Work Package Test Review Report

This report reviews the current automated test approach for the authentication and authorisation work package and assesses whether the implemented test coverage is strong enough to treat the package as hardened and complete.

## Review scope

- **Work package**: `./docs/003-authentication-and-authorisation/`
- **Review depth**: `standard`
- **Reviewer perspective**: `Senior Test Architect`
- **Reviewed artifacts**:
  - `docs/003-authentication-and-authorisation/requirements.md`
  - `docs/003-authentication-and-authorisation/technical-specification.md`
  - `docs/003-authentication-and-authorisation/plans/001-delivery-plan.md`
  - `src/TNC.Trading.Platform.Web/Authentication/*`
  - `src/TNC.Trading.Platform.Web/Components/Authorization/*`
  - `src/TNC.Trading.Platform.Api/Authentication/*`
  - `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/*`
  - `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/Authentication/*`
  - `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/Authentication/*`
  - `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/*`

## Executive summary

- **Overall test confidence**: `medium`
- **Overall coverage assessment**: `partial`
- **Top concerns**:
  1. `F1` The only real AppHost plus Keycloak browser smoke currently fails, so `IR1` and the strongest `TR3` evidence are not green.
  2. `F2` UI role-boundary coverage is thinner than API role-boundary coverage, especially for viewer versus operator versus administrator behavior across protected routes and privileged elevation paths.
  3. `F3` Observability and configuration risks are only partially covered because current tests focus on audit-event payloads and happy-path binding, not structured log redaction or missing-provider configuration failures.

The current automated suite gives good confidence in the local test-provider path and in API fail-closed behavior. The strongest evidence is in Web unit tests, Web functional tests, and API integration tests. Current execution state from this review is:

- `dotnet build` at the repository root: passed.
- `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests`: passed (`16/16`).
- `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests`: passed (`11/11`).
- `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests`: passed (`18/18`).
- `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests`: failed (`3/4`), with `PlatformDashboardAuthenticationE2ETests.OperatorUi_ShouldRenderPlatformStatus_WhenSeededViewerSignsInFromAspireDashboard` timing out because `https://localhost:7281/authentication/sign-in?returnUrl=%2Fstatus` never became reachable.

## Requirement coverage matrix

| Requirement / area | Existing coverage | Evidence | Gap assessment | Recommendation |
| --- | --- | --- | --- | --- |
| `FR1`, `TR3` sign-in, sign-out, and session recovery | Partial | `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/Authentication/PlatformAuthenticationFunctionalTests.cs` - `ConfigurationPage_ShouldReturnProtectedContent_WhenOperatorSignsInThroughTestProvider`, `SignOut_ShouldReturnLandingPageContent_WhenSignedInOperatorEndsPlatformSession`, `ConfigurationRoute_ShouldRedirectToSignIn_WhenOperatorRequestsProtectedRouteAfterSignOut`, `ConfigurationPage_ShouldRequireReauthentication_WhenPlatformSessionIsLost` | Strong on the local test-provider flow, but only partial for the real Keycloak path because the real browser smoke is failing and there is no real-provider sign-out or re-authentication coverage. | Fix `F1` first, then add one real-provider sign-out and one real-provider re-authentication smoke that reuse the working AppHost path. |
| `FR2` public entry and public surfaces | Covered | `PlatformAuthenticationFunctionalTests.LandingPage_ShouldReturnPublicEntryContent_WhenAnonymousUserRequestsRoot`; `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/PlatformAuthenticationIntegrationTests.cs` - `HealthEndpoints_ShouldReturnOk_WhenRequestedAnonymously`; `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/Authentication/PlatformProtectedRouteFunctionalTests.cs` anonymous redirect tests | Public landing and health endpoints are well covered for anonymous access. | Keep as baseline regression coverage. |
| `FR3`, `FR10` no-role handling and access denied | Covered | `PlatformOperatorContextAccessorTests.GetCurrentAsync_ShouldReturnAuthenticatedNoRoleContext_WhenPrincipalHasNoPlatformRole`; `PlatformProtectedRouteFunctionalTests.StatusRoute_ShouldRedirectToAccessDenied_WhenNoRoleUserRequestsProtectedRoute`; `PlatformAuthenticationIntegrationTests.StatusEndpoint_ShouldReturnForbidden_WhenAuthenticatedCallerHasNoPlatformRole`; `PlatformAuthenticationE2ETests.LandingPage_ShouldRouteNoRoleUserToAccessDenied_WhenNoRoleUserSignsIn` | Coverage proves the documented no-role behavior across unit, functional, integration, and browser levels. | Retain; add one assertion for access-denied guidance text if the page content becomes more specific. |
| `FR4`, `FR8` UI authentication state and minimum operator context | Covered | `PlatformOperatorContextAccessorTests.GetCurrentAsync_ShouldReturnAnonymousContext_WhenUserIsNotAuthenticated`, `GetCurrentAsync_ShouldUseFallbackDisplayName_WhenPrimaryDisplayNameClaimIsMissing`, `GetCurrentAsync_ShouldHonorConfiguredClaimTypes_WhenCustomRoleAndDisplayNameClaimsAreUsed`; `PlatformAuthenticationFunctionalTests.ConfigurationPage_ShouldReturnProtectedContent_WhenOperatorSignsInThroughTestProvider` | Good lower-level coverage for display-name mapping and derived capability flags. | Add one functional assertion that the rendered authenticated landing page shows only the intended minimum operator context. |
| `FR5` protected Blazor routes | Covered | `PlatformAuthorizationRedirectResolverTests.CreateDecision_ShouldReturnSignInDestination_WhenUserIsAnonymous`; `PlatformProtectedRouteFunctionalTests.StatusRoute_ShouldRedirectToSignIn_WhenAnonymousUserRequestsProtectedRoute`, `ConfigurationRoute_ShouldRedirectToSignIn_WhenAnonymousUserRequestsProtectedRoute`, `AuthenticationAdministrationRoute_ShouldRedirectToSignIn_WhenAnonymousUserRequestsProtectedRoute` | Anonymous challenge behavior is well exercised. | Keep current coverage and extend it with authenticated role-matrix route checks from `F2`. |
| `FR6` protected API endpoints | Covered | `PlatformAuthenticationIntegrationTests.StatusEndpoint_ShouldReturnUnauthorized_WhenAnonymousCallerRequestsProtectedSurface`, invalid issuer/audience/signature/expiry tests, `ConfigurationEndpoint_ShouldReturnForbidden_WhenViewerTokenLacksOperatorRole`, `ConfigurationEndpoint_ShouldReturnOk_WhenOperatorUpdatesConfiguration` | API protection and fail-closed token validation are strong. | Keep as a model for the thinner UI authorization coverage. |
| `FR7`, `FR9`, `TR2` shared role matrix across UI and API | Partial | `PlatformWebAuthenticationServiceCollectionExtensionsTests.AddPlatformWebAuthentication_ShouldRegisterExpectedRolePolicies_WhenConfiguredForTests`; `PlatformAuthenticationIntegrationTests` role tests for viewer, operator, and administrator; `PlatformAuthenticationE2ETests.AuthenticationAdministrationPage_ShouldRenderForAdministrator_WhenAdminSignsInWithAdminScope` | API role coverage is strong, but UI role coverage is incomplete. There is no full browser or functional matrix proving viewer, operator, and administrator outcomes across `/status`, `/configuration`, and `/administration/authentication`. | Add focused Web functional tests for each seeded role against each protected route before adding more E2E breadth. |
| `NF2`, `SR4` fail-closed missing, invalid, or expired auth state | Partial | `PlatformAccessTokenProviderTests.GetAccessTokenAsync_ShouldThrowInvalidOperationException_WhenSessionHasNoAccessToken`; `PlatformNavigationAccessCoordinatorTests` anonymous and missing-scope redirects; API invalid-token tests; `PlatformAuthenticationFunctionalTests.ConfigurationPage_ShouldRequireReauthentication_WhenPlatformSessionIsLost` | Strong for missing tokens, invalid bearer tokens, and lost sessions. Missing coverage for invalid OIDC callback handling, declined elevation, and silent renewal boundaries. | Add lower-level tests for callback rejection and privileged-scope acquisition failure paths. |
| `NF4`, `DR1`, `SR2` auth observability and secret safety | Partial | `PlatformAuthAuditClientTests`; `PlatformAuthenticationIntegrationTests.AuthAuditEndpoint_ShouldPersistSignInEvent_WhenAuthenticatedCallerPostsAuditRecord`, `...SignOutEvent...`, `...AccessDeniedEvent...`, `...TokenAcquisitionFailedEvent...` | Audit-event persistence is covered and checks for obvious secret leakage are present, but the tests post directly to the audit API and do not verify that Web log messages or end-to-end audit emission remain secret-safe. | Add tests that drive Web sign-in, sign-out, denial, and scope-failure flows and then assert the resulting audit records and log output. |
| `IR1` local identity-provider integration | Partial | `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/Authentication/PlatformDashboardAuthenticationE2ETests.cs` - `OperatorUi_ShouldRenderPlatformStatus_WhenSeededViewerSignsInFromAspireDashboard` | The intended real AppHost plus Keycloak coverage exists but is currently failing, so this requirement is not in a reliable green state. | Repair the E2E test to discover the actual runtime Web endpoint instead of relying on a fixed launch-settings URL. |
| `IR2` delegated Web-to-API access and scope propagation | Partial | `PlatformAccessTokenProviderTests` required-scope success and failure tests; `PlatformNavigationAccessCoordinatorTests` sign-in redirection with scope preservation; API role and scope integration tests | Lower-level pieces are covered, but there is limited end-to-end proof that a browser journey acquires elevated scope and then successfully crosses the Web-to-API boundary for privileged areas. | Add one functional or E2E privileged-area journey for operator scope and one for administrator scope after `F1` is repaired. |
| `NF1`, `NF3`, `OR1`, `SR3` standards-compatible externalized configuration | Missing | Evidence reviewed was implementation code in `src/TNC.Trading.Platform.Api/Authentication/PlatformApiAuthenticationServiceCollectionExtensions.cs` and `src/TNC.Trading.Platform.Web/Authentication/*`; no direct automated tests were found for missing or malformed provider configuration, environment switching, or secret rotation assumptions. | This is the clearest automated-coverage gap in the package. | Add unit or integration tests for missing authority, missing audience, provider selection, and startup validation behavior without introducing real secrets. |
| `OR2` documentation-backed local validation | Partial | Work-package docs and plan describe local validation; browser and functional tests exercise the same seeded accounts and routes. | Automated coverage supports the documented workflow, but the review did not re-run the manual checklist or validate wiki links. | Keep documentation review as a release gate and add one link-validation or docs-smoke step in CI if this package becomes a long-lived reference. |

## Existing test strengths

- Tests are organized by level in a way that matches the work-package strategy: unit, functional, integration, and E2E coverage are all present.
- Many tests include strong XML documentation comments with requirement traceability and an explicit statement of what each test verifies and why it matters.
- API integration coverage is especially strong on negative security paths, including invalid issuer, invalid audience, invalid signature, expired token, anonymous access, and insufficient-role access.
- Web unit tests use focused lower-level seams such as `PlatformOperatorContextAccessor`, `PlatformAuthorizationRedirectResolver`, `PlatformNavigationAccessCoordinator`, and `PlatformAccessTokenProvider`, which keeps many auth behaviors deterministic and inexpensive to validate.
- Audit-event tests explicitly assert that obvious secret material is not written into persisted event payloads.
- The package includes both synthetic local test-provider coverage and an attempt at a real AppHost plus Keycloak browser smoke, which is the right overall direction even though the latter currently fails.

## Gaps in testing

### Missing coverage

- `F2` UI role-matrix coverage is incomplete. Existing Web tests prove anonymous redirect behavior, no-role denial, and one administrator success path, but they do not fully prove viewer, operator, and administrator outcomes across the main protected routes and privileged areas.
- `F4` Configuration and provider-selection behavior is effectively untested at the automated level. No direct tests were found for missing authority, missing audience, invalid provider selection, or startup validation outcomes tied to externalized configuration.
- `F5` Several fail-closed scenarios described by the specification are not directly covered: invalid OIDC callback handling, declined elevated-scope acquisition, and session-expiry boundaries beyond simple cookie loss.

### Weak or fragile tests

- `F1` `PlatformDashboardAuthenticationE2ETests.OperatorUi_ShouldRenderPlatformStatus_WhenSeededViewerSignsInFromAspireDashboard` is currently failing because it waits for a fixed launch-settings URL (`https://localhost:7281/...`) rather than proving against the actual AppHost-assigned runtime endpoint. This makes the highest-value local IdP smoke brittle.
- `F3` Observability tests verify the audit API in isolation, but they do not strongly prove that the real Web auth handlers emit the expected audit records and secret-safe logs during sign-in, sign-out, denial, and missing-scope journeys.

### Risks not adequately tested

- Real local Keycloak sign-out and re-authentication remain under-tested because the only real-provider smoke is red.
- Structured application logs are not directly asserted for redaction, so `NF4` and `SR2` still depend partly on code review.
- Externalized configuration resilience is not exercised, so misconfiguration risk called out in the work package remains only partially mitigated by automation.
- Privileged scope-elevation flows are not yet proven end-to-end for both successful and declined elevation paths in the browser.

## Recommendations to strengthen existing tests

1. Address `F1` by changing the dashboard E2E test to discover the actual Web endpoint from the running AppHost instead of using `launchSettings.json`, then keep the repaired test as the primary real Keycloak smoke.
2. Address `F2` by extending the existing Web functional suite with a compact route matrix for `local-viewer`, `local-operator`, `local-admin`, and `local-norole` across `/status`, `/configuration`, and `/administration/authentication` before adding broader E2E coverage.
3. Address `F3` and `F4` by adding lower-level tests for audit emission from the Web handlers and startup-validation behavior for missing or invalid authentication configuration, using deterministic test doubles rather than more infrastructure-heavy tests.

## Recommendations for new tests

| Priority | Area | Test level | Recommendation | Reason |
| --- | --- | --- | --- | --- |
| High | `F1` real local IdP smoke | E2E | Repair and re-enable the AppHost plus Keycloak browser journey so it uses the actual runtime Web endpoint and asserts sign-in reaches `/status`. | This is the strongest proof of `IR1` and is currently the only failing auth suite. |
| High | `F2` Web route role matrix | Functional | Add seeded-user tests for viewer, operator, administrator, and no-role access outcomes across `/status`, `/configuration`, and `/administration/authentication`. | UI role enforcement is the main remaining asymmetry versus the better-covered API layer. |
| High | `F4` configuration validation | Unit / Integration | Add tests for missing authority, missing API audience, invalid provider selection, and provider-specific startup validation behavior. | Misconfiguration is a stated work-package risk but is not currently automated. |
| Medium | `F3` end-to-end audit emission | Functional / Integration | Drive sign-in, sign-out, access-denied, and missing-scope flows through the Web app and then assert the resulting auth events and secret-safe summaries. | Current audit tests bypass the Web handlers and therefore miss instrumentation regressions. |
| Medium | `F5` privileged scope elevation | Functional | Add tests that prove elevation to `platform.operator` and `platform.admin`, plus a denied or unavailable elevation path that fails closed. | The specification calls out incremental delegated scopes, but current browser-level evidence is thin. |
| Medium | `F5` invalid callback and session expiry | Functional / Integration | Add deterministic tests for invalid callback rejection and for session expiry behavior beyond cookie deletion. | These are explicit security and reliability behaviors in the specification. |
| Low | `OR2` documentation alignment | Functional / CI smoke | Add a lightweight docs or link-validation step for the work-package wiki pages referenced by the delivery plan. | This would harden the documentation gate without adding much runtime cost. |

## Hardening recommendations

- Prefer runtime-discovered endpoints over fixed `launchSettings.json` ports in all AppHost-driven tests.
- Keep favoring lower-level tests for auth decisions, claim mapping, and configuration validation before adding more browser-heavy scenarios.
- Where a test proves a requirement from `FRx`, `NFx`, `SRx`, `IRx`, or `TRx`, continue the current practice of documenting that traceability in the test comment.
- Consider introducing shared AppHost fixtures for the slower functional and integration suites if runtime becomes a recurring bottleneck in CI.
- Add explicit assertions for operator-facing denial guidance text and secret-redaction outcomes so those behaviors are not left only to manual review.

## Assumptions and missing information

- The review is based on the current work-package documents plus the auth-related `src/` and `test/` files inspected during this session.
- Manual validation steps and wiki-link checks described in the delivery plan were not re-executed as part of this review.
- Evidence for current test state comes from targeted `dotnet build` and `dotnet test` runs against the relevant auth suites.
- No automated test discovery results were available from the IDE test query for the project names used in this review, so test-source inspection and direct test execution were used as the authoritative evidence instead.

## Suggested next steps

1. Fix `F1` and make the real AppHost plus Keycloak E2E smoke reliably green.
2. Add the `F2` Web route role-matrix functional tests so UI authorization confidence matches API authorization confidence more closely.
3. Add `F4` configuration-validation tests and `F3` end-to-end observability tests, then re-run the full auth suite and update this report if the package state changes.
