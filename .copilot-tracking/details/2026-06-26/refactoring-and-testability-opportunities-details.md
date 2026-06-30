---
title: Refactoring and Testability Opportunities Details
description: Step-by-step implementation details for the recommended refactoring path with recurring test-harness validation.
author: GitHub Copilot
ms.date: 2026-06-26
ms.topic: how-to
keywords:
  - refactoring
  - testability
  - validation
  - apphost
estimated_reading_time: 9
---
<!-- markdownlint-disable-file -->

## Context Reference

Sources: .copilot-tracking/research/2026-06-26/refactoring-and-testability-opportunities-research.md, test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/AuthRetryCycleTests.cs, test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/ConfigurationTests.cs, test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/RealAuthenticationIntegrationTestFixture.cs, test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/Authentication/RealAuthenticationFunctionalTestFixture.cs, /memories/repo/aspire-test-notes.md

## Implementation Phase 1: Baseline current behavior and establish refactor safety rails

<!-- parallelizable: false -->

### Step 1.1: Capture hotspot-to-harness mapping

Document the first-wave refactor targets and pair each one with the fastest trustworthy harness plus the heavier regression harness that protects it. The goal is to avoid entering the refactor with unclear validation ownership.

Files:
* src/TNC.Trading.Platform.Application/Services/PlatformStateCoordinator.cs - first application hotspot
* src/TNC.Trading.Platform.Application/Services/PlatformAuthSupervisor.cs - supervisor hotspot
* src/TNC.Trading.Platform.Web/Components/Pages/Configuration.razor - first Web extraction target
* src/TNC.Trading.Platform.Web/Components/Pages/Home.razor - second Web extraction target
* src/TNC.Trading.Platform.Web/Components/Pages/Status.razor - third Web extraction target
* src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs - API decomposition target
* src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/SqlPlatformConfigurationStore.cs - infrastructure rules-plus-persistence target
* src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/NotificationDispatcher.cs - infrastructure dispatch target
* src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/OperationalRecordRetentionProcessor.cs - infrastructure retention target
* test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/AuthRetryCycleTests.cs - application behavior harness
* test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/ConfigurationTests.cs - Web behavior harness
* test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/RealAuthenticationIntegrationTestFixture.cs - API milestone harness
* test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/Authentication/RealAuthenticationFunctionalTestFixture.cs - Web milestone harness

Success criteria:
* Each first-wave refactor slice has at least one fast validation lane and one milestone regression lane assigned
* No targeted production file enters a phase without a named proving harness

Context references:
* .copilot-tracking/research/2026-06-26/refactoring-and-testability-opportunities-research.md (Lines 41-72) - hotspot inventory
* .copilot-tracking/research/2026-06-26/refactoring-and-testability-opportunities-research.md (Lines 119-142) - current high-cost harness shape

Dependencies:
* Research findings accepted as the prioritization basis

### Step 1.2: Define the recurring validation cadence

Set the execution rules before implementation starts. Lightweight gates run after every refactor slice in the touched area. Milestone gates run after completing each major boundary shift: application seams, Web seam extraction, API and infrastructure decomposition, and shared harness consolidation.

Validation rules:
* After each application slice, run the scoped application unit tests that cover the touched transition or supervisor behavior
* After each Web slice, run the scoped Web unit tests and any touched API client tests
* After each API or infrastructure slice, run the scoped unit or contract tests for the touched area
* After Phases 2, 3, 4, and 5, rerun the AppHost-backed authentication integration and functional harnesses
* Treat the Web E2E authentication lane as a mandatory milestone lane after Web-boundary refactors, shared-harness refactors, and final validation, and as a conditional lane after application or API phases when auth bootstrap contracts or browser-facing flows change
* Before Phase 6 completes, run the full selected project test set, including any E2E lane the team treats as release-blocking

Discrepancy references:
* DD-01

Success criteria:
* The plan defines both slice-level and milestone-level validation expectations
* Test execution is described as a required gate, not optional follow-up

Context references:
* .copilot-tracking/research/2026-06-26/refactoring-and-testability-opportunities-research.md (Lines 132-142) - distributed auth confidence currently depends on expensive suites
* /memories/repo/aspire-test-notes.md (Lines 1-1) - repository-specific caution for Aspire-oriented test execution

Dependencies:
* Step 1.1 completion

### Step 1.3: Run and record the baseline harness set

Execute the current fast and milestone lanes before changing code. This creates a known-good baseline for behavior, failure signatures, and runtime expectations. If any lane is already unstable, note that in the working log before proceeding so new failures can be separated from existing noise.

Validation commands:
* dotnet test test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests
* dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests
* dotnet test test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests --filter Authentication
* dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests --filter Authentication

Success criteria:
* Baseline results are captured before the first production edit
* Existing failures, if any, are explicitly separated from refactor regressions

Context references:
* .copilot-tracking/research/2026-06-26/refactoring-and-testability-opportunities-research.md (Lines 73-104) - representative test files

Dependencies:
* Step 1.2 completion

## Implementation Phase 2: Extract application-layer domain seams first

<!-- parallelizable: false -->

### Step 2.1: Split transition decisions from PlatformStateCoordinator orchestration

Extract pure decision-making into a dedicated transition engine or equivalent domain service that accepts current state, retry context, broker outcomes, and manual commands, then returns the next state and declared effects. Keep the existing coordinator as the orchestration shell responsible for persistence and integration calls.

Files:
* src/TNC.Trading.Platform.Application/Services/PlatformStateCoordinator.cs - reduce orchestration and duplicate transition logic
* src/TNC.Trading.Platform.Application/Services/ - add new decision-oriented seam types
* test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/ - add table-driven tests for extracted decision logic

Discrepancy references:
* None

Success criteria:
* Transition rules are directly unit testable without EF-backed collaborator setup
* Coordinator orchestration remains behaviorally equivalent at the public boundary

Context references:
* .copilot-tracking/research/2026-06-26/refactoring-and-testability-opportunities-research.md (Lines 186-211) - recommended first move and rationale

Dependencies:
* Phase 1 complete

### Step 2.2: Run the immediate coordinator validation gate

Before expanding the application refactor, run the focused coordinator tests introduced or touched by Step 2.1. This enforces the rule that each structural slice proves itself before the next slice starts.

Validation commands:
* dotnet test test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests --filter AuthRetryCycleTests

Success criteria:
* The new transition seam is validated immediately after extraction
* Failures are attributable to the coordinator slice rather than a later combined set of changes

Dependencies:
* Step 2.1 completion

### Step 2.3: Isolate secondary side effects and supervisor timing control

Move proof-data enrichment, notification emission, operational event recording, and retry-cycle persistence behind dedicated collaborators where the coordinator currently mixes them with primary decision flow. In parallel within the same phase sequence, reshape PlatformAuthSupervisor around a single-tick runner or scheduler abstraction that removes direct wall-clock coupling from its core behavior.

Files:
* src/TNC.Trading.Platform.Application/Services/PlatformStateCoordinator.cs - narrow orchestration responsibilities
* src/TNC.Trading.Platform.Application/Services/PlatformAuthSupervisor.cs - isolate timing and loop control
* src/TNC.Trading.Platform.Application/Services/ - add side-effect collaborators or interfaces as needed
* test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/ - add focused coordinator and supervisor tests

Discrepancy references:
* None

Success criteria:
* Optional side effects are no longer fused to the coordinator happy path
* Supervisor behavior can be tested without real delay handling

Context references:
* .copilot-tracking/research/2026-06-26/refactoring-and-testability-opportunities-research.md (Lines 41-45, 212-224) - application hotspot evidence and sequencing

Dependencies:
* Step 2.2 completion

### Step 2.4: Run the application milestone validation gate

Do not proceed to Web or API work until the application slice passes both fast and milestone validation. This gate is the first required proof that the refactor preserved runtime behavior.

Validation commands:
* dotnet test test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests --filter AuthRetryCycleTests
* dotnet test test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests
* dotnet test test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests --filter Authentication
* dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests --filter Authentication

Success criteria:
* New transition and supervisor seams are covered by focused unit tests
* The AppHost-backed authentication lanes still pass after the application-layer refactor

Context references:
* .copilot-tracking/research/2026-06-26/refactoring-and-testability-opportunities-research.md (Lines 230-240) - expensive suites remain as regression coverage

Dependencies:
* Step 2.3 completion

## Implementation Phase 3: Extract Web orchestration into presenter-style seams

<!-- parallelizable: false -->

### Step 3.1: Extract Configuration page orchestration first

Move page load, save flow, request mapping, form-state decisions, and alert composition out of Configuration.razor into plain objects or services that can be unit tested without component rendering. Keep the component focused on view binding and lifecycle delegation.

Files:
* src/TNC.Trading.Platform.Web/Components/Pages/Configuration.razor - thin the component
* src/TNC.Trading.Platform.Web/Components/ - add presenter, form service, or page model abstractions
* test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/ConfigurationTests.cs - shift tests toward extracted seams

Success criteria:
* Most Configuration behavior no longer requires broad component harness setup
* Mapping and alert decisions are directly unit testable

Context references:
* .copilot-tracking/research/2026-06-26/refactoring-and-testability-opportunities-research.md (Lines 48-55, 171-184, 212-216) - Configuration as the strongest early Web candidate

Dependencies:
* Phase 2 complete

### Step 3.2: Run the immediate Configuration validation gate

Validate the first Web seam before broadening the pattern. This is the first proof that page-level orchestration can move out of the component without breaking current Configuration behavior.

Validation commands:
* dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests --filter Configuration

Success criteria:
* Configuration-specific seam extraction passes before Home and Status work starts
* Any regressions stay tightly attributable to the Configuration slice

Dependencies:
* Step 3.1 completion

### Step 3.3: Apply the seam pattern to Home and Status

Repeat the extraction approach for redirect decisions, auth checks, refresh behavior, and view-shaping logic currently embedded in Home and Status. Use the same seam pattern to avoid introducing three competing abstractions.

Files:
* src/TNC.Trading.Platform.Web/Components/Pages/Home.razor - extract redirect and auth orchestration
* src/TNC.Trading.Platform.Web/Components/Pages/Status.razor - extract refresh and shaping orchestration
* test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/ - add focused tests for new seams

Success criteria:
* Home and Status page behavior is mostly covered by plain unit tests instead of lifecycle-heavy rendering tests
* Shared page orchestration patterns stay consistent across extracted seams

Context references:
* .copilot-tracking/research/2026-06-26/refactoring-and-testability-opportunities-research.md (Lines 51-55, 171-184) - Home and Status hotspot evidence

Dependencies:
* Step 3.2 completion

### Step 3.4: Run the Web milestone validation gate

Run the fast Web lane after each page slice and rerun the distributed authentication milestone lanes after the phase completes. Because this phase changes browser-facing orchestration directly, include the Web E2E authentication lane here as a mandatory milestone harness.

Validation commands:
* dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests --filter Configuration
* dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests
* dotnet test test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests --filter Authentication
* dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests --filter Authentication
* dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests --filter Authentication

Success criteria:
* Extracted Web seams are covered by focused unit tests
* AppHost-backed auth and page-flow lanes remain green after the Web refactor phase

Context references:
* .copilot-tracking/research/2026-06-26/refactoring-and-testability-opportunities-research.md (Lines 119-142, 230-240) - current Web and distributed-auth test costs

Dependencies:
* Step 3.3 completion

## Implementation Phase 4: Decompose API and infrastructure hotspots

<!-- parallelizable: false -->

### Step 4.1: Break PlatformEndpoints into smaller contract-tested handlers

Preserve declarative route registration, but move validation handling, exception-to-result translation, and auth-audit side effects into smaller handlers or endpoint-specific modules. The goal is direct test coverage for HTTP contract behavior without routing all checks through the full static module.

Files:
* src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs - reduce inline behavior concentration
* src/TNC.Trading.Platform.Api/Features/Platform/ - add smaller handler or module types
* test/TNC.Trading.Platform.Api/ - add narrower contract tests for result translation and conflict behavior

Success criteria:
* Endpoint result translation can be tested directly in smaller units
* Route registration remains readable and behaviorally unchanged

Context references:
* .copilot-tracking/research/2026-06-26/refactoring-and-testability-opportunities-research.md (Lines 59-62, 217-220) - endpoint hotspot evidence

Dependencies:
* Phase 3 complete

### Step 4.2: Run the immediate API validation gate

Validate the endpoint decomposition before beginning infrastructure refactors. This keeps HTTP contract regressions isolated to the API slice.

Validation commands:
* dotnet test test/TNC.Trading.Platform.Api

Success criteria:
* API contract and handler changes are proven before infrastructure edits start
* Failures remain attributable to the endpoint decomposition slice

Dependencies:
* Step 4.1 completion

### Step 4.3: Separate domain rules from persistence-heavy infrastructure services

Refactor SqlPlatformConfigurationStore, NotificationDispatcher, and OperationalRecordRetentionProcessor so parsing, rule evaluation, transport policy, and provider-specific retention logic can be tested as plain logic. Leave database interaction and transport integration in narrower boundary components.

Files:
* src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/SqlPlatformConfigurationStore.cs - separate bootstrap parsing and restart-needed rules
* src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/NotificationDispatcher.cs - separate dispatch policy from persistence and event recording
* src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/OperationalRecordRetentionProcessor.cs - separate provider-specific retention rules from execution
* test/TNC.Trading.Platform.Infrastructure/ - add narrow logic and persistence tests per extracted seam

Success criteria:
* Rules and mapping logic are testable without full EF or provider setup
* Persistence tests narrow their scope to actual storage behavior

Context references:
* .copilot-tracking/research/2026-06-26/refactoring-and-testability-opportunities-research.md (Lines 45-48, 220-224) - infrastructure hotspot evidence

Dependencies:
* Step 4.2 completion

### Step 4.4: Run the API and infrastructure milestone validation gate

Run the local contract and unit lanes for the touched API and infrastructure code, then rerun the milestone authentication suites before starting any shared harness consolidation. Run the Web E2E authentication lane here only if the API changes altered auth bootstrap contracts, browser-observable auth flows, or end-to-end startup behavior.

Validation commands:
* dotnet test test/TNC.Trading.Platform.Api
* dotnet test test/TNC.Trading.Platform.Infrastructure
* dotnet test test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests --filter Authentication
* dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests --filter Authentication
* Conditional: dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests --filter Authentication

Success criteria:
* API contract tests and infrastructure tests cover the extracted seams
* Distributed authentication behavior remains unchanged after API and infrastructure refactors

Context references:
* .copilot-tracking/research/2026-06-26/refactoring-and-testability-opportunities-research.md (Lines 230-240) - recommended validation value of the expensive suites

Dependencies:
* Step 4.3 completion

## Implementation Phase 5: Consolidate duplicated test harness infrastructure

<!-- parallelizable: false -->

### Step 5.1: Share AppHost-backed startup and readiness infrastructure

Consolidate duplicated AppHost process factories, readiness polling, and authentication fixture bootstrapping across the API integration, Web functional, and Web E2E suites. Do this only after the production seams are clearer, so the shared test abstractions align to stable usage patterns rather than current incidental complexity.

Files:
* test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/RealAppHostProcessFactory.cs - consolidation source
* test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/Authentication/RealAppHostProcessFactory.cs - consolidation source
* test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/Authentication/AppHostProcessFactory.cs - consolidation source
* test/ - add shared AppHost-backed test infrastructure where appropriate

Discrepancy references:
* DD-02

Success criteria:
* Shared startup and readiness behavior exists in one maintainable abstraction
* Diagnostics and readiness logic stay consistent across API, functional, and E2E lanes

Context references:
* .copilot-tracking/research/2026-06-26/refactoring-and-testability-opportunities-research.md (Lines 80-84, 145-169) - duplicated harness evidence and secondary priority rationale

Dependencies:
* Phase 4 complete

### Step 5.2: Run the immediate shared-AppHost-harness validation gate

Because Step 5.1 changes the proving infrastructure for the distributed-auth lanes, validate that shared AppHost-backed helpers still drive the existing suites correctly before touching component harness setup.

Validation commands:
* dotnet test test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests --filter Authentication
* dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests --filter Authentication
* dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests --filter Authentication

Success criteria:
* Shared AppHost-backed helper changes are proven in every affected distributed-auth lane before further harness edits continue
* Failures are attributable to shared startup or readiness changes, not later component-harness changes

Dependencies:
* Step 5.1 completion

### Step 5.3: Reduce duplicated Web component harness setup

After presenter-style seams land, simplify the broad PlatformComponentTestContext and related setup patterns so component tests only carry rendering-specific concerns. Shared setup should no longer compensate for domain logic trapped inside components.

Files:
* test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/PlatformComponentTestContext.cs - reduce broad shared wiring
* test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/ - update tests to use narrower setup

Success criteria:
* Component test setup reflects rendering concerns rather than orchestration compensation
* Presenter and service tests cover most non-rendering behavior

Context references:
* .copilot-tracking/research/2026-06-26/refactoring-and-testability-opportunities-research.md (Lines 76-79, 171-184) - broad component harness evidence

Dependencies:
* Step 5.2 completion

### Step 5.4: Run the shared-harness milestone validation gate

Because this phase changes the proving infrastructure itself, rerun every affected lane. Treat failures here as high-signal until proven otherwise.

Validation commands:
* dotnet test test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests --filter Authentication
* dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests --filter Authentication
* dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests --filter Authentication
* dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests

Success criteria:
* Shared harness changes do not reduce confidence or break existing distributed-auth coverage
* Web unit tests still pass with the slimmer component setup

Context references:
* .copilot-tracking/research/2026-06-26/refactoring-and-testability-opportunities-research.md (Lines 145-169, 230-240) - why shared harness work is supporting, not primary

Dependencies:
* Step 5.3 completion

## Implementation Phase 6: Final validation

<!-- parallelizable: false -->

### Step 6.1: Run the full selected validation set

Execute a final full pass across the projects touched by the refactor. Keep the milestone authentication suites and the Web E2E authentication lane in the run even if earlier gates were green, because they are the broadest proof that the application still starts and authenticates correctly after the complete series of changes.

Validation commands:
* dotnet test test/TNC.Trading.Platform.Application
* dotnet test test/TNC.Trading.Platform.Web
* dotnet test test/TNC.Trading.Platform.Api
* dotnet test test/TNC.Trading.Platform.Infrastructure
* dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests --filter Authentication

Success criteria:
* All touched test projects pass in a final aggregate run
* The repeated harness cadence, including the explicit Web E2E policy, detects no late integration regressions

Context references:
* .copilot-tracking/research/2026-06-26/refactoring-and-testability-opportunities-research.md (Lines 230-240) - final regression value of combined suites

Dependencies:
* Phase 5 complete

### Step 6.2: Fix minor validation issues

Address straightforward lint, compile, or test issues discovered in the final validation pass when they remain inside the chosen implementation slices.

Dependencies:
* Step 6.1 completion

### Step 6.3: Report blocking issues

If final validation reveals unstable distributed-auth lanes, structural design disagreements, or large cross-cutting regressions, stop and produce a focused follow-on plan instead of widening this refactor indefinitely.

Dependencies:
* Step 6.2 completion

## Dependencies

* .NET SDK and repository test prerequisites
* AppHost-capable local environment for milestone authentication harnesses
* Stable access to any browser automation prerequisites used by the Web E2E lane

## Success Criteria

* The refactor path is sequenced from application seams outward
* Validation gates run throughout the refactor, not only at the end
* Milestone AppHost-backed harnesses are preserved as repeated regression checks