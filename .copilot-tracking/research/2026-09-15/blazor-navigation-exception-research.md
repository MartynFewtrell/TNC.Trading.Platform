<!-- markdownlint-disable-file -->
# Task Research: Blazor NavigationException During Account Details

The reported exception occurs when Account Details detects that the current user is anonymous or lacks the required delegated scope and starts the sign-in redirect.

## Task Implementation Requests

* Determine whether the `NavigationException` is an application defect or expected Blazor Server navigation control flow.
* Explain the exact Account Details to sign-in redirect path.
* Identify whether recent repository changes introduced this behavior.
* Define the smallest validation needed to distinguish a debugger break from a genuine redirect failure.

## Scope and Success Criteria

* Scope: Account Details initialization, scope checking, sign-in URL construction, authentication endpoint mapping, local Aspire authentication wiring, tests, and recent history.
* Exclusions: No product source changes are indicated or made based on the supplied stack trace alone.
* Success Criteria:
  * Explain every relevant stack frame.
  * Identify the expected redirect outcome.
  * State the conditions that would make the exception a real defect.
  * Provide a focused reproducible validation.

## Research Executed

### File Analysis

* `src/TNC.Trading.Platform.Web/Components/Pages/AccountDetails/AccountDetails.razor:82-88`
  * Calls `EnsureRequiredScopesAsync` for `platform.viewer`; protected data loading occurs only when the check returns true.
* `src/TNC.Trading.Platform.Web/Authentication/PlatformNavigationAccessCoordinator.cs:13-59`
  * Anonymous, missing-token, unusable-token, or unavailable-token paths call `NavigateToSignIn`, which invokes `NavigationManager.NavigateTo(destination, forceLoad: true)`.
* `src/TNC.Trading.Platform.Web/Authentication/PlatformAuthenticationEndpointRouteBuilderExtensions.cs:14-65`
  * Maps `/authentication/sign-in`; the endpoint either runs the explicit test sign-in flow or invokes the OIDC challenge.
* `src/TNC.Trading.Platform.Web/Authentication/PlatformWebAuthenticationServiceCollectionExtensions.cs:18-74`
  * Registers the authentication state, coordinator, token provider, cookie authentication, and OIDC for non-test providers.
* `src/TNC.Trading.Platform.AppHost/AppHostEnvironmentWiring.cs:23-32,68-74`
  * Wires local Keycloak, callback path, required scopes, client ID, and role claims.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/PlatformNavigationAccessCoordinatorTests.cs:12-145`
  * Focused coordinator tests pass: 5 succeeded.

### Recent History

* `8863cd9` (2026-08-07) introduced Account Details and scope-gated initialization.
* `155d719` (2026-08-08) changed presentation and tabs without removing the initialization guard.
* `94615ee` (2026-05-09) centralized sign-in URL construction while retaining forced navigation and added URL tests.
* The inspected recent history does not show a regression that turns this intentional redirect into accidental navigation.

## Key Discoveries

The stack is internally consistent:

1. `AccountDetails.OnInitializedAsync` requests the Viewer scope.
2. `EnsureRequiredScopesAsync` determines that the current state cannot satisfy the requirement.
3. `NavigateToSignIn` builds `/authentication/sign-in` with the return URL and requested scopes.
4. `NavigationManager.NavigateTo(..., forceLoad: true)` asks the browser to leave the interactive circuit.
5. Blazor Server's `RemoteNavigationManager` throws `Microsoft.AspNetCore.Components.NavigationException` as part of stopping the current component execution during that navigation.

Therefore, the exception is expected control flow when the debugger is configured to break on thrown or first-chance CLR exceptions. Continuing should take the browser to `/authentication/sign-in`. The exception becomes a real product problem only if the browser remains on `/account-details`, the sign-in endpoint returns an error or 404, the OIDC challenge fails, or another exception occurs after continuing.

## Technical Scenario

### Expected Scope Challenge

**Preferred Approach:** Leave the coordinator flow unchanged. Verify the browser navigation and the sign-in endpoint response rather than treating the debugger break itself as a failure.

**Validation:**

1. Start AppHost and open the HTTPS Web URL.
2. Use an anonymous session or a token without `platform.viewer`.
3. Navigate to `/account-details`.
4. When the debugger breaks at `RemoteNavigationManager.NavigateToCore`, continue execution.
5. Confirm navigation to `/authentication/sign-in` and successful test/OIDC sign-in.
6. Repeat with a valid token containing the required scope and confirm that no redirect occurs and `Presenter.LoadAsync` runs.

### Genuine Redirect Failure

A defect is indicated if continuing execution does not leave `/account-details`, if `/authentication/sign-in` returns 404/500, or if Web logs show an OIDC/provider configuration failure. Capture the browser network response, Web logs, configured provider, callback URL, and final URL. The likely owning boundaries would then be sign-in endpoint routing, HTTPS/certificate configuration, Keycloak readiness, or OIDC authority/client configuration, not the `NavigationException` frame itself.

## Considered Alternatives

* Remove the `NavigateTo` call or catch and suppress `NavigationException` in Account Details.
  * Rejected: this would risk continuing protected-page initialization after the authentication requirement failed and would hide the intended Blazor navigation mechanism.
* Treat the debugger break as proof of an application exception.
  * Rejected: the focused coordinator tests pass and the stack represents a deliberate forced navigation.
* Change the scope guard before observing browser behavior.
  * Rejected: the guard is the security boundary that prevents protected data loading without the required scope.

## Selected Approach

No product code change is currently justified. Treat this as an expected debugger-visible navigation sentinel, then validate the redirect result. If the redirect fails after continuing, diagnose that concrete HTTP/OIDC failure separately.

## Research Summary

* Expected navigation path verified: yes.
* Focused tests: 5 passed.
* Recent regression found in this path: no.
* Product code change recommended from the supplied stack trace: no.
* Required follow-up if user-visible failure remains: capture `/authentication/sign-in` response and Web/OIDC logs.
