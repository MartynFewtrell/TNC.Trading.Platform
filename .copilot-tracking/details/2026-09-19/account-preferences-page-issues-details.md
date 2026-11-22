<!-- markdownlint-disable-file -->
# Implementation Details: Account Preferences Page Issues

## Context Reference

Sources: `.copilot-tracking/research/2026-09-19/account-preferences-page-issues-research.md`; `.copilot-tracking/research/subagents/2026-09-19/account-preferences-plan-verification-research.md`; user request.

## Implementation Phase 1: Normalize Account-Mismatch Server Contract

<!-- parallelizable: false -->

### Step 1.1: Centralize the safe mismatch reason and align guards

Create `AccountPreferencesAccountMismatch.cs` in `src/TNC.Trading.Platform.Application/Features/AccountPreferences/`. Make it the feature-local owner of the stable Problem Details type, title, `AccountMismatch` category, and approved safe detail. Use it in the durable-target guards in Check and Save plus both fresh-session gateway guards. Change only Save's durable-target mismatch outcome to `AccountMismatch`, retaining its conflict state.

Use this exact detail:

`The authenticated IG account has changed. Trailing-stops preferences remain bound to the previously configured account. Reauthenticate with the intended account, then check status. Contact an administrator to change the configured account.`

Files:

* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/AccountPreferencesAccountMismatch.cs` - new shared safe-reason source
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/CheckAccountPreferencesStatusHandler.cs` - shared category and detail
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/SaveAccountPreferencesHandler.cs` - categorized durable-target mismatch
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgAccountPreferencesGateway.cs` - shared detail in observation and remediation guards

Discrepancy references:

* Addresses DR-01 by adopting the research-selected local-time/null omission policy without inventing a timestamp.

Success criteria:

* Both guards remain before any IG preferences read or write.
* New public text, logs, and UI output contain no configured, latest, or fresh IG account identifiers.

Context references:

* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/CheckAccountPreferencesStatusHandler.cs` (Lines 16-20) - typed Check guard
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/SaveAccountPreferencesHandler.cs` (Lines 17-31) - generic Save conflict
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgAccountPreferencesGateway.cs` (Lines 15-23 and 35-43) - fresh-session guards

Dependencies:

* Existing feature outcome and failure-category contracts

### Step 1.2: Map Save mismatch through typed Problem Details

Modify `ToSaveHttpResult` in `AccountPreferencesEndpointMapping.cs` so categorized `AccountMismatch` returns `ToProblemResult` before generic conflict handling. Leave generic `Conflict()` behavior unchanged for stale revisions, idempotency reuse, lease contention, and ordinary state conflicts.

Files:

* `src/TNC.Trading.Platform.Api/Features/Platform/AccountPreferencesEndpointMapping.cs` - categorized Save mismatch branch

Discrepancy references:

* Addresses DD-01 by retaining generic HTTP 409 semantics for non-mismatch conflicts.

Success criteria:

* Save and Check return matching 409 status, type, title, category, and safe detail for account mismatch.
* Non-mismatch conflicts retain their existing generic response.

Context references:

* `src/TNC.Trading.Platform.Api/Features/Platform/AccountPreferencesEndpointMapping.cs` (Lines 32-40 and 52-111) - divergent current branches

Dependencies:

* Step 1.1 completion

### Step 1.3: Cover server contract and safety boundaries

Add tests for Save durable-target mismatch, gateway observation mismatch, and API response parity. Extend the functional fixture only as needed to control latest snapshot and fresh provider account identity for a later A-to-B case.

Files:

* `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/AccountPreferences/SaveAccountPreferencesHandlerTests.cs` - category, unchanged state, no remediation
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/Integrations/Ig/IgAccountPreferencesGatewayTests.cs` - observation mismatch creates only a session request
* `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/Features/Platform/AccountPreferencesEndpointMappingTests.cs` - Save/Check Problem Details parity
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/AccountPreferences/AccountPreferencesFunctionalTests.cs` - HTTP boundary regression

Success criteria:

* Tests assert status 409, stable type, category, title, safe detail, and no account-ID disclosure.
* Tests assert zero `accounts/preferences` GET and PUT requests after mismatch detection.

Dependencies:

* Steps 1.1 and 1.2 completion

### Step 1.4: Validate server changes

Validation commands:

* `dotnet test test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/TNC.Trading.Platform.Application.UnitTests.csproj --filter "FullyQualifiedName~SaveAccountPreferencesHandlerTests|FullyQualifiedName~CheckAccountPreferencesStatusHandlerTests"`
* `dotnet test test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/TNC.Trading.Platform.Infrastructure.UnitTests.csproj --filter "FullyQualifiedName~IgAccountPreferencesGatewayTests"`
* `dotnet test test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/TNC.Trading.Platform.Api.UnitTests.csproj --filter "FullyQualifiedName~AccountPreferencesEndpoint"`

## Implementation Phase 2: Preserve and Present Typed Save Failures

<!-- parallelizable: false -->

### Step 2.1: Retain Problem Details identity in the Web client

Extend `PlatformApiClient.cs` with a feature-appropriate error representation retaining HTTP status, Problem Details type, `failureCategory`, title, and detail. Parse the payload with structured JSON APIs and retain safe fallback behavior for malformed or non-Problem Details responses. Create a dedicated exception file only when no current Web type fits the contract.

Files:

* `src/TNC.Trading.Platform.Web/PlatformApiClient.cs` - preserve error metadata
* `src/TNC.Trading.Platform.Web/PlatformApiException.cs` - conditional new error type
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/PlatformApiClientTests.cs` - parsing and fallback tests

Success criteria:

* Failure identity is not inferred from user-facing text.
* Unrecognized failures remain compatible with generic caller behavior.

Context references:

* `src/TNC.Trading.Platform.Web/PlatformApiClient.cs` (Lines 200-216) - title/detail-only error path

Dependencies:

* Phase 1 typed API outcome

### Step 2.2: Render only recognized mismatch guidance

Update `AccountPreferencesPagePresenter.SaveAsync` to show the safe detail only when the type is `/problems/account-preferences/account-mismatch` and the category is `AccountMismatch`. Preserve generic Save failure text for malformed, unknown, and unrelated 409 responses. Do not change successful Save behavior or Check's state projection.

Files:

* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPagePresenter.cs` - exact Save failure presentation

Discrepancy references:

* Addresses DD-01 by preventing an arbitrary conflict from receiving account-change recovery advice.

Success criteria:

* Recognized mismatch guidance tells operators to reauthenticate, check status, and contact an administrator.
* All other failures remain generic.

Context references:

* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPagePresenter.cs` (Lines 54-82 and 97-103) - generic Save failures

Dependencies:

* Step 2.1 completion

### Step 2.3: Cover client parsing and presentation

Add Web tests for matching type/category, arbitrary 409 fallback, malformed payload fallback, and non-disclosure. Retain the existing generic-409 test as the regression proof for conflicts unrelated to account identity.

Files:

* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountPreferencesTests.cs` - bUnit failure presentation
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/PlatformApiClientTests.cs` - error contract parsing

Success criteria:

* Matching metadata renders the safe guidance.
* A nonmatching 409 renders generic text and no identifier data.

Dependencies:

* Steps 2.1 and 2.2 completion

### Step 2.4: Validate Web error handling

Validation command:

* `dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj --filter "FullyQualifiedName~AccountPreferencesTests|FullyQualifiedName~PlatformApiClientTests"`

## Implementation Phase 3: Correct Timestamp and Responsive Control Row

<!-- parallelizable: false -->

### Step 3.1: Render the actual confirmation time

Replace the Boolean output under `Last confirmed` with a semantic `<time>` sourced from nullable `LastVerifiedAtUtc`. Use round-trip ISO 8601 in `datetime`, browser-local formatted display text, and omit the complete definition-list row when the value is null.

Files:

* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor` - conditional timestamp rendering
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountPreferencesTests.cs` - timestamp and null omission tests

Discrepancy references:

* Addresses DR-01 with the selected policy; WI-01 records the outstanding product confirmation.

Success criteria:

* A timestamp produces valid `datetime` markup and localized visible time.
* Null produces no `Last confirmed` label or Boolean substitute.

Context references:

* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor` (Lines 27-30) - incorrect Boolean binding
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPageViewModel.cs` (Lines 25-40) - nullable timestamp source

Dependencies:

* Existing view-model data flow

### Step 3.2: Move controls into the horizontal preferences pane

Move the existing native trailing-stops choice controls (`InputRadioGroup`) and Save action out of separate block areas and into one horizontal desktop preferences pane. Retain the fieldset, legend, bindings, disabled conditions, test IDs, and focus-return behavior. Scope CSS to the pane: a left-to-right flex row, compact inline labels, 44px targets, and visible focus. At narrow widths, allow wrapping without horizontal overflow or full-width tiles.

Files:

* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor` - control-row markup
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor.css` - scoped responsive styling

Success criteria:

* Enabled, Disabled, and Save preferences visibly share the horizontal desktop preferences pane when space allows.
* Narrow viewports wrap predictably and retain native keyboard interaction.

Context references:

* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor` (Lines 40-48) - separate siblings
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor.css` (Lines 90-114 and 159-174) - tile and stacking rules

Dependencies:

* Step 3.1 completion because both changes edit the Razor component

### Step 3.3: Update semantic and viewport coverage

Replace Boolean expectations with timestamp markup/text assertions. Add Playwright desktop bounding-box checks, narrow viewport no-overflow and wrapping checks, and native radio keyboard selection checks. Preserve current success, disabled, and focus behavior coverage.

Files:

* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountPreferencesTests.cs` - markup and native semantic tests
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/AccountPreferences/AccountPreferencesE2ETests.cs` - geometry and keyboard tests

Success criteria:

* bUnit proves timestamp semantics and null omission.
* Browser tests prove desktop alignment, no narrow overflow, and usable radios.

Dependencies:

* Steps 3.1 and 3.2 completion

### Step 3.4: Validate page behavior

Validation commands:

* `dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj --filter "FullyQualifiedName~AccountPreferencesTests"`
* `dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/TNC.Trading.Platform.Web.E2ETests.csproj --filter "FullyQualifiedName~AccountPreferencesE2ETests"`

## Implementation Phase 4: Prove Account-Switch Safety End to End

<!-- parallelizable: false -->

### Step 4.1: Drive an account-switch case through both actions

Use the functional fixture to save against account A, change latest login snapshot and fresh provider identity to B, then invoke Check and Save independently. Keep raw account IDs inside fixture setup rather than public payloads or assertion diagnostics.

Files:

* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/AccountPreferences/AccountPreferencesFunctionalTests.cs` - A-to-B test
* Existing functional fixture files under `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/` - only if required to control session identity and request recording

Success criteria:

* Each action receives the stable safe mismatch response.
* The fixture distinguishes session creation from preferences I/O.

Dependencies:

* Phase 1 completion

### Step 4.2: Preserve remediation and background boundaries

Assert mismatched Check and Save make no preferences GET or PUT and leave durable target A unchanged. Preserve explicit tests proving Save repairs a drifted provider setting before local commit and scheduled reconciliation only records drift.

Files:

* `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/AccountPreferences/SaveAccountPreferencesHandlerTests.cs` - IG-first repair cases
* `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/AccountPreferences/ReconcileAccountPreferencesHandlerTests.cs` - observation-only reconciliation
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/AccountPreferences/AccountPreferencesFunctionalTests.cs` - no-I/O proof

Success criteria:

* Mismatch leaves durable target and desired state unchanged.
* Save remains confirmation-gated; background reconciliation remains observation-only.

Dependencies:

* Step 4.1 completion

## Implementation Phase 5: Final Validation

<!-- parallelizable: false -->

### Step 5.1: Run focused regression matrix and build

Run phase commands, then execute the account-preferences functional suite and build the solution.

Validation commands:

* `dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/TNC.Trading.Platform.Web.FunctionalTests.csproj --filter "FullyQualifiedName~AccountPreferences"`
* `dotnet test test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/TNC.Trading.Platform.Application.UnitTests.csproj --filter "FullyQualifiedName~AccountPreferences"`
* `dotnet test test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/TNC.Trading.Platform.Infrastructure.UnitTests.csproj --filter "FullyQualifiedName~IgAccountPreferencesGatewayTests"`
* `dotnet test test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/TNC.Trading.Platform.Api.UnitTests.csproj --filter "FullyQualifiedName~AccountPreferencesEndpoint"`
* `dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj --filter "FullyQualifiedName~AccountPreferencesTests|FullyQualifiedName~PlatformApiClientTests"`
* `dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/TNC.Trading.Platform.Web.E2ETests.csproj --filter "FullyQualifiedName~AccountPreferencesE2ETests"`
* `dotnet build TNC.Trading.Platform.slnx`

### Step 5.2: Address minor validation failures

Correct isolated compilation, fixture, selector, assertion, or formatting failures and rerun the affected focused command.

### Step 5.3: Report scope blockers

Create follow-on work for explicit account rebinding, public support wording changes, or a timestamp-policy reversal rather than expanding this change.

## Dependencies

* .NET 10 SDK and restored packages
* Docker for Aspire-backed E2E tests
* Deterministic fixture controls for IG account and request recording

## Success Criteria

* Selected behavior is covered by focused unit, functional, and E2E validation.
* No change weakens durable-target or fresh-session account-safety invariants.