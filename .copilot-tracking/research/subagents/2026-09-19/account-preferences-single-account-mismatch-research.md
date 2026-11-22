---
title: Single Account Mismatch Research
description: Traces the trailing-stops account-mismatch warning for a single IG login and records a safe diagnostic workflow.
author: GitHub Copilot
ms.date: 2026-09-19
ms.topic: troubleshooting
status: complete
---

## Research Scope

Investigate the trailing-stops account-mismatch warning produced by Check and
Save when the operator has one IG account and one login. Establish the exact
compared identifiers, likely single-account causes, safe diagnostics, and a
user-facing explanation without changing product code.

## Verified Findings

### Exact identifier comparisons

The warning is not based on the number of configured logins. It is based on
exact string comparisons of an account identifier at two independent points.

* Check loads persisted `AccountPreferencesCurrentState.AccountId` for the
	active platform and broker environment. It loads the latest successful login
	snapshot by broker environment and returns `AccountMismatch` when the
	snapshot is absent or `snapshot.CurrentAccountId != state.AccountId`. This
	occurs before a lease or any account-preferences provider I/O. See
	`src/TNC.Trading.Platform.Application/Features/AccountPreferences/CheckAccountPreferencesStatusHandler.cs:16-21`.
* Save loads the same latest login snapshot. An absent snapshot returns
	`AccountMismatch` with `No authenticated IG account.`. When a persisted
	target exists and differs from `snapshot.CurrentAccountId`, Save returns a
	conflict before it creates an operation or calls IG. See
	`src/TNC.Trading.Platform.Application/Features/AccountPreferences/SaveAccountPreferencesHandler.cs:17-31`.
* On a first successful Save, there is no persisted target to compare. Save
	uses `snapshot.CurrentAccountId` as both the operation target and the new
	durable target. See
	`src/TNC.Trading.Platform.Application/Features/AccountPreferences/SaveAccountPreferencesHandler.cs:32-52`.
* The provider gateway creates a fresh IG `POST session`, reads
	`currentAccountId`, and compares it with the requested durable target using
	`string.Equals(..., StringComparison.Ordinal)`. Both Observe and Remediate
	stop before the preferences GET or PUT on a mismatch. See
	`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgAccountPreferencesGateway.cs:15-23, 35-43, 119-135`.

The application preflight comparison uses C# `!=`, and the provider comparison
uses ordinal equality. Neither path trims or case-folds account IDs. Therefore
whitespace or casing differences would be treated as a mismatch if supplied by
a snapshot or provider response, although no evidence shows IG returning such
a variant.

### First-save and missing-login behavior

First save is not a durable-target mismatch scenario: absent state means Save
binds the new preference target to the latest login snapshot. A first save can
still fail only if no latest snapshot exists, in which case its safe reason is
`No authenticated IG account.` and the category is `AccountMismatch`.

Check is different: once a state has been saved, an absent latest snapshot is
deliberately treated identically to a differing snapshot account ID. It emits
`IG session account did not match the configured target account.` without
contacting IG. See
`src/TNC.Trading.Platform.Application/Features/AccountPreferences/CheckAccountPreferencesStatusHandler.cs:18-20`.

### Snapshot and durable-state persistence

Successful login processing replaces all existing `Latest` snapshots for the
same broker environment, then stores a new latest snapshot and preserves a
separate daily history snapshot. Reading uses `BrokerEnvironment` plus
`SnapshotKind == Latest`, ordered by capture time. See
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfPlatformIgLoginSnapshotStore.cs:14-42, 54-65`.

The durable target is updated only by a confirmed Save. Current state is read
by both platform and broker environment, and a confirmed save writes the
account ID into `AccountPreferencesCurrentStates` and an audit record. See
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfAccountPreferencesCurrentStateStore.cs:8-24, 28-37`.

The login snapshot table is keyed/configured by broker environment, while the
preference state is keyed/configured by platform environment and broker
environment. See
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/PlatformDbContext.cs:81-90, 166-184`.
Consequently, changing platform environment while retaining the same broker
environment can select a different preference state but the same latest login
snapshot. This is intentional partitioning but is relevant when local, test,
or shared databases retain old state.

Migration `20260831130930_AddAccountPreferencesCurrentState` introduced the
state and audit tables, including `AccountId` and the unique environment/account
index. It creates the schema; it does not normalize, migrate, or clear account
IDs. Existing rows persist through later application starts and migrations.
See
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/Migrations/20260831130930_AddAccountPreferencesCurrentState.cs:14-78`.

### Visible warning and safe inspection surfaces

Check returns state with its safe reason in `FailureSummary`; the Razor page
renders that value in an alert. See
`src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs:145-157`
and
`src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:16-22`.

The Status page exposes the latest successful-login timestamp, trading day,
and current account ID only to an authorized user. It does not display session
tokens, and labels stored headers as non-secret. See
`src/TNC.Trading.Platform.Web/Components/Pages/Status/Status.razor:130-170`.
The account-preferences API response also includes persisted and observed
account IDs, revision, environment-scoped state, timestamps, failure summary,
and retry information. See
`src/TNC.Trading.Platform.Api/Features/Platform/AccountPreferencesEndpointMapping.cs:8-18, 106-123`.

### Tests and gaps

The focused Check handler test proves a snapshot account `OTHER` versus stored
target `TARGET` returns the exact mismatch reason and makes zero Observe calls.
See
`test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/AccountPreferences/CheckAccountPreferencesStatusHandlerTests.cs:19-27`.

The provider test proves a fresh session account `OTHER` versus requested
target `TEST` returns `AccountMismatch`, performs no write, and makes only the
session request. See
`test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/Integrations/Ig/IgAccountPreferencesGatewayTests.cs:24-33`.

Save handler tests cover successful and idempotent saves but do not explicitly
cover no snapshot or persisted-target versus latest-snapshot mismatch. See
`test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/AccountPreferences/SaveAccountPreferencesHandlerTests.cs:9-40`.
Check tests likewise do not explicitly cover a null snapshot. The two missing
tests do not alter the code-derived behavior above.

## Alternatives Considered

### One login means no mismatch

Rejected. The code never counts login configurations. A single login can
produce different values over time between the persisted preference target,
the latest stored successful-login snapshot, and the fresh IG session.

### Automatically rebind trailing stops to the current account

Rejected. It would overwrite the durable target after an identity change and
allow a newly authenticated account to inherit preferences without an explicit,
authorized rebind workflow. The existing two guards are expressly designed to
prevent that.

### Treat account IDs as case-insensitive or trim them

Rejected as an operational workaround. The provider guard intentionally uses
ordinal equality. Normalization must be a deliberate product and provider
contract decision, backed by evidence that IG returns equivalent IDs with
format variation; it must not be applied ad hoc to bypass an identity guard.

### Show account IDs in the warning

Rejected. IDs are not credentials, but putting them in browser alerts,
screenshots, tickets, or logs needlessly broadens operational-data exposure.
Use controlled Status/API/DB inspection and report equality or hashes instead.

## Selected Diagnostic Workflow

Perform these steps in the same deployed environment and configuration database
that produced the warning. Do not copy API keys, passwords, CST, or
`X-SECURITY-TOKEN` values into the diagnostic record.

1. Record the active platform and broker environment, Check/Save timestamp,
	 response status/type, safe reason, and request correlation ID. The endpoint
	 maps a categorized Check mismatch to HTTP 409 and
	 `/problems/account-preferences/account-mismatch`; Save's existing
	 durable-target branch currently returns a generic 409 conflict, a known
	 contract inconsistency documented in
	 `.copilot-tracking/research/subagents/2026-09-19/account-preferences-resolution-options-research.md`.
2. On Status, inspect whether a latest successful IG login exists and record
	 only its capture time, broker environment, and a protected comparison of
	 its current account ID. If none exists, the Check explanation is confirmed:
	 the warning is an absent-login-snapshot guard, not proof of a second
	 account.
3. Retrieve the authenticated operator's account-preferences state and compare
	 `AccountId` with the latest snapshot `CurrentAccountId` in a controlled
	 support session. Prefer an equality result, ID length, or SHA-256 hash over
	 recording raw IDs in tickets.
4. If the stored snapshot and target match, run one Check and correlate the
	 provider attempt. A mismatch then points to the fresh IG session
	 `currentAccountId` differing from the stored target. This is the only path
	 that establishes an IG-at-request-time difference.
5. Verify the database/instance selected by the active environment. Inspect
	 state, desired-state audit, operations, and latest login snapshot metadata
	 for the active environment. Look for a prior successful save or test data
	 with a different account-ID hash, a missing `Latest` snapshot, and the
	 timestamp/order of configuration changes and migrations.

For a controlled SQL Server support session, the following query returns no
raw account ID, secret, credential, or session token. Replace the literals with
the active enum names, and restrict access to the platform database.

```sql
SELECT
		state.PlatformEnvironment,
		state.BrokerEnvironment,
		state.DesiredRevision,
		state.DesiredChangedAtUtc,
		state.VerificationStatus,
		state.LastVerifiedAtUtc,
		state.FailureSummary,
		CONVERT(varchar(64), HASHBYTES('SHA2_256', state.AccountId), 2) AS TargetAccountHash,
		snapshot.CapturedAtUtc AS LatestSnapshotCapturedAtUtc,
		CONVERT(varchar(64), HASHBYTES('SHA2_256', snapshot.CurrentAccountId), 2) AS LatestSnapshotAccountHash,
		CASE WHEN state.AccountId = snapshot.CurrentAccountId THEN 'Match' ELSE 'DifferentOrMissing' END AS SnapshotComparison
FROM AccountPreferencesCurrentStates AS state
OUTER APPLY (
		SELECT TOP (1) CurrentAccountId, CapturedAtUtc
		FROM IgLoginSnapshots
		WHERE BrokerEnvironment = state.BrokerEnvironment
			AND SnapshotKind = 'Latest'
		ORDER BY CapturedAtUtc DESC
) AS snapshot
WHERE state.PlatformEnvironment = @PlatformEnvironment
	AND state.BrokerEnvironment = @BrokerEnvironment;
```

The query determines the application preflight comparison only. It cannot
prove the provider-gateway comparison because the gateway creates a fresh IG
session per Observe or Remediate request.

## Safe User-Facing Explanation

The warning does not mean multiple logins were configured. Trailing-stops
settings are safely bound to the account recorded when they were first saved.
The system could not verify that the latest authenticated IG session is for
that same account, so it did not read or change preferences. Sign in again
with the intended account, then use Check status. An administrator must handle
any intentional change of the configured account.

Do not include account IDs, passwords, API keys, session tokens, or raw IG
response headers in the page warning, screenshots, support tickets, or logs.

## Open Questions

* The screenshot itself was not available to this research subagent. The
	previously completed account-preferences research in this workspace confirms
	the same warning path and reported Check/Save behavior, but cannot identify
	the exact runtime snapshot or database row.
* A controlled comparison is required to distinguish stale or absent snapshot
	from a fresh IG session returning a different `currentAccountId`.
* Product ownership must define an explicit, authorized, audited rebind
	workflow before a different account can intentionally receive the saved
	trailing-stops preference.

## Ranked Likely Causes

1. A previously saved durable target remains in the active configuration
	 database, while the latest successful IG login snapshot is missing or has a
	 different account-ID value. This exactly explains a Check warning with one
	 login and requires no provider request.
2. The durable target and stored snapshot agree, but a fresh session created
	 for Check or Save reports a different `currentAccountId`. This exactly
	 explains the gateway mismatch and can occur even with one credential set.
3. Local/test/shared database or active-environment confusion selected stale
	 preference state. State is persisted by platform plus broker environment,
	 but latest login snapshots are selected by broker environment only.
4. A format variation in the source account ID, such as casing or whitespace.
	 This is technically possible because comparisons are exact; no current test
	 or provider evidence establishes it as likely.
5. A migration defect. Low likelihood: the relevant migration only creates
	 the preference-state tables and neither transforms nor clears account IDs.

First-save state ranks below these causes because a first successful Save
adopts the latest snapshot account ID rather than comparing it with a prior
durable target.