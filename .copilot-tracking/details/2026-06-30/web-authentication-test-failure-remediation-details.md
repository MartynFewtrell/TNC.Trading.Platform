---
title: Web Authentication Test Failure Remediation Details
description: Step-by-step implementation details for resolving the shared AppHost-backed startup failure affecting the Web functional and E2E authentication suites.
author: GitHub Copilot
ms.date: 2026-06-30
ms.topic: how-to
keywords:
  - aspire
  - keycloak
  - playwright
  - functional tests
  - e2e tests
estimated_reading_time: 8
---
<!-- markdownlint-disable-file -->

## Context Reference

Sources: .copilot-tracking/research/2026-06-30/web-test-failures-research.md, .copilot-tracking/research/subagents/2026-06-30/functional-tests-failure-research.md, .copilot-tracking/research/subagents/2026-06-30/e2e-tests-failure-research.md, test/Shared/Authentication/AppHostProcessHandle.cs, test/Shared/Authentication/RealAppHostProcessFactory.cs, src/TNC.Trading.Platform.AppHost/AppHostProjectRegistration.cs, src/TNC.Trading.Platform.AppHost/AppHostEnvironmentWiring.cs

## Implementation Phase 1: Capture discriminating startup evidence in the shared harness

<!-- parallelizable: false -->

### Step 1.1: Add targeted diagnostics to the shared AppHost-backed startup path

Instrument the shared startup code so timeout and hang failures report the resource and endpoint evidence needed to choose the real fix quickly. Keep the instrumentation inside the shared authentication harness rather than scattering ad hoc logging across both Web test projects.

Files:
* test/Shared/Authentication/AppHostProcessHandle.cs - enrich timeout exceptions with recent process output, candidate endpoint attempts, and the last redirect chain seen by the probe
* test/Shared/Authentication/RealAppHostProcessFactory.cs - capture the exact AppHost launch arguments and relevant environment overrides used by both suites

Discrepancy references:
* DR-01
* DR-02

Success criteria:
* A rerun of either failing suite produces actionable evidence that distinguishes listener discovery failure from Web or Keycloak startup failure
* Diagnostic output remains local to the shared harness and does not require changes in the individual FunctionalTests or E2ETests projects

Context references:
* .copilot-tracking/research/2026-06-30/web-test-failures-research.md (Lines 22-27) - remaining research gap and hang-dump fallback
* .copilot-tracking/research/2026-06-30/web-test-failures-research.md (Lines 102-121) - current shared detection path and its ambiguity

Dependencies:
* Existing failure research accepted as the source of truth for the shared failure path

### Step 1.2: Reproduce the failure with narrow validation commands and collect the new evidence

Use the smallest failing checks that exercise the shared startup path. Prefer the same functional single-test and E2E project commands already proven to fail so the new evidence is comparable to the current research baseline.

Files:
* test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/Authentication/RealAuthenticationFunctionalTestFixture.cs - reference point for the functional fixture that blocks before test execution
* test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/Authentication/RealAuthenticationE2ETestFixture.cs - reference point for the E2E fixture that blocks before browser navigation

Success criteria:
* The functional repro confirms whether the AppHost process is hanging, the Web endpoint is absent, or the Keycloak redirect chain is incomplete
* The E2E repro confirms whether the same shared failure shape still occurs after instrumentation
* The captured evidence is sufficient to decide whether Phase 2 or Phase 3 contains the controlling fix

Context references:
* .copilot-tracking/research/subagents/2026-06-30/functional-tests-failure-research.md (Lines 16-70) - baseline functional repro and hang-abort evidence
* .copilot-tracking/research/subagents/2026-06-30/e2e-tests-failure-research.md (Lines 20-66) - baseline E2E timeout evidence

Dependencies:
* Step 1.1 completion

### Step 1.3: Inspect Aspire resource-level logs for `web` and `keycloak`

Before choosing between discovery hardening and AppHost readiness changes, inspect the resource-level startup evidence for the AppHost-composed `web` and `keycloak` resources. Treat this as the discriminating gate required by the research. Use the new harness diagnostics as supporting evidence, not as the sole decision source.

Primary access path:
* Use the Aspire dashboard resource logs opened from the dashboard URL emitted by the AppHost startup output, because the local-development guide treats AppHost dashboard links and runtime listener output as the supported validation surface.
* If the dashboard UI cannot be used in the current run, preserve the AppHost console output in `artifacts/local/` and record the dashboard URL plus the `web` and `keycloak` startup lines observed during the same reproduction.

Files:
* src/TNC.Trading.Platform.AppHost/AppHostProjectRegistration.cs - reference point for the `web` resource registration and external endpoint exposure
* src/TNC.Trading.Platform.AppHost/AppHostEnvironmentWiring.cs - reference point for Keycloak-backed Web auth wiring and resource wait ordering
* test/Shared/Authentication/RealAppHostProcessFactory.cs - reference point for the AppHost startup environment used by both suites

Discrepancy references:
* DR-01
* DD-01

Success criteria:
* Phase 1 explicitly records whether `web` failed to start, failed to publish an external endpoint, or started successfully while remaining undiscoverable to the current harness
* The decision to enter Phase 2 or Phase 3 is backed by resource-level evidence, not by harness output alone

Context references:
* .copilot-tracking/research/2026-06-30/web-test-failures-research.md (Lines 22-31) - research-required next step and rationale
* .copilot-tracking/research/2026-06-30/web-test-failures-research.md (Lines 176-177) - resource startup logs as the next targeted check
* docs/wiki/local-development.md (Lines 117-121, 162-190) - supported dashboard and runtime-listener validation surfaces for AppHost-managed local runs

Dependencies:
* Step 1.2 completion

### Step 1.4: Inspect an existing hang dump if resource-level evidence remains inconclusive

If the reruns and resource logs still do not identify the controlling wait, inspect one of the existing AppHost or testhost hang dumps produced by the functional suite. Keep this as a bounded fallback inside the same remediation plan rather than reopening broad research immediately.

Primary access path:
* Prefer Visual Studio dump debugging when available because it is the most direct way to inspect managed wait state in the existing `testhost` and `TNC.Trading.Platform.AppHost` dumps.
* If Visual Studio dump debugging is not available, use `dotnet-dump analyze` when the tool is installed.
* If neither tool is available, record the missing prerequisite and preserve the dump path in the implementation notes before reopening research.

Files:
* test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/TestResults/ - existing hang-dump artifacts from the known failing run
* test/Shared/Authentication/AppHostProcessHandle.cs - reference point for the wait loop the dump should help explain

Discrepancy references:
* DR-02

Success criteria:
* The escalation path remains within the current remediation plan if Phase 1 evidence is still ambiguous
* Dump inspection either identifies the blocking wait directly or definitively justifies moving to a new research pass

Context references:
* .copilot-tracking/research/2026-06-30/web-test-failures-research.md (Lines 33-35) - documented hang-dump fallback
* .copilot-tracking/research/subagents/2026-06-30/functional-tests-failure-research.md (Lines 37-70) - existing dump-producing functional run

Dependencies:
* Step 1.3 completion only when resource-level evidence is still inconclusive

### Step 1.5: Validate Phase 1 changes

Run the two narrow repro commands after the shared diagnostics land.

Validation commands:
* dotnet test .\test\TNC.Trading.Platform.Web\TNC.Trading.Platform.Web.FunctionalTests\TNC.Trading.Platform.Web.FunctionalTests.csproj --filter "FullyQualifiedName~RootRoute_ShouldRenderSignInPage_WhenAnonymousUserRequestsApplicationEntry" - narrow functional startup repro
* dotnet test .\test\TNC.Trading.Platform.Web\TNC.Trading.Platform.Web.E2ETests\TNC.Trading.Platform.Web.E2ETests.csproj --nologo --verbosity normal - narrow E2E startup repro

## Implementation Phase 2: Replace brittle endpoint discovery with stable shared discovery when the Web resource is healthy

<!-- parallelizable: false -->

### Step 2.1: Move shared endpoint discovery away from parent-process output scraping

If Phase 1 shows that the Web and Keycloak resources are healthy enough to start but the harness still only discovers the Aspire dashboard listener, replace the current discovery strategy with a more reliable source of truth. Favor Aspire-supported resource or endpoint discovery if it can be introduced without rewriting the entire suite, otherwise centralize a deterministic discovery mechanism in the shared harness rather than relying on incidental `Now listening on:` lines and local port scans.

Files:
* test/Shared/Authentication/AppHostProcessHandle.cs - replace or augment `Now listening on:` parsing and local port enumeration with a stable endpoint-resolution path
* test/Shared/Authentication/RealAppHostProcessFactory.cs - align startup and lifetime handling with the chosen discovery mechanism
* test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/Authentication/AppHostProcessFactory.cs - keep the E2E fixture aligned with the shared mechanism
* test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/Authentication/RealAppHostProcessFactory.cs - keep the functional fixture aligned with the shared mechanism

Discrepancy references:
* DR-02
* DD-01

Success criteria:
* The shared harness resolves the Web sign-in base URI without depending on dashboard-only output
* Both suites continue to authenticate against the real Keycloak-backed sign-in flow rather than a synthetic fallback
* Timeout exceptions remain diagnostic if the Web endpoint still cannot be resolved

Context references:
* .copilot-tracking/research/2026-06-30/web-test-failures-research.md (Lines 102-121) - evidence that current discovery depends on output scraping and local ports
* .github/instructions/aspire-tests.instructions.md (Lines 15-34) - repository preference for Aspire-managed closed-box testing instead of brittle in-process assumptions

Dependencies:
* Phase 1 evidence, including resource-level logs, showing the Web resource is healthy enough that discovery, not startup, is the controlling failure

### Step 2.2: Keep the functional and E2E auth flows consistent after the shared discovery change

After changing the discovery mechanism, verify that the functional suite still uses Playwright only to obtain an authenticated session and that the E2E suite still drives the real Keycloak login page in the browser. Avoid introducing divergent startup paths that would let one suite pass while masking the other.

Files:
* test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/Authentication/RealAuthenticationSessionFactory.cs - preserve authenticated cookie acquisition for HttpClient-based tests
* test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/Authentication/PlatformDashboardAuthenticationE2ETests.cs - preserve browser-based sign-in expectations against the real provider

Success criteria:
* Functional tests still acquire a real authenticated session before issuing HttpClient requests
* E2E tests still navigate through the expected Keycloak-backed sign-in page and dashboard landing flow

Context references:
* .copilot-tracking/research/subagents/2026-06-30/web-test-classification-research.md (Lines 1-44) - classification of each suite and the shared AppHost-backed auth model

Dependencies:
* Step 2.1 completion

## Implementation Phase 3: Repair AppHost Web or Keycloak readiness if startup, not discovery, is the blocker

<!-- parallelizable: false -->

### Step 3.1: Fix the controlling AppHost startup or readiness gate exposed by Phase 1

If Phase 1 shows that the Web resource never becomes externally reachable, repair the startup gate at the owning AppHost layer instead of compensating in the tests. Start with the Web and Keycloak wiring because the current composition explicitly forces the Web app to wait for Keycloak and use the real Keycloak provider.

Files:
* src/TNC.Trading.Platform.AppHost/AppHostEnvironmentWiring.cs - adjust Web authentication environment values or readiness dependencies only if the evidence shows they are preventing Web startup or endpoint publication
* src/TNC.Trading.Platform.AppHost/AppHostProjectRegistration.cs - adjust endpoint exposure or dependency ordering only if the current resource registration prevents the Web listener from becoming externally discoverable
* src/TNC.Trading.Platform.Web/Authentication/PlatformAuthenticationEndpointRouteBuilderExtensions.cs - touch only if the redirect chain or route behavior itself is proven to be incompatible with the shared probe

Discrepancy references:
* DR-01
* DD-01

Success criteria:
* The Web resource exposes a usable external endpoint under the same AppHost-backed startup path the test harness exercises
* The Keycloak-backed sign-in flow remains the real authentication path for both suites
* Any AppHost change is backed by Phase 1 evidence, not by timeout inflation or speculative wiring edits

Context references:
* .copilot-tracking/research/2026-06-30/web-test-failures-research.md (Lines 44-63) - current AppHost and Web control points
* .copilot-tracking/research/subagents/2026-06-30/e2e-tests-failure-research.md (Lines 68-121) - evidence that the Web resource may not be surfacing its endpoint before timeout

Dependencies:
* Phase 1 evidence, including resource-level logs, showing that resource startup or readiness, not discovery alone, is the controlling failure

### Step 3.2: Keep the shared harness expectations strict after the AppHost fix

Do not relax the probe into accepting any anonymous landing page or dashboard endpoint. Preserve the requirement that the shared harness reaches the real Keycloak OIDC redirect path so the suites continue to validate the production-like authentication flow.

Files:
* test/Shared/Authentication/AppHostProcessHandle.cs - retain strict OIDC redirect validation while updating any supporting diagnostics or endpoint checks

Success criteria:
* The tests fail loudly if authentication drifts away from the real Keycloak-backed flow
* The fix does not reduce the suites to shallow reachability checks

Context references:
* test/Shared/Authentication/AppHostProcessHandle.cs (Lines 87-129) - existing sign-in probe contract
* .github/instructions/playwright.instructions.md (Lines 16-33) - avoid time-based flake workarounds and preserve meaningful end-to-end assertions

Dependencies:
* Step 3.1 completion

## Implementation Phase 4: Final validation and closure

<!-- parallelizable: false -->

### Step 4.1: Run the full validation set for the affected area

Execute the focused validation commands first, then run both target Web test projects without narrowing to prove that the shared startup path is stable across the full authentication suites.

Validation commands:
* dotnet test .\test\TNC.Trading.Platform.Web\TNC.Trading.Platform.Web.FunctionalTests\TNC.Trading.Platform.Web.FunctionalTests.csproj --filter "FullyQualifiedName~RootRoute_ShouldRenderSignInPage_WhenAnonymousUserRequestsApplicationEntry" - focused functional confirmation before the full-project pass
* dotnet test .\test\TNC.Trading.Platform.Web\TNC.Trading.Platform.Web.E2ETests\TNC.Trading.Platform.Web.E2ETests.csproj --nologo --verbosity normal - focused E2E confirmation before the full-project pass
* dotnet test .\test\TNC.Trading.Platform.Web\TNC.Trading.Platform.Web.FunctionalTests\TNC.Trading.Platform.Web.FunctionalTests.csproj
* dotnet test .\test\TNC.Trading.Platform.Web\TNC.Trading.Platform.Web.E2ETests\TNC.Trading.Platform.Web.E2ETests.csproj

### Step 4.2: Fix minor validation regressions and report any remaining blocker

Address straightforward follow-on issues discovered by the validation pass, such as a stale assertion or missing fixture alignment after the shared fix. If validation still fails because the Web resource remains undiscoverable or unhealthy, record the new evidence and open a new research pass rather than widening the current change set without a controlling hypothesis.

## Dependencies

* Existing failure research under .copilot-tracking/research/2026-06-30/
* Docker and local Keycloak-capable AppHost startup environment
* Ability to run the two target dotnet test projects locally
* Access to the AppHost-emitted Aspire dashboard URL or persisted AppHost console output in `artifacts/local/` for resource-log inspection
* Visual Studio dump debugging or `dotnet-dump analyze` available if the hang-dump fallback becomes necessary

## Success Criteria

* The shared startup path no longer times out before either Web test suite begins its real assertions
* Both target test projects pass while continuing to authenticate against the real Keycloak-backed sign-in flow
* The final fix is applied in the controlling shared harness or AppHost layer rather than duplicated separately in the two test projects
