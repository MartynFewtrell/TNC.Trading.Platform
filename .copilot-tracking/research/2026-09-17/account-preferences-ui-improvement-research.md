<!-- markdownlint-disable-file -->
# Task Research: Account Preferences UI Improvement

This research evaluates a clearer, more professional Account Preferences presentation while preserving the current account-preference API, persistence, and cursor-paging contracts.

## Task Implementation Requests

* Make Account Preferences behavior and hierarchy clearer.
* Correct inconsistent label emphasis.
* Elevate account-mismatch warnings.
* Align actions with established application button patterns.
* Reduce the visual dominance of trailing-stop controls.
* Make observed history easier to access, using tabs if appropriate.

## Scope and Success Criteria

* Scope: The Blazor Account Preferences view, scoped CSS, the Account Details tab pattern, and relevant UI tests.
* Exclusions: API, application, presenter, persistence, history cursor, and domain behavior changes.
* Assumption: One-way history traversal through an opaque next cursor remains intended.
* Success criteria:
  * Separate safety-sensitive settings from diagnostic history.
  * Put warnings and status feedback with the workflow they qualify.
  * Keep controls accessible and consistent with repository conventions.
  * Protect the new information architecture with focused bUnit and Playwright coverage.

## Research Executed

### File Analysis

* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor`
  * Lines 18-55 render state, editor, and observed history as one vertical sequence.
  * Lines 28-29, 44, 49, and 55 place commands as ungrouped sibling buttons.
  * Line 54 displays `RecordedAtUtc`, though server ordering is by observation time.
  * The page does not render the presenter's populated `SaveError`.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor.css`
  * Lines 12-35 implement radio choices as a full-width two-column grid.
  * Lines 19-46 make selected radio labels more prominent than unselected labels, while state labels are separately bolded.
  * Lines 62-91 set control height but provide no action-group spacing or hierarchy.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPageViewModel.cs`
  * Lines 3-47 distinguish pending operator selection from confirmed provider observation; `HasPendingChange` remains the dirty-state source.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPagePresenter.cs`
  * Lines 21-48 load current state before history and maintain separate current/history errors.
  * Lines 50-82 populate `SaveError`; lines 120-124 retain server-returned history order.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountDetails/AccountDetails.razor`
  * Lines 27-62 establish page-local `RadzenTabs` with an accessible label, selected-index retention, and keyed panels.
  * Lines 65-68 establish the repository's labelled `<nav>` pager pattern.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfTrailingStopsPreferenceObservationStore.cs`
  * Lines 27-63 define newest-observed-first ordering (`ObservedAtUtc`, then ID) and keyset paging.

### Project Conventions

* `.github/instructions/radzen-blazor.instructions.md` lines 24-45 selects free Radzen controls as the default shared UI library, requiring meaningful labels, keyboard navigation, visible validation feedback, semantic headings, and focused interaction tests.
* `.github/instructions/tests.instructions.md` lines 43-61 assigns bUnit to rendering/lifecycle coverage and Playwright to browser-only behavior.
* `.github/instructions/playwright.instructions.md` lines 20-38 requires accessibility-first locators and web-first assertions.

### External Research

* [Radzen Tabs API](https://blazor.radzen.com/docs/api/Radzen.Blazor.RadzenTabs.html): `AriaLabel` and `AriaLabelledBy` are supported; Arrow, Home, End, Space, and Enter are documented keyboard operations.
* [Radzen Tabs](https://blazor.radzen.com/tabs): Default server rendering matches Account Details and excludes the inactive history panel from initial output.
* [Blazor input components](https://learn.microsoft.com/aspnet/core/blazor/forms/input-components?view=aspnetcore-10.0): The existing native `InputRadioGroup` is appropriate; replacement is unnecessary for a layout issue.
* [Radzen Accordion](https://blazor.radzen.com/accordion): Requires meaningful expand/collapse labels and adds disclosure state, which is not warranted for two workflows.

## Key Discoveries

| Requested area | Verified issue | Recommendation |
| --- | --- | --- |
| Label weight | State labels use `strong`; checked radios add bold/underline while unchecked labels inherit their weight. | Use a definition list with uniform `dt` emphasis. Let selection use border/background/focus state, not a second label hierarchy. |
| Warning position | The failure summary is mixed into state content, page-level alerts are detached, and `SaveError` is invisible. | Place the provider warning at the top of Settings before state/actions. Render current, save, and history errors in their owning context. |
| Actions | Retry, Apply, Save, Dismiss, and paging buttons lack shared layout or hierarchy. Save alone has `platform-primary-action`. | Use labelled flex action groups with a gap, wrap behavior, 44px targets, and one clear primary Save action. |
| Binary setting scale | Each option consumes half the panel width. | Retain native radio semantics but constrain the group to a readable maximum inline size and stack on narrow screens. |
| History access | The table follows a long settings sequence despite being a separate read-only workflow. | Move it to an `Observed history` tab with a labelled pager. |
| History chronology | Server ordering is newest observed first, but the UI displays recorded time. | Preserve sorting; state chronology explicitly and decide whether to display observed or clearly-labelled audit time. |

### Existing Test Coverage

* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountPreferencesTests.cs` lines 17-354 covers state, failures, radio selection, saving, focus restoration, and cursor use but not tab behavior, feedback placement, action groups, or history labelling.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountDetailsTests.cs` lines 17-159 is the nearest tab test model.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/AccountPreferences/AccountPreferencesE2ETests.cs` lines 19-40 covers the provider-unavailable state but needs tab keyboard and history-control coverage.

## Technical Scenario: Two Page-Local Tabs

**Requirements:** Keep settings immediately available, separate history without a route transition, preserve keyboard navigation, and limit work to component/CSS/UI-test slices.

**Preferred approach:** Add exactly two default-server-rendered `RadzenTabs` to `AccountPreferences.razor`: `Settings` and `Observed history`. Follow Account Details using `AriaLabel` or `AriaLabelledBy`, `@bind-SelectedIndex`, and `@key` for history rows.

```text
Modify
  src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor
  src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor.css
  test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountPreferencesTests.cs
  test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/AccountPreferences/AccountPreferencesE2ETests.cs

Do not modify
  AccountPreferencesPagePresenter.cs
  AccountPreferencesPageViewModel.cs
  API, application, or persistence projects
```

```razor
<RadzenTabs AriaLabel="Account preference views" @bind-SelectedIndex="selectedTabIndex">
    <Tabs>
        <RadzenTabsItem Text="Settings">
            <h2>Account preference state</h2>
            @if (!string.IsNullOrWhiteSpace(Presenter.State.FailureSummary))
            {
                <p class="error account-preferences-warning" role="alert">
                    @Presenter.State.FailureSummary
                </p>
            }
            <dl class="account-preferences-state">
                <div><dt>Desired setting</dt><dd>@DesiredSettingText</dd></div>
                <div><dt>Target account</dt><dd>@(Presenter.State.AccountId ?? "Not configured")</dd></div>
                <div><dt>Last observed at IG</dt><dd>@ObservedSettingText</dd></div>
                <div><dt>Observed account</dt><dd>@(Presenter.State.ObservedAccountId ?? "None")</dd></div>
                <div><dt>Verification status</dt><dd>@Presenter.State.VerificationStatus</dd></div>
            </dl>
            <div class="account-preferences-actions" role="group" aria-label="Verification actions">
                <button type="button" @onclick="RetryAsync">Retry verification</button>
                <button type="button" @onclick="RemediateAsync">Apply desired setting</button>
            </div>
            @* Retain the existing native fieldset/InputRadioGroup and its binding. *@
        </RadzenTabsItem>
        <RadzenTabsItem Text="Observed history">
            @* Keep table, history-specific alert, and cursor pager here. *@
        </RadzenTabsItem>
    </Tabs>
</RadzenTabs>
```

Current request failures can remain beneath the page heading because they block both workflows. A provider mismatch belongs inside Settings above the data/actions it explains. History failures belong inside `Observed history`. Use `role="alert"` for new errors; retain the existing `role="status"` for successful saves and add a concise status update after loading another history page.

```css
.account-preferences-actions {
    display: flex;
    flex-wrap: wrap;
    gap: 0.75rem;
    margin-block: 1rem;
}

.account-preferences-actions > button {
    min-block-size: 44px;
}

.account-preferences-choice {
    max-inline-size: 32rem;
}

.account-preferences-choice > div {
    display: grid;
    grid-template-columns: repeat(2, minmax(0, 1fr));
}

@media (max-width: 600px) {
    .account-preferences-choice > div {
        grid-template-columns: 1fr;
    }

    .account-preferences-actions > button {
        inline-size: 100%;
    }
}
```

Retain existing visible checked and `:focus-visible` states. Scope them to `.account-preferences-choice`; do not target Radzen-generated child markup. Reuse existing global action classes for primary treatment where appropriate.

### Considered Alternatives

* **Stacked Radzen accordion panels:** Rejected. Settings should be inspectable immediately; disclosure adds an unnecessary interaction state and additional labels/tests.
* **Anchored sections on one page:** Rejected. The settings workflow remains visually adjacent to diagnostics, and focus/scroll behavior becomes custom responsibility.
* **Switch or radio-list replacement:** Rejected. Native `InputRadioGroup` already makes both choices explicit and has existing test coverage. The issue is scale and styling, not semantics.
* **Client sorting or a paged grid:** Rejected. Server keyset order is authoritative and the API has no count or reverse cursor. Numeric paging would be misleading.

## Selected Approach

Implement two page-local `RadzenTabs`, with `Settings` first and `Observed history` second. In Settings, use a definition list; surface the account-mismatch warning before the state/actions it affects; constrain the native radio control; and group actions consistently. Keep history server-sorted and cursor-paged in its dedicated tab, with an honest `Load older observations` control.

This resolves every reported concern with the smallest scope, reuses a nearby tested pattern, supports keyboard navigation, and preserves ownership boundaries: presentation owns layout, the presenter owns state, and persistence owns ordering.

## Implementation and Validation Impact

* Add bUnit assertions for labelled tabs, default Settings selection, definition-list terms/descriptions, native radio group, warning/error context, action-group labels, and history pager accessible name.
* Retain save confirmation/dismiss-and-focus coverage, asserting it remains an atomic `role="status"` while Settings is active.
* Add Playwright role-locator coverage for selecting `Observed history`, keyboard navigation, the history table's accessible name/caption, and `Load older observations` state.
* Run focused Web unit tests and Account Preferences E2E tests after implementation. Inspect desktop and mobile screenshots for control wrapping and horizontal scroll.

## Open Product Decision

The current API only traverses history through `NextCursor`. Use `Load older observations`; do not introduce numeric page counts, Previous/Newer actions, or random paging unless the API exposes matching contracts. Confirm whether operators should see `ObservedAtUtc` as the primary chronology timestamp or retain `RecordedAtUtc` under a clearer audit-time label.

## Supporting Research

* `.copilot-tracking/research/subagents/2026-09-17/account-preferences-ui-current-state.md`
* `.copilot-tracking/research/subagents/2026-09-17/account-preferences-ui-alternatives.md`
