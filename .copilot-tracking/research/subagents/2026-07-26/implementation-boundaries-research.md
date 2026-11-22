---
title: Implementation Boundaries Research
description: Evidence-led assessment of implementation organization and architectural boundary behavior
ms.date: 2026-07-26
ms.topic: concept
---

## Research scope

Assess actual implementation organization and boundary behavior across representative API, Application, business logic, Infrastructure, persistence, configuration, authentication, Web UI, and dependency injection paths.

The assessment covers:

* Vertical slices versus technical layers
* Ports, adapters, and Dependency Inversion
* Infrastructure leakage and separation of concerns
* Transactions and data access
* Cross-feature coupling and shared abstractions
* Architectural smells
* Fit with Layered, Clean, Onion, Hexagonal, Vertical Slice, and Modular Monolith styles

Production files are read-only for this research.

## Working hypothesis

The implementation is a hybrid. HTTP operations use feature-oriented endpoint and Application handler surfaces, but the handlers delegate most behavior to shared Application services and configuration models. Project references and Application-owned interfaces preserve Dependency Inversion toward Infrastructure adapters, while persistence commits inside individual stores limit application-level transaction coordination.

A representative update path and a representative operational/auth path will test this hypothesis. The hypothesis is weakened if handlers own cohesive use-case logic, features avoid shared coordinators, or one explicit unit of work controls multi-store mutations.

## Evidence ledger

Evidence entries use workspace-relative paths and exact 1-based line ranges.

### Solution and dependency direction

* `src/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.csproj:1-13` has no project references, but it does reference the ASP.NET Core shared framework.
* `src/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.csproj:11-13` references Application. Its EF Core and Azure adapter dependencies are declared at lines 15-20.
* `src/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.csproj:3-7` references Application, Infrastructure, and ServiceDefaults. The API is the main in-process composition root.
* `src/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.csproj:3-6` references Application and ServiceDefaults but not Infrastructure. Web reaches the API over HTTP rather than calling persistence adapters in process.
* `src/TNC.Trading.Platform.AppHost/AppHostProjectRegistration.cs:12-27` gives the API database and mail infrastructure references. Lines 30-46 register Web as a separate process that references and waits for API.

### API boundaries

* `src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs:26-51` groups platform routes and applies Viewer, Operator, or Administrator policies per operation.
* `src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs:57-97` shows thin read and command endpoints that construct Application requests, invoke one handler, and map one response.
* `src/TNC.Trading.Platform.Api/Features/Platform/UpdatePlatformConfigurationEndpointHandler.cs:10-27` validates the transport request, maps it to an Application request, dispatches the handler, maps the response, and translates validation failures.
* `src/TNC.Trading.Platform.Api/Features/UpdatePlatformConfiguration/UpdatePlatformConfigurationMapping.cs:8-37` maps API DTOs into Application configuration objects. Lines 39-66 map Application results back into API DTOs, preventing EF entities from crossing the HTTP boundary.
* `src/TNC.Trading.Platform.Api/Features/UpdatePlatformConfiguration/UpdatePlatformConfigurationValidator.cs:7-92` validates input shape but also enforces schedule, retry, and Test/Live compatibility rules using Application enums. The policy at lines 80-86 is duplicated by the Application state engine.
* `src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs:105-122` is a vertical-slice exception: the audit endpoint injects a shared Application service and persistence port directly instead of dispatching an Application handler.
* `src/TNC.Trading.Platform.Api/Features/Platform/RecordAuthAuditEventEndpointHandler.cs:11-55` performs audit use-case orchestration in the HTTP project, including configuration lookup, claims extraction, correlation, event construction, and persistence.

### Application use cases, ports, and business logic

* `src/TNC.Trading.Platform.Application/Features/GetPlatformStatus/GetPlatformStatusRequest.cs:1-3`, `src/TNC.Trading.Platform.Application/Features/GetPlatformStatus/GetPlatformStatusResponse.cs:1-5`, and `src/TNC.Trading.Platform.Application/Features/GetPlatformStatus/GetPlatformStatusHandler.cs:1-12` provide the intended request/response/handler feature shape.
* `src/TNC.Trading.Platform.Application/Features/UpdatePlatformConfiguration/UpdatePlatformConfigurationHandler.cs:5-15` coordinates configuration persistence and a state tick. The handler contains little use-case policy itself.
* `src/TNC.Trading.Platform.Application/Services/PlatformConfigurationService.cs:5-20` is a pass-through facade over `IPlatformConfigurationStore`; it does not add policy or transaction control.
* `src/TNC.Trading.Platform.Application/Services/IPlatformConfigurationStore.cs:5-16` and `src/TNC.Trading.Platform.Application/Services/IPlatformRuntimeStateStore.cs:5-10` are Application-owned output ports expressed in Application models.
* `src/TNC.Trading.Platform.Application/Services/PlatformStateCoordinator.cs:9-32` depends on configuration, six persistence/integration ports, a schedule gate, a state engine helper, notification dispatch, time, and logging. This is the central cross-feature orchestrator.
* `src/TNC.Trading.Platform.Application/Services/PlatformStateCoordinator.cs:34-77` shows that a status query triggers a state-machine tick and then reads several stores. The query therefore has side effects.
* `src/TNC.Trading.Platform.Application/Services/PlatformStateCoordinator.cs:85-146` enforces manual-retry eligibility, mutates runtime state, writes a retry cycle and event, saves state, attempts external recovery, and saves state again.
* `src/TNC.Trading.Platform.Application/Services/PlatformStateTransitionEngine.cs:7-42` contains deterministic state transition policy independent of HTTP and EF. Lines 44-75 model decisions explicitly.
* `src/TNC.Trading.Platform.Application/Services/TradingScheduleGate.cs:7-32` contains trading schedule policy independent of adapters. Lines 34-64 handle weekend and time-zone behavior.
* `src/TNC.Trading.Platform.Application/Services/PlatformStateCoordinatorSideEffects.cs:5-75` groups retry, event, notification, and logging side effects, but each call remains a separate port operation rather than one transaction.
* `src/TNC.Trading.Platform.Application/Authentication/PlatformAuthorizationPolicyRegistration.cs:1-35` places ASP.NET Core `AuthorizationOptions` policy registration in Application. This shares policy names and roles but introduces framework coupling into the inner project.

### Infrastructure adapters and persistence

* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/PlatformInfrastructureServiceCollectionExtensions.cs:18-43` configures one EF `PlatformDbContext`, with SQL Server for normal operation and EF InMemory only for test authentication mode.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/PlatformInfrastructureServiceCollectionExtensions.cs:46-64` binds Application ports to EF, credential, notification, in-memory proof-data, and typed HTTP adapters. This is direct Dependency Inversion evidence.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/PlatformDbContext.cs:5-21` exposes one context for configuration, credentials, auth runtime state, retry cycles, login snapshots, events, audits, and notification records. Lines 23-114 configure all these concerns in one model.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/SqlPlatformConfigurationStore.cs:11-43` implements the Application configuration port and combines persisted configuration with protected credential presence.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/SqlPlatformConfigurationStore.cs:45-115` maps updates into EF state, invokes the credential service, creates an audit record, commits, and maps the result. This adapter owns policy-adjacent orchestration as well as persistence mapping.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/ProtectedCredentialService.cs:27-45` stages protected credential upserts without calling `SaveChangesAsync`. Lines 64-91 add or modify entities on the same scoped context, allowing the configuration store's final save to commit configuration, credentials, and audit together.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/SqlPlatformConfigurationStore.cs:117-153` lazily bootstraps missing configuration and commits it from inside the adapter.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/EfPlatformEventStore.cs:38-56` commits every event addition internally. Similar port-level commits make wider coordinator workflows incrementally durable rather than atomic.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/PlatformConfigurationRestartPolicy.cs:6-13` makes restart policy depend directly on an EF entity. The rule is therefore not reusable without Infrastructure.

### Web adapter and feature calls

* `src/TNC.Trading.Platform.Web/Program.cs:7-20` composes authentication, UI services, Razor components, and typed clients for the API and audit API.
* `src/TNC.Trading.Platform.Web/PlatformApiClient.cs:9-123` is a platform-wide HTTP gateway covering status, configuration, retry, events, administration, and login history. It uses hard-coded API routes and Application-owned scope constants.
* `src/TNC.Trading.Platform.Web/Components/Pages/ConfigurationPagePresenter.cs:5-40` separates page-oriented loading, mapping, save messaging, and expected exception translation from Razor rendering.
* `src/TNC.Trading.Platform.Web/Components/Pages/Configuration.razor:1-4` applies an Application-owned authorization policy at the page boundary. Lines 169-213 delegate scope checks, load, and save operations to collaborators while retaining UI state management.
* `src/TNC.Trading.Platform.Web/Components/Pages/Configuration.razor:37-49` duplicates the Test/Live environment rule in presentation behavior by disabling the Live broker option.

### Composition and startup behavior

* `src/TNC.Trading.Platform.Application/Services/PlatformApplicationServiceCollectionExtensions.cs:13-27` registers shared services, all handlers, and the auth supervisor in one Application registration extension.
* `src/TNC.Trading.Platform.Api/Program.cs:11-21` is the composition root for Application and Infrastructure registrations.
* `src/TNC.Trading.Platform.Api/Program.cs:23-55` directly resolves `PlatformDbContext`, `PlatformConfigurationService`, an Infrastructure retention processor, and the shared coordinator during startup. Development startup deletes and recreates SQL Server at lines 31-40.
* `src/TNC.Trading.Platform.AppHost/AppHost.cs:3-12` keeps distributed-process orchestration concise and delegates infrastructure, project, environment, and identity-provider wiring to focused registration classes.

## Representative flows

### Update configuration command

```text
Configuration.razor
  -> ConfigurationPagePresenter.SaveAsync
  -> PlatformApiClient PUT /api/platform/configuration
  -> PlatformEndpoints.UpdatePlatformConfigurationAsync
  -> API validator and API-to-Application mapping
  -> UpdatePlatformConfigurationHandler
  -> PlatformConfigurationService
  -> IPlatformConfigurationStore
  -> SqlPlatformConfigurationStore
      -> stage configuration entity changes
      -> IProtectedCredentialService stages protected credential changes
      -> add configuration audit
      -> one DbContext.SaveChangesAsync commits these three sets
  -> PlatformStateCoordinator.TickAsync
      -> state engine decision
      -> multiple store/event/notification operations, each potentially committing
  -> Application-to-API mapping
  -> Web view model and form mapping
```

The UI-to-handler portion is a recognizable vertical slice. The slice loses cohesion behind the handler because a shared facade and coordinator own the behavior. Configuration, credential, and audit writes are atomic within the store because they share the scoped context and final save (`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/SqlPlatformConfigurationStore.cs:45-115`; `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/ProtectedCredentialService.cs:27-45,64-91`). The command as a whole is not atomic because the handler then calls a coordinator whose ports commit independently (`src/TNC.Trading.Platform.Application/Features/UpdatePlatformConfiguration/UpdatePlatformConfigurationHandler.cs:9-13`; `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/EfPlatformEventStore.cs:38-56`). A failed tick can therefore leave the configuration committed while runtime reconciliation is incomplete.

### Status query

```text
GET /api/platform/status
  -> GetPlatformStatusHandler
  -> PlatformStateCoordinator.GetStatusAsync
      -> TickAsync mutates and persists runtime state and side effects
      -> read runtime configuration, schedule, snapshot, and proof stores
  -> status projection
  -> API response mapping
```

The transport path is thin (`src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs:57-62`), but the nominal query is not read-only (`src/TNC.Trading.Platform.Application/Services/PlatformStateCoordinator.cs:34-77`). This weakens CQRS semantics and makes latency, availability, and mutation behavior of a read endpoint depend on IG auth and persistence state.

### Authentication audit command

```text
POST /api/platform/auth/audit
  -> RecordAuthAuditEventEndpointHandler
      -> resolve event from HTTP request and ClaimsPrincipal
      -> load current configuration through shared service
      -> construct PlatformEventRecord
      -> IPlatformEventStore.AddAsync
  -> 202 Accepted
```

This is implemented as API-layer orchestration rather than an Application feature (`src/TNC.Trading.Platform.Api/Features/Platform/RecordAuthAuditEventEndpointHandler.cs:11-55`). The persistence dependency is still an Application-owned port, but business workflow and transport concerns are interleaved.

## Architectural assessment

### Vertical Slice

Evidence for:

* API and Application have operation-named feature folders and explicit request, response, handler, mapping, and validator types (`GetPlatformStatusRequest.cs:1-3`; `GetPlatformStatusResponse.cs:1-5`; `GetPlatformStatusHandler.cs:1-12`; `UpdatePlatformConfigurationEndpointHandler.cs:10-27`).
* Most endpoints dispatch exactly one handler and map results (`PlatformEndpoints.cs:57-97`).

Evidence against:

* Handlers are thin wrappers over shared technical services and one large coordinator (`UpdatePlatformConfigurationHandler.cs:5-15`; `GetPlatformStatusHandler.cs:5-11`).
* `PlatformStateCoordinator` centralizes status, events, retry, external auth, persistence, and notifications (`PlatformStateCoordinator.cs:9-32,34-146`). Changes in auth behavior can affect several feature entry points.
* The audit operation bypasses the Application feature pattern (`PlatformEndpoints.cs:105-122`; `RecordAuthAuditEventEndpointHandler.cs:11-55`).

Assessment: **Vertical-slice entry points over a shared, technically layered application core**, not full end-to-end vertical slices.

### Layered Architecture

Evidence for:

* Projects map naturally to presentation/API, Application, Infrastructure, and host composition concerns.
* Web calls API over HTTP, API invokes Application, and Infrastructure implements persistence and integrations.

Evidence against:

* Infrastructure references Application rather than Application referencing Infrastructure (`Infrastructure.csproj:11-13`), which follows Dependency Inversion rather than a traditional downward layer chain.
* Application owns adapter ports, and API is allowed to compose both inward and outward projects.

Assessment: **Strong organizational layering, but not classic dependency-down layered architecture**.

### Clean Architecture

Evidence for:

* Application has no project dependency on Infrastructure (`Application.csproj:1-13`).
* Infrastructure implements Application-owned ports, and the API composition root binds them (`PlatformInfrastructureServiceCollectionExtensions.cs:46-64`).
* Deterministic business policy is testable without EF or HTTP (`PlatformStateTransitionEngine.cs:7-42`; `TradingScheduleGate.cs:7-64`).
* API transport DTOs are mapped to Application models (`UpdatePlatformConfigurationMapping.cs:8-66`).

Evidence against:

* Application references `Microsoft.AspNetCore.App` (`Application.csproj:8-10`) and contains ASP.NET Core authorization registration (`PlatformAuthorizationPolicyRegistration.cs:1-35`).
* No independent Domain project or domain-owned aggregate boundary exists. Most business models sit in `Application.Configuration` and behavior sits in Application services.
* Infrastructure owns restart policy tied to an EF entity (`PlatformConfigurationRestartPolicy.cs:6-13`).

Assessment: **Clean-inspired dependency direction with framework and policy leakage**, not strict Clean Architecture.

### Onion Architecture

Evidence for:

* Application models, state transition rules, and ports form an inward dependency target.
* EF, data protection, SMTP/Azure notifications, and IG HTTP access are outward adapters registered in Infrastructure (`PlatformInfrastructureServiceCollectionExtensions.cs:46-64`).

Evidence against:

* There is no distinct domain center with entities/value objects owning invariants.
* Application's framework reference and authorization registration prevent a framework-neutral inner ring.

Assessment: **Partial Onion characteristics**, primarily dependency direction rather than explicit concentric domain structure.

### Hexagonal Architecture

Evidence for:

* `IPlatformConfigurationStore`, state/event/snapshot/retry stores, `INotificationDispatcher`, and `IIgSessionClient` are output ports owned inward and bound to adapters outward (`IPlatformConfigurationStore.cs:5-16`; `PlatformInfrastructureServiceCollectionExtensions.cs:46-64`).
* HTTP API and Web are inbound/outbound adapters around Application behavior.

Evidence against:

* Input ports are concrete handlers rather than interfaces, which is acceptable pragmatically but less explicit.
* The audit HTTP adapter directly assembles a use case from multiple output ports (`RecordAuthAuditEventEndpointHandler.cs:11-55`).
* Some port contracts are broad stores rather than capability-specific interfaces, especially configuration (`IPlatformConfigurationStore.cs:5-16`).

Assessment: **Meaningful Hexagonal ports-and-adapters structure**, with inconsistent inbound boundaries and coarse ports.

### Modular Monolith

Evidence for:

* API and Web are separately hosted processes under one solution, while API behavior is deployed as one application backed by one database.
* Internal types and project boundaries constrain direct access from consumers.

Evidence against:

* Projects are technical layers, not business modules.
* One `PlatformDbContext` spans all business concerns (`PlatformDbContext.cs:5-21`), and one coordinator couples configuration, auth, retry, events, snapshots, proof data, and notifications (`PlatformStateCoordinator.cs:9-32`).
* There are no module-local composition roots, schemas, contracts, or enforced inter-module APIs.

Assessment: **A layered monolith with process-separated Web and API, not a mature modular monolith**.

### Overall classification

The closest description is **a Clean/Hexagonal-inspired layered monolith with vertical-slice HTTP and handler entry points**. Vertical Slice is visible in naming and dispatch, but shared Application services and the coordinator determine most behavior. Clean and Hexagonal classifications have stronger dependency evidence than Onion because ports are explicit, but all three are weakened by ASP.NET Core coupling in Application and the absence of a distinct domain center. Modular Monolith is the weakest fit because technical projects, a shared database model, and a shared coordinator do not create business module boundaries.

## Risks and smells

* The `PlatformStateCoordinator` has a large dependency surface and coordinates unrelated persistence, integration, policy, and notification concerns (`PlatformStateCoordinator.cs:9-32`). It is a change-coupling hotspot and an emerging god service.
* Read endpoints can mutate state because status and event queries call `TickAsync` (`PlatformStateCoordinator.cs:34-82`). This surprises callers, weakens CQRS, and increases read-path failure modes.
* Store methods call `SaveChangesAsync` internally (`SqlPlatformConfigurationStore.cs:45-115`; `EfPlatformEventStore.cs:38-56`). Multi-store workflows have no explicit Application transaction boundary and can partially commit.
* Configuration update commits before runtime reconciliation (`UpdatePlatformConfigurationHandler.cs:9-13`). A reconciliation failure returns an error after durable configuration change, inviting retries and ambiguous operator outcomes.
* Business rules are duplicated across Web presentation, API validation, and Application state policy: Test platform plus Live broker is disabled in `Configuration.razor:37-49`, rejected in `UpdatePlatformConfigurationValidator.cs:80-86`, and handled in `PlatformStateTransitionEngine.cs:14-18`.
* Infrastructure owns restart policy using an EF entity (`PlatformConfigurationRestartPolicy.cs:6-13`), leaking a business decision outward and making it persistence-shaped.
* The pass-through `PlatformConfigurationService` adds indirection without a distinct policy or transaction role (`PlatformConfigurationService.cs:5-20`).
* The audit endpoint implements workflow in the API (`RecordAuthAuditEventEndpointHandler.cs:11-55`), creating an inconsistent use-case boundary and making handler-level testing conventions less uniform.
* `PlatformApiClient` is a cross-feature client facade (`PlatformApiClient.cs:9-123`). It is manageable at current size but will accumulate routes, scopes, DTO knowledge, and error behavior as features grow.
* Application's ASP.NET Core framework dependency and authorization registration (`Application.csproj:8-10`; `PlatformAuthorizationPolicyRegistration.cs:1-35`) reduce portability and blur policy definition versus host registration.
* The single EF model spans all concerns (`PlatformDbContext.cs:5-114`), so schema and transaction changes have broad blast radius. This also prevents claims of module-level persistence isolation.
* API startup directly deletes the development SQL database (`Program.cs:31-40`) and directly resolves Infrastructure types (`Program.cs:25-51`). This is explicit composition, but destructive schema lifecycle and operational maintenance are mixed into HTTP host startup.

## Assumptions and limitations

* Classification is based on representative implementation paths and recurring registrations, not every source file.
* Internal accessibility is treated as an intentional assembly boundary. Tests may gain access through `InternalsVisibleTo`, which was not evaluated as a production coupling mechanism.
* A shared scoped `PlatformDbContext` is assumed for one HTTP request because `AddDbContext` uses scoped lifetime by default (`PlatformInfrastructureServiceCollectionExtensions.cs:20-43`). This explains why staged protected credentials participate in the configuration store's save.
* No explicit transaction or unit-of-work abstraction appeared in the inspected paths. EF's individual `SaveChangesAsync` calls are assumed to provide atomicity only for each call, not across the whole handler.
* The absence of a separate Domain project does not mean there is no domain logic. The state engine and schedule gate are substantive business policy in Application.
* Aspire process wiring is deployment composition, not evidence of business modularity (`AppHostProjectRegistration.cs:12-46`).

## Clarifying questions

No blocking clarifying questions remain. A desired target architecture would be needed before turning these observations into a refactoring recommendation.

## Next research

* [ ] Measure coordinator coupling through tests and call sites to identify safe extraction boundaries.
* [ ] Trace notification dispatch and IG session calls through failure paths to assess idempotency and external side-effect ordering.
* [ ] Inspect concurrency tokens, database constraints, and retry behavior for configuration and runtime-state updates.
* [ ] Compare test organization with the intended vertical-slice boundaries and determine whether tests encode current shared-service coupling.
* [ ] Evaluate whether auth contracts belong in a small shared contracts project or remain appropriately placed in Application.
