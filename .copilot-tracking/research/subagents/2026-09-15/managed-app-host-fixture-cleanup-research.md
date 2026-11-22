<!-- markdownlint-disable-file -->

# Managed AppHost Fixture Cleanup Research

## Research Scope

* Harden AppHost-backed test cleanup so DCP tunnel proxy containers do not remain after a test session ends.
* Inspect the fixture and process-handle cleanup paths, direct fixture consumers, and test project configuration.
* Identify a testable implementation path and practical validation commands.

## Initial Hypothesis

`ManagedAppHostFixture.DisposeAsync` currently performs stop and both disposals as a single sequential flow. If `StopAsync` throws or observes cancellation, it skips disposal of the distributed application and testing builder, which own the DCP session. Independent `try`/`finally` cleanup should ensure both owners are disposed and reveal cleanup failures after attempting all cleanup steps.

## Evidence To Verify

* Direct consumers and collection lifetimes determine how many AppHost sessions can be active concurrently.
* Existing tests and project packages determine whether the cleanup logic can be tested without Docker.
* The prior `AppHostProcessHandle` cleanup path provides a local precedent for exception aggregation.

## Status

Complete. The local implementation has a credible disposal short-circuit that can orphan a running DCP tunnel proxy when AppHost shutdown fails. The original research establishes that the five manually removed containers were DCP-owned resources whose labelled creator processes had already exited.

## Concrete Findings

### Ownership and Failure Path

* `test/Shared/Authentication/ManagedAppHostFixture.cs:10-17` owns an `IDistributedApplicationTestingBuilder` and its `DistributedApplication` for one test collection.
* `test/Shared/Authentication/ManagedAppHostFixture.cs:28-53` creates the testing builder, builds and starts the AppHost, waits for Keycloak, API, and Web health, then resolves resource endpoints.
* `test/Shared/Authentication/ManagedAppHostFixture.cs:39-42` catches initialization failures and invokes `DisposeAsync`, so partial initialization has an intended cleanup path.
* `test/Shared/Authentication/ManagedAppHostFixture.cs:66-79` is not failure-resilient: `application.StopAsync()` is awaited before `application.DisposeAsync()` and `builder.DisposeAsync()`. An exception or cancellation from stop prevents both later owner-disposal calls. The application and builder fields are also reset only after their respective calls complete.
* `test/Shared/Authentication/AppHostProcessHandle.cs:179-238` is the relevant local precedent. It attempts every cleanup operation, records a first exception plus later cleanup exceptions, and releases the process in a `finally` block. Its test-hosted application and builder cleanup occurs at lines 207-216.
* `test/Shared/Authentication/RealAppHostProcessFactory.cs:55-134` retains an unused alternative testing-builder startup path. It independently disposes the application, builder, and environment scopes on initialization failure at lines 111-134. No direct consumer of `StartManagedAppHostProcessAsync` was found.

### Direct Consumers and Lifetime Boundaries

| Fixture owner | Managed fixture creation and forwarding | Collection and test consumers |
|---|---|---|
| API real authentication | `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/RealAuthenticationIntegrationTestFixture.cs:5-30` | `RealAuthenticationIntegrationTestCollection.cs:3-6` disables parallelization; `PlatformAuthenticationIntegrationTests.cs:6-18` uses this fixture as an xUnit class fixture within that collection |
| API synthetic tokens | `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/SyntheticTokenIntegrationTestFixture.cs:10-31` | `AuthenticationIntegrationTestCollection.cs:3-6` is a non-parallel collection fixture; `PlatformAuthenticationSyntheticTokenIntegrationTests.cs:8-9` consumes it |
| Web functional | `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/Authentication/RealAuthenticationFunctionalTestFixture.cs:6-54` | `AuthenticationFunctionalTestCollection.cs:5-8` is a non-parallel collection fixture; `PlatformAuthenticationFunctionalTests.cs:6-16`, `PlatformProtectedRouteFunctionalTests.cs:5-14`, and `AccountPreferences/AccountPreferencesFunctionalTests.cs:7-16` use it |
| Web E2E | `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/Authentication/RealAuthenticationE2ETestFixture.cs:6-50` | `AuthenticationE2ETestCollection.cs:5-8` is a non-parallel collection fixture; `PlatformDashboardAuthenticationE2ETests.cs:7-17` and `AccountPreferences/AccountPreferencesE2ETests.cs:6-15` use it |

Each owner forwards disposal directly to the managed fixture. The two Web wrappers then dispose their controlled IG provider, but only after managed-fixture disposal succeeds: `RealAuthenticationFunctionalTestFixture.cs:38-52` and `RealAuthenticationE2ETestFixture.cs:34-48` share this secondary short-circuit risk.

### Test Configuration and Concurrency

* The fixture source is linked, rather than referenced through a shared test assembly, by all three AppHost-backed projects: API integration project `TNC.Trading.Platform.Api.IntegrationTests.csproj:27-33`, Web functional project `TNC.Trading.Platform.Web.FunctionalTests.csproj:23-30`, and Web E2E project `TNC.Trading.Platform.Web.E2ETests.csproj:23-29`.
* All three resolve `Aspire.Hosting.Testing` version `13.5.3`: API integration project lines 9-16, Web functional project lines 9-16, and Web E2E project lines 9-16.
* The API integration project is the only test project with a runner configuration. `xunit.runner.json:1-4` disables collection parallelism for that project. The Functional and E2E projects have no `xunit.runner.json` or solution-level `.runsettings` file.
* All four AppHost-owning collections explicitly use `DisableParallelization = true`, so tests inside a collection do not create simultaneous AppHost instances. This does not serialize separate `dotnet test` processes or separate test projects. The repository instructions prohibit using global serialization to conceal lifecycle defects: `.github/instructions/tests.instructions.md:71-72`.
* The Aspire test rules require the fixture to own and await asynchronous cleanup of the application and builder: `.github/instructions/aspire-tests.instructions.md:27-30`. They also require randomized ports and allow concurrent AppHost instances: lines 37-38.

## Recommended Implementation Path

1. Harden `ManagedAppHostFixture.DisposeAsync` at `test/Shared/Authentication/ManagedAppHostFixture.cs:66-79` as the root-cause correction. Capture cleanup failures, then attempt, in order, `application.StopAsync()`, `application.DisposeAsync()`, and `builder.DisposeAsync()` regardless of earlier failure. Set each field to `null` in a `finally` block associated with its disposal attempt to preserve idempotency after a failed cleanup.
2. Preserve the primary shutdown failure for xUnit diagnostics and attach later cleanup failures in `Exception.Data["CleanupExceptions"]`, following `AppHostProcessHandle.DisposeAsync` at `test/Shared/Authentication/AppHostProcessHandle.cs:179-238`. Do not swallow disposal exceptions, because a failed DCP cleanup is operationally significant.
3. Harden the Functional and E2E wrapper disposals at `RealAuthenticationFunctionalTestFixture.cs:38-52` and `RealAuthenticationE2ETestFixture.cs:34-48` in the same pattern so their `ControllableIgProvider` is released even if AppHost disposal fails. Aggregate rather than mask cleanup failures.
4. Do not add Docker container discovery or deletion to fixtures. It would violate the rule against examining arbitrary Docker containers and could terminate another active AppHost session. The test fixture must only manage the builder and resources it created.
5. Do not disable DCP tunnelling, disable port randomization, or introduce global test serialization. The original research establishes that DCP creates the tunnel proxy as required infrastructure for host-run API/Web projects reaching Docker resources. The current per-collection serialization is appropriate because the collection owns shared external state.
6. Keep the current collection boundaries. If CI launches the three projects concurrently, allow it after the disposal fix; Aspire randomizes ports specifically for concurrent test instances. Run them serially only while diagnosing a residual DCP leak.

## Suggested Testability Refactoring

`DistributedApplication` is a concrete Aspire type, which makes a direct no-Docker unit test of its stop/dispose sequence awkward. Extract only the sequencing and aggregation logic into an `internal` static helper that accepts three delegates: `Func<Task> stopApplication`, `Func<ValueTask> disposeApplication`, and `Func<ValueTask> disposeBuilder`.

Keep the helper in `ManagedAppHostFixture.cs` as an `internal` member so the linked source compiles into each existing test project without adding a shared test project or changing public APIs. Add a focused API integration-project test alongside the existing authentication test infrastructure that calls the helper with in-memory delegates. The test should verify all of the following:

* A stop failure still executes application and builder disposal.
* An application-disposal failure still executes builder disposal.
* The first failure is thrown and later failures are attached as cleanup diagnostics.
* A no-failure sequence executes stop, application disposal, then builder disposal exactly once.

Document each new test with the required traceability, expected outcome, and reason. This focused test is deterministic and does not need Docker, while the existing closed-box suites retain end-to-end proof that Aspire and DCP actually tear down their resources.

## Validation Commands

Run these commands from the repository root after implementation. Start with the deterministic helper test, then prove the distributed lifecycle and inspect DCP resources before and after each run.

```powershell
dotnet build TNC.Trading.Platform.slnx --no-restore

dotnet test test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/TNC.Trading.Platform.Api.IntegrationTests.csproj --no-restore --filter "FullyQualifiedName~ManagedAppHostFixture"

docker ps -a --filter "name=aspire-container-network-tunnelproxy" --format "table {{.ID}}\t{{.Names}}\t{{.Status}}\t{{.CreatedAt}}"

dotnet test test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/TNC.Trading.Platform.Api.IntegrationTests.csproj --no-restore --filter "FullyQualifiedName~PlatformAuthenticationIntegrationTests"

docker ps -a --filter "name=aspire-container-network-tunnelproxy" --format "table {{.ID}}\t{{.Names}}\t{{.Status}}\t{{.CreatedAt}}"

dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/TNC.Trading.Platform.Web.FunctionalTests.csproj --no-restore

dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/TNC.Trading.Platform.Web.E2ETests.csproj --no-restore

docker ps -a --filter "name=aspire-container-network-tunnelproxy" --format "table {{.ID}}\t{{.Names}}\t{{.Status}}\t{{.CreatedAt}}"
```

The expected post-run state is no remaining tunnel proxy whose Docker labels identify an exited testhost/AppHost creator. If one remains, collect the labels and creator process evidence without deleting an active session:

```powershell
docker inspect <container-id> --format "{{json .Config.Labels}}"

Get-Process -Id <creator-process-id>
```

The final behavioural check should deliberately terminate a focused distributed test after its AppHost has started, then record whether DCP leaves a proxy after the testhost exits. This is an operational experiment, not an automated assertion, because forced process termination cannot guarantee xUnit fixture disposal. It distinguishes an unavoidable abrupt-termination limitation from a remaining normal-shutdown leak in Aspire `13.5.3`.

## Remaining Gaps and Questions

* The available evidence proves a source-level cleanup gap and confirms the five manually removed proxies had exited creator processes. It cannot prove whether the original containers resulted from that gap, a forced testhost termination, or an Aspire/DCP `13.5.3` defect after a successful fixture disposal.
* The user or CI configuration must confirm whether the API integration, Web functional, and Web E2E projects are ever invoked concurrently. Concurrent project runs are supported after cleanup is hardened, but explaining the historic count of five requires the original command history.
* A focused normal-completion run followed by Docker label inspection is required to establish the actual post-fix behaviour of the installed Docker Desktop and Aspire/DCP bundle.