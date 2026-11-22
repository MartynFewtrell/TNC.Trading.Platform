<!-- markdownlint-disable-file -->
# Release Changes: Trailing Stops Configuration Issues

**Related Plan**: trailing-stops-configuration-issues-plan.instructions.md
**Implementation Date**: 2026-09-19

## Summary

Implementation tracking for the trailing-stops configuration layout correction and protected account-binding recovery procedure.

## Changes

### Added

* None yet

### Modified

* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor.css` - Removed fieldset growth and vertically centered the existing control row.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/AccountPreferences/AccountPreferencesE2ETests.cs` - Added desktop bounding-box assertions for adjacent and vertically centered Save placement.

### Removed

* Focused Account Preferences E2E validation: the isolated desktop geometry test passed; the class remains affected by the provider-unavailable remediation scenario in the shared Aspire state.
* `dotnet build` succeeded with 4 warnings, including the existing SSH.NET and AngleSharp vulnerability advisories.
* Full `dotnet test --no-build`: 610 passed and 5 failed. Failures are in existing API authentication/account-preference state tests, provider-unavailable functional behavior, and two E2E scenarios; no failure was caused by the isolated layout assertion.

## Additional or Deviating Changes

* Phase 2 account recovery could not be executed because no authenticated IG Demo browser session or credentials were available, and the Aspire dashboard URL was not discoverable from the current context.
	* No application, API, infrastructure, database, or account-binding behavior was changed.
* Intentional rebinding and account-ID canonicalization remain deferred until IG identity semantics and product/security authorization are available.
	* Ordinary Save remains prohibited from silently rebinding accounts.
* Account recovery was attempted successfully through the authenticated browser session:
	* Reauthenticated and invoked Check status on Account Preferences.
	* The application reproduced the warning: `The authenticated IG account has changed. Trailing-stops preferences remain bound to the previously configured account.`
	* No Save operation, SQL edit, rebinding, credential capture, bearer token capture, or full account identifier was performed.
* Browser evidence indicates the action runs through the active Blazor circuit (`/_blazor/negotiate`); no standalone fetch/XHR response was exposed. Console output was empty during the retry. The persistent mismatch is escalated as provider identity-contract or durable-binding diagnosis.

## Release Summary

Phase 1 layout implementation completed. The isolated geometry test and full build pass. Account recovery was attempted with an authenticated session but the mismatch persisted, so the fail-closed policy correctly blocked preference I/O. Full tests are not green because five existing API/Web integration failures remain. No account-binding policy was weakened.
