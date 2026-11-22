---
title: Account Type Tabs Correction Research
description: Evidence-backed implementation implications for grouping Account Details records by account type.
author: GitHub Copilot
ms.date: 2026-08-08
ms.topic: concept
keywords:
  - account details
  - account type
  - Radzen Blazor
  - tabs
---

## Research Scope

* Establish the consequences of displaying one Account Details tab per account type, rather than per account record
* Verify label, fallback, ordering, selection-preservation, and bottom-pagination behavior
* Evaluate free Radzen components available in the installed 10.3.2 package
* Identify concise, evidence-backed tests without changing product code

## Status

Complete. The prior per-account-tabs recommendation is superseded by the
account-type grouping approach below.

## Evidence

* `src/TNC.Trading.Platform.Web/Components/Pages/AccountDetails/AccountDetails.razor:26-40`
  renders every `retrieval.Accounts` record sequentially inside one panel, then
  renders the Older/Newer navigation after that panel. The page has no account
  or type selection state.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountDetails/AccountDetailsViewModels.cs:9-29`
  provides a flat `IReadOnlyList<AccountDetailsAccountViewModel>` and exposes
  `AccountType` on each account record. It has no grouped presentation model.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountDetails/AccountDetailsPagePresenter.cs:10-24`
  replaces `State` when it loads a retrieval. `LoadOlderAsync` and
  `LoadNewerAsync` delegate to that same replacement path (lines 26-28), and
  `RefreshAsync` replaces the retrieval with the saved refresh result
  (lines 30-49).
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfAccountDetailsSnapshotStore.cs:61-64`
  orders persisted accounts only by `AccountId`. It does not establish account
  type order.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgAccountDetailsGateway.cs:69-76`
  rejects a missing or whitespace `AccountType` before successful captures are
  saved. The fallback below is therefore defensive for malformed/legacy UI
  input, not a normal IG response path.
* `src/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.csproj:8-17`
  references free `Radzen.Blazor` version `10.3.2`. Its installed
  `net10.0/Radzen.Blazor.xml` documents `RadzenTabs.RenderMode`,
  `SelectedIndex`, `SelectedIndexChanged`, `Change`, and
  `RadzenTabsItem.Text`, as well as `RadzenRow` and `RadzenColumn` responsive
  sizing parameters.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/PlatformComponentTestContext.cs:29-48`
  registers Radzen and uses loose JS interop. The focused page tests only cover
  empty, one-account, and refresh-failure states in
  `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountDetailsTests.cs:9-61`.
* `.copilot-tracking/research/2026-08-08/account-details-tabbed-redesign-research.md:17-22`
  explicitly assumed one tab per account record. Its component selection is
  useful, but its grouping, labels, and selection recommendations no longer
  match the corrected requirement.

## Selected Approach

Use one `RadzenTabsItem` per distinct normalized account type. The selected
type panel renders all accounts in that type, retaining one semantic `article`
per account and the current account fields. Retrieval metadata remains above
the tabs. The existing history `<nav>` remains after the tab control and is not
placed inside an item panel.

`RadzenTabs` is the appropriate free 10.3.2 control: it owns tab roles,
keyboard behavior, selected-index events, and item rendering. Use its default
server render mode rather than adding client-only tab behavior. This page is
already Interactive Server, selection must reconcile after retrieval changes,
and server selection is directly testable by the existing bUnit host. The
number of account types is expected to be small and no evidence supports a
grid, paging, filtering, or an accordion workflow.

Do not carry selection into the API, application, persistence, cursor, or URL
contracts. It is ephemeral page state, distinct from the immutable retrieval
and its account children.

## Type Grouping, Labels, And Order

Construct a page-local list of type groups from the active retrieval before
rendering tabs. Each group contains a stable normalized key, a visible label,
and the accounts to render in that panel.

* Normalize with `AccountType.Trim()` and group keys using
  `StringComparer.OrdinalIgnoreCase`. This prevents casing-only provider
  differences from producing duplicate logical type tabs.
* Use the first nonblank trimmed source value as the visible label for a valid
  group. Expected examples render exactly as `CFD` and `Spreadbet`; do not add
  account IDs, aliases, or counts to type-tab labels.
* Map null, empty, or whitespace values to an `Other` fallback group. This is
  defensive because the current IG gateway rejects such data. Keep its key
  distinct from a literal provider-supplied `Other` type if that distinction is
  required by future broker data; no current type vocabulary answers that
  policy question.
* Sort valid groups by label using ordinal case-insensitive comparison, with a
  deterministic ordinal tie-breaker. Put the fallback group last. This avoids
  inheriting the persistence layer's account-ID ordering.
* Preserve the incoming account order inside each group. Persisted historical
  retrievals are already deterministic by account ID; a just-refreshed result
  retains the provider response order until its next read.

The grouping helper should be pure and independently testable. It can be a
small internal presentation helper or an internal page-local method; no domain
type or database migration is warranted.

## Selected Type Across Retrieval Changes

Store the selected normalized type key, not a tab index and not an account ID.
An index becomes invalid when a refresh or history retrieval has a different
set or order of types.

* On initial retrieval, select the first ordered type group.
* On tab selection, translate Radzen's selected index to that group's normalized
  key.
* When `Presenter.State.Retrieval.RetrievalId` changes after refresh, Older, or
  Newer, retain the selected type only when a matching normalized key exists in
  the newly calculated groups.
* When the prior type is absent, select the first ordered type and replace the
  stored key. This prevents a missing type from being resurrected when the user
  later revisits a retrieval where it happened to exist.
* A refresh failure leaves `Presenter.State` unchanged
  (`AccountDetailsPagePresenter.cs:39-42`), so it must leave selected-type
  state unchanged as well.

The page can calculate `SelectedIndex` from the selected type key on every
render and handle `SelectedIndexChanged` to store the new key. Reconcile the
stored key when the retrieval ID changes, rather than mutating presenter state
or relying on `@key` for business selection. `@key` may still be applied to
tab items using the normalized type key to preserve Blazor element identity.

## Retrieval Navigation Placement

Keep the structure in this order:

```text
retrieval metadata
RadzenTabs
  one item per AccountType
    one article per account in that AccountType
history error, when present
Older/Newer navigation
```

The history navigation must remain a sibling after `RadzenTabs`, outside every
`RadzenTabsItem`. This retains the current bottom-of-page reading and focus
order, gives Older/Newer one retrieval-wide meaning, and prevents it from
moving when a type panel has a different number of account records. Retain the
existing accessible label, `Account details history`, and existing disabled
cursor conditions. Add only page-level spacing/alignment if needed; do not
target generated Radzen tab markup with CSS.

## Recommended Tests

Add focused xUnit/bUnit tests in
`test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountDetailsTests.cs`.
Each test should retain the repository's XML requirement-traceability comment
and `MethodName_ShouldExpectedResult_WhenState` naming convention.

* `Render_ShouldCreateOneTypeTabAndRenderAllMatchingAccounts_WhenRetrievalHasDuplicateAccountTypes`:
  supply two CFD accounts and one Spreadbet account. Assert exactly two type
  labels, two CFD account articles in the CFD panel, and no third per-account
  tab.
* `Render_ShouldGroupCaseVariantsAndPlaceFallbackLast_WhenAccountTypesAreInconsistent`:
  cover `CFD`/`cfd`, whitespace, and a normal type. Assert one CFD tab,
  `Other` as the final fallback tab, and deterministic valid-type order. This
  is best targeted at the pure grouping helper if one is extracted.
* `SelectType_ShouldRenderOnlySelectedTypeAccounts_WhenAnotherTabIsActivated`:
  activate Spreadbet through the rendered tab control and assert its account
  records render in the active panel while CFD records are not active. Use the
  Radzen semantics emitted by 10.3.2 rather than CSS-class internals as the
  primary locator.
* `Refresh_ShouldRetainSelectedType_WhenRefreshedRetrievalContainsThatType`:
  select Spreadbet, return a successful refresh containing CFD and Spreadbet,
  and assert Spreadbet remains selected.
* `LoadOlder_ShouldSelectFirstType_WhenPreviouslySelectedTypeIsAbsent`:
  select Spreadbet, load an older retrieval with only CFD, and assert CFD is
  selected. Follow with Newer and assert CFD remains selected, proving the
  fallback replaced stale type selection.
* `Render_ShouldKeepHistoryNavigationAfterTabs_WhenMultipleTypeTabsRender`:
  assert the labelled Older/Newer navigation is outside item panels and follows
  the tab control in document order. Verify its cursor-disabled states still
  match the response.

The existing empty-state and refresh-failure tests should remain unchanged.
Run the focused web unit-test project after implementation. Add browser-level
coverage only when the delivery includes explicit keyboard or responsive
acceptance criteria; bUnit should cover the grouping and retrieval-state rules.

## Unresolved Questions

* The repository has no canonical IG account-type vocabulary or business
  priority order. Alphabetical ordinal ordering is the recommended neutral
  default. A domain-owned order, such as CFD before Spreadbet regardless of
  label, requires a confirmed product rule.
* No known persisted snapshot contains blank or casing-inconsistent account
  types. The `Other` fallback and case-insensitive grouping are defensive.
* The corrected requirement does not state whether an account-type label should
  include a record count. This recommendation omits counts to keep labels
  stable across retrieval history.
