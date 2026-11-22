---
title: Account Details Layout Alternatives
ms.date: 2026-08-08
ms.topic: research
---

## Scope

Evaluate implementation alternatives for four requested Account details changes:

* Increase the separation between Trigger and the account-type tabs
* Present Account ID, Alias, and Status as label/value pairs in an identity/status row
* Add separation before the Account type and Currency detail groups
* Render a page-local baseline/underline visual for account-type tabs

## Confirmed Workspace Evidence

* `src/TNC.Trading.Platform.Web/Components/Pages/AccountDetails/AccountDetails.razor:27-29` renders the retrieval metadata `<dl>` immediately before `RadzenTabs`. Trigger is the final metadata entry, so this markup boundary controls the requested first gap.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountDetails/AccountDetails.razor:36-53` renders every account as a nested `panel`, with the identity/status `<dl>` followed immediately by a `RadzenRow` containing Account type and Currency groups.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountDetails/AccountDetails.razor:38-42` provides a dedicated class on the identity/status `<dl>` but has no class on the surrounding account article, tabs, retrieval metadata, or details row.
* `src/TNC.Trading.Platform.Web/wwwroot/app.css:295-317` defines the reusable two-column `.platform-key-value-grid` and the Account details identity grid. The identity grid currently uses three equal columns, so labels sit above values.
* `src/TNC.Trading.Platform.Web/wwwroot/app.css:502-516` collapses both grid types to a single column below 960px. The current responsive behavior can be retained by extending the existing Account details selector.
* `src/TNC.Trading.Platform.Web/Components/App.razor:9-16` loads the global `app.css` before the shared Radzen theme. `PlatformThemeState` selects `software` and `software-dark` in `src/TNC.Trading.Platform.Web/Components/Layout/PlatformThemeState.cs:73-75`.
* `src/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.csproj:10` pins `Radzen.Blazor` to 10.3.2. The current Tabs declaration only uses the supported behavior parameters already present in source: `SelectedIndex`, `Change`, and `AriaLabel` (`AccountDetails.razor:29`).
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountDetailsTests.cs:20-65` asserts account-type tab roles, the accessible tab-list name, selection behavior, and current content visibility. It contains no CSS or viewport assertions.

## Findings

The Account details page has a small, self-contained layout surface. The outer
retrieval panel and nested account panels are already semantic containers, and the
identity/status fields already have a dedicated `<dl>` class. The needed structural
addition is only a class on the retrieval metadata and a wrapper around `RadzenTabs`.

The installed local package metadata at
`C:/Users/martynfewtrell/.nuget/packages/radzen.blazor/10.3.2/lib/net10.0/Radzen.Blazor.xml:22048-22128`
confirms `RenderMode`, `TabPosition`, `SelectedIndex`, `Change`, `AllowReorder`, and
`RadzenTabsItem` template/style-related members. It does not expose a Tabs parameter
that selects a baseline or bottom-underline visual.

The installed active theme assets are
`C:/Users/martynfewtrell/.nuget/packages/radzen.blazor/10.3.2/staticwebassets/css/software.css`
and `software-dark.css`. Their shared tabs variables include `--rz-tabs-padding`,
`--rz-tabs-tab-selected-color`, and `--rz-tabs-tab-selected-top-border-color`.
Their generated tabs structure uses `.rz-tabview-top > .rz-tabview-nav`,
`.rz-tabview-nav li`, and `.rz-tabview-selected`; in top position, the selected visual
is a top border. A variable override can change its color but cannot relocate it to a
bottom underline or add a full-width baseline.

### Alternative A: Small Markup Change Plus Isolated Page CSS

Feasibility: high. Add `account-details-retrieval` to the retrieval metadata at
`AccountDetails.razor:28`, wrap the existing `RadzenTabs` at `AccountDetails.razor:29-60`
in an `account-details-tabs` `<div>`, and add
`Components/Pages/AccountDetails/AccountDetails.razor.css`. The wrapper limits the
two Radzen generated-markup selectors to this page; ordinary layout styling uses
existing semantic elements and named classes.

This approach satisfies all four changes. It places spacing directly at the metadata
and identity/detail boundaries, changes each existing identity `<div>` to a
label/value mini-grid, and creates the tab baseline and selected underline from the
version-pinned local Radzen class names. The mobile media rule must repeat the
existing 960px breakpoint because CSS-isolation selectors are more specific than the
global rule at `app.css:502-505`.

Accessibility and theme responsiveness: preserves `RadzenTabs`, its existing
`AriaLabel`, roles, keyboard behavior, `Change` callback, and focus treatment. Use
`var(--platform-border-color)` and `var(--platform-accent-color)`, which are defined
for both light and dark modes at `app.css:1-35`; do not override Radzen focus styles.
The tab selectors are an intentional narrow exception because no component parameter
or theme variable produces the required visual. This otherwise follows the Radzen
instruction to prefer component behavior and CSS variables before generated-markup
overrides.

Risks: this is the first isolated page stylesheet in the Web project, and the
`::deep` selectors depend on the Radzen 10.3.2 DOM classes. The dependency is bounded
by the package pin at `TNC.Trading.Platform.Web.csproj:10` and must be rechecked when
that package changes.

### Alternative B: CSS-Only Changes to Existing Grids

Feasibility: partial. Global rules at `app.css:295-317` can change the identity
layout by setting `.account-details-account-header > div` to a two-column grid and
can add a bottom margin to the existing header. They cannot target only the retrieval
metadata because it shares `.platform-key-value-grid` with unrelated views.

Adding a sibling combinator or positional selector to provide the Trigger-to-tabs gap
would couple the rule to the current DOM order. It cannot create a controlled tabs
baseline without the same Radzen generated-markup selectors required by Alternative
A. Altering `.platform-key-value-grid` would also change the two Account type/Currency
columns and other pages using that shared class.

Accessibility and theme responsiveness: the existing 960px grid collapse survives,
but global changes have an unnecessary broad visual blast radius. This does not align
with the instruction to keep shared visual decisions centralized while avoiding
unrelated page regressions; it is rejected.

### Alternative C: Radzen Tabs Parameters or Theme Variable Override

Feasibility: partial. `TabPosition` can move headers, but `TabPosition.Bottom` puts
the tabs after the panels rather than retaining top-position tabs with a bottom
underline. `RenderMode`, `SelectedIndex`, `Change`, and item templates change behavior
or content but do not expose the requested baseline treatment. The current parameters
at `AccountDetails.razor:29` are sufficient for interaction behavior.

The `--rz-tabs-*` variables from the active software themes can adjust padding and
selected colors locally, which could refine spacing or color. They cannot add the
baseline or change the top-position selected border to a bottom underline. A global
theme-variable override would also affect every Radzen Tabs instance, contrary to the
requested page-local visual.

Accessibility and theme responsiveness: parameters and variables are the lowest-risk
options for keyboard behavior and both themes. They align with Radzen guidance, but
they do not fully meet the visual requirement, so they are rejected as the complete
solution. Retain existing Radzen behavior and platform variables within Alternative
A instead.

### Alternative D: Global Generated-Markup CSS Override

Feasibility: high but undesirable. `app.css` can target the confirmed Radzen classes
`.rz-tabview-top > .rz-tabview-nav`, `.rz-tabview-nav li`, and
`.rz-tabview-selected` to create the desired baseline and underline.

Risks: the override applies to future and unrelated Radzen Tabs, competes with the
shared `software` and `software-dark` theme, and depends on internal rendered markup.
It violates the Radzen instruction against fragile generated-markup selectors when
supported alternatives exist. Alternative A narrows the same unavoidable dependency
to the account page and uses variables for theme-sensitive color, so this global form
is rejected.

## Recommendation

Select Alternative A: a small Account details markup change plus
`AccountDetails.razor.css`. It is the only option that addresses all four requested
layout changes without changing shared grid behavior or all Radzen Tabs. Keep the
existing `RadzenTabs` parameters and behavior unchanged.

Proposed markup at `AccountDetails.razor:28-60`, derived from the current page:

```razor
<dl class="platform-key-value-grid account-details-retrieval">
  <dt>Retrieved at</dt><dd>@retrieval.RetrievedAtUtc.ToLocalTime().ToString("g")</dd>
  <dt>Trading day</dt><dd>@retrieval.TradingDay</dd>
  <dt>Trigger</dt><dd>@retrieval.TriggerSource</dd>
</dl>
<div class="account-details-tabs">
  <RadzenTabs @bind-SelectedIndex="selectedIndex" Change="OnTabChanged" AriaLabel="Account types">
    <Tabs>
      @* Preserve the existing accountTypeGroups loop and tab items unchanged. *@
    </Tabs>
  </RadzenTabs>
</div>
```

Proposed `AccountDetails.razor.css`, derived from the current shared spacing,
breakpoint, and theme tokens:

```css
.account-details-retrieval {
  margin-bottom: 1.5rem;
}

.account-details-account-header {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 0.75rem 1rem;
  margin: 0 0 1.5rem;
}

.account-details-account-header > div {
  display: grid;
  grid-template-columns: max-content minmax(0, 1fr);
  align-items: center;
  column-gap: 0.5rem;
}

.account-details-tabs ::deep .rz-tabview-top > .rz-tabview-nav {
  border-bottom: 1px solid var(--platform-border-color);
}

.account-details-tabs ::deep .rz-tabview-top > .rz-tabview-nav li {
  background: transparent;
  border: 0;
  border-radius: 0;
}

.account-details-tabs ::deep .rz-tabview-top > .rz-tabview-nav .rz-tabview-selected {
  border-bottom: 2px solid var(--platform-accent-color);
  margin-bottom: -1px;
}

@media (max-width: 960px) {
  .account-details-account-header {
    grid-template-columns: 1fr;
  }
}
```

The account header's existing `dd { margin: 0; }` rule at `app.css:317` remains
valid. The proposed page stylesheet intentionally duplicates only the identity
breakpoint needed to preserve its mobile behavior after isolation specificity is
introduced.

## Validation

Run the focused existing bUnit tests in
`test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountDetailsTests.cs`.
They should continue to prove the tab-list name, tab roles, visible tab content,
selection retention, refresh behavior, and history navigation. Add one narrow markup
assertion that the retrieval `<dl>` has `account-details-retrieval` and that the tabs
wrapper exists; no behavior test is needed for spacing itself.

Perform manual visual checks in the interactive Web app at desktop width above 960px
and a viewport at or below 960px:

* Confirm a visible gap separates Trigger from the tab list
* Confirm Account ID, Alias, and Status each read horizontally as label then value
* Confirm the identity/status row is separated from Account type and Currency details
* Confirm the tab list has a full baseline and only the selected tab has the accent
  underline
* Switch between light and dark modes and confirm border, underline, text, hover,
  focus, keyboard tab activation, and selected-tab visibility remain legible
* Select another account type, refresh, then navigate older/newer to confirm the
  existing selection behavior remains unchanged

Run the Web unit-test project after the markup change. A browser-based visual test is
not present in the current source evidence, so manual responsive/theme verification is
required unless a Playwright test is intentionally added.

## Open Issues

* The requested numeric values for the three visual gaps are not specified. The
  snippets use the established 1.5rem panel rhythm and 0.5rem inline label/value gap;
  product/design input can adjust those values.
* The nested `article` retains the shared `panel` padding, border radius, and bottom
  margin from `app.css:241-247`. This research does not change that broader card
  treatment.
* The two `::deep` Radzen selectors must be visually revalidated whenever
  `Radzen.Blazor` changes from 10.3.2.
