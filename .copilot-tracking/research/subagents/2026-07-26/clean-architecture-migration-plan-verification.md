---
title: Clean Architecture Migration Plan Verification
description: Evidence-backed verification of the current topology, independently reviewable refactor boundaries, validation gates, tests, documentation effects, sequencing decisions, and conditional Domain project risks.
author: GitHub Copilot
ms.date: 2026-07-26
ms.topic: reference
---

## Research status

Complete. Verified against the repository state on 2026-07-26.

## Research questions

1. What is the exact current production and test project topology?
2. Which significant refactors can be implemented, validated, and reviewed independently?
3. Which existing files and symbols should each phase modify, and which files should each phase create?
4. Which validation commands or scripts exist, and how should narrow checks precede mandatory full solution build and test gates after every significant refactor?
5. Which tests should be added during each phase under repository naming and documentation conventions?
6. Which documentation and wiki pages require updates?
7. Which unresolved decisions genuinely block or alter implementation sequencing?
8. What risks arise from adding a Domain project conditionally rather than prematurely?

## Evidence classification

* Verified fact: Directly supported by current repository source, project, solution, instruction, test, script, or documentation evidence.
* Recommendation: Proposed implementation or sequencing guidance derived from verified facts.
* Blocking decision: A choice that changes correctness, public behavior, deployment semantics, or the order of implementation phases.

## Current topology

### Verified production topology

The solution contains exactly six production projects and nine test projects.
`TNC.Trading.Platform.slnx:2-34` lists every project explicitly; the empty
`/test/` folder entry does not add another project.

| Project | Direct production references | Verified role-relevant package or framework facts |
|---------|------------------------------|---------------------------------------------------|
| `src/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.csproj` | None | Targets `net10.0` and references the complete `Microsoft.AspNetCore.App` shared framework (`:3-12`) |
| `src/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.csproj` | Application | Owns Azure Communication Email, Azure Identity, EF Core InMemory, and EF Core SQL Server packages (`:11-22`) |
| `src/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.csproj` | Application, Infrastructure, ServiceDefaults | Also duplicates Azure Communication Email, Azure Identity, EF Core InMemory, and EF Core SQL Server package references (`:3-17`) |
| `src/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.csproj` | Application, ServiceDefaults | Owns OpenID Connect and Radzen Blazor packages (`:3-13`) |
| `src/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.csproj` | API, Web | Owns Aspire Keycloak and SQL Server hosting packages (`:1-8`) |
| `src/TNC.Trading.Platform.ServiceDefaults/TNC.Trading.Platform.ServiceDefaults.csproj` | None | Is marked as an Aspire shared project and owns service discovery, resilience, health, and telemetry packages (`:3-20`) |

The direct production graph is therefore:

```text
AppHost -> API, Web
API -> Application, Infrastructure, ServiceDefaults
Infrastructure -> Application
Web -> Application, ServiceDefaults
Application -> none
ServiceDefaults -> none
```

This graph is acyclic and points inward at assembly level. It does not prove
that code inside Application or Infrastructure owns the correct responsibility.

The SDK is pinned to .NET SDK `10.0.200` with `latestFeature` roll-forward in
`global.json:1-6`. Every production and test project currently targets
`net10.0`.

### Verified test topology

All nine solution-listed test projects are under `test/`:

| Test project | Direct production reference | Actual boundary represented by current dependencies |
|--------------|-----------------------------|-----------------------------------------------------|
| `test/TNC.Trading.Platform.Architecture/TNC.Trading.Platform.Architecture.IntegrationTests` | None | Repository project-file graph inspection |
| `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests` | Application and Infrastructure | Mixed unit and adapter-backed workflow coverage, not a pure Application unit boundary (`TNC.Trading.Platform.Application.UnitTests.csproj:22-23`) |
| `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests` | Application and Infrastructure | Adapter and policy tests (`TNC.Trading.Platform.Infrastructure.UnitTests.csproj:22-23`) |
| `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests` | API | API transport, mapping, validation, and registration tests (`TNC.Trading.Platform.Api.UnitTests.csproj:22`) |
| `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests` | AppHost | Aspire closed-box API tests, despite the service-oriented folder name (`TNC.Trading.Platform.Api.IntegrationTests.csproj:11-27`) |
| `test/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.UnitTests` | AppHost | AppHost model and configuration tests; it includes `Aspire.Hosting.Testing` (`TNC.Trading.Platform.AppHost.UnitTests.csproj:11-26`) |
| `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests` | Web | bUnit and Web service tests (`TNC.Trading.Platform.Web.UnitTests.csproj:11-27`) |
| `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests` | AppHost | Playwright plus Aspire real-topology functional tests (`TNC.Trading.Platform.Web.FunctionalTests.csproj:11-23`) |
| `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests` | AppHost | Playwright xUnit plus Aspire real-topology E2E tests (`TNC.Trading.Platform.Web.E2ETests.csproj:11-23`) |

The workspace tree also contains a top-level
`TNC.Trading.Platform.Architecture/` directory, but no project under that path
is listed in `TNC.Trading.Platform.slnx`. The active architecture project is the
one under `test/TNC.Trading.Platform.Architecture/`.

## Independently reviewable refactor phases

### Phase 0: Record blocking semantics and add architecture safety rails

Status: Recommended prerequisite. This phase changes tests and durable
architecture documentation, but not runtime behavior.

Verified facts:

* The existing graph test loads every `src/**/*.csproj`, validates missing
	targets and cycles, and intentionally does not encode roles
	(`test/TNC.Trading.Platform.Architecture/TNC.Trading.Platform.Architecture.IntegrationTests/ProjectGraph/RepositoryProjectGraphTests.cs:8-17`,
	`ProjectGraph/ProjectGraphLoader.cs:7-12`, and
	`ProjectGraph/ProjectGraphValidator.cs:5-10`).
* The focused architecture run completed with 7 passed and 0 failed tests on
	2026-07-26.
* Application has no project references but has a full ASP.NET Core framework
	reference. Assembly acyclicity therefore does not prove inner-layer purity.

Modify:

* `test/TNC.Trading.Platform.Architecture/TNC.Trading.Platform.Architecture.IntegrationTests/TNC.Trading.Platform.Architecture.IntegrationTests.csproj`
	only if a library is selected for compiled dependency inspection
* `docs/wiki/architecture.md` when the stable role rules are accepted
* `docs/wiki/testing-and-quality.md` to describe the new fitness checks

Create:

* `test/TNC.Trading.Platform.Architecture/TNC.Trading.Platform.Architecture.IntegrationTests/ArchitectureRules/RepositoryArchitectureRuleTests.cs`
* `test/TNC.Trading.Platform.Architecture/TNC.Trading.Platform.Architecture.IntegrationTests/ArchitectureRules/ProductionProjectRolePolicy.cs`
* `test/TNC.Trading.Platform.Architecture/TNC.Trading.Platform.Architecture.IntegrationTests/ArchitectureRules/ProductionProjectRoleValidator.cs`
* focused validator tests in
	`ArchitectureRules/ProductionProjectRoleValidatorTests.cs`

Recommendation:

* Keep the existing topology-neutral graph checks unchanged.
* Add a separate role policy that enforces current stable edges and prohibited
	outward edges. Check project references and package/framework references from
	project XML first. Add compiled dependency checks only for stable forbidden
	namespaces or assemblies, not filename or folder inventories.
* Encode the current exception that Web may reference Application while shared
	host-authentication types remain there. Remove that allowance only in the
	hosting-cleanup phase.
* Do not encode a Domain project count or require a Domain project. When Domain
	exists, discover it and apply its zero-outward-reference rule conditionally.

Tests to add:

* `Validate_ShouldRejectInfrastructureReference_WhenApplicationReferencesInfrastructure`
* `Validate_ShouldRejectOuterFrameworkReference_WhenApplicationReferencesForbiddenFramework`
* `Validate_ShouldAllowWebApplicationReference_WhenSharedAuthenticationContractsRemain`
* `Validate_ShouldRequireNoProductionReferences_WhenDomainProjectExists`

Each test requires an XML documentation comment immediately above the method
that names the relevant architecture requirement or migration decision and
states what is verified, the expected diagnostic, and the regression risk.

Narrow validation:

```powershell
dotnet test test/TNC.Trading.Platform.Architecture/TNC.Trading.Platform.Architecture.IntegrationTests/TNC.Trading.Platform.Architecture.IntegrationTests.csproj
```

Dependency: None. Completion additionally requires the full gates.

### Phase 1: Make reconciliation explicit and reads side-effect free

Status: Recommended first runtime refactor after Phase 0 decisions establish
the writer model and freshness contract.

Verified facts:

* `PlatformStateCoordinator.GetStatusAsync` calls `TickAsync` before reading
	(`PlatformStateCoordinator.cs:31-35`).
* `PlatformStateCoordinator.GetEventsAsync` also calls `TickAsync`
	(`PlatformStateCoordinator.cs:77-80`).
* `TickAsync` can authenticate with IG, transition mutable state, write retry
	cycles and events, dispatch notifications, capture snapshots, and save runtime
	state (`PlatformStateCoordinator.cs:145-179` and `:181-284`).
* Startup enters `TickAsync` directly (`src/TNC.Trading.Platform.Api/Program.cs:23-50`).
	The Application-owned hosted service enters it every second through a scoped
	coordinator (`PlatformAuthSupervisor.cs:34-48` and
	`PlatformAuthSupervisorRunner.cs:15-29`). Configuration update also calls it
	(`Features/UpdatePlatformConfiguration/UpdatePlatformConfigurationHandler.cs:9-13`).
* `IPlatformRuntimeStateStore` exposes only creating read and save operations
	(`Services/IPlatformRuntimeStateStore.cs:5-10`). A side-effect-free status
	query cannot currently distinguish missing state without creation.

Modify:

* `src/TNC.Trading.Platform.Application/Features/GetPlatformStatus/GetPlatformStatusHandler.cs`
* `src/TNC.Trading.Platform.Application/Features/GetPlatformEvents/GetPlatformEventsHandler.cs`
* `src/TNC.Trading.Platform.Application/Services/IPlatformRuntimeStateStore.cs`
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/EfPlatformRuntimeStateStore.cs`
* `src/TNC.Trading.Platform.Application/Services/PlatformStateCoordinator.cs`
* `src/TNC.Trading.Platform.Application/Services/PlatformApplicationServiceCollectionExtensions.cs`
* `src/TNC.Trading.Platform.Api/Program.cs`
* `src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs` only if
	the status missing/freshness outcome changes transport mapping

Create:

* `src/TNC.Trading.Platform.Application/Features/ReconcilePlatformAuthentication/ReconcilePlatformAuthenticationRequest.cs`
* `src/TNC.Trading.Platform.Application/Features/ReconcilePlatformAuthentication/ReconcilePlatformAuthenticationResponse.cs`
* `src/TNC.Trading.Platform.Application/Features/ReconcilePlatformAuthentication/ReconcilePlatformAuthenticationHandler.cs`
* query-specific read ports or one narrow projection reader under the consuming
	feature folders; do not add a generic repository
* `src/TNC.Trading.Platform.Api/Hosting/PlatformAuthenticationSupervisor.cs`
* `src/TNC.Trading.Platform.Api/Hosting/PlatformAuthenticationSupervisorTickRunner.cs`
	only if retaining a separately testable runner remains useful after the move

Recommendation:

1. Add a non-creating runtime-state read, such as `GetAsync`, whose explicit
	 missing result can be mapped by the status handler.
2. Introduce the reconcile command as the sole Application entry point for a
	 scheduled reconciliation. It may initially delegate to a narrowed existing
	 coordinator method so the first change establishes semantics without also
	 decomposing every write.
3. Remove `TickAsync` calls from both query paths. Status should report the
	 latest persisted state plus an observed/reconciled timestamp; events should
	 read the event store only.
4. Move cadence, scope creation, logging, cancellation, and error continuation
	 to the API hosted adapter. Preserve the current one-second cadence unless a
	 product decision changes it.
5. Preserve blocking startup reconciliation and readiness ordering unless the
	 API contract explicitly gains a `Starting` or unavailable status.
6. Route configuration-triggered reconciliation through the same serialized
	 writer mechanism; do not leave a second direct coordinator entry point.

Tests to create or modify:

* `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/GetPlatformStatus/GetPlatformStatusHandlerTests.cs`
	with `HandleAsync_ShouldNotInvokeReconciliation_WhenStatusIsRead` and
	`HandleAsync_ShouldReturnExplicitMissingState_WhenRuntimeStateDoesNotExist`
* `.../Features/GetPlatformEvents/GetPlatformEventsHandlerTests.cs` with
	`HandleAsync_ShouldOnlyReadEvents_WhenEventsAreRequested`
* `.../Features/ReconcilePlatformAuthentication/ReconcilePlatformAuthenticationHandlerTests.cs`
	with transition, cancellation, failure, and serialization behavior
* move or replace `PlatformAuthSupervisorTests.cs` with API-hosted-adapter tests
	under `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/Hosting/`
* update `AuthRetryCycleTests.cs` and `IgProofDataTests.cs` cases that currently
	expect `GetStatusAsync` to authenticate or mutate. Preserve those behaviors
	under reconcile-command tests instead of deleting their coverage.
* add API contract coverage for unchanged status codes/JSON and explicit
	freshness fields if the response changes.

Narrow validation:

```powershell
dotnet test test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/TNC.Trading.Platform.Application.UnitTests.csproj
dotnet test test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/TNC.Trading.Platform.Api.UnitTests.csproj
dotnet test test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/TNC.Trading.Platform.Api.IntegrationTests.csproj
```

Dependency: Phase 0 decisions for single-writer or concurrency behavior and
latest-known freshness semantics. Completion additionally requires full gates.

### Phase 2: Move configuration and state policy inward

Status: Independently reviewable policy refactor. Domain creation is not part
of this phase.

Verified facts:

* API validation currently owns schedule-window, trading-day, retry-bound,
	environment compatibility, notification provider, and actor rules
	(`src/TNC.Trading.Platform.Api/Features/UpdatePlatformConfiguration/UpdatePlatformConfigurationValidator.cs:10-85`).
* `TradingScheduleConfiguration` and `RetryPolicyConfiguration` are unconstrained
	records (`Application/Configuration/TradingScheduleConfiguration.cs:1-9` and
	`RetryPolicyConfiguration.cs:1-8`).
* `TradingScheduleGate.ResolveTimeZone` silently substitutes UTC for unknown or
	invalid time-zone identifiers (`Services/TradingScheduleGate.cs:47-63`).
* `PlatformRuntimeState` exposes unrestricted public setters for every state
	field (`Configuration/PlatformRuntimeState.cs:3-41`).
* Infrastructure decides restart requirements, retention eligibility, and
	operational sensitivity in
	`PlatformConfigurationRestartPolicy.cs`,
	`OperationalRecordRetentionPolicy.cs`, and `OperationalDataRedactor.cs`.

Modify:

* `src/TNC.Trading.Platform.Application/Configuration/TradingScheduleConfiguration.cs`
* `src/TNC.Trading.Platform.Application/Configuration/RetryPolicyConfiguration.cs`
* `src/TNC.Trading.Platform.Application/Configuration/PlatformRuntimeState.cs`
* `src/TNC.Trading.Platform.Application/Services/TradingScheduleGate.cs`
* `src/TNC.Trading.Platform.Application/Services/PlatformStateTransitionEngine.cs`
* `src/TNC.Trading.Platform.Application/Features/UpdatePlatformConfiguration/UpdatePlatformConfigurationHandler.cs`
* `src/TNC.Trading.Platform.Api/Features/UpdatePlatformConfiguration/UpdatePlatformConfigurationValidator.cs`
* Infrastructure policy callers after each decision is moved inward

Create:

* one Application validator under
	`Features/UpdatePlatformConfiguration/UpdatePlatformConfigurationValidator.cs`
	for use-case input and invariants
* one result/error type per file for typed validation outcomes if exceptions are
	not retained at the use-case boundary
* split `PlatformTickDecisionKind.cs` and `PlatformTickDecision.cs` from
	`PlatformStateTransitionEngine.cs` to satisfy the repository's one-top-level-type
	rule while touching that code
* inward policy types for restart decisions, retention cutoffs, and sensitivity
	classification only when each outward mechanism can consume a simple result

Recommendation:

* Keep API validation for malformed transport strings and Problem Details field
	mapping, but make the Application handler enforce all use-case invariants so
	non-HTTP callers cannot bypass them.
* Define whether invalid persisted/bootstrap values fail startup or are migrated.
	Do not retain the current silent UTC fallback as implicit validation.
* Move one policy at a time with direct tests. Leave EF deletion, JSON transport,
	and notification delivery in Infrastructure.
* Do not make mutable persistence projections into Domain entities. First expose
	behavior through validated values and transition methods in Application.

Tests to add:

* `Validate_ShouldRejectEmptyTradingDays_WhenConfigurationIsUpdated`
* `Validate_ShouldRejectUnknownTimeZone_WhenConfigurationIsUpdated`
* `Create_ShouldRejectMaxDelayBelowInitialDelay_WhenRetryPolicyIsInvalid`
* `DecideTickAction_ShouldReturnBlockedLive_WhenTestPlatformTargetsLiveBroker`
* `IsRestartRequired_ShouldReturnTrue_WhenStartupFixedEnvironmentChanges`
* `CreatePlan_ShouldExcludeCurrentRecords_WhenRetentionCutoffIsEvaluated`

Move corresponding policy tests from Infrastructure to Application only after
ownership moves. Keep adapter/mechanism tests in Infrastructure.

Narrow validation:

```powershell
dotnet test test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/TNC.Trading.Platform.Application.UnitTests.csproj
dotnet test test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/TNC.Trading.Platform.Infrastructure.UnitTests.csproj
dotnet test test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/TNC.Trading.Platform.Api.UnitTests.csproj
```

Dependency: Phase 1 for state/freshness ownership where policy affects reads.
Completion additionally requires full gates.

### Phase 3: Normalize the IG outbound adapter boundary

Status: Independently reviewable provider-boundary refactor. This should precede
extracting authentication orchestration into multiple handlers so provider
shapes are not copied into the new use cases.

Verified facts:

* Application owns IG protocol-shaped types under
	`Application/Infrastructure/Ig`, including authentication responses, CST and
	security-token-bearing fields, account/position response shapes, a sanitizer,
	and a three-operation `IIgSessionClient`.
* Infrastructure's `IgSessionClient` returns tokens across the Application
	boundary (`IgSessionClient.cs:23-69`) and accepts raw CST, security token, and
	API key arguments for later proof calls (`:71-161`).
* The Infrastructure registration hard-codes the Demo base address regardless
	of the request's broker environment
	(`PlatformInfrastructureServiceCollectionExtensions.cs:62-65`).

Modify:

* `src/TNC.Trading.Platform.Application/Infrastructure/Ig/IIgSessionClient.cs`
	during transition, then delete it when all callers migrate
* `src/TNC.Trading.Platform.Application/Services/PlatformIgProofDataEnricher.cs`
* `src/TNC.Trading.Platform.Application/Services/PlatformStateCoordinator.cs`
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/Ig/IgSessionClient.cs`
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/PlatformInfrastructureServiceCollectionExtensions.cs`
* `src/TNC.Trading.Platform.Application/Properties/InternalsVisibleTo.cs` and
	Infrastructure friend exposure only if the new boundary removes a demonstrated
	need

Create:

* `src/TNC.Trading.Platform.Application/Features/PlatformAuthentication/Ports/IBrokerAuthenticationGateway.cs`
* `.../BrokerAuthenticationRequest.cs`
* `.../BrokerAuthenticationOutcome.cs`
* `.../BrokerAuthenticationFailure.cs`
* `.../BrokerAuthenticationEvidence.cs`
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/Ig/IgBrokerAuthenticationGateway.cs`
* provider wire DTO files in Infrastructure when extracted from nested or
	Application-owned records

Delete after migration:

* provider-shaped records under
	`src/TNC.Trading.Platform.Application/Infrastructure/Ig/` that no longer form
	an inward contract
* `IgAuthenticationResponseSanitizer.cs` and `IgLoginSnapshotMapper.cs` from
	Application after equivalent provider-to-inward translation exists in the
	adapter

Recommendation:

* Use one operation-scoped authenticate-and-collect-proof capability for the
	current use case. Keep API key, CST, security token, HTTP headers, provider
	JSON, and provider exceptions inside Infrastructure.
* Return only secret-safe inward outcomes and proof evidence.
* Reject Live explicitly until product requirements authorize the production
	IG endpoint. Never map a Live request to Demo silently.
* Do not introduce a durable session handle until expiry, disposal, process
	affinity, replica routing, and restart behavior are defined.

Tests to add or move:

* Infrastructure: `AuthenticateAndCollectProofAsync_ShouldUseDemoEndpoint_WhenBrokerEnvironmentIsDemo`
* Infrastructure: `AuthenticateAndCollectProofAsync_ShouldRejectLiveWithoutNetworkCall_WhenLiveIsUnsupported`
* Infrastructure: `AuthenticateAndCollectProofAsync_ShouldExcludeSessionTokens_WhenResultCrossesApplicationBoundary`
* Infrastructure: success and each current HTTP failure classification using a
	fake `HttpMessageHandler`
* Architecture: `Validate_ShouldRejectProviderProtocolType_WhenApplicationMentionsIgHeadersOrHttpClient`
* Move sanitizer and mapper behavior from Application tests to Infrastructure
	adapter tests as ownership moves.

Narrow validation:

```powershell
dotnet test test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/TNC.Trading.Platform.Infrastructure.UnitTests.csproj --filter "FullyQualifiedName~IgSessionClientTests"
dotnet test test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/TNC.Trading.Platform.Infrastructure.UnitTests.csproj
dotnet test test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/TNC.Trading.Platform.Application.UnitTests.csproj
dotnet test test/TNC.Trading.Platform.Architecture/TNC.Trading.Platform.Architecture.IntegrationTests/TNC.Trading.Platform.Architecture.IntegrationTests.csproj
```

Dependency: Phase 2 for the inward request/outcome vocabulary. Completion
additionally requires full gates.

### Phase 4: Extract manual retry as the first complete write slice

Status: Recommended first substantial coordinator decomposition.

Verified facts:

* `TriggerManualAuthRetryHandler` is a one-line coordinator wrapper
	(`Features/TriggerManualAuthRetry/TriggerManualAuthRetryHandler.cs:5-10`).
* `PlatformStateCoordinator.TriggerManualRetryAsync` performs eligibility,
	environment safety, state mutation, retry-cycle writes, events, persistence,
	immediate broker recovery, snapshots, and notifications
	(`PlatformStateCoordinator.cs:83-143` and `:181-284`).
* Normal rejection is expressed as `InvalidOperationException`. The API catches
	every `InvalidOperationException` and maps it to `409 Conflict`
	(`src/TNC.Trading.Platform.Api/Features/Platform/TriggerManualAuthRetryEndpointHandler.cs:20-35`).
* Existing tests call the coordinator, not the handler
	(`test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/AuthRetryCycleTests.cs:235-275`).

Modify:

* `src/TNC.Trading.Platform.Application/Features/TriggerManualAuthRetry/TriggerManualAuthRetryHandler.cs`
* `.../TriggerManualAuthRetryResponse.cs`
* `src/TNC.Trading.Platform.Api/Features/Platform/TriggerManualAuthRetryEndpointHandler.cs`
* `src/TNC.Trading.Platform.Application/Services/PlatformStateCoordinator.cs`
* relevant persistence ports and Infrastructure implementations only where a
	narrower operation-specific commit is introduced

Create:

* `src/TNC.Trading.Platform.Application/Features/TriggerManualAuthRetry/TriggerManualAuthRetryOutcome.cs`
* `.../ManualAuthRetryRejectionReason.cs`
* feature-local ports under
	`Features/TriggerManualAuthRetry/Ports/` for the exact state load, broker
	operation, and local commit intent required by this use case
* `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/TriggerManualAuthRetry/TriggerManualAuthRetryHandlerTests.cs`

Recommendation:

* Replace normal exception-driven conflicts with a typed outcome while
	preserving the current `202 Accepted` success and `409 Conflict` rejection
	HTTP contract.
* Keep one operation-specific local commit intent for state, retry-cycle, event,
	snapshot, and audit persistence. Do not split those into unrelated saves only
	to create narrow interfaces.
* Keep the remote IG call and notification delivery outside a SQL transaction.
	Apply the selected idempotency/resume semantics from Phase 0.
* Delete the coordinator method only after no caller remains.

Tests to add:

* `HandleAsync_ShouldRejectWithScheduleInactiveReason_WhenTradingScheduleIsInactive`
* `HandleAsync_ShouldRejectWithBlockedLiveReason_WhenTestPlatformTargetsLiveBroker`
* `HandleAsync_ShouldRejectWithoutWrites_WhenRetryLimitIsNotReached`
* `HandleAsync_ShouldCommitNewRetryCycle_WhenManualRetryIsAllowed`
* `HandleAsync_ShouldMapTypedRejectionToConflict_WhenManualRetryIsUnavailable`
* failure-injection tests at every external-effect boundary selected by the
	consistency decision

Narrow validation:

```powershell
dotnet test test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/TNC.Trading.Platform.Application.UnitTests.csproj --filter "FullyQualifiedName~TriggerManualAuthRetryHandlerTests"
dotnet test test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/TNC.Trading.Platform.Api.UnitTests.csproj --filter "FullyQualifiedName~PlatformEndpointHandlerTests"
dotnet test test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/TNC.Trading.Platform.Infrastructure.UnitTests.csproj
```

Dependency: Phases 1 through 3. Completion additionally requires full gates.

### Phase 5: Extract the remaining operations one at a time

Status: A sequence of independent reviews, not one broad coordinator rewrite.

Recommended order and exact anchors:

1. Record auth audit event.
	 Modify `src/TNC.Trading.Platform.Api/Features/Platform/RecordAuthAuditEventEndpointHandler.cs`
	 and `PlatformEndpoints.cs`. Create Application request, response, handler,
	 and feature tests under `Features/RecordAuthAuditEvent/`. The current endpoint
	 directly combines claim-derived transport input with configuration and event
	 persistence (`RecordAuthAuditEventEndpointHandler.cs:11-54`). Preserve API
	 identity extraction and move event intent/orchestration inward.
2. Update platform configuration.
	 Modify the existing request, response, and handler under
	 `Application/Features/UpdatePlatformConfiguration/`, plus
	 `PlatformConfigurationService.cs` and `SqlPlatformConfigurationStore.cs`.
	 Preserve the current single scoped `PlatformDbContext` behavior in which
	 credentials, configuration, and audit records are saved through related
	 services (`SqlPlatformConfigurationStore.cs:48-116`). Route post-update
	 reconciliation through the Phase 1 command.
3. Reconciliation.
	 Move remaining orchestration from `PlatformStateCoordinator.TickAsync` into
	 the existing Phase 1 reconcile handler and feature-local collaborators. Keep
	 state transition policy separate from provider and persistence mechanics.
4. Status and event queries.
	 Remove the coordinator dependency entirely after direct projection ports are
	 stable. Preserve API mapping files and wire shapes unless the freshness
	 decision deliberately changes them.
5. IG login history.
	 Refine `Application/Features/GetIgLoginHistory/` only if its broad store port
	 remains after the other slices. It is already a read-shaped feature and does
	 not need churn merely for symmetry.

For every subphase, create a direct `<HandlerName>Tests.cs` file and cover one
success, one rejection/missing outcome, cancellation propagation, and no-write
behavior where applicable. Run that feature's test class, its owning project,
then both full gates before beginning the next operation.

Dependency: Phase 4 establishes the first proven write-slice pattern. The audit
subphase is technically independent and may precede Phase 4 if the team wants a
smaller pilot.

### Phase 6: Clean startup, database lifecycle, and hosting composition

Status: Split into two reviews because database lifecycle and auth-hosting
ownership have different risks.

#### Phase 6A: Infrastructure-owned startup initializer

Verified facts:

* API startup directly resolves `PlatformDbContext`, deletes Development SQL,
	calls `EnsureCreatedAsync`, applies configuration, runs retention, and invokes
	reconciliation (`Program.cs:23-50`).
* No EF migrations were found in the Infrastructure source inventory.

Modify:

* `src/TNC.Trading.Platform.Api/Program.cs`
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/PlatformInfrastructureServiceCollectionExtensions.cs`
* `src/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.csproj`
* `src/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.csproj`

Create after the migration strategy is decided:

* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Startup/IPlatformStartupInitializer.cs`
	only if the interface must cross the composition boundary; otherwise expose a
	registration/startup extension without a pass-through interface
* `.../PlatformStartupInitializer.cs`
* EF migrations under the Infrastructure-owned persistence area if migrations
	are selected
* Infrastructure integration tests against real SQL Server for migration and
	transaction behavior; these belong in a new
	`test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests`
	project if real SQL behavior is required, because current Infrastructure tests
	are designated unit tests

Do not implement this phase until the no-history local database transition,
migration ownership, and deployment/startup application policy are decided.
After concrete EF/Azure use leaves API, remove its duplicate Azure and EF package
references (`Api.csproj:10-14`) and unnecessary Infrastructure friend access.

#### Phase 6B: Remove ASP.NET Core hosting concerns from Application

Verified facts:

* Application contains `BackgroundService`, DI registration, ASP.NET Core
	authorization policy registration, `IConfiguration`, and logging dependencies.
* API and Web use Application authentication defaults/options/resolvers broadly.
	For example, API registration consumes them in
	`Api/Authentication/PlatformApiAuthenticationServiceCollectionExtensions.cs:4-67`,
	and Web registration consumes them in
	`Web/Authentication/PlatformWebAuthenticationServiceCollectionExtensions.cs:5-120`.
* `PlatformAuthorizationPolicyRegistration` directly consumes
	`Microsoft.AspNetCore.Authorization.AuthorizationOptions`
	(`Application/Authentication/PlatformAuthorizationPolicyRegistration.cs:1-35`).

Modify:

* API and Web authentication registration and tests
* `src/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.csproj`
* `src/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.csproj`
* `src/TNC.Trading.Platform.Application/Authentication/*`
* `src/TNC.Trading.Platform.Application/Services/PlatformApplicationServiceCollectionExtensions.cs`

Recommendation:

* Move host framework registration into API and Web. Keep neutral role, scope,
	claim, provider, and audit-event constants together only after deciding their
	owner.
* Remove Web's Application project reference only when no real shared contract
	remains. Do not create a generic shared project to force that result.
* Remove `Microsoft.AspNetCore.App` from Application only after hosted services,
	authorization registration, DI extension methods, configuration, and logging
	dependencies are all absent.

Narrow validation for Phase 6:

```powershell
dotnet test test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/TNC.Trading.Platform.Infrastructure.UnitTests.csproj
dotnet test test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/TNC.Trading.Platform.Api.UnitTests.csproj
dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj
dotnet test test/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.UnitTests/TNC.Trading.Platform.AppHost.UnitTests.csproj
dotnet test test/TNC.Trading.Platform.Architecture/TNC.Trading.Platform.Architecture.IntegrationTests/TNC.Trading.Platform.Architecture.IntegrationTests.csproj
```

Dependency: Phase 6A is blocked by database lifecycle decisions. Phase 6B
depends on Phase 1 hosting movement but can otherwise proceed separately.

### Phase 7: Reorganize Infrastructure mechanically

Status: Last structural phase, after ownership is stable.

Verified fact: Current paths and namespaces repeat the layer name, including
`Infrastructure/Infrastructure/Platform` and
`TNC.Trading.Platform.Infrastructure.Infrastructure.Platform`. The broad
`Platform` folder mixes persistence, IG, time, configuration, retention,
credentials, proof storage, and notifications.

Recommendation:

* Move files mechanically into the decided adapter-oriented folders, such as
	`Persistence/EntityFramework`, `Configuration/SqlServer`,
	`Credentials/DataProtection`, `Integrations/Ig`, `Notifications`,
	`Operations/Retention`, `Startup`, and `Time`.
* Update namespaces, registrations, friend access, and tests without changing
	behavior in the same review.
* Use compiler-guided moves in small batches. Do not combine this phase with
	policy extraction, port redesign, or Domain creation.

Tests: No new behavior tests are required solely for namespace moves. Existing
Infrastructure, API, architecture, and full gates must remain green after each
move batch.

Dependency: Phases 2 through 6 establish ownership and retirement targets.

## Validation gates

### Verified current validation surface

No repository `*.ps1`, `*.cmd`, `*.bat`, or `*.sh` validation script was found.
Current instructions and plans call `dotnet build` and `dotnet test` directly.
The exact solution-qualified commands requested for this migration are valid:

```powershell
dotnet build TNC.Trading.Platform.slnx
dotnet test TNC.Trading.Platform.slnx
```

`dotnet test TNC.Trading.Platform.slnx` means the complete nine-project suite,
not unit tests alone. It includes Aspire-backed API integration tests and
Playwright/Aspire Web functional and E2E tests. The full gate therefore requires
the repository's documented Docker-backed local dependencies and may be slower
or more environment-sensitive than focused project runs.

Recommendation: Every significant refactor must use this order:

1. Run the narrowest affected test project, optionally filtered to the changed
	class or behavior.
2. Run any directly adjacent architecture or adapter test project needed to
	validate the boundary.
3. Run `dotnet build TNC.Trading.Platform.slnx` from the repository root.
4. Run `dotnet test TNC.Trading.Platform.slnx` from the repository root.
5. Do not mark the significant refactor complete, merge it, or begin an
	independently reviewable next phase until both full commands pass.

This is stronger and more explicit than using the common
`FullyQualifiedName!~IntegrationTests` filter. That filter depends on test names
and namespaces, not project type, so it is not an authoritative way to select
all and only unit-test projects.

### Enforcement recommendation

Verified fact: No `.github/workflows` files, `.vscode/tasks.json`, repository
shell validation scripts, `Directory.Build.*`, or `Directory.Packages.props`
were found. The repository currently has no automated mechanism that guarantees
the two solution-qualified commands run after each significant refactor.

Recommendation:

* Put the two exact full commands in every implementation work item's completion
	criteria and evidence record.
* Define a significant refactor as any completed phase or subphase above that
	changes project references, package/framework references, public or internal
	boundary contracts, runtime orchestration, persistence semantics, provider
	translation, startup behavior, or ownership of a policy.
* Require recorded command, exit code, and passed/failed/skipped counts before
	the next significant refactor begins.
* Use `--no-build` only for a focused rerun after a successful build when no
	source changed. Do not use it for the final full test gate.
* If parallel MSBuild child-node exits occur, retry the full test command with
	`-m:1`, as already documented in
	`docs/wiki/testing-and-quality.md:219-228` and
	`docs/wiki/local-development.md:130-140`. Record the retry and outcome.
* Add CI or a repository script in the implementation plan if durable
	enforcement is required. That addition is a planning decision because no such
	automation exists today. A script should run the exact solution-qualified
	commands and fail immediately on either non-zero exit.

The complete gate requires Docker because the solution includes real
AppHost-backed API integration, Web functional, and Web E2E suites. The current
local guide declares Docker Desktop as a prerequisite
(`docs/wiki/local-development.md:5-9`) and explains that these suites use the
real Aspire, SQL Server, Mailpit, and Keycloak topology (`:23-25`, `:142-150`).

## Test additions

### Repository conventions that apply to every phase

Verified facts:

* Tests must use xUnit, live under `test/`, and keep unit, integration, E2E, and
	functional boundaries in separate projects
	(`.github/instructions/tests.instructions.md:17-46`).
* Every test method name must follow
	`MethodName_StateUnderTest_ExpectedResult`
	(`tests.instructions.md:24-28`).
* Every test method requires an immediately preceding comment that captures
	requirement traceability where applicable, what is verified, the expected
	result, and why the behavior matters (`tests.instructions.md:29-36`).
* Unit tests may not use network, containers, databases, or file-system
	infrastructure (`tests.instructions.md:39-42`).
* Aspire tests must remain closed-box and use external boundaries
	(`aspire-tests.instructions.md:20-35`).

Recommended test progression for each phase:

1. Add or adjust direct policy/handler tests before changing the outer adapter.
2. Add Infrastructure adapter tests with fake HTTP handlers or in-memory EF only
	 when provider-specific behavior is not under test.
3. Add real SQL Server integration tests for migrations, constraints,
	 transactions, concurrency, and SQL-specific retention queries.
4. Preserve API transport status, JSON, Problem Details, and authorization
	 behavior through API unit/integration tests.
5. Add or update architecture rules for each stable dependency decision.
6. Use Aspire closed-box tests only for startup readiness, service wiring,
	 authentication, and representative cross-process behavior that lower-level
	 tests cannot prove.

The Phase sections list concrete test files and names. Existing
`PlatformEndpointHandlerTests.cs` has methods without required documentation
comments (`:9-55`). When those methods are touched by migration work, add the
required comments rather than copying the omission into new tests.

## Documentation effects

Verified fact: Repository instructions make `docs/wiki/` the implementation
documentation source of truth and require affected wiki pages to be updated
before a numbered plan is complete (`.github/copilot-instructions.md:50-54`).

Update these pages as their corresponding behavior lands:

| Phase | Required wiki updates | Reason |
|-------|-----------------------|--------|
| 0 | `docs/wiki/architecture.md`, `docs/wiki/testing-and-quality.md` | Record enforced roles and distinguish graph integrity from responsibility fitness |
| 1 | `docs/wiki/architecture.md`, `docs/wiki/runtime-behavior.md`, `docs/wiki/api-reference.md`, `docs/wiki/testing-and-quality.md`, `docs/wiki/local-development.md` | These pages currently state that the coordinator owns reads, startup runs `TickAsync`, the background service ticks every second, and status is current; query purity introduces latest-known/freshness semantics and an API-owned hosted adapter |
| 2 | `docs/wiki/architecture.md`, `docs/wiki/runtime-behavior.md`, `docs/wiki/api-reference.md`, `docs/wiki/operator-guide.md` | Validation ownership and invalid-configuration behavior may change while HTTP Problem Details and operator messages should remain stable |
| 3 | `docs/wiki/architecture.md`, `docs/wiki/runtime-behavior.md`, `docs/wiki/testing-and-quality.md`, `docs/wiki/ig-day-trading-with-ig-apis.md` | IG token lifetime, Demo/Live routing, provider translation ownership, and deterministic adapter tests change |
| 4-5 | `docs/wiki/architecture.md`, `docs/wiki/runtime-behavior.md`, `docs/wiki/api-reference.md`, `docs/wiki/testing-and-quality.md` | Feature ownership moves from coordinator/API into direct handlers while transport behavior must remain documented |
| 6A | `docs/wiki/architecture.md`, `docs/wiki/runtime-behavior.md`, `docs/wiki/local-development.md`, `docs/wiki/testing-and-quality.md` | Startup sequence, database creation/migration, reset guidance, readiness, and SQL integration validation change |
| 6B | `docs/wiki/architecture.md`, `docs/wiki/application-overview.md`, `docs/wiki/testing-and-quality.md` | Application framework neutrality and API/Web authentication ownership change |
| Conditional Domain | `docs/wiki/architecture.md`, `docs/wiki/application-overview.md`, `docs/wiki/testing-and-quality.md` | The project graph, role description, test topology, and policy ownership change |
| 7 | `docs/wiki/architecture.md` | Implemented folder/namespace examples must match the final adapter layout |

Specific stale statements to revisit include:

* `docs/wiki/architecture.md:151-191`, which says API startup is thin, describes
	Application-hosted auth supervision, and calls the coordinator the central
	runtime decision-maker
* `docs/wiki/runtime-behavior.md:33-65`, which diagrams direct DbContext,
	retention processor, coordinator, and IG calls from startup
* `docs/wiki/local-development.md:65-78`, which promises that the background
	supervisor attempts IG authentication within a few seconds
* `docs/wiki/api-reference.md:115-219`, which describes the status response as
	current but has no explicit reconciliation freshness contract
* `docs/wiki/testing-and-quality.md:11-21`, whose project table must include a
	Domain unit-test project or Infrastructure integration-test project if added

Update `docs/wiki/README.md` only when navigation or the listed implemented
surface changes. Update `docs/wiki/operator-guide.md` only for visible status,
freshness, validation, retry, or startup behavior, not for internal namespace
moves.

## Blocking decisions

The following decisions genuinely block or alter sequencing. They cannot be
resolved from source inspection alone.

1. Deployment writer and replica model.
	Decide whether the API is guaranteed to run as one writer, uses a database
	lease, or uses optimistic concurrency. `AuthRuntimeStateEntity` has no
	visible concurrency token in `PlatformDbContext` mappings
	(`PlatformDbContext.cs:34-47`), and the runtime store uses read-then-save with
	no version check (`EfPlatformRuntimeStateStore.cs:8-73`). This blocks final
	serialization semantics for Phase 1 and write-slice commits.
2. Reconciliation consistency and external-effect guarantee.
	Decide which local records must commit atomically, how a crash between IG,
	state/event/snapshot writes, and notification delivery resumes, and whether
	notifications are at-most-once, at-least-once, or outbox-backed. This blocks
	the final Phase 4 port and commit shape.
3. Query freshness and startup readiness contract.
	Decide how status represents missing state, last reconciliation, stale
	supervision, and startup-in-progress. This changes Phase 1 response models,
	API docs, tests, and whether startup remains blocking.
4. Database lifecycle and migration ownership.
	Decide how existing no-history databases transition, where migrations run,
	whether Development data is still deleted, and whether deployment or guarded
	startup applies migrations. This blocks Phase 6A.
5. Invalid persisted/bootstrap configuration behavior.
	Decide whether invalid schedules, time zones, and retry values fail startup,
	are repaired through a migration/default, or are represented as an operator
	error. This changes Phase 2 sequencing and migration tests.
6. IG Live support.
	Decide whether Live is unsupported, blocked by environment policy, or routed
	to `https://api.ig.com/gateway/deal`. Current registration always uses Demo.
	This changes the Phase 3 gateway contract and tests.
7. Proof-data durability and IG session scope.
	Decide whether proof data remains process-local, becomes persisted, or is
	explicitly ephemeral; also define token/session disposal and replica affinity
	before introducing any reusable session abstraction. This changes Phase 3
	and potentially Phase 6 persistence work.
8. Data Protection key-ring persistence.
	Decide the durable shared key-ring mechanism for restarts/replicas. Current
	API registration calls `AddDataProtection` without visible durable key-ring
	configuration (`Program.cs:15-19`). This affects operational hardening and
	credential recoverability, but does not block query-purity work.
9. Shared host-authentication contract ownership.
	Decide whether neutral constants/options remain in Application temporarily,
	are duplicated host-locally, or justify a narrowly named shared host-auth
	assembly. This alters Phase 6B and whether Web can remove its Application
	reference. It does not justify a generic `Shared` project.

Decisions 1 through 3 are required before Phase 1 is considered complete.
Decision 5 is required before Phase 2 validates persisted state strictly.
Decision 6 is required before Phase 3 claims correct environment routing.
Decisions 2 and 7 shape Phase 4. Decision 4 blocks Phase 6A. Decisions 8 and 9
can proceed on separate tracks after their owning behavior is explicit.

## Conditional Domain project assessment

### Verified readiness state

No current cluster meets a strong Domain extraction gate unchanged:

* `TradingScheduleGate` is framework-neutral and directly tested, but its input
	is an unconstrained Application record and invalid time zones silently become
	UTC (`TradingScheduleGate.cs:47-63`).
* `PlatformStateTransitionEngine` expresses real policy, but it consumes broad
	mutable `PlatformConfigurationSnapshot` and `PlatformRuntimeState` objects and
	shares one file with its decision enum and record
	(`PlatformStateTransitionEngine.cs:5-75`).
* Retry timing is a static helper on the 13-dependency coordinator
	(`PlatformStateCoordinator.cs:10-28` and `:757-769`).
* Restart, retention, notification, and redaction policy are mixed into
	Infrastructure and still consume framework/configuration or persistence-shaped
	inputs.

### Recommended extraction gate

Create Domain only when one cohesive policy cluster satisfies all of these:

1. It owns validated construction or invariant-preserving transitions.
2. It accepts its own values, not Application snapshots, EF entities, HTTP
	 models, provider DTOs, `IConfiguration`, or hosting types.
3. It has direct in-process behavior tests with no Infrastructure, web host,
	 database, container, file system, or provider.
4. Application can map into and out of it without a reverse dependency.
5. Moving it creates a meaningful compile-time protection, not only a new
	 namespace.
6. The behavior, values, results, and tests can move together in one review.

The likely first candidates are a validated trading-schedule policy or a
closed authentication-transition model after Phase 2. The transition model is
the stronger long-term candidate if it stops exposing unrestricted setters.

### Exact conditional project changes

Create only after the gate passes:

* `src/TNC.Trading.Platform.Domain/TNC.Trading.Platform.Domain.csproj`
* `test/TNC.Trading.Platform.Domain/TNC.Trading.Platform.Domain.UnitTests/TNC.Trading.Platform.Domain.UnitTests.csproj`
* one feature-named folder containing the complete policy cluster

Modify:

* `TNC.Trading.Platform.slnx` to add both projects
* `src/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.csproj`
	to add `Application -> Domain`
* architecture role tests to enforce Domain's no-outward-reference rule
* affected Application mappings and direct Domain tests

Do not add `Infrastructure -> Domain` unless a concrete EF mapping or adapter
directly consumes a Domain type. Do not move requests, responses, handlers,
persistence ports, projections, provider DTOs, auth host options, or mutable EF
shapes into Domain.

### Risks of conditional introduction

* Delaying too long leaves policy protected only by convention. Mitigate with
	Phase 0 fitness tests and a named readiness checkpoint after each Phase 2
	policy extraction.
* Introducing it during a larger orchestration refactor increases namespace,
	project, and behavior churn in one review. Mitigate by moving one already
	cohesive and fully tested cluster only.
* A conditional gate can become subjective. Mitigate by recording the six
	binary criteria above and evidence for each candidate.
* Moving anemic records prematurely creates a nominal Domain assembly while
	invariants remain elsewhere. This is the principal premature-introduction
	risk in the current code.
* Infrastructure may acquire an unnecessary Domain reference for convenience.
	Require a demonstrated direct type use and architecture-test the absence of
	broad outward edges.
* A new test project expands the full solution gate. Add it to the solution in
	the same review so `dotnet test TNC.Trading.Platform.slnx` proves the boundary.

Conditional Domain creation does not block Phases 3 through 7. If no candidate
passes the gate, a purified combined Application core remains a valid Clean
Architecture implementation.

## Recommended planning implications

1. Treat this migration as a chain of independently mergeable refactors, not a
	single folder/project rewrite. Use Phases 0 through 7 as review boundaries;
	split Phase 5 and Phase 6 into the listed subphases.
2. Resolve writer/replica, consistency, freshness, invalid-configuration, IG
	Live, and database lifecycle decisions at the point identified above. Do not
	block unrelated policy or adapter work on key-ring or shared-auth ownership.
3. Require a narrow test first, then the exact full
	`dotnet build TNC.Trading.Platform.slnx` and
	`dotnet test TNC.Trading.Platform.slnx` gates after every significant
	refactor. Docker-backed distributed suites are part of the full test gate.
4. Normalize the IG adapter before decomposing authentication orchestration
	broadly. Otherwise new handlers will inherit CST, headers, provider JSON, and
	exception semantics as Application contracts.
5. Use manual retry as the first complete write-slice extraction and auth audit
	as the smallest independent pilot. Preserve HTTP status and response shapes
	while replacing exceptions and direct endpoint orchestration inward.
6. Defer startup initializer implementation until migrations and existing local
	database transition are decided. Moving `EnsureCreated` behind an interface
	without that decision would hide, not solve, the lifecycle problem.
7. Defer Infrastructure folder/namespace normalization until policy and port
	ownership settle. Keep mechanical moves separate from behavioral changes.
8. Add Domain only when one policy cluster passes the explicit gate. Record a
	gate assessment after Phase 2; absence of a qualifying cluster is a valid
	outcome, not a planning failure.
9. Update affected wiki pages inside each phase before its full completion gate.
	Do not postpone all documentation to a final cleanup phase because the wiki
	is the repository's implemented-state source of truth.

## References

### Primary repository evidence

* `.copilot-tracking/research/2026-07-26/clean-architecture-migration-research.md`
* `.github/copilot-instructions.md`
* `.github/instructions/architecture.instructions.md`
* `.github/instructions/tests.instructions.md`
* `.github/instructions/aspire-tests.instructions.md`
* `TNC.Trading.Platform.slnx`
* `global.json`
* production and test project files under `src/` and `test/`
* `src/TNC.Trading.Platform.Api/Program.cs`
* `src/TNC.Trading.Platform.Application/Services/PlatformStateCoordinator.cs`
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/PlatformInfrastructureServiceCollectionExtensions.cs`
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/Ig/IgSessionClient.cs`
* `test/TNC.Trading.Platform.Architecture/TNC.Trading.Platform.Architecture.IntegrationTests/ProjectGraph/`
* `docs/wiki/architecture.md`
* `docs/wiki/runtime-behavior.md`
* `docs/wiki/testing-and-quality.md`
* `docs/wiki/local-development.md`
* `docs/wiki/api-reference.md`
