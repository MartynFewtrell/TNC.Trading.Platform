---
title: Web Layout Inventory Research
description: Verified inventory and folder-boundary research for the Web project and closest Web tests.
ms.date: 2026-08-04
ms.topic: reference
---

## Research Scope

* Repository: `d:/Repos/TNC.Trading/TNC.Trading.Platform`
* Production scope: files directly under `src/TNC.Trading.Platform.Web`
* Test scope: closest tests under `test/TNC.Trading.Platform.Web`
* Research questions: file roles, namespaces, references, routes, registrations, project settings, path-sensitive behavior, and natural folder boundaries

## Status

Complete. Findings below were verified from the repository files on 2026-08-04.

## Project Boundary

* `src/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.csproj:1-21` is an ASP.NET Core Web SDK project targeting `net10.0`, with nullable reference types and implicit usings enabled.
* The Web project references `TNC.Trading.Platform.Application` and `TNC.Trading.Platform.ServiceDefaults` at `src/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.csproj:3-6`.
* The project directly references `Microsoft.AspNetCore.Authentication.OpenIdConnect` and `Radzen.Blazor` at `src/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.csproj:9-12`.
* The solution includes the project at `TNC.Trading.Platform.slnx:8`; the AppHost references it at `src/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.csproj:7`.
* `TNC.Trading.Platform.Web.csproj.lscache:1-66` is generated C# Dev Kit cache data. It lists the same project assembly/root namespace and source tree, contains machine/path-sensitive design-time values, and explicitly says it is not intended for manual editing.

## Direct Root Inventory

All files directly under `src/TNC.Trading.Platform.Web` are listed here. The root is intentionally a contract/startup surface; UI implementation is under `Components`, authentication under `Authentication`, and browser assets under `wwwroot`.

| File | Role and verified evidence |
| --- | --- |
| `appsettings.json:1-45` | Configuration. Defaults the provider to Keycloak, callback/sign-out paths, API audience, viewer scope, Keycloak local authority/client values, Entra instance, test provider values, claim names, logging, and allowed hosts. |
| `Program.cs:1-78` | Host/startup. Adds ServiceDefaults, data protection, Web authentication/UI, Razor interactive server components, API/audit HttpClients, middleware, root sign-in/session-token gate, static assets, auth endpoints, Razor components, and default endpoints. |
| `PlatformWebUiServiceCollectionExtensions.cs:1-19` | Registration. Adds Radzen and scoped page presenters/theme/shell services. |
| `PlatformApiClient.cs:1-127` | Service/API client. Sends bearer-authenticated requests and defines the Web-to-API path/scope map. |
| `AuthAdministrationViewModel.cs:1-6` | Model. Administrator authentication summary contract. |
| `AuthStateViewModel.cs:1-6` | Model. Current authentication session/degraded-state contract. |
| `CredentialPresenceViewModel.cs:1-6` | Model. Non-secret presence flags for stored credentials. |
| `IgLoginHistorySnapshotViewModel.cs:1-12` | Model. Retained non-secret IG login snapshot contract. |
| `IgLoginSnapshotViewModel.cs:1-12` | Model. Latest non-secret IG login snapshot contract. |
| `IgLoginStatusViewModel.cs:1-13` | Model. Current IG login, schedule, retry, snapshot, and proof-data contract. |
| `IgProofDataViewModel.cs:1-8` | Model. Read-only IG proof-data contract. |
| `ManualRetryViewModel.cs:1-3` | Model. Manual retry cycle identifier response. |
| `NotificationSettingsViewModel.cs:1-6` | Model. Notification provider/recipient contract. |
| `PlatformConfigurationViewModel.cs:1-11` | Model. Read configuration aggregate, including restart-required state. |
| `PlatformEventItemViewModel.cs:1-11` | Model. Single platform event contract. |
| `PlatformEventsViewModel.cs:1-5` | Model. Event collection contract. |
| `PlatformStatusViewModel.cs:1-13` | Model. Status aggregate used by home/status/shell. |
| `RetryPolicyViewModel.cs:1-8` | Model. Read retry-policy contract. |
| `RetryStateViewModel.cs:1-8` | Model. Runtime retry-state contract. |
| `TradingScheduleStateViewModel.cs:1-6` | Model. Active/inactive schedule state contract. |
| `TradingScheduleViewModel.cs:1-10` | Model. Read trading-schedule contract. |
| `UpdateCredentialsViewModel.cs:1-9` | Model. Mutable credential replacement request; values are nullable and only represent replacements. |
| `UpdateNotificationSettingsViewModel.cs:1-8` | Model. Mutable notification update request. |
| `UpdatePlatformConfigurationViewModel.cs:1-17` | Model. Mutable aggregate configuration request, including `ChangedBy`. |
| `UpdateRetryPolicyViewModel.cs:1-13` | Model. Mutable retry-policy update request. |
| `UpdateTradingScheduleViewModel.cs:1-17` | Model. Mutable trading-schedule update request. |
| `TradingScheduleStateViewModel.cs:1-6` | Model. Runtime schedule state; this is the sole duplicate-looking root name only because the read and update contracts have different names (`TradingScheduleViewModel` versus `UpdateTradingScheduleViewModel`). |
| `TNC.Trading.Platform.Web.csproj:1-21` | Configuration/build. SDK, target framework, nullable/implicit-usings settings, project references, and package references. |
| `TNC.Trading.Platform.Web.csproj.lscache:1-66` | Generated tooling cache; path-sensitive and not a source boundary. |

## Nested Production Layout

### Authentication

`src/TNC.Trading.Platform.Web/Authentication/` is a cohesive authentication/service boundary. All files use `TNC.Trading.Platform.Web.Authentication` (for example `PlatformAccessTokenProvider.cs:1-6` and `PlatformWebAuthenticationServiceCollectionExtensions.cs:7-10`).

* `PlatformWebAuthenticationServiceCollectionExtensions.cs:11-178` registers cookie authentication, optional OIDC, role policies, scoped auth services, provider validation, PKCE/code flow, token saving, scopes, claim mappings, and sign-in/sign-out/audit events.
* `PlatformAuthenticationEndpointRouteBuilderExtensions.cs:11-116` registers `GET /authentication/sign-in` and protected `POST /authentication/sign-out`; it normalizes local return URLs and supports test-provider or OIDC flows.
* `PlatformAccessTokenProvider.cs:8-75`, `PlatformTokenScopeEvaluator.cs:5-69`, and `PlatformScopeChallengeRequiredException.cs:3-7` validate delegated JWT usability and effective scopes.
* `PlatformNavigationAccessCoordinator.cs:7-59`, `PlatformSignInUrlBuilder.cs:3-36`, `PlatformOperatorContext.cs:5-18`, and `PlatformOperatorContextAccessor.cs:6-52` coordinate role/scope-aware navigation and claim-derived operator context.
* `PlatformAuthAuditClient.cs:8-118` and `PlatformAuthAuditRequest.cs:3-7` send auth audit events to `/api/platform/auth/audit` with a five-second linked timeout.
* `PlatformTestAuthenticationSignInHandler.cs:7-77`, `TestAuthenticationTokenFactory.cs:9-84`, and `PlatformTestAuthenticationSignInHandler.cs:7-77` implement the explicit local test-provider sign-in surface and synthetic users.

### Components, Pages, and Authorization

`Components/_Imports.razor:1-15` establishes shared Razor namespaces, including authorization, Radzen, Application authentication constants, Web authentication, and layout namespaces. `Components/App.razor:1-62` owns the HTML document, root `/` base href, `app.css`, Radzen JS, `platform-theme.js`, Blazor JS, and theme initialization retry. `Components/Routes.razor:1-10` owns the Router, cascading auth state, `AuthorizeRouteView`, `MainLayout`, unauthorized redirect, and focus-on-navigation.

* `Components/Pages/Home.razor:1-139` route `/`, allows anonymous access, renders signed-out and authenticated operator overview states, and delegates loading/redirect decisions to `HomePagePresenter`.
* `Components/Pages/Status.razor:1-270` route `/status`, requires the Viewer policy, loads status/events, renders schedule/auth/IG/retry/proof data, and exposes operator-only manual retry.
* `Components/Pages/Configuration.razor:1-224` route `/configuration`, requires the Operator policy, maps the editable configuration form, protects credentials, and reports restart-required saves.
* `Components/Pages/IgLoginHistory.razor:1-151` route `/ig-login/history`, requires Viewer, loads retained snapshots, and formats raw JSON for expandable non-secret history details.
* `Components/Pages/Administration.razor:1-55` route `/administration/authentication`, requires Administrator, and reads the auth administration summary.
* `Components/Pages/AccessDenied.razor:1-34` route `/authentication/access-denied`, allows anonymous rendering, and records the denied path through `PlatformAuthAuditClient`.
* `Components/Pages/Error.razor:1-6` route `/error`; it is the non-development exception-handler destination configured by `Program.cs:26-29`.
* `Components/Authorization/PlatformAuthorizationRedirectResolver.cs:1-29` computes the return URL and chooses sign-in versus access-denied; `RedirectToAuthorizationOutcome.razor:1-31` performs the redirect and records authenticated access denial.
* `Components/Pages/ConfigurationFormModel.cs:1-45` is the UI edit model with display strings/defaults; `ConfigurationFormModelMapper.cs:1-79` converts read contracts to form state and form state to update requests; `ConfigurationPagePresenter.cs:1-53`, `HomePagePresenter.cs:1-78`, and `StatusPagePresenter.cs:1-113` are page orchestration/presentation services.

### Layout

`Components/Layout/` is the shared shell boundary. `MainLayout.razor:1-103` renders the header, role-filtered sidebar, page frame, environment loading loop, and disposal. `PlatformHeader.razor:1-76` renders sign-in/sign-out, theme, environment, and sidebar controls. `PlatformNavItem.razor:1-20`, `PlatformAccordionSection.razor:1-20`, `PlatformEnvironmentBadge.razor:1-19`, and `PlatformThemeToggle.razor:1-18` are reusable shell controls. `PlatformShellContextProvider.cs:1-45`, `PlatformShellEnvironment.cs:1-9`, `PlatformThemeState.cs:1-71`, and `PlatformThemeMode.cs:1-7` supply cached shell data and browser theme state.

### Assets and Host Configuration

* `wwwroot/app.css:1-100` begins the global platform theme/layout stylesheet. It uses the `--platform-*` variable family and styles the shell/page surfaces; `App.razor:9-10` references it as root-relative `app.css`.
* `wwwroot/platform-theme.js:1-36` defines the global `window.platformTheme` API, persists `tnc-trading-platform.theme` in local storage, and applies `data-platform-theme` to the document root. `App.razor:19-20` references it as root-relative `platform-theme.js`.
* `Properties/launchSettings.json:1-22` defines Development HTTPS `https://localhost:7281` plus HTTP `http://localhost:5281`, and an HTTP-only profile. These are local launch paths, not production endpoint registration.
* `Properties/InternalsVisibleTo.cs:1-3` grants `TNC.Trading.Platform.Web.UnitTests` access to internal Web types, enabling direct unit/bUnit tests without changing production visibility.

## Registrations, Routes, and Path-Sensitive Behavior

* Startup order is explicit in `Program.cs:8-20`; `PlatformApiClient` and `PlatformAuthAuditClient` use Aspire service discovery base address `https+http://api`.
* Middleware order is authentication, authorization, antiforgery, then a custom root gate at `Program.cs:31-69`. A request to `/` without `platformPrompted=1`, without an authenticated identity, or without a usable access token is redirected to `/authentication/sign-in?returnUrl=%2F&prompt=login`; an existing cookie alone is insufficient.
* Static files are mapped by `app.MapStaticAssets()` at `Program.cs:72`; Razor components are mapped at `Program.cs:74-75`, and default health/readiness endpoints come from `MapDefaultEndpoints()` at `Program.cs:76`.
* The root-relative `<base href="/">` at `Components/App.razor:7`, root-relative assets at `Components/App.razor:9-10`, and hard-coded absolute navigation paths in `MainLayout.razor:19-34`, `PlatformHeader.razor:15,29`, and page files mean the current deployment assumes the Web app is hosted at the domain root. A non-root path base would require coordinated changes.
* The sign-out form is a non-enhanced POST to `/authentication/sign-out` with an antiforgery token at `Components/Layout/PlatformHeader.razor:24-32`; the endpoint also requires authorization at `Authentication/PlatformAuthenticationEndpointRouteBuilderExtensions.cs:15-16`.
* Route authorization is centralized through policy names in the page attributes (`Status.razor:1-2`, `Configuration.razor:1-2`, `IgLoginHistory.razor:1-2`, `Administration.razor:1-2`) and policy definitions in `PlatformWebAuthenticationServiceCollectionExtensions.cs:24-43`.
* The default local configuration includes a concrete Keycloak seeded password in `appsettings.json:8-14`; this is a path-sensitive operational concern because the file is copied/configured with the Web host and should be treated as local-development-only configuration.

## Closest Web Test Mirror

The closest test project is `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests`. Its project file targets `net10.0`, references `Microsoft.AspNetCore.App`, uses bUnit/xUnit/test SDK packages, and references the production Web project at `TNC.Trading.Platform.Web.UnitTests.csproj:1-29`.

* Component/page mirrors: `HomeTests.cs:1-75`, `StatusTests.cs:1-220`, `ConfigurationTests.cs:1-136`, `IgLoginHistoryTests.cs:1-112`, and `MainLayoutTests.cs:1-91` render the corresponding pages/layout with bUnit and assert visible behavior/selectors.
* Shared component setup: `PlatformComponentTestContext.cs:20-190` configures bUnit, Radzen, Razor components, test auth state, `HttpContext`, API/audit handlers, Web services, presenters, shell provider, and theme state. `PlatformWebTestData.cs:5-143` supplies status/configuration/events/IG snapshot fixtures.
* API/service mirrors: `PlatformApiClientTests.cs`, `PlatformAuthAuditClientTests.cs`, `PlatformAccessTokenProviderTests.cs`, and `PlatformShellContextProviderTests.cs:13-148` verify request paths, bearer/scopes, audit behavior, token checks, and shell environment caching/retry.
* Authentication mirrors: `PlatformWebAuthenticationServiceCollectionExtensionsTests.cs:14-269` verifies role policies, test-provider scheme selection, Keycloak scope/OIDC setup, sign-out ID-token hints, prompts, and startup validation. `PlatformAuthenticationRegistrationTests.cs:10-105`, `PlatformAuthorizationRedirectResolverTests.cs`, `PlatformNavigationAccessCoordinatorTests.cs`, `PlatformOperatorContextAccessorTests.cs`, `PlatformSignInUrlBuilderTests.cs`, and `PlatformTestAuthenticationSignInHandlerTests.cs` cover the remaining auth decision helpers and test sign-in flow.
* Layout/theme mirrors: `PlatformThemeStateTests.cs:5-100` verifies dark/light parsing, Radzen theme mapping, and JS interop retry. `MainLayoutTests.cs:9-91` verifies anonymous full-width shell, role navigation/environment badge, and sidebar collapse.
* Test infrastructure: `RecordingHttpMessageHandler.cs`, `SequencedHttpMessageHandler.cs`, `TestAuthenticationService.cs`, `TestAuthenticationStateProvider.cs`, `TestNavigationManager.cs`, and `TestWebHostEnvironment.cs` provide deterministic HTTP/auth/navigation/environment seams. `PlatformWebTestData.cs:5-143` is the fixture factory.
* Test namespaces are consistently `TNC.Trading.Platform.Web.UnitTests` (for example `PlatformComponentTestContext.cs:20`), while production namespaces mirror folder boundaries for Authentication, Components.Pages, Components.Layout, and Components.Authorization.

## Natural Folder Boundaries

The verified boundaries are: root for host startup, API client, and shared transport contracts; `Authentication` for auth registration, endpoints, token/scope decisions, operator context, and audit; `Components/Pages` for routable pages plus page presenters/form mapping; `Components/Layout` for shell/theme controls and state; `Components/Authorization` for unauthorized-route decisions; `Components` root for document/router/imports; `wwwroot` for browser-served assets; and `Properties` for launch/test visibility metadata. Moving a root view model into a feature folder would require updating the shared `TNC.Trading.Platform.Web` namespace consumers, while moving Razor files would affect component discovery only through namespaces/imports and route assembly scanning.

## Gaps and Follow-On Research

* No Playwright/browser or distributed Web-host test was found in the closest Web test project; current route, static-asset, middleware, and root sign-in-gate behavior is not exercised end-to-end by these tests.
* `Administration.razor`, `AccessDenied.razor`, `Error.razor`, `PlatformHeader.razor`, and `RedirectToAuthorizationOutcome.razor` have no dedicated direct component test in the inspected Web test inventory, although related auth helper behavior is tested.
* `wwwroot/app.css` was only inspected at its opening section because the requested concern was layout boundary/path behavior; a full CSS selector-to-component audit remains outstanding.
* The generated `.csproj.lscache` contains a truncated source listing in the inspected range; it is sufficient to verify generated/cache status but should not be used as authoritative source inventory.
* Recommended next research: inspect AppHost resource wiring and Aspire published URL/path behavior; inspect any Playwright/AppHost tests outside `test/TNC.Trading.Platform.Web`; and perform a full static route/asset/base-path validation if deployment under a path prefix is a requirement.
