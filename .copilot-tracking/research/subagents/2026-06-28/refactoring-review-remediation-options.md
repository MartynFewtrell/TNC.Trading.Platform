---
title: Refactoring Review Remediation Options
description: Research-backed follow-up planning options for the remaining open issues after the AppHost refactor test-mitigation evidence pass
ms.date: 2026-06-28
ms.topic: reference
---

## Research scope

* Primary review source: docs/005-refactor-app-host/002-project-test-review-report.md
* Freshness check: docs/005-refactor-app-host/004-quality-evidence-after-test-mitigation.md
* Goal: identify which issues are still open after the mitigation note and propose the most effective follow-up planning strategy

## Status

* Complete

## Findings closed by the mitigation note

* `F1` is closed. The mitigation note records a new AppHost unit suite with `94.69%` line coverage and `100.00%` branch coverage, and a new AppHost mutation score of `77.86%`: docs/005-refactor-app-host/004-quality-evidence-after-test-mitigation.md:13, docs/005-refactor-app-host/004-quality-evidence-after-test-mitigation.md:31
* `F4` is closed. The repository now contains the previously missing Web component tests named in the baseline concern: test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/MainLayoutTests.cs, test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/HomeTests.cs, test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/StatusTests.cs, test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/ConfigurationTests.cs. The wiki now explicitly claims those tests as delivered coverage: docs/wiki/testing-and-quality.md:97
* `F6` is largely closed for AppHost provider branching. The mitigation note says the new AppHost suite closes the previous direct-evidence gap for findings `F1` and `F6`: docs/005-refactor-app-host/004-quality-evidence-after-test-mitigation.md:39
* `F2` is substantially mitigated. The wiki now describes only one retained functional sign-out smoke, one retained insufficient-role smoke, one retained CSRF negative, and one retained real Keycloak browser smoke, which is materially narrower than the baseline repeated-startup concern: docs/wiki/testing-and-quality.md:101, docs/wiki/testing-and-quality.md:103, docs/wiki/testing-and-quality.md:104, docs/wiki/testing-and-quality.md:105
* `F3` is only partially closed. Reflection-heavy API testing was removed and the wiki records the typed replacement seam, but the mitigation note still says API unit coverage remains the weakest lower-level area and that many API mutants remain uncovered outside the hardened seams: docs/wiki/testing-and-quality.md:85, docs/005-refactor-app-host/004-quality-evidence-after-test-mitigation.md:25, docs/005-refactor-app-host/004-quality-evidence-after-test-mitigation.md:40

## Open issues after mitigation

### Issue 1: API fast-feedback coverage remains the weakest low-cost regression net

Evidence:

* The mitigation note still calls API unit coverage the weakest lower-level area and explicitly recommends continued cheap API-unit expansion ahead of more distributed tests: docs/005-refactor-app-host/004-quality-evidence-after-test-mitigation.md:25, docs/005-refactor-app-host/004-quality-evidence-after-test-mitigation.md:62
* The API unit suite is still small in breadth. The current test inventory is limited to five unit-test files focused on status mapping, validation, auth-audit resolution, auth registration, and a small endpoint-handler surface: test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/GetPlatformStatusMappingTests.cs, test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/UpdatePlatformConfigurationValidatorTests.cs, test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/PlatformAuthAuditEventResolverTests.cs, test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/PlatformApiAuthenticationServiceCollectionExtensionsTests.cs, test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/PlatformEndpointHandlerTests.cs
* The endpoint-handler unit tests currently cover only three negative-path cases: test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/PlatformEndpointHandlerTests.cs:10, test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/PlatformEndpointHandlerTests.cs:24, test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/PlatformEndpointHandlerTests.cs:35

Practical remediation tasks:

* Expand API unit tests around endpoint-handler result mapping and problem-shape behavior for success and additional failure branches, especially protected configuration, manual retry, auth-audit recording, and status or event response projection.
* Add unit tests that inventory protected endpoint registration or route-policy intent at the host seam so route additions fail cheaply before integration coverage is updated.
* Add cheap auth-configuration resolver tests for valid Keycloak and Entra happy-path cases, not only missing-configuration failures.

Likely affected areas:

* src/TNC.Trading.Platform.Api/Features/Platform/
* src/TNC.Trading.Platform.Api/Features/UpdatePlatformConfiguration/
* src/TNC.Trading.Platform.Api/Authentication/
* test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/
* docs/wiki/testing-and-quality.md

Acceptance evidence:

* New API unit tests materially extend beyond the current five-file, mostly negative-path surface.
* Coverlet and Stryker reruns show measurable improvement for test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests without adding new distributed runtime cases.
* The wiki remains accurate about what the API unit suite covers.

### Issue 2: Application orchestration mutation resistance is still flat in the highest-complexity state machine

Evidence:

* The mitigation note says Application mutation resistance did not improve and identifies Application hardening as a highest-value remaining follow-up area: docs/005-refactor-app-host/004-quality-evidence-after-test-mitigation.md:44, docs/005-refactor-app-host/004-quality-evidence-after-test-mitigation.md:56
* The same note calls out Application hotspots in PlatformStateCoordinator, PlatformRetryCycle, and PlatformRuntimeState: docs/005-refactor-app-host/004-quality-evidence-after-test-mitigation.md:49
* PlatformStateCoordinator still contains a large number of branch-heavy transitions and side-effect paths, including manual retry, blocked live handling, degraded transitions, scheduled retry waiting, and state context application: src/TNC.Trading.Platform.Application/Services/PlatformStateCoordinator.cs:28, src/TNC.Trading.Platform.Application/Services/PlatformStateCoordinator.cs:138, src/TNC.Trading.Platform.Application/Services/PlatformStateCoordinator.cs:182, src/TNC.Trading.Platform.Application/Services/PlatformStateCoordinator.cs:267, src/TNC.Trading.Platform.Application/Services/PlatformStateCoordinator.cs:307, src/TNC.Trading.Platform.Application/Services/PlatformStateCoordinator.cs:348, src/TNC.Trading.Platform.Application/Services/PlatformStateCoordinator.cs:448, src/TNC.Trading.Platform.Application/Services/PlatformStateCoordinator.cs:561
* The extracted transition engine is small and testable, but it currently covers only part of the coordinator decision space: src/TNC.Trading.Platform.Application/Services/PlatformStateTransitionEngine.cs:7

Practical remediation tasks:

* Add focused unit tests around coordinator transition outcomes that are currently mutation-prone but not yet expressed as direct assertions, especially recovery versus degraded transitions, retry scheduling boundaries, blocked-live behavior, and session-expiry side effects.
* Expand transition-engine tests only where a rule can be isolated cleanly from EF-backed orchestration.
* Add assertions around emitted operational events and notification dispatch sequencing for the critical coordinator paths that survived mutation.

Likely affected areas:

* src/TNC.Trading.Platform.Application/Services/PlatformStateCoordinator.cs
* src/TNC.Trading.Platform.Application/Services/PlatformStateTransitionEngine.cs
* src/TNC.Trading.Platform.Application/Configuration/PlatformRetryCycle.cs
* src/TNC.Trading.Platform.Application/Configuration/PlatformRuntimeState.cs
* test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/AuthRetryCycleTests.cs
* test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/

Acceptance evidence:

* New Application unit tests directly target coordinator branches named in the mitigation note hotspot list.
* Stryker reruns improve mutation resistance for test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests.
* New tests assert observable state, events, and notifications rather than internal implementation detail.

### Issue 3: Infrastructure persistence and retention branches still have under-asserted mutation hotspots

Evidence:

* The mitigation note says Infrastructure mutation resistance improved only slightly and remains concentrated in persistence and configuration-heavy code: docs/005-refactor-app-host/004-quality-evidence-after-test-mitigation.md:41
* It names SqlPlatformConfigurationStore, PlatformDbContext, and OperationalRecordRetentionProcessor as the current Infrastructure hotspots: docs/005-refactor-app-host/004-quality-evidence-after-test-mitigation.md:50
* SqlPlatformConfigurationStore contains multiple branch points around startup configuration seeding, restart-required activation, audit shape, runtime projection, and secret-presence mapping: src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/SqlPlatformConfigurationStore.cs:12, src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/SqlPlatformConfigurationStore.cs:29, src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/SqlPlatformConfigurationStore.cs:41, src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/SqlPlatformConfigurationStore.cs:100, src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/SqlPlatformConfigurationStore.cs:145
* OperationalRecordRetentionProcessor has provider-specific delete paths and a zero-deletion early return that are easy mutation survivors if not asserted precisely: src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/OperationalRecordRetentionProcessor.cs:15, src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/OperationalRecordRetentionProcessor.cs:20, src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/OperationalRecordRetentionProcessor.cs:38, src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/OperationalRecordRetentionProcessor.cs:67

Practical remediation tasks:

* Add Infrastructure unit tests for restart-required false paths, runtime projection when restart is pending versus cleared, and audit-detail edge cases in SqlPlatformConfigurationStore.
* Add retention-processor assertions for zero-deletion logging behavior, provider-branch behavior, and retained snapshot versus non-retained snapshot deletion boundaries.
* Use mutation results to target exact unasserted branches rather than broad coverage increases.

Likely affected areas:

* src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/SqlPlatformConfigurationStore.cs
* src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/OperationalRecordRetentionProcessor.cs
* src/TNC.Trading.Platform.Infrastructure/Persistence/
* test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/SqlPlatformConfigurationStoreTests.cs
* test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/OperationalRecordRetentionProcessorTests.cs
* test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/

Acceptance evidence:

* New Infrastructure tests cover the specific branch points named above.
* Stryker reruns for test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests show improvement in the identified hotspots.
* No documentation drift is introduced around persistence or retention behavior.

### Issue 4: Web mutation evidence is still blocked and prevents closure of the quality story

Evidence:

* The mitigation note explicitly says Web mutation evidence is still blocked because Stryker mutating src/TNC.Trading.Platform.Web/Program.cs yields `CS0246` for `App` and exits before report generation: docs/005-refactor-app-host/004-quality-evidence-after-test-mitigation.md:45
* Program.cs maps the top-level Blazor root component directly with `app.MapRazorComponents<App>()`: src/TNC.Trading.Platform.Web/Program.cs:73
* The routed component tree is defined in the Razor component graph rooted via the Router assembly reference: src/TNC.Trading.Platform.Web/Components/Routes.razor:2

Practical remediation tasks:

* Reproduce and isolate the Stryker-versus-Blazor compile failure around Program.cs and the generated App component.
* Decide whether the fix belongs in test tooling configuration, Stryker file exclusion, or a minimal production-code structural seam that preserves behavior while avoiding mutated top-level-program breakage.
* Document the chosen limitation or resolution explicitly in the testing-and-quality wiki and future evidence notes.

Likely affected areas:

* src/TNC.Trading.Platform.Web/Program.cs
* src/TNC.Trading.Platform.Web/Components/Routes.razor
* test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/
* Stryker configuration files if introduced later in this repository
* docs/wiki/testing-and-quality.md
* docs/005-refactor-app-host/ follow-up evidence notes

Acceptance evidence:

* A focused reproduction note identifies whether the blocker is tooling-only or code-shape-sensitive.
* Either a successful Web Stryker report is produced, or an explicit repository-standard exclusion/limitation is documented with rationale.
* The follow-up evidence note can state the Web mutation status unambiguously rather than as a blocker.

## Follow-up strategy options

### Strategy A: Narrow quality-evidence closure plan

Goal:

* Close only the issues that prevent the `005-refactor-app-host` quality story from being considered complete.

Scope:

* Solve the Web Stryker blocker.
* Add a small amount of API unit coverage in the weakest fast-feedback seams.
* Refresh the evidence note and wiki only where that work changes the status.

Advantages:

* Fastest path to a clean closure artifact for work package `005`.
* Lowest risk of accidental architectural scope creep.
* Keeps distributed-test costs stable by following the mitigation note guidance to prefer cheap API-unit expansion first.

Disadvantages:

* Leaves Application and Infrastructure mutation hotspots largely untouched even though the mitigation note identifies them as high-value remaining work.
* Produces a complete closure story for `005` while still leaving deeper orchestration risk in the broader platform.

Best fit when:

* The main objective is to finish the quality-evidence and review-closure thread for the AppHost refactor with minimal extra delivery.

### Strategy B: Broader architectural hardening plan

Goal:

* Use the residual review findings as the entry point for a broader round of low-cost test hardening across API, Application, Infrastructure, and Web mutation tooling.

Scope:

* Solve the Web Stryker blocker.
* Expand API unit coverage materially.
* Harden Application coordinator mutation hotspots.
* Harden Infrastructure persistence and retention hotspots.
* Publish a broader follow-up evidence note rather than only closing `005`.

Advantages:

* Best aligns with the mitigation note statement that the highest remaining value lies in Application orchestration and still-low-signal API areas: docs/005-refactor-app-host/004-quality-evidence-after-test-mitigation.md:56
* Reduces real residual risk instead of only closing the documentation and evidence gap.
* Improves the cheapest regression nets in the most branch-heavy services.

Disadvantages:

* Bigger scope and longer cycle time.
* Higher chance of opening adjacent changes beyond the original `005` work-package boundary.
* More dependencies across Application, Infrastructure, API, and Web tooling.

Best fit when:

* The team wants to trade a slower closeout for materially stronger mutation resistance and lower regression risk in the stateful core of the platform.

## Recommended strategy

Recommend Strategy A with a staged bridge into selected Strategy B items.

Rationale:

* The explicit still-open blocker for the `005` workstream is the missing Web mutation evidence: docs/005-refactor-app-host/004-quality-evidence-after-test-mitigation.md:45
* The same note explicitly frames Application and Infrastructure hotspots as future hardening candidates rather than required closure items for this work package: docs/005-refactor-app-host/004-quality-evidence-after-test-mitigation.md:58
* API unit coverage is the one non-tooling gap that still weakens the `005` quality story directly, because the mitigation note continues to single it out as the weakest low-cost suite and recommends growing it before more expensive runtime work: docs/005-refactor-app-host/004-quality-evidence-after-test-mitigation.md:25, docs/005-refactor-app-host/004-quality-evidence-after-test-mitigation.md:62
* A narrow closure plan can finish the `005` evidence thread quickly while sequencing Application and Infrastructure hardening as a separate, better-bounded follow-on package tied to their own hotspot files.

## Recommended sequencing

1. Isolate and resolve or document the Web Stryker blocker around src/TNC.Trading.Platform.Web/Program.cs and the generated App component path.
2. Expand API unit coverage in the cheapest remaining seams, prioritizing endpoint-handler branch coverage and happy-path auth-configuration resolution.
3. Rerun Web and API quality evidence so the `005` follow-up note can be closed without ambiguity.
4. Open a separate hardening plan for Application and Infrastructure mutation hotspots if the repository wants broader regression-net improvement beyond `005` closure.

## Dependencies

* Web mutation closure depends on understanding whether the Program.cs and Razor root-component issue can be fixed through tooling configuration or requires a production-code seam.
* API unit expansion depends on preserving the typed internal seams already introduced during mitigation, especially the auth-audit resolver seam documented in the wiki: docs/wiki/testing-and-quality.md:85
* Any broader Application or Infrastructure hardening depends on using the Stryker hotspot list as targeting input rather than reopening the original AppHost refactor scope.

## Risks

* Treating the old baseline findings as still open would waste effort on already closed AppHost and Web component-coverage work.
* Solving the Web Stryker blocker may expose a tooling limitation that cannot be fully eliminated without a documented exclusion strategy.
* Expanding API unit tests too broadly can drift into integration-behavior duplication if endpoint registration and handler seams are not kept narrow.
* Pulling Application and Infrastructure hardening into the same plan risks turning a quality-evidence closure task into a broad regression-program without explicit sponsorship.

## Top planning tasks

* Create a focused investigation task for the Web Stryker `Program.cs` and `App` compile failure and define the acceptable closure condition: successful report or explicit repository-standard exclusion.
* Create an API fast-feedback task that adds endpoint-handler and auth-configuration happy-path tests, then reruns Coverlet and Stryker for test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests.
* Prepare a short closure evidence update for docs/005-refactor-app-host once the Web mutation status and API unit improvements are verified.
* Defer Application and Infrastructure mutation hardening into a separate follow-up plan anchored on src/TNC.Trading.Platform.Application/Services/PlatformStateCoordinator.cs, src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/SqlPlatformConfigurationStore.cs, and src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/OperationalRecordRetentionProcessor.cs.

## Clarifying questions

* None identified from repository evidence alone. The remaining uncertainty is execution-level rather than documentary: whether the Web Stryker blocker is best resolved by tooling configuration or by a small code-structure seam.