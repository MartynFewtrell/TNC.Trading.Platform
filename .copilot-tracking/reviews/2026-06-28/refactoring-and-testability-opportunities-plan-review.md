<!-- markdownlint-disable-file -->
---
title: Refactoring and Testability Opportunities Review
description: Review log for implementation validation against the refactoring and testability opportunities plan
author: GitHub Copilot
ms.date: 2026-06-28
ms.topic: troubleshooting
keywords:
  - implementation review
  - refactoring
  - testability
estimated_reading_time: 8
---

## Metadata

* Review date: 2026-06-28
* Related plan: .copilot-tracking/plans/2026-06-26/refactoring-and-testability-opportunities-plan.instructions.md
* Changes log: .copilot-tracking/changes/2026-06-26/refactoring-and-testability-opportunities-changes.md
* Research document: .copilot-tracking/research/2026-06-26/refactoring-and-testability-opportunities-research.md
* Overall status: Needs Rework

## Summary

* Validation status: Complete
* Severity counts:
  * Critical: 0
  * Major: 5
  * Minor: 4

The implementation is real and substantial, but the completion claims in the plan and changes log overstate what the evidence currently supports. The dominant issue is not missing code across every phase. It is that several phases are marked complete even though their own acceptance criteria still require either stronger evidence, narrower wording, or follow-up remediation of the accepted residual Web unit failure.

## RPI Validation

### Phase 1

* Status: Partial
* Major finding: Step 1.1 required every first-wave hotspot to have both a fast lane and a milestone lane assigned, but the planning log still records missing direct fast lanes for `PlatformAuthSupervisor` and `PlatformEndpoints`, and no dedicated milestone lane for `OperationalRecordRetentionProcessor`.
* Evidence:
  * .copilot-tracking/details/2026-06-26/refactoring-and-testability-opportunities-details.md requires both fast and milestone lanes for each hotspot in Step 1.1.
  * .copilot-tracking/plans/logs/2026-06-26/refactoring-and-testability-opportunities-log.md records those missing lanes in the Phase 1 hotspot table.

### Phase 2

* Status: Partial
* Major finding: Step 2.1 is only partially implemented relative to the plan. `PlatformStateTransitionEngine` extracts top-level tick decisions, but substantial transition-state mutation and declared side-effect logic remain inside `PlatformStateCoordinator`.
* Evidence:
  * `src/TNC.Trading.Platform.Application/Services/PlatformStateTransitionEngine.cs` contains a narrow decision seam.
  * `src/TNC.Trading.Platform.Application/Services/PlatformStateCoordinator.cs` still contains the transition mutation and side-effect-heavy flows for active, degraded, blocked, expired, and retry paths.
* Supporting note: Step 2.3 is well supported. The side-effect collaborators and supervisor timing seam exist, and the focused supervisor tests pass.

### Phase 3

* Status: Partial
* Major finding: The Web presenter extraction is implemented, but Step 3.4 cannot be treated as fully green because the required full Web unit lane still contains the known baseline `PlatformApiClientTests.GetStatusAsync_ShouldReturnParsedStatus_WhenApiReturnsPayload` failure.
* Evidence:
  * The presenter seams exist for Configuration, Home, and Status, with matching focused Web tests.
  * The planning log records the unchanged baseline Web unit failure during the Phase 3 milestone gate.

### Phase 4

* Status: Partial
* Major finding: Step 4.4 requires the API authentication integration lane and Web authentication functional lane to be rerun at the milestone gate, but the recorded Phase 4 evidence only shows the focused infrastructure lane and an intentional Web E2E skip.
* Evidence:
  * .copilot-tracking/details/2026-06-26/refactoring-and-testability-opportunities-details.md defines those required milestone reruns.
  * .copilot-tracking/plans/logs/2026-06-26/refactoring-and-testability-opportunities-log.md does not record them in Phase 4.
* Minor finding: The extracted restart-policy seam does not have the same level of direct focused unit coverage as the other newly extracted infrastructure rule seams.

### Phase 5

* Status: Partial
* Major finding: Step 5.4 requires the milestone gate to validate the slimmer Web harness, but the full Web unit lane still ends with the same single failing `PlatformApiClientTests` case. The phase is implemented, but its success criteria are not fully satisfied as written.
* Evidence:
  * Shared AppHost helper consolidation is present in `test/Shared/Authentication` and the suite-specific wrappers.
  * The planning log records `88 passed, 1 failed` for the full Web unit lane in the Phase 5 milestone gate.
* Minor finding: The changes log does not list the shared AppHost factory and wrapper files in its file-level modifications section, which weakens traceability for this phase.

### Phase 6

* Status: Partial
* Major finding: Final validation still includes one failing selected lane, the Web unit project, so the Phase 6 completion claim does not match its own final-validation success criteria.
* Evidence:
  * The planning log records `88 passed, 1 failed` for the final Web unit run.
  * The details artifact requires the selected project validation set to pass before Phase 6 completes.
* Minor finding: The plan's folder-form `dotnet test` commands are not directly executable from the repository root and had to be replaced with concrete `.csproj` paths during execution.

## Implementation Quality

* Status: Partial assessment only
* The implementation-quality subagent could not complete a defensible repository-wide quality review in its environment, so no additional severity-graded code-quality findings were accepted from that run.
* Direct repository checks in this review did confirm two things:
  * The narrow supervisor seam is covered by passing focused tests.
  * The residual Web unit failure still reproduces today with the same null assertion shape in `PlatformApiClientTests.GetStatusAsync_ShouldReturnParsedStatus_WhenApiReturnsPayload`.

## Validation Commands

* `runTests` on `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/PlatformApiClientTests.cs`: 10 passed, 1 failed.
  * Failure: `PlatformApiClientTests.GetStatusAsync_ShouldReturnParsedStatus_WhenApiReturnsPayload`
  * Assertion: `Assert.NotNull() Failure: Value is null`
* `runTests` on `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/PlatformAuthSupervisorTests.cs`: 2 passed, 0 failed.
* `get_errors` across `src/` and `test/`: no diagnostics reported.

## Missing Work and Deviations

* Either assign or explicitly waive the missing Phase 1 proving lanes for `PlatformAuthSupervisor`, `PlatformEndpoints`, and `OperationalRecordRetentionProcessor`.
* Either deepen Phase 2 seam extraction or narrow the Step 2.1 completion claim so it matches the implemented extraction boundary.
* Reconcile all phase-complete wording with the accepted residual Web unit failure. The current artifacts often treat an unchanged baseline failure as both acceptable noise and full success, which is internally inconsistent.
* Record the missing Phase 4 milestone auth reruns if they happened. If they did not happen, Phase 4 should remain partial.
* Improve Phase 5 traceability by listing the shared AppHost files in the changes log.
* Normalize future plan commands to concrete executable project paths instead of folder-form `dotnet test` commands.

## Follow-Up Recommendations

### Deferred from scope

* Decide whether the baseline `PlatformApiClientTests.GetStatusAsync_ShouldReturnParsedStatus_WhenApiReturnsPayload` failure is an approved exception or must be fixed before claiming plan completion.
* Add direct focused coverage for `PlatformConfigurationRestartPolicy` so the extracted infrastructure seams have consistent test depth.

### Discovered during review

* Align the plan, planning log, and changes log so unchanged baseline failures result in either `Partial` phase status or explicitly revised success criteria.
* If the team intends these artifacts to serve as audit evidence, update the changes log to include the shared AppHost harness files touched in Phase 5.
* Consider a short follow-on plan to close the residual Web unit failure and then rerun the final validation set under the original all-green success criterion.

## Reviewer Notes

The repository appears to contain the planned refactoring work. The review outcome is `Needs Rework` because the artifact trail does not justify the current all-phases-complete story. The cleanest resolution path is to either tighten the claims to match the accepted residual failure and missing gate evidence, or finish the remaining validation and residual remediation work so the original success criteria are actually true.