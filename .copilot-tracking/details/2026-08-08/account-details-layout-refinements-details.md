<!-- markdownlint-disable-file -->
# Implementation Details: Account Details Layout Refinements

## Context Reference

Sources: user request; `.copilot-tracking/research/2026-08-08/account-details-layout-research.md`; `.copilot-tracking/research/subagents/2026-08-08/account-details-layout-verification-research.md`.

## Implementation Phase 1: Add Page Layout Hooks And Coverage

<!-- parallelizable: false -->

### Step 1.1: Add Stable Page Layout Hooks

Modify only the retrieval metadata and tab boundary. Add `account-details-retrieval` alongside the existing shared key/value-grid class. Wrap the existing `RadzenTabs` in a semantic-neutral `div.account-details-tabs`.

Keep `@bind-SelectedIndex`, `Change`, `AriaLabel`, `Tabs` content, the account-type loop, and `@key` expressions unchanged.

Files:
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountDetails/AccountDetails.razor` - Add the class and wrapper around the markup at lines 25-29.

Discrepancy references:
* DR-01 - Exact spacing values are initial values pending visual review.

Success criteria:
* Trigger metadata has a page-local CSS hook.
* The wrapper encloses the whole existing Radzen tab component without changing item rendering or event flow.

Context references:
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountDetails/AccountDetails.razor` (lines 25-29) - Retrieval-to-tabs boundary.
* `.copilot-tracking/research/2026-08-08/account-details-layout-research.md` (lines 83-94) - Selected markup pattern.

Dependencies:
* None.

### Step 1.2: Protect Markup Hooks With Focused Tests

Extend the existing bUnit fixture with one narrow test using the repository's `MethodName_StateUnderTest_ExpectedResult` naming convention. Render a response with an account type, assert the retrieval `<dl>` has both `platform-key-value-grid` and `account-details-retrieval`, and assert `RadzenTabs` is a descendant of `.account-details-tabs`.

Do not test stylesheet-computed geometry in bUnit and do not alter the existing tab-selection tests.

Files:
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountDetailsTests.cs` - Add one markup-contract test near rendering/accessibility coverage.

Success criteria:
* The test fails when either new selector hook is removed or renamed.
* Existing accessibility and tab-state tests remain unchanged.

Context references:
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountDetailsTests.cs` (lines 20-116) - Existing fixture and behavioral coverage.
* `.copilot-tracking/research/2026-08-08/account-details-layout-research.md` (lines 219-226) - Test and visual validation boundary.

Dependencies:
* Step 1.1 completion.

### Step 1.3: Validate Phase 1 Changes

Run the focused tests after adding the markup hooks and their assertion.

Validation commands:
* `dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj --filter FullyQualifiedName~AccountDetailsTests` - Account details bUnit tests.

## Implementation Phase 2: Apply Account-Scoped Visual Treatment

<!-- parallelizable: false -->

### Step 2.1: Create Page-Local Layout Rules

Create the CSS-isolation file beside the Razor component. Apply `margin-bottom: 1.5rem` to the retrieval hook. Keep the three-column `account-details-account-header` above 960px, make each immediate field wrapper a `max-content minmax(0, 1fr)` grid with centered alignment and a `0.5rem` column gap, and set an outer `margin: 0 0 1.5rem`.

Retain the established <=960px one-column collapse and the lower Account type/Currency `RadzenRow` markup. Do not modify shared grid, panel, token, or responsive rules in `app.css`.

Files:
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountDetails/AccountDetails.razor.css` - New layout rules and responsive override.
* `src/TNC.Trading.Platform.Web/wwwroot/app.css` - Intentionally unchanged.

Discrepancy references:
* DR-01 - Initial `1.5rem` structural and `0.5rem` inline values need final visual approval.

Success criteria:
* Account ID, Alias, and Status values sit right of labels at desktop widths.
* The identity/status block separates from Account type/Currency without disrupting responsive lower columns.
* At <=960px, identity/status fields remain legible as one-column rows.

Context references:
* `src/TNC.Trading.Platform.Web/wwwroot/app.css` (lines 295-321) - Shared grid/header rules to leave unchanged.
* `src/TNC.Trading.Platform.Web/wwwroot/app.css` (lines 502-517) - Breakpoint to match.
* `.copilot-tracking/research/2026-08-08/account-details-layout-research.md` (lines 97-156) - Selected CSS layout pattern.

Dependencies:
* Step 1.1 completion.

### Step 2.2: Replace Only The Local Tab Frame

In the same CSS-isolation file, scope Radzen custom-property overrides and `::deep` selectors below `.account-details-tabs`. Set tab and panel surfaces transparent, remove only their borders/radius/shadow, add a `1px` baseline using `var(--platform-border-color)`, and add a `2px` selected bottom border using `var(--platform-accent-color)` with a `-1px` margin.

Use platform muted/text tokens for inactive, selected, and hovered labels. Preserve Radzen focus-specific styling and do not create global `.rz-tabview` selectors.

Files:
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountDetails/AccountDetails.razor.css` - Page-scoped Radzen selectors.

Discrepancy references:
* DD-01 - Selectors intentionally depend on Radzen 10.3.2 generated classes because supported parameters/variables cannot produce the required baseline and underline.

Success criteria:
* Default connected tabs and framed content are absent only within Account details.
* The tab list has one low-contrast baseline and the selected item has an aligned accent underline.
* Both themes use existing platform tokens without hard-coded colors.

Context references:
* `.copilot-tracking/research/2026-08-08/account-details-layout-research.md` (lines 48-54) - Radzen 10.3.2 selector evidence.
* `.copilot-tracking/research/2026-08-08/account-details-layout-research.md` (lines 165-201) - Token and selector constraints.

Dependencies:
* Step 1.1 completion.
* Radzen.Blazor remains at the researched 10.3.2 rendered structure.

### Step 2.3: Validate Phase 2 Changes

Run the focused suite after adding CSS isolation. This confirms Razor structure and interaction behavior; visual intent is validated in Phase 3.

Validation commands:
* `dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj --filter FullyQualifiedName~AccountDetailsTests` - Account details bUnit tests.

## Implementation Phase 3: Validation

<!-- parallelizable: false -->

### Step 3.1: Run Automated Validation

Run the focused test suite and build the affected Web project. Treat existing package-security or nullable warnings as baseline noise unless changed files add a warning or failure.

Validation commands:
* `dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj --filter FullyQualifiedName~AccountDetailsTests`
* `dotnet build src/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.csproj`

Success criteria:
* All Account details tests pass, including the new markup-hook assertion.
* The Web project builds without errors attributable to the two UI changes or test addition.

### Step 3.2: Perform Visual And Interaction Review

Run the application with its Docker-backed local infrastructure. Inspect Account details in both platform themes at desktop and <=960px widths. Confirm both requested gaps, horizontal desktop pairs, responsive collapse, transparent headers/panels, baseline, selected underline, hover contrast, Radzen focus visibility, keyboard navigation, refresh retention, and history navigation selection.

Success criteria:
* All four visual refinements appear in both themes and at both responsive widths.
* Keyboard and persisted-selection interactions remain unchanged.

Context references:
* `src/TNC.Trading.Platform.Web/Components/Layout/PlatformThemeState.cs` (lines 73-76) - Theme selection.
* `.copilot-tracking/research/2026-08-08/account-details-layout-research.md` (lines 219-226) - Manual checks.

Dependencies:
* Steps 1.1 through 2.2 completion.
* Local Docker dependencies available when app-host validation is required.

### Step 3.3: Report Non-Minor Validation Blockers

Do not expand to shared CSS or custom tab controls to resolve visual differences. Record selector, theme, viewport, and exact evidence when the initial values need design adjustment or a Radzen upgrade changes generated markup. Create follow-on planning when the correction exceeds local CSS tuning.

Success criteria:
* Each remaining issue identifies its affected selector, theme, viewport, and required decision.

## Dependencies

* .NET SDK selected by `global.json`.
* Existing Web test dependencies and Radzen.Blazor 10.3.2.
* Docker and Aspire-backed local services for manual app validation.

## Success Criteria

* The implementation changes `AccountDetails.razor`, creates `AccountDetails.razor.css`, and adds one markup-contract test.
* Shared `app.css`, presenter, view models, API client, and tab-state logic remain unchanged.
* Automated and manual validation demonstrate visual hierarchy while retaining tab accessibility and behavior.