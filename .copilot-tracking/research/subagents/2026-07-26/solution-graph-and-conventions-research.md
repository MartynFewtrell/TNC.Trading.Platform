---
title: Solution Graph and Architecture Conventions Research
description: Evidence-based analysis of the production project graph, project responsibilities, and compliance with repository architecture guidance
ms.date: 2026-07-26
ms.topic: reference
---

## Research scope

* Determine the exact current production project-reference and dependency graph
* Identify each production project's architectural responsibility from project and composition evidence
* Compare source dependency direction with the current Clean Architecture, vertical slice, Aspire, authentication, configuration, API, and UI guidance
* Record architecturally relevant SDK, framework, and package details
* Cite workspace-relative files with exact one-based line ranges

Status: Complete.

## Governing conventions

The governing architecture rule treats roles as conceptual responsibilities,
not mandatory project names. Policy must remain framework-independent; use
cases may depend on policy and inner-owned ports; outbound adapters implement
those ports; and concrete adapter selection belongs in composition roots.
Source references must point inward even when runtime calls travel outward.
Evidence: `.github/instructions/architecture.instructions.md:22-55`.

The repository also requires feature-first organization, one request and one
response per operation, thin endpoints, transport mapping at adapter
boundaries, and direct tests of handlers and validators. Evidence:
`.github/instructions/architecture.instructions.md:61-89`.

Startup-specific conventions require `Program.cs` to remain focused on
orchestration and endpoint mappings to live in dedicated registration
extensions. Aspire must act as the local composition root, while shared service
defaults centralize health, telemetry, and resilience. Evidence:
`.github/copilot-instructions.md:41-42` and
`.github/instructions/aspire.instructions.md:20-38`.

## Solution and project inventory

The solution contains six production projects under `src/` and nine test
projects under `test/`. The six production entries are API, AppHost,
Application, Infrastructure, ServiceDefaults, and Web. Evidence:
`TNC.Trading.Platform.slnx:2-9`. The test solution folders and project entries
are declared at `TNC.Trading.Platform.slnx:10-34`.

## Production dependency graph

The direct compile-time project-reference edges are:

```text
AppHost -> Api
AppHost -> Web
Api -> Application
Api -> Infrastructure
Api -> ServiceDefaults
Infrastructure -> Application
Web -> Application
Web -> ServiceDefaults
Application -> none
ServiceDefaults -> none
```

Evidence: `src/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.csproj:3-8`,
`src/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.csproj:3-7`,
`src/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.csproj:13-15`,
and `src/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.csproj:3-6`.
Application and ServiceDefaults declare no project references. Evidence:
`src/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.csproj:1-13`
and
`src/TNC.Trading.Platform.ServiceDefaults/TNC.Trading.Platform.ServiceDefaults.csproj:1-21`.

The graph is acyclic. At assembly level, the adapter project Infrastructure
points inward to Application, while API and Web are outer hosts. AppHost points
to deployable projects as the local distributed composition root.

The complete test-to-production project-reference edges are:

```text
Architecture.IntegrationTests -> none
Api.IntegrationTests -> AppHost
Api.UnitTests -> Api
AppHost.UnitTests -> AppHost
Application.UnitTests -> Application
Application.UnitTests -> Infrastructure
Infrastructure.UnitTests -> Application
Infrastructure.UnitTests -> Infrastructure
Web.E2ETests -> AppHost
Web.FunctionalTests -> AppHost
Web.UnitTests -> Web
```

Evidence: `test/TNC.Trading.Platform.Architecture/TNC.Trading.Platform.Architecture.IntegrationTests/TNC.Trading.Platform.Architecture.IntegrationTests.csproj:1-21`,
`test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/TNC.Trading.Platform.Api.IntegrationTests.csproj:21-28`,
`test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/TNC.Trading.Platform.Api.UnitTests.csproj:20-23`,
`test/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.UnitTests/TNC.Trading.Platform.AppHost.UnitTests.csproj:24-27`,
`test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/TNC.Trading.Platform.Application.UnitTests.csproj:20-24`,
`test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/TNC.Trading.Platform.Infrastructure.UnitTests.csproj:20-24`,
`test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/TNC.Trading.Platform.Web.E2ETests.csproj:21-24`,
`test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/TNC.Trading.Platform.Web.FunctionalTests.csproj:21-24`,
and
`test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj:25-28`.

The repository architecture test loads the current production graph and passes
it to a validator. The validator checks only missing targets and cycles.
Evidence:
`test/TNC.Trading.Platform.Architecture/TNC.Trading.Platform.Architecture.IntegrationTests/ProjectGraph/RepositoryProjectGraphTests.cs:7-17`
and
`test/TNC.Trading.Platform.Architecture/TNC.Trading.Platform.Architecture.IntegrationTests/ProjectGraph/ProjectGraphValidator.cs:3-10`.
The focused repository graph test passed on 2026-07-26: 1 passed, 0 failed.

## Project responsibilities

### Application

Application is the inward use-case and policy assembly. It owns feature request,
response, and handler contracts, platform configuration and state models,
state-transition behavior, and outbound ports for persistence, notification,
credentials, and the IG session provider. Representative use-case evidence:
`src/TNC.Trading.Platform.Application/Features/GetPlatformStatus/GetPlatformStatusHandler.cs:1-12`
and
`src/TNC.Trading.Platform.Application/Features/UpdatePlatformConfiguration/UpdatePlatformConfigurationHandler.cs:1-15`.
Representative policy evidence:
`src/TNC.Trading.Platform.Application/Services/PlatformStateTransitionEngine.cs:1-56`.
The IG port is owned inward at
`src/TNC.Trading.Platform.Application/Infrastructure/Ig/IIgSessionClient.cs:1-19`.

Application also currently owns shared authentication options, constants,
ASP.NET Core authorization-policy registration, dependency-injection
registration, and a hosted background supervisor. Those are framework and
composition responsibilities rather than pure policy or use cases. Evidence:
`src/TNC.Trading.Platform.Application/Authentication/PlatformAuthenticationOptions.cs:1-52`,
`src/TNC.Trading.Platform.Application/Authentication/PlatformAuthorizationPolicyRegistration.cs:1-32`,
`src/TNC.Trading.Platform.Application/Services/PlatformApplicationServiceCollectionExtensions.cs:1-29`,
and
`src/TNC.Trading.Platform.Application/Services/PlatformAuthSupervisor.cs:1-40`.

### Infrastructure

Infrastructure is the outbound-adapter and external-mechanism assembly. It
implements Application-owned ports with EF Core stores, SQL Server and test
in-memory persistence, protected credentials, HTTP access to IG, and recorded,
SMTP, and Azure Communication Services notification providers. Adapter
selection is visible at
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/PlatformInfrastructureServiceCollectionExtensions.cs:16-69`.
The IG adapter translates HTTP and provider JSON into inward-owned response
models at
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/Ig/IgSessionClient.cs:1-117`.
Persistence entities and EF mappings remain inside Infrastructure at
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/PlatformDbContext.cs:1-119`.

Infrastructure additionally contains configuration restart, notification
dispatch, and retention decisions. Evidence:
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/PlatformConfigurationRestartPolicy.cs:1-13`,
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/NotificationDispatchPolicy.cs:1-25`,
and
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/OperationalRecordRetentionPolicy.cs:1-17`.

### API

API is both the HTTP inbound adapter and the process composition root. It
configures authentication, OpenAPI, Scalar, Application, Infrastructure, and
shared defaults at `src/TNC.Trading.Platform.Api/Program.cs:11-19`. Its endpoint
group binds transport input, applies authorization, dispatches most operations
to Application handlers, and maps inward responses to HTTP responses at
`src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs:26-98`.
Transport-to-Application translation is explicit at
`src/TNC.Trading.Platform.Api/Features/UpdatePlatformConfiguration/UpdatePlatformConfigurationMapping.cs:1-58`.

API also directly controls database schema recreation and invokes concrete
Infrastructure and Application services during startup at
`src/TNC.Trading.Platform.Api/Program.cs:22-50`. One audit operation executes
the workflow directly in an endpoint helper instead of through an Application
request/response/handler at
`src/TNC.Trading.Platform.Api/Features/Platform/RecordAuthAuditEventEndpointHandler.cs:10-54`.

### Web

Web is the Blazor Server inbound/UI adapter and a web-process composition root.
It configures OIDC/cookie authentication, Radzen, interactive Razor components,
and service-discovered HTTP clients at `src/TNC.Trading.Platform.Web/Program.cs:7-23`.
It calls API over HTTP through `PlatformApiClient`, including bearer-token
acquisition and transport model deserialization, rather than referencing
Infrastructure. Evidence: `src/TNC.Trading.Platform.Web/PlatformApiClient.cs:1-119`.
UI presenters and shell services are composed at
`src/TNC.Trading.Platform.Web/PlatformWebUiServiceCollectionExtensions.cs:1-23`.

### AppHost

AppHost is the local distributed composition root. Its entry point delegates
resource creation, project registration, and environment wiring before building
and running the distributed application. Evidence:
`src/TNC.Trading.Platform.AppHost/AppHost.cs:1-13`.
It provisions persistent SQL Server and Mailpit resources plus Keycloak with a
realm import at
`src/TNC.Trading.Platform.AppHost/AppHostInfrastructureRegistration.cs:14-43`.
It registers API and Web, wires API to SQL/Mailpit, wires Web to API, and adds
dashboard links at
`src/TNC.Trading.Platform.AppHost/AppHostProjectRegistration.cs:8-44`.

### ServiceDefaults

ServiceDefaults is a shared framework/driver project for host-wide service
discovery, standard HTTP resilience, OpenTelemetry, readiness, and liveness.
Evidence: `src/TNC.Trading.Platform.ServiceDefaults/Extensions.cs:15-51`,
`src/TNC.Trading.Platform.ServiceDefaults/Extensions.cs:56-91`, and
`src/TNC.Trading.Platform.ServiceDefaults/Extensions.cs:128-166`.

## Architecture conformance assessment

### Conforming structure

The production project graph follows the required inward
dependency direction and contains no direct inward-to-outward project edge.
This graph alone cannot prove source-level conformance, as the architecture
guidance itself states. Evidence:
`.github/instructions/architecture.instructions.md:125-131`.

Dependency inversion is present for persistence, credentials, notifications,
and IG integration: Application owns the ports, Infrastructure implements them,
and API composes them. The `IIgSessionClient` and `IgSessionClient` pair is a
representative example. This follows the required form `use case -> inner-owned
port <- outbound adapter`.

API feature mappings keep transport request and response models separate from
Application feature contracts. Most endpoint methods bind or construct one
Application request, invoke one handler, and map the response. AppHost remains
focused on resource and environment composition. Web reaches API over HTTP and
does not reference the concrete Infrastructure project.

### Clear violations

Application is not framework-independent. It has a `Microsoft.AspNetCore.App`
framework reference at
`src/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.csproj:9-11`.
Its authorization registration takes ASP.NET Core `AuthorizationOptions`, its
supervisor derives from `BackgroundService`, and its core coordinator accepts
`IConfiguration` and `ILogger`. Evidence:
`src/TNC.Trading.Platform.Application/Authentication/PlatformAuthorizationPolicyRegistration.cs:1-32`,
`src/TNC.Trading.Platform.Application/Services/PlatformAuthSupervisor.cs:1-40`,
and
`src/TNC.Trading.Platform.Application/Services/PlatformStateCoordinator.cs:1-25`.
This violates the requirement that policy and use-case code remain independent
of framework and hosting details at
`.github/instructions/architecture.instructions.md:45-55`.

The authentication-audit operation bypasses the required Application feature
shape. API maps the operation at
`src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs:41-45`,
then injects `PlatformConfigurationService` and `IPlatformEventStore` directly
at `src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs:100-121`.
The endpoint helper resolves the event, loads configuration, constructs an
Application event record, and writes the store at
`src/TNC.Trading.Platform.Api/Features/Platform/RecordAuthAuditEventEndpointHandler.cs:10-54`.
No `RecordAuthAuditEvent` Application request, response, and handler exist.
This violates the one-operation handler shape and the prohibition on business
or persistence orchestration in endpoints at
`.github/instructions/architecture.instructions.md:65-86` and
`.github/instructions/architecture.instructions.md:108-116`.

The API validator makes the business decision that Test platform plus Live
broker is unavailable at
`src/TNC.Trading.Platform.Api/Features/UpdatePlatformConfiguration/UpdatePlatformConfigurationValidator.cs:79-83`.
The guidance limits validators to input validity and explicitly excludes
business decisions at `.github/instructions/architecture.instructions.md:115-116`.
The same rule is correctly represented as policy in the Application transition
engine for runtime behavior at
`src/TNC.Trading.Platform.Application/Services/PlatformStateTransitionEngine.cs:17-21`.

Business policy also resides in Infrastructure. The clearest case is deciding
that platform or broker environment changes require restart at
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/PlatformConfigurationRestartPolicy.cs:6-12`.
Notification recipient/provider selection and operational-retention defaults
are likewise made in the outbound-adapter assembly at
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/NotificationDispatchPolicy.cs:6-24`
and
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/OperationalRecordRetentionPolicy.cs:6-16`.
These conflict with the rule that business decisions belong in policy or use
cases, not outbound adapters, at
`.github/instructions/architecture.instructions.md:53-55`.

### Boundary risks and partial conformance

API's project reference to Infrastructure is permitted for composition, but
`Program.cs` reaches into `PlatformDbContext`, chooses a provider-specific
development deletion policy, recreates the schema, applies configuration,
runs retention, and ticks the state coordinator at
`src/TNC.Trading.Platform.Api/Program.cs:22-50`. Registration is a valid
composition-root concern; the provider-specific schema lifecycle decision and
operational workflow make the entry point broader than the repository rule to
keep `Program.cs` focused on startup orchestration at
`.github/copilot-instructions.md:41-42`.

Application grants internals visibility to API and Infrastructure at
`src/TNC.Trading.Platform.Application/Properties/InternalsVisibleTo.cs:1-6`.
Infrastructure grants internals visibility back to API at
`src/TNC.Trading.Platform.Infrastructure/Properties/InternalsVisibleTo.cs:1-5`.
These friend-assembly links do not reverse project references, but they weaken
assembly encapsulation and allow API to construct internal use-case types and
operate concrete persistence types directly.

Application unit tests reference Infrastructure and build concrete EF stores,
credential services, and notification dispatchers. The project edge is at
`test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/TNC.Trading.Platform.Application.UnitTests.csproj:20-24`;
representative concrete usage is at
`test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/AuthRetryCycleTests.cs:5-39`.
These are integration-style tests inside the Application unit-test project and
do not satisfy the guidance to test policy and use cases independently of
databases and external mechanisms whenever possible.

The current architecture test intentionally does not encode project names,
counts, or layer roles at
`test/TNC.Trading.Platform.Architecture/TNC.Trading.Platform.Architecture.IntegrationTests/ProjectGraph/RepositoryProjectGraphTests.cs:7-10`.
Its passing result therefore proves graph integrity, not Clean Architecture
responsibility placement or boundary translation.

## Architecturally relevant platform details

The repository pins SDK `10.0.200` with `latestFeature` roll-forward at
`global.json:1-6`. Every production and test project targets `net10.0`.

API and Web use `Microsoft.NET.Sdk.Web`; Application, Infrastructure, and
ServiceDefaults use `Microsoft.NET.Sdk`; AppHost uses
`Aspire.AppHost.Sdk/13.2.3`. Evidence:
`src/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.csproj:1-24`,
`src/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.csproj:1-18`,
`src/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.csproj:1-13`,
`src/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.csproj:1-24`,
`src/TNC.Trading.Platform.ServiceDefaults/TNC.Trading.Platform.ServiceDefaults.csproj:1-21`,
and
`src/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.csproj:1-22`.

Infrastructure declares EF Core SQL Server and in-memory providers, Azure
Identity, and Azure Communication Services Email. API duplicates those Azure
and EF provider package references while also declaring JWT bearer, OpenAPI,
and Scalar. Evidence:
`src/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.csproj:17-22`
and
`src/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.csproj:9-17`.
The API's direct EF dependency is exercised by provider-specific startup code;
the Azure package responsibilities are implemented in Infrastructure.

Web declares OIDC and Radzen at
`src/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.csproj:8-11`.
ServiceDefaults carries service discovery, HTTP resilience, OpenAPI, and
OpenTelemetry dependencies at
`src/TNC.Trading.Platform.ServiceDefaults/TNC.Trading.Platform.ServiceDefaults.csproj:10-20`.
AppHost declares SQL Server and a preview Keycloak integration at
`src/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.csproj:3-7`.
The preview Keycloak package is architecturally relevant because the scoped
Aspire guidance requires checking current Aspire documentation before adopting
preview APIs.

Package versions are managed independently in each project because no
`Directory.Packages.props` or `Directory.Build.props` is present. One current
version skew is architecturally visible: AppHost and most Aspire test projects
use Aspire `13.2.3`, while API integration tests use
`Aspire.Hosting.Testing` `13.1.2` at
`test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/TNC.Trading.Platform.Api.IntegrationTests.csproj:9-15`.

## Evidence and references

Primary repository evidence inspected:

* `.github/copilot-instructions.md:17-63`
* `.github/instructions/architecture.instructions.md:22-131`
* `.github/instructions/aspire.instructions.md:18-64`
* `.github/instructions/auth.instructions.md:18-75`
* `.github/instructions/configuration.instructions.md:18-52`
* `.github/instructions/dotnet-stack.instructions.md:18-83`
* `.github/instructions/scalar.instructions.md:18-66`
* `TNC.Trading.Platform.slnx:1-35`
* `global.json:1-6`
* All six production project files under `src/`
* All nine solution-listed test project files under `test/`
* Representative startup, composition, feature, port, adapter, persistence,
	policy, UI client, and architecture-test files cited above

## Unresolved gaps

* No repository rule formally assigns every conceptual role to a named assembly.
	The findings classify responsibilities from source behavior, as the guidance
	requires, rather than assuming names define layers.
* The review did not exhaustively classify every source file. It followed all
	project edges and inspected representative owning abstractions for each
	project, plus targeted searches for cross-project and framework dependencies.
* No static namespace/type dependency test currently proves that Application
	excludes ASP.NET Core, hosting, persistence, UI, or provider representations.
* The focused graph test passed, but a full solution build and full test suite
	were outside this research-only validation scope.

## Recommended next research

* Define the intended assembly-level boundary policy, especially whether shared
	authentication host configuration should remain in Application or move to an
	outer shared host assembly
* Inventory every Application dependency on ASP.NET Core and
	`Microsoft.Extensions.*`, then separate pure policy, use-case orchestration,
	hosting, and cross-cutting observability concerns
* Design a `RecordAuthAuditEvent` Application feature and determine the minimal
	inward-owned audit input that avoids passing `ClaimsPrincipal` or
	`HttpContext` inward
* Classify restart, notification, and retention decisions one by one to decide
	which belong in Application policy and which are genuinely adapter mechanics
* Evaluate replacing friend-assembly access with deliberate public operation
	contracts or a narrow composition surface
* Add architecture tests for forbidden production dependencies and framework
	types after the intended boundaries are explicitly agreed
* Split Application unit tests that use EF and concrete Infrastructure into an
	integration-test project, retaining direct handler and policy tests with
	inward-owned fakes
* Reconcile Aspire package versions and verify the preview Keycloak integration
	against current Aspire documentation
