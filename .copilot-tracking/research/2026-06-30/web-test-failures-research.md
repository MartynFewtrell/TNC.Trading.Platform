<!-- markdownlint-disable-file -->
# Task Research: Web Test Failures

Investigate the failing tests in `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests` and `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests` to determine the failure causes and identify the most appropriate resolution direction.

## Task Implementation Requests

* Determine why tests are failing in the functional test project.
* Determine why tests are failing in the E2E test project.
* Compare likely resolution options and recommend the best next step.

## Scope and Success Criteria

* Scope: Failure analysis only for the two named Web test projects, including test execution evidence, relevant code/configuration references, and recommended remediation direction. No product-code changes.
* Assumptions: Current workspace state reflects the failing behavior; failures can be reproduced locally or their cause can be established from existing evidence; unrelated test failures outside the two target projects are out of scope unless they block diagnosis.
* Success Criteria:
  * Capture reproducible or otherwise verified failure evidence for both projects.
  * Identify the controlling cause for each failure with supporting file references.
  * Evaluate plausible resolution paths and recommend one approach.

## Outline

1. Collect failure evidence from each target project.
2. Trace each failure to the controlling test/runtime/configuration path.
3. Compare resolution options and recommend the best next action.

## Potential Next Research

* Inspect Aspire resource-level logs for `web` and `keycloak`
  * Reasoning: Current evidence proves the Web endpoint was not discovered, but it does not yet distinguish between a true Web startup stall and a listener-publication mismatch.
  * Reference: test/Shared/Authentication/AppHostProcessHandle.cs:87-129,205-226,327

* Inspect one of the generated hang dumps if resource logs remain inconclusive
  * Reasoning: The functional test run produced AppHost and testhost hang dumps that can confirm the exact blocking wait.
  * Reference: test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/TestResults/

## Research Executed

### File Analysis

* test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/Authentication/RealAuthenticationFunctionalTestFixture.cs
  * Fixture initialization starts AppHost and blocks on shared Web base URI discovery before any test body runs.
* test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/Authentication/RealAuthenticationE2ETestFixture.cs
  * E2E fixture initialization follows the same startup sequence, so the failure happens before Playwright interaction.
* test/Shared/Authentication/AppHostProcessHandle.cs
  * `WaitForWebSignInUriAsync` loops until it can probe a candidate sign-in endpoint that redirects into a real Keycloak OIDC authorization path.
  * Discovery depends on `Now listening on:` output and newly observed local TCP ports.
  * Timeout exceptions include the discovered listener set, which in the failing E2E run contained only the Aspire dashboard URI.
* src/TNC.Trading.Platform.AppHost/AppHostProjectRegistration.cs
  * The Web project is still registered as an external HTTP endpoint resource, so the failure is not explained by the Web project being removed from AppHost composition.
* src/TNC.Trading.Platform.AppHost/AppHostEnvironmentWiring.cs
  * The Web project is hard-wired to Keycloak authentication and waits for Keycloak before it becomes useful to the tests.
  * Interactive test sign-in is optional and only enabled when AppHost settings request it.

### Code Search Results

* Narrow file reads were sufficient to trace the controlling path; no broader code search was required after the shared fixture and AppHost startup path were identified.

### External Research

* None. The failure cause was established from local test execution and repository code paths.

### Project Conventions

* Standards referenced: repository Copilot instructions and applicable markdown/folder guidance.
* Instructions followed: research limited to `.copilot-tracking/research/` for authored files.

## Key Discoveries

### Project Structure

Both failing projects depend on the same shared authentication startup harness in `test/Shared/Authentication`. The individual test projects diverge only after fixture initialization, but both fail earlier than that at AppHost-driven Web endpoint discovery.

### Implementation Patterns

The shared harness starts AppHost as a child process, scrapes local listener information, probes `/authentication/sign-in`, and accepts success only when the redirect chain reaches a real Keycloak OIDC authorization path. This means a single AppHost or listener-discovery regression can break both functional and E2E suites even when their test bodies are unrelated.

### Complete Examples

```text
Functional fixture:
appHostProcess = RealAppHostProcessFactory.StartAppHostProcess();
WebBaseUri = await RealAppHostProcessFactory.GetWebBaseUriAsync(appHostProcess);

Shared discovery gate:
throw new TimeoutException("The AppHost-started Web sign-in URL could not be discovered...");
```

### API and Schema Documentation

Not applicable. The investigation was limited to local test/runtime behavior.

### Configuration Examples

```text
Web authentication under AppHost:
Authentication__Provider = Keycloak
Authentication__Keycloak__Authority = <local keycloak authority>
Authentication__Test__EnableInteractiveSignIn = true only when the fixture enables it
```

## Technical Scenarios

### Initial Failure Analysis

The two failing test projects are not failing for two separate assertion-level reasons. They converge on one shared startup failure path: AppHost launches, but the shared harness never discovers a reachable Web sign-in endpoint before its timeout expires.

**Requirements:**

* Establish concrete failure mode for both test projects.
* Locate the code or configuration that controls the failure.
* Recommend the best resolution direction.

**Preferred Approach:**

* Treat this as a shared AppHost startup and endpoint-discovery failure. Investigate why the AppHost-composed Web plus Keycloak path does not surface a discoverable Web listener before changing either test project.

```text
FunctionalTests
  -> RealAuthenticationFunctionalTestFixture.InitializeAsync()
  -> shared AppHost startup
  -> WaitForWebSignInUriAsync(timeout)
  -> timeout / hang abort before test body

E2ETests
  -> RealAuthenticationE2ETestFixture.InitializeAsync()
  -> shared AppHost startup
  -> WaitForWebSignInUriAsync(timeout)
  -> timeout before Playwright navigation
```

**Implementation Details:**

The strongest evidence comes from the targeted project-level test runs:

* Functional test evidence
  * A narrow `dotnet test` run for `RootRoute_ShouldRenderSignInPage_WhenAnonymousUserRequestsApplicationEntry` hung during startup and ended with `TESTRUNABORT`, producing hang dumps for `testhost`, `dotnet`, and `TNC.Trading.Platform.AppHost`.
  * This points to fixture startup hanging rather than an assertion failure or browser issue.

* E2E test evidence
  * A narrow `dotnet test` run for `TNC.Trading.Platform.Web.E2ETests` failed with `System.TimeoutException` from `test/Shared/Authentication/AppHostProcessHandle.cs:129`.
  * The exception text reported that only `https://localhost:17257` was discovered, which is the Aspire dashboard listener rather than the Web front end.

* Shared controlling logic
  * `test/Shared/Authentication/AppHostProcessHandle.cs:87-129` loops until a candidate sign-in URI returns a redirect chain that reaches `protocol/openid-connect/auth`.
  * `test/Shared/Authentication/AppHostProcessHandle.cs:327` records listeners only from lines containing `Now listening on:`.
  * `src/TNC.Trading.Platform.AppHost/AppHostProjectRegistration.cs:30-40` still registers the Web project with external HTTP endpoints.
  * `src/TNC.Trading.Platform.AppHost/AppHostEnvironmentWiring.cs:63-76,169-182` configures the Web project for Keycloak and makes it wait for Keycloak.

This leaves two technically viable explanations:

1. The Web resource is not actually becoming ready because its startup path is blocked by Keycloak, API readiness, or another runtime dependency.
2. The Web resource is healthy, but the harness no longer discovers it reliably because it scrapes parent-process listener output and local ports instead of querying Aspire resource state directly.

Current evidence is stronger for a shared runtime startup or publication problem than for a per-test bug. The direct AppHost run captured only the dashboard listener during the observed window, which is consistent with both explanations above and inconsistent with a test assertion defect.

```text
Representative timeout:
System.TimeoutException: The AppHost-started Web sign-in URL could not be discovered from runtime listeners before the timeout expired. Discovered listener URIs: https://localhost:17257.
```

#### Considered Alternatives

* Change individual tests or assertions
  * Rejected. Both failures occur before test bodies execute, so test assertions are not the controlling cause.

* Treat the issue as a Playwright or browser problem
  * Rejected. The E2E fixture fails before `Page.GotoAsync`, and the functional failure uses `HttpClient` rather than Playwright.

* Assume the sign-in route was removed from the Web app
  * Rejected. The route still exists and the root path still redirects unauthenticated users into it.

* Increase the timeout and rerun
  * Rejected as a primary fix. It may hide the symptom temporarily, but it does not explain why only the dashboard listener is discoverable.

* Selected approach
  * Investigate the AppHost-composed Web and Keycloak startup path first, then harden the shared discovery harness only if resource-level evidence shows the Web endpoint is healthy but not observable to the current detection logic.
