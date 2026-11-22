---
title: Account Details Tabbed Redesign Verification Research
description: Verification evidence for an implementation-ready Account Details tabbed redesign plan
ms.date: 2026-08-08
ms.topic: concept
---

## Research Scope

Verify the current Account Details Web implementation, focused tests, styling, project test configuration, and the supplied redesign research. Record exact implementation evidence, plan-critical deviations, likely validation commands, and unresolved questions for an implementation plan.

## Status

Complete.

## Verified Implementation Surface

* `src/TNC.Trading.Platform.Web/Components/Pages/AccountDetails/AccountDetails.razor` lines 1-13 define the Viewer-authorized `/account-details` route, page header, and Operator-only refresh action.
* Lines 15-22 retain distinct access, initial-loading, and no-retrieval states. The redesign must not move these states into a tab panel.
* Lines 24-33 render retrieval metadata first, then flatten every account in `retrieval.Accounts` into consecutive articles. The metadata currently sits inside the single outer `panel` at line 25.
* Lines 35-38 render the retrieval-history navigation after that outer panel, with the existing `Account details history` label and cursor/loading disabled rules.
* Line 41 independently renders an error again whenever a retrieval and error both exist. A failed refresh therefore produces two visible error paragraphs: the test only asserts that the text exists, not that it appears once.
* Lines 47-50 keep access-scope checks and initial load in the Razor `@code` block. No `AccountDetails.razor.cs` file exists; `AccountDetailsPagePresenter.cs` is the related backing implementation.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountDetails/AccountDetailsViewModels.cs` lines 3-29 already expose every requested identity, profile, transfer, currency, and financial field. No API, application, persistence, or view-model contract change is required for the tabbed layout.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountDetails/AccountDetailsPagePresenter.cs` lines 5-51 own snapshot loading, refresh, errors, and history cursor transitions. It has no account-type selection state. Keep tab selection component-local; do not add it to the presenter or API contract.
* Refresh at lines 30-51 replaces the retrieval, clears the older cursor, and retains the previous newer cursor. Selection reconciliation must run after refresh and after either history navigation result.

## Verified UI and Component Baseline

* `src/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.csproj` lines 8-17 reference `Radzen.Blazor` `10.3.2` on `net10.0`.
* `src/TNC.Trading.Platform.Web/Components/_Imports.razor` lines 8-9 import `Radzen` and `Radzen.Blazor`; `PlatformWebUiServiceCollectionExtensions.cs` line 13 registers Radzen services; `Components/App.razor` lines 11 and 16 load the Radzen theme and script. `Routes` uses `InteractiveServer` at line 14.
* Installed package XML documentation at `%USERPROFILE%/.nuget/packages/radzen.blazor/10.3.2/lib/net10.0/Radzen.Blazor.xml` lines 22041-22082 verifies `RadzenTabs` supports `RenderMode`, `TabPosition`, `@bind-SelectedIndex`, and `Change`. Its default server mode renders only the selected item; this matches the small snapshot assumption and makes active-panel tests direct.
* `src/TNC.Trading.Platform.Web/wwwroot/app.css` lines 241-248 define the shared `panel` surface. Lines 295-308 define the two-column label/value `platform-key-value-grid`, and lines 500-502 stack it below 960px. No Account Details-specific selectors exist.

## Focused Test Evidence

* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountDetailsTests.cs` has three tests only:
  * Lines 9-16 verify the no-retrieval empty state.
  * Lines 21-39 verify one `CFD` account, visible account fields, no Operator refresh button for a Viewer, and older/newer disabled state.
  * Lines 44-61 verify a failed Operator refresh leaves the saved retrieval visible and exposes an error.
* Actual values in the focused Web fixtures are `Status = "Enabled"`, `AccountType = "CFD"`, and triggers `Manual` and `Automatic` at lines 23 and 44. The focused suite has no multi-account, multi-type, casing, blank-type, selection, or status-badge assertions.
* Related account-details tests show status casing is not stable across layers: application fixture `ENABLED` at `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/AccountDetails/AccountDetailsHandlerTests.cs` line 155; IG gateway payloads `ENABLED` at `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/Integrations/Ig/IgAccountDetailsGatewayTests.cs` lines 47-48 and 73; API mapping fixture `Active` at `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/AccountDetailsEndpointMappingTests.cs` line 102. All inspected account-details fixtures use account type `CFD`.
* `PlatformComponentTestContext.cs` lines 41 and 48-49 set bUnit JS interop to loose mode and register Radzen services. The existing context supports focused Radzen tab tests without new test infrastructure.
* The focused class was executed successfully on 2026-08-08: 3 passed, 0 failed.

## Plan-Critical Deviations From Supplied Research

* The research correctly identifies the existing outer panel, but its proposed wording that every account uses an "existing panel surface" is not current behavior. `AccountDetails.razor` line 27 renders plain articles inside one retrieval-level panel. Do not add `panel` to every account while retaining the outer panel, because that creates nested panel surfaces. The implementation plan should place metadata in its own top-level panel or unframed metadata region, place `RadzenTabs` outside it, and make each repeated account article the only panel surface in its tab.
* The research proposes a status badge but does not account for the verified `Enabled`/`ENABLED`/`Active` variation. A local helper must use ordinal case-insensitive comparisons and return a neutral `Info` badge style for every unrecognized value. The product owner must confirm which statuses, if any, carry success, warning, or danger meaning before semantic colors are assigned.
* The research recommends retaining the selected normalized account type after snapshot replacement. The current page has no selection state, and the presenter changes state after refresh, older, and newer loads. Plan local selection reconciliation after every rendered retrieval change, without changing the presenter contract.
* The research suggests a defensive `Other` tab and alphabetical account-type ordering. Neither behavior is currently asserted or defined by a business contract. Include both only after confirming the desired business order and malformed-value display. The source model types `AccountType` as non-nullable, but the UI can still defensively normalize whitespace.
* The research does not identify the duplicate refresh-error markup at lines 35 and 41. The redesign should either preserve it deliberately or consolidate it to one error location and add an exact-count test. This is a small adjacent correction, not a prerequisite for tabs.

## Implementation-Ready Plan

1. Change only `src/TNC.Trading.Platform.Web/Components/Pages/AccountDetails/AccountDetails.razor` and `src/TNC.Trading.Platform.Web/wwwroot/app.css`, unless a small page-local C# helper file is needed to keep grouping and badge mapping readable. Keep `AccountDetailsPagePresenter`, view models, API, application, and persistence unchanged.
2. In the component, derive a local ordered collection of normalized account-type groups from the loaded retrieval and retain selection by normalized type key. Bind the corresponding group index to `RadzenTabs.SelectedIndex`; when a new retrieval no longer contains that key, select the first group.
3. Keep retrieval metadata before `RadzenTabs` and history navigation after it. Do not place the navigation inside a `RadzenTabsItem`.
4. Add one `RadzenTabsItem` per group with `@key` on the normalized account-type key. Render every account in the active type as a single `article.panel`, avoiding any enclosing account-content panel.
5. Put Account ID, Alias, and Status in a dedicated header grid at the top of each account article. Place Account type, Preferred, Transfer from, and Transfer to in the left responsive column; place Currency, Balance, Deposit, Profit/loss, and Available in the right column. Reuse `platform-key-value-grid` for label/value formatting.
6. Add narrowly scoped CSS only for the three-item account identity header and its narrow-screen stack. Retain shared panel, color, spacing, tab, and key-value-grid rules; do not target Radzen-generated markup.
7. Extend `AccountDetailsTests.cs` with documented bUnit tests for two accounts in one type and one in another, active-tab-only account rendering, required field placement/content, refresh/history selection retention and fallback, metadata/navigation position, and the chosen error-count behavior. Use role, visible text, or stable test IDs instead of Radzen internal classes.

## Validation Commands

Run the focused class during implementation:

```powershell
dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj --filter "FullyQualifiedName~AccountDetailsTests"
```

Run the full Web unit-test project before completing the slice:

```powershell
dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj
```

## Remaining Uncertainties

* Confirm the desired business ordering of account types. The verified fixtures provide only `CFD`; alphabetical ordering and a fallback-last `Other` group remain a reasonable UI proposal, not a confirmed rule.
* Confirm the official semantic meanings of all IG account statuses before mapping badge colors. Known fixture spellings are `Enabled`, `ENABLED`, and `Active` only.
* Confirm whether selecting an account type must survive browser navigation or deep links. The current research assumes in-page, non-bookmarkable selection, which is sufficient for a component-local key.
* Confirm whether the duplicate refresh error is intentionally visible. The implementation should document the decision in an assertion.

## Follow-On Questions

* What is the required business display order for `CFD`, `Spreadbet`, and any other account types?
* Which account statuses should use success, warning, danger, or neutral presentation?
