---
title: Account preferences repository research
description: Verified repository research for the account-preferences UI, persistence flow, Radzen patterns, and focused tests
ms.date: 2026-08-31
ms.topic: reference
---

## Research scope

* Trace the owning Razor and component files for the account-preferences UI.
* Trace the Boolean model and state, load, save, and update flow.
* Record the current introductory text, heading, label, and persistent confirmation behavior.
* Inspect related API and application contracts only as needed to establish the end-to-end flow.
* Inventory focused tests and nearby Radzen patterns for explicit Boolean controls and feedback.
* Record current test commands, risks, remaining gaps, and implementation-ready recommendations.

## Status

Complete for the repository implementation and source-controlled tests present on
2026-08-31. No production code was modified and no test suite was executed during
this research-only task.

## Executive findings

* The owning page is
	`src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:1-45`.
	The page owns authorization, lifecycle cancellation, markup, and event forwarding.
	Its scoped presenter and mutable view model own the asynchronous workflow and page state.
* The current Boolean editor is Blazor `InputCheckbox`, not a Radzen Boolean control.
	No `RadzenSwitch`, `RadzenToggleButton`, `RadzenSelectBar`,
	`RadzenRadioButtonList`, or `RadzenCheckBox` occurs in source-controlled Web code.
* The displayed preference is the provider-observed `bool TrailingStopsEnabled`.
	The UI can edit it locally, but persistence does not occur until the explicit
	`Save preference` action.
* A save is confirmed by an authoritative provider GET after the PUT. A mismatched
	readback becomes HTTP 409, so the Web success message is not shown for an
	unconfirmed value.
* The current success text is persistent component state, not a transient toast.
	It remains visible until another save starts or the page component is discarded.
	It is not cleared when the operator changes the Boolean after a successful save.
* Existing feedback conventions use inline semantic HTML with `role="status"` and
	`role="alert"`. The repository registers Radzen services but has no established
	Radzen notification, dialog, or alert implementation to reuse.
* Focused UI behavior is covered with bUnit. No source-controlled account-preferences
	Playwright functional or E2E test was found.

## Owning UI and visible content

### Route, access, and navigation

* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:1-7`
	declares `/account-preferences`, requires the Operator policy, injects the presenter,
	and injects the navigation access coordinator.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:31-45`
	verifies the Operator scope before loading, passes a component-lifetime cancellation
	token to load/save/history operations, and cancels it during disposal.
* `src/TNC.Trading.Platform.Web/Components/Layout/MainLayout.razor:22-32` adds the
	`Account preferences` navigation item only for an operator.

### Current text and controls

All current user-facing account-preference copy is concentrated in
`src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:9-25`:

* Browser title and H1: `Account preferences`
* Initial loading text: `Loading account preferences...`
* Introductory text: `Live trailing stops control for the configured Test account.`
* Boolean label: `Trailing stops enabled`
* Boolean control: `<InputCheckbox id="trailing-stops" ... />`
* Save action: `Save preference`, changing to `Saving...` while pending
* Default status line: `Application status: {ApplicationStatus}`
* Success confirmation: supplied by the presenter as
	`Trailing stops preference saved and confirmed.`
* Error feedback: rendered above the editor as an inline paragraph with
	`role="alert"`
* Persistent status feedback: always rendered as an inline paragraph with
	`role="status"`; it shows the success message when present and application status otherwise
* History heading: `Observed history`; history displays recorded time, raw Boolean,
	observation kind, and platform/broker environment

The checkbox and save button are disabled while saving. During initial load, the
entire editor is replaced by the loading paragraph, so no save action is available.

## Boolean model and page state

* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPageViewModel.cs:3-25`
	owns the editable `bool TrailingStopsEnabled`, provider application status,
	observation timestamp, history, and next cursor. `Apply` replaces the editable
	Boolean with the latest authoritative API response; `ApplyHistory` replaces the
	current history page.
* `src/TNC.Trading.Platform.Web/PlatformApiClient.cs:183-185` defines the Web transport
	records. The current preference and each history observation both use non-nullable
	`bool TrailingStopsEnabled`.
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/AccountPreferences.cs:1-7`
	defines the inward application model with the same non-nullable Boolean plus status
	and observed timestamp.
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/UpdateAccountPreferencesRequest.cs:1-3`
	deliberately makes the command Boolean nullable so omitted JSON can be distinguished
	from explicit `false`.
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/UpdateAccountPreferencesValidator.cs:1-7`
	rejects only `null`; both `true` and `false` are valid explicit updates.

No separate dirty flag, last-confirmed value, or selection-change callback exists.
The bound view-model property is both the pending editor value and the location into
which the confirmed server value is applied.

## Load, save, and persistence flow

### Initial load

1. `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:31-36`
   verifies scope and calls `Presenter.LoadAsync`.
2. `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPagePresenter.cs:11-27`
   sets `IsLoading`, clears `Error`,
	 loads current preferences, applies them to the page state, then loads history page 1.
3. `src/TNC.Trading.Platform.Web/PlatformApiClient.cs:139-146` sends authenticated
	 `GET /api/platform/account-preferences` with the Operator scope and requires a
	 successful JSON response.
4. `src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs:54-59,130-148`
   protects the GET and history routes with the
	 Operator policy and delegates to thin endpoint handlers.
5. `src/TNC.Trading.Platform.Application/Features/AccountPreferences/GetAccountPreferencesHandler.cs:13-35`
   checks the Test/Demo environment, calls the
	 gateway GET, and appends a `ReadObserved` observation for a successful provider value.
6. `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgAccountPreferencesGateway.cs:15-18,20-55,69-90`
   creates an IG Demo session,
	 calls version 1 `accounts/preferences`, requires a JSON Boolean, and returns the
	 observed value with status `Test`.
7. `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPagePresenter.cs:47-53`
   requests 25 history rows, and the view
	 model replaces its history and cursor.

Any `HttpRequestException` or `InvalidOperationException` in current-preference or
history loading becomes `Unable to load account preferences.`. Owned cancellation is
swallowed and loading state is reset.

### Save and confirmation

1. The checkbox two-way binds directly to `Presenter.State.TrailingStopsEnabled` at
	 `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:17-20`;
   clicking the checkbox changes only client state.
2. The explicit save button calls `Presenter.SaveAsync` at
	 `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:20,37`.
3. `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPagePresenter.cs:29-45`
   sets `IsSaving`, clears prior error and
	 message, sends the selected Boolean, applies the returned authoritative value,
	 sets `Trailing stops preference saved and confirmed.`, then reloads history page 1.
4. `src/TNC.Trading.Platform.Web/PlatformApiClient.cs:148-156` sends authenticated
	 `PUT /api/platform/account-preferences` with JSON
	 `{ TrailingStopsEnabled = trailingStopsEnabled }`.
5. `src/TNC.Trading.Platform.Api/Features/Platform/UpdateAccountPreferencesEndpointHandler.cs:8-18`
   translates the nullable HTTP body
	 into the application request, validates it, dispatches the handler, and maps the outcome.
6. `src/TNC.Trading.Platform.Application/Features/AccountPreferences/UpdateAccountPreferencesHandler.cs:13-60`
   rejects unsupported environments, performs
	 one PUT, never retries an indeterminate PUT, and always performs one authoritative GET
	 unless the PUT definitively failed.
7. When GET equals the requested Boolean,
   `src/TNC.Trading.Platform.Application/Features/AccountPreferences/UpdateAccountPreferencesHandler.cs:36-43`
	 records `UpdateConfirmed` or `ReconciliationObserved` and returns the GET result.
8. When GET differs,
   `src/TNC.Trading.Platform.Application/Features/AccountPreferences/UpdateAccountPreferencesHandler.cs:45-49`
   records the observed
	 value as `ReadObserved`, emits `UpdateNotApplied`, and returns `NotApplied`.
9. `src/TNC.Trading.Platform.Api/Features/Platform/AccountPreferencesEndpointMapping.cs:15-32`
   maps confirmed success to HTTP 200,
	 an unconfirmed mismatch to HTTP 409 with requested/observed extension values, and
	 provider failures to stable Problem Details responses.
10. `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfTrailingStopsPreferenceObservationStore.cs:9-62`
    persists each observation to SQL Server and implements partitioned, descending
    keyset history reads.

The Boolean preference itself remains provider-owned. SQL stores an append-only
observation/audit history, not an independently editable source of truth.

## Persistent confirmation behavior

`src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPagePresenter.cs:7-9,29-45`
stores confirmation in the scoped presenter's `Message` property.
`src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:21`
renders that message in
the always-present `role="status"` paragraph. Verified lifecycle behavior is:

* It is absent on initial construction.
* It is cleared at the beginning of every save.
* It is assigned only after the update API returns a confirmed response.
* It remains assigned after a successful history refresh.
* It remains assigned when the operator changes the checkbox after save.
* It is lost when the scoped presenter/page instance is discarded or a new circuit scope is created.
* It can coexist with an error if the update succeeds but the subsequent history reload fails,
	because the success message is assigned before history loading and the catch only sets `Error`.

This is persistent inline confirmation for the current page instance, not durable
confirmation across navigation, reload, or a new session.

## Nearby Radzen and feedback patterns

### Radzen setup and current usage

* `src/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.csproj:7-10` references
	`Radzen.Blazor` version `10.3.2`.
* `src/TNC.Trading.Platform.Web/PlatformWebUiServiceCollectionExtensions.cs:1-18`
	calls `AddRadzenComponents()` and registers the account-preferences presenter.
* `src/TNC.Trading.Platform.Web/Components/App.razor:7-18` hosts the Radzen theme,
	interactive server routes, and the Radzen JavaScript asset.
* `src/TNC.Trading.Platform.Web/Components/Layout/PlatformThemeToggle.razor:1-15`
	is the closest Boolean-like Radzen interaction. It uses `RadzenButton` to toggle
	dark/light theme state, changes icon and text to describe the next action, and does
	not expose an on/off form value.
* `src/TNC.Trading.Platform.Web/Components/Layout/PlatformHeader.razor:3-14` uses a
	small flat secondary `RadzenButton` for sidebar collapse/expand.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountDetails/AccountDetails.razor:30-61`
	uses `RadzenTabs`, `RadzenBadge`, `RadzenRow`, and `RadzenColumn` for explicit
	selected views and status presentation.

Repository search found no established Radzen switch, checkbox, toggle button,
select bar, radio-button list, notification service, dialog service, or alert component.
An account-preferences implementation must therefore select a new Radzen Boolean
pattern from official Radzen demos/API guidance rather than copying a local precedent.

### Existing explicit-option controls

`src/TNC.Trading.Platform.Web/Components/Pages/Configuration/Configuration.razor:43-58,75-91`
uses labelled Blazor `InputSelect` controls with explicit option text for environment
and weekend behavior. This is the nearest form precedent for making a finite choice
visibly explicit, but it is not Radzen and it is not Boolean.

### Existing feedback

* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:16,21`
  uses an inline error with `role="alert"` and an
	always-present status line with `role="status"`.
* `src/TNC.Trading.Platform.Web/Components/Pages/Configuration/Configuration.razor:59-65,146-153,181-187`
  uses conditional inline warning/status
	paragraphs for restart requirements, credential re-entry, and save confirmation.
* `src/TNC.Trading.Platform.Web/Components/Pages/Status/Status.razor:91-106`
  uses `role="alert"` for degraded authentication and
	`role="status"` for manual-retry results.

These patterns favor persistent, page-local, accessible feedback over transient
notifications. No dialog confirmation precedent was found for this operator action.

## Focused tests and commands

### Account-preferences test inventory

* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountPreferencesTests.cs:14-172`
	contains ten bUnit tests for initial value/history rendering, checkbox interaction,
	confirmed save feedback, hidden save during initial loading, disposal cancellation,
	presenter cancellation cleanup, disabled save while pending, load failure feedback,
	paging cursor forwarding, and denied viewer access. The save-success test does not
	change the control before saving or assert the outbound PUT body.
* `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/AccountPreferences/UpdateAccountPreferencesValidatorTests.cs:7-29`
	proves omitted input is rejected and both explicit Boolean values are accepted.
* `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/AccountPreferences/AccountPreferencesEnvironmentPolicyTests.cs:8-31`
	proves legacy Demo is presented as Test and only Test/Demo is supported.
* `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/AccountPreferences/AccountPreferencesHandlerTests.cs:11-151`
	covers Test/Live safety, mismatched readback, indeterminate PUT reconciliation,
	terminal failure observation rules, repeated equal writes, sanitized telemetry,
	and deterministic history cursor forwarding.
* `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/AccountPreferencesEndpointTests.cs:9-35`
	covers malformed paging input and omitted PUT input at the HTTP boundary.
* `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/AccountPreferencesEndpointMappingTests.cs:9-86`
	covers safe failure mapping, confirmed response mapping, Demo-to-Test history
	presentation, 409 mismatch details, nullable PUT validation, and history validation.
* `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/PlatformApplicationServiceCollectionExtensionsTests.cs:12-43`
	verifies route construction and resolution of all account-preferences application services.
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/Integrations/Ig/IgAccountPreferencesGatewayTests.cs:10-99`
	covers IG headers/session contract, strict Boolean JSON, one GET replay after 401,
	rate-limit classification, PUT JSON, no PUT replay, and malformed acknowledgement.
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/EfTrailingStopsPreferenceObservationStoreTests.cs:8-39`
	covers append-only ordering, deterministic paging, page limits, and invalid cursors
	against the in-memory EF test context.
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/AccountPreferencesSqlIntegrationTests.cs:13-179`
	contains seven SQL Server tests for migrations, rerun safety, restart durability,
	equal-timestamp paging, partition isolation, invalid cursors, and retention.

No account-preferences test was found in the source-controlled Web functional or E2E
projects. There is also no focused test for changing the selection after confirmation,
save failure after editing, update-success/history-failure feedback, accessible Boolean
control semantics, or the PUT body's selected false/true value from the Web layer.

### Current commands

Repository guidance at `docs/wiki/local-development.md:204-219` uses these root commands:

```powershell
dotnet test
dotnet test -m:1
```

The second command is the documented serialized fallback for intermittent MSBuild
child-node exits. `docs/wiki/testing-and-quality.md:451-465` also documents:

```powershell
dotnet test TNC.Trading.Platform.slnx -m:1
```

Implementation work can use these narrower, behavior-focused commands from the root:

```powershell
dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj --filter "FullyQualifiedName~AccountPreferencesTests"
dotnet test test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/TNC.Trading.Platform.Application.UnitTests.csproj --filter "FullyQualifiedName~AccountPreferences"
dotnet test test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/TNC.Trading.Platform.Api.UnitTests.csproj --filter "FullyQualifiedName~AccountPreferences"
dotnet test test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/TNC.Trading.Platform.Infrastructure.UnitTests.csproj --filter "FullyQualifiedName~AccountPreferences|FullyQualifiedName~TrailingStopsPreference"
dotnet test test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/TNC.Trading.Platform.Infrastructure.IntegrationTests.csproj --filter "FullyQualifiedName~AccountPreferencesSqlIntegrationTests"
```

The SQL integration command requires the repository's SQL Server test prerequisite.
The first four focused commands are designed around fake/in-memory boundaries and do
not require live IG credentials.

## Risks

* Stale confirmation: after a successful save, changing the Boolean does not clear
	`Message`, so `saved and confirmed` can describe a value that is only pending locally.
* Contradictory feedback: the presenter sets success before refreshing history. If the
	refresh fails, both the success status and save error render together.
* Collapsed failure meaning: `PlatformApiClient` calls `EnsureSuccessStatusCode()`.
	The API exposes safe 409 requested/observed values, but the presenter reduces every
	non-success save to `Unable to save and confirm account preferences.`.
* Control ambiguity: a checkbox conveys checked/unchecked, while the surrounding copy
	uses enabled/disabled domain language. A clearly labelled switch or two-option
	selector would make the requested state more explicit.
* Missing dirty-state protection: save is available even when the selection equals the
	loaded value, and there is no visual distinction between observed and edited state.
* Coverage gap: bUnit tests assert checked markup and text, but not role/name/state
	semantics for assistive technology or the actual outbound Boolean body.
* Browser gap: no functional/Playwright test proves keyboard interaction, focus,
	responsive layout, or persistent confirmation in a real rendered circuit.
* History refresh coupling: successful preference confirmation is treated as a failed
	save when only the secondary history refresh fails.
* Local precedent gap: adopting Radzen notification/dialog feedback would introduce a
	new interaction convention. It would also conflict with the established persistent
	inline feedback requirement unless explicitly justified.

## Implementation-ready recommendations

1. Keep the current Web presenter, API contract, application Boolean, and provider
	 confirmation workflow. A control/copy/feedback update does not require API,
	 application, gateway, or SQL changes.
2. Replace the `InputCheckbox` in
	`src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:17-20`
	with one explicit
	 Radzen Boolean control after checking the Radzen 10.3.2 API. Prefer a two-option
	 enabled/disabled selector when the requirement prioritizes unmistakable state;
	 prefer `RadzenSwitch` when compact direct manipulation is preferred. Preserve a
	 visible label and an accessible name that includes `Trailing stops`.
3. Keep save explicit. Do not persist immediately on toggle because the existing
	 workflow intentionally separates pending local choice from provider-confirmed state,
	 and the application performs a network PUT plus authoritative GET.
4. Add presenter/view-model dirty-state handling: retain the last confirmed Boolean,
	 clear confirmation on selection change, and optionally disable save until the
	 selection differs from the confirmed value. Do this in the UI slice only.
5. Make confirmation sequencing unambiguous. Apply the confirmed response first, then
	 treat history refresh as a separate secondary operation. Either set confirmation
	 after history succeeds or preserve confirmation while reporting history failure with
	 distinct wording; never show `saved and confirmed` alongside a generic save failure.
6. Retain persistent inline `role="status"` feedback rather than introducing a toast or
	 dialog. This matches the current account-preferences, configuration, and status-page
	 conventions and keeps confirmation available for assistive technology and later review.
7. Consider translating the safe 409 Problem Details into a specific operator message
	 and reapplying the observed Boolean. Keep provider diagnostics hidden. This is the
	 only recommendation that may require a narrow Web API-client error contract change.
8. Update bUnit coverage to assert both enabled and disabled choices, accessible label
	 and state, selected PUT body, save disabled/dirty behavior, confirmation persistence,
	 confirmation clearing after edit, and update-success/history-failure feedback.
9. Add one focused Playwright functional scenario only if the change introduces a new
	 Radzen interactive control whose keyboard/accessibility behavior is not adequately
	 proven by bUnit. Use role/label locators and web-first assertions per repository rules.
10. Keep the introductory text and heading change localized to
		`src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:9-21`.
    No current copy is duplicated in contracts or tests
		except assertions around `saved and confirmed` and generic load failure.

## Remaining gaps and clarifying questions

No repository research blocker remains. Product intent must answer these implementation
choices:

* Whether the requested explicit Boolean presentation is a compact switch or a
	two-option `Enabled`/`Disabled` segmented selector
* Whether persistent confirmation should survive only subsequent edits, page navigation,
	a browser reload, or an entire authenticated session
* Whether unchanged values may be saved intentionally to create another audit observation
* Whether an authoritative mismatch should expose requested and observed values to the
	operator or retain the current generic message
* The exact replacement introductory text, heading, label, and confirmation copy if the
	user-provided visual specification is not available to the implementing agent

## Suggested next research

* [ ] Confirm the desired switch versus segmented-selector interaction from the user's visual specification.
* [ ] Check the official Radzen 10.3.2 API/demo for the chosen component's binding,
	disabled, accessibility, and change-event parameters before implementation.
* [ ] Confirm product semantics for unchanged saves and confirmation lifetime.
* [ ] Inspect the rendered page at desktop and mobile sizes only if implementation proceeds.

## References

Repository rules applied during this research:

* `.github/copilot-instructions.md:1-76`
* `.github/AGENTS.md:1-43`
* `.github/lessons.md:1-9`
* `.github/instructions/folders.instructions.md:1-49`
* `.github/instructions/radzen-blazor.instructions.md:1-103`
* `.github/instructions/tests.instructions.md:1-104`
* `.github/instructions/playwright.instructions.md:1-66`
* `.github/instructions/architecture.instructions.md:1-176`
* `.github/instructions/configuration.instructions.md:1-57`
* `.github/instructions/csharp.instructions.md:1-85`
* External Markdown and writing-style instructions supplied for this workspace session
