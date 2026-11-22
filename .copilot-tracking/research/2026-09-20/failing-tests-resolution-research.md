<!-- markdownlint-disable-file -->
# Task Research: Failing Test Resolution

Investigation of the three reported failing tests in the current workspace. A
full current test run reproduced two failures and 612 passing tests. The third
reported failure has no retained output and did not recur.

## Task Implementation Requests

* Determine the cause of each reported failing test
* Recommend whether each resolution belongs in the test or the platform

## Scope and Success Criteria

* Scope: The Account Preferences failures reproduced by the current test run
  and the unverified third report
* Assumptions: The workspace's uncommitted Account Preferences layout changes
  represent the intended pending UI redesign
* Success criteria:
  * Classify each reproducible failure as a platform defect or stale test
    expectation
  * Recommend the smallest behavior-preserving resolution
  * Avoid classifying the unreproduced failure without evidence

## Outline

1. Record the observed test results and code-path evidence.
2. Evaluate whether each expectation remains consistent with the platform.
3. Select a resolution and identify the follow-up validation.

## Potential Next Research

* Capture the third failure's test name and stack trace from its originating
  run
  * Reasoning: The current full run has only two failures, so no evidence
    exists to determine a cause or owner for the reported third failure
  * Reference: .copilot-tracking/research/subagents/2026-09-20/failing-tests-investigation.md

## Research Executed

### File Analysis

* test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/AccountPreferences/AccountPreferencesE2ETests.cs:66-118
  * Contains both reproduced failing E2E assertions: the provider warning at
    line 77 and desktop radio positioning at line 112
* src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:16-20
  * Renders `account-preferences-warning` only when `FailureSummary` is
    populated
* src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPagePresenter.cs:103-110
  * Applies the check-status response to the state rendered by the component
* src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPageViewModel.cs:20-37
  * Carries `FailureSummary` through the page model
* src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs:146-163
  * Converts a provider failure into a successful account-preferences state
    response with `VerificationFailed` and `FailureSummary`
* test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/AccountPreferences/AccountPreferencesFunctionalTests.cs:70-90
  * Establishes the API contract for the unavailable-provider scenario: HTTP
    200 plus a non-empty unavailable failure summary
* test/Shared/AccountPreferences/ControllableIgProvider.cs:121-134
  * Returns HTTP 503 for all provider routes in the unavailable state,
    including session creation
* src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgAccountPreferencesGateway.cs:20-31
  * Maps session HTTP failure to `IG account preferences service is unavailable.`
* src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor.css:76-93
  * Uses a vertical control row and wrapping flex layout for the radio choices

### Code Search Results

* `account-preferences-warning`
  * The warning locator is created only by the Account Preferences Razor
    component and is asserted by the failing E2E test
* `FailureSummary`
  * The API, presenter, view model, and component comprise the relevant
    end-to-end state flow

### Project Conventions

* Standards referenced: .github/copilot-instructions.md
* Instructions followed: .github/instructions/folders.instructions.md and
  the Task Researcher operating constraints

## Key Discoveries

### Current Result Set

The current full run reported 612 passed tests and two failures. The only newer
persisted desktop-geometry TRX is passing. No terminal, IDE session log, or
artifact retained the claimed third failure. A 31 August quality-gate TRX
contains 11 failures but predates the current branch's 18 September Account
Preferences changes and cannot establish current behavior.

### Provider Warning Flow

The API deliberately models an unavailable provider as a valid check-status
response rather than as a failed HTTP request. The component's rendering rule
is therefore correct in principle: a non-empty `FailureSummary` must cause an
operator-visible warning. The failing E2E test cannot find that element,
showing that the web flow fails to propagate or retain that response state.

The test's exact expected sentence is not stable. The controllable provider
returns 503 before an observation can be made, so the gateway's resulting
summary is `IG account preferences service is unavailable.` The business
contract is unavailability, not the specific observation-level phrase.

### Responsive Choice Layout

The pending CSS redesign changed the choices from a two-column grid to a
wrapping flex container. At the E2E viewport, `Disabled` can legally wrap below
`Enabled`; consequently, it need not have a greater X coordinate. The geometry
test was added in the same uncommitted working tree as the layout and asserts a
constraint the CSS does not promise.

## Technical Scenarios

### Provider-Unavailable Warning

The check-status endpoint returns a successful state response even when the IG
provider cannot be reached. The presenter must retain its `FailureSummary`, and
the component must render the warning from that state.

**Requirements:**

* Show an operator-visible warning whenever the check-status state includes a
  non-empty `FailureSummary`
* Preserve the API's unavailable-provider behavior
* Keep the browser test focused on the user-facing availability outcome

**Preferred Approach:**

* Change the Web check-status flow so the successful response's
  `FailureSummary` reaches the presenter state used by the Razor component.
  This is a platform defect because the public API contract and the component
  rendering rule agree, but the warning is absent in the browser.

```text
src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/
  AccountPreferencesPagePresenter.cs     # repair state propagation
test/TNC.Trading.Platform.Web/
  TNC.Trading.Platform.Web.E2ETests/AccountPreferences/
    AccountPreferencesE2ETests.cs         # assert unavailable warning semantic
```

**Implementation Details:**

1. Trace the check-status handler and make its success branch assign the
   response `FailureSummary` to the state passed to the page.
2. Add a focused presenter or component test using a successful response with
   `VerificationFailed` and a non-empty `FailureSummary`; assert that the
   warning state is retained or rendered.
3. Change the E2E assertion from the exact observation-level sentence to an
   assertion that the warning exists and contains `unavailable`.
4. Run the focused unit or component test followed by the provider-unavailable
   E2E test.

#### Considered Alternatives

* Update only the E2E expected text. Rejected because the warning locator is
  absent, which leaves the operator without required failure feedback.
* Change the endpoint to return an HTTP failure. Rejected because the existing
  functional contract intentionally supplies a check-status state containing
  remediation information.

### Desktop Choice Geometry

The UI now allows choice controls to wrap responsively. A desktop test must
assert the accepted visual behavior, not a previous grid implementation.

**Requirements:**

* Preserve accessible choice order, usable hit areas, and Save placement
* Do not require horizontal placement while the CSS permits wrapping

**Preferred Approach:**

* Update the E2E test to validate the intended responsive layout. This is a
  stale test expectation, provided product design accepts wrapping at the
  desktop viewport.

```text
test/TNC.Trading.Platform.Web/
  TNC.Trading.Platform.Web.E2ETests/AccountPreferences/
    AccountPreferencesE2ETests.cs         # replace X-position assertion
```

**Implementation Details:**

1. Remove the assertion requiring `Disabled` to be to the right of `Enabled`.
2. Assert source or accessible order, visible and non-overlapping choice hit
   areas, Save control visibility, and absence of horizontal overflow.
3. Run the focused geometry test at its established wide viewport.

#### Considered Alternatives

* Restore a non-wrapping desktop grid and keep the X-position assertion.
  Reject unless the product requirement explicitly says both controls must
  appear on one row at 1280px. The current CSS intentionally allows wrapping.

### Reported Third Failure

No technical scenario can be selected for the third report because neither a
test name nor a failure trace is available. Do not alter tests or platform code
for it. Preserve the test output or TRX, then rerun that named test in
isolation and repeat this classification process.

## Selected Resolution

1. Repair the Web Account Preferences check-status state propagation and add a
   focused state-to-warning test. Update the related E2E test to assert
   unavailability rather than an unstable exact sentence.
2. Update the desktop geometry E2E test to assess responsive usability rather
   than right-of positioning. Do not change the CSS unless product design
   requires a same-row desktop layout.
3. Obtain evidence for the third reported failure before making any change.

## Follow-Up Validation

* Run the focused warning test after the platform correction.
* Run `AccountPreferences_ShouldShowExactWarning_WhenProviderIsUnavailableDuringRemediation`
  after updating its semantic assertion.
* Run `AccountPreferences_ShouldAlignChoiceAndSaveControls_WhenDesktopViewportIsWide`
  after replacing its geometry condition.
* Re-run the complete affected Web test project and retain its TRX result.