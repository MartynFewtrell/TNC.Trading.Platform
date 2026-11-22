<!-- markdownlint-disable-file -->
# Implementation Details: Clean Architecture Migration

## Context Reference

Sources: `.copilot-tracking/research/2026-07-26/clean-architecture-migration-research.md`, `.copilot-tracking/research/subagents/2026-07-26/clean-architecture-migration-plan-verification.md`, `.github/instructions/architecture.instructions.md`, `.github/instructions/refactoring.instructions.md`, `.github/instructions/tests.instructions.md`, and the user requirement that every significant refactor pass a solution build and the complete test suite before further migration work begins.

## Execution Contract

### Significant refactor definition

Treat each completed phase or explicitly named subphase in this document as a separate significant refactor when it changes any of the following:

* Project or package references
* Boundary contracts or ownership
* Runtime orchestration or hosting
* Persistence, transaction, or concurrency semantics
* Provider translation or external-effect ordering
* Policy ownership or invariant enforcement
* Public API behavior or documented runtime behavior
* A mechanical namespace or folder move batch

Do not combine two significant refactors into one unvalidated change. A subphase that is called out as an independent migration unit must reach its full quality gate before the next subphase begins.

Unless a step is explicitly limited to recording a decision, evaluating a gate, validating an already completed change, or reconciling documentation, every numbered production-code step is one independently committed significant refactor. Do not combine adjacent numbered production-code steps behind one quality gate.

### Mandatory quality gate

For every significant refactor:

1. Add or update direct tests before or with the production change whenever the refactor exposes missing behavior coverage.
2. Run the narrowest tests that can falsify the change.
3. Run all adjacent project or architecture suites listed for the phase.
4. Run `dotnet build TNC.Trading.Platform.slnx` from the repository root.
5. Run `dotnet test TNC.Trading.Platform.slnx` from the repository root without `--no-build` or exclusion filters.
6. Record the command, exit code, and passed, failed, and skipped totals in the phase evidence.
7. Do not start, merge, or mark the next significant refactor complete until both solution-qualified commands pass with zero failures.

If the complete test command encounters the documented intermittent MSBuild child-node exit, rerun `dotnet test TNC.Trading.Platform.slnx -m:1`, record both attempts, and require the serialized run to pass. An environment or infrastructure failure is still a blocked gate; it is not permission to continue with the next refactor.

The complete gate includes all nine current test projects, including Docker-backed Aspire API integration tests and Web functional/E2E tests. Docker Desktop, the .NET SDK from `global.json`, Playwright browser prerequisites, and the repository's Keycloak/SQL Server/Mailpit topology must therefore be available before migration starts.

### Test authoring contract

All new tests must:

* Live under `test/` in the correct unit, integration, functional, or E2E project
* Use xUnit and the `MethodName_StateUnderTest_ExpectedResult` naming pattern
* Include an immediately preceding documentation comment that identifies the migration requirement or source behavior, explains the expected result, and states the regression risk
* Keep unit tests in-process and free from network, containers, databases, and file-system dependencies
* Use fake HTTP handlers for IG protocol behavior and real SQL Server integration tests for provider-specific EF behavior
* Preserve API route, status code, JSON, Problem Details, authentication, and authorization contracts unless a recorded decision explicitly changes them

## Implementation Phase 0: Resolve Semantics and Establish Baselines

<!-- parallelizable: false -->

### Step 0.1: Record migration decisions

Record the following decisions in the architecture and runtime wiki pages before changing runtime orchestration:

* Writer model: select single writer, database lease, or optimistic concurrency for startup, scheduled, manual, and configuration-triggered reconciliation
* Local consistency: identify which configuration, credential, runtime-state, event, retry-cycle, snapshot, and audit writes commit atomically
* External effects: define crash recovery and notification delivery guarantees while keeping IG and notification I/O outside database transactions
* Read contract: define missing state, last-reconciled timestamp, stale supervision, and startup-readiness behavior
* Database lifecycle: define migration ownership, existing no-history database transition, Development reset behavior, and deployment-versus-startup migration execution
* Invalid stored configuration: select startup failure, migration/default repair, or explicit operator-error behavior
* IG environments: explicitly support Live or reject it before any network call; never route a Live request to Demo
* Proof and credential durability: define IG proof-data lifetime, token/session scope, and shared Data Protection key-ring persistence
* Shared host-authentication contracts: identify the temporary owner and the condition for removing Web's Application reference

Recommended defaults when no product or deployment requirement overrides them:

* Keep blocking startup reconciliation until an explicit `Starting` or unavailable API contract is approved.
* Expose latest-known state with `LastReconciledAt` and an explicit missing/stale outcome rather than reconciling during reads.
* Reject Live IG requests without a network call until Live operation is authorized and tested.
* Keep provider tokens operation-scoped and inside Infrastructure.
* Retain Web's Application reference temporarily rather than creating a generic shared project.
* Use source-controlled EF Core migrations owned by Infrastructure; define a guarded transition for existing local databases before replacing `EnsureCreated`.

Files:
* `docs/wiki/architecture.md` - Selected responsibility, dependency, writer, and host-contract decisions
* `docs/wiki/runtime-behavior.md` - Reconciliation, consistency, freshness, and external-effect semantics
* `docs/wiki/local-development.md` - Database lifecycle, Docker, key-ring, and startup assumptions
* `docs/wiki/ig-day-trading-with-ig-apis.md` - Demo/Live and session-lifetime decisions

Success criteria:
* Decisions 1 through 3 from the research verification are resolved before Phase 1 runtime work.
* Each deferred operational decision names its blocking phase and default behavior.
* No implementation relies on an implicit concurrency, freshness, or delivery guarantee.

Dependencies:
* Product and deployment input where repository evidence cannot determine semantics.

### Step 0.2: Establish a repeatable full quality gate

Create a repository PowerShell gate that runs the exact solution-qualified build and complete test commands, fails on either non-zero exit, emits timestamped logs and TRX results under the existing `artifacts/` folder, and records aggregate passed, failed, and skipped counts. Keep the script free from test filters so it cannot silently omit Aspire, functional, E2E, or newly added test projects.

Files:
* `.github/scripts/Invoke-CleanArchitectureQualityGate.ps1` - Repeatable migration quality gate
* `docs/wiki/testing-and-quality.md` - Gate usage, prerequisites, evidence location, and serialized retry guidance

Success criteria:
* The script runs `dotnet build TNC.Trading.Platform.slnx` and then `dotnet test TNC.Trading.Platform.slnx` from the repository root.
* The script exits non-zero if either command fails.
* New projects added to the solution are included automatically.
* Gate output identifies the migration phase or subphase and preserves evidence without storing secrets.

Context references:
* `.copilot-tracking/research/subagents/2026-07-26/clean-architecture-migration-plan-verification.md` (Lines 638-704) - Verified lack of current automation and full-suite requirements
* `docs/wiki/testing-and-quality.md` (Lines 219-228) - Existing serialized retry guidance

Dependencies:
* Step 0.1 decisions may remain in progress; the gate itself has no runtime-code dependency.

### Step 0.3: Capture the pre-refactor baseline

Run focused architecture tests, the new full quality gate, and representative manual startup checks before the first refactor. Fix environmental prerequisites rather than changing production behavior to make the baseline pass. If a product defect prevents a clean baseline, isolate and resolve it as a separate work item before migration.

Validation commands:
* `dotnet test test/TNC.Trading.Platform.Architecture/TNC.Trading.Platform.Architecture.IntegrationTests/TNC.Trading.Platform.Architecture.IntegrationTests.csproj`
* `.github/scripts/Invoke-CleanArchitectureQualityGate.ps1 -GateId phase-0-baseline`

Success criteria:
* The solution build passes.
* All solution-listed tests pass with zero failures.
* Baseline API/Web contract responses and startup readiness are recorded for later characterization comparisons.

Dependencies:
* Step 0.2 completion
* Docker-backed local dependencies and Playwright prerequisites

## Implementation Phase 1: Add Characterization and Architecture Safety Rails

<!-- parallelizable: false -->

### Step 1.1: Characterize external and runtime behavior

Add tests around routes, status codes, JSON fields, Problem Details, authorization policies, startup readiness, configuration update behavior, manual retry outcomes, status reads, event reads, and representative Web rendering. Characterization tests should assert observable behavior and must not freeze internal coordinator calls or folder structure.

Files:
* `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/Features/Platform/PlatformEndpointHandlerTests.cs` - Transport mapping characterization
* `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/` - Closed-box API and readiness characterization where unit coverage is insufficient
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/` - View and API-client contract characterization
* `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/` - Current reconciliation, retry, schedule, transition, and persistence-intent behavior

Tests to add or update:
* `HandleAsync_ShouldReturnAccepted_WhenManualRetryIsAccepted`
* `HandleAsync_ShouldReturnConflictProblemDetails_WhenManualRetryIsRejected`
* `GetStatus_ShouldPreserveResponseContract_WhenLatestStateExists`
* `GetEvents_ShouldPreserveOrderingAndShape_WhenEventsExist`
* `Readiness_ShouldBecomeHealthy_WhenStartupInitializationCompletes`

Success criteria:
* Each externally visible behavior that later phases may affect has a regression-focused assertion.
* Existing undocumented test methods touched in this phase receive required documentation comments.

Dependencies:
* Phase 0 baseline passes.

### Step 1.2: Add role-aware architecture fitness tests

Retain the existing topology-neutral graph validator and add a separate repository-specific role policy. Validate project/package/framework references and stable forbidden dependencies without freezing exact file inventories or requiring a Domain project.

Files:
* `test/TNC.Trading.Platform.Architecture/TNC.Trading.Platform.Architecture.IntegrationTests/ArchitectureRules/ProductionProjectRolePolicy.cs` - Stable role policy
* `test/TNC.Trading.Platform.Architecture/TNC.Trading.Platform.Architecture.IntegrationTests/ArchitectureRules/ProductionProjectRoleValidator.cs` - Structured project/package validator
* `test/TNC.Trading.Platform.Architecture/TNC.Trading.Platform.Architecture.IntegrationTests/ArchitectureRules/ProductionProjectRoleValidatorTests.cs` - Policy behavior tests
* `test/TNC.Trading.Platform.Architecture/TNC.Trading.Platform.Architecture.IntegrationTests/ArchitectureRules/RepositoryArchitectureRuleTests.cs` - Repository assertion

Tests to add:
* `Validate_ShouldRejectInfrastructureReference_WhenApplicationReferencesInfrastructure`
* `Validate_ShouldRejectOuterFrameworkReference_WhenApplicationReferencesForbiddenFramework`
* `Validate_ShouldAllowWebApplicationReference_WhenSharedAuthenticationContractsRemain`
* `Validate_ShouldRequireNoProductionReferences_WhenDomainProjectExists`

Success criteria:
* Current intentional exceptions are explicit and carry retirement conditions.
* Domain rules activate conditionally only when a Domain project exists.
* A deliberate local forbidden-reference probe fails the new rule and is removed before the phase gate.

### Step 1.3: Validate safety rails

Update `docs/wiki/architecture.md` and `docs/wiki/testing-and-quality.md` for the implemented characterization and fitness-test scope before running the gate.

Validation commands:
* `dotnet test test/TNC.Trading.Platform.Architecture/TNC.Trading.Platform.Architecture.IntegrationTests/TNC.Trading.Platform.Architecture.IntegrationTests.csproj`
* `dotnet test test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/TNC.Trading.Platform.Api.UnitTests.csproj`
* `.github/scripts/Invoke-CleanArchitectureQualityGate.ps1 -GateId phase-1-safety-rails`

Success criteria:
* Focused suites and the complete solution gate pass before Phase 2 begins.
* `docs/wiki/architecture.md` and `docs/wiki/testing-and-quality.md` describe the enforced rules and their limits.

## Implementation Phase 2: Make Reconciliation Explicit and Queries Side-Effect Free

<!-- parallelizable: false -->

### Step 2.1: Add an explicit reconciliation use case

Create a command-shaped vertical slice as the sole Application entry point for scheduled reconciliation. It may initially delegate to a narrowed coordinator method so query purity and hosting ownership are changed without simultaneously decomposing every write path.

Files:
* `src/TNC.Trading.Platform.Application/Features/ReconcilePlatformAuthentication/ReconcilePlatformAuthenticationRequest.cs` - Command input
* `src/TNC.Trading.Platform.Application/Features/ReconcilePlatformAuthentication/ReconcilePlatformAuthenticationResponse.cs` - Typed result
* `src/TNC.Trading.Platform.Application/Features/ReconcilePlatformAuthentication/ReconcilePlatformAuthenticationHandler.cs` - Use-case entry point
* `src/TNC.Trading.Platform.Application/Services/PlatformStateCoordinator.cs` - Temporary narrowed reconciliation implementation
* `src/TNC.Trading.Platform.Application/Services/PlatformApplicationServiceCollectionExtensions.cs` - Transitional registration

Tests:
* `HandleAsync_ShouldSerializeReconciliation_WhenAnotherWriterIsActive`
* `HandleAsync_ShouldPersistExpectedOutcome_WhenAuthenticationCompletes`
* `HandleAsync_ShouldPropagateCancellation_WhenReconciliationIsCancelled`
* `HandleAsync_ShouldReturnTypedFailure_WhenProviderFails`

Success criteria:
* Startup, scheduled, manual, and configuration-triggered calls use the selected writer model.
* The command does not place IG or notification I/O inside a database transaction.
* Update `docs/wiki/architecture.md` and `docs/wiki/runtime-behavior.md` for the explicit command and writer model before running this step's gate.

Mandatory gate before Step 2.2:
* Run focused reconcile-handler and concurrency tests.
* Run `.github/scripts/Invoke-CleanArchitectureQualityGate.ps1 -GateId phase-2-1-reconcile-command`.
* Do not begin the hosting move until the solution build and unfiltered complete suite pass.

### Step 2.2: Move supervision hosting to API

Move cadence, scope creation, cancellation, logging, and error continuation from Application `BackgroundService` types into an API-owned hosted adapter that invokes the reconcile handler. Preserve the current cadence and blocking startup ordering unless Phase 0 explicitly changed those contracts.

Files:
* `src/TNC.Trading.Platform.Api/Hosting/PlatformAuthenticationSupervisor.cs` - API hosted adapter
* `src/TNC.Trading.Platform.Api/Hosting/PlatformAuthenticationSupervisorTickRunner.cs` - Optional testable tick runner when it removes real hosting complexity
* `src/TNC.Trading.Platform.Api/Program.cs` - Composition and blocking startup invocation
* `src/TNC.Trading.Platform.Application/Services/PlatformAuthSupervisor.cs` - Delete after callers move
* `src/TNC.Trading.Platform.Application/Services/PlatformAuthSupervisorRunner.cs` - Delete or reduce after callers move

Tests:
* `ExecuteAsync_ShouldContinueAfterTransientFailure_WhenNextTickRuns`
* `ExecuteAsync_ShouldStopPromptly_WhenCancellationIsRequested`
* `StartAsync_ShouldCompleteInitialReconciliation_BeforeReadinessIsHealthy`

Success criteria:
* Application contains no background-service scheduling mechanics for reconciliation.
* API remains a thin host adapter and does not acquire business decisions.
* Update `docs/wiki/architecture.md`, `docs/wiki/runtime-behavior.md`, `docs/wiki/testing-and-quality.md`, and `docs/wiki/local-development.md` for the implemented hosting and startup behavior before running this step's gate.

Mandatory gate before Step 2.3:
* Run focused Application and API hosting tests.
* Run `.github/scripts/Invoke-CleanArchitectureQualityGate.ps1 -GateId phase-2-2-api-supervision`.
* Do not begin the query rewrite until the solution build and unfiltered complete suite pass.

### Step 2.3: Replace mutating queries with projection reads

Add non-creating read ports owned by the consuming features. Remove `TickAsync` calls from status and event queries. Return the latest-known state plus the Phase 0 freshness/missing-state outcome; do not create state, authenticate, notify, or persist during reads.

Files:
* `src/TNC.Trading.Platform.Application/Features/GetPlatformStatus/GetPlatformStatusHandler.cs` - Pure query handler
* `src/TNC.Trading.Platform.Application/Features/GetPlatformStatus/Ports/` - Status projection read contract
* `src/TNC.Trading.Platform.Application/Features/GetPlatformEvents/GetPlatformEventsHandler.cs` - Pure event query
* `src/TNC.Trading.Platform.Application/Features/GetPlatformEvents/Ports/` - Event projection read contract
* `src/TNC.Trading.Platform.Application/Services/IPlatformRuntimeStateStore.cs` - Retire creating reads from query paths
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/EfPlatformRuntimeStateStore.cs` - Non-creating adapter behavior
* `src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs` - Transport mapping only if the approved read contract changes

Tests:
* `HandleAsync_ShouldNotInvokeReconciliation_WhenStatusIsRead`
* `HandleAsync_ShouldReturnExplicitMissingState_WhenRuntimeStateDoesNotExist`
* `HandleAsync_ShouldReturnFreshnessMetadata_WhenLatestStateExists`
* `HandleAsync_ShouldOnlyReadEvents_WhenEventsAreRequested`
* `HandleAsync_ShouldPerformNoWrites_WhenEventsAreRequested`

Success criteria:
* Status and event reads perform no IG calls, writes, inserts, notifications, or transitions.
* Existing tests that expected reads to reconcile are moved to reconcile-command coverage rather than deleted.
* Update `docs/wiki/runtime-behavior.md`, `docs/wiki/api-reference.md`, `docs/wiki/operator-guide.md`, and `docs/wiki/testing-and-quality.md` for the implemented missing/freshness contract before running this step's gate.

Mandatory gate before Step 2.4:
* Run focused status/event handler and API contract tests.
* Run `.github/scripts/Invoke-CleanArchitectureQualityGate.ps1 -GateId phase-2-3-query-purity`.
* Do not begin another significant refactor until the solution build and unfiltered complete suite pass.

### Step 2.4: Validate explicit reconciliation and query purity

Validation commands:
* `dotnet test test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/TNC.Trading.Platform.Application.UnitTests.csproj`
* `dotnet test test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/TNC.Trading.Platform.Api.UnitTests.csproj`
* `dotnet test test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/TNC.Trading.Platform.Api.IntegrationTests.csproj`
* Verify the recorded `phase-2-1-reconcile-command`, `phase-2-2-api-supervision`, and `phase-2-3-query-purity` evidence.

Success criteria:
* All three independent refactor gates pass before policy ownership changes.
* All affected wiki pages were updated before their corresponding refactor gate.

Dependencies:
* Phase 0 writer and freshness decisions
* Phase 1 safety rails

## Implementation Phase 3: Move Validation and Business Policy Inward

<!-- parallelizable: false -->

### Step 3.1: Enforce configuration invariants in Application

Keep API validation limited to malformed transport input and field-to-Problem-Details mapping. Enforce schedule, trading-day, time-zone, retry-bound, Test/Live compatibility, provider selection, and actor rules in the Application use case so non-HTTP callers cannot bypass them.

Files:
* `src/TNC.Trading.Platform.Application/Features/UpdatePlatformConfiguration/UpdatePlatformConfigurationValidator.cs` - Use-case validation
* `src/TNC.Trading.Platform.Application/Features/UpdatePlatformConfiguration/UpdatePlatformConfigurationHandler.cs` - Typed validation outcome handling
* `src/TNC.Trading.Platform.Application/Configuration/TradingScheduleConfiguration.cs` - Validated construction candidate
* `src/TNC.Trading.Platform.Application/Configuration/RetryPolicyConfiguration.cs` - Validated construction candidate
* `src/TNC.Trading.Platform.Api/Features/UpdatePlatformConfiguration/UpdatePlatformConfigurationValidator.cs` - Transport-only checks

Tests:
* `Validate_ShouldRejectEmptyTradingDays_WhenConfigurationIsUpdated`
* `Validate_ShouldRejectUnknownTimeZone_WhenConfigurationIsUpdated`
* `Validate_ShouldRejectMaxDelayBelowInitialDelay_WhenRetryPolicyIsInvalid`
* `HandleAsync_ShouldRejectInvalidConfiguration_WithoutPersistingChanges`
* `HandleAsync_ShouldPreserveProblemDetailsFields_WhenUseCaseRejectsConfiguration`

Success criteria:
* Invalid use-case input is rejected outside HTTP as well as through API.
* Invalid persisted/bootstrap configuration follows the recorded Phase 0 behavior.
* Update `docs/wiki/api-reference.md`, `docs/wiki/operator-guide.md`, and `docs/wiki/runtime-behavior.md` for implemented validation behavior before running this step's gate.

Mandatory gate before Step 3.2:
* Run focused Application validator/handler and API Problem Details tests.
* Run `.github/scripts/Invoke-CleanArchitectureQualityGate.ps1 -GateId phase-3-1-configuration-invariants`.
* Do not move policy ownership until the solution build and unfiltered complete suite pass.

### Step 3.2: Move policy decisions from mechanisms to the core

Move restart classification, retention eligibility/cutoff, operational sensitivity/redaction classification, trading schedule, retry timing, and state-transition decisions into framework-neutral Application policy. Leave EF deletion, provider transport, encryption, JSON mapping, and notification delivery in Infrastructure.

Treat each policy family as a separate significant refactor in this order, with no combined schedule/retry change:

1. Trading schedule: `phase-3-2-trading-schedule`
2. Retry timing: `phase-3-3-retry-timing`
3. Authentication state transitions: `phase-3-4-auth-transitions`
4. Restart classification: `phase-3-5-restart-classification`
5. Retention eligibility/cutoff: `phase-3-6-retention-policy`
6. Operational sensitivity/redaction classification: `phase-3-7-sensitivity-policy`

For each policy family, move its behavior and tests together, update affected wiki ownership/behavior statements, run focused Application and Infrastructure tests, and run the named gate. Do not start the next policy family until the solution build and unfiltered complete suite pass.

Files:
* `src/TNC.Trading.Platform.Application/Services/TradingScheduleGate.cs` - Schedule policy pending Domain gate
* `src/TNC.Trading.Platform.Application/Services/PlatformStateTransitionEngine.cs` - Transition policy pending Domain gate
* `src/TNC.Trading.Platform.Application/Configuration/PlatformRuntimeState.cs` - Replace unrestricted mutation with controlled transitions where feasible
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/PlatformConfigurationRestartPolicy.cs` - Retire after inward move
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/OperationalRecordRetentionPolicy.cs` - Retain only mechanism-facing translation after policy moves
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/OperationalDataRedactor.cs` - Consume inward classification rather than decide sensitivity

Tests:
* `DecideTickAction_ShouldReturnBlockedLive_WhenTestPlatformTargetsLiveBroker`
* `IsRestartRequired_ShouldReturnTrue_WhenStartupFixedEnvironmentChanges`
* `CreatePlan_ShouldExcludeCurrentRecords_WhenRetentionCutoffIsEvaluated`
* `Classify_ShouldMarkCredentialMetadataSensitive_WhenOperationalDataIsEvaluated`
* `Apply_ShouldRejectInvalidTransition_WhenAuthenticationStateChanges`

Success criteria:
* Policy tests run without Infrastructure, a host, database, container, provider, or file system.
* Each moved top-level type has its own matching file.
* Adapter tests remain in Infrastructure and policy tests move to Application only after ownership moves.
* `docs/wiki/architecture.md` and `docs/wiki/runtime-behavior.md`, plus `docs/wiki/operator-guide.md` when behavior is operator-visible, are current before each policy-family gate.

### Step 3.3: Evaluate the Domain extraction gate

Evaluate the validated trading-schedule cluster and the closed authentication-transition cluster against every gate:

1. Validated construction or invariant-preserving transitions
2. Domain-owned inputs rather than Application snapshots or mutable persistence projections
3. Direct in-process tests without outer dependencies
4. One-way Application mapping with no reverse dependency
5. Meaningful compile-time protection from a real forbidden dependency
6. Behavior, values, results, and tests can move together

If neither cluster passes, record `Domain deferred` and continue with a purified combined Application core. Do not create an empty project. If one cluster passes, execute Step 3.4 as its own significant refactor.

### Step 3.4: Conditionally introduce Domain through one cohesive cluster

Create Domain only when Step 3.3 passes. Move one complete policy cluster with its values, results, and tests; keep requests, responses, handlers, persistence ports, query projections, provider DTOs, host options, and EF-shaped records out.

Files:
* `src/TNC.Trading.Platform.Domain/TNC.Trading.Platform.Domain.csproj` - Framework-neutral policy assembly
* `test/TNC.Trading.Platform.Domain/TNC.Trading.Platform.Domain.UnitTests/TNC.Trading.Platform.Domain.UnitTests.csproj` - Direct policy tests
* `TNC.Trading.Platform.slnx` - Register production and test projects
* `src/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.csproj` - Add `Application -> Domain`
* `test/TNC.Trading.Platform.Architecture/TNC.Trading.Platform.Architecture.IntegrationTests/ArchitectureRules/` - Enforce zero outward Domain dependencies

Success criteria:
* Domain has no production project reference and no ASP.NET Core, EF Core, HTTP, Aspire, Azure, UI, or provider dependency.
* Infrastructure references Domain only when a concrete mapping directly consumes a Domain type.
* The moved policy cluster's direct tests pass independently.
* Update `docs/wiki/architecture.md`, `docs/wiki/application-overview.md`, and `docs/wiki/testing-and-quality.md` for the implemented project graph, policy ownership, and Domain test project before the Domain gate.

Mandatory gate after conditional Domain extraction:
* Run direct Domain tests and architecture rules.
* Run `.github/scripts/Invoke-CleanArchitectureQualityGate.ps1 -GateId phase-3-domain-extraction`.
* Do not begin another significant refactor until the solution build and unfiltered complete suite pass.

### Step 3.5: Validate policy ownership and optional Domain boundary

Validation commands:
* `dotnet test test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/TNC.Trading.Platform.Application.UnitTests.csproj`
* `dotnet test test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/TNC.Trading.Platform.Infrastructure.UnitTests.csproj`
* `dotnet test test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/TNC.Trading.Platform.Api.UnitTests.csproj`
* `dotnet test test/TNC.Trading.Platform.Architecture/TNC.Trading.Platform.Architecture.IntegrationTests/TNC.Trading.Platform.Architecture.IntegrationTests.csproj`
* `dotnet test test/TNC.Trading.Platform.Domain/TNC.Trading.Platform.Domain.UnitTests/TNC.Trading.Platform.Domain.UnitTests.csproj` only when Domain exists
* Verify the recorded `phase-3-1-configuration-invariants`, `phase-3-2-trading-schedule`, `phase-3-3-retry-timing`, `phase-3-4-auth-transitions`, `phase-3-5-restart-classification`, `phase-3-6-retention-policy`, and `phase-3-7-sensitivity-policy` gates.
* Verify `phase-3-domain-extraction` when Step 3.4 executed.

Success criteria:
* Every configuration and policy-family refactor has its own passing full gate.
* Relevant architecture, runtime, API, operator, application-overview, and testing wiki pages were updated before their corresponding gate.

Dependencies:
* Phase 2 state and read ownership

## Implementation Phase 4: Normalize the IG Outbound Adapter

<!-- parallelizable: false -->

Steps 4.1 and 4.2 form one atomic significant refactor because the new Application port has no valid production implementation until its Infrastructure adapter and registration exist. Keep both steps in one compile-safe change, then run Step 4.3 before any later refactor.

### Step 4.1: Define an operation-scoped inward port

Replace the provider-shaped `IIgSessionClient` boundary with an Application-owned authenticate-and-collect-proof capability. The request and result must use inward vocabulary and exclude API keys, CST/security tokens, HTTP headers, provider JSON records, and provider exceptions.

Files:
* `src/TNC.Trading.Platform.Application/Features/PlatformAuthentication/Ports/IBrokerAuthenticationGateway.cs` - Operation-scoped port
* `src/TNC.Trading.Platform.Application/Features/PlatformAuthentication/Ports/BrokerAuthenticationRequest.cs` - Inward request
* `src/TNC.Trading.Platform.Application/Features/PlatformAuthentication/Ports/BrokerAuthenticationOutcome.cs` - Secret-safe result
* `src/TNC.Trading.Platform.Application/Features/PlatformAuthentication/Ports/BrokerAuthenticationFailure.cs` - Typed failure
* `src/TNC.Trading.Platform.Application/Features/PlatformAuthentication/Ports/BrokerAuthenticationEvidence.cs` - Sanitized evidence

Success criteria:
* Application source no longer requires IG protocol values to coordinate authentication and proof collection.
* The port represents a current use-case capability, not a generic provider client or durable session abstraction.

### Step 4.2: Translate protocol details inside Infrastructure

Implement the port with an Infrastructure adapter that performs login and dependent proof calls in one operation, keeps secrets local, translates status/failure categories, and discards raw tokens before returning. Apply the Phase 0 Demo/Live decision explicitly.

Files:
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/Ig/IgBrokerAuthenticationGateway.cs` - Outbound adapter
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/Ig/IgSessionClient.cs` - Transitional implementation to retire
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/PlatformInfrastructureServiceCollectionExtensions.cs` - Environment-aware registration
* `src/TNC.Trading.Platform.Application/Services/PlatformIgProofDataEnricher.cs` - Retire or reshape after adapter consolidation
* `src/TNC.Trading.Platform.Application/Infrastructure/Ig/` - Delete provider wire models after all callers migrate

Tests:
* `AuthenticateAndCollectProofAsync_ShouldUseDemoEndpoint_WhenBrokerEnvironmentIsDemo`
* `AuthenticateAndCollectProofAsync_ShouldRejectLiveWithoutNetworkCall_WhenLiveIsUnsupported`
* `AuthenticateAndCollectProofAsync_ShouldExcludeSessionTokens_WhenResultCrossesApplicationBoundary`
* `AuthenticateAndCollectProofAsync_ShouldTranslateFailure_WhenIgReturnsEachSupportedErrorClass`
* `Validate_ShouldRejectProviderProtocolType_WhenApplicationMentionsHttpOrIgHeaders`

Success criteria:
* Raw tokens and provider exceptions do not cross into Application.
* Existing sanitizer and mapper behavior is covered at the adapter boundary before old Application types are removed.

### Step 4.3: Validate the IG boundary

Update `docs/wiki/architecture.md`, `docs/wiki/runtime-behavior.md`, `docs/wiki/testing-and-quality.md`, and `docs/wiki/ig-day-trading-with-ig-apis.md` for the implemented adapter, token lifetime, and environment routing before running the gate.

Validation commands:
* `dotnet test test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/TNC.Trading.Platform.Infrastructure.UnitTests.csproj --filter "FullyQualifiedName~Ig"`
* `dotnet test test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/TNC.Trading.Platform.Infrastructure.UnitTests.csproj`
* `dotnet test test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/TNC.Trading.Platform.Application.UnitTests.csproj`
* `dotnet test test/TNC.Trading.Platform.Architecture/TNC.Trading.Platform.Architecture.IntegrationTests/TNC.Trading.Platform.Architecture.IntegrationTests.csproj`
* `.github/scripts/Invoke-CleanArchitectureQualityGate.ps1 -GateId phase-4-ig-boundary`

Success criteria:
* Focused and complete gates pass before coordinator decomposition.
* IG, runtime, architecture, and testing wiki pages describe the implemented provider boundary and environment behavior.

Dependencies:
* Phase 3 inward policy vocabulary
* Phase 0 IG environment and proof-lifetime decisions

## Implementation Phase 5: Extract Manual Retry as the First Complete Write Slice

<!-- parallelizable: false -->

### Step 5.1: Move manual retry orchestration into its handler

Make `TriggerManualAuthRetryHandler` own the use case: load state, ask policy for eligibility, call the broker gateway, apply the outcome, and request an operation-specific local commit. Replace normal `InvalidOperationException` conflicts with a typed outcome while preserving `202 Accepted` and `409 Conflict` at the API boundary.

Files:
* `src/TNC.Trading.Platform.Application/Features/TriggerManualAuthRetry/TriggerManualAuthRetryHandler.cs` - Use-case orchestration
* `src/TNC.Trading.Platform.Application/Features/TriggerManualAuthRetry/TriggerManualAuthRetryResponse.cs` - Response contract
* `src/TNC.Trading.Platform.Application/Features/TriggerManualAuthRetry/TriggerManualAuthRetryOutcome.cs` - Typed result
* `src/TNC.Trading.Platform.Application/Features/TriggerManualAuthRetry/ManualAuthRetryRejectionReason.cs` - Typed rejection
* `src/TNC.Trading.Platform.Application/Features/TriggerManualAuthRetry/Ports/` - Narrow state load and local commit intents
* `src/TNC.Trading.Platform.Api/Features/Platform/TriggerManualAuthRetryEndpointHandler.cs` - Typed result-to-HTTP translation
* `src/TNC.Trading.Platform.Application/Services/PlatformStateCoordinator.cs` - Remove manual retry behavior after migration

Tests:
* `HandleAsync_ShouldRejectWithScheduleInactiveReason_WhenTradingScheduleIsInactive`
* `HandleAsync_ShouldRejectWithBlockedLiveReason_WhenTestPlatformTargetsLiveBroker`
* `HandleAsync_ShouldRejectWithoutWrites_WhenRetryLimitIsNotReached`
* `HandleAsync_ShouldCommitNewRetryCycle_WhenManualRetryIsAllowed`
* `HandleAsync_ShouldPropagateCancellation_WhenBrokerCallIsCancelled`
* `HandleAsync_ShouldMapTypedRejectionToConflict_WhenManualRetryIsUnavailable`
* Failure-injection tests for every boundary covered by the Phase 0 consistency decision

Success criteria:
* The handler has direct unit tests with fakes and no Infrastructure reference.
* State, retry-cycle, event, snapshot, and audit writes preserve the selected atomic commit intent.
* IG and notification I/O remain outside the SQL transaction and follow recorded retry/idempotency semantics.
* Update `docs/wiki/architecture.md`, `docs/wiki/runtime-behavior.md`, and `docs/wiki/api-reference.md` for direct handler ownership and preserved HTTP behavior before running this step's gate.

Mandatory gate before Step 5.2:
* Run direct manual retry handler, API mapping, persistence, and failure-injection tests.
* Run `.github/scripts/Invoke-CleanArchitectureQualityGate.ps1 -GateId phase-5-1-manual-retry`.
* Do not reclassify test projects until the solution build and unfiltered complete suite pass.

### Step 5.2: Reclassify existing workflow tests

Move EF/Data Protection/adapter-backed scenarios out of the Application unit boundary. Keep pure handler/policy tests in Application; place adapter tests in Infrastructure and add a real SQL integration project only for behavior requiring SQL Server semantics.

Files:
* `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/TNC.Trading.Platform.Application.UnitTests.csproj` - Remove Infrastructure reference when no test requires it
* `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/TriggerManualAuthRetry/TriggerManualAuthRetryHandlerTests.cs` - Pure use-case tests
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/` - Adapter/mechanism tests
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/` - Create only for real SQL Server transaction/concurrency behavior
* `TNC.Trading.Platform.slnx` - Register an Infrastructure integration project if created

Success criteria:
* Application unit tests no longer construct EF, Data Protection, notification, or provider implementations.
* Test project labels match their actual infrastructure boundary.
* Update `docs/wiki/testing-and-quality.md` for the implemented test project topology before running this step's gate.

Mandatory gate before Step 5.3:
* Run Application unit, Infrastructure unit/integration, and architecture tests.
* Run `.github/scripts/Invoke-CleanArchitectureQualityGate.ps1 -GateId phase-5-2-test-boundaries`.
* Do not begin another significant refactor until the solution build and unfiltered complete suite pass.

### Step 5.3: Validate the first write slice

Validation commands:
* `dotnet test test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/TNC.Trading.Platform.Application.UnitTests.csproj --filter "FullyQualifiedName~TriggerManualAuthRetryHandlerTests"`
* `dotnet test test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/TNC.Trading.Platform.Api.UnitTests.csproj --filter "FullyQualifiedName~PlatformEndpointHandlerTests"`
* `dotnet test test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/TNC.Trading.Platform.Infrastructure.UnitTests.csproj`
* Verify the recorded `phase-5-1-manual-retry` and `phase-5-2-test-boundaries` evidence.

Success criteria:
* Both independent full gates pass before another coordinator operation moves.
* Architecture, runtime, API, and testing wiki pages were updated before their corresponding gate.

Dependencies:
* Phases 2 through 4

## Implementation Phase 6: Extract Remaining Operations One at a Time

<!-- parallelizable: false -->

Each step in this phase is an independent significant refactor. Run its focused tests and the full quality gate before starting the next step. Do not execute these steps in parallel because they share coordinator, registration, persistence, and API surfaces.

### Step 6.1: Extract auth audit recording

Keep claim and HTTP binding in API; move event intent, configuration lookup, and persistence orchestration into a new Application vertical slice.

Files:
* `src/TNC.Trading.Platform.Application/Features/RecordAuthAuditEvent/RecordAuthAuditEventRequest.cs`
* `src/TNC.Trading.Platform.Application/Features/RecordAuthAuditEvent/RecordAuthAuditEventResponse.cs`
* `src/TNC.Trading.Platform.Application/Features/RecordAuthAuditEvent/RecordAuthAuditEventHandler.cs`
* `src/TNC.Trading.Platform.Api/Features/Platform/RecordAuthAuditEventEndpointHandler.cs`
* `src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs`
* `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/RecordAuthAuditEvent/RecordAuthAuditEventHandlerTests.cs`

Required coverage:
* Success, missing configuration, persistence failure translation, cancellation, and no-write rejection
* Existing route, authorization, request, response, and Problem Details behavior

Gate:
* `.github/scripts/Invoke-CleanArchitectureQualityGate.ps1 -GateId phase-6-1-auth-audit`

### Step 6.2: Extract configuration update

Move orchestration from `PlatformConfigurationService` into the existing feature handler while preserving one scoped local commit for credentials, configuration, and audit records. Route post-update reconciliation through the explicit command.

Files:
* `src/TNC.Trading.Platform.Application/Features/UpdatePlatformConfiguration/`
* `src/TNC.Trading.Platform.Application/Services/PlatformConfigurationService.cs`
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/SqlPlatformConfigurationStore.cs`
* Direct Application handler tests and Infrastructure persistence tests

Required coverage:
* Valid update, invalid update without writes, credential-presence behavior, atomic local commit, restart-required outcome, cancellation, and reconciliation dispatch

Gate:
* `.github/scripts/Invoke-CleanArchitectureQualityGate.ps1 -GateId phase-6-2-configuration-update`

### Step 6.3: Complete reconciliation extraction

Move remaining `TickAsync` workflow behavior into the reconcile handler and feature-local collaborators. Keep state-transition policy separate from broker, persistence, notification, and hosting adapters. Delete coordinator reconciliation methods only after all callers have moved.

Files:
* `src/TNC.Trading.Platform.Application/Features/ReconcilePlatformAuthentication/`
* `src/TNC.Trading.Platform.Application/Services/PlatformStateCoordinator.cs`
* Feature-local ports and Infrastructure implementations required by the use case
* Direct handler, policy, failure-injection, and concurrency tests

Required coverage:
* Success, each transition class, missing configuration, provider failure, notification failure, retry scheduling, idempotency/replay, cancellation, and writer contention

Gate:
* `.github/scripts/Invoke-CleanArchitectureQualityGate.ps1 -GateId phase-6-3-reconciliation`

### Step 6.4: Complete status and event query extraction

Remove coordinator dependencies from both query handlers after projection ports are stable. Preserve transport mappings and do not add write-capable stores to query handlers.

Files:
* `src/TNC.Trading.Platform.Application/Features/GetPlatformStatus/`
* `src/TNC.Trading.Platform.Application/Features/GetPlatformEvents/`
* Corresponding Infrastructure projection adapters and direct handler tests

Required coverage:
* Existing/missing/stale status, event ordering/paging if present, cancellation, and strict no-write/no-provider-call assertions

Gate:
* `.github/scripts/Invoke-CleanArchitectureQualityGate.ps1 -GateId phase-6-4-status-events`

### Step 6.5: Review IG login history boundary

Retain the current slice when its port is already narrow and query-shaped. Refactor only if it still exposes a broad store, provider DTO, EF entity, or coordinator dependency after prior extractions.

Files:
* `src/TNC.Trading.Platform.Application/Features/GetIgLoginHistory/`
* Corresponding Infrastructure adapter and direct handler tests only when a real boundary issue remains

Required coverage when changed:
* Existing/missing history, projection mapping, cancellation, ordering, and no-write behavior

Gate when changed:
* `.github/scripts/Invoke-CleanArchitectureQualityGate.ps1 -GateId phase-6-5-ig-history`

### Step 6.6: Retire the coordinator

Delete `PlatformStateCoordinator` only after code-usage search proves no caller remains and every operation has an explicit handler plus direct tests. Remove dead registrations and broad ports without weakening coverage.

Files:
* `src/TNC.Trading.Platform.Application/Services/PlatformStateCoordinator.cs` - Delete
* `src/TNC.Trading.Platform.Application/Services/PlatformApplicationServiceCollectionExtensions.cs` - Remove registration
* Affected tests and architecture rules

Gate:
* `.github/scripts/Invoke-CleanArchitectureQualityGate.ps1 -GateId phase-6-6-coordinator-retirement`

Success criteria:
* Every migrated operation has one request, response, handler, validator when required, narrow inward ports, and direct unit tests.
* Every subphase passes the complete solution gate before the next begins.
* Wiki pages are updated within the subphase that changes implemented behavior or ownership.

Dependencies:
* Phase 5 establishes the write-slice pattern.

## Implementation Phase 7: Clean Startup and Database Lifecycle

<!-- parallelizable: false -->

### Step 7.1: Introduce Infrastructure-owned schema migrations

Implement the Phase 0 database decision with source-controlled EF Core migrations beside `PlatformDbContext`. Define the no-history database transition and retain or remove Development deletion only as explicitly decided. Add real SQL Server integration tests for migration, constraints, transaction rollback, concurrency, and SQL-specific retention behavior.

Files:
* `src/TNC.Trading.Platform.Infrastructure/` - Context-owned migrations and mappings
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/` - Real SQL Server integration suite
* `TNC.Trading.Platform.slnx` - Test project registration

Tests:
* `MigrateAsync_ShouldCreateCurrentSchema_WhenDatabaseIsEmpty`
* `MigrateAsync_ShouldUpgradeWithoutDataLoss_WhenNoHistoryDatabaseIsTransitioned`
* `CommitAsync_ShouldRollbackAllLocalWrites_WhenOneWriteFails`
* `SaveAsync_ShouldRejectStaleWriter_WhenConcurrencyVersionChanged`
* `DeleteExpiredAsync_ShouldPreserveCurrentRecords_WhenSqlServerExecutesRetention`

Success criteria:
* Schema evolution no longer relies on `EnsureCreated` for deployed databases.
* Provider-specific behavior is not claimed from EF InMemory tests.
* Update `docs/wiki/architecture.md`, `docs/wiki/runtime-behavior.md`, `docs/wiki/local-development.md`, and `docs/wiki/testing-and-quality.md` for the implemented migration and SQL-test model before running this step's gate.

Mandatory gate before Step 7.2:
* Run focused Infrastructure migration, transaction, concurrency, and retention tests.
* Run `.github/scripts/Invoke-CleanArchitectureQualityGate.ps1 -GateId phase-7-1-schema-migrations`.
* Do not begin startup composition changes until the solution build and unfiltered complete suite pass.

### Step 7.2: Add one startup initialization surface

Move schema lifecycle, bootstrap configuration, retention startup work, and initialization ordering behind an Infrastructure-owned composition surface. API invokes it without resolving `PlatformDbContext` or concrete processors. Keep business reconciliation behind its Application handler.

Files:
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Startup/PlatformStartupInitializer.cs` - Initialization mechanism
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Startup/PlatformStartupExtensions.cs` - Composition surface when preferable to a pass-through interface
* `src/TNC.Trading.Platform.Api/Program.cs` - Startup orchestration only
* `src/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.csproj` - Remove duplicate EF/Azure packages when unused

Tests:
* `InitializeAsync_ShouldApplyStepsInRequiredOrder_WhenApiStarts`
* `InitializeAsync_ShouldFailReadiness_WhenMigrationFails`
* `InitializeAsync_ShouldNotExposeDbContext_ToApiComposition`

Success criteria:
* API startup no longer directly performs EF schema or retention mechanics.
* Startup readiness and failure behavior match the Phase 0 decision.
* Update `docs/wiki/architecture.md`, `docs/wiki/runtime-behavior.md`, and `docs/wiki/local-development.md` for the implemented initializer and readiness order before running this step's gate.

Mandatory gate before Step 7.3:
* Run focused Infrastructure, API, AppHost, and startup readiness tests.
* Run `.github/scripts/Invoke-CleanArchitectureQualityGate.ps1 -GateId phase-7-2-startup-initializer`.
* Do not begin another significant refactor until the solution build and unfiltered complete suite pass.

### Step 7.3: Validate startup and database lifecycle

Validation commands:
* `dotnet test test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/TNC.Trading.Platform.Infrastructure.IntegrationTests.csproj`
* `dotnet test test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/TNC.Trading.Platform.Api.UnitTests.csproj`
* `dotnet test test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/TNC.Trading.Platform.Api.IntegrationTests.csproj`
* `dotnet test test/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.UnitTests/TNC.Trading.Platform.AppHost.UnitTests.csproj`
* Verify the recorded `phase-7-1-schema-migrations` and `phase-7-2-startup-initializer` evidence.

Success criteria:
* Both independent full gates pass.
* Architecture, runtime, local development, and testing wiki pages were updated before their corresponding gate.

Dependencies:
* Phase 0 database decision
* Phase 6 explicit reconciliation ownership

## Implementation Phase 8: Remove Hosting and Framework Concerns from Application

<!-- parallelizable: false -->

This is an independent sequencing lane that becomes eligible immediately after Phase 2 and the Phase 0 shared host-authentication contract decision. It does not depend on Phases 3 through 7 or database lifecycle work. Execute it immediately after Phase 2 unless a concrete remaining Application framework use is named as a narrower prerequisite; do not use unresolved migration decisions as a reason to defer host-framework cleanup.

### Step 8.1: Move authentication and authorization registration outward

Move ASP.NET Core authorization policies, options binding, and host registration into API and Web. Keep only genuinely framework-neutral role, scope, claim, provider, and audit vocabulary at an inward/shared boundary. Do not create a generic shared project to eliminate a reference cosmetically.

Files:
* `src/TNC.Trading.Platform.Api/Authentication/PlatformApiAuthenticationServiceCollectionExtensions.cs`
* `src/TNC.Trading.Platform.Web/Authentication/PlatformWebAuthenticationServiceCollectionExtensions.cs`
* `src/TNC.Trading.Platform.Application/Authentication/PlatformAuthorizationPolicyRegistration.cs` - Delete after host-local registration exists
* API and Web authentication unit/integration tests

Tests:
* `AddPlatformAuthentication_ShouldRegisterRequiredPolicies_WhenApiStarts`
* `AddPlatformAuthentication_ShouldRegisterRequiredPolicies_WhenWebStarts`
* Existing role, scope, claim, sign-in, sign-out, and route-denial tests

Success criteria:
* Application no longer imports ASP.NET Core authorization or host options types.
* API and Web continue to enforce identical documented policy names and semantics.
* Update `docs/wiki/architecture.md`, `docs/wiki/application-overview.md`, and `docs/wiki/testing-and-quality.md` for host-local authentication ownership before running this step's gate.

Mandatory gate before Step 8.2:
* Run focused API, Web, AppHost, authentication, and architecture tests.
* Run `.github/scripts/Invoke-CleanArchitectureQualityGate.ps1 -GateId phase-8-1-host-auth-ownership`.
* Do not remove framework/package references until the solution build and unfiltered complete suite pass.

### Step 8.2: Remove remaining Application framework dependencies

Move or replace DI extensions, `IConfiguration`, logging, and hosting concerns so Application code receives explicit inward values and dependencies. Remove `Microsoft.AspNetCore.App` only after source and architecture tests prove no use remains.

Files:
* `src/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.csproj` - Remove framework reference
* `src/TNC.Trading.Platform.Application/Services/PlatformApplicationServiceCollectionExtensions.cs` - Move composition outward or narrow to framework-neutral registration only if justified
* API/Web composition extensions - Own host-specific registration
* `src/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.csproj` - Remove Application reference only when no intentional contract remains

Success criteria:
* Application builds without `Microsoft.AspNetCore.App`.
* Web either removes Application or records the remaining narrow contract and retirement condition.
* API's Infrastructure use is limited to registration and startup composition surfaces.
* Update `docs/wiki/architecture.md`, `docs/wiki/application-overview.md`, and `docs/wiki/testing-and-quality.md` for the implemented framework-neutral boundary before running this step's gate.

Mandatory gate before Step 8.3:
* Run focused Application, API, Web, AppHost, and architecture tests.
* Run `.github/scripts/Invoke-CleanArchitectureQualityGate.ps1 -GateId phase-8-2-framework-reference-removal`.
* Do not begin another significant refactor until the solution build and unfiltered complete suite pass.

### Step 8.3: Validate framework neutrality

Validation commands:
* `dotnet test test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/TNC.Trading.Platform.Api.UnitTests.csproj`
* `dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj`
* `dotnet test test/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.UnitTests/TNC.Trading.Platform.AppHost.UnitTests.csproj`
* `dotnet test test/TNC.Trading.Platform.Architecture/TNC.Trading.Platform.Architecture.IntegrationTests/TNC.Trading.Platform.Architecture.IntegrationTests.csproj`
* Verify the recorded `phase-8-1-host-auth-ownership` and `phase-8-2-framework-reference-removal` evidence.

Success criteria:
* Both independent full gates pass with real authentication flows intact.
* Architecture, application overview, and testing wiki pages were updated before their corresponding gate.

Dependencies:
* Phase 2 hosting move
* Phase 0 shared host-contract decision

## Implementation Phase 9: Reorganize Infrastructure Mechanically

<!-- parallelizable: false -->

### Step 9.1: Move one adapter family per batch

After ownership is stable, move adapter files from `Infrastructure/Infrastructure/Platform` into boundary-oriented folders. Use compiler-assisted moves and namespace updates. Do not redesign policy, ports, transactions, or behavior in the same batch.

Target layout:
* `DependencyInjection/`
* `Persistence/EntityFramework/Entities/`
* `Persistence/EntityFramework/Configurations/`
* `Persistence/EntityFramework/Migrations/`
* `Configuration/SqlServer/`
* `Credentials/DataProtection/`
* `Integrations/Ig/`
* `Notifications/Recorded/`
* `Notifications/Smtp/`
* `Notifications/AzureCommunicationServices/`
* `Operations/Retention/`
* `Startup/`
* `Time/`

Move order and required gate IDs:
1. Time and isolated helpers: `phase-9-1-time`
2. IG integration: `phase-9-2-ig-integration`
3. Notifications: `phase-9-3-notifications`
4. Credentials and configuration: `phase-9-4-credentials-configuration`
5. Retention operations: `phase-9-5-retention`
6. EF persistence and startup: `phase-9-6-persistence-startup`
7. Dependency injection registrations: `phase-9-7-dependency-injection`

For each numbered move batch:

* Update namespaces, registration, tests, and friend access.
* Update `docs/wiki/architecture.md` with the implemented adapter layout before the batch gate when documented paths or namespaces change.
* Run the affected Infrastructure/API tests.
* Run the concrete gate ID assigned to that batch above.
* Do not begin the next move until the full gate passes.

Success criteria:
* The redundant `Infrastructure.Infrastructure` namespace is gone.
* Folder names describe adapter boundaries.
* No pass-through abstraction exists solely to satisfy the target tree.
* No new behavior test is required for a purely mechanical move, but existing tests and full gates remain green after every batch.

Dependencies:
* Phases 3 through 8 establish final ownership and retirement targets.

## Implementation Phase 10: Complete Operational Hardening

<!-- parallelizable: false -->

### Step 10.1: Prove restart, failure, and replica semantics

Implement and test the remaining Phase 0 decisions across real failure paths:

* Shared durable Data Protection key-ring and key rotation
* Proof-data persistence or explicitly documented process-lifetime semantics
* Notification idempotency/outbox or the selected delivery guarantee
* Configuration update and reconciliation consistency
* Multi-replica lease or optimistic concurrency behavior
* Migration rollback and recovery

Tests:
* Integration tests using SQL Server for lease/version and transaction behavior
* Aspire closed-box restart/readiness tests for durable credentials and startup recovery
* Failure-injection tests for notification and provider boundaries
* Minimal E2E coverage for one representative recovery path

Treat each hardening item as an independent significant refactor in this order:

1. Shared Data Protection key ring and rotation: `phase-10-1-data-protection-key-ring`
2. Proof-data durability or explicit process lifetime: `phase-10-2-proof-data-durability`
3. Notification delivery/idempotency: `phase-10-3-notification-delivery`
4. Configuration-update and reconciliation consistency: `phase-10-4-configuration-reconciliation`
5. Multi-replica lease or optimistic concurrency: `phase-10-5-replica-concurrency`
6. Migration rollback and recovery: `phase-10-6-migration-recovery`

Before each gate, update the affected `docs/wiki/runtime-behavior.md`, `docs/wiki/local-development.md`, `docs/wiki/operator-guide.md`, `docs/wiki/architecture.md`, or `docs/wiki/testing-and-quality.md` pages for the implemented guarantee. Run focused tests followed by the concrete gate assigned above, and do not begin the next hardening item until the solution build and unfiltered complete suite pass.

Success criteria:
* Every selected operational guarantee has executable evidence at the lowest useful test level.
* AppHost and ServiceDefaults retain only distributed composition and cross-cutting service defaults.
* Each independent hardening change runs its own focused checks and full quality gate.

Dependencies:
* Phase 7 persistence lifecycle
* Phase 8 final composition boundaries

## Implementation Phase 11: Final Architecture Verification and Handoff

<!-- parallelizable: false -->

### Step 11.1: Verify dependency and responsibility boundaries

Confirm:

* Policy/use cases do not depend on HTTP, UI, EF, provider, hosting, Azure, Aspire, or Infrastructure types.
* Infrastructure depends inward and translates provider/persistence representations.
* API endpoints bind, dispatch, and translate only.
* Web consumes API contracts over HTTP and has no Infrastructure reference.
* AppHost owns distributed resources and process references only.
* ServiceDefaults owns discovery, resilience, health, and telemetry only.
* Domain, when present, contains one or more cohesive invariant-owning clusters and has zero outward dependencies.
* Every operation has direct handler/validator tests and every adapter has tests at its appropriate boundary.

### Step 11.2: Run final validation

Validation commands:
* `dotnet test test/TNC.Trading.Platform.Architecture/TNC.Trading.Platform.Architecture.IntegrationTests/TNC.Trading.Platform.Architecture.IntegrationTests.csproj`
* `dotnet build TNC.Trading.Platform.slnx`
* `dotnet test TNC.Trading.Platform.slnx`
* `.github/scripts/Invoke-CleanArchitectureQualityGate.ps1 -GateId phase-11-final`

Also start the AppHost and manually verify API readiness/liveness, Scalar, Web sign-in, one protected API operation, one protected Web route, Keycloak, SQL Server, and Mailpit through runtime listener URLs.

### Step 11.3: Reconcile documentation and evidence

Update all affected pages under `docs/wiki/`, remove transitional exceptions and retired-type references, and ensure the implementation evidence identifies a passing full gate for every significant refactor and subphase.

Success criteria:
* Final focused and complete gates pass with zero failures.
* No significant refactor lacks recorded build and complete-suite evidence.
* Wiki documentation matches implemented architecture, runtime, API, local development, operator behavior, and testing.
* Deferred items are explicit follow-on work rather than hidden partial migrations.

## Dependencies

* .NET SDK `10.0.200` selected by `global.json`
* Docker Desktop and the repository's Aspire-managed SQL Server, Keycloak, and Mailpit resources
* Playwright browser prerequisites for Web functional and E2E suites
* Product/deployment decisions listed in Phase 0
* Existing xUnit, Aspire testing, bUnit, and Playwright test infrastructure

## Success Criteria

* Compile-time dependencies point inward while runtime calls travel outward only through inward-owned ports.
* Application is framework-neutral and organized as cohesive vertical slices with direct handler and validator tests.
* Infrastructure contains persistence, provider, encryption, notification, and other external mechanisms without owning business decisions.
* Domain is introduced only if a cohesive, tested, invariant-owning policy cluster passes the extraction gate.
* API, Web, AppHost, and ServiceDefaults remain stable except for narrow composition and hosting changes.
* Every significant refactor passes `dotnet build TNC.Trading.Platform.slnx` and the unfiltered `dotnet test TNC.Trading.Platform.slnx` before subsequent migration work begins.
* Additional tests are added throughout at the lowest appropriate boundary, and no existing regression coverage is weakened.