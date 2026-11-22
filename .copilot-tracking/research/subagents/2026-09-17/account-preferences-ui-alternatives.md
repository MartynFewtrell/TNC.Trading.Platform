---
title: Account Preferences UI Alternatives
status: Complete
ms.date: 2026-09-17
ms.topic: research
---

## Research Scope

Evaluate phase-2 Account Preferences UI alternatives:

1. Two page-local Radzen tabs
2. Stacked collapsible panels
3. Single page with anchored sections

The evaluation covers current repository patterns, official Radzen and Blazor
accessibility features, warning placement, state display, action groups, compact
binary selection, tab panels, and paged history.

## Evidence

* The account-preferences page has two distinct workflows: a stateful,
  operator-only trailing-stops update and read-only, cursor-paged observation
  history. The page currently places both in a single linear sequence:
  `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:18-55`.
* The preference state intentionally distinguishes the editable pending value
  from the provider-confirmed value. `HasPendingChange` compares them and must
  remain the source for dirty-state presentation:
  `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPageViewModel.cs:4-18`.
* The existing Account Details page already uses page-local `RadzenTabs` with
  `AriaLabel`, two-way selected-index binding, and the default server rendering:
  `src/TNC.Trading.Platform.Web/Components/Pages/AccountDetails/AccountDetails.razor:30-61`.
  It also establishes the repository's labelled `<nav>` pager pattern at lines
  65-68.
* The Web project runs globally as `InteractiveServer`, has the Radzen theme and
  script installed, registers Radzen services, and currently references
  Radzen.Blazor 10.3.2. Evidence: `src/TNC.Trading.Platform.Web/Components/App.razor:12-17`,
  `src/TNC.Trading.Platform.Web/PlatformWebUiServiceCollectionExtensions.cs:13`,
  and `src/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.csproj:10`.
* Current page-level errors are live alerts, save confirmation is an atomic
  status region with dismissal focus returned to Save, and the choice is a
  native `fieldset` plus `InputRadioGroup`. Preserve these semantics rather than
  replacing them with visual-only controls:
  `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:15-48`.
* Current scoped CSS makes the two radio labels a compact two-column control,
  retains visible checked and focus states, and stacks it at 600px. Retain this
  responsive behavior while narrowing its selectors to the Settings tab:
  `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor.css:1-77`.
* The history endpoint returns one 25-item page and a next cursor. The presenter
  owns the API call and error state, so no API, application, or persistence
  change is required for a presentation-only redesign:
  `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPagePresenter.cs:76-122`.

## External Accessibility Rules

* Radzen Tabs provides `AriaLabel` and `AriaLabelledBy`; its documented keyboard
  navigation includes Arrow, Home, End, Space, and Enter. Use an explicit
  visible heading with `AriaLabelledBy`, or an explicit `AriaLabel` when no
  heading is available. Source: [RadzenTabs API](https://blazor.radzen.com/docs/api/Radzen.Blazor.RadzenTabs.html).
* Radzen Tabs defaults to server rendering, which renders only the selected
  panel. Client rendering renders all panels initially and switches them in
  JavaScript. Default server mode matches the Account Details precedent and
  avoids placing an inactive history table in the initial DOM. Source:
  [Radzen Tabs demo](https://blazor.radzen.com/tabs).
* Radzen Accordion is accessible only when each item is given meaningful
  `ExpandAriaLabel` and `CollapseAriaLabel`; it supports `Multiple="true"` when
  independently expanded sections are required. It adds unnecessary disclosure
  state for this two-workflow page. Sources: [Radzen Accordion demo](https://blazor.radzen.com/accordion)
  and [RadzenAccordionItem API](https://blazor.radzen.com/docs/api/Radzen.Blazor.RadzenAccordionItem.html).
* RadzenRadioButtonList exposes `AriaLabelledBy` and supports Arrow, Space, and
  Enter keyboard operation. The existing native `InputRadioGroup` already emits
  a standard radio group inside a visible `fieldset` and is the lower-risk
  choice for this binary setting. Sources: [RadzenRadioButtonList API](https://blazor.radzen.com/docs/api/Radzen.Blazor.RadzenRadioButtonList-1.html)
  and [Blazor input components](https://learn.microsoft.com/aspnet/core/blazor/forms/input-components?view=aspnetcore-10.0).
* A Radzen DataGrid supports paging and accepts explicit ARIA labels for first,
  previous, next, last, and numeric page controls. It is not a fit for the
  existing opaque next-cursor response because the UI has no total count or
  random-page capability. Sources: [RadzenDataGrid API](https://blazor.radzen.com/docs/api/Radzen.Blazor.RadzenDataGrid-1.html)
  and [PagedDataBoundComponent API](https://blazor.radzen.com/docs/api/Radzen.PagedDataBoundComponent-1.html).
* Interactive controls require an interactive render mode. The app already
  provides `InteractiveServer`; preserve it for tab selection, radio changes,
  commands, and paging. Source: [Blazor render modes](https://learn.microsoft.com/aspnet/core/blazor/components/render-modes?view=aspnetcore-10.0).
* Use declarative Razor output and `ElementReference.FocusAsync` for the existing
  post-dismiss focus behavior. Do not directly mutate Blazor-controlled DOM via
  JavaScript. Source: [Blazor JS interop](https://learn.microsoft.com/aspnet/core/blazor/javascript-interoperability/?view=aspnetcore-10.0).

## Alternative Evaluation

| Alternative | Fit | Accessibility and delivery assessment |
| --- | --- | --- |
| Two page-local Radzen tabs | Selected | Separates the edit workflow from history without a route transition, follows the existing local tab pattern, exposes a labelled tab list with documented keyboard support, and keeps the page compact. Two stable labels avoid overflow or wrapping concerns. |
| Stacked collapsible panels | Do not select | Accordion disclosure adds an expand/collapse decision before both primary tasks. A settings panel should be immediately inspectable for safety; a history panel can remain secondary through a tab. Each accordion item also needs explicit expand/collapse ARIA labels and test coverage. |
| Single page with anchored sections | Do not select | It preserves native headings and works without JavaScript, but the lengthy state block, edit action, and history table remain visually adjacent. Anchor links do not materially improve the repeated operator task, and navigation focus/scroll behavior becomes custom responsibility. |

## Recommendation

Use exactly two page-local `RadzenTabs`: `Settings` first and `Observed history`
second. Keep `RadzenTabs` in its default server render mode and bind the selected
index only if the page needs to retain the active tab after a render. Do not add
accordions, anchor navigation, a new route, a new component library, or a
Radzen DataGrid.

This is the smallest design that isolates the safety-sensitive configuration
work from the diagnostic table while matching the existing Account Details
implementation. The Settings panel remains immediately available, history is
one deliberate tab selection away, and built-in tab keyboard semantics are
preserved. The presenter, API contract, and cursor paging stay unchanged.

## Proposed Markup

The following is runnable Razor-shaped implementation guidance for
`AccountPreferences.razor`. It preserves the repository's native binary radio
pattern, no-JavaScript error and status semantics, and cursor paging.

```razor
<section class="panel platform-page-header">
	<div>
		<h1>Account preferences</h1>
		<p class="hint">Live trailing stops control for the configured account.</p>
	</div>
</section>

@if (!string.IsNullOrWhiteSpace(Presenter.CurrentError))
{
	<p class="error" role="alert" data-testid="account-preferences-error">
		@Presenter.CurrentError
	</p>
}

<section class="panel account-preferences-workspace"
		 aria-labelledby="account-preferences-workspace-heading">
	<h2 id="account-preferences-workspace-heading">Manage trailing stops</h2>
	<RadzenTabs AriaLabelledBy="account-preferences-workspace-heading"
				@bind-SelectedIndex="selectedTabIndex">
		<Tabs>
			<RadzenTabsItem Text="Settings">
				<dl class="platform-key-value-grid account-preferences-state"
					aria-label="Account preference state">
					<div><dt>Desired setting</dt><dd>@DesiredSettingText</dd></div>
					<div><dt>Target account</dt><dd>@(Presenter.State.AccountId ?? "Not configured")</dd></div>
					<div><dt>Last observed at IG</dt><dd>@ObservedSettingText</dd></div>
					<div><dt>Observed account</dt><dd>@(Presenter.State.ObservedAccountId ?? "None")</dd></div>
					<div><dt>Verification status</dt><dd>@Presenter.State.VerificationStatus</dd></div>
				</dl>

				@if (!string.IsNullOrWhiteSpace(Presenter.State.FailureSummary))
				{
					<p class="error account-preferences-warning" role="alert">
						@Presenter.State.FailureSummary
					</p>
				}

				<div class="account-preferences-actions" role="group"
					 aria-label="Verification actions">
					<button type="button" @onclick="RetryAsync"
							disabled="@(Presenter.IsOperating || Presenter.IsSaving)">
						Retry verification
					</button>
					<button type="button" @onclick="RemediateAsync"
							disabled="@(Presenter.IsOperating || Presenter.IsSaving)">
						Apply desired setting
					</button>
				</div>

				@if (Presenter.State.HasConfirmedValue)
				{
					<fieldset class="account-preferences-choice" disabled="@Presenter.IsSaving">
						<legend>Trailing stops</legend>
						<p id="trailing-stops-help" class="hint">
							Last confirmed: @(Presenter.State.ConfirmedTrailingStopsEnabled == true ? "Enabled" : "Disabled")
						</p>
						<InputRadioGroup TValue="bool" Value="@Presenter.State.TrailingStopsEnabled"
										 ValueChanged="Presenter.Select"
										 ValueExpression="@(() => Presenter.State.TrailingStopsEnabled)"
										 aria-describedby="trailing-stops-help">
							<label><InputRadio Value="true" /> Enabled</label>
							<label><InputRadio Value="false" /> Disabled</label>
						</InputRadioGroup>
					</fieldset>
					<div class="account-preferences-actions" role="group"
						 aria-label="Preference actions">
						<button @ref="saveButton" class="platform-primary-action" type="button"
								@onclick="SaveAsync"
								disabled="@(Presenter.IsSaving || Presenter.IsLoading)">
							Save preference
						</button>
					</div>
				}

				@if (!string.IsNullOrWhiteSpace(Presenter.Message))
				{
					<div role="status" aria-atomic="true"
						 data-testid="account-preferences-confirmation">
						<span>@Presenter.Message</span>
						<button type="button" @onclick="DismissConfirmationAsync">
							Dismiss save confirmation
						</button>
					</div>
				}
			</RadzenTabsItem>
			<RadzenTabsItem Text="Observed history">
				@if (!string.IsNullOrWhiteSpace(Presenter.HistoryError))
				{
					<p class="error" role="alert"
					   data-testid="account-preferences-history-error">
						@Presenter.HistoryError
					</p>
				}
				<table>
					<caption class="visually-hidden">Trailing stops observation history</caption>
					<thead><tr><th scope="col">Recorded</th><th scope="col">Enabled</th><th scope="col">Kind</th><th scope="col">Environment</th></tr></thead>
					<tbody>
						@foreach (var observation in Presenter.State.History)
						{
							<tr @key="observation.Id">
								<td><time datetime="@observation.RecordedAtUtc.ToString("O")">@observation.RecordedAtUtc.ToLocalTime().ToString("g")</time></td>
								<td>@(observation.TrailingStopsEnabled ? "Enabled" : "Disabled")</td>
								<td>@observation.ObservationKind</td>
								<td>@observation.PlatformEnvironment / @observation.BrokerEnvironment</td>
							</tr>
						}
					</tbody>
				</table>
				<nav class="platform-pagination" aria-label="Observed history pagination">
					<button type="button" data-testid="account-preferences-next"
							aria-label="Load older observations"
							@onclick="LoadNextAsync"
							disabled="@(Presenter.IsLoading || Presenter.State.NextCursor is null)">
						Load older
					</button>
				</nav>
				<p class="visually-hidden" role="status" aria-atomic="true">
					@historyUpdateMessage
				</p>
			</RadzenTabsItem>
		</Tabs>
	</RadzenTabs>
</section>
```

Warning placement is intentional: a current-state request error remains directly
after the page header because it prevents all workflows; a provider-state warning
is inside Settings, after the definition list and before the actions it explains;
and a history error stays inside Observed history. Keep `role="alert"` only for
new urgent failures. Do not use it for unchanged state or the successful save
message, which belongs in the existing `role="status"` region.

`DesiredSettingText` and `ObservedSettingText` should be private presentation
properties that map nullable values to `Enabled`, `Disabled`, `Unconfigured`, or
`No observation`. The `LoadNextAsync` wrapper should set `historyUpdateMessage`
to a concise result such as `Older observations loaded.` only after successful
completion. It should not move focus because the invoking button remains present.

## Proposed CSS

Keep the existing 44px control height, visible `:focus-visible` outline, checked
state, and mobile stacking. Scope only the revised layout styles; do not target
Radzen's generated internal markup.

```css
.account-preferences-state {
	margin-block: 1rem;
}

.account-preferences-warning {
	margin-block: 1rem;
}

.account-preferences-actions {
	display: flex;
	flex-wrap: wrap;
	gap: 0.75rem;
	margin-block: 1rem;
}

.account-preferences-actions > button {
	min-block-size: 44px;
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

The existing checked indicator and `:focus-within` outline rules should be moved
from `.panel fieldset` selectors to `.account-preferences-choice` selectors. The
`visually-hidden` helper must be reused only if it already exists in global CSS;
otherwise add the conventional screen-reader-only utility in the page stylesheet.

## Exact Implementation Impact

* Modify `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:14-57` to place the current panel content in the two tab items, convert repeated state paragraphs to a `<dl>`, group verification and save commands, localize history feedback, and add `selectedTabIndex`, presentation text properties, and the successful-page status update.
* Modify `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor.css:1-77` to scope the current compact binary control, preserve keyboard focus and 44px targets, add action-group wrapping, and ensure tab content remains usable below 600px.
* Do not modify `AccountPreferencesPagePresenter.cs`, `AccountPreferencesPageViewModel.cs`, API endpoints, or contracts unless the delivery design changes cursor history into count-based paging. The recommended design does not require that change.

## Exact Test Impact

* Extend `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountPreferencesTests.cs:103-127` to assert the labelled two-tab structure, definition-list terms and descriptions, the retained native radio fieldset, and the Settings action-group labels after a successful load.
* Extend `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountPreferencesTests.cs:174-196` to assert save confirmation remains `role="status"`, atomic, dismissible, and returns focus to Save while Settings remains active.
* Extend `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountPreferencesTests.cs:354-368` to assert the history tab's labelled pagination navigation, button accessible name, disabled state without a cursor, cursor request, and post-page status text.
* Add Playwright assertions in `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/AccountPreferences/AccountPreferencesE2ETests.cs:20-37` that use role locators to select `Observed history`, verify focusable tab operation with keyboard, confirm the history table caption or accessible name, and verify the labelled `Load older observations` button. Retain the existing provider-unavailable test.
* Existing state, request-contract, cancellation, and recovery coverage in `AccountPreferencesTests.cs:17-468` already protects behavior that the markup change must not regress. No functional API test changes are needed.

## Validation Rules

* Verify Settings is the default selected tab and `Observed history` can be selected with Arrow/Home/End and Space/Enter using the documented Radzen tab behavior.
* Verify each radio option has visible text, exactly one checked value after a successful load, a programmatic group label through the `legend`, and a visible focus indicator.
* Verify current-state failure is announced as an alert, successful save is announced as a status, and history paging completion is announced once through its status region.
* Verify tab changes preserve the presenter-backed pending value and do not accidentally save, retry, or remediate.
* Verify narrow and mobile layouts retain the full label text and do not require horizontal scrolling for the binary selection or action group.

## Open Questions

* The current history API supplies only a next cursor. Product owners must decide whether one-way chronological traversal is sufficient. The recommended design labels the action as `Load older`; it should not present a misleading numeric page count or a disabled `Newer` button without a matching cursor.
* The research did not execute the authenticated Playwright suite. Delivery should run the focused unit and E2E tests after implementation.

## Recommendation

Pending evidence review.

## Sources

Pending evidence review.

## Implementation and Test Impact

Pending repository analysis.

## Open Questions

Pending repository analysis.
