---
title: Radzen Tabs Reference Style Research
ms.date: 2026-08-08
ms.topic: research
---

## Scope
Investigate the cited Radzen Tabs reference, supplied visual direction, and current
Account details implementation. Record a Radzen.Blazor 10.3.2-compatible, page-local
CSS direction that uses the platform light and dark tokens without modifying product
files.

## Requested Visual
The cited reference and supplied screenshot establish navigation integrated with the
page surface:
* Labels are not individually filled or rounded controls
* A subtle baseline spans the available tab-list width
* Inactive and active tabs have transparent backgrounds
* Selection is an understated bottom indicator aligned to the baseline
* Tab content starts beneath navigation without another border, radius, fill, or shadow

The installed Software theme differs: its default tabs have connected backgrounds and
borders; the selected top-position tab has a 2px top border; and the panels container
has a background, border, shadow, and rounded lower corners.

## Workspace Evidence
* src/TNC.Trading.Platform.Web/Components/Pages/AccountDetails/AccountDetails.razor:27-60 renders retrieval metadata immediately before `RadzenTabs`, then each account in a nested `panel`. No wrapper currently scopes a page-specific tab style.
* src/TNC.Trading.Platform.Web/Components/Pages/AccountDetails/AccountDetails.razor:29 binds `SelectedIndex`, handles `Change`, and provides `AriaLabel="Account types"`. Preserve all three.
* src/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.csproj:10 pins `Radzen.Blazor` to 10.3.2.
* src/TNC.Trading.Platform.Web/wwwroot/app.css:1-35 defines light/dark semantic tokens: `--platform-border-color`, `--platform-text-color`, `--platform-muted-text-color`, and `--platform-accent-color`.
* src/TNC.Trading.Platform.Web/wwwroot/app.css:241-248 gives each `panel` a strong background, border, 1.25rem radius, shadow, and lower margin. This outer surface remains intentional; the tab treatment should not add a second nested frame.
* src/TNC.Trading.Platform.Web/wwwroot/app.css:295-321 owns key/value layouts, not the tab frame.
* test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountDetailsTests.cs:20-116 tests tab roles, tab-list name, selection, visible content, and selection retention. It does not test computed CSS or geometry.

## Official Findings
[Radzen Tabs](https://blazor.radzen.com/tabs) demonstrates the component and its
`TabPosition` option. Position changes the location of the full header strip; it does
not offer a baseline-plus-bottom-indicator style.

The [RadzenTabs API](https://blazor.radzen.com/api/tabs.md) confirms `SelectedIndex`,
`Change`, `AriaLabel`, `AriaLabelledBy`, and `TabPosition`. No public parameter or
documented variable moves the selected top border to the bottom edge.

Installed package evidence from C:/Users/martynfewtrell/.nuget/packages/radzen.blazor/10.3.2/staticwebassets/css/software.css confirms the relevant DOM classes: `.rz-tabview`, `.rz-tabview-top`, `.rz-tabview-nav`, `.rz-tabview-selected`, `.rz-tabview-panels`, and `.rz-tabview-panel`. The Software theme exposes `--rz-tabs-*` variables for backgrounds, border, shadow, colors, and padding. Its structural top-border selection treatment still requires narrowly scoped generated-markup selectors to become a bottom indicator.

## Amended Prior Recommendation
Amend .copilot-tracking/research/subagents/2026-08-08/account-details-layout-alternatives.md:

* Retain its `account-details-tabs` wrapper and adjacent `AccountDetails.razor.css` recommendation.
* Replace its earlier three tab rules with a complete local reset of the Radzen tab view, tab headers, and panels container. Resetting list items alone leaves the default panels frame visible.
* Use `--rz-tabs-*` values to remove the nested surfaces and retain normal Radzen spacing. Use `::deep` selectors only for the baseline, square tab edges, and selected bottom indicator, which variables cannot express.
* Do not use `TabPosition.Bottom`; it puts navigation after account content rather than styling the existing top-position navigation.
* Do not modify shared `app.css`, `.panel`, or global `.rz-tabview*` rules. The outer Account details panel stays as the only framed page surface.

## Exact CSS Direction
Wrap the existing component at src/TNC.Trading.Platform.Web/Components/Pages/AccountDetails/AccountDetails.razor:29-60:
```razor
<div class="account-details-tabs">
<RadzenTabs @bind-SelectedIndex="selectedIndex" Change="OnTabChanged" AriaLabel="Account types">
<Tabs>
@* Preserve the current dynamic RadzenTabsItem loop unchanged. *@
</Tabs>
</RadzenTabs>
</div>
```

Add this direction in the adjacent isolated stylesheet. It is deliberately local to
Radzen.Blazor 10.3.2 and its Software theme structure:
```css
.account-details-tabs ::deep .rz-tabview {
--rz-tabs-background-color: transparent;
--rz-tabs-border: 0;
--rz-tabs-shadow: none;
--rz-tabs-padding: 1.25rem 0 0;
--rz-tabs-tab-background-color: transparent;
--rz-tabs-tab-selected-background-color: transparent;
--rz-tabs-tab-hover-background-color: transparent;
--rz-tabs-tab-color: var(--platform-muted-text-color);
--rz-tabs-tab-selected-color: var(--platform-text-color);
--rz-tabs-tab-hover-color: var(--platform-text-color);
}
.account-details-tabs ::deep .rz-tabview-top > .rz-tabview-nav {
border-bottom: 1px solid var(--platform-border-color);
}
.account-details-tabs ::deep .rz-tabview-top > .rz-tabview-nav li {
background-color: transparent;
border: 0;
border-bottom: 2px solid transparent;
border-radius: 0;
}
.account-details-tabs ::deep .rz-tabview-top > .rz-tabview-nav .rz-tabview-selected {
border-bottom-color: var(--platform-accent-color);
margin-bottom: -1px;
}
.account-details-tabs ::deep .rz-tabview-panels {
background-color: transparent;
border: 0;
border-radius: 0;
box-shadow: none;
}

The transparent 2px bottom border on every tab stabilizes header height. The selected
tab moves down 1px so its 2px accent indicator overlaps the 1px shared baseline rather
than creating a double line. Radzen retains its normal padding and panel spacing.

## Accessibility Requirements
* Keep `RadzenTabs`, `@bind-SelectedIndex`, `Change`, `AriaLabel`, the existing dynamic item loop, and each `@key`.
* Preserve Radzen focus styles. Do not set `outline: none`, reset `--rz-tabs-tab-focus-*`, or remove `.rz-state-focused` distinction.
* Preserve non-color selected-state cues: the bottom indicator, selected text color, and Radzen-managed selected ARIA state. Do not reduce tab padding or hit targets to make the tabs quieter.
* Manually verify mouse selection, keyboard focus and activation/navigation supported by Radzen, refresh/history selection retention, and focus visibility in both platform themes.
* Test at the existing 960px responsive boundary. When labels need to wrap, scope the official demo pattern `ul[role=tablist] { flex-wrap: wrap; }` below `account-details-tabs`; do not shrink text or clip labels.

## Limits Of Reference Matching
The official demo documents component behavior and reflects its host theme. It does
not supply a portable design token set or parameter for this exact visual. The requested
screenshot also differs from this repository's Software light/dark theme and rounded
outer panels.

The recommendation matches the hierarchy, transparency, baseline, and restrained
selection indicator. It cannot guarantee pixel-identical colors, typography, hover
motion, border contrast, or spacing from another host theme without importing that
theme's tokens and broader styles. That would conflict with platform tokens and affect
unrelated Radzen controls. Visual review in both application themes remains decisive.

## Status And Validation
Status: Complete. No product files were modified.
When implementation begins, retain the focused test command:
```powershell
dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj --filter FullyQualifiedName~AccountDetailsTests
```
Add a narrow markup assertion for the wrapper, then manually verify desktop and <=960px
layouts in both themes. Confirm that the baseline spans the tab list, inactive tabs stay
unframed, the selected indicator is visible, focus remains visible, and the content has
no duplicate Radzen frame.

## Sources
* [Radzen Tabs demo](https://blazor.radzen.com/tabs)
* [RadzenTabs API reference](https://blazor.radzen.com/api/tabs.md)
* C:/Users/martynfewtrell/.nuget/packages/radzen.blazor/10.3.2/staticwebassets/css/software.css
* src/TNC.Trading.Platform.Web/Components/Pages/AccountDetails/AccountDetails.razor
* src/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.csproj
* src/TNC.Trading.Platform.Web/wwwroot/app.css
* test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountDetailsTests.cs
* .copilot-tracking/research/subagents/2026-08-08/account-details-layout-alternatives.md
