<!-- markdownlint-disable-file -->
# Repository Clean Architecture Alignment Research

## Research scope

* Trace exact solution and project references.
* Assess dependency direction against Robert C. Martin's Clean Architecture.
* Locate composition roots and representative API and Blazor request paths.
* Inspect application use cases, domain policies, infrastructure adapters, contracts, interfaces, authentication, configuration, and tests.
* Assess whether project names map cleanly to entities, use cases, interface adapters, and frameworks.
* Identify strengths, gaps, ambiguous ownership, concrete risks, unresolved questions, and recommended next research.

## Status

Complete for the requested representative codebase assessment. No product files were modified.

## Evidence and findings

### Solution and project dependency graph

* `TNC.Trading.Platform.slnx:1-29` includes six production projects and eight test projects. The solution grouping is organizational only; dependency direction is defined by the project files.
* `src/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.csproj:1-13` has no production project reference, making it the innermost production assembly in the literal graph. It does reference the entire `Microsoft.AspNetCore.App` shared framework at lines 9-11, so it is not framework-independent.
* `src/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.csproj:1-24` references Application at lines 13-15 and owns Azure Communication Services and EF Core provider dependencies at lines 17-22. This is the principal correct inward dependency edge.
* `src/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.csproj:1-25` references Application, Infrastructure, and ServiceDefaults at lines 3-7. As an outer host/composition root, those references point in a defensible direction. The duplicate Azure and EF Core package references at lines 9-16 are broader than the source responsibilities shown so far and make the host capable of direct framework coupling.
* `src/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.csproj:1-19` references Application and ServiceDefaults at lines 3-6 and directly uses OpenID Connect and Radzen at lines 8-11. Web is an interface/framework adapter, but its direct Application dependency is currently used for shared authentication definitions rather than application use cases.
* `src/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.csproj:1-20` references the API and Web executable projects at lines 3-8 and owns Aspire Keycloak/SQL orchestration dependencies. It is a deployment composition project outside the canonical Clean Architecture circles.
* `src/TNC.Trading.Platform.ServiceDefaults/TNC.Trading.Platform.ServiceDefaults.csproj:1-21` is a framework-focused shared host utility with ASP.NET Core, service discovery, resilience, OpenAPI, and OpenTelemetry dependencies. It is also outside the canonical four circles and is correctly consumed by outer hosts only.

### Composition roots and runtime orchestration

* `src/TNC.Trading.Platform.Api/Program.cs:1-16` is the API composition root: it registers API authentication, Application services, Infrastructure adapters, a framework time provider, and endpoint validation.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/PlatformInfrastructureServiceCollectionExtensions.cs:16-70` binds Application ports to EF, SQL, notification, in-memory proof, and IG HTTP implementations. Lines 39-65 are a strong Dependency Inversion implementation because concrete adapters are registered against interfaces declared inward.
* `src/TNC.Trading.Platform.Application/Services/PlatformApplicationServiceCollectionExtensions.cs:1-29` registers use-case handlers and application coordinators, but also registers `PlatformAuthSupervisor` as an ASP.NET hosted service at line 24. This registration API and hosted-service concern help explain the Application project's ASP.NET framework dependency.
* `src/TNC.Trading.Platform.Api/Program.cs:18-51` performs database creation/deletion and invokes configuration, retention, and state-transition workflows directly during host startup. This is valid composition-root access to concrete infrastructure, but it places business/operational sequencing in the framework host rather than behind one application startup use case. The development-only `EnsureDeletedAsync` at lines 29-34 creates a concrete data-loss risk whenever a persistent development SQL database is reused.
* `src/TNC.Trading.Platform.Api/Program.cs:54-88` owns HTTP middleware, authentication/authorization, endpoint mapping, API documentation, and health endpoints, all appropriate outer-layer concerns.
* `src/TNC.Trading.Platform.Web/Program.cs:1-25` is the Web composition root and configures service defaults, authentication, UI presenters, Razor components, and typed API clients. The Web-to-API dependency is runtime HTTP through service discovery at lines 14-22, which keeps Web independent of the API assembly.
* `src/TNC.Trading.Platform.Web/Program.cs:27-77` contains a root-route authentication/session workflow directly in middleware. This is framework-specific policy, but its size and sign-out/redirect/token decisions make ownership ambiguous between the Web adapter and a dedicated authentication coordinator.

### API adapter and use-case dispatch

* `src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs:22-52` groups HTTP routes, assigns authorization policies, and keeps endpoint registration in an interface-adapter module rather than `Program.cs`.
* `src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs:57-92` shows representative query and command dispatch: endpoint methods construct Application request records, invoke feature handlers, then map Application responses to API response contracts. This is a clear interface-adapter boundary.
* `src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs:94-103` maps query parameters into the events use case and maps its response outward, following the same boundary.
* `src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs:116-132` is an exception: the auth-audit endpoint injects `PlatformConfigurationService` and `IPlatformEventStore` directly and delegates HTTP principal/context plus those dependencies to an endpoint handler. The API adapter therefore coordinates application capabilities without a dedicated Application request/handler, weakening use-case ownership and making the HTTP boundary part of the workflow contract.

### Preliminary naming assessment

* `Application` approximates both the entities and use-cases circles because no separate Domain project exists. Its feature handlers fit use cases, while configuration/state records and transition policies act as enterprise/application policy.
* `Api` and `Web` approximate interface adapters plus framework drivers. They are not pure interface-adapter projects because they also contain executable host composition and middleware.
* `Infrastructure` approximates frameworks and drivers and correctly implements inward interfaces, although some framework-independent adapter policy also lives there.
* `AppHost` and `ServiceDefaults` are deployment/framework support projects with no direct canonical Clean Architecture circle.
* The project naming is therefore useful but approximate, not a one-to-one mapping to entities, use cases, interface adapters, and frameworks.

### Representative configuration mutation path

* `src/TNC.Trading.Platform.Web/Components/Pages/Configuration.razor:1-3` makes the Blazor route an authorized Operator view and injects a presenter plus navigation-access coordinator. Lines 180-228 load and save through the presenter, but pass `CancellationToken.None`, so circuit/request cancellation does not propagate into the use case.
* `src/TNC.Trading.Platform.Web/Components/Pages/ConfigurationPagePresenter.cs:5-42` translates page actions into API-client operations and translates transport/format/scope failures into UI result records. This is presentation logic in an appropriate adapter layer.
* `src/TNC.Trading.Platform.Web/PlatformApiClient.cs:27-57` creates bearer-authorized GET/PUT requests for the configuration resource and serializes/deserializes Web-owned view models. Lines 125-135 centralize access-token acquisition and HTTP authorization. The UI communicates with API over HTTP rather than referencing the API assembly.
* `src/TNC.Trading.Platform.Api/Features/Platform/UpdatePlatformConfigurationEndpointHandler.cs:8-30` validates the API request, maps it inward, invokes the Application handler, and maps the result outward. This is a focused controller/interface-adapter role.
* `src/TNC.Trading.Platform.Api/Features/UpdatePlatformConfiguration/UpdatePlatformConfigurationMapping.cs:1-62` explicitly converts API contracts to Application configuration models and back to API response contracts. The duplication is purposeful boundary isolation, not accidental model sharing.
* `src/TNC.Trading.Platform.Application/Features/UpdatePlatformConfiguration/UpdatePlatformConfigurationRequest.cs:1-5` wraps the application-level update model in a use-case request. `src/TNC.Trading.Platform.Application/Features/UpdatePlatformConfiguration/UpdatePlatformConfigurationHandler.cs:1-16` sequences persistence and a state-coordinator tick, so the principal command workflow is inward.
* `src/TNC.Trading.Platform.Application/Services/PlatformConfigurationService.cs:1-22` merely forwards every operation to `IPlatformConfigurationStore`. `src/TNC.Trading.Platform.Application/Services/IPlatformConfigurationStore.cs:1-17` gives the inward layer a persistence port, but the port is coarse-grained and includes startup application, runtime fallback, update behavior, and result policy.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/SqlPlatformConfigurationStore.cs:40-116` decides restart behavior, mutates configuration, updates protected credentials, creates audit semantics and summaries, redacts details, persists, and returns an Application result. This adapter does substantially more than translate/persist and therefore owns application policy that should be stable inward.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/PlatformConfigurationRestartPolicy.cs:1-14` places the definition of which settings require restart in Infrastructure and couples that policy simultaneously to an EF entity and an Application update model. A database representation change can therefore force a policy change, contrary to the desired independence of business rules from persistence details.
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/SqlPlatformConfigurationStoreTests.cs:77-139` verifies restart, audit, and secret-redaction requirements against the concrete SQL store. The behavior is protected, but the test location confirms that these requirements are treated as infrastructure behavior rather than application policy.

### Domain and policy location

* `src/TNC.Trading.Platform.Application/Services/PlatformStateTransitionEngine.cs:1-50` contains deterministic authentication/session transition policy, including schedule, live-environment, expiry, retry, credential, and active/degraded decisions. This is a strong inward policy object with no infrastructure dependency.
* `src/TNC.Trading.Platform.Application/Services/TradingScheduleGate.cs:1-56` contains deterministic trading-calendar policy and depends only on Application configuration records plus BCL time-zone APIs. This is also appropriately inward.
* `src/TNC.Trading.Platform.Application/Services/PlatformStateCoordinator.cs:11-29` is the main application orchestrator and receives ports for state, retries, events, notifications, IG, credentials, and proof data. It also depends directly on `IConfiguration` and `ILogger`, which couples the use-case layer to host configuration and logging frameworks.
* `src/TNC.Trading.Platform.Application/Services/PlatformStateCoordinator.cs:31-72` composes current state and configuration into an Application status projection. Lines 74-128 enforce manual-retry policy and coordinate side effects. Lines 130-180 run the state transition decision and save state, demonstrating substantial use-case ownership in Application.
* `src/TNC.Trading.Platform.Application/Services/PlatformStateCoordinator.cs:350-425` coordinates IG authentication, sanitization, snapshot/proof capture, state mutation, retry-cycle updates, event creation, and recovery notification. This inward orchestration is consistent with a use-case layer, although the class has a high responsibility and dependency count.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/NotificationDispatchPolicy.cs:1-25` selects recipient/provider behavior and redaction in Infrastructure. Like restart policy, this is an ambiguous ownership choice because notification behavior is application policy even though concrete transports belong outward.
* No separate Domain/Entities project appears in the solution (`TNC.Trading.Platform.slnx:1-29`). Representative state/configuration records such as `src/TNC.Trading.Platform.Application/Configuration/PlatformConfigurationSnapshot.cs:1-16` and policy classes above live in Application. This can be a pragmatic architecture for a small system, but it means enterprise rules and application orchestration have no compile-time boundary from one another.

### Infrastructure ports and adapters

* `src/TNC.Trading.Platform.Application/Services/IPlatformConfigurationStore.cs:1-17` and `src/TNC.Trading.Platform.Application/Configuration/IProtectedCredentialService.cs:1-17` are inward ports. `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/PlatformInfrastructureServiceCollectionExtensions.cs:39-51` binds them and the runtime/event/retry/login/notification ports to concrete Infrastructure implementations.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/ProtectedCredentialService.cs:1-81` implements credential persistence and encryption with EF Core and ASP.NET Data Protection. It returns inward `CredentialPresence`/`IgCredentials` models and does not leak EF entities, a strong adapter boundary.
* `src/TNC.Trading.Platform.Application/Infrastructure/Ig/IIgSessionClient.cs:1-19` defines the IG gateway port inward, but its namespace and folder are named `Application.Infrastructure.Ig`, and its methods expose IG-shaped authenticate/accounts/positions contracts. This is dependency inversion at assembly level with ambiguous conceptual ownership: the interfaces belong inward, while broker wire vocabulary is allowed to shape the use-case boundary.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/Ig/IgSessionClient.cs:1-150` translates HTTP status, headers, and JSON into the inward IG contracts. Lines 152-186 keep raw deserialization DTOs private to the adapter, which is a strong anti-corruption detail even though higher-level contracts remain broker-shaped.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/PlatformDbContext.cs:1-114` contains EF sets and all relational mappings. Persistence entities remain in Infrastructure and do not appear in API/Web contracts, preserving the central model from EF annotations and database APIs.

### Authentication and configuration boundaries

* `src/TNC.Trading.Platform.Application/Authentication/PlatformAuthorizationPolicyRegistration.cs:1-35` directly consumes ASP.NET `AuthorizationOptions` and registers role policies. This is the concrete source-level reason Application references `Microsoft.AspNetCore.App`; shared policy consistency is gained by moving framework configuration into the inner assembly.
* `src/TNC.Trading.Platform.Api/Authentication/PlatformApiAuthenticationServiceCollectionExtensions.cs:1-68` binds shared options and configures JWT bearer validation in the API adapter. `src/TNC.Trading.Platform.Web/Authentication/PlatformWebAuthenticationServiceCollectionExtensions.cs:1-96` binds the same shared options and configures cookie/OIDC behavior in the Web adapter. Provider-specific framework setup is correctly outer-layer code.
* `src/TNC.Trading.Platform.Web/Authentication/PlatformWebAuthenticationServiceCollectionExtensions.cs:98-163` dynamically adds scopes and records a sign-in audit through an HTTP client from the OIDC ticket event. The audit failure is deliberately non-blocking, but authentication callback behavior, API communication, and operational auditing are coupled in one framework registration module.
* `src/TNC.Trading.Platform.Api/Features/Platform/RecordAuthAuditEventEndpointHandler.cs:1-56` validates an adapter request, reads claims/HTTP trace IDs, fetches configuration, constructs an Application event record, and writes directly to `IPlatformEventStore`. This is a missing application use-case boundary: HTTP concepts and workflow orchestration coexist in the endpoint handler.
* `src/TNC.Trading.Platform.AppHost/AppHostInfrastructureRegistration.cs:1-42` externalizes SQL, Mailpit, and Keycloak resource composition. `src/TNC.Trading.Platform.AppHost/AppHostProjectRegistration.cs:7-48` composes API and Web runtime references and readiness waits. These are appropriate framework/deployment responsibilities.
* `src/TNC.Trading.Platform.AppHost/AppHostEnvironmentWiring.cs:7-31` supplies API/Web authentication configuration, while lines 84-100 supply notification transport configuration. This keeps environment wiring in the deployment host, but lines 17-29 embed a seeded local-user password as a normal environment value, creating secret/configuration ambiguity even if intended only for local Keycloak seeding.
* `src/TNC.Trading.Platform.AppHost/AppHostInfrastructureRegistration.cs:13-16` deliberately configures a persistent SQL volume, while `src/TNC.Trading.Platform.Api/Program.cs:25-37` deletes and recreates the SQL database on every Development startup. Those choices conflict operationally: local persistence is retained at the container level but destroyed by the API host, risking unexpected data loss and hiding migration compatibility problems.

### Test architecture

* `TNC.Trading.Platform.slnx:9-28` separates API unit/integration, AppHost unit, Application unit, Infrastructure unit, and Web unit/functional/E2E projects. This is a strong layered test portfolio.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/PlatformApiClientTests.cs:8-41` tests Web-to-API contract parsing and bearer-token dispatch. Lines 103-160 test the configuration mutation boundary, including HTTP method, route, serialized secret input, and actor identity. Lines 213-266 verify failure behavior for unauthorized, forbidden, and validation responses.
* `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/PlatformEndpointHandlerTests.cs:10-56` isolates validation, conflict mapping, and unsupported audit-event behavior at endpoint handlers. This is useful adapter-level testing, although no test in that file exercises successful auth-audit orchestration.
* `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/AuthRetryCycleTests.cs:12-64` constructs the application coordinator with concrete EF, notification, credential, and IG adapters. Lines 66-120 verify missing-credential behavior through persisted infrastructure outputs. These are valuable component tests but are mislabeled as pure Application unit tests.
* `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/TNC.Trading.Platform.Application.UnitTests.csproj:21-24` references both Application and Infrastructure. `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/ApplicationReflection.cs:1-24` directly creates an EF `PlatformDbContext` and Data Protection provider. This test dependency weakens the ability to prove that Application use cases remain independently testable through ports.
* No architecture-testing package is present in the project package references shown by the test project files, and the solution currently relies on ordinary compiler references rather than an explicit dependency-rule test. The production graph is currently mostly correct, but there is no automated guard against a future Application-to-Infrastructure project reference or forbidden framework namespace.
* `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/PlatformAuthenticationIntegrationTests.cs:17-98` exercises anonymous health access, protected API challenges, real Keycloak viewer access, and role denial. Lines 100-147 verify auth-audit persistence through the HTTP boundary, compensating for the missing successful unit-level audit-handler test.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/Authentication/PlatformProtectedRouteFunctionalTests.cs:5-39` exercises a real Keycloak-backed insufficient-role redirect. `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/Authentication/PlatformDashboardAuthenticationE2ETests.cs:7-50` exercises one real browser sign-in and protected page render.
* `test/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.UnitTests/AppHostCompositionTests.cs:23-100` asserts the assembled resource graph, waits, endpoints, and links, giving the deployment composition root focused coverage.

### Boundary visibility and dependency enforcement

* `src/TNC.Trading.Platform.Application/Properties/InternalsVisibleTo.cs:1-6` grants API, Infrastructure, and two test assemblies friend access to all Application internal types. `src/TNC.Trading.Platform.Infrastructure/Properties/InternalsVisibleTo.cs:1-5` likewise grants API and test assemblies friend access to Infrastructure internals.
* Most use-case handlers, ports, and policy models are internal, while shared authentication options/defaults are public. Friend assemblies reduce accidental public API exposure, but they do not create narrow architectural contracts: API and Infrastructure can reach any Application internal type, and API can reach any Infrastructure internal type. This weakens compiler-enforced boundaries inside the otherwise correct project graph.
* `src/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.csproj:9-16` declares Azure Communication Services, Azure Identity, EF InMemory, and EF SQL packages. A source scan found no Azure namespace use in API and only the direct EF provider-name/database startup workflow in `src/TNC.Trading.Platform.Api/Program.cs:18-39`. The Azure packages and EF InMemory package appear stale or over-broad at the host layer.

## Overall assessment

The repository is materially aligned with Clean Architecture, but not strictly mapped to Martin's four circles. Its strongest property is the production project-reference direction: Application is the innermost project, Infrastructure depends inward on it, and executable hosts compose outward dependencies. Representative API and Blazor flows use explicit boundary contracts and mappings, EF entities stay outward, and deployment concerns are isolated in AppHost/ServiceDefaults.

The alignment is weakened by framework and policy leakage rather than by a reversed production project reference. Application combines enterprise policy, use cases, shared host authentication contracts, hosted-service registration, configuration access, and logging. Infrastructure owns several business decisions. API sometimes bypasses use-case handlers and directly orchestrates stores/services. Broad `InternalsVisibleTo` grants and the lack of architecture tests mean the intended boundaries are conventions rather than strongly enforced contracts.

### Strengths

* Correct principal production dependency direction: `Infrastructure -> Application`, with no `Application -> Infrastructure` project reference (`src/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.csproj:13-15`; `src/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.csproj:1-13`).
* Explicit API/Application DTO mapping and separate Web transport view models (`src/TNC.Trading.Platform.Api/Features/UpdatePlatformConfiguration/UpdatePlatformConfigurationMapping.cs:1-62`; `src/TNC.Trading.Platform.Web/PlatformApiClient.cs:27-57`).
* Inward ports with outer EF, encryption, HTTP, and notification adapters (`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/PlatformInfrastructureServiceCollectionExtensions.cs:39-65`).
* Deterministic transition and trading-schedule policy inside Application (`src/TNC.Trading.Platform.Application/Services/PlatformStateTransitionEngine.cs:1-50`; `src/TNC.Trading.Platform.Application/Services/TradingScheduleGate.cs:1-56`).
* EF entities and mappings remain in Infrastructure (`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/PlatformDbContext.cs:1-114`).
* AppHost externalizes deployment topology and Web communicates with API over HTTP rather than by assembly reference (`src/TNC.Trading.Platform.AppHost/AppHostProjectRegistration.cs:7-48`; `src/TNC.Trading.Platform.Web/Program.cs:14-22`).
* Layered tests cover unit, adapter, distributed integration, functional, and browser paths (`TNC.Trading.Platform.slnx:9-28`).

### Gaps and ambiguous ownership

* There is no separate Domain/Entities assembly; Application owns both policy/state models and use-case orchestration (`TNC.Trading.Platform.slnx:1-29`; `src/TNC.Trading.Platform.Application/Configuration/PlatformConfigurationSnapshot.cs:1-16`). This is not inherently wrong, but the project name does not map cleanly to one circle.
* Application is ASP.NET-aware through authorization registration and hosted-service/DI registration (`src/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.csproj:9-11`; `src/TNC.Trading.Platform.Application/Authentication/PlatformAuthorizationPolicyRegistration.cs:1-35`; `src/TNC.Trading.Platform.Application/Services/PlatformApplicationServiceCollectionExtensions.cs:11-27`).
* Application orchestration reads `IConfiguration` and logging directly (`src/TNC.Trading.Platform.Application/Services/PlatformStateCoordinator.cs:11-29`) instead of receiving application-specific settings and telemetry ports.
* Restart, audit-summary, and notification-dispatch policy live in Infrastructure (`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/SqlPlatformConfigurationStore.cs:40-116`; `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/NotificationDispatchPolicy.cs:1-25`).
* The IG inward port uses broker-specific contracts under an `Application.Infrastructure` namespace (`src/TNC.Trading.Platform.Application/Infrastructure/Ig/IIgSessionClient.cs:1-19`), making the anti-corruption boundary partial rather than complete.
* The auth-audit endpoint lacks an Application request/handler and directly coordinates a service and store from HTTP (`src/TNC.Trading.Platform.Api/Features/Platform/RecordAuthAuditEventEndpointHandler.cs:9-56`).
* Broad friend-assembly access substitutes for narrow public ports (`src/TNC.Trading.Platform.Application/Properties/InternalsVisibleTo.cs:1-6`; `src/TNC.Trading.Platform.Infrastructure/Properties/InternalsVisibleTo.cs:1-5`).

### Concrete risks

* High: Development startup deletes the SQL database while AppHost configures persistent SQL storage, causing surprising local data loss and preventing migration-compatibility feedback (`src/TNC.Trading.Platform.Api/Program.cs:25-37`; `src/TNC.Trading.Platform.AppHost/AppHostInfrastructureRegistration.cs:13-16`).
* Medium: Business rules in `SqlPlatformConfigurationStore` can change accidentally with EF/persistence refactors because policy and persistence are co-located (`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/SqlPlatformConfigurationStore.cs:40-116`).
* Medium: `PlatformStateCoordinator` has at least thirteen injected dependencies plus direct configuration/logging and handles status projection, retry policy, IG login, persistence, events, and notifications, increasing change coupling (`src/TNC.Trading.Platform.Application/Services/PlatformStateCoordinator.cs:11-29`; `src/TNC.Trading.Platform.Application/Services/PlatformStateCoordinator.cs:31-180`).
* Medium: No automated dependency-rule test prevents future inward-layer references to Infrastructure or additional ASP.NET/EF dependencies (project references in `src/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.csproj:1-13`; test references in `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/TNC.Trading.Platform.Application.UnitTests.csproj:21-24`).
* Medium: The local seeded password is embedded in AppHost environment defaults and repeated in a browser test, increasing the chance that a development credential becomes normalized outside its intended boundary (`src/TNC.Trading.Platform.AppHost/AppHostEnvironmentWiring.cs:17-29`; `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/Authentication/PlatformDashboardAuthenticationE2ETests.cs:36-38`).
* Low to medium: Application tests rely on concrete Infrastructure, so replacing adapters or tightening visibility will cause broad test churn and pure use-case testability is not demonstrated (`test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/AuthRetryCycleTests.cs:12-64`; `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/ApplicationReflection.cs:1-24`).
* Low: API declares unused Azure packages and an apparently unused EF InMemory package, increasing restore/security-update surface and obscuring ownership (`src/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.csproj:9-16`; API source usage at `src/TNC.Trading.Platform.Api/Program.cs:18-39`).
* Low: Blazor configuration actions use `CancellationToken.None`, so disconnect/navigation cancellation does not flow through the HTTP and use-case path (`src/TNC.Trading.Platform.Web/Components/Pages/Configuration.razor:180-228`).

## Unresolved gaps

* This representative review did not inspect every handler, store, entity, page, or test. Uninspected slices may contain additional boundary exceptions.
* No runtime test suite was executed because the request was research-only and source/test inspection already established the architectural evidence. Test health remains unverified in this session.
* The intended long-term choice between a pragmatic three-layer architecture and a strict Domain/Application/Adapters/Frameworks split is not stated in an architecture decision record. Code alone cannot resolve whether the current approximation is deliberate.
* The operational intent behind Development database deletion versus persistent AppHost storage cannot be resolved from code alone.
* The intended security lifecycle of `Authentication__Keycloak__SeededUserPassword` cannot be resolved from code alone.

## Recommended next research

* Confirm with maintainers whether strict Clean Architecture is a target or whether the current Application/Infrastructure/host split is the accepted model.
* Trace all Infrastructure classes that contain `Policy`, `Processor`, or workflow semantics and classify each decision as application policy versus adapter mechanics.
* Inventory all direct `IConfiguration`, `IOptions`, `ILogger`, ASP.NET, EF, Azure, and HTTP dependencies in Application and define an explicit allowed-dependency rule set.
* Prototype an architecture test that asserts project references, namespaces, and forbidden dependencies without changing production behavior.
* Evaluate a narrow Contracts/Auth shared assembly versus moving ASP.NET policy registration to each outer host, measuring whether either option meaningfully improves dependency direction.
* Investigate the development database lifecycle and the seeded Keycloak credential with the maintainers before proposing mitigation.
* Run the existing layered test projects after any future boundary refactor, with special attention to real Keycloak integration and Playwright smoke coverage.
