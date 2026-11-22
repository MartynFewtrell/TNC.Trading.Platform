---
title: Trailing Stops Account Binding Research
description: Verified investigation of Trailing stops account binding and IG identity reconciliation.
ms.date: 2026-09-19
ms.topic: reference
---

## Scope

Research the Trailing stops configuration account binding, authenticated IG identity reconciliation,
account-mismatch warning, command behavior, persistence, and regression coverage. Product files are
out of scope.

## Executive Finding

The durable Trailing stops state is bound to the raw IG `currentAccountId` at the first confirmed
save. It is not bound to the signed-in web user's claims or to a separate operator-managed account
configuration value. A mismatch is intentionally fail-closed: save and status commands return the
stable non-identifying warning and make no IG preference GET or PUT request.

One configured IG credential set can still yield the mismatch when the latest authentication snapshot
and a later IG session identify different accounts, or when the same logical identifier has different
case or whitespace. The implementation preserves raw account IDs and uses ordinal string comparison;
no identity normalization was found. Consistent IG `currentAccountId` responses avoid the mismatch.

## Verified Behavior

### Binding and identity sources

* The web API resolves the authenticated user's preferred name/name only as the audit actor; it does
	not use claims for IG identity. `src/TNC.Trading.Platform.Api/Features/Platform/PlatformAuthAuditEventResolver.cs:28-37`
* The broker authentication gateway takes `currentAccountId` directly from the IG session response,
	validates only that it is nonblank, and returns it as authentication evidence.
	`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgBrokerAuthenticationGateway.cs:55-73`
* Successful authentication creates a latest IG-login snapshot using that unaltered evidence account
	ID. `src/TNC.Trading.Platform.Application/Features/ReconcilePlatformAuthentication/PlatformAuthenticationReconciler.cs:692-718`
* Persistence replaces the previous latest snapshot for the broker environment and writes
	`CurrentAccountId` as supplied. `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfPlatformIgLoginSnapshotStore.cs:12-38`
	and `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfPlatformIgLoginSnapshotStore.cs:78-96`
* A confirmed save writes the snapshot account ID to `AccountPreferencesCurrentState.AccountId` and
	records it in the desired-state audit and operation journal. The state is scoped by platform and
	broker environment. `src/TNC.Trading.Platform.Application/Features/AccountPreferences/SaveAccountPreferencesHandler.cs:37-52`
	and `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfAccountPreferencesCurrentStateStore.cs:15-25`
* The schema stores the durable bound account ID, observed account ID, verification fields, and a
	failure summary. `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/Migrations/20260831130930_AddAccountPreferencesCurrentState.cs:15-43`
	The current-state query selects one state row by platform and broker environment, even though the
	unique index also includes `AccountId`. `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfAccountPreferencesCurrentStateStore.cs:10-15`

### Exact warning and conditions

* The authoritative warning is the constant: "The authenticated IG account has changed. Trailing-stops
	preferences remain bound to the previously configured account. Reauthenticate with the intended
	account, then check status. Contact an administrator to change the configured account."
	`src/TNC.Trading.Platform.Application/Features/AccountPreferences/AccountPreferencesAccountMismatch.cs:4-9`
* Save returns this warning with `AccountMismatch` and HTTP 409 when a durable state account exists
	and differs from the latest IG-login snapshot account. It returns a different AccountMismatch reason
	when no snapshot exists. `src/TNC.Trading.Platform.Application/Features/AccountPreferences/SaveAccountPreferencesHandler.cs:16-31`
* Check status returns this warning when there is no latest snapshot or its account differs from the
	durable state account. It performs neither a lease acquisition nor IG preference I/O on this path.
	`src/TNC.Trading.Platform.Application/Features/AccountPreferences/CheckAccountPreferencesStatusHandler.cs:16-21`
* The IG preference gateway independently creates a new session and compares that raw
	`currentAccountId` with the requested target using `StringComparison.Ordinal`; observation and
	remediation return the same mismatch before preference I/O when unequal.
	`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgAccountPreferencesGateway.cs:16-27`
	and `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgAccountPreferencesGateway.cs:36-49`
* The API maps AccountMismatch to HTTP 409 and Problem Details type
	`/problems/account-preferences/account-mismatch`. `src/TNC.Trading.Platform.Api/Features/Platform/AccountPreferencesEndpointMapping.cs:35-52`
	and `src/TNC.Trading.Platform.Api/Features/Platform/AccountPreferencesEndpointMapping.cs:56-67`

### Why commands appear to stop working

* This is an intentional safety stop, not a disabled-control state. Tests prove that status mismatch
	performs no IG observation and save mismatch performs no remediation or state finalization.
	`test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/AccountPreferences/CheckAccountPreferencesStatusHandlerTests.cs:18-27`
	and `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/AccountPreferences/SaveAccountPreferencesHandlerTests.cs:30-44`
* The functional test verifies both commands preserve the prior account ID and desired revision and
	issue no IG preferences GET or PUT after the authenticated account changes.
	`test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/AccountPreferences/AccountPreferencesFunctionalTests.cs:105-145`
* The page disables Save and Check only while loading, saving, or checking. A mismatch itself does
	not permanently disable the controls; Save shows the typed Problem Details detail, while Check
	returns an HTTP 200 state with `VerificationFailed` and `FailureSummary` for display.
	`src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:17-55`
	`src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPagePresenter.cs:49-87`
	and `src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs:141-156`
* Background reconciliation can persist the warning: gateway failure creates a
	`VerificationFailed` state, increments retry count, schedules a retry, and stores the safe reason.
	`src/TNC.Trading.Platform.Application/Features/AccountPreferences/ReconcileAccountPreferencesHandler.cs:18-29`
	and `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfAccountPreferencesCurrentStateStore.cs:67-72`

### Authentication reconciliation

* Each successful authentication nudges due verification only for a current-state row whose bound
	account matches the newly authenticated account. A login under another account does not rebind the
	state. `src/TNC.Trading.Platform.Application/Features/ReconcilePlatformAuthentication/PlatformAuthenticationReconciler.cs:455-469`
	and `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfAccountPreferencesCurrentStateStore.cs:54-62`
* Account preferences are deliberately supported only for `Test` platform plus `Demo` broker
	environment. `src/TNC.Trading.Platform.Application/Features/AccountPreferences/AccountPreferencesEnvironmentPolicy.cs:5-9`
	The gateway also requests Demo credentials, which aligns with that policy.
	`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgAccountPreferencesGateway.cs:19-21`

## Viable Remedies

* Operational recovery: authenticate the configured Demo credentials until the latest IG-login
	snapshot reports the intended raw account ID, then use Check status. On a match, Check observes or
	remediates the persisted desired value and clears the failure summary upon successful completion.
	`src/TNC.Trading.Platform.Application/Features/AccountPreferences/CheckAccountPreferencesStatusHandler.cs:26-42`
* Correct credentials or IG default-account selection if the new session points at another account.
	Do not retry Save against a mismatch: it is designed not to write to an unbound account.
* For an intentional account change, add an explicit administrator-authorized rebind operation rather
	than editing SQL state directly. It should authenticate/observe the target first, require an expected
	revision, write an audited account-binding transition, and retain the old observation and operation
	history.
* Normalize account IDs once at the IG boundary only if IG documents the identifier as
	case-insensitive and whitespace-insignificant. Apply the same canonicalization when capturing the
	authentication snapshot, binding state, comparing gateway sessions, and querying reconciliation
	leases. Do not use case-insensitive comparison without that provider contract.

## Recommended Approach

Keep the fail-closed safety boundary. Add a dedicated audited rebind command guarded by an operator or
administrator policy and confirmation of the active IG session account. Introduce a single
`IgAccountId` normalization/value-object boundary only after verifying IG identifier semantics. This
preserves protection against cross-account writes while making an intentional binding change recoverable
without manual database mutation.

## Proposed Regression Tests

* Reauthentication to the same normalized account should nudge and allow Check status to converge.
* A fresh preferences-gateway session returning a different account than the latest authentication
	snapshot must return AccountMismatch and make no IG preferences GET or PUT.
* Case and leading/trailing-space variants should be tested according to the confirmed IG identifier
	contract: either converge through canonicalization or remain fail-closed.
* An authorized rebind should require expected revision, perform an IG account confirmation before
	mutation, emit an audit record, and make subsequent Check/Save target only the new account.
* A rebind conflict, account-confirmation failure, or provider timeout must preserve the original
	durable binding and desired preference.
* Web tests should verify the page keeps controls enabled after a mismatch response, displays the safe
	message, and succeeds after a recovered matching authentication snapshot.

## Remaining Questions

* Does the IG provider contract guarantee a stable, case-sensitive `currentAccountId` for a fixed Demo
	credential set, and can a session default account be changed without changing credentials?
* Which administrator authorization policy and operator workflow should own an intentional account
	rebind?

## Evidence Coverage

No official external documentation was required. Workspace implementation and unit/functional tests
provided the direct evidence for the warning contract, guards, persistence behavior, and UI behavior.