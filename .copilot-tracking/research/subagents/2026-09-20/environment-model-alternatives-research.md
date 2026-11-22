---
title: Environment Model Alternatives Research
description: Evidence-led evaluation of platform and broker environment designs for the TNC Trading Platform.
ms.date: 2026-09-20
ms.topic: architecture
---

## Research Status

Complete.

## Scope

* Evaluate viable designs for an immutable platform environment, persistent broker-environment catalog and current selection, credentials, environment-scoped account data, shared shell UX, creation workflow, availability gating, and migration from singleton SQL configuration.
* Select one end-to-end approach with bounded data, API, and UI contracts.
* Treat Training as planned future scope and do not invent IG integration.
* Address restart semantics and the incompatibility between the current Live selection and Demo-only gateways.

## Evidence and Findings

### Current State

* The runtime currently uses one `PlatformConfigurations` row containing both
	`PlatformEnvironment` and `BrokerEnvironment`. The store creates it from
	bootstrap values when absent, updates both values in one commit, and makes
	credentials follow the selected broker enum. See
	`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Configuration/SqlServer/SqlPlatformConfigurationStore.cs`.
* A platform/broker change is persisted immediately and sets `RestartRequired`.
	Before the next start, runtime reads retain the start-applied pair. Startup
	clears the flag. The current contract is described in
	`docs/wiki/runtime-behavior.md` under "Startup-fixed changes" and encoded by
	`PlatformConfigurationRestartPolicy` in
	`src/TNC.Trading.Platform.Application/Configuration/PlatformConfigurationRestartPolicy.cs`.
* `PlatformEnvironmentKind` has `Test` and `Live`; `BrokerEnvironmentKind` has
	`Demo` and `Live`. This makes the current broker identity a two-value enum,
	rather than a durable catalog record. See
	`src/TNC.Trading.Platform.Application/Configuration/PlatformEnvironmentKind.cs`
	and `src/TNC.Trading.Platform.Application/Configuration/BrokerEnvironmentKind.cs`.
* Protected credentials are encrypted with ASP.NET Core Data Protection and
	uniquely keyed by broker-environment enum plus credential type. See
	`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Credentials/DataProtection/ProtectedCredentialService.cs`,
	`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/Entities/ProtectedCredentialEntity.cs`,
	and `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/PlatformDbContext.cs`.
* Account-preference state, observations, audit entries, and operations already
	use the platform/broker pair. Account-details retrievals, login snapshots,
	and proof data use broker environment only. See
	`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/PlatformDbContext.cs`.
* The Web shell fetches status once per scoped context and renders a display-only
	center `Platform / Broker` badge. There is no selector, mutation callback, or
	refresh path after a selection. See
	`src/TNC.Trading.Platform.Web/Components/Layout/MainLayout.razor`,
	`src/TNC.Trading.Platform.Web/Components/Layout/PlatformShellContextProvider.cs`,
	`src/TNC.Trading.Platform.Web/Components/Layout/PlatformHeader.razor`, and
	`src/TNC.Trading.Platform.Web/Components/Layout/PlatformEnvironmentBadge.razor`.

### Capability Mismatch

The current Live choice is not executable. The authentication gateway returns
`UnsupportedEnvironment` before HTTP when asked for `Live`; its HTTP client is
registered against `https://demo-api.ig.com/gateway/deal/`. Account-details is
also hard-wired to Demo credentials and the Demo base URL, while account
preferences always reads Demo credentials. See
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgBrokerAuthenticationGateway.cs`,
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgAccountDetailsGateway.cs`,
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgAccountPreferencesGateway.cs`,
and
`src/TNC.Trading.Platform.Infrastructure/Infrastructure/DependencyInjection/PlatformInfrastructureServiceCollectionExtensions.cs`.

Therefore, persisting or displaying a current `Live` broker selection must not
imply that it can authenticate, read account data, or execute preferences. The
selected design needs an explicit capability decision before any gateway or
credential access. A UI-only disable is insufficient.

### Deployment Environment Guidance

`platform-environment-guidance-research.md` recommends a deployment-owned,
startup-fixed `Platform:Environment`, distinct from ASP.NET Core's host
environment and from an operator-managed broker selection. It cites Microsoft
guidance that the app environment cannot change during runtime, and recommends
allow-listed options validation with `ValidateOnStart`.

The relevant Microsoft sources confirm the boundary:

* [ASP.NET Core runtime environments](https://learn.microsoft.com/aspnet/core/fundamentals/environments?view=aspnetcore-10.0)
	states that the app environment cannot change while the application runs and
	identifies `IHostEnvironment.EnvironmentName` as the runtime source.
* [Options pattern](https://learn.microsoft.com/dotnet/core/extensions/options)
	documents `ValidateOnStart` and distinguishes immutable `IOptions<T>` from
	refresh-capable options mechanisms.
* [Configure ASP.NET Core Data Protection](https://learn.microsoft.com/aspnet/core/security/data-protection/configuration/overview?view=aspnetcore-10.0)
	requires durable, access-controlled key storage for containers and describes
	Key Vault protection with managed identity for Azure deployments.

`current-environment-implementation-research.md` supplies the implementation
inventory, including the singleton row, header behavior, SQL keying, gateway
restrictions, and existing coverage. `environment-model-rationalisation-research.md`
sets the scope assumptions that Desktop and Demo are the only currently
available environments and that Training is planned.

## Selected End-to-End Approach

### Ownership Model

Adopt a three-part model.

| Concern | Owner | Mutability | Source of truth |
| --- | --- | --- | --- |
| Platform environment | Deployment composition | Immutable for a process lifetime | Required `Platform:Environment` deployment setting, validated at startup |
| Broker-environment catalog | Operator control plane | Persistent and versioned | SQL catalog |
| Current broker selection | Operator control plane | Persistent, restart-applied | SQL singleton selection record referencing catalog identity |

`Platform:Environment` remains `Test` or `Live`, is supplied by AppHost or the
deployment definition as a non-secret, and is bound once through a
`PlatformEnvironmentOptions` type. Validate it against an allow list and
deployment policy using `ValidateOnStart`; expose an immutable
`IPlatformEnvironmentContext` value to the Application layer. It must not be
read from the SQL operator configuration, remotely refreshed, or altered by a
broker-selection API.

The broker catalog uses a stable `BrokerEnvironmentId` (`Guid`) rather than an
enum as the durable foreign key. Seed one active `IG Demo` record from the
legacy `Demo` value. A record contains only non-secret metadata:

* `BrokerEnvironmentId`, `BrokerKind`, display name, and immutable creation
	audit metadata
* `EnvironmentKind`: `Demo`, `Live`, or planned `Training`
* `LifecycleState`: `Draft`, `Available`, `Unavailable`, `Retired`
* capability flags or a normalized capability set for `Authenticate`,
	`ReadAccountDetails`, `ReadAccountPreferences`, and
	`WriteAccountPreferences`
* a non-secret endpoint profile key, not an arbitrary operator-provided URL
* reason text for unavailable capabilities and optimistic concurrency token

Training is a planned catalog kind only. It has no IG endpoint, no IG gateway
registration, and no implied behavior. A future broker adapter can add an
explicit Training endpoint profile and capabilities after a separate design and
integration are approved.

Store current selection in a one-row `BrokerEnvironmentSelection` record:
`SelectedBrokerEnvironmentId`, `AppliedBrokerEnvironmentId`, `Revision`,
`RestartRequired`, and audit metadata. `Selected` is the persisted intent;
`Applied` is the process-start selection. This preserves the current pending
restart semantics without allowing a post-save runtime switch. The immutable
platform value is supplied separately to every status, operation, and audit
projection.

### Capability and Safety Rules

The selection service evaluates a broker record against the immutable platform
environment and locally registered adapter capabilities before persisting an
executable selection. It returns a structured availability result, not a
boolean inferred from `Test` versus `Live`.

For the present implementation:

* `IG Demo` is `Available` in platform `Test` and exposes only capabilities
	actually implemented by the Demo gateway registrations.
* `IG Live` is represented, when needed, as `Unavailable` because the
	registered gateway set is Demo-only. It must expose a reason such as
	`No registered live gateway` even when platform `Live` would otherwise permit
	it.
* On platform `Test`, `IG Live` has an additional policy block. The API records
	the blocked selection or operation attempt and follows the existing
	notification policy; no credential read and no outbound request occur.
* `Training` is `Unavailable` with reason `Planned environment; no integration
	is registered`. It cannot be selected as applied and cannot create IG
	credentials.

Every operation resolves the applied catalog identity once, then runs a
`CanExecute(capability, platformEnvironment, brokerEnvironment)` check before
retrieving protected credentials or dispatching to a gateway. Gateway routing
is by endpoint profile and registered adapter capability, never by a user
supplied URL. This removes the current risk that a persisted Live selection can
be mistaken for a supported route.

### Credentials and Environment-Scoped Data

Move protected credentials to `BrokerEnvironmentId` plus credential type; keep
the value write-only and retain `ProtectionKind`, timestamps, actor, and an
optimistic concurrency token. No list or status response returns protected
values. Credential-presence status is catalog-scoped and only writable for an
available environment that supports authentication.

Convert all broker-only operational partitions to `BrokerEnvironmentId`. Keep
the immutable platform environment alongside records whose policy and audit
meaning depend on it, including account preferences. Add platform environment
to account-details, login-snapshot, and proof-data partition keys only if the
same physical database will intentionally retain data from multiple platform
deployments. The selected deployment model should instead use an isolated
database per platform environment; that is the safer default and avoids
cross-environment retention in the first place.

Within one database, each current environment-scoped record must use the
catalog identity, and unique indexes must include it. Read APIs take either no
environment identifier, meaning the applied broker selection, or an explicit
read-only historical `BrokerEnvironmentId` with authorization. Mutations never
take a caller-controlled environment identifier: they use the applied
selection after capability gating.

### Technical Flow

1. Deployment supplies `Platform__Environment`; API and Web validate the same
	 immutable value on startup. AppHost local configuration supplies `Test`.
2. The API runs migrations, creates the Demo catalog record and selection if
	 missing, migrates legacy values, then snapshots `Selected` into `Applied`.
3. Status returns the immutable platform value, applied catalog summary,
	 pending selected summary when different, capabilities, and restart state.
4. An administrator creates a Draft broker environment with provider, kind,
	 display name, and allowed endpoint profile. The server derives capabilities
	 from registered adapters and platform policy; it does not accept them from
	 the browser.
5. The administrator saves credentials only for an available, authentication-
	 capable environment. Secret fields are write-only.
6. An administrator requests current selection with a row-version/selection
	 revision. The API validates lifecycle, policy, adapter availability, and
	 credentials. It writes `Selected`, emits an audit event, and sets
	 `RestartRequired` when `Selected != Applied`.
7. Until restart, all background work and mutation endpoints use `Applied`.
	 After restart, initialization copies the selected identity to `Applied`,
	 clears the restart flag, and reconciliation runs only if the applied record
	 is executable for the requested capability.

### Bounded Contracts

Replace the environment fields in generic configuration with dedicated,
operation-specific contracts.

| Surface | Contract boundary |
| --- | --- |
| Read status | `platformEnvironment`, `appliedBrokerEnvironment`, optional `pendingBrokerEnvironment`, `restartRequired`, and availability/capability summaries. No secrets and no endpoint URLs. |
| List catalog | Catalog summaries only: id, name, provider, kind, lifecycle, availability, reason, and credential presence. |
| Create/update catalog | Administrator-only command with provider, kind, display name, and endpoint profile key. Reject arbitrary URLs, duplicate display identities, and `Training` activation. |
| Save credentials | Administrator-only command scoped to catalog id. Write-only `apiKey`, `identifier`, and `password`; reject when authentication is unavailable. |
| Select current broker environment | Administrator-only command with catalog id and concurrency token. Returns applied and pending summaries, restart requirement, and a structured rejection reason. |
| Gateway request | Internal request carries immutable platform context, applied catalog id, and required capability. It does not carry a client-provided broker enum or raw endpoint. |

The centered header remains compact: `Platform / Broker` means immutable
platform plus **applied** broker selection. When a restart is pending, retain
the applied pair and show a small restart-required state in the existing
tooltip or adjacent indicator. It must not prematurely show the persisted
selection as active. Authorized administrators can open a popover/menu from
the broker portion only; it lists catalog entries with availability and reason,
links to the create workflow, and confirms a selection request. Viewers see a
non-interactive badge. The full create and credential workflow belongs on a
dedicated broker-environments page, not the header.

## Migration From Singleton Configuration

1. Introduce immutable platform-environment options and context before changing
	 SQL ownership. Remove platform environment from the operator update request
	 once the host-composed value is active.
2. Add catalog and selection tables plus `BrokerEnvironmentId` nullable foreign
	 keys to credentials and every broker-scoped table. Keep legacy enum columns
	 during backfill.
3. In one migration transaction, create `IG Demo`, map legacy `Demo` records to
	 its id, create the selection row from the old singleton broker value, and
	 set both selected and applied identity to Demo. Do not synthesize Live or
	 Training credentials or history.
4. Backfill records with the Demo catalog id. For any legacy Live records,
	 create an `IG Live` catalog entry in `Unavailable` state and map their data
	 without activating it. Preserve original audit timestamps and actors.
5. Change stores, projections, gateways, and indexes to use catalog ids;
	 deploy code that can read both transitional fields if rolling deployment is
	 required.
6. Validate migration counts and foreign-key coverage, then remove legacy
	 broker enum columns and the singleton environment fields in a later migration.
	 Retain the singleton only for dynamic global settings such as schedule,
	 retry, and notification configuration, renamed to avoid implying ownership
	 of environment state.

The migration must not erase a pending restart. If the legacy configuration
has `RestartRequired=true`, map the persisted legacy broker selection to
`Selected` and retain the previously start-applied pair as `Applied` when it is
available from runtime state. If the prior applied identity cannot be recovered,
fail closed, require restart, and do not execute broker work until startup has
validated an applied catalog record.

## Likely File Impacts

These are evidence-derived likely paths, not implementation changes made by
this research.

* Startup composition and immutable context:
	`src/TNC.Trading.Platform.Api/Program.cs`,
	`src/TNC.Trading.Platform.AppHost/AppHostEnvironmentWiring.cs`,
	`src/TNC.Trading.Platform.AppHost/AppHostSettings.cs`, and
	`src/TNC.Trading.Platform.Api/appsettings.json`.
* Application configuration and operation contracts:
	`src/TNC.Trading.Platform.Application/Configuration/PlatformEnvironmentKind.cs`,
	`src/TNC.Trading.Platform.Application/Configuration/BrokerEnvironmentKind.cs`,
	`src/TNC.Trading.Platform.Application/Configuration/PlatformConfigurationSnapshot.cs`,
	`src/TNC.Trading.Platform.Application/Services/IPlatformConfigurationStore.cs`,
	`src/TNC.Trading.Platform.Application/Services/PlatformConfigurationService.cs`,
	and the feature contracts under
	`src/TNC.Trading.Platform.Application/Features/GetPlatformConfiguration/` and
	`src/TNC.Trading.Platform.Application/Features/UpdatePlatformConfiguration/`.
* SQL catalog, credentials, and migration:
	`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/PlatformDbContext.cs`,
	`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/Entities/PlatformConfigurationEntity.cs`,
	`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/Entities/ProtectedCredentialEntity.cs`,
	a new catalog/selection entity pair in the same `Entities` folder,
	`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Configuration/SqlServer/SqlPlatformConfigurationStore.cs`,
	`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Credentials/DataProtection/ProtectedCredentialService.cs`,
	and a new EF migration under
	`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/Migrations/`.
* Capability-safe IG routing:
	`src/TNC.Trading.Platform.Infrastructure/Infrastructure/DependencyInjection/PlatformInfrastructureServiceCollectionExtensions.cs`,
	`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgBrokerAuthenticationGateway.cs`,
	`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgAccountDetailsGateway.cs`,
	and
	`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgAccountPreferencesGateway.cs`.
* HTTP and operator UI:
	`src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs`,
	new focused API feature folders for broker catalog and selection,
	`src/TNC.Trading.Platform.Web/PlatformApiClient.cs`,
	`src/TNC.Trading.Platform.Web/Components/Layout/PlatformShellContextProvider.cs`,
	`src/TNC.Trading.Platform.Web/Components/Layout/PlatformHeader.razor`,
	`src/TNC.Trading.Platform.Web/Components/Layout/PlatformEnvironmentBadge.razor`,
	and a new administrator page under
	`src/TNC.Trading.Platform.Web/Components/Pages/`.
* Evidence points to updates in
	`docs/wiki/application-overview.md`, `docs/wiki/api-reference.md`,
	`docs/wiki/operator-guide.md`, `docs/wiki/runtime-behavior.md`, and
	`docs/wiki/architecture.md` when implementation begins.

## Acceptance and Test Scenarios

* Startup fails when `Platform:Environment` is missing, unknown, or violates
	deployment policy; changing SQL configuration cannot alter the immutable
	runtime platform value.
* Fresh bootstrap creates only `IG Demo`, selects and applies it, and exposes
	it in status/header as `Test / IG Demo` (or the approved label).
* An administrator creates a catalog record, but it remains Draft without
	executable capabilities until server-side validation accepts its endpoint
	profile and adapter support.
* An `IG Live` catalog record is visible but unavailable on Test, records a
	blocked attempt, sends the configured notification, reads no credentials, and
	dispatches no HTTP request.
* An `IG Live` record stays unavailable on platform Live while the registered
	adapters are Demo-only. The response clearly reports missing gateway support;
	it cannot be selected as applied.
* A Training record can be created only as planned/unavailable, has no IG
	credential form or gateway calls, and cannot be selected.
* Credential writes encrypt values, return only presence, and are isolated by
	catalog id. Decryption of one catalog's credential never returns another's
	value.
* Account details, login snapshots, proof data, account preferences, audit,
	and operations never cross catalog ids. Their indexes reject duplicate data
	only within the intended catalog scope.
* Selecting a different available broker persists `Selected`, leaves
	`Applied` unchanged, sets restart-required, and leaves existing runtime work
	on the applied identity. Header status shows the applied pair and pending
	restart state.
* After restart, startup applies the selected identity exactly once, clears the
	pending marker only after validation, and routes authentication through the
	selected environment's supported adapter.
* Concurrent selection updates honor the selection row version and return a
	conflict rather than overwriting another operator's selection.
* API integration tests cover role boundaries and no-secret responses; Web
	component/functional tests cover the centered header, unavailable reasons,
	selection confirmation, pending restart display, and cancellation-safe status
	refresh. Infrastructure integration tests cover backfill, foreign keys,
	credential encryption, and capability guards before network dispatch.

## Alternatives Rejected

### Keep the Singleton and Extend the Enum

Rejected because it cannot represent multiple named broker environments,
per-environment lifecycle, endpoint profile, capability status, or a durable
selection identity. It also continues to conflate mutable operator selection
with immutable platform policy.

### Make Platform Environment an Operator Setting

Rejected because an operator mutation cannot safely alter deployment safety
classification. It conflicts with the supplied platform-environment guidance
and Microsoft runtime-environment immutability guidance. A SQL update would
also allow platform policy to differ across replicas before restart.

### Immediate Runtime Broker Switching

Rejected for this platform stage. Existing reconciliation, credentials, and
gateway clients assume a start-applied value, and immediate switching risks
in-flight operations spanning credentials or endpoints. Persisted selection
with explicit restart is safer and matches current semantics.

### Expose Live as Available on Platform Live Now

Rejected because capability is absent. The current authentication, account
details, and account-preferences paths are Demo-only. Platform Live permission
is necessary but not sufficient; a capability-safe catalog requires both
policy and registered adapter support.

### Add Training as an IG Environment Now

Rejected because no Training provider behavior, endpoint, credentials, or IG
integration evidence exists. Treating a label as a functional environment
would invent a broker contract and weaken availability signals.

## Open Questions and Blockers

### Decisions Needed Before Delivery

* Confirm the approved display labels, especially whether the seeded Demo record
	is named `IG Demo` or `IG Test` while preserving the existing IG terminology.
* Confirm whether separate databases are mandatory for Test and Live. This
	research recommends that separation; a shared database requires broader
	platform-environment partitioning for all historical data.
* Confirm whether unavailable catalog records may be created for future Live
	and Training use now, or whether the catalog initially permits only Demo.
* Confirm the deployment target and how it provides `Platform__Environment` in
	Azure. The supplied guidance leaves this as a local policy decision.

### Blockers

No research-access blocker remains. Delivery is blocked on the four product and
deployment decisions above, particularly the authoritative database-isolation
policy and the intended naming/creation policy for future environments.
