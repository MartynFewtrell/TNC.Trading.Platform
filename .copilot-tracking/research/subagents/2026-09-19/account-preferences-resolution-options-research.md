---
title: Account Preferences Resolution Options Evaluation
description: Evaluation of cohesive implementation options for timestamp display, compact controls, and account-safe Check and Save behavior
author: GitHub Copilot
ms.date: 2026-09-19
ms.topic: troubleshooting
keywords:
  - account preferences
  - trailing stops
  - account validation
  - API contract
status: complete
---

## Research Scope

Evaluate cohesive implementation alternatives for the reported account-preferences
defects using the verified UI and account-validation research. The selected
package must cover a timestamp-or-blank `Last confirmed` value, compact
left-to-right `Enabled`/`Disabled`/`Save` controls without tiles, actionable
but account-ID-safe warnings for Check and Save, a stable API contract, and no
unsafe automatic rebinding.

## Status

Complete. Product code was not modified. The recommendations were checked
against the current endpoint mapper, application handlers, Web presenter and
component, and the focused application, API, bUnit, and Playwright tests.

## Decision Criteria

The selected package must meet all of these criteria.

* Show `Last confirmed` as a timestamp when `LastVerifiedAtUtc` has a value;
  otherwise show no misleading Boolean or synthetic date
* Keep native radio semantics while placing `Enabled`, `Disabled`, and `Save
  preferences` in one compact, left-to-right, wrapping control row
* Give Check and Save the same actionable account-safe remediation without
  exposing either configured or current account IDs
* Preserve an explicit, documented Problem Details contract for account
  mismatch across both actions
* Retain the two existing account checks and never bind preferences to a newly
  authenticated account as a side effect of Check or Save
* Keep ownership aligned with established boundaries: policy in application
  handlers, HTTP translation in the endpoint mapper, and display treatment in
  the Web presenter and Razor component
* Add deterministic unit coverage first, then bUnit rendering coverage, and
  reserve Playwright for browser geometry and workflow verification

## Verified Baseline

### Display and layout

`AccountPreferencesPageViewModel.Apply` already carries nullable
`LastVerifiedAtUtc` from the Web response. The Razor component instead gates
and displays `ConfirmedTrailingStopsEnabled` as `Enabled` or `Disabled`.
This is the direct source of the incorrect `Last confirmed` value.

* Web transport DTO: `src/TNC.Trading.Platform.Web/PlatformApiClient.cs:218-239`
* View-model projection: `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPageViewModel.cs:25-40`
* Incorrect status binding: `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:27-30`
* Nullable API projection: `src/TNC.Trading.Platform.Api/Features/Platform/AccountPreferencesEndpointMapping.cs:8-13`
* Explicit unconfigured null timestamp: `src/TNC.Trading.Platform.Api/Features/Platform/AccountPreferencesEndpointMapping.cs:15`

The current editor deliberately separates its radio fieldset and Save action
into two block siblings. Its labels are explicitly styled as bordered tiles,
and the narrow-viewport media rule stacks both choices and action buttons.

* Separate control/action markup: `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:40-48`
* Tile and two-column rules: `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor.css:83-114`
* Narrow-viewport stacking and full-width Save: `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor.css:159-174`

### Account binding and HTTP behavior

The durable preference target is `AccountPreferencesCurrentState.AccountId`.
Save establishes it from the latest `IgLoginSnapshot.CurrentAccountId` only
when no durable target exists. Later Save and Check operations stop before
provider preference I/O when that latest authenticated identity differs.

* Save initial binding and durable-target guard: `src/TNC.Trading.Platform.Application/Features/AccountPreferences/SaveAccountPreferencesHandler.cs:18-19, 28-31, 37-45`
* Check durable-target guard before lease or gateway I/O: `src/TNC.Trading.Platform.Application/Features/AccountPreferences/CheckAccountPreferencesStatusHandler.cs:16-20`
* Check passes the durable target to provider operations: `src/TNC.Trading.Platform.Application/Features/AccountPreferences/CheckAccountPreferencesStatusHandler.cs:26, 30`
* Gateway observation/remediation time-of-check/time-of-use guard: `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgAccountPreferencesGateway.cs:15-23, 35-43`

Check uses `AccountMismatch`, which maps through `ToProblemResult` to `409`,
`/problems/account-preferences/account-mismatch`, and a `failureCategory`
extension. Save currently sets `Conflict: true` with `Rejected`, which makes
`ToSaveHttpResult` return an untyped `409` before it considers the failure
category. This is the contract inconsistency that blocks consistent UI
feedback.

* Check endpoint and response overlay: `src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs:145-156`
* Save mismatch result: `src/TNC.Trading.Platform.Application/Features/AccountPreferences/SaveAccountPreferencesHandler.cs:28-31`
* Save's untyped conflict shortcut: `src/TNC.Trading.Platform.Api/Features/Platform/AccountPreferencesEndpointMapping.cs:27-40`
* Stable category mapping: `src/TNC.Trading.Platform.Api/Features/Platform/AccountPreferencesEndpointMapping.cs:57-61, 66-68, 81-83, 94-97`
* Save's generic presentation: `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPagePresenter.cs:54-82, 97-103`

## Alternatives Considered

### A. Cohesive native-control and stable-contract package

Keep the native `InputRadio` controls, correct the timestamp binding, move the
radio group and Save command into one flex-wrapping row, and normalize only
the account-mismatch branch to the existing typed Problem Details mapping.
Use one safe, actionable reason in both application handlers and provider
guards. Teach the Web presenter to surface that known safe response for Save.

This option changes no request JSON, response-success schema, persistence
schema, account-binding rule, or IG request contract. It uses the existing
component semantics, error plumbing, and failure-category mechanism.

### B. UI-only correction

Correct the timestamp and CSS while retaining the generic Save error and
untyped Save conflict.

Rejected. It fixes two visible defects but leaves the reported Save warning
non-actionable and the Check/Save contract inconsistent.

### C. Replace the native radios with RadzenRadioButtonList

Use a Radzen horizontal radio list and stack for the compact row.

Rejected for this defect package. The current controls already have correct
native semantics and bUnit locators. A Radzen replacement broadens the markup
and disabled-state validation surface without improving the account-safety or
API requirements. It remains a separate UI-standardization decision.

### D. Include account IDs, including masked IDs, in warnings

Show configured/current account identifiers to explain the mismatch.

Rejected. The identifiers are operational data and would propagate into
browser alerts, screenshots, support tickets, and potentially client-side
logs. A category, safe explanation, and server correlation context provide a
safer support path.

### E. Automatically rebind when a new account authenticates

Overwrite the durable target with the latest login snapshot during Check or
Save.

Rejected. It would allow a new authenticated account to inherit settings
without explicit authorization and defeats both the application preflight and
gateway race guard. It is unsafe by design.

## Selected Approach

Select Alternative A: a focused, cohesive package that corrects the status
time, replaces tile styling with compact inline radio labels, and gives Check
and Save one stable account-mismatch behavior.

### UI behavior

* Render `Last confirmed` from `LastVerifiedAtUtc`, using a semantic `<time>`
  element with an ISO-8601 `datetime` attribute and the existing local `g`
  display convention. When the timestamp is null, omit the status row.
* Keep the `fieldset` and `legend`. Place the two radios and the existing
  Save button in one named control row in DOM and tab order: Enabled, Disabled,
  Save preferences.
* Change only the scoped page styles for that row: inline-flex/flex-wrap,
  aligned controls, 44px minimum targets, no label border/background tile,
  retained visible focus treatment, and no narrow-screen forced full-width
  Save rule for this row. Wrapping on a narrow viewport is acceptable; fixed
  horizontal overflow is not.

### Safety and contract behavior

* Define one account-mismatch safe-reason source in the
  `Features/AccountPreferences` application feature. Reuse it in Check,
  Save's durable-target branch, and the two gateway mismatch branches.
* Classify the Save durable-target mismatch as `AccountMismatch` rather than
  `Rejected`, and do not set its general conflict shortcut. Preserve `Rejected`
  for stale revisions, incompatible idempotency reuse, and lease contention.
* Map the categorized Save outcome through `ToProblemResult`. Both Check and
  Save then return `409`, type
  `/problems/account-preferences/account-mismatch`, and extension
  `failureCategory: "AccountMismatch"`. Keep the title stable and generic;
  put the remediation text in the safe Problem Details detail.
* Let the Web presenter display the known safe Save Problem Details content,
  while retaining generic Save text for unclassified failures. Do not add
  account IDs to API payloads, page text, browser logs, notifications, or new
  structured logs.
* Preserve both account guards. The application guard prevents known mismatch
  provider I/O; the gateway guard closes the session-change race immediately
  before provider I/O.

## Proposed User Copy

Use this exact safe reason for the account-mismatch category:

> The authenticated IG account has changed. Trailing-stops preferences remain bound to the previously configured account. Reauthenticate with the intended account, then check status. Contact an administrator to change the configured account.

The stable Problem Details title remains:

> IG session account does not match the configured account.

The UI must not interpolate or append account identifiers to either message.

## Genuine Product Decision

Two policy decisions require product-owner confirmation; they cannot be
derived from source code.

* Blank timestamp presentation: this evaluation recommends omitting the
  `Last confirmed` row when no confirmation time exists. If the product needs
  a permanently visible label, it must define an accessible blank-state phrase
  rather than reuse the Boolean value.
* Intentional rebinding: no current workflow defines who may change the
  durable target, under which authorization, with what confirmation, and with
  what audit record. Until that policy exists, automatic rebinding and an
  implicit self-service rebind action are out of scope.

## Staged Implementation and Test Plan

### Stage 1: Establish the safe account-mismatch contract

1. Add a feature-local account-mismatch safe-reason source under
   `src/TNC.Trading.Platform.Application/Features/AccountPreferences/`.
2. Replace the literal reasons in
   `CheckAccountPreferencesStatusHandler.cs` and
   `SaveAccountPreferencesHandler.cs` only for no-current-account and
   durable-target mismatch branches as appropriate. Keep no-snapshot distinct
   if product copy requires it, but classify it as `AccountMismatch`.
3. Reuse the safe mismatch reason in
   `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgAccountPreferencesGateway.cs`
   for both observation and remediation mismatch checks.
4. Change the Save durable-target mismatch result to
   `AccountPreferencesFailureCategory.AccountMismatch` with no conflict
   shortcut. Do not change stale revision, idempotency, busy, or provider
   outcomes.
5. Adjust `ToSaveHttpResult` so categorized account mismatch uses
   `ToProblemResult`, producing the same type and extension as Check.

Test Stage 1 in the existing focused projects.

* Add Save handler tests in
  `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/AccountPreferences/SaveAccountPreferencesHandlerTests.cs`
  for durable target versus latest snapshot mismatch and no snapshot. Assert
  category, safe copy, zero `RemediateAsync` calls, and no operation start.
* Extend
  `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/AccountPreferences/CheckAccountPreferencesStatusHandlerTests.cs`
  to assert the shared safe copy and zero observe/remediate calls.
* Extend
  `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/Integrations/Ig/IgAccountPreferencesGatewayTests.cs`
  with `ObserveAsync` mismatch coverage and account-ID non-disclosure checks.
* Add endpoint mapping tests in
  `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/AccountPreferencesEndpointMappingTests.cs`
  that compare Check and Save account-mismatch status, type, title, detail,
  and `failureCategory`, while asserting neither configured nor current IDs
  occur anywhere in public Problem Details.

### Stage 2: Present a useful safe Save warning

1. In `AccountPreferencesPagePresenter.SaveAsync`, recognize the typed,
   already-safe account-mismatch response and present its safe detail.
2. Preserve the current generic text for malformed, unavailable, and unknown
   failures. Do not surface raw provider diagnostics through exception text.
3. Keep Check's persisted `FailureSummary` path unchanged, except that it now
   contains the shared safe message.

Test Stage 2 in
`test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountPreferencesTests.cs`.

* Replace the current generic-save-error expectation in
  `Render_ShouldPlaceFeedbackAndActionsInOwningWorkflow_WhenSaveFails` with a
  typed account-mismatch response and the safe message.
* Add a generic-failure regression test so an arbitrary `409` cannot be
  mistaken for the known account mismatch.
* Assert visible Check and Save warning text contains the action guidance and
  excludes representative configured/current account IDs.

### Stage 3: Correct the timestamp and compact control row

1. Change the `Last confirmed` Razor binding to `LastVerifiedAtUtc`.
2. Group native radios and Save within a single named row without changing
   `Presenter.Select`, save request JSON, disabled states, or confirmation
   focus return.
3. Replace tile-specific scoped CSS with compact row styles and a
  narrow-screen wrapping rule.

Test Stage 3 first with bUnit, then with Playwright where rendering geometry
matters.

* Update `AccountPreferencesTests.cs` so a populated time yields one
  `Last confirmed` `<time>` element with an ISO `datetime` value and no
  Boolean as that description.
* Add the null-timestamp case specified by the product decision. For the
  selected recommendation it asserts no `Last confirmed` term or description.
* Preserve assertions for native radio count, fieldset semantics, action
  name, disabled behavior, outbound `trailingStopsEnabled` JSON, and focus
  return.
* Update
  `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/AccountPreferences/AccountPreferencesE2ETests.cs`
  tests that currently expect `Last confirmed` followed by `Enabled` or
  `Disabled` at lines 42-72. Assert the timestamp instead.
* Add a desktop Playwright viewport test using role/test-id locators and
  bounding-box top coordinates to prove the visible radio group and Save
  button share a row. Add a narrow viewport test that permits wrapping but
  asserts no horizontal overflow, visible controls, and usable keyboard
  selection. Use Playwright web-first assertions and no timed waits.

### Stage 4: Validate the account-switch safety path

1. Add a fixture-owned functional or E2E scenario: save preferences as
   account A, switch the latest durable session and provider identity to B,
   then invoke Check and Save.
2. Assert both actions yield the stable safe warning and no provider
   `accounts/preferences` GET or PUT occurs after the mismatch.
3. Confirm state remains bound to A. Do not add a test that expects automatic
   rebinding.

This is the only stage requiring the Docker/Aspire-backed test fixture. The
test remains closed-box and must use its fixture-owned state, endpoints, and
cleanup rather than discovering infrastructure.

## Test and Project Convention Validation

The plan follows established repository conventions.

* Existing tests are xUnit and use the required
  `MethodName_ShouldExpectedResult_WhenState` naming pattern with traceability
  comments, demonstrated in
  `SaveAccountPreferencesHandlerTests.cs:10-41` and
  `AccountPreferencesEndpointMappingTests.cs:12-174`.
* Handler mismatch tests belong in application unit tests. HTTP shape belongs
  in API unit tests. Razor semantics belong in bUnit. Browser geometry and
  viewport behavior belong in Playwright E2E, matching repository testing
  guidance and the current page test split.
* Existing bUnit tests already check native radio controls, named groups,
  disabled states, request JSON, and confirmation focus. The selected approach
  deliberately preserves those semantics:
  `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountPreferencesTests.cs:102-138, 286-332`.
* Existing E2E tests use `PageTest`, role/test-id locators, and web-first
  expectations. The viewport assertion should extend this class rather than
  add an alternate browser framework:
  `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/AccountPreferences/AccountPreferencesE2ETests.cs:1-78`.
* The plan maintains vertical-slice ownership. It introduces no API-to-Web
  dependency, no persistence schema change, and no provider SDK type in
  application policy.

## Risks and Mitigations

| Risk | Mitigation |
| --- | --- |
| Updating the account-mismatch title or detail breaks consumers that compare text | Keep the existing title and stable type/extension; introduce the actionable wording as a documented safe detail |
| A generic 409 is shown as an account mismatch | Present detailed Save guidance only when the stable Problem Details type and category both identify `AccountMismatch` |
| CSS changes weaken accessibility | Retain fieldset/legend, native radios, 44px target size, keyboard order, and focus styling; validate with bUnit and Playwright |
| A changed session triggers provider I/O | Assert preflight and gateway guard tests, then add the account-switch E2E proof |
| Implementers conflate rebinding with remediation | Keep explicit comments and tests that mismatch preserves the durable target; route a deliberate rebind to a separately authorized product workflow |

## Remaining Gap

The selected behavior for a null `LastVerifiedAtUtc` and the authority/audit
model for intentional account rebinding require product-owner decisions. No
Docker-backed account-switch test or browser geometry test was executed during
this research-only evaluation; Stage 4 and the Stage 3 Playwright additions
are the required implementation validation.