<!-- markdownlint-disable-file -->
# Implementation Details: Keycloak Testing Improvements

## Context Reference

Sources: `.copilot-tracking/research/2026-07-27/keycloak-aspire-readiness-and-persistence-research.md`; `.copilot-tracking/research/subagents/2026-07-27/keycloak-testing-plan-surface-research.md`; repository test, Aspire, authentication, architecture, and documentation instructions.

## Implementation Phase 1: Readiness Policy and Package Alignment

<!-- parallelizable: false -->

### Step 1.1: Align Aspire testing dependencies

Update the API integration test project from `Aspire.Hosting.Testing` 13.1.2 to 13.2.3 so all AppHost-backed test projects use the version matched by the AppHost SDK. Restore and build the project before relying on 13.2.3 resource-notification APIs.

Files:
* `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/TNC.Trading.Platform.Api.IntegrationTests.csproj` - Align the Aspire testing package and link new shared test helpers when they are introduced.

Discrepancy references:
* Addresses DD-01 by treating package alignment as prerequisite cleanup rather than the readiness root-cause fix.

Success criteria:
* All AppHost-backed test projects reference `Aspire.Hosting.Testing` 13.2.3.
* The API integration test project restores and builds without package downgrade or compatibility warnings.

Context references:
* `.copilot-tracking/research/2026-07-27/keycloak-aspire-readiness-and-persistence-research.md` (Lines 69-70) - Package drift finding.
* `.copilot-tracking/research/subagents/2026-07-27/keycloak-testing-plan-surface-research.md` (Lines 104-127) - Project version and reference inventory.

Dependencies:
* .NET SDK 10.0.200 from `global.json`

### Step 1.2: Add a testable bounded readiness policy

Create one shared test-infrastructure helper that executes condition-based HTTP probes against a single caller-supplied deadline. Model connection failures, temporary 404 during realm import, HTTP 429, and HTTP 503 as transient. Fail immediately for HTTP 400, HTTP 401, issuer mismatch, malformed successful responses, and other permanent failures. Include the attempted URI, status, elapsed time, and last transient failure in timeout diagnostics without recording credentials or tokens.

Keep the helper independent of Docker and Aspire types so classification and cancellation behavior can be covered by fast unit tests with a scripted `HttpMessageHandler` or narrow send delegate. Do not use fixed sleeps as the readiness strategy; inject delay behavior for unit tests and use bounded condition polling in integration paths.

Files:
* `test/Shared/Authentication/KeycloakReadinessPolicy.cs` - Shared deadline, retry classification, realm discovery, and issuer validation.
* `test/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.UnitTests/KeycloakReadinessPolicyTests.cs` - Fast transient, permanent, success, cancellation, and timeout coverage.
* `test/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.UnitTests/TNC.Trading.Platform.AppHost.UnitTests.csproj` - Link the pure shared helper for unit testing if required.
* `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/TNC.Trading.Platform.Api.IntegrationTests.csproj` - Link the shared helper for API token retry classification.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/TNC.Trading.Platform.Web.FunctionalTests.csproj` - Link the shared helper.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/TNC.Trading.Platform.Web.E2ETests.csproj` - Link the shared helper.

Discrepancy references:
* Addresses DD-02 by sharing retry classification while preserving suite-specific behavior-level readiness checks.

Success criteria:
* Unit tests prove eventual success after transient failures, exact issuer validation, immediate permanent failure, cancellation, and overall deadline exhaustion.
* Tests use `MethodName_StateUnderTest_ExpectedResult` names and XML comments with requirement or research traceability, expected outcomes, and regression significance.
* The helper never logs response bodies from token endpoints or secret-bearing request content.

Context references:
* `.copilot-tracking/research/2026-07-27/keycloak-aspire-readiness-and-persistence-research.md` (Lines 113-130) - Required layered readiness and retry classification.
* `.copilot-tracking/research/subagents/2026-07-27/keycloak-testing-plan-surface-research.md` (Lines 129-149) - Recommended unit-test seams and scenarios.

Dependencies:
* Step 1.1 completion
* Existing xUnit and AppHost unit-test infrastructure

### Step 1.3: Validate phase changes

Validation commands:
* `dotnet test test/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.UnitTests/TNC.Trading.Platform.AppHost.UnitTests.csproj` - Readiness policy and AppHost helper unit tests.
* `dotnet build test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/TNC.Trading.Platform.Api.IntegrationTests.csproj` - API test package compatibility.

## Implementation Phase 2: Managed Startup and Cleanup Hardening

<!-- parallelizable: false -->

### Step 2.1: Gate direct Keycloak calls on layered readiness

Refactor `StartManagedAppHostProcessAsync` to create one bounded cancellation deadline before startup. After `StartAsync`, await `application.ResourceNotifications.WaitForResourceHealthyAsync("keycloak", token)`, then use the shared readiness policy to retrieve application-realm discovery and assert the exact configured issuer. Only after both barriers succeed may the managed Web fixture request an admin token or update Web callback configuration.

Run the admin-token request, application-client query, and client update through the same bounded transient policy. Retry connection failures, temporary 404, HTTP 429, and HTTP 503; fail immediately for HTTP 400 and 401 with secret-safe diagnostics. Add scripted fault-injection coverage for transient success, permanent failure, and deadline exhaustion across this Admin sequence.

Preserve the unmanaged API fixture and its existing gates: runtime listener discovery, API `/health/ready`, anonymous protected-surface `401`, and successful real application-token issuance. Aspire resource notifications apply only to managed Web startup because the unmanaged API process does not expose `DistributedApplication.ResourceNotifications`. Web tests must still follow the OIDC challenge to a recognizable Keycloak login page. Do not use `WaitForCompletion` for the long-running Keycloak resource and do not increase `Dcp:DependencyCheckTimeout` without timing evidence.

Files:
* `test/Shared/Authentication/RealAppHostProcessFactory.cs` - Add resource-health and realm-discovery barriers before Admin API calls.
* `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/RealKeycloakAccessTokenFactory.cs` - Apply shared transient/permanent response classification to token readiness.
* `test/Shared/Authentication/AppHostProcessHandle.cs` - Preserve API and Web behavior-level readiness diagnostics.
* `test/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.UnitTests/KeycloakAdminReadinessTests.cs` - Script Admin token, client-query, and client-update transient and permanent outcomes through an extracted send seam.

Discrepancy references:
* Addresses the selected implementation path and DD-02.

Success criteria:
* No managed Web Admin API request occurs before Keycloak is Healthy and application-realm discovery returns the expected issuer.
* Admin token, client-query, and client-update operations retry only documented transient failures under the shared deadline and have fault-injection coverage.
* The unmanaged API fixture retains its existing API health, protected-surface, and real-token behavior gates.
* HTTP 400 and 401 token responses fail immediately with secret-safe diagnostics instead of retrying until timeout.
* All waits share bounded cancellation and no fixed startup sleep is introduced.

Context references:
* `.copilot-tracking/research/2026-07-27/keycloak-aspire-readiness-and-persistence-research.md` (Lines 55-68, 102-130) - Proven race and selected readiness sequence.
* `.copilot-tracking/research/subagents/2026-07-27/keycloak-testing-plan-surface-research.md` (Lines 28-75) - Exact factory, handle, and token-helper surfaces.

Dependencies:
* Implementation Phase 1 completion

### Step 2.2: Make partial-start cleanup exception-complete

Keep builder and application references visible to the managed factory failure path. Dispose a started application, then the builder, then environment scopes after failures at build, start, health, discovery, or client-configuration stages. Make `AppHostProcessHandle.DisposeAsync` attempt every cleanup action even when an earlier disposal throws, preserving the first failure and attaching later cleanup failures where practical.

If deterministic lifecycle tests require an injectable seam, add the narrowest delegate or lifecycle adapter needed to script create, build, start, and dispose failures. Keep Aspire distributed tests closed-box; use the seam only for the helper's failure-order unit tests.

Files:
* `test/Shared/Authentication/RealAppHostProcessFactory.cs` - Dispose partial builder/application state during failed initialization.
* `test/Shared/Authentication/AppHostProcessHandle.cs` - Ensure all cleanup stages are attempted.
* `test/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.UnitTests/RealAppHostLifecycleTests.cs` - Add failure-path cleanup tests if a narrow deterministic seam is introduced.

Success criteria:
* Failures after build or start do not leave an AppHost process, Keycloak container, environment override, or fixed-port owner behind.
* Cleanup attempts application, builder, and environment restoration even when one disposal operation fails.
* Failure diagnostics retain the initialization cause and identify any cleanup failure.

Context references:
* `.copilot-tracking/research/subagents/2026-07-27/keycloak-testing-plan-surface-research.md` (Lines 28-60, 154-170) - Partial-start and exception-complete cleanup requirements.

Dependencies:
* Step 2.1 implementation can proceed in the same edit slice because every new wait increases partial-start exposure.

### Step 2.3: Validate phase changes

Validation commands:
* `dotnet test test/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.UnitTests/TNC.Trading.Platform.AppHost.UnitTests.csproj` - Retry and lifecycle failure coverage.
* `dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/TNC.Trading.Platform.Web.FunctionalTests.csproj --filter FullyQualifiedName~Authentication` - Managed Web readiness and login-path proof.
* `dotnet test test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/TNC.Trading.Platform.Api.IntegrationTests.csproj --filter FullyQualifiedName~PlatformAuthenticationIntegrationTests` - Real token readiness proof.

## Implementation Phase 3: Baseline Topology and Timing Evidence

<!-- parallelizable: false -->

### Step 3.1: Verify repository-owned test execution topology

Confirm the committed quality gate still invokes one solution-level `dotnet test` command on one runner and results directory. Record that separate test hosts can overlap while sharing the machine filesystem and host network. Inspect any CI configuration introduced after planning; if external jobs use separate runners, document that the machine-local lease applies per runner and that separate runners do not share host port 8080.

Files:
* `.github/scripts/Invoke-CleanArchitectureQualityGate.ps1` - Verify the committed solution-level test topology; modify only if implementation discovers a required gate change.
* `.github/workflows/` - Inspect if workflows are added before implementation; no repository workflows exist at planning time.
* `.copilot-tracking/changes/2026-07-27/keycloak-testing-improvements-changes.md` - Record verified topology and external-CI limits in the implementation evidence artifact.

Discrepancy references:
* Supports DD-03 by verifying the machine-local topology in which the port-specific lease operates.

Success criteria:
* The implementation records whether participating projects share a machine, filesystem, and host network in committed automation.
* Claims about lease scope explicitly exclude unobserved organization-level CI and separate runners.

Context references:
* `.copilot-tracking/research/subagents/2026-07-27/keycloak-testing-plan-validation-gaps-research.md` (Lines 14-27) - Committed one-runner solution test topology.

Dependencies:
* Implementation Phase 2 completion

### Step 3.2: Capture pre-lease contention and startup timing evidence

Build once, then launch the API integration, Web functional, and Web E2E project commands concurrently with `--no-build`. Save each process ID, start/end time, exit code, stdout, and stderr under a timestamped `before/` evidence directory. While commands overlap, sample port 8080 listeners and owning processes. Expected pre-fix bind failures or timeouts are valid evidence and must not be treated as a passing validation gate.

In the same bounded evidence run, record secret-safe timestamps for managed `StartAsync`, Keycloak Healthy, realm discovery, admin token, client query/update, Web/API readiness, and login-page readiness. Capture cold-image, fresh session with image present, and warm local persistent cases where the environment permits. Do not record tokens, passwords, cookies, or response bodies.

Files:
* `test/Shared/Authentication/RealAppHostProcessFactory.cs` - Emit structured, secret-safe transition diagnostics.
* `test/Shared/Authentication/AppHostProcessHandle.cs` - Include transition timing in timeout diagnostics.
* `artifacts/quality-gates/keycloak-testing-hardening/` - Store before/after process, port-owner, lease-event, and timing evidence through the repository quality-evidence workflow.

Discrepancy references:
* Addresses DD-06 by obtaining baseline and deadline evidence before lease implementation.

Success criteria:
* Evidence records overlapping test processes and port 8080 ownership even when the pre-fix suites fail.
* Evidence distinguishes cold-image, clean-session, and warm transitions where each state can be established.
* A finite provisional lease deadline is selected from the longest observed run plus documented margin.
* Any `Dcp:DependencyCheckTimeout` change cites measured Healthy-transition data; otherwise the 60-second setting remains unchanged.

Context references:
* `.copilot-tracking/research/2026-07-27/keycloak-aspire-readiness-and-persistence-research.md` (Lines 25-32, 258-262) - Timing and before/after concurrency evidence requirements.
* `.copilot-tracking/research/subagents/2026-07-27/keycloak-testing-plan-validation-gaps-research.md` (Lines 81-111) - Executable baseline evidence design.

Dependencies:
* Step 3.1 completion
* Docker and Playwright prerequisites

### Step 3.3: Validate phase evidence

Validation checks:
* Confirm all pre-lease commands, exit codes, logs, and port samples are retained even when a project fails.
* Confirm timing and process diagnostics contain no tokens, passwords, cookies, client secrets, or token response bodies.
* Record the selected finite lease deadline and rationale before Phase 4 begins.

## Implementation Phase 4: Cross-Process Ownership and State Simplification

<!-- parallelizable: false -->

### Step 4.1: Add a portable bounded lease for port 8080

Create a shared asynchronous lease backed by an exclusively opened lock file in a stable machine-local temporary location. Use one port-specific lease key across participating repository test processes. Acquisition must honor cancellation and the Phase 3 deadline, report the lock path, elapsed time, and process identity on timeout, and retain the file stream until disposal.

The lease coordinates participating tests but does not prove that port 8080 is free from an unrelated developer AppHost. Keep bind-failure diagnostics distinct from lease-timeout diagnostics. Validate real filesystem and child-process behavior in the existing API integration test project, not a unit-test project.

Files:
* `test/Shared/Authentication/KeycloakPortLease.cs` - Portable exclusive file lease.
* `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/KeycloakPortLeaseTests.cs` - Real child-process contention, bounded timeout, cancellation, release, and reacquisition integration tests.
* `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/TNC.Trading.Platform.Api.IntegrationTests.csproj` - Link the shared lease helper.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/TNC.Trading.Platform.Web.FunctionalTests.csproj` - Link the shared lease helper.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/TNC.Trading.Platform.Web.E2ETests.csproj` - Link the shared lease helper.

Discrepancy references:
* Addresses DD-03 and DD-05 by using an integration boundary for real filesystem behavior.

Success criteria:
* A second child process cannot acquire the same lease while it is held.
* Disposal permits immediate reacquisition.
* Timeout and cancellation are bounded and diagnostic.
* The lease contains no Windows-only synchronization primitive.

Context references:
* `.copilot-tracking/research/subagents/2026-07-27/keycloak-testing-plan-validation-gaps-research.md` (Lines 29-58) - Integration-test ownership and linking design.

Dependencies:
* Implementation Phase 3 completion and selected deadline

### Step 4.2: Apply the lease to every real-Keycloak fixture

Acquire the lease before any environment override or AppHost startup in API integration, Web functional, and Web E2E fixtures. Retain it until browser/client state, AppHost resources, environment scopes, and fixture state have been cleaned up. Release it in initialization-failure paths as well as normal disposal.

Files:
* `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/RealAuthenticationIntegrationTestFixture.cs` - API lease lifecycle.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/Authentication/RealAuthenticationFunctionalTestFixture.cs` - Functional lease lifecycle.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/Authentication/RealAuthenticationE2ETestFixture.cs` - E2E lease lifecycle.

Success criteria:
* All three fixtures acquire the same lease before AppHost startup and release it only after cleanup.
* Existing xUnit collection serialization remains in place for intra-assembly reuse.
* Initialization failures release both AppHost state and the lease.

Context references:
* `.copilot-tracking/research/subagents/2026-07-27/keycloak-testing-plan-surface-research.md` (Lines 77-102) - Fixture call sites and lifecycle order.

Dependencies:
* Step 4.1 completion

### Step 4.3: Capture post-lease concurrency evidence

Validation commands:
* `dotnet test test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/TNC.Trading.Platform.Api.IntegrationTests.csproj --filter FullyQualifiedName~KeycloakPortLeaseTests` - Real cross-process lease behavior.
* Re-run the exact Phase 3 concurrent harness and store output under the matching `after/` evidence directory - Require all three projects to pass, non-overlapping lease intervals, and at most one port 8080 owner.

### Step 4.4: Prove whether dynamic Web client mutation is necessary

Run the retained real Web sign-in and sign-out flow using only the wildcard localhost redirect URIs and origins imported from the checked-in realm. If both flows pass, remove the Admin API callback mutation and its bootstrap-admin dependency from managed fixture setup. If the flow fails, retain the mutation and record the exact URI mismatch as evidence; do not introduce shared persistent Keycloak test state.

Files:
* `src/TNC.Trading.Platform.AppHost/Realms/tnc-trading-platform-realm.json` - Verify imported wildcard callback/origin contract; modify only if a narrowly justified checked-in realm correction is required.
* `test/Shared/Authentication/RealAppHostProcessFactory.cs` - Remove dynamic mutation only after the behavior proof passes.
* Existing Web functional and E2E authentication tests - Prove sign-in, sign-out, and callback behavior.

Discrepancy references:
* Addresses DD-07; shared or persistent test reuse remains rejected until mutable client state is eliminated or reconciled.

Success criteria:
* The decision to retain or remove Admin API mutation is backed by a real browser sign-in and sign-out result.
* Session-scoped Keycloak remains the automated-test baseline in either outcome.
* No test depends on stale state from a previous realm import.

Context references:
* `.copilot-tracking/research/2026-07-27/keycloak-aspire-readiness-and-persistence-research.md` (Lines 195-219) - Mutable client state and safe experiment order.

Dependencies:
* Step 4.3 completion so callback changes cannot alter baseline lease evidence
* Docker and Playwright prerequisites

### Step 4.5: Validate state-simplification changes

Validation commands:
* Repeat the managed Web authentication fixture at least 20 times with the image present and a fresh session container - Readiness reliability acceptance check.
* Run focused Web functional and E2E authentication tests - Wildcard callback and login/sign-out proof.

## Implementation Phase 5: Documentation and Final Validation

<!-- parallelizable: false -->

### Step 5.1: Update testing and local-development guidance

Document layered Keycloak readiness, permanent versus transient failure behavior, exception-complete cleanup, and the cross-process lease. Explain that xUnit collections serialize only inside one assembly, the lease coordinates participating processes sharing port 8080, and an external AppHost can still cause a bind conflict. Preserve guidance that automated tests use volume-free session state.

Files:
* `docs/wiki/testing-and-quality.md` - Test architecture, focused commands, concurrent lease proof, and retained real-runtime scope.
* `docs/wiki/local-development.md` - Readiness expectations, lease troubleshooting, session-state isolation, and stale-realm guidance.
* `docs/wiki/architecture.md` - Update only if runtime composition or data persistence changes.

Discrepancy references:
* Records the resolved CI topology, before/after evidence, and test isolation decisions from Phases 3-4.
* Preserves local data persistence as optional follow-on work unless explicitly selected.

Success criteria:
* Wiki guidance matches delivered test behavior and validation commands.
* Documentation distinguishes persistent container lifetime, persistent data, and session-scoped test isolation.
* Affected wiki links resolve.

Context references:
* `.copilot-tracking/research/subagents/2026-07-27/keycloak-testing-plan-surface-research.md` (Lines 263-280) - Required and conditional wiki updates.

Dependencies:
* Implementation Phases 1-4 completion

### Step 5.2: Run full project validation

Execute all validation commands for the project:
* `dotnet build TNC.Trading.Platform.slnx`
* Run the three real-authentication projects individually with the focused filters from prior phases.
* Run the three real-authentication projects concurrently to prove lease behavior.
* `.github/scripts/Invoke-CleanArchitectureQualityGate.ps1 -GateId keycloak-testing-hardening`
* Use `dotnet test TNC.Trading.Platform.slnx -m:1` only as the documented fallback for an intermittent MSBuild child-node failure, not as a substitute for concurrent lease proof.

### Step 5.3: Fix minor validation issues

Iterate on isolated lint, build, cleanup, and test failures. Do not mask a failed readiness or lease check with longer unmeasured timeouts, fixed sleeps, filters, or retries that classify permanent failures as transient.

### Step 5.4: Report blocking issues

When failures require authority/issuer redesign, dynamic Keycloak ports, shared persistent test state, idempotent provisioning, or CI topology changes, document the evidence and create follow-on research and planning rather than expanding this implementation inline.

## Dependencies

* Docker Desktop and the Aspire-managed Keycloak runtime
* .NET SDK 10.0.200
* Playwright prerequisites for Web E2E validation
* Exclusive machine-local access to port 8080 during real-Keycloak tests

## Success Criteria

* Keycloak Admin and token protocol calls are gated by bounded health and realm readiness.
* Permanent auth/configuration failures fail immediately with secret-safe diagnostics.
* Partial initialization and disposal failures do not leak AppHost resources, environment overrides, leases, or port ownership.
* Real-Keycloak test projects coordinate fixed port 8080 across processes while retaining per-assembly fixtures.
* Automated tests retain fresh, volume-free session state and do not depend on stale realm imports.
* Twenty repeated managed Web fixture runs pass with a fresh session container and an already-present image.
* Wiki guidance and the full Clean Architecture quality gate reflect the delivered testing approach.