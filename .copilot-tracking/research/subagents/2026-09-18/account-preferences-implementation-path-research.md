---
title: Account Preferences Implementation Path Research
description: Verified implementation anchors for IG-first account-preferences save, check-status convergence, UI rationalisation, testing, and documentation.
ms.date: 2026-09-18
ms.topic: reference
---

## Research Scope

* Verify the implementation paths for the selected account-preferences design:
	IG-first idempotent save journal, body-free Check status compare-and-converge
	command, Blazor rationalisation, test scope, and wiki updates.
* Record source, test, wiki, and validation-command evidence without modifying
	product code.

## Status

Complete. The recommended design is feasible within the existing vertical
slice, but requires new persistence and application contracts rather than a
route-only change.

## Selected Implementation Path

Keep `GET /api/platform/account-preferences` as the existing SQL-only page
projection. Replace the browser-facing retry and remediation workflow with two
explicit commands:

* An IG-first `PUT /api/platform/account-preferences` with a required
	`Idempotency-Key` header and a body containing only
	`trailingStopsEnabled` and `expectedRevision`.
* A body-free `POST /api/platform/account-preferences/check-status` that
	compares IG with the persisted desired state, remediates drift, and returns
	the resulting persisted projection.

Derive the target account from the current successful IG login snapshot and
the actor from authenticated claims. The browser must not supply either value.
Use a durable operation journal around the IG-to-SQL boundary. This prevents a
second remote write when IG has confirmed a save but SQL finalisation failed or
the caller timed out.

## Verified Source Anchors

### API and Application

* `src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs:54-62`
	maps the current projection, update, observations, retry, and remediation
	routes under the Operator policy. Add Check status here and remove the legacy
	browser routes after migration.
* `src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs:71-75`
	dispatches retry and remediation separately. A new thin endpoint handler is
	the appropriate owner for a body-free command.
* `src/TNC.Trading.Platform.Api/Features/Platform/AccountPreferencesEndpointMapping.cs:8-19`
	maps the complete current-state projection and unconfigured response.
	Preserve projection ownership here while removing browser-facing aliases when
	the Web client has migrated.
* `src/TNC.Trading.Platform.Api/Features/Platform/AccountPreferencesEndpointMapping.cs:105-107`
	currently defines HTTP DTOs that accept `AccountId` and `Actor`; replace them
	with a minimal save DTO and no Check status DTO.
* `src/TNC.Trading.Platform.Api/Features/Platform/PlatformAuthAuditEventResolver.cs:25-34`
	provides `ResolveUserName(ClaimsPrincipal)` for the server-derived actor.
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/UpdateAccountPreferencesHandler.cs:14-30`
	is the active durable save branch. It writes SQL desired state first and
	returns a successful outcome without IG I/O, which is the root behavioral
	mismatch to replace.
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/UpdateAccountPreferencesHandler.cs:35-75`
	is the retained direct gateway branch. Its update/readback and indeterminate
	outcome handling supplies useful semantics for the new IG-first command, but
	it is not the active durable branch.
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/ReconcileAccountPreferencesHandler.cs:12-29`
	already supplies account lease acquisition, state reread, revision guard,
	provider observation, retry timing, and safe bounded failure persistence. It
	observes drift only and does not remediate it.
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/RemediateAccountPreferencesHandler.cs:7-24`
	owns the existing revision-bound, leased remediation path. Fold its gateway
	call into the new Check status handler rather than retaining an independent
	browser command.
* `src/TNC.Trading.Platform.Api/Hosting/PlatformApplicationServiceCollectionExtensions.cs:45-50`
	registers the current handlers. Register the new Check status and journal
	save handlers here.

### IG, Identity, and Persistence

* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgAccountPreferencesGateway.cs:12-28`
	observes account-bound preferences and returns the exact requested account
	mismatch warning before preference I/O.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgAccountPreferencesGateway.cs:30-50`
	performs the required read, conditional write, readback remediation sequence
	in one IG session.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgAccountPreferencesGateway.cs:151-176`
	uses `accounts/preferences`, `trailingStopsEnabled`, and a `SUCCESS` PUT
	acknowledgement. These are the production-double contract anchors.
* `src/TNC.Trading.Platform.Application/Services/IPlatformIgLoginSnapshotStore.cs:5-12`
	exposes `GetLatestSnapshotAsync`, not the `GetLatestSuccessfulAsync` name
	used by the earlier recommendation.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfPlatformIgLoginSnapshotStore.cs:40-56`
	reads the `Latest` snapshot, and `:92-105` maps its `CurrentAccountId`.
	This is the existing server-side target-account source.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfAccountPreferencesCurrentStateStore.cs:11-27`
	atomically writes desired state plus audit and sets `Pending`. It needs a
	journal-aware finalisation method that atomically persists desired state,
	audit, confirmed observation, `InSync` projection, and journal completion.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfAccountPreferencesCurrentStateStore.cs:41-47`
	conditionally applies reconciliation by state ID and revision. Retain this
	compare-and-set boundary for Check status.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/PlatformDbContext.cs:165-194`
	maps current state and desired-state audit. Add the operation-journal DbSet,
	mapping, unique idempotency key, and EF migration alongside these entities.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/DependencyInjection/PlatformInfrastructureServiceCollectionExtensions.cs:73-76`
	registers the snapshot store; `:94-97` registers the observation/current
	state stores and account lease. Register the journal store in this composition
	root.

### Blazor Rationalisation

* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:14-57`
	renders the redundant title, desired/target/observed/status values, separate
	retry/remediation actions, and current configuration section. Replace this
	with `Trailing stops`, one durable warning region, `Last confirmed`, one
	Check status control, and horizontal configuration controls.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:59-84`
	retains the read-only observed-history tab and paged history control. Keep
	this diagnostic view.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferences.razor:112-125`
	delegates Save, Retry, and Remediate to the presenter. Replace the latter two
	with a Check status delegate.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPagePresenter.cs:54-83`
	applies the save projection but incorrectly announces confirmation under the
	current SQL-first behavior. Its projection refresh and history-reload pattern
	can be retained after a confirmed IG-first save.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountPreferences/AccountPreferencesPagePresenter.cs:95-114`
	exposes separate retry and remediation calls. Replace both with a single
	operating state and Check status call.
* `src/TNC.Trading.Platform.Web/PlatformApiClient.cs:149-176`
	sends only the Boolean on save but sends browser-supplied account ID and
	revision on legacy actions. Add an idempotency header and expected revision
	to save; replace the two legacy methods with a body-free Check status method.

## Verified Test Scope

* `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/AccountPreferences/AccountPreferencesHandlerTests.cs:38-69`
	proves the SQL-only unconfigured/configured GET behavior. Retain this
	non-I/O query contract.
* `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/AccountPreferences/AccountPreferencesHandlerTests.cs:74-147`
	covers the legacy direct gateway save branch. Replace or supplement this with
	tests for journal replay, lease/revision conflicts before IG I/O, confirmed
	IG-first finalisation, equal-value idempotence, and unknown remote outcomes.
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/AccountPreferencesSqlIntegrationTests.cs:175-223`
	covers state/audit durability and due work; `:226-244` covers lease
	contention. Extend it for journal uniqueness/replay, `RemoteApplied`
	recovery, and atomic desired/audit/history/journal completion.
* `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/AccountPreferencesEndpointTests.cs:22-32`
	is the current endpoint-handler unit-test anchor. Add minimal DTO validation,
	required idempotency header, body-free Check status, server-derived identity,
	`409`, and unknown-save `503` tests here or in focused adjacent files.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/AccountPreferences/AccountPreferencesFunctionalTests.cs:15-79`
	currently proves SQL-first persistence and retry recovery while posting
	client account/actor values. Replace it with closed-box provider sequences
	for IG-first save and Check status convergence.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountPreferencesTests.cs:92-184`
	anchors bUnit information architecture and feedback tests. Update expected
	labels, warning placement, one action model, projection refresh, and removal
	of account/status/old confirmation text.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/AccountPreferences/AccountPreferencesE2ETests.cs:22-116`
	has five focused browser scenarios. Lines 45-47, 68-70, 88-90, and 108-110
	use `Space` to select a Radzen tab, and lines 66-67 use `ArrowRight`; retain
	a deliberately specified keyboard regression only if the component supports
	it, otherwise use `ClickAsync` for activation tests.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/Authentication/RealAuthenticationE2ETestFixture.cs:16-22`
	creates one real AppHost fixture with a controllable provider. It does not
	reset SQL state itself, so scenarios that assume `Unconfigured` need
	fixture-owned reset/setup.
* `test/Shared/AccountPreferences/ControllableIgProvider.cs:107-115`
	serves obsolete `GET` and `PUT /gateway/deal/preferences` routes and an
	`enabled` property. Production instead uses `accounts/preferences` and
	`trailingStopsEnabled`.
* `test/Shared/AccountPreferences/ControllableIgProvider.cs:117-125` also
	omits the required session response `currentAccountId` and the `CST` and
	`X-SECURITY-TOKEN` headers required by
	`IgAccountPreferencesGateway.CreateSessionAsync` at
	`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgAccountPreferencesGateway.cs:126-139`.
	Fix this double and add a provider contract test before relying on it for
	successful distributed checks.

## Wiki Updates

* `docs/wiki/api-reference.md:551-574` documents the current SQL-first save,
	retry, and separate remediation API. Update it for the idempotency header,
	minimal save body, body-free Check status, server-derived account/actor, and
	`409`/`503` outcomes.
* `docs/wiki/runtime-behavior.md:728-747` describes pending desired state and
	observe-only verification. Update it for IG-first confirmation, journal
	recovery, and Check status compare-and-converge while retaining SQL-only GET.
* `docs/wiki/operator-guide.md:565-589` contains the old terminology and
	confirmation/retry/remediation workflow. Update the names, warning behavior,
	automatic correction, and revised page layout.
* `docs/wiki/architecture.md:600-619` should document the new operation
	ownership, lease/revision guard, history, and journal boundary.
* `docs/wiki/application-overview.md:195-212` should describe explicit broker
	commands over a SQL projection rather than a page-load editor.
* `docs/wiki/local-development.md:146-164` should record the corrected
	controllable-provider route/payload and repeatable mismatch/recovery checks.
* `docs/wiki/testing-and-quality.md:559-605` should replace the current
	retry/remediation coverage description with unit, SQL, functional, bUnit,
	and browser responsibilities for the new behavior.

## Repository Validation Commands

CI is the authoritative project-partition pattern:

```powershell
dotnet restore TNC.Trading.Platform.slnx
dotnet build TNC.Trading.Platform.slnx --no-restore --configuration Release
dotnet test test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/TNC.Trading.Platform.Application.UnitTests.csproj --configuration Release --no-restore
dotnet test test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/TNC.Trading.Platform.Infrastructure.IntegrationTests.csproj --configuration Release --no-restore
dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj --configuration Release --no-restore
dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/TNC.Trading.Platform.Web.FunctionalTests.csproj --configuration Release --no-restore
dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/TNC.Trading.Platform.Web.E2ETests.csproj --configuration Release --no-restore
```

Evidence: `.github/workflows/ci.yml:31-54`, `:80-93`, and `:121-140`.
The local wiki baseline is `dotnet build` and `dotnet test` from the repository
root at `docs/wiki/local-development.md:88-94` and `:214-223`; it documents
`dotnet test -m:1` for intermittent local MSBuild child-node exits.

## Gaps and Clarifying Questions

* The current idempotency key lifecycle is a design decision: confirm retention
	period, whether completed entries retain the full response or state ID only,
	and whether a matching retry blocks or returns a recoverable in-progress
	outcome.
* Define whether an account change in the latest login snapshot should create a
	new unconfigured account-preferences state or surface a safe mismatch until
	an operator saves again.
* Define the exact HTTP contract for a Check status command that has processed
	but cannot confirm convergence. The recommendation is `200` plus the durable
	`VerificationFailed` projection; this needs product-owner confirmation.
* The supplied session context reports that the filtered
	`AccountPreferencesE2ETests` command exited with code 1, but its detailed
	output was not available during this research. The static test and provider
	mismatches above are verified; reproduce the focused run after implementation
	planning begins to classify the exact failures.
