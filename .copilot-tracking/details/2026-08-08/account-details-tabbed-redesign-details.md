<!-- markdownlint-disable-file -->
# Implementation Details: Account Details Tabbed Redesign

## Context Reference

Sources: user request; `.copilot-tracking/research/2026-08-08/account-details-tabbed-redesign-research.md`; `.copilot-tracking/research/subagents/2026-08-08/account-details-tabbed-redesign-verification-research.md`; relevant Radzen, architecture, and test instructions.

## Implementation Phase 1: Build Local Tabbed Presentation

<!-- parallelizable: false -->

### Step 1.1: Add grouping and component-local selection

Modify `AccountDetails.razor` only. Trim Account type values, group them using ordinal-ignore-case keys, preserve the first trimmed display label, and retain source order for both the first-occurring type groups and the accounts inside each group. Do not add a business display-order rule or malformed-type fallback in this slice; the source model and IG boundary require Account type. Store the selected normalized key locally.

Replace direct button calls to presenter methods with page-local `RefreshAsync`, `LoadOlderAsync`, and `LoadNewerAsync` wrappers. Each wrapper awaits its corresponding presenter operation, then reconciles the selected key to the new groups and sets the bound index. A failed refresh preserves the key because the presenter retains the existing retrieval. Do not modify the presenter, API, application, or persistence.

Files:
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountDetails/AccountDetails.razor` - grouping, selected-key state, and reconciliation.

Discrepancy references:
* DR-01 - intentionally defers business ordering and malformed-type display policy.

Success criteria:
* Casing-only type differences produce one group.
* Every presenter-driven retrieval replacement passes through a page-local reconciliation wrapper.
* No backend or presenter contract changes occur.

Context references:
* `AccountDetails.razor` (Lines 24-50) - current presentation and lifecycle surface.
* `AccountDetailsPagePresenter.cs` (Lines 5-51) - retrieval replacement paths.

Dependencies:
* Existing view model fields at `AccountDetailsViewModels.cs` lines 3-29.

### Step 1.2: Render tabs, panel headers, and field columns

Before authoring the tab markup, inspect the installed Radzen 10.3.2 `RadzenTabs` source/XML documentation to identify the supported tab-list accessible-name parameter or attribute path. Apply that supported path with the explicit name `Account types`, then assert the rendered element with `role="tablist"` has that accessible name in the focused bUnit suite. Do not rely on an online version's API parameters.

Place metadata in a top-level metadata region. Render bound `RadzenTabs` after it, with one `RadzenTabsItem` per group and `@key` set to the normalized key. Use the installed 10.3.2 selected-index binding and default server mode. Render every active-group account as one `article.panel`, without an enclosing account-content panel. Add Account ID, Alias, and visible Status text in an `account-details-account-header` definition-list grid. Render Status with `RadzenBadge`, keeping neutral `Info` styling for all statuses until business meanings are known. Use `RadzenRow` and `RadzenColumn Size="12" SizeMD="6"`: profile/transfer labels left and currency/financial labels right.

Files:
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountDetails/AccountDetails.razor` - tab items, account articles, badge, and responsive columns.

Discrepancy references:
* DD-01 - avoids nested panels.
* DR-02 and DR-04 - retains visible status text without unverified semantic colours and requires a verified tab-list accessible name.

Success criteria:
* A type, not an account, is the tab unit.
* The active panel contains every account of the selected type and all requested fields.
* The tab list renders with the accessible name `Account types` through an installed-package-supported configuration.

Dependencies:
* Step 1.1 completion and existing Radzen configuration.

### Step 1.3: Preserve retrieval-wide structure and correct duplicate errors

Keep Retrieved at, Trading day, and Trigger before `RadzenTabs`. Keep labelled `Account details history` navigation after tabs and outside each tab item, retaining cursor/loading disabled conditions. Render an initial-load error as today, but consolidate a refresh error with an existing retrieval to one paragraph, near refresh controls or metadata; remove the duplicate post-content paragraph.

Files:
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountDetails/AccountDetails.razor` - metadata, navigation, and error locations.

Discrepancy references:
* DD-02 - corrects verified duplicate refresh-error markup.

Success criteria:
* Metadata and history navigation remain retrieval-wide.
* A failed refresh displays error text once while preserving retrieval and selected type.

Dependencies:
* Step 1.2 completion.

### Step 1.4: Validate presentation changes

Validation commands:
* `dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj --filter "FullyQualifiedName~AccountDetailsTests"` - focused bUnit validation.

## Implementation Phase 2: Add Responsive Header Styling

<!-- parallelizable: false -->

### Step 2.1: Add narrow account-header CSS

In `app.css`, add `.account-details-account-header` as a three-track desktop grid, then stack it at the existing narrow breakpoint. Reuse CSS variables and existing `panel`/`platform-key-value-grid` rules. Do not target Radzen-generated markup or replace the library's focus and active styles.

Files:
* `src/TNC.Trading.Platform.Web/wwwroot/app.css` - header grid and narrow-screen override.

Success criteria:
* Identity fields are across the account top at wide widths and stack without overlap at narrow widths.

Dependencies:
* Phase 1 class names.

### Step 2.2: Verify responsive and accessible interaction

Inspect a multi-type retrieval at wide and narrow viewports. Verify visible tab focus and keyboard selection, metadata order, complete account headers, stacked field groups, and bottom history navigation.

Success criteria:
* Custom CSS does not defeat Radzen focus, labels, or keyboard behaviour.

Dependencies:
* Steps 1.2 and 2.1 completion.

## Implementation Phase 3: Extend Focused Account Details Tests

<!-- parallelizable: false -->

### Step 3.1: Cover grouping, selection, and content

Extend `AccountDetailsTests.cs` using the existing `PlatformComponentTestContext`. Create two CFD records and one Spreadbet record. Assert two type labels, source-order tab presentation, all CFD records in CFD, no per-account tabs, an accessible tab list named `Account types`, and all requested header and two-column content. Activate Spreadbet through a visible role, text, or stable test ID, then assert only its record is active. Add isolated cases for casing normalization, selection retention after each page-local refresh/older/newer wrapper, and fallback when the selected key disappears. Document every test with requirement-oriented XML comments.

Files:
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountDetailsTests.cs` - documented bUnit scenarios.

Discrepancy references:
* DR-01, DR-02, and DR-04.

Success criteria:
* Tests prove type grouping, active-tab-only content, accessible tab-list naming, selection reconciliation, and user-visible field placement without Radzen internal selectors.

Dependencies:
* Phase 1 completion.

### Step 3.2: Assert one refresh error

Strengthen the existing failed-refresh test with an exact-count assertion, retaining its requirement that saved retrieval content remains visible.

Files:
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountDetailsTests.cs` - failed-refresh regression test.

Discrepancy references:
* DD-02.

Success criteria:
* Error text occurs once and selection is preserved after failed refresh.

Dependencies:
* Step 1.3 completion.

### Step 3.3: Validate focused tests

Validation commands:
* `dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj --filter "FullyQualifiedName~AccountDetailsTests"` - focused bUnit validation.

## Implementation Phase 4: Validation

<!-- parallelizable: false -->

### Step 4.1: Run full validation

Validation commands:
* `dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj` - full Web unit tests.
* `dotnet build src/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.csproj` - Web compile and analyzers.

### Step 4.2: Correct and rerun validation

Fix isolated markup, CSS, test, compile, or analyzer failures. Rerun both Step 4.1 commands after every correction. Stop and report a blocker when resolution would require backend contract work, a business decision, or non-isolated refactoring.

### Step 4.3: Report blockers

Document business type order, status semantics, or deep-link requirements that need a separate decision.

## Dependencies

* Radzen Blazor 10.3.2, Interactive Server rendering, and the existing Web bUnit context.

## Success Criteria

* Focused and full Web tests pass, the Web project builds, and the UI meets the requested tabbed presentation without a backend change.