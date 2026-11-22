<!-- markdownlint-disable-file -->

# Implementation Details: Trailing Stops Configuration Issues

## Context Reference

Sources: `.copilot-tracking/research/2026-09-19/trailing-stops-configuration-issues-research.md`; `.copilot-tracking/research/subagents/2026-09-19/trailing-stops-delivery-scope-research.md`; `.github/copilot-instructions.md`; `docs/wiki/local-development.md`.

## Implementation Phase 1: Correct the Configuration Control Layout

<!-- parallelizable: false -->

### Step 1.1: Make the existing control row size to its content

Modify only the flex declarations that control the trailing-stops preference row. At `AccountPreferences.razor.css:68-85`, remove the `flex: 1 1 auto` behavior from `.account-preferences-choice` (or replace it with an equivalent non-growing declaration) and set `.account-preferences-control-row` to `align-items: center`. Preserve the current gap, `flex-wrap`, radio-label, focus, and responsive declarations. Do not change `.razor` markup, the presenter, API contracts, or Radzen component usage.

Files:

* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor.css` - Remove the fieldset growth that pushes Save right and center controls on the row cross axis

Success criteria:

* At desktop width, the radio fieldset occupies its natural content width rather than the available row width
* The Save button is centered against the fieldset rather than bottom-aligned
* Narrow widths continue to wrap the existing controls without horizontal overflow

Context references:

* `.copilot-tracking/research/subagents/2026-09-19/trailing-stops-delivery-scope-research.md` (Lines 22-40) - Verified CSS cause and constrained implementation surface
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:40-56` - Existing semantic controls and action container

Dependencies:

* None

### Step 1.2: Make browser geometry assertions describe the intended layout

Update the desktop geometry portion of `AccountPreferencesE2ETests.cs:82-108`. Replace the assertion that only requires Save to be somewhere right of the fieldset with bounding-box assertions that require Save's left edge to begin near the fieldset's right edge and its vertical center to be within a small documented tolerance of the fieldset's vertical center. Use the test's existing Playwright locator and assertion style. Keep the focused test's existing narrow-viewport no-overflow checks and keyboard radio-selection checks intact.

Select a tolerance that covers browser sub-pixel rounding while failing the previous bottom-aligned layout. Start with a fixed CSS-pixel tolerance recorded in the test's requirement-traceability comment; refine only when the focused suite demonstrates cross-browser variance.

Files:

* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/AccountPreferences/AccountPreferencesE2ETests.cs` - Strengthen desktop visual-contract coverage

Success criteria:

* The test fails with the current expanded-fieldset and bottom-aligned layout
* The test passes after the CSS adjustment and still covers interaction and narrow-screen behavior
* Test comments retain the repository-required requirement, expected outcome, and behavior rationale traceability

Context references:

* `.copilot-tracking/research/subagents/2026-09-19/trailing-stops-delivery-scope-research.md` (Lines 29-40) - Current desktop, narrow, and keyboard coverage
* `.copilot-tracking/research/2026-09-19/trailing-stops-configuration-issues-research.md` (Lines 117-135) - Selected CSS-only approach and test expectation

Dependencies:

* Step 1.1 implementation target is defined; the test may be authored before the CSS patch but must be validated against both states

### Step 1.3: Validate the layout slice

Run the Account Preferences E2E class with Docker and the Aspire test prerequisites available. The class includes the desktop, narrow-viewport, and keyboard paths, so do not split them into unrelated test work. Then build the solution to catch CSS isolation, Web project, and test compilation issues.

Validation commands:

* `dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/TNC.Trading.Platform.Web.E2ETests.csproj --filter "FullyQualifiedName~AccountPreferencesE2ETests"` - Browser behavior and geometry
* `dotnet build` - Repository compilation

Success criteria:

* The filtered Account Preferences E2E class passes with the local Aspire and Docker prerequisites satisfied
* The solution builds without errors

Dependencies:

* Steps 1.1 and 1.2 completion
* Docker, Playwright browser installation, and local Aspire test dependencies

## Implementation Phase 2: Recover the Current Account-Binding Incident

<!-- parallelizable: false -->

### Step 2.1: Use the existing matching-account recovery procedure

Do not modify `SaveAccountPreferencesHandler`, `CheckAccountPreferencesStatusHandler`, `IgAccountPreferencesGateway`, API mappings, or account-ID comparison rules as part of this incident. Authenticate the intended IG Demo account, confirm authentication succeeds, and then select Check status. The operation may proceed only when the latest authenticated snapshot matches the durable account binding. Do not select Save to rebind and do not change SQL state.

Files:

* No source-file modification - This is an operational recovery using the existing protected route

Success criteria:

* A matching authenticated account permits Check status to observe or remediate preferences
* A remaining mismatch continues to prevent provider preference I/O and preserves the durable binding

Context references:

* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/CheckAccountPreferencesStatusHandler.cs:14-42` - Guard before lease and provider operations
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/SaveAccountPreferencesHandler.cs:15-54` - Guard and first-save binding behavior
* `.copilot-tracking/research/subagents/2026-09-19/trailing-stops-delivery-scope-research.md` (Lines 42-77) - Verified recovery and UI-command behavior

Dependencies:

* Intended IG Demo credentials and a successful local authentication session

### Step 2.2: Diagnose a recovery that remains blocked

When successful reauthentication does not clear the warning, capture the Check status request and response from browser Network tools and the associated Console output. Preserve the returned problem type, HTTP status, correlation information when present, account identifiers only under approved handling, and evidence of authorization, revision, lease, or provider errors. Use the evidence to distinguish an actual raw account-ID mismatch from another failure; do not infer a safe normalization rule from a single browser session.

Files:

* No source-file modification - Incident evidence becomes the input to a targeted diagnosis or follow-on plan

Success criteria:

* The incident is classified as matching-account recovery, actual mismatch, authorization, concurrency, or provider failure
* Sensitive credentials, bearer tokens, and full account identifiers are excluded from tickets and logs unless the approved incident process permits them

Context references:

* `src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs:145-158` - Check status response behavior
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPagePresenter.cs:91-98` - Projection rendering path

Dependencies:

* Step 2.1 attempts recovery first
* Browser DevTools and permitted incident evidence storage

### Step 2.3: Hold rebind and canonicalization for a separate vertical slice

Treat an intentional account switch as a new product workflow. Before implementation, obtain IG's authoritative `currentAccountId` case and whitespace contract. If a rebind is approved, plan an administrator-only operation with fresh target-account confirmation, expected revision, lease and transaction handling, a distinct audit event, retained history, API authorization, UI affordance, and Application, Infrastructure, functional, and E2E coverage. Do not change ordinary Save to perform a silent rebind.

Files:

* No source-file modification in this delivery - New source and test files are defined only after authorization and provider-contract research

Discrepancy references:

* Defers DR-01 and DR-03 until provider semantics and product authorization are available

Success criteria:

* The delivered CSS fix does not weaken cross-account protection
* A future account switch has a separate authorized design and test plan before code changes begin

Context references:

* `.copilot-tracking/research/2026-09-19/trailing-stops-configuration-issues-research.md` (Lines 168-198) - Required rebind safeguards and rejected alternatives

Dependencies:

* Provider identity-contract confirmation
* Product owner and security approval for the authorization model

## Implementation Phase 3: Final Validation

<!-- parallelizable: false -->

### Step 3.1: Run full project validation

After the focused E2E result is green and the incident has been recovered or captured for diagnosis, run repository-level build and tests. Use the documented single-node fallback only when the known intermittent child-node issue is observed.

Validation commands:

* `dotnet build` - Full repository build
* `dotnet test` - Full repository test suite
* `dotnet test -m:1` - Fallback only for documented child-node instability

Success criteria:

* Build and tests pass, or each pre-existing/environmental failure is reported with its command output and scope

Dependencies:

* Phase 1 implementation and validation
* Phase 2 recovery result or captured evidence

### Step 3.2: Resolve minor validation failures and report blockers

Fix isolated CSS, locator, tolerance, formatting, or compilation issues in the Phase 1 slice and rerun the failed focused check. Do not expand into identity normalization or rebinding when validation indicates an account-ID semantics issue. Record that issue as follow-on research with the captured provider evidence.

Success criteria:

* Minor defects are corrected and revalidated in the same focused scope
* Identity-policy changes remain blocked pending their explicit prerequisites

Dependencies:

* Step 3.1 result