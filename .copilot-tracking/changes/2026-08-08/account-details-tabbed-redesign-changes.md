<!-- markdownlint-disable-file -->
# Release Changes: Account Details Tabbed Redesign

**Related Plan**: account-details-tabbed-redesign-plan.instructions.md
**Implementation Date**: 2026-08-08

## Summary

Implemented the Account Details tabbed UI, responsive identity header, and focused bUnit coverage.

## Changes

### Added

### Modified

* `src/TNC.Trading.Platform.Web/Components/Pages/AccountDetails/AccountDetails.razor` - added source-ordered Account type tabs, local selection reconciliation, responsive account panels, status badges, requested field groups, and page-local retrieval transition wrappers.
* `src/TNC.Trading.Platform.Web/wwwroot/app.css` - added scoped desktop and narrow-screen account identity header layout.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountDetailsTests.cs` - added multi-type, active-tab, state retention/fallback, layout content, and refresh-error regression coverage.

### Removed

## Additional or Deviating Changes

* Refresh test fixtures now return the retrieval shape expected by `RefreshAccountDetailsAsync`, while history fixtures retain the wrapped response shape.
* The selection-retention test follows the presenter contract by navigating through the newer cursor after refresh, because refresh clears the older cursor.

## Release Summary

All four implementation phases are complete. The Web unit-test project passes 97 tests and the Web project builds successfully.

The implementation remains presentation-only. No API, application, persistence, presenter, authorization, or routing contracts changed. Manual browser viewport and keyboard verification was not run in this session; automated bUnit coverage verifies the public tab interaction and accessible tab-list label text.

Validation warnings remain for the existing `Microsoft.OpenApi` high-severity package advisory, the existing `AngleSharp` moderate-severity advisory, and use of a .NET preview SDK.
