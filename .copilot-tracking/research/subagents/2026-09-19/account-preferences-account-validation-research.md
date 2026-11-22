---
title: Account Preferences Account Validation Research
description: Verified research into trailing-stops account validation for Check status and Save preferences
author: GitHub Copilot
ms.date: 2026-09-19
ms.topic: reference
keywords:
  - account preferences
  - trailing stops
  - IG
  - account validation
status: complete
---

## Research Scope

This note investigates the trailing-stops Check status and Save preferences
workflows. It traces the warning `IG session account did not match the
configured target account.`, identifies the account sources, inventories
contracts, tests, and logging, and recommends a non-sensitive operator
experience.

## Account Identities

There is no account ID on `PlatformConfigurationSnapshot`. Here, configured
target account means the durable `AccountPreferencesCurrentState.AccountId`,
scoped by platform and broker environment. See
`src/TNC.Trading.Platform.Application/Features/AccountPreferences/AccountPreferencesCurrentState.cs:3-23`.

The target is established by Save: it reads the latest durable IG login snapshot
and creates operation/desired-state records with `snapshot.CurrentAccountId` at
`src/TNC.Trading.Platform.Application/Features/AccountPreferences/SaveAccountPreferencesHandler.cs:18-19`,
`37`, and `52`. The EF store persists this account ID at
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfAccountPreferencesCurrentStateStore.cs:19-24`.

The durable session identity is `IgLoginSnapshot.CurrentAccountId`
(`src/TNC.Trading.Platform.Application/Configuration/IgLoginSnapshot.cs:3-13`).
The authentication reconciler creates it from `BrokerAuthenticationEvidence.AccountId`
and persists it at
`src/TNC.Trading.Platform.Application/Features/ReconcilePlatformAuthentication/PlatformAuthenticationReconciler.cs:702-713`.
The EF store retains and reads the latest snapshot per broker environment at
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfPlatformIgLoginSnapshotStore.cs:13-62`.

The provider-bound identity is independently obtained from a fresh IG
`POST session` response field `currentAccountId`. The gateway requires it to be
non-empty and keeps it in a private `Session` record at
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgAccountPreferencesGateway.cs:102-118`
and `153-154`.

## Verified Flow

### Check Status

`POST /api/platform/account-preferences/check-status` dispatches the Check
handler at `src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs:145-156`.
The handler reads configuration and durable preference state. With no desired
revision/account it returns without IG I/O. Otherwise it reads the latest login
snapshot. A missing snapshot or a snapshot account differing from the durable
target returns the warning with `AccountMismatch` before a lease or gateway
call at
`src/TNC.Trading.Platform.Application/Features/AccountPreferences/CheckAccountPreferencesStatusHandler.cs:16-20`.

For matching state, Check puts the durable target into `AccountPreferencesObserveRequest`
and `AccountPreferencesRemediateRequest` at
`src/TNC.Trading.Platform.Application/Features/AccountPreferences/CheckAccountPreferencesStatusHandler.cs:26`
and `30`; the contracts are at
`src/TNC.Trading.Platform.Application/Features/AccountPreferences/AccountPreferencesObserveRequest.cs:3`
and `src/TNC.Trading.Platform.Application/Features/AccountPreferences/AccountPreferencesRemediateRequest.cs:3`.
The gateway repeats the comparison with a fresh IG session before a GET at
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgAccountPreferencesGateway.cs:15-23`.
When state exists, the API returns HTTP 200 and overlays `FailureSummary` with
the safe reason at
`src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs:151-155`.

### Save Preferences

`PUT /api/platform/account-preferences` validates the request and idempotency
key, resolves the server-side actor, then invokes Save at
`src/TNC.Trading.Platform.Api/Features/Platform/UpdateAccountPreferencesEndpointHandler.cs:8-19`.
Save reads the same latest login snapshot. With no snapshot it returns
`AccountMismatch` and `No authenticated IG account.`. When existing state is
bound to another account, it rejects before provider I/O at
`src/TNC.Trading.Platform.Application/Features/AccountPreferences/SaveAccountPreferencesHandler.cs:18-19`
and `28-31`.

This is why the warning applies to Save: an account newly authenticated after
preferences were configured cannot silently take over that durable target. On
the first save there is no target, so the latest snapshot account becomes it.
For a valid save, that snapshot account is passed to remediation at
`src/TNC.Trading.Platform.Application/Features/AccountPreferences/SaveAccountPreferencesHandler.cs:37-45`.

The gateway repeats the comparison before a preference read/write at
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgAccountPreferencesGateway.cs:35-43`. The application preflight prevents
acting on known changed durable session state. The gateway preflight closes the
time-of-check/time-of-use gap before IG preference I/O.

## Warning Origin And Contracts

The exact warning originates in three production branches:

* Check application preflight: `src/TNC.Trading.Platform.Application/Features/AccountPreferences/CheckAccountPreferencesStatusHandler.cs:20`
* Gateway observation preflight: `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgAccountPreferencesGateway.cs:23`
* Gateway remediation preflight: `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgAccountPreferencesGateway.cs:43`

Save uses the text at
`src/TNC.Trading.Platform.Application/Features/AccountPreferences/SaveAccountPreferencesHandler.cs:31`,
but classifies it as `Rejected` with `Conflict: true`; Check uses `AccountMismatch`. The enum is
defined at
`src/TNC.Trading.Platform.Application/Features/AccountPreferences/AccountPreferencesFailureCategory.cs:3-15`.
Check and Save result contracts carry state, phase, category, safe reason, and
conflict at
`src/TNC.Trading.Platform.Application/Features/AccountPreferences/CheckAccountPreferencesStatusResult.cs:3-8`
and `src/TNC.Trading.Platform.Application/Features/AccountPreferences/SaveAccountPreferencesResult.cs:3-9`.

`AccountMismatch` maps to HTTP 409, type
`/problems/account-preferences/account-mismatch`, a stable title, and a
`failureCategory` extension at
`src/TNC.Trading.Platform.Api/Features/Platform/AccountPreferencesEndpointMapping.cs:57-61`,
`66-68`, `81-83`, and `94-97`. Save conflict instead returns HTTP 409 with the
safe reason as title and no stable type/category at
`src/TNC.Trading.Platform.Api/Features/Platform/AccountPreferencesEndpointMapping.cs:32-40`.
This is the key API inconsistency.

## Presentation And Logging

The web client turns Problem Details title/detail into an `HttpRequestException`
at `src/TNC.Trading.Platform.Web/PlatformApiClient.cs:194-212`. Check preserves
the detail in `CurrentError`, while Save discards it and sets only `Unable to
save and confirm account preferences.` at
`src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPagePresenter.cs:54-82`
and `97-103`. The page renders `CurrentError`, durable `FailureSummary`, and
`SaveError` as alerts at
`src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:16-20`
and `55-56`.

No `ILogger` dependency or `LogInformation`, `LogWarning`, or `LogError` call
exists in either account-preferences handler or the IG gateway. For Check,
`FailAsync` bounds the safe reason to 512 characters, increments retry state,
sets a one-minute retry, and persists through reconciliation at
`src/TNC.Trading.Platform.Application/Features/AccountPreferences/CheckAccountPreferencesStatusHandler.cs:40-46`.
The EF store writes `FailureSummary` and correlation ID at
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfAccountPreferencesCurrentStateStore.cs:65-72`.

Successful authentication snapshot capture emits an operational event with the
raw `CurrentAccountId` in metadata at
`src/TNC.Trading.Platform.Application/Features/ReconcilePlatformAuthentication/PlatformAuthenticationReconciler.cs:715-728`.
It is not a mismatch event. Account IDs are not credentials, but exposing them
in browser errors or new logs unnecessarily enlarges operational-data exposure.

## Existing Tests

### Present Coverage

* Check exact warning and no observe call:
  `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/AccountPreferences/CheckAccountPreferencesStatusHandlerTests.cs:17-26`
* Check durable failure/retry projection:
  `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/AccountPreferences/CheckAccountPreferencesStatusHandlerTests.cs:51-60`
* Gateway remediation mismatch before preference read/write:
  `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/Integrations/Ig/IgAccountPreferencesGatewayTests.cs:23-32`
* Gateway matching observation account:
  `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/Integrations/Ig/IgAccountPreferencesGatewayTests.cs:11-21`
* Account-mismatch HTTP mapping and provider-diagnostic non-leakage:
  `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/AccountPreferencesEndpointMappingTests.cs:11-37`
* Functional Check provider-unavailable projection/recovery:
  `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/AccountPreferences/AccountPreferencesFunctionalTests.cs:69-105`
* E2E rendering of a Check provider-failure warning:
  `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/AccountPreferences/AccountPreferencesE2ETests.cs:63-78`
* Component placement of persisted warning and generic save error:
  `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountPreferencesTests.cs:145-164`

### Missing Coverage

* Save durable-target versus latest-snapshot mismatch.
* Save with no latest login snapshot and its Problem Details response.
* Gateway `ObserveAsync` mismatch before its preference GET.
* API tests for Check-with-state HTTP 200 plus `failureSummary`, and Save's
  current conflict shape.
* Functional/E2E account-switch behavior for both buttons.
* Mismatch-specific structured logging. None exists today.

## Recommendation

Select a stable, actionable, secret-safe account-mismatch contract for both
actions. Keep both validation layers; they prevent wrong-account preference
reads/writes and close a session-change race. Change presentation consistency,
not the safety invariant.

Use a single safe message such as:

`The authenticated IG account has changed. Trailing-stops preferences remain bound to the previously configured account. Reauthenticate with the intended account, then check status. Contact an administrator to change the configured account.`

Do not reveal configured/current account IDs in Problem Details, page text,
browser logs, or notifications. Account values make diagnosis easier but leak
operational identifiers through screenshots and support tickets. A correlation
ID is preferable support context if it follows existing platform policy.

## Implementation And Test Steps

1. Introduce a single account-mismatch reason/factory in the account-preferences
   feature and use it in Check, Save, and both gateway branches.
2. Classify the Save durable-target mismatch as `AccountMismatch`, not
   `Rejected`; route it through `ToProblemResult` for the same HTTP 409 type
   and `failureCategory` as Check.
3. Update `AccountPreferencesPagePresenter.SaveAsync` to show the known safe
   account-mismatch message. Retain generic text for unknown failures.
4. Preserve Check's durable `FailureSummary` behavior using the selected text.
   If structured logging is added, log only category, environments, and
   correlation ID, not account IDs, API keys, CST, security tokens, or passwords.
5. Add a Save handler mismatch test with target `TARGET` and snapshot `OTHER`.
   Assert category, wording, zero remediation calls, and no operation start.
6. Add no-snapshot Save, Observe mismatch, Check/Save endpoint contract, and
   page/presenter tests. Assert neither UI nor API reveals account IDs.
7. Add a functional or E2E account-switch test: save as account A, switch the
   stored/latest session and provider to account B, invoke both actions, and
   assert no `accounts/preferences` GET or PUT follows either mismatch.

Do not silently rebind preferences. An intentional rebind should be a separate
administrator workflow with explicit policy and audit requirements.

## Remaining Gaps

* This research did not execute Docker-backed functional/E2E tests or alter
  fixture state; findings are verified through static production and test code.
* Product policy must define who may intentionally rebind preferences to a
  different IG account. No explicit migration workflow was found.
* Organizational classification of account IDs was not found. The selected
  approach conservatively treats them as non-public operational data.