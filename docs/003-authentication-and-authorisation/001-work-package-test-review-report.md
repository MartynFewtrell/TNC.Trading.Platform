# Work Package Test Review Report

> Use this template to review a work package under `./docs/00x-work/` and assess whether its current automated tests provide sufficient coverage, strength, and confidence. Ground findings in repository evidence and separate confirmed gaps from assumptions.

## Review scope

- **Work package**: `./docs/003-authentication-and-authorisation/`
- **Review depth**: `standard`
- **Reviewer perspective**: `Senior Test Architect`
- **Reviewed artifacts**:
  - `docs/003-authentication-and-authorisation/requirements.md`
  - `docs/003-authentication-and-authorisation/technical-specification.md`
  - `docs/003-authentication-and-authorisation/plans/001-delivery-plan.md`
  - `docs/003-authentication-and-authorisation/plans/002-work-package-test-mitigation-plan.md`
  - `src/TNC.Trading.Platform.Web/Authentication/PlatformAccessTokenProvider.cs`
  - `src/TNC.Trading.Platform.Web/Authentication/PlatformAuthAuditClient.cs`
  - `src/TNC.Trading.Platform.Web/Authentication/PlatformNavigationAccessCoordinator.cs`
  - `src/TNC.Trading.Platform.Web/Components/Authorization/PlatformAuthorizationRedirectResolver.cs`
  - `src/TNC.Trading.Platform.Web/Components/Authorization/RedirectToAuthorizationOutcome.razor`
  - `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/PlatformWebAuthenticationServiceCollectionExtensionsTests.cs`
  - `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/PlatformAuthorizationRedirectResolverTests.cs`
  - `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/PlatformOperatorContextAccessorTests.cs`
  - `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/PlatformAccessTokenProviderTests.cs`
  - `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/PlatformNavigationAccessCoordinatorTests.cs`
  - `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/PlatformAuthAuditClientTests.cs`
  - `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/Authentication/PlatformAuthenticationFunctionalTests.cs`
  - `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/Authentication/PlatformProtectedRouteFunctionalTests.cs`
  - `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/Authentication/PlatformAuthenticationE2ETests.cs`
  - `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/Authentication/PlatformDashboardAuthenticationE2ETests.cs`
  - `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/PlatformAuthenticationIntegrationTests.cs`
  - `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/TestJwtTokenFactory.cs`
  - `docs/wiki/testing-and-quality.md`
  - `docs/wiki/local-development.md`
  - `docs/wiki/runtime-behavior.md`
  - `docs/wiki/operator-guide.md`

## Executive summary

- **Overall test confidence**: `high`
- **Overall coverage assessment**: `strong`
- **Top concerns**:
  1. `F1` Invalid or tampered OIDC callback and sign-in failure behavior around `/signin-oidc` is still not directly covered by automation.
  2. `F2` The Blazor UI role matrix is strong for anonymous challenge, no-role denial, operator configuration access, and administrator access, but it still lacks explicit underprivileged signed-in route assertions for some privileged combinations.
  3. `F3` Auth observability is well covered through audit-event persistence, but secret-safe structured logging remains inferred rather than directly asserted.

The work package has materially improved since the earlier review. The current suite now exercises the intended unit, integration, functional, and E2E layers and aligns well with the completed delivery and mitigation plans. The highest-risk gaps called out previously around sign-out, session loss, protected-route challenge, invalid bearer tokens, no-role API denial, audit-event persistence, and retained real-infrastructure E2E fragility are now covered with concrete automated evidence.

What remains is narrower: a few residual negative-path and hardening scenarios are still not directly proven. These issues do not invalidate the completed work package, but they are the main candidates for follow-up if the team wants to move from strong confidence to near-complete regression confidence.

## Requirement coverage matrix

| Requirement / area | Existing coverage | Evidence | Gap assessment | Recommendation |
| --- | --- | --- | --- | --- |
| `FR1`, `TR3` sign-in, sign-out, post-sign-out denial, session recovery | Covered | `PlatformAuthenticationFunctionalTests.SignOut_ShouldReturnLandingPageContent_WhenSignedInOperatorEndsPlatformSession`; `ConfigurationRoute_ShouldRedirectToSignIn_WhenOperatorRequestsProtectedRouteAfterSignOut`; `ConfigurationPage_ShouldRequireReauthentication_WhenPlatformSessionIsLost`; `PlatformDashboardAuthenticationE2ETests.OperatorUi_ShouldRenderPlatformStatus_WhenSeededViewerSignsInFromAspireDashboard` | Core acceptance behavior is now covered. `F1` remains only for callback-failure handling. | Add one focused negative-path callback test rather than expanding happy-path coverage. |
| `FR2` public landing page and health/readiness | Covered | `PlatformAuthenticationFunctionalTests.LandingPage_ShouldReturnPublicEntryContent_WhenAnonymousUserRequestsRoot`; `PlatformAuthenticationIntegrationTests.HealthEndpoints_ShouldReturnOk_WhenRequestedAnonymously` | No material gap found in the documented anonymous surface set. | Extend only if new anonymous surfaces are introduced. |
| `FR3`, `FR10` no-role sign-in and access denied | Covered | `PlatformOperatorContextAccessorTests.GetCurrentAsync_ShouldReturnAuthenticatedNoRoleContext_WhenPrincipalHasNoPlatformRole`; `PlatformProtectedRouteFunctionalTests.StatusRoute_ShouldRedirectToAccessDenied_WhenNoRoleUserRequestsProtectedRoute`; `PlatformAuthenticationE2ETests.LandingPage_ShouldRouteNoRoleUserToAccessDenied_WhenNoRoleUserSignsIn`; `PlatformAuthenticationIntegrationTests.StatusEndpoint_ShouldReturnForbidden_WhenAuthenticatedCallerHasNoPlatformRole` | No material gap remains for the no-role contract. | Keep the current UI and API no-role matrix stable. |
| `FR4`, `FR8` UI authentication state and minimal operator context | Partial | `PlatformOperatorContextAccessorTests.GetCurrentAsync_ShouldReturnAnonymousContext_WhenUserIsNotAuthenticated`; `GetCurrentAsync_ShouldHonorConfiguredClaimTypes_WhenCustomRoleAndDisplayNameClaimsAreUsed`; `PlatformNavigationAccessCoordinatorTests.EnsureRequiredScopesAsync_ShouldReturnTrue_WhenRequiredScopeIsAlreadyGranted` | `F2` Unit coverage is strong, but there is no direct functional assertion for authenticated landing-page role-shaped navigation. | Add one functional landing-page state test for viewer, operator, and administrator navigation links. |
| `FR5` anonymous Blazor route protection | Covered | `PlatformProtectedRouteFunctionalTests.StatusRoute_ShouldRedirectToSignIn_WhenAnonymousUserRequestsProtectedRoute`; `ConfigurationRoute_ShouldRedirectToSignIn_WhenAnonymousUserRequestsProtectedRoute`; `AuthenticationAdministrationRoute_ShouldRedirectToSignIn_WhenAnonymousUserRequestsProtectedRoute`; `PlatformAuthorizationRedirectResolverTests.CreateDecision_ShouldReturnSignInDestination_WhenUserIsAnonymous` | Anonymous challenge behavior is now well covered. | No immediate gap. |
| `FR6` protected API endpoint authentication | Covered | `PlatformAuthenticationIntegrationTests.StatusEndpoint_ShouldReturnUnauthorized_WhenAnonymousCallerRequestsProtectedSurface`; invalid issuer/audience/signature/expired token tests; `ConfigurationEndpoint_ShouldReturnOk_WhenOperatorUpdatesConfiguration`; `ManualRetryEndpoint_ShouldReturnConflict_WhenOperatorTokenIsProvidedAndManualRetryIsUnavailable`; `EventsEndpoint_ShouldReturnOk_WhenViewerTokenIsProvided` | API fail-closed and protected-endpoint coverage is strong. | Continue using the JWT-helper pattern for future endpoints. |
| `FR7`, `FR9`, `TR2` role boundaries | Partial | `PlatformWebAuthenticationServiceCollectionExtensionsTests.AddPlatformWebAuthentication_ShouldRegisterExpectedRolePolicies_WhenConfiguredForTests`; `PlatformAuthenticationIntegrationTests.ConfigurationEndpoint_ShouldReturnForbidden_WhenViewerTokenLacksOperatorRole`; `AuthAdministrationEndpoint_ShouldReturnForbidden_WhenOperatorTokenLacksAdministratorRole`; `AuthAdministrationEndpoint_ShouldReturnOk_WhenAdministratorTokenIsProvided`; `PlatformAuthenticationE2ETests.AuthenticationAdministrationPage_ShouldRenderForAdministrator_WhenAdminSignsInWithAdminScope` | `F2` The API role matrix is stronger than the UI route matrix. Explicit signed-in UI denial for viewer-to-operator and operator-to-administrator routes is still absent. | Add a small UI role-matrix slice for `local-viewer -> /configuration` and `local-operator -> /administration/authentication`. |
| `NF2`, `SR4` fail-closed behavior | Partial | `PlatformAuthenticationFunctionalTests.ConfigurationPage_ShouldRequireReauthentication_WhenPlatformSessionIsLost`; `PlatformAccessTokenProviderTests.GetAccessTokenAsync_ShouldThrowInvalidOperationException_WhenSessionHasNoAccessToken`; API invalid-token tests in `PlatformAuthenticationIntegrationTests` | Session and bearer-token fail-closed behavior is covered. `F1` remains for invalid callback handling. | Add one low-cost test for invalid or tampered `/signin-oidc` handling. |
| `NF4`, `DR1`, `SR2` observability and audit retention | Partial | `PlatformAuthAuditClientTests.RecordAccessDeniedAsync_ShouldSkipRequest_WhenCurrentSessionHasNoAccessToken`; `RecordSignOutCompletedAsync_ShouldContinue_WhenAuditEndpointReturnsNonSuccessStatusCode`; integration tests for `OperatorSignInCompleted`, `OperatorSignOutCompleted`, `OperatorAccessDenied`, and `OperatorTokenAcquisitionFailed`; `docs/wiki/runtime-behavior.md` | Audit-event persistence is now strong. `F3` remains because structured log content itself is not directly asserted. | Add one logger-backed unit or integration test for denial/failure logging. |
| `IR1`, `OR2` local Keycloak/Aspire path and local guidance | Covered | `PlatformDashboardAuthenticationE2ETests.OperatorUi_ShouldRenderPlatformStatus_WhenSeededViewerSignsInFromAspireDashboard`; `docs/wiki/local-development.md` | The local real-infrastructure path and local validation guidance are aligned. | No immediate gap. |
| `IR2` delegated scope acquisition and recovery | Partial | `PlatformAccessTokenProviderTests.GetAccessTokenAsync_ShouldRecordAuditAndThrowScopeChallenge_WhenRequiredScopeIsMissing`; `PlatformNavigationAccessCoordinatorTests.EnsureRequiredScopesAsync_ShouldRedirectToSignInWithUserHint_WhenRequiredScopeIsMissing`; `PlatformAuthenticationFunctionalTests.SignInPage_ShouldPreserveRequestedScope_WhenPrivilegedAreaRequestsElevation` | `F4` Lower-level recovery behavior is covered, but there is no signed-in end-to-end elevation journey that starts from a lower-privilege session and proves the full redirect-back flow. | Add one optional functional or E2E elevation-journey test only if future regressions justify it. |
| `TR1` anonymous versus authenticated access | Covered | Anonymous challenge, no-role denial, operator success, and administrator success are covered across functional, integration, and E2E suites. | No broad gap remains for the base anonymous-versus-authenticated contract. | Keep future additions route-first and endpoint-first. |

## Existing test strengths

- The suite now has good layered balance: unit tests cover policy registration, redirect resolution, operator-context mapping, access-token evaluation, navigation recovery, and audit helper behavior; API integration tests cover protected-endpoint auth and audit persistence; functional tests cover route-first Web behavior; and E2E tests cover lightweight browser flows plus one real Keycloak/Aspire smoke.
- The highest-risk gaps from the previous review are now concretely addressed: sign-out, post-sign-out denial, session-loss recovery, anonymous route challenge, no-role API denial, invalid issuer/audience/signature/expired bearer tokens, and persisted auth audit events all have direct evidence.
- Test names and XML documentation comments are consistently strong and traceable to `FRx`, `NFx`, `SRx`, `TRx`, `IRx`, and `ORx` requirements.
- The retained real-infrastructure smoke is narrower and less brittle than before because it no longer depends on fixed dashboard port assumptions.
- The API negative-path strategy based on `TestJwtTokenFactory` is readable, maintainable, and appropriate for fail-closed security coverage.

## Gaps in testing

### Missing coverage

- `F1` No automated test was found for invalid, tampered, or failed OIDC callback handling on `/signin-oidc`, even though `SR4` and the technical specification explicitly call out invalid callback rejection.
- `F2` No automated functional or browser test was found that explicitly proves `local-viewer` is denied from `/configuration` or that `local-operator` is denied from `/administration/authentication` in the UI.
- `F2` No automated functional test was found for the authenticated landing-page role-shaped navigation contract after successful sign-in.

### Weak or fragile tests

- `F4` `PlatformDashboardAuthenticationE2ETests` is materially improved, but it now derives the Web sign-in URL from `src/TNC.Trading.Platform.Web/Properties/launchSettings.json` instead of validating the dashboard-exposed resource link itself. This is acceptable for a minimal smoke, but it no longer proves dashboard resource-link wiring end to end.
- `F4` `PlatformAuthenticationE2ETests.AuthenticationAdministrationPage_ShouldRenderForAdministrator_WhenAdminSignsInWithAdminScope` still starts from a composed sign-in URL rather than from an already authenticated lower-scope session requesting elevation.

### Risks not adequately tested

- `F1` Callback-failure handling remains the most security-relevant untested path because it is the direct boundary where invalid external identity-provider responses should fail closed.
- `F3` Secret-safe auth logging remains a residual observability risk because audit persistence is covered, but direct assertions against emitted log content are still absent.
- `F2` UI role-boundary regression risk remains moderate because the Web layer does not yet prove every underprivileged signed-in route combination that the API matrix now covers.

## Recommendations to strengthen existing tests

1. Strengthen the Web functional suite with a small signed-in role-boundary matrix that starts from the protected route and asserts final redirect or denial outcome for `local-viewer -> /configuration` and `local-operator -> /administration/authentication`. This closes `F2` with low additional cost.
2. Strengthen the auth failure coverage with one focused negative-path test around `/signin-oidc` or the relevant callback/error handler so `SR4` invalid-callback rejection is directly proven. This addresses `F1`.
3. Strengthen auth observability confidence with one logger-backed unit or integration test that asserts denial or failure logs contain the event context but do not contain token-like payloads, bearer headers, or signing-key material. This addresses `F3`.

## Recommendations for new tests

| Priority | Area | Test level | Recommendation | Reason |
| --- | --- | --- | --- | --- |
| High | OIDC callback failure handling (`F1`) | Functional / Integration | Add one test for invalid or tampered callback handling on `/signin-oidc` and assert fail-closed redirect or denial behavior. | This is the main remaining security-relevant gap against `SR4`. |
| Medium | Signed-in UI role matrix (`F2`) | Functional | Add explicit denied-route tests for `local-viewer` on `/configuration` and `local-operator` on `/administration/authentication`. | The API role matrix is stronger than the Web role matrix today. |
| Medium | Authenticated landing page state (`F2`) | Functional | Add one role-shaped landing-page assertion test that verifies welcome content, sign-out visibility, and allowed navigation links after sign-in. | This directly proves `FR4`, `FR8`, and the authenticated landing-page contract in the specification. |
| Medium | Secret-safe auth logging (`F3`) | Unit / Integration | Add a sink-backed log assertion for access-denied or token-acquisition-failed paths. | Audit storage is covered; emitted logs are not. |
| Low | Signed-in scope elevation journey (`F4`) | Functional / E2E | Add one optional test that starts authenticated at a lower scope and requests a higher-privilege route to prove the complete re-challenge path. | Current unit coverage is good; only the end-to-end elevation journey remains unproven. |
| Low | Dashboard resource-link wiring (`F4`) | E2E | If dashboard link correctness becomes important, add a separate narrow smoke that validates the dashboard resource link itself without coupling it to the main Keycloak sign-in smoke. | This avoids making the retained smoke brittle again. |

## Hardening recommendations

- Keep preferring lower-level auth tests before adding browser breadth. The remaining gaps are small and can mostly be closed with focused functional or unit coverage.
- Preserve the current XML-comment traceability style. It is one of the strongest qualities of the suite.
- Keep the retained real-infrastructure smoke minimal. If a dashboard-link assertion is reintroduced, separate it from the main Keycloak sign-in smoke so one fragile dashboard UI change does not invalidate the high-value auth-path check.
- If log assertions are added, use a deterministic logger sink and assert both presence of expected event context and absence of token-like values such as `Bearer`, `eyJ`, raw scopes embedded in token payloads, or test signing keys.

## Assumptions and missing information

- This review is limited to repository evidence tied directly to work package `003-authentication-and-authorisation` and its related auth tests.
- No dedicated Microsoft Entra ID automated tests were found. The current assessment therefore remains primarily about the delivered local/test-provider and Keycloak-oriented flows.
- The report assumes the final green validation recorded in `plans/001-delivery-plan.md` and `plans/002-work-package-test-mitigation-plan.md` remains representative of the current branch state.
- Manual validation requirements in `docs/wiki/local-development.md` and `docs/wiki/operator-guide.md` were reviewed for alignment, but this report focuses on automated-test evidence.

## Suggested next steps

1. Treat the work package as test-strong and complete for current delivery, with `F1`, `F2`, and `F3` tracked as residual hardening items rather than blockers.
2. Add one callback-failure test and one small signed-in UI role-matrix slice if the team wants to close the remaining medium-risk gaps.
3. Add one logger-backed observability assertion if secret-safe auth logging needs the same level of proof already achieved for audit-event persistence.
