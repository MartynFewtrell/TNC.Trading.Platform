# Failing Web Tests Repair Surfaces Research

## Status

Complete. This is a plan-only investigation; no product or test code was changed.

## Research Questions

- Verify the attached conclusions for the 10 currently failing Web tests.
- Inspect the concrete repair surfaces: the controllable IG provider, its nearest regression-test home and project, E2E and functional authentication fixtures, AppHost configuration, test projects, and CI commands.
- Record current line references, minimally scoped files and changes, test names and commands, planning discrepancies, remaining gaps, and clarifying questions.

## Findings

### Verified Failure Scope

The attached current-testing research identifies 10 current Web failures: eight in
`TNC.Trading.Platform.Web.FunctionalTests` and two in
`TNC.Trading.Platform.Web.E2ETests`. The captured failures occur in shared fixture
startup or at the Keycloak sign-in page before a feature-specific product assertion.
The present source supports two separate, test-infrastructure repair slices:

| Failure group | Tests | Verified controlling surface | Planned repair |
| --- | ---: | --- | --- |
| Functional fixture startup | 8 | `test/Shared/AccountPreferences/ControllableIgProvider.cs:36-56` | Create and configure a fresh `HttpListener` for every bind attempt, disposing each failed attempt. Add a focused retry regression test. |
| Browser sign-in page | 2 | `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/Authentication/RealAuthenticationE2ETestFixture.cs:18-24` | Add the existing functional fixture's fixture-scoped `AppHost:UsePersistentKeycloakState=false` setting. |

The eight functional tests are:

* `PlatformAuthenticationFunctionalTests.SignOutEndpoint_ShouldReturnBadRequest_WhenAntiforgeryTokenIsMissing`
* `PlatformAuthenticationFunctionalTests.SignInEndpoint_ShouldRedirectToLandingPage_WhenExternalReturnUrlIsSupplied`
* `PlatformAuthenticationFunctionalTests.SignOutEndpoint_ShouldReturnNotFound_WhenBrowserUsesLegacyGetRequest`
* `PlatformAuthenticationFunctionalTests.RootRoute_ShouldRenderSignInPage_WhenAnonymousUserRequestsApplicationEntry`
* `PlatformAuthenticationFunctionalTests.ConfigurationRoute_ShouldRedirectToSignIn_WhenOperatorRequestsProtectedRouteAfterSignOut`
* `PlatformProtectedRouteFunctionalTests.ConfigurationRoute_ShouldRedirectToAccessDenied_WhenViewerRequestsOperatorRoute`
* `AccountPreferencesFunctionalTests.AccountPreferences_ShouldPersistAndConverge_WhenProviderRecovers`
* `AccountPreferencesFunctionalTests.AccountPreferences_ShouldReturnUnconfiguredWithoutProviderRead_WhenFreshInstallProviderIsUnavailable`

The two browser E2E tests are:

* `PlatformDashboardAuthenticationE2ETests.OperatorUi_ShouldRenderOperatorHome_WhenSeededViewerSignsInFromAspireDashboard`
* `AccountPreferencesE2ETests.AccountPreferences_ShouldShowDesiredAndObservedLabels_WhenProviderIsUnavailable`

### Controllable Provider Repair and Regression Test Home

`ControllableIgProvider.Start()` creates one `HttpListener` before the retry loop
at `test/Shared/AccountPreferences/ControllableIgProvider.cs:38`. It adds one prefix,
starts that same instance, and, after `HttpListenerException`, clears that same
instance's prefixes before retrying at lines 39-49. Reusing a listener after its
failed bind is the localized failure path. The provider otherwise owns its listener
and request task from construction through `DisposeAsync` at lines 18-23 and 53-59.

Minimal production-test-support change:

1. Move `new HttpListener()` and prefix configuration inside the loop at lines 37-50.
2. On `HttpListenerException`, close or dispose that attempt before the next one.
3. Preserve the 20-attempt bound, the random high-port range, the returned `BaseUri`,
	 routes, state, and asynchronous disposal behavior.
4. Introduce a deliberately narrow test seam for candidate-port selection so one test
	 can force an occupied first port followed by a usable candidate. The existing
	 parameterless `Start()` cannot support that deterministic setup because it calls
	 `Random.Shared.Next` directly at line 40.

The nearest appropriate regression home is a new
`test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/AccountPreferences/ControllableIgProviderTests.cs`.
That project already compiles the shared provider by link at
`test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/TNC.Trading.Platform.Web.FunctionalTests.csproj:27-30`,
already references xUnit at lines 10-14, and owns both affected account-preferences
functional cases at `AccountPreferencesFunctionalTests.cs:5-61`. No project-file
change is required for that home.

Recommended regression test name:

`Start_ShouldRetryWithFreshListener_WhenFirstCandidatePortIsOccupied`

The test should hold the first supplied loopback port, use the new test-only candidate
seam to make the next candidate usable, start the provider, and issue an existing
route request to prove the successful replacement listener serves responses. It should
dispose both the provider and the occupying listener. This remains a focused
in-process test of test support; it must not start the AppHost or rely on a sleep.

### AppHost and Authentication Configuration

The Web E2E fixture creates the provider and passes only the IG base URL plus disabled
interactive test sign-in at
`test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/Authentication/RealAuthenticationE2ETestFixture.cs:18-22`.
The equivalent functional fixture has the same settings and explicitly supplies
`AppHost:UsePersistentKeycloakState = bool.FalseString` at
`test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/Authentication/RealAuthenticationFunctionalTestFixture.cs:20-26`.

The AppHost defaults this setting to persistent: it treats only case-insensitive
`false` as non-persistent at
`src/TNC.Trading.Platform.AppHost/AppHostInfrastructureRegistration.cs:8-16`, then
assigns Keycloak persistent or session lifetime at lines 41-47. `ManagedAppHostFixture`
passes its configuration dictionary to the distributed-application builder as command
line settings at `test/Shared/Authentication/ManagedAppHostFixture.cs:25-36`, awaits
named Keycloak, API, and Web health at lines 39-44, and obtains their named endpoints
at lines 45-47. Therefore the minimally scoped E2E change is one additional dictionary
entry next to the two existing E2E options; it requires no AppHost behavior change.

The IG setting is separately propagated as intended: `AppHostSettings` reads it at
`src/TNC.Trading.Platform.AppHost/AppHostSettings.cs:7-24`, and API wiring passes it
as `Ig__AccountPreferencesBaseUrl` only when configured at
`src/TNC.Trading.Platform.AppHost/AppHostEnvironmentWiring.cs:98-141`. This supports
retaining the fixtures' dynamic provider URL unchanged.

Both E2E tests wait for Keycloak `#username` before credentials are entered:
`PlatformDashboardAuthenticationE2ETests.cs:26-29` and
`AccountPreferencesE2ETests.cs:23-26`. This is consistent with the attached
classification that the two browser failures are sign-in-page state-isolation failures,
not dashboard or account-preferences assertion failures.

### Test Projects and CI Commands

Both higher-level Web test projects target `net10.0`, use `Aspire.Hosting.Testing`
13.5.3, and link the same shared provider:

* Functional: `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/TNC.Trading.Platform.Web.FunctionalTests.csproj:3-30`
* E2E: `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/TNC.Trading.Platform.Web.E2ETests.csproj:3-29`

Each suite owns a disabled-parallelization xUnit collection for its real fixture:
`AuthenticationFunctionalTestCollection.cs:5-7` and
`AuthenticationE2ETestCollection.cs:5-7`. This only serializes tests within the
collection and is not a replacement for fixing the listener lifecycle.

Focused validation, in repair order:

```powershell
dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/TNC.Trading.Platform.Web.FunctionalTests.csproj --filter "FullyQualifiedName~ControllableIgProviderTests.Start_ShouldRetryWithFreshListener_WhenFirstCandidatePortIsOccupied"
dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/TNC.Trading.Platform.Web.FunctionalTests.csproj --configuration Release
dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/TNC.Trading.Platform.Web.E2ETests.csproj --configuration Release --filter "FullyQualifiedName~PlatformDashboardAuthenticationE2ETests.OperatorUi_ShouldRenderOperatorHome_WhenSeededViewerSignsInFromAspireDashboard"
dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/TNC.Trading.Platform.Web.E2ETests.csproj --configuration Release --filter "FullyQualifiedName~AccountPreferencesE2ETests.AccountPreferences_ShouldShowDesiredAndObservedLabels_WhenProviderIsUnavailable"
dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/TNC.Trading.Platform.Web.E2ETests.csproj --configuration Release
```

CI restores and builds the solution in Release at `.github/workflows/ci.yml:23-29`.
It runs the functional project in the Docker-backed integration job at lines 70-93;
runs the E2E project after building it and installing Chromium at lines 117-131; and
runs the solution summary at lines 151-161. The repair should be validated using the
same Release configuration and without `--no-restore` locally unless restore is known
to have completed.

## Planning Discrepancies

* The attached research accurately identifies the two current repair surfaces, but its
	root-cause wording overstates what source inspection alone proves: the source proves
	reuse of a failed `HttpListener`; whether the Windows runtime has already disposed it
	after a bind failure must be demonstrated by the proposed regression test or current
	captured exception. The repair remains the same.
* The recommended provider regression test is not currently implementable as described
	because `Start()` has no candidate-port injection point. Include a narrow internal or
	test-only candidate selection seam in the same provider change. Do not make the test
	probabilistic by hoping a `Random.Shared` candidate collides.
* No test project currently provides a dedicated shared-test-support assembly. Adding a
	new project merely for this one provider retry test would broaden scope; the existing
	functional project is the smallest compatible home and needs no `.csproj` edit.
* Do not modify `AppHostInfrastructureRegistration` to make all AppHost instances
	non-persistent. Persistent Keycloak remains the default at lines 8-16; the missing
	setting belongs only to the E2E fixture, as proven by the adjacent functional fixture.
* The attached test name says `FromAspireDashboard`, but the current E2E test navigates
	to `fixture.WebBaseUri` at `PlatformDashboardAuthenticationE2ETests.cs:25`; it does
	not interact with the dashboard. This is naming/documentation debt, not a necessary
	repair change.

## References

* `.copilot-tracking/research/2026-09-15/current-testing-strategy-and-failing-tests-research.md:62-176`
* `test/Shared/AccountPreferences/ControllableIgProvider.cs:6-111`
* `test/Shared/Authentication/ManagedAppHostFixture.cs:9-82`
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/Authentication/RealAuthenticationFunctionalTestFixture.cs:6-51`
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/Authentication/RealAuthenticationE2ETestFixture.cs:6-48`
* `src/TNC.Trading.Platform.AppHost/AppHostInfrastructureRegistration.cs:3-55`
* `.github/workflows/ci.yml:23-168`

## Remaining Gaps

* This plan-only pass did not reproduce the listener exception or execute the E2E suite.
	The attached research records those observed results, while this document verifies the
	current implementation paths and scopes the fix.
* The exact API behavior to use for an injectable candidate sequence is an implementation
	decision. Keep it internal to the direct-compilation test assembly or otherwise
	non-public; preserve the current public parameterless `Start()` contract.
* A second full E2E-project run after the focused cases is required to prove the
	non-persistent Keycloak configuration does not leak state across a complete fixture
	lifecycle.

## Clarifying Questions

No user input is required to implement the minimal repair plan. The implementer may
choose the narrowest internal candidate-port seam that keeps `ControllableIgProvider`
test-only support and preserves `Start()` for its existing callers.

## References

Pending targeted source inspection.

## Remaining Gaps

Pending targeted source inspection.

## Clarifying Questions

Pending targeted source inspection.