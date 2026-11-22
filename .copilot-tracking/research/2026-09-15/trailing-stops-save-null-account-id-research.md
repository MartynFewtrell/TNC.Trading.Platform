<!-- markdownlint-disable-file -->
# Task Research: Trailing Stops Save Fails with Null Audit Account ID

This investigation traces the `DbUpdateException` raised when saving `Trailing stops enabled`. SQL Server rejects a null `AccountId` while inserting a desired-state audit row.

## Task Implementation Requests

* Explain why saving the Trailing Stops setting produces a null `AccountId` audit insert.
* Determine whether the failure is caused by EF Core, SQL migration state, or the application request flow.
* Identify the minimal correct remediation and validation coverage.

## Scope and Success Criteria

* Scope: Account Preferences page save, Web API client payload, API request/handler, Application command validation, EF current-state store, desired-state audit entity, SQL migration, and existing tests.
* Assumptions: The user performed the ordinary UI save path, rather than a test or direct API request.
* Success Criteria:
  * Identify the exact point at which account identity is lost.
  * Verify whether the database schema is correct.
  * Recommend one remediation that preserves audit integrity.
  * State the additional decision required to support an initial save.

## Research Executed

### File Analysis

* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPagePresenter.cs:61`
  * Calls the Web API client with only the selected Boolean value.
* `src/TNC.Trading.Platform.Web/PlatformApiClient.cs:149-153`
  * Serializes only `TrailingStopsEnabled`; it omits account and actor identity.
* `src/TNC.Trading.Platform.Api/Features/Platform/AccountPreferencesEndpointMapping.cs:105`
  * Exposes nullable account and actor request members.
* `src/TNC.Trading.Platform.Api/Features/Platform/UpdateAccountPreferencesEndpointHandler.cs:8-16`
  * Forwards nullable account and actor unchanged.
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/UpdateAccountPreferencesValidator.cs:8-11`
  * Permits both fields to be null.
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/UpdateAccountPreferencesHandler.cs:14-23`
  * Uses null-forgiving operators while creating the durable desired-state change; null reaches persistence at runtime.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfAccountPreferencesCurrentStateStore.cs:20-24`
  * Copies the nullable change account ID into the audit row and calls EF Core save.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/Entities/AccountPreferencesDesiredStateAuditEntity.cs:9-15`
  * Declares `AccountId` as required.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/PlatformDbContext.cs:179-190`
  * Configures the audit account ID as required.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/Migrations/20260831130930_AddAccountPreferencesCurrentState.cs:45-85`
  * Creates the audit `AccountId` as non-nullable, matching the live SQL exception.

### History and Validation

* Commit `f4b3b3b4` (2026-08-30) introduced the Web payload without account or actor.
* Commit `6d38c494` (2026-08-31) introduced durable desired state and required audit fields, but did not update that Web payload.
* Commit `3ef847ed` (2026-09-15) added the current endpoint forwarding behavior.
* `AccountPreferencesSqlIntegrationTests.cs` passed: 14 tests, 0 failures. These tests provide a valid account and actor, proving that the EF model, migration, and store function with a valid contract.

## Key Discoveries

### Root Cause

The ordinary UI sends an account-less and actor-less request. The API and Application layers accept those null values, and the durable branch constructs an audit change using null-forgiving operators. The EF store then inserts the null account ID into a column intentionally defined as non-nullable.

The SQL Server error is therefore correct and expected for the input the Web page supplies. It is not a schema mismatch or EF Core defect.

The next persistence failure would likely concern `Actor`, because the ordinary UI payload omits that required audit value too.

### Why a Migration Is Not the Fix

The required audit fields are consistently defined by the entity, EF mapping, migration, and model snapshot. Making them nullable would weaken the audit trail and hide an invalid durable operation. A new migration is neither needed nor appropriate for this error.

## Technical Scenario

### Durable Account Preference Update

**Requirements:** Each desired-state update must identify the target account and a trusted actor; the audit record must preserve both values.

**Preferred Approach:** Keep `AccountId` and `Actor` required, derive actor identity on the server from the authenticated principal, and require an explicit canonical target account before persistence.

```text
Web page -> API client: trailingStopsEnabled + canonical targetAccountId
API endpoint -> resolves actor from ClaimsPrincipal
Application handler -> validates nonblank account and actor
EF store -> persists desired state and non-null audit record
```

**Implementation Details:**

1. Derive actor server-side using the existing convention in `src/TNC.Trading.Platform.Api/Features/Platform/PlatformAuthAuditEventResolver.cs:28-38`; do not accept a browser-supplied actor as audit truth.
2. Reject a missing/blank target account ID at the API/application boundary before creating `AccountPreferencesDesiredStateChange`; remove the null-forgiving operators in the durable handler.
3. Change the Web page/client contract so an existing configured account ID is included when saving.
4. Resolve the first-save identity explicitly. `State.AccountId` can be null for an unconfigured response, so merely passing it fixes later updates only. The authoritative source must be the active IG login, a persisted preferred account, or explicit operator selection. Selecting the first account implicitly would be unsafe.

### Considered Alternatives

* Make audit `AccountId` and `Actor` nullable.
  * Rejected: destroys required audit attribution and merely masks a broken request contract.
* Add a migration to alter the audit table.
  * Rejected: the migration, mapping, and schema agree; the application supplies null.
* Pass the client-provided actor through unchanged.
  * Rejected: audit identity must be derived from the authenticated server-side principal.
* Pass `State.AccountId` without resolving first-save behavior.
  * Rejected: first-time preferences have no account ID, so the same failure remains for the first save.

## Recommended Tests

* Web unit test: after loading a known account, save and assert that the PUT payload contains that account ID.
* API unit test: a missing account returns validation output and does not invoke the application handler.
* API unit test: actor is resolved from the authenticated principal rather than request JSON.
* Functional test: use the normal page/API save path and verify a successful response and non-null audit account/actor values.
* Extend the existing SQL integration assertion to include the persisted audit account ID.

## Recommended Conclusion

The immediate exception is a real contract defect introduced when durable desired-state auditing was added without extending the existing page save request. The schema is correct. Repair the request and command boundary so an explicit account ID is provided and actor identity is resolved server-side; do not alter the audit schema.

Before implementation, decide the canonical target account source for a user’s first preference save. The repository does not establish one in the traced path, so this is an authorization/domain decision rather than a persistence detail.
