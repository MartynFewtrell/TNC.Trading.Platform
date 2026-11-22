<!-- markdownlint-disable-file -->
# Task Research: Account Preferences UI Clarity

Research improvements to the account-preferences interface so its wording, preference control, state feedback, and save confirmation communicate the trailing-stops setting clearly.

## Task Implementation Requests

* Replace the account-specific introductory text with "Live trailing stops control for the configured account."
* Replace the checkbox with a control that explicitly displays enabled and disabled states while retaining the Boolean backend contract.
* Make the subheading state-driven: "Trailing stops enabled" or "Trailing stops disabled".
* Replace the persistent save confirmation with a dismissible popup or boxed notification.
* Recommend one evidence-based implementation approach without changing production code.

## Scope and Success Criteria

* Scope: The existing account-preferences Blazor UI, its immediate state/update path, relevant UI tests, established Radzen patterns, accessibility considerations, and focused implementation/validation steps.
* Assumptions: The backend preference remains Boolean; the requested introductory sentence should include normal terminal punctuation; research does not modify production or test files.
* Success Criteria:
  * Identify the exact owning files and current behavior with line references.
  * Evaluate viable explicit Boolean controls and confirmation patterns.
  * Select one approach aligned with repository conventions and accessibility requirements.
  * Provide implementation-ready details, examples, risks, and focused validation steps.

## Outline

1. Trace the account-preferences component, Boolean state, update confirmation, and tests.
2. Inventory established Radzen toggle and notification patterns in the repository.
3. Verify relevant accessibility and framework guidance.
4. Evaluate alternatives and select one implementation approach.
5. Record concrete file changes and validation scenarios.

## Potential Next Research

* Render and assistive-technology validation of the selected control and confirmation box.
  * Reasoning: Source and standards analysis establishes the intended semantics, but browser layout, focus, and announcements need verification in the running application.
  * Reference: src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:9-25
* Deterministic account-preferences provider fixture for browser automation.
  * Reasoning: The current Blazor Server browser harness cannot replace server-side API traffic through browser request interception, and CI must not depend on live IG credentials.
  * Reference: test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/TNC.Trading.Platform.Web.FunctionalTests.csproj:1-31

## Research Executed

### File Analysis

* src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:9-25
  * Owns all requested copy and control changes. It currently renders the Test-specific introduction, fixed checkbox label, `InputCheckbox`, save action, inline error, and an always-present status paragraph.
* src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPagePresenter.cs:5-50
  * Owns loading, saving, confirmation, error state, and history refresh. Confirmation persists until the next save or page disposal and is not cleared by an edited selection.
* src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPageViewModel.cs:3-25
  * Owns the editable Boolean. It does not currently distinguish pending selection from the last server-confirmed value.
* src/TNC.Trading.Platform.Web/PlatformApiClient.cs:139-156,183-185
  * The Web contract already reads and writes `bool TrailingStopsEnabled`; no transport change is required.
* src/TNC.Trading.Platform.Application/Features/AccountPreferences/UpdateAccountPreferencesHandler.cs:13-60
  * PUT is followed by an authoritative GET. Only a matching observed value is returned as confirmed success; mismatch becomes `NotApplied`.
* test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountPreferencesTests.cs:14-172
  * Existing bUnit coverage is checkbox-oriented and lacks semantic selection, dismissal, stale-message, outbound-Boolean, and history-refresh partial-success cases.

### Code Search Results

* `RadzenSwitch|RadzenSelectBar|RadzenRadioButtonList|RadzenNotification|RadzenAlert`
  * No established use exists in source-controlled Web code. The implementation must be selected from installed-version behavior rather than a local component precedent.
* `role="status"|role="alert"`
  * Account preferences, configuration, and status pages use page-local semantic feedback. This supports an inline status box over introducing a global toast convention.
* `TrailingStopsEnabled`
  * The value remains Boolean through Web, application, provider gateway, and persistence observation models. The UI can map explicit labels to `true` and `false` without backend changes.

### External Research

* Microsoft Learn: `InputRadioGroup<TValue>` and `InputRadio<TValue>`
  * Built-in Blazor controls support direct Boolean binding and native radio behavior.
  * Source: [ASP.NET Core Blazor input components](https://learn.microsoft.com/aspnet/core/blazor/forms/input-components)
* Radzen 10.3.2 source: switch, select bar, notification, and alert
  * `RadzenSwitch` has valid switch semantics but does not display both alternatives. Installed `RadzenSelectBar` uses toolbar/toggle-button semantics. `RadzenNotification` defaults to 3000 ms. Installed `RadzenAlert` has an icon-only close button without an explicit accessible name.
  * Sources: [Switch](https://github.com/radzenhq/radzen-blazor/blob/v10.3.2/Radzen.Blazor/RadzenSwitch.razor), [SelectBar](https://github.com/radzenhq/radzen-blazor/blob/v10.3.2/Radzen.Blazor/RadzenSelectBar.razor), [Notification](https://github.com/radzenhq/radzen-blazor/blob/v10.3.2/Radzen.Blazor/RadzenNotificationMessage.razor), [Alert](https://github.com/radzenhq/radzen-blazor/blob/v10.3.2/Radzen.Blazor/RadzenAlert.razor)
* WAI-ARIA Authoring Practices
  * Native radio-group semantics directly represent one selection from two named choices. A switch requires a stable label and communicates only its checked state. Toolbars are recommended for groups of three or more controls.
  * Sources: [Radio Group Pattern](https://www.w3.org/WAI/ARIA/apg/patterns/radio/), [Switch Pattern](https://www.w3.org/WAI/ARIA/apg/patterns/switch/), [Toolbar Pattern](https://www.w3.org/WAI/ARIA/apg/patterns/toolbar/)
* WCAG 2.2 status, timing, reflow, and focus guidance
  * Success feedback should be exposed without moving focus. The only confirmation should not disappear after a short timeout. Normal-flow content is easier to reflow and less likely to obscure focus than a fixed popup.
  * Sources: [Status Messages](https://www.w3.org/WAI/WCAG22/Understanding/status-messages), [Timing Adjustable](https://www.w3.org/WAI/WCAG22/Understanding/timing-adjustable), [Reflow](https://www.w3.org/WAI/WCAG22/Understanding/reflow), [Focus Not Obscured](https://www.w3.org/WAI/WCAG22/Understanding/focus-not-obscured-minimum)

### Project Conventions

* Standards referenced: .github/copilot-instructions.md; .github/AGENTS.md; .github/lessons.md; .github/instructions/radzen-blazor.instructions.md; .github/instructions/tests.instructions.md; .github/instructions/playwright.instructions.md; .github/instructions/architecture.instructions.md; .github/instructions/csharp.instructions.md; .github/instructions/folders.instructions.md.
* Instructions followed: Research-only changes are confined to .copilot-tracking/research/. The recommendation keeps presentation concerns in the Web adapter and tests observable behavior rather than CSS internals.

## Key Discoveries

### Project Structure

The owning page delegates asynchronous behavior to a scoped presenter and mutable page view model. The API endpoint and application handler already implement the safety-critical provider confirmation workflow. The requested change is therefore a Web presentation slice, not a domain or API change.

Radzen Blazor 10.3.2 is installed and services are registered, but the repository has no existing switch, select bar, notification, dialog, or alert convention. Existing feedback is inline and page-local.

### Implementation Patterns

* Keep the explicit `Save preference` command. Selecting an option changes pending UI state only; persistence still requires PUT plus authoritative GET.
* Track both editable `TrailingStopsEnabled` and `ConfirmedTrailingStopsEnabled` in the view model. `HasPendingChange` is their comparison.
* Clear stale success and prior save errors whenever the operator selects a different value.
* Separate durable `Application status` data from transient save confirmation so dismissing confirmation does not remove domain information.
* Treat a history-refresh failure after confirmed update as qualified success, not as a failed save.

### Complete Examples

```razor
<h2>@(Presenter.State.TrailingStopsEnabled
  ? "Trailing stops enabled"
  : "Trailing stops disabled")</h2>

<fieldset class="trailing-stops-choice" disabled="@Presenter.IsSaving">
  <legend>Trailing stops</legend>
  <InputRadioGroup TValue="bool"
           Value="Presenter.State.TrailingStopsEnabled"
           ValueChanged="SelectionChanged"
           ValueExpression="@(() => Presenter.State.TrailingStopsEnabled)">
    <label><InputRadio TValue="bool" Value="true" /> Enabled</label>
    <label><InputRadio TValue="bool" Value="false" /> Disabled</label>
  </InputRadioGroup>
</fieldset>

<p>Last confirmed: @(Presenter.State.ConfirmedTrailingStopsEnabled ? "Enabled" : "Disabled")</p>

@if (!string.IsNullOrWhiteSpace(Presenter.Message))
{
  <div role="status" aria-atomic="true" class="account-preferences-confirmation">
    <span>@Presenter.Message</span>
    <button type="button"
        aria-label="Dismiss save confirmation"
        @onclick="DismissMessageAsync">Dismiss</button>
  </div>
}
```

The final subheading deliberately uses the exact requested wording. `Last confirmed` and the save confirmation distinguish pending selection from authoritative provider state without weakening that copy.

### API and Schema Documentation

No API or schema change is recommended. The Web transport, application model, nullable update request validation, provider gateway, and audit history already support explicit `true` and `false`. The existing PUT/readback flow remains the authority for confirmation.

### Configuration Examples

No configuration change is required.

## Technical Scenarios

### Explicit Boolean Preference Control

**Requirements:** Both `Enabled` and `Disabled` must be visible, one value must map directly to the existing Boolean, and keyboard and assistive-technology behavior must be unambiguous.

**Preferred Approach:** Use a styled `InputRadioGroup<bool>` in a `fieldset` with legend `Trailing stops`. Map `Enabled` to `true` and `Disabled` to `false`. Style the labels as connected segments while preserving native inputs, focus indication, and radio keyboard behavior.

#### Considered Alternatives

* `RadzenSwitch` 10.3.2: valid and compact, but does not expose both named choices without extra explanatory UI. Rejected because the request prioritizes explicit enabled/disabled choices.
* `RadzenSelectBar<bool>` 10.3.2: visually suitable, but the installed version renders a toolbar with pressed buttons rather than a radio group. Rejected because native radios model two mutually exclusive values more directly.
* Custom toggle buttons: would require hand-implemented role, state, and keyboard behavior. Rejected because built-in radios already solve those concerns.

### State-Driven Heading

**Requirements:** Render exactly `Trailing stops enabled` or `Trailing stops disabled` from the current selected Boolean.

**Preferred Approach:** Derive the subheading from the editable pending value so selection produces immediate visible feedback. Render `Last confirmed: Enabled/Disabled` separately and show a concise unsaved cue when pending and confirmed values differ. The radio's checked state provides the accessibility update; the heading should not be another live region.

#### Considered Alternatives

* Heading from confirmed state only: rejected because it stays unchanged when the operator selects another value and makes the control feel unresponsive.
* Heading from pending state without confirmed-state context: rejected because it can imply provider confirmation before Save completes.

### Dismissible Save Confirmation

**Requirements:** Confirmation remains available until acknowledged, can be dismissed, is announced without taking focus, and does not permanently occupy the page after dismissal.

**Preferred Approach:** Render a conditional inline success box in normal document flow with `role="status"`, `aria-atomic="true"`, and a visible button whose accessible name is `Dismiss save confirmation`. Dismissal clears only the confirmation and restores focus to `Save preference` because the activated close control is removed from the DOM. Keep `Application status` in a separate persistent element.

#### Considered Alternatives

* `RadzenNotification`: rejected because version 10.3.2 defaults to a three-second timeout and fixed popup placement risks being missed or obscuring narrow content.
* `RadzenAlert` 10.3.2: rejected because its icon-only close control lacks an explicit accessible name in the installed version.
* Modal dialog: rejected because routine non-blocking success does not justify interrupting workflow or moving initial focus.

## Selected Approach

Implement a segmented visual selector backed by native `InputRadioGroup<bool>`, render the exact state-driven subheading from pending selection, display last-confirmed state separately, and show successful save feedback in a persistent dismissible inline status box.

This approach is selected because it makes both Boolean values explicit, preserves the current backend and explicit-save workflow, uses native semantics compatible with the installed stack, follows the repository's inline feedback convention, and avoids the timeout and accessibility shortcomings of the available Radzen feedback components.

The presenter flow should be corrected in the same UI slice:

1. Selection updates pending state and clears stale save feedback.
2. Save clears feedback and calls the existing update API.
3. Confirmed response updates both pending and confirmed values, then shows success.
4. History refresh runs as a secondary operation.
5. If only history refresh fails, show `Trailing stops preference saved and confirmed. Observed history could not be refreshed.` as one polite status; do not also show the generic save error.
6. Dismissal clears only the status message and returns focus to the save action.

## Implementation Impact

Production changes should remain confined to four Web presentation files:

* src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:9-25,37-39
  * Replace the Test-specific introduction with `Live trailing stops control for the configured account.`
  * Add the dynamic requested subheading, explicit Boolean radio selector, last-confirmed/pending context, separate application status, and dismissible confirmation.
  * Wire selection and dismissal handlers and save-button focus restoration.
* src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPagePresenter.cs:5-50
  * Add selection and dismissal methods; clear stale feedback; separate confirmed save from secondary history-refresh failure.
* src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPageViewModel.cs:3-19
  * Track confirmed and pending Boolean values and derive dirty state.
* src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor.css:1-end
  * Add page-scoped segmented-control, focus, confirmation, and narrow-layout styles.

Focused tests remain in test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountPreferencesTests.cs:14-172. No client, API, application, provider, persistence, database, or configuration files need changes.

Responsive styling should use two flexible equal segments at ordinary widths and stack at narrow widths. Controls need visible focus, wrapping labels, stable dimensions, and at least 44 by 44 CSS pixel targets. The page must avoid horizontal overflow at 320 CSS pixels and 400% zoom.

## Validation Strategy

Extend bUnit coverage to verify:

* Loaded `true` and `false` select exactly one matching radio while both options remain visible.
* Selection changes the exact enabled/disabled subheading, preserves last-confirmed text, and shows pending context.
* Save sends the selected Boolean, applies the authoritative result, and displays one polite confirmation.
* Editing after success clears stale confirmation.
* Dismissal removes only confirmation and leaves application status visible.
* Update failure displays only the urgent error.
* Confirmed update followed by history-refresh failure displays only qualified success.
* The selector and save action are disabled while saving.

Run:

```powershell
dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj --filter "FullyQualifiedName~AccountPreferencesTests"
```

Then perform authenticated browser validation with a configured IG Test account: keyboard radio operation, visible focus, polite status announcement, named dismissal and focus restoration, 320 CSS pixel reflow, 400% zoom, NVDA with Firefox or Chrome, and Narrator with Edge. Do not add a live-IG-dependent Playwright CI test; first establish a deterministic provider fixture as separate infrastructure work.

## Evidence Documents

* .copilot-tracking/research/subagents/2026-08-31/account-preferences-repository-research.md
* .copilot-tracking/research/subagents/2026-08-31/account-preferences-accessibility-research.md
* .copilot-tracking/research/subagents/2026-08-31/account-preferences-alternatives-research.md
