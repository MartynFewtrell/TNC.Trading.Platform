---
title: Broker Environment Create and Delete Research
description: Research on named broker-environment lifecycle management, bootstrap defaults, deletion safety, and data retention.
ms.date: 2026-09-21
ms.topic: concept
---

## Research scope

Investigate the existing environment-rationalisation implementation for these additions:

* Named broker environments, including multiple Training environments
* Creation defaults aligned with the current SQL database load/bootstrap script
* Administrator deletion of an environment and its related settings and data
* An accidental-deletion safeguard and protection for the currently applied runtime environment

## Research status

In progress. The current implementation remains keyed by the `BrokerEnvironmentKind`
enum, which has only `Demo` and `Live` values. This is an initial incompatibility with
multiple named Training environments that the remainder of this research will trace
through database storage, APIs, UI, documentation, and tests.

## Initial evidence

* `src/TNC.Trading.Platform.Application/Configuration/BrokerEnvironmentKind.cs`
  defines the current broker-environment key type.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/PlatformDbContext.cs`
  maps numerous records with a `BrokerEnvironment` scalar column rather than an
  environment aggregate and foreign-key relationship.

## Existing Rationalisation Research

The predecessor research is
`.copilot-tracking/research/2026-09-20/environment-model-rationalisation-research.md`.
Its selected model remains appropriate: a durable broker-environment catalog,
separate selected and applied identities, and an immutable deployment-owned
platform environment.

The additions in this research refine that model as follows:

* A catalog record needs a unique administrator-selected display name. `Kind`
  remains a capability and policy classification (`Demo`, `Training`, `Live`),
  not a unique identifier. Therefore, multiple Training records are supported.
* Creation must copy safe defaults from the current bootstrap configuration,
  rather than copying a potentially unsuitable existing environment.
* Removal must be a lifecycle transition for records with audit history, not a
  physical cascade through audit evidence.

## Current State and Evidence

### Bootstrap and SQL setup

There is no checked-in production SQL load or seed script. The two `.sql`
files under `artifacts/quality-gates/` are generated EF migration scripts,
not supported data-loading assets:

* `artifacts/quality-gates/phase-8-account-preferences-migration.sql:1-218`
  creates initial tables and migration history, including scalar
  `BrokerEnvironment` columns, but inserts no platform or broker configuration
  data.
* `artifacts/quality-gates/phase-3-3-account-preferences-migration.sql:1-218`
  has the same generated-schema role.

The supported startup path is EF migration followed by code bootstrap:

* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Startup/PlatformStartupInitializer.cs:28-56`
  runs migrations before applying startup configuration and retention.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Configuration/SqlServer/SqlPlatformConfigurationStore.cs:127-157`
  creates one `PlatformConfigurations` row only when none exists.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Configuration/SqlServer/PlatformConfigurationBootstrapParser.cs:8-44`
  requires `Bootstrap:BrokerEnvironment`, parses it as the enum, and supplies
  schedule, retry, notification, and `UpdatedBy` defaults.
* `src/TNC.Trading.Platform.Api/appsettings.json:45-74` currently supplies
  `Test` / `Demo`, 08:00-16:30 UTC on weekdays, excluded weekends, a 1/5/2/60/5
  retry policy, `operator@local.test`, and `api-bootstrap`.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Configuration/SqlServer/SqlPlatformConfigurationStore.cs:20-52`
  preserves a pending startup-fixed pair as the runtime value until restart;
  `PlatformConfigurationRestartPolicy.cs:3-14` makes broker-environment
  change restart-required.

Creation defaults should therefore be a versioned server-owned default profile
matching those bootstrap values, not browser-provided values and not a clone of
the active environment. The create response should state the selected defaults
and leave credentials absent. The server should seed exactly one `IG Demo`
catalog record from the legacy bootstrap `Demo` value during migration.

### Broker-environment keyed tables and referential behavior

`PlatformDbContext.cs:7-33` declares the full current persistence surface.
`PlatformDbContext.cs:39-197` maps every environment value as a scalar string.
The current direct keys are:

| Table | Current broker key and relevant constraint | Evidence |
| --- | --- | --- |
| `PlatformConfigurations` | Singleton row with mutable scalar broker value | `PlatformDbContext.cs:39-49`; `20260727202238_InitialPlatformSchema.cs:155-182` |
| `ProtectedCredentials` | Unique `(BrokerEnvironment, CredentialType)` | `PlatformDbContext.cs:51-59`; `20260727202238_InitialPlatformSchema.cs:184-229` |
| `AuthRuntimeStates` | Scalar environment context | `PlatformDbContext.cs:61-72`; `20260727202238_InitialPlatformSchema.cs:38-67` |
| `AuthRetryCycles` | Scalar environment context | `PlatformDbContext.cs:74-83`; `20260727202238_InitialPlatformSchema.cs:14-36` |
| `IgLoginSnapshots` | Index `(BrokerEnvironment, SnapshotKind, TradingDay)` | `PlatformDbContext.cs:85-94`; `20260727202238_InitialPlatformSchema.cs:90-108` |
| `IgProofData` | Unique `BrokerEnvironment` | `PlatformDbContext.cs:96-104`; `20260728094818_AddDurableIgProofData.cs:14-43` |
| `OperationalEvents` | Scalar audit/event context | `PlatformDbContext.cs:106-117`; `20260727202238_InitialPlatformSchema.cs:132-152` |
| `ConfigurationAudits` | Scalar audit context | `PlatformDbContext.cs:119-130`; `20260727202238_InitialPlatformSchema.cs:69-88` |
| `NotificationRecords` | Scalar notification evidence context | `PlatformDbContext.cs:132-144`; `20260727202238_InitialPlatformSchema.cs:110-130` |
| `AccountDetailsRetrievals` | Broker-scoped indexes and automatic-per-day uniqueness | `PlatformDbContext.cs:146-163`; `20260807103334_AddAccountDetailsSnapshots.cs:14-30,69-81` |
| `TrailingStopsPreferenceObservations` | Broker/platform/time index | `PlatformDbContext.cs:165-177`; `20260830160041_RemoveTrailingStopsBrokerEnvironmentIndex.cs:13-24` |
| `AccountPreferencesCurrentStates` | Unique `(PlatformEnvironment, BrokerEnvironment, AccountId)` | `PlatformDbContext.cs:179-194`; `20260831130930_AddAccountPreferencesCurrentState.cs:14-84` |
| `AccountPreferencesDesiredStateAudits` | Broker/platform/account/revision index | `PlatformDbContext.cs:196-208`; `20260831130930_AddAccountPreferencesCurrentState.cs:44-99` |
| `AccountPreferencesOperations` | Unique `(PlatformEnvironment, BrokerEnvironment, IdempotencyKey)` | `PlatformDbContext.cs:210-220`; `20260918164317_AddAccountPreferencesOperations.cs:14-50` |

There is no `BrokerEnvironments` table and no foreign key from any listed table
to a broker-environment owner. The only relevant cascade in the entire model is
`AccountDetailsRetrievals -> AccountDetailsAccounts`:

* `PlatformDbContext.cs:155-163` specifies `DeleteBehavior.Cascade` for the
  retrieval's account children.
* `20260807103334_AddAccountDetailsSnapshots.cs:54-59` creates that foreign
  key and cascade.

Consequently, an environment delete cannot safely rely on database cascade
today. Manual free-text deletes would risk incomplete cleanup, and converting
all data to cascade through a catalog record would remove audit evidence.

### API, UI, and integration constraints

The runtime is presently enum-bound end to end:

* `src/TNC.Trading.Platform.Application/Configuration/BrokerEnvironmentKind.cs:3-7`
  defines only `Demo` and `Live`.
* `src/TNC.Trading.Platform.Api/Features/UpdatePlatformConfiguration/UpdatePlatformConfigurationValidator.cs:9-25`
  rejects values other than those enum members.
* `src/TNC.Trading.Platform.Api/Features/UpdatePlatformConfiguration/UpdatePlatformConfigurationMapping.cs:7-28`
  parses the request directly to the enum.
* `src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs:37-64`
  has no catalog, creation, deletion, or deletion-confirmation endpoint;
  configuration changes are currently Operator-authorized, while the sole
  administrator endpoint is read-only authentication metadata.
* `src/TNC.Trading.Platform.Web/Components/Pages/Configuration/Configuration.razor:42-62`
  offers only `Demo` and `Live` selectors. There is no delete dialog or
  confirmation interaction anywhere in the Web UI.
* `src/TNC.Trading.Platform.Web/PlatformApiClient.cs:31-60` exposes only the
  generic configuration get/update contract.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgBrokerAuthenticationGateway.cs:17-33`
  rejects `Live`; `IgAccountDetailsGateway.cs:17-23` and
  `IgAccountPreferencesGateway.cs:16-76` resolve `Demo` credentials directly.

The practical consequence is that creating a named Training environment must
not make it selectable or executable until an adapter and capability profile
support it. A name is metadata, not proof of safe broker routing.

### Retention and audit evidence

Operational and audit data is deliberately retained, then aged out:

* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Operations/Retention/OperationalRecordRetentionProcessor.cs:17-58`
  deletes expired events, configuration audits, notifications, retained login
  snapshots, account retrievals, and preference observations.
* `docs/wiki/runtime-behavior.md:11-27` documents the 90-day operational
  retention policy and the historical Demo-to-Test presentation mapping.
* `docs/wiki/runtime-behavior.md:163-178` describes configuration audit and
  notification persistence as diagnostic evidence.

Physical hard deletion is not appropriate for audit records. It would override
the configured retention policy, make it impossible to explain who created,
changed, retired, or attempted to remove an environment, and could break
historical correlation. Catalog deletion should be logical: set lifecycle to
`Retired`, disable selection and credentials, revoke/erase protected credentials,
and retain the catalog identity and audit-bearing records until the ordinary
retention policy permits their deletion. A separate, explicitly approved data
purge policy is required before deleting audit records or a catalog tombstone.

## Recommended Lifecycle and Data Model

### Catalog record

Introduce `BrokerEnvironments` with a stable `BrokerEnvironmentId` (`Guid`),
unique normalized name, display name, provider, kind, lifecycle, approved
endpoint-profile key, availability reason, created/updated/retired audit data,
and a rowversion concurrency token. The name should be required, case-insensitive
unique, trim-normalized, and immutable after first successful use. A display-name
rename could be allowed with an audit entry, but identifier mutation should not
rekey historical records.

Permit many records where `Kind = Training`; require names such as
`Training Alpha` and `Training Regression`. `Kind` controls policy and adapter
eligibility, while the `Guid` partitions credentials and historical data.

Introduce a separate singleton `BrokerEnvironmentSelection` record with
`SelectedBrokerEnvironmentId`, `AppliedBrokerEnvironmentId`, revision,
restart-required state, and audit values. Creating an environment does not
select it. Selecting changes `Selected`; startup validates it and moves it to
`Applied`. All runtime gateway, credential, lock, projection, and status logic
uses `Applied` only.

### Creation contract and defaults

Use an Administrator-only `POST /api/platform/broker-environments` command with
a minimal request: name, display name, kind, provider, and approved
endpoint-profile key. Do not accept arbitrary URLs, initial credentials,
schedule, retry, platform-environment, or capability flags from the browser.

The server creates the catalog record in `Draft` or `Unavailable` and creates
the configuration/settings profile from a named bootstrap-default profile:

* weekdays Monday-Friday
* 08:00-16:30 `UTC`
* `ExcludeWeekends`
* retry values 1, 5, 2, 60, and 5
* notification recipient `operator@local.test` and resolved provider
* no credentials and no applied/selected state change

The actual durable creation defaults should be centralized in a new named
`BrokerEnvironmentDefaults` value object used by bootstrap and creation. This
prevents default drift between `appsettings.json`, bootstrap parsing, and the
administrator flow. Tests should assert the new value object against the
current bootstrap values above.

### Deletion semantics and accidental-deletion safeguard

The user requirement to delete the broker environment plus related data/settings
is achievable only for mutable, non-audit dependent data. Treat administrator
Delete as a two-stage *retire and credential purge* operation:

1. `POST /api/platform/broker-environments/{id}/delete-preview` returns the
   immutable identity, display name, lifecycle, selected/applied status, and
   counts of credentials, settings, runtime state, snapshots, account data,
   preference state, operations, and audit records. It mints a short-lived
   single-use confirmation token bound to the administrator, target ID,
   rowversion, and preview counts.
2. `DELETE /api/platform/broker-environments/{id}` requires the current
   rowversion, confirmation token, and exact normalized display name. The
   command is Administrator-only, records a `BrokerEnvironmentRetired` audit,
   clears protected credentials, removes mutable per-environment settings and
   current runtime projections, and marks the catalog row `Retired`.

The UI should require an explicit action from the Broker Environments
administration page, show the preview counts, require typing the environment's
exact name, and disable the final command until the phrase matches. A generic
browser confirmation is insufficient because it does not bind the command to
the server-observed target, concurrency state, or administrator identity.

The API must reject both preview and deletion with `409 Conflict` when the
target is `AppliedBrokerEnvironmentId`, and reject ordinary selection of a
retired record. It should also reject when the target is `Selected` unless the
same command first changes selection to a different available record and that
selection has been successfully applied after restart. This avoids deleting the
currently applied runtime environment, including the subtle case where a
selection is pending but the old applied record remains active.

For an emergency forced-delete concept, do not provide a normal API route.
Require an offline, audited operator procedure that stops all API replicas,
verifies no applied/selected reference, exports data as required by retention
policy, and executes a migration-approved purge. This is a governance decision,
not an ordinary UI capability.

## Migration Implications

Use additive migrations and do not rewrite or delete historical values in the
first release:

1. Add catalog and selection tables; seed deterministic `IG Demo` and
   unavailable `IG Live` catalog records. Map legacy `Demo` to `IG Demo` and
   legacy `Live` to `IG Live`.
2. Add nullable `BrokerEnvironmentId` columns and foreign keys to active
   configuration, protected credentials, current runtime state, retry state,
   proof data, snapshots, account retrievals, and account-preference state and
   operations. Backfill all rows by legacy scalar value.
3. Keep audit/event/notification records append-only. Add nullable catalog ID
   where useful for query efficiency, retain original broker label and a
   name-at-event snapshot for historical readability, and use `Restrict` or no
   FK cascade from audit tables.
4. Change all reads, indexes, locks, idempotency keys, routes, and credential
   lookup to catalog IDs. Replace the enum contract incrementally. In
   particular, update the broker-specific application ports before enabling a
   second available environment.
5. Verify non-null backfill, foreign-key integrity, index replacement,
   selected/applied protection, and record counts. Only remove legacy scalar
   columns in a later migration after a documented compatibility window.

Current `BrokerEnvironment` strings are max length 32 in the model, so a `Guid`
foreign key avoids imposing display-name limitations on data partitioning.
The migration must retain the existing `AccountDetailsAccounts` cascade from a
retrieval, but must not introduce a cascading catalog-to-retrieval path that
could delete parent and child history accidentally.

## API, UI, Documentation, and Test Work

### API and UI behavior

Add administrator-only catalog list, create, detail, credential-update,
selection, deletion-preview, and deletion/retire endpoints. Return no protected
values. Return `403` for non-administrators, `404` for absent IDs, `409` for
rowversion, active/selected, lifecycle, or confirmation-token conflicts, and
`422` for invalid names/default profiles. Keep the existing configuration endpoint
for schedule/retry/notification changes only after broker selection is removed
from it.

Create a dedicated `/administration/broker-environments` page rather than
overloading `/configuration`. Show list state, lifecycle, availability reason,
selected/applied markers, credential presence, create form, write-only
credential form, and a destructive confirmation dialog. The centered shell
indicator remains display-only for viewers and reflects the applied name; it
must show a pending selection/restart indicator separately.

### Required test coverage

Follow existing patterns:

* Bootstrap and store tests: `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/PlatformConfigurationBootstrapParserTests.cs:7-37`
  and `SqlPlatformConfigurationStoreTests.cs:16-258` test bootstrap defaults,
  durability, restart semantics, and secret-safe audit data.
* SQL migration tests: `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/PlatformSqlServerIntegrationTests.cs:49-155`
  migrate an empty database and validate broker-scoped lock behavior.
* API role tests: `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/PlatformAuthenticationIntegrationTests.cs:76-224`
  prove Viewer, Operator, and Administrator boundaries against real Keycloak.
* Component tests: `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/ConfigurationTests.cs:12-129`
  cover load, save, restart indication, errors, and secret-safe rendering.

Add unit, SQL integration, API integration, component, and Playwright coverage
for named duplicate-kind Training creation, unique-name races, default alignment,
unavailable capability before credential/network access, selected versus applied
restart behavior, role denial, stale rowversion, expired/replayed confirmation
token, typed-name mismatch, active/selected deletion conflict, credential purge,
and audit/data retention after retirement. Extend
`test/Shared/Authentication/ManagedAppHostFixture.cs:75-116` so cleanup keys by
catalog ID rather than literal `Demo`.

### Wiki and Copilot updates

Delivery must update these Wiki pages, as required by
`.github/copilot-instructions.md:49-50`:

* `docs/wiki/application-overview.md:9-32` for applied named broker context
* `docs/wiki/architecture.md:105-125,239-278` for catalog, selection, ID
  partitioning, migration, and non-cascading audit retention
* `docs/wiki/runtime-behavior.md:11-27,163-178,301-344` for applied versus
  selected state, startup, retirement, credential purge, and retention
* `docs/wiki/operator-guide.md:16-30,68-90,255-330` for administrator create,
  selection, pending restart, and typed deletion confirmation
* `docs/wiki/api-reference.md:150-222,342-446` for catalog/selection/delete
  endpoints, authorization, conflict responses, and secret-safe response shapes
* `docs/wiki/local-development.md:80-151` for bootstrap defaults, seeded
  `IG Demo`, and local lifecycle/reset boundaries

Update `.github/instructions/configuration.instructions.md` with the catalog,
selection, write-only credential, and retirement rules. Update
`.github/instructions/work-packages.instructions.md` so plans that alter broker
environment lifecycle must assess the listed Wiki pages and state whether
retention/audit behavior changes. The predecessor research already recommends
the latter Wiki-assessment rule; do not duplicate it in every agent artifact.

## Risks and Decisions Needed

* The user requirement says deletion must remove related data/settings, but
  audit, event, notification, and historical account data have a documented
  retention purpose. Product and compliance owners must approve whether any of
  those records may ever be purged early. This research recommends no.
* There is no defined provider or capability contract for `Training`. The
  system can create multiple named Training records now, but they should be
  `Unavailable` until a supported adapter/endpoint profile is supplied.
* The destination selection rule after deleting a selected-but-not-applied
  environment needs product confirmation. This research recommends rejecting
  deletion until a different selection has been applied by restart.
* The current singleton configuration has one schedule/retry/notification
  profile. Confirm whether those settings become per broker environment or
  remain platform-wide. The recommended creation defaults assume an
  environment-scoped profile, because the request requires related settings to
  be removed with the environment.

## Research Status

Complete. No product files were changed. The requested research artifact now
contains the current implementation evidence, all identified keyed tables and
foreign-key behavior, lifecycle recommendation, deletion safeguard, migration
and test implications, Wiki/Copilot updates, and decision blockers.