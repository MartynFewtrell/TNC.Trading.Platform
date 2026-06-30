<!-- markdownlint-disable-file -->
# Task Research: Refactoring and Testability Opportunities

Identify repository areas where refactoring would improve code quality and create better opportunities for focused, lower-friction testing.

## Task Implementation Requests

* Review the repository for code quality refactoring opportunities.
* Identify places where refactoring would improve or unlock testing.
* Recommend a prioritized approach grounded in repository evidence.

## Scope and Success Criteria

* Scope: Repository-level review of implementation seams, coupling hotspots, orchestration complexity, and existing test coverage shape across src/ and test/. Excludes making product code changes.
* Assumptions:
  * The current runtime and test projects in the solution represent the intended architectural seams.
  * Existing documentation and previously captured repository research are broadly accurate unless contradicted by direct evidence.
  * The most useful output is a prioritized shortlist rather than an exhaustive defect inventory.
* Success Criteria:
  * Identify concrete refactoring candidates with evidence.
  * Explain how each candidate affects code quality and testing leverage.
  * Select a recommended prioritization path.

## Outline

1. Review backend orchestration and persistence-heavy slices for complexity and test seams.
2. Review API and Web surfaces for coupling, duplication, or hard-to-test behavior.
3. Review current test projects to identify where design limits coverage or test precision.
4. Consolidate alternatives and recommend a prioritized refactoring path.

## Potential Next Research

* Quantify suite runtime cost for the AppHost-backed authentication and browser-driven test lanes.
  * Reasoning: Several recommendations are justified by fixture complexity and setup duplication; timing data would sharpen the return-on-investment case.
  * Reference: test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/RealAuthenticationIntegrationTestFixture.cs:15-30, test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/Authentication/RealAuthenticationFunctionalTestFixture.cs:9-24.

* Inspect whether any existing architecture or work-package documents already prescribe a preferred state-machine, presenter, or endpoint-module pattern.
  * Reasoning: Some refactors are structural and could align with already documented design direction.
  * Reference: docs/wiki/architecture.md, docs/003-authentication-and-authorisation/, docs/004-ui-update-and-refactor/.

## Research Executed

### File Analysis

* src/TNC.Trading.Platform.Application/Services/PlatformStateCoordinator.cs
  * Centralizes runtime state transitions, retry policy execution, IG session calls, event persistence, proof-data capture, and notifications across multiple public and private methods. Evidence: lines 10-23, 28-79, 80-141, 142-191, 192-646, 772-858.
* src/TNC.Trading.Platform.Application/Services/PlatformAuthSupervisor.cs
  * Implements infinite-loop background ticking with direct delay handling and per-iteration scope creation. Evidence: lines 7-32.
* src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/SqlPlatformConfigurationStore.cs
  * Mixes bootstrap parsing, configuration persistence, credential updates, audit composition, and restart-required evaluation. Evidence: lines 49-117, 119-168, 169-241, 242-341.
* src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/NotificationDispatcher.cs
  * Couples provider dispatch, sanitization, persistence, and operational-event recording in one component. Evidence: lines 10-103.
* src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/OperationalRecordRetentionProcessor.cs
  * Contains provider-conditional delete behavior with separate SQL Server and fallback execution paths. Evidence: lines 14-50.
* src/TNC.Trading.Platform.Web/Components/Pages/Configuration.razor
  * Holds page lifecycle orchestration, save flow, alert composition, and request/form mapping inside the component. Evidence: lines 31-173, 190-245, 246-359.
* src/TNC.Trading.Platform.Web/Components/Pages/Home.razor
  * Mixes auth checks, redirect timing, audit side effects, API calls, and alert creation inside lifecycle methods. Evidence: lines 115-187.
* src/TNC.Trading.Platform.Web/Components/Pages/Status.razor
  * Carries page state loading, refresh behavior, and view shaping inside the Razor component. Evidence: lines 239-324.
* src/TNC.Trading.Platform.Web/PlatformApiClient.cs
  * Repeats the same authorized-request, send, ensure-success, and deserialize workflow across many methods. Evidence: lines 13-143.
* src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs
  * Centralizes endpoint registration, authorization, validation handling, exception-to-result translation, and auth-audit persistence. Evidence: lines 26-138.
* src/TNC.Trading.Platform.Api/Program.cs and src/TNC.Trading.Platform.Web/Program.cs
  * Keep startup and runtime initialization workflows embedded in top-level host wiring. Evidence: Api lines 15-97; Web lines 10-75.
* Representative test files
  * test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/AuthRetryCycleTests.cs:29-804.
  * test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/PlatformComponentTestContext.cs:23-111.
  * test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/ConfigurationTests.cs:15-140.
  * test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/RealAuthenticationIntegrationTestFixture.cs:15-30.
  * test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/RealAppHostProcessFactory.cs:5-118.
  * test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/Authentication/RealAppHostProcessFactory.cs:5-118.
  * test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/Authentication/AppHostProcessFactory.cs:9-147.
  * test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/InfrastructureReflection.cs:11-27.

### Code Search Results

* `PlatformStateCoordinator|TransitionToActiveAsync|TransitionToDegradedAsync|HandleSessionExpiredAsync|TryCaptureLiveProofDataAsync`
  * Confirms that the main auth/runtime coordinator contains duplicated transition logic and optional proof-data enrichment tightly coupled to the success path.
* `PlatformAuthSupervisor`
  * Confirms the background supervision loop exists in production code and has no corresponding direct test references.
* `Configuration.razor|Status.razor|Home.razor|PlatformComponentTestContext`
  * Confirms page behavior is concentrated in components while tests rely on a broad shared harness.
* `PlatformEndpoints|InvalidOperationException|Results.Conflict|ValidationProblem`
  * Confirms endpoint translation and HTTP behavior live inline inside a large static endpoint module.
* `RealAppHostProcessFactory|AppHostProcessFactory`
  * Confirms duplicated AppHost startup/process helpers across API integration, Web functional, and Web E2E suites.
* `CreateDbContext|InMemory`
  * Confirms infrastructure unit tests lean heavily on EF InMemory helpers.

### External Research

* None planned unless repository evidence is insufficient.

### Project Conventions

* Standards referenced: Task Researcher workflow; existing repository research notes.
* Instructions followed: Task Researcher mode.

## Key Discoveries

### Project Structure

The repository already has a layered runtime and test-project structure, but several high-value behavior seams are still implemented as framework-heavy or persistence-heavy units. That means the nominal architecture is cleaner than the most important execution paths inside it. The main symptoms are large orchestration classes, fat Razor pages, broad static endpoint modules, and duplicated test harness code around AppHost-backed flows.

### Implementation Patterns

The strongest cross-cutting pattern is behavior concentrated at host and workflow boundaries rather than inside small pure decision objects. In Application, the main example is PlatformStateCoordinator, whose constructor and methods indicate that runtime policy, state mutation, integration calls, and persistence are all driven from one place. In Web, Configuration, Home, and Status keep orchestration and mapping inside components, which pushes tests into bUnit and full-page lifecycle coverage. In Api, PlatformEndpoints holds routing, policies, mapping, exception translation, and audit recording in one static module, so contract behavior is mostly verified indirectly.

The strongest test-pattern signal is that expensive or broad fixtures are compensating for missing seams in production code. Application tests frequently construct EF-backed collaborators to exercise coordinator behavior. Web unit tests assemble a large component test context before reaching page-specific assertions. Authentication confidence relies heavily on AppHost-backed integration, functional, and E2E suites with duplicated process/startup helpers and readiness polling.

### Complete Examples

```text
High-leverage hotspots

Application
  src/TNC.Trading.Platform.Application/Services/PlatformStateCoordinator.cs
  src/TNC.Trading.Platform.Application/Services/PlatformAuthSupervisor.cs

Infrastructure
  src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/SqlPlatformConfigurationStore.cs
  src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/NotificationDispatcher.cs
  src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/OperationalRecordRetentionProcessor.cs

Web
  src/TNC.Trading.Platform.Web/Components/Pages/Configuration.razor
  src/TNC.Trading.Platform.Web/Components/Pages/Home.razor
  src/TNC.Trading.Platform.Web/Components/Pages/Status.razor
  src/TNC.Trading.Platform.Web/PlatformApiClient.cs

Api
  src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs
  src/TNC.Trading.Platform.Api/Program.cs
```

### API and Schema Documentation

No external API research was required. The relevant contract surfaces are repository-native: PlatformEndpoints for HTTP behavior, PlatformApiClient for Web-to-Api usage, and Configuration page form mapping for operator-side request shaping.

### Configuration Examples

```text
Current high-cost test seams

Application coordinator tests
  EF-backed stores + notification + credential + IG client setup

Web page tests
  bUnit page render + auth state + navigation + API client + audit client

Distributed auth tests
  AppHost boot + Keycloak + SQL + API readiness + token readiness + optional browser automation
```

## Technical Scenarios

### Alternative 1: Test-Only Consolidation First

This approach would start by deduplicating AppHost test harnesses, shared fixtures, and EF InMemory helpers without changing production code.

**Requirements:**

* Reduce maintenance cost across existing test suites.
* Improve consistency of startup and readiness diagnostics.

**Preferred Approach:**

* Useful as a supporting change, but not the best first move because it does not materially improve the underlying production-code seams that force broad tests in the first place.

```text
Potential changes
  Shared test infrastructure for AppHost process startup
  Shared readiness probing helpers
  Shared component-test bootstrapping helpers
```

**Implementation Details:**

This alternative has a good cost-to-benefit ratio for test maintenance, especially because AppHost process factories are duplicated across three suites. It should still be treated as secondary because the broad fixtures exist for a reason: key production behavior is not separated into smaller units yet.

#### Considered Alternatives

Rejected as the primary recommendation because it reduces duplication more than it improves test precision. Evidence: duplicated AppHost helpers in test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/RealAppHostProcessFactory.cs:5-118, test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/Authentication/RealAppHostProcessFactory.cs:5-118, and test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/Authentication/AppHostProcessFactory.cs:9-147.

### Alternative 2: UI-First Presenter Extraction

This approach would first extract presenter or page-model style orchestration from the Web pages so most page tests become direct unit tests over plain objects.

**Requirements:**

* Reduce BUnit harness cost.
* Isolate form parsing, save decisions, redirect behavior, and alert composition from rendering.

**Preferred Approach:**

* Strong candidate for early work, especially on Configuration.razor, but it should follow the application-layer seam extraction because the Web pages still depend on backend orchestration shapes that remain broad and side-effect-heavy.

```text
Potential extraction targets
  Configuration page load/save service
  Status page read model builder
  Home page access/redirect decision service
```

**Implementation Details:**

This path quickly improves UI test ergonomics and will likely shrink PlatformComponentTestContext. It does not address the biggest domain-complexity hotspot, which remains PlatformStateCoordinator.

#### Considered Alternatives

Not selected as the first recommendation because it improves one boundary while leaving the highest-risk orchestration logic unchanged. Evidence: src/TNC.Trading.Platform.Web/Components/Pages/Configuration.razor:190-359, src/TNC.Trading.Platform.Web/Components/Pages/Home.razor:115-187, src/TNC.Trading.Platform.Web/Components/Pages/Status.razor:239-324.

### Alternative 3: Backend Domain Seam Extraction First

This approach starts by splitting pure decision logic from orchestration in PlatformStateCoordinator and nearby workflow components, then follows with narrower boundary refactors in Web, Api, and Infrastructure.

**Requirements:**

* Reduce the size and coupling of the most complex runtime behavior.
* Create direct unit-test seams for retry, transition, expiry, and notification decisions.
* Preserve existing integration and scenario suites as regression coverage while enabling cheaper new tests.

**Preferred Approach:**

* Recommended. This is the highest-leverage starting point because it improves both code quality and testability at the core runtime decision layer, and it creates clearer downstream seams for API and Web work.

```text
Recommended refactoring sequence
1. Extract a pure auth/runtime transition engine from PlatformStateCoordinator
2. Move side effects behind dedicated collaborators
   - retry-cycle persistence
   - notification emission
   - operational event recording
   - IG proof-data enrichment
3. Introduce a test seam for PlatformAuthSupervisor tick scheduling
4. Extract page-level orchestration from Configuration, Home, and Status
5. Break PlatformEndpoints into feature-local modules or injectable handlers
6. Decompose infrastructure stores/services where domain rules and persistence are mixed
7. Consolidate duplicated AppHost-backed test harness code
```

**Implementation Details:**

The first step should target the repeated transition logic in PlatformStateCoordinator. A pure transition engine can take current state, schedule status, retry state, broker outcomes, and manual commands as inputs and return a decision object describing the next state and required effects. The orchestration shell would remain responsible for calling stores, the IG client, and notification/event collaborators. That keeps the public runtime behavior intact while creating a direct seam for table-driven unit tests.

Once that seam exists, the next best move is to split optional and secondary behaviors away from the coordinator success path, especially IG proof-data enrichment. After that, PlatformAuthSupervisor can be reduced to loop control around a single-tick runner or scheduler abstraction.

On the Web side, extracting Configuration page mapping and save/load decisions into a presenter or form service should materially shrink the component harness. The same pattern can then be applied to Home and Status. In the API layer, PlatformEndpoints should be decomposed so route registration stays declarative and request-to-result translation becomes directly testable. Infrastructure refactors should then separate bootstrap parsing, persistence mapping, notification transport, and provider-specific delete behavior from storage concerns.

```text
Tests this approach would unlock

Application
  Table-driven transition tests for retry, expiry, blocked-live, and manual retry rules
  Direct scheduler/supervisor tests without wall-clock delays

Web
  Plain unit tests for form parsing, save decision outcomes, redirect rules, and alert generation

Api
  Fast contract tests for conflict, validation-problem, and audit-result behaviors

Infrastructure
  Pure unit tests for configuration bootstrap parsing, retention rules, and notification policy
  Narrower EF or SQL-backed tests for actual persistence behavior only
```

#### Considered Alternatives

Selected because it attacks the root cause behind the broadest and most expensive tests instead of optimizing around it. Evidence: src/TNC.Trading.Platform.Application/Services/PlatformStateCoordinator.cs:10-858, test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/AuthRetryCycleTests.cs:29-804, src/TNC.Trading.Platform.Web/Components/Pages/Configuration.razor:190-359, src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs:26-138.
