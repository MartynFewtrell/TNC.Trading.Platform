<!-- markdownlint-disable-file -->
# Implementation Details: Account Preferences UI Improvement

## Context Reference

Sources: User-provided `.copilot-tracking/research/2026-09-17/account-preferences-ui-improvement-research.md`; verified `.copilot-tracking/research/subagents/2026-09-17/account-preferences-ui-plan-verification.md`.

## Implementation Phase 1: Reframe Account Preferences Workflows

<!-- parallelizable: false -->

### Step 1.1: Introduce accessible page-local tabs

Modify the Account Preferences component to use two default-server-rendered `RadzenTabs` items named `Settings` and `Observed history`. Bind a page-local selected index, set an accessible tabs label, and preserve row identity with `@key`, matching the Account Details tab pattern. Keep Settings first so operators arrive at the editable, safety-sensitive workflow.

Files:
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor` - Add tab state and relocate existing markup into tabs.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountDetails/AccountDetails.razor` - Read-only reference for the established pattern; do not modify.

Discrepancy references:
* Addresses DR-01: no implementation discrepancy currently identified.

Success criteria:
* Tabs have an accessible name and Settings is selected on initial render.
* Inactive history content does not appear in the initial server-rendered Settings panel.

Context references:
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor` (Lines 18-56) - Existing stacked page content.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountDetails/AccountDetails.razor` (Lines 27-62) - Existing tabs convention.

Dependencies:
* Existing Radzen Blazor Web project reference.

### Step 1.2: Locate feedback and commands with their workflows

Leave page-level request failures beneath the page heading because they block both tabs. Place provider mismatch and current/save feedback within Settings before the state and actions they qualify, rendering the presenter's existing `SaveError`. Place history-only failures, the semantic table, and the cursor pager in Observed history. Retain successful save feedback as `role="status"`; provide a concise status message after an older page loads. Name the cursor command `Load older observations`; do not add numeric, reverse, or random paging.

Files:
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor` - Reposition existing feedback, commands, table, and pager only.

Discrepancy references:
* DD-01: Prefer `ObservedAtUtc` as the history display timestamp if the current response model exposes it. If it does not, retain `RecordedAtUtc` and label it explicitly as audit time pending product confirmation.

Success criteria:
* Each error appears in the tab or page scope affected by that failure.
* The history pager communicates one-way traversal and retains the opaque cursor behavior.

Context references:
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPagePresenter.cs` (Lines 21-82) - Current, save, and history feedback state.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPagePresenter.cs` (Lines 120-124) - Server-authoritative history order.

Dependencies:
* Step 1.1 completion.

### Step 1.3: Establish proportional settings hierarchy

Replace state-label `strong` markup with a definition list using uniformly emphasized terms and ordinary descriptions. Retain the native `InputRadioGroup` and its bindings. In scoped CSS, constrain the choice group inline width, retain visible selected/focus states, stack choices below 600px, and group commands with wrapping flex layout, 44px minimum targets, and one existing primary Save treatment. Avoid selectors that depend on Radzen-generated markup.

Files:
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor` - Use semantic state and labelled action-group markup.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor.css` - Add scoped hierarchy, action, and responsive choice styles.

Success criteria:
* Checked and unchecked option labels retain equal typographic weight.
* Actions wrap cleanly, and choices become single-column on narrow screens.

Context references:
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor.css` (Lines 12-91) - Existing choice and control styles.

Dependencies:
* Step 1.2 completion.

## Implementation Phase 2: Cover the New Page Contract

<!-- parallelizable: true -->

### Step 2.1: Add focused bUnit coverage

Extend the Account Preferences unit suite to assert tab accessible labels, initial Settings rendering, definition-list hierarchy, native radio semantics, contextual warning/current/save/history error placement, labelled action groups, history table name or caption, and `Load older observations`. Retain existing save confirmation, dismiss-and-focus, and cursor tests; adjust selectors only where markup changed.

Files:
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountPreferencesTests.cs` - Add rendering and lifecycle assertions.

Success criteria:
* Tests prove the information architecture without coupling to incidental CSS classes.
* Existing presenter-bound behavior remains covered.

Context references:
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountPreferencesTests.cs` (Lines 17-357) - Existing test fixture and behavioral coverage.

Dependencies:
* Phase 1 markup contract.

### Step 2.2: Add focused Playwright coverage

Use role-first locators to activate Observed history, navigate tabs with the documented keyboard behavior, and assert the history table and older-observations command are available only in the correct workflow. Reuse the existing provider-unavailable setup and web-first assertions; avoid timing delays and CSS selectors.

Files:
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/AccountPreferences/AccountPreferencesE2ETests.cs` - Add browser-only tab and history interaction scenarios.

Success criteria:
* Browser tests validate keyboard-accessible tab navigation and history controls.
* Tests do not assume numeric page counts or client-side ordering.

Context references:
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/AccountPreferences/AccountPreferencesE2ETests.cs` (Lines 6-40) - Existing E2E fixture.

Dependencies:
* Phase 1 markup contract and Docker-backed test infrastructure.

## Implementation Phase 3: Validation

<!-- parallelizable: false -->

### Step 3.1: Run focused unit validation

Validation commands:
* `dotnet build TNC.Trading.Platform.slnx --no-restore` - Compile the complete solution, including Razor and test projects.
* `dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj --no-restore --filter "FullyQualifiedName~AccountPreferencesTests"` - Account Preferences bUnit slice.

Success criteria:
* All focused unit tests pass.

### Step 3.2: Run browser and visual validation

Start or reuse the Docker-backed Aspire environment, then run:

* `dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/TNC.Trading.Platform.Web.E2ETests.csproj --no-restore --filter "FullyQualifiedName~AccountPreferencesE2ETests"` - Account Preferences browser slice.

Inspect desktop and mobile screenshots for tab usability, 44px action targets, wrapped controls, and absence of horizontal scrolling.

Success criteria:
* Focused E2E tests pass with the expected infrastructure running.
* Both viewport checks show readable, non-overlapping controls.

### Step 3.3: Triage validation findings

Fix small, isolated implementation or test issues and repeat the failing focused command. Record a new research and planning task when a fix would alter API contracts, presenter responsibility, persistence ordering, or requires product clarification of the preferred history timestamp.

Dependencies:
* Phase 1 and Phase 2 completion.

## Dependencies

* .NET SDK version specified by `global.json`
* Docker, Aspire, and existing authenticated E2E test prerequisites

## Success Criteria

* The UI separates configuration from diagnostics while preserving Account Preferences behavior and contracts.
* Focused unit, browser, desktop, and mobile validation establish the implemented presentation contract.