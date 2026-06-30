<!-- markdownlint-disable-file -->
# Release Changes: Refactoring and Testability Opportunities

**Related Plan**: refactoring-and-testability-opportunities-plan.instructions.md
**Implementation Date**: 2026-06-26

## Summary

Implementation tracking for the prioritized refactoring and recurring validation work.

Phase 1 established the hotspot-to-harness ownership map, defined the recurring validation cadence for lightweight and milestone lanes, and recorded the pre-refactor baseline outcomes for the required baseline harness set. The baseline now shows one existing Web unit failure while the Application, API authentication integration, and Web authentication functional lanes are green.

Phase 2 extracted a pure tick-decision seam from `PlatformStateCoordinator`, moved secondary application side effects behind dedicated collaborators, and reshaped `PlatformAuthSupervisor` around a single-tick runner plus injected delay seam. The focused Application lane and the required milestone authentication lanes remained green after these changes.

Phase 3 extracted presenter-style Web seams for Configuration, Home, and Status so page orchestration, request mapping, redirect decisions, IG status shaping, and alert composition can be tested directly without concentrating that behavior inside Razor components. The immediate Configuration gate passed after the first extraction, and the Web milestone gate finished with the same single pre-existing Web unit failure while the API authentication integration, Web authentication functional, and Web authentication E2E lanes all remained green.

Phase 4 decomposed the API and infrastructure hotspots without widening into the shared-harness consolidation reserved for Phase 5. The Platform endpoint module now delegates configuration-update validation translation, manual-retry conflict translation, and auth-audit request translation to smaller feature-local handlers with direct unit coverage. The infrastructure layer now exposes dedicated plain-rule seams for bootstrap parsing, restart-required evaluation, notification dispatch context creation, and retention planning, leaving the existing SQL, EF, and provider-backed components focused on boundary work.

Phase 5 completed the deferred shared-harness consolidation work without widening beyond the test infrastructure scope reserved for this phase. The shared AppHost authentication helpers remained validated through strict sequential execution, and the Web unit harness now exposes a lighter service-only setup for presenter and client tests so render-only registrations stay limited to actual component-rendering checks. The required milestone gate reran every affected authentication and Web unit lane, with all distributed-auth lanes green and the Web unit lane preserving only the documented baseline `PlatformApiClientTests` failure shape.

## Changes

### Added

* Baseline hotspot-to-harness ownership map covering the first-wave Application, Web, API, and Infrastructure refactor targets.
* Phase gate validation cadence that separates lightweight slice checks from milestone AppHost-backed authentication lanes.
* Pre-refactor baseline test run record for the required Application unit, Web unit, API authentication integration, and Web authentication functional lanes, including the existing Web unit failure classification.
* `PlatformStateTransitionEngine` and `PlatformTickDecision` seam types for direct unit testing of coordinator branch selection.
* `PlatformStateCoordinatorSideEffects` and `PlatformIgProofDataEnricher` collaborators to isolate secondary coordinator concerns.
* `PlatformAuthSupervisorTickRunner`, `IPlatformAuthSupervisorDelay`, and focused supervisor loop tests to remove wall-clock waiting from the supervisor test surface.
* `ConfigurationPagePresenter`, `ConfigurationFormModel`, and `ConfigurationFormModelMapper` to extract Configuration load/save orchestration and request-mapping behavior from the Razor component.
* `HomePagePresenter` to extract overview loading, access-denied redirect handling, and alert composition from the Home page.
* `StatusPagePresenter` to extract status loading, manual-retry message shaping, and IG login display formatting from the Status page.
* `UpdatePlatformConfigurationEndpointHandler`, `TriggerManualAuthRetryEndpointHandler`, and `RecordAuthAuditEventEndpointHandler` to isolate API request-to-result translation from the static endpoint registration module.
* `PlatformConfigurationBootstrapParser` and `PlatformConfigurationRestartPolicy` to isolate bootstrap parsing and restart-required policy from `SqlPlatformConfigurationStore`.
* `NotificationDispatchPolicy` and `NotificationDispatchContext` to isolate recipient, provider, and redaction decisions from `NotificationDispatcher`.
* `OperationalRecordRetentionPolicy` and `OperationalRecordRetentionPlan` to isolate retention-window parsing and retained-snapshot-kind selection from `OperationalRecordRetentionProcessor`.

### Modified

* .copilot-tracking/changes/2026-06-26/refactoring-and-testability-opportunities-changes.md - Recorded Phase 1 scope, baseline harness ownership, and validation outcomes.
* .copilot-tracking/plans/2026-06-26/refactoring-and-testability-opportunities-plan.instructions.md - Marked Implementation Phase 1 and its steps complete after baseline evidence was captured.
* .copilot-tracking/plans/logs/2026-06-26/refactoring-and-testability-opportunities-log.md - Logged the hotspot inventory, validation cadence, and baseline lane outcomes, including any environment-sensitive behavior.
* src/TNC.Trading.Platform.Application/Services/PlatformStateCoordinator.cs - Routed tick branch selection through the extracted decision engine and delegated secondary effects to dedicated collaborators.
* src/TNC.Trading.Platform.Application/Services/PlatformAuthSupervisor.cs - Reduced the background loop to tick-runner plus injected-delay orchestration.
* test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/AuthRetryCycleTests.cs - Added direct tests for the new coordinator tick-decision seam.
* src/TNC.Trading.Platform.Web/Components/Pages/Configuration.razor - Replaced inline load/save orchestration with presenter delegation while preserving view binding behavior.
* src/TNC.Trading.Platform.Web/Components/Pages/Home.razor - Replaced inline role-routing and overview-loading logic with presenter delegation.
* src/TNC.Trading.Platform.Web/Components/Pages/Status.razor - Replaced inline load, retry, and IG status formatting logic with presenter delegation.
* src/TNC.Trading.Platform.Web/PlatformWebUiServiceCollectionExtensions.cs - Registered the new Web presenter seams for scoped resolution.
* test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/ConfigurationTests.cs - Added direct presenter tests alongside the existing component behavior checks.
* test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/HomeTests.cs - Added focused tests for the extracted Home presenter behavior.
* test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/StatusTests.cs - Added focused tests for the extracted Status presenter behavior.
* test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/PlatformComponentTestContext.cs - Registered the new presenters for Web unit tests.
* test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/PlatformComponentTestContext.cs - Added a lighter service-only context path so presenter and API-client tests no longer carry render-only registrations by default.
* test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/PlatformApiClientTests.cs - Moved API-client-only coverage onto the lighter service context.
* test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/ConfigurationTests.cs - Moved presenter-only coverage onto the lighter service context while keeping render tests on the full component context.
* test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/HomeTests.cs - Moved presenter-only coverage onto the lighter service context while keeping render tests on the full component context.
* test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/StatusTests.cs - Moved presenter-only coverage onto the lighter service context while keeping render tests on the full component context.
* src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs - Reduced inline translation logic by delegating the validation-heavy and exception-translation-heavy endpoints to dedicated handlers while preserving route registrations.
* src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/SqlPlatformConfigurationStore.cs - Delegated bootstrap parsing and restart-required policy to extracted plain-rule collaborators.
* src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/NotificationDispatcher.cs - Delegated recipient, provider, and summary-redaction policy to an extracted dispatch-policy seam.
* src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/OperationalRecordRetentionProcessor.cs - Delegated retention-window parsing and retained-snapshot planning to an extracted policy seam.
* test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/PlatformEndpointHandlerTests.cs - Added direct coverage for validation-problem, conflict, and unsupported auth-audit translation behavior.
* test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/PlatformConfigurationBootstrapParserTests.cs - Added focused bootstrap parsing and notification-provider selection tests.
* test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/NotificationDispatchPolicyTests.cs - Added focused recipient and redaction policy tests.
* test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/OperationalRecordRetentionPolicyTests.cs - Added focused retention-window and retained-snapshot policy tests.

### Removed

* None yet.

## Additional or Deviating Changes

* The baseline harness inventory expands the representative files named in the plan into concrete owning lanes for Home, Status, PlatformEndpoints, SqlPlatformConfigurationStore, NotificationDispatcher, and OperationalRecordRetentionProcessor so later phases can choose the cheapest proving lane without reopening discovery.
* The milestone-lane notes keep the Web E2E authentication suite out of the Phase 1 baseline command set, matching the details artifact that reserves it for Web-boundary, shared-harness, and final validation milestones.
* The baseline run surfaced an existing failing Web unit test in `PlatformApiClientTests`. Later phases should not treat that signal as a fresh production-code regression unless the failure shape changes.
* The Web functional authentication baseline required clearing a one-time terminal approval prompt before the lane could be rerun to completion. That prompt was environmental rather than product-code related.

## Release Summary

Phase 1 completed the hotspot inventory, validation-cadence work, and full baseline harness capture before any refactor slice begins. The tracking artifacts now assign each first-wave hotspot both a fast proving lane and a heavier regression lane, define when lightweight versus milestone harnesses must run, and record that the Application, API authentication, and Web functional authentication baselines were green while the Web unit lane already had one failing test.

Phase 2 completed the planned application-layer seam extraction without widening into later Web, API, or Infrastructure phases. The coordinator now exposes directly testable transition-decision logic, optional proof-data enrichment and other secondary effects are isolated behind dedicated collaborators, and the supervisor loop can be exercised without real time delays. The required immediate and milestone validation gates both completed successfully, with no change to the known pre-existing Web unit failure outside this phase's scope.

Phase 3 completed the planned Web seam extraction without widening into the later API, Infrastructure, or shared-harness phases. Configuration now routes page load, save, mapping, and save-message decisions through a plain presenter seam. Home now routes access-denied and overview-loading behavior through a presenter. Status now routes refresh, manual-retry result shaping, and IG state formatting through a presenter. The immediate Configuration gate passed before Home and Status were touched, the touched Web slice tests passed after one local test-fix iteration, the full Web unit lane still showed only the known pre-existing `PlatformApiClientTests` failure, and the milestone API authentication integration, Web authentication functional, and Web authentication E2E lanes all completed successfully.

Phase 4 completed the planned API and infrastructure hotspot decomposition without beginning the shared-harness consolidation reserved for Phase 5. The API now exposes directly callable endpoint translation handlers for configuration validation, manual retry conflicts, and auth-audit request handling while preserving the existing routes and authorization behavior. The infrastructure layer now exposes directly testable seams for bootstrap parsing, restart-required evaluation, notification dispatch context creation, and retention planning, while existing persistence-oriented tests continue to cover the SQL and EF boundaries. The immediate API gate passed before any infrastructure edits began, the focused infrastructure lane passed after the rule extraction, and the milestone validation set will record the Web E2E authentication lane as intentionally skipped because Phase 4 did not alter auth bootstrap contracts, browser-facing authentication behavior, or end-to-end startup behavior.

Phase 5 completed the planned shared-harness work and stayed inside the test-infrastructure scope reserved for it. The earlier shared AppHost helper consolidation remained validated once the distributed-auth lanes were kept strictly sequential, which resolved the misleading contradictory failures caused by overlapping AppHost-backed runs. The Web unit harness now distinguishes between service-only presenter and API-client checks versus actual component-rendering checks, which trims duplicated render-only bootstrapping from the non-rendering slice without disturbing the bUnit-backed component tests. The Phase 5 milestone gate reran the API authentication integration, Web authentication functional, Web authentication E2E, and full Web unit lanes. The three distributed-auth lanes passed, and the full Web unit lane still showed only the already-documented `PlatformApiClientTests.GetStatusAsync_ShouldReturnParsedStatus_WhenApiReturnsPayload` failure with the same null-snapshot assertion shape.

Phase 6 completed the final selected-project validation set and confirmed that the refactor sequence closed without introducing any new failures inside the changed slices. The Application unit, API unit, API authentication integration, Infrastructure unit, Web authentication functional, and Web authentication E2E lanes all passed. The full Web unit lane still showed only the already-accepted baseline `PlatformApiClientTests.GetStatusAsync_ShouldReturnParsedStatus_WhenApiReturnsPayload` null assertion with no change in failure shape, so the plan was closed with a documented residual noise item rather than reopening earlier phases.

The final validation pass also documented one tooling nuance in the repository: folder-form commands such as `dotnet test test/TNC.Trading.Platform.Api` are not directly executable from the repository root because those directories do not contain a local solution or project file. For Phase 6, the selected project set was validated through the concrete contained `.csproj` files while preserving the intended slice coverage from the plan.