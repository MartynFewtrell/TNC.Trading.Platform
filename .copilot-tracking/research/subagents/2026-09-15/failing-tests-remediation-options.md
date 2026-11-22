---
title: Failing Test Remediation Options
description: Research record evaluating remediation options for the verified dynamic-port retry and persistent Keycloak-state test failures.
author: GitHub Copilot
ms.date: 2026-09-15
ms.topic: troubleshooting
keywords:
  - testing
  - HttpListener
  - Keycloak
  - Aspire
estimated_reading_time: 6
---

## Research Scope

Evaluate remediation alternatives for two verified current failures without modifying
product or test source:

1. `ControllableIgProvider` retries `HttpListener` startup after a dynamic-port bind
   failure.
2. The E2E fixture implicitly permits persistent Keycloak state.

The analysis will compare a minimal repair, relevant server abstractions or framework
alternatives, and environment or CI-only workarounds. It will conclude with one selected
recommendation per failure, implementation outline, evidence, validation, drawbacks, and
whether documentation or strategy changes are justified.

## Research Status

Complete. The two specified failure mechanisms and their owning code paths were
inspected. No product or test source was changed.

## Findings

### Governing constraints

The repository requires deterministic tests with no shared mutable state, fixture ownership
of mutable integration dependencies, bounded readiness, and no global serialization or
fixed-port leases as a workaround. The Aspire test guidance also requires closed-box
interaction through external boundaries and configuration scoped to the fixture.

The current tests use `Aspire.Hosting.Testing` 13.5.3 and Playwright, while the AppHost
uses `Aspire.Hosting.Keycloak` 13.5.3-preview.1.26425.3. No existing test-server,
`WebApplicationFactory`, `TestServer`, WireMock, or Kestrel abstraction is present in the
repository.

### Failure 1: `HttpListener` retry after bind failure

`ControllableIgProvider.Start` creates one `HttpListener` before a twenty-attempt loop. On
each attempt it chooses an arbitrary port in the 40000-60000 range, adds the prefix, starts
the listener, and, after `HttpListenerException`, clears only its prefixes. The next attempt
therefore retries with the same listener instance that has already failed to start. This is
the verified failure path. The random candidate-port approach also retains a normal
time-of-check-to-bind race, although retrying with a new listener makes that race recoverable.

The shared fake is used by both the functional and browser E2E fixture, so the repair must
keep a reachable loopback HTTP server and the existing `BaseUri` configuration contract.

#### Options

| Option | Assessment | Decision |
|--------|------------|----------|
| Recreate `HttpListener` for each bind attempt | Keeps the current external HTTP fake and its contract. Closing the failed listener before the next iteration removes the invalid retry state with a small, localized change. | Selected |
| Replace the fake with Kestrel bound to port `0` | Kestrel can atomically bind an OS-assigned port and report its address, eliminating manual random-port selection. It would require an ASP.NET Core host, endpoint mapping, address discovery, and shutdown coordination for a small three-route fake. | Deferred unless the fake gains substantially more HTTP behavior |
| Use `TestServer` or `WebApplicationFactory` | These use an in-memory `TestServer`. The AppHost runs the API in another process and requires a network-reachable `Ig:AccountPreferencesBaseUrl`, so this does not meet the closed-box boundary. | Rejected |
| Add WireMock.Net or another stub-server framework | A reachable server is feasible, but the repository has no such dependency and the current fake only needs three stateful endpoints. New package and behavioral abstraction costs exceed the repair. | Rejected |
| Serialize tests, lease a fixed port, pre-clear ports, or retry the CI job | These mask a resource-lifecycle defect and conflict with the repository rules against global serialization and fixed-port leases. CI retry can reduce noise but cannot establish deterministic behavior. | Rejected |

#### Selected recommendation

Repair `ControllableIgProvider.Start` by constructing a new `HttpListener` inside each
attempt. Add the candidate prefix, call `Start`, and return that listener with its URI only
after success. On `HttpListenerException`, close or dispose that attempt's listener before
trying the next candidate. Preserve the existing twenty-attempt limit and the final
diagnostic exception.

Add a focused regression test beside the E2E account-preferences tests or in an existing
shared-test location. Give `Start` a narrowly scoped internal test seam for deterministic
candidate ports, such as an enumerable or port-provider delegate. Keep the first candidate
occupied by a fixture-owned `TcpListener`; assert the next candidate returns a live provider
that handles one existing endpoint. The test proves the retry uses a fresh server object,
rather than merely increasing retry count.

Targeted post-change validation:

```powershell
dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/TNC.Trading.Platform.Web.E2ETests.csproj --filter "FullyQualifiedName~ControllableIgProviderTests.Start_ShouldRetryWithFreshListener_WhenFirstCandidatePortIsOccupied"
```

Then run the existing browser scenario that exercises the same fake through the external
AppHost boundary:

```powershell
dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/TNC.Trading.Platform.Web.E2ETests.csproj --filter "FullyQualifiedName~AccountPreferences_ShouldShowDesiredAndObservedLabels_WhenProviderIsUnavailable"
```

Drawbacks: the minimal repair still samples a port before binding, so it is a bounded retry
rather than an atomic port-allocation design. The deterministic seam adds a small API surface
to a test helper. Kestrel port `0` is the appropriate follow-up only if repeated port flakiness
continues after this repair or the fake expands enough to benefit from routing middleware.

### Failure 2: E2E fixture permits persistent Keycloak state

The AppHost defaults Keycloak to a persistent container lifetime unless
`AppHost:UsePersistentKeycloakState` equals `false`. The functional authentication fixture
passes that exact setting, but the comparable browser E2E fixture does not. Consequently, the
E2E fixture permits state from the persistent Keycloak resource, including imported realm
contents, users, sessions, and client mutations, to cross test sessions. This conflicts with
the repository's documented automated-auth lifecycle.

`ManagedAppHostFixture` already accepts configuration and sends each setting to
`DistributedApplicationTestingBuilder.CreateAsync` as an AppHost command-line argument. No
new fixture architecture is needed to express the intended state boundary.

#### Options

| Option | Assessment | Decision |
|--------|------------|----------|
| Add the existing Keycloak persistence opt-out to the E2E fixture | Matches the functional fixture, uses the AppHost's tested configuration switch, leaves normal local development persistent, and gives the E2E fixture a session-scoped Keycloak resource. | Selected |
| Make `ManagedAppHostFixture` always add the opt-out | This would centralize the policy, but it changes behavior for every present and future caller. Only two callers exist now, and a caller may later intentionally need persistence. | Defer until a third caller demonstrates duplication |
| Create a Keycloak-specific wrapper or use direct Admin API reset | A wrapper adds indirection around one configuration entry. An Admin API reset is slower, adds credentials and readiness paths, and cannot guarantee cleanup after a fixture startup failure as directly as a session resource. | Rejected |
| Replace the real Keycloak flow with `WebApplicationFactory`, `TestServer`, or synthetic authentication | This would remove the real OIDC browser journey, which is the E2E scenario's purpose. Aspire closed-box testing does not allow DI replacement inside launched application processes. | Rejected |
| Delete Docker volumes or containers in CI, serialize the suite, or rely on clean CI agents | These work only in selected environments, risk disrupting developer resources, and do not make the fixture self-contained. | Rejected |

#### Selected recommendation

Add `"AppHost:UsePersistentKeycloakState" = bool.FalseString` to the configuration dictionary
in `RealAuthenticationE2ETestFixture.InitializeAsync`, next to the existing IG base-URL and
interactive-sign-in settings. Do not change the AppHost default: persistent Keycloak remains
the expected interactive local-development mode.

Keep the existing `IAsyncLifetime` cleanup and bounded resource-health waits. The test should
continue using the real Keycloak login page and Playwright, not a synthetic authentication
replacement. Include an explicit fixture-level test comment describing that this setting
preserves a clean realm import for browser E2E runs.

Targeted post-change validation:

```powershell
dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/TNC.Trading.Platform.Web.E2ETests.csproj --filter "FullyQualifiedName~AccountPreferences_ShouldShowDesiredAndObservedLabels_WhenProviderIsUnavailable"
```

Run the same filtered test a second time after the first process completes. The second run
must re-import the realm and complete the Keycloak login without inheriting previous state.

Drawbacks: session-scoped Keycloak makes each E2E run pay container initialization and realm
import time. It deliberately does not clean up an already persistent local resource from prior
runs; the fixture instead prevents future E2E sessions from using persistent state.

### Evidence

* `test/Shared/AccountPreferences/ControllableIgProvider.cs:6-23` defines the stateful
  loopback fake and stores the listener given to it after startup.
* `test/Shared/AccountPreferences/ControllableIgProvider.cs:36-56` creates one listener,
  retries `Start` with random ports, and clears prefixes after a bind exception.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/Authentication/RealAuthenticationFunctionalTestFixture.cs:16-28`
  provisions the provider and explicitly disables persistent Keycloak state.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/Authentication/RealAuthenticationE2ETestFixture.cs:14-25`
  provisions the same provider and managed AppHost but omits the Keycloak persistence setting.
* `test/Shared/Authentication/ManagedAppHostFixture.cs:21-35` forwards fixture configuration
  to `DistributedApplicationTestingBuilder.CreateAsync`.
* `test/Shared/Authentication/ManagedAppHostFixture.cs:43-54` waits for Keycloak, API, and
  Web before exposing their endpoints.
* `src/TNC.Trading.Platform.AppHost/AppHostInfrastructureRegistration.cs:5-17` defines the
  Keycloak persistence configuration key and defaults it to persistent when not `false`.
* `src/TNC.Trading.Platform.AppHost/AppHostInfrastructureRegistration.cs:41-49` applies the
  selected Keycloak container lifetime and realm import.
* `test/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.UnitTests/AppHostInfrastructureRegistrationTests.cs:55-73`
  already proves that the opt-out produces a session-lifetime Keycloak resource.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/AccountPreferences/AccountPreferencesE2ETests.cs:7-36`
  is the browser test that uses the fixture, real Keycloak login form, and external provider.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/TNC.Trading.Platform.Web.E2ETests.csproj:9-31`
  confirms the E2E project uses Aspire Testing and Playwright and links the shared provider
  and managed fixture.
* `docs/wiki/local-development.md:23-27` defines the intended automated-auth policy: set the
  Keycloak persistence opt-out and import a clean realm without changing local development.

### External References

* [Aspire testing overview](https://aspire.dev/testing/overview/) confirms Aspire tests are
  closed-box, separately process-hosted tests configured through environment or configuration;
  it identifies `WebApplicationFactory<T>` as the in-memory alternative and states that
  random proxy ports are enabled by default.
* [Kestrel endpoint configuration](https://learn.microsoft.com/aspnet/core/fundamentals/servers/kestrel/endpoints?view=aspnetcore-10.0)
  documents `Listen` and port `0` dynamic binding, including runtime address discovery.
* [ASP.NET Core integration tests](https://learn.microsoft.com/aspnet/core/test/integration-tests?view=aspnetcore-10.0)
  describes `WebApplicationFactory<T>` and its in-memory `TestServer` as integration-test
  infrastructure, rather than a reachable dependency for separately hosted processes.

### Documentation and Strategy Decision

No documentation change is justified for either selected repair. The Keycloak policy is
already stated accurately in `docs/wiki/local-development.md`; the E2E fixture has drifted
from it. The existing test and Aspire instructions already prohibit the proposed CI-only
workarounds. Add regression coverage for the listener retry and perform the focused E2E test
twice during the remediation review, but do not introduce a broader testing-strategy change.

## Follow-On Questions

* None required to implement the selected repairs.
* Reconsider a Kestrel port-`0` fake only if the minimal listener repair produces continued
  bind flakes or the provider simulation becomes materially more complex.
