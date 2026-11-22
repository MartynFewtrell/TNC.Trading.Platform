<!-- markdownlint-disable-file -->
# Release Changes: Account Details Layout Refinements

**Related Plan**: account-details-layout-refinements-plan.instructions.md
**Implementation Date**: 2026-08-08

## Summary

Added local layout hooks, scoped responsive styling, and Radzen tab visual refinements for Account details.

## Changes

### Added

* `src/TNC.Trading.Platform.Web/Components/Pages/AccountDetails/AccountDetails.razor.css` - Added page-local responsive layout rules and scoped Radzen tab visual treatment.

### Modified

* `src/TNC.Trading.Platform.Web/Components/App.razor` - Linked the generated CSS-isolation bundle so page-scoped Account details styles are delivered to browsers.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountDetails/AccountDetails.razor` - Added local retrieval and tab-wrapper hooks while preserving tab behavior.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountDetailsTests.cs` - Added a markup-contract test for the new hooks.

### Removed

## Additional or Deviating Changes

* CSS isolation was compiled but not loaded by the shared application shell.
	* Added the generated `TNC.Trading.Platform.Web.styles.css` bundle link to make the page-local stylesheet effective.
	* Confirmed the restarted Web resource serves the bundle containing the Account details layout and tab rules.

## Release Summary

Phases 1 and 2 completed. The focused Account details suite passed with 8 tests after both markup and stylesheet changes, and the Web project build succeeded. The application shell now links the CSS-isolation bundle, and the restarted Web resource serves the Account details stylesheet at `https://localhost:7281/TNC.Trading.Platform.Web.styles.css`. Manual visual review remains pending user inspection; existing package vulnerability and preview SDK warnings remain unrelated baseline warnings.
