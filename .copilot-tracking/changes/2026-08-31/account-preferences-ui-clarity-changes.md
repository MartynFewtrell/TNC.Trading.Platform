<!-- markdownlint-disable-file -->
# Release Changes: Account Preferences UI Clarity

**Related Plan**: [account-preferences-ui-clarity-plan.instructions.md](../../plans/2026-08-31/account-preferences-ui-clarity-plan.instructions.md)
**Implementation Date**: 2026-08-31

## Summary

Implementation tracking for the Account Preferences UI clarity delivery.

## Changes

### Added

* None yet

### Modified

* `.copilot-tracking/plans/2026-08-31/account-preferences-ui-clarity-plan.instructions.md` - Marked the focused baseline phase complete after 10 passing account-preferences tests.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPageViewModel.cs` - Added explicit nullable confirmed state, pending selection, dirty-state detection, and authoritative synchronization.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountPreferencesTests.cs` - Added traceable pending, confirmed, unconfirmed, dirty-state, and synchronization coverage.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPagePresenter.cs` - Separated durable load/history feedback from transient save feedback and corrected selection, dismissal, rejection, cancellation, and partial-success transitions.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountPreferencesTests.cs` - Added presenter transition coverage for HTTP 409, cancellation, dismissal, selection invalidation, and history-refresh partial success.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor` - Replaced the checkbox with explicit semantic Boolean radios, pending/confirmed state rendering, separated status messaging, persistent dismissal, and Save focus restoration.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountPreferencesTests.cs` - Replaced checkbox assertions with semantic, payload, failure-safety, in-flight, confirmation, and focus-interoperability coverage.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor.css` - Added scoped responsive segmented-selector, focus, status, wrapping, and narrow-layout styles.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountPreferencesTests.cs` - Stabilized async component setup and HTML radio event values; focused suite now passes 25 tests.
* `docs/wiki/operator-guide.md` - Documented the explicit selector, pending and provider-confirmed states, dismissal, safe load failure, and qualified history feedback.
* `docs/wiki/testing-and-quality.md` - Documented deterministic semantic, transition, payload, failure, partial-success, dismissal, and safe-load coverage boundaries.

### Removed

* None

## Additional or Deviating Changes

* Phase 0 completed read-only. Focused command passed with 10 tests passed, 0 failed, and 0 skipped; no Docker, SQL Server, Keycloak, or live IG credentials were required.
* Phase 1 completed with the focused command passing 16 tests, 0 failures, and 0 skipped. Transport, application, provider, presenter, and component contracts were unchanged.
* Phase 2 completed with the focused command passing 21 tests, 0 failures, and 0 skipped. A durable history-page failure boundary was added in the presenter as an in-scope feedback ownership correction.
* Phase 3 completed after migrating obsolete checkbox tests; the focused command passes with 21 tests, 0 failures, and 0 skipped.
* Phase 4 completed with scoped styling and a passing focused suite at 25 tests. Web build passed. Web and test format verification reported pre-existing charset, final-newline, and import-order diagnostics; no CSS compile failure was reported.
* Phase 5 completed with both wiki sections updated. Scoped whitespace and local-link checks passed; optional Markdown lint could not run because `npx` is unavailable.
* Phase 6 is partial. AppHost build/start and anonymous viewport checks passed; quality-gate build passed, but 11 API integration tests failed because the Keycloak port 8080 lease timed out. Authenticated account-preferences browser, 400% zoom, NVDA, and Narrator checks remain blocked without a safe local test-account session. Evidence is retained under `artifacts/quality-gates/account-preferences-ui-clarity/20260831-124715/`.

## Release Summary

Phases 0 through 5 complete. Phase 6 has partial evidence with environmental and authentication blockers requiring follow-on validation.

## Release Summary

The Web presentation slice now has explicit Enabled/Disabled Boolean radios, separate pending and provider-confirmed state, state-driven headings, separated save and durable feedback, persistent dismissible confirmation, Save focus restoration, and scoped responsive styling. Focused Account Preferences coverage passes 25 tests, and the Web build passes. The operator and testing wiki pages reflect the delivered behavior. No API, application, provider, persistence, configuration, schema, package, or project contract changes were introduced. Repository quality-gate and authenticated accessibility evidence remain incomplete because Keycloak could not lease port 8080 and no safe local test-account session was available.
