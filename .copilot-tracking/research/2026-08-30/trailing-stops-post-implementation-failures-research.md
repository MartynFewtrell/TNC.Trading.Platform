<!-- markdownlint-disable-file -->
# Task Research: Trailing-Stops Post-Implementation Failures

Research the test failures and application startup regression reported after completing the trailing-stops feature plan.

## Task Implementation Requests

* Reproduce and classify the new test failures.
* Reproduce the application startup failure.
* Identify root causes attributable to the trailing-stops implementation or its integration.
* Propose evidence-based, prioritized solutions without modifying product or test code.

## Scope and Success Criteria

* Scope: Build, automated tests, application startup, database migration/configuration, dependency injection, API, web UI, and Aspire orchestration affected by the trailing-stops implementation.
* Exclusions: No product, test, configuration, database, or wiki fixes are applied by this research task. Automated tests must not call real IG.
* Assumptions: The attached completed plan describes intended behavior; the current worktree contains uncommitted user changes that must not be altered.
* Success Criteria:
  * Capture reproducible failure signatures without allowing tests to run indefinitely.
  * Separate code defects from environment-dependent or pre-existing failures.
  * Map each root cause to exact workspace files and line ranges.
  * Select one prioritized remediation approach with focused validation steps.

## Outline

1. Establish the implementation footprint and retained validation evidence.
2. Reproduce startup directly and reconstruct test failure cascades from fixtures.
3. Audit feature integration for defects hidden by passing focused tests.
4. Separate startup restoration from release-blocking correctness work.
5. Select a staged repair and bounded validation sequence.

## Potential Next Research

* Inventory `20260830112227_SnapshotRepair` in CI, shared, staging, and deployed databases.
  * Reasoning: Its deployment scope determines whether migration history may be regenerated or must be preserved.
  * Reference: `.copilot-tracking/research/subagents/2026-08-30/trailing-stops-startup-failure-research.md:123-203`
* Approve the public API contract for a provider confirmation that differs from the requested value.
  * Reasoning: The selected behavior is a typed not-applied outcome mapped to HTTP 409, but the exact Problem Details extensions remain a product contract decision.
  * Reference: `src/TNC.Trading.Platform.Application/Features/AccountPreferences/UpdateAccountPreferencesHandler.cs:20-31`
* Capture sanitized manual IG Test evidence for `GET` and `PUT /accounts/preferences`.
  * Reasoning: Endpoint availability and allowance behavior remain unverified and must not be tested automatically against a real broker account.
  * Reference: `https://labs.ig.com/reference/accounts-preferences.html`

## Research Executed

### File Analysis

* `.copilot-tracking/research/subagents/2026-08-30/trailing-stops-startup-failure-research.md:1-310`
  * Reproduced AppHost composition and direct API startup, identified the exact endpoint-construction exception, inspected SQL migration history, and proved the duplicate migration hazard.
* `.copilot-tracking/research/subagents/2026-08-30/trailing-stops-build-test-failures-research.md:1-287`
  * Reconstructed the 15-test failure cascade, inventoried fixture timeout budgets, distinguished apparent hangs from unbounded SQL risks, and specified externally bounded checks.
* `.copilot-tracking/research/subagents/2026-08-30/trailing-stops-static-integration-audit-research.md:1-304`
  * Audited application, infrastructure, API, Web, migration, and test integration against the accepted plan and neighboring patterns.
* `.copilot-tracking/research/subagents/2026-08-30/trailing-stops-remediation-alternatives-research.md:1-307`
  * Evaluated repair alternatives and selected a conservative, failure-first remediation sequence.
* `.copilot-tracking/research/subagents/2026-08-30/account-details-test-failure-evidence.md:22-51`
  * Verified an independent account-details unit-test failure caused by a hard-coded date combined with `TimeProvider.System`.

### Code Search Results

* New account-preferences route dependencies
  * Four route services are required by `src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs:54-60` and `src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs:130-146`.
  * None are registered by `src/TNC.Trading.Platform.Api/Hosting/PlatformApplicationServiceCollectionExtensions.cs:18-46`.
* Distributed test waits
  * Real API discovery allows 120 seconds in `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/RealAuthenticationIntegrationTestFixture.cs:13-31`.
  * Synthetic API readiness allows five minutes in `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/PlatformAuthenticationIntegrationTestRuntime.cs:5-37`.
  * Keycloak lease acquisition allows five minutes in `test/Shared/Authentication/KeycloakPortLease.cs:5-54`.
  * Web sign-in discovery allows 120 seconds through `test/Shared/Authentication/RealAppHostProcessFactory.cs:45-99`.
* Migration identity
  * `_SnapshotRepair/20260830112227_SnapshotRepair.Designer.cs:12-16` supplies discoverable migration metadata.
  * `20260830120000_AddTrailingStopsPreferenceObservations.cs:1-42` duplicates the table but has no designer or migration metadata.

### External Research

* IG Labs Accounts Preferences reference
  * The accepted provider contract uses `trailingStopsEnabled` for both GET and PUT.
  * Source: [IG Labs Accounts Preferences](https://labs.ig.com/reference/accounts-preferences.html)

### Project Conventions

* Standards referenced: `.github/copilot-instructions.md`, `.github/instructions/architecture.instructions.md`, `.github/instructions/aspire-tests.instructions.md`, `.github/instructions/configuration.instructions.md`, `.github/instructions/tests.instructions.md`, and applicable C#, .NET, Radzen Blazor, Scalar, folder, Markdown, and writing-style instructions.
* Instructions followed: Research-only mode; no source, test, configuration, database, or wiki files were modified.
* Existing worktree changes were treated as user changes and preserved.

## Key Discoveries

### Project Structure

The trailing-stops work is an uncommitted vertical slice spanning Application contracts and handlers, an IG gateway, EF persistence and migrations, API routes, Web UI, tests, configuration, and wiki documentation. The baseline is commit `ac8d6f3` on branch `012-add-account-details`. The audit found 18 tracked modifications and 40 untracked files attributable to the implementation footprint.

The API uses an explicit application-service composition list rather than assembly scanning. Infrastructure ports for the observation store and IG gateway are registered, but the API-owned Application handlers and validator are not.

### Implementation Patterns

#### Immediate startup root cause

The API builds and completes SQL startup initialization, then exits while constructing Minimal API endpoints:

```text
System.InvalidOperationException: Body was inferred but the method does not allow inferred body parameters.
Parameter | Source
handler   | Body (Inferred)
Did you mean to register the "Body (Inferred)" parameter(s) as a Service or apply the [FromServices] or [FromBody] attribute?
```

The three new routes require `GetAccountPreferencesHandler`, `UpdateAccountPreferencesValidator`, `UpdateAccountPreferencesHandler`, and `GetTrailingStopsPreferenceObservationsHandler`. Because none are registered, ASP.NET Core treats the first complex GET parameter as an inferred body and rejects the route graph. The failure terminates the API, and Web remains unavailable because AppHost configures Web to wait for API.

Confidence: very high. The exception was reproduced by running the API directly against the existing SQL container after successful migration initialization.

#### Why the tests appear to run forever

The reported 15 failures align with the 15 facts in `PlatformAuthenticationIntegrationTests`. Their shared class fixture starts AppHost once, then waits up to 120 seconds for an API listener. The API has already exited, so fixture initialization times out before any test body runs and xUnit reports the same fixture failure against every fact.

Other test paths can amplify the delay:

* Synthetic API readiness can poll for five minutes.
* Web functional and E2E fixtures can wait 120 seconds for sign-in discovery while Web is blocked behind API.
* AppHost projects serialize access to fixed Keycloak port 8080 through a lease that can wait five minutes.
* SQL fixture Docker, connection, reset, and disposal operations have no internal cancellation or command timeout.

No individual authentication test body is proven to hang. The observed behavior is a fast API crash hidden behind long fixture polling. The SQL fixture has a genuine unbounded-wait risk, but no retained August 30 result proves that a SQL operation caused the reported run.

#### Independent test failure

`CaptureAutomaticAsync_ShouldDeduplicateSuccessAndRetryAfterFailure` fails in milliseconds because the test hard-codes trading day 2026-08-07 at `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/AccountDetails/AccountDetailsHandlerTests.cs:96-120` while injecting `TimeProvider.System` at the same file's lines 145-163. On 2026-08-30 production computes a different trading day and reaches a different outcome. This is deterministic date drift, not a trailing-stops regression.

#### Migration history defect

The local persistent database has `20260830112227_SnapshotRepair` applied. That migration creates `TrailingStopsPreferenceObservations` and two indexes. The intended later `AddTrailingStopsPreferenceObservations` source creates the same table but lacks generated migration metadata, so EF does not discover it.

Making the later migration discoverable without reconciling history would execute a duplicate `CREATE TABLE`; a direct SQL probe returned error 2714. `PlatformDbContext` also configures the entity twice, and current SQL tests assert table shape without asserting the intended migration identifier.

This defect did not cause the reproduced startup failure because API migration initialization completed. It remains release-blocking schema debt.

#### Correctness defects hidden by passing tests

Forty focused new tests passed, but their mocks and test boundaries encode or omit several defects:

* The IG gateway sends and parses `trailingStops`, while the provider contract requires `trailingStopsEnabled`, at `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgAccountPreferencesGateway.cs:15-17` and the same file's lines 78-99.
* Update success accepts any successful confirmation GET, even when its Boolean differs from the requested state, at `src/TNC.Trading.Platform.Application/Features/AccountPreferences/UpdateAccountPreferencesHandler.cs:20-31`.
* Malformed 2xx PUT acknowledgements are treated as definite failure rather than an uncertain write requiring GET reconciliation at `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgAccountPreferencesGateway.cs:78-89`.
* GET failure telemetry omits the injected event store at `src/TNC.Trading.Platform.Application/Features/AccountPreferences/GetAccountPreferencesHandler.cs:22-41`; update telemetry skips or collapses failed and indeterminate operations at `src/TNC.Trading.Platform.Application/Features/AccountPreferences/UpdateAccountPreferencesHandler.cs:33-38`.
* History reads are global rather than partitioned by current platform and broker environment at `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfTrailingStopsPreferenceObservationStore.cs:28-50`.
* Malformed, unknown, and cross-partition cursors can silently restart paging from the newest global page at `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfTrailingStopsPreferenceObservationStore.cs:28-50` and `src/TNC.Trading.Platform.Api/Features/Platform/GetTrailingStopsPreferenceObservationsEndpointHandler.cs:10-23`.
* The Test-only guard checks legacy broker `Demo` but not application `Test` status at `src/TNC.Trading.Platform.Application/Features/AccountPreferences/AccountPreferencesEnvironmentPolicy.cs:5-10`.
* API history presentation leaks legacy `Demo` instead of presenting `Test` at `src/TNC.Trading.Platform.Api/Features/Platform/AccountPreferencesEndpointMapping.cs:9-13`.
* `Retention:TrailingStopsPreferenceObservationsDays` at `src/TNC.Trading.Platform.Api/appsettings.json:32-37` is inert because the processor binds a different setting at `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Operations/Retention/OperationalRecordRetentionProcessor.cs:16-21` and uses a fixed trailing-stops cutoff at the same file's lines 52-55 and 86-89.
* The Web page uses `CancellationToken.None` and does not cancel HTTP operations on component disposal at `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:27-35`.

The completed plan and execution log conflict: the checklist marks final validation complete, while the log records unresolved distributed startup failures.

### Complete Examples

The first source repair should follow the API's existing explicit registration pattern:

```csharp
services.AddScoped<GetAccountPreferencesHandler>();
services.AddScoped<UpdateAccountPreferencesValidator>();
services.AddScoped<UpdateAccountPreferencesHandler>();
services.AddScoped<GetTrailingStopsPreferenceObservationsHandler>();
```

An API composition test should use the real `AddPlatformApplication` registration, map `MapPlatformEndpoints`, force endpoint metadata construction, and resolve all four services. Static endpoint-helper tests with `null!` handlers do not exercise the failing route graph.

### API and Schema Documentation

The provider request and response property must be `trailingStopsEnabled`. Keep the nullable HTTP-only request member because it distinguishes omitted JSON from explicit `false`.

Confirmed update semantics should be:

1. Send one PUT with no blind retry.
2. Perform an authoritative GET after a successful or indeterminate PUT.
3. Return confirmed success only when the observed Boolean equals the requested Boolean.
4. Persist valid observed state even when it differs from the request.
5. Return a typed not-applied outcome, recommended as HTTP 409, when the observed value differs.
6. Emit one bounded, sanitized operational event for each failed or indeterminate provider operation.

History queries should partition by `PlatformEnvironment` and legacy `BrokerEnvironment`. A GUID cursor is sufficient for the current schema if syntax is validated before dispatch and the ID must exist in the selected partition. Malformed, unknown, and cross-partition cursors should return validation errors rather than restart at page one.

### Configuration Examples

Retain one effective retention setting. The accepted initial product decision reuses the validated operational-record retention plan:

```json
{
  "Retention": {
    "OperationalRecordsDays": 90
  }
}
```

Remove the inert trailing-stops-specific key and fixed constant, then use the validated operational cutoff for both SQL and in-memory deletion paths. A separate bound option is viable only if product owners explicitly require independent retention.

## Technical Scenarios

### Restore Build, Test, and Startup Health

The selected approach is a conservative, failure-first repair. The four missing API registrations are the only confirmed initiating production blocker in the reproduced database state; post-fix startup remains unverified and may expose a later blocker. Registration repair unblocks the route graph, while fail-fast fixture behavior is required before broad distributed validation can provide bounded diagnostics.

**Requirements:**

* Preserve explicit dependency registration.
* Detect invalid route composition without starting Aspire.
* Cap all test and startup checks below 60 seconds.
* Fail fixtures when API reaches a terminal state instead of polling until deadline.
* Preserve child-process diagnostics and clean up resources created by each check.

**Preferred Approach:**

1. Add the four missing scoped registrations.
2. Add an in-process route-composition and service-resolution test.
3. Propagate a shared sub-60-second cancellation token through Aspire create, build, start, health, and discovery.
4. Detect an exited or failed API resource immediately and include its captured diagnostics.
5. Run one exact-filter test per fixture before broadening by project.
6. Add cancellation and command timeouts to every SQL fixture Docker and SQL operation.
7. Repair the unrelated account-details test with a fixed `TimeProvider`.

#### Considered Alternatives

* Add `[FromServices]` without registrations: rejected because requests still fail service resolution.
* Introduce assembly scanning: rejected because the repository uses explicit composition and the larger change has no demonstrated benefit for this repair.
* Increase Aspire timeouts: rejected because it lengthens the cascade and hides an early deterministic crash.
* Run the full solution first: rejected because one fixture initialization failure fans out across dependent tests and obscures independent signal.
* Reset SQL as the startup fix: rejected because endpoint composition still fails and the reset destroys durable local state.

### Reconcile Migration History

**Requirements:**

* Do not rewrite migration history that may exist in non-disposable environments.
* Avoid duplicate table creation.
* Remove duplicate model configuration.
* Validate empty-database and in-place upgrade paths.

**Preferred Approach:**

Preserve `20260830112227_SnapshotRepair` by default. Remove the undiscoverable duplicate migration, deduplicate entity configuration, regenerate the model snapshot, and add a later corrective migration only for a genuine schema difference. SQL tests must assert applied migration IDs as well as table and index shape.

If every owner proves `SnapshotRepair` exists only in disposable local databases, deleting both artifacts and generating one canonical migration is cleaner. That alternative must not be chosen from the current evidence.

#### Considered Alternatives

* Add metadata to the duplicate named migration: rejected because existing databases execute duplicate `CREATE TABLE` and fail with SQL error 2714.
* Delete `SnapshotRepair` immediately: rejected because its non-disposable deployment scope is unknown.
* Keep both source artifacts: rejected because the migration sequence remains misleading and unsafe.

### Restore Provider and Feature Correctness

**Requirements:**

* Match the official IG wire contract.
* Never report a mismatching provider observation as confirmed success.
* Never blind-retry PUT.
* Preserve valid observations and sanitized failure evidence.
* Keep Test/Demo compatibility internal while presenting Test externally.

**Preferred Approach:**

Use typed provider request and response DTOs with `trailingStopsEnabled`. Treat a malformed successful PUT acknowledgement as indeterminate, reconcile once through GET, and compare the observed value with the request. Represent mismatch as typed not-applied behavior mapped to HTTP 409. Wire telemetry through real handler paths and emit one event per failed or indeterminate operation.

Partition history, validate cursor syntax and membership, enforce both platform Test and broker Demo compatibility, apply the existing presentation mapper, share the operational retention setting, and cancel Web operations when the component is disposed.

#### Considered Alternatives

* Return generic success with a mismatching observed value: rejected because it falsely claims the update was applied.
* Treat malformed 2xx acknowledgement as definite failure: rejected because the write may already have occurred.
* Keep global history queries: rejected because observations from different environments can mix.
* Keep the inert dedicated retention setting: rejected because operator configuration appears effective when it is not.

## Selected Remediation Sequence

### Stage 0: Migration Decision Gate

Inventory `__EFMigrationsHistory` in every non-disposable environment. Preserve `SnapshotRepair` unless all owners prove it is local-only. This parallel decision track blocks Stage 3 migration edits only; it does not block Stage 1 composition repair or its focused route test.

### Stage 1: Restore Startup and Test Signal

Add the four API registrations and an in-process route-composition test. Add sub-60-second fixture cancellation, terminal API-state detection, and captured child diagnostics. Run one API integration smoke and one Web/AppHost smoke after the composition test passes.

### Stage 2: Restore Provider and Command Correctness

Correct the IG property, classify uncertain acknowledgements, require value equality for confirmation, add the typed mismatch outcome, and repair handler-path telemetry.

### Stage 3: Reconcile Persistence

Apply the approved migration-history strategy, remove duplicate entity configuration, regenerate the snapshot, and test empty and already-applied upgrade paths.

### Stage 4: Complete Feature Correctness

Partition history, validate cursors, enforce Test/Demo policy and presentation, remove inert retention configuration, and add component-owned cancellation.

### Stage 5: Correct Delivery Records

After all gates pass, update wiki behavior and correct the plan checklist and execution log so they no longer claim validation that did not complete.

## Bounded Validation Strategy

Every executable command must have an external 60-second watchdog that terminates the complete process tree and captures standard output and error. Test commands should also use both `--blame-hang` and `--blame-hang-timeout 45s`, plus `--no-restore` and, after a separate successful build, `--no-build`. A timeout is a failed gate, not a reason to increase the budget.

Run gates in this order and stop at the first failure:

1. Build Application, Infrastructure, API, and Web separately.
2. Run only the API route-composition test.
3. Run only Account Preferences Application tests.
4. Run IG gateway, EF store, and retention unit slices separately.
5. Run one SQL fact at a time for empty migration, applied-`SnapshotRepair` upgrade, migration ID, equal-timestamp paging, and retention.
6. Start API under a 55-second process-tree watchdog and probe readiness; fail immediately if the process exits.
7. Run exactly one synthetic-token API integration fact.
8. Run exactly one AppHost-backed Web or E2E smoke fact.
9. Run only Account Preferences Web unit tests.
10. Broaden by project only after every preceding gate passes; split projects whose normal duration exceeds 60 seconds.

The same watchdog pattern applies to builds, startup, and tests. For a long-running API or AppHost process, add a readiness probe inside the deadline and terminate the captured process tree after readiness succeeds. Always include bounded output tails in timeout and nonzero-exit diagnostics.

Example reusable watchdog and exact test filter:

```powershell
function Invoke-BoundedProcess {
  param(
    [string] $FilePath,
    [string[]] $ArgumentList,
    [int] $TimeoutMilliseconds = 55000
  )

  $outputPath = [System.IO.Path]::GetTempFileName()
  $errorPath = [System.IO.Path]::GetTempFileName()
  $process = Start-Process $FilePath -ArgumentList $ArgumentList -PassThru `
    -RedirectStandardOutput $outputPath -RedirectStandardError $errorPath

  try {
    if (-not $process.WaitForExit($TimeoutMilliseconds)) {
      taskkill /PID $process.Id /T /F | Out-Null
      $outputTail = Get-Content $outputPath -Tail 40 -ErrorAction SilentlyContinue | Out-String
      $errorTail = Get-Content $errorPath -Tail 40 -ErrorAction SilentlyContinue | Out-String
      throw "Process exceeded $TimeoutMilliseconds ms.`nSTDOUT:`n$outputTail`nSTDERR:`n$errorTail"
    }

    if ($process.ExitCode -ne 0) {
      $outputTail = Get-Content $outputPath -Tail 40 -ErrorAction SilentlyContinue | Out-String
      $errorTail = Get-Content $errorPath -Tail 40 -ErrorAction SilentlyContinue | Out-String
      throw "Process failed with exit code $($process.ExitCode).`nSTDOUT:`n$outputTail`nSTDERR:`n$errorTail"
    }
  }
  finally {
    Remove-Item $outputPath, $errorPath -Force -ErrorAction SilentlyContinue
  }
}

$project = 'test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/TNC.Trading.Platform.Api.IntegrationTests.csproj'
$filter = 'FullyQualifiedName=TNC.Trading.Platform.Api.IntegrationTests.Authentication.PlatformAuthenticationIntegrationTests.HealthEndpoints_ShouldReturnOk_WhenRequestedAnonymously'
Invoke-BoundedProcess dotnet @(
  'test', $project, '--no-build', '--no-restore', '--filter', $filter,
  '--blame-hang', '--blame-hang-timeout', '45s'
)
```

Record application process IDs and container IDs before AppHost checks. In `finally`, stop only resources created by that check. Keep intentionally persistent SQL, Mailpit, and Keycloak resources running when they predated the test.

## Evidence Limits

* No fresh test suite was run after the user reported indefinite execution. Existing direct runtime evidence, retained reports, fixture source, and bounded static analysis were sufficient.
* The original 15-failure TRX was not retained. Mapping those failures to the unique 15-fact real-authentication class is high-confidence reconstruction, not parsed result evidence.
* The 11 synthetic API tests, six Web functional tests, and one Web E2E test are proven dependent on the failed startup graph, but no retained result proves they were members of the observed 15.
* SQL fixture operations are structurally unbounded, but no specific August 30 SQL test is proven to have hung.
* Real IG Test endpoint behavior remains unverified.
