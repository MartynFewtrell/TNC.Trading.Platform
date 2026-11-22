<!-- markdownlint-disable-file -->
# Implementation Details: Account Preferences UI Clarity

## Context Reference

Sources: `.copilot-tracking/research/2026-08-31/account-preferences-ui-clarity-research.md`, `.copilot-tracking/research/2026-08-31/account-preferences-ui-clarity-delivery-plan-research.md`, and `.copilot-tracking/research/subagents/2026-08-31/account-preferences-ui-clarity-plan-verification-research.md`.

## Implementation Phase 0: Establish Focused Baseline

<!-- parallelizable: false -->

### Step 0.1: Run the current account-preferences tests

Run the filtered bUnit class before editing runtime or test files. Record the result in the implementation execution log and classify unrelated failures without repairing them in this delivery slice.

Files:
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountPreferencesTests.cs` - Existing focused behavior baseline

Success criteria:
* The command runs without Docker, SQL Server, Keycloak, or live IG credentials
* The starting result is recorded; planning research observed 10 passed and 0 failed

Context references:
* `.copilot-tracking/research/subagents/2026-08-31/account-preferences-ui-clarity-plan-verification-research.md` (Lines 146-151) - Cheapest focused command

Dependencies:
* .NET SDK 10.0.200 selected through `global.json`

Validation commands:
* `dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj --filter "FullyQualifiedName~AccountPreferencesTests"`

## Implementation Phase 1: Define Pending and Confirmed State

<!-- parallelizable: false -->

### Step 1.1: Specify the page-state contract with focused tests

Add traceable tests for the unconfirmed initial state, authoritative application of enabled and disabled values, selection that changes only pending state, exact dirty-state comparison, and a later authoritative response that resets pending and confirmed values together. Use `MethodName_StateUnderTest_ExpectedResult` names and place an XML traceability comment immediately above every added or changed test. Cite the account-preferences clarity task; use existing requirement identifiers only when the test genuinely proves them.

Files:
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountPreferencesTests.cs` - Add page-state behavior coverage

Discrepancy references:
* `DD-03` - Do not invent or over-apply unrelated requirement identifiers

Success criteria:
* New tests fail only because explicit unconfirmed, pending, and confirmed state is absent
* Tests describe both Boolean values without changing transport contracts

Context references:
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPageViewModel.cs` (Lines 3-22) - Current single-Boolean state model
* `.copilot-tracking/research/2026-08-31/account-preferences-ui-clarity-delivery-plan-research.md` (Lines 169-190) - Phase 1 behavior requirements

Dependencies:
* Step 0.1 completion

### Step 1.2: Implement explicit confirmation state

Add nullable confirmed state (or an equivalent explicit confirmation representation), `HasConfirmedValue`, `HasPendingChange`, and a selection method. Make authoritative `Apply` update pending value, confirmed value, application status, and observation timestamp together. Do not alter API, application, or provider contracts.

Files:
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPageViewModel.cs` - Own pending and confirmed presentation state

Success criteria:
* No Boolean is represented as confirmed before a successful live preference response
* Selection retains the prior confirmed value
* Dirty state is true exactly when pending differs from confirmed
* Applying authoritative data synchronizes pending and confirmed state

Context references:
* `.copilot-tracking/research/2026-08-31/account-preferences-ui-clarity-research.md` (Lines 92-99) - Selected pending/confirmed pattern

Dependencies:
* Step 1.1 failing tests

### Step 1.3: Validate phase changes

Run the same filtered test class immediately after the state-model edit.

Validation commands:
* `dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj --filter "FullyQualifiedName~AccountPreferencesTests"` - Focused state contract

## Implementation Phase 2: Correct Presenter Transitions and Feedback Ownership

<!-- parallelizable: false -->

### Step 2.1: Specify transition and feedback behavior

Add focused tests for selection after success, update rejection, HTTP 409 authoritative mismatch, cancellation, dismissal, and confirmed update followed by history-refresh failure. Include assertions that selection clears transient save confirmation and the prior save error without erasing still-relevant load or history feedback. Do not add a differing-successful-HTTP-200 scenario because production maps provider mismatch to HTTP 409.

Files:
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountPreferencesTests.cs` - Presenter transition coverage

Discrepancy references:
* `DD-01` - Exercise the production-realistic HTTP 409 mismatch contract
* `DD-02` - Separate feedback ownership before selection clearing

Success criteria:
* New tests distinguish save failure from load/history feedback
* HTTP 409 preserves the prior confirmed state and exposes only urgent save failure
* Confirmed update plus history failure expects qualified success, not contradictory success and error
* `IsSaving` returns to false after success, expected failure, and owned cancellation

Context references:
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPagePresenter.cs` (Lines 3-53) - Current shared error and save/history boundary
* `.copilot-tracking/research/subagents/2026-08-31/account-preferences-ui-clarity-plan-verification-research.md` (Lines 227-252) - Contract and feedback corrections

Dependencies:
* Implementation Phase 1 completion

### Step 2.2: Separate feedback state and implement transitions

Distinguish durable load/history feedback from transient save error and save confirmation before wiring selection clearing. Add presenter operations for selection and confirmation dismissal. On save, clear transient feedback, call the existing update API, apply authoritative success, and then refresh history in a secondary expected-failure boundary. Preserve cancellation ownership and the `finally` reset of `IsSaving`.

Files:
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPagePresenter.cs` - Own corrected asynchronous transitions and feedback categories

Success criteria:
* Selection invalidates stale save confirmation and prior save error only
* Update rejection and HTTP 409 show `Unable to save and confirm account preferences.` without changing confirmed state
* Successful update applies the authoritative response before secondary history refresh
* History refresh failure after confirmation shows `Trailing stops preference saved and confirmed. Observed history could not be refreshed.` as the only transient save status
* Dismissal clears only transient confirmation and preserves application status and history

Context references:
* `.copilot-tracking/research/2026-08-31/account-preferences-ui-clarity-research.md` (Lines 176-184) - Selected presenter flow

Dependencies:
* Step 2.1 failing tests

### Step 2.3: Validate phase changes

Run the filtered account-preferences class immediately after the presenter edit.

Validation commands:
* `dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj --filter "FullyQualifiedName~AccountPreferencesTests"` - Focused transition contract

## Implementation Phase 3: Deliver Semantic Component Behavior

<!-- parallelizable: false -->

### Step 3.1: Specify component semantics and observable behavior

Replace checkbox-oriented assertions with bUnit checks for both radio labels, exactly one selected value, exact pending-state heading, last-confirmed text, pending cue, separated application status, persistent confirmation, dismissal, safe load failure, outbound JSON for `true` and `false`, and disabled fieldset and Save action while an update is in flight. Verify message removal and expected focus interop invocation without claiming bUnit proves real browser focus or assistive-technology announcements.

Files:
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountPreferencesTests.cs` - Replace obsolete checkbox assertions and add component coverage

Discrepancy references:
* `DD-04` - Keep actual focus and announcement verification in manual gates

Success criteria:
* Tests fail against the old checkbox markup for expected semantic reasons
* Tests verify serialized Boolean payloads through the existing sequenced HTTP handler
* Tests verify no editor, state heading, or Save action appears after initial preference-load failure

Context references:
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/SequencedHttpMessageHandler.cs` (Lines 5-39) - Existing request capture
* `.copilot-tracking/research/2026-08-31/account-preferences-ui-clarity-delivery-plan-research.md` (Lines 226-259) - Component acceptance behavior

Dependencies:
* Implementation Phase 2 completion

### Step 3.2: Replace the checkbox with explicit Boolean choices

Change the introduction to exactly `Live trailing stops control for the configured account.` Render the exact pending-state H2 only after a live value is confirmed. Use `InputRadioGroup<bool>` inside a disabled-while-saving `fieldset` with legend `Trailing stops`; map `Enabled` to `true` and `Disabled` to `false`. Render last-confirmed state and a non-live pending cue separately. Keep explicit Save available even when unchanged because repeated confirmed writes create valid observations.

Files:
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor` - Own semantic controls, copy, and conditional rendering

Success criteria:
* Both named choices remain visible and exactly one reflects loaded state
* Selection immediately changes `Trailing stops enabled` or `Trailing stops disabled`
* Last-confirmed text changes only after authoritative success
* Initial preference-load failure exposes urgent feedback without an actionable default `false`
* Existing Boolean request and response contracts remain unchanged

Context references:
* `.copilot-tracking/research/2026-08-31/account-preferences-ui-clarity-research.md` (Lines 137-161) - Selected radio and state-heading approach

Dependencies:
* Step 3.1 failing tests

### Step 3.3: Add dismissible confirmation and focus restoration

Keep persistent application status separate from a conditional atomic `role="status"` confirmation box. Add a visible button with accessible name `Dismiss save confirmation`. Store a Save `ElementReference`; dismissal clears confirmation and then focuses Save. Do not move focus when confirmation first appears.

Files:
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor` - Add status markup, dismissal callback, and focus restoration

Success criteria:
* Confirmation persists until dismissed or invalidated by selection
* Dismissal does not remove application status or history
* Dismissal requests focus restoration to Save after removing the dismiss control

Context references:
* `.copilot-tracking/research/2026-08-31/account-preferences-ui-clarity-research.md` (Lines 163-174) - Selected persistent inline status behavior

Dependencies:
* Step 3.2 completion

### Step 3.4: Validate phase changes

Run the filtered account-preferences class immediately after the Razor edits.

Validation commands:
* `dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj --filter "FullyQualifiedName~AccountPreferencesTests"` - Focused component behavior

## Implementation Phase 4: Add Scoped Responsive Styling

<!-- parallelizable: false -->

### Step 4.1: Implement the segmented visual treatment

Create a page-scoped stylesheet using two equal flexible tracks, connected option styling, selected-state treatment, visible focus through existing platform CSS variables, wrapping labels, and at least 44 by 44 CSS pixel targets. Give the status box flexible layout and stack controls at a narrow breakpoint. Avoid global selectors, generated Radzen classes, fixed widths, viewport-scaled fonts, and negative letter spacing.

Files:
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor.css` - New page-scoped selector and status styles
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountDetails/AccountDetails.razor.css` - Read-only local CSS precedent

Success criteria:
* Native radio semantics remain available without relying on CSS for meaning
* Focus and selection are distinguishable without color alone
* Labels can wrap without changing the control contract or overlapping adjacent content

Context references:
* `.copilot-tracking/research/2026-08-31/account-preferences-ui-clarity-delivery-plan-research.md` (Lines 263-285) - Styling constraints and acceptance criteria

Dependencies:
* Implementation Phase 3 completion

### Step 4.2: Run focused and Web project gates

Run the account-preferences class, all Web unit tests, the Web build, and format checks. Repair only isolated issues caused by this slice.

Validation commands:
* `dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj --filter "FullyQualifiedName~AccountPreferencesTests"`
* `dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj`
* `dotnet build src/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.csproj`
* `dotnet format src/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.csproj --verify-no-changes --no-restore`
* `dotnet format test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj --verify-no-changes --no-restore`

## Implementation Phase 5: Align Wiki Documentation

<!-- parallelizable: true -->

### Step 5.1: Update operator guidance

Replace ambiguous toggle language with the delivered Enabled/Disabled selector, pending versus last-confirmed distinction, explicit save workflow, persistent dismissal behavior, safe failed-load state, and qualified history-refresh message. Preserve the Test-only safety boundary.

Files:
* `docs/wiki/operator-guide.md` - Update the account-preferences workflow at current lines 565-580

Success criteria:
* Operator guidance matches delivered wording and interaction behavior
* Documentation does not imply a backend, provider-authority, or environment-safety change

Context references:
* `.copilot-tracking/research/subagents/2026-08-31/account-preferences-ui-clarity-plan-verification-research.md` (Lines 118-130) - Wiki ownership and unchanged pages

Dependencies:
* Implementation Phase 4 completion

### Step 5.2: Update testing guidance

Document deterministic bUnit coverage for radio semantics, pending/confirmed transitions, outbound Boolean values, save failure, HTTP 409 mismatch, partial success, dismissal, and safe load failure. Retain the rule that default automation does not contact real IG.

Files:
* `docs/wiki/testing-and-quality.md` - Update account-preferences coverage at current lines 553-566

Success criteria:
* Testing guidance describes the implemented deterministic coverage
* Browser and assistive-technology checks are not misrepresented as unit-test guarantees

Context references:
* `.copilot-tracking/research/subagents/2026-08-31/account-preferences-ui-clarity-plan-verification-research.md` (Lines 123-130) - Testing documentation scope

Dependencies:
* Implementation Phase 4 completion

### Step 5.3: Validate documentation changes

Join Steps 5.1 and 5.2, then check whitespace and local Markdown link/image targets. The repository has no pinned Markdown linter; use the optional `markdownlint-cli2` fallback only when full syntax linting is required and Node.js plus network or cached package access are available. Do not reopen a completed work package or create a new numbered work package.

Validation commands:
* `git diff --check -- docs/wiki/operator-guide.md docs/wiki/testing-and-quality.md`
* `$files = @('docs/wiki/operator-guide.md', 'docs/wiki/testing-and-quality.md'); $broken = foreach ($file in $files) { $base = Split-Path $file; $text = Get-Content -Raw $file; foreach ($match in [regex]::Matches($text, '\[[^\]]+\]\((?<target>[^)\s]+)(?:\s+"[^"]*")?\)')) { $target = $match.Groups['target'].Value; if ($target -notmatch '^(?:[a-z][a-z0-9+.-]*:|#)') { $path = ($target -split '#', 2)[0]; if ($path -and -not (Test-Path (Join-Path $base ([uri]::UnescapeDataString($path))))) { "$file -> $target" } } } }; if ($broken) { $broken; exit 1 }`
* `npx --yes markdownlint-cli2@latest "docs/wiki/operator-guide.md" "docs/wiki/testing-and-quality.md"` - Optional unpinned fallback

## Implementation Phase 6: Complete Repository and Accessibility Validation

<!-- parallelizable: false -->

### Step 6.1: Run the repository quality gate

Use the existing quality-gate script so solution build and test evidence is written under `artifacts/quality-gates/account-preferences-ui-clarity/`. Ensure Docker and distributed-suite prerequisites are available. If the documented intermittent MSBuild child-node failure occurs, retain the failed evidence and run the serialized test fallback.

Validation commands:
* `.github/scripts/Invoke-CleanArchitectureQualityGate.ps1 -GateId account-preferences-ui-clarity`
* `dotnet test TNC.Trading.Platform.slnx -m:1` - Fallback only for the documented child-node failure

Success criteria:
* Solution build and tests pass, or unrelated/environmental blockers are recorded with evidence
* No live IG credentials appear in tests, screenshots, logs, or artifacts

Context references:
* `.github/scripts/Invoke-CleanArchitectureQualityGate.ps1` (Lines 1-92) - Repository-wide evidence workflow
* `docs/wiki/testing-and-quality.md` (Lines 424-453) - Distributed-suite prerequisites

Dependencies:
* Implementation Phases 1 through 5 completion
* Docker, SQL Server, Keycloak, and Playwright prerequisites for distributed suites

### Step 6.2: Perform browser reflow and keyboard checks

Run the supported AppHost and use the Aspire-assigned Web URL. Validate loaded enabled and disabled states, native Tab/Arrow/Space radio operation, visible focus, pending heading, unchanged last-confirmed text before save, in-flight disabled controls, persistent success, dismissal focus restoration, controlled provider failure, and qualified history-refresh failure. Repeat at desktop width, 320 CSS pixels, and 400 percent zoom.

Validation commands:
* `dotnet run --project src/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.csproj`

Success criteria:
* No horizontal document scrolling, clipping, overlap, or obscured focus at 320 CSS pixels or 400 percent zoom
* Browser focus returns to Save after dismissal
* No live-IG-dependent Playwright test is added

Context references:
* `.copilot-tracking/research/2026-08-31/account-preferences-ui-clarity-delivery-plan-research.md` (Lines 360-369) - Manual browser workflow

Dependencies:
* Step 6.1 completion or a documented environment-only blocker
* Configured local Test account for manual validation; never place credentials in artifacts

### Step 6.3: Verify assistive-technology behavior

Verify group naming, selected state, polite confirmation announcement, urgent failure announcement, dismissal accessible name, and focus restoration using NVDA with Firefox or Chrome and Narrator with Edge.

Success criteria:
* Both screen-reader combinations expose the fieldset legend and checked radio correctly
* Success is announced without moving focus
* Dismissal is named and returns focus to Save

Discrepancy references:
* `DR-01` - Manual validation remains required because bUnit cannot prove browser and assistive-technology behavior

Dependencies:
* Step 6.2 completion
* NVDA, Narrator, and supported browsers

### Step 6.4: Fix minor issues and report blockers

Apply only isolated corrections found by validation. Record issues requiring wider architecture, provider-fixture, or test-infrastructure work as follow-on items rather than expanding this delivery slice.

Success criteria:
* Minor issues caused by this slice are corrected and relevant gates rerun
* Larger blockers are documented with affected files, evidence, and recommended next planning step

## Implementation Risks

* R-01: Feedback separation can regress existing load or history messaging
	* Likelihood: medium
	* Impact: high because relevant operator warnings could disappear or conflict
	* Trigger: Selection, dismissal, or save transitions clear or render feedback from the wrong category
	* Mitigation: Add presenter transition tests before changing feedback ownership and rerun the focused class immediately
	* Owner: Implementer for Phase 2
* R-02: Pending UI state can be mistaken for provider-confirmed state
	* Likelihood: medium
	* Impact: high because the page controls a safety-relevant trading preference
	* Trigger: Heading, last-confirmed text, or success feedback changes before authoritative update success
	* Mitigation: Keep separate pending and confirmed values; test HTTP 409 and partial-success paths
	* Owner: Implementer for Phases 1 through 3
* R-03: Native radio styling can hide focus, clip labels, or create narrow-layout overflow
	* Likelihood: medium
	* Impact: medium
	* Trigger: Failed keyboard, 320 CSS pixel, or 400 percent zoom checks
	* Mitigation: Preserve native inputs, use flexible tracks and wrapping labels, and require manual reflow and focus gates
	* Owner: Implementer for Phases 3, 4, and 6
* R-04: Final distributed validation can fail for environmental reasons unrelated to this UI slice
	* Likelihood: medium
	* Impact: medium because completion evidence can be delayed
	* Trigger: Docker, Keycloak, SQL Server, Playwright, or documented MSBuild child-node failure blocks the quality gate
	* Mitigation: Retain failed evidence, distinguish environmental failures, and use the documented serialized fallback only for child-node failure
	* Owner: Implementer for Phase 6
* R-05: Manual provider and accessibility validation can expose credentials or sensitive account data in artifacts
	* Likelihood: low
	* Impact: high
	* Trigger: Screenshots, logs, test data, or transcripts contain credentials or account identifiers
	* Mitigation: Use configured local secrets, sanitize evidence, and stop before capturing sensitive output
	* Owner: Implementer for Phase 6

## Rollback Boundaries

* Phase 0: No rollback; the phase is read-only
* Phase 1: Revert the view-model state contract and its direct tests; no component or transport behavior depends on it yet
* Phase 2: Revert presenter transition and feedback changes with their tests while retaining the independently valid view-model contract
* Phase 3: Revert Razor behavior and component assertions together; retain validated view-model and presenter contracts
* Phase 4: Delete the scoped stylesheet and revert class-only markup changes; native radio semantics remain usable
* Phase 5: Revert only the two wiki sections when runtime implementation is backed out
* Phase 6: Do not revert validated implementation for environmental failures; retain evidence, report the blocker, and revert only isolated corrections that introduced regressions

## Implementation Handoff

Start at Phase 0 and execute phases in order. Within Phase 5, Steps 5.1 and 5.2 may run concurrently against separate wiki files; join before Step 5.3. Use the same focused test command immediately after each behavior edit before broadening scope.

Expected evidence:
* Focused account-preferences baseline and post-phase test results
* Web unit-test, build, and format results
* Repository quality-gate transcript and TRX output under `artifacts/quality-gates/account-preferences-ui-clarity/`
* Sanitized manual checklist results for desktop, 320 CSS pixels, 400 percent zoom, keyboard operation, NVDA, and Narrator

Stop conditions:
* Stop the current slice after three failed repair attempts and record the blocker instead of expanding scope
* Stop before changing API, application, provider, persistence, configuration, schema, package, or project contracts; those changes require new research and planning
* Stop before adding live IG credentials, account identifiers, or sensitive output to tests or artifacts
* Treat missing distributed or assistive-technology prerequisites as documented blockers, not permission to weaken validation criteria

Resolved decision:
* Keep unchanged saves available because confirmed writes create valid observations; revisit only with an explicit product requirement

## Dependencies

* .NET SDK 10.0.200
* Existing Blazor, Radzen 10.3.2, bUnit, and xUnit project dependencies
* Docker and documented distributed-test prerequisites for the repository quality gate
* NVDA, Narrator, Firefox or Chrome, and Edge for manual accessibility completion

## Success Criteria

* The explicit Boolean selector, state-driven heading, pending/confirmed distinction, and dismissible status are implemented entirely in the Web presentation boundary
* The existing Boolean transport and authoritative provider-confirmation workflow remain unchanged
* Focused, Web-project, repository, responsive, keyboard, and assistive-technology gates produce recorded evidence
* The two affected wiki sections describe the delivered behavior before work is considered complete
