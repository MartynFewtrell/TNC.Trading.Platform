---
description: "Implementation plan for prioritized refactoring work that improves code quality and testability while preserving behavior through recurring test-harness execution"
applyTo: '.copilot-tracking/changes/2026-06-26/refactoring-and-testability-opportunities-changes.md'
---
<!-- markdownlint-disable-file -->
# Implementation Plan: Refactoring and Testability Opportunities

## Overview

Execute the recommended backend-first refactoring path in narrow slices, with mandatory recurring unit, integration, functional, and AppHost-backed harness runs so each slice proves the application still works before the next one begins.

## Objectives

### User Requirements

* Review the repository for code quality refactoring opportunities. Source: .copilot-tracking/research/2026-06-26/refactoring-and-testability-opportunities-research.md.
* Identify places where refactoring would improve or unlock testing. Source: .copilot-tracking/research/2026-06-26/refactoring-and-testability-opportunities-research.md.
* Recommend a prioritized approach grounded in repository evidence. Source: .copilot-tracking/research/2026-06-26/refactoring-and-testability-opportunities-research.md.
* Ensure the plan enforces regular runs of the test harnesses during the refactor to ensure that the application is not broken. Source: user request on 2026-06-26.

### Derived Objectives

* Start with PlatformStateCoordinator and PlatformAuthSupervisor because research identifies them as the highest-leverage complexity and testability hotspot. Derived from: .copilot-tracking/research/2026-06-26/refactoring-and-testability-opportunities-research.md (Lines 41-45, 186-224).
* Treat test execution as a phase gate, not an end-of-project activity, so regressions are detected close to the slice that introduced them. Derived from: the repository's reliance on broad AppHost-backed and component harnesses described in .copilot-tracking/research/2026-06-26/refactoring-and-testability-opportunities-research.md (Lines 119-142, 218-240).
* Preserve the expensive distributed authentication and browser-backed suites as milestone regression coverage while shifting new behavior checks into cheaper unit-level seams. Derived from: .copilot-tracking/research/2026-06-26/refactoring-and-testability-opportunities-research.md (Lines 132-142, 230-240).
* Consolidate duplicated test harness infrastructure only after production seams improve enough to make the new shared abstractions clear. Derived from: .copilot-tracking/research/2026-06-26/refactoring-and-testability-opportunities-research.md (Lines 145-169, 186-224).

## Context Summary

### Project Files

* src/TNC.Trading.Platform.Application/Services/PlatformStateCoordinator.cs - primary orchestration hotspot and first refactoring target.
* src/TNC.Trading.Platform.Application/Services/PlatformAuthSupervisor.cs - supervision loop that needs an isolated scheduler or tick seam.
* src/TNC.Trading.Platform.Web/Components/Pages/Configuration.razor - first Web orchestration extraction candidate.
* src/TNC.Trading.Platform.Web/Components/Pages/Home.razor - auth and redirect orchestration candidate.
* src/TNC.Trading.Platform.Web/Components/Pages/Status.razor - status page orchestration candidate.
* src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs - large endpoint module needing decomposition.
* src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/SqlPlatformConfigurationStore.cs - persistence-plus-rules hotspot.
* src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/NotificationDispatcher.cs - notification dispatch and persistence hotspot.
* src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/OperationalRecordRetentionProcessor.cs - provider-conditional retention hotspot.
* test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/AuthRetryCycleTests.cs - representative coordinator behavior harness.
* test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/ConfigurationTests.cs - representative Web behavior harness.
* test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/RealAuthenticationIntegrationTestFixture.cs - AppHost-backed API regression harness.
* test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/Authentication/RealAuthenticationFunctionalTestFixture.cs - AppHost-backed Web regression harness.

### References

* .copilot-tracking/research/2026-06-26/refactoring-and-testability-opportunities-research.md - primary research, prioritization evidence, and recommended refactoring sequence.
* .copilot-tracking/details/2026-06-26/refactoring-and-testability-opportunities-details.md - implementation step details and validation cadence.
* /memories/repo/aspire-test-notes.md - repository memory note about Aspire-oriented test execution pitfalls.

### Standards References

* c:\Users\martynfewtrell\.vscode\extensions\ise-hve-essentials.hve-core-3.2.2\.github\instructions\hve-core\markdown.instructions.md - markdown authoring requirements.
* c:\Users\martynfewtrell\.vscode\extensions\ise-hve-essentials.hve-core-3.2.2\.github\instructions\hve-core\writing-style.instructions.md - writing style requirements.
* c:\Users\martynfewtrell\.vscode\extensions\ise-hve-essentials.hve-core-3.2.2\.github\instructions\hve-core\prompt-builder.instructions.md - requirements for .instructions.md plan artifacts.

## Implementation Checklist

### [x] Implementation Phase 1: Baseline current behavior and establish refactor safety rails

<!-- parallelizable: false -->

* [x] Step 1.1: Capture the current hotspot inventory and map each hotspot to its owning test harnesses
  * Details: .copilot-tracking/details/2026-06-26/refactoring-and-testability-opportunities-details.md (Lines 23-41)
* [x] Step 1.2: Define the recurring validation cadence and lightweight versus milestone harness lanes before changing production code
  * Details: .copilot-tracking/details/2026-06-26/refactoring-and-testability-opportunities-details.md (Lines 43-67)
* [x] Step 1.3: Run the baseline harness set and record known-good behavior before the first refactor slice
  * Details: .copilot-tracking/details/2026-06-26/refactoring-and-testability-opportunities-details.md (Lines 69-92)

### [x] Implementation Phase 2: Extract application-layer domain seams first

<!-- parallelizable: false -->

* [x] Step 2.1: Split pure transition decision logic out of PlatformStateCoordinator while preserving its orchestration shell
  * Details: .copilot-tracking/details/2026-06-26/refactoring-and-testability-opportunities-details.md (Lines 102-125)
* [x] Step 2.2: Run the immediate coordinator validation gate before starting the next application slice
  * Details: .copilot-tracking/details/2026-06-26/refactoring-and-testability-opportunities-details.md (Lines 127-143)
* [x] Step 2.3: Move secondary side effects and supervisor tick control behind dedicated collaborators
  * Details: .copilot-tracking/details/2026-06-26/refactoring-and-testability-opportunities-details.md (Lines 145-168)
* [x] Step 2.4: Run the application milestone validation gate before any adjacent boundary slice begins
  * Details: .copilot-tracking/details/2026-06-26/refactoring-and-testability-opportunities-details.md (Lines 170-193)

### [x] Implementation Phase 3: Extract Web orchestration into presenter-style seams

<!-- parallelizable: false -->

* [x] Step 3.1: Extract Configuration page load, save, mapping, and alert decisions into plain services or presenters
  * Details: .copilot-tracking/details/2026-06-26/refactoring-and-testability-opportunities-details.md (Lines 199-219)
* [x] Step 3.2: Run the immediate Configuration validation gate before expanding to other pages
  * Details: .copilot-tracking/details/2026-06-26/refactoring-and-testability-opportunities-details.md (Lines 221-236)
* [x] Step 3.3: Apply the same pattern to Home and Status to shrink component-only logic
  * Details: .copilot-tracking/details/2026-06-26/refactoring-and-testability-opportunities-details.md (Lines 238-255)
* [x] Step 3.4: Run the Web milestone validation gate, including the milestone AppHost-backed harnesses
  * Details: .copilot-tracking/details/2026-06-26/refactoring-and-testability-opportunities-details.md (Lines 257-280)

### [x] Implementation Phase 4: Decompose API and infrastructure hotspots

<!-- parallelizable: false -->

* [x] Step 4.1: Break PlatformEndpoints into smaller handlers or endpoint modules with directly testable request-to-result translation
  * Details: .copilot-tracking/details/2026-06-26/refactoring-and-testability-opportunities-details.md (Lines 286-303)
* [x] Step 4.2: Run the immediate API validation gate before touching infrastructure services
  * Details: .copilot-tracking/details/2026-06-26/refactoring-and-testability-opportunities-details.md (Lines 305-320)
* [x] Step 4.3: Separate domain rules from persistence concerns in the configuration, notification, and retention infrastructure services
  * Details: .copilot-tracking/details/2026-06-26/refactoring-and-testability-opportunities-details.md (Lines 322-343)
* [x] Step 4.4: Run the API and infrastructure milestone validation gate before shared harness consolidation
  * Details: .copilot-tracking/details/2026-06-26/refactoring-and-testability-opportunities-details.md (Lines 345-368)

### [x] Implementation Phase 5: Consolidate duplicated test harness infrastructure

<!-- parallelizable: false -->

* [x] Step 5.1: Share AppHost process startup, readiness probing, and repeated fixture wiring across API, functional, and E2E suites
  * Details: .copilot-tracking/details/2026-06-26/refactoring-and-testability-opportunities-details.md (Lines 374-392)
* [x] Step 5.2: Run the immediate shared-AppHost-harness validation gate before changing component test setup
  * Details: .copilot-tracking/details/2026-06-26/refactoring-and-testability-opportunities-details.md (Lines 394-410)
* [x] Step 5.3: Reduce duplicated Web component test bootstrapping only after presenter seams are established
  * Details: .copilot-tracking/details/2026-06-26/refactoring-and-testability-opportunities-details.md (Lines 412-427)
* [x] Step 5.4: Run the shared-harness milestone validation gate across every affected test lane
  * Details: .copilot-tracking/details/2026-06-26/refactoring-and-testability-opportunities-details.md (Lines 429-453)

### [x] Implementation Phase 6: Final validation

<!-- parallelizable: false -->

* [x] Step 6.1: Run the full project validation set after all refactor slices complete
  * Details: .copilot-tracking/details/2026-06-26/refactoring-and-testability-opportunities-details.md (Lines 459-474)
* [x] Step 6.2: Fix minor validation issues that stay inside the selected slices
  * Details: .copilot-tracking/details/2026-06-26/refactoring-and-testability-opportunities-details.md (Lines 476-479)
* [x] Step 6.3: Report blockers that require new research or a follow-on plan
  * Details: .copilot-tracking/details/2026-06-26/refactoring-and-testability-opportunities-details.md (Lines 481-485)

## Planning Log

See .copilot-tracking/plans/logs/2026-06-26/refactoring-and-testability-opportunities-log.md for discrepancy tracking, implementation paths considered, and suggested follow-on work.

## Dependencies

* Repository write access across src/ and test/ projects involved in the selected slices
* .NET test execution for unit, integration, functional, and E2E harnesses
* AppHost-capable local environment for milestone and final distributed-auth validation
* Access to existing test fixtures, fake collaborators, and supporting infrastructure projects

## Success Criteria

* The implementation starts with application-layer seam extraction and follows the backend-first prioritization from research. Traces to: .copilot-tracking/research/2026-06-26/refactoring-and-testability-opportunities-research.md (Lines 186-224).
* Every production refactor slice defines an immediate validation gate that runs the relevant harnesses before the next slice proceeds. Traces to: user requirement for regular harness runs during the refactor.
* Milestone phases rerun the AppHost-backed API and Web authentication harnesses, and rerun the Web E2E authentication lane after Web-boundary changes, shared-harness changes, and final validation, so expensive end-to-end confidence is preserved while lower-cost seams expand. Traces to: .copilot-tracking/research/2026-06-26/refactoring-and-testability-opportunities-research.md (Lines 132-142, 230-240).
* Shared test harness consolidation is sequenced after production seams improve, rather than as the first move. Traces to: .copilot-tracking/research/2026-06-26/refactoring-and-testability-opportunities-research.md (Lines 145-169, 186-224).