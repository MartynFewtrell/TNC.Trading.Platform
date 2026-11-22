---
title: Account preferences alternatives analysis
description: Selected UI approach and implementation-ready state flow for account-preferences clarity changes
ms.date: 2026-08-31
ms.topic: concept
---

## Research scope

* Compare `RadzenSwitch`, installed `RadzenSelectBar<bool>`, and a styled
  `InputRadioGroup<bool>` for the trailing-stops preference.
* Compare `RadzenNotification`, installed `RadzenAlert`, and a custom
  dismissible inline `role="status"` box for save confirmation.
* Decide whether the state-driven heading represents the pending selection or
  the last server-confirmed state.
* Define a state flow that prevents stale success feedback and contradictory
  success and error feedback when history refresh fails.
* Identify exact production and test impact, accessibility behavior,
  responsive constraints, and focused validation.

## Status

Complete. The two completed account-preferences research reports were treated
as verified inputs, and the current owning UI, presenter, view model, styles,
and test harnesses were checked for exact impact. No production or test code was
modified during this research-only analysis.

## Decision

Use a styled native `InputRadioGroup<bool>` with visible `Enabled` and
`Disabled` options, a heading that reflects the pending selection, and a custom
persistent inline success box with polite status semantics and a named dismiss
button. Track the last server-confirmed Boolean separately in the page view
model. A selection change clears old interaction feedback, and a failed history
refresh becomes qualified success feedback rather than a save error.

This combination gives the clearest visible choice and the most accurate
installed-version semantics. It also matches the repository's existing inline
feedback convention without relying on a timed overlay or an inaccessible
component close button.

## Boolean control comparison

| Candidate                        | Installed behavior and fit                                      | Decision |
|----------------------------------|-----------------------------------------------------------------|----------|
| `RadzenSwitch` 10.3.2            | Native checkbox with `role="switch"`; compact and keyboard operable, but only the current on/off position is visible without extra state text | Reject for this requirement because both named choices must remain visible |
| `RadzenSelectBar<bool>` 10.3.2    | Toolbar containing `aria-pressed` buttons; both labels are visible, but version 10.3.2 does not expose radio-group semantics and selection requires Space or Enter after arrow-key focus | Reject because a two-choice mutually exclusive setting is not a toolbar |
| Styled `InputRadioGroup<bool>`   | Native exclusive radio choice; both labels remain visible; fieldset and legend provide a stable group name; browser-native Tab, Arrow, and Space behavior | Select as the strongest semantic and visual fit |

The repository preference for Radzen components does not require a Radzen
control when built-in Blazor and semantic HTML are sufficient. The scoped
Radzen guidance explicitly rejects complex components where plain HTML or
built-in controls meet the need.

## Feedback comparison

| Candidate                                  | Installed behavior and fit                                      | Decision |
|--------------------------------------------|-----------------------------------------------------------------|----------|
| `RadzenNotification` 10.3.2                | Polite popup with a named close button, but defaults to a 3000 ms timeout and can obscure content or focus at narrow widths | Reject because save confirmation must remain available until deliberate dismissal |
| `RadzenAlert` 10.3.2                       | Persistent inline polite live region and dismissible by default, but its icon-only close button has no explicit accessible name in this version | Reject until the package supplies a reliable close-label parameter or is upgraded |
| Custom dismissible inline `role="status"` | Persistent normal-flow box with explicit polite semantics, atomic announcement, visible text, and a named native dismiss button | Select because semantics, lifetime, focus, and tests remain under local control |

The durable application status and interaction confirmation must be separate.
Dismissal removes only the save confirmation. `Application status: ...` remains
visible as domain data.

## Heading semantics

Select a heading that reflects the editable pending value, for example
`Trailing stops selection: Enabled`. The radio state changes immediately, so a
pending-selection heading gives direct visual confirmation of the operator's
action. The word `selection` avoids claiming that the provider has accepted the
value.

Render the authoritative value separately as `Last confirmed: Enabled` or
`Last confirmed: Disabled`. When the values differ, add concise non-live text
such as `Save preference to apply this selection.` Do not announce the heading
as a live region because the user-initiated radio state change already provides
the accessible state update.

A heading based only on the last server-confirmed value was rejected. It would
remain static after a radio change and weaken the requested immediate clarity.
Using pending wording without a separate confirmed value was also rejected
because it would blur local selection and provider-confirmed state.

## State ownership and flow

Keep all new state in the Web adapter. The view model should distinguish:

* `TrailingStopsEnabled`: editable pending selection
* `ConfirmedTrailingStopsEnabled`: last value returned by the server
* `HasPendingChange`: comparison of pending and confirmed values

`Apply` sets both Boolean properties from an authoritative GET or confirmed PUT
response. A new `SelectTrailingStops(bool enabled)` method changes only the
pending value. Do not disable unchanged saves in this clarity change because
the current workflow permits repeated equal writes to create observations, and
product intent for that behavior is unresolved.

The presenter should expose two small interaction methods:

```csharp
public void SelectTrailingStops(bool enabled)
{
    State.SelectTrailingStops(enabled);
    Message = null;
    Error = null;
}

public void DismissMessage() => Message = null;
```

Clearing both fields on a new selection prevents an old success or failed-save
message from describing the newly pending value. Initial-load feedback is not
affected because the selector is unavailable during initial loading.

Use this save sequence:

1. Set `IsSaving`, clear `Error`, and clear `Message`.
2. Await the PUT and authoritative GET through the existing
   `UpdateAccountPreferencesAsync` call.
3. If that call fails, set only
   `Unable to save and confirm account preferences.` and do not show success.
4. If it succeeds, call `State.Apply(response)` immediately. This updates both
   pending and confirmed values to the server-confirmed result.
5. Set `Message` to `Trailing stops preference saved and confirmed.`
6. Refresh history in a nested `try` block.
7. If history refresh fails with an expected HTTP or invalid-operation failure,
   keep `Error` null and replace the message with
   `Trailing stops preference saved and confirmed. Observed history could not be refreshed.`
8. Preserve the existing owned-cancellation behavior and reset `IsSaving` in
   `finally`.

This ordering prevents both current defects. A later edit clears success, so
feedback cannot become stale. A secondary history failure never enters the
save-failure catch, so the page cannot claim both confirmed success and failed
save. The combined polite message accurately reports the partial outcome in one
place. A later explicit history action can retain its existing load-failure
behavior.

## Implementation-ready markup sketch

The following sketch shows structure and state wiring rather than final visual
copy:

```razor
<section class="panel platform-page-header">
    <div>
        <h1>Trailing stops selection: @(Presenter.State.TrailingStopsEnabled ? "Enabled" : "Disabled")</h1>
        <p class="hint">Choose whether trailing stops are enabled for the configured Test account.</p>
    </div>
</section>

@if (!string.IsNullOrWhiteSpace(Presenter.Error))
{
    <p class="error" role="alert" data-testid="account-preferences-error">@Presenter.Error</p>
}

<section class="panel account-preferences-editor">
    <fieldset class="account-preferences-choice" disabled="@Presenter.IsSaving">
        <legend>Trailing stops</legend>
        <InputRadioGroup TValue="bool"
                         Value="Presenter.State.TrailingStopsEnabled"
                         ValueChanged="SelectionChanged"
                         ValueExpression="@(() => Presenter.State.TrailingStopsEnabled)">
            <label class="account-preferences-choice__option">
                <InputRadio TValue="bool" Value="true" />
                <span>Enabled</span>
            </label>
            <label class="account-preferences-choice__option">
                <InputRadio TValue="bool" Value="false" />
                <span>Disabled</span>
            </label>
        </InputRadioGroup>
    </fieldset>

    <p id="trailing-stops-confirmed">Last confirmed: @(Presenter.State.ConfirmedTrailingStopsEnabled ? "Enabled" : "Disabled")</p>
    @if (Presenter.State.HasPendingChange)
    {
        <p class="hint">Save preference to apply this selection.</p>
    }

    <button @ref="saveButton" class="platform-primary-action" type="button"
            data-testid="account-preferences-save" @onclick="SaveAsync"
            disabled="@(Presenter.IsSaving || Presenter.IsLoading)">
        @(Presenter.IsSaving ? "Saving..." : "Save preference")
    </button>

    @if (!string.IsNullOrWhiteSpace(Presenter.Message))
    {
        <div class="account-preferences-confirmation" role="status"
             aria-atomic="true" data-testid="account-preferences-status">
            <span>@Presenter.Message</span>
            <button type="button" @onclick="DismissMessageAsync"
                    aria-label="Dismiss save confirmation">Dismiss</button>
        </div>
    }

    <p data-testid="account-preferences-application-status">
        Application status: @Presenter.State.ApplicationStatus
    </p>
</section>
```

`SelectionChanged` calls `Presenter.SelectTrailingStops(enabled)`. The dismiss
handler calls `Presenter.DismissMessage()`, rerenders, and returns focus to the
save button through its `ElementReference`. This explicit focus restoration is
needed because activation removes the currently focused dismiss button from the
document.

## Accessible interaction contract

* The `fieldset` and `legend` expose the stable group name `Trailing stops`.
* Each native radio has a visible label, `Enabled` or `Disabled`, and exactly
  one radio is checked.
* Tab enters and leaves the group. Arrow keys move and select within it. Space
  selects the focused option. No custom keyboard handler is needed.
* Disabled save state also disables the fieldset while the confirmed update is
  pending. The Boolean `false` value never controls disabled state.
* The dynamic heading is visible context, not a live region. Radio checked state
  supplies the immediate assistive-technology update.
* Errors retain `role="alert"`. Confirmed or qualified-success feedback uses
  `role="status"` with `aria-atomic="true"` and does not move focus when it first
  appears.
* The dismiss button has the stable accessible name
  `Dismiss save confirmation`. After dismissal, focus returns to
  `Save preference`.
* Application status stays in normal document flow and remains visible after
  confirmation dismissal.
* Focus order remains heading, radio group, save, dismiss when present, durable
  application status, and history controls.

## Responsive constraints

Add page-scoped CSS rather than new global component behavior. The segmented
radio wrapper should use two equal flexible tracks at ordinary widths and one
column below a narrow breakpoint. Each option needs a minimum 44 by 44 CSS
pixel activation area, visible focus treatment, and wrapping text. Avoid fixed
widths and markup-dependent Radzen selectors.

The confirmation box should use normal flow with a flexible text column and a
non-shrinking dismiss button. At 320 CSS pixels, stack the dismiss action below
the message when needed. The page must have no horizontal document overflow at
320 CSS pixels or 400% browser zoom. The heading, longest option label, save
button, and status text must wrap without overlap or clipping.

## Exact implementation impact

Production changes are confined to the Web presentation adapter:

* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:9-25`
  replaces the fixed heading, checkbox, combined status paragraph, and editor
  markup. Lines 37-39 gain selection and dismissal handlers plus save-button
  focus restoration.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPagePresenter.cs:5-50`
  adds selection and dismissal methods and splits confirmed update handling
  from secondary history-refresh failure handling.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPageViewModel.cs:3-19`
  adds confirmed-value and dirty-state ownership, updates `Apply`, and adds the
  pending-selection method.
* New file
  `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor.css:1-end`
  owns the segmented-radio, focus, confirmation, and narrow-width layout.

Focused automated tests remain in one existing file:

* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountPreferencesTests.cs:14-50`
  updates checkbox-based initial, selection, and successful-save assertions to
  radio-group semantics and the pending versus confirmed heading.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountPreferencesTests.cs:123-136`
  updates pending-save assertions to cover the disabled fieldset as well as the
  save action.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountPreferencesTests.cs:173`
  is the insertion point before helpers for stale-confirmation, dismissal,
  outbound Boolean, save-failure, and history-refresh-failure cases.

No change is needed in `PlatformApiClient`, API endpoint code, application
handlers, provider gateway, persistence, project files, or the shared bUnit
test context. No new browser test file should be added in this slice because the
existing browser harness has no deterministic account-preferences provider
seam.

## No-backend-change rationale

All three candidate controls map directly to the existing non-nullable Boolean.
The current PUT already sends that Boolean, and the application already performs
an authoritative GET before returning success. The clarity issue is therefore
presentation state: naming pending and confirmed values, invalidating stale
feedback, and classifying a secondary history-refresh result.

The selected design preserves the route, authorization policy, request and
response DTOs, PUT and GET sequence, HTTP status mapping, provider safety rules,
and append-only history. Changing backend contracts would increase risk without
adding information needed by this UI.

## Focused validation

Extend bUnit coverage with requirement-documented tests that assert observable
semantics rather than CSS internals:

* Loaded `true` and `false` values each check exactly one corresponding radio,
  render both visible choices, and show matching pending and confirmed text.
* Selecting with the radio input changes the pending heading, retains the last
  confirmed text, and shows the unsaved cue.
* Saving sends the selected Boolean in the captured PUT JSON, applies the
  authoritative response, clears dirty state, and shows one polite status.
* Changing selection after success removes the old confirmation.
* Dismissal removes only confirmation and leaves application status visible.
* A PUT or confirmation failure shows only the urgent error and no success box.
* A successful update followed by failed history refresh shows only the
  qualified combined status, never the generic save error.
* While saving, both radio choices and the save action are disabled.

Run the focused test project command from the repository root:

```powershell
dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj --filter "FullyQualifiedName~AccountPreferencesTests"
```

Browser validation should use an authenticated local Operator session with a
configured IG Test account. Verify with role and label locators where possible:

1. At desktop width, Tab to `Trailing stops`, use Arrow keys to select each
   option, and confirm visible focus, checked state, and pending heading.
2. Save and confirm the status is announced without focus movement. Tab to
   `Dismiss save confirmation`, activate it, and verify focus returns to
   `Save preference` while application status remains.
3. Repeat at a 320 CSS pixel viewport and at 400% zoom. Verify no horizontal
   page overflow, clipping, overlap, or obscured focus.
4. Check NVDA with Firefox or Chrome and Narrator with Edge for group name,
   option state, polite success announcement, urgent error announcement, and
   named dismissal.

Do not make a new Playwright CI test depend on live IG credentials. Browser
automation becomes appropriate after the AppHost can supply a supported fake
account-preferences boundary or another deterministic provider fixture. Browser
request interception alone cannot replace these API responses because the
Blazor Server UI calls the API from the server process.

## Rejected alternatives summary

* Reject `RadzenSwitch` because it cannot keep both domain choices visible
  without supplemental UI, while the requested clarity centers on explicit
  `Enabled` and `Disabled` choices.
* Reject installed `RadzenSelectBar<bool>` because its toolbar and toggle-button
  semantics are weaker than native radios for one choice from two values.
* Reject `RadzenNotification` because its default timeout and fixed overlay make
  the only save confirmation easier to miss and harder to reflow safely.
* Reject installed `RadzenAlert` because version 10.3.2 lacks an accessible name
  for its icon-only close button.
* Reject a confirmed-state-only heading because it provides no immediate visual
  response to an edited selection.
* Reject showing both a success message and generic save error after history
  failure because the save did succeed. Use one qualified status instead.

## Remaining gaps

* Exact product-approved copy for the heading, introductory sentence,
  confirmation, and qualified history-refresh message is not supplied.
* Product intent for deliberately saving an unchanged value remains unresolved;
  this recommendation preserves current behavior.
* Native segmented-radio visuals and focus treatment have not been rendered in
  the current theme.
* No screen-reader or 320 CSS pixel browser matrix has been run.
* Deterministic browser automation for a successful provider-backed account
  preference flow requires a test boundary that the current AppHost does not
  expose.

## Suggested follow-up

* [ ] Confirm the proposed pending-selection and last-confirmed wording with the
  product owner.
* [ ] Implement the four-file Web slice and focused bUnit cases listed above.
* [ ] Run the focused bUnit command and then the broader Web unit-test project.
* [ ] Complete the authenticated keyboard, reflow, zoom, and screen-reader
  browser checks in a configured Test environment.
* [ ] Consider a deterministic provider fixture as separate test-infrastructure
  work before adding account-preferences Playwright coverage to CI.

## Evidence

Verified research inputs:

* `.copilot-tracking/research/subagents/2026-08-31/account-preferences-repository-research.md`
* `.copilot-tracking/research/subagents/2026-08-31/account-preferences-accessibility-research.md`

Current implementation evidence:

* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:1-45`
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPagePresenter.cs:1-50`
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPageViewModel.cs:1-20`
* `src/TNC.Trading.Platform.Web/wwwroot/app.css:235-290,438-459`
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountPreferencesTests.cs:1-216`
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/PlatformComponentTestContext.cs:1-158`
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/TNC.Trading.Platform.Web.FunctionalTests.csproj:1-31`
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/TNC.Trading.Platform.Web.E2ETests.csproj:1-28`

Repository rules applied:

* `.github/copilot-instructions.md`
* `.github/instructions/folders.instructions.md`
* `.github/instructions/radzen-blazor.instructions.md`
* `.github/instructions/tests.instructions.md`
* `.github/instructions/playwright.instructions.md`
* `.github/instructions/csharp.instructions.md`
* `.github/instructions/architecture.instructions.md`
* External Markdown and writing-style instructions supplied for this workspace
  session