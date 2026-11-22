---
description: "Current-codebase research for planning account-preferences desired-state persistence, account-bound verification, reconciliation, remediation, UI, tests, and documentation."
ms.date: 2026-08-31
---

# Account Preferences Implementation Planning Research

## Research Status

Complete. All requested architecture surfaces were traced. Product and API decisions that code research cannot resolve are listed under Unresolved Decisions.

## Research Questions

* What exact files and types currently own account-preference reads, updates, observations, API contracts, UI presentation, IG access, dependency injection, and background processing?
* What is the cheapest first diagnostic fix for the generic account-preferences load failure, and which focused check can prove it?
* Which existing persistence, revision, optimistic-concurrency, lease, retry, and due-work patterns should the implementation follow?
* How can preference verification remain bound to the selected IG account and one authenticated session generation?
* Where should observe-only reconciliation and explicit revision-bound remediation live?
* Which API compatibility choices and parallel implementation boundaries require explicit planning?
* Which migrations, tests, wiki pages, and executable validation commands will be affected?

## Instruction Constraints

* Application handlers must own use-case policy behind inward-owned ports; endpoints, EF entities, provider payloads, and UI models must remain adapters (`.github/instructions/architecture.instructions.md:25-75`).
* Every operation uses one request, response, and handler, with a validator for malformed external input (`.github/instructions/architecture.instructions.md:76-105`).
* Operator-managed settings belong in SQL Server and must flow through explicit services or repositories and the Blazor UI (`.github/instructions/configuration.instructions.md:24-48`).
* C# changes require one top-level type per matching file, intentional Problem Details at HTTP boundaries, and focused tests (`.github/instructions/csharp.instructions.md:20-57`).
* Radzen is the preferred shared component library, but plain semantic HTML remains appropriate for simple display; interactive state must remain accessible and tested (`.github/instructions/radzen-blazor.instructions.md:27-75`).
* Tests are unit-first, use xUnit, follow the repository naming convention, and require traceability comments. Aspire tests remain closed-box and Playwright tests use accessible locators and web-first assertions (`.github/instructions/tests.instructions.md:25-89`, `.github/instructions/aspire-tests.instructions.md:21-55`, `.github/instructions/playwright.instructions.md:20-55`).
* Implemented behavior changes require corresponding `docs/wiki/` updates before a numbered plan completes. A new work package must not be drafted unless explicitly requested (`.github/instructions/docs.instructions.md:20-47`, `.github/instructions/work-packages.instructions.md:25-75`).

## Findings

### Cheapest First Diagnostic Fix

The cheapest code fix is independent presenter error handling, followed by the omitted telemetry dependency fix:

1. Split the initial current-state request from the history request in `AccountPreferencesPagePresenter.LoadAsync`. The current single `try` wraps both calls and maps either failure to `Unable to load account preferences.` (`src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPagePresenter.cs:19-36`). The save path already demonstrates the intended independent-history behavior (`src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPagePresenter.cs:38-66`).
2. Preserve the current-state display when history fails and expose a separate history error. The cheapest discriminating test is a presenter or bUnit test where `GET /api/platform/account-preferences` returns `200` and `GET /api/platform/account-preferences/observations` fails; the state must remain visible and only history must report failure.
3. Pass the injected `eventStore` to both provider-failure calls in `GetAccountPreferencesHandler`. The handler injects the store but calls the optional static parameter without it, causing `RecordFailureAsync` to return immediately (`src/TNC.Trading.Platform.Application/Features/AccountPreferences/GetAccountPreferencesHandler.cs:6-11`, `src/TNC.Trading.Platform.Application/Features/AccountPreferences/GetAccountPreferencesHandler.cs:25-31`, `src/TNC.Trading.Platform.Application/Features/AccountPreferences/GetAccountPreferencesHandler.cs:43-48`). A handler unit test should invoke the ordinary failed outcome path and assert one sanitized event, rather than calling the static helper directly.

The runtime diagnostic remains the browser/API request pair in order: `GET /api/platform/account-preferences`, then `GET /api/platform/account-preferences/observations?pageSize=25`. The endpoint mapping distinguishes `429`, `502`, `503`, and `504`, but `PlatformApiClient` currently discards Problem Details through `EnsureSuccessStatusCode` (`src/TNC.Trading.Platform.Api/Features/Platform/AccountPreferencesEndpointMapping.cs:17-50`, `src/TNC.Trading.Platform.Web/PlatformApiClient.cs:139-169`).

### Current Feature Ownership

* API route ownership is `PlatformEndpoints.MapPlatformEndpoints`: Operator-only `GET`, `PUT`, and observation-history `GET` routes under `/api/platform/account-preferences` (`src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs:31-61`, `src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs:130-147`).
* Thin endpoint adapters are `GetAccountPreferencesEndpointHandler`, `UpdateAccountPreferencesEndpointHandler`, and `GetTrailingStopsPreferenceObservationsEndpointHandler`. The update adapter validates nullable input before dispatch (`src/TNC.Trading.Platform.Api/Features/Platform/GetAccountPreferencesEndpointHandler.cs:6-12`, `src/TNC.Trading.Platform.Api/Features/Platform/UpdateAccountPreferencesEndpointHandler.cs:6-18`).
* Public mapping and HTTP records currently live together in `AccountPreferencesEndpointMapping.cs`. The current success shape is exactly `TrailingStopsEnabled`, `ApplicationStatus`, and `ObservedAtUtc`; updates return the same `200 OK` shape, while a provider mismatch maps to `409` (`src/TNC.Trading.Platform.Api/Features/Platform/AccountPreferencesEndpointMapping.cs:8-32`, `src/TNC.Trading.Platform.Api/Features/Platform/AccountPreferencesEndpointMapping.cs:52-55`).
* Application ownership is split between `GetAccountPreferencesHandler`, `UpdateAccountPreferencesHandler`, and `GetTrailingStopsPreferenceObservationsHandler` under `Features/AccountPreferences`. Current-state reads call IG and append history; updates call IG and then perform a second gateway read (`src/TNC.Trading.Platform.Application/Features/AccountPreferences/GetAccountPreferencesHandler.cs:13-35`, `src/TNC.Trading.Platform.Application/Features/AccountPreferences/UpdateAccountPreferencesHandler.cs:13-58`).
* The inward provider port is `IAccountPreferencesGateway`, which exposes only independent `GetAsync` and `UpdateAsync` calls and carries no target account or session context (`src/TNC.Trading.Platform.Application/Features/AccountPreferences/IAccountPreferencesGateway.cs:3-7`).
* Infrastructure ownership is `IgAccountPreferencesGateway`, `EfTrailingStopsPreferenceObservationStore`, `PlatformDbContext`, and `PlatformInfrastructureServiceCollectionExtensions` (`src/TNC.Trading.Platform.Infrastructure/Infrastructure/DependencyInjection/PlatformInfrastructureServiceCollectionExtensions.cs:90-108`).
* Web ownership is `PlatformApiClient`, `AccountPreferencesPagePresenter`, `AccountPreferencesPageViewModel`, `AccountPreferences.razor`, and its scoped CSS. The Web response model mirrors the three-field API response exactly (`src/TNC.Trading.Platform.Web/PlatformApiClient.cs:139-169`, `src/TNC.Trading.Platform.Web/PlatformApiClient.cs:188-190`).

### Persistence, Revision, and Lease Patterns

There is no durable desired/current account-preference state, desired revision, reconciliation lease, or due-work record today. The only account-preference persistence port appends and pages observations by platform and broker environment (`src/TNC.Trading.Platform.Application/Features/AccountPreferences/ITrailingStopsPreferenceObservationStore.cs:5-11`).

The current observation persistence is partially prepared for account binding but drops it in translation:

* `TrailingStopsPreferenceObservationEntity.AccountId` exists and is nullable (`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/Entities/TrailingStopsPreferenceObservationEntity.cs:3-25`).
* `TrailingStopsPreferenceObservation` has no account field (`src/TNC.Trading.Platform.Application/Features/AccountPreferences/TrailingStopsPreferenceObservation.cs:5-16`).
* `EfTrailingStopsPreferenceObservationStore` omits `AccountId` on write and read (`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfTrailingStopsPreferenceObservationStore.cs:10-26`, `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfTrailingStopsPreferenceObservationStore.cs:50-57`).
* `PlatformDbContext` configures the field but indexes only environment plus observation ordering (`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/PlatformDbContext.cs:150-161`).

The nearest replica-exclusion precedent is `IAccountDetailsRefreshLease` with `SqlAccountDetailsRefreshLease`. It takes an exclusive, zero-wait SQL Server `sp_getapplock`, retains the open connection for session lock ownership, and releases it through `IAsyncDisposable` (`src/TNC.Trading.Platform.Application/Features/AccountDetails/IAccountDetailsRefreshLease.cs:5-12`, `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/SqlAccountDetailsRefreshLease.cs:9-59`). A preference lease should scope its resource key at least by broker environment and target account. Revision safety is a separate concern: the current-state update must use a desired revision or concurrency token in its `WHERE` predicate so a late verifier cannot overwrite newer intent after acquiring or while holding a lease.

### IG Account and Session Binding

`IgAccountPreferencesGateway` always loads Demo credentials, creates a new Version 2 session per gateway method, and sends the preference request with that session's `CST` and `X-SECURITY-TOKEN` (`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgAccountPreferencesGateway.cs:19-35`, `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgAccountPreferencesGateway.cs:52-76`). It does not deserialize or return `currentAccountId`. Consequently, `UpdateAccountPreferencesHandler` performs `PUT` and confirming `GET` through two independently created sessions (`src/TNC.Trading.Platform.Application/Features/AccountPreferences/UpdateAccountPreferencesHandler.cs:20-35`).

The authentication gateway proves where account identity is available: Version 2 `POST /session` returns `CurrentAccountId`, and subsequent account/position proof requests reuse the same tokens (`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgBrokerAuthenticationGateway.cs:32-79`, `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgBrokerAuthenticationGateway.cs:105-150`). The preference boundary therefore needs one account-bound operation or session callback that returns account identity plus a generation/evidence identifier and keeps initial `GET`, optional `PUT`, and confirming `GET` in the same token context.

### Reconciliation and Remediation

The closest query/refresh split is Account Details: `GetAccountDetailsHandler` reads SQL only, while `RefreshAccountDetailsHandler` owns provider I/O, in-process coalescing, SQL lease acquisition, and persistence (`src/TNC.Trading.Platform.Application/Features/AccountDetails/GetAccountDetailsHandler.cs:5-39`, `src/TNC.Trading.Platform.Application/Features/AccountDetails/RefreshAccountDetailsHandler.cs:6-76`). Account Preferences should follow the same separation with distinct query, desired-state update, observe-only reconciliation, manual retry, and explicit remediation operations.

The existing background pattern is a `BackgroundService` whose tick runner creates an async DI scope and resolves a scoped handler (`src/TNC.Trading.Platform.Api/Hosting/PlatformAuthenticationSupervisor.cs:8-75`). Authentication reconciliation is protected by an application lease and stores retry timing (`src/TNC.Trading.Platform.Application/Features/ReconcilePlatformAuthentication/PlatformAuthenticationReconciler.cs:82-101`, `src/TNC.Trading.Platform.Application/Features/ReconcilePlatformAuthentication/PlatformAuthenticationReconciler.cs:130-138`). A preference supervisor can reuse this composition shape, but it should query durable due work rather than poll IG every second.

Observe-only reconciliation must load desired account/value/revision, acquire an account-partitioned lease, perform one bound `GET`, classify equality/drift/failure, append an account-bound observation where factual, and conditionally persist the result only when the desired revision still matches. Explicit remediation must be a separate command authorized against the same revision; each attempt starts with `GET`, performs at most one `PUT`, and ends with authoritative `GET` in one session context.

### API Compatibility

The existing public compatibility surface is narrow but synchronous: `GET` and successful `PUT` both return `200` with the same three required fields (`src/TNC.Trading.Platform.Api/Features/Platform/AccountPreferencesEndpointMapping.cs:8-19`, `src/TNC.Trading.Platform.Web/PlatformApiClient.cs:139-158`). Replacing those fields outright would break the Web client and any external consumer.

The least disruptive staged option is to keep route and method identities, expand `GET` with nullable desired/observed/reconciliation fields, and decide whether to retain legacy aliases during one compatibility interval. The `PUT` decision is unresolved: it can return the committed desired-state projection immediately as `200`, or change to `202 Accepted` for asynchronous verification. `202` is semantically cleaner but is a contract break that requires coordinated Web and API tests. Explicit remediation and retry should use new `POST` routes rather than flags on `PUT`, preserving one operation per handler.

### UI Components and Presenters

`AccountPreferencesPagePresenter` owns loading, saving, pagination, and user-facing errors. `AccountPreferencesPageViewModel` currently collapses editable selection and provider-confirmed state into `TrailingStopsEnabled` plus nullable `ConfirmedTrailingStopsEnabled` (`src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPagePresenter.cs:3-85`, `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPageViewModel.cs:3-30`). The redesign should model these independently:

* Nullable persisted desired value and desired revision
* Nullable last observed IG value, account, and timestamp
* Verification status and freshness
* Retry/failure summary
* Pending editor value and stale-revision save/remediation feedback

`AccountPreferences.razor` currently hides the entire editor until a provider-confirmed value exists and labels the page as a live control (`src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:11-43`). It should instead always render a durable-state section, including `Unconfigured`, and separately render `Last observed at IG`. Drift, stale, failed, and unsupported states need an accessible warning (`role="alert"` for actionable failure or drift); pending verification can use `role="status"`. Add explicit `Retry verification` and, only if product-approved, `Apply desired setting to IG` commands. The existing page uses plain semantic radio/button/table controls, which is compatible with the Radzen guidance because no advanced shared widget is needed.

`PlatformApiClient` owns serialization and authorization scopes, while `PlatformWebUiServiceCollectionExtensions` registers the presenter (`src/TNC.Trading.Platform.Web/PlatformApiClient.cs:139-169`, `src/TNC.Trading.Platform.Web/PlatformWebUiServiceCollectionExtensions.cs:8-23`). Problem Details parsing should be added here or in a reusable API-error helper so the presenter can distinguish current-state, history, stale revision, drift, and unavailable provider outcomes.

### Tests and Validation Commands

Existing focused coverage and required changes:

* Application unit tests in `AccountPreferencesHandlerTests` currently encode provider-first GET and synchronous PUT/GET behavior. Replace or split them into SQL-only query, desired-state commit, observe-only classification, stale-revision rejection, retry scheduling, explicit remediation, and account/session binding tests (`test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/AccountPreferences/AccountPreferencesHandlerTests.cs:9-151`).
* API tests in `AccountPreferencesEndpointMappingTests` encode the three-field success shape and `409` provider mismatch. Add expanded projection, nullable `Unconfigured`, compatibility aliases if selected, revision validation, manual retry, remediation authorization, and stale-revision Problem Details tests (`test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/AccountPreferencesEndpointMappingTests.cs:8-90`). Retain route authorization coverage in `AccountPreferencesEndpointTests` and DI construction coverage in `PlatformApplicationServiceCollectionExtensionsTests`.
* IG gateway tests already prove Version 2 session creation, Version 1 preference requests, strict Boolean parsing, one GET replay after `401`, and no blind PUT replay (`test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/Integrations/Ig/IgAccountPreferencesGatewayTests.cs:10-93`). Extend them with `currentAccountId` parsing, requested-account mismatch, one-session `GET -> PUT -> GET`, and generation evidence.
* SQL integration tests cover migration history, context restart, keyset paging, partition isolation, invalid cursors, and retention (`test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/AccountPreferencesSqlIntegrationTests.cs:13-183`). Extend them with `Unconfigured` bootstrap, desired-state restart durability, atomic desired/audit commit, monotonic revision, compare-and-set stale completion, account-partitioned lease contention, due-work persistence, and account-bound observations. Update fixture cleanup for the new table (`test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/SqlServerDatabaseFixture.cs:65-91`).
* Web bUnit tests encode live-provider labels and current/history all-or-nothing loading (`test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountPreferencesTests.cs:12-174`). Replace those expectations with desired versus observed labels and every verification status. Add the cheapest diagnostic test: successful current state plus failed history retains the current state and reports only history failure.
* No account-preferences tests exist in the Aspire E2E or Playwright functional projects. Both projects already reference `Aspire.Hosting.Testing`; E2E also references `Microsoft.Playwright.Xunit` (`test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/TNC.Trading.Platform.Web.E2ETests.csproj:8-25`, `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/TNC.Trading.Platform.Web.FunctionalTests.csproj:8-25`). Add one bounded closed-box workflow with a controllable provider double through environment/configuration, not DI replacement inside launched processes. Add a Playwright scenario only after requirements define durable UI wording and remediation behavior.

Executable validation sequence from repository root:

```powershell
dotnet build TNC.Trading.Platform.slnx
dotnet test test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/TNC.Trading.Platform.Application.UnitTests.csproj --filter "FullyQualifiedName~AccountPreferences"
dotnet test test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/TNC.Trading.Platform.Api.UnitTests.csproj --filter "FullyQualifiedName~AccountPreferences"
dotnet test test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/TNC.Trading.Platform.Infrastructure.UnitTests.csproj --filter "FullyQualifiedName~AccountPreferences"
dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj --filter "FullyQualifiedName~AccountPreferences"
dotnet test test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/TNC.Trading.Platform.Infrastructure.IntegrationTests.csproj --filter "FullyQualifiedName~AccountPreferences"
dotnet test test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/TNC.Trading.Platform.Api.IntegrationTests.csproj --filter "FullyQualifiedName~AccountPreferences"
dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/TNC.Trading.Platform.Web.E2ETests.csproj --filter "FullyQualifiedName~AccountPreferences"
dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/TNC.Trading.Platform.Web.FunctionalTests.csproj --filter "FullyQualifiedName~AccountPreferences"
dotnet test TNC.Trading.Platform.slnx --no-build
```

The SQL integration command requires the Aspire SQL Server container to remain running. The fixture discovers that container and creates an isolated database (`test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/SqlServerDatabaseFixture.cs:8-58`). Migration creation requires a valid design-time connection path because no `IDesignTimeDbContextFactory` exists; use the repository's configured API startup project, for example:

```powershell
dotnet ef migrations add AddAccountPreferencesCurrentState --project src/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.csproj --startup-project src/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.csproj --context PlatformDbContext
```

The migration must produce the normal migration, designer, and updated `PlatformDbContextModelSnapshot`; inspect generated SQL and test both empty-database and existing-observation upgrade paths before accepting it.

Baseline verification on 2026-08-31 passed 58 of 58 focused tests across the five existing Account Preferences Application, API mapping, IG gateway, Web bUnit, and SQL integration test files. No implementation was changed.

### Wiki and Work-Package Documentation

Seven wiki sections currently state that IG live reads are authoritative and observations are never a fallback. They must change with implementation:

* `docs/wiki/api-reference.md:551-585` for expanded `GET`, changed `PUT`, retry/remediation routes, status values, revision conflicts, and compatibility
* `docs/wiki/application-overview.md:195-210` for desired/current-state store and reconciler ownership
* `docs/wiki/architecture.md:600-615` for SQL intent authority, IG observation authority, account/session binding, leases, and stale-writer protection
* `docs/wiki/local-development.md:144-159` for provider-double setup, migration, diagnostics, and manual verification
* `docs/wiki/operator-guide.md:565-600` for `Unconfigured`, desired/observed labels, warnings, retry, and explicit remediation
* `docs/wiki/runtime-behavior.md:728-746` for triggers, due work, observe-only transitions, startup independence, and future fail-closed trading gate
* `docs/wiki/testing-and-quality.md:553-575` for the new unit, SQL concurrency, closed-box Aspire, and Playwright coverage

The recommendation is substantial enough for a dedicated work package, but repository rules prohibit drafting one unless explicitly requested. When requested, allocate the next monotonically increasing `docs/00x-account-preferences-state-verification/` folder with `requirements.md`, `technical-specification.md`, and `plans/001-delivery-plan.md`; do not place a plan at the package root.

### Likely Files to Modify

Application and API:

* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/GetAccountPreferencesHandler.cs`
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/GetAccountPreferencesResponse.cs`
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/UpdateAccountPreferencesHandler.cs`
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/UpdateAccountPreferencesRequest.cs`
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/UpdateAccountPreferencesResponse.cs`
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/UpdateAccountPreferencesValidator.cs`
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/IAccountPreferencesGateway.cs`
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/TrailingStopsPreferenceObservation.cs`
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/ITrailingStopsPreferenceObservationStore.cs`
* `src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs`
* `src/TNC.Trading.Platform.Api/Features/Platform/AccountPreferencesEndpointMapping.cs`
* `src/TNC.Trading.Platform.Api/Features/Platform/GetAccountPreferencesEndpointHandler.cs`
* `src/TNC.Trading.Platform.Api/Features/Platform/UpdateAccountPreferencesEndpointHandler.cs`
* `src/TNC.Trading.Platform.Api/Hosting/PlatformApplicationServiceCollectionExtensions.cs`
* `src/TNC.Trading.Platform.Api/Program.cs`

Infrastructure and Web:

* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgAccountPreferencesGateway.cs`
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/Entities/TrailingStopsPreferenceObservationEntity.cs`
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfTrailingStopsPreferenceObservationStore.cs`
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/PlatformDbContext.cs`
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/Migrations/PlatformDbContextModelSnapshot.cs`
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/DependencyInjection/PlatformInfrastructureServiceCollectionExtensions.cs`
* `src/TNC.Trading.Platform.Web/PlatformApiClient.cs`
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPagePresenter.cs`
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPageViewModel.cs`
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor`
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor.css`

Tests and documentation:

* Existing account-preferences unit and integration files identified under Tests and Validation Commands
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/SqlServerDatabaseFixture.cs`
* The seven wiki pages identified under Wiki and Work-Package Documentation

### Likely Files to Create

Exact names can follow the repository's one-operation-per-folder-file pattern. The likely minimum is:

* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/AccountPreferencesCurrentState.cs`
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/AccountPreferencesVerificationStatus.cs`
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/IAccountPreferencesCurrentStateStore.cs`
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/IAccountPreferencesReconciliationLease.cs`
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/ReconcileAccountPreferencesRequest.cs`
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/ReconcileAccountPreferencesResponse.cs`
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/ReconcileAccountPreferencesHandler.cs`
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/RetryAccountPreferencesVerificationRequest.cs`
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/RetryAccountPreferencesVerificationResponse.cs`
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/RetryAccountPreferencesVerificationHandler.cs`
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/RemediateAccountPreferencesRequest.cs`
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/RemediateAccountPreferencesResponse.cs`
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/RemediateAccountPreferencesHandler.cs`
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/RemediateAccountPreferencesValidator.cs`
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/Entities/AccountPreferencesCurrentStateEntity.cs`
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfAccountPreferencesCurrentStateStore.cs`
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/SqlAccountPreferencesReconciliationLease.cs`
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/Migrations/<timestamp>_AddAccountPreferencesCurrentState.cs`
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/Migrations/<timestamp>_AddAccountPreferencesCurrentState.Designer.cs`
* `src/TNC.Trading.Platform.Api/Hosting/AccountPreferencesReconciliationSupervisor.cs`
* `src/TNC.Trading.Platform.Api/Features/Platform/RetryAccountPreferencesVerificationEndpointHandler.cs`
* `src/TNC.Trading.Platform.Api/Features/Platform/RemediateAccountPreferencesEndpointHandler.cs`
* `src/TNC.Trading.Platform.Api/Features/Platform/AccountPreferencesResponse.cs`
* `src/TNC.Trading.Platform.Api/Features/Platform/AccountPreferencesObservationResponse.cs`
* `src/TNC.Trading.Platform.Api/Features/Platform/AccountPreferencesHistoryResponse.cs`
* `src/TNC.Trading.Platform.Api/Features/Platform/UpdateAccountPreferencesHttpRequest.cs`

The four API records currently share `AccountPreferencesEndpointMapping.cs`; touching this contract is the appropriate time to move each top-level record to its matching file under the repository's one-type-per-file rule. Create focused test files when classes would otherwise become broad, for example `ReconcileAccountPreferencesHandlerTests.cs`, `RemediateAccountPreferencesHandlerTests.cs`, `EfAccountPreferencesCurrentStateStoreTests.cs`, `SqlAccountPreferencesReconciliationLeaseTests.cs`, and an `AccountPreferences/AccountPreferencesStateE2ETests.cs` under the existing E2E project. A Playwright file belongs under a stable feature folder in the existing FunctionalTests project, such as `AccountPreferences/AccountPreferencesStateTests.cs`.

### Parallel Implementation Boundaries

After the contract and schema names are fixed, these lanes can proceed mostly in parallel:

1. Diagnostic lane: presenter split, Problem Details preservation, ordinary-path telemetry fix, and focused Web/Application tests. This has no dependency on the schema redesign and should land first.
2. Persistence lane: current-state model/port, EF entity/store, audit transaction, migration, revision compare-and-set, account-bound observation mapping, SQL lease, and integration tests.
3. Provider lane: account-bound gateway contract and one-session observe/remediate implementation with HTTP unit tests. It depends only on the agreed inward contract, not EF.
4. Reconciliation lane: observe-only handler, due-work policy, retry/manual retry, explicit remediation, stale-revision policy, and application unit tests. It consumes the persistence and provider contracts and can begin against fakes.
5. API lane: expanded projection and new command endpoints with compatibility tests. It depends on finalized response/status semantics but can use fake handlers.
6. Web lane: desired/observed view model, status presentation, warnings, retry/remediation controls, and bUnit tests. It depends on the API JSON contract and product wording.
7. Background and closed-box lane: supervisor registration, trigger wiring, controllable-provider Aspire scenario, and optional Playwright flow. It integrates lanes 2 through 6 and should land after their focused tests pass.
8. Documentation lane: wiki updates can be drafted against the accepted contracts in parallel, then verified against the integrated behavior before plan completion.

Do not parallelize competing edits to `PlatformDbContext.cs`, `PlatformEndpoints.cs`, `PlatformApiClient.cs`, or the existing large test files without assigning one owner per file.

## Unresolved Decisions

* Which explicit IG account ID is operator-approved for automated trading? `IgLoginSnapshot.CurrentAccountId` is authentication evidence and `IgProofDataSnapshot.PreferredAccountId` is provider preference, not durable operator intent (`src/TNC.Trading.Platform.Application/Configuration/IgLoginSnapshot.cs:3-14`, `src/TNC.Trading.Platform.Application/Configuration/IgProofDataSnapshot.cs:3-8`).
* Is trailing stops always required, or can the operator choose either Boolean?
* Does `PUT /account-preferences` remain `200` with committed desired state, change to `202`, or retain legacy fields during a compatibility window?
* Is explicit remediation a separate `Apply desired setting to IG` action, or a product-level `Save and apply` option that still creates a separate revision-bound command internally?
* What verification freshness duration, retry backoff, retry exhaustion, notification threshold, and manual override policy apply?
* What exactly constitutes a session generation? `PlatformRuntimeState.LatestIgLoginSnapshotId` identifies the latest successful auth snapshot (`src/TNC.Trading.Platform.Application/Configuration/PlatformRuntimeState.cs:25-39`), but preference calls currently establish separate sessions. Persist both the operation's account ID and a non-secret generated session/attempt ID unless a shared session service is introduced.
* Should account switch use IG's account-switch endpoint or reject any login whose `currentAccountId` differs from the configured target? The current code has no switch operation.
* Which statuses are factual and public? Recommended minimum: `Unconfigured`, `PendingVerification`, `InSync`, `Drifted`, `VerificationFailed`, and `Unsupported`; staleness may be a derived flag instead of another persisted status.
* How long should observations remain visible after desired account changes, and should history paging add account ID to its partition key and response?
* No order submission exists. The future fail-closed trading gate should be documented as a separate application-boundary requirement, not implemented in authentication startup now.

## Recommended Plan Structure

1. Requirements and compatibility decisions: target account selection, desired-value policy, public statuses, freshness/retry rules, remediation authorization, and HTTP compatibility.
2. Cheapest diagnostic correction: independent current/history load feedback, Problem Details preservation, and missing event-store forwarding with focused tests.
3. Durable state foundation: `Unconfigured` bootstrap, desired value/account/revision, observed facts, retry metadata, audit, migration, restart durability, and compare-and-set tests. Do not backfill desired intent from retained observations.
4. Account-bound provider boundary: one session context, account identity verification, observation operation, remediation operation, strict no-blind-write retry, and gateway tests.
5. Observe-only reconciliation: equality/drift/failure transitions, account-partitioned SQL lease, revision guard, durable due work, manual retry, and notification/event rules.
6. Explicit remediation: revision-bound authorization, initial `GET`, at most one `PUT`, confirming `GET`, stale authorization rejection, and audit/event coverage.
7. API evolution: SQL-only projection, desired update semantics, retry/remediation endpoints, Problem Details, authorization, OpenAPI, and compatibility tests.
8. UI evolution: durable desired state, separate observed state, `Unconfigured`, status/freshness warnings, retry/remediation controls, independent history failure, accessibility, and bUnit coverage.
9. Background integration: best-effort trigger after usable account-bound authentication, trigger after desired changes, durable due-work supervisor, startup independence, and replica behavior.
10. System validation and documentation: SQL migration upgrade, focused suites, closed-box Aspire convergence, optional Playwright operator flow, full solution test, and all affected wiki pages.

Each numbered implementation phase should have an executable exit criterion and should avoid claiming future trading safety. The future order boundary must independently require fresh, in-sync evidence matching desired revision, target account, and session generation.

## Validation-Gap Addendum for DR-05 Through DR-07

### DR-05 Dedicated Desired-State Audit

`ConfigurationAuditEntity` is not the account-preference audit boundary. Its required `ConfigurationId` and platform-configuration-specific fields make it structurally coupled to the singleton platform configuration (`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/Entities/ConfigurationAuditEntity.cs:3-23`). `SqlPlatformConfigurationStore.CommitAsync` sets that identifier, writes a generic redacted details payload, and commits the configuration and audit through one `PlatformDbContext.SaveChangesAsync` call (`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Configuration/SqlServer/SqlPlatformConfigurationStore.cs:53-126`). The reusable precedent is the same-context unit of work, not the entity or table.

Use a dedicated append-only `AccountPreferencesDesiredStateAuditEntity` and let `EfAccountPreferencesCurrentStateStore` own the atomic write. Do not introduce a separately saving audit store. The inward `IAccountPreferencesCurrentStateStore.CommitDesiredStateAsync` operation should load or create the partition row, validate the expected revision, increment the desired revision, update current state, add one typed audit entity, and call `SaveChangesAsync` once. EF wraps that relational save in a transaction; an explicit transaction is needed only if the implementation requires multiple saves. The existing manual-auth committer shows the explicit transaction pattern when several subordinate stores save independently (`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/ManualAuthRetryCommitter.cs:9-51`).

The audit entity should contain these typed columns rather than a platform-configuration `DetailsJson` surrogate:

* `AccountPreferencesDesiredStateAuditId` as a `long` identity primary key
* `AccountPreferencesCurrentStateId` as the owning state-row identifier, with delete restricted
* `PlatformEnvironment`, `BrokerEnvironment`, and `AccountId` as the immutable partition snapshot
* `PreviousDesiredTrailingStopsEnabled` and `DesiredTrailingStopsEnabled`
* `PreviousDesiredRevision` and `DesiredRevision`
* `OccurredAtUtc`, `ChangedBy`, `ChangeType`, and `CorrelationId`

Create or modify these files:

* Create `src/TNC.Trading.Platform.Application/Features/AccountPreferences/AccountPreferencesDesiredStateChange.cs` for the inward commit data, including expected revision, actor, and correlation ID
* Modify `src/TNC.Trading.Platform.Application/Features/AccountPreferences/IAccountPreferencesCurrentStateStore.cs` so one commit operation owns state plus audit atomically
* Create `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/Entities/AccountPreferencesDesiredStateAuditEntity.cs`
* Modify `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfAccountPreferencesCurrentStateStore.cs` to stage both entities and save once
* Modify `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/PlatformDbContext.cs` to add the audit `DbSet`, lengths, restricted relationship, a unique `(AccountPreferencesCurrentStateId, DesiredRevision)` index, and a history index on `(BrokerEnvironment, AccountId, DesiredRevision)`; current entity registration and audit registration precedents are at lines 9-25 and 111-121
* Modify `src/TNC.Trading.Platform.Infrastructure/Infrastructure/DependencyInjection/PlatformInfrastructureServiceCollectionExtensions.cs` to register the current-state store; this is the active composition namespace imported by API startup (`src/TNC.Trading.Platform.Api/Program.cs:7-21`)
* Create the migration and designer and modify `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/Migrations/PlatformDbContextModelSnapshot.cs`
* Modify `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/SqlServerDatabaseFixture.cs` to drop the audit table before the current-state table; existing cleanup is at lines 65-91

Do not add this desired-state history to `OperationalRecordRetentionProcessor` without a separate product retention decision. The current generic processor deletes `ConfigurationAudits` and observations under `Retention:OperationalRecordsDays` (`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Operations/Retention/OperationalRecordRetentionProcessor.cs:21-55`, `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Operations/Retention/OperationalRecordRetentionProcessor.cs:65-109`). Desired revisions are operator intent history, not provider observation telemetry.

Add SQL integration tests to `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/AccountPreferencesSqlIntegrationTests.cs` for first configuration, monotonic revisions, actor/correlation capture, restart readability, and uniqueness. Add an audit-insert command interceptor, following `FailConfigurationAuditInsertInterceptor`, and prove that an audit insert failure leaves both the prior desired row and revision unchanged (`test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/PlatformSqlServerIntegrationTests.cs:277-351`, `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/PlatformSqlServerIntegrationTests.cs:533-582`). Add a retention test proving generic operational retention does not delete desired-state audits.

### DR-06 Authentication Success Nudge

There are two live authentication owners and one dormant duplicate path:

* Automatic reconciliation calls the broker in `PlatformAuthenticationReconciler.TransitionToActiveAsync`, captures the successful snapshot and account identity, applies Active state, and then invokes best-effort account-details capture (`src/TNC.Trading.Platform.Application/Features/ReconcilePlatformAuthentication/PlatformAuthenticationReconciler.cs:392-480`). `ReconcileCoreAsync` persists runtime state after the transition returns (`src/TNC.Trading.Platform.Application/Features/ReconcilePlatformAuthentication/PlatformAuthenticationReconciler.cs:162-192`).
* The HTTP manual-retry route calls `TriggerManualAuthRetryHandler`, not the reconciler (`src/TNC.Trading.Platform.Api/Features/Platform/TriggerManualAuthRetryEndpointHandler.cs:8-17`). On success the handler creates a snapshot, applies Active state, atomically commits the recovery intent, and only then invokes best-effort account-details capture (`src/TNC.Trading.Platform.Application/Features/TriggerManualAuthRetry/TriggerManualAuthRetryHandler.cs:84-145`).
* `PlatformAuthenticationReconciler.TriggerManualRetryAsync` and `AttemptImmediateRecoveryAsync` retain another successful broker path (`src/TNC.Trading.Platform.Application/Features/ReconcilePlatformAuthentication/PlatformAuthenticationReconciler.cs:84-90`, `src/TNC.Trading.Platform.Application/Features/ReconcilePlatformAuthentication/PlatformAuthenticationReconciler.cs:221-273`), but `IPlatformAuthenticationReconciler` exposes only `ReconcileAsync` and no production caller invokes this method (`src/TNC.Trading.Platform.Application/Features/ReconcilePlatformAuthentication/IPlatformAuthenticationReconciler.cs:5-8`). Remove that stale manual path when practical; if retained, give it the same nudge.

Create an Account Preferences-owned cross-feature hook, mirroring `IAccountDetailsDailyCapture` (`src/TNC.Trading.Platform.Application/Features/AccountDetails/IAccountDetailsDailyCapture.cs:3-6`):

* Create `NudgeAccountPreferencesVerificationRequest.cs` with `BrokerEnvironment`, `AccountId`, `AuthenticationSnapshotId`, and `AuthenticatedAtUtc`
* Create `NudgeAccountPreferencesVerificationResponse.cs` with a typed `NoConfiguredState`, `AccountMismatch`, `AlreadyNudged`, or `MarkedDue` outcome
* Create `IAccountPreferencesVerificationNudge.cs` with `HandleAsync(request, cancellationToken)`
* Create `NudgeAccountPreferencesVerificationHandler.cs` implementing the interface through `IAccountPreferencesCurrentStateStore`

The handler should only mark matching configured state immediately due and persist the latest authentication snapshot ID as non-secret session-generation evidence. It must not call IG, run reconciliation inline, or change authentication health. Repeated snapshot IDs must be idempotent, and account mismatch must be a no-op.

Inject `IAccountPreferencesVerificationNudge` into both live authentication owners. In `TriggerManualAuthRetryHandler`, call it after the recovered `IManualAuthRetryCommitter.CommitAsync` completes and before the existing account-details capture. In `PlatformAuthenticationReconciler.TransitionToActiveAsync`, call it beside the existing best-effort post-success capture, using `successfulSnapshot.CurrentAccountId`, `successfulSnapshot.Id`, and `CapturedAtUtc`. Catch non-cancellation failures so a nudge cannot invalidate authentication success. Register the handler and interface in `src/TNC.Trading.Platform.Api/Hosting/PlatformApplicationServiceCollectionExtensions.cs`, whose current authentication and Account Preferences registrations are at lines 29-48.

Add direct handler tests for matching account, mismatch, unconfigured state, repeated generation, and an already-due row. Extend `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/TriggerManualAuthRetry/TriggerManualAuthRetryHandlerTests.cs`; its existing workflow-order test proves post-commit best-effort capture at lines 91-113. Extend `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/AuthRetryCycleTests.cs`; its automatic post-success durability test and coordinator injection point are at lines 465-493 and 1050-1077. Assert that nudge failure is swallowed, cancellation propagates, and both automatic and HTTP manual success pass the exact persisted snapshot/account evidence.

### DR-07 Provider Base URL and Closed-Box Double

The active Account Preferences client hard-codes `https://demo-api.ig.com/gateway/deal/` in `PlatformInfrastructureServiceCollectionExtensions` (`src/TNC.Trading.Platform.Infrastructure/Infrastructure/DependencyInjection/PlatformInfrastructureServiceCollectionExtensions.cs:97-104`). `IgAccountPreferencesGateway` uses relative `session` and `accounts/preferences` paths, so changing only that named client's base address redirects its complete session and preference exchange (`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgAccountPreferencesGateway.cs:52-76`).

Introduce configuration key `Ig:AccountPreferencesBaseUrl` with environment form `Ig__AccountPreferencesBaseUrl`:

* Modify `src/TNC.Trading.Platform.Api/appsettings.json` to add the production default `https://demo-api.ig.com/gateway/deal/`
* Modify `src/TNC.Trading.Platform.Infrastructure/Infrastructure/DependencyInjection/PlatformInfrastructureServiceCollectionExtensions.cs` so only `IAccountPreferencesGateway` reads and validates this key as an absolute HTTP or HTTPS URI with a trailing slash; leave the authentication and Account Details clients unchanged
* Modify `src/TNC.Trading.Platform.AppHost/AppHostSettings.cs` to parse nullable `AccountPreferencesBaseUrl`; its current configuration projection is at lines 5-25
* Modify `src/TNC.Trading.Platform.AppHost/AppHostEnvironmentWiring.cs` so `ConfigureApiProject` forwards a non-empty value as `Ig__AccountPreferencesBaseUrl`; its current API environment loop and transport forwarding are at lines 106-140
* `src/TNC.Trading.Platform.AppHost/AppHost.cs` already parses settings before configuring the API project, so no control-flow change is needed (`src/TNC.Trading.Platform.AppHost/AppHost.cs:3-13`)

Add composition tests in `test/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.UnitTests/AppHostSettingsTests.cs` and `AppHostEnvironmentWiringTests.cs` for absent, valid, and forwarded values. Add an Infrastructure DI test that resolves the named gateway client with an override and rejects malformed or non-HTTP values. Existing gateway request tests may continue constructing `HttpClient` directly (`test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/Integrations/Ig/IgAccountPreferencesGatewayTests.cs:10-93`).

For closed-box control, use one shared WireMock server compiled into both existing Web test projects:

* Create `test/Shared/AccountPreferences/ControllableIgProvider.cs` to start on a random loopback port, expose a base URL ending in `/gateway/deal/`, queue responses, record requests, and reset between scenarios
* Add `WireMock.Net` to `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/TNC.Trading.Platform.Web.E2ETests.csproj` and `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/TNC.Trading.Platform.Web.FunctionalTests.csproj`, and link the shared source file as those projects already do for the shared AppHost harness (`test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/TNC.Trading.Platform.Web.E2ETests.csproj:8-32`, `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/TNC.Trading.Platform.Web.FunctionalTests.csproj:8-32`)
* Modify `test/Shared/Authentication/RealAppHostProcessFactory.cs` so both launch methods accept optional additional environment overrides, merge `Ig__AccountPreferencesBaseUrl`, establish environment scopes before `DistributedApplicationTestingBuilder.CreateAsync`, and retain them for disposal; the current environment construction and managed launch are at lines 10-42 and 45-91
* Modify the E2E and Functional `Authentication/AppHostProcessFactory.cs` wrappers to pass the provider URL
* Modify `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/Authentication/RealAuthenticationE2ETestFixture.cs` and `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/Authentication/RealAuthenticationFunctionalTestFixture.cs` to start the provider before AppHost and dispose it after AppHost
* Create `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/AccountPreferences/AccountPreferencesStateE2ETests.cs`; add the later browser flow under the matching FunctionalTests folder

The double must script `POST /gateway/deal/session` with `CST`, `X-SECURITY-TOKEN`, and `currentAccountId`, `GET /gateway/deal/accounts/preferences` with `trailingStopsEnabled`, and `PUT /gateway/deal/accounts/preferences` with a success acknowledgement. It should support connection failure or `503`, recovery, drift values, request-order assertions, and delayed responses. Tests control the external double directly and interact with the platform only through public HTTP/browser boundaries, satisfying the launched-process rule without DI replacement. The current fixture already uses `DistributedApplicationTestingBuilder` and retains environment scopes for cleanup (`test/Shared/Authentication/RealAppHostProcessFactory.cs:61-91`).

No product code or planning file was modified during this research.

## References and Evidence

Primary recommendation: `.copilot-tracking/research/2026-08-31/account-preferences-state-verification-research.md`.

Key architecture evidence:

* Current API startup performs SQL initialization and a blocking authentication reconciliation before serving, while the hosted supervisor also performs a tick during `StartAsync` (`src/TNC.Trading.Platform.Api/Program.cs:26-39`, `src/TNC.Trading.Platform.Api/Hosting/PlatformAuthenticationSupervisor.cs:30-45`). Preference verification must not be added to this blocking startup scope.
* SQL-only query plus separate provider refresh is already established by Account Details (`src/TNC.Trading.Platform.Application/Features/AccountDetails/GetAccountDetailsHandler.cs:5-39`, `src/TNC.Trading.Platform.Application/Features/AccountDetails/RefreshAccountDetailsHandler.cs:6-76`).
* Configuration update demonstrates desired commit followed by reconciliation, and the SQL store demonstrates same-context configuration plus audit persistence (`src/TNC.Trading.Platform.Application/Features/UpdatePlatformConfiguration/UpdatePlatformConfigurationHandler.cs:5-18`, `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Configuration/SqlServer/SqlPlatformConfigurationStore.cs:53-126`). It does not provide revision concurrency and should be treated as an audit/ordering precedent only.
* Authentication persists current account identity in login snapshots and optional preferred-account proof separately (`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfPlatformIgLoginSnapshotStore.cs:13-57`, `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfPlatformIgProofDataStore.cs:9-61`).
* Background services create scopes for scoped processors/handlers (`src/TNC.Trading.Platform.Api/Hosting/PlatformAuthenticationSupervisor.cs:62-75`, `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Operations/Retention/OperationalRecordRetentionService.cs:8-38`).