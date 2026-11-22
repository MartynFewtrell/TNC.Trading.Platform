---
title: Current Environment Implementation Research
description: Evidence-based inventory of platform runtime and broker environment implementation paths
ms.date: 2026-09-20
ms.topic: reference
---

## Status

Complete.

## Research Questions

* How the AppHost composes platform runtime and broker-related resources and configuration
* How ASP.NET selects configuration and deployment environments
* How application, domain, and infrastructure persistence schemas and migrations represent environment state
* Which API endpoints expose or mutate environment-related settings
* How the Blazor layout, header, and selection behavior represent environments
* Which automated tests cover these behaviors and which gaps remain

## Applicable Instructions

* `.github/instructions/folders.instructions.md` applies to all artifacts and requires product code under `src/` and tests under `test/`.
* `.github/instructions/aspire.instructions.md` applies to AppHost and ServiceDefaults artifacts. It requires the AppHost to own local dependency composition, externalized configuration, stable resource names, and health checks.
* `.github/instructions/auth.instructions.md` applies to AppHost, C#, project, and JSON files. It requires Aspire-managed Keycloak locally, Entra ID in Azure, and environment-driven provider selection.
* `.github/instructions/configuration.instructions.md` applies to C#, JSON, Razor, and infrastructure. It requires SQL Server for operator-managed configuration and write-only handling for persisted secrets.
* `.github/instructions/architecture.instructions.md` applies to C# production and test files. It assigns persistence adapter selection to composition and keeps endpoint logic thin.
* `.github/instructions/radzen-blazor.instructions.md` applies to Razor, C#, and CSS UI artifacts. It prefers Radzen controls and requires interaction validation.
* `.github/instructions/tests.instructions.md`, `.github/instructions/aspire-tests.instructions.md`, and `.github/instructions/playwright.instructions.md` apply to test artifacts. They require xUnit, isolated boundaries, Aspire-managed closed-box tests where applicable, and resilient browser locators.
* `.github/instructions/csharp.instructions.md` applies to every C# file and requires null-safe, intentional error handling with behavior-appropriate tests.
* `.github/instructions/dotnet-stack.instructions.md` applies to the runtime C#, Razor, JSON, and infrastructure artifacts. It requires Minimal APIs, Blazor, SQL Server migrations, Aspire local composition, and container-ready external configuration.

## Findings

### Environment Model and Runtime Control

* `PlatformEnvironmentKind` defines `Test` and `Live` in `src/TNC.Trading.Platform.Application/Configuration/PlatformEnvironmentKind.cs` lines 3-7. `BrokerEnvironmentKind` independently defines `Demo` and `Live` in `src/TNC.Trading.Platform.Application/Configuration/BrokerEnvironmentKind.cs` lines 3-7.
* The API bootstrap configuration defaults to `Test` plus `Demo` in `src/TNC.Trading.Platform.Api/appsettings.json` lines 45-48. The SQL bootstrap parser requires `Bootstrap:BrokerEnvironment`, defaults a missing platform value to `Test`, and parses both enum values in `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Configuration/SqlServer/PlatformConfigurationBootstrapParser.cs` lines 8-24.
* `SqlPlatformConfigurationStore` is the runtime owner: it seeds one SQL configuration row when absent, returns it through `GetCurrentAsync`, writes both environment values in `CommitAsync`, records an audit, and maps the snapshot's `LiveOptionAvailable` to `platformEnvironment != Test`. See `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Configuration/SqlServer/SqlPlatformConfigurationStore.cs` lines 17-109 and 111-190.
* `UpdatePlatformConfigurationHandler` validates, commits, then runs authentication reconciliation in `src/TNC.Trading.Platform.Application/Features/UpdatePlatformConfiguration/UpdatePlatformConfigurationHandler.cs` lines 5-18. The validator rejects a `Test` platform with `Live` broker selection in `src/TNC.Trading.Platform.Application/Features/UpdatePlatformConfiguration/UpdatePlatformConfigurationValidator.cs` lines 56-70.

### AppHost and Hosting Configuration

* The AppHost composition root creates infrastructure, registers API and Web projects, wires runtime settings, then runs in `src/TNC.Trading.Platform.AppHost/AppHost.cs` lines 3-13.
* `AppHostInfrastructureRegistration` creates stable `sql`, `platformdb`, `mailpit`, and `keycloak` resources. SQL and Keycloak can retain container state according to `AppHost:*Persistent*State`; Keycloak imports `./Realms`. See `src/TNC.Trading.Platform.AppHost/AppHostInfrastructureRegistration.cs` lines 5-58.
* The API waits for SQL and Mailpit and references `platformdb`; Web references API and `platformdb`, waits for API, and exposes external HTTP endpoints. See `src/TNC.Trading.Platform.AppHost/AppHostProjectRegistration.cs` lines 7-49.
* `AppHostEnvironmentWiring` injects auth, SMTP, ACS, account-preference, and Keycloak authority settings. It does not inject `Bootstrap:PlatformEnvironment` or `Bootstrap:BrokerEnvironment`; those remain API configuration inputs. See `src/TNC.Trading.Platform.AppHost/AppHostEnvironmentWiring.cs` lines 5-202.
* API startup calls `InitializePlatformAsync`, performs a first reconciliation, configures auth, maps endpoints, and only exposes OpenAPI/Scalar when the ASP.NET deployment environment is Development. See `src/TNC.Trading.Platform.Api/Program.cs` lines 11-67. Web startup applies exception handling and HSTS only outside Development, performs OIDC session routing, and maps interactive server components in `src/TNC.Trading.Platform.Web/Program.cs` lines 7-52.
* API and Web start with `WebApplication.CreateBuilder(args)` and do not add explicit configuration providers. They therefore use the standard ASP.NET host configuration pipeline. Their local profiles set `ASPNETCORE_ENVIRONMENT=Development` in `src/TNC.Trading.Platform.Api/Properties/launchSettings.json` lines 3-25 and `src/TNC.Trading.Platform.Web/Properties/launchSettings.json` lines 3-25. AppHost sets both `ASPNETCORE_ENVIRONMENT` and `DOTNET_ENVIRONMENT` to Development in `src/TNC.Trading.Platform.AppHost/Properties/launchSettings.json` lines 3-37.
* Only API has an `appsettings.Development.json` file, and it contains logging only. AppHost's base and development appsettings files also contain logging and DCP tuning only. See `src/TNC.Trading.Platform.Api/appsettings.Development.json` lines 1-8, `src/TNC.Trading.Platform.AppHost/appsettings.json` lines 1-13, and `src/TNC.Trading.Platform.AppHost/appsettings.Development.json` lines 1-8. No Azure-specific appsettings file was found.

### Persistence and Schema

* Infrastructure chooses SQL Server from the `platformdb` connection string; its only non-SQL path is explicitly guarded in-memory persistence for isolated tests with `Authentication:Provider=Test`. See `src/TNC.Trading.Platform.Infrastructure/Infrastructure/DependencyInjection/PlatformInfrastructureServiceCollectionExtensions.cs` lines 32-59.
* `PlatformStartupInitializer` applies EF migrations for SQL Server, otherwise calls `EnsureCreatedAsync`; it establishes a guarded legacy baseline only for a complete prior schema and then applies migrations. See `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Startup/PlatformStartupInitializer.cs` lines 15-105.
* `PlatformDbContext` persists the configuration row, protected credentials keyed by broker environment, runtime/auth records keyed by both environments, and account-preference state/audit/operations keyed by the environment pair. See `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/PlatformDbContext.cs` lines 7-27, 37-119, and 147-199. The initial schema migration creates the core configuration and environment columns in `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/Migrations/20260727202238_InitialPlatformSchema.cs` lines 12-116; later migrations add account preferences and operations.

### Broker Gateway Behavior

* `IgBrokerAuthenticationGateway` exits before sending HTTP when given `Live`, returning an `UnsupportedEnvironment` failure. For other values, its client base address is the IG demo gateway. See `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgBrokerAuthenticationGateway.cs` lines 17-31 and `src/TNC.Trading.Platform.Infrastructure/Infrastructure/DependencyInjection/PlatformInfrastructureServiceCollectionExtensions.cs` lines 95-98.
* `IgAccountDetailsGateway` always gets Demo credentials, and its registered HTTP client is fixed to the demo gateway. See `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgAccountDetailsGateway.cs` lines 17-32 and `src/TNC.Trading.Platform.Infrastructure/Infrastructure/DependencyInjection/PlatformInfrastructureServiceCollectionExtensions.cs` lines 87-94.
* `IgAccountPreferencesGateway` always gets Demo credentials for reads, observations, and writes. See `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgAccountPreferencesGateway.cs` lines 14-87. Its base URL is configurable but its credentials remain Demo-selected.

### API and UI Surfaces

* `GET /api/platform/configuration` and operator-authorized `PUT /api/platform/configuration` are the environment read/write API surface. `GET /status` exposes current runtime values to viewers. Endpoint mapping and authorization are in `src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs` lines 28-67; request validation and dispatch are in `src/TNC.Trading.Platform.Api/Features/Platform/UpdatePlatformConfigurationEndpointHandler.cs` lines 8-29.
* The Blazor configuration page presents Test/Live platform selection and Demo/Live broker selection; the Live broker option is disabled while the bound platform value is Test. It only persists on Save through `ConfigurationPagePresenter`. See `src/TNC.Trading.Platform.Web/Components/Pages/Configuration/Configuration.razor` lines 40-62 and 176-219, and `src/TNC.Trading.Platform.Web/Components/Pages/Configuration/ConfigurationPagePresenter.cs` lines 5-44.
* The shared layout loads the environment only after first render for users with a platform role and passes it to the header. It retries status loading once per second until successful or disposed. See `src/TNC.Trading.Platform.Web/Components/Layout/MainLayout.razor` lines 4-61 and 63-119.
* `PlatformShellContextProvider` reads status once per scoped provider and converts it to `PlatformShellEnvironment`; `PlatformHeader` passes that value to a display-only badge. No selector or callback exists in the header/badge. See `src/TNC.Trading.Platform.Web/Components/Layout/PlatformShellContextProvider.cs` lines 8-54, `src/TNC.Trading.Platform.Web/Components/Layout/PlatformHeader.razor` lines 3-68, and `src/TNC.Trading.Platform.Web/Components/Layout/PlatformEnvironmentBadge.razor` lines 1-18.

### Existing Test Coverage

* AppHost unit coverage validates resource registration, default persistent SQL state, and session-scoped state when persistence is disabled in `test/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.UnitTests/AppHostInfrastructureRegistrationTests.cs` lines 13-112. It validates injected auth, mail, ACS, and Keycloak project configuration in `test/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.UnitTests/AppHostEnvironmentWiringTests.cs` lines 26-349.
* Configuration persistence tests cover initial Demo bootstrap, retaining persisted configuration despite changed bootstrap values, audit/restart persistence, Live/Demo audit separation, and activating deferred configuration after startup in `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/SqlPlatformConfigurationStoreTests.cs` lines 17-294.
* Gateway unit coverage proves Demo-host authentication and that Live authentication returns `UnsupportedEnvironment` before a network call in `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/Integrations/Ig/IgBrokerAuthenticationGatewayTests.cs` lines 14-104.
* Application/API unit tests cover validation and policy, including the `Test + Live` block, in `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/UpdatePlatformConfigurationValidatorTests.cs` lines 8-29, `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/TradingScheduleGateTests.cs` lines 8-60, and `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/TriggerManualAuthRetry/TriggerManualAuthRetryHandlerTests.cs` lines 28-51.
* Blazor unit tests verify that configuration opens its environment accordion, preserves failed-save edits, and displays restart guidance in `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/ConfigurationTests.cs` lines 11-154. They verify the shared layout renders `Test / Demo` for an authorized user in `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/MainLayoutTests.cs` lines 23-58, and the provider caches/retries status reads in `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/PlatformShellContextProviderTests.cs` lines 17-61.
* Functional and E2E source files contain authentication and account-preferences coverage, but source-only searching found no functional/E2E test that saves configuration, switches platform/broker environment, verifies a header refresh, or verifies outbound IG endpoint/credential selection after a valid environment change.

## Searches Used

* `PlatformEnvironment|BrokerEnvironment|RuntimeEnvironment|EnvironmentSelection|SelectedEnvironment|Broker.*Environment` across `src/**`
* `AddKeycloak|AddSqlServer|AddProject|AddConnectionString|WithReference|WithEnvironment|WithRealmImport|WithDataVolume` in the AppHost source
* `ASPNETCORE_ENVIRONMENT|DOTNET_ENVIRONMENT|IsDevelopment|IsProduction|AddJsonFile|AddEnvironmentVariables|WebApplication.CreateBuilder|Configuration` across runtime source
* `MapGet|MapPost|MapPut|MapDelete|MapGroup` in the API source
* `DbContext|EntityTypeBuilder|HasData|Migration|CreateTable|PlatformEnvironment|BrokerEnvironment` in Infrastructure source
* `Layout|Header|environment|Environment|selected|Selected` in Web source
* `configuration-save-button|configuration-startup-hint|PlatformEnvironment|BrokerEnvironment|/configuration` in Web Functional/E2E source
* `BrokerEnvironmentKind\.Live|PlatformEnvironmentKind\.Live|UnsupportedEnvironment|Test.*Live|Live.*Test` in test source
* `Bootstrap__PlatformEnvironment|Bootstrap:PlatformEnvironment|Bootstrap__BrokerEnvironment|Bootstrap:BrokerEnvironment` across runtime source

## Gaps and Likely Affected Features

* The requested environment-selection capability is not a header action. The header badge is read-only, so any request to select or switch runtime/broker environment from the header would require new UI interaction, state refresh, API usage, and tests. Likely affected features: `Components/Layout`, `PlatformShellContextProvider`, `PlatformApiClient`, and the configuration flow.
* Valid `Live + Live` configuration can be persisted and activated after restart, but broker authentication deliberately rejects Live and account-details/account-preferences gateways always select Demo credentials. The system can display/persist an active Live broker environment without a corresponding Live IG transport implementation. Likely affected features: IG gateway contracts/registration, credential retrieval, configuration validation/policy, reconciliation, account details, and account preferences.
* AppHost does not expose platform or broker bootstrap variables as AppHost settings or project environment values. Runtime defaults originate from API `appsettings.json` only on first SQL initialization; existing persistent SQL configuration takes precedence. Likely affected features: `AppHostSettings`, `AppHostEnvironmentWiring`, AppHost tests, and deployment configuration if operator/bootstrap selection must be explicit.
* No Azure deployment-specific configuration composition was found. The code supports Entra options but local AppHost wiring forces Keycloak. A deployment implementation would need external Entra authority/client configuration and secret injection, plus an environment-specific provider decision. Likely affected features: authentication service extensions, deployment manifests or IaC, API/Web settings, and AppHost-local-only guards.
* There is no high-level test that changes environments through the API/UI and observes restart semantics, status/header refresh, safe blocked behavior for unsupported Live flows, or the eventual Live integration once supplied. Likely affected suites: Infrastructure unit/integration, API integration, Web unit, Web Functional/E2E, and AppHost integration.
* The current header retry loop uses a one-second delay. That is implementation behavior rather than a test readiness mechanism, but a change to environment refresh should define bounded retry/cancellation semantics and include a component-level test.

## Follow-On Questions

* Is Live broker environment intentionally a durable future-state option, or must it be removed/disabled until all IG gateway operations support Live endpoints and credentials?
* Should platform/broker selection remain an operator-only configuration-page action with restart semantics, or is the intended request an immediate header-based runtime switch?
* What Azure deployment artifacts are authoritative for this repository, and how should they inject Entra and SQL configuration without embedding secrets?
* When a configuration update changes environment and reconciliation runs before restart, should status show the existing runtime pair, the pending persisted pair, or both?

## Blockers

* No acceptance criteria or intended behavior for a new environment-selection request were supplied, so gaps are stated against the observed implementation and implied selection capability rather than a formal feature specification.
* No blocking repository access issue was encountered.
