---
title: Account Preferences Tests and Documentation Research
description: Delegated research into AccountPreferences automated test failures, test coverage, supporting infrastructure, artifacts, and wiki documentation.
ms.date: 2026-09-18
ms.topic: research
---

## Scope

* Inspect AccountPreferences automated tests, test setup and support, the current failure context, relevant quality-gate artifacts, and wiki documentation covering account preferences, configuration, and IG behavior.
* Identify evidence-backed likely causes of the present `AccountPreferencesE2ETests` failure.
* Recommend targeted unit, integration, and E2E test changes, plus precise wiki documentation updates.

## Research Status

Complete. Production and test code were not modified. The filtered browser
suite was rerun to collect the present failure evidence.

## Repository Conventions

* Keep unit tests in-process and infrastructure-free; favor them for policy,
	mapping, and presenter behavior.
* Use Aspire-hosted tests for closed-box distributed behavior, with
	fixture-owned resources, random ports, named resources, readiness checks,
	cleanup, and no process-wide configuration.
* Use `Microsoft.Playwright.Xunit`, inherit `PageTest`, group interaction in
	`Test.StepAsync`, prefer role/label/text locators, and assert with Playwright
	web-first expectations. Do not mask a failure with sleeps or larger global
	timeouts.
* Test method names follow
	`MethodName_StateUnderTest_ExpectedResult`; each test needs an XML comment
	covering traceability, verified behavior, expected outcome, and regression
	risk.
* Account Preferences is a vertical slice. Unit tests should exercise the
	Application handler and inward-owned ports directly; API, browser, and IG
	adapter concerns remain at their boundary-specific levels.

Evidence: .github/instructions/tests.instructions.md,
.github/instructions/playwright.instructions.md,
.github/instructions/aspire-tests.instructions.md,
.github/instructions/csharp.instructions.md, and
.github/instructions/architecture.instructions.md.

## Automated Test Inventory

* Application unit coverage in
	test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/AccountPreferences/
	covers Test-only policy, legacy Demo-to-Test presentation, nullable Boolean
	validation, SQL-only query behavior, older synchronous update outcomes,
	observation history paging, and telemetry redaction.
* Infrastructure unit coverage in
	test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/Integrations/Ig/IgAccountPreferencesGatewayTests.cs
	covers IG session/account binding, GET parsing/classification, a limited
	401 replay, and remediation's GET-PUT-GET sequence. The related retention
	store test is
	test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/EfTrailingStopsPreferenceObservationStoreTests.cs.
* SQL integration coverage in
	test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/AccountPreferencesSqlIntegrationTests.cs
	covers migrations, history ordering/partition/cursors/retention, desired
	state durability, due-work claiming, lease contention, and stale completion.
* API unit coverage in
	test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/AccountPreferencesEndpointTests.cs
	and AccountPreferencesEndpointMappingTests.cs covers nullable input,
	history validation, Problem Details mappings, and legacy response mapping.
* Web bUnit coverage in
	test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountPreferencesTests.cs
	covers page state, initial load, save feedback, history paging, cancellation,
	and markup-level tab structure. It tests a click on the second tab but not
	browser keyboard activation.
* Closed-box functional coverage in
	test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/AccountPreferences/AccountPreferencesFunctionalTests.cs
	verifies fresh-install SQL-only reads during outage and provider recovery
	through verification retry. Browser coverage is in
	test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/AccountPreferences/AccountPreferencesE2ETests.cs.

## Present E2E Failure

The rerun of the requested filtered E2E suite produced five failures and no
passes. This is direct evidence, not an inferred historical result.

* `AccountPreferences_ShouldActivateObservedHistory_WhenHistoryTabIsSelected`
	fails at AccountPreferencesE2ETests.cs:51: after `Space`, the `Observed
	history` tab remains `aria-selected="false"`.
* `AccountPreferences_ShouldNavigateTabsWithKeyboard_WhenSettingsTabHasFocus`
	fails at AccountPreferencesE2ETests.cs:76: after `ArrowRight`, the same tab
	remains unselected.
* `AccountPreferences_ShouldExposeAccessibleHistoryControls_WhenHistoryTabIsActive`
	fails at AccountPreferencesE2ETests.cs:94 because the history table never
	becomes visible.
* `AccountPreferences_ShouldDisableLoadOlderObservations_WhenNoNextCursorExists`
	fails at AccountPreferencesE2ETests.cs:112 because the history button is not
	found.
* `AccountPreferences_ShouldShowDesiredAndObservedLabels_WhenProviderIsUnavailable`
	fails at AccountPreferencesE2ETests.cs:28 because exact text `Unconfigured`
	is not present.

The first two failures establish the primary cause: the real browser does not
activate the Radzen tab from the key interactions the tests use. The third and
fourth are consequential failures, not separate history defects. Source at
src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:18
binds `RadzenTabs` to `selectedTabIndex`; the browser output confirms its
second tab remains unselected. The bUnit click coverage at
test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountPreferencesTests.cs:176
does not disprove this keyboard defect.

The fifth failure is distinct. The E2E fixture makes no explicit SQL-state
arrangement, while the test requires a fresh `Unconfigured` projection:
test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/Authentication/RealAuthenticationE2ETestFixture.cs:16-25.
AppHost selects persistent SQL unless `AppHost:UsePersistentSqlState` is
explicitly false: src/TNC.Trading.Platform.AppHost/AppHostInfrastructureRegistration.cs:14-29.
The fixture does not provide that setting, making persistent state the likely
cause of this failure. Set session lifetime explicitly and assert the response
state through an owned setup action before using it as a UI precondition. The
test run did not capture the rendered status, so the exact non-Unconfigured
value remains unproven.

## Additional Contract Risks

* The controlled provider accepts `GET/PUT /gateway/deal/preferences` and
	returns `{"enabled":...}`: test/Shared/AccountPreferences/ControllableIgProvider.cs:91-93.
	The IG adapter's unit test establishes the production GET route as
	`/gateway/deal/accounts/preferences` and its required JSON member as
	`trailingStopsEnabled`: test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/Integrations/Ig/IgAccountPreferencesGatewayTests.cs:53-68.
	Any closed-box recovery/reconciliation test that reaches the real adapter
	will therefore get a `404` or malformed payload from this double. The
	existing functional recovery test cannot prove the intended success path
	until this double matches the adapter contract.
* The UI checks `PendingVerification`:
	src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:31.
	The Application enum exposes `Pending`, and the API/wiki contract documents
	`Pending`: src/TNC.Trading.Platform.Application/Features/AccountPreferences/ReconcileAccountPreferencesHandler.cs:25,
	docs/wiki/api-reference.md:555-559. This means the pending message cannot
	render for a normal pending response.
* The Web client update sends only `TrailingStopsEnabled`:
	src/TNC.Trading.Platform.Web/PlatformApiClient.cs:151-158. The API handler
	passes `request.AccountId` and `request.Actor` to the Application request:
	src/TNC.Trading.Platform.Api/Features/Platform/UpdateAccountPreferencesEndpointHandler.cs:7-15.
	The desired-state path dereferences both values using null-forgiving syntax:
	src/TNC.Trading.Platform.Application/Features/AccountPreferences/UpdateAccountPreferencesHandler.cs:18-24.
	No test currently proves a browser-initiated update has an account and actor
	or that their absence is rejected before persistence. This is a likely
	production workflow failure and should be decided at the operation contract,
	not patched only in the browser.
* Current Account Preferences handler tests retain an older synchronous
	gateway update/readback branch, while the durable-state branch commits SQL
	and returns pending without provider I/O:
	src/TNC.Trading.Platform.Application/Features/AccountPreferences/UpdateAccountPreferencesHandler.cs:14-33.
	The docs describe the durable asynchronous model. Tests need to pivot toward
	the current branch and its dedicated reconciler.
* There is no Account Preferences unit test for
	`ReconcileAccountPreferencesHandler`; repository search found no matching
	test class. This leaves lease contention, reread-before-observe, due-work
	selection, retry schedule, result classification, bounded failure summary,
	and stale completion behavior only partially or not at all covered at the
	Application boundary.

## Recommended Test Changes

### Unit Tests

Add `ReconcileAccountPreferencesHandlerTests.cs` under
test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/AccountPreferences/
with fake state store, gateway, lease, configuration service, and fixed time.

* `HandleAsync_ShouldReturnNoWorkWithoutGatewayCall_WhenNoDueDesiredStateExists`
* `HandleAsync_ShouldReturnNoWorkWithoutGatewayCall_WhenRequestedAccountDoesNotMatchState`
* `HandleAsync_ShouldReturnLeaseUnavailableWithoutGatewayCall_WhenLeaseIsHeld`
* `HandleAsync_ShouldNotObserve_WhenStateRevisionOrAccountChangesAfterLeaseAcquisition`
* `HandleAsync_ShouldCompleteInSyncAndClearRetryMetadata_WhenObservationMatchesDesiredState`
* `HandleAsync_ShouldCompleteDriftedWithoutProviderWrite_WhenObservationDiffersFromDesiredState`
* `HandleAsync_ShouldScheduleFiveSecondRetryAndPreserveLastObservation_WhenFirstObservationFails`
* `HandleAsync_ShouldScheduleThirtySecondTwoMinuteAndTenMinuteRetries_WhenFailuresRepeat`
* `HandleAsync_ShouldBoundFailureSummaryTo160Characters_WhenProviderReasonIsLong`
* `HandleAsync_ShouldNotReportApplied_WhenCompletionRejectsStaleDesiredRevision`

Update `AccountPreferencesHandlerTests.cs` with durable update tests:

* `UpdateAccountPreferences_ShouldCommitPendingDesiredStateWithoutGatewayCall_WhenRequestIsValid`
* `UpdateAccountPreferences_ShouldReturnConflictWithoutGatewayCall_WhenExpectedRevisionIsStale`
* `UpdateAccountPreferences_ShouldRejectLiveBeforeCommitOrGatewayCall_WhenEnvironmentIsUnsupported`
* `UpdateAccountPreferences_ShouldRejectMissingAccountOrActor_WhenDurableStateIsRequested`

Update Web bUnit coverage:

* `Render_ShouldShowPendingVerificationMessage_WhenResponseStatusIsPending`
	to lock the canonical status spelling.
* `Save_ShouldSendConfiguredAccountAndActor_WhenOperatorUpdatesPreference`, or,
	if the API derives these values server-side,
	`Save_ShouldNotSendAccountOrActor_WhenServerDerivesIdentityAndTarget`.
	The implementation must choose one contract before this test is written.
* `Render_ShouldShowRetryAndRemediateActions_WhenStateIsPendingOrDrifted`
	with commands disabled only while their operation is active.

### Integration and Functional Tests

First correct the controlled provider contract and its test:

* Rename the provider test to
	`Start_ShouldServeAccountPreferencesContract_WhenListenerStarts`.
* Assert `GET /gateway/deal/accounts/preferences` returns
	`{"trailingStopsEnabled":false}` and `PUT` changes the same property.
* Retain the occupied-port test as
	`Start_ShouldRetryWithFreshListener_WhenFirstCandidatePortIsOccupied`.

Then expand `AccountPreferencesFunctionalTests.cs`:

* `AccountPreferences_ShouldReturnPendingWithoutProviderCall_WhenDesiredStateIsCommittedDuringProviderOutage`
* `AccountPreferences_ShouldConvergeToInSyncAfterVerificationRetry_WhenProviderRecoversAndObservedValueMatches`
* `AccountPreferences_ShouldReturnDriftedWithoutProviderWrite_WhenObservedValueDiffersFromDesiredState`
* `AccountPreferences_ShouldRejectStaleRemediationWithoutProviderCall_WhenDesiredRevisionChanged`
* `AccountPreferences_ShouldPerformSingleGetPutGet_WhenRevisionBoundRemediationCorrectsDrift`
* `AccountPreferences_ShouldRetainDesiredProjectionWhenVerificationFails_WhenProviderIsUnavailable`

Each case should use the fixture-owned provider ledger to assert the exact
external sequence. Create a fresh session-lifetime SQL fixture per collection
or expose an explicit API-only setup/reset arrangement; do not rely on test
ordering or a shared prior write.

### Browser E2E Tests

Repair the current browser suite only after deciding the tab keyboard contract.
Do not replace keyboard tests with click-only tests if arrow navigation is a
requirement.

* Keep
	`AccountPreferences_ShouldNavigateTabsWithKeyboard_WhenSettingsTabHasFocus`
	as the regression test. Verify that focus and `aria-selected` move with the
	chosen documented key after the UI/Radzen event binding is fixed.
* Change the non-keyboard activation case to
	`AccountPreferences_ShouldShowObservedHistory_WhenOperatorSelectsHistoryTab`
	and use `ClickAsync`, then expect the selected tab, history heading, table,
	and load-older button.
* Fold the consequential table/button checks into that activated-history flow,
	or make a helper that explicitly asserts tab selection before checking panel
	content. This preserves useful failure localization.
* Replace the assumed initial state with explicit API preconditioning through
	the fixture-owned AppHost, then add
	`AccountPreferences_ShouldShowPendingDesiredStateWithoutProviderError_WhenProviderIsUnavailableAfterSave`.
	It should prove desired and observed labels remain visible, `Pending` is
	shown, and no provider error displaces the SQL state.
* Add
	`AccountPreferences_ShouldShowInSyncObservedState_WhenVerificationRetrySucceeds`
	and
	`AccountPreferences_ShouldShowDriftWarningAndRemediationAction_WhenVerificationFindsDifferentValue`.
	These must use the corrected provider contract and web-first URL/ARIA/text
	assertions only.
* Add
	`AccountPreferences_ShouldKeepHistoryReadOnly_WhenOperatorLoadsOlderObservations`
	to prove history paging does not alter the selected desired state.

## Quality-Gate Artifacts

artifacts/quality-gates/account-preferences-ui-clarity/20260831-124715/quality-gate.log
records a 2026-08-31 full build with zero compile errors and historical suite
execution. Its TRX folder has no Account Preferences E2E result. The archive
therefore supports only past build health, not the present browser behavior.
artifacts/quality-gates/phase-8-account-preferences-migration.sql contains the
generated migration script, including the observations table, but does not
contain browser or reconciliation execution evidence. Existing infrastructure
TRX files under test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/TestResults/
cover migration and history checks, including one historical test-filter miss;
they do not explain the current E2E failure.

## Wiki Documentation Updates

Update these locations after the behavior and tests are corrected. Line ranges
refer to the current working tree.

* docs/wiki/operator-guide.md:565-586. Replace the stale save-confirmed
	language with the selected SQL-first update contract, the canonical pending
	status, verification retry, drift/remediation rules, and the observed-history
	tab keyboard behavior.
* docs/wiki/runtime-behavior.md:728-747. Retain the authority split, then add
	the UI-visible state progression from `Unconfigured` to `Pending`,
	`InSync`, `Drifted`, `VerificationFailed`, or `Unsupported`; state whether
	retry is immediate/manual/background and that remediation is the only write
	to IG.
* docs/wiki/api-reference.md:551-574. Make request shapes explicit, especially
	the source/requirement for target account, actor, and expected revision.
	Align route status results with the actual durable update and reconciliation
	contracts, including the provider-safe error mapping.
* docs/wiki/architecture.md:600-619. Document the final operation ownership:
	SQL desired-state commit, observe-only reconciler, revision-bound remediation,
	and no blind retries. Align it with the precise test seams above.
* docs/wiki/application-overview.md:195-212. Add the pending UI/reconciliation
	transition and state that the Web page is a projection of SQL state rather
	than a synchronous IG preference editor.
* docs/wiki/local-development.md:146-162. Correct the provider-double guidance
	to the actual IG adapter route/payload and describe the reproducible local
	recovery and mismatch exercises. Do not claim WireMock when the suite uses
	`ControllableIgProvider` unless the implementation is changed to WireMock.
* docs/wiki/testing-and-quality.md:559-605. Correct the overstatement that
	reconciliation is covered, describe the currently failing browser keyboard
	gap, and list the added unit/functional/E2E protections once they exist.

## Remaining Gaps

* The rerun output did not retain a DOM snapshot for the first test, so the
	state replacing exact `Unconfigured` is unknown. Capture Playwright trace or
	screenshot on retry after the tab issue is addressed.
* The intended authoritative source for update `AccountId` and `Actor` was not
	specified. Decide whether the HTTP boundary requires them, derives them from
	authenticated/configured state, or rejects UI updates until a configured
	account exists.
* The intended keyboard standard for Radzen tabs is not documented. Confirm
	whether the product requires Arrow keys plus Space, Arrow keys plus Enter,
	or direct activation on arrow focus, then test that chosen behavior in a real
	browser.
