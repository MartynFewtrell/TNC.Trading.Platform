---
title: Radzen Account Details Tabs Research
description: Evidence-backed research on a professional tabbed Account Details redesign using free Radzen Blazor Components.
author: GitHub Copilot
ms.date: 2026-08-08
ms.topic: concept
keywords:
  - radzen
  - blazor
  - tabs
  - account details
  - accessibility
---

## Research Scope

Research the official Free Radzen Blazor Components documentation and the local web project to determine the best professional Account Details redesign for:

* One tab per account type or account record
* Page-level tabs or link navigation
* A responsive two-column panel layout
* Status display and accessibility
* Free component availability, authoritative URLs, and concise implementation examples

## Status

Complete. Official documentation, the local implementation surface, and the
installed Radzen 10.3.2 package API were checked on 2026-08-08.

## Evidence

### Free component availability

* Radzen Blazor Components are free, open source, MIT licensed, and free for
  commercial use. Radzen lists Tabs, Link, Panel, Card, Stack, Row/Column, and
  Badge among its free components: [Radzen Blazor GitHub README](https://github.com/radzenhq/radzen-blazor).
* The repository already references `Radzen.Blazor` 10.3.2 in
  `src/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.csproj` and has the
  required namespaces, theme, JavaScript, and `AddRadzenComponents()` setup in
  the web application.
* Radzen's [Get started](https://blazor.radzen.com/get-started) guide confirms
  the project setup pattern and requires interactive rendering for interactive
  controls. The web application uses `InteractiveServer`.

### Tabs and navigation

* [RadzenTabs](https://blazor.radzen.com/tabs) organizes content into tabbed
  panels, supports dynamic tab items, positions, and server or client render
  modes. Server render mode renders only the selected panel; client mode
  renders every panel initially and switches with JavaScript.
* The [RadzenTabs API](https://blazor.radzen.com/api/tabs.md) documents
  `@bind-SelectedIndex`, `Change`, `RenderMode`, `AriaLabel`,
  `AriaLabelledBy`, and `Tabs`. These directly support dynamically generated
  account tabs and an accessible tab list.
* The installed 10.3.2 XML documentation confirms `SelectedIndex`,
  `SelectedIndexChanged`, `RenderMode`, `RadzenTabsItem.Text`, and
  `RadzenTabsItem.Icon`. It does not document `AriaLabel` or
  `AriaLabelledBy`; those current-online API parameters must not be used until
  the package is upgraded and verified.
* [RadzenLink](https://blazor.radzen.com/link) provides route-aware internal
  links via `Path`, with `Match` for active-state matching. It is appropriate
  only when the active account must have a bookmarkable URL or independent
  browser history entry; it is not necessary for switching among accounts in
  one loaded retrieval.

### Layout and status

* [RadzenRow](https://blazor.radzen.com/row) and
  [RadzenColumn](https://blazor.radzen.com/column) provide a responsive
  12-column grid. `Size="12" SizeMD="6"` on each column produces one column
  below the medium breakpoint and two columns at and above it.
* [RadzenBadge](https://blazor.radzen.com/badge) is intended for labels and
  status indicators. Its API exposes semantic `BadgeStyle` values such as
  `Success`, `Warning`, and `Danger`, plus `Text`, `Variant`, and `Shade`.
* The existing page uses `platform-key-value-grid`; repository CSS already
  collapses it to one column on narrow screens. Retain it within each new
  Radzen column instead of restyling definition-list semantics.
* The installed 10.3.2 XML documentation confirms `RadzenColumn.Size` and
  `SizeMD`, `RadzenBadge.BadgeStyle` and `Text`, and `RadzenLink.Path` and
  `Match`.

### Accessibility

* Radzen's [Accessibility](https://blazor.radzen.com/accessibility) guidance
  says components provide ARIA, semantic HTML, responsive behavior, and
  keyboard compatibility, but the application still must provide labels,
  suitable configuration, contrast, and testing. It documents optional WCAG
  AA theme colors.
* The [WAI-ARIA Tabs Pattern](https://www.w3.org/WAI/ARIA/apg/patterns/tabs/)
  requires a labelled tab list, associated tabs and panels, and keyboard
  operation. It recommends automatic activation only when content appears
  without noticeable latency.

### Local implementation surface

* `src/TNC.Trading.Platform.Web/Components/Pages/AccountDetails/AccountDetails.razor`
  presently renders every account in a repeated `<article>` and semantic
  `<dl>`. `AccountDetailsAccountViewModel` has stable `AccountId`, display
  name/alias, account type, status, balances, currency, and transfer flags.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountDetailsTests.cs`
  validates displayed fields and refresh/history behavior, but does not yet
  verify tabs, keyboard use, or narrow-screen layout.

## Evaluated Approaches

### Dynamic RadzenTabs

Recommended for the currently loaded account snapshot. Generate one
`RadzenTabsItem` for each `AccountDetailsAccountViewModel`, use `AccountId` as
the Razor `@key`, and bind the selected index. This keeps selection on one page
and follows the existing retrieval/history model. The account list is already
loaded, so client render mode makes selection immediate and avoids an
Interactive Server round trip. It is appropriate while the account count stays
small.

### Route-backed RadzenLink navigation

Use only when an account must have a shareable, bookmarkable URL or independent
browser history entry. This would require a route design such as
`/account-details/{accountId}` and state restoration when the active retrieval
changes. The existing page treats a retrieval as one snapshot and has no such
requirement, so this adds complexity without a present user benefit.

### One long page or collapsible panels

Not recommended as the primary redesign. The current repeated articles make it
hard to scan one account at a time, while accordion-style expansion keeps many
account headers visible but does not serve the requested account-switching
workflow. A panel remains useful within the selected tab to group related
fields.

## Recommendation

Use top-positioned `RadzenTabs` with one tab per account record. Use a concise,
unique tab label in this order: `AccountName`, `AccountAlias`, then `AccountId`.
In each selected tab, use a `RadzenRow` with two `RadzenColumn` values set to
`Size="12" SizeMD="6"`. The first column should contain an Account profile
panel (identifier, type, currency, preferred and transfer permissions); the
second should contain a Balances panel (balance, deposit, profit/loss, and
available funds). On narrow screens the columns stack naturally.

Render a textual status badge in the profile panel. Map known healthy,
attention, and unavailable statuses to `Success`, `Warning`, and `Danger`
respectively, but keep the status word as text and map unknown statuses to
`Info`; color alone must not carry meaning. Do not make account tabs draggable
or closable because their order and existence are controlled by the saved
retrieval.

For the known small account collection, select
`RenderMode="TabRenderMode.Client"` so keyboard selection does not wait on a
server round trip. Re-evaluate this if snapshots can contain many accounts or
the panels become data-heavy; in that case use server render mode, which renders
only the active panel.

## Concise Implementation Example

The following uses only parameters confirmed in the installed 10.3.2 package.
It is a design sketch, not a proposed product-code change.

```razor
<h2>Accounts</h2>
<RadzenTabs @bind-SelectedIndex="selectedAccountIndex"
            RenderMode="TabRenderMode.Client">
    <Tabs>
        @foreach (var account in retrieval.Accounts)
        {
            <RadzenTabsItem Text="@(account.AccountName ?? account.AccountAlias ?? account.AccountId)"
                            @key="account.AccountId">
                <RadzenRow Gap="1rem" RowGap="1rem">
                    <RadzenColumn Size="12" SizeMD="6">
                        <RadzenPanel>
                            <HeaderTemplate><h3>Account profile</h3></HeaderTemplate>
                            <ChildContent>
                                <RadzenBadge BadgeStyle="@GetStatusStyle(account.Status)"
                                             Text="@account.Status" />
                                <dl class="platform-key-value-grid">
                                    <dt>Account ID</dt><dd>@account.AccountId</dd>
                                    <dt>Account type</dt><dd>@account.AccountType</dd>
                                    <dt>Currency</dt><dd>@account.Currency</dd>
                                </dl>
                            </ChildContent>
                        </RadzenPanel>
                    </RadzenColumn>
                    <RadzenColumn Size="12" SizeMD="6">
                        <RadzenPanel>
                            <HeaderTemplate><h3>Balances</h3></HeaderTemplate>
                            <ChildContent>
                                <dl class="platform-key-value-grid">
                                    <dt>Balance</dt><dd>@account.Balance</dd>
                                    <dt>Profit/loss</dt><dd>@account.ProfitLoss</dd>
                                    <dt>Available</dt><dd>@account.Available</dd>
                                </dl>
                            </ChildContent>
                        </RadzenPanel>
                    </RadzenColumn>
                </RadzenRow>
            </RadzenTabsItem>
        }
    </Tabs>
</RadzenTabs>
```

## Accessibility and Validation Requirements

* Verify the rendered 10.3.2 output has a labelled `tablist`, tab-to-panel
  associations, and appropriate `aria-selected` state. The current online
  `AriaLabel` and `AriaLabelledBy` parameters are not available in the
  installed-package documentation, so confirm the supported 10.3.2 mechanism
  before adding a custom label or upgrade Radzen first.
* Test keyboard access using Tab, Left Arrow, Right Arrow, Home, End, Space,
  and Enter against the WAI-ARIA tab pattern. Ensure focus is visible and the
  selected account panel is reachable.
* Keep headings in logical order: page `h1`, visible Accounts `h2`, and panel
  `h3` headings. Retain the semantic definition lists for label/value pairs.
* Test status semantics at normal and high zoom and with a screen reader. The
  badge's text must remain meaningful without its color.
* Add focused markup tests for a multiple-account snapshot, selected-tab
  change, retained account field coverage, and error/refresh behavior. Add
  browser-level responsive checks at a width below and above Radzen's medium
  breakpoint.

## Authoritative References

* [Radzen Blazor Components README and MIT license](https://github.com/radzenhq/radzen-blazor)
* [Radzen Get started](https://blazor.radzen.com/get-started)
* [Radzen Tabs demo](https://blazor.radzen.com/tabs)
* [Radzen Tabs API](https://blazor.radzen.com/api/tabs.md)
* [Radzen Link demo](https://blazor.radzen.com/link)
* [Radzen Row and Column demos](https://blazor.radzen.com/row)
* [Radzen Column demo](https://blazor.radzen.com/column)
* [Radzen Badge demo](https://blazor.radzen.com/badge)
* [Radzen Accessibility](https://blazor.radzen.com/accessibility)
* [WAI-ARIA Tabs Pattern](https://www.w3.org/WAI/ARIA/apg/patterns/tabs/)

## Open Questions

* Does the account-details workflow require direct links to a specific account,
  or is one selected account within the current retrieval sufficient?
* What exact account-status vocabulary does IG return in saved snapshots? This
  determines the final `BadgeStyle` mapping.
* Before implementation, inspect the 10.3.2 rendered tab markup to determine
  its supported tab-list labelling mechanism, or upgrade and validate the
  current `AriaLabel`/`AriaLabelledBy` API.
