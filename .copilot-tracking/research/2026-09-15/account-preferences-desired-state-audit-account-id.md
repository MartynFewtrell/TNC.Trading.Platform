---
title: Account Preferences Desired-State Audit Account ID Research
description: Trace and root-cause analysis for the NULL AccountId desired-state audit insert.
author: GitHub Copilot
ms.date: 2026-09-15
ms.topic: troubleshooting
keywords:
  - account preferences
  - entity framework core
  - sql server
  - audit
---

## Research scope

Investigate the `DbUpdateException` raised when saving `Trailing stops enabled`, where SQL
rejects `NULL` for `platformdb.dbo.AccountPreferencesDesiredStateAudits.AccountId`.

Questions:

* Trace the ordinary UI save request through API, application, and EF persistence
* Verify the desired-state audit entity, model configuration, and migration schema
* Determine whether a migration or schema mismatch could explain the exception
* Identify the smallest corrective change and focused tests

## Initial hypothesis

The ordinary web save request omits `AccountId`; the API and application handler permit that
omission and create a desired-state change whose account ID is null. The EF store copies that
value to an audit column which the migration defines as non-nullable. A focused source and
integration-test inspection will confirm or disprove this.

## Verified request trace

1. `AccountPreferencesPagePresenter.SaveAsync` calls
  `PlatformApiClient.UpdateAccountPreferencesAsync` with only the selected Boolean.
  Source: `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPagePresenter.cs:61`.
2. The web client creates a PUT body containing only `TrailingStopsEnabled`.
  Source: `src/TNC.Trading.Platform.Web/PlatformApiClient.cs:149-153`.
3. The endpoint request contract accepts nullable `AccountId` and `Actor`. The endpoint handler
  forwards both unmodified to the application request.
  Sources: `src/TNC.Trading.Platform.Api/Features/Platform/AccountPreferencesEndpointMapping.cs:105`;
  `src/TNC.Trading.Platform.Api/Features/Platform/UpdateAccountPreferencesEndpointHandler.cs:8-16`.
4. Current validation only rejects a blank account ID or actor when an account ID was supplied.
  It allows both values to be null.
  Source: `src/TNC.Trading.Platform.Application/Features/AccountPreferences/UpdateAccountPreferencesValidator.cs:8-11`.
5. With the current-state store registered, the update handler creates a desired-state change
  using null-forgiving operators for `TargetAccountId` and `Actor`. The operators do not add a
  runtime check, so null is passed through.
  Source: `src/TNC.Trading.Platform.Application/Features/AccountPreferences/UpdateAccountPreferencesHandler.cs:14-23`.
6. The EF store assigns `change.AccountId` to both the current-state entity and the audit entity,
  then calls `SaveChangesAsync`.
  Source: `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfAccountPreferencesCurrentStateStore.cs:20-24`.

## Root cause

The ordinary UI save produces an account-less, actor-less desired-state request. This crosses a
validation gap in the durable desired-state path. EF then inserts the null account ID into the
audit entity. SQL Server correctly rejects it because the audit column is required.

The null-forgiving expressions in the application handler conceal the incorrect nullable request
contract from nullable analysis. They are not the origin of null, but they allow it to reach the
database unchanged.

The next database error would likely be `Actor`, because the same UI payload omits it and the
audit column is also required.

## Audit schema and migration assessment

The non-null SQL column matches the intended EF model, generated snapshot, and creating
migration:

* Entity: `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/Entities/AccountPreferencesDesiredStateAuditEntity.cs:9-15`
* Fluent model: `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/PlatformDbContext.cs:179-190`
* Migration `20260831130930_AddAccountPreferencesCurrentState`:
  `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/Migrations/20260831130930_AddAccountPreferencesCurrentState.cs:45-85`
* Current EF snapshot: `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/Migrations/PlatformDbContextModelSnapshot.cs:240-301`

All four declare `AccountPreferencesDesiredStateAudits.AccountId` as required/non-nullable.
`AccountPreferencesCurrentStates.AccountId` is intentionally nullable, which does not change the
audit contract. The runtime initializer applies pending SQL Server migrations at startup.
Source: `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Startup/PlatformStartupInitializer.cs:43-51`.

No new migration or model/schema mismatch is necessary to explain the exception. The reported
SQL constraint is exactly what the current model and migration require. A deployment-specific
schema mismatch cannot be ruled out without querying that database's `__EFMigrationsHistory`,
but it is not indicated by this failure and would not be the root cause of the null value.

## History

Commit `f4b3b3b4` introduced the web client payload on 2026-08-30 without account or actor.
Commit `6d38c494` introduced durable desired state and required audit columns on 2026-08-31;
it added the EF store's audit insertion and the application's durable branch without updating the
web client payload. `git blame` shows the endpoint forwarding change was added later in
`3ef847ed` on 2026-09-15.

## Existing tests and verification

`AccountPreferences_ShouldPersistAndConverge_WhenProviderRecovers` passes both `accountId` and
`actor`, so it bypasses the production UI payload defect.
Source: `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/AccountPreferences/AccountPreferencesFunctionalTests.cs:63-75`.

`CommitDesiredStateAsync_ShouldPersistStateAndTypedAudit_WhenContextRestarts` migrates a fresh
database and persists an audit using a non-null account ID.
Source: `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/AccountPreferencesSqlIntegrationTests.cs:191-211`.

Focused safe verification ran the latter SQL integration test file. Result: 14 passed, 0 failed.
This confirms the current migrations, mapping, and store work when their required input contract
is satisfied.

The web unit-test helper constructs preference responses with `AccountId = null`, so it cannot
assert save payload propagation. Sources:
`test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountPreferencesTests.cs:14`;
`src/TNC.Trading.Platform.Web/PlatformApiClient.cs:231-234`.

## Selected minimal remediation

Treat account ID and actor as required durable-operation inputs. Do not make the audit columns
nullable or add a migration, because that would weaken the audit record and still fail on the
required actor column.

1. At the API boundary, obtain the actor from the authenticated `ClaimsPrincipal` with the
  existing `PlatformAuthAuditEventResolver.ResolveUserName` convention, rather than accepting
  a browser-supplied actor. Sources:
  `src/TNC.Trading.Platform.Api/Features/Platform/PlatformAuthAuditEventResolver.cs:28-38`;
  `src/TNC.Trading.Platform.Api/Features/Platform/RecordAuthAuditEventEndpointHandler.cs:12-34`.
2. Require a nonblank target account ID before constructing `AccountPreferencesDesiredStateChange`.
  Remove the nullable-forgiving assumptions in the durable branch. The API must return a
  validation response before persistence if it lacks an account.
3. Change the page/client save operation to propagate the selected canonical account ID. The page
  already retains a loaded state account ID in `State.AccountId`, but a fresh `Unconfigured`
  response explicitly has no account ID. Sources:
  `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPageViewModel.cs:24-41`;
  `src/TNC.Trading.Platform.Api/Features/Platform/AccountPreferencesEndpointMapping.cs:18`.
4. For the first save, obtain an explicit canonical account ID from an existing authoritative
  account-selection/login-state contract. Do not silently select the first account from account
  details. The application already treats an account ID as target identity for reconciliation and
  rejects a missing desired-state account.
  Source: `src/TNC.Trading.Platform.Application/Features/AccountPreferences/ReconcileAccountPreferencesHandler.cs:15-24`.

The smallest code-only partial fix, passing `State.AccountId`, is insufficient because it cannot
support first-time saves. The selected remediation is the smallest correct fix: preserve the
strict audit schema, derive actor server-side, and make account selection explicit before save.

## Recommended tests for the remediation

* Web unit test: load a response with a known account ID, save, and assert the PUT JSON contains
  that account ID.
* API unit test: no account ID returns `ValidationProblem` and does not invoke the application
  handler.
* API unit test: the desired-state command actor equals the authenticated principal's resolved
  user name, not request-body input.
* Functional test: exercise the ordinary page/API save path with the canonical account source,
  then assert HTTP 200 and a persisted audit row with non-null account ID and actor.
* Retain the existing SQL integration test and extend its audit assertion to verify `AccountId`.

## Clarifying question

Which existing contract is authoritative for the account chosen on a first preferences save:
the active IG login session, a persisted preferred account, or an explicit operator selection?
The source inspected here exposes account IDs through several read models but does not identify
one as the preferences page's first-save authority.
