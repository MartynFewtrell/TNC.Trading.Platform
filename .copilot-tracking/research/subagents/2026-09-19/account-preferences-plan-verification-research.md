---
title: Account Preferences Plan Verification Research
description: Verification evidence for a multi-phase account preferences implementation plan
ms.date: 2026-09-19
ms.topic: reference
---

## Research Scope

* Verify the implementation recommendations in
  `.copilot-tracking/research/2026-09-19/account-preferences-page-issues-research.md`.
* Record production and test ownership with exact line ranges.
* Identify plan phases, focused validation commands, scope risks, and unresolved questions.

## Status

Complete.

## Verified Implementation Facts

### Status Binding and Layout

* `LastVerifiedAtUtc` is populated into the page state at
  `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPageViewModel.cs:25-40`.
  The Razor page instead renders `ConfirmedTrailingStopsEnabled` as `Enabled`
  or `Disabled` for the `Last confirmed` row at
  `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:27-30`.
  The Boolean explains the observed setting, not the confirmation time.
* The radio fieldset and Save button are separate block siblings at
  `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:40-48`.
  CSS intentionally makes the options into two bordered tiles at
  `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor.css:90-114`,
  then stacks the choices and makes action buttons full width below 600px at
  `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor.css:159-174`.
  A CSS-only change cannot put Save on the same desktop row.

### Account-Mismatch Contract

* Check status returns `AccountMismatch` when no latest IG login snapshot
  exists or its account differs from the durable target, before gateway I/O:
  `src/TNC.Trading.Platform.Application/Features/AccountPreferences/CheckAccountPreferencesStatusHandler.cs:16-20`.
* Save uses the same durable target comparison but returns `Rejected` and sets
  `Conflict` instead: `src/TNC.Trading.Platform.Application/Features/AccountPreferences/SaveAccountPreferencesHandler.cs:17-31`.
  This is the cross-action category inconsistency.
* The gateway repeats the target-account check after creating a fresh IG
  session. It blocks preference I/O in both observation and remediation:
  `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgAccountPreferencesGateway.cs:15-23`
  and `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgAccountPreferencesGateway.cs:35-43`.
  This protects the account-switch race and must remain.
* Categorized failures map to Problem Details with an HTTP status, stable type,
  title, detail, and `failureCategory` extension at
  `src/TNC.Trading.Platform.Api/Features/Platform/AccountPreferencesEndpointMapping.cs:52-57`.
  `AccountMismatch` is already a 409 with
  `/problems/account-preferences/account-mismatch` at
  `src/TNC.Trading.Platform.Api/Features/Platform/AccountPreferencesEndpointMapping.cs:68-111`.
  Save bypasses that mapper whenever `Conflict` is true and produces an untyped
  409 at `src/TNC.Trading.Platform.Api/Features/Platform/AccountPreferencesEndpointMapping.cs:32-40`.
* `PlatformApiClient` discards Problem Details `type` and extensions, keeping
  only title and detail before throwing `HttpRequestException` at
  `src/TNC.Trading.Platform.Web/PlatformApiClient.cs:200-216`.
  Therefore the presenter cannot safely distinguish account mismatch from an
  arbitrary 409 with the current client contract. Save then overwrites every
  failure with generic text at
  `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPagePresenter.cs:54-82`.

### Existing Test Baseline and Gaps

* Check mismatch has a no-provider-I/O application unit test at
  `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/AccountPreferences/CheckAccountPreferencesStatusHandlerTests.cs:20-29`.
  Save has no equivalent durable-target mismatch test:
  `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/AccountPreferences/SaveAccountPreferencesHandlerTests.cs:8-51`.
* Gateway remediation mismatch is tested as one session request and no
  preference read/write at
  `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/Integrations/Ig/IgAccountPreferencesGatewayTests.cs:24-34`.
  Observation mismatch has no equivalent test.
* Web unit tests encode the Boolean `Last confirmed` output at
  `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountPreferencesTests.cs:102-138`
  and `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountPreferencesTests.cs:221-279`.
  They currently exercise a generic 409 save error at
  `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountPreferencesTests.cs:145-163`.
* Browser checks also expect `Disabled` after `Last confirmed` at
  `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/AccountPreferences/AccountPreferencesE2ETests.cs:42-72`.
  No browser test measures desktop shared-row geometry or narrow viewport
  wrapping.

## Recommended Multi-Phase Plan

### Phase 1: Normalize Server-Side Mismatch Semantics

1. Add a feature-local account-mismatch safe-reason source under
   `src/TNC.Trading.Platform.Application/Features/AccountPreferences/` so the
   handlers and IG gateway do not duplicate text.
2. Change the Save durable-target mismatch only to
   `AccountPreferencesFailureCategory.AccountMismatch`, retaining `Conflict`.
   Do not change idempotency, stale-revision, lease, or contention conflicts,
   which must remain generic state conflicts.
3. Change `ToSaveHttpResult` so a categorized `AccountMismatch` reaches
   `ToProblemResult`; retain generic 409 behavior for unrelated conflicts.
4. Use a non-identifying safe detail for both Check and Save:
   `The authenticated IG account has changed. Trailing-stops preferences remain bound to the previously configured account. Reauthenticate with the intended account, then check status. Contact an administrator to change the configured account.`
   Keep account identifiers out of public payloads, logs, and UI.

Validation additions:

* Add Save handler coverage for durable-target mismatch, `AccountMismatch`,
  no gateway remediation call, and unchanged durable state.
* Add gateway observation-mismatch coverage proving only the session request.
* Add API mapper/endpoint coverage for Save and Check with status 409, stable
  type, category, title, safe detail, and no account-ID disclosure.

### Phase 2: Preserve and Present the Typed Failure

1. Extend `src/TNC.Trading.Platform.Web/PlatformApiClient.cs` with a
   feature-appropriate error representation that retains HTTP status, Problem
   Details type, `failureCategory`, title, and detail. Avoid inferring identity
   from user-facing text.
2. Update `AccountPreferencesPagePresenter.SaveAsync` to render the safe
   account-mismatch guidance only when both the stable type and category match.
   Preserve generic Save text for malformed, unknown, and other 409 responses.
3. Retain existing Check behavior unless the client-error change requires a
   shared presentation helper; Check already receives mismatch state as a
   successful state projection in its current route behavior.

Validation additions:

* Add Web client tests for Problem Details parsing and fallback behavior.
* Add bUnit tests for recognized Save mismatch guidance, no identifier leakage,
  and generic rendering for unrelated 409 responses.

### Phase 3: Correct the Page State and Responsive Control Row

1. In `AccountPreferences.razor`, render `LastVerifiedAtUtc` in a semantic
   `time` element with an ISO 8601 `datetime` value and localized visible
   value. Omit the complete row when the timestamp is null.
2. Keep `fieldset`, `legend`, native `InputRadioGroup`, test ID, disabled
   rules, and post-save focus behavior. Move the radio group and existing Save
   button into a common flex-wrapping control row.
3. In scoped CSS, replace the bordered-tile treatment only for this control row
   with compact inline labels. Retain 44px minimum targets, visible focus, and
   source order. At narrow widths, let the row wrap without forcing controls or
   the button to full width.

Validation additions:

* Update bUnit expectations from Boolean last-confirmed output to timestamp
  markup and text, then add null-timestamp omission coverage.
* Add browser geometry checks at desktop width for shared row alignment and at
  narrow width for no horizontal overflow, wrapping, and radio keyboard use.

### Phase 4: Prove Account-Switch Safety End to End

1. Add a fixture-controlled scenario that saves against account A, changes the
   latest login/session account to B, and invokes both Check and Save.
2. Assert identical safe mismatch identity at both boundaries, no
   `accounts/preferences` GET or PUT after the mismatch, and that the durable
   target remains A.
3. Preserve the existing IG-first save and Check drift-repair scenarios. They
   protect behavior outside the targeted presentation changes.

## Recommended Validation Commands

Run focused tests first. The target frameworks are `net10.0`, as confirmed by
`test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj:3-8`
and
`test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/TNC.Trading.Platform.Web.E2ETests.csproj:3-8`.

```powershell
dotnet test test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/TNC.Trading.Platform.Application.UnitTests.csproj --filter "FullyQualifiedName~SaveAccountPreferencesHandlerTests|FullyQualifiedName~CheckAccountPreferencesStatusHandlerTests"
dotnet test test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/TNC.Trading.Platform.Infrastructure.UnitTests.csproj --filter "FullyQualifiedName~IgAccountPreferencesGatewayTests"
dotnet test test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/TNC.Trading.Platform.Api.UnitTests.csproj --filter "FullyQualifiedName~AccountPreferencesEndpoint"
dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj --filter "FullyQualifiedName~AccountPreferencesTests"
dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/TNC.Trading.Platform.Web.E2ETests.csproj --filter "FullyQualifiedName~AccountPreferencesE2ETests"
```

The final browser command starts Aspire and Keycloak through its test fixture,
so Docker must be available. The functional suite at
`test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/AccountPreferences/AccountPreferencesFunctionalTests.cs:17-118`
is the appropriate HTTP-level regression layer for the Save/Check account
switch once its fixture can control the session account.

## Scope Risks

* Treating every Save 409 as an account mismatch would give incorrect recovery
  guidance for stale revisions and idempotency misuse. Preserve typed
  discrimination at the client boundary.
* Automatic rebinding would allow preferences for one account to be applied to
  another. Do not add it as part of Save or Check.
* Exposing configured or authenticated account identifiers, including masked
  forms, creates support-ticket and browser-log disclosure paths.
* A layout-only change can degrade keyboard targets or narrow-screen overflow.
  Preserve native semantics and validate both viewports.
* The timestamp display format is a product policy. Locale conversion is
  available in the current page, but the desired time zone and null-row policy
  require confirmation before implementation.

## Unresolved Questions

* Should `Last confirmed` use the operator browser's local time, an explicitly
  configured platform time zone, or include a UTC offset?
* Is omission of `Last confirmed` when null acceptable, or must the page show a
  stable row with a neutral value such as `Not yet confirmed`?
* Who is authorized to rebind the durable target account, and what audit and
  confirmation requirements govern that separate workflow?
* The requested safe detail directs operators to reauthenticate and contact an
  administrator. Confirm that this wording matches the support model before it
  becomes a public API contract.
