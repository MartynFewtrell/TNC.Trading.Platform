---
title: Solution and Projects Research
description: First-pass repository research for solution structure, project responsibilities, and likely entry points
author: GitHub Copilot
ms.date: 2026-06-26
ms.topic: overview
keywords:
  - solution structure
  - project responsibilities
  - architecture
  - entry points
estimated_reading_time: 6
---

## Research Scope

* Inspect the solution and source/test folders to identify runtime and test projects.
* Determine likely architectural layering and responsibilities of Api, AppHost, Application, Infrastructure, ServiceDefaults, and Web.
* Identify major technology and framework cues from project files, runtime entry points, and nearby docs.
* Capture exact workspace-relative file references with line numbers for the strongest evidence.
* Recommend strong entry points for future implementation or debugging work.

## Status

Complete.

## Findings

Complete.

* AppHost is the local distributed-application composition root rather than a business-logic host. Its project SDK is `Aspire.AppHost.Sdk`, it references only Api and Web, and its top-level startup delegates to infrastructure registration, project registration, and authentication wiring before running the distributed app. Evidence: src/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.csproj:1-17; src/TNC.Trading.Platform.AppHost/AppHost.cs:3-14.

* The solution contains six main runtime projects under `src/` and eight test projects under `test/`, with the solution file explicitly listing each project. Evidence: `TNC.Trading.Platform.slnx:1-20`.
* `AppHost` is the distributed composition root rather than a business-logic host. It builds infrastructure, registers API and Web projects, then wires environment and authentication settings before running. Evidence: `src/TNC.Trading.Platform.AppHost/AppHost.cs:1-11`, `src/TNC.Trading.Platform.AppHost/AppHostInfrastructureRegistration.cs:5-31`, `src/TNC.Trading.Platform.AppHost/AppHostProjectRegistration.cs:5-39`.
* `Api` is a Minimal API backend that depends on `Application`, `Infrastructure`, and `ServiceDefaults`, and its startup path stays thin by delegating most behavior into registered services and endpoint handlers. Evidence: `src/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.csproj:1-22`, `src/TNC.Trading.Platform.Api/Program.cs:9-17`, `src/TNC.Trading.Platform.Api/Program.cs:21-48`, `src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs:22-48`.
* `Web` is a Blazor Server operator UI. Its project references `Application` and `ServiceDefaults`, its UI registration adds Radzen components, and its runtime path uses Razor components plus authenticated HTTP clients that call the API through service discovery. Evidence: `src/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.csproj:1-18`, `src/TNC.Trading.Platform.Web/Program.cs:7-18`, `src/TNC.Trading.Platform.Web/Program.cs:20-75`, `src/TNC.Trading.Platform.Web/PlatformWebUiServiceCollectionExtensions.cs:5-16`, `src/TNC.Trading.Platform.Web/PlatformApiClient.cs:9-113`.
* `Application` is the core orchestration layer. It registers feature handlers, a trading-schedule gate, the central `PlatformStateCoordinator`, and the `PlatformAuthSupervisor` background loop, which strongly suggests this is the main implementation entry point for platform runtime behavior. Evidence: `src/TNC.Trading.Platform.Application/Services/PlatformApplicationServiceCollectionExtensions.cs:9-25`, `src/TNC.Trading.Platform.Application/Services/PlatformStateCoordinator.cs:10-24`, `docs/wiki/architecture.md:100-123`.
* `Infrastructure` owns persistence and external integrations. It configures EF Core with SQL Server or restricted in-memory mode, registers the `PlatformDbContext`, protected credential storage, platform stores, notification providers, retention processing, and the `IgSessionClient` HTTP adapter to the IG Demo REST API. Evidence: `src/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.csproj:1-20`, `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/PlatformInfrastructureServiceCollectionExtensions.cs:13-58`, `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/PlatformDbContext.cs:5-21`, `docs/wiki/architecture.md:126-146`.
* `ServiceDefaults` is the shared cross-cutting host layer for health checks, OpenTelemetry, resilience, and service discovery. Both API and Web call `AddServiceDefaults()`, and the implementation wires default health endpoints, OpenTelemetry instrumentation, and HTTP client resilience. Evidence: `src/TNC.Trading.Platform.Api/Program.cs:11`, `src/TNC.Trading.Platform.Web/Program.cs:8`, `src/TNC.Trading.Platform.ServiceDefaults/Extensions.cs:16-45`, `src/TNC.Trading.Platform.ServiceDefaults/Extensions.cs:53-83`, `src/TNC.Trading.Platform.ServiceDefaults/Extensions.cs:109-146`.
* The security boundary is shared across API and Web via `Application` authentication helpers, not duplicated per host. Shared role policies and provider resolution live in `Application`, while Web applies cookie and OpenID Connect wiring on top. Evidence: `src/TNC.Trading.Platform.Application/Authentication/PlatformAuthorizationPolicyRegistration.cs:8-28`, `src/TNC.Trading.Platform.Application/Authentication/PlatformAuthenticationConfigurationResolver.cs:7-51`, `src/TNC.Trading.Platform.Web/Authentication/PlatformWebAuthenticationServiceCollectionExtensions.cs:10-46`.
* The strongest framework cues are .NET 10, .NET Aspire, Blazor Server, Minimal APIs, EF Core SQL Server/InMemory, OpenTelemetry, Keycloak, Radzen, Scalar/OpenAPI, and Playwright. Evidence: `global.json`, `src/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.csproj:1-18`, `src/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.csproj:8-15`, `src/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.csproj:8-11`, `src/TNC.Trading.Platform.ServiceDefaults/TNC.Trading.Platform.ServiceDefaults.csproj:9-17`, `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/TNC.Trading.Platform.Web.E2ETests.csproj:8-13`.
* The test strategy is layered by slice: unit tests for API, Application, Infrastructure, AppHost, and Web; AppHost-backed integration tests for the API; AppHost-backed functional tests for the Web; and Playwright browser-level end-to-end tests. Evidence: `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/TNC.Trading.Platform.Api.UnitTests.csproj:1-20`, `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/TNC.Trading.Platform.Api.IntegrationTests.csproj:1-23`, `test/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.UnitTests/TNC.Trading.Platform.AppHost.UnitTests.csproj:1-30`, `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/TNC.Trading.Platform.Web.FunctionalTests.csproj:1-22`, `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/TNC.Trading.Platform.Web.E2ETests.csproj:1-19`, `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj:1-24`.

* Api is a Minimal API control-plane backend. Its project file references Application, Infrastructure, and ServiceDefaults, and its startup registers OpenAPI, shared service defaults, API authentication, Data Protection, application services, infrastructure services, and an update validator. During startup it ensures the database exists, applies startup configuration and retention processing, runs an initial coordinator tick, and then maps platform endpoints. Evidence: src/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.csproj:3-19; src/TNC.Trading.Platform.Api/Program.cs:12-19; src/TNC.Trading.Platform.Api/Program.cs:25-48; src/TNC.Trading.Platform.Api/Program.cs:84-97.

* The API surface is grouped under `/api/platform` and the endpoint layer is intentionally thin. Endpoint handlers mostly validate or adapt requests, then delegate to application handlers, with role-based authorization split across Viewer, Operator, and Administrator policies. Evidence: src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs:24-48; docs/wiki/architecture.md:99-114.

* Web is a Blazor Server style operator UI with delegated API access. Its startup adds shared service defaults, platform web authentication, platform UI services, Razor interactive server components, and typed `HttpClient` registrations that target the logical Aspire API service name `https+http://api`. The root request pipeline redirects to sign-in unless the operator has a usable access token. Evidence: src/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.csproj:3-14; src/TNC.Trading.Platform.Web/Program.cs:9-21; src/TNC.Trading.Platform.Web/Program.cs:29-75; docs/wiki/architecture.md:73-78.

* `PlatformApiClient` is the clearest Web-to-Api seam for operator workflows. It wraps authenticated calls for status, configuration, manual retry, event history, admin auth details, and IG login history, so it is a strong debugging entry point for UI/API contract issues. Evidence: src/TNC.Trading.Platform.Web/PlatformApiClient.cs:12-18; src/TNC.Trading.Platform.Web/PlatformApiClient.cs:24-32; src/TNC.Trading.Platform.Web/PlatformApiClient.cs:52-60; src/TNC.Trading.Platform.Web/PlatformApiClient.cs:81-101.

* Application is the orchestration layer. Its project file carries only the shared ASP.NET framework reference, and its DI registration adds feature handlers plus `TradingScheduleGate`, `PlatformStateCoordinator`, and the hosted `PlatformAuthSupervisor`. `PlatformStateCoordinator` depends on runtime-state stores, snapshot stores, notification dispatch, trading schedule evaluation, the IG session client abstraction, protected credential access, and proof-data storage, which makes it the central runtime decision maker. Evidence: src/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.csproj:1-9; src/TNC.Trading.Platform.Application/Services/PlatformApplicationServiceCollectionExtensions.cs:11-24; src/TNC.Trading.Platform.Application/Services/PlatformStateCoordinator.cs:10-23; docs/wiki/architecture.md:118-142.

* Infrastructure owns persistence and external integration concerns. It references Application, configures `PlatformDbContext`, supports SQL Server or test-only in-memory persistence, registers credential protection, EF-backed stores, notification providers, retention processing, and the IG HTTP adapter at `https://demo-api.ig.com/gateway/deal/`. `PlatformDbContext` persists configuration, protected credentials, runtime state, retry cycles, IG login snapshots, operational events, configuration audits, and notification records. Evidence: src/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.csproj:9-17; src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/PlatformInfrastructureServiceCollectionExtensions.cs:15-65; src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/PlatformDbContext.cs:5-99; docs/wiki/architecture.md:146-170.
* `global.json` likely pins the SDK version and is a useful technology cue, but it was not read during this pass, so the exact SDK pin is still unverified in the evidence set.
* The `Web` project clearly behaves as Blazor Server from `Program.cs`, but a deeper UI-component pass would be needed to map page-level domains beyond status, configuration, authentication administration, and IG login history.
* The API feature folders and application handler folders were sampled only at the platform entrypoint level. A second pass would be needed to produce a feature-by-feature endpoint and handler inventory.

* The visible test technology stack is deliberate and layered. Unit test projects use xUnit and the .NET test SDK. Api integration, AppHost unit, Web functional, and Web end-to-end tests all reference `Aspire.Hosting.Testing`, which suggests AppHost-backed black-box runtime testing. Web functional and end-to-end tests additionally use Playwright, while Web unit tests use bUnit. Evidence: test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/TNC.Trading.Platform.Api.UnitTests.csproj:8-19; test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/TNC.Trading.Platform.Api.IntegrationTests.csproj:8-23; test/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.UnitTests/TNC.Trading.Platform.AppHost.UnitTests.csproj:8-31; test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/TNC.Trading.Platform.Web.FunctionalTests.csproj:8-23; test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/TNC.Trading.Platform.Web.E2ETests.csproj:8-22; test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj:10-25.

## Recommended Entry Points

* For local startup and environment issues, start at src/TNC.Trading.Platform.AppHost/AppHost.cs:3-14, then follow into src/TNC.Trading.Platform.AppHost/AppHostInfrastructureRegistration.cs:9-36 and src/TNC.Trading.Platform.AppHost/AppHostProjectRegistration.cs:13-43.

* For backend endpoint, auth, or startup-sequence issues, start at src/TNC.Trading.Platform.Api/Program.cs:12-19 and src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs:24-48.

* For business behavior, retry state, trading-schedule decisions, or IG authentication flow, start at src/TNC.Trading.Platform.Application/Services/PlatformStateCoordinator.cs:10-23 and continue through the feature handlers registered in src/TNC.Trading.Platform.Application/Services/PlatformApplicationServiceCollectionExtensions.cs:13-22.

* For persistence, secrets, or external-service integration issues, start at src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/PlatformInfrastructureServiceCollectionExtensions.cs:17-63 and src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/PlatformDbContext.cs:5-99.

* For UI behavior or API contract mismatches, start at src/TNC.Trading.Platform.Web/Program.cs:9-21 and src/TNC.Trading.Platform.Web/PlatformApiClient.cs:12-109, then move to the relevant page component under src/TNC.Trading.Platform.Web/Components/Pages/.

## Open Questions

* The strongest solution-level architecture evidence is consistent across the root README and the wiki architecture document, but some responsibility details are still documentation-backed rather than verified from every concrete implementation file. The main remaining example is finer-grained behavior inside individual feature handlers and page components.

* `TNC.Trading.Platform.Application` intentionally has no package references beyond the shared framework reference, which strongly suggests a pure application-layer library. I did not inspect every folder in that project, so there may be additional subdomains or feature slices not visible from the service-registration surface alone.

## Next Research

* Inspect the specific API feature folders and corresponding Application handlers if the next task targets one endpoint or workflow.

* Inspect Web page components under src/TNC.Trading.Platform.Web/Components/Pages/ when the next task involves operator navigation, rendering, or interaction behavior.

* Inspect AppHost authentication wiring files if the next task involves Keycloak, Entra, or environment-specific auth configuration.

* Inspect Infrastructure notification providers and IG adapter files if the next task involves outbound integrations or operational alerts.
