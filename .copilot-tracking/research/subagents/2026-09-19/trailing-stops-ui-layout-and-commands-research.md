---
title: Trailing Stops UI Layout and Commands Research
description: Evidence for the Trailing stops account preferences screen layout, commands, and test coverage.
ms.date: 2026-09-19
ms.topic: reference
---

## Research Scope

* Identify the Blazor and Radzen components, CSS, view models, and command wiring for the Trailing stops screen
* Establish why the Enabled and Disabled controls differ in alignment from the status section and why Save preferences is farther right
* Investigate why Check status and Save preferences may not work
* Locate relevant test coverage and project conventions

## Status

Complete. Product files were not modified. Current source and current tests are
authoritative; an earlier research note at
`.copilot-tracking/research/subagents/2026-09-19/account-preferences-ui-research.md`
describes older markup and styling that no longer match this source tree.

## Verified Findings

### Component composition and render mode

The route is `AccountPreferences`, which uses a native Blazor
`InputRadioGroup<bool>` and `InputRadio<bool>` controls for Enabled and
Disabled. `RadzenTabs` is the only Radzen component on this screen, separating
Settings and Observed history. The page uses normal HTML buttons for Check
status and Save preferences.

* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:1-8` declares the route, Operator policy, injected presenter, and navigation coordinator
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:21-78` defines `RadzenTabs`, state, controls, actions, and history
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:44-50` defines the native radio group and binds changes to `Presenter.Select`
* `src/TNC.Trading.Platform.Web/PlatformWebUiServiceCollectionExtensions.cs:8-23` registers Radzen services and the scoped presenter
* `src/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.csproj:5-18` references `Radzen.Blazor` 10.3.2 and targets `net10.0`

The controls should be interactive in a healthy application. Interactive Server
components are enabled in `src/TNC.Trading.Platform.Web/Program.cs:12-14` and
`src/TNC.Trading.Platform.Web/Program.cs:70-73`; the application applies
`InteractiveServer` to `HeadOutlet` and `Routes` in
`src/TNC.Trading.Platform.Web/Components/App.razor:13-16`. Static server-side
rendering is therefore not supported by the current code as an explanation for
buttons doing nothing.

Microsoft documents `InputRadioGroup` as rendering a group of child
`InputRadio` controls, and permits input components outside a form. This
matches the current command-button design:
<https://learn.microsoft.com/aspnet/core/blazor/forms/input-components?view=aspnetcore-10.0>.
Radzen documents `RadzenTabs` selected-index binding and server-rendered
selected-panel behavior:
<https://blazor.radzen.com/tabs>.

### Layout cause

The Status section and configuration section use different DOM arrangements.

The status section puts Check status in a standalone
`.account-preferences-actions` flex container. That container does not set
`justify-content`, so its default is `flex-start`, placing the button at the
left edge:

* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:25-37`
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor.css:57-66`

The configuration section puts a growing `<fieldset>` and a separate actions
container in `.account-preferences-control-row`:

* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:39-59`
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor.css:68-87`

The direct cause of Save preferences being farther right is
`.account-preferences-choice { flex: 1 1 auto; }` at lines 68-74. It consumes
available horizontal space before the later sibling actions container. The row
is a wrapping flexbox with a `1.5rem` column gap, so Save follows the expanded
fieldset rather than the natural radio-group width.

The radio controls are vertically centered within each label because each label
is a flexbox with `align-items: center`, a 44px minimum block size, and padding:

* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor.css:89-105`

The entire configuration row uses `align-items: end`, not vertical centering,
at lines 76-82. It bottom-aligns the fieldset and Save action container. Thus,
the report's apparent vertical centering is caused by the labels themselves;
the distinct row alignment is bottom alignment.

The global `.platform-primary-action` is an `inline-flex` button, but it has no
horizontal positioning rule. It contributes padding and a 0.75rem bottom
margin, not the rightward placement:

* `src/TNC.Trading.Platform.Web/wwwroot/app.css:277-289`

At narrow widths the current flex row can wrap. There is no feature-specific
rule to stack or stretch the configuration action, so the wrap point is based
on available width and intrinsic content. The only 600px media query affects
the state definition list and confirmation-status block:

* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor.css:145-161`

### View model and command wiring

`AccountPreferencesPageViewModel` stores the current selection separately from
the confirmed observation. `Apply` initializes the edit value from desired
state and confirmation from observed state; `HasConfirmedValue` gates editor
visibility:

* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPageViewModel.cs:3-20`
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPageViewModel.cs:23-43`

The page can call `SaveAsync` only after `HasConfirmedValue` is true because
the complete configuration panel is conditionally rendered at
`AccountPreferences.razor:39-59`. Check status is conditionally rendered only
when `DesiredRevision` is non-null at lines 35-37. This yields the following
observed conditions:

| Condition | Visible behavior | Source evidence |
| --- | --- | --- |
| Initial state is unconfigured, so desired revision is null | Check status is absent | `AccountPreferences.razor:35-37`; `AccountPreferencesPageViewModel.cs:8, 32` |
| The API has no observed Boolean value | Enabled, Disabled, and Save preferences are absent | `AccountPreferences.razor:39-59`; `AccountPreferencesPageViewModel.cs:17, 28-30` |
| An action or initial load is in progress | Save is disabled; Check status is disabled while save or check is in progress | `AccountPreferences.razor:36, 52` |
| Required Operator scope is unavailable | The page displays Redirecting and never loads controls | `AccountPreferences.razor:10-13, 92-95` |
| The web request fails | The presenter displays an error instead of silently succeeding | `AccountPreferencesPagePresenter.cs:54-91, 104-109` |

The component methods forward button events to the scoped presenter with the
page lifetime cancellation token:

* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:82-108`

The presenter creates a new idempotency key for each save, sends its selected
Boolean and desired revision, applies a successful response, and refreshes
history. Check status sends a body-free request and applies its response:

* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPagePresenter.cs:54-91`
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPagePresenter.cs:104-119`

The API client uses an authorized Operator-scoped PUT with `Idempotency-Key`
and a POST to the check-status route:

* `src/TNC.Trading.Platform.Web/PlatformApiClient.cs:143-171`

The corresponding API routes require the Operator policy:

* `src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs:49-58`

The PUT handler rejects a missing Boolean or idempotency key before dispatching
to the application command:

* `src/TNC.Trading.Platform.Api/Features/Platform/UpdateAccountPreferencesEndpointHandler.cs:8-20`

At application level, Check status returns early without provider I/O when
there is no desired revision or account. It can report account mismatch, lease
contention, stale state, or provider failure. It also remediates provider drift
before persisting an in-sync observation:

* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/CheckAccountPreferencesStatusHandler.cs:15-42`

Save can fail because there is no authenticated IG account, an account changed,
a revision is stale, a lease is unavailable, the provider cannot confirm the
change, or durable finalization fails:

* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/SaveAccountPreferencesHandler.cs:15-58`

The operator guide and API reference agree with these command semantics:

* `docs/wiki/operator-guide.md:567-589`
* `docs/wiki/api-reference.md:553-570`

### Why the commands may appear not to work

No broken client-side event wiring was found. Current browser E2E tests click
both controls on the interactive page and assert observable results. The
strongest supported explanations for a user-facing failure are:

1. The relevant control is hidden by its state gate, notably Check status
	without a desired revision or Save before an observed value exists.
2. A button is disabled while the load, save, or check operation is active.
3. The Operator scope/navigation guard prevents the page from reaching its
	interactive state.
4. The request reaches the server and the presenter renders an error or durable
	warning for authorization, account binding, stale-revision, lease, or IG
	provider failure.

The last case can look like no action when an operator has not noticed the
page-level alert, but it is not a no-op in the implementation. The page's
top-level current error is visible at `AccountPreferences.razor:16`; durable
server warning is visible at lines 17-20; save error is visible at line 55.

## Existing Tests and Conventions

The repository standard is xUnit, bUnit for in-process Blazor rendering, and
Playwright for browser-only workflows. It requires descriptive
`MethodName_StateUnderTest_ExpectedResult` names and comments documenting
traceability, behavior, expected result, and regression risk:

* `.github/instructions/tests.instructions.md:15-76`

Existing tests cover the relevant layers:

| Test layer | Verified coverage | Evidence |
| --- | --- | --- |
| bUnit unit tests | Initial state, conditional editor visibility, native radio binding, disabled save during requests, successful save, error feedback, and outbound Boolean JSON | `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountPreferencesTests.cs:18-355, 366-435, 481-630` |
| API client unit tests | Typed Problem Details preservation and malformed-problem fallback for the save request | `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/PlatformApiClientTests.cs:278-322` |
| Functional tests | Real authenticated PUT and POST contracts, drift convergence, provider outage recovery, and account mismatch behavior | `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/AccountPreferences/AccountPreferencesFunctionalTests.cs:17-162` |
| Playwright E2E tests | Radio selection, save confirmation, Check status results, responsive geometry, tab navigation, and history controls | `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/AccountPreferences/AccountPreferencesE2ETests.cs:22-218` |

The E2E test at lines 87-108 explicitly asserts the current behavior that Save
is to the right of the fieldset on a 1280px viewport. It demonstrates that the
reported horizontal placement is intentional and tested, not a regression in
uncovered CSS.

## Alternatives Considered

### Preserve the current expanding fieldset

Leave the configuration row unchanged. This retains the current E2E geometry
but preserves the visual contrast with the left-aligned status action and
continues to spend unused width between choice labels and Save.

### Move Save to a separate left-aligned line

Render Save in its own action group below the radios, matching the Status
section's left alignment. This removes the rightward distance but makes the
setting-and-commit workflow taller and abandons the existing compact desktop
control row.

### Use a Radzen layout or input component

Radzen layout primitives could arrange the controls, but native Blazor radios
are already semantically correct, keyboard-tested, and appropriate for two
Boolean choices. Replacing them would add a component-library interaction
pattern without addressing the actual CSS growth rule.

## Recommended Solution

Keep the native `InputRadioGroup` and its existing presenter/API contract.
Change only the configuration layout so it matches the status section's
left-start action alignment:

* Remove `flex: 1 1 auto` from `.account-preferences-choice`
* Change `.account-preferences-control-row` to `align-items: center`
* Preserve its wrapping and gap for narrow screens
* Keep the radios and Save in the same named preference action group, with
  Save directly after the natural-width radio group

This is the smallest change that removes the unexplained empty horizontal area,
aligns the row vertically, retains the existing controls and accessibility
semantics, and does not require API or view-model changes. Do not change the
status action CSS; it is already left aligned by default flex behavior.

## Proposed Tests

1. Replace the current wide-viewport geometry assertion in `AccountPreferencesE2ETests` with one that verifies Save begins shortly after the radio group rather than after a fieldset expanded to the row width. Keep the existing no-horizontal-overflow assertion.
2. Add a Playwright assertion at desktop width that the vertical centers of the first radio label and Save are within a small pixel tolerance after `align-items: center` is applied.
3. Retain the narrow-viewport wrap test and assert that both radio choices and Save remain visible and keyboard reachable after wrapping.
4. Add bUnit coverage that Check status is absent when `DesiredRevision` is null and present after a loaded response supplies it. Add a companion test that a visible Check status click issues POST `/api/platform/account-preferences/check-status` and applies its returned state.
5. Keep the existing save JSON test and add an explicit assertion that a disabled Save produces no additional PUT request while a request is in flight.

## Unanswered Questions

* The reported runtime context is not available. Browser console errors, network responses, and the visible disabled/alert state are needed to identify which command-failure condition occurred for the user.
* Product intent has not specified whether the configuration controls should remain on one row until natural wrapping, or stack below a particular viewport width. The recommended natural-wrap behavior preserves the current responsive strategy.
* The current E2E assertion encodes right-of-fieldset placement. The product owner should confirm that the desired replacement is left-aligned Save immediately after the radio group before that assertion is changed.

## Next Research

* Capture the affected browser session's Network and Console output while clicking each command.
* Compare the viewport and computed styles from the reported screen with the E2E fixture's 1280px layout.
* Confirm the intended desktop and mobile control arrangement with the operator workflow owner.
