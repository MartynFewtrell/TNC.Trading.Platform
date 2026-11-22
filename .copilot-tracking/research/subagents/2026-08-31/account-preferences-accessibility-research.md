---
title: Account Preferences Accessibility Research
description: Authoritative guidance and implementation alternatives for explicit Boolean preference controls, state-driven headings, and dismissible save feedback
ms.date: 2026-08-31
ms.topic: concept
---

## Research scope

Investigate authoritative guidance and viable implementation alternatives for the
Blazor/Radzen account-preferences UI:

* Replace the checkbox with a control that visibly states Enabled and Disabled
  while preserving the Boolean backend contract
* Render a heading driven by the current Boolean state
* Replace persistent save confirmation with a dismissible popup or boxed
  notification
* Compare `RadzenSwitch` with an explicit two-option selector or segmented
  control
* Compare `RadzenNotification` with a dismissible inline alert
* Evaluate semantics, visible labels, keyboard operation, live-region behavior,
  dismissal, timeout risk, responsive behavior, and testability

## Repository applicability

* Current UI: `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor`
* Current presenter: `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPagePresenter.cs`
* Current tests: `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountPreferencesTests.cs`
* The page binds `InputCheckbox` directly to
  `Presenter.State.TrailingStopsEnabled`, displays a fixed `Account preferences`
  heading, and renders either save confirmation or application status in a
  persistent `role="status"` paragraph.
* The API and view-model contracts already use a non-nullable Boolean, so either
  candidate control can remain a presentation-layer mapping without changing
  the backend contract.

## Working hypothesis and discriminating checks

The explicit two-option selector is likely the clearest choice when both state
names must remain visible, while `RadzenSwitch` is likely suitable only when an
adjacent visible label and state text make its current Boolean value
unambiguous. Official component output and keyboard behavior, APG role patterns,
and WCAG timing and status-message requirements will confirm or disconfirm this.

The feedback choice depends on whether confirmation must persist until deliberate
dismissal. Official Radzen notification defaults and WAI/WCAG guidance will be
checked for live-region semantics, focus behavior, close controls, and timeout
risk.

## Findings

### Boolean control and visible state

The backend contract does not need to change. Blazor supports direct Boolean
binding for `RadzenSwitch`, `RadzenSelectBar<bool>`, and
`InputRadioGroup<bool>`. A native radio group needs two `InputRadio<bool>`
options whose values map to `true` and `false`; this is a presentation-layer
choice only.

The requirement to keep both `Enabled` and `Disabled` visible favors a
two-option selector. It lets users inspect both available values before acting,
and it does not rely on the position or color of a switch thumb to communicate
the alternatives.

The installed Radzen version changes the comparison materially:

* `RadzenSwitch` 10.3.2 renders an HTML checkbox with `role="switch"`, the
  native `checked` state, and `aria-checked`. It is a sound on/off control when
  it has a stable visible and accessible label such as `Trailing stops`.
* `RadzenSelectBar<bool>` 10.3.2 renders a `role="toolbar"` containing buttons
  with `aria-pressed`. It does not render the `radiogroup`, `radio`, and
  `aria-checked` semantics present in current upstream Radzen source.
* The WAI-ARIA Authoring Practices Guide recommends using `toolbar` to group
  three or more controls. A two-choice mutually exclusive setting is expressed
  more directly as a radio group.
* A native `InputRadioGroup<bool>` styled as two connected segments provides
  the required visual treatment while retaining native radio semantics. It is
  the strongest option on the installed package version.

`Enabled` and `Disabled` should remain the option labels. The radio group needs
its own visible group label, for example `Trailing stops`. For a switch, the
accessible label must not change with state. WAI-ARIA APG explicitly requires a
switch label to remain stable; the `aria-checked` state communicates on or off.
Adjacent visible state text can repeat `Enabled` or `Disabled`, but it should not
replace the stable control label.

### Keyboard operation

* A switch is a single tab stop. Space toggles it, and Enter is optional.
  `RadzenSwitch` uses a native checkbox input, so native Space behavior is
  available; Radzen also handles Enter.
* A native radio group follows the familiar radio pattern: Tab enters or leaves
  the group, arrow keys move and select among options, and Space selects the
  focused option. Browser behavior supplies this for native radio inputs.
* `RadzenSelectBar` 10.3.2 is one tab stop at the toolbar container. Left and
  Right Arrow move an internal focus index, while Space or Enter selects the
  focused item. Selection does not follow arrow-key focus in this version.
  This is internally consistent with a toolbar of toggle buttons, but it is less
  direct than radio semantics for one choice from two values.

All options require a visible focus indication and must preserve disabled
behavior while a save is pending. The implementation should not disable the
preference merely because the initial value is `false`; disabled state and
Boolean value are separate concepts.

### State-driven heading

A heading such as `Trailing stops: Enabled` or `Trailing stops: Disabled` can
derive directly from `Presenter.State.TrailingStopsEnabled`. Blazor rerenders
the text when the bound Boolean changes, with no backend or presenter contract
change.

The heading should describe the current pending form selection. It must not say
that the setting is saved until the authoritative save response returns. Save
confirmation remains a separate status message. If the heading is intended to
describe only confirmed server state, a separate pending field would be needed;
the current presenter uses the same value for both editable and confirmed state.

A changing heading is not, by itself, guaranteed to be announced by assistive
technology. That is acceptable for a user-initiated switch or radio change
because the control's updated checked state is announced. Do not add a live
region to the heading merely to repeat every selection. If state can change
asynchronously without user action, announce that event through a dedicated,
concise status region instead.

### Save confirmation and durable status

Successful save feedback is a WCAG status message: it reports the result of an
action without moving focus. It should use polite status semantics. Assertive
`role="alert"` is better reserved for important errors or time-sensitive
warnings; W3C lists assertive treatment of non-urgent content as a failure risk.

The current paragraph combines two different concepts:

* Transient interaction feedback: `Trailing stops preference saved and
  confirmed.`
* Durable domain data: `Application status: ...`

These should be rendered separately. Replacing or dismissing save confirmation
must not remove the durable application status from the page.

`RadzenNotification` 10.3.2 provides a polite live region and a close button
named `Close`. However, an omitted `Duration` does not mean persistent: source
for version 10.3.2 defaults it to 3000 milliseconds. A three-second toast is
risky when it is the only save confirmation. WCAG timing guidance says a timed
toast is acceptable only when the same information remains discoverable by
another means; otherwise, the time limit must be removable, adjustable, or
extendable.

`RadzenAlert` 10.3.2 is inline, polite (`aria-live="polite"`), persistent until
dismissed, and dismissible by default. It sets its internal visibility to false
when its close button is activated and exposes `Close` and `VisibleChanged`
callbacks. This makes the confirmation easy to locate again, keeps it in normal
document flow, and avoids timeout risk. Version 10.3.2 does not provide the
newer `CloseAriaLabel` parameter or render `role="alert"`; the close button is an
icon-only native button with no explicit accessible name in that version. This
is an accessibility defect for the proposed use unless the implementation can
add a reliable accessible name or uses a small custom inline status box with a
named close button.

Current upstream Radzen improves `RadzenAlert` by rendering `role="alert"`,
`aria-atomic="true"`, and an `aria-label` on the close button. Those current
docs must not be assumed to describe package 10.3.2.

### Responsive behavior

An inline message in normal flow is less likely than a fixed toast to obscure a
focused control and naturally contributes to page height. A fixed notification
must be checked against WCAG 2.2 Focus Not Obscured and against reflow at a
320 CSS pixel viewport. Its text and close target must remain fully usable
without horizontal page scrolling.

The two-option selector should allow its labels to wrap or stack vertically at
narrow widths. It must not rely on a fixed width that clips `Disabled` at
320 CSS pixels or at 400% browser zoom. The heading, selector, confirmation,
save button, and application status should remain in a logical reading and focus
order.

### Testability

Prefer assertions against user-observable semantics over Radzen CSS class names:

* Query the group by its accessible name and assert that it exposes two options
  named `Enabled` and `Disabled`.
* Assert exactly one option is checked and that its Boolean mapping matches the
  loaded value.
* Exercise keyboard selection in browser-level tests: Tab into the group, use
  arrow keys, and verify selection and visible heading text.
* Assert the heading changes after selection and reflects pending state before
  save.
* After save, assert a polite status message contains `saved and confirmed`, the
  close button has an accessible name such as `Dismiss save confirmation`, and
  activating it removes only the confirmation.
* Assert `Application status: ...` remains visible after confirmation dismissal.
* Assert failed saves still use urgent error semantics and do not show a success
  confirmation.
* Assert preference controls and the save action retain their pending-save
  disabled behavior.
* Add browser-level checks at 320 CSS pixels and 400% zoom for wrapping,
  horizontal overflow, visible focus, and notification overlap.

## Alternatives and recommendation

### Control comparison

| Option                                  | Semantics in this repository                         | Visible alternatives             | Keyboard model                         | Main tradeoff                                                    |
|-----------------------------------------|------------------------------------------------------|----------------------------------|----------------------------------------|------------------------------------------------------------------|
| `RadzenSwitch` 10.3.2                   | Native checkbox with `role="switch"`                | Needs adjacent state text        | One tab stop; Space and Enter toggle    | Compact, but does not expose both choices visually by itself      |
| `RadzenSelectBar<bool>` 10.3.2           | Toolbar with `aria-pressed` toggle buttons            | Both labels remain visible       | Arrow focus; Space or Enter selects     | Good visual fit, but weak semantic fit for two exclusive choices  |
| Styled `InputRadioGroup<bool>`           | Native mutually exclusive radio group                | Both labels remain visible       | Native Tab, arrow, and Space behavior   | Needs local styling to appear segmented                           |
| Custom two-button segmented selector     | Must implement group, selection, and keyboard states | Both labels remain visible       | Must be implemented and maintained      | Avoid unless native radios cannot meet the visual requirement     |

Recommend a styled `InputRadioGroup<bool>` with `Enabled` mapped to `true` and
`Disabled` mapped to `false`. Place it in a `fieldset` whose `legend` is
`Trailing stops` so the visible group label is also programmatically associated.
Retain direct Boolean binding, and let the H1 or section heading display
`Trailing stops: Enabled` or `Trailing stops: Disabled` from the pending value.

Use `RadzenSwitch` only if a compact switch is a stronger product requirement
than showing both choices. In that variant, keep `Trailing stops` as the stable
label and render the current state as separate visible text. Do not change the
switch's label between `Enabled` and `Disabled`.

Do not adopt `RadzenSelectBar<bool>` 10.3.2 solely on the basis of current
Radzen documentation. Its installed markup differs from current upstream. It
can be defensible as two toggle buttons in a toolbar, but APG advises using a
toolbar for at least three controls, and users must infer mutual exclusion from
the visual treatment.

### Feedback comparison

| Option                                      | Announcement in 10.3.2      | Dismissal                         | Timeout                               | Main tradeoff                                                       |
|---------------------------------------------|------------------------------|-----------------------------------|---------------------------------------|---------------------------------------------------------------------|
| `RadzenNotification`                        | Polite live region           | Named `Close` button              | Defaults to 3000 ms when omitted      | Global popup, but easy to miss and can overlap content               |
| `RadzenAlert` 10.3.2                        | Polite live region           | Persistent and closable           | None                                  | Best layout behavior, but its icon-only close lacks a name in 10.3.2 |
| Custom inline `role="status"` status box   | Polite status semantics      | Explicitly named native button    | None                                  | Small local implementation with precise semantics                    |

Recommend a persistent inline success status box in normal document flow with
`role="status"`, `aria-atomic="true"`, and a native close button named
`Dismiss save confirmation`. It can use Radzen visual classes or equivalent
page styling, but its semantics and close name should be explicit and testable.
Dismissal should clear only the confirmation message and leave keyboard focus
in a predictable nearby location, normally on the save button or the next
logical control.

`RadzenAlert` becomes the preferred component-level implementation after a
Radzen upgrade that includes an accessible close label, provided the success
variant is kept polite rather than assertive. On 10.3.2, use it only if the
rendered close button can be given an accessible name without brittle DOM or
JavaScript manipulation.

Use `RadzenNotification` only when popup placement is an explicit interaction
requirement. Configure a deliberate, sufficiently long or effectively
persistent duration, retain another durable indication of the last saved state,
and verify the fixed notification does not obscure focus or fail reflow. Merely
omitting `Duration` is not sufficient.

## Evidence and URLs

### Repository evidence

* `src/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.csproj` pins
  `Radzen.Blazor` 10.3.2.
* `src/TNC.Trading.Platform.Web/PlatformWebUiServiceCollectionExtensions.cs`
  already registers Radzen services through `AddRadzenComponents()`.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor`
  binds the Boolean directly, uses a fixed heading, and combines save feedback
  with application status in one `role="status"` paragraph.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountPreferencesTests.cs`
  currently locates the checkbox by ID and checks the raw `checked` attribute.
  The candidate selector and dismissal behavior need semantic test coverage.

### Authoritative and primary sources

* [Microsoft Blazor input components](https://learn.microsoft.com/aspnet/core/blazor/forms/input-components) documents `InputRadioGroup<TValue>`, `InputRadio<TValue>`, and model binding.
* [Radzen Switch documentation](https://blazor.radzen.com/switch) describes Boolean binding and switch usage.
* [Radzen SelectBar documentation](https://blazor.radzen.com/selectbar) describes segmented selection, orientation, and Boolean examples.
* [Radzen Notification documentation](https://blazor.radzen.com/notification) documents notification severity, duration, and close behavior.
* [Radzen Alert API](https://blazor.radzen.com/docs/api/Radzen.Blazor.RadzenAlert.html) documents dismissal, visibility callbacks, and current close-label support.
* [Radzen 10.3.2 Switch source](https://github.com/radzenhq/radzen-blazor/blob/v10.3.2/Radzen.Blazor/RadzenSwitch.razor) establishes the installed HTML checkbox, switch role, and checked state.
* [Radzen 10.3.2 SelectBar source](https://github.com/radzenhq/radzen-blazor/blob/v10.3.2/Radzen.Blazor/RadzenSelectBar.razor) establishes the installed toolbar and pressed-button markup.
* [Radzen 10.3.2 SelectBar behavior](https://github.com/radzenhq/radzen-blazor/blob/v10.3.2/Radzen.Blazor/RadzenSelectBar.razor.cs) establishes Arrow, Home, End, Space, and Enter handling.
* [Radzen 10.3.2 Notification source](https://github.com/radzenhq/radzen-blazor/blob/v10.3.2/Radzen.Blazor/RadzenNotificationMessage.razor) establishes the polite live region, named close button, and 3000-millisecond fallback.
* [Radzen 10.3.2 Alert source](https://github.com/radzenhq/radzen-blazor/blob/v10.3.2/Radzen.Blazor/RadzenAlert.razor) establishes polite live-region output and the icon-only close button.
* [WAI-ARIA APG Switch Pattern](https://www.w3.org/WAI/ARIA/apg/patterns/switch/) defines switch naming, `aria-checked`, stable labels, and Space behavior.
* [WAI-ARIA APG Button Pattern](https://www.w3.org/WAI/ARIA/apg/patterns/button/) defines toggle-button `aria-pressed`, stable labels, and Space or Enter activation.
* [WAI-ARIA APG Toolbar Pattern](https://www.w3.org/WAI/ARIA/apg/patterns/toolbar/) defines toolbar grouping and arrow-key behavior and recommends toolbar grouping for three or more controls.
* [WAI-ARIA APG Radio Group Pattern](https://www.w3.org/WAI/ARIA/apg/patterns/radio/) defines group naming, `radio`, `aria-checked`, and radio keyboard behavior.
* [WCAG 2.2 Success Criterion 3.3.2](https://www.w3.org/WAI/WCAG22/Understanding/labels-or-instructions) requires labels for inputs and appropriate labels for every radio or checkbox option.
* [WCAG 2.2 Success Criterion 4.1.2](https://www.w3.org/WAI/WCAG22/Understanding/name-role-value) requires programmatically determinable names, roles, and values or states.
* [WCAG 2.2 Success Criterion 4.1.3](https://www.w3.org/WAI/WCAG22/Understanding/status-messages) requires status messages to be exposed without moving focus and identifies `role="status"` for success feedback.
* [WCAG 2.2 Success Criterion 2.2.1](https://www.w3.org/WAI/WCAG22/Understanding/timing-adjustable) explains that a timed toast needs an untimed alternative when it is the only way to discover the information.
* [WCAG 2.2 Success Criterion 1.4.10](https://www.w3.org/WAI/WCAG22/Understanding/reflow) requires content to work at a width equivalent to 320 CSS pixels without two-dimensional scrolling.
* [WCAG 2.2 Success Criterion 2.4.11](https://www.w3.org/WAI/WCAG22/Understanding/focus-not-obscured-minimum) requires author-created content not to obscure a focused component completely.

## Remaining gaps

* Component source establishes expected markup, but no screen reader and browser
  matrix has been run against the application. NVDA with Firefox or Chrome and
  Narrator with Edge should verify announced group names, states, save status,
  and dismissal.
* No rendered visual check has confirmed that native radios can match the
  intended segmented appearance within the existing theme.
* The exact focus destination after dismissing an inline confirmation needs a
  product decision. Avoid forcing focus movement unless dismissal removes the
  currently focused close button; then move focus to the save button or another
  logical nearby control.
* A package upgrade could change the recommendation. Current upstream Radzen
  renders improved SelectBar radio semantics and an explicitly named Alert close
  button, but the repository remains on 10.3.2.

## Clarifying questions

* Should the state-driven heading represent the editable pending selection or
  only the last state confirmed by the server?
* After a focused close button dismisses save confirmation, should focus return
  to `Save preference` or move to the durable application status?
* Is popup placement a product requirement, or is a boxed inline confirmation
  acceptable?

## Suggested next research

* [ ] Prototype the styled native Boolean radio group in an isolated component
      or design fixture and inspect its accessibility tree.
* [ ] Verify the selector, dynamic heading, status announcement, and dismissal
      with NVDA and Narrator in the supported browser matrix.
* [ ] Run 320 CSS pixel, 200% text resize, and 400% browser zoom checks against
      the full account-preferences page.
* [ ] Evaluate the latest Radzen package in a temporary branch to determine
      whether upgraded `RadzenSelectBar` and `RadzenAlert` semantics justify a
      dependency update.
* [ ] Confirm product wording for the group label, state-driven heading, save
      confirmation, and close-button accessible name with UX stakeholders.
