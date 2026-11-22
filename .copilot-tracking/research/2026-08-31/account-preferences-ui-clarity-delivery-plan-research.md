<!-- markdownlint-disable-file -->
# Task Research: Account Preferences UI Clarity Delivery Plan

Plan delivery of the selected account-preferences UI clarity approach documented in the existing research, while preserving the Boolean backend contract and authoritative provider-confirmation workflow.

## Task Implementation Requests

* Convert the selected research recommendation into an implementation-ready delivery sequence.
* Identify exact production and test changes, dependencies, risks, acceptance criteria, and validation gates.
* Keep the plan aligned with repository architecture, Blazor, Radzen, accessibility, and test conventions.
* Do not modify production or test code during planning.

## Scope and Success Criteria

* Scope: Account-preferences Web presentation files, presenter/view-model state handling, page-scoped styling, focused bUnit tests, documentation impact, and executable validation.
* Assumptions: The existing research recommendation remains authoritative unless current repository evidence conflicts; the API and application contracts remain unchanged; implementation will be performed in a later coding task.
* Success Criteria:
  * Define ordered, independently verifiable implementation phases.
  * Map every recommendation to exact files and observable acceptance criteria.
  * Identify the cheapest focused validation after each implementation phase.
  * Record alternatives, risks, rollback boundaries, and the selected delivery approach.

## Outline

1. Verify repository planning conventions and current owning code.
2. Confirm the smallest coherent implementation boundary.
3. Define phased production and test edits with acceptance criteria.
4. Evaluate delivery-order alternatives and select one approach.
5. Record commands, manual checks, risks, and implementation handoff.

## Potential Next Research

* Render the segmented selector at desktop width, 320 CSS pixels, and 400 percent zoom after implementation.
  * Reasoning: Source analysis cannot prove reflow, focus visibility, or theme integration.
  * Reference: src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor.css
* Verify focus restoration and announcements with NVDA and Narrator after implementation.
  * Reasoning: bUnit can prove markup and state transitions but not the complete browser and assistive-technology interaction.
  * Reference: src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:31-45
* Design a deterministic AppHost account-preferences provider fixture before adding Playwright CI coverage.
  * Reasoning: Browser request interception cannot replace the server-side API/provider path, and CI must not depend on live IG credentials.
  * Reference: test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/TNC.Trading.Platform.Web.FunctionalTests.csproj:1-31

## Research Executed

### File Analysis

* .copilot-tracking/research/2026-08-31/account-preferences-ui-clarity-research.md
  * Selects native Boolean radios styled as segments, a pending/confirmed state distinction, a state-driven heading, and a persistent dismissible inline status box.
* src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:1-45
  * Owns the route, authorization, requested text, Boolean control, feedback markup, load/save event forwarding, and component disposal.
* src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPageViewModel.cs:3-25
  * Stores one editable Boolean and cannot distinguish an unconfirmed initial value, pending selection, and last confirmed state.
* src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPagePresenter.cs:3-53
  * Owns load, save, feedback, and history transitions. Update and history refresh currently share one `try`, so a confirmed update followed by history failure is misclassified as a save failure.
* src/TNC.Trading.Platform.Web/PlatformApiClient.cs:139-166,183-185
  * Already reads and writes a non-nullable Boolean and provides the authoritative response needed by the UI. No client-contract change is required.
* src/TNC.Trading.Platform.Application/Features/AccountPreferences/UpdateAccountPreferencesHandler.cs:13-60
  * Already performs one PUT followed by authoritative GET and rejects a known mismatch.
* test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountPreferencesTests.cs:10-172
  * Contains 10 passing checkbox-oriented tests but lacks pending/confirmed, stale-message, dismissal, outbound-Boolean, partial-success, and safe-load-failure coverage.
* docs/wiki/operator-guide.md:565-580
  * Must be updated because it describes a Test-account toggle and does not cover pending, confirmed, dismissal, or partial-success behavior.
* docs/wiki/testing-and-quality.md:553-566
  * Must be updated to describe the expanded deterministic bUnit coverage.

### Code Search Results

* `AccountPreferences.razor.css`
  * No scoped stylesheet currently exists for this page. It will be the fourth runtime presentation file and should follow the local CSS pattern in `AccountDetails.razor.css`.
* `TrailingStopsEnabled`
  * The value remains Boolean through Web, API, application, gateway, and observed persistence models. No API, application, database, or configuration change is justified.
* Account-preferences browser tests
  * No functional or end-to-end account-preferences test exists. A deterministic provider fixture is required before reliable Playwright coverage can be added.

### External Research

* Existing research records Microsoft Learn, WAI-ARIA, WCAG 2.2, and Radzen 10.3.2 evidence.
  * Native Boolean radios remain the selected control because they expose both values and supply the correct group semantics and keyboard behavior.
  * A persistent inline `role="status"` box remains the selected confirmation because it avoids timeout and fixed-position risks while allowing explicit dismissal.

### Project Conventions

* Standards referenced: .github/copilot-instructions.md; .github/AGENTS.md; .github/lessons.md; architecture, C#, Radzen Blazor, tests, Playwright, refactoring, documentation, work-package, folder, and .NET stack instructions.
* Instructions applied:
  * Keep presentation state in the Web adapter and preserve application/provider confirmation policy.
  * Use built-in Blazor and semantic HTML where these are simpler and more accessible than Radzen components.
  * Name tests using `MethodName_StateUnderTest_ExpectedResult` and place a traceability comment immediately above every added or changed test.
  * Validate after each small behavior edit and update affected wiki pages before implementation is complete.
  * Do not create or reopen a numbered work package for this request.

## Key Discoveries

### Project Structure

The verified runtime boundary is exactly four Web presentation files: the view model, presenter, Razor component, and a new page-scoped stylesheet. Focused automated changes remain in the existing account-preferences bUnit test file. Repository completion additionally requires two existing wiki pages.

No package, project, API, application, infrastructure, persistence, configuration, schema, or database change is needed. The current API maps authoritative mismatch to HTTP 409, and the Web client intentionally converts non-success responses to `HttpRequestException`. Rich mismatch wording would require a wider client error contract and is excluded.

### Implementation Patterns

* Treat the editable Boolean as pending selection and retain a separate nullable confirmed value.
* Do not render an editor, state heading, or Save action until a live value has been confirmed. This prevents the default `false` from appearing actionable after initial-load failure.
* Clear prior save success and save error when selection changes.
* Apply the authoritative update response to both pending and confirmed values.
* Separate confirmed update from the secondary history refresh so history failure produces qualified success rather than generic save failure.
* Keep application status and observed history independent from transient save confirmation.
* Preserve explicit Save and unchanged-save behavior because repeated confirmed writes currently create valid observations.
* Restore focus to Save after dismissal because the activated dismiss control is removed from the DOM.

### Complete Examples

```csharp
internal sealed class AccountPreferencesPageViewModel
{
  public bool TrailingStopsEnabled { get; private set; }
  public bool? ConfirmedTrailingStopsEnabled { get; private set; }
  public bool HasConfirmedValue => ConfirmedTrailingStopsEnabled.HasValue;
  public bool HasPendingChange => ConfirmedTrailingStopsEnabled is bool confirmed
    && TrailingStopsEnabled != confirmed;

  public void SelectTrailingStops(bool enabled) =>
    TrailingStopsEnabled = enabled;

  public void Apply(AccountPreferencesViewModel preferences)
  {
    TrailingStopsEnabled = preferences.TrailingStopsEnabled;
    ConfirmedTrailingStopsEnabled = preferences.TrailingStopsEnabled;
    ApplicationStatus = preferences.ApplicationStatus;
    ObservedAtUtc = preferences.ObservedAtUtc;
  }
}
```

```razor
@if (Presenter.State.HasConfirmedValue)
{
  <h2>@(Presenter.State.TrailingStopsEnabled
    ? "Trailing stops enabled"
    : "Trailing stops disabled")</h2>

  <fieldset disabled="@Presenter.IsSaving">
    <legend>Trailing stops</legend>
    <InputRadioGroup TValue="bool"
             Value="Presenter.State.TrailingStopsEnabled"
             ValueChanged="SelectionChanged"
             ValueExpression="@(() => Presenter.State.TrailingStopsEnabled)">
      <label><InputRadio TValue="bool" Value="true" /> Enabled</label>
      <label><InputRadio TValue="bool" Value="false" /> Disabled</label>
    </InputRadioGroup>
  </fieldset>
}
```

### API and Schema Documentation

No API or schema change is required. The existing PUT/readback flow remains the authority for confirmation, and the existing Boolean request and response records support both explicit choices.

### Configuration Examples

No configuration change is expected.

## Technical Scenarios

### Delivery Sequencing

Three delivery orders were evaluated.

**Preferred Approach:** Add focused failing behavior tests, implement the smallest state slice, and rerun the same filtered class before proceeding. Deliver view-model state first, presenter transitions second, component semantics third, and styling last.

This order gives the fastest falsification of the risky state transitions and ensures the visual wording is backed by an executable pending/confirmed contract.

#### Considered Alternatives

* Presentation first: visually fast but allows the heading and selector to imply provider authority before the state model is correct. Rejected because appearance would lead behavior.
* Production state first with tests last: keeps production edits compilable but allows stale-message, partial-success, and failed-load regressions to survive until late in the change. Rejected because validation lags the riskiest decisions.
* Focused test-first vertical slices: selected because each transition is executable before dependent markup is introduced, each rollback boundary is small, and it matches repository test and refactoring conventions.

## Selected Approach

Deliver focused test-first vertical slices in this order:

1. Establish the current focused baseline.
2. Specify and implement explicit pending and confirmed state.
3. Specify and implement presenter selection, dismissal, failure, and partial-success transitions.
4. Specify and implement semantic radio markup, requested copy, feedback separation, and focus restoration.
5. Add page-scoped responsive and focus styling.
6. Update the operator and testing wiki pages, then run broader validation.

The approach keeps runtime work inside the verified four-file Web boundary, uses the existing Boolean and authoritative provider-confirmation contracts, and ensures every behavior phase has a cheap executable check before the next dependency is introduced.

## Implementation Plan

### Phase 0: Establish Baseline

Files changed: none.

Actions:

1. Run the focused account-preferences tests.
2. Record unrelated failures without repairing them in this slice.

Acceptance criteria:

* Tests run without Docker, SQL Server, or live IG credentials.
* The starting result is known. The verified research baseline is 10 passed and 0 failed.

Validation:

```powershell
dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj --filter "FullyQualifiedName~AccountPreferencesTests"
```

Rollback boundary: none; this phase is read-only.

### Phase 1: Pending and Confirmed State

Files:

* test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountPreferencesTests.cs
* src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPageViewModel.cs

Actions:

1. Add tests for no confirmed initial value, authoritative apply, pending selection, dirty comparison, and later authoritative reset.
2. Run the filtered class and confirm the new tests fail for the absent state contract.
3. Add nullable confirmed state (or an equivalent explicit confirmation flag), `HasConfirmedValue`, `HasPendingChange`, and a pending selection method.
4. Make `Apply` set pending, confirmed, application status, and observed time together.
5. Rerun the same filtered class immediately.

Acceptance criteria:

* Before a successful live GET, no Boolean is represented as confirmed.
* Selection changes pending state only.
* Dirty state is true exactly when pending differs from confirmed.
* Applying an authoritative response makes pending and confirmed values match server truth.

Test requirements:

* Use names such as `SelectTrailingStops_ShouldRetainConfirmedValue_WhenSelectionChanges`.
* Add an immediately preceding traceability comment to every changed or added test, citing the source task and relevant `NF2`, `NF3`, or `NF7` requirement where applicable.

Rollback boundary: revert the view-model contract and its direct state tests. No component or transport change depends on it yet.

### Phase 2: Presenter Transitions

Files:

* test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountPreferencesTests.cs
* src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPagePresenter.cs

Actions:

1. Add tests for selection after success, update rejection, HTTP 409 mismatch, controlled differing HTTP 200 response, confirmed update plus history failure, cancellation, and dismissal.
2. Run the focused class and verify failures identify the missing transition behavior.
3. Add a selection method that changes pending state and clears stale save message and save error.
4. Add dismissal that clears only transient confirmation.
5. Split update confirmation from history refresh. Apply authoritative success first, then catch only expected history failures and convert them to qualified success.
6. Preserve owned cancellation handling and the `finally` reset of `IsSaving`.
7. Rerun the same filtered class immediately.

Acceptance criteria:

* New selection clears stale confirmation and prior save error.
* Update rejection or HTTP 409 shows only `Unable to save and confirm account preferences.` and preserves prior confirmed state.
* HTTP 200 applies the returned Boolean as authoritative, even in a controlled test where it differs from pending selection.
* Confirmed update plus history failure shows only `Trailing stops preference saved and confirmed. Observed history could not be refreshed.`
* Dismissal preserves application status and history.
* `IsSaving` returns to false after success, expected failure, and owned cancellation.

Rollback boundary: revert presenter transition methods and their tests while retaining the independently valid view-model contract.

### Phase 3: Component Semantics and Copy

Files:

* test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountPreferencesTests.cs
* src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor

Actions:

1. Replace checkbox-oriented assertions with semantic checks for both radio labels, selected state, exact heading, last-confirmed state, pending cue, application status, confirmation, dismissal, and safe load failure.
2. Assert serialized outbound JSON for both `true` and `false` selections and disabled fieldset/Save behavior during an in-flight update.
3. Run the focused class and verify failures correspond to the old markup.
4. Replace the introduction with exactly `Live trailing stops control for the configured account.`
5. Render the exact state-driven H2 from pending selection.
6. Replace `InputCheckbox` with `InputRadioGroup<bool>` inside a `fieldset` with legend `Trailing stops`; map `Enabled` to `true` and `Disabled` to `false`.
7. Render `Last confirmed: Enabled/Disabled` separately and a non-live pending cue only when values differ.
8. Suppress the editor, state heading, and Save action until confirmed state exists.
9. Keep explicit Save and unchanged-save behavior.
10. Separate persistent application status from a conditional, atomic `role="status"` confirmation box with a button named `Dismiss save confirmation`.
11. Add a Save `ElementReference`; dismissal clears confirmation and restores focus to Save.
12. Rerun the focused class immediately.

Acceptance criteria:

* Both choices are visible and exactly one is selected after confirmed load.
* The heading immediately uses `Trailing stops enabled` or `Trailing stops disabled` from pending selection.
* Last-confirmed text changes only after authoritative success.
* Save emits the selected Boolean without transport changes.
* Selector and Save are disabled while saving.
* Initial load failure shows urgent feedback but no editable default `false` state.
* Confirmation persists until dismissed or invalidated by selection and never removes application status.
* Browser dismissal returns focus to Save.

Rollback boundary: revert Razor and component assertions together. Presenter and view-model behavior remain independently valid.

### Phase 4: Scoped Styling

File:

* New src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor.css

Actions:

1. Add two equal flexible selector tracks with connected option styling and selected state.
2. Add visible focus treatment using existing platform CSS variables.
3. Give each option at least a 44 by 44 CSS pixel target and permit text wrapping.
4. Use flexible confirmation layout and stack controls at a narrow breakpoint when needed.
5. Avoid global selectors, generated Radzen classes, negative letter spacing, viewport-scaled fonts, and fixed widths.
6. Run the focused tests and Web build immediately, then perform browser reflow checks.

Acceptance criteria:

* No clipping, overlap, or horizontal document scrolling at 320 CSS pixels or 400 percent zoom.
* Focus and selection are distinguishable without color alone.
* Dynamic feedback does not destabilize selector dimensions.

Rollback boundary: delete the scoped stylesheet. Native radio semantics remain usable without segmented styling.

### Phase 5: Wiki and Completion Gates

Files:

* docs/wiki/operator-guide.md
* docs/wiki/testing-and-quality.md

Actions:

1. Update the operator guide with explicit Enabled/Disabled selection, pending and last-confirmed distinction, save/dismiss workflow, safe failed-load behavior, and qualified history-refresh feedback.
2. Update testing guidance with radio semantics, state transitions, outbound Boolean, failure, partial-success, dismissal, and load-safety coverage.
3. Retain the documented boundary that default automation does not contact real IG.
4. Check existing links and headings, then run broader Web and solution gates.

Acceptance criteria:

* Documentation describes delivered behavior and no longer calls the control an ambiguous toggle.
* No completed work-package plan is reopened and no new numbered work package is created.
* Existing API, runtime, architecture, application-overview, and local-development pages remain unchanged because their contracts remain accurate.

Rollback boundary: revert the two wiki sections if implementation is backed out.

## Edge Cases and Decisions

* Initial current-preference load failure: show urgent error and no editor or Save action.
* Initial history failure after preference load: the confirmed editor may render because a confirmed value exists, while the existing aggregate load error remains. Separating this warning is outside scope.
* Selection after success: clear transient success and prior save error, retain application status and history.
* Update failure or authoritative mismatch: preserve prior confirmed state and show generic urgent failure only.
* Differing controlled HTTP 200 response: treat returned value as authoritative and reset pending and confirmed state to it.
* History failure after confirmation: retain authoritative state and show one qualified polite message.
* Unchanged save: keep available because equal confirmed writes create valid observations and no product requirement changes this behavior.
* Dismissal: remove only transient confirmation and restore focus to Save.
* Keyboard behavior: rely on native radio Tab, Arrow, and Space interaction inside a named fieldset.
* Browser automation: defer until a deterministic server-side provider fixture exists.

## Validation Strategy

Run from the repository root.

After every behavior edit:

```powershell
dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj --filter "FullyQualifiedName~AccountPreferencesTests"
```

After component and styling completion:

```powershell
dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj
dotnet build src/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.csproj
dotnet format src/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.csproj --verify-no-changes --no-restore
dotnet format test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj --verify-no-changes --no-restore
```

Final solution gates:

```powershell
dotnet build TNC.Trading.Platform.slnx
dotnet test TNC.Trading.Platform.slnx -m:1
```

Manual browser and accessibility validation:

1. Start the supported distributed application with `dotnet run --project src/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.csproj` and use the Aspire-assigned Web URL.
2. Confirm loaded `true` and `false` states expose both choices and select exactly one.
3. Operate the group with Tab, Arrow keys, and Space; confirm visible focus, pending heading, unchanged last-confirmed text, and pending cue.
4. Save and confirm controls disable in flight, success is announced without moving focus, and application status remains visible.
5. Dismiss confirmation and confirm focus returns to Save.
6. Exercise a controlled provider failure and confirm urgent failure without false success.
7. Repeat at 320 CSS pixels and 400 percent zoom; confirm no overflow, clipping, overlap, or obscured focus.
8. Verify group naming, checked state, announcements, dismissal name, and focus restoration with NVDA plus Firefox or Chrome and Narrator plus Edge.

Do not use live credentials in test data, screenshots, or artifacts. Do not add a live-IG-dependent Playwright test.

## Evidence Documents

* .copilot-tracking/research/2026-08-31/account-preferences-ui-clarity-research.md
* .copilot-tracking/research/subagents/2026-08-31/account-preferences-ui-clarity-delivery-plan-research.md
* .copilot-tracking/research/subagents/2026-08-31/account-preferences-repository-research.md
* .copilot-tracking/research/subagents/2026-08-31/account-preferences-accessibility-research.md
* .copilot-tracking/research/subagents/2026-08-31/account-preferences-alternatives-research.md
