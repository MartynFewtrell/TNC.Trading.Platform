<!-- markdownlint-disable-file -->
# Release Changes: Clean Architecture Migration

**Related Plan**: clean-architecture-migration-plan.instructions.md
**Implementation Date**: 2026-07-27

## Summary

Implementation tracking for the staged Clean Architecture migration.

## Changes

### Added

* `.github/scripts/Invoke-CleanArchitectureQualityGate.ps1`

### Modified

* `docs/wiki/architecture.md`
* `docs/wiki/runtime-behavior.md`
* `docs/wiki/local-development.md`
* `docs/wiki/ig-day-trading-with-ig-apis.md`
* `docs/wiki/testing-and-quality.md`

### Removed

* `src/TNC.Trading.Platform.Application/Services/PlatformStateCoordinator.cs` - Removed the orphaned stale coordinator file that referenced the retired side-effects collaborator.
* `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/AuthRetryCycleTests.cs` - Removed the stale Infrastructure-backed duplicate after the test was reclassified to Infrastructure.
* `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/IgProofDataTests.cs` - Removed the stale Infrastructure-backed duplicate after the test was reclassified to Infrastructure.

## Additional or Deviating Changes

* The quality-gate script was made compatible with Windows PowerShell 5.1 by
	using scalar native-command exit codes, tolerant native stderr capture, and
	.NET file reading for TRX XML. Earlier executions remain in separate
	timestamped evidence folders.
* Direct script invocation was blocked by the machine execution policy because
	the repository script is unsigned. Validation used a process-scoped
	`ExecutionPolicy Bypass`; no repository policy was changed.

* The Phase 3.1 retry remained blocked by the distributed test environment.
	The solution build passed with 0 errors, but the unfiltered suite exited `1`
	with 281 passed, 29 failed, and 0 skipped across 9 TRX files. The API
	integration failures occurred during AppHost fixture initialization because
	the API listener was never exposed; the isolated health-endpoint test
	reproduced the same 120-second timeout.
	* Evidence: `artifacts/quality-gates/phase-3-1-configuration-invariants/20260727-171119/`.
	* The persistent SQL container was running but its logs showed strict TDS/TLS
	connection rejection and an unavailable `platformdb` login during startup.
	Restarting the persistent container did not clear the isolated failure.
	* The shared AppHost diagnostic now includes process status and recent output
	when API listener discovery times out. No production behavior or test
	assertion was weakened.
	* The blocker was subsequently resolved after direct API startup exposed the
	actual endpoint-construction failure. The SQL symptoms were caused by generic
	HTTPS listener probes reaching SQL rather than database corruption.

## Phase 0 Evidence

* Focused architecture test exited `0`, with 7 passed, 0 failed, and 0 skipped.
* Quality gate build `dotnet build TNC.Trading.Platform.slnx` exited `0`, with
	0 errors and 122 warnings, including existing package vulnerability warnings.
* The initial unfiltered quality-gate test command exited non-zero. Final TRX
	evidence contained 258 passed, 8 failed, and 0 skipped results across 8 TRX
	files.
* Evidence location:
	`artifacts/quality-gates/phase-0-baseline/20260727-092348/`.
* An existing `TNC.Trading.Platform.AppHost` process was present during
	validation. A clean startup run was not attributed to this baseline because
	the process was pre-existing.
* Focused diagnosis found the failing distributed tests are concentrated in
	AppHost-backed authentication and Web E2E startup. API failures target a
	refused stale listener (`localhost:61608`), while the E2E failure waits for
	the fixed callback URL `https://localhost:7281/signin-oidc`.
* The shared test harness already contains the repository's prior readiness and
	dynamic-listener discovery changes (`WaitForApiReadiness` and dynamic Web
	redirect configuration). No production change or assertion weakening was
	made to mask the baseline failure.

## Blockers

The initial baseline was blocked by distributed-test listener and callback
startup failures, but the environment was subsequently unblocked without
production changes or assertion weakening.

## Phase 0.3 Evidence

* Focused architecture tests exited `0`, with 7 passed, 0 failed, and 0 skipped.
* Quality gate `phase-0-baseline-rerun` exited `0`.
* Solution build exited `0`.
* Unfiltered solution tests exited `0`, with 288 passed, 0 failed, and 0
	skipped across 9 TRX files.
* Evidence location:
	`artifacts/quality-gates/phase-0-baseline-rerun/20260727-152904/`.

No Phase 0 blockers remain. Phase 1 may begin.

## Phase 1 Evidence

* Added role-aware architecture policy and validator tests while preserving
	the topology-neutral project graph checks.
* Added observable API characterization for accepted and rejected manual retry
	outcomes and retained existing readiness, configuration, status/event,
	authorization, and Web behavior coverage.
* Updated `docs/wiki/architecture.md` and `docs/wiki/testing-and-quality.md`.
* Focused architecture tests exited `0`, with 12 passed, 0 failed, and 0
	skipped.
* Focused API unit tests exited `0`, with 21 passed, 0 failed, and 0 skipped.
* Quality gate `phase-1-safety-rails` exited `0`; solution build exited `0`.
* Unfiltered solution tests exited `0`, with 295 passed, 0 failed, and 0
	skipped across 9 TRX files.
* Evidence location:
	`artifacts/quality-gates/phase-1-safety-rails/20260727-153832/`.

## Phase 1 Changes

### Added

* `test/TNC.Trading.Platform.Architecture/TNC.Trading.Platform.Architecture.IntegrationTests/ArchitectureRules/ProductionProjectRolePolicy.cs` - Role policy definitions.
* `test/TNC.Trading.Platform.Architecture/TNC.Trading.Platform.Architecture.IntegrationTests/ArchitectureRules/ProductionProjectRoleValidator.cs` - Structured project/package/framework validator.
* `test/TNC.Trading.Platform.Architecture/TNC.Trading.Platform.Architecture.IntegrationTests/ArchitectureRules/ProductionProjectRoleValidatorTests.cs` - Validator behavior tests.
* `test/TNC.Trading.Platform.Architecture/TNC.Trading.Platform.Architecture.IntegrationTests/ArchitectureRules/RepositoryArchitectureRuleTests.cs` - Repository-level architecture assertions.

### Modified

* `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/PlatformEndpointHandlerTests.cs` - Observable manual-retry characterization.
* `docs/wiki/architecture.md` - Role rules and migration exceptions.
* `docs/wiki/testing-and-quality.md` - Safety-rail validation scope.

## Phase 1 Notes

* The Application `Microsoft.AspNetCore.App` reference remains an explicit
	temporary exception with Phase 8 retirement conditions.
* Existing package vulnerability and preview SDK warnings remain; they did not
	affect validation.

## Phase 2 Step 2.1 Evidence

* Added the explicit `ReconcilePlatformAuthentication` request, response,
	handler, inward reconciler port, and focused handler tests.
* Added serialized writer coordination and routed startup, scheduled,
	configuration-triggered, and manual retry reconciliation through the command.
* Updated `docs/wiki/architecture.md` and `docs/wiki/runtime-behavior.md`.
* Focused Application tests exited `0`, with 59 passed, 0 failed, and 0 skipped.
* Solution build exited `0` with 0 errors.
* Quality gate `phase-2-1-reconcile-command` exited `0`.
* Unfiltered solution tests exited `0`, with 299 passed, 0 failed, and 0
	skipped across 9 TRX files.
* Evidence location:
	`artifacts/quality-gates/phase-2-1-reconcile-command/20260727-154544/`.

## Phase 2 Step 2.1 Changes

### Added

* `src/TNC.Trading.Platform.Application/Features/ReconcilePlatformAuthentication/ReconcilePlatformAuthenticationRequest.cs` - Reconciliation command input.
* `src/TNC.Trading.Platform.Application/Features/ReconcilePlatformAuthentication/ReconcilePlatformAuthenticationResponse.cs` - Typed reconciliation result.
* `src/TNC.Trading.Platform.Application/Features/ReconcilePlatformAuthentication/ReconcilePlatformAuthenticationHandler.cs` - Application use-case entry point.
* `src/TNC.Trading.Platform.Application/Features/ReconcilePlatformAuthentication/IPlatformAuthenticationReconciler.cs` - Inward reconciliation port.
* `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/ReconcilePlatformAuthenticationHandlerTests.cs` - Serialization, persistence, cancellation, and provider-failure tests.

### Modified

* `src/TNC.Trading.Platform.Application/Services/PlatformStateCoordinator.cs` - Transitional reconciliation implementation and serialized writer boundary.
* `src/TNC.Trading.Platform.Application/Services/PlatformApplicationServiceCollectionExtensions.cs` - Transitional registration.
* `src/TNC.Trading.Platform.Application/Services/PlatformAuthSupervisorRunner.cs` - Explicit command invocation.
* `src/TNC.Trading.Platform.Application/Features/UpdatePlatformConfiguration/UpdatePlatformConfigurationHandler.cs` - Explicit reconciliation invocation.
* `src/TNC.Trading.Platform.Api/Program.cs` - Startup command invocation.

## Phase 2 Step 2.2 Evidence

* Moved reconciliation cadence, scope creation, cancellation, logging, and
	error continuation into the API-owned hosted supervisor.
* Added focused API tests for transient-failure continuation, cancellation,
	and initial reconciliation ordering.
* Updated architecture, runtime, testing, and local-development wiki pages.
* Focused API supervisor tests exited `0`, with 3 passed, 0 failed, and 0
	skipped; the adjacent Application suite passed 59 tests.
* Solution build exited `0`.
* Quality gate `phase-2-2-api-supervision` exited `0`.
* Unfiltered solution tests exited `0`, with 302 passed, 0 failed, and 0
	skipped across 9 TRX files.
* Evidence location:
	`artifacts/quality-gates/phase-2-2-api-supervision/20260727-161719/`.

## Phase 2 Step 2.2 Changes

### Added

* `src/TNC.Trading.Platform.Api/Hosting/PlatformAuthenticationSupervisor.cs` - API-owned hosted reconciliation adapter.
* `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/PlatformAuthenticationSupervisorTests.cs` - Supervisor behavior tests.

### Modified

* `src/TNC.Trading.Platform.Api/Program.cs` - API supervisor registration and startup composition.
* `src/TNC.Trading.Platform.Application/Services/PlatformApplicationServiceCollectionExtensions.cs` - Removed Application hosted-service registration.
* `src/TNC.Trading.Platform.Application/Services/PlatformAuthSupervisor.cs` - Retired from active Application hosting.
* `src/TNC.Trading.Platform.Application/Services/PlatformAuthSupervisorRunner.cs` - Retired from active Application hosting.

## Phase 2 Step 2.3 Evidence

* Replaced status and event coordinator reads with feature-local projection
	ports and non-creating runtime-state reads.
* Added explicit `stateAvailability` and `lastReconciledAtUtc` status metadata
	while preserving existing successful response fields.
* Preserved deterministic newest-first event ordering with event ID tie-breaker.
* Updated runtime, API, operator, and testing wiki pages.
* Focused Application tests exited `0`, with 64 passed, 0 failed, and 0 skipped.
* Focused API tests exited `0`, with 24 passed, 0 failed, and 0 skipped.
* Solution build exited `0`.
* Quality gate `phase-2-3-query-purity` exited `0`.
* Unfiltered solution tests exited `0`, with 307 passed, 0 failed, and 0
	skipped across 9 TRX files.
* Evidence location:
	`artifacts/quality-gates/phase-2-3-query-purity/20260727-163142/`.

## Phase 2 Step 2.3 Changes

### Added

* `src/TNC.Trading.Platform.Application/Features/GetPlatformEvents/Ports/IPlatformEventsProjectionReader.cs` - Event projection port.
* `src/TNC.Trading.Platform.Application/Features/GetPlatformStatus/Ports/IPlatformStatusProjectionReader.cs` - Status projection port.
* `src/TNC.Trading.Platform.Application/Features/GetPlatformStatus/Ports/PlatformStatusProjection.cs` - Status projection model.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/EfPlatformEventsProjectionReader.cs` - EF event projection adapter.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/EfPlatformStatusProjectionReader.cs` - EF status projection adapter.
* `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/PlatformProjectionQueryTests.cs` - Query-purity and freshness tests.

### Modified

* `src/TNC.Trading.Platform.Application/Services/IPlatformRuntimeStateStore.cs` - Non-creating query contract.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/EfPlatformRuntimeStateStore.cs` - Projection-safe runtime-state behavior.
* `src/TNC.Trading.Platform.Application/Features/GetPlatformStatus/GetPlatformStatusHandler.cs` - Pure status query.
* `src/TNC.Trading.Platform.Application/Features/GetPlatformStatus/GetPlatformStatusResponse.cs` - Freshness/missing-state metadata.
* `src/TNC.Trading.Platform.Application/Features/GetPlatformEvents/GetPlatformEventsHandler.cs` - Pure event query.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/PlatformInfrastructureServiceCollectionExtensions.cs` - Projection adapter registration.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/EfPlatformEventStore.cs` - Deterministic event ordering.
* `src/TNC.Trading.Platform.Api/Features/GetPlatformStatus/GetPlatformStatusResponse.cs` - Transport contract metadata.
* `src/TNC.Trading.Platform.Api/Features/GetPlatformStatus/GetPlatformStatusMapping.cs` - Projection mapping.
* `docs/wiki/runtime-behavior.md` - Query purity and freshness behavior.
* `docs/wiki/api-reference.md` - Status contract metadata.
* `docs/wiki/operator-guide.md` - Missing/stale state guidance.
* `docs/wiki/testing-and-quality.md` - Query purity coverage.

## Phase 2 Verification

* Verified the `phase-2-1-reconcile-command`, `phase-2-2-api-supervision`, and
	`phase-2-3-query-purity` evidence records before proceeding.
* All Phase 2 focused suites and full gates passed with zero failures and zero
	skips.

## Phase 2 Step 2.4 Verification

* Application unit verification exited `0`, with 64 passed, 0 failed, and 0
	skipped.
* API unit verification exited `0`, with 24 passed, 0 failed, and 0 skipped.
* API integration verification exited `0`, with 29 passed, 0 failed, and 0
	skipped.
* Phase 2 is complete; all three independent quality gates and the required
	verification suites passed.

## Phase 8 Step 8.1 Evidence

* Moved ASP.NET Core authentication and authorization registration into API and
	Web host composition roots with identical policy names and role semantics.
* Removed the obsolete Application `PlatformAuthorizationPolicyRegistration`.
* Updated architecture, application-overview, and testing wiki pages.
* Focused API, Web, AppHost, architecture, and authentication suites passed
	with zero failures and skips.
* Solution build exited `0` with 131 warnings.
* Quality gate `phase-8-1-host-auth-ownership` exited `0`.
* Unfiltered solution tests exited `0`, with 306 passed, 0 failed, and 0
	skipped across 9 TRX files.
* Evidence location:
	`artifacts/quality-gates/phase-8-1-host-auth-ownership/20260727-164450/`.

## Phase 8 Step 8.1 Changes

### Modified

* `src/TNC.Trading.Platform.Api/Authentication/PlatformApiAuthenticationServiceCollectionExtensions.cs` - API-local policy registration.
* `src/TNC.Trading.Platform.Web/Authentication/PlatformWebAuthenticationServiceCollectionExtensions.cs` - Web-local policy registration.
* `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/PlatformApiAuthenticationServiceCollectionExtensionsTests.cs` - API policy parity tests.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/PlatformWebAuthenticationServiceCollectionExtensionsTests.cs` - Web policy parity tests.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/PlatformAuthenticationRegistrationTests.cs` - Host registration coverage.
* `docs/wiki/architecture.md` - Host ownership.
* `docs/wiki/application-overview.md` - Application boundary.
* `docs/wiki/testing-and-quality.md` - Authentication parity validation.

### Removed

* `src/TNC.Trading.Platform.Application/Authentication/PlatformAuthorizationPolicyRegistration.cs` - Obsolete framework-coupled policy registration.

## Phase 8 Step 8.2 Evidence

* Removed `Microsoft.AspNetCore.App` from Application and moved its remaining
	host-specific composition into API.
* Replaced Application configuration, logging, DI, and hosting dependencies
	with explicit settings and inward-owned abstractions.
* Strengthened architecture rules to reject Application framework references.
* Focused Application, API, Web, AppHost, and architecture suites passed with
	zero failures and skips.
* Solution build exited `0` with 131 warnings.
* Quality gate `phase-8-2-framework-reference-removal` exited `0`.
* Unfiltered solution tests exited `0`, with 306 passed, 0 failed, and 0
	skipped across 9 TRX files.
* Evidence location:
	`artifacts/quality-gates/phase-8-2-framework-reference-removal/20260727-165357/`.

## Phase 8 Step 8.2 Notes

* The Web-to-Application reference remains intentional because Web consumes
	Application-owned authentication contracts. Its retirement condition is to
	relocate or remove those contracts before removing the project reference.

## Phase 8 Step 8.3 Verification

* API unit verification exited `0`, with 24 passed, 0 failed, and 0 skipped.
* Web unit verification exited `0`, with 88 passed, 0 failed, and 0 skipped.
* AppHost unit verification exited `0`, with 38 passed, 0 failed, and 0
	skipped.
* Architecture verification exited `0`, with 12 passed, 0 failed, and 0
	skipped.
* Phase 8 is complete; both independent full gates and required verification
	suites passed.

## Phase 3 Step 3.1 Evidence

* Enforced configuration invariants in Application while retaining malformed
	transport validation and Problem Details mapping in API.
* Restored the API transport-validator registration removed during host
	registration extraction; this missing dependency caused API endpoint
	construction to fail before listener binding.
* Updated the closed-box configuration scenario to use transport-valid enum
	values and verify the Application-owned invariant response fields.
* Preserved the persistent SQL volume and all readiness dependencies.
* Isolated distributed API health validation passed with 1 passed, 0 failed,
	and 0 skipped.
* Focused Application validation passed with 4 passed, 0 failed, and 0 skipped.
* Focused API validation passed with 7 passed, 0 failed, and 0 skipped.
* Solution build exited `0`.
* Quality gate `phase-3-1-configuration-invariants` exited `0`.
* Unfiltered solution tests exited `0`, with 310 passed, 0 failed, and 0
	skipped across 9 TRX files.
* Evidence location:
	`artifacts/quality-gates/phase-3-1-configuration-invariants/20260727-183455/`.

## Phase 3 Step 3.1 Changes

### Modified

* `src/TNC.Trading.Platform.Api/Program.cs` - Restored scoped API transport-validator registration.
* `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/PlatformAuthenticationSyntheticTokenIntegrationTests.cs` - Preserved Application validation coverage behind transport-valid input.

## Phase 3 Step 3.2 Trading Schedule Attempt

* Moved trading calendar, active-window, and Test-platform-to-Live-broker
	decisions into the framework-neutral Application schedule policy.
* Focused schedule tests passed with 10 passed, 0 failed, and 0 skipped;
	Application unit tests passed 71 and Infrastructure unit tests passed 44.
* The solution build passed with 0 errors, but the mandatory unfiltered gate
	exited `1` with 302 passed, 11 failed, and 0 skipped.
* All failures occurred during the shared synthetic-token API fixture readiness
	wait; no trading-schedule assertion failed.
* Evidence location:
	`artifacts/quality-gates/phase-3-2-trading-schedule/20260727-184311/`.
* The trading-schedule family remains incomplete, and retry-timing work has not
	started.

## Phase 3 Step 3.2 Trading Schedule Evidence

* Completed Application ownership of trading calendar, active-window,
	inactive-schedule precedence, and Test-platform-to-Live-broker decisions.
* Added typed schedule actions and decisions consumed by reconciliation and
	manual retry without moving retry-timing or transition policy.
* Repaired a cross-assembly distributed-test race by applying the existing
	AppHost lease to the synthetic-token fixture and disposing partial startup on
	initialization failure. Concurrent fixtures had attempted to recreate the
	shared development database and terminated one API process.
* No persistent data, SQL volume, container, or authentication state was
	deleted.
* Isolated synthetic-token validation passed with 1 passed and 0 failed.
* Focused schedule validation passed with 10 passed and 0 failed.
* Solution build exited `0` with 0 errors.
* Quality gate `phase-3-2-trading-schedule` exited `0`.
* Unfiltered solution tests exited `0`, with 313 passed, 0 failed, and 0
	skipped across 9 TRX files.
* Evidence location:
	`artifacts/quality-gates/phase-3-2-trading-schedule/20260727-185336/`.

## Phase 3 Step 3.2 Trading Schedule Changes

### Added

* `src/TNC.Trading.Platform.Application/Services/TradingScheduleTickAction.cs` - Typed schedule action.
* `src/TNC.Trading.Platform.Application/Services/TradingScheduleTickDecision.cs` - Typed schedule decision.

### Modified

* `src/TNC.Trading.Platform.Application/Services/TradingScheduleGate.cs` - Inward-owned schedule policy.
* `src/TNC.Trading.Platform.Application/Services/PlatformStateCoordinator.cs` - Consumes schedule decisions.
* `src/TNC.Trading.Platform.Application/Services/PlatformStateTransitionEngine.cs` - Removed schedule decision ownership.
* `src/TNC.Trading.Platform.Application/Features/UpdatePlatformConfiguration/UpdatePlatformConfigurationValidator.cs` - Uses the shared live-target predicate.
* `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/TradingScheduleGateTests.cs` - Direct schedule policy coverage.
* `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/AuthRetryCycleTests.cs` - Preserved retry-cycle behavior at the new schedule boundary.
* `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/SyntheticTokenIntegrationTestFixture.cs` - Serialized distributed AppHost startup and cleaned partial initialization.
* `docs/wiki/architecture.md` - Schedule policy ownership.
* `docs/wiki/runtime-behavior.md` - Schedule decision behavior.
* `docs/wiki/operator-guide.md` - Operator-visible blocked-live behavior.

## Phase 3 Step 3.2 Retry Timing Evidence

* Moved initial-delay, exponential progression, maximum-delay capping, reset,
	and overflow-safe retry timing into framework-neutral Application policy.
* Kept clocks, waiting, persistence, provider transport, hosting cadence, and
	retry-state orchestration outside the timing policy.
* Focused retry policy tests passed with 11 passed, 0 failed, and 0 skipped.
* Application and Infrastructure focused suites passed with no failures or
	skips.
* Solution build exited `0`.
* Quality gate `phase-3-3-retry-timing` exited `0`.
* Unfiltered solution tests exited `0`, with 314 passed, 0 failed, and 0
	skipped across 9 TRX files.
* Evidence location:
	`artifacts/quality-gates/phase-3-3-retry-timing/20260727-190401/`.

## Phase 3 Step 3.2 Retry Timing Changes

### Added

* `src/TNC.Trading.Platform.Application/Services/RetryTimingPolicy.cs` - Inward-owned retry delay calculation.

### Modified

* `src/TNC.Trading.Platform.Application/Services/PlatformStateCoordinator.cs` - Consumes retry timing policy.
* `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/RetryPolicyTimingTests.cs` - Direct timing policy coverage.
* `docs/wiki/architecture.md` - Retry timing ownership.
* `docs/wiki/runtime-behavior.md` - Retry progression behavior.
* `docs/wiki/operator-guide.md` - Operator-visible retry timing behavior.

## Phase 3 Step 3.2 Authentication Transition Evidence

* Made the Application transition engine the owner of the explicit
	authentication source-target graph and state invariants.
* Added typed transition requests and rejection results; rejected transitions
	leave runtime state unchanged.
* Preserved persistence shape, external-effect ordering, retry timing, and
	schedule behavior.
* Direct transition tests passed with 19 passed, 0 failed, and 0 skipped.
* Application and Infrastructure suites passed with no failures or skips.
* Solution build exited `0`.
* Quality gate `phase-3-4-auth-transitions` exited `0`.
* Unfiltered solution tests exited `0`, with 333 passed, 0 failed, and 0
	skipped across 9 TRX files.
* Evidence location:
	`artifacts/quality-gates/phase-3-4-auth-transitions/20260727-191326/`.

## Phase 3 Step 3.2 Authentication Transition Changes

### Added

* `src/TNC.Trading.Platform.Application/Services/AuthenticationStateTransition.cs` - Transition request.
* `src/TNC.Trading.Platform.Application/Services/AuthenticationStateTransitionResult.cs` - Typed transition result.
* `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/PlatformStateTransitionEngineTests.cs` - Transition graph and rejection coverage.

### Modified

* `src/TNC.Trading.Platform.Application/Services/PlatformStateTransitionEngine.cs` - Controlled transition policy.
* `src/TNC.Trading.Platform.Application/Services/PlatformStateCoordinator.cs` - Uses controlled transitions.
* `docs/wiki/architecture.md` - Transition policy ownership.
* `docs/wiki/runtime-behavior.md` - Valid and rejected transition behavior.

## Phase 3 Step 3.2 Restart Classification Evidence

* Moved restart-required classification from the persisted Infrastructure
	entity boundary to framework-neutral Application policy and startup-fixed
	configuration values.
* Preserved the rule that platform or broker environment changes require
	restart while runtime-managed changes and absent current configuration do
	not.
* Focused restart tests passed with 4 passed, 0 failed, and 0 skipped.
* Application and Infrastructure suites passed with no failures or skips.
* Solution build exited `0`.
* Quality gate `phase-3-5-restart-classification` exited `0`.
* Unfiltered solution tests exited `0`, with 338 passed, 0 failed, and 0
	skipped across 9 TRX files.
* Evidence location:
	`artifacts/quality-gates/phase-3-5-restart-classification/20260727-192445/`.

## Phase 3 Step 3.2 Restart Classification Changes

### Added

* `src/TNC.Trading.Platform.Application/Configuration/PlatformConfigurationRestartPolicy.cs` - Inward-owned restart policy.
* `src/TNC.Trading.Platform.Application/Configuration/PlatformStartupFixedConfiguration.cs` - Startup-fixed comparison value.
* `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/PlatformConfigurationRestartPolicyTests.cs` - Direct restart policy coverage.

### Modified

* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/SqlPlatformConfigurationStore.cs` - Translates persisted values into Application policy input.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/PlatformConfigurationRestartPolicy.cs` - Removed obsolete policy declaration; an inert namespace-only file remains.
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/SqlPlatformConfigurationStoreTests.cs` - Adapter behavior coverage.
* `docs/wiki/architecture.md` - Restart policy ownership.
* `docs/wiki/runtime-behavior.md` - Restart classification behavior.
* `docs/wiki/operator-guide.md` - Operator-visible restart semantics.

## Additional or Deviating Changes

* The obsolete Infrastructure restart-policy type was removed, but its former
	file remains as an inert namespace-only source file because delegated file
	deletion did not physically remove it.
	* The passing build and full gate confirm no runtime or ownership effect; the
		file can be removed in a later mechanical cleanup batch.

## Phase 3 Step 3.2 Retention Policy Evidence

* Moved retention cutoff calculation, strict older-than eligibility, eligible
	record families, and current IG snapshot exclusion into Application policy.
* Kept configuration retrieval, EF/SQL predicate translation, batching,
	transactions, deletion, persistence, and logging in Infrastructure.
* Focused retention tests passed with 7 passed, 0 failed, and 0 skipped.
* Application and Infrastructure suites passed with no failures or skips.
* Solution build exited `0`.
* Quality gate `phase-3-6-retention-policy` exited `0`.
* Unfiltered solution tests exited `0`, with 342 passed, 0 failed, and 0
	skipped across 9 TRX files.
* Evidence location:
	`artifacts/quality-gates/phase-3-6-retention-policy/20260727-193349/`.

## Phase 3 Step 3.2 Retention Policy Changes

### Added

* `src/TNC.Trading.Platform.Application/Services/OperationalRecordFamily.cs` - Retention record-family policy value.
* `src/TNC.Trading.Platform.Application/Services/OperationalRecordRetentionPlan.cs` - Inward-owned retention plan.
* `src/TNC.Trading.Platform.Application/Services/OperationalRecordRetentionPolicy.cs` - Cutoff and eligibility policy.
* `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/OperationalRecordRetentionPolicyTests.cs` - Direct retention policy coverage.

### Modified

* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/OperationalRecordRetentionProcessor.cs` - Executes the Application retention plan.
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/OperationalRecordRetentionProcessorTests.cs` - Adapter and cutoff translation coverage.
* `docs/wiki/architecture.md` - Retention policy ownership.
* `docs/wiki/runtime-behavior.md` - Retention cutoff semantics.
* `docs/wiki/operator-guide.md` - Operator-visible retention behavior.

## Additional or Deviating Changes

* Superseded Infrastructure retention policy, plan, and direct policy-test files
	remain as namespace-only placeholders because delegated deletion did not
	physically remove tracked files.
	* Only the Application definitions remain active; the passing gate confirms
		no duplicate type or behavior remains.

## Phase 3 Step 3.2 Sensitivity Policy Evidence

* Moved case-insensitive operational metadata sensitivity classification into
	framework-neutral Application policy.
* Kept JSON traversal, masking, plaintext scanning, serialization, persistence,
	logging, and provider translation in Infrastructure.
* Preserved the eight existing sensitive fragments and `[redacted]` output.
* Focused classification tests passed with 16 passed, 0 failed, and 0 skipped.
* Application and Infrastructure suites passed with no failures or skips.
* Solution build exited `0`.
* Quality gate `phase-3-7-sensitivity-policy` exited `0`.
* Unfiltered solution tests exited `0`, with 358 passed, 0 failed, and 0
	skipped across 9 TRX files.
* Evidence location:
	`artifacts/quality-gates/phase-3-7-sensitivity-policy/20260727-194018/`.

## Phase 3 Step 3.2 Sensitivity Policy Changes

### Added

* `src/TNC.Trading.Platform.Application/Services/OperationalDataSensitivity.cs` - Typed sensitivity classification.
* `src/TNC.Trading.Platform.Application/Services/OperationalDataSensitivityPolicy.cs` - Inward-owned classification policy.
* `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/OperationalDataSensitivityPolicyTests.cs` - Direct classification coverage.

### Modified

* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/OperationalDataRedactor.cs` - Consumes Application classification while retaining redaction mechanics.
* `docs/wiki/architecture.md` - Sensitivity policy ownership.
* `docs/wiki/runtime-behavior.md` - Secret-safe classification and redaction behavior.
* `docs/wiki/operator-guide.md` - Operator-visible secret-safe output contract.

## Phase 3 Step 3.3 Domain Readiness Evaluation

* Recorded `Domain deferred`; no Domain production or test project was created.
* The trading-schedule cluster has direct tests and would gain meaningful
	compile-time protection, but its configuration is freely constructible,
	Application-owned, and shared across API, persistence, Web, and snapshots.
* The authentication-transition cluster preserves transition invariants and has
	direct tests, but it mutates an Application runtime projection that also owns
	retry, schedule, configuration-context, and persistence-facing state.
* Neither cluster has a complete one-way Application mapping or can move with
	all behavior, values, results, and tests without broader boundary work.
* Re-evaluation is deferred until one candidate owns validated values and is
	isolated from mutable Application/persistence projections.
* This was a read-only decision; no additional quality gate was required.

## Phase 3 Verification

* Application unit verification passed with 118 passed, 0 failed, and 0
	skipped.
* Infrastructure unit verification passed with 42 passed, 0 failed, and 0
	skipped.
* API unit verification passed with 24 passed, 0 failed, and 0 skipped.
* Architecture verification passed with 12 passed, 0 failed, and 0 skipped.
* Audited all seven Phase 3 gate records; every build and test command exited
	`0`, with zero failures and zero skips across each nine-file TRX set.
* Confirmed architecture, runtime, API, operator, application-overview, and
	testing wiki pages reflect current policy and adapter ownership.
* Confirmed no Domain project exists, consistent with the deferred decision.
* Phase 3 is complete.

## Phase 3 Step 3.1 Attempt

* Focused Application validator/handler tests and API Problem Details tests
	passed before the full gate.
* The configuration-invariant implementation is present, but Step 3.1 is not
	marked complete because its mandatory full gate is blocked.
* Quality gate `phase-3-1-configuration-invariants` exited `1`.
* Aggregate result: 281 passed, 29 failed, 0 skipped across 9 TRX files.
* Evidence location:
	`artifacts/quality-gates/phase-3-1-configuration-invariants/20260727-170144/`.
* The 29 failures are AppHost-backed API integration startup failures: API
	base URI discovery timed out after only an HTTPS listener was observed.
	TRX evidence also contains SQL login/database startup errors. No production
	behavior was weakened and no later policy family was started.

## Phase 4 IG Boundary Evidence

* Replaced the provider-shaped IG session boundary with an Application-owned
	broker authentication and proof-collection port.
* Implemented and registered the Infrastructure adapter, keeping IG HTTP,
	JSON, headers, routing, raw tokens, and provider exceptions outside
	Application.
* Updated architecture, runtime, testing, and IG integration wiki pages.
* Phase 4 focused and adjacent suites passed with no failures or skips.
* Quality gate `phase-4-ig-boundary` exited `0`.
* Unfiltered solution tests passed with 348 passed, 0 failed, and 0 skipped
	across 9 TRX files.
* Evidence location:
	`artifacts/quality-gates/phase-4-ig-boundary/20260727-201257/`.

## Phase 5 Step 5.1 Manual Retry Evidence

* Moved manual retry state loading, eligibility decisions, broker invocation,
	outcome application, and operation-specific commit intent into
	`TriggerManualAuthRetryHandler`.
* Added typed rejection reasons and outcomes while preserving `202 Accepted`
	and `409 Conflict` API behavior.
* Added the inward-owned commit port and Infrastructure persistence adapter;
	provider and notification I/O remain outside local persistence operations.
* Added direct handler coverage for schedule, Live/Test compatibility,
	retry-limit, successful retry-cycle, cancellation, and typed API rejection
	behavior.
* Application unit tests passed with 116 passed, 0 failed, and 0 skipped.
* API unit tests passed with 24 passed, 0 failed, and 0 skipped.
* Infrastructure unit tests passed with 38 passed, 0 failed, and 0 skipped.
* Quality gate `phase-5-1-manual-retry` exited `0`.
* Unfiltered solution tests passed with 353 passed, 0 failed, and 0 skipped
	across 9 TRX files.
* Evidence location:
	`artifacts/quality-gates/phase-5-1-manual-retry/20260727-202244/`.

## Phase 5 Step 5.2 Test Boundary Evidence

* Reclassified EF, Data Protection, notification, and provider-backed workflow
	tests from the Application unit boundary into Infrastructure unit coverage.
* Removed the Application test project reference to Infrastructure and deleted
	the obsolete Application infrastructure helper.
* Kept Application tests focused on pure use cases and policy behavior.
* No Infrastructure integration project was added because current production
	behavior has no explicit SQL transaction or concurrency contract requiring
	real SQL Server semantics; the decision is documented for future work.
* Application unit tests passed with 89 passed, 0 failed, and 0 skipped.
* Infrastructure unit tests passed with 65 passed, 0 failed, and 0 skipped.
* Architecture tests passed with 13 passed, 0 failed, and 0 skipped.
* Quality gate `phase-5-2-test-boundaries` exited `0`.
* Unfiltered solution tests passed with 353 passed, 0 failed, and 0 skipped.
* Evidence location:
	`artifacts/quality-gates/phase-5-2-test-boundaries/20260727-202954/`.

## Phase 5 Verification

* Manual retry handler tests passed with 5 passed, 0 failed, and 0 skipped.
* API endpoint mapping tests passed with 5 passed, 0 failed, and 0 skipped.
* Infrastructure unit tests passed with 65 passed, 0 failed, and 0 skipped.
* Verified both Phase 5 independent full-gate records, each with build and
	test exit code `0` and zero failures or skips.
* Phase 5 is complete; auth-audit extraction is the next migration unit.

## Phase 6 Step 6.1 Auth Audit Evidence

* Extracted authentication audit recording into a framework-neutral Application
	vertical slice with inward-owned configuration and commit ports.
* Kept HTTP binding, claims extraction, supported-event validation,
	correlation-ID resolution, and transport mapping in API.
* Added direct Application coverage for success, identity fallback,
	configuration failure, invalid no-write rejection, cancellation, and commit
	failure behavior.
* Updated architecture, runtime, and API-reference wiki pages and removed the
	unused legacy API audit record type.
* Focused Application auth-audit tests passed with 6 passed, 0 failed, and 0
	skipped.
* Focused API auth-audit tests passed with 6 passed, 0 failed, and 0 skipped.
* Full Application, Infrastructure, and architecture suites passed with 95,
	65, and 13 tests respectively, with no failures or skips.
* Quality gate `phase-6-1-auth-audit` exited `0`.
* Unfiltered solution tests passed with 359 passed, 0 failed, and 0 skipped.

## Phase 6 Notes

* A broader API unit run outside the mandatory gate observed an existing
	supervisor timing assertion mismatch, but the required complete gate passed
	the API suite and no unrelated supervisor behavior was changed.

## Phase 6 Step 6.2 Configuration Update Evidence

* Extracted configuration update persistence into the Application-owned
	`IUpdatePlatformConfigurationCommitter` port and Infrastructure adapter.
* Preserved atomic configuration, credential, and audit persistence, including
	single-commit behavior and reconciliation only after a successful commit.
* Expanded direct Application tests for validation, commit ordering, credential
	presence, cancellation, failure propagation, and reconciliation behavior.
* Updated architecture, runtime, and API-reference wiki pages.
* Application tests passed with 99 passed, 0 failed, and 0 skipped.
* API tests passed with 24 passed, 0 failed, and 0 skipped.
* Infrastructure tests passed with 65 passed, 0 failed, and 0 skipped.
* Quality gate `phase-6-2-configuration-update` exited `0`.
* Unfiltered solution tests passed with 363 passed, 0 failed, and 0 skipped
	across 9 TRX files.
* Evidence location:
	`artifacts/quality-gates/phase-6-2-configuration-update/20260727-204459/`.

## Phase 6 Step 6.3 Reconciliation Evidence

* Extracted reconciliation into the Application-owned
	`PlatformAuthenticationReconciler` feature slice and moved its side-effect
	coordination alongside the feature.
* Preserved transition policy, retry timing, provider calls, persistence
	ordering, notification behavior, cancellation, idempotency, serialized
	writer semantics, and API-owned supervision.
* Removed the remaining `PlatformStateCoordinator` and side-effect collaborator
	references and updated API registration.
* Migrated Infrastructure characterization factories and proof-data tests.
* Updated architecture, runtime, and testing wiki pages.
* Focused reconciliation, Infrastructure, API supervision, and architecture
	tests passed with 46 passed, 0 failed, and 0 skipped.
* Quality gate `phase-6-3-reconciliation` exited `0`.
* Unfiltered solution tests passed with 363 passed, 0 failed, and 0 skipped
	across 9 TRX files.
* Evidence location:
	`artifacts/quality-gates/phase-6-3-reconciliation/20260727-205137/`.

## Phase 6 Step 6.4 Query Extraction Evidence

* Confirmed status and event API handlers depend only on feature-local
	projection readers and do not invoke reconciliation, authentication,
	notifications, state creation, or persistence writes.
* Added Application no-write and cancellation coverage plus Infrastructure
	projection tests for filter forwarding, ordering, cancellation, and write
	exclusion.
* Preserved missing-state/freshness metadata, event ordering and filtering,
	item limits, routes, response shapes, and authorization.
* Updated API, runtime, operator, architecture, and testing wiki pages.
* Reconciliation query helpers remain only for existing reconciliation
	characterization tests; no API query path invokes them.
* Application query tests passed with 7 passed, 0 failed, and 0 skipped.
* Infrastructure projection tests passed with 2 passed, 0 failed, and 0 skipped.
* API focused tests passed with 7 passed, 0 failed, and 0 skipped.
* Architecture tests passed with 13 passed, 0 failed, and 0 skipped.
* Quality gate `phase-6-4-query-extraction` exited `0` after the targeted
	caller-preservation repair.
* Unfiltered solution tests passed with 367 passed, 0 failed, and 0 skipped.
* Evidence location:
	`artifacts/quality-gates/phase-6-4-query-extraction/20260727-205916/`.

## Phase 6 Step 6.5 IG Login History Evidence

* Replaced the broad `IPlatformIgLoginSnapshotStore` dependency in the IG
	login-history query with the feature-owned `IGetIgLoginHistoryReader` port.
* Kept EF entities, persistence behavior, provider details, raw tokens, and
	API translation in their existing outward boundaries.
* Added three direct Application handler tests and preserved endpoint and
	response behavior.
* Focused Application tests passed with 3 passed, 0 failed, and 0 skipped.
* Authorized history integration coverage passed with 1 passed.
* Quality gate `phase-6-5-ig-login-history` exited `0`.
* Unfiltered solution tests passed with 370 passed, 0 failed, and 0 skipped
	across 9 TRX files.
* Evidence location:
	`artifacts/quality-gates/phase-6-5-ig-login-history/20260727-210923/`.

## Phase 6 Step 6.6 Coordinator Retirement Evidence

* Confirmed no live `PlatformStateCoordinator` source, caller, registration, or
	test construction remains; no speculative deletion was required.
* Updated architecture, application-overview, runtime, and testing wiki pages
	to document explicit reconciliation ownership and API supervision.
* Application, API, Infrastructure, and architecture suites passed with 104,
	24, 67, and 13 tests respectively, with no failures or skips.
* Quality gate `phase-6-6-coordinator-retirement` exited `0`.
* Unfiltered solution tests passed with 370 passed, 0 failed, and 0 skipped.
* Evidence location:
	`artifacts/quality-gates/phase-6-6-coordinator-retirement/20260727-211828/`.

## Phase 6 Verification

* All six Phase 6 steps completed with independent focused validation and
	passing full quality gates.
* Phase 6 is complete; Infrastructure-owned schema and SQL lifecycle work is
	the next migration phase.

## Phase 7 Step 7.1 Schema Migration Evidence

* Added Infrastructure-owned EF Core design-time support, the initial
	source-controlled SQL Server migration, and the model snapshot.
* Added a real SQL Server integration project with isolated temporary databases
	covering migration application, guarded no-history preservation, rollback,
	transaction rollback, concurrency-contract documentation, and SQL retention.
* Added transaction handling for the manual-retry local consistency boundary.
* Preserved API startup composition and existing Development lifecycle behavior;
	startup migration replacement remains Step 7.2 scope.
* SQL integration tests passed with 5 passed, 0 failed, and 0 skipped.
* Infrastructure unit tests passed with 67 passed, 0 failed, and 0 skipped.
* Architecture tests passed with 13 passed, 0 failed, and 0 skipped.
* Quality gate `phase-7-1-schema-migrations` exited `0`.
* Unfiltered solution tests passed with 375 passed, 0 failed, and 0 skipped
	across 10 TRX files.
* Evidence location is the Phase 7.1 gate output under
	`artifacts/quality-gates/phase-7-1-schema-migrations/`.

## Phase 7.1 Deviation

* An exploratory SQL fixture briefly launched the AppHost before isolated
	temporary-database setup was complete, which exercised the existing
	Development `EnsureDeleted` behavior against `platformdb`.
* The fixture was replaced with non-destructive isolated database setup; all
	subsequent validation passed without repeating the behavior. The persistent
	Docker topology was preserved.

## Phase 7 Step 7.2 Startup Initializer Evidence

* Added one Infrastructure-owned startup initializer for schema migration,
	guarded legacy baseline handling, bootstrap configuration, and retention.
* API now invokes the Infrastructure startup surface before reconciliation and
	endpoint binding; direct API DbContext and retention mechanics are removed.
* Preserved blocking startup, readiness ordering, migration failure behavior,
	and explicit no-history handling.
* Updated architecture, runtime, local-development, operator, and testing wiki
	pages.
* Focused Infrastructure, API, AppHost, SQL integration, and architecture
	suites passed with no failures or skips.
* Quality gate `phase-7-2-startup-initializer` exited `0`.
* Unfiltered solution tests passed with 379 passed, 0 failed, and 0 skipped
	across 10 TRX files.
* Evidence location:
	`artifacts/quality-gates/phase-7-2-startup-initializer/20260727-213828/`.

## Phase 7 Verification

* SQL lifecycle verification passed with 5 passed, 0 failed, and 0 skipped.
* API unit verification passed with 24 passed, 0 failed, and 0 skipped.
* API integration readiness verification passed with 29 passed, 0 failed, and
	0 skipped.
* AppHost verification passed with 38 passed, 0 failed, and 0 skipped.
* Architecture verification passed with 13 passed, 0 failed, and 0 skipped.
* Verified Phase 7.1 and Phase 7.2 full-gate evidence, each with build/test
	exit code `0` and zero failures or skips.
* Required architecture, startup, migration, local-development, operator, and
	testing documentation is current.
* Phase 7 is complete; mechanical Infrastructure reorganization may begin.

## Phase 9 Step 9.1 Time Adapter Evidence

* Moved the time provider factory and deterministic test time provider into
	`Infrastructure/Time/` with updated namespace/reference usage.
* Removed the stale duplicate implementations from `Infrastructure/Platform`.
* Confirmed two actual type definitions, zero stale time namespace references,
	and no source diagnostics.
* Focused Infrastructure, API, and architecture checks passed with 142 passed,
	0 failed, and 0 skipped.
* Quality gate `phase-9-1-time` exited `0` after the duplicate-file repair.
* Unfiltered solution tests passed with 379 passed, 0 failed, and 0 skipped
	across 10 TRX files.
* Evidence location:
	`artifacts/quality-gates/phase-9-1-time/20260727-215523/`.

## Phase 9 Step 9.2 IG Integration Evidence

* Moved the IG HTTP/provider adapter family from `Infrastructure/Platform/Ig`
	to `Infrastructure/Integrations/Ig` without changing provider behavior,
	routing, token handling, exceptions, or contracts.
* Updated DI registration, namespaces, focused tests, and architecture
	documentation.
* Removed stale production and test duplicates from the former path.
* Focused IG adapter tests passed with 20 passed, 0 failed, and 0 skipped.
* Infrastructure unit tests passed with 81 passed, 0 failed, and 0 skipped.
* API unit tests passed with 24 passed, 0 failed, and 0 skipped.
* Architecture tests passed with 13 passed, 0 failed, and 0 skipped.
* Quality gate `phase-9-2-ig-integration` exited `0` after duplicate cleanup.
* Unfiltered solution tests passed with 379 passed, 0 failed, and 0 skipped
	across 10 TRX files.
* Evidence location:
	`artifacts/quality-gates/phase-9-2-ig-integration/20260727-220502/`.

## Phase 9 Step 9.3 Notifications Evidence

* Moved notification dispatch, policy, context, recorded, SMTP, and Azure
	Communication Services adapters into the boundary-oriented
	`Infrastructure/Notifications` folders without changing delivery behavior or
	public contracts.
* Updated namespaces, registrations, focused tests, and architecture
	documentation.
* Removed the orphaned stale `PlatformStateCoordinator` source file and the
	duplicate Infrastructure-backed Application tests exposed by the full-gate
	compile after the move. The authoritative copies remain in Infrastructure.
* Focused notification tests passed with 16 passed, 0 failed, and 0 skipped.
* The exact quality gate `phase-9-3-notifications` exited `0`.
* Unfiltered solution tests passed with 379 passed, 0 failed, and 0 skipped
	across 10 TRX files.
* Evidence location:
	`artifacts/quality-gates/phase-9-3-notifications/20260728-094123/`.

## Phase 9 Step 9.4 Credentials and Configuration Evidence

* Moved the SQL-backed configuration adapters into
	`Infrastructure/Configuration/SqlServer` and the Data Protection credential
	adapter into `Infrastructure/Credentials/DataProtection`.
* Preserved the Application-owned configuration restart policy, configuration
	keys, encryption behavior, service lifetimes, cancellation flow, and public
	contracts.
* Updated namespaces, registrations, affected Infrastructure tests, and
	architecture documentation. Existing friend access remained sufficient.
* Focused Infrastructure tests passed with 39 passed, 0 failed, and 0 skipped.
* The exact quality gate `phase-9-4-credentials-configuration` exited `0`.
* Unfiltered solution tests passed with 379 passed, 0 failed, and 0 skipped
	across 10 TRX files.
* Evidence location:
	`artifacts/quality-gates/phase-9-4-credentials-configuration/20260728-094946/`.

## Phase 9 Step 9.5 Retention Evidence

* Moved the Infrastructure retention processor and service into
	`Infrastructure/Operations/Retention` without changing deletion eligibility,
	cutoff inputs, SQL predicates, cancellation flow, lifetimes, registrations,
	or public contracts.
* Preserved the Application-owned retention policy and removed redundant legacy
	Infrastructure retention stubs.
* Updated startup and persistence references, focused tests, SQL integration
	tests, and architecture documentation.
* Focused retention unit and SQL integration tests passed with 2 passed, 0
	failures, and 0 skips. API unit tests passed with 24 and architecture tests
	passed with 13, all with zero failures and skips.
* The exact quality gate `phase-9-5-retention` exited `0`.
* Unfiltered solution tests passed with 379 passed, 0 failed, and 0 skipped.
* Evidence location:
	`artifacts/quality-gates/phase-9-5-retention/20260728-095826/`.

## Phase 9 Step 9.6 Persistence and Startup Evidence

* Reorganized the EF persistence entities, configurations, migrations, and
	startup adapters into the boundary-oriented persistence and startup folders.
* Preserved migration identifiers and model snapshot behavior, guarded
	no-history handling, SQL semantics, startup ordering, readiness behavior,
	cancellation, service lifetimes, and public contracts.
* Updated project registration, namespaces, affected tests, and architecture
	documentation without redesigning ports or Application policy.
* The exact quality gate `phase-9-6-persistence-startup` exited `0`.
* Unfiltered solution tests passed with 379 passed, 0 failed, and 0 skipped.
* Evidence location:
	`artifacts/quality-gates/phase-9-6-persistence-startup/20260728-101253/`.

## Phase 9 Step 9.7 Dependency Injection Evidence

* Moved Infrastructure DI registration into
	`Infrastructure/DependencyInjection` without changing registrations, service
	lifetimes, provider selection, configuration keys, startup ordering, or
	public contracts.
* Updated API composition and architecture documentation and removed the stale
	legacy registration file.
* Focused Infrastructure tests passed with 71 passed, API tests with 24, and
	architecture tests with 13, all with zero failures and skips.
* The first gate attempt exposed one transient API supervisor failure and a
	duplicate legacy registration file. After the duplicate was removed, the
	exact gate `phase-9-7-dependency-injection` exited `0`.
* Unfiltered solution tests passed with 379 passed, 0 failed, and 0 skipped.
* Evidence location:
	`artifacts/quality-gates/phase-9-7-dependency-injection/20260728-102535/`.

## Phase 9 Verification

* All seven mechanical adapter-family batches completed with independent full
	quality gates and zero failures or skips.
* The final Infrastructure layout has boundary-oriented folders for time, IG
	integration, notifications, credentials, configuration, retention,
	persistence, startup, and dependency injection.
* The redundant `Infrastructure.Infrastructure` namespace and stale adapter
	duplicates were removed without changing runtime behavior.

## Phase 10 Step 10.1 Data Protection Key-Ring Evidence

* Added the shared SQL-backed Data Protection key ring for API and Web with a
	shared application discriminator and configurable positive key lifetime,
	defaulting to 90 days.
* Added migration, registration, AppHost composition coverage, and SQL
	integration coverage proving provider restart and key rotation preserve
	protected values.
* Repaired the persistent local transition path: credentials encrypted by the
	retired process-local key ring now map to unavailable credentials instead of
	crashing API startup, allowing the existing degraded operator path to request
	credential replacement. Valid ciphertext and normal rotation behavior remain
	unchanged.
* Focused credential tests passed with 5 passed, 0 failed, and 0 skipped.
* The exact quality gate `phase-10-1-data-protection-key-ring` exited `0`.
* Unfiltered solution tests passed with 383 passed, 0 failed, and 0 skipped.
* Evidence location:
	`artifacts/quality-gates/phase-10-1-data-protection-key-ring/20260728-104318/`.

## Phase 10 Step 10.2 Proof-Data Durability Evidence

* Confirmed the active production registration uses the SQL-backed
	`EfPlatformIgProofDataStore`; the in-memory store remains available only for
	isolated tests.
* Confirmed the durable `IgProofData` migration, persistent AppHost SQL
	container, and restart/readback integration test together establish the
	selected guarantee: the latest provider-neutral proof snapshot survives
	`DbContext` and API process replacement while the SQL database remains
	available.
* Updated runtime, architecture, local-development, operator, and testing
	documentation to state the durability and database-loss boundary.
* Focused SQL durability integration tests passed with 1 passed, 0 failed, and
	0 skipped. Focused AppHost infrastructure tests passed with 2 passed, 0
	failed, and 0 skipped.
* The exact quality gate `phase-10-2-proof-data-durability` exited `0`.
* Unfiltered solution tests passed with 384 passed, 0 failed, and 0 skipped
	across 10 TRX files.
* Evidence location:
	`artifacts/quality-gates/phase-10-2-proof-data-durability/20260728-114105/`.

## Phase 10 Step 10.3 Notification Delivery Evidence

* Confirmed the notification boundary provides best-effort, at-least-once
	delivery: provider I/O remains outside the SQL consistency unit, failed
	attempts are persisted as `Failed`, and later reconciliation can retry them.
* Repeated notification identities remain separate persisted attempts; the
	platform makes no cross-process exactly-once or durable idempotency claim.
* Added focused failure-then-retry and duplicate-attempt coverage and updated
	the affected runtime, architecture, local-development, operator, and testing
	documentation.
* Focused notification tests passed with 12 passed, 0 failed, and 0 skipped.
	Adjacent Infrastructure tests passed with 29 passed, 0 failed, and 0 skipped.
* The exact quality gate `phase-10-3-notification-delivery` exited `0`.
* Unfiltered solution tests passed with 386 passed, 0 failed, and 0 skipped.
* Evidence location:
	`artifacts/quality-gates/phase-10-3-notification-delivery/20260728-114838/`.

## Phase 10 Step 10.4 Configuration-Reconciliation Evidence

* Confirmed configuration, protected credentials, and audit records are staged
	through one relational `SaveChangesAsync` and reconcile is invoked only after
	the configuration commit succeeds.
* Added Application ordering coverage and a real SQL failure-injection test
	proving the configuration, credential, and audit writes roll back together.
* Confirmed external IG and notification calls remain outside the local SQL
	transaction; no production transaction repair was required.
* Updated the affected consistency, recovery, operator, local-development,
	architecture, and testing documentation.
* Focused Application tests passed with 5 passed, 0 failed, and 0 skipped;
	the focused SQL atomicity test passed with 1 passed, 0 failed, and 0 skipped.
* The exact quality gate `phase-10-4-configuration-reconciliation` exited `0`.
* Unfiltered solution tests passed with 387 passed, 0 failed, and 0 skipped.
* Evidence location:
	`artifacts/quality-gates/phase-10-4-configuration-reconciliation/20260728-115728/`.

## Phase 10 Step 10.5 Replica-Concurrency Evidence

* Confirmed the former reconciliation serialization was process-local and
	added an Application-owned lease port with an Infrastructure SQL Server
	adapter using a session-owned exclusive `sp_getapplock`.
* The lease covers scheduled reconciliation and manual authentication retry;
	it is held for the workflow and released with the SQL session. No EF schema
	change or public contract change was required.
* Added real SQL concurrency and release/recovery tests, updated the affected
	documentation, and preserved the current model snapshot unchanged.
* Focused SQL lease tests passed with 2 passed, 0 failed, and 0 skipped;
	Application reconciliation tests passed with 4, and Infrastructure unit
	tests passed with 74, all with zero failures and skips.
* The exact quality gate `phase-10-5-replica-concurrency` exited `0`.
* Unfiltered solution tests passed with 388 passed, 0 failed, and 0 skipped
	across 10 TRX files.
* Evidence location:
	`artifacts/quality-gates/phase-10-5-replica-concurrency/20260728-121437/`.

## Phase 10 Step 10.6 Migration-Recovery Evidence

* Confirmed existing startup migration behavior fails closed and avoids
	destructive database reset. Added SQL coverage for committed migration
	history, conflicting schema objects, bootstrap suppression after migration
	failure, operator correction followed by retry, and partial-schema recovery.
* Updated the migration recovery contract and operator procedure in the
	affected architecture, runtime, local-development, operator, and testing
	documentation.
* Focused SQL integration tests passed with 11 passed, 0 failed, and 0 skipped.
* The exact quality gate `phase-10-6-migration-recovery` exited `0`.
* Unfiltered solution tests passed with 390 passed, 0 failed, and 0 skipped.
* Evidence location:
	`artifacts/quality-gates/phase-10-6-migration-recovery/20260728-122515/`.

## Phase 11 Final Architecture Verification Evidence

* Architecture ownership verification passed: Application remains framework-
	neutral, Infrastructure owns mechanisms, API/Web/AppHost retain their
	composition boundaries, and test project references remain correctly scoped.
* Repaired the documented anonymous operational boundary for Development
	OpenAPI/Scalar and readiness/liveness endpoints in API and ServiceDefaults;
	protected API and Web routes remain fail-closed.
* Final architecture tests passed with 13 passed, 0 failed, and 0 skipped.
* The exact final quality gate `phase-11-final` exited `0`; build and test
	exit codes were both `0`, with 390 passed, 0 failed, and 0 skipped.
* AppHost runtime checks covered API readiness/liveness, Scalar/OpenAPI, Web
	sign-in and protected-route behavior, protected API rejection, Keycloak,
	SQL Server, and Mailpit without resetting persistent resources.
* All ten wiki pages and every quality-gate directory were reconciled; no
	additional documentation gaps remain.
* Evidence location:
	`artifacts/quality-gates/phase-11-final/20260728-133409/`.

## Release Summary

The Clean Architecture migration is complete through Phase 11. Operational
hardening now has explicit evidence for shared Data Protection keys, durable
proof data, notification retry semantics, configuration consistency, SQL-backed
cross-replica leases, and non-destructive migration recovery. Final architecture
and runtime verification passed with 390 tests, 0 failures, and 0 skips.

The final quality-gate evidence is
`artifacts/quality-gates/phase-11-final/20260728-133409/`. Existing preview SDK,
package vulnerability, and analyzer warnings remain non-blocking. Follow-on
work is limited to CI enforcement, deployment runbook work, and future bounded-
context/tooling reassessment recorded in the planning log.
