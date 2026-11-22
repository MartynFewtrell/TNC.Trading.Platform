<!-- markdownlint-disable-file -->
# Implementation Details: Account Preferences UI Alignment

## Context Reference

Sources: user-supplied account-preferences-ui-alignment-research.md;
.copilot-tracking/research/2026-09-17/account-preferences-ui-alignment-research.md;
.copilot-tracking/research/subagents/2026-09-17/account-preferences-ui-alignment-verification-research.md.

## Implementation Phase 1: Preserve Navigation Authorization and Order

<!-- parallelizable: false -->

### Step 1.1: Relocate the Operator-gated Account Preferences navigation item

Modify src/TNC.Trading.Platform.Web/Components/Layout/MainLayout.razor around the
Viewer and Operator navigation blocks currently verified at lines 19-30. Keep Account
details in its Viewer condition. Move the unchanged Account preferences
`PlatformNavItem` into a separate `operatorContext.IsOperator` condition immediately
after the Viewer condition. Leave Configuration in its existing following
Operator-gated condition.

Files:
* src/TNC.Trading.Platform.Web/Components/Layout/MainLayout.razor - reorder the existing navigation item without changing route, icon, or role condition

Discrepancy references:
* None

Success criteria:
* Combined Viewer and Operator navigation places `/account-preferences` directly after `/account-details`.
* An Operator-only principal continues to receive `/account-preferences`.

Context references:
* src/TNC.Trading.Platform.Web/Components/Layout/MainLayout.razor (Lines 19-30) - role-gated navigation.

Dependencies:
* None

### Step 1.2: Add navigation order and visibility coverage

Extend test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/MainLayoutTests.cs
near the signed-in navigation tests at lines 34-57. Use the existing component test
principal setup to assert Viewer-only and Operator-only visibility independently. Add
a combined-role test that queries navigation anchors in document order and verifies
the Account Details link directly precedes Account Preferences.

Files:
* test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/MainLayoutTests.cs - cover authorization preservation and requested ordering

Success criteria:
* Tests fail against the old navigation ordering and pass after Step 1.1.
* Tests do not depend on styling implementation details.

Context references:
* test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/MainLayoutTests.cs (Lines 34-57) - existing signed-in navigation assertions.
* test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/PlatformComponentTestContext.cs (Lines 195-199) - local role-specific principal fixtures.

Dependencies:
* Step 1.1 completion

### Step 1.3: Validate phase changes

Run the narrow navigation test before editing the page surface.

Validation commands:
* `dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj --no-restore --filter "FullyQualifiedName~MainLayoutTests"` - navigation unit tests

## Implementation Phase 2: Align Account Preferences Presentation

<!-- parallelizable: false -->

### Step 2.1: Add an isolated tab styling wrapper

Modify src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor
around `RadzenTabs` at lines 17-76. Add an `account-preferences-tabs` `div` around the
existing tabs. Preserve `selectedTabIndex`, the change handler, accessibility label,
tab content, `@key`, command callbacks, and all disabled conditions. Do not add an
outer `panel`, because tab contents already contain workflow panels.

Files:
* src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor - add the scoped styling hook

Discrepancy references:
* None

Success criteria:
* Existing Settings and Observed history tab behavior is unchanged.
* The wrapper is a plain container, not additional card chrome.

Context references:
* src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor (Lines 17-76) - existing tab behavior.
* src/TNC.Trading.Platform.Web/Components/Pages/AccountDetails/AccountDetails.razor (Lines 27-64) - wrapper reference.

Dependencies:
* Phase 1 validation completion

### Step 2.2: Scope the Account Details tab presentation to Account Preferences

Modify src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor.css.
Port the tab-specific rules from AccountDetails.razor.css lines 19-53, changing the
scope from `.account-details-tabs` to `.account-preferences-tabs`. Include only the
tab-view reset, tab list divider, selected underline, and generated tab-panel reset.
Do not copy Account Details summary-grid rules or change global app.css.

Files:
* src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor.css - add isolated Radzen tab presentation rules

Success criteria:
* Radzen generated markup is styled only beneath `.account-preferences-tabs`.
* Existing local responsive layout, focus, state-grid, and feedback styles remain intact.

Context references:
* src/TNC.Trading.Platform.Web/Components/Pages/AccountDetails/AccountDetails.razor.css (Lines 19-53) - approved local styling pattern.
* src/TNC.Trading.Platform.Web/wwwroot/app.css (Lines 241-248) - shared panel treatment to avoid nesting.

Dependencies:
* Step 2.1 completion

### Step 2.3: Align action classes and prove page structure

In AccountPreferences.razor, add `class="platform-primary-action"` to the Retry
verification and Apply desired setting buttons at current lines 32-33. Leave their
presenter calls and disabled expressions unchanged. Extend AccountPreferencesTests.cs
near its initial render coverage at lines 73-150 to assert the wrapper and that Retry
verification, Apply desired setting, and Save preference all carry the shared class.

Files:
* src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor - reuse the shared action class
* test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountPreferencesTests.cs - add structural styling-contract assertions

Success criteria:
* Every requested action uses `platform-primary-action`.
* Tests retain coverage of tab accessibility and deferred history rendering.

Context references:
* src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor (Lines 32-50) - existing action markup.
* test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountDetailsTests.cs (Lines 53-70) - local wrapper assertion pattern.

Dependencies:
* Steps 2.1 and 2.2 completion

### Step 2.4: Validate phase changes

Run the affected page and reference-component test slice after Phase 2.

Validation commands:
* `dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj --no-restore --filter "FullyQualifiedName~AccountPreferencesTests|FullyQualifiedName~AccountDetailsTests"` - page structure and tab behavior

## Implementation Phase 3: Final Validation

<!-- parallelizable: false -->

### Step 3.1: Run focused Web validation

Execute the combined focused unit suite, then build the owning Web project without
restoring packages. Correct only isolated failures attributable to this change.

Validation commands:
* `dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj --no-restore --filter "FullyQualifiedName~MainLayoutTests|FullyQualifiedName~AccountPreferencesTests|FullyQualifiedName~AccountDetailsTests"` - full changed unit slice
* `dotnet build src/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.csproj --no-restore` - owning project compilation

Success criteria:
* The focused unit suite passes.
* The Web project builds successfully.

### Step 3.2: Run focused browser coverage when available

With Docker and Aspire-backed E2E infrastructure running, execute the current Account
Preferences Playwright suite. Preserve its accessible locators and keyboard coverage;
use visual inspection against Account Details only where the implementation workflow
provides a stable browser environment.

Validation commands:
* `dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/TNC.Trading.Platform.Web.E2ETests.csproj --no-restore --filter "FullyQualifiedName~AccountPreferencesE2ETests"` - browser tab behavior

Success criteria:
* The existing keyboard and tab behavior remain functional in a browser.

Dependencies:
* Docker, Aspire test hosting, and Playwright browser prerequisites

### Step 3.3: Handle validation outcomes

Apply minor, local corrections for compilation, selector, or test assertion failures.
For visual differences that require a new acceptance baseline or browser screenshot
framework, record the issue as follow-on work rather than expanding this task.

## Dependencies

* global.json-compatible .NET SDK
* Restored Web unit-test and Web project dependencies
* Docker and Aspire prerequisites for E2E execution

## Success Criteria

* Navigation, Account Preferences tab structure, and primary-action classes are covered by focused unit tests.
* Account Preferences retains accessible Radzen tab interactions and existing workflow behavior.
* The owning Web project builds after the UI-only changes.