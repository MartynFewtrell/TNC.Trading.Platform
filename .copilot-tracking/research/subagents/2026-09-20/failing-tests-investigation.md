---
title: Failing Tests Investigation
description: Evidence-based investigation of the three currently failing workspace tests
author: GitHub Copilot
ms.date: 2026-09-20
ms.topic: troubleshooting
keywords:
  - tests
  - investigation
  - failures
---

## Scope

Investigate the three currently failing tests in the workspace. For each failure,
record the exact test name, observed failure output, owning test and implementation
paths, whether the expectation is stale or the platform is defective, and a
recommended resolution.

## Investigation Status

Complete for the reproducible workspace state. The full current test run produced
612 passes and two failures, not the reported three. The third reported failure
could not be located in retained terminal, IDE, workspace, or artifact output.

## Evidence Sources

* Current execution: the VS Code test runner executed all discovered tests on
  2026-09-20 and reported 612 passed, 2 failed.
* Current persisted result: test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/TestResults/desktop-geometry.trx:8-9
  is the newest TRX, dated 2026-09-19, and records one passing geometry test.
* Historical artifact: artifacts/quality-gates/account-preferences-ui-clarity/20260831-124715/trx/martynfewtrell_TNCITWOR001_2026-08-31_12_48_13_net10.0.trx
  records 11 failures. It predates the current branch's 2026-09-18 Account
  Preferences changes and is not evidence for the present two failures.
* No retained VS Code session log contained a third failure. The source worktree
  is dirty, including the Account Preferences migration and the newly added
  geometry test.

## Reproduced Failures

### Provider-Unavailable Warning Is Not Rendered

* Test: `AccountPreferences_ShouldShowExactWarning_WhenProviderIsUnavailableDuringRemediation`
* Owning test: test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/AccountPreferences/AccountPreferencesE2ETests.cs:66-77
* Reproduced failure: Playwright waited five seconds for
  `data-testid="account-preferences-warning"` to contain
  `IG account preference observation was not available.` The locator was absent
  (`But was: 'null'`), with the assertion at line 77.
* Classification: platform behavior is defective at the web E2E surface, not a
  stale locator. The Razor page only creates this element when
  `Presenter.State.FailureSummary` is non-empty:
  src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:16-20.
  The presenter applies the check-status response into that state:
  src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPagePresenter.cs:103-110,
  and the view model preserves `FailureSummary`:
  src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPageViewModel.cs:20-37.
* Contract evidence: provider failure is explicitly converted to a state response
  with `VerificationFailed` and `FailureSummary` by the endpoint:
  src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs:146-163.
  The functional test validates the same unavailable-provider scenario and
  expects HTTP 200 plus a non-empty unavailable failure summary:
  test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/AccountPreferences/AccountPreferencesFunctionalTests.cs:70-90.
* Relevant detail: the E2E provider returns `503` for every route when marked
  unavailable, including session creation:
  test/Shared/AccountPreferences/ControllableIgProvider.cs:121-134. The gateway
  turns a session HTTP failure into the alternate summary `IG account preferences
  service is unavailable.`:
  src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgAccountPreferencesGateway.cs:20-31.
  Therefore, after rendering is repaired the E2E assertion should verify a
  warning with an unavailable semantic rather than pinning the request-level
  `observation was not available` wording.
* Recommendation: fix the Web check-status flow so a successful API state with
  `FailureSummary` always results in the warning rendering. Add a focused bUnit
  presenter/component test for that response shape. Then adjust the E2E test to
  assert the rendered warning contains `unavailable` unless the product contract
  intentionally requires a single exact provider-failure message.

### Desktop Radio-Order Expectation Contradicts the New Layout

* Test: `AccountPreferences_ShouldAlignChoiceAndSaveControls_WhenDesktopViewportIsWide`
* Owning test: test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/AccountPreferences/AccountPreferencesE2ETests.cs:87-118
* Reproduced failure: `Assert.True()` expected `disabledBox.X > enabledBox.X` at
  line 112, but received `False`.
* Classification: stale test expectation caused by the uncommitted intended UI
  redesign. The current CSS changes the radio container from a two-column grid
  to a wrapping flex layout:
  src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor.css:89-93.
  It also changes the parent into a vertical control row:
  src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor.css:76-82.
  At the observed rendering, `Disabled` wraps below `Enabled`, so its X position
  is not greater.
* History: the failing geometry test is uncommitted, while the prior Account
  Preferences UI came from commit `48fc3b7` on 2026-09-18. The migration and
  account-operation changes were introduced by `9152bbc` later the same day.
  The layout CSS and geometry test are current working-tree changes.
* Recommendation: revise the uncommitted E2E geometry test to match the accepted
  responsive design. Either require same-row placement and restore a layout rule
  that guarantees it at 1280px, or accept vertical wrapping and assert the
  accessible order, minimum hit areas, Save placement, and no horizontal
  overflow. Do not retain a right-of assertion when the CSS permits wrapping.

### Reported Third Failure

* Status: not reproducible and not evidenced.
* Evidence: a complete current test run found two failures only. The newest
  persisted TRX is a passing desktop-geometry run, and no matching failure was
  present in the current VS Code session logs. The only later failure artifact
  available is the unrelated 31 August quality-gate run with 11 failures.
* Classification: undetermined. There is insufficient failure output to decide
  whether the third report is a stale expectation or a platform defect.
* Recommendation: preserve the terminal output or TRX from the run that reports
  three failures, then re-run that named test in isolation before changing code.

## Relevant History

* `48fc3b7` (2026-09-18): enhanced the Account Preferences UI with tabbed
  navigation and accessibility improvements.
* `9152bbc` (2026-09-18): added Account Preferences operations, migration, and
  unit tests, including account-mismatch and provider-failure behavior.

## Follow-On Questions

* Which exact test name and failure text constituted the reported third failure?
  The current workspace run does not contain it.

## Findings

Pending evidence collection.

## Follow-On Questions

None identified.