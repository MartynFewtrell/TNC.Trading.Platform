<!-- markdownlint-disable-file -->
# Task Research: Account Preferences UI Alignment

Research the navigation ordering and visual consistency issues shown in the Account preferences and Account details views.

## Task Implementation Requests

* Move Account preferences directly beneath Account details in the main navigation
* Align the Account preferences page shell and tabs with Account details
* Align verification-action buttons with the Save preference button

## Scope and Success Criteria

* Scope: Analyze the existing Blazor navigation and account-related page components, styles, and tests. No product-code changes are made during this research task.
* Assumptions: The supplied screenshots represent the intended target comparison, and Account details is the reference implementation for page-shell styling.
* Success Criteria:
  * Identify the source files and controls responsible for each discrepancy
  * Recommend one minimal, project-consistent implementation approach
  * Identify focused validation that can verify the changes

## Outline

1. Current implementation and control points
2. Evaluated alternatives
3. Selected implementation and validation

## Potential Next Research

* No further research is required before implementation. The verification research at
  `.copilot-tracking/research/subagents/2026-09-17/account-preferences-ui-alignment-verification-research.md`
  confirms the remaining scope and records one deferred visual-regression decision.

## Verification Update

The focused implementation-surface verification completed on 2026-09-17. It found
that the Account Preferences two-tab workflow, selected-tab state, save-error
feedback, and accessible E2E coverage already exist. The remaining implementation
scope is navigation ordering, the page-scoped tab wrapper and styling, and use of
the existing primary-action class on the two verification buttons.

The focused `MainLayoutTests`, `AccountPreferencesTests`, and `AccountDetailsTests`
slice passed with 41 tests. The existing tests do not yet assert navigation order,
the Account Preferences wrapper, or primary-action classes, so the implementation
plan adds those assertions.

## Research Executed

### File Analysis

* `src/TNC.Trading.Platform.Web/Components/Layout/MainLayout.razor`
  * Lines 20-34 render the role-gated navigation. Account details is within the Viewer block; Account preferences is within the Operator block after Configuration.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor`
  * Lines 15-81 render the shared header, unwrapped `RadzenTabs`, two Settings panels, and Observed history panel.
  * Lines 30-33 render Retry verification and Apply desired setting as native unclassified buttons.
  * Line 50 renders Save preference with `platform-primary-action`.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountDetails/AccountDetails.razor`
  * Lines 27-59 use a parent retrieval panel and the `account-details-tabs` wrapper around `RadzenTabs`.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountDetails/AccountDetails.razor.css`
  * Lines 19-54 provide a scoped Radzen tab-view reset, strip divider, selected underline, and panel reset.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor.css`
  * Lines 1-149 provide local state-grid, actions, radio-control, feedback, focus, and responsive layout styling, but no scoped Radzen tab reset.
* `src/TNC.Trading.Platform.Web/wwwroot/app.css`
  * Lines 241-248 define `.panel`; lines 277-287 define the reusable `.platform-primary-action` used by Save preference.

### Code Search Results

* `Account details|Account preferences`
  * Identified the navigation markup in `src/TNC.Trading.Platform.Web/Components/Layout/MainLayout.razor` and the page-level authorization boundary for each destination.
* `Retry verification|Apply desired setting|Save preference`
  * Identified all requested controls in `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor`.
* `account-details-tabs|platform-primary-action|account-preferences-actions`
  * Identified the existing scoped Radzen override and project-standard primary button class.
* `RetryAsync|RemediateAsync|SaveAsync`
  * Confirmed the UI delegates behavior to `AccountPreferencesPagePresenter`; the requested changes do not require API or presenter changes.

### External Research

* Not required. The repository contains a direct visual and implementation reference in Account details.

### Project Conventions

* Standards referenced: `.github/copilot-instructions.md`, `.github/instructions/folders.instructions.md`
* Instructions followed: Task Researcher mode, repository layout guidance, Markdown style guidance

## Key Discoveries

### Project Structure

The Web project owns the reported UI. Navigation is centralized in `MainLayout.razor`; account feature markup and scoped styles are colocated in the corresponding `Components/Pages/AccountDetails` and `Components/Pages/AccountPreferences` directories. bUnit tests mirror the feature folders in `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests`.

### Implementation Patterns

Account details is the local reference for removing Radzen-generated tab container chrome. Its wrapper scopes `::deep` selectors so its changes do not affect the other tab-based pages. Account preferences already uses isolated CSS and two single-level workflow panels. Adding an outer `.panel` would create nested cards, duplicate the border, shadow, padding, and margin defined by shared CSS.

The only shared action appearance is `.platform-primary-action`. Retry verification and Apply desired setting are forward-progress or recovery actions. They are neither destructive nor secondary in the displayed failure state, so they should use the standard primary class rather than introducing a new component or semantic class.

Account details is Viewer-gated and Account preferences is Operator-gated. Test principals in `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/PlatformComponentTestContext.cs` lines 195-199 hold one scope each. Account preferences must remain inside an independent Operator block when its markup moves.

### Complete Examples

The selected navigation ordering preserves independent authorization while placing Account preferences directly after Account details whenever both scopes are present:

```razor
@if (operatorContext.IsViewer)
{
  <PlatformNavItem Href="/account-details" Icon="account_balance" Text="Account details" />
}

@if (operatorContext.IsOperator)
{
  <PlatformNavItem Href="/account-preferences" Icon="tune" Text="Account preferences" />
  <PlatformNavItem Href="/configuration" Icon="settings" Text="Configuration" />
}

@if (operatorContext.IsViewer)
{
  <PlatformNavItem Href="/status" Icon="monitor_heart" Text="Status" />
}
```

## Technical Scenarios

### Account Preferences Alignment

Account Preferences requires three scoped changes: navigation item relocation, removal of default Radzen tab chrome, and action class consistency. No behavior, API, route, or authorization-policy change is required.

**Requirements:**

* Render Account preferences immediately beneath Account details when the operator possesses both scopes
* Retain availability of Account preferences to an Operator-only principal
* Match Account details tab appearance without framing current panels within another panel
* Use the existing standard action appearance for both verification commands

#### Considered Alternatives

* Move Account preferences into the Viewer block. Rejected because it would hide the Operator-only page link from the established local Operator test principal.
* Keep the present navigation grouping. Rejected because it leaves Status and Configuration between the two requested account destinations.
* Add global `.rz-tabview` changes to `wwwroot/app.css`. Rejected because Account details uses a page-local scoped override, and global selectors could alter unrelated tab views.
* Replace `RadzenTabs` with native tabs. Rejected because it would replace established keyboard behavior already covered by E2E tests and expands the change beyond styling.
* Wrap all Account preferences tabs in another `.panel`. Rejected because each workflow is already a panel; the result would be nested cards and duplicated visual chrome.
* Add a new secondary or destructive action style. Rejected because no such shared action pattern exists, and the commands are recovery actions rather than cancellation or deletion.

**Preferred Approach:**

* Move the existing Operator-gated Account preferences item to its own block immediately following the Viewer-gated Account details item in `src/TNC.Trading.Platform.Web/Components/Layout/MainLayout.razor`. Keep its authorization condition unchanged.
* Wrap the existing `RadzenTabs` in `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor` with `<div class="account-preferences-tabs">`.
* Copy the scoped tab-view reset, tab strip divider, selected underline, and panel reset from `AccountDetails.razor.css` to `AccountPreferences.razor.css`, changing only the wrapper selector. Keep the existing two Settings panels intact.
* Add `class="platform-primary-action"` to Retry verification and Apply desired setting. Preserve existing operation state, disabled conditions, group names, and presenter calls.

```text
src/TNC.Trading.Platform.Web/Components/Layout/MainLayout.razor
src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor
src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor.css
test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/MainLayoutTests.cs
test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountPreferencesTests.cs
```

**Implementation Details:**

Use page-scoped CSS to retain Radzen accessibility semantics and avoid application-wide changes:

```css
.account-preferences-tabs ::deep .rz-tabview {
  background: transparent;
  border: 0;
  box-shadow: none;
  padding: 1.25rem 0 0;
}
```

Port the remainder of the established Account Details selector rules for the tab headers, tab list, selected indicator, and panel reset. Do not copy its unrelated three-column account summary styles.

Extend `MainLayoutTests` to assert Viewer and Operator visibility stays independent. Extend the initial Account Preferences render test to assert the wrapper and primary action class on all three commands. Add control-state coverage for Retry and Apply desired setting if it does not already cover their visibility and in-flight disabled state.

Run the focused unit slice after implementation:

```powershell
dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj --filter "FullyQualifiedName~MainLayoutTests|FullyQualifiedName~AccountPreferencesTests|FullyQualifiedName~AccountDetailsTests"
```

Then build the Web project:

```powershell
dotnet build src/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.csproj
```

Run the focused Account Preferences browser suite when the Aspire-backed E2E environment is available, then compare the tab strip visually against Account details:

```powershell
dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/TNC.Trading.Platform.Web.E2ETests.csproj --filter "FullyQualifiedName~AccountPreferencesE2ETests"
```
