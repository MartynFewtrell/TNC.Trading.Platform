---
title: Account Preferences Trailing Stops UI Research
description: Verified investigation of the trailing stops status binding and control-row layout.
author: GitHub Copilot
ms.date: 2026-09-19
ms.topic: troubleshooting
keywords:
  - account preferences
  - trailing stops
  - blazor
  - radzen
  - ui research
estimated_reading_time: 8
---

## Research Scope

Investigate the account-preferences trailing-stops UI reports:

* `Last confirmed` displays `Disabled` instead of a timestamp or blank value
* `Enabled`/`Disabled` and `Save preferences` render as vertically stacked, large outlined boxes rather than a compact horizontal control row

This note will identify the controlling Razor, CSS, view-model, and test code; assess likely causes; and propose verified implementation and validation options. Product code is intentionally out of scope.

## Status

Complete. The two reported behaviors are independently reproduced from the
source path and their direct controls have been identified. No product code was
modified during this investigation.

## Verified Evidence

### Timestamp data path

| Layer | Verified behavior | Evidence |
|-------|-------------------|----------|
| API projection | The current-state response includes nullable `LastVerifiedAtUtc` from the application state | `src/TNC.Trading.Platform.Api/Features/Platform/AccountPreferencesEndpointMapping.cs:8-13, 113-126` |
| Web transport DTO | JSON deserialization retains `LastVerifiedAtUtc` as a nullable `DateTimeOffset` | `src/TNC.Trading.Platform.Web/PlatformApiClient.cs:218-239` |
| Page view model | `Apply` copies `preferences.LastVerifiedAtUtc` into `LastVerifiedAtUtc` | `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPageViewModel.cs:12, 25-40` |
| Razor display | The `Last confirmed` description conditionally uses `ConfirmedTrailingStopsEnabled`, then renders the Boolean as `Enabled` or `Disabled` | `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:27-30` |

`ConfirmedTrailingStopsEnabled` is deliberately assigned from
`preferences.ObservedTrailingStopsEnabled`, while `LastVerifiedAtUtc` is also
assigned but never consumed by the Razor markup. The name `Last confirmed`
therefore describes a time while its bound data describes the last observed
Boolean value. This is the direct and sufficient cause of the first report.

The API can legitimately supply no time because the property is nullable. The
unconfigured response explicitly supplies `null` for it:
`src/TNC.Trading.Platform.Api/Features/Platform/AccountPreferencesEndpointMapping.cs:15`.

### Layout path

| Visible behavior | Direct control | Evidence |
|------------------|----------------|----------|
| Two outlined choice boxes | Each native-radio label has a border, surface background, `0.625rem 0.875rem` padding, and a `44px` minimum height | `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor.css:90-103` |
| Selected outlined box | The selected label increases to a `2px` accent border and adds a tinted background | `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor.css:108-114` |
| Horizontal choices on larger viewports | The `InputRadioGroup` generated direct `div` is a two-column CSS grid | `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor.css:83-88` |
| Vertically stacked choices on narrow viewports | The `max-width: 600px` rule changes that grid to one column | `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor.css:159-170` |
| Save below the choices | The fieldset and the Save button's action group are separate block-level siblings | `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:40-48` |
| Full-width Save on narrow viewports | The mobile media query assigns `inline-size: 100%` to action buttons | `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor.css:172-174` |

The large outlined appearance is intentional CSS, not a generated-style
failure. At any viewport the separate sibling containers prevent the Boolean
choices and Save command from becoming one horizontal row. At `600px` or less,
both controls are explicitly stacked. The compiled scoped CSS preserves these
rules, confirming the source stylesheet is active:
`src/TNC.Trading.Platform.Web/obj/Debug/net10.0/scopedcss/Components/Pages/AccountPreferences/AccountPreferences.razor.rz.scp.css:83-104, 163-174`.

### Existing tests and coverage gap

* The bUnit test helper creates its simple response with the verification time
  populated, but UI assertions check `Last confirmed` together with Boolean
  text, not a time. See
  `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountPreferencesTests.cs:12, 219-233, 242-252`.
* The E2E tests assert that `Last confirmed` is followed by `Disabled` or
  `Enabled`; these assertions encode the reported defect. See
  `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/AccountPreferences/AccountPreferencesE2ETests.cs:42-72`.
* Existing bUnit tests validate semantic tabs, radio presence, action group
  names, disabled behavior, and saves. They do not execute scoped CSS or
  assert geometry. See
  `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountPreferencesTests.cs:102-138, 286-332`.
* The E2E project includes Playwright, so it can validate viewport-specific
  layout without adding a framework. See
  `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/TNC.Trading.Platform.Web.E2ETests.csproj:8-14`.

## Cause Assessment

### Last confirmed value

Confidence: high.

The field's label and binding disagree. The existing Boolean remains useful
for a distinct row such as `Last confirmed setting`, but it cannot represent a
confirmation time. The correct transport property is already available in the
page view model; the change is a Razor binding correction rather than an API,
presenter, persistence, or reconciliation change.

### Compact control row

Confidence: high.

The DOM structure and scoped CSS explicitly implement separate vertical
blocks, tile-like radio labels, and mobile stacking. No evidence indicates a
Radzen rendering regression. The requested compact horizontal control row
requires both a markup grouping change and scoped CSS changes; changing only
the media query would still leave Save below the fieldset.

## Implementation Alternatives

### Alternative A: Keep native Blazor radios and make the row compact

Move the editor and Save command into one named control row. Bind the status
time to `LastVerifiedAtUtc` and omit the row when no time exists, preserving
the current absence-of-unconfirmed-information behavior.

```razor
<dl class="account-preferences-state">
    <div>
        <dt>Trailing stops</dt>
        <dd>@(Presenter.State.DesiredTrailingStopsEnabled is bool desired
            ? (desired ? "Enabled" : "Disabled") : "Unconfigured")</dd>
    </div>
    @if (Presenter.State.LastVerifiedAtUtc is { } lastVerifiedAtUtc)
    {
        <div>
            <dt>Last confirmed</dt>
            <dd><time datetime="@lastVerifiedAtUtc.ToString(&quot;O&quot;)">@lastVerifiedAtUtc.ToLocalTime().ToString("g")</time></dd>
        </div>
    }
</dl>

<fieldset class="account-preferences-controls" disabled="@Presenter.IsSaving">
    <legend>Trailing stops configuration</legend>
    <div class="account-preferences-control-row" role="group" aria-label="Preference actions">
        <InputRadioGroup TValue="bool" Value="@Presenter.State.TrailingStopsEnabled"
                         ValueChanged="Presenter.Select"
                         ValueExpression="@(() => Presenter.State.TrailingStopsEnabled)">
            <label><InputRadio Value="true" /> Enabled</label>
            <label><InputRadio Value="false" /> Disabled</label>
        </InputRadioGroup>
        <button @ref="saveButton" class="platform-primary-action" type="button"
                data-testid="account-preferences-save" @onclick="SaveAsync"
                disabled="@(Presenter.IsSaving || Presenter.IsOperating || Presenter.IsLoading)">
            @(Presenter.IsSaving ? "Saving..." : "Save preferences")
        </button>
    </div>
</fieldset>
```

```css
.account-preferences-controls {
    margin: 1.25rem 0;
    padding: 0;
    border: 0;
}

.account-preferences-control-row,
.account-preferences-control-row > div {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: 0.75rem;
}

.account-preferences-control-row label {
    display: inline-flex;
    align-items: center;
    gap: 0.5rem;
    min-block-size: 44px;
    padding: 0;
    border: 0;
    background: transparent;
}

.account-preferences-control-row label + label {
    margin: 0;
}

.account-preferences-control-row label:has(input[type="radio"]:checked) {
    font-weight: 700;
}

@media (max-width: 600px) {
    .account-preferences-control-row {
        align-items: flex-start;
    }
}
```

This is the lowest-risk approach: it retains native radio semantics, the
existing `Presenter.Select(bool)` contract, and the current direct binding.
It also removes the conflicting mobile full-width action rule for this
specific control row while retaining the existing `44px` target size.

### Alternative B: Use a Radzen radio list with a Radzen stack

The project already uses Radzen Tabs and repository guidance prefers Radzen
components for common controls. Radzen documents `RadzenRadioButtonList` with
`TValue`, static items, `Orientation`, wrapping, alignment, and `Change` as
the appropriate compact radio-list pattern. Source:
<https://blazor.radzen.com/radiobuttonlist>.

```razor
<fieldset class="account-preferences-controls" disabled="@Presenter.IsSaving">
    <legend>Trailing stops configuration</legend>
    <RadzenStack Orientation="Orientation.Horizontal" AlignItems="AlignItems.Center"
                 Wrap="FlexWrap.Wrap" Gap="0.75rem" role="group"
                 aria-label="Preference actions">
        <RadzenRadioButtonList TValue="bool"
                               Value="@Presenter.State.TrailingStopsEnabled"
                               Change="Presenter.Select"
                               Orientation="Orientation.Horizontal"
                               AlignItems="AlignItems.Center"
                               Wrap="FlexWrap.Wrap" Gap="0.75rem">
            <Items>
                <RadzenRadioButtonListItem Text="Enabled" Value="true" />
                <RadzenRadioButtonListItem Text="Disabled" Value="false" />
            </Items>
        </RadzenRadioButtonList>
        <button @ref="saveButton" class="platform-primary-action" type="button"
                data-testid="account-preferences-save" @onclick="SaveAsync"
                disabled="@(Presenter.IsSaving || Presenter.IsOperating || Presenter.IsLoading)">
            @(Presenter.IsSaving ? "Saving..." : "Save preferences")
        </button>
    </RadzenStack>
</fieldset>
```

This option could reduce bespoke radio-layout CSS, but it changes rendered
markup and may require adapting bUnit/Playwright locators and confirming that
the enclosing disabled fieldset disables the installed Radzen version as
expected. It is broader than the reported fix.

## Recommendation

Choose Alternative A in a focused page-level change.

* Render `Last confirmed` from `LastVerifiedAtUtc`; render no row when it is
  null. This directly meets the timestamp-or-blank report using an existing
  API value and avoids a backend change.
* Keep native `InputRadio` controls, but place them and Save in a single
  flex-wrapping row and replace tile styling with compact inline labels.
* Do not change `ConfirmedTrailingStopsEnabled`, `HasConfirmedValue`, or the
  API contract. Those still control whether a confirmed editor can be shown
  and whether there is an unsaved selection.

Alternative B is appropriate only if the team wants to standardize this
specific editor on Radzen's radio-list component as a deliberate UI-library
adoption, rather than to fix this report.

## Accessibility Considerations

* Use `<time datetime="...">` for the verified instant. The visible localized
  time can use the existing `ToLocalTime().ToString("g")` convention already
  used for history in
  `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:60`.
* Preserve a `<fieldset>` and meaningful `<legend>` so the radio choices remain
  announced as one group. A compact visual row must not replace native radio
  input semantics with generic clickable elements.
* Retain the existing 44px minimum interactive size and focus-visible outline;
  removing label tiles must not remove keyboard focus feedback. The existing
  focus rule is at
  `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor.css:115-120`.
* Use the radio's checked state, not color alone, to communicate selection.
  A bold label is supplementary; the native checked input supplies the primary
  state indicator.
* Keep the DOM order as Enabled, Disabled, Save so keyboard traversal follows
  the visual workflow. Keep the existing Save disabled states and confirmation
  focus return behavior at
  `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:47-55, 87-92`.

## Focused Validation Recommendations

1. Add a bUnit render test with a known UTC value that asserts the `Last
   confirmed` description contains a `time` element with an ISO-8601
   `datetime` attribute and does not contain `Enabled` or `Disabled` as its
   value. Add a null-time case that asserts no `Last confirmed` row is
   rendered. The closest test surface is
   `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountPreferencesTests.cs:102-138`.
2. Update the three E2E assertions at
   `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/AccountPreferences/AccountPreferencesE2ETests.cs:42-72`
   to locate the status time instead of asserting its old Boolean output.
3. Add one Playwright desktop viewport check that confirms the selected radio
   control and Save button share a row, using bounding-box top coordinates
   rather than generated component classes. Add a narrow viewport check that
   permits wrapping without overflow and retains usable radio and Save targets.
   Capture screenshots on failure for visual diagnosis.
4. Run the focused Web unit test project first, then the AccountPreferences
   E2E class. The E2E suite uses Aspire and real authentication, so run it only
   with its fixture-owned environment available.

## Remaining Gaps

* The requested blank behavior is ambiguous: this note recommends omitting the
  entire `Last confirmed` row when no timestamp exists, matching the current
  conditional-row pattern. If an always-visible label with an empty value is a
  product requirement, use an empty `<dd>` and an accessible explanation
  rather than a misleading Boolean.
* No browser viewport or screenshot was executed because this was research
  only. The CSS source provides a definitive cause, but a focused Playwright
  run should verify the desired geometry after implementation.
* Alternative B's exact rendered markup and disabled propagation should be
  confirmed against the repository's installed Radzen package before adoption.
