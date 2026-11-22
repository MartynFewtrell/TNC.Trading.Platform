---
title: Account Details Repository Patterns
description: Research evidence for implementing account details as a vertical slice in the current workspace
author: GitHub Copilot
ms.date: 2026-08-07
ms.topic: reference
keywords:
  - IG API
  - EF Core
  - Blazor
  - account details
---

## Research Scope

Research only. This report identifies the existing patterns a future
account-details capability should follow across IG integration, persistence,
Application/API contracts, Web UI, and tests.

## Critical Findings

### IG Account and Session Conventions

* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgBrokerAuthenticationGateway.cs:13-110`
  owns IG test-environment authentication for the configured test account. It retrieves protected credentials, creates a
  version-2 `POST session` request, and derives `CST` plus
  `X-SECURITY-TOKEN` from the response headers.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgBrokerAuthenticationGateway.cs:115-163`
  makes the existing account-like read calls during the authenticated session:
  `GET accounts` with IG API version 1 and `GET positions` with version 2.
  Both calls include `X-IG-API-KEY`, `CST`, and `X-SECURITY-TOKEN`.
* The gateway chooses the preferred account, or the first account as a
  deterministic fallback, and reduces its output to account name, account ID,
  balance, and open-position count. The inward contract is
  `src/TNC.Trading.Platform.Application/Features/PlatformAuthentication/Ports/BrokerAuthenticationProof.cs:3-7`.
* Session credentials do not cross the Infrastructure-to-Application boundary.
  `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgBrokerAuthenticationGateway.cs:165-177`
  uses them only to construct provider requests, and
  `src/TNC.Trading.Platform.Application/Features/PlatformAuthentication/Ports/BrokerAuthenticationEvidence.cs:3-6`
  carries only account ID, streaming endpoint, and expiry inward.
* Live broker authentication is deliberately rejected before network traffic at
  `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgBrokerAuthenticationGateway.cs:24-30`.
  The initial account-details feature must use the current IG test-account environment
  unless that policy changes explicitly.

### Persistence, Snapshot, and History Conventions

* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/PlatformDbContext.cs:7-23`
  owns the EF Core `DbSet` inventory. Entity configuration is inline in the
  same context, including a unique `BrokerEnvironment` index for current proof
  data at `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/PlatformDbContext.cs:81-90`.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/Entities/IgProofDataEntity.cs:3-19`
  is the existing current account-projection model. It stores account name,
  account ID, balance, open-position count, and retrieval time, keyed to a
  broker environment.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfPlatformIgProofDataStore.cs:8-68`
  reads with `AsNoTracking` and performs an environment-scoped upsert. The
  inward port remains simple:
  `src/TNC.Trading.Platform.Application/Services/IPlatformIgProofDataStore.cs:5-8`.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfPlatformIgLoginSnapshotStore.cs:11-76`
  is the adjacent history pattern: replace the latest snapshot per environment,
  retain the first successful snapshot per trading day, and query retained
  history with `AsNoTracking` in descending date order.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/Migrations/20260728094818_AddDurableIgProofData.cs:8-48`
  is the migration that introduced `IgProofData`, including SQL types and its
  unique index. The context model snapshot is
  `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/Migrations/PlatformDbContextModelSnapshot.cs`.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Startup/PlatformStartupInitializer.cs:31-66`
  migrates SQL Server at startup and otherwise uses `EnsureCreated` for the
  test-only in-memory provider. Its guarded migration-history baseline checks
  a fixed table list at lines 17-29. Adding a new table therefore requires
  updating that list and its migration-history tests, not only generating a
  migration.

### Application and API Conventions

* Read operations live under feature folders. For example,
  `src/TNC.Trading.Platform.Application/Features/GetIgLoginHistory/` contains
  a request, response, handler, and inward reader port. The handler at
  `src/TNC.Trading.Platform.Application/Features/GetIgLoginHistory/GetIgLoginHistoryHandler.cs:6-19`
  resolves the active broker environment through `PlatformConfigurationService`
  then calls the read-only port.
* `src/TNC.Trading.Platform.Application/Features/GetPlatformStatus/GetPlatformStatusHandler.cs:5-11`
  provides the closest current projection query. It reads the composed status,
  which already contains `LatestProofData`.
* API transport types and mappings are feature-local. The status mapping adds
  `IgProofDataResponse` only when a snapshot exists at
  `src/TNC.Trading.Platform.Api/Features/GetPlatformStatus/GetPlatformStatusMapping.cs:67-88`.
* `src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs:24-49`
  is the single API registration surface. It maps `/api/platform` routes,
  assigns viewer/operator/administrator authorization policies, creates
  Application request objects, calls handlers, and maps results. Application
  handler registrations live in
  `src/TNC.Trading.Platform.Api/Hosting/PlatformApplicationServiceCollectionExtensions.cs:12-38`.
* Infrastructure implements inward ports and registers concrete adapters in
  `src/TNC.Trading.Platform.Infrastructure/Infrastructure/DependencyInjection/PlatformInfrastructureServiceCollectionExtensions.cs:30-94`.

### Web, Authorization, Refresh, and Test Conventions

* `src/TNC.Trading.Platform.Web/PlatformApiClient.cs:13-129` is the Web-to-API
  boundary. Each method constructs an authorized request with the minimal
  required scope; status and IG history use the Viewer scope.
* The existing account detail display is already in the status page:
  `src/TNC.Trading.Platform.Web/Components/Pages/Status/Status.razor:161-184`.
  It shows the cached preferred account, ID, balance, open-position count,
  retrieval time, and its read-only IG test-account source.
* `src/TNC.Trading.Platform.Web/Components/Pages/Status/Status.razor:235-263`
  follows the page load and refresh convention. It authorizes via
  `PlatformNavigationAccessCoordinator`, loads through a presenter, and reloads
  status after the manual-retry command completes. The presenter composes
  status plus events in
  `src/TNC.Trading.Platform.Web/Components/Pages/Status/StatusPagePresenter.cs:6-38`.
* Pages carry `[Authorize]`, then call
  `PlatformNavigationAccessCoordinator.EnsureRequiredScopesAsync` to obtain an
  access token or force a scope-aware sign-in redirect. See
  `src/TNC.Trading.Platform.Web/Authentication/PlatformNavigationAccessCoordinator.cs:12-49`.
  `src/TNC.Trading.Platform.Web/Components/Layout/MainLayout.razor:16-33`
  conditionally renders Viewer navigation, including Status and IG login
  history.
* Radzen is registered application-wide at
  `src/TNC.Trading.Platform.Web/PlatformWebUiServiceCollectionExtensions.cs:8-20`.
  Existing status/history pages primarily use HTML and shared components,
  retaining stable `data-testid` attributes for component tests.
* Relevant test projects and nearest examples are:

  * `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/`
    for the IG adapter and persistence, especially
    `Integrations/Ig/IgBrokerAuthenticationGatewayTests.cs:13-129` and
    `IgProofDataTests.cs:17-119`
  * `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/`
    for a query handler and port fakes, especially
    `Features/GetIgLoginHistory/GetIgLoginHistoryHandlerTests.cs:10-65`
  * `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/`
    for endpoint-handler and API-mapping tests
  * `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/`
    for API-client and bUnit page rendering, especially
    `PlatformApiClientTests.cs:13-44`, `StatusTests.cs:69-97`, and
    `IgLoginHistoryTests.cs:14-50`
  * `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/`
    for real Keycloak role-boundary journeys
  * `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/`
    for AppHost plus Playwright smoke coverage, especially
    `Authentication/PlatformDashboardAuthenticationE2ETests.cs:9-48`

## Recommended Vertical Slice

Use the existing cached proof-data projection first. It already meets the
apparent account-details read need without persisting session tokens or adding a
second independently authenticated IG flow.

For an independently routable page, add a `GetIgAccountDetails` query in these
locations:

* `src/TNC.Trading.Platform.Application/Features/GetIgAccountDetails/`:
  request, response, handler, and a narrow read port owned by the feature
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/`:
  implement that port by reading the existing `IgProofData` row with
  `AsNoTracking`; no new entity or migration is needed when the required fields
  are limited to the current proof snapshot
* `src/TNC.Trading.Platform.Api/Features/GetIgAccountDetails/`:
  response record and Application-to-API mapping, with a Viewer-protected
  `GET /api/platform/ig-account` registration in `PlatformEndpoints`
* `src/TNC.Trading.Platform.Web/`:
  add a `PlatformApiClient` method, account-details view model, Viewer-protected
  Razor page, and Viewer-gated navigation item in `MainLayout`

Keep the current `GET /api/platform/status` proof-data section when a separate
page is not a product requirement. The separate query is preferable only when
the account view needs a stable independent API/UI contract or its own refresh
command.

Do not add direct `GET /accounts` calls from the Web or API layer. A live-refresh
requirement is a separate design change because the existing IG tokens have
operation scope and are not persisted. It should introduce an inward
Application port and an Infrastructure adapter that obtains a fresh authorized
session without exposing credentials or tokens across boundaries.

## Applicable Instructions

* `.github/copilot-instructions.md` requires C# vertical slices, dedicated
  Minimal API endpoint registration, Keycloak locally, SQL-backed operator
  configuration, one top-level C# type per file, and documented test methods.
* `.github/instructions/architecture.instructions.md` requires one operation
  per request/response/handler, inward-owned ports, thin endpoints, and direct
  handler tests.
* `.github/instructions/auth.instructions.md` requires Keycloak in Aspire for
  local auth, OIDC/OAuth alignment, and externalized secrets.
* `.github/instructions/configuration.instructions.md` requires SQL Server for
  operator-managed configuration and write-only handling for IG credentials.
* `.github/instructions/dotnet-stack.instructions.md` requires the configured
  SDK, Minimal APIs, Blazor, SQL Server, and migration-based schema changes.
  `global.json:1-6` pins SDK `10.0.200`.
* `.github/instructions/csharp.instructions.md`,
  `.github/instructions/radzen-blazor.instructions.md`,
  `.github/instructions/tests.instructions.md`,
  `.github/instructions/playwright.instructions.md`, and
  `.github/instructions/aspire-tests.instructions.md` govern source style,
  Radzen, test shape, Playwright, and AppHost testing.
* `.github/instructions/folders.instructions.md` keeps product code under
  `src/` and tests under `test/`.
* The research artifact is governed by
  `c:/Users/martynfewtrell/.vscode/extensions/ise-hve-essentials.hve-core-3.2.2/.github/instructions/hve-core/markdown.instructions.md`:
  YAML frontmatter is required, `title` replaces an H1, headings and lists use
  Markdown lint structure, and code fences need a language. Its writing style
  companion also applies. Workspace source references in this artifact remain
  plain text because `.copilot-tracking/` consumers do not use Markdown links.

## Future Validation

Run focused tests as the feature is built:

```powershell
dotnet test test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/TNC.Trading.Platform.Application.UnitTests.csproj --filter FullyQualifiedName~GetIgAccountDetails
dotnet test test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/TNC.Trading.Platform.Infrastructure.UnitTests.csproj --filter FullyQualifiedName~IgAccountDetails
dotnet test test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/TNC.Trading.Platform.Api.UnitTests.csproj --filter FullyQualifiedName~GetIgAccountDetails
dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj --filter FullyQualifiedName~IgAccountDetails
```

Then validate the affected projects and, when authorization/navigation changes,
the real hosted boundary:

```powershell
dotnet build TNC.Trading.Platform.slnx
dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/TNC.Trading.Platform.Web.FunctionalTests.csproj
dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/TNC.Trading.Platform.Web.E2ETests.csproj
```

For a new EF entity, add an EF Core migration plus matching designer and model
snapshot updates, extend the startup baseline table list, and run the
Infrastructure integration tests against SQL Server.

## Unanswered Gaps

* Product scope has not established whether account details mean the existing
  cached preferred-account proof data or a live, potentially multi-account IG
  response.
* No current reusable authenticated IG session abstraction exists outside the
  authentication gateway call. A live refresh requires an explicit session and
  credential-lifecycle design before implementation.
* The required display fields, polling/refresh policy, freshness threshold, and
  Viewer versus Operator authorization level remain product decisions.
