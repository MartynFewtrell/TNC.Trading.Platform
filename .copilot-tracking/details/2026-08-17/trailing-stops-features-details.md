<!-- markdownlint-disable-file -->
# Implementation Details: Trailing Stops Features

## Context Reference

Sources: `.copilot-tracking/research/2026-08-17/trailing-stops-features-research.md`; `.copilot-tracking/research/2026-06-06/ig-account-preferences-web-ui-integration-research.md`; `.copilot-tracking/research/subagents/2026-08-17/trailing-stops-feature-plan-context-research.md`.

## Implementation Phase 1: Confirm Product Guards and Compatibility

<!-- parallelizable: false -->

### Step 1.1: Configure 90-day history retention

Set `TrailingStopsPreferenceObservations` to the approved initial 90-day retention duration. Reuse the existing operational retention configuration value only where it is explicitly documented as the policy source for this history, then extend SQL and in-memory deletion behavior. Archive/export is not required for this initial delivery and remains follow-on work.

Files:

* `src/TNC.Trading.Platform.Application/Services/TrailingStopsPreferenceObservationRetentionPolicy.cs` - explicit 90-day policy contract
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Operations/Retention/OperationalRecordRetentionProcessor.cs` - approved deletion behavior
* `src/TNC.Trading.Platform.Infrastructure/appsettings.json` - setting declaration

Discrepancy references:

* Resolved DR-01

Success criteria:

* Tests prove deletion according to the approved 90-day rule.
* Documentation records that archive/export is deferred from this delivery.

Dependencies:

* No additional product decision is required for initial delivery.

### Step 1.2: Preserve legacy provider-key compatibility

Map existing persisted `BrokerEnvironmentKind.Demo` records to Test presentation in the new feature without renaming historical data. Add a focused test for legacy records resolving to Test.

Files:

* `src/TNC.Trading.Platform.Application/Configuration/BrokerEnvironmentKind.cs`
* `src/TNC.Trading.Platform.Application/Configuration/PlatformEnvironmentKind.cs`
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/`
* `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/AccountPreferences/`

Discrepancy references:

* DR-02
* DD-01

Success criteria:

* New API/UI models never present Demo as an application status; old data remains predictably partitioned.

### Step 1.3: Confirm delivery environment guard

Enforce Test-only support in the application feature. State that this preference does not authorize real orders or monetary exposure. Capture non-secret manual evidence for the configured Test account; never make real IG access an automated-test dependency.

Files:

* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/`
* `docs/wiki/operator-guide.md`
* `docs/wiki/runtime-behavior.md`

Discrepancy references:

* DR-03

Success criteria:

* Unsupported Live execution returns a predictable typed outcome until explicit Live controls are delivered.

## Implementation Phase 2: Add Application Contracts and Behavior

<!-- parallelizable: false -->

### Step 2.1: Create cohesive Account Preferences operations

Add one top-level type per file under `src/TNC.Trading.Platform.Application/Features/AccountPreferences/`: `AccountPreferences`, `IAccountPreferencesGateway`, typed gateway outcomes/errors, `TrailingStopsPreferenceObservation`, `ITrailingStopsPreferenceObservationStore`, and Get/Update/List request-response-handler files. Add `UpdateAccountPreferencesValidator` for malformed external input and supported status only.

Files:

* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/AccountPreferences.cs`
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/IAccountPreferencesGateway.cs`
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/ITrailingStopsPreferenceObservationStore.cs`
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/GetAccountPreferencesRequest.cs` and related files
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/UpdateAccountPreferencesRequest.cs` and related files
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/GetTrailingStopsPreferenceObservationsRequest.cs` and related files

Success criteria:

* Application code has no IG, HTTP, EF, or Razor types; each operation owns one request, response, and handler.

Context references:

* `IAccountDetailsGateway.cs:3-11`
* `RefreshAccountDetailsHandler.cs:6-89`

### Step 2.2: Enforce confirmed updates and observations

Issue one PUT then a fresh GET in `UpdateAccountPreferencesHandler`. Return success only from strict GET confirmation and append an `UpdateConfirmed` observation. After indeterminate write failure, GET to reconcile and append only a successful `ReconciliationObserved` value. Every successful GET appends an observation, including equal values; local history never replaces a failed live read.

Files:

* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/UpdateAccountPreferencesHandler.cs`
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/GetAccountPreferencesHandler.cs`

Success criteria:

* No blind PUT retry, nullable observation value, or false default on provider failure.

### Step 2.3: Persist sanitized provider failure telemetry

Define a feature-local allow-listed operational-event payload and append it through the existing application-owned operational-event port for failed or indeterminate provider operations. Include only stable failure classification, operation type, status/environment scope, correlation identifier, and a bounded safe diagnostic reason. Explicitly exclude API key, CST, security token, provider response body, arbitrary error code, request body, account credentials, and exception stack details.

Files:

* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/` - create and issue safe feature failure event data
* `src/TNC.Trading.Platform.Application/Services/IPlatformEventStore.cs` or existing event-store port - reuse existing bounded operational-event boundary
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfPlatformEventStore.cs` - reuse existing append behavior without introducing a preference-history failure row

Discrepancy references:

* DR-05

Success criteria:

* Every failure event is bounded and allow-listed; no secret, raw provider body, or arbitrary provider diagnostic persists.
* Failed/indeterminate operations remain absent from `TrailingStopsPreferenceObservations`.

### Step 2.4: Add Application unit coverage

Add direct xUnit handler/validator tests with requirement-traceability comments for Test behavior, typed failures, confirmation, reconciliation, append count, repeated values, and deterministic cursors.

Files:

* `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/AccountPreferences/AccountPreferencesHandlerTests.cs`
* `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/AccountPreferences/UpdateAccountPreferencesValidatorTests.cs`

Validation command:

* `dotnet test test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/TNC.Trading.Platform.Application.UnitTests.csproj --filter "FullyQualifiedName~AccountPreferences"`

Success criteria:

* Tests use fakes only, follow required naming, never access network or database, and prove sanitized failure-event payloads exclude secrets/provider bodies.

## Implementation Phase 3: Add IG Boundary and Observation Persistence

<!-- parallelizable: false -->

### Step 3.1: Implement the feature-specific IG adapter

Add `IgAccountPreferencesGateway` and typed-client registration. Establish Version 2 session credentials, request Version 1 GET/PUT `/accounts/preferences`, send JSON content for PUT, strictly parse Boolean and `SUCCESS`, replay once after 401, classify allowance 403 as rate limiting, redact diagnostics, and do not retry an indeterminate PUT.

Files:

* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgAccountPreferencesGateway.cs`
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/DependencyInjection/PlatformInfrastructureServiceCollectionExtensions.cs`
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/Integrations/Ig/IgAccountPreferencesGatewayTests.cs`

Success criteria:

* Tests prove method, path, Version, headers, JSON, parsing, one replay, classification, redaction, and no blind write retry.

### Step 3.2: Persist verified observation history

Add an append-only observation entity/store with value, observed/recorded UTC timestamps, status/environment partition, optional account ID, observation kind/source/actor, correlation/command IDs, and success outcome. Configure a non-unique partition-plus-observed-time-plus-identity index, register it in DbContext, create an additive migration and model snapshot update, and apply approved retention.

Files:

* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/Entities/TrailingStopsPreferenceObservationEntity.cs`
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfTrailingStopsPreferenceObservationStore.cs`
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/PlatformDbContext.cs`
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/Migrations/<timestamp>_AddTrailingStopsPreferenceObservations.cs`
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/Migrations/PlatformDbContextModelSnapshot.cs`
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Operations/Retention/OperationalRecordRetentionProcessor.cs`

Success criteria:

* Equal consecutive reads remain retained and history paging is deterministic when timestamps match.

### Step 3.3: Prove provider and persistence behavior

Add gateway, store, migration, index, restart, ordering, and retention tests. Keep store tests in-memory; use the existing Docker-backed SQL integration project for migration and restart proof.

Files:

* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/EfTrailingStopsPreferenceObservationStoreTests.cs`
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/PlatformSqlServerIntegrationTests.cs`

Validation commands:

* `dotnet test test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/TNC.Trading.Platform.Infrastructure.UnitTests.csproj --filter "FullyQualifiedName~AccountPreferences|FullyQualifiedName~TrailingStops|FullyQualifiedName~Retention"`
* `dotnet test test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/TNC.Trading.Platform.Infrastructure.IntegrationTests.csproj --filter "FullyQualifiedName~AccountPreferences"`

## Implementation Phase 4: Add Operator API and Web Experience

<!-- parallelizable: false -->

### Step 4.1: Expose thin live Operator API operations

Register `GET /api/platform/account-preferences` and `PUT /api/platform/account-preferences`, both Operator-only. Use `UpdateAccountPreferencesHttpRequest(bool? TrailingStopsEnabled)` to distinguish omitted input from false. Map invalid input to 400, allowance exhaustion to 429, malformed provider payload to 502, provider rejection/unavailability to 503, and timeout to 504. Return the confirmed platform model, never provider diagnostics or raw `SUCCESS`.

Files:

* `src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs`
* `src/TNC.Trading.Platform.Api/Features/Platform/AccountPreferencesEndpointMapping.cs`
* `src/TNC.Trading.Platform.Api/Features/Platform/GetAccountPreferencesEndpointHandler.cs`
* `src/TNC.Trading.Platform.Api/Features/Platform/UpdateAccountPreferencesEndpointHandler.cs`

Success criteria:

* A provider 403 cannot be mistaken for platform-user authorization failure.

### Step 4.2: Expose the paged history API operation

Register `GET /api/platform/account-preferences/observations` with Operator policy. Bind a bounded page-size and optional opaque/deterministic keyset cursor to `GetTrailingStopsPreferenceObservationsRequest`; return ordered rows and the adjacent cursor. Validate malformed cursor/page-size input before dispatch, and return platform-safe validation Problem Details. The history endpoint reads SQL observations only and must never initiate an IG call or use history as a live-value fallback.

Files:

* `src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs`
* `src/TNC.Trading.Platform.Api/Features/Platform/GetTrailingStopsPreferenceObservationsEndpointHandler.cs`
* `src/TNC.Trading.Platform.Api/Features/Platform/AccountPreferencesEndpointMapping.cs`

Discrepancy references:

* DR-04

Success criteria:

* Cursor ordering is deterministic and page-size limits are validated.
* History is Operator-only and contains no provider diagnostics or credentials.

### Step 4.3: Deliver the Account Preferences page

Add API-client models/calls, presenter, Operator-authorized `/account-preferences` Razor page, DI registration, navigation item, labelled toggle, disabled loading/save state, accessible feedback, confirmed-value replacement, and paged read-only observation history. Keep Account Details unchanged.

Files:

* `src/TNC.Trading.Platform.Web/PlatformApiClient.cs`
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor`
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPagePresenter.cs`
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPageViewModel.cs`
* `src/TNC.Trading.Platform.Web/PlatformWebUiServiceCollectionExtensions.cs`
* `src/TNC.Trading.Platform.Web/Components/Layout/MainLayout.razor`

Discrepancy references:

* DD-02

Success criteria:

* Only Operators can use the feature; Test does not appear to authorize real trading; history is explicitly observed state, not a continuous guarantee.

### Step 4.4: Add API and Web tests

Add API tests for policy/input/mappings/no leakage and bUnit tests for initial load, toggle, disabled save, confirmation, reconciliation/failure feedback, authorization, and history rendering. Include traceability comments.

Files:

* `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/AccountPreferencesEndpointMappingTests.cs`
* `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/AccountPreferencesEndpointTests.cs`
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountPreferencesTests.cs`

Validation commands:

* `dotnet test test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/TNC.Trading.Platform.Api.UnitTests.csproj --filter "FullyQualifiedName~AccountPreferences"`
* `dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj --filter "FullyQualifiedName~AccountPreferences"`

Success criteria:

* API tests prove page-size/cursor validation and deterministic history response; bUnit tests prove page-to-history-client wiring and next-page behavior.

## Implementation Phase 5: Document and Validate Delivery

<!-- parallelizable: false -->

### Step 5.1: Update implementation documentation

Update API, operator, runtime, architecture, and testing wiki pages with feature authority, policy, Test-only behavior, no-cache fallback, confirmed update/reconciliation, observation limitations, and retention.

Files:

* `docs/wiki/api-reference.md`
* `docs/wiki/operator-guide.md`
* `docs/wiki/runtime-behavior.md`
* `docs/wiki/application-overview.md`
* `docs/wiki/architecture.md`
* `docs/wiki/testing-and-quality.md`

### Step 5.2: Run final validation

Run all focused commands, then build and run the solution suite. Apply only isolated fixes; re-plan retention, endpoint availability, or Live enablement changes.

Validation commands:

* `dotnet build TNC.Trading.Platform.slnx`
* `dotnet test TNC.Trading.Platform.slnx`

Success criteria:

* Feature tests, SQL integration, build, and relevant suite checks pass with no real IG calls.

### Step 5.3: Report non-minor blockers

Record unresolved retention/archive, Test endpoint, allowance, or Live-safety constraints for a follow-on plan.

## Dependencies

* Approved 90-day retention policy, legacy Demo mapping decision, Test credentials, and Docker-backed SQL infrastructure.

## Success Criteria

* The feature exposes confirmed IG state to Operators, stores only verified historical evidence, and documents its environment/authority boundaries.