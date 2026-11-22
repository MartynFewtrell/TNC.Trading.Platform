<!-- markdownlint-disable-file -->
# Release Changes: Account Preferences UI Improvement

**Related Plan**: account-preferences-ui-improvement-plan.instructions.md
**Implementation Date**: 2026-09-17

## Summary

Implementation tracking for the Account Preferences settings and observed-history UI improvement.

## Changes

### Added

* None.

### Modified

* [AccountPreferences.razor](src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor) - Added accessible Settings and Observed history tabs, scoped feedback, semantic state markup, and labelled action/history controls while preserving presenter bindings.
* [AccountPreferences.razor.css](src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor.css) - Added responsive choice sizing and wrapped 44px action-group styling with preserved focus/selection states.
* [AccountPreferencesTests.cs](test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountPreferencesTests.cs) - Added focused tab, hierarchy, feedback, action, history, and cursor-control assertions; stabilized a retained status assertion against the semantic test id.
* [AccountPreferencesE2ETests.cs](test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/AccountPreferences/AccountPreferencesE2ETests.cs) - Added role-first tab activation, keyboard navigation, history table, and cursor-command browser scenarios.

### Removed

* None.

## Additional or Deviating Changes

* `ObservedAtUtc` was available on the response model and is used as the history timestamp, resolving the planned fallback without API changes.
* Focused E2E validation remains blocked: all five scenarios, including the pre-existing provider-unavailable baseline, reported missing page elements in the current Docker/Aspire authenticated fixture. No API, presenter, or persistence scope expansion was required.

## Release Summary

Phase 1 and bUnit coverage completed. The solution build passed with existing package vulnerability warnings, and the focused bUnit suite passed 30/30. E2E execution is pending environment diagnosis and responsive screenshot inspection.
