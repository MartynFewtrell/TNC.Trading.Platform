---
title: NavigationException Investigation
description: Research findings for the Blazor Server sign-in redirect reported from Account Details.
ms.date: 2026-09-15
ms.topic: troubleshooting
---

# NavigationException Investigation

## Questions
- Is `Microsoft.AspNetCore.Components.NavigationException` from `RemoteNavigationManager.NavigateToCore` expected during Blazor Server redirects in this flow?
- How do `PlatformNavigationAccessCoordinator.NavigateToSignIn`, `EnsureRequiredScopesAsync`, `AccountDetails.razor.OnInitializedAsync`, and authentication state/redirect handling interact?
- What recent git changes affected this path, and why might the debugger break after using the app?

## Findings

### Control flow

- `AccountDetails.razor` calls `EnsureRequiredScopesAsync` for `platform.viewer` at line 84 and only evaluates the Operator check when the Viewer check succeeds at line 85. `LoadAsync` is reached only when `hasRequiredScope` is true at line 87.
- `PlatformNavigationAccessCoordinator.EnsureRequiredScopesAsync` checks the cascading authentication state through `PlatformOperatorContextAccessor` at lines 20-24. Anonymous state redirects with `forcePrompt: true`; authenticated state then obtains the delegated token at line 29.
- Missing, unusable, or unavailable tokens are converted into a sign-in redirect by the two exception handlers at lines 31-41. `NavigateToSignIn` builds the sign-in URL and calls `NavigationManager.NavigateTo(destination, forceLoad: true)` at line 59.
- The redirect target is a real HTTP endpoint. `MapPlatformAuthenticationEndpoints` registers `/authentication/sign-in` at `PlatformAuthenticationEndpointRouteBuilderExtensions.cs` line 16, and `SignInAsync` either invokes the explicit test harness or returns an OIDC challenge at lines 38-65.
- Authentication services register cascading authentication state and the scoped coordinator/token provider at `PlatformWebAuthenticationServiceCollectionExtensions.cs` lines 18-21. Cookie authentication is the default at lines 53-58; OIDC is added for non-Test providers at lines 72-74.
- Aspire local Web configuration selects Keycloak and supplies the OIDC callback path, required Viewer scope, Web client ID, and role claim mapping in `AppHostEnvironmentWiring.cs` lines 23-32 and 68-74. The Web launch profile exposes HTTPS on `https://localhost:7281` in `src/TNC.Trading.Platform.Web/Properties/launchSettings.json` lines 4-12.

### Interpretation of the exception

`NavigationException` from `RemoteNavigationManager.NavigateToCore` is expected control flow for a forced navigation initiated while an interactive Blazor component is initializing. The coordinator intentionally starts a full browser navigation to an HTTP sign-in endpoint and then returns `false`; the exception interrupts the current component render/circuit so execution does not continue into protected page work. It is not evidence that Account Details data loading failed.

The debugger can still break on it because it is a thrown exception visible to the debugger, commonly as a first-chance or user-unhandled exception, before Blazor's navigation machinery completes the redirect. When execution continues, the browser should follow the forced navigation. A debugger break alone therefore does not identify a product defect. It becomes suspicious only if the browser fails to leave the protected route, the sign-in endpoint returns an error, or an exception escapes after the redirect attempt.

### Recent history

- `8863cd9` (2026-08-07) introduced the Account Details feature and its scope-gated initialization.
- `155d719` (2026-08-08) enhanced the Account Details presentation and tabs; the relevant initialization guard remains the same.
- `94615ee` (2026-05-09) replaced string concatenation in `NavigateToSignIn` with `PlatformSignInUrlBuilder`, preserving `forceLoad: true` and adding tests for the resulting URL.
- The recent history contains no change that removes the `hasRequiredScope` guard or changes forced navigation into an accidental render-time navigation.

### Likely root cause and required changes

The likely root cause is debugger exception settings surfacing Blazor's expected navigation sentinel during a scope challenge, often after the current session has no access token or lacks the requested scope. No product code change is indicated by the inspected path. The focused tests already encode the intended redirect contract.

One residual operational dependency is that the redirected `/authentication/sign-in` endpoint must be reachable and correctly configured. A failed redirect, a 404 from the disabled Test harness, or an OIDC configuration failure would be a separate defect and should be diagnosed from the HTTP response and Web logs rather than from the `NavigationException` stack frame.

## Evidence

- `src/TNC.Trading.Platform.Web/Authentication/PlatformNavigationAccessCoordinator.cs:13-59`
- `src/TNC.Trading.Platform.Web/Components/Pages/AccountDetails/AccountDetails.razor:82-88`
- `src/TNC.Trading.Platform.Web/Authentication/PlatformOperatorContextAccessor.cs:12-47`
- `src/TNC.Trading.Platform.Web/Authentication/PlatformAccessTokenProvider.cs:14-58`
- `src/TNC.Trading.Platform.Web/Authentication/PlatformAuthenticationEndpointRouteBuilderExtensions.cs:14-65`
- `src/TNC.Trading.Platform.Web/Authentication/PlatformWebAuthenticationServiceCollectionExtensions.cs:18-74`
- `src/TNC.Trading.Platform.Web/Program.cs:21-74`
- `src/TNC.Trading.Platform.AppHost/AppHostEnvironmentWiring.cs:23-32,68-74`
- `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/PlatformNavigationAccessCoordinatorTests.cs:12-145`
- `src/TNC.Trading.Platform.Web/Properties/launchSettings.json:4-18`

The focused command `dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj --filter FullyQualifiedName~PlatformNavigationAccessCoordinatorTests` passed: 5 tests succeeded. Restore/build emitted the existing `AngleSharp` NU1902 warning and unrelated nullable warnings.

## Validation and Reproduction

1. Start the Aspire AppHost with Docker-backed Keycloak available, then open the HTTPS Web URL from the Aspire dashboard.
2. Use an anonymous session, or sign in with a session whose access token is absent or lacks `platform.viewer`/`platform.operator`.
3. Navigate to `/account-details` and enable breaking on thrown CLR exceptions in the debugger. The break should occur at `RemoteNavigationManager.NavigateToCore` while `PlatformNavigationAccessCoordinator.NavigateToSignIn` is executing.
4. Continue execution. Verify that the browser navigates to `/authentication/sign-in` with `returnUrl`, `scope`, and, for the enabled Test harness, the expected `user` hint; then verify the configured test or OIDC sign-in flow completes.
5. Repeat with a valid authenticated token containing the required scope. `EnsureRequiredScopesAsync` should return true, no sign-in navigation should occur, and `AccountDetails.razor` should proceed to `Presenter.LoadAsync`.
6. If continuing does not navigate, capture the network response for `/authentication/sign-in`, Web logs, and the configured provider values. That result would disconfirm the expected-navigation interpretation and indicate a separate redirect/auth configuration problem.

## Open Questions

- The report does not include whether the browser completed the redirect or which debugger exception category was enabled. That observation is needed to distinguish a normal first-chance break from a redirect that genuinely fails.
