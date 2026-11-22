---
title: Trailing Stops Remediation Alternatives Research
description: Phase 2 analysis of remediation choices for trailing-stops post-implementation failures
author: GitHub Copilot
ms.date: 2026-08-30
ms.topic: troubleshooting
---

## Status

Complete. Research-only analysis completed against the uncommitted worktree on
2026-08-30. No production or test files were modified.

## Research Questions

* Which remediation options best restore missing API dependency injection registrations and startup?
* Which changes isolate genuine test hangs from Aspire timeout cascades?
* Which migration cleanup is safe when `SnapshotRepair` may already be applied?
* Which changes are required for startup and test restoration, and which are follow-up correctness work?
* How should endpoint DTO shape, IG wire format, confirmation semantics, telemetry, history validation, environment policy, retention, and UI cancellation be corrected?
* Which bounded checks can validate each stage without any command running longer than 60 seconds?

## Evidence Reviewed

All existing same-day trailing-stops reports were read:

* `.copilot-tracking/research/2026-08-30/trailing-stops-post-implementation-failures-research.md` was the primary research scaffold when this alternatives analysis began and has since been completed.
* `.copilot-tracking/research/subagents/2026-08-30/trailing-stops-build-test-failures-research.md` was in progress when this alternatives analysis began and has since been completed. The original TRX still was not retained, so the 15 failed test names remain a high-confidence fixture reconstruction rather than parsed result evidence.
* `.copilot-tracking/research/subagents/2026-08-30/trailing-stops-startup-failure-research.md:89-150` reproduces the API startup exception and traces it to missing route-service registrations.
* `.copilot-tracking/research/subagents/2026-08-30/trailing-stops-startup-failure-research.md:154-203` proves that `SnapshotRepair` is applied locally, owns the feature table, and would conflict with the undiscoverable duplicate migration.
* `.copilot-tracking/research/subagents/2026-08-30/trailing-stops-static-integration-audit-research.md:67-252` identifies the remaining contract, persistence, telemetry, policy, retention, and lifecycle defects.

Current source was checked directly for every decision below. The controlling
intent is also explicit in
`.copilot-tracking/details/2026-08-17/trailing-stops-features-details.md:99-133`,
`.copilot-tracking/details/2026-08-17/trailing-stops-features-details.md:154-181`,
and
`.copilot-tracking/details/2026-08-17/trailing-stops-features-details.md:203-232`.

## Alternatives Analysis

### API Composition And Startup

Evidence:

* The new delegates require four services at `src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs:51-61` and `src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs:129-146`.
* The explicit composition list ends without those services at `src/TNC.Trading.Platform.Api/Hosting/PlatformApplicationServiceCollectionExtensions.cs:18-48`.
* Infrastructure ports are already registered, so this is not an Infrastructure DI omission. The reproduced host reaches endpoint construction and fails with `Body was inferred` as recorded at `.copilot-tracking/research/subagents/2026-08-30/trailing-stops-startup-failure-research.md:100-150`.

Options:

1. Add explicit scoped registrations for the three handlers and validator, then add an API composition test that uses the real registration and route mapping. This matches the existing composition style and detects both missing DI and invalid endpoint inference. Selected.
2. Add `[FromServices]` to delegate parameters. Reject as a standalone fix: endpoint construction may proceed, but requests still fail when the services cannot resolve.
3. Introduce assembly scanning or mediator-style auto-registration. Reject for this repair: it changes the repository's explicit composition convention and expands the blast radius without solving a demonstrated abstraction problem.

This is the only confirmed production change required to restore API and AppHost
startup in the reproduced database state.

### Test Hangs And Aspire Timeout Cascades

Evidence:

* AppHost starts while API exits and Web waits behind API, as recorded at `.copilot-tracking/research/subagents/2026-08-30/trailing-stops-startup-failure-research.md:60-84`.
* The shared managed AppHost path has a 90-second startup deadline at `test/Shared/Authentication/RealAppHostProcessFactory.cs:45-78`.
* Real API and Web discovery each allow 120 seconds at `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/RealAuthenticationIntegrationTestFixture.cs:14-29` and `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/Authentication/AppHostProcessFactory.cs:12-16`.
* Synthetic API initialization calls `CreateAsync`, `BuildAsync`, and `StartAsync` without a caller deadline at `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/SyntheticTokenIntegrationTestFixture.cs:18-33`, followed by a five-minute readiness loop at `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/PlatformAuthenticationIntegrationTestRuntime.cs:7-37`.
* URI discovery intentionally suppresses transient request failures while polling at `test/Shared/Authentication/AppHostProcessHandle.cs:76-120`. That behavior turns an early child-process crash into a late timeout.

Options:

1. Repair DI first, prove the route graph in-process, then run one AppHost fixture slice under a 60-second external deadline. Add one shared startup token capped below 60 seconds to create/build/start/readiness, fail early when API terminates, and preserve child output. Selected.
2. Increase readiness and lease timeouts. Reject: this lengthens the cascade and hides the deterministic startup exception.
3. Run the full solution to inventory failures. Reject until startup is green: fixture initialization failures fan out across dependent tests and obscure independent failures.
4. Treat every reported timeout as an independent test defect. Reject: the available evidence supports a common dead API dependency, while the 15-test member inventory was not retained.

The DI repair is required for test restoration. Shorter cancellation propagation,
child-exit detection, and diagnostics are required harness hardening before broad
distributed validation, but they do not replace the production fix.

### EF Migration Identity And Existing Databases

Evidence:

* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/Migrations/_SnapshotRepair/20260830112227_SnapshotRepair.Designer.cs:12-16` makes `SnapshotRepair` discoverable.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/Migrations/_SnapshotRepair/20260830112227_SnapshotRepair.cs:11-54` creates the table and two indexes.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/Migrations/20260830120000_AddTrailingStopsPreferenceObservations.cs:1-42` duplicates the table but has no migration metadata.
* The entity is configured twice at `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/PlatformDbContext.cs:147-159` and `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/PlatformDbContext.cs:176-182`.
* The feature SQL test checks the table and one index, not the applied migration identity, at `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/AccountPreferencesSqlIntegrationTests.cs:14-26`.

Options:

1. Preserve `20260830112227_SnapshotRepair` as immutable applied history, remove the undiscoverable duplicate, deduplicate the model, regenerate the snapshot, and add a later corrective migration only if the corrected model differs from the schema. Selected because non-disposable deployment scope is unknown.
2. Delete both artifacts and generate one canonical `AddTrailingStopsPreferenceObservations` migration. Cleaner history, but select it only after proving `SnapshotRepair` never reached a shared, CI, staging, or deployed database and explicitly resetting every disposable database where it was applied.
3. Add metadata to the current named migration. Reject: a database with `SnapshotRepair` already applied executes a duplicate `CREATE TABLE`; the direct SQL probe produced error 2714 at `.copilot-tracking/research/subagents/2026-08-30/trailing-stops-startup-failure-research.md:180-194`.
4. Reset SQL without source reconciliation. Reject: it loses local durable state and does not fix API DI.

### HTTP DTO And Provider Wire Contract

Keep `UpdateAccountPreferencesHttpRequest(bool? TrailingStopsEnabled)` at
`src/TNC.Trading.Platform.Api/Features/Platform/AccountPreferencesEndpointMapping.cs:45`.
The nullable HTTP-only DTO is intentional at
`.copilot-tracking/details/2026-08-17/trailing-stops-features-details.md:203-209`:
it distinguishes an omitted member from an explicit `false`. Binding a plain
`bool` or the Application request directly is rejected.

Correct the provider boundary to serialize and deserialize
`trailingStopsEnabled`. Current PUT serialization uses `trailingStops` at
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgAccountPreferencesGateway.cs:15-17`; current GET parsing repeats that wrong name at
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgAccountPreferencesGateway.cs:92-99`.
The accepted provider research states the exact property at
`.copilot-tracking/research/2026-08-17/trailing-stops-features-research.md:12-17`.
Using one typed provider request/response pair is preferred over reflection on an
anonymous object.

### Confirmation And Public Response Semantics

Current behavior returns any successful confirmation GET as success at
`src/TNC.Trading.Platform.Application/Features/AccountPreferences/UpdateAccountPreferencesHandler.cs:20-31`, and its test explicitly requests `false`, observes `true`, and calls that confirmed at
`test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/AccountPreferences/AccountPreferencesHandlerTests.cs:39-54`.

Selected behavior:

* A successful PUT must be followed by a GET whose Boolean equals the requested Boolean before returning `Succeeded` and appending `UpdateConfirmed`.
* A valid GET with a different Boolean remains a valuable observation, but returns a typed `NotApplied` or equivalent mismatch outcome. Map it to a stable `409` response and do not label it confirmed.
* An indeterminate PUT must be recorded as an indeterminate provider operation, followed by one GET and no PUT retry. A valid reconciled GET is appended as `ReconciliationObserved`; it becomes confirmed success only when it equals the request.
* A malformed 2xx PUT acknowledgement is indeterminate, not definitely failed, because the provider may have applied the write. Current definite-failure classification is at `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgAccountPreferencesGateway.cs:83-89`.

Returning `200` with the mismatching provider value is rejected because it claims
the requested update succeeded. Expanding every successful response with an
`Applied` flag is also rejected: the existing confirmed platform DTO is adequate
for success, while mismatch deserves a distinct outcome and status.

### Failure Telemetry

Pass the injected event store through the real GET failure path and make the
store parameter non-optional. The current handler omits it at
`src/TNC.Trading.Platform.Application/Features/AccountPreferences/GetAccountPreferencesHandler.cs:22-31`, while the helper silently returns when null at
`src/TNC.Trading.Platform.Application/Features/AccountPreferences/GetAccountPreferencesHandler.cs:36-41`.

Emit one bounded, allow-listed event for each failed or indeterminate provider
operation, including the initial PUT even when reconciliation succeeds and the
reconciliation GET when it fails. Continue to exclude raw response bodies,
credentials, tokens, arbitrary codes, and stacks as required by
`.copilot-tracking/details/2026-08-17/trailing-stops-features-details.md:114-131`.
Testing only the helper, as at
`test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/AccountPreferences/AccountPreferencesHandlerTests.cs:116-131`,
is rejected because it does not prove handler wiring.

### History Partitioning And Cursor Validation

The store contract currently accepts only cursor and page size at
`src/TNC.Trading.Platform.Application/Features/AccountPreferences/ITrailingStopsPreferenceObservationStore.cs:3-7`.
Its EF implementation queries globally, accepts any GUID, and silently restarts
at the newest page for malformed or unknown cursors at
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfTrailingStopsPreferenceObservationStore.cs:28-50`.
The HTTP boundary checks only empty and overlong text at
`src/TNC.Trading.Platform.Api/Features/Platform/GetTrailingStopsPreferenceObservationsEndpointHandler.cs:10-23`.

Selected behavior:

* Partition every read and cursor lookup by current `PlatformEnvironment` and legacy `BrokerEnvironment` from configuration.
* Keep account ID out of the initial partition until the gateway exposes a trustworthy account identifier; silently filtering on null would provide false isolation.
* Define one deterministic cursor contract. A GUID is sufficient for the current schema if syntax is validated before dispatch and the ID must exist within the selected partition; malformed, unknown, or cross-partition cursors return a typed validation error rather than page one.
* Preserve ordering by observed timestamp plus ID and add SQL Server coverage for equal timestamps and a non-null cursor.

Embedding raw environment values in an unsigned client cursor is rejected. A
versioned opaque cursor could replace GUIDs later, but is not required to repair
the current contract.

### Environment Policy And Presentation

Require both `PlatformEnvironmentKind.Test` and the compatible legacy
`BrokerEnvironmentKind.Demo`. The current policy accepts only the broker key at
`src/TNC.Trading.Platform.Application/Features/AccountPreferences/AccountPreferencesEnvironmentPolicy.cs:5-10`, although both values are available on
`src/TNC.Trading.Platform.Application/Configuration/PlatformConfigurationSnapshot.cs:3-14`.

Use `BrokerEnvironmentPresentation.ForAccountPreferences` from
`src/TNC.Trading.Platform.Application/Configuration/BrokerEnvironmentPresentation.cs:3-13`
when creating API-facing history models. Current mapping leaks `Demo` via
`ToString()` at
`src/TNC.Trading.Platform.Api/Features/Platform/AccountPreferencesEndpointMapping.cs:9-13`.
Renaming persisted `Demo` data is rejected for this repair because the selected
compatibility strategy deliberately preserves the provider key while presenting
the application status as `Test`.

### Retention Configuration

The dedicated setting at `src/TNC.Trading.Platform.Api/appsettings.json:32-37`
is inert: the processor reads `Retention:OperationalRecordsDays` at
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Operations/Retention/OperationalRecordRetentionProcessor.cs:16-21`, but trailing-stop deletion uses a separate constant at
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Operations/Retention/OperationalRecordRetentionProcessor.cs:52-55` and
`src/TNC.Trading.Platform.Application/Services/TrailingStopsPreferenceObservationRetentionPolicy.cs:3-8`.

Select the original product decision: reuse the validated operational-retention
plan for this initial history, as stated at
`.copilot-tracking/research/2026-08-17/trailing-stops-features-research.md:31-36`.
Remove the inert dedicated key and constant, and use the plan cutoff in both SQL
and in-memory branches. Binding a dedicated option is viable only if product
owners now require independently configurable retention; leaving two apparent
settings with only one effective value is rejected.

### UI Cancellation

Give the page an owned `CancellationTokenSource`, pass its token to load, save,
and paging, cancel it during asynchronous disposal, and treat caller cancellation
as normal teardown. Current operations all use `CancellationToken.None` at
`src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:27-34`.
The presenter currently catches only request and invalid-operation failures at
`src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPagePresenter.cs:10-39`,
so disposal cancellation should not become an error banner. A global static token
or per-click unowned token is rejected because neither follows component lifetime.

## Selected Staged Approach

Use a conservative, failure-first sequence.

### Stage 0: Establish The Migration Decision Gate

Inventory `__EFMigrationsHistory` in every non-disposable environment. Unless all
owners prove `SnapshotRepair` is local-only, preserve its identity. This stage is
read-only and blocks destructive migration cleanup.

### Stage 1: Restore Startup And Bound Test Failure

* Add the four explicit API registrations.
* Add an in-process API composition/route-graph test that resolves all four services and constructs endpoint metadata.
* Propagate a shared sub-60-second cancellation token through Aspire create, build, start, health, and discovery. Detect API exit before polling to deadline and include child output.
* Run only the API composition test, then one synthetic API fixture test, then one Web/AppHost smoke test.

Stage 1 is required to restore startup and obtain trustworthy test results. Do
not combine later behavioral changes into this gate.

### Stage 2: Restore Provider And Command Correctness

* Preserve the nullable HTTP request DTO.
* Correct `trailingStopsEnabled` request and response JSON with typed provider DTOs.
* Treat malformed write acknowledgements as indeterminate.
* Require requested-value equality for confirmed success; add a typed mismatch outcome and truthful Web response.
* Wire bounded telemetry through real handler paths for every failed or indeterminate operation.

These are release-blocking correctness fixes, but they do not cause the current
startup exception.

### Stage 3: Reconcile Persistence Safely

* Preserve `SnapshotRepair` by default.
* Remove the undiscoverable duplicate migration and duplicate entity configuration.
* Regenerate the model snapshot and generate a corrective successor only for a real schema delta, such as dropping an unintended broker-only index.
* Extend SQL tests to assert migration IDs, empty-database migration, and upgrade from an already-applied `SnapshotRepair` state.

This is release-blocking schema hygiene. It is not required for startup against
the reproduced local database, which already passed `MigrateAsync`.

### Stage 4: Complete Feature Correctness

* Partition history and reject malformed, unknown, and cross-partition cursors.
* Enforce platform Test plus broker Demo compatibility and map Demo to Test at API presentation.
* Reuse the operational retention plan and remove inert configuration.
* Add component-owned cancellation and disposal coverage.

These are follow-up correctness and lifecycle fixes. They should land before the
feature is declared complete, but after startup and test signal are restored.

## Bounded Validation Strategy

Every command must have an external 60-second wall-clock deadline that kills the
entire process tree on expiry. Test commands should also use
`--blame-hang-timeout 45s`, `--no-restore`, and, after a separate successful
build, `--no-build`. A timeout is a failed validation result, not permission to
increase the budget.

Run these gates in order, stopping at the first failure:

1. Build only the touched project. Use `dotnet build <project> --no-restore` separately for Application, Infrastructure, API, and Web; never build the solution in this bounded loop.
2. Run API route composition only: `dotnet test test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/TNC.Trading.Platform.Api.UnitTests.csproj --no-build --no-restore --filter "FullyQualifiedName~AccountPreferencesComposition" --blame-hang-timeout 45s`.
3. Run Application behavior only: `dotnet test test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/TNC.Trading.Platform.Application.UnitTests.csproj --no-build --no-restore --filter "FullyQualifiedName~AccountPreferences" --blame-hang-timeout 45s`.
4. Run IG adapter and store unit slices separately: `dotnet test test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/TNC.Trading.Platform.Infrastructure.UnitTests.csproj --no-build --no-restore --filter "FullyQualifiedName~IgAccountPreferencesGateway" --blame-hang-timeout 45s`, then repeat with `FullyQualifiedName~EfTrailingStopsPreferenceObservationStore` and `FullyQualifiedName~Retention`.
5. Run one SQL test at a time against a disposable database: empty migration, applied-`SnapshotRepair` upgrade, migration-ID assertion, equal-timestamp cursor paging, and retention. Use the Infrastructure IntegrationTests project with an exact `FullyQualifiedName=` filter and `--blame-hang-timeout 45s`.
6. Start the API under a 55-second process-tree watchdog, probe `/health/ready`, and fail immediately on process exit or the `Body (Inferred)` signature. Stop the process in `finally`.
7. Run exactly one synthetic-token API integration test with the fixture's internal startup/readiness deadline below 55 seconds.
8. Run exactly one AppHost-backed Web or E2E smoke test with an exact filter and the same external deadline. Verify API and Web listener discovery, then clean up session DCP/application processes while retaining only intentionally persistent infrastructure.
9. Run Account Preferences Web unit tests only: `dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj --no-build --no-restore --filter "FullyQualifiedName~AccountPreferences" --blame-hang-timeout 45s`.
10. Broaden by project, never by whole solution, only after every prior gate passes. Split any project whose normal duration exceeds 60 seconds by namespace, class, or collection filter.

No automated validation should call real IG. Manual Test-account evidence remains
separate and must not contain credentials, tokens, or raw provider payloads.

## Gaps And Clarifying Questions

* The build/test subagent report is complete, but the 15 failed test names and full exception chains were not retained. This analysis cannot prove that every one belongs to the Aspire cascade.
* It is unknown whether `20260830112227_SnapshotRepair` reached any shared, CI, staging, or deployed database. The selected migration-preservation path is deliberately conservative until owners answer that question.
* Product ownership has not explicitly chosen the public mismatch payload. This analysis selects a typed not-applied outcome and HTTP `409`; the exact Problem Details extension fields still require API-contract approval.
* The provider does not currently expose a trustworthy account ID through this adapter. History is therefore partitioned by platform and broker environment only until that contract changes.
* Real IG Test account endpoint availability and allowance behavior remain unverified. They require controlled manual evidence and are not a reason to weaken automated tests.

## Recommended Next Research Not Completed

* [ ] Recover the 15-test failure inventory from retained VS Code test results or logs and map each failure to its collection fixture
* [ ] Inventory `SnapshotRepair` in every non-disposable database before migration edits
* [ ] Confirm the public API contract for the selected `409` not-applied outcome
* [ ] Confirm that trailing-stop history must continue sharing `Retention:OperationalRecordsDays`
* [ ] Capture sanitized manual evidence for IG Test `GET` and `PUT /accounts/preferences`