# Work Package Test Mitigation Plan

This plan turns the test review findings for work package 002 into a prioritized set of mitigation actions. It focuses on closing the highest-risk safety and confidence gaps first, prefers lower-level automated coverage before broader UI or E2E expansion, and keeps each work item traceable to the review report, requirements, and current repository evidence.

## Summary

- **Source review**: `../work-package-test-review-report.md`
- **Work package**: `./docs/002-environment-and-auth-foundation/`
- **Status**: `completed`
- **Inputs**:
  - `../work-package-test-review-report.md`
  - `../requirements.md`
  - `../technical-specification.md`
  - `001-delivery-plan.md`

## Description of work

This mitigation plan will strengthen the current automated test suite for work package 002 by addressing the missing safety coverage around notifications, blocked live usage, and live-auth suppression; by closing retry-cycle, degraded-state, and schedule-gating gaps; by replacing weak UI substring checks with requirement-driven UI assertions; and by adding higher-fidelity validation for SQL-backed configuration, configuration audit, credential rotation, and Mailpit-backed notification visibility. The plan also includes targeted supporting implementation or test-harness changes where current behaviors are not externally observable enough to be tested deterministically.

## Mitigation approach

- **Delivery model**: `phased hardening`
- **Branching**: `implement on 002-environment-and-auth-foundation in sequenced PR-sized slices, completing higher-risk lower-level coverage before broader UI or infrastructure validation`
- **Dependencies**: `TNC.Trading.Platform.Application unit tests; TNC.Trading.Platform.Api unit and integration tests; TNC.Trading.Platform.Infrastructure unit tests; TNC.Trading.Platform.Web functional and E2E tests; Aspire AppHost orchestration; SQL Server-backed configuration path; Mailpit local notification path; IG authentication test doubles or observable fakes`
- **Key risks**:
  - `Notification assertions may remain indirect if event payloads or dispatch records are not exposed cleanly; mitigate by adding small test-focused observable seams rather than broad product changes.`
  - `Retry and schedule tests can become flaky if they keep relying on fixed waits; mitigate by asserting externally visible state transitions and controllable timing inputs.`
  - `UI hardening can drift from backend contracts if browser tests are added before status/configuration assertions are stabilized; mitigate by locking API and lower-level assertions first.`
  - `Infrastructure-on validation can destabilize local runs if introduced too broadly; mitigate by adding one narrow high-fidelity path for SQL Server and Mailpit before expanding.`

## Review findings to address

| Finding ID | Review area | Review assessment | Source evidence | Planned mitigation |
| --- | --- | --- | --- | --- |
| `F1` | `FR10`, `FR11`, `FR17`, `FR19` notification behavior | Missing | Review report found no confirmed automated evidence beyond `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/NotificationProviderTests.cs`; no integration or functional notification assertions were identified for failure, recovery, blocked-live, new-cycle failure, or retry-limit content. | Add unit tests for notification mapping and content, then add integration tests that trigger each required transition and assert dispatch records, summaries, and secret-safe payloads. |
| `F2` | `FR9`, `SR1` live-auth suppression | Missing | Review report found no direct test proving live authentication is never attempted; current evidence is indirect validator coverage in `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/UpdatePlatformConfigurationValidatorTests.cs`. | Add guard-focused unit tests plus integration tests with an IG double that proves no live-auth request is issued when live use is blocked. |
| `F3` | `FR12`, `FR14`, `FR15`, `FR16`, `FR18`, `FR22` retry-cycle and schedule lifecycle | Partial | `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/ApiHealthIntegrationTests.cs` covers recovery and manual retry availability, but does not prove degraded startup, periodic retry transition, retry-budget reset after failed manual retry, or schedule-based retry suppression end to end. | Add deterministic unit and integration coverage for degraded startup, periodic retry mode, manual-retry reset behavior, retry progress visibility, and out-of-schedule suppression. |
| `F4` | `FR13`, `FR18`, `FR20` operator UI coverage | Weak | `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/002-environment-and-auth-foundation/PlatformOperatorUiFunctionalTests.cs` uses raw HTML substring checks such as `Assert.Contains("Auth state", html)` and `Assert.Contains("Stored values are never shown", html)`; `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/PlatformOperatorUiE2ETests.cs` uses `HttpClient` rather than browser interaction. | Replace weak substring checks with requirement-driven UI assertions and add browser-driven coverage for degraded status, retry progress, blocked reasons, and configuration editing behavior. |
| `F5` | `FR1` to `FR3`, `FR8`, `TR4` environment separation and blocked-live UX | Partial | Current confirmed evidence covers status visibility and validator rejection, but the review did not find proof of environment-scoped persistence separation, visible-but-disabled live option behavior, or recorded blocked-live attempts per use path. | Add persistence and API/UI tests that prove environment-scoped records stay distinct and the live option is visible but unavailable with blocked attempts recorded. |
| `F6` | `FR20`, `SR3`, `DR1`, `IR5` durable configuration management | Partial | Review report found basic configuration update coverage in `ApiHealthIntegrationTests`, `PlatformOperatorUiFunctionalTests`, `PlatformOperatorUiE2ETests`, `SqlPlatformConfigurationStoreTests`, and `ProtectedCredentialServiceTests`, but not configuration-audit assertions, startup-fixed next-restart behavior, or credential rotation. | Add focused persistence and integration tests for configuration audit, next-start application of startup-fixed settings, secure credential rotation, and secret-safe historical review. |
| `F7` | `FR21`, `FR22`, `TR13` trading-schedule enforcement | Partial | `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/TradingScheduleGateTests.cs` and `PlatformOperatorUiE2ETests` cover selected cases, but the review found missing weekend permutations, connection-stop behavior, and automatic retry suppression outside schedule. | Extend unit coverage for remaining schedule permutations and add integration tests proving no connect or retry occurs while the trading schedule is inactive. |
| `F8` | Test determinism and fidelity | Weak / Risk | `ApiHealthIntegrationTests.cs` includes `WaitForConditionAsync` with `Task.Delay(100)` polling; higher-level tests set `AppHost__EnableInfrastructureContainers=false`, reducing confidence in real SQL Server and Mailpit behavior. | Replace fixed-delay polling with observable-state assertions where possible and add one narrow infrastructure-on validation path for SQL Server and Mailpit. |

## Mitigation Plan

### Execution gates (required)

Before starting *any* mitigation work item, and again before marking a work item as complete, run the build + test suite and resolve any failures.

| Gate | When | Required actions | If failures occur |
| --- | --- | --- | --- |
| Baseline | Before starting any work item | Run build and all tests listed in **Cross-cutting validation** | Fix or revert until build/tests are green before continuing |
| Pre-completion | Before completing a work item | Re-run build and all tests listed in **Cross-cutting validation** | Fix failures before marking the work item complete |

### Planned work items

| Work item | Description | Traceability (review findings) | Traceability (requirements) | Dependencies | Validation | Rollback/Backout | User instructions |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Work Item 1: Safety-critical notification and live-auth coverage | Add lower-level tests for failure, recovery, blocked-live, retry-limit, and manual-retry-cycle notifications, plus direct proof that live authentication is never attempted. | `F1`, `F2`, `F5` | `FR8`-`FR11`, `FR17`, `FR19`, `NF5`, `SR1`, `SR4`, `SR5`, `IR3`, `IR4`, `TR4`-`TR7`, `TR9`, `TR11` | Existing notification, validator, and integration test projects; IG test doubles or observable fakes | Unit and integration suites pass; notifications assert required fields without secrets; blocked live use records and no-live-auth assertions are proven | Revert new notification assertions and any minimal observability hooks if they destabilize the harness; keep prior notification/provider behavior intact | Reviewers should verify that notification content stays concise and secret-safe and that no test depends on real live-auth endpoints |
| Work Item 2: Retry-cycle and trading-schedule lifecycle hardening | Close degraded-startup, periodic-retry, manual-reset, retry-progress, and out-of-schedule suppression gaps using deterministic unit and integration tests. | `F3`, `F7`, `F8` | `FR12`-`FR18`, `FR21`, `FR22`, `NF1`, `NF2`, `NF5`, `TR2`, `TR8`-`TR10`, `TR13`, `OR1`, `OR4`-`OR6` | Retry and schedule logic already covered in lower-level tests; API status and events endpoints | Unit and integration suites prove state transitions without arbitrary waits; retry progress and schedule state remain observable through public endpoints | Revert test-only timing seams or transition assertions if they create brittle coupling; retain current retry runtime behavior until deterministic coverage is ready | Reviewers should confirm that test timing is controlled through observable state or injectable time sources, not long sleeps |
| Work Item 3: Operator UI and browser-level requirement coverage | Replace weak substring-based functional checks with requirement-driven assertions and add browser-driven UI flows for degraded status, blocked actions, retry progress, and configuration management. | `F4`, `F5`, `F8` | `FR8`, `FR13`, `FR18`, `FR20`, `NF2`, `TR4`, `TR8`, `TR10`, `TR12`, `OR1`, `OR5`-`OR8` | Stable status/configuration API contracts from Work Items 1 and 2; existing Blazor UI routes and functional/E2E test projects | Functional and browser-driven tests prove visible state, blocked reasons, control enablement, and persisted configuration outcomes | Revert overly brittle UI selectors or browser flows if they fail to reflect stable operator behavior; fall back to stable semantic assertions while keeping coverage meaningful | Reviewers should validate that new UI tests map directly to requirement outcomes and use readable `MethodName_StateUnderTest_ExpectedResult` names while keeping requirement traceability explicit outside the method name |
| Work Item 4: Durable configuration, audit, rotation, and infrastructure fidelity | Add missing SQL-backed audit and rotation coverage, then add one infrastructure-on validation path for SQL Server and Mailpit to improve confidence in real persistence and notification behavior. | `F6`, `F8` | `FR7`, `FR20`, `FR21`, `FR22`, `NF4`, `SR2`, `SR3`, `DR1`, `IR4`, `IR5`, `TR3`, `TR12`, `TR13` | SQL-backed persistence path, protected credential flow, AppHost infrastructure containers, Mailpit availability | Persistence and integration tests pass with audit and rotation assertions; one high-fidelity path proves SQL-backed persistence and Mailpit visibility | Revert infrastructure-on test wiring or isolate it behind opt-in configuration if it causes instability; keep lower-level tests as the primary safety net | Reviewers should run the infrastructure-on validation path only after lower-level suites are green and confirm secrets are not exposed in SQL records, APIs, or notification content |

### Work Item 1 details

- [x] Work Item 1: Safety-critical notification and live-auth coverage
  - [x] Build and test baseline established
  - [x] Task 1: Strengthen notification content and mapping coverage
    - [x] Step 1: Extend lower-level tests around notification message building for failure, recovery, blocked-live, retry-limit, and manual-retry-cycle failure events.
    - [x] Step 2: Assert required fields for event type, timestamp, environment context, concise summary, manual-retry guidance, last delay, and periodic delay where applicable.
    - [x] Step 3: Assert that notification payloads stay secret-safe across all supported event types.
  - [x] Task 2: Add integration coverage for notification dispatch behavior
    - [x] Step 1: Trigger auth failure, recovery, retry-limit reached, and blocked-live transitions through public flows.
    - [x] Step 2: Assert that one notification is recorded or dispatched for each required transition and for each blocked-live attempt.
    - [x] Step 3: Assert that a manual retry that starts a new failure cycle emits a fresh failure notification for that new cycle.
  - [x] Task 3: Prove live-auth suppression directly
    - [x] Step 1: Add unit tests around the relevant command guards and validators so live-auth paths fail before any external call.
    - [x] Step 2: Add an integration test with an observable IG double that records attempted environment usage.
    - [x] Step 3: Assert that blocked live use never reaches a live-auth request path.
  - [x] Task 4: Add minimal supporting seams if required
    - [x] Step 1: Introduce only the smallest observable notification or auth-call record needed to make the tests deterministic.
    - [x] Step 2: Keep any new seams internal and aligned to existing abstractions.
  - [x] Build and test validation

  - **Files**:
    - `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/NotificationProviderTests.cs`: expand provider-level assertions or split into requirement-focused notification tests.
    - `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/UpdatePlatformConfigurationValidatorTests.cs`: add blocked-live and live-auth suppression guard assertions.
    - `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/ApiHealthIntegrationTests.cs`: add or split integration scenarios for failure, recovery, retry-limit, blocked-live, and new-cycle notification behavior.
    - `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/IgAuthenticationResponseSanitizerTests.cs`: extend secret-safety assertions where notification or auth summaries reuse sanitized data.
    - `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/OperationalDataRedactorTests.cs`: verify redaction for notification and blocked-live outputs.
  - **Work Item Dependencies**: Start here before broader UI and infrastructure work because the notification and live-auth gaps are high-risk safety concerns and are best proven at unit and integration level first.
  - **User Instructions**: Keep notification-provider configuration test-friendly during this phase; do not point tests at real live-auth endpoints.

### Work Item 2 details

- [x] Work Item 2: Retry-cycle and trading-schedule lifecycle hardening
  - [x] Build and test baseline established
  - [x] Task 1: Close retry-cycle state-transition gaps
    - [x] Step 1: Extend retry-cycle unit tests to cover degraded startup, transition into periodic retry, manual retry availability, and retry-budget reset after a failed manual retry.
    - [x] Step 2: Assert per-cycle notification state so a manual retry can start a fresh cycle without corrupting the prior cycle state.
    - [x] Step 3: Assert retry progress fields for attempt number, retry mode, and next scheduled retry time.
  - [x] Task 2: Close trading-schedule enforcement gaps
    - [x] Step 1: Add schedule unit tests for remaining weekend and bank-holiday permutations.
    - [x] Step 2: Add integration coverage proving no auth connect or retry activity occurs while the schedule is inactive.
    - [x] Step 3: Assert that out-of-schedule state is observable without being reported as degraded auth failure.
  - [x] Task 3: Remove fragile timing dependence from integration tests
    - [x] Step 1: Replace fixed-delay polling with assertions against public status or event endpoints, or introduce controllable timing inputs where the production design already supports them.
    - [x] Step 2: Keep retry tests bounded and deterministic so they can run reliably in CI.
  - [x] Task 4: Add targeted supporting implementation only if coverage cannot be expressed through current contracts
    - [x] Step 1: Add minimal read-model or state-transition visibility rather than broad refactors.
  - [x] Build and test validation

  - **Files**:
    - `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/AuthRetryCycleTests.cs`: add new-cycle reset, retry-limit, and per-cycle notification assertions.
    - `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/RetryPolicyTimingTests.cs`: extend timing and phase-transition coverage.
    - `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/TradingScheduleGateTests.cs`: add remaining schedule permutations and suppression cases.
    - `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/ApiHealthIntegrationTests.cs`: add degraded-startup, periodic mode, retry reset, and out-of-schedule integration scenarios; remove or reduce fixed-delay polling.
  - **Work Item Dependencies**: Depends on Work Item 1 only where notification-cycle assertions reuse the same transition model; otherwise this can proceed in parallel after baseline stabilization.
  - **User Instructions**: Prefer state-based assertions over elapsed-time assumptions when reviewing or extending retry tests.

### Work Item 3 details

- [x] Work Item 3: Operator UI and browser-level requirement coverage
  - [x] Build and test baseline established
  - [x] Task 1: Strengthen existing functional tests
    - [x] Step 1: Replace raw HTML substring assertions with requirement-driven checks for degraded banner text, blocked reasons, live-option availability state, retry progress fields, and restart-required indicators.
    - [x] Step 2: Keep requirement traceability explicit while using readable `MethodName_StateUnderTest_ExpectedResult` coverage names instead of legacy `002_FRx_point_of_test` methods.
  - [x] Task 2: Add browser-driven UI coverage for operator-critical flows
    - [x] Step 1: Add browser-level tests for degraded-state visibility and auth-dependent action blocking.
    - [x] Step 2: Add browser-level tests for retry-progress visibility and manual-retry control state.
    - [x] Step 3: Add browser-level tests for configuration editing, write-only secret handling, and startup-fixed change messaging.
  - [x] Task 3: Align UI tests with backend contracts
    - [x] Step 1: Assert control states and user-visible outcomes rather than raw markup fragments.
    - [x] Step 2: Reuse API setup flows only where needed to put the UI into a meaningful state.
  - [x] Task 4: Keep tests maintainable
    - [x] Step 1: Use stable selectors or accessible labels and avoid brittle page-text coupling.
    - [x] Step 2: Keep browser coverage narrow and operator-critical.
  - [x] Build and test validation

  - **Files**:
    - `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/002-environment-and-auth-foundation/PlatformOperatorUiFunctionalTests.cs`: replace weak substring checks with requirement-driven assertions.
    - `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/PlatformOperatorUiE2ETests.cs`: convert or supplement `HttpClient`-style checks with browser-driven operator flows.
    - `src/TNC.Trading.Platform.Web/Components/Pages/Status.razor`: add only minimal observable UI hooks if required for stable assertions.
    - `src/TNC.Trading.Platform.Web/Components/Pages/Configuration.razor`: add only minimal observable UI hooks if required for stable assertions.
  - **Work Item Dependencies**: Prefer after Work Items 1 and 2 so the UI tests rely on already-stabilized backend behavior and status contracts.
  - **User Instructions**: Review new browser tests for direct mapping to requirement outcomes, not for markup implementation details.

### Work Item 4 details

- [x] Work Item 4: Durable configuration, audit, rotation, and infrastructure fidelity
  - [x] Build and test baseline established
  - [x] Task 1: Close durable configuration and audit coverage gaps
    - [x] Step 1: Extend persistence and integration tests to assert configuration-audit records for supported updates.
    - [x] Step 2: Add tests proving startup-fixed settings apply on subsequent startup rather than as runtime environment switches.
    - [x] Step 3: Assert that environment-scoped configuration and operational records remain separately retrievable.
  - [x] Task 2: Add secure credential rotation coverage
    - [x] Step 1: Add tests that rotate credentials through the supported write-only flow.
    - [x] Step 2: Assert new auth attempts use updated credentials while prior records remain reviewable and secret-safe.
  - [x] Task 3: Add one high-fidelity infrastructure-on path
    - [x] Step 1: Enable infrastructure containers for one narrow validation suite.
    - [x] Step 2: Assert SQL-backed persistence behavior with real infrastructure rather than only in-memory or disabled-container paths.
    - [x] Step 3: Assert that notification delivery is visible in Mailpit for supported auth or blocked-live transitions.
  - [x] Task 4: Keep the high-fidelity path operationally safe
    - [x] Step 1: Make the infrastructure-on path opt-in or narrowly scoped if runtime cost is materially higher than the baseline suite.
    - [x] Step 2: Preserve lower-level tests as the primary regression signal.
  - [x] Build and test validation

  - **Files**:
    - `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/SqlPlatformConfigurationStoreTests.cs`: add audit, separation, and retrieval assertions.
    - `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/ProtectedCredentialServiceTests.cs`: add credential-rotation behavior and secret-safe history assertions.
    - `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/ApiHealthIntegrationTests.cs`: add next-start application and SQL-backed configuration scenarios, or split them into focused integration classes.
    - `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/PlatformOperatorUiE2ETests.cs`: add end-to-end verification of persisted configuration and operator-visible outcomes where still justified.
    - `src/TNC.Trading.Platform.AppHost/AppHost.cs`: adjust only if a narrow infrastructure-on validation path needs explicit orchestration support.
  - **Work Item Dependencies**: Execute after lower-level safety and lifecycle gaps are closed so the infrastructure-on path validates nearly complete behavior rather than discovering basic contract issues late.
  - **User Instructions**: Run the infrastructure-on path only with the required local dependencies available and confirm Mailpit visibility manually if the first automation slice remains read-only.

## Cross-cutting validation

- **Build**: `dotnet build`
- **Unit tests**: `dotnet test test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests && dotnet test test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests && dotnet test test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests`
- **Integration tests**: `dotnet test test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests`
- **Functional tests**: `dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests`
- **E2E tests**: `dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests`
- **Manual checks**:
  - Run one infrastructure-on validation path with SQL Server and Mailpit enabled.
  - Verify that a blocked live attempt produces a recorded event and a visible notification outcome without secrets.
  - Verify that a failed manual retry starts a new retry cycle and that the next failure emits a fresh notification for that cycle.
  - Verify that out-of-schedule state suppresses broker connection and retry activity while remaining visible in the operator surface.
  - Verify that configuration changes requiring restart are communicated as next-start changes rather than runtime environment switches.
- **Security checks**:
  - Inspect notification content, event payloads, API responses, UI output, and persisted records for redaction of credentials, tokens, API keys, and passwords.
  - Verify that blocked live paths fail before any live-auth request is issued.
  - Verify that credential rotation changes future auth behavior without disclosing stored or historical secrets.

## Acceptance checklist

- [x] Every planned mitigation maps back to one or more findings in `../work-package-test-review-report.md`.
- [x] High-priority missing or weak coverage is addressed before lower-priority improvements.
- [x] The plan prefers lower-level automated tests before higher-level tests where practical.
- [x] Required validation steps are defined for each work item.
- [x] Docs updated under `./docs/002-environment-and-auth-foundation/`.
- [x] Rollback/backout plan documented for each work item.

## Notes

- Confirmed evidence from the repository includes weak UI substring checks in `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/002-environment-and-auth-foundation/PlatformOperatorUiFunctionalTests.cs`, `HttpClient`-driven E2E checks in `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/PlatformOperatorUiE2ETests.cs`, and fixed-delay polling in `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/ApiHealthIntegrationTests.cs`.
- Confirmed lower-level strength exists in the current secret-handling and retry/schedule unit coverage, including `IgAuthenticationResponseSanitizerTests.cs`, `OperationalDataRedactorTests.cs`, `ProtectedCredentialServiceTests.cs`, `AuthRetryCycleTests.cs`, `RetryPolicyTimingTests.cs`, and `TradingScheduleGateTests.cs`.
- Final implementation evidence for Work Item 4 now includes audit and restart assertions in `SqlPlatformConfigurationStoreTests.cs`, credential-rotation assertions in `ProtectedCredentialServiceTests.cs`, infrastructure-on SQL Server and Mailpit validation in `ApiHealthIntegrationTests.cs`, and persisted operator outcomes in `PlatformOperatorUiE2ETests.cs`.
- Deterministic coverage for the startup-fixed integration path now pins the test time provider to an in-schedule instant so validation is not coupled to wall-clock execution time.
- Assumption: the disabled-container test setting (`AppHost__EnableInfrastructureContainers=false`) means current higher-level tests are not exercising real SQL Server or Mailpit behavior. This is strongly suggested by naming and the review report, but should be re-checked when implementing Work Item 4.
- Assumption: a narrow infrastructure-on validation slice is feasible without changing the repository delivery model. If that assumption proves false, keep Work Item 4 focused on opt-in local validation rather than broad CI expansion.
