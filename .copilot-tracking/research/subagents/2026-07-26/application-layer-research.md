---
title: Application Layer Research
description: Evidence-based inventory and architectural assessment of the Application project
author: GitHub Copilot
ms.date: 2026-07-26
ms.topic: reference
---

## Research scope

* Inventory folders, top-level types, namespaces, responsibilities, dependencies,
  CQRS-style operations, ports, models, validation, and DTOs in
  `src/TNC.Trading.Platform.Application`.
* Trace representative operations through inbound callers and outbound adapter
  implementations, including corresponding tests.
* Classify code as domain policy, application orchestration, contract, port, or
  misplaced adapter/framework concern.
* Evaluate the implementation against
  `.github/instructions/architecture.instructions.md` and repository conventions.
* Recommend concrete, incremental folder, project, and naming changes without
  modifying production files.

## Status

Complete.

## Initial evidence and hypotheses

The Application project contains 76 C# source files excluding generated output:
4 under `Authentication`, 26 under `Configuration`, 18 under `Features`, 11
under `Infrastructure/Ig`, 16 under `Services`, and 1 under `Properties`.
There are six feature folders and 18 feature files, with one request, response,
and handler in each folder. No Application validator file was found.

The project has no package or project references, but it references the complete
ASP.NET Core shared framework. Evidence:
`src/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.csproj:1-13`.
The working hypothesis is that this reference supports misplaced authorization,
dependency-injection, hosting, configuration, and logging concerns rather than
domain or use-case requirements. The hypothesis will be tested by tracing every
framework import and determining whether it reaches policy decisions.

The second working hypothesis is that the feature handlers provide CQRS-shaped
entry points but delegate most policy and orchestration to shared services,
especially `PlatformStateCoordinator`. The hypothesis is weakened if the
representative handlers own cohesive use-case decisions and depend only on
narrow, feature-relevant ports.

The third working hypothesis is that `Infrastructure/Ig` combines an inward-owned
IG session port with provider wire contracts and mapping/sanitization code. The
port belongs inward, while provider response models and response sanitization
belong in an outbound adapter. This will be tested against concrete implementations
and all call sites.

## Inventory

### Project and namespace shape

The 76 files declare 82 top-level types. The excess comes from
`PlatformAuthSupervisorRunner.cs`, which declares two interfaces and two
classes, and `PlatformStateTransitionEngine.cs`, which declares a class, enum,
and record. This conflicts with the one-top-level-type-per-file repository rule.
Evidence: `.github/copilot-instructions.md:26-29`;
`.github/instructions/csharp.instructions.md:18-29`;
`src/TNC.Trading.Platform.Application/Services/PlatformAuthSupervisorRunner.cs:6-47`;
`src/TNC.Trading.Platform.Application/Services/PlatformStateTransitionEngine.cs:5-75`.

The namespaces follow technical buckets rather than capabilities:

* `Application.Authentication` contains four public shared host configuration
  types: defaults, options, configuration resolution, and ASP.NET Core policy
  registration. Evidence:
  `src/TNC.Trading.Platform.Application/Authentication/PlatformAuthenticationDefaults.cs:1-45`;
  `src/TNC.Trading.Platform.Application/Authentication/PlatformAuthenticationOptions.cs:1-174`;
  `src/TNC.Trading.Platform.Application/Authentication/PlatformAuthenticationConfigurationResolver.cs:1-141`;
  `src/TNC.Trading.Platform.Application/Authentication/PlatformAuthorizationPolicyRegistration.cs:1-35`.
* `Application.Configuration` contains 26 files and 26 internal types: 7 enums,
  16 records, 2 mutable classes, and 1 port. The name is misleading because the
  folder also owns runtime auth state, retry state, event write/read models, IG
  login/proof projections, use-case inputs, and use-case results.
* `Application.Features.<Operation>` contains six operation folders:
  `GetIgLoginHistory`, `GetPlatformConfiguration`, `GetPlatformEvents`,
  `GetPlatformStatus`, `TriggerManualAuthRetry`, and
  `UpdatePlatformConfiguration`. Each has exactly one request, response, and
  handler. Evidence:
  `src/TNC.Trading.Platform.Application/Features/GetPlatformStatus/GetPlatformStatusRequest.cs:1-3`;
  `src/TNC.Trading.Platform.Application/Features/GetPlatformStatus/GetPlatformStatusResponse.cs:1-5`;
  `src/TNC.Trading.Platform.Application/Features/GetPlatformStatus/GetPlatformStatusHandler.cs:1-12`.
* `Application.Infrastructure.Ig` contains one outward port plus ten
  provider-oriented request, response, projection, mapping, and sanitization
  types. Its `Infrastructure` name inside the inner project accurately signals
  adapter concepts but not correct responsibility placement.
* `Application.Services` contains seven store/notification ports, six
  orchestration or policy services, one hosted framework service, one DI
  registration class, and hosted-runner/delay support types.
* `Application.Properties` contains friend-assembly declarations for API,
  Application tests, Infrastructure tests, and Infrastructure. Evidence:
  `src/TNC.Trading.Platform.Application/Properties/InternalsVisibleTo.cs:1-6`.

All production namespaces are file-scoped. All top-level files except the two
multi-type service files match their primary top-level type. Most Application
types are `internal`; the four authentication host-sharing types are public.
Friend assemblies provide API and Infrastructure access to internal operations,
ports, and contracts rather than exposing a deliberate public boundary.

### CQRS-style operations and contracts

The six operation folders satisfy the required naming and one-request,
one-response, one-handler shape. All request and response types are immutable
records. Five handlers delegate one call and wrap its result; only
`GetIgLoginHistoryHandler` coordinates two dependencies. Evidence:
`src/TNC.Trading.Platform.Application/Features/GetPlatformConfiguration/GetPlatformConfigurationHandler.cs:5-12`;
`src/TNC.Trading.Platform.Application/Features/GetPlatformEvents/GetPlatformEventsHandler.cs:5-12`;
`src/TNC.Trading.Platform.Application/Features/TriggerManualAuthRetry/TriggerManualAuthRetryHandler.cs:5-12`;
`src/TNC.Trading.Platform.Application/Features/UpdatePlatformConfiguration/UpdatePlatformConfigurationHandler.cs:5-15`;
`src/TNC.Trading.Platform.Application/Features/GetIgLoginHistory/GetIgLoginHistoryHandler.cs:5-17`.

The contracts are inward-owned but often wrap shared `Configuration` models
without defining operation-specific boundary data. For example,
`UpdatePlatformConfigurationRequest` wraps `PlatformConfigurationUpdate`, and
its response wraps `UpdatePlatformConfigurationResult`. Evidence:
`src/TNC.Trading.Platform.Application/Features/UpdatePlatformConfiguration/UpdatePlatformConfigurationRequest.cs:1-5`;
`src/TNC.Trading.Platform.Application/Features/UpdatePlatformConfiguration/UpdatePlatformConfigurationResponse.cs:1-5`.
This is structurally compliant but leaves the feature coupled to a broad
cross-feature model bucket.

`GetPlatformStatus` and `GetPlatformEvents` are named as queries but call
`TickAsync` before reading data. Evidence:
`src/TNC.Trading.Platform.Application/Services/PlatformStateCoordinator.cs:31-77`.
They can persist runtime state, call IG, emit events, and send notifications.
The names and HTTP verbs therefore promise read semantics that the operations
do not provide.

### Ports and dependencies

Application owns nine outward ports:

* `IPlatformConfigurationStore` for startup application, current/runtime reads,
  and updates
* `IPlatformRuntimeStateStore` for loading and saving runtime state
* `IPlatformRetryCycleStore` for retry-cycle upsert
* `IPlatformEventStore` for event reads and writes
* `IPlatformIgLoginSnapshotStore` for snapshot capture and reads
* `IPlatformIgProofDataStore` for proof snapshot reads and writes
* `INotificationDispatcher` for four notification intents
* `IProtectedCredentialService` for protected credential presence, retrieval,
  and update
* `IIgSessionClient` for authentication, account reads, and position reads

Representative definitions:
`src/TNC.Trading.Platform.Application/Services/IPlatformConfigurationStore.cs:1-16`;
`src/TNC.Trading.Platform.Application/Services/IPlatformRuntimeStateStore.cs:1-10`;
`src/TNC.Trading.Platform.Application/Services/IPlatformEventStore.cs:1-10`;
`src/TNC.Trading.Platform.Application/Services/INotificationDispatcher.cs:1-14`;
`src/TNC.Trading.Platform.Application/Configuration/IProtectedCredentialService.cs:1-16`;
`src/TNC.Trading.Platform.Application/Infrastructure/Ig/IIgSessionClient.cs:1-19`.

Infrastructure registers concrete implementations for all nine ports. Evidence:
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/PlatformInfrastructureServiceCollectionExtensions.cs:43-67`.
This is valid Dependency Inversion at the project boundary. Port placement is
less cohesive: seven are in `Services`, credential protection is in
`Configuration`, and IG is in `Infrastructure/Ig`.

The persistence ports use inward-owned models, but `IIgSessionClient` leaks
provider protocol details inward: raw CST/security tokens, API keys, response
headers, and provider response shapes cross the port. Evidence:
`src/TNC.Trading.Platform.Application/Infrastructure/Ig/IIgSessionClient.cs:3-19`;
`src/TNC.Trading.Platform.Application/Infrastructure/Ig/IgAuthenticateResponse.cs:1-9`.
`IPlatformConfigurationStore` also combines four reasons to change: startup
bootstrap, operator query, runtime fallback resolution, and update persistence.

### Models, value objects, DTOs, and validation

The `Configuration` types split into four practical groups:

* Policy vocabulary: `PlatformEnvironmentKind`, `BrokerEnvironmentKind`,
  `WeekendBehavior`, `PlatformSessionStatus`, `AuthRetryPhase`,
  `IgLoginSnapshotKind`, and schedule/retry configuration
* Mutable state/persistence-shaped data: `PlatformRuntimeState` and
  `PlatformRetryCycle`
* Use-case input/output and projections: `PlatformConfigurationUpdate`,
  `PlatformConfigurationSnapshot`, `UpdatePlatformConfigurationResult`,
  `ManualRetryResult`, `PlatformStatusModel`, `OperationalEventModel`, and IG
  status/history/proof models
* Port data: `IgCredentials`, `CredentialPresence`, and `PlatformEventRecord`

The records are immutable data carriers, but they are not value objects with
enforced invariants. `TradingScheduleConfiguration` accepts an end before a
start, empty trading days, and arbitrary time-zone strings; retry configuration
accepts non-positive and inconsistent values. Evidence:
`src/TNC.Trading.Platform.Application/Configuration/TradingScheduleConfiguration.cs:1-9`;
`src/TNC.Trading.Platform.Application/Configuration/RetryPolicyConfiguration.cs:1-8`.
`PlatformRuntimeState` and `PlatformRetryCycle` expose public setters for every
field and do not guard valid transitions. Evidence:
`src/TNC.Trading.Platform.Application/Configuration/PlatformRuntimeState.cs:1-44`;
`src/TNC.Trading.Platform.Application/Configuration/PlatformRetryCycle.cs:1-34`.

No Application validator exists. External update validation resides in the API
adapter, including cross-field Test/Live and schedule policy. Application
handlers do not validate requests, so non-HTTP callers can construct invalid
models. The manual-retry handler relies on exceptions from the coordinator for
eligibility decisions.

The feature requests/responses are application operation contracts, not HTTP
DTOs. API and Web define separate transport/view models and map at their
boundaries. The `Ig*Response` types under `Application.Infrastructure.Ig`, by
contrast, model provider wire responses and are adapter contracts misplaced in
the inner assembly.

### Framework dependencies and composition

The ASP.NET Core shared framework supports five kinds of outer concern in the
Application assembly:

* ASP.NET Core authorization registration through `AuthorizationOptions`
* Microsoft dependency injection registration and service location
* `BackgroundService` hosting
* `IConfiguration` lookup inside the central coordinator
* `ILogger<T>` in orchestration helpers

Evidence:
`src/TNC.Trading.Platform.Application/Authentication/PlatformAuthorizationPolicyRegistration.cs:1-35`;
`src/TNC.Trading.Platform.Application/Services/PlatformApplicationServiceCollectionExtensions.cs:1-28`;
`src/TNC.Trading.Platform.Application/Services/PlatformAuthSupervisor.cs:1-48`;
`src/TNC.Trading.Platform.Application/Services/PlatformAuthSupervisorRunner.cs:1-47`;
`src/TNC.Trading.Platform.Application/Services/PlatformStateCoordinator.cs:1-28,677-684`.
These are not required by the pure schedule or transition policy and prevent the
Application assembly from being framework-neutral.

### Tests

The Application unit-test project contains eight C# files. It directly covers
schedule evaluation, retry-delay calculation, transition decisions, coordinator
workflows, hosted supervisor looping, IG response sanitization, IG login mapping,
and proof-data behavior. Representative policy evidence:
`test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/TradingScheduleGateTests.cs:1-145`;
`test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/RetryPolicyTimingTests.cs:1-64`.

No test directly constructs any of the six feature handlers, and there are no
Application validator tests because no validator exists. This conflicts with
the architecture instruction to unit test handlers and validators directly.
Evidence: `.github/instructions/architecture.instructions.md:70-72`.

The test project references both Application and Infrastructure. Evidence:
`test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/TNC.Trading.Platform.Application.UnitTests.csproj:18-25`.
Coordinator tests instantiate `PlatformDbContext`, EF stores, Data Protection,
the SQL configuration store, and concrete notification adapters. Evidence:
`test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/AuthRetryCycleTests.cs:1-46`;
`test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/IgProofDataTests.cs:1-15,188-261`;
`test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/ApplicationReflection.cs:1-24`.
Those tests exercise useful behavior, but they are adapter-integrated tests in a
nominal unit project and cannot prove that the use cases are independently
testable through narrow fakes.

## Representative operation traces

### Update platform configuration

```text
Configuration.razor SaveAsync
  -> ConfigurationPagePresenter.SaveAsync
  -> PlatformApiClient PUT /api/platform/configuration
  -> PlatformEndpoints.UpdatePlatformConfigurationAsync
  -> UpdatePlatformConfigurationEndpointHandler
      -> API UpdatePlatformConfigurationValidator
      -> API-to-Application mapping
  -> Application UpdatePlatformConfigurationHandler
      -> PlatformConfigurationService.UpdateAsync
      -> IPlatformConfigurationStore.UpdateAsync
      -> SqlPlatformConfigurationStore.UpdateAsync
          -> PlatformConfigurationRestartPolicy
          -> IProtectedCredentialService.UpdateAsync
          -> configuration audit creation
          -> DbContext.SaveChangesAsync
      -> PlatformStateCoordinator.TickAsync
          -> policy decision
          -> IG/notification/event/snapshot/retry/runtime ports
  -> Application-to-API mapping
  -> Web view model and form mapping
```

The Blazor page manages UI state at
`src/TNC.Trading.Platform.Web/Components/Pages/Configuration.razor:181-230`.
The presenter and typed HTTP client translate UI intent into a PUT request at
`src/TNC.Trading.Platform.Web/Components/Pages/ConfigurationPagePresenter.cs:20-42`
and `src/TNC.Trading.Platform.Web/PlatformApiClient.cs:41-54`. The Minimal API
endpoint validates, maps, dispatches one handler, and maps the result at
`src/TNC.Trading.Platform.Api/Features/Platform/UpdatePlatformConfigurationEndpointHandler.cs:10-29`
and
`src/TNC.Trading.Platform.Api/Features/UpdatePlatformConfiguration/UpdatePlatformConfigurationMapping.cs:8-66`.

Validation includes input shape plus genuine policy: invalid schedule/retry
combinations and Test-platform/Live-broker incompatibility. Evidence:
`src/TNC.Trading.Platform.Api/Features/UpdatePlatformConfiguration/UpdatePlatformConfigurationValidator.cs:7-92`.
The handler then performs only persistence plus reconciliation. Evidence:
`src/TNC.Trading.Platform.Application/Features/UpdatePlatformConfiguration/UpdatePlatformConfigurationHandler.cs:5-15`.

The concrete store stages configuration, protected credentials, and audit data
against one scoped `DbContext`, then commits them together. Evidence:
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/SqlPlatformConfigurationStore.cs:45-115`;
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/ProtectedCredentialService.cs:27-91`.
The restart decision is business/application policy but currently compares an EF
entity directly in Infrastructure. Evidence:
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/PlatformConfigurationRestartPolicy.cs:1-13`.

After that commit, the handler invokes `TickAsync`. Retry cycles, operational
events, notifications, snapshots, and runtime state each save through separate
adapter calls. Representative evidence:
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/EfPlatformRetryCycleStore.cs:8-51`;
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/EfPlatformEventStore.cs:38-56`;
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/EfPlatformRuntimeStateStore.cs:30-62`.
The whole command is therefore not atomic. A reconciliation failure can leave
the configuration committed with partially applied runtime side effects.

### Trigger manual authentication retry

```text
StatusPagePresenter.TriggerManualRetryAsync
  -> PlatformApiClient POST /api/platform/auth/manual-retry
  -> TriggerManualAuthRetryEndpointHandler
      -> Application TriggerManualAuthRetryHandler
      -> PlatformStateCoordinator.TriggerManualRetryAsync
          -> load runtime state and runtime configuration
          -> evaluate schedule and manual-retry eligibility
          -> persist retry cycle, operational event, and runtime state
          -> obtain protected credentials
          -> IIgSessionClient.AuthenticateAsync
          -> capture sanitized login snapshot and proof data
          -> emit recovery/failure events and notifications
          -> persist final runtime state
  -> HTTP 202 Accepted or 409 Conflict
```

The Web call and HTTP endpoint translation are at
`src/TNC.Trading.Platform.Web/Components/Pages/StatusPagePresenter.cs:22-34`,
`src/TNC.Trading.Platform.Web/PlatformApiClient.cs:57-70`, and
`src/TNC.Trading.Platform.Api/Features/Platform/TriggerManualAuthRetryEndpointHandler.cs:8-36`.
The Application handler adds no policy. Evidence:
`src/TNC.Trading.Platform.Application/Features/TriggerManualAuthRetry/TriggerManualAuthRetryHandler.cs:5-12`.

The coordinator is the real use case. It enforces schedule, environment, retry
limit, and degraded-state rules, mutates state, persists side effects, attempts
immediate recovery, and saves again. Evidence:
`src/TNC.Trading.Platform.Application/Services/PlatformStateCoordinator.cs:79-146`.
Authentication constructs an IG-specific request and consumes an IG-specific
response inside Application. Evidence:
`src/TNC.Trading.Platform.Application/Services/PlatformStateCoordinator.cs:591-659`.
The concrete HTTP adapter translates those contracts to IG headers and JSON at
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/Ig/IgSessionClient.cs:9-158`.

The endpoint has unit coverage for conflict translation and closed-box coverage
for operator authorization. Evidence:
`test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/PlatformEndpointHandlerTests.cs:27-39`;
`test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/PlatformAuthenticationIntegrationTests.cs:215-239`.
Coordinator eligibility has direct behavior coverage, but the Application
handler itself does not.

### Background authentication supervision and status reads

```text
API composition root AddPlatformApplication
  -> hosted PlatformAuthSupervisor
  -> PlatformAuthSupervisorTickRunner creates DI scope
  -> resolves PlatformStateCoordinator
  -> TickAsync every second
  -> state decision and outward ports

GET /api/platform/status or GET /api/platform/events
  -> corresponding Application handler
  -> PlatformStateCoordinator read method
  -> TickAsync again
  -> projection/event read
```

Application registers the hosted service with every handler and coordinator at
`src/TNC.Trading.Platform.Application/Services/PlatformApplicationServiceCollectionExtensions.cs:11-28`.
The hosted framework adapter and service-locating runner are at
`src/TNC.Trading.Platform.Application/Services/PlatformAuthSupervisor.cs:7-48`
and
`src/TNC.Trading.Platform.Application/Services/PlatformAuthSupervisorRunner.cs:6-47`.
API startup also manually ticks the coordinator before accepting requests.
Evidence: `src/TNC.Trading.Platform.Api/Program.cs:11-51`.

Status is called not only by a status page but also by the home presenter and
layout context provider. Evidence:
`src/TNC.Trading.Platform.Web/Components/Pages/StatusPagePresenter.cs:7-19`;
`src/TNC.Trading.Platform.Web/Components/Pages/HomePagePresenter.cs:24-31`;
`src/TNC.Trading.Platform.Web/Components/Layout/PlatformShellContextProvider.cs:19-43`.
Each nominal read can trigger external authentication and writes because both
`GetStatusAsync` and `GetEventsAsync` call `TickAsync`. Evidence:
`src/TNC.Trading.Platform.Application/Services/PlatformStateCoordinator.cs:31-77`.
The background timer, startup tick, status requests, and event requests can all
enter the same state machine; no coordination or concurrency version is visible
at the Application boundary.

## Responsibility classification

### Genuine domain or enterprise policy

* Trading-window evaluation, holiday precedence, weekend behavior, and time-zone
  conversion in `TradingScheduleGate` are deterministic domain policy. Evidence:
  `src/TNC.Trading.Platform.Application/Services/TradingScheduleGate.cs:5-64`.
* Tick decision priority, including out-of-schedule, Test/Live blocking, session
  expiry, retry waiting, active transition, and degraded transition, is policy.
  Evidence:
  `src/TNC.Trading.Platform.Application/Services/PlatformStateTransitionEngine.cs:5-75`.
* Exponential retry delay and cap calculation is policy, but it remains a static
  method on the coordinator. Evidence:
  `src/TNC.Trading.Platform.Application/Services/PlatformStateCoordinator.cs:752-768`.
* Manual retry eligibility, allowed environment combinations, notification
  deduplication, and session-state transition assignments are policy mixed into
  the coordinator. Evidence:
  `src/TNC.Trading.Platform.Application/Services/PlatformStateCoordinator.cs:79-146,267-345,441-553`.
* Restart-required comparison and notification dispatch context selection are
  policy misplaced in Infrastructure. Evidence:
  `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/PlatformConfigurationRestartPolicy.cs:5-13`;
  `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/NotificationDispatchPolicy.cs:1-54`.

These rules do not yet justify a new Domain project by themselves. They first
need cohesive, invariant-owning types and dependency-free tests. A separate
assembly created before that extraction would mostly move anemic records and
marker-like policy helpers.

### Application and use-case orchestration

* The six handlers are inbound use-case entry points, although five are thin
  wrappers over shared services.
* `PlatformStateCoordinator` is the actual auth-session lifecycle use case and
  crosses configuration, runtime state, IG authentication, retry cycles, login
  snapshots, proof data, events, notifications, time, logging, and host
  configuration. Its 13 constructor dependencies and multiple public operations
  make it a shared workflow service rather than one vertical slice. Evidence:
  `src/TNC.Trading.Platform.Application/Services/PlatformStateCoordinator.cs:9-29`.
* `PlatformStateCoordinatorSideEffects` and `PlatformIgProofDataEnricher` are
  application orchestration helpers, not domain policy. Evidence:
  `src/TNC.Trading.Platform.Application/Services/PlatformStateCoordinatorSideEffects.cs:6-105`;
  `src/TNC.Trading.Platform.Application/Services/PlatformIgProofDataEnricher.cs:7-61`.
* `PlatformConfigurationService` is a pass-through facade over one port. It owns
  no policy and obscures which port methods each handler needs. Evidence:
  `src/TNC.Trading.Platform.Application/Services/PlatformConfigurationService.cs:5-20`.

### Contracts and ports

* Feature request/response records are valid inward-owned application contracts.
* The nine interfaces are legitimate outward ports because Application consumes
  them and Infrastructure implements them. Their current technical folders and
  breadth reduce feature cohesion but do not reverse dependency direction.
* Status, event, configuration, login snapshot, and retry result records are
  read/write models or application DTOs, not domain entities merely because they
  live inward.
* `PlatformRuntimeState` is application workflow state. It could become a domain
  model if transition methods own its invariants; currently it is a mutable data
  bag shaped for persistence mapping.

### Misplaced adapter and framework concerns

* `PlatformAuthSupervisor`, its scoped runner/delay, and
  `PlatformApplicationServiceCollectionExtensions` are hosting/composition
  adapters in Application.
* `PlatformAuthorizationPolicyRegistration`, `PlatformAuthenticationOptions`,
  and provider configuration resolution are shared API/Web host contracts and
  framework configuration, not application use cases. The constants contain a
  mix of protocol, scheme, role, policy, claim, scope, and audit-event names.
* `IConfiguration` access for session lifetime belongs in composition-bound
  options translated to a simple `TimeSpan`; logging can remain cross-cutting if
  accepted by repository policy, but it currently forces framework references
  into orchestration helpers.
* IG raw session tokens, API keys, headers, and wire-style response records are
  outbound adapter details. Application should request capabilities such as
  authenticate and capture proof, receiving secret-safe inward results.
* `IgAuthenticationResponseSanitizer` and `IgLoginSnapshotMapper` are boundary
  translation/security logic. They belong beside the IG HTTP adapter unless the
  port itself is redesigned to return an already sanitized inward result.
* `[JsonIgnore]` on `CredentialPresence.IsComplete` introduces serialization
  concern into an inward policy/data type. Evidence:
  `src/TNC.Trading.Platform.Application/Configuration/CredentialPresence.cs:1-12`.

## Architecture alignment

The implementation partially aligns with the repository standard:

* Compliant: six operations use feature folders and the required
  request/response/handler names; endpoints mostly bind, dispatch, and map;
  transport models are distinct from persistence entities; Infrastructure
  implements inward-owned ports; pure policy has direct tests.
* Partial: feature handlers exist, but most behavior remains in shared technical
  services, so slices are cohesive only at their entry points. Contracts often
  wrap broad shared models instead of feature-local data.
* Non-compliant: no Application validators exist despite malformed external
  update input and meaningful invariants; business policy is enforced only in
  the HTTP validator; no feature handler has a direct unit test.
* Non-compliant: Application depends on ASP.NET Core authorization, DI, hosting,
  configuration, and logging framework types. This violates framework-neutral
  use-case guidance.
* Non-compliant: provider responses and session credentials cross into
  Application through `IIgSessionClient`, contrary to translating external
  representations at the adapter.
* Non-compliant: the primary organization outside 18 feature files is technical
  (`Configuration`, `Services`, `Infrastructure`) rather than
  `Features/<FeatureName>`.
* Non-compliant: `PlatformAuthSupervisorRunner.cs` and
  `PlatformStateTransitionEngine.cs` contain multiple top-level types.
* Risk: status and events are side-effecting queries, weakening CQRS semantics
  and making read latency/availability depend on auth and persistence writes.
* Risk: separately committing ports and multiple tick triggers create partial
  failure and concurrency exposure. No unit-of-work or optimistic concurrency
  contract is visible at the use-case boundary.

The closest accurate description is **CQRS-shaped vertical-slice entry points
over a shared application workflow core, with Clean/Hexagonal dependency
inversion at persistence boundaries but framework and provider leakage inside
the inner assembly**.

## Incremental migration candidates

The candidates are ordered to preserve behavior and avoid speculative projects.

1. Move validation policy inward for `UpdatePlatformConfiguration`. Add
   `Features/UpdatePlatformConfiguration/UpdatePlatformConfigurationValidator.cs`
   and direct validator/handler tests. Keep API validation limited to transport
   parsing and map typed validation outcomes to Problem Details. This closes the
   easiest bypass without changing projects.
2. Remove the pass-through `PlatformConfigurationService`. Inject a narrow
   feature-owned query/update port into each operation, or rename and split the
   current port into capability-focused contracts such as
   `IPlatformConfigurationReader`, `IPlatformConfigurationWriter`, and a
   startup-only bootstrap capability. Keep ports in their consuming feature or
   `Features/PlatformConfiguration/Ports`.
3. Separate read operations from reconciliation. Make `GetPlatformStatus` and
   `GetPlatformEvents` pure reads. Retain state progression behind an explicit
   `ReconcilePlatformAuthentication` operation invoked by startup and the hosted
   scheduler. This restores meaningful command/query semantics and reduces
   duplicate concurrent ticks.
4. Extract `TriggerManualAuthRetry` orchestration from
   `PlatformStateCoordinator` into its handler and feature-local collaborators.
   Move manual eligibility and typed outcomes into the feature, replacing
   `InvalidOperationException` as a normal conflict path. Add fakes for narrow
   ports and move EF-backed coordinator scenarios to Infrastructure integration
   tests.
5. Redesign the IG port before moving files. Replace `IIgSessionClient` with an
   inward capability such as `IIgAuthenticationGateway` that returns a
   secret-safe `IgAuthenticationResult`; hide CST, security-token, API-key, and
   raw-header handling inside Infrastructure. Move all ten current
   `Application/Infrastructure/Ig` mapping/provider types to
   `Infrastructure/Infrastructure/Platform/Ig`, retaining only inward results
   needed by use cases.
6. Move framework adapters outward. Put hosted supervisor scheduling and scoped
   resolution in API hosting code, and move DI registration to API composition
   or a thin assembly-level registration adapter. Bind session lifetime to a
   simple value at composition. Move ASP.NET Core policy registration and auth
   options/resolution to a host-shared project only if API/Web sharing warrants
   one; otherwise keep host-specific registration in each process and share only
   framework-neutral names/contracts.
7. Reorganize remaining models by capability instead of technical kind. Likely
   destinations include `Features/PlatformConfiguration`,
   `Features/PlatformAuthentication`, `Features/GetPlatformStatus`,
   `Features/GetPlatformEvents`, and each feature's `Ports` subfolder. Rename
   generic suffixes where ownership is clearer: `PlatformStatusModel` to
   `GetPlatformStatusResponse` data, `OperationalEventModel` to an event query
   item, and `PlatformEventRecord` to an append command/value.
8. Extract mutable auth state policy. Give the state model transition methods or
   use a pure transition function that returns a new state plus explicit effects.
   Move `PlatformStateTransitionEngine`, `PlatformTickDecisionKind`, and
   `PlatformTickDecision` to separate matching files immediately. Consider a
   dedicated Domain project only after this policy becomes cohesive and has no
   framework/use-case dependencies.
9. Move outward-owned policy inward where appropriate. Rewrite restart policy to
   compare inward configuration values instead of an EF entity. Separate
   notification selection/sanitization policy from transport dispatch, leaving
   Azure/SMTP/provider lookup in Infrastructure.
10. Define consistency deliberately. Either introduce one application-owned
    unit-of-work operation for local EF side effects, or document and test an
    idempotent, resumable workflow/outbox model. Do not wrap the external IG and
    notification calls in a database transaction.

Each candidate should preserve existing endpoint contracts, add direct
handler/policy tests first, and move one boundary at a time. The Application unit
project should ultimately drop its Infrastructure project reference; EF and Data
Protection tests belong under Infrastructure unit/integration coverage.

## Open gaps and clarifying questions

* No production files were modified, and no runtime tests were necessary for
  this source-level research. Existing tests were inspected but not executed.
* The intended consistency guarantee across configuration update and runtime
  reconciliation is not documented in the inspected code. Product input is
  needed to choose between atomic local state, eventual reconciliation, and
  explicit partial-success reporting.
* The reason API and Web authentication contracts were placed in Application is
  inferable as reuse, but no architecture decision record confirms whether a
  separate host-contracts project is acceptable.
* The security requirement for retaining non-secret IG response headers and raw
  JSON snapshots should be confirmed before the IG boundary is simplified.
* The intended concurrency model for startup, hosted, status, and events ticks
  is not explicit. Database constraints and deployment replica count need review
  before selecting locking, optimistic concurrency, or single-writer scheduling.

## Recommended next research

* [ ] Trace EF keys/concurrency tokens and deployment replica assumptions for the
  authentication runtime state.
* [ ] Confirm documented requirements for configuration-update consistency and
  restart semantics.
* [ ] Inventory API/Web authentication sharing to decide between duplicated host
  adapters and a small framework-specific shared contracts project.
* [ ] Build a test ownership map that moves adapter-integrated Application tests
  to Infrastructure while preserving requirement traceability.
* [ ] Prototype one migration candidate, preferably inward update validation or
  pure status reads, and use direct handler tests as the acceptance check.