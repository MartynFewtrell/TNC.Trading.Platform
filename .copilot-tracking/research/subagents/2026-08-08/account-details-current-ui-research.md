---
title: Account Details Current UI Research
description: Verified research into the current Account details page, its data path, visual conventions, tests, and tabbed-redesign implications.
ms.date: 2026-08-08
ms.topic: concept
---

## Status

Complete. The findings were verified against the current workspace files and
the focused Account Details web unit-test baseline.

## Research Scope

* Locate the current Account details page, route, components, data model,
	visual conventions, and automated tests.
* Capture exact file and line evidence relevant to redesigning account records
	into individual tabs.
* Read applicable Razor/C# and Markdown repository instructions, especially
	Radzen Blazor guidance.
* Do not modify product code.

## Current UI

* The Viewer-authorized page route is `/account-details`. It injects
	`AccountDetailsPagePresenter` and `PlatformNavigationAccessCoordinator`:
	src/TNC.Trading.Platform.Web/Components/Pages/AccountDetails/AccountDetails.razor:1-6.
* The sidebar exposes Account details only to Viewer-role users, with the
	`account_balance` icon: src/TNC.Trading.Platform.Web/Components/Layout/MainLayout.razor:20-26.
* The page checks Viewer scope before loading. It separately checks Operator
	scope before showing the Refresh button, then calls `Presenter.LoadAsync()`:
	src/TNC.Trading.Platform.Web/Components/Pages/AccountDetails/AccountDetails.razor:42-51.
* It presents four state branches: redirecting, initial loading, no saved
	snapshot, and a saved retrieval. The saved retrieval displays metadata in a
	key-value list, then iterates every account as a consecutive `<article>` in
	one panel: src/TNC.Trading.Platform.Web/Components/Pages/AccountDetails/AccountDetails.razor:15-40.
* Each account article uses the display name with account ID fallback and
	renders identity, status, transfer flags, currency, and Balance, Deposit,
	Profit/loss, and Available. No selected-account state, tabs, account
	filtering, or account-specific route exists: src/TNC.Trading.Platform.Web/Components/Pages/AccountDetails/AccountDetails.razor:29-34.
* Older/Newer navigation remains retrieval-history navigation, not
	account navigation: src/TNC.Trading.Platform.Web/Components/Pages/AccountDetails/AccountDetails.razor:37-40.

## Components And Data Path

* `AccountDetailsPagePresenter` owns page state, loading and refresh flags,
	error text, cursor navigation, and refresh replacement behavior. A refresh
	preserves the currently held newer cursor: src/TNC.Trading.Platform.Web/Components/Pages/AccountDetails/AccountDetailsPagePresenter.cs:5-51.
* The web view models are immutable records. A retrieval contains metadata and
	`IReadOnlyList<AccountDetailsAccountViewModel>`; each account includes all
	fields currently rendered: src/TNC.Trading.Platform.Web/Components/Pages/AccountDetails/AccountDetailsViewModels.cs:3-29.
* `PlatformApiClient` requests `GET /api/platform/account-details` with the
	Viewer scope and an optional escaped cursor. Refresh posts to the same
	resource's `/refresh` endpoint with the Operator scope:
	src/TNC.Trading.Platform.Web/PlatformApiClient.cs:118-137.
* The API assigns Viewer authorization to the query and Operator authorization
	to refresh. It delegates both operations to separate application handlers:
	src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs:49-51,
	106-121.
* API mapping projects the whole ordered `snapshot.Accounts` collection into
	`AccountDetailsAccountResponse` records without a UI-selected account field:
	src/TNC.Trading.Platform.Api/Features/Platform/AccountDetailsEndpointMapping.cs:5-55.
* The inward snapshot is immutable and consists of retrieval metadata plus an
	account list: src/TNC.Trading.Platform.Application/Features/AccountDetails/AccountDetailsSnapshot.cs:5-11.
	The account record holds the 13 account-level fields presented by the UI:
	src/TNC.Trading.Platform.Application/Features/AccountDetails/AccountDetailsAccount.cs:3-16.

## Visual Conventions

* The Account details page uses the shared `panel`, `platform-page-header`,
	`hint`, `platform-key-value-grid`, `platform-button`, and
	`platform-pagination` vocabulary:
	src/TNC.Trading.Platform.Web/Components/Pages/AccountDetails/AccountDetails.razor:10-14,
	25-40.
* `panel` is the established framed surface. It has the shared strong surface,
	border, 1.25rem radius, padding, shadow, and bottom margin:
	src/TNC.Trading.Platform.Web/wwwroot/app.css:241-248.
* Key-value content uses a two-column grid that collapses to one column at the
	mobile breakpoint, so current account details remain responsive without
	account-page-specific CSS:
	src/TNC.Trading.Platform.Web/wwwroot/app.css:295-310, 500-502.
* Adjacent read-only pages use the same panel and key-value pattern. Status
	groups dense information with `PlatformAccordionSection`:
	src/TNC.Trading.Platform.Web/Components/Pages/Status/Status.razor:24-53.
	IG login history expands each history record with native `<details>`:
	src/TNC.Trading.Platform.Web/Components/Pages/IgLogin/IgLoginHistory.razor:31-69.
* Radzen is already available globally through `_Imports.razor`, service
	registration, app-level theme, official script, and Interactive Server
	rendering:
	src/TNC.Trading.Platform.Web/Components/_Imports.razor:1-14;
	src/TNC.Trading.Platform.Web/PlatformWebUiServiceCollectionExtensions.cs:7-22;
	src/TNC.Trading.Platform.Web/Components/App.razor:1-16;
	src/TNC.Trading.Platform.Web/Program.cs:11-19.
	The project references Radzen.Blazor 10.3.2:
	src/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.csproj:8-17.

## Existing Tests

* The only Account Details web UI test file is the bUnit suite:
	test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountDetailsTests.cs.
* It asserts the empty state: AccountDetailsTests.cs:9-17.
* It uses one account to assert rendered account content, Balance/Currency,
	history-button enablement, and hidden Refresh for a Viewer:
	AccountDetailsTests.cs:19-39.
* It asserts a failed Operator refresh retains the saved retrieval and renders
	an error: AccountDetailsTests.cs:41-61.
* The current suite has no test with multiple accounts, tab selection,
	generated tab labels, keyboard tab behavior, focus retention after refresh
	or history navigation, or narrow/mobile tab handling.
* API mapping coverage verifies null retrievals, one-account retrieval mapping,
	cursors, success, conflict, and provider failure status mapping:
	test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/AccountDetailsEndpointMappingTests.cs:8-108.
* Application tests cover account-detail cursor determinism and retrieval
	behavior, but not web presentation:
	test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/AccountDetails/AccountDetailsContractTests.cs:7-48;
	test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/AccountDetails/AccountDetailsHandlerTests.cs.

## Applicable Instructions

* `.github/instructions/radzen-blazor.instructions.md` applies to Razor, C#,
	and CSS. It makes free Radzen controls the preferred shared UI choice,
	requires interactive rendering, accessibility, component parameters and CSS
	variables before markup-dependent overrides, and focused interaction tests.
	It specifically lists tabs as a preferred common UI capability.
* `.github/instructions/dotnet-stack.instructions.md` applies to the Account
	Details Razor and C# paths. It requires Blazor for new UI work and defers
	component guidance to the Radzen instructions.
* `.github/instructions/configuration.instructions.md` applies to these paths,
	but has no direct data-model impact on this read-only page. It matters only
	if a future tab introduces configuration or secret editing; persisted secrets
	must never be rendered.
* `.github/instructions/csharp.instructions.md` applies to presenter and model
	changes: maintain null safety, matching single-top-level-type files, existing
	language version, and tests for visible behavior.
* `.github/instructions/architecture.instructions.md` applies to the API and
	application C# paths. A visual account selector should stay in the inbound
	Blazor adapter and must not introduce UI state into application contracts or
	persistence.
* `.github/instructions/auth.instructions.md` applies to C# but does not call
	for an authentication change. Preserve the existing Viewer query and
	Operator refresh authorization boundary.
* `.github/instructions/tests.instructions.md` applies to test C# and requires
	xUnit, in-process unit tests for presentation behavior, descriptive test
	names, and full requirement-oriented test comments.
* `.github/instructions/playwright.instructions.md` applies if the redesign
	adds Playwright coverage. Use role/label/text locators and web-first
	assertions; do not use timing waits or brittle CSS selectors.
* The report itself follows the applicable generic Markdown instructions from
	the installed hve-core extension. The repository `docs.instructions.md` does
	not apply because this report is outside `docs/`.

## Implementation Implications

* The smallest change is presentation-only: replace the current `foreach`
	account articles with a `RadzenTabs` item for each account, retaining the
	retrieval metadata, access checks, refresh behavior, cursor navigation, and
	existing account detail `<dl>` inside each tab.
* Use a stable, distinguishable tab label such as account name with account ID
	fallback. Duplicate or blank names are plausible because account name is
	nullable at the web boundary, so account ID should remain present in the
	visible tab label or another accessible label.
* Track selection in the component as an index or account ID. Account ID is
	more resilient when a newer retrieval changes account order; reset selection
	safely when the selected account no longer exists. This state does not need
	an API, presenter, application, or database change.
* Use `@key` on repeated tab items or a dedicated reusable account-tab
	component if maintaining item identity or focus matters. The Radzen
	instructions explicitly require `@key` for collection rendering where
	identity or focus matters.
* Choose `RadzenTabs` server render mode by default for this detail-heavy
	content. The official Radzen example states that server mode renders only the
	selected tab, while client mode renders all tab content initially. This keeps
	a large account list from creating unnecessary initial markup.
* Preserve the page's existing `panel` frame and `platform-key-value-grid`
	rather than adding nested card surfaces or custom generated-markup CSS.
* Expand bUnit coverage with a two-or-more-account response that verifies the
	first visible tab, selection change, account field isolation, account-ID
	fallback, and selection reset/preservation across refresh and cursor loads.
	Add Playwright coverage only if the product requires browser-level keyboard
	interaction evidence.

## Alternatives

* **RadzenTabs (recommended):** Matches the requested individual-tab model,
	reuses the configured component library, and preserves each account's full
	detail view without new backend contracts.
* **Native accessible tabs:** Could minimize a new component dependency, but
	Radzen is already available and the repository guidance prefers it for common
	tab interactions. It would also require the team to own ARIA keyboard and
	focus behavior.
* **Accordion or `<details>` per account:** Consistent with the existing IG
	login history and better for seeing several accounts together, but it does
	not meet a literal individual-tabs requirement as directly.
* **Data grid plus a detail panel:** Appropriate if account counts become
	large and scanning/sorting is a core workflow. The current contract provides
	no evidence that this is necessary, and it adds selection semantics beyond
	the requested redesign.

## Follow-On Questions

* Should the tab label prioritize Account alias, Account name, or Account ID
	when all are present, and how should duplicate display names be disambiguated?
* Should a refresh or history navigation retain the same account ID when it is
	present in the newly loaded retrieval, or always select the first account?
* Is browser-level keyboard and responsive interaction coverage a requirement,
	or are bUnit component tests sufficient for this redesign?

## References

* [Radzen Tabs documentation](https://blazor.radzen.com/tabs) documents
	`RadzenTabs`, dynamic tab-item generation, selection binding, server/client
	render modes, and tab positioning.
* `.github/instructions/radzen-blazor.instructions.md`
* `.github/instructions/csharp.instructions.md`
* `.github/instructions/architecture.instructions.md`
* `.github/instructions/configuration.instructions.md`
* `.github/instructions/auth.instructions.md`
* `.github/instructions/dotnet-stack.instructions.md`
* `.github/instructions/tests.instructions.md`
* `.github/instructions/playwright.instructions.md`
