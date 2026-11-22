<!-- markdownlint-disable-file -->
# Implementation Details: Testing Improvement

## Context Reference

Sources: `.copilot-tracking/research/2026-09-15/testing-improvement-plan-research.md`, `.copilot-tracking/research/2026-09-15/failing-tests-research.md`, and `.copilot-tracking/research/subagents/2026-09-15/testing-improvement-plan-scope.md`.

## Implementation Phase 1: Restore Web Unit Dependency Compatibility

<!-- parallelizable: true -->

### Step 1.1: Remove the direct AngleSharp override

Delete only the direct `AngleSharp` 1.8.1 `PackageReference` in `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj`. Retain `bunit.web` 1.40.0 so NuGet resolves bUnit's supported transitive DOM dependency. Do not migrate bUnit suites or replace their rendering assertions with browser tests.

Files:
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj` - remove the incompatible test-only override

Discrepancy references:
* Addresses DR-01: deterministic test dependency incompatibility

Success criteria:
* The project contains no direct AngleSharp reference.
* The seven existing bUnit component suites remain present and unchanged unless a compile adjustment is required.

Context references:
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj` (Lines 15-16) - conflicting package references

Dependencies:
* None

### Step 1.2: Validate bUnit dependency resolution

Restore packages if the implementation operation requires it, then execute the Web unit project. Confirm the four failures no longer throw `MissingMethodException` before assertions and inspect resolved dependencies only if the failure persists.

Validation commands:
* `dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj --no-restore` - validates the isolated Web unit suite

## Implementation Phase 2: Migrate AppHost-Backed Fixtures

<!-- parallelizable: false -->

### Step 2.1: Create the shared managed Aspire fixture

Create a linked-source fixture in `test/Shared/Authentication/` so the existing API, functional, and E2E projects can compile it through their current shared-source mechanism. Build the AppHost with `DistributedApplicationTestingBuilder.CreateAsync<Projects.TNC_Trading_Platform_AppHost>()`, use a caller-owned bounded cancellation token, start the application, await health for each required named resource, and asynchronously dispose both builder/application.

Expose only `CreateApiClient()`, `CreateWebClient()`, a named Web endpoint URI, `WaitForResourceHealthyAsync(resourceName)`, scoped test configuration inputs, and direct connection data only when a suite truly needs it. Keep randomized proxy ports enabled and the dashboard disabled. Apply configuration at builder creation or through fixture-scoped arguments, never through process-wide environment mutation.

Files:
* `test/Shared/Authentication/ManagedAppHostFixture.cs` - new shared resource-oriented lifecycle owner
* API, functional, and E2E test project files - include the new linked source consistently with existing shared helpers

Discrepancy references:
* Addresses DR-02: endpoint discovery uses dashboard/process socket inference
* Addresses DR-03: fixed port and process-global configuration coupling

Success criteria:
* The fixture starts the AppHost without an externally launched process abstraction.
* Resource access is through Aspire names rather than scanned ports or launch profile endpoints.

Context references:
* `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/SyntheticTokenIntegrationTestFixture.cs` (Lines 33-38 and 76-84) - named API client example
* `test/Shared/Authentication/RealAppHostProcessFactory.cs` (Lines 71-118) - behavior to replace
* `.github/instructions/aspire-tests.instructions.md` (Lines 22-45) - governing Aspire standards

Dependencies:
* Docker Desktop and the AppHost's required local images

### Step 2.2: Migrate API authentication collections

Refactor `SyntheticTokenIntegrationTestFixture` and `RealAuthenticationIntegrationTestFixture` to wrap or consume the managed fixture. Preserve the synthetic API-only authentication override but supply it through the new scoped configuration contract. Preserve the real Keycloak token assertions, acquire token endpoints from the healthy named resource, and call API endpoints through the fixture's named API client. Remove the project-local real AppHost forwarding factory after callers migrate.

Files:
* `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/SyntheticTokenIntegrationTestFixture.cs` - replace fixed lease/environment use
* `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/RealAuthenticationIntegrationTestFixture.cs` - replace process and port discovery
* `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/RealAppHostProcessFactory.cs` - delete when unreferenced
* `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/*Collection.cs` - update fixture contracts as needed

Success criteria:
* Real and synthetic authentication suites use named clients/endpoints.
* Existing token and public HTTP behaviors remain covered.

Context references:
* `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/RealAuthenticationIntegrationTestFixture.cs` (Lines 20-35) - current lease and process lifecycle

Dependencies:
* Step 2.1 completion

### Step 2.3: Migrate the AppHost Keycloak authority contract

Add or update focused AppHost unit tests first to capture the semantic configuration contract: API and Web receive an authority derived from the Keycloak named `http` endpoint and realm name, never `https://localhost:8080`. Do not assert fragile manifest serialization until a focused composition probe confirms the exact reference-expression output for the installed Aspire version.

Remove the fixed authority constant and `port: 8080` infrastructure registration. Update AppHost environment wiring so API and Web use `WithReference(keycloak)` and an endpoint-derived reference expression for the realm authority. Retain the realm's existing dynamic localhost callback/origin support unless the dynamic flow proof identifies a narrow missing setting.

Files:
* `src/TNC.Trading.Platform.AppHost/AppHostCompositionConstants.cs` - remove or replace fixed Keycloak authority constant
* `src/TNC.Trading.Platform.AppHost/AppHostInfrastructureRegistration.cs` - remove fixed Keycloak port registration
* `src/TNC.Trading.Platform.AppHost/AppHostEnvironmentWiring.cs` - derive API/Web authority from the named Keycloak endpoint
* `test/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.UnitTests/AppHostEnvironmentWiringTests.cs` - verify endpoint-derived API/Web authority
* `test/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.UnitTests/AppHostCompositionTests.cs` - verify composition has no fixed Keycloak endpoint assumption

Success criteria:
* AppHost composition and wiring tests pass without an authority containing `localhost:8080`.
* API and Web wait for and reference Keycloak through the named resource contract.

Dependencies:
* Step 2.1 completion

### Step 2.4: Prove dynamic callback support and migrate Web fixtures

First run a focused real authorization-code flow using the dynamic fixture-provided Web URI. Verify the Keycloak realm client accepts the randomized callback/origin, discovery issuer is obtained from the healthy resource, sign-in completes, and the protected Web page is reached. Update the realm/test configuration only as narrowly as needed to support dynamic loopback callback endpoints.

After the proof passes, refactor `RealAuthenticationFunctionalTestFixture` and `RealAuthenticationE2ETestFixture` to consume the managed fixture. Supply the fixture-provided Web URI to HTTP and Playwright clients; remove localhost regular expressions, process endpoint discovery, and explicit readiness delays. Keep Playwright web-first assertions and accessibility-first locators.

Files:
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/Authentication/RealAuthenticationFunctionalTestFixture.cs` - migrate to managed fixture
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/Authentication/RealAuthenticationE2ETestFixture.cs` - migrate to managed fixture
* Keycloak realm/test configuration file identified during the callback proof - adjust only if dynamic callback support is absent

Discrepancy references:
* Addresses DD-01: fixed `8080` callback behavior is replaced only after a real dynamic-endpoint proof

Success criteria:
* Functional and E2E fixtures do not need a fixed localhost port.
* The retained real sign-in journey passes against a randomized named Web endpoint.

Dependencies:
* Steps 2.1 and 2.2 completion

### Step 2.5: Replace controllable IG endpoint and retire legacy helpers

Reimplement `ControllableIgProvider` as an endpoint-owning test server. Bind first, then publish the assigned base URI; if its HTTP hosting API cannot expose a port-zero binding, implement a bounded bind-and-retry loop that retains ownership until teardown. Preserve deterministic ledger and response controls. Pass its URI using managed-fixture scoped configuration before the AppHost builds.

Once every API, Web functional, and Web E2E consumer uses the managed fixture, delete `AppHostProcessHandle.cs`, `RealAppHostProcessFactory.cs`, `KeycloakPortLease.cs`, project forwarding wrappers, lease tests, and stale project compile entries. Search the complete `test/` tree before deletion to prove no legacy consumer remains.

Files:
* `test/Shared/AccountPreferences/ControllableIgProvider.cs` - remove released-port race
* `test/Shared/Authentication/AppHostProcessHandle.cs` - delete after migration
* `test/Shared/Authentication/RealAppHostProcessFactory.cs` - delete after migration
* `test/Shared/Authentication/KeycloakPortLease.cs` - delete after migration
* `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/KeycloakPortLeaseTests.cs` - delete or replace with managed-fixture lifecycle coverage
* API, functional, and E2E project compile includes and local wrappers - remove stale entries

Success criteria:
* No test code scans local TCP listeners, treats a dashboard URI as a resource URI, reserves a released port, or relies on a Keycloak port lock.
* Account-preferences functional and E2E tests use the fixture-owned IG URI.

Dependencies:
* Step 2.4 completion

### Step 2.6: Validate AppHost fixture isolation

Run each affected project twice. Then start the API integration, Web functional, and Web E2E commands concurrently in separate terminals without global xUnit or solution serialization. Retain TRX and resource logs outside the repository only for failures.

Validation commands:
* `dotnet test test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/TNC.Trading.Platform.Api.IntegrationTests.csproj --filter "FullyQualifiedName~PlatformAuthenticationIntegrationTests"` - real-auth contract
* `dotnet test test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/TNC.Trading.Platform.Api.IntegrationTests.csproj --filter "FullyQualifiedName~PlatformAuthenticationSyntheticTokenIntegrationTests"` - synthetic controlled-token contract
* `dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/TNC.Trading.Platform.Web.FunctionalTests.csproj --filter "FullyQualifiedName~Authentication|FullyQualifiedName~AccountPreferences"` - functional fixture and controlled provider
* `dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/TNC.Trading.Platform.Web.E2ETests.csproj --filter "FullyQualifiedName~Authentication|FullyQualifiedName~AccountPreferences"` - browser fixture and controlled provider

## Implementation Phase 3: Own SQL Integration Lifecycle

<!-- parallelizable: true -->

### Step 3.1: Add a dedicated owned SQL test fixture

Select the repository-compatible SQL test-container library and add it only to the Infrastructure integration project. Replace image enumeration and Docker credential inspection with a fixture that starts its own SQL Server, awaits a successful SQL connection/readiness check, creates a unique per-run database, resets the database between test cases, exposes only its connection string, captures diagnostics on failure, and asynchronously disposes the server.

Do not depend on the production AppHost: these tests validate Infrastructure SQL adapters and migrations, not distributed topology. Keep the real SQL Server provider and existing migration/transaction assertions.

Files:
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/TNC.Trading.Platform.Infrastructure.IntegrationTests.csproj` - add only the selected lifecycle package
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/SqlServerDatabaseFixture.cs` - replace Docker discovery with owned lifecycle

Discrepancy references:
* Addresses DR-04: test fixture attaches to arbitrary Docker SQL Server without readiness or ownership

Success criteria:
* The fixture starts and stops the SQL Server it uses.
* Each run owns a unique database and no test reads Docker metadata from unrelated containers.

Context references:
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/SqlServerDatabaseFixture.cs` (Lines 13-106) - existing discovery, reset, and cleanup behavior
* `.github/instructions/tests.instructions.md` (Lines 56-70) - isolation and cleanup requirements

Dependencies:
* Docker Desktop and selected library compatibility with .NET 10

### Step 3.2: Preserve consumer coverage under the owned fixture

Update `AccountPreferencesSqlIntegrationTests` and `PlatformSqlServerIntegrationTests` only for the fixture's new connection/reset contract. Maintain their real-provider migration, transaction, persistence, and concurrency assertions. Keep collection behavior localized to the shared mutable database boundary.

Files:
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/AccountPreferencesSqlIntegrationTests.cs` - adopt fixture contract if required
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/PlatformSqlServerIntegrationTests.cs` - adopt fixture contract if required

Success criteria:
* Existing provider-specific assertions pass without API/Web/AppHost startup.

Dependencies:
* Step 3.1 completion

### Step 3.3: Validate SQL lifecycle repeatability

Run the Infrastructure integration project twice. Confirm each run creates its own resource/database, completes cleanup, and does not require an already-running SQL Docker container.

Validation commands:
* `dotnet test test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/TNC.Trading.Platform.Infrastructure.IntegrationTests.csproj --no-restore` - full Infrastructure SQL integration suite

## Implementation Phase 4: Enforce and Document the Strategy

<!-- parallelizable: false -->

### Step 4.1: Strengthen testing instructions

Update `.github/instructions/aspire-tests.instructions.md` to require named `CreateHttpClient(resourceName[, endpointName])` and endpoint APIs, bounded `WaitForResourceHealthyAsync`, fixture-scoped configuration, async disposal, and randomized ports. Explicitly prohibit parsing AppHost output, TCP listener scans, launch-profile port assumptions, dashboard endpoint use, fixed-port leases, and process-wide environment mutation.

Update `.github/instructions/tests.instructions.md` to require every mutable integration dependency to be fixture-owned or explicitly provisioned and readiness-checked; prohibit arbitrary Docker container discovery/credential inspection; require xUnit collection fixtures only for suites that share an intentional configuration/data-isolation boundary; forbid global serialization as a workaround. Add the selected UI boundary rule: plain xUnit for state/policy, bUnit for component rendering/lifecycle, Aspire-hosted tests for topology, and Playwright for browser-only behavior. Prohibit direct DOM-parser dependencies unless a documented test needs one independently.

Files:
* `.github/instructions/aspire-tests.instructions.md` - resource-oriented hosted testing rules
* `.github/instructions/tests.instructions.md` - ownership and test-layer rules

Success criteria:
* Future test changes have clear MUST and MUST NOT guidance matching the chosen fixture model.

Dependencies:
* Phase 2 contract confirmed

### Step 4.2: Record durable repository guidance

Add concise routing rules to `.github/AGENTS.md` and `.github/lessons.md`: Aspire test changes use named resources, bounded readiness, randomized ports, fixture-scoped configuration, and async disposal; socket scans, dashboard assumptions, fixed-port leases, and global overrides are invalid resource contracts.

Files:
* `.github/AGENTS.md` - stable operating rule for testing customization work
* `.github/lessons.md` - concise durable implementation lesson

Success criteria:
* The guidance is non-duplicative and points future agents toward the selected model.

Dependencies:
* Step 4.1 completion

### Step 4.3: Align wiki operational guidance

Revise `docs/wiki/testing-and-quality.md` to replace listener-discovery, fixed `8080`, and lock-file guidance with named resource clients/endpoints, randomized ports, bounded health readiness, fixture-owned controlled endpoints, SQL lifecycle ownership, diagnostics, and the selected xUnit/bUnit/Aspire/Playwright responsibility split.

Revise the automated-validation portion of `docs/wiki/local-development.md` to distinguish it from manual local-development endpoints. Preserve stable manual access information, but state that automated AppHost tests use randomized resource endpoints and fixture lifecycle rather than listener discovery or leases.

Files:
* `docs/wiki/testing-and-quality.md` - test strategy and fixture model source of truth
* `docs/wiki/local-development.md` - manual versus automated endpoint guidance

Success criteria:
* Wiki guidance matches implemented behavior and does not direct engineers to retired mechanisms.

Dependencies:
* Phases 2 and 3 completion

### Step 4.4: Add CI test stages and failure artifacts

Create `.github/workflows/ci.yml` for a Linux runner with four ordered jobs: restore/build, fast project tests, Docker-backed Aspire/service integration tests, and browser/real-Keycloak tests. The final summary job runs the ordinary solution test command only after the scoped jobs are green; it does not replace their project-level result output.

Give every project command a unique `--results-directory` under `artifacts/test-results/<stage>/<project>` and a TRX logger. Upload TRX files unconditionally and upload high-volume diagnostics using `actions/upload-artifact@v4` only on failure. Add a shared fixture-failure artifact collector that writes sanitized resource status/logs, endpoint metadata, container diagnostics, and SQL diagnostic output to `TEST_ARTIFACTS_DIRECTORY`. Configure Playwright trace, screenshot, and video output so the browser job uploads it on failure. Never log credentials or unredacted connection strings.

Files:
* `.github/workflows/ci.yml` - new staged GitHub Actions validation workflow
* `test/Shared/Diagnostics/AspireTestFailureArtifactCollector.cs` - new redacted AppHost fixture diagnostic collector
* API integration, Web functional, and Web E2E project files - link the collector
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/Authentication/PlatformDashboardAuthenticationE2ETests.cs` - configure retained browser failure artifacts and dynamic endpoint assertion
* `docs/wiki/testing-and-quality.md` - document CI-stage and failure-artifact ownership

Success criteria:
* CI keeps fast tests separate from Docker-backed and browser runtime tests.
* Failed distributed/browser jobs publish TRX plus sanitized fixture/Playwright diagnostics.

Dependencies:
* Phase 2 managed-fixture and Phase 3 SQL lifecycle implementations

### Step 4.5: Validate workflow and documentation artifacts

Validate workflow YAML using the repository's available action/workflow linter or a GitHub Actions dry-run if configured. Run the repository's Markdown/frontmatter validation if present, then inspect the changed guidance for obsolete fixed-port, listener-discovery, and arbitrary Docker-discovery language. Run the exact per-stage commands locally where their prerequisites are available.

Validation commands:
* `npm run validate:frontmatter` - validate frontmatter if the repository script is available
* `actionlint .github/workflows/ci.yml` - validate workflow syntax if `actionlint` is installed
* `rg "DcpPublisher:RandomizePorts=false|KeycloakPortLease|AppHostProcessHandle|listener discovery|observed local listeners" .github docs/wiki test` - confirm retired guidance/code references are absent or intentionally historical

## Implementation Phase 5: Final Validation

<!-- parallelizable: false -->

### Step 5.1: Execute full validation

After the phase-specific double runs and concurrency proof, run the complete build and solution test gate from the repository root with normal test concurrency. Do not use solution-wide serialization.

Validation commands:
* `dotnet build TNC.Trading.Platform.slnx --no-restore` - compile all projects
* `dotnet test TNC.Trading.Platform.slnx --no-build --logger "console;verbosity=minimal"` - complete regression gate

Success criteria:
* All projects build and tests pass.
* No fixture cleanup failure follows a failed test setup.

### Step 5.2: Triage remaining failures

For an isolated package, compile, or test failure, repair the smallest local defect and rerun its focused command. For an issue that changes the shared fixture contract, SQL lifecycle choice, or Keycloak dynamic callback model, capture logs and create a focused follow-up research/plan item rather than masking it with retries, sleeps, fixed ports, or serialization.

Dependencies:
* All prior phases

## Dependencies

* Docker Desktop, .NET SDK 10.0.302, test-safe Keycloak realm/client configuration, required SQL image availability, and Playwright browser installation.

## Success Criteria

* All current test failures are resolved through dependency compatibility or owned fixture lifecycle changes.
* Durable instruction and wiki artifacts enforce the selected future testing direction.
* Full solution validation passes under ordinary concurrency.
