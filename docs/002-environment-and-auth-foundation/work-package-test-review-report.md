# Work Package Test Review Report

> Use this template to review a work package under `./docs/00x-work/` and assess whether its current automated tests provide sufficient coverage, strength, and confidence. Ground findings in repository evidence and separate confirmed gaps from assumptions.

## Review scope

- **Work package**: `./docs/002-environment-and-auth-foundation/`
- **Review depth**: `standard`
- **Reviewer perspective**: `Senior Test Architect`
- **Reviewed artifacts**:
  - `docs/002-environment-and-auth-foundation/requirements.md`
  - `docs/002-environment-and-auth-foundation/technical-specification.md`
  - `docs/002-environment-and-auth-foundation/delivery-plan.md`
  - `src/TNC.Trading.Platform.Api/...`
  - `src/TNC.Trading.Platform.Web/...`
  - `test/TNC.Trading.Platform.Api/...`
  - `test/TNC.Trading.Platform.Application/...`
  - `test/TNC.Trading.Platform.Infrastructure/...`
  - `test/TNC.Trading.Platform.Web/...`

## Executive summary

- **Overall test confidence**: `low`
- **Overall coverage assessment**: `partial`
- **Top concerns**:
  1. `Notification, blocked-live, and retry-cycle behaviors required by FR10/FR11/FR17/FR19 are largely unverified.`
  2. `UI-oriented functional/E2E coverage is very thin and mostly checks raw HTML substrings rather than user interactions or state transitions.`
  3. `Current higher-level tests appear to avoid real infrastructure and rely on polling/string checks, leaving SQL Server, Mailpit, and end-to-end behavior under-tested.`

## Requirement coverage matrix

| Requirement / area | Existing coverage | Evidence | Gap assessment | Recommendation |
| --- | --- | --- | --- | --- |
| `TR1 / FR1-FR3 environment selection, visibility, separation` | Partial | `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/ApiHealthIntegrationTests.cs` - `StartupConfiguration_IsVisibleInPlatformStatus`; `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/UpdatePlatformConfigurationValidatorTests.cs` - `Validate_WithTestPlatformAndLiveBroker_ThrowsPlatformValidationException`; `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/SqlPlatformConfigurationStoreTests.cs` | Visibility is checked, but explicit-selection-required startup failure, environment-scoped record separation, and demo/live isolation of operational history are not demonstrated. | Add unit/integration tests for missing broker environment at startup, environment-tagged event persistence, and distinct retrieval of demo vs live-scoped records/config. |
| `TR2 / FR4-FR6, FR12, FR14-FR16 auth lifecycle and retry behavior` | Partial | `ApiHealthIntegrationTests.PlatformConfigurationEndpoints_HideSecretsAndAcceptUpdates`; `ApiHealthIntegrationTests.ExpiredActiveSession_IsRecordedAndRecovered`; `ApiHealthIntegrationTests.ManualRetryEndpoint_BecomesAvailableAfterRetryExhaustion`; `RetryPolicyTimingTests`; `AuthRetryCycleTests`; `TradingScheduleGateTests` | Session activation, expiry, recovery, delay math, and retry exhaustion are touched. Missing: degraded startup assertions, transition into periodic retry, retry suppression outside schedule, and FR16 retry-budget reset after failed manual retry. | Add focused unit tests for retry-cycle reset logic and integration tests for degraded startup, periodic mode transition, and resumed automatic retries after failed manual retry. |
| `TR3 / FR7, SR2, SR3 secrets protection` | Covered / Partial | `PlatformOperatorUiFunctionalTests.cs` functional coverage for `FR7` (current method still uses legacy `_002_FR7_point_of_test` naming); `ApiHealthIntegrationTests.PlatformEvents_ReturnRedactedAuthDetails`; `IgAuthenticationResponseSanitizerTests`; `OperationalDataRedactorTests`; `ProtectedCredentialServiceTests` | Secret redaction is the strongest area. Still missing are credential-rotation scenarios and broader inspection of notifications/audit payloads. | Keep current tests, then add credential rotation and configuration-audit assertions to close SR3 gaps. |
| `TR4 / FR8, SR4, IR3 test-platform live restriction` | Partial | `UpdatePlatformConfigurationValidatorTests.Validate_WithTestPlatformAndLiveBroker_ThrowsPlatformValidationException`; UI renders live availability in `src/TNC.Trading.Platform.Web/Components/Pages/Status.razor` | Current coverage only proves validator rejection. It does not prove the live option is visible-but-disabled in UI, blocked attempts are recorded, or per-attempt behavior is enforced. | Add API/UI tests for visible-but-blocked live option and integration tests that verify blocked attempts create records. |
| `TR5 / FR9, SR1 no live authentication attempts` | Missing | No test found that asserts no live-auth call is issued; current evidence is indirect validator coverage only. | This is a core safety requirement and is currently not proven. | Add unit tests around command guards and integration tests with an IG test double proving live-auth is never invoked. |
| `TR6 / FR10, NF5, SR5, IR4 auth/session notifications` | Missing | Only provider behavior is covered in `NotificationProviderTests`; no state-transition notification assertions found in integration/functional suites. | Failure/recovery notifications, payload shape, timing, and secret safety are unverified. | Add unit tests for notification mapping and integration tests asserting failure and recovery notifications are recorded/dispatched with required fields. |
| `TR7 / FR11, NF5, SR5, IR4 blocked live-attempt notifications` | Missing | No test found. | High-risk safety signal is absent from automation. | Add integration tests that trigger blocked live attempts and assert one notification per attempt with environment context and no secrets. |
| `TR8 / FR13, NF2 degraded-state UI behavior` | Partial | `PlatformOperatorUiFunctionalTests.cs` functional coverage for `FR13` (current method still uses legacy `_002_FR13_point_of_test` naming); `Status.razor` shows degraded messaging and blocked reason | The functional test only checks that `/status` HTML contains `"Auth state"`; it does not assert degraded state, blocked actions, or visible reason text. | Replace substring-only checks with browser-level assertions for degraded banner, blocked reason, and manual-retry control state. |
| `TR9 / FR17 re-notification after manual retry starts new cycle` | Missing | No test found. | A required notification-cycle behavior is uncovered. | Add unit tests for per-cycle notification state and integration tests for fresh failure notification after failed manual retry. |
| `TR10 / FR18, NF2 retry progress visibility` | Partial | `Status.razor` renders retry phase, attempt number, and next retry; `ApiHealthIntegrationTests.ManualRetryEndpoint_BecomesAvailableAfterRetryExhaustion` checks `retryLimitReached` and `manualRetryAvailable` | UI/API display of current attempt number and next retry time is not asserted end-to-end. Periodic mode visibility is not proven. | Add API contract tests and UI tests for automatic attempt count, periodic mode label, next scheduled retry time, and recovery clearing the values. |
| `TR11 / FR19, NF5, SR5, IR4 retry-limit notification content` | Missing | No test found. | Required content fields such as manual-retry guidance, last delay, and periodic delay are not verified. | Add notification content tests at unit level, then integration tests for retry-limit events. |
| `TR12 / FR20, NF2, NF4, SR2, SR3, IR5 durable config management and secure credential updates` | Partial | `ApiHealthIntegrationTests.PlatformConfigurationEndpoints_HideSecretsAndAcceptUpdates`; `PlatformOperatorUiFunctionalTests.cs` functional coverage for `FR20` (current method still uses legacy `_002_FR20_point_of_test` naming); `PlatformOperatorUiE2ETests.ConfigurationUpdates_AreReflectedInTheOperatorUi`; `SqlPlatformConfigurationStoreTests`; `ProtectedCredentialServiceTests` | Review/update flow and secret masking are covered at a basic level. Missing: audit trail assertions, startup-fixed changes applying on next restart, and proof against real SQL-backed infrastructure. | Add integration tests for configuration audit records, restart-required behavior, and one high-fidelity SQL-backed scenario. |
| `TR13 / FR21-FR22, NF1, NF2 trading schedule and connection-window behavior` | Partial | `TradingScheduleGateTests`; `PlatformOperatorUiE2ETests.TradingScheduleInactivity_IsVisibleInTheOperatorUi` | Bank-holiday inactivity is covered, but weekend permutations, schedule-bound connection stop/suppress behavior, retry suppression outside schedule, and current in/out-of-schedule observability are not fully proven. | Add unit coverage for remaining schedule permutations and integration tests for no-connect/no-retry behavior outside active schedule. |

## Existing test strengths

- Secret-handling coverage is comparatively good across unit, integration, and functional levels:
  - `IgAuthenticationResponseSanitizerTests`
  - `OperationalDataRedactorTests`
  - `ProtectedCredentialServiceTests`
  - `ApiHealthIntegrationTests.PlatformEvents_ReturnRedactedAuthDetails`
  - `PlatformOperatorUiFunctionalTests.cs` functional coverage for `FR7` (current method still uses legacy `_002_FR7_point_of_test` naming)
- Retry timing and trading-schedule rules have lower-level coverage, which is the right direction for deterministic behavior:
  - `RetryPolicyTimingTests`
  - `TradingScheduleGateTests`
  - `AuthRetryCycleTests`
- The repo has started requirement-traceable functional tests under the correct work-package folder:
  - `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/002-environment-and-auth-foundation/PlatformOperatorUiFunctionalTests.cs`
  - The current functional method names still use the legacy `_002_FRx_point_of_test` pattern and should move to readable `MethodName_StateUnderTest_ExpectedResult` names while keeping `FRx` traceability outside the method name.
- Current test inventory is executable and passing in the workspace; `get_tests` returned 32 matching tests, all passed.

## Gaps in testing

### Missing coverage

- No confirmed tests for `FR10`, `FR11`, `FR17`, or `FR19` notification behavior.
- No confirmed test for `FR9` proving live authentication is never attempted.
- No confirmed test for `FR16` automatic retry-budget reset after a failed manual retry.
- No confirmed test for environment-scoped operational record separation required by `FR3`.
- No confirmed test for configuration audit persistence and reviewability required by `FR20`/`DR1`.
- No confirmed test for credential rotation behavior required by `SR3`.
- No confirmed test for out-of-schedule retry suppression and connection stop behavior required by `FR22`.

### Weak or fragile tests

- The legacy-named `PlatformOperatorUiFunctionalTests` method for `FR13` only checks for `"Auth state"` in returned HTML, which is too weak to prove degraded-state usability or blocked auth-dependent actions.
- The legacy-named `PlatformOperatorUiFunctionalTests` method for `FR20` only checks for `"Stored values are never shown"`, which does not verify persistence, validation, restart-required behavior, or write-only update semantics.
- Legacy `_002_FRx_point_of_test` method names reduce readability and should be renamed to `MethodName_StateUnderTest_ExpectedResult` while keeping requirement traceability in folders, metadata, or supporting docs.
- `PlatformOperatorUiE2ETests` is not browser-driven; it uses `HttpClient` and substring checks instead of exercising actual UI interaction paths.
- `ApiHealthIntegrationTests.WaitForConditionAsync` uses polling with `Task.Delay(100)` (`ApiHealthIntegrationTests.cs:387-398`), which is a flake risk and conflicts with repo guidance against time-based waits as a primary strategy.
- Several unit suites use reflection heavily (`ApiReflection`, `ApplicationReflection`, `InfrastructureReflection`), which weakens compile-time safety and makes tests rename/signature brittle.

### Risks not adequately tested

- Safety risk of accidentally reaching live-auth code paths.
- Notification correctness risk: missing or malformed failure/recovery/retry-limit/blocked-live messages.
- Operational drift risk between UI expectations and backend state transitions during degraded and periodic-retry modes.
- Persistence fidelity risk for SQL-backed configuration/audit behavior.
- Schedule-gating risk around weekends, bank holidays, and retry suppression outside the trading window.
- Regression risk in manual retry cycle behavior, especially budget reset and per-cycle notification semantics.

## Recommendations to strengthen existing tests

1. Strengthen the existing functional/UI tests so they assert requirement outcomes, not static HTML presence. For example, verify degraded banners, blocked-reason text, disabled/enabled retry controls, and visible retry progress.
2. Strengthen integration tests around retry behavior by asserting explicit state transitions (`degraded`, `initial automatic`, `periodic`, `recovered`) and event payload fields rather than only checking that events exist.
3. Strengthen configuration tests to assert audit persistence, restart-required semantics, and secure credential update behavior beyond simple presence flags.

## Recommendations for new tests

| Priority | Area | Test level | Recommendation | Reason |
| --- | --- | --- | --- | --- |
| High | Notification state transitions | Unit | Add mapper/content tests for failure, recovery, blocked-live, retry-limit, and new-manual-cycle failure notifications. | Fastest way to lock down required payload fields and secret safety. |
| High | Notification dispatch behavior | Integration | Add AppHost/API tests that trigger auth failure, recovery, retry exhaustion, and blocked live attempts, then assert notification records and summaries. | FR10/FR11/FR17/FR19 are currently unproven. |
| High | Live-auth suppression | Unit / Integration | Add tests proving live-auth paths are blocked before any IG call is made. | This is a core safety requirement with no direct evidence today. |
| High | Manual retry cycle reset | Unit / Integration | Add tests for FR16 so a failed manual retry reopens a fresh automatic cycle with the same policy. | Current manual-retry coverage stops at availability and 202 response. |
| High | Degraded-state operator UX | Functional | Add requirement-driven tests for degraded banner, blocked reason, manual-retry button state, and retry progress visibility. | Current FR13 functional evidence is too weak. |
| Medium | Trading-schedule suppression | Integration | Add tests for out-of-schedule no-connect/no-retry behavior, weekend treatment permutations, and bank-holiday handling. | FR22 needs stronger system-level proof. |
| Medium | Configuration auditing and restart semantics | Integration | Add tests that verify persisted audit records and next-start behavior for startup-fixed settings. | FR20/DR1/SR3 are only partially covered. |
| Medium | Browser-driven UI flows | Functional / E2E | Add Playwright-based tests for configuration editing and status-page behavior. | Current UI suites do not validate actual user interaction or control behavior. |
| Medium | High-fidelity infrastructure | Integration / E2E | Add at least one suite that runs with real infrastructure enabled and validates SQL-backed persistence plus Mailpit notification visibility. | Current tests appear to opt out of infrastructure containers, reducing confidence in delivery-plan commitments. |

## Hardening recommendations

- Keep the bulk of coverage at unit and focused integration level, then add only a small number of browser/E2E tests for operator-critical flows.
- Replace substring-only HTML assertions with semantic assertions against state-bearing fields or browser-visible elements.
- Avoid polling loops with fixed delays as the main readiness strategy; prefer asserting externally visible state transitions exposed by the API.
- Add a requirement-to-test traceability list for `FR1`-`FR22` so uncovered requirements are explicit in PR review.
- Add one high-fidelity CI path that does not disable infrastructure containers for SQL/Mailpit validation.
- Use unique test data or explicit cleanup for persisted records if higher-fidelity infrastructure is introduced.

## Assumptions and missing information

- Tests in `ApiHealthIntegrationTests`, `PlatformOperatorUiFunctionalTests`, and `PlatformOperatorUiE2ETests` set `AppHost__EnableInfrastructureContainers=false`; this strongly suggests current higher-level tests do not validate real containerized infrastructure. The exact effect of that flag is inferred from its name.
- No automated evidence was found for the manual Mailpit validation step called out in `docs/002-environment-and-auth-foundation/delivery-plan.md`.
- This review is based on repository evidence only; it does not assume undocumented manual checks.

## Suggested next steps

1. Add unit and integration coverage for notification behavior, live-auth suppression, and manual-retry cycle reset before adding more E2E tests.
2. Replace the current weak UI substring tests with requirement-traceable browser-driven tests for degraded-state and configuration-management flows.
3. Add one infrastructure-on validation path for SQL Server and Mailpit to raise confidence from partial to medium.
