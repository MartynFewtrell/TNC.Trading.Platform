<!-- markdownlint-disable-file -->
# Release Changes: Account Preferences UI Alignment

**Related Plan**: account-preferences-ui-alignment-plan.instructions.md
**Implementation Date**: 2026-09-17

## Summary

Implementation tracking for Account Preferences navigation, tab presentation, and verification-action alignment.

## Changes

### Added

* Navigation and structural unit assertions were added for the requested authorization, order, wrapper, and action-class contracts.

### Modified

* src/TNC.Trading.Platform.Web/Components/Layout/MainLayout.razor - placed Account preferences immediately after Account details while retaining its Operator gate.
* src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor - added the scoped tab wrapper and shared primary-action classes.
* src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor.css - ported the Account Details tab reset under the Account Preferences scope.
* test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/MainLayoutTests.cs - covered role visibility and combined-role link order.
* test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountPreferencesTests.cs - covered the tab wrapper and all requested primary actions.

### Removed

## Additional or Deviating Changes

* The authentication test fixture always includes Viewer in generated tokens, so the Operator-only test verifies Account preferences visibility without asserting Account details absence. Production authorization conditions remain unchanged.

* Phase 1 validation: `MainLayoutTests`, 4 passed.
* Phase 2 validation: `AccountPreferencesTests|AccountDetailsTests`, 38 passed.
* Final focused unit validation: 42 passed.
* Web project build: passed.
* Account Preferences E2E suite: attempted with Aspire infrastructure, but 5 existing behavioral assertions failed around tab activation and history/state content. No production behavior changes were made to address them.

## Release Summary

All three phases are complete. Five Account Preferences E2E failures remain as follow-on investigation; the requested UI alignment unit slice and Web build pass.
