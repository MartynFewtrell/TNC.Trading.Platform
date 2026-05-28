# Work Package Test Review Report Template

> Project-wide test review adapted from the repository scaffold. Evidence is limited to the files inspected in the current workspace.

## Review scope

- **Work package**: `Entire project (repository root)`
- **Review depth**: `standard`
- **Reviewer perspective**: `Senior Test Architect`
- **Output path**: `./docs/005-refactor-app-host/001-project-test-review-report.md`
- **Reviewed artifacts**:
  - `README.md`
  - `docs/README.md`
  - `docs/business-requirements.md`
  - `docs/systems-analysis.md`
  - `docs/wiki/README.md`
  - `docs/wiki/testing-and-quality.md`
  - `docs/003-authentication-and-authorisation/requirements.md`
  - `docs/004-ui-update-and-refactor/requirements.md`
  - `docs/004-ui-update-and-refactor/plans/001-delivery-plan.md`
  - `.github/templates/test-review-report.template.md`
  - `.github/copilot-instructions.md`
  - `.github/instructions/tests.instructions.md`
  - `.github/instructions/aspire-tests.instructions.md`
  - `.github/instructions/playwright.instructions.md`
  - `global.json`
  - `src/TNC.Trading.Platform.AppHost/AppHost.cs`
  - `src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs`
  - `src/TNC.Trading.Platform.Web/PlatformApiClient.cs`
  - `src/TNC.Trading.Platform.Web/Components/Layout/MainLayout.razor`
  - `src/TNC.Trading.Platform.Web/Components/Pages/Home.razor`
  - `src/TNC.Trading.Platform.Web/Components/Pages/Status.razor`
  - `src/TNC.Trading.Platform.Web/Components/Pages/Configuration.razor`
  - `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/ApplicationReflection.cs`
  - `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/RetryPolicyTimingTests.cs`
  - `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/TradingScheduleGateTests.cs`
  - `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/AuthRetryCycleTests.cs`
  - `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/InfrastructureReflection.cs`
  - `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/ProtectedCredentialServiceTests.cs`
  - `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/OperationalDataRedactorTests.cs`
  - `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/PlatformApiAuthenticationServiceCollectionExtensionsTests.cs`
  - `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/UpdatePlatformConfigurationValidatorTests.cs`
  - `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/PlatformAuthenticationIntegrationTests.cs`
  - `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/PlatformWebAuthenticationServiceCollectionExtensionsTests.cs`
  - `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/PlatformThemeStateTests.cs`
  - `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/PlatformAccessTokenProviderTests.cs`
  - `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/PlatformShellContextProviderTests.cs`
  - `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/PlatformAuthAuditClientTests.cs`
  - `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/Authentication/AuthenticationFunctionalTestCollection.cs`
  - `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/Authentication/FunctionalBrowserClientFactory.cs`
  - `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/Authentication/RealAppHostProcessFactory.cs`
  - `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/Authentication/RealAuthenticationSessionFactory.cs`
  - `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/Authentication/PlatformAuthenticationFunctionalTests.cs`
  - `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/Authentication/PlatformProtectedRouteFunctionalTests.cs`
  - `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/Authentication/AuthenticationE2ETestCollection.cs`
  - `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/Authentication/AppHostProcessHandle.cs`
  - `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/Authentication/PlatformAuthenticationE2ETests.cs`
  - `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/Authentication/PlatformDashboardAuthenticationE2ETests.cs`

## Executive summary

- **Overall test confidence**: `medium`
- **Overall coverage assessment**: `partial`
- **Top concerns**:
  1. `F1` The Web test pyramid is too expensive for the current scope: many auth behaviors are revalidated through real AppHost, Keycloak, Playwright, and process-level orchestration instead of lower-level tests.
  2. `F2` Several unit suites depend on reflection-based access to non-public implementation details, which weakens refactor safety and test maintainability.
  3. `F3` Coverage is strong for authentication/control-plane behavior, but weaker for UI refresh behavior, API negative contracts, and project-wide traceability.

The current repository has good security-focused coverage for the implemented control plane: policy registration, protected API/UI access, role boundaries, audit events, credential protection, and secret redaction are all exercised across unit, integration, functional, and E2E layers. The main quality risk is not absence of tests around authentication; it is imbalance. The suite spends substantial runtime budget on real-runtime auth validation while leaving lower-cost gaps in UI component behavior and API edge cases.

## Requirement coverage matrix

| Requirement / area | Existing coverage | Evidence | Gap assessment | Recommendation |
| --- | --- | --- | --- | --- |
| Authentication and authorization enforcement across API and Blazor UI | Covered | `test/TNC.Trading.Platform.Api/.../PlatformAuthenticationIntegrationTests.cs`, `test/TNC.Trading.Platform.Web/.../PlatformProtectedRouteFunctionalTests.cs`, `test/TNC.Trading.Platform.Web/.../PlatformAuthenticationE2ETests.cs`, `test/TNC.Trading.Platform.Web/.../PlatformWebAuthenticationServiceCollectionExtensionsTests.cs` | Strong for anonymous/authenticated/role paths. Cost is high because the same behavior is repeated at multiple higher levels. | Reduce duplication by moving more navigation/scope logic to unit/component tests and keep only a narrow real-runtime smoke path (`F1`). |
| Session lifecycle: sign-in, sign-out, session loss, re-authentication | Covered | `PlatformAuthenticationFunctionalTests.cs` covers sign-out, post-sign-out denial, session loss recovery; `PlatformAuthenticationE2ETests.cs` and `PlatformDashboardAuthenticationE2ETests.cs` cover browser-level sign-in/out | Positive and fail-closed paths are well represented. Latest-session expiry behavior still relies on real-runtime paths. | Add lower-level tests for session/cookie/token handling helpers and keep one browser smoke per flow (`F1`, `F3`). |
| Configuration validation, operator access, and secret handling | Partial | `UpdatePlatformConfigurationValidatorTests.cs`, `ProtectedCredentialServiceTests.cs`, `OperationalDataRedactorTests.cs`, `PlatformAuthenticationIntegrationTests.cs` (`ConfigurationEndpoint_ShouldReturnOk_WhenOperatorUpdatesConfiguration`) | Validation exists at validator and happy-path integration levels, but API-level negative response contracts are thin. No direct lower-level tests were found for `PlatformApiClient` error behavior. | Add unit/integration tests for invalid configuration requests through `/api/platform/configuration`, and unit tests for `PlatformApiClient` response/error mapping (`F3`). |
| Auth supervision, retry scheduling, degraded-state behavior, and trading schedule gates | Partial | `AuthRetryCycleTests.cs`, `RetryPolicyTimingTests.cs`, `TradingScheduleGateTests.cs`, `Status.razor` | Application logic is well covered, but UI rendering and operator interactions for status/manual retry are only lightly automated. | Add component/functional tests for status page sections, degraded warnings, retry button visibility/disabled states, and message rendering (`F3`). |
| UI shell, theme behavior, and WP004 usability changes | Partial | `PlatformThemeStateTests.cs`, `PlatformShellContextProviderTests.cs`, `MainLayout.razor`, `Home.razor`, `Status.razor`, and `Configuration.razor` | Theme parsing/state has unit coverage, but no dedicated component suite was identified for sidebar collapse, header environment indicator, accordion defaults, preserved edits, or signed-in/signed-out shell transitions. | Add lower-level Blazor component tests for refreshed pages and layout before adding more E2E coverage (`F1`, `F3`). |
| Observability, audit trail, and secret-safe auth event recording | Covered | `PlatformAuthenticationIntegrationTests.cs` audit-event tests, `PlatformAuthAuditClientTests.cs`, `OperationalDataRedactorTests.cs`, `PlatformEndpoints.cs` | Positive audit paths are covered. Negative API contract for unsupported audit event types was not found. | Add tests for unsupported `EventType`, username fallback logic, and validation-problem payload shape on `/api/platform/auth/audit` (`F3`). |
| Distributed AppHost / Aspire local-runtime validation | Partial | `PlatformAuthenticationFunctionalTests.cs`, `PlatformAuthenticationE2ETests.cs`, `PlatformDashboardAuthenticationE2ETests.cs`, `AppHostProcessHandle.cs`, `RealAppHostProcessFactory.cs`, `AppHost.cs` | Real-runtime coverage exists, but it is slow, mostly serialized, and partly driven by custom process/polling helpers instead of shared fixtures. | Consolidate to fewer smoke tests, reuse fixtures/sessions, and prefer `Aspire.Hosting.Testing` where possible (`F1`). |
| Project-wide traceability and testing documentation accuracy | Partial | `docs/wiki/testing-and-quality.md:105-115` still references current traceability mainly through `docs/002-environment-and-auth-foundation/requirements.md`; tests now span 003/004 and some use `regression` comments | Documentation and some test comments do not yet present a clean repository-wide traceability view. | Update the wiki and introduce a maintained project-wide test traceability matrix in docs (`F4`). |
| Future trading-domain capabilities: IG live auth, market data, orders, strategies, risk controls, reconciliation | Missing | `README.md:21-26` lists these capabilities as not implemented | Missing coverage is expected today, but the project has no visible test strategy artifact for these higher-risk business areas yet. | Define a forward test strategy per area early, with unit-first seams and contract-level tests before implementation begins. |

## Existing test strengths

- Test naming is mostly consistent with the repository convention and readable, for example `GetAccessTokenAsync_ShouldThrowScopeChallenge_WhenAccessTokenIsExpired` and `ConfigurationRoute_ShouldRedirectToSignIn_WhenAnonymousUserRequestsProtectedRoute`.
- Many tests include useful XML comments with traceability and rationale, especially in API, Web auth, and application suites.
- Security-sensitive behavior is a clear strength:
  - credential-at-rest protection in `ProtectedCredentialServiceTests.cs`
  - secret redaction in `OperationalDataRedactorTests.cs`
  - invalid token rejection and role-boundary enforcement in `PlatformAuthenticationIntegrationTests.cs`
- The repository has meaningful coverage across multiple levels rather than relying only on browser tests.
- Real-runtime auth coverage avoids fixed-port assumptions and uses listener discovery in `AppHostProcessHandle.cs`, which is better than static launch-settings coupling.
- Unit coverage around retry timing and schedule logic is specific and deterministic.
- The AppHost composition root is thin (`src/TNC.Trading.Platform.AppHost/AppHost.cs`), which supports clearer integration boundaries.

## Gaps in testing

### Missing coverage

- `F3` No direct tests were found for negative API contract branches in `src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs`, including:
  - unsupported auth audit event types returning validation problems
  - `ResolveUserName` fallback behavior
  - response-body contract for invalid `/api/platform/configuration` requests at the HTTP boundary
- `F3` No dedicated lower-level tests were found for `src/TNC.Trading.Platform.Web/PlatformApiClient.cs`, despite it being the main Web-to-API boundary for status, configuration, events, manual retry, and auth administration.
- `F3` WP004 UI behavior is only partially automated. Evidence reviewed for `MainLayout.razor`, `Home.razor`, `Status.razor`, and `Configuration.razor` shows behavior such as:
  - sidebar collapse state
  - header environment indicator
  - accordion defaults
  - preserved in-progress configuration edits
  - manual retry button state  
  but the inspected Web tests cover theme state and shell environment caching rather than these rendered behaviors.
- Project-wide business risk areas such as market data freshness, order lifecycle, risk controls, reconciliation, and run controls remain untested because they are not implemented yet (`README.md:21-26`, `docs/business-requirements.md`, `docs/systems-analysis.md`).

### Weak or fragile tests

- `F1` The Web functional and E2E suites are costly and potentially fragile because many tests:
  - start a real `dotnet run` AppHost process (`RealAppHostProcessFactory.cs`, `PlatformAuthenticationE2ETests.cs`, `PlatformDashboardAuthenticationE2ETests.cs`)
  - depend on real Keycloak sign-in
  - use serialized collections (`AuthenticationFunctionalTestCollection.cs`, `AuthenticationE2ETestCollection.cs`)
  - poll for readiness/session establishment with repeated `Task.Delay` loops (`AppHostProcessHandle.cs`, `RealAuthenticationSessionFactory.cs`, `PlatformAuthenticationIntegrationTests.cs`)
- `F2` Reflection-heavy unit tests reduce maintainability and compile-time safety. `ApplicationReflection.cs` and `InfrastructureReflection.cs` construct and invoke non-public members by string. This makes tests brittle under internal renames and weakens refactor guidance.
- `F4` Traceability is uneven. Some tests use explicit requirement IDs, while others use only `Trace: regression`, for example in `PlatformThemeStateTests.cs` and `PlatformShellContextProviderTests.cs`. That is acceptable for regression notes, but repository-wide intent becomes harder to audit.
- `F4` `docs/wiki/testing-and-quality.md` is stale as a project-wide traceability source. It states that many current tests map to `docs/002-environment-and-auth-foundation/requirements.md`, which no longer reflects the repository-wide state.

### Risks not adequately tested

- `F3` UI regression risk for WP004 remains under-protected. Manual validation is explicitly relied on in `docs/wiki/testing-and-quality.md:156-165` and `docs/004-ui-update-and-refactor/requirements.md:180-183`.
- `F3` Error-handling risk remains around Web client behavior because `PlatformApiClient` currently uses `EnsureSuccessStatusCode()` for all calls, but no inspected tests verify operator-facing behavior when the API returns validation errors, `409 Conflict`, or malformed payloads.
- `F1` CI/runtime stability risk is elevated by duplicate high-level auth coverage across functional and E2E suites.
- `F2` Refactor risk is elevated in application/infrastructure unit suites because reflection hides compile-time breaks until runtime string lookup.

## Slow-running or flaky tests, suspected causes, and mitigation strategies

- `F1` **Authentication functional tests**  
  Evidence: `PlatformAuthenticationFunctionalTests.cs`, `RealAppHostProcessFactory.cs`, `RealAuthenticationSessionFactory.cs`, `AuthenticationFunctionalTestCollection.cs`.  
  Suspected causes:
  - full AppHost startup
  - real Keycloak authentication
  - Playwright session bootstrapping inside functional tests
  - disabled parallelization  
  Mitigation:
  - keep only a small set of real-runtime functional flows
  - reuse one authenticated browser/session fixture per collection where isolation allows
  - move route/scope/redirect logic into lower-level tests

- `F1` **Authentication E2E tests**  
  Evidence: `PlatformAuthenticationE2ETests.cs`, `PlatformDashboardAuthenticationE2ETests.cs`, `AppHostProcessHandle.cs`, `AuthenticationE2ETestCollection.cs`.  
  Suspected causes:
  - separate AppHost process per test
  - listener discovery polling
  - repeated real sign-in journeys
  - serialized execution  
  Mitigation:
  - reduce to smoke coverage for sign-in, sign-out, and one protected-route role boundary
  - share AppHost lifecycle across tests
  - centralize authenticated storage state/session reuse instead of repeating full login

- `F1` **API integration auth tests**  
  Evidence: `PlatformAuthenticationIntegrationTests.cs` repeatedly builds and starts `DistributedApplicationTestingBuilder`, plus `WaitForApiReadinessAsync` polling.  
  Suspected causes:
  - repeated distributed app startup for each test
  - real token acquisition for positive auth cases  
  Mitigation:
  - introduce collection fixtures/app reuse for grouped auth scenarios
  - keep synthetic negative JWT cases isolated, but batch real-runtime positive cases against one shared app instance

These tests are not using arbitrary sleeps as the primary assertion strategy, which is good. The issue is repeated polling and environment startup cost, not gross anti-patterns.

## Recommendations to strengthen existing tests

1. **Address `F1` by rebalancing the Web pyramid.** Add lower-level tests for navigation, token/scope decisions, page rendering, and API client behavior; then trim duplicate functional/E2E auth scenarios to a small smoke set.
2. **Address `F2` by replacing reflection-driven tests with typed seams where practical.** Prefer `internal` types plus `InternalsVisibleTo`, or public abstractions around core policies/services, so refactors break at compile time instead of runtime string lookup.
3. **Address `F3` and `F4` by tightening API/UI edge coverage and traceability.** Add explicit tests for negative HTTP contracts, update stale test documentation, and replace `regression`-only comments with requirement/risk references when the behavior maps to a documented area.

## Recommendations for new tests

| Priority | Area | Test level | Recommendation | Reason |
| --- | --- | --- | --- | --- |
| High | Web layout and refreshed UI behavior | Unit / Functional | Add Blazor component tests for `MainLayout`, `Home`, `Status`, and `Configuration` covering sidebar visibility, environment indicator, accordion default state, manual retry control state, and preserved edit behavior. | Closes `F3` with cheaper coverage than more E2E tests. |
| High | Web-to-API boundary | Unit | Add `PlatformApiClient` tests for success parsing, `409 Conflict`, validation errors, unauthorized/forbidden responses, and empty payload handling. | Important operator-facing boundary currently lacks direct tests (`F3`). |
| High | API negative contracts | Integration | Add HTTP-level tests for unsupported auth audit event types, invalid configuration payloads returning validation problems, and auth username fallback behavior. | Covers untested branches in `PlatformEndpoints.cs` (`F3`). |
| Medium | Application and infrastructure core logic | Unit | Refactor reflection-based tests toward typed access and add assertions through public/internal contracts instead of string-invoked members. | Improves maintainability and refactor confidence (`F2`). |
| Medium | Distributed auth runtime | Functional / E2E | Consolidate browser/runtime coverage to one sign-in smoke, one sign-out smoke, and one role-boundary smoke using shared fixtures. | Reduces runtime and flake exposure without losing confidence (`F1`). |
| Medium | Traceability and documentation | Functional / Documentation support | Add a maintained project-wide coverage map in docs and ensure tests for WP003/WP004 use explicit requirement/risk references where applicable. | Fixes stale project-wide traceability (`F4`). |

## Hardening recommendations

- Introduce shared collection fixtures for AppHost-backed auth tests so one runtime can serve multiple related assertions safely.
- Track per-suite duration in CI and set explicit budgets for unit, integration, functional, and E2E layers.
- Keep synthetic invalid-token cases isolated, but avoid re-running the same positive auth matrix at both functional and E2E levels.
- Prefer semantic assertions over status-only assertions where contract details matter, especially for validation responses and audit event payloads.
- Update `docs/wiki/testing-and-quality.md` to reflect current repository scope, including 003/004 coverage and current suite responsibilities.
- For tests that remain tagged as `regression`, add a brief risk note or feature-area reference so intent stays stable after future work packages.

## Assumptions and missing information

- Direct refresh against Microsoft Learn and `aspire.dev` could not be retrieved with the available toolset in this session, so external-guidance confirmation is based on repository instruction files and referenced documentation rather than freshly downloaded source pages.
- No CI telemetry, historical flake data, or suite-duration trend data was available in the reviewed workspace, so flakiness and runtime findings are static-analysis based.
- The latest numbered work package folder identified in `./docs/` is `./docs/004-ui-update-and-refactor/`.
- Existing report files were found under `docs/003-authentication-and-authorisation/`, and none were identified for project-wide test review under `docs/004-ui-update-and-refactor/`.

## Suggested next steps

1. Implement the three highest-value lower-level additions first: `PlatformApiClient` tests, API negative-contract integration tests, and Blazor component tests for WP004 surfaces.
2. Refactor AppHost-backed functional/E2E auth suites to shared fixtures and reduce duplicate browser flows to a smoke set.
3. Update `docs/wiki/testing-and-quality.md` with a repository-wide traceability matrix and current suite ownership, then align test comments where traceability is currently only marked as `regression`.
