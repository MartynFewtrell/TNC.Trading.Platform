---
applyTo: 'src/TNC.Trading.Platform.Api/**/*, src/TNC.Trading.Platform.Infrastructure/**/*, test/TNC.Trading.Platform.Api/**/*, test/TNC.Trading.Platform.Infrastructure/**/*'
---
<!-- markdownlint-disable-file -->

# Implementation Plan: Operational Record Retention Cancellation

## Overview

Resolve the API startup failure that causes the reported operational-record
retention `TaskCanceledException`, then make the retention worker handle a
confirmed host shutdown consistently without concealing genuine failures.

## Objectives

### User Requirements

* Create a plan from `.copilot-tracking/research/2026-09-22/operational-record-retention-cancellation-research.md`.
* Store the plan and associated planning record under `.copilot-tracking/plans/`.

### Derived Objectives

* Restore the active dependency-injection registration required by broker-environment endpoints.
* Prove endpoint construction succeeds before live startup validation.
* Treat cancellation as normal only when the retention worker's stopping token was cancelled.
* Retain logging and retry behavior for genuine processing failures and unrelated cancellation.

## Context Summary

### Root Cause

The active `Infrastructure.DependencyInjection.AddPlatformInfrastructure`
extension omits the scoped `IBrokerEnvironmentCatalogService` to
`SqlBrokerEnvironmentCatalogService` registration. Minimal APIs therefore
infer the unresolved catalog-service parameter of broker-environment GET
handlers as an invalid request body, terminating API startup. The host then
stops its background services and cancels the retention worker's
`Task.Delay(..., stoppingToken)`.

The reported cancellation is a shutdown symptom, not the startup root cause.
The worker still needs a lifecycle correction because its filtered expected
cancellation handling wraps processor execution but not the periodic delay.

### Project Files

* `src/TNC.Trading.Platform.Api/Program.cs:36` - invokes the active Infrastructure composition root.
* `src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs:93-98` - maps broker-environment GET handlers.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/DependencyInjection/PlatformInfrastructureServiceCollectionExtensions.cs:1-125` - active extension missing the catalog registration.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/PlatformInfrastructureServiceCollectionExtensions.cs:1-106` - inactive duplicate that evidences, but must not replace, the missing registration.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Operations/Retention/OperationalRecordRetentionService.cs:8-34` - worker whose periodic delay is outside the existing filtered cancellation handler.
* `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/PlatformApplicationServiceCollectionExtensionsTests.cs:10-48` - endpoint-construction regression-test pattern.
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/OperationalRecordRetentionProcessorTests.cs:10-117` - retention-test location and documentation convention.

### References

* `.copilot-tracking/research/2026-09-22/operational-record-retention-cancellation-research.md`
* `.github/instructions/csharp.instructions.md`
* `.github/instructions/tests.instructions.md`

## Implementation Checklist

### [x] Implementation Phase 1: Repair Active Registration And Startup Regression

<!-- parallelizable: false -->

* [x] Step 1.1: Update `src/TNC.Trading.Platform.Infrastructure/Infrastructure/DependencyInjection/PlatformInfrastructureServiceCollectionExtensions.cs` to import the Broker Environments application namespace and register `IBrokerEnvironmentCatalogService` as scoped with `SqlBrokerEnvironmentCatalogService`.
  * Do not modify or use the inactive duplicate extension; it has diverged from the active composition root.

* [x] Step 1.2: Add `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/PlatformInfrastructureServiceCollectionExtensionsTests.cs`.
  * Use test-only configuration satisfying in-memory persistence and account-preferences URL validation.
  * Resolve `IBrokerEnvironmentCatalogService` from a scope and assert the concrete implementation.
  * Document the test with the fault trace, expected outcome, and regression risk.

* [x] Step 1.3: Extend `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/PlatformApplicationServiceCollectionExtensionsTests.cs`.
  * Register a focused test catalog service, materialize endpoint data sources, and assert the broker-environment GET route is present.
  * Follow the existing XML documentation and naming convention.

### [x] Implementation Phase 2: Handle Confirmed Worker Shutdown Consistently

<!-- parallelizable: false -->

* [x] Step 2.1: Update `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Operations/Retention/OperationalRecordRetentionService.cs`.
  * Extend the existing `OperationCanceledException when (stoppingToken.IsCancellationRequested)` boundary to cover processor work and `Task.Delay(TimeSpan.FromHours(1), stoppingToken)`.
  * Retain the stopping token on the delay and exit normally only when that exact token was cancelled.
  * Preserve logging and retry for non-cancellation processor exceptions; do not catch every `OperationCanceledException`.

* [x] Step 2.2: Add `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/OperationalRecordRetentionServiceTests.cs`.
  * Add only the narrowest internal scheduling seam needed to avoid a real one-hour wait.
  * Verify cancellation during the periodic wait, cancellation during processor work, and logging/retry for non-cancellation failures.
  * Fully document each test with traceability, expected outcome, and regression rationale.

### [x] Implementation Phase 3: Validate The Root Fix And Lifecycle Behavior

<!-- parallelizable: false -->

* [x] Step 3.1: Run focused Infrastructure and API unit-test projects.
* [x] Step 3.2: Build `src/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.csproj`.
* [x] Step 3.3: Start the Docker-backed AppHost and, before shutdown, confirm that the API remains running, the `Body was inferred` error is absent, and API logs/traces show no retention cancellation during successful startup.
* [x] Step 3.4: Stop the AppHost normally and confirm that periodic-delay cancellation is not reported as an independent worker failure.

## Dependencies

* .NET SDK 10.0.302 from `global.json`.
* Docker running for SQL Server, Keycloak, and AppHost validation.
* Existing Aspire orchestration and API configuration.

## Success Criteria

* The active Infrastructure extension resolves `IBrokerEnvironmentCatalogService` as `SqlBrokerEnvironmentCatalogService`.
* Broker-environment GET endpoints construct without an inferred-body error.
* The retention worker completes normally when its stopping token cancels during processing or its periodic delay.
* Genuine processing failures still log and retry, and unrelated cancellation is not silently reclassified.
* Focused unit tests and the affected API build pass.
* A live AppHost run keeps the API running through startup without the inferred-body error or retention cancellation.

## Planning Log

See `.copilot-tracking/plans/logs/2026-09-22/operational-record-retention-cancellation-log.md`.
