---
title: Account Details Architecture Research
description: Verified architecture seams and delivery guidance for Account Details
ms.date: 2026-08-07
ms.topic: reference
---

## Research Scope

* Verify the existing successful authentication paths where daily Account Details capture must hook in
* Locate API endpoint and feature registration plus Web navigation and history seams
* Locate Infrastructure DI, EF migration and baseline, retention, SQL lease, and relevant test commands

## Status

Complete. Source-verified against the current workspace on 2026-08-07. No product code was changed and no test command was run for this research-only task.

## Confirmed Findings

### Successful authentication paths and daily-capture hooks

* The reconciler owns automatic successful-login transitions. It authenticates, persists the login snapshot and reduced proof, then sets active state in both immediate recovery and normal inactive-to-active paths: `src/TNC.Trading.Platform.Application/Features/ReconcilePlatformAuthentication/PlatformAuthenticationReconciler.cs:L220-L243` and `src/TNC.Trading.Platform.Application/Features/ReconcilePlatformAuthentication/PlatformAuthenticationReconciler.cs:L380-L413`.
* Durable runtime-state save occurs after the reconciliation transition returns: `src/TNC.Trading.Platform.Application/Features/ReconcilePlatformAuthentication/PlatformAuthenticationReconciler.cs:L162-L189`. A daily Account Details hook inside `TransitionToActiveAsync` would run before that save. Have `ReconcileCoreAsync` identify new success, save state, then invoke a non-fatal capture command so account-capture failure cannot invalidate login.
* Reconciliation runs at API startup, each supervisor tick, and after configuration updates: `src/TNC.Trading.Platform.Api/Program.cs:L30-L34`, `src/TNC.Trading.Platform.Api/Hosting/PlatformAuthenticationSupervisor.cs:L67-L75`, and `src/TNC.Trading.Platform.Application/Features/UpdatePlatformConfiguration/UpdatePlatformConfigurationHandler.cs:L7-L16`.
* The HTTP manual-retry path does not use `PlatformAuthenticationReconciler.TriggerManualRetryAsync`, which has no current caller. The active path is `TriggerManualAuthRetryHandler`, registered at `src/TNC.Trading.Platform.Api/Hosting/PlatformApplicationServiceCollectionExtensions.cs:L38`, routed at `src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs:L42-L43`, and initiated from Status at `src/TNC.Trading.Platform.Web/Components/Pages/Status/StatusPagePresenter.cs:L22-L26`.
* Direct manual retry authenticates at `src/TNC.Trading.Platform.Application/Features/TriggerManualAuthRetry/TriggerManualAuthRetryHandler.cs:L82-L83`, then commits recovered state, login snapshot, proof data, retry cycle, and event at `TriggerManualAuthRetryHandler.cs:L112-L121`. The hook belongs after that final commit, before or after notification according to failure-isolation policy. Do not include external IG retrieval inside the existing transaction.
* `ManualAuthRetryCommitter` opens a relational transaction and persists the successful login/proof before retry-state records: `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/ManualAuthRetryCommitter.cs:L16-L58`. Use a separate local transaction for an already-validated Account Details result, not the provider call.
* `IgBrokerAuthenticationGateway` creates a v2 session, retains `CST` and `X-SECURITY-TOKEN` locally, and best-effort calls version-1 `GET accounts`: `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgBrokerAuthenticationGateway.cs:L28-L92` and `IgBrokerAuthenticationGateway.cs:L111-L174`. It silently reduces the result to one preferred-account proof, so it is not the Account Details operation. A dedicated port/gateway may share only a private Infrastructure session helper.

### API feature registration and Web seams

* API handler registration is centralized in `src/TNC.Trading.Platform.Api/Hosting/PlatformApplicationServiceCollectionExtensions.cs:L17-L43`. Add Account Details read/refresh handlers there; register their adapter implementations in the Infrastructure extension.
* Endpoint registration is centralized in `src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs:L27-L51`, called once from `src/TNC.Trading.Platform.Api/Program.cs:L74`. The closest patterns are Viewer `GET /api/platform/ig-login/history` at `PlatformEndpoints.cs:L36-L37` and Operator `POST /api/platform/auth/manual-retry` at `PlatformEndpoints.cs:L42-L43`.
* `src/TNC.Trading.Platform.Web/Components/Pages/IgLogin/IgLoginHistory.razor` is the closest page. It enforces Viewer access and verifies Viewer scope before load at `IgLoginHistory.razor:L1-L7` and `IgLoginHistory.razor:L101-L118`. Reuse its saved-data load/error shape and add an Operator-scoped refresh control.
* Viewer navigation lives in `src/TNC.Trading.Platform.Web/Components/Layout/MainLayout.razor:L22-L25`; add `/account-details` adjacent to login history. The current history page returns every retained snapshot, so it is presentation precedent only, not keyset-navigation precedent.
* `PlatformApiClient` owns authorized calls. Its history method sends the Viewer scope and maps the response at `src/TNC.Trading.Platform.Web/PlatformApiClient.cs:L103-L114`; shared bearer request construction is at `PlatformApiClient.cs:L117-L126`. Add distinct Account Details GET and POST methods here.
* Razor page discovery is assembly-based through `src/TNC.Trading.Platform.Web/Program.cs:L74-L75`; the page needs an `@page` directive, not a separate route registry entry.

### Infrastructure, schema, retention, and lease seams

* SQL Server selection and Infrastructure composition are in `src/TNC.Trading.Platform.Infrastructure/Infrastructure/DependencyInjection/PlatformInfrastructureServiceCollectionExtensions.cs:L32-L58`. Existing lease, retention, proof-store, and typed IG-client registrations at `PlatformInfrastructureServiceCollectionExtensions.cs:L68-L91` are the registration pattern for Account Details adapters.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/PlatformDbContext.cs:L8-L129` owns DbSets and EF mappings. Add retrieval-header/account-row DbSets and mappings, an additive migration, and model snapshot update.
* SQL Server applies migrations while non-SQL tests use `EnsureCreatedAsync`: `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Startup/PlatformStartupInitializer.cs:L43-L54`. The guarded legacy baseline recognizes only complete original-schema tables at `PlatformStartupInitializer.cs:L19-L29` and records the initial migration at `PlatformStartupInitializer.cs:L17-L18` and `PlatformStartupInitializer.cs:L98-L110`. Do not add Account Details tables to `RequiredPlatformTables`: that list permits baseline migration for pre-feature databases and should remain scoped to the initial schema.
* Operational retention starts through registrations at `PlatformInfrastructureServiceCollectionExtensions.cs:L85-L87`. SQL uses set-based deletes and tests use an in-memory fallback: `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Operations/Retention/OperationalRecordRetentionProcessor.cs:L24-L76`. The default is 90 days at `src/TNC.Trading.Platform.Application/Services/OperationalRecordRetentionPolicy.cs:L5-L24`. Extend both branches for retrieval headers, using cascading child deletion. Preserving newest-success is new behavior; existing retention does not do this.
* `SqlPlatformReconciliationLease` provides a session-owned, zero-timeout exclusive `sp_getapplock` at `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/SqlPlatformReconciliationLease.cs:L12-L42`, released at `SqlPlatformReconciliationLease.cs:L47-L61`. Use a distinct resource scoped by broker environment for Account Details refresh, rather than blocking reconciliation. Direct manual retry has no equivalent lease today.

### Existing test seams and observed commands

* Manual retry tests at `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/TriggerManualAuthRetry/TriggerManualAuthRetryHandlerTests.cs:L61-L80` cover the successful final commit. Add fake capture-port tests for post-commit ordering, once-per-day behavior, automatic-capture failure, and rejected/failed auth outcomes.
* Reconcile handler tests only cover serialization and response mapping: `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/ReconcilePlatformAuthenticationHandlerTests.cs:L8-L114`. Add feature-level tests for the post-save automatic-capture decision.
* Infrastructure precedents are `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/OperationalRecordRetentionProcessorTests.cs:L18-L73` and `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/PlatformStartupInitializerTests.cs:L19-L121`. Add atomic header/children, daily uniqueness, retention/cascade, and startup/migration coverage.
* API endpoint tests are in `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/PlatformEndpointHandlerTests.cs:L17-L77`. Web bUnit precedents are `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/IgLoginHistoryTests.cs:L17-L115` and `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/MainLayoutTests.cs:L17-L93`.
* Current isolated Application, Infrastructure, API, and Web unit projects target `net10.0` and use xUnit. Their project files are under their respective test directories. The documented focused API command is `docs/wiki/testing-and-quality.md:L51-L52`; the repository uses .NET SDK `10.0.200` in `global.json:L2-L4`.

## Implementation Risks

* Hooking inside `TransitionToActiveAsync` or before direct manual retry's final commit risks changing successful-login behavior when Account Details fails. Capture only after durable success and isolate automatic-capture failure.
* The existing account call is proof-only. Expanding it in place would retain one account and preserve silent failure semantics. Use a dedicated full-list contract.
* Do not hold a database transaction around session creation or `GET accounts`. Validate the full response first, then locally commit the retrieval and all child rows atomically.
* Reusing the global reconciliation lease would serialize refreshes with auth supervision. Use an Account Details environment-scoped resource and a typed in-progress outcome.
* The source feature research's proposed update to `RequiredPlatformTables` conflicts with the actual initial-schema baseline mechanism. Leave that list unchanged for this additive migration.
* Existing retention deletes all aged retained login snapshots. A newest-retrieval exception needs a new predicate and tests for both SQL and in-memory branches.
* Viewer read and Operator refresh require API policy enforcement. Web visibility alone is not authorization.

## Recommended Delivery Sequence

1. Define Application Account Details read, manual-refresh, and best-effort daily-capture operations with inward-owned ports and typed outcomes.
2. Invoke daily capture after durable success in both actual paths: after `ReconcileCoreAsync` saves new success and after `TriggerManualAuthRetryHandler` completes its final success commit. Add focused Application tests before adapters.
3. Implement Infrastructure's dedicated IG session/accounts gateway, environment lease, EF entities/store/migration, and retention. Persist only after full provider validation, with one local transaction for header and children.
4. Register handlers/adapters, add Viewer GET and Operator POST endpoint mappings, and add endpoint-handler tests.
5. Add `PlatformApiClient` methods, Account Details Razor page, Viewer navigation, Operator refresh control, and bUnit states for empty/latest/history/error/pending/access.
6. Add SQL/AppHost coverage for migration, retention, lease contention, authorization, and the Viewer/Operator journey. Keep provider calls fake by default; a real IG test-account smoke check is opt-in.
7. Update owning `docs/wiki/` pages for real IG test-account handling, capture timing, persistence/retention, authorization, refresh behavior, and local test-account credentials.

## Validation Commands

The following existing commands were observed in repository documentation and project layout. They were not executed during this research task.

```powershell
dotnet build TNC.Trading.Platform.slnx
dotnet test test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/TNC.Trading.Platform.Application.UnitTests.csproj
dotnet test test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/TNC.Trading.Platform.Infrastructure.UnitTests.csproj
dotnet test test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/TNC.Trading.Platform.Api.UnitTests.csproj
dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj
dotnet test test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/TNC.Trading.Platform.Api.IntegrationTests.csproj
dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/TNC.Trading.Platform.Web.FunctionalTests.csproj
```

Use Docker-backed AppHost validation only for the distributed commands. The local AppHost command is `dotnet run --project src/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.csproj`, documented at `docs/wiki/local-development.md:L97-L99`.

## Remaining Questions

* Should automatic capture use configured trading-schedule time-zone day or UTC date? Current IG login snapshots use `DateOnly.FromDateTime(capturedAtUtc.UtcDateTime)`, so schedule-zone behavior would be a deliberate new semantic.
* Must manual refresh retain authenticated operator identity for audit?
* Is keyset history navigation required in the first delivery, or should retained history initially support newest-only display?
* When another replica owns the refresh lease, should manual refresh return `409 Conflict`, `202 Accepted`, or a latest snapshot with an in-progress marker?
