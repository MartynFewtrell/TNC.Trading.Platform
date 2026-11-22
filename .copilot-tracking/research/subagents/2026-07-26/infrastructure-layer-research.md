---
title: Infrastructure Layer Research
description: Evidence-based assessment of Infrastructure ownership, adapters, dependencies, consumption, and tests
author: GitHub Copilot
ms.date: 2026-07-26
ms.topic: reference
---

## Research scope

* Inventory Infrastructure folders, types, dependencies, and ownership boundaries.
* Inspect dependency injection registration, persistence, configuration,
  authentication, IG integration, notifications, and other external adapters.
* Trace how API, Web, and Application consume Infrastructure behavior.
* Identify dependency-direction issues, misplaced abstractions, provider and
  framework coupling, and adapter naming or organization opportunities.
* Evaluate representative call paths and tests against repository architecture,
  configuration, and authentication rules.
* Recommend incremental structural changes without modifying production files.

## Status

Complete.

## Governing criteria

The repository defines Infrastructure as an outbound-adapter and
framework/driver responsibility. A use case that needs an outward service must
own a narrow inward port, and Infrastructure may implement it. Persistence,
provider, identity, SDK, and hosting representations must not cross into use
cases. Concrete adapter access is permitted in a composition root only for
registration, configuration, adapter selection, startup orchestration, and
hosting. Evidence: `.github/instructions/architecture.instructions.md:17-54`.

The same rules require business decisions to remain in policy or use-case code,
boundary data to be inward-owned and simple, and use-case behavior to be tested
without external mechanisms when practical. They explicitly reject mandatory
project proliferation and pass-through abstractions. Evidence:
`.github/instructions/architecture.instructions.md:43-54` and
`.github/instructions/architecture.instructions.md:92-118`.

Configuration must be SQL Server-backed, use explicit application services or
repositories, protect IG credentials at rest and in transit, and return only
credential presence rather than stored values to operator-facing APIs and UI.
Evidence: `.github/instructions/configuration.instructions.md:17-49`.

End-user authentication must use Keycloak locally, Microsoft Entra ID in Azure,
OIDC Authorization Code with PKCE for Web, and OAuth 2.0/JWT validation for API.
HTTPS metadata may be disabled only in Development. Evidence:
`.github/instructions/auth.instructions.md:20-70`.

Tests must keep unit execution in-process and in-memory, while real provider or
database behavior belongs in integration tests. Evidence:
`.github/instructions/tests.instructions.md:30-65`.

## Investigation method

The investigation inventoried all source files in the Infrastructure project,
all 13 files in its unit-test project, source project references, package
references, DI registration, all Application-owned ports implemented by
Infrastructure, concrete Infrastructure references from API, and representative
Web-to-API-to-Application-to-adapter flows. Generated `bin/`, `obj/`, cache,
and artifact files were excluded as source evidence.

The source inventory contains 37 Infrastructure types. The project has one
source-project dependency, on Application, and no source dependency points from
Application or Web to Infrastructure. API is the composition root and directly
references both. Evidence:
`src/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.csproj:1-23`;
`src/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.csproj:1-23`;
`src/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.csproj:1-13`.

Focused validation passed on 2026-07-26: all 44 discovered Infrastructure unit
tests passed, and the repository production-graph test passed. These results
confirm current adapter behavior and graph integrity; they do not disprove the
documented ownership, environment-routing, provider-coupling, or missing-test
findings because the existing suites do not assert those boundaries.

## Inventory

### Project dependencies

Infrastructure targets `net10.0`, references the ASP.NET Core shared framework,
and references Application. Its direct packages are Azure Communication
Services Email, Azure Identity, EF Core InMemory, and EF Core SQL Server.
Evidence:
`src/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.csproj:1-23`.

This graph direction is correct:

```text
Application-owned ports <- Infrastructure adapters <- API composition root
```

Application has no source project or package dependency on Infrastructure.
Infrastructure receives access to internal Application contracts through
`InternalsVisibleTo`; API and both unit-test projects receive access to internal
Infrastructure types. Evidence:
`src/TNC.Trading.Platform.Application/Properties/InternalsVisibleTo.cs:1-6`;
`src/TNC.Trading.Platform.Infrastructure/Properties/InternalsVisibleTo.cs:1-5`.

API repeats Infrastructure's Azure Communication Services, Azure Identity, EF
Core InMemory, and EF Core SQL Server package references. Source inspection
found no Azure SDK use in API and only direct EF startup operations in
`Program.cs`. Evidence:
`src/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.csproj:8-16`;
`src/TNC.Trading.Platform.Api/Program.cs:26-40`.

### Persistence

`Infrastructure/Persistence` contains `PlatformDbContext` and nine internal EF
entities:

* `PlatformConfigurationEntity`
* `ProtectedCredentialEntity`
* `AuthRuntimeStateEntity`
* `AuthRetryCycleEntity`
* `IgLoginSnapshotEntity`
* `OperationalEventEntity`
* `ConfigurationAuditEntity`
* `NotificationRecordEntity`

The context configures keys, unique credential scope, indexes, and field lengths
without exposing entities across the assembly boundary. Evidence:
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/PlatformDbContext.cs:5-119`;
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/PlatformConfigurationEntity.cs:1-3`;
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/ProtectedCredentialEntity.cs:1-3`.

The EF adapters implement Application-owned ports:

* `EfPlatformRuntimeStateStore` implements `IPlatformRuntimeStateStore` and
  maps between `AuthRuntimeStateEntity` and inward `PlatformRuntimeState`.
  Evidence:
  `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/EfPlatformRuntimeStateStore.cs:8-82`.
* `EfPlatformRetryCycleStore` implements `IPlatformRetryCycleStore` and owns EF
  upsert mechanics. Evidence:
  `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/EfPlatformRetryCycleStore.cs:8-51`.
* `EfPlatformEventStore` implements `IPlatformEventStore`, applies query limits,
  maps entities to inward models, and serializes event details. Evidence:
  `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/EfPlatformEventStore.cs:8-58`.
* `EfPlatformIgLoginSnapshotStore` implements
  `IPlatformIgLoginSnapshotStore`, maps entities, and manages latest and daily
  retained snapshots. Evidence:
  `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/EfPlatformIgLoginSnapshotStore.cs:9-104`.
* `SqlPlatformConfigurationStore` implements
  `IPlatformConfigurationStore`, seeds SQL configuration, maps snapshots,
  updates settings, writes audit records, and coordinates credential updates.
  Evidence:
  `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/SqlPlatformConfigurationStore.cs:10-207`.

### Credentials and configuration

`ProtectedCredentialService` implements the Application-owned
`IProtectedCredentialService`. It scopes credentials by broker environment,
stores Data Protection ciphertext, returns only presence metadata on the
configuration read path, and decrypts values only for the runtime broker call.
Evidence:
`src/TNC.Trading.Platform.Application/Configuration/IProtectedCredentialService.cs:1-14`;
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/ProtectedCredentialService.cs:8-88`.

`PlatformConfigurationBootstrapParser` translates host configuration into an
Infrastructure bootstrap record. `SqlPlatformConfigurationStore` uses it only
when no durable row exists, so persisted operator settings win after first
startup. Evidence:
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/PlatformConfigurationBootstrapParser.cs:6-112`;
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/SqlPlatformConfigurationStore.cs:119-163`.

`PlatformConfigurationRestartPolicy` decides that platform or broker
environment changes require restart. `PlatformTimeProviderFactory` creates a
system or incrementing clock from bootstrap settings. Evidence:
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/PlatformConfigurationRestartPolicy.cs:6-13`;
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/PlatformTimeProviderFactory.cs:5-25`.

### IG broker adapter

`IgSessionClient` implements the Application-owned `IIgSessionClient` using
`HttpClient`. It translates local IG request/response models to HTTP methods,
headers, anonymous JSON request bodies, and private deserialization records.
No `HttpRequestMessage`, `HttpResponseMessage`, or JSON DTO crosses inward.
Evidence:
`src/TNC.Trading.Platform.Application/Infrastructure/Ig/IIgSessionClient.cs:1-24`;
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/Ig/IgSessionClient.cs:9-177`.

Application owns `IgAuthenticateRequest`, account, position, and authentication
response records. The request includes `BrokerEnvironmentKind`, but the adapter
does not inspect it. Evidence:
`src/TNC.Trading.Platform.Application/Infrastructure/Ig/IgAuthenticateRequest.cs:1-9`;
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/Ig/IgSessionClient.cs:24-62`.

`InMemoryPlatformIgProofDataStore` implements
`IPlatformIgProofDataStore` with a singleton concurrent dictionary. This is a
real runtime registration, not test-only setup. Evidence:
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/InMemoryPlatformIgProofDataStore.cs:6-17`;
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/PlatformInfrastructureServiceCollectionExtensions.cs:60-60`.

### Notifications and operational records

Application owns an event-specific `INotificationDispatcher` port.
Infrastructure implements it with `NotificationDispatcher`, which chooses a
transport, sends the message, redacts content, writes a notification record and
an operational event, and commits both through EF. Evidence:
`src/TNC.Trading.Platform.Application/Services/INotificationDispatcher.cs:5-14`;
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/NotificationDispatcher.cs:10-122`.

Infrastructure also owns a private transport abstraction,
`INotificationProvider`, with `RecordedNotificationProvider`,
`SmtpNotificationProvider`, and
`AzureCommunicationServicesEmailNotificationProvider` implementations. Their
message and result records remain internal to Infrastructure. Evidence:
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Notifications/INotificationProvider.cs:1-8`;
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Notifications/SmtpNotificationProvider.cs:8-69`;
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Notifications/AzureCommunicationServicesEmailNotificationProvider.cs:8-80`.

`OperationalRecordRetentionService` is an hourly hosted driver. It creates a
scope and calls `OperationalRecordRetentionProcessor`; that processor uses
set-based SQL Server deletes and an in-memory-compatible fallback. Evidence:
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/OperationalRecordRetentionService.cs:7-34`;
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/OperationalRecordRetentionProcessor.cs:8-99`.

### Dependency injection and composition

`AddPlatformInfrastructure` registers the EF context, configuration and
credential services, five persistence adapters, three notification providers,
the notification dispatcher, retention processor and hosted service, the
in-memory proof store, and the IG typed client. Evidence:
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/PlatformInfrastructureServiceCollectionExtensions.cs:16-68`.

API is the only production project that calls this extension. It also selects
the time provider and directly resolves `PlatformDbContext` and
`OperationalRecordRetentionProcessor` for startup. Web does not reference
Infrastructure; it communicates through service-discovered API HTTP clients.
Evidence: `src/TNC.Trading.Platform.Api/Program.cs:11-50`;
`src/TNC.Trading.Platform.Web/Program.cs:7-22`.

End-user identity adapters are outside Infrastructure. API configures JWT
validation, while Web configures cookie and OIDC Authorization Code with PKCE.
Both derive HTTPS metadata behavior from Development status. Evidence:
`src/TNC.Trading.Platform.Api/Authentication/PlatformApiAuthenticationServiceCollectionExtensions.cs:8-68`;
`src/TNC.Trading.Platform.Web/Authentication/PlatformWebAuthenticationServiceCollectionExtensions.cs:9-89`.

## Dependency and ownership findings

### High priority

1. The IG adapter always targets the demo API, including requests whose inward
   contract specifies `BrokerEnvironmentKind.Live`. DI hard-codes
   `https://demo-api.ig.com/gateway/deal/`, while `IgSessionClient` never uses
   `IgAuthenticateRequest.Environment`. Accounts and positions have no
   environment argument and therefore continue on the same typed-client base
   address. This is a runtime environment-routing defect and an external
   provider concern hidden in composition. Evidence:
   `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/PlatformInfrastructureServiceCollectionExtensions.cs:62-65`;
   `src/TNC.Trading.Platform.Application/Infrastructure/Ig/IgAuthenticateRequest.cs:5-9`;
   `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/Ig/IgSessionClient.cs:24-62`;
   `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/Ig/IgSessionClient.cs:64-137`.

2. API performs concrete EF schema lifecycle operations through an internal
   `PlatformDbContext`, including deleting the Development SQL database before
   every start. API also directly resolves the concrete retention processor.
   This is composition/startup work, so it does not reverse policy dependency,
   but it couples the host to EF provider identity, database lifecycle, and an
   Infrastructure workflow. The coupling requires
   `InternalsVisibleTo("TNC.Trading.Platform.Api")` and duplicated EF package
   references. Evidence:
   `src/TNC.Trading.Platform.Api/Program.cs:26-47`;
   `src/TNC.Trading.Platform.Infrastructure/Properties/InternalsVisibleTo.cs:1-5`;
   `src/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.csproj:8-16`.

3. Infrastructure owns several policy decisions, not only mechanism:

   * `PlatformConfigurationRestartPolicy` decides which operator changes require
     restart and depends directly on an EF entity. This business decision should
     operate on inward configuration values, while the adapter persists the
     resulting flag. Evidence:
     `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/PlatformConfigurationRestartPolicy.cs:1-13`.
   * `NotificationDispatchPolicy` decides recipient fallback, provider
     selection, and sanitization before delivery. The configured provider is an
     inward configuration value, yet the decision is implemented outward.
     Evidence:
     `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/NotificationDispatchPolicy.cs:6-24`.
   * `OperationalRecordRetentionPolicy` decides a 90-day default and which IG
     snapshot kind is eligible. EF deletion mechanics belong in Infrastructure,
     but the retention rule is stable application policy. Evidence:
     `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/OperationalRecordRetentionPolicy.cs:6-17`.
   * `OperationalDataRedactor` defines the cross-cutting secret taxonomy used for
     persisted event details and notification summaries. This is security policy
     mixed into the persistence adapter. Evidence:
     `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/OperationalDataRedactor.cs:7-120`.

### Medium priority

4. The broad `IPlatformConfigurationStore` combines current reads, runtime
   reads, startup activation, writes, secret coordination, audit production,
   and restart semantics. `PlatformConfigurationService` is a pass-through over
   this port, so Infrastructure effectively owns most configuration behavior.
   The interface is correctly owned inward, but its responsibility is wider than
   a persistence port. Evidence:
   `src/TNC.Trading.Platform.Application/Services/IPlatformConfigurationStore.cs:5-17`;
   `src/TNC.Trading.Platform.Application/Services/PlatformConfigurationService.cs:5-20`;
   `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/SqlPlatformConfigurationStore.cs:10-117`.

5. IG contracts are inward-owned, but their namespace is
   `Application.Infrastructure.Ig`, which describes the outer layer rather than
   the consuming feature or port. `IgAuthenticateResponse` also carries raw CST,
   security-token, and arbitrary header representations inward. The adapter does
   translate HTTP objects, but Application remains coupled to IG protocol token
   concepts and retrieves the API key from a response-header dictionary for
   proof-data calls. Evidence:
   `src/TNC.Trading.Platform.Application/Infrastructure/Ig/IIgSessionClient.cs:1-24`;
   `src/TNC.Trading.Platform.Application/Services/PlatformIgProofDataEnricher.cs:11-38`.

6. `IProtectedCredentialService` is correctly inner-owned, but its name and
   contract combine secret persistence and runtime decryption. The adapter uses
   ASP.NET Core Data Protection correctly at the mechanism boundary; however,
   the inspected registration does not make Data Protection key persistence or
   production key-ring ownership explicit. Ciphertext recoverability across
   host/container replacement cannot be established from this project alone.
   Evidence:
   `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/ProtectedCredentialService.cs:8-88`;
   `src/TNC.Trading.Platform.Api/Program.cs:15-19`.

7. `InMemoryPlatformIgProofDataStore` is a production singleton adapter. Proof
   data disappears on process restart and differs from the otherwise durable SQL
   operational model. If proof data is operator-visible operational state, its
   ownership and durability requirement should be explicit rather than implied
   by an Infrastructure class named `InMemory`. Evidence:
   `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/InMemoryPlatformIgProofDataStore.cs:6-17`;
   `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/PlatformInfrastructureServiceCollectionExtensions.cs:60-60`.

8. All notification providers receive raw `IConfiguration` and parse keys on
   each dispatch. Bootstrap parsing and retention do the same. This is valid at
   the outer boundary but spreads key names, defaults, validation, and provider
   aliases across adapters. Typed options validated at registration would keep
   provider concerns explicit and make configuration failures deterministic.
   Evidence:
   `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Notifications/SmtpNotificationProvider.cs:8-31`;
   `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Notifications/AzureCommunicationServicesEmailNotificationProvider.cs:8-31`;
   `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/OperationalRecordRetentionPolicy.cs:6-13`.

### Lower priority and naming

9. The project path already supplies the `Infrastructure` layer name, but source
   files add another `Infrastructure/` directory, and two namespaces repeat it
   as `Infrastructure.Infrastructure.Platform`. `Platform` then groups EF stores,
   configuration, credentials, notification orchestration, retention, clocks,
   proof storage, and IG. This hides adapter boundaries and conflicts with the
   repository preference for feature-oriented organization. Evidence:
   `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/Ig/IgSessionClient.cs:7-9`;
   `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/InMemoryPlatformIgProofDataStore.cs:4-6`.

10. `AddPlatformInfrastructure` accepts `IHostEnvironment` but never uses it.
    `ProtectedCredentialService` is registered both as itself and through its
    port because `SqlPlatformConfigurationStore` needs the port while startup
    consumers need only Application services. These are small signals that
    registration and startup ownership have drifted. Evidence:
    `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/PlatformInfrastructureServiceCollectionExtensions.cs:16-50`.

11. Infrastructure's private `INotificationProvider` is not inherently an
    inward-dependency violation because no inner code consumes it; it is a local
    transport strategy. The problem is that provider selection and dispatch
    policy surround it in Infrastructure. Moving the private interface inward
    without first moving those decisions would create a pass-through abstraction
    rather than improve the boundary.

12. End-user authentication does not present an Infrastructure ownership issue.
    Keycloak/Entra selection and OIDC/JWT adapters remain in AppHost, API, and
    Web. Infrastructure's Azure Identity dependency is for Azure Communication
    Services email authentication, not user authentication. Evidence:
    `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Notifications/AzureCommunicationServicesEmailNotificationProvider.cs:1-9`;
    `src/TNC.Trading.Platform.Api/Authentication/PlatformApiAuthenticationServiceCollectionExtensions.cs:8-68`;
    `src/TNC.Trading.Platform.Web/Authentication/PlatformWebAuthenticationServiceCollectionExtensions.cs:9-89`.

## Representative call paths

### Operator configuration update

1. Web sends an authorized `PUT /api/platform/configuration` using a Web-owned
   view model. Evidence: `src/TNC.Trading.Platform.Web/PlatformApiClient.cs:41-54`.
2. API binds and validates its transport request, then maps it to the
   Application request and inward configuration records. Evidence:
   `src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs:78-83`;
   `src/TNC.Trading.Platform.Api/Features/Platform/UpdatePlatformConfigurationEndpointHandler.cs:10-27`;
   `src/TNC.Trading.Platform.Api/Features/UpdatePlatformConfiguration/UpdatePlatformConfigurationMapping.cs:8-32`.
3. `UpdatePlatformConfigurationHandler` calls the Application configuration
   service, then ticks the coordinator. Evidence:
   `src/TNC.Trading.Platform.Application/Features/UpdatePlatformConfiguration/UpdatePlatformConfigurationHandler.cs:5-15`.
4. The service calls the Application-owned store port. Evidence:
   `src/TNC.Trading.Platform.Application/Services/PlatformConfigurationService.cs:5-20`;
   `src/TNC.Trading.Platform.Application/Services/IPlatformConfigurationStore.cs:5-17`.
5. `SqlPlatformConfigurationStore` maps inward values to an EF entity, decides
   restart through an outward policy, updates protected credentials, writes a
   redacted audit, commits, and maps back to an inward snapshot containing only
   credential-presence booleans. Evidence:
   `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/SqlPlatformConfigurationStore.cs:43-117`;
   `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/SqlPlatformConfigurationStore.cs:165-207`.
6. API maps the inward result to a transport response that exposes credential
   presence, not secret values. Evidence:
   `src/TNC.Trading.Platform.Api/Features/UpdatePlatformConfiguration/UpdatePlatformConfigurationMapping.cs:34-59`.

This path satisfies the Blazor-mediated, SQL-backed, write-only secret rule.
The boundary weakness is policy placement inside the store, not secret return.

### Broker authentication and proof capture

1. Application retrieves protected credentials by broker environment and builds
   an inward `IgAuthenticateRequest`. Evidence:
   `src/TNC.Trading.Platform.Application/Services/PlatformStateCoordinator.cs:647-659`.
2. The `IIgSessionClient` runtime implementation is the Infrastructure typed
   HTTP client. Evidence:
   `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/PlatformInfrastructureServiceCollectionExtensions.cs:62-65`.
3. `IgSessionClient` posts credentials, maps IG response headers and body to the
   inward response, and throws status-bearing `HttpRequestException` failures.
   Evidence:
   `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/Ig/IgSessionClient.cs:24-62`.
4. Application classifies transport failures, maps a sanitized login snapshot,
   and invokes the Application-owned snapshot-store port. Evidence:
   `src/TNC.Trading.Platform.Application/Services/PlatformStateCoordinator.cs:620-683`.
5. The EF adapter replaces the latest snapshot and preserves the first
   successful snapshot per trading day. Evidence:
   `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/EfPlatformIgLoginSnapshotStore.cs:9-49`.
6. Proof enrichment calls accounts and positions through the same port and saves
   a compact inward snapshot through `IPlatformIgProofDataStore`. Evidence:
   `src/TNC.Trading.Platform.Application/Services/PlatformIgProofDataEnricher.cs:11-57`.

The dependency inversion is sound, but every runtime call still targets the demo
host because environment routing is absent from the adapter.

### Failure notification and audit

1. `PlatformStateCoordinator` decides when failure, recovery, blocked-live, and
   retry-limit events occur. Representative calls are at
   `src/TNC.Trading.Platform.Application/Services/PlatformStateCoordinator.cs:422-510`.
2. `PlatformStateCoordinatorSideEffects` forwards the event to the inward-owned
   `INotificationDispatcher`. Evidence:
   `src/TNC.Trading.Platform.Application/Services/PlatformStateCoordinatorSideEffects.cs:80-107`.
3. Infrastructure creates a redacted dispatch context, selects an internal
   transport by configured name, handles supported provider exceptions, and
   persists a notification plus operational event. Evidence:
   `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/NotificationDispatcher.cs:25-122`.
4. SMTP or Azure Communication Services translates the Infrastructure message to
   its provider API. Evidence:
   `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Notifications/SmtpNotificationProvider.cs:13-69`;
   `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Notifications/AzureCommunicationServicesEmailNotificationProvider.cs:13-60`.

Runtime calls move outward while source references remain inward. The mixed
responsibility is that the adapter also determines fallback recipient, provider
selection outcome, redaction, persistence records, and transport execution.

### Startup and retention

1. API registers Application and Infrastructure, then opens a startup scope.
   Evidence: `src/TNC.Trading.Platform.Api/Program.cs:17-26`.
2. API resolves the concrete EF context, conditionally deletes Development SQL,
   ensures schema creation, activates startup configuration, runs retention, and
   ticks the coordinator. Evidence: `src/TNC.Trading.Platform.Api/Program.cs:28-50`.
3. The Infrastructure hosted service repeats retention hourly by resolving the
   concrete processor from a scope. Evidence:
   `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/OperationalRecordRetentionService.cs:7-34`.

The sequence is composition work, but concrete EF and processor access leaks
mechanism details into API and duplicates the first retention execution outside
the hosted driver.

## Test assessment

The Infrastructure unit project references both Application and Infrastructure,
uses xUnit, and has no real external dependencies. Evidence:
`test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/TNC.Trading.Platform.Infrastructure.UnitTests.csproj:1-26`.

Strong coverage includes:

* SQL configuration seeding, durable-value precedence, restart flags, audit
  history, and secret-safe audit details using EF InMemory. Evidence:
  `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/SqlPlatformConfigurationStoreTests.cs:10-249`.
* Credential environment isolation, protected-at-rest values, rotation, runtime
  decryption, and partial credentials. Evidence:
  `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/ProtectedCredentialServiceTests.cs:8-120`.
* IG HTTP methods, paths, version and API-key headers, request body shape, token
  mapping, and non-success behavior with a fake `HttpMessageHandler`. Evidence:
  `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/Infrastructure/Platform/Ig/IgSessionClientTests.cs:10-345`.
* Notification provider fallback, redaction, persisted status, missing provider,
  and handled provider exceptions. Evidence:
  `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/NotificationProviderTests.cs:9-349`.
* Retention behavior and latest-versus-daily IG snapshot semantics. Evidence:
  `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/OperationalRecordRetentionProcessorTests.cs:8-70`;
  `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/EfPlatformIgLoginSnapshotStoreTests.cs:6-48`.

Material gaps are:

* No test covers `AddPlatformInfrastructure`, missing connection-string failure,
  test-only InMemory guards, complete port registration, or hosted-service
  registration.
* No test covers live-versus-demo IG endpoint routing. Existing IG tests create
  the client with the demo base address and never assert that the request's
  `Environment` changes the host. Evidence:
  `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/Infrastructure/Platform/Ig/IgSessionClientTests.cs:326-345`.
* Retention tests exercise only EF InMemory. The SQL Server
  `ExecuteDeleteAsync` branch, model constraints, indexes, transaction behavior,
  and provider-specific query translation lack a real SQL integration test.
* SMTP and Azure Communication Services tests cover unconfigured fallback, not
  successful provider translation, authentication mode, timeout, or SDK failure
  handling. Evidence:
  `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/NotificationProviderTests.cs:31-81`.
* No test proves Data Protection ciphertext can be decrypted after a new service
  provider or host instance uses the intended persistent key ring.
* API startup schema creation/deletion, startup configuration activation, first
  retention pass, and coordinator tick are not isolated behind a startup
  abstraction and have no focused startup test.
* The Application unit-test project directly references Infrastructure and
  constructs EF stores, notification providers, and credential services. These
  tests cover useful workflows but are adapter-coupled integration-style tests
  inside the nominal Application unit suite. Evidence:
  `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/TNC.Trading.Platform.Application.UnitTests.csproj:18-25`;
  `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/AuthRetryCycleTests.cs:1-46`.
* Architecture tests enforce only reference resolution and acyclicity. They do
  not encode the intended `Infrastructure -> Application` direction, prevent
  Web-to-Infrastructure references, constrain API concrete use to composition,
  or detect framework/provider types in Application. Evidence:
  `test/TNC.Trading.Platform.Architecture/TNC.Trading.Platform.Architecture.IntegrationTests/ProjectGraph/RepositoryProjectGraphTests.cs:3-17`.

## Incremental recommendations

1. Fix and characterize IG environment routing first. Add a focused adapter test
   that asserts Demo and Live requests use different approved base URIs. Move
   endpoint resolution into an Infrastructure-owned typed options or resolver
   component, and make every session, accounts, and positions call explicitly
   associated with the selected broker environment. Keep `IIgSessionClient`
   inward-owned; do not expose `HttpClient` or provider DTOs.

2. Introduce one narrow Infrastructure startup facade or registration-owned
   initializer for database creation/migration and retention startup. API should
   invoke one composition-level operation without resolving `PlatformDbContext`
   or `OperationalRecordRetentionProcessor`. This allows removal of API's EF and
   unused Azure package references and, if no other concrete access remains,
   Infrastructure's API friend-assembly grant. Preserve startup ordering.

3. Move one policy at a time to Application, with behavior tests before moving
   mechanisms:

   * First move restart determination to the update use case using inward current
     and proposed configuration values.
   * Then move retention-window and retained-snapshot eligibility decisions to a
     small inward policy or pass an inward retention specification to the EF
     adapter.
   * Then move redaction classification to an inward security policy used by
     outward persistence and notification adapters.
   * Keep EF queries, entity mapping, SMTP/ACS construction, and SDK exception
     translation in Infrastructure.

4. Split configuration orchestration from persistence without inventing a new
   project. Let the Application update handler coordinate restart policy,
   credential replacement, audit intent, and runtime refresh. Narrow the store
   toward reading and persisting inward configuration state. Preserve a single
   transaction boundary where configuration and audit consistency requires it;
   do not create interfaces that only forward calls.

5. Rename and organize adapters around their boundary within the existing
   project. A low-risk target is:

   ```text
   DependencyInjection/
   Persistence/EntityFramework/
   Configuration/SqlServer/
   Credentials/DataProtection/
   Integrations/Ig/
   Notifications/Recorded/
   Notifications/Smtp/
   Notifications/AzureCommunicationServices/
   Operations/Retention/
   Time/
   ```

   Remove the duplicated `Infrastructure/Infrastructure` namespace. Rename the
   Application IG port namespace toward the consuming capability, for example
   `Application.Brokerage.Ig` or a feature-local `Ports` namespace. Treat these
   as mechanical follow-ups after behavior changes, not a prerequisite.

6. Bind typed and validated outer options at registration for IG endpoints,
   SMTP, Azure Communication Services, retention, bootstrap settings, and test
   clock behavior. Pass options to adapters instead of raw `IConfiguration`.
   Keep secrets sourced externally and prefer Azure credential-based ACS setup
   over connection strings where deployment supports it.

7. Decide the proof-data durability requirement. If it is operator-visible
   runtime evidence, add a SQL-backed adapter under the existing
   `IPlatformIgProofDataStore` port. If it is intentionally ephemeral, rename it
   to communicate cache semantics and document process-lifetime loss.

8. Separate test responsibilities incrementally. Replace Infrastructure
   constructions in Application unit tests with fakes for Application-owned
   ports, and move the existing EF-backed workflow cases to Infrastructure or a
   service integration suite. Add a SQL Server integration slice for EF model,
   retention, and configuration transactions while retaining fast InMemory
   mapping tests.

9. Extend architecture tests only after declaring stable roles. At minimum,
   assert that Application does not reference Infrastructure, Web does not
   reference Infrastructure, Infrastructure references Application, and
   Application source does not depend on EF, Azure SDK, HTTP, or Infrastructure
   namespaces. Avoid tests tied to exact project counts or folder snapshots.

10. Keep end-user authentication where it is. No move into Infrastructure is
    warranted. Add `Microsoft.Identity.Web` only when the Azure Entra deployment
    integration is implemented, as required by repository auth rules; do not
    conflate that work with the IG broker adapter.

## Unresolved gaps

* The intended production IG Live REST base URI and whether Live broker actions
  are planned beyond current safeguards are not declared in the inspected
  adapter or tests.
* The durability requirement for IG proof data is not inferable from code alone.
* Production Data Protection key-ring storage and rotation ownership are not
  visible in the inspected API, Infrastructure, or AppHost registration paths.
* No database migrations were found in the Infrastructure inventory. Whether
  `EnsureCreatedAsync` plus Development deletion is temporary scaffolding or an
  accepted lifecycle strategy requires product and operations input.
* Notification delivery atomicity is undefined. The current implementation sends
  externally before saving the notification and operational records, so a
  database failure after provider success can lose audit evidence and a retry can
  duplicate delivery. No outbox/idempotency requirement was found.
* The desired transaction boundary between configuration updates, protected
  credential writes, and configuration-audit writes is not documented. They use
  one scoped context today, but the consistency guarantee is implicit.

## Recommended next research

* Confirm IG Demo and Live endpoint requirements against the broker integration
  specification and define environment-routing acceptance tests.
* Trace Data Protection key-ring configuration through deployment manifests and
  Azure hosting configuration.
* Inspect SQL container and deployment lifecycle requirements to choose EF
  migrations versus controlled schema initialization.
* Decide proof-data persistence and notification outbox/idempotency requirements
  with product and operations owners.
* Measure Application unit-test cases that can switch to port fakes before moving
  the remaining EF-backed cases into an integration boundary.