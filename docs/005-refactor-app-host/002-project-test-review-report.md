# Project Test Review Report

This report is saved as `docs/005-refactor-app-host/002-project-test-review-report.md`.

## Review scope

- **Work package**: `Entire project (repository root)`
- **Review depth**: `standard`
- **Reviewer perspective**: `Senior Test Architect`
- **Reviewed artifacts**:
  - `README.md`
  - `docs/README.md`
  - `docs/005-refactor-app-host/requirements.md`
  - `docs/005-refactor-app-host/technical-specification.md`
  - `docs/005-refactor-app-host/plans/001-delivery-plan.md`
  - `docs/wiki/application-overview.md`
  - `docs/wiki/local-development.md`
  - `docs/wiki/testing-and-quality.md`
  - `src/TNC.Trading.Platform.AppHost/AppHost.cs`
  - `src/TNC.Trading.Platform.AppHost/AppHostSettings.cs`
  - `src/TNC.Trading.Platform.AppHost/AppHostInfrastructureRegistration.cs`
  - `src/TNC.Trading.Platform.AppHost/AppHostProjectRegistration.cs`
  - `src/TNC.Trading.Platform.AppHost/AppHostEnvironmentWiring.cs`
  - `src/TNC.Trading.Platform.Web/PlatformApiClient.cs`
  - `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/*`
  - `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/*`
  - `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/*`
  - `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/*`
  - `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/*`
  - `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/*`
  - `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/*`
- **External guidance refreshed**:
  - Microsoft Learn .NET testing guidance
  - Microsoft Learn Blazor testing guidance
  - `https://aspire.dev/testing/overview/`

## Executive summary

- **Overall test confidence**: `medium`
- **Overall coverage assessment**: `partial`
- **Top concerns**:
  1. `F1` AppHost refactor coverage is still mostly indirect. The refactored support units exist in `src/TNC.Trading.Platform.AppHost/*`, but this review found no focused lower-level tests for `AppHostSettings`, resource registration, project registration, or environment wiring.
  2. `F2` High-cost distributed startup is repeated more than necessary. `PlatformAuthenticationIntegrationTests` repeatedly creates and starts AppHost per test (`test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/PlatformAuthenticationIntegrationTests.cs:26-30`), and several anonymous Web functional tests do the same (`test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/Authentication/PlatformAuthenticationFunctionalTests.cs:26-33`, `98-105`, `126-133`; `.../PlatformProtectedRouteFunctionalTests.cs:25-32`, `50-57`, `75-82`).
  3. `F3` Lower-level coverage is uneven and some tests remain brittle. API unit coverage is extremely low, API unit tests still use string-based reflection (`test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/ApiReflection.cs:18-65`), and the documented Web component-coverage claim is not clearly evidenced by the reviewed Web unit-test files.

## Requirement coverage matrix

| Requirement / area | Existing coverage | Evidence | Gap assessment | Recommendation |
| --- | --- | --- | --- | --- |
| `RA1` AppHost composition and preserved topology | Partial | `src/TNC.Trading.Platform.AppHost/AppHost.cs:3-14`; `AppHostInfrastructureRegistration.cs:5-36`; `AppHostProjectRegistration.cs:7-52`; `AppHostEnvironmentWiring.cs:7-118`; indirect runtime exercise through `PlatformAuthenticationIntegrationTests` and the Web real-runtime fixtures | `F1` Coverage is mostly indirect through distributed tests; no focused tests were found for the refactored AppHost support units. | Add targeted tests for settings parsing and environment wiring, then retain a narrow real-topology smoke. |
| `RA2` API authentication and authorization boundaries | Covered | `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/PlatformAuthenticationIntegrationTests.cs:17-257` covers anonymous `401`, authenticated `403`, and role-based success paths | Strong protected-surface evidence exists for current API routes. | Keep this baseline and add endpoint-inventory checks as routes grow. |
| `RA3` Blazor protected navigation and sign-in flows | Partial | `PlatformProtectedRouteFunctionalTests.cs:16-118`; `PlatformAuthenticationFunctionalTests.cs:17-160`; `PlatformDashboardAuthenticationE2ETests.cs:22-49` | Route protection is good, but many simple redirect cases still use AppHost-backed functional tests. `F2` | Move simple anonymous redirect checks to cheaper lower-level tests where possible. |
| `RA4` Configuration validation and write-only secret handling | Covered | `UpdatePlatformConfigurationValidatorTests.cs:7-43`; `ProtectedCredentialServiceTests.cs:8-74`; `PlatformApiClientTests.cs:83-139` | Current validation and protected credential storage are well represented. | Extend only when configuration model changes. |
| `RA5` Notification, redaction, and retention | Partial | `NotificationProviderTests.cs:74-220`; `OperationalRecordRetentionProcessorTests.cs:9-48` | Lower-level storage and redaction are covered, but broader operational behavior and notification-path confidence remain narrower. | Add targeted tests for operational event/log shape and runtime notification-path assumptions. |
| `RA6` Web UI component and rendering behavior | Partial | `PlatformNavigationAccessCoordinatorTests.cs:14-149`; `PlatformAuthorizationRedirectResolverTests.cs:9-52`; `PlatformThemeStateTests.cs:9-92`; `PlatformShellContextProviderTests.cs:17-63` | `F3` `docs/wiki/testing-and-quality.md:86-87` claims bUnit coverage for `MainLayout`, `Home`, `Status`, and `Configuration`, but this review did not surface matching reviewed component test classes. | Add real Blazor component tests for those pages/layouts or correct the wiki. |
| `RA7` Test determinism, isolation, and runtime cost | Partial | Web functional/E2E collection fixtures reuse AppHost (`AuthenticationFunctionalTestCollection.cs:5-6`, `AuthenticationE2ETestCollection.cs:5-6`), but API integration tests still start AppHost per test | `F2` Improvement exists, but not consistently across suites. | Reuse AppHost per collection more broadly and keep high-cost checks intentionally narrow. |
| `RA8` Traceability, naming, and maintainability | Partial | Strong XML comments across many tests; reflection helper in `ApiReflection.cs`; duplicate XML tag in `PlatformAuthenticationFunctionalTests.cs:43-49`; naming drift in `PlatformThemeStateTests.cs:19-47` | `F3`, `F5` Traceability is generally strong, but some tests are harder to maintain and some names/comments drift from repo standards. | Remove reflection where possible and normalize method names/comments to repo conventions. |

## Code-coverage summary

### Coverlet runs completed

| Test project | Result | Coverage report |
| --- | --- | --- |
| `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests` | Success | `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/TestResults/f6d892e3-23aa-4711-b902-3e4d8252dfc2/coverage.cobertura.xml` |
| `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests` | Success | `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/TestResults/9a1306be-3949-4aed-af62-4042b6688afd/coverage.cobertura.xml` |
| `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests` | Success | `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/TestResults/3ef59283-0212-474f-99a0-082348c667a0/coverage.cobertura.xml` |
| `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests` | Success | `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TestResults/e1d7aa00-e1a4-4d43-8290-efabf328eba9/coverage.cobertura.xml` |

### Coverage totals

| Test project | Line coverage | Branch coverage | Material findings |
| --- | --- | --- | --- |
| `Application.UnitTests` | `55.11%` | `42.81%` | Moderate signal for application logic. Package split shows `TNC.Trading.Platform.Application` at `50.72%` and referenced `TNC.Trading.Platform.Infrastructure` at `59.07%`, which suggests useful lower-level coverage but also some cross-layer testing. |
| `Infrastructure.UnitTests` | `35.82%` | `22.93%` | Uneven signal. Package split shows `TNC.Trading.Platform.Infrastructure` at `64.31%` but `TNC.Trading.Platform.Application` at only `4.24%`, so the suite is focused but overall branch coverage remains weak. |
| `Api.UnitTests` | `4.88%` | `5.10%` | Very low coverage. Package split shows `TNC.Trading.Platform.Api` at `7.29%`, `Application` at `10.39%`, and `Infrastructure`/`ServiceDefaults` at `0%`. This matches the small, reflection-heavy unit suite and reinforces `F3`. |
| `Web.UnitTests` | `34.17%` | `26.16%` | Mixed signal. Package split shows `TNC.Trading.Platform.Web` at `53.11%`, `Application` at `11.39%`, and `ServiceDefaults` at `0%`. Web helper/service logic is covered more than shared/runtime concerns. |

### Coverage conclusions

- `F3` API unit coverage is the weakest area by a wide margin and is not strong enough to act as a fast regression net for API behavior.
- `F1` No focused coverage evidence was found for the AppHost refactor support units, even though work package `005` is specifically about AppHost maintainability and realistic distributed validation.
- The strongest lower-level signal is currently in application and infrastructure domain logic, not in AppHost composition or API host behavior.
- The current coverage shape supports the repository's unit-first intent in some areas, but it remains uneven across architectural layers.

## Mutation-testing summary

### Stryker runs completed

| Test project | Mutation target | Result | Report output |
| --- | --- | --- | --- |
| `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests` | `src/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.csproj` | Success | `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/StrykerOutput/2026-05-23.18-48-32/reports/mutation-report.html` |
| `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests` | `src/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.csproj` | Success | `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/StrykerOutput/2026-05-23.18-40-33/reports/mutation-report.html` |
| `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests` | `src/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.csproj` | Success | `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/StrykerOutput/2026-05-23.18-48-32/reports/mutation-report.html` |
| `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests` | `src/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.csproj` | Failed during mutation processing | `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/StrykerOutput/2026-05-23.18-40-33/` (no HTML report generated) |

### Mutation results

| Test project | Final mutation score | Console summary | Material findings |
| --- | --- | --- | --- |
| `Application.UnitTests` | `18.80%` | `Killed: 77`, `Survived: 99`, `Timeout: 1` | Low score. Current unit tests do not strongly kill many logic mutations in application orchestration and retry/state behavior. |
| `Infrastructure.UnitTests` | `25.67%` | `Killed: 143`, `Survived: 237`, `Timeout: 0` | Better than Application, but still low. Many infrastructure logic mutations survived despite reasonable direct line coverage. |
| `Api.UnitTests` | `18.52%` | Console tail showed `Killed: 35`, `Survived: 18`, `Timeout: 0`; Stryker reported final score `18.52%` | Very low score, which aligns with the narrow and reflection-based unit suite. |
| `Web.UnitTests` | Not available | Failed during mutation processing and left only a stub output folder | Mutation evidence for Web was not obtained in this review. |

### Confirmed mutation evidence

- Mutation signal is weak across the three successful projects. All completed scores are below `30%`.
- The Infrastructure Stryker HTML report was generated successfully and included mutated areas tied to `SqlPlatformConfigurationStoreTests`, which confirms configuration-store behavior is part of the surviving-mutant picture.
- An initial Application Stryker attempt failed because the target assembly file was locked by `VBCSCompiler`; a retry succeeded.

### Assumptions and likely survived-mutant themes

- Based on the low scores and the reviewed test focus, likely weak areas include conditional business logic, configuration-store branching, and orchestration/state transitions rather than simple happy-path assertions.
- The successful Infrastructure report content and the reviewed test inventory suggest configuration persistence, startup-configuration branching, and related secret-safe audit logic remain the most plausible survived-mutant clusters.
- These are informed assumptions, not fully extracted per-mutant classifications for every project.

## Existing test strengths

- The repository has a clear multi-level structure with separate unit, integration, functional, and E2E projects.
- Security-sensitive areas are well represented:
  - API auth boundary checks in `PlatformAuthenticationIntegrationTests`
  - role-policy registration in `PlatformAuthenticationRegistrationTests` and `PlatformApiAuthenticationServiceCollectionExtensionsTests`
  - credential protection in `ProtectedCredentialServiceTests`
  - redaction and notification storage in `NotificationProviderTests`
- Several lower-level tests are deterministic and isolated:
  - fixed time in `OperationalRecordRetentionProcessorTests.cs:18-40`
  - test time provider in `AuthRetryCycleTests.cs:493-500`
  - in-memory DbContext helpers in `ApplicationReflection.cs:11-23` and `InfrastructureReflection.cs:11-23`
- Web functional and E2E suites already reuse expensive runtime startup through xUnit collection fixtures.
- Test comments are usually strong and traceable, especially in the authentication-focused suites.

## Gaps in testing

### Missing coverage

- `F1` No focused tests were found for:
  - `AppHostSettings.FromConfiguration` in `src/TNC.Trading.Platform.AppHost/AppHostSettings.cs:12-24`
  - infrastructure registration in `AppHostInfrastructureRegistration.cs:5-36`
  - project registration in `AppHostProjectRegistration.cs:7-52`
  - environment wiring and provider branching in `AppHostEnvironmentWiring.cs:7-118`
- `F3` API unit tests do not provide a strong low-cost regression net. Coverlet and Stryker both show very weak signal for this suite.
- `F4` `docs/wiki/testing-and-quality.md:86-87` claims bUnit coverage for `MainLayout`, `Home`, `Status`, and `Configuration`, but this review did not identify matching reviewed component test classes.
- `F6` Provider-parity coverage remains narrow. `AppHostEnvironmentWiring.ConfigureAuthenticationProvider` still contains provider branching, but focused tests for those branches were not found.

### Weak or fragile tests

- `F2` Many API integration tests still pay full AppHost startup cost per test, even for scenarios that do not appear to require isolated startup.
- `F3` API unit tests use reflection by string for type and method access (`ApiReflection.cs:18-65`; `UpdatePlatformConfigurationValidatorTests.cs:16-19`), which weakens compile-time safety.
- `F5` Test hygiene is inconsistent in places:
  - duplicate `<summary>` tag in `PlatformAuthenticationFunctionalTests.cs:43-49`
  - naming drift such as `ParseThemeMode_NoStoredPreference_ReturnsDarkTheme` and `ParseThemeMode_LightPreference_ReturnsLightTheme` in `PlatformThemeStateTests.cs:19-47`

### Risks not adequately tested

- `F1` AppHost refactor regressions could slip into environment keys, waits, or links before a distributed suite exposes them.
- `F2` Runtime-coupled suites remain a cost and flake risk because they depend on Docker, AppHost startup, Keycloak, listener discovery, polling, and long browser waits.
- `F6` Provider-branch regressions remain under-tested, especially outside the main Keycloak runtime path.
- `F4` Documentation-to-test drift creates false confidence if wiki coverage claims are broader than the actual test assets.

## Slow-running or flaky tests

- **Likely slow**
  - API integration tests that start a fresh AppHost per test: `PlatformAuthenticationIntegrationTests.cs:26-30`
  - Web functional tests that still create AppHost ad hoc for anonymous redirect scenarios: `PlatformAuthenticationFunctionalTests.cs:26-33`, `98-105`, `126-133`; `PlatformProtectedRouteFunctionalTests.cs:25-32`, `50-57`, `75-82`
- **Likely flake contributors**
  - listener polling and retry loop in `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/Authentication/AppHostProcessHandle.cs:51-88`
  - fixed half-second polling delay in `AppHostProcessHandle.cs:78-81`
  - long Playwright waits in `PlatformDashboardAuthenticationE2ETests.cs:31-39`
- **Suspected causes**
  - repeated process-graph startup
  - dependence on Docker-backed infrastructure
  - readiness discovery through output parsing and polling rather than one centralized readiness contract
- **Mitigation strategies**
  - reuse AppHost per collection in API integration suites, as already done in Web functional/E2E
  - move simple anonymous redirect and policy-shape checks to unit tests or lightweight HTTP tests
  - keep one or two real-runtime browser smokes, not broad matrices
  - prefer explicit readiness signals over more time-based retries

## Recommendations to strengthen existing tests

1. `F1` Add focused AppHost tests first. Cover `AppHostSettings.FromConfiguration`, provider branch selection, and key environment wiring rules before relying on distributed suites to catch AppHost regressions.
2. `F3` Reduce reflection in API unit tests. Replace `ApiReflection`-driven tests with compile-time-accessible seams such as `InternalsVisibleTo` or narrower public/internal abstractions.
3. `F2` Normalize distributed fixture reuse. Introduce an API integration collection fixture and stop starting AppHost per test where isolation is unnecessary.
4. `F4` Reconcile docs with actual test assets. Either add the claimed Blazor component tests or correct `docs/wiki/testing-and-quality.md`.
5. `F5` Clean up naming and comments. Fix malformed XML comments and rename outliers to the required `MethodName_StateUnderTest_ExpectedResult` style.

## Recommendations for new tests

| Priority | Area | Test level | Recommendation | Reason |
| --- | --- | --- | --- | --- |
| High | AppHost settings and wiring | Unit | Add tests for `AppHostSettings.FromConfiguration` and provider/environment decisions in `AppHostEnvironmentWiring`. | Closes `F1` with the cheapest, most stable coverage. |
| High | AppHost resource/model composition | Integration | Add a focused AppHost composition smoke that asserts expected resources, waits, and exposed links remain present after refactors. | Protects the `005` work-package risk area without broadening expensive suites too far. |
| High | API host behavior | Unit | Expand API unit tests beyond validator/configuration guards so they directly exercise more endpoint registration and auth-configuration behavior without reflection. | Addresses `F3` and the very low API coverage/mutation scores. |
| Medium | Web page/layout component coverage | Unit | Add real Blazor component tests for `MainLayout`, `Home`, `Status`, and `Configuration`, or remove the documentation claim. | Resolves `F4` and improves UI regression signal. |
| Medium | Provider parity | Unit / Integration | Add focused tests for Keycloak/Test/Entra configuration resolution and AppHost auth-provider wiring. | Reduces `F6` risk without broadening expensive browser coverage. |
| Medium | API integration startup reuse | Integration | Convert API auth integration tests to a shared collection fixture when isolation is not required. | Reduces `F2` runtime cost and flake surface. |
| Low | Naming and traceability compliance | Unit / Convention | Add a convention test that flags malformed XML comments and method names outside the repo standard. | Prevents repeat `F5` drift cheaply. |

## Hardening recommendations

- Prefer lower-level tests over new distributed tests when validating configuration parsing, policy registration, or environment wiring.
- Keep real AppHost-plus-Keycloak coverage narrow and deliberate.
- Avoid additional arbitrary waits; improve readiness signaling instead.
- Where runtime tests remain necessary, reuse shared fixtures consistently across suites.
- Treat docs as evidence-backed artifacts: if `docs/wiki/testing-and-quality.md` claims coverage, there should be a discoverable test class behind that claim.
- Use mutation scores as prioritization input. The current Stryker results show that line coverage alone is overstating confidence in Application, Infrastructure, and API unit suites.

## Assumptions and missing information

- Microsoft Learn and Aspire guidance were refreshed at a high level in this review, but the fetched page output was not retained as structured repository artifacts.
- Web Stryker did not complete successfully in this review, so no mutation score is available for `TNC.Trading.Platform.Web.UnitTests`.
- Survived-mutant themes were only partially extracted from the generated HTML; the report distinguishes confirmed scores from inferred weak areas.
- This review focused scanning on the files needed to establish project-wide scope, evidence, risks, and recommendations rather than exhaustively reading every source file.
- `run_build` completed successfully at the end of the review.

## Suggested next steps

1. Implement the high-priority AppHost unit/integration tests first (`F1`) so work package `005` has direct evidence for the behavior it is changing.
2. Strengthen the API unit suite next (`F3`) by removing reflection-based tests and expanding low-cost host/configuration coverage.
3. Rework distributed startup reuse (`F2`) and then either add or correct the claimed Blazor component coverage (`F4`).
4. After those changes, rerun Coverlet and Stryker to confirm that API and Application mutation scores improve materially and that the new AppHost tests provide measurable coverage signal.
