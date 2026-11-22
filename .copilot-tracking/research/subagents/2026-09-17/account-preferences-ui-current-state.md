---
title: Account Preferences UI Current-State Research
description: Verified repository research for requested Account Preferences UI improvements
author: GitHub Copilot
ms.date: 2026-09-17
ms.topic: reference
keywords:
  - account preferences
  - blazor
  - radzen
  - accessibility
estimated_reading_time: 5
---

## Research Scope

Research the current Account Preferences UI, trailing-stops setting, observed history,
and Account Details account-tab patterns. Record verified implementation evidence, test
coverage, accessibility and usability issues, and a single recommended implementation
direction.

## Contribution Rules

* `.github/instructions/radzen-blazor.instructions.md` lines 24-45 makes free Radzen
  components the default for shared controls, including tabs; it requires meaningful
  labels, keyboard navigation, visible validation feedback, semantic headings, and
  focused interaction tests.
* The same instruction, lines 62-69, prohibits fragile generated-markup selectors and
  requires adapting Radzen examples to repository architecture and accessibility.
* `.github/instructions/tests.instructions.md` lines 43-61 assigns bUnit to component
  rendering and lifecycle behavior, Playwright to browser-only workflows, and requires
  documented xUnit tests with the `MethodName_StateUnderTest_ExpectedResult` convention.
* `.github/instructions/playwright.instructions.md` lines 20-38 requires
  accessibility-first locators, web-first assertions, and no explicit delay-based
  synchronization for Playwright UI coverage.
* `c:\Users\martynfewtrell\.vscode\extensions\ise-hve-essentials.hve-core-3.2.2\.github\instructions\hve-core\markdown.instructions.md`
  requires YAML frontmatter, an H2-first body when `title` is present, ATX headings,
  ASCII punctuation, and consistently formatted lists. This research record follows
  those rules.

## Current UI

### Page and State

* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor`
  lines 1-16 defines the Operator-only `/account-preferences` page, injects the
  presenter, and renders a header and top-level current/history errors.
* Lines 18-30 render a plain-text state panel. It exposes desired value, target
  account, observed value and account, verification status, pending-verification
  status, failure summary, and Retry/Apply actions.
* Lines 31-52 render the confirmed trailing-stops editor with native
  `InputRadioGroup<bool>` inputs, a `platform-primary-action` save button, and a
  dismissible `role="status"` confirmation.
* Lines 53-55 render observed history as a native table and a single `Next page`
  button. There is no empty-state row, caption, scope attributes, Previous action, or
  history-specific navigation landmark.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPageViewModel.cs`
  lines 3-47 keeps pending selection, desired state, confirmed observation, verification
  state, and history separate. `HasPendingChange` compares the pending Boolean to the
  confirmed Boolean, so the UI can distinguish operator intent from provider fact.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPagePresenter.cs`
  lines 21-48 load current state before history and retain separate `CurrentError` and
  `HistoryError`. Lines 50-82 save, refresh history, and retain `SaveError`, but the
  Razor page has no markup that renders `SaveError`.

### Styling and Controls

* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor.css`
  lines 12-35 makes the radio group a `width: 100%` two-column grid. On typical desktop
  panel widths, Enabled and Disabled each occupy half the panel, making this binary
  setting visually over-wide.
* Lines 19-46 use the label as the radio's large interactive target and apply
  `font-weight: 700`, underline, and a 2px border only to the selected option. The
  unselected label uses inherited weight, while state-panel labels use `strong`; this
  produces inconsistent label emphasis across the page.
* Lines 62-91 only give direct panel buttons a common 44px minimum height. The buttons
  are individual sibling controls without a shared action-group layout, alignment,
  spacing, or primary/secondary hierarchy.
* `src/TNC.Trading.Platform.Web/wwwroot/app.css` lines 241-254 defines the rounded panel
  and lines 277-288 defines the gradient primary action with its own bottom margin. The
  page inherits global native-button styling for Retry, Apply, Next page, and Dismiss,
  so they do not visually match the Save action.

### Observed History Ordering

* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor`
  line 54 displays `RecordedAtUtc` under the `Recorded` header. It does not sort the
  observations.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPagePresenter.cs`
  lines 120-124 passes the API list directly to the view model. Presentation sorting
  here would risk contradicting paged server order.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfTrailingStopsPreferenceObservationStore.cs`
  lines 27-63 supplies the authoritative order: descending `ObservedAtUtc`, then
  descending observation ID, including the keyset cursor predicate. Thus the table's
  displayed timestamp is not necessarily the sort key. The immediate usability issue
  is ambiguous chronology, not nondeterministic ordering.
* `src/TNC.Trading.Platform.Web/PlatformApiClient.cs` lines 188-197 requests a bounded
  page of 25 observations and transports its opaque next cursor unchanged.

### Existing Account Details Tab Pattern

* `src/TNC.Trading.Platform.Web/Components/Pages/AccountDetails/AccountDetails.razor`
  lines 27-62 provides the repository's direct comparison pattern: a page-local
  `RadzenTabs` wrapper, `AriaLabel="Account types"`, source-ordered account-type groups,
  `@key` values, and a selected-index change handler.
* Lines 75-90 preserve the normalized selected group through refresh and older/newer
  history retrieval, falling back to the first group only when it disappears.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountDetails/AccountDetails.razor.css`
  lines 18-51 scopes Radzen customization through the page wrapper and CSS variables.
  It creates an unframed tab strip with a visible selected bottom border without
  targeting individual generated child structures beyond Radzen's documented classes.

## Accessibility and Usability Issues

| Requested area | Verified current issue | Evidence |
|----------------|------------------------|----------|
| Label weight inconsistency | Selected radio labels are bold and underlined, unselected options are inherited weight, and state labels are separately bolded with `strong`. The visual weight communicates selection but lacks a consistent label hierarchy. | AccountPreferences.razor.css lines 19-46; AccountPreferences.razor lines 20-25 and 39-41 |
| Warning placement | Current-state and history alerts sit above every panel, rather than beside the state or history they qualify. Failure summary is inside state, but save failures are never rendered even though `SaveError` is populated. | AccountPreferences.razor lines 16-18 and 27; AccountPreferencesPagePresenter.cs lines 50-82 |
| Button spacing and consistency | Retry, Apply, Save, Dismiss, and Next page have no shared action layout. Save has bespoke primary styling and bottom margin; other actions are native buttons with only a common 44px height when they are direct panel children. | AccountPreferences.razor lines 28-29, 44, 49, and 55; AccountPreferences.razor.css lines 62-91; app.css lines 277-288 |
| Overly-wide trailing-stops setting | The two radio options each fill half of the panel via a full-width grid. This is excessive for a binary preference and visually overstates its breadth. | AccountPreferences.razor.css lines 12-18 |
| History ordering | The server correctly orders newest observation first by `ObservedAtUtc`, but the UI labels and displays `RecordedAtUtc`. Operators cannot infer the actual sort key from the table. | EfTrailingStopsPreferenceObservationStore.cs lines 27-52; AccountPreferences.razor line 54 |
| Using tabs | Account Details establishes accessible, state-preserving Radzen tabs for grouped content. Account Preferences is a single long sequence of state, editing, and evidence panels, with no tabs or an equivalent navigation model. | AccountDetails.razor lines 27-62 and 75-90; AccountPreferences.razor lines 18-55 |

## Test Coverage

* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountPreferencesTests.cs`
  lines 17-90 test confirmed versus pending state. Lines 92-354 test loading, current and
  history failures, radio selection, save feedback, save blocking, focus restoration,
  and cursor use. No bUnit test asserts compact selector layout, button grouping, warning
  placement, a history caption/empty state, displayed sort-key clarity, or tab behavior.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/AccountPreferences/AccountPreferencesE2ETests.cs`
  lines 19-40 signs in an operator and verifies desired and observed labels during
  provider unavailability. It does not exercise saving, paging, visual layout, or
  keyboard navigation.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/AccountPreferences/AccountPreferencesFunctionalTests.cs`
  lines 15-90 covers API durability and recovery, not rendered UI behavior.
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/EfTrailingStopsPreferenceObservationStoreTests.cs`
  lines 10-26 verifies deterministic equal-timestamp ordering and cursor paging.
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/AccountPreferencesSqlIntegrationTests.cs`
  lines 70-112 verifies SQL order and keyset paging against the persisted query.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountDetailsTests.cs`
  lines 17-159 verifies tab labels, selected-panel filtering, source ordering, layout
  hooks, and selected-tab retention. It is the appropriate local test model for a tabbed
  Account Preferences presentation.

## Recommended Direction

Refactor only `AccountPreferences.razor` and its scoped CSS into two named
`RadzenTabs`: `Preference` for state plus the compact trailing-stops editor, and
`Observed history` for the read-only table and pagination. Reuse the Account Details
pattern: `AriaLabel`, `@bind-SelectedIndex`, `Change`, `@key`, page-local wrapper, and
CSS-variable customization. Keep current API calls, `AccountPreferencesPageViewModel`,
presenter state, server-side sort, and cursor contract unchanged.

Within the Preference tab, render state data as a semantic `dl` with uniform `dt` label
weight, constrain the radio group with a practical max inline size, and put Retry, Apply,
and Save inside one action group with explicit primary/secondary styling. Render each
error or status message inside the tab and directly beside the operation it qualifies,
including a visible `SaveError`. Within Observed history, add a caption or heading that
states `Newest observed first`; display `ObservedAtUtc` in the chronology column, or
rename `Recorded` to make clear that it is audit-recorded time. Use a `nav` landmark and
an accessible `View older observations` label for paging.

This direction addresses every requested issue while preserving the established
architecture: persistence owns ordering, the presenter owns UI state, and the component
owns markup and layout.

### Rejected Alternatives

* Sort rows in the presenter or Razor component. This would duplicate and potentially
  contradict the server's keyset-pagination order.
* Replace the binary radio group with a switch. The existing radio labels make both
  choices explicit, retain native form semantics, and are already covered by bUnit tests.
  The documented issue is width and hierarchy, which CSS and structure can resolve.
* Add a new shared tab or button abstraction. Account Details already supplies a close,
  page-local Radzen pattern; shared abstraction would add scope without proven reuse.
* Build custom HTML/JavaScript tabs. RadzenTabs is the repository default and already
  demonstrates keyboard-oriented tab behavior and scoped styling.

## Remaining Gaps

* No user-provided screenshots, viewport dimensions, or priority ordering were available,
  so the compact selector max width and final desktop/mobile spacing need design review.
* No browser accessibility-tree or keyboard test currently confirms the Account Details
  pattern under the repository's real theme. A follow-up Playwright test should use
  role-based locators and test tab navigation, focus order, and visible tab panels.
* The intent of `RecordedAtUtc` versus `ObservedAtUtc` for operator display needs product
  confirmation. The repository evidence establishes the sorting contract but does not say
  which timestamp users expect to inspect.