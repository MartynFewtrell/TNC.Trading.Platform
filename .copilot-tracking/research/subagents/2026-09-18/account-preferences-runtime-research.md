---
title: Account Preferences Runtime Research
description: Delegated research into the Account Preferences runtime flow, persistence, IG integration, and remediation behavior
ms.date: 2026-09-18
ms.topic: concept
---

## Scope

Research only. This record covers the Account Preferences Web UI, API contracts,
application handlers, IG adapter, current-state persistence, audit and observation
histories, retry verification, target-account handling, and relevant history.

## Status

Complete. No production or test code was modified.

## Current Runtime Flow

### Web UI and HTTP boundary

* The operator-authorized page loads `GET /api/platform/account-preferences`,
	then loads the first observed-history page. It renders the desired value,
	target account, observed value/account, and verification status separately.
	src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:L1-L35
	src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:L64-L106
* `AccountPreferencesPagePresenter.SaveAsync` submits only the selected Boolean,
	applies the returned projection, then states that it was "saved and confirmed".
	Retry and remediation reuse the account and revision from the projection.
	src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPagePresenter.cs:L54-L81
	src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPagePresenter.cs:L97-L122
* `PlatformApiClient.UpdateAccountPreferencesAsync` sends `{ trailingStopsEnabled }`.
	It does not send account ID, actor, expected revision, or correlation ID.
	src/TNC.Trading.Platform.Web/PlatformApiClient.cs:L149-L176
* All five Account Preferences routes require the Operator policy. The HTTP DTOs
	expose optional account and actor fields for updates, but no expected revision.
	src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs:L54-L63
	src/TNC.Trading.Platform.Api/Features/Platform/AccountPreferencesEndpointMapping.cs:L89-L107
* The update endpoint validates the transport DTO, then passes caller-supplied
	account/actor directly into the application request. It receives no
	`ClaimsPrincipal`, so audit identity is not server-derived.
	src/TNC.Trading.Platform.Api/Features/Platform/UpdateAccountPreferencesEndpointHandler.cs:L7-L17

### Desired state and reconciliation

* Infrastructure registers the durable current-state store, SQL lease, observed
	history store, and IG gateway in the normal API composition path. Therefore the
	durable branch, rather than the legacy direct IG branch, is active at runtime.
	src/TNC.Trading.Platform.Infrastructure/Infrastructure/DependencyInjection/PlatformInfrastructureServiceCollectionExtensions.cs:L74-L97
	src/TNC.Trading.Platform.Api/Hosting/PlatformApplicationServiceCollectionExtensions.cs:L40-L52
* `GetAccountPreferencesHandler` treats the current-state projection as
	authoritative: no row returns `Unconfigured`; a row returns it without an IG
	request. Direct IG reads and observed-history appends are compatibility behavior
	only when no current-state store is registered.
	src/TNC.Trading.Platform.Application/Features/AccountPreferences/GetAccountPreferencesHandler.cs:L14-L42
* In the durable branch, `UpdateAccountPreferencesHandler` checks Test/Demo
	support, commits desired state, and returns a successful outcome based on that
	desired value. It does not call `IAccountPreferencesGateway`.
	src/TNC.Trading.Platform.Application/Features/AccountPreferences/UpdateAccountPreferencesHandler.cs:L14-L29
	src/TNC.Trading.Platform.Application/Features/AccountPreferences/AccountPreferencesEnvironmentPolicy.cs:L5-L9
* Current state keeps distinct desired, observed, verification, retry, account,
	attempt, authentication snapshot, correlation, and row-version values.
	src/TNC.Trading.Platform.Application/Features/AccountPreferences/AccountPreferencesCurrentState.cs:L5-L25
* Commit writes a desired-state audit in the same `SaveChangesAsync` transaction,
	increments a revision, and sets status to `Pending`; it does not set
	`NextRetryAtUtc`.
	src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfAccountPreferencesCurrentStateStore.cs:L13-L25
* The background service invokes reconciliation every second. Reconciliation
	selects due work or a requested account, acquires an account-scoped SQL
	application lock, re-reads the projection, observes IG, and conditionally
	completes the matching desired revision. Failures back off at 5, 30, 120, then
	600 seconds; successful verification clears retry state.
	src/TNC.Trading.Platform.Api/Hosting/AccountPreferencesReconciliationSupervisor.cs:L12-L26
	src/TNC.Trading.Platform.Application/Features/AccountPreferences/ReconcileAccountPreferencesHandler.cs:L12-L29
	src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/SqlAccountPreferencesReconciliationLease.cs:L11-L17
* `POST /verification-retry` invokes that same reconciler immediately for the
	posted account. It maps only lease/unavailable state to errors and otherwise
	returns the projection, irrespective of the `Applied` flag.
	src/TNC.Trading.Platform.Api/Features/Platform/RetryAccountPreferencesVerificationEndpointHandler.cs:L7-L16
* A successful automatic or manual IG authentication calls the nudge handler with
	the authenticated account ID, which sets its existing projection due now. Both
	callers swallow nudge exceptions unless cancellation was requested.
	src/TNC.Trading.Platform.Application/Features/AccountPreferences/NudgeAccountPreferencesVerificationHandler.cs:L7-L13
	src/TNC.Trading.Platform.Application/Features/ReconcilePlatformAuthentication/PlatformAuthenticationReconciler.cs:L456-L474
	src/TNC.Trading.Platform.Application/Features/TriggerManualAuthRetry/TriggerManualAuthRetryHandler.cs:L127-L145
	src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfAccountPreferencesCurrentStateStore.cs:L28-L39

### Remediation and IG observation

* Remediation first validates the posted account/revision against persisted state,
	acquires the same lock, invokes `RemediateAsync`, and writes the returned
	observation into current state. It clears retry state even for provider failures.
	src/TNC.Trading.Platform.Application/Features/AccountPreferences/RemediateAccountPreferencesHandler.cs:L7-L24
	src/TNC.Trading.Platform.Api/Features/Platform/RemediateAccountPreferencesEndpointHandler.cs:L7-L17
* IG observation opens a new Demo-credential session and rejects a session whose
	`currentAccountId` differs from the persisted target. IG remediation performs
	session, read, conditional write, and readback with the same session tokens.
	src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgAccountPreferencesGateway.cs:L15-L55
	src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgAccountPreferencesGateway.cs:L105-L148
* The implementation is deliberately restricted to Test/Demo. The IG adapter also
	hard-codes `BrokerEnvironmentKind.Demo`, which currently agrees with that policy.
	src/TNC.Trading.Platform.Application/Features/AccountPreferences/AccountPreferencesEnvironmentPolicy.cs:L5-L9
	src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgAccountPreferencesGateway.cs:L19-L21

### Current state and histories

* The SQL model has one row per platform/broker/account unique key, a row-version,
	and a separate desired-state audit table keyed by state/revision. Repository
	lookup and update currently select by platform/broker only.
	src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/PlatformDbContext.cs:L153-L190
	src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfAccountPreferencesCurrentStateStore.cs:L10-L25
* The observed-history store is append-only and pages by descending
	`(ObservedAtUtc, Id)` within the platform/broker partition. It persists account,
	actor, source, kind, and correlation; the API response drops account ID, and the
	page displays only time, value, kind, and environment.
	src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfTrailingStopsPreferenceObservationStore.cs:L10-L60
	src/TNC.Trading.Platform.Api/Features/Platform/AccountPreferencesEndpointMapping.cs:L15-L16
	src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:L64-L75
* Desired-state audit records persist actor, account, old/new value, revision,
	timestamp, and correlation. No application query, API route, or Web view exposes
	this history.
	src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/Entities/AccountPreferencesDesiredStateAuditEntity.cs:L3-L17
	src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/PlatformDbContext.cs:L179-L191

## Behavioral Gaps and Risks

* A normal Web save sends neither `AccountId` nor `Actor`, although the durable
	handler force-unwraps both and the audit entity requires both. The validator only
	requires them when account is non-null, so this path reaches persistence and can
	fail with a database error rather than a validation problem.
	src/TNC.Trading.Platform.Web/PlatformApiClient.cs:L149-L156
	src/TNC.Trading.Platform.Application/Features/AccountPreferences/UpdateAccountPreferencesValidator.cs:L5-L12
	src/TNC.Trading.Platform.Application/Features/AccountPreferences/UpdateAccountPreferencesHandler.cs:L22-L23
	src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/PlatformDbContext.cs:L181-L190
* A new durable desired state is never scheduled because commit does not populate
	`NextRetryAtUtc`; the supervisor claims only due rows. Verification therefore
	requires a later successful authentication nudge or the manual retry button.
	src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfAccountPreferencesCurrentStateStore.cs:L21-L24
	src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfAccountPreferencesCurrentStateStore.cs:L38-L39
* The UI calls a durable save "confirmed" even though it only committed desired
	state. `HasConfirmedValue` is based on desired, while the displayed confirmation
	comes from observed; a pending state without observation renders "Last confirmed:
	Disabled" and offers an editor. The Pending message also checks
	`PendingVerification`, but the application enum serializes as `Pending`.
	src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPagePresenter.cs:L54-L80
	src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPageViewModel.cs:L19-L36
	src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:L31-L61
	src/TNC.Trading.Platform.Application/Features/AccountPreferences/AccountPreferencesVerificationStatus.cs:L3-L10
* The normal durable reconciliation and remediation flows do not call
	`ITrailingStopsPreferenceObservationStore.AppendAsync`. Since the production
	query path avoids the legacy IG read that does append, the observed-history tab
	will not record normal verification/remediation evidence.
	src/TNC.Trading.Platform.Application/Features/AccountPreferences/GetAccountPreferencesHandler.cs:L14-L42
	src/TNC.Trading.Platform.Application/Features/AccountPreferences/ReconcileAccountPreferencesHandler.cs:L12-L29
	src/TNC.Trading.Platform.Application/Features/AccountPreferences/RemediateAccountPreferencesHandler.cs:L7-L24
* There is no configured target-account source. `PlatformConfigurationSnapshot`
	contains no account field; IG proof data includes a `PreferredAccountId`, but no
	Account Preferences handler uses it. The HTTP body is currently the only durable
	desired-state source, so a caller can nominate any account before IG detects a
	mismatch.
	src/TNC.Trading.Platform.Application/Configuration/PlatformConfigurationSnapshot.cs:L3-L14
	src/TNC.Trading.Platform.Application/Configuration/IgProofDataSnapshot.cs:L3-L8
	src/TNC.Trading.Platform.Application/Features/AccountPreferences/UpdateAccountPreferencesRequest.cs:L3-L3
* Optimistic concurrency is incomplete. The application request supports an
	expected revision, but the HTTP contract and Web client do not expose it. The
	row-version is configured, yet repository operations do not catch/translate
	`DbUpdateConcurrencyException`; two concurrent saves can surface as 500 rather
	than a typed conflict. Completion is revision-guarded, but an independent auth
	nudge can still race with a completion update.
	src/TNC.Trading.Platform.Application/Features/AccountPreferences/UpdateAccountPreferencesRequest.cs:L3-L3
	src/TNC.Trading.Platform.Api/Features/Platform/AccountPreferencesEndpointMapping.cs:L105-L107
	src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfAccountPreferencesCurrentStateStore.cs:L13-L46
	src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/PlatformDbContext.cs:L169-L177
* Observation/remediation converts provider failures such as unauthorized,
	rate-limited, and malformed responses into a generic `Transient` result in many
	durable cases, reducing retry and operator diagnostics. Authentication nudges
	can fail silently, preventing automatic scheduling with no visible warning.
	src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgAccountPreferencesGateway.cs:L15-L32
	src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgAccountPreferencesGateway.cs:L35-L55
	src/TNC.Trading.Platform.Application/Features/ReconcilePlatformAuthentication/PlatformAuthenticationReconciler.cs:L456-L474
* Retries and remediation can return HTTP 200 with a projection when conditional
	completion did not apply, which does not tell the client to reload and retry.
	Remediation also clears `NextRetryAtUtc` and resets retry count after a failed
	provider operation, so it can stop automatic recovery.
	src/TNC.Trading.Platform.Api/Features/Platform/RetryAccountPreferencesVerificationEndpointHandler.cs:L12-L16
	src/TNC.Trading.Platform.Application/Features/AccountPreferences/RemediateAccountPreferencesHandler.cs:L17-L24

## Recommended Design

Use one account-bound desired-state command that the API constructs from
server-side authenticated IG evidence, not from the browser. Require an optimistic
revision, commit desired state plus audit and an immediate due time atomically, and
return `Pending` until reconciliation has observed IG. Keep the external IG adapter
behind the existing application-owned gateway port.

```csharp
var change = new AccountPreferencesDesiredStateChange(
		configuration.PlatformEnvironment,
		configuration.BrokerEnvironment,
		authenticatedEvidence.AccountId,
		request.TrailingStopsEnabled,
		request.ExpectedRevision,
		actorFromClaims,
		timeProvider.GetUtcNow(),
		correlationId);
```

The commit should set `NextRetryAtUtc` to the command timestamp. Reconciliation
should append a successful `VerificationObserved` record, including target/observed
account, and remediation should append `RemediationConfirmed` after its readback.
Persist and return provider failure categories without collapsing them. Translate
row-version and conditional-completion loss into HTTP 409 responses with the latest
projection or an explicit reload requirement.

The UI should send the expected revision, display `Pending` after save, treat only
an in-sync observed value as confirmed, and show account/actor/correlation in the
history diagnostic view. Expose desired-state audit history separately from observed
history because they answer different operator questions.

## Evidence From Tests and History

* Existing tests validate durable state/audit restart persistence, due-work claim
	after authentication nudge, SQL lease contention, stale completion rejection,
	observed-history keyset paging, and retention behavior. They do not exercise the
	Web payload against the durable handler or verify reconciliation/remediation
	history appends.
	test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/AccountPreferencesSqlIntegrationTests.cs:L180-L329
	test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/AccountPreferences/AccountPreferencesHandlerTests.cs:L36-L184
* The test suite captures the prior direct IG update/readback behavior, which is
	bypassed when the current-state store is registered. This explains why legacy
	confirmation-oriented tests can pass while the durable runtime remains pending.
	test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/AccountPreferences/AccountPreferencesHandlerTests.cs:L66-L147
* Commit `6d38c4943f16a0e60e8711ce9b6e4a2ca63f86b4` added current state, desired
	audits, reconciliation, and the UI/API contract. Commit
	`48fc3b7f0e924a039822235443b75437c00aa8aa` changed the page tabs and
	accessibility presentation only.

## Remaining Research Needs

* Confirm the product decision for the canonical target account: authenticated
	session account, stored preferred account, or an explicit operator-selected
	account with server-side authorization.
* Confirm whether failed observations should be represented in the observed-history
	table, a separate attempt/failure history, or both.
* Run an authenticated end-to-end save against SQL-backed infrastructure after the
	command contract is corrected; the current investigation did not execute runtime
	code because the request was research-only.