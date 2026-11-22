---
title: Account Preferences Alternatives Research
description: Research on coherent AccountPreferences status and save operation design.
ms.date: 2026-09-18
ms.topic: concept
---

## Research Scope

Evaluate AccountPreferences implementation alternatives for status convergence,
IG-first save semantics, account and actor sourcing, and warning/error modelling.

## Current State

The durable branch currently makes `GET /account-preferences` a SQL-only
projection and returns `Unconfigured` without calling IG when no row exists.
That is intentional and covered by application tests. The same branch of the
update handler commits desired SQL state and its audit first, returns `Pending`,
and does not call IG. Reconciliation later observes IG, while a separate route
performs the provider write. This does not meet the requested check-status
convergence or IG-first save semantics.

Evidence:

* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/GetAccountPreferencesHandler.cs:14-32`
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/UpdateAccountPreferencesHandler.cs:14-31`
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/ReconcileAccountPreferencesHandler.cs:12-29`
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/RemediateAccountPreferencesHandler.cs:9-21`
* `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/AccountPreferences/AccountPreferencesHandlerTests.cs:36-69`
* `docs/wiki/api-reference.md:553-569`

The current state already records desired and observed values separately, a
desired revision, retry state, sanitized failure summary, correlation ID, and a
row version. Desired-state changes create append-only audits in the same SQL
save. Successful legacy reads and writes append observation history, but the
durable reconcile and remediation handlers do not currently append that history.

Evidence:

* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/AccountPreferencesCurrentState.cs:5-23`
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfAccountPreferencesCurrentStateStore.cs:13-25`
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfAccountPreferencesCurrentStateStore.cs:41-50`
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/ITrailingStopsPreferenceObservationStore.cs:5-9`
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/UpdateAccountPreferencesHandler.cs:44-59`

The IG adapter has the needed account-bound behavior: it rejects a session whose
current account differs from the requested target, reads before writing, avoids
the PUT when the value is already correct, and confirms a PUT by reading again.
It returns safe categories rather than provider payloads. This is suitable for
the command design, but its remediation result does not expose the initial
observation, so preserving both the pre-remediation drift and post-write state
in history requires a small gateway-result expansion or an explicit observation
before remediation.

Evidence:

* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgAccountPreferencesGateway.cs:13-55`
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/AccountPreferencesObservationResult.cs:3-9`
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/AccountPreferencesRemediationResult.cs:3-10`
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/AccountPreferencesFailureCategory.cs:3-15`

`AccountId` and `Actor` are presently accepted from the account-preferences
HTTP request. Retry and remediation also require a client-supplied account, and
the remediation endpoint substitutes the literal `operator`. These are not
authoritative sources. The authentication workflow already persists the latest
successful IG session account, and the API has a reusable claim resolver for
the authenticated operator name.

Evidence:

* `src/TNC.Trading.Platform.Api/Features/Platform/AccountPreferencesEndpointMapping.cs:103-105`
* `src/TNC.Trading.Platform.Api/Features/Platform/UpdateAccountPreferencesEndpointHandler.cs:8-17`
* `src/TNC.Trading.Platform.Api/Features/Platform/RetryAccountPreferencesVerificationEndpointHandler.cs:7-15`
* `src/TNC.Trading.Platform.Api/Features/Platform/RemediateAccountPreferencesEndpointHandler.cs:7-15`
* `src/TNC.Trading.Platform.Application/Services/IPlatformIgLoginSnapshotStore.cs:5-10`
* `src/TNC.Trading.Platform.Application/Configuration/IgLoginSnapshot.cs:3-13`
* `src/TNC.Trading.Platform.Api/Features/Platform/PlatformAuthAuditEventResolver.cs:28-36`

## Recommended Design

Adopt one coherent command-oriented design with a read projection and two
operator commands:

* Keep `GET /api/platform/account-preferences` as a fast SQL-only projection.
* Add `POST /api/platform/account-preferences/check-status` as
  `CheckAccountPreferencesStatus`. It has no request body.
* Retain `PUT /api/platform/account-preferences`, but reduce its body to
  `trailingStopsEnabled` and an `expectedRevision`. Implement it as
  `SaveAccountPreferences`.

The commands return the same complete `AccountPreferencesResponse` projection
as `GET`, including desired and observed values, revision, verification status,
retry metadata, and a safe warning. The Web page applies that response directly
to refresh the top-page projection. The history remains a separate paged query.

This retains a useful availability property: opening the page does not make IG
availability a prerequisite. The explicit Check status command makes provider
I/O and automated convergence an intentional, observable operator action.

### Check Status

`CheckAccountPreferencesStatus` resolves the target account from the latest
successful `IPlatformIgLoginSnapshotStore` record for the configured broker
environment. It then obtains the SQL state for the configured platform and
broker environment. If no desired state exists, return the fresh SQL
`Unconfigured` projection without an IG write. If the state account and latest
authenticated account differ, return a `200` projection with
`VerificationFailed` and safe `AccountMismatch` warning; do not query or write
IG against an untrusted choice.

For a matching configured state, take the existing SQL lease, reread the row,
and ensure its desired revision and account still match. Observe IG. On a
successful observation, append an `Observed` history row with the authenticated
actor and server-generated correlation ID, then compare observed and desired:

* When equal, complete the projection as `InSync` and return `200`.
* When different, call the account-bound remediation gateway with the stored
  desired value. It performs at most one PUT and confirms with a GET. Append a
  second `Remediated` history row for the confirmed result, complete as
  `InSync`, and return `200`.
* When observation or remediation is unavailable, unknown, rejected, or has an
  account mismatch, update failure/retry fields, retain the last known observed
  value, and return `200` with `VerificationFailed` plus a safe warning. The
  command was processed; provider convergence was not established.

The command must append history only for a successful observed Boolean, never
for an unknown provider outcome. It must persist the final projection before
returning it. The existing reconciliation completion is already revision-guarded
and the lease is keyed by environment and account, which supplies the core
cross-replica protection.

Evidence for existing guards:

* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/PlatformDbContext.cs:166-181`
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/ReconcileAccountPreferencesHandler.cs:20-29`
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfAccountPreferencesCurrentStateStore.cs:41-47`
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/AccountPreferencesSqlIntegrationTests.cs:260-293`

### Save Preferences

`SaveAccountPreferences` resolves the same server-owned target account and
actor, validates `expectedRevision` against the current state, takes the lease,
and calls account-bound IG remediation before committing a new desired SQL
revision. A successful IG result includes a confirmed GET value. Only when that
value equals the requested Boolean does the command atomically commit desired
state and its audit, then complete the fresh projection as `InSync` and append
a `Saved` observation. The response is the persisted projection, not a gateway
outcome or a synthetic `Pending` response.

There is no distributed transaction between IG and SQL. To make the requested
IG-first ordering recoverable, introduce a small `AccountPreferencesOperations`
table before the IG call. It is an idempotency journal, not desired state: store
the caller's required `Idempotency-Key`, target account, requested value,
baseline revision, actor, correlation ID, and phase. It prevents a duplicate
write on a retried request and marks the operation `RemoteApplied` before the
SQL desired-state transaction. The desired-state row and audit are written only
after IG confirms the value.

If IG is indeterminate, return `503` with a safe `SaveOutcomeUnknown` warning,
leave desired state unchanged, and mark the operation for resolution. Check
status resolves an active operation for the same account by observing IG before
it compares desired state or remediates drift. If IG has the requested value, it
finalizes the desired-state commit exactly once; otherwise, it marks the
operation failed and proceeds from the durable desired value. If the SQL commit
fails after IG confirmation, return `503` with the same warning and retain
`RemoteApplied` for retry. This prevents a later check from silently remediating
IG back to the old desired value.

An equal requested value remains idempotent: the gateway avoids PUT, the command
does not advance desired revision or add an audit when the current desired and
confirmed IG values already match, and it may append one observed history row
for the command. A stale expected revision or a held lease returns `409`; neither
calls IG. A reused idempotency key with different value, account, or baseline
returns `409`. Cancellation before a provider response is treated as an unknown
remote outcome and must retain the journal rather than retry the PUT blindly.

## API, UI, and Persistence Changes

API changes:

* Map the protected `POST /account-preferences/check-status` route and inject
  `ClaimsPrincipal` and `HttpContext` into both command handlers.
* Remove `AccountId` and `Actor` from update, retry, and remediation request
  bodies. Fold retry and remediation into Check status, then retire their routes
  after client migration.
* Use `PlatformAuthAuditEventResolver.ResolveUserName` for actor and
  `HttpContext.TraceIdentifier` or a validated `Idempotency-Key` for correlation.
  Do not accept either identity from JSON.
* Return the response projection for successful and recoverably degraded command
  outcomes. Use Problem Details only for input validation (`400`), stale or
  conflicting keys (`409`), lease contention (`409`), and unresolved save
  outcome (`503`). Problems must retain the existing secret-safe category,
  title, and detail discipline.

UI changes:

* Add a Check status action that calls the new command and applies its returned
  projection. Replace the separate Retry verification and Apply desired setting
  controls.
* Save sends `trailingStopsEnabled`, `expectedRevision`, and an idempotency key.
  It applies the returned projection, so confirmation accurately says saved and
  confirmed only for `InSync`.
* Model command feedback separately from the durable projection warning. The
  existing presenter already keeps load, save, history, and current-operation
  errors distinct, which is an appropriate pattern to retain.

Evidence:

* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPagePresenter.cs:5-13`
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPagePresenter.cs:46-95`
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:14-42`
* `src/TNC.Trading.Platform.Web/PlatformApiClient.cs:140-181`

Persistence changes:

* Add the operations journal with a unique idempotency-key index and an
  account/environment/baseline-revision index for resolution.
* Extend the current-state store with transactional finalize methods that write
  the desired audit, operation phase, fresh projection, and history with
  revision checks. Do not use a client-provided account to select the row.
* Either extend `AccountPreferencesRemediationResult` to return both initial and
  confirmed observations, or have Check status explicitly observe then remediate
  and append both successful observations.

## Warning and Error Model

Keep verification status as a durable fact, not a proxy for HTTP success:
`Unconfigured`, `InSync`, `VerificationFailed`, and `Drifted` remain meaningful.
Under the recommended automatic remediation, `Drifted` is normally transient
inside a Check status command and should be persisted only if remediation cannot
establish convergence. `Pending` belongs only to a journalled, unresolved save,
not to a response that claims IG-first save completion.

Expose a typed, safe warning code alongside the existing bounded
`FailureSummary`, for example `NoAuthenticatedAccount`, `AccountMismatch`,
`ProviderUnavailable`, `ProviderTimeout`, `ProviderRejected`,
`SaveOutcomeUnknown`, and `LocalPersistenceFailed`. Map it from
`AccountPreferencesFailureCategory`; never expose provider bodies, credentials,
or session tokens. The existing HTTP mapping already centralizes category-to-
Problem Details behavior and should remain the single mapping point.

Evidence:

* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/AccountPreferencesVerificationStatus.cs:3-11`
* `src/TNC.Trading.Platform.Api/Features/Platform/AccountPreferencesEndpointMapping.cs:20-91`
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/GetAccountPreferencesHandler.cs:40-47`
* `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/AccountPreferences/AccountPreferencesHandlerTests.cs:121-135`

## Rejected Alternatives

### Keep the SQL-Only GET and Existing Separate Commands

Rejected because it cannot satisfy a Check status request that compares current
desired SQL state to IG and automatically repairs drift. It also leaves the UI
with two manual recovery paths whose account IDs are supplied by the browser.

### Change GET into a Converging Provider Command

Rejected because GET would perform provider writes. It breaks HTTP read
semantics, makes page load mutate an external broker setting, and turns an IG
outage into an initial-page failure. Current tests and documentation explicitly
protect SQL-only availability during provider failure.

Evidence:

* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/AccountPreferences/AccountPreferencesE2ETests.cs:18-28`
* `docs/wiki/runtime-behavior.md:728-741`

### Commit SQL Desired State Before Calling IG

Rejected because it contradicts the requested save ordering and reports local
intent as durable before provider confirmation. It is the current durable-path
behavior and is suitable only for an asynchronous-intent workflow.

### IG-First Save Without an Idempotency Journal

Rejected because a timeout or SQL failure after a confirmed IG change creates an
ambiguous remote change with the old local desired state. A subsequent automatic
status check could then undo the operator's successful remote change. The
journal resolves this narrow but consequential consistency gap without claiming
a distributed transaction.

### Derive Target Account or Actor from Client JSON

Rejected because browser input is not authority for broker account selection or
audit identity. The latest successful IG login snapshot and authenticated claims
are both available server-side and are consistent with the account-mismatch
protection already enforced by the IG adapter.

## Test and Documentation Implications

Replace the expectations that GET is the only status path and that save returns
`Pending`. Add focused application tests for matching status, drift followed by
one remediation, observe/remediate failures, account mismatch, lease contention,
revision race, history append order, and safe warnings. Add persistence
integration tests for idempotency-key uniqueness, replay after IG confirmation
but before desired-state commit, and journal resolution without revision loss.

Update API tests for body-free Check status, server-derived actor/account,
projection-on-degraded-status, Problem Details conflict and unknown-save cases,
and removed client identity fields. Update bUnit and Playwright coverage for a
single Check status recovery action, automatic drift correction, projection
refresh after save, disabled command states, and distinct durable warning versus
transient save feedback. The controllable provider supports bounded HTTP
request-sequence assertions, although it must model the current-account ID and
the typed provider response used by the production adapter before it can prove
account mismatch and convergence end-to-end.

Documentation requiring revision:

* `docs/wiki/api-reference.md:553-569`
* `docs/wiki/runtime-behavior.md:728-746`
* `docs/wiki/application-overview.md:195-212`
* `docs/wiki/operator-guide.md:565-589`
* `docs/wiki/local-development.md:146-164`
* `docs/wiki/testing-and-quality.md:559-590`