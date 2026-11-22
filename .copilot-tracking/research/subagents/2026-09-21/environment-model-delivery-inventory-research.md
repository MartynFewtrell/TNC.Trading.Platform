---
title: Environment Model Delivery Inventory Research
description: Verified implementation inventory and delivery sequence for environment model rationalisation.
author: GitHub Copilot
ms.date: 2026-09-21
ms.topic: reference
keywords:
  - environment model
  - broker environment
  - delivery inventory
---

## Research Scope

* Verify the primary environment-model research against the current repository.
* Inventory exact implementation paths, symbols, tests, documentation, and governance assets.
* Define planning-ready implementation slices, validation commands, migration hazards, and unresolved design items.

## Status

Complete. The primary research was checked against the current repository on
2026-09-21. Product code was not modified.

## Sources

* Primary research: `.copilot-tracking/research/2026-09-20/environment-model-rationalisation-research.md`
* Current implementation: source, test, Wiki, and governance files listed in
  the verified inventory below

## Delivery Baseline

The selected model in the primary research remains suitable for delivery:

* `Platform:Environment` is deployment-owned, immutable for the process
  lifetime, and allows `Desktop`, `Development`, `Test`, and `Live`.
* A SQL-backed broker catalog owns named broker environments, their
  provider/kind/lifecycle metadata, approved endpoint-profile key, and
  availability reason.
* A separate SQL selection row owns selected and applied broker catalog IDs.
  Selection is restart-applied, so the current process does not change its
  account or endpoint after an operator saves a new selection.
* `IG Demo` is the single available bootstrap record. Other known kinds can be
  created but remain unavailable until an adapter declares them capable.
* Normal removal is retire and purge: delete credentials and mutable data,
  retain audit/history under ordinary retention, then mark the catalog record
  `Retired`.

## Verified Implementation Inventory

### Application

* `src/TNC.Trading.Platform.Application/Configuration/PlatformEnvironmentKind.cs:3-7`
  contains only `Test` and `Live`. Add `Desktop` and `Development`, then move
  platform classification out of mutable operator configuration.
* `src/TNC.Trading.Platform.Application/Configuration/BrokerEnvironmentKind.cs:3-7`
  contains only `Demo` and `Live`. It is embedded in many feature contracts and
  should be replaced incrementally by a catalog-ID context, rather than
  expanded to represent named environments.
* `src/TNC.Trading.Platform.Application/Configuration/PlatformConfigurationSnapshot.cs`,
  `PlatformConfigurationUpdate.cs`, and
  `PlatformStartupFixedConfiguration.cs` encode the legacy combined model.
* `src/TNC.Trading.Platform.Application/Services/IPlatformConfigurationStore.cs`
  and `PlatformConfigurationService.cs` are the current configuration
  boundary. Split immutable platform context, broker catalog/selection, and
  broker-scoped profile ports here before changing downstream handlers.
* Account Details, Account Preferences, login history, proof data, retry,
  runtime state, event, notification, and auth reconciliation feature ports
  all currently accept or expose `BrokerEnvironmentKind`. The representative
  account preferences policy is
  `src/TNC.Trading.Platform.Application/Features/AccountPreferences/AccountPreferencesEnvironmentPolicy.cs`.

### Infrastructure And Data

* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Configuration/SqlServer/SqlPlatformConfigurationStore.cs:20-185`
  owns bootstrap, selected values, restart state, profile values, credentials,
  and configuration audit in one singleton row. `CommitAsync` at lines 52-128
  mutates both platform and broker values. This is the primary replacement
  seam.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/PlatformDbContext.cs:9-31`
  declares the database sets. Lines 40-224 map scalar broker-environment
  strings and no catalog foreign keys.
* `PlatformConfigurationEntity` and its singleton configuration row mix
  platform environment, broker environment, schedule, retry, notification,
  and restart state. Replace it with platform-independent selection and
  broker-scoped profile tables.
* `ProtectedCredentialEntity` is unique on
  `(BrokerEnvironment, CredentialType)` at
  `PlatformDbContext.cs:51-58`; migrate it to `BrokerEnvironmentId` before
  enabling multiple records of the same kind.
* Tables that need a migration classification are: `AuthRuntimeStates`,
  `AuthRetryCycles`, `IgLoginSnapshots`, `IgProofData`, `OperationalEvents`,
  `ConfigurationAudits`, `NotificationRecords`, `AccountDetailsRetrievals`,
  `TrailingStopsPreferenceObservations`, `AccountPreferencesCurrentStates`,
  `AccountPreferencesDesiredStateAudits`, and
  `AccountPreferencesOperations`. `AccountDetailsAccounts` follows its
  retrieval through the existing parent-child cascade.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Startup/PlatformStartupInitializer.cs:25-112`
  runs `MigrateAsync` in the API startup process, then applies configuration
  and retention. This must be changed for Test/Live so a migration bundle or
  one-shot deployment identity applies schema exactly once. Preserve the
  current guarded legacy-baseline behavior at lines 56-111.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgBrokerAuthenticationGateway.cs:23-29`
  rejects Live before retrieving credentials or starting network I/O.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgAccountDetailsGateway.cs:17-24`
  always retrieves `BrokerEnvironmentKind.Demo` credentials. The corresponding
  Account Preferences gateway follows the same Demo-oriented adapter model.
  These must consume an applied catalog context and an approved endpoint
  profile, never a browser-supplied URL.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Credentials/DataProtection/ProtectedCredentialService.cs`
  is the existing write-only secret protection seam. It must become catalog-ID
  scoped while preserving its no-secret read model.
* Current EF migrations are under
  `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/Migrations/`.
  The latest checked-in migration is
  `20260918164317_AddAccountPreferencesOperations.cs`; create additive
  catalog/defaults migrations before a later legacy-column removal migration.

### API And Hosting

* `src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs:28-67`
  registers the current generic `GET` and `PUT /api/platform/configuration`
  with Operator authorization. Replace the environment-changing portion with
  catalog, credential, broker-selection, deletion-preview, and retirement
  features; retain separate profile editing where appropriate.
* `src/TNC.Trading.Platform.Api/Program.cs:14-36` wires Application and
  Infrastructure, then initializes and reconciles before serving traffic.
  Add immutable platform options/context with startup validation here or in a
  focused service-registration extension. Do not derive platform safety from
  `IHostEnvironment.EnvironmentName`, which remains host diagnostics.
* `src/TNC.Trading.Platform.AppHost/AppHostEnvironmentWiring.cs:7-169`
  injects auth, notification, and Account Preferences values but no
  `Platform__Environment`. Add `Desktop` for local API and Web composition.
* `src/TNC.Trading.Platform.AppHost/AppHostProjectRegistration.cs:7-49`
  attaches both projects to the local SQL resource. It is the local composition
  boundary only; the portable production contract is container image,
  `Platform__Environment`, external SQL connection, secret references, health
  endpoints, and ordinary environment-based configuration.

### Web

* `src/TNC.Trading.Platform.Web/Components/Layout/PlatformHeader.razor:3-68`
  already centers `PlatformEnvironmentBadge` in the shared shell.
* `src/TNC.Trading.Platform.Web/Components/Layout/PlatformEnvironmentBadge.razor:1-18`
  is display-only and currently renders `Platform / Broker`. Extend its model
  for applied broker and restart-pending state; keep it non-interactive.
* `src/TNC.Trading.Platform.Web/Components/Layout/PlatformShellContextProvider.cs`
  and `PlatformShellEnvironment.cs` map the status response into the header.
  They need selected-versus-applied/pending status fields.
* `src/TNC.Trading.Platform.Web/Components/Pages/Configuration/Configuration.razor:40-62`
  presents mutable Platform and Broker selects. Lines 176-219 handle save.
  Replace this portion with the operator broker-selection workflow and its
  server-validated warning acknowledgement. Keep platform display immutable.
* `src/TNC.Trading.Platform.Web/PlatformApiClient.cs`,
  `PlatformConfigurationViewModel.cs`, `UpdatePlatformConfigurationViewModel.cs`,
  and Configuration page presenter/form/model mapper files are the API-client
  and form-contract migration seam.
* Add a dedicated administrator Broker Environments page for named creation,
  metadata, write-only credentials, deletion preview, and typed retirement
  confirmation. Do not turn the header into a management control.

### Tests

* Application unit tests are in
  `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests`.
  Existing restart-policy, configuration-update, Account Details, Account
  Preferences, and reconciliation tests use the legacy enums.
* Infrastructure unit tests are in
  `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests`.
  Key replacement anchors include `SqlPlatformConfigurationStoreTests.cs`,
  `ProtectedCredentialServiceTests.cs`, `PlatformStartupInitializerTests.cs`,
  `OperationalRecordRetentionProcessorTests.cs`, and the three IG gateway test
  files under `Integrations/Ig/`.
* SQL/Docker integration coverage is in
  `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests`.
  `PlatformSqlServerIntegrationTests.cs` and
  `AccountPreferencesSqlIntegrationTests.cs` are the natural migration,
  isolation, backfill, and retention anchors.
* API unit and integration projects are
  `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests` and
  `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests`.
  The existing endpoint mapping and update-validator tests should be replaced
  or narrowed as the generic update contract disappears.
* Web unit tests are in
  `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests`.
  `MainLayoutTests.cs:51` currently asserts `Test / Demo`; `ConfigurationTests.cs`
  and `PlatformApiClientTests.cs` cover the legacy page/client contracts.
* Docker-backed Web functional tests and browser E2E tests are in
  `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests` and
  `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests`.
* AppHost unit coverage is in
  `test/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.UnitTests`.
  Add `Platform__Environment=Desktop` composition assertions here.
* Architecture guard coverage is in
  `test/TNC.Trading.Platform.Architecture/TNC.Trading.Platform.Architecture.IntegrationTests`.

### Wiki And Governance

* `docs/wiki/operator-guide.md:255-330` says Operators can change platform
  and broker environments. Replace with immutable platform display,
  operator/admnistrator selection, unavailable state, restart warning, and
  administrator catalog lifecycle guidance.
* `docs/wiki/runtime-behavior.md:11-27`, `:163-178`, and `:301-303` describe
  Demo-only behavior, startup schema migration, and current environment
  semantics. Update for catalog capability checks, selected/applied behavior,
  retirement retention, and migration-bundle deployment.
* `docs/wiki/api-reference.md:21-36` lists the generic configuration contract.
  Replace it with catalog, credential, selection, preview, retirement, status,
  authorization, warning acknowledgement, concurrency, and no-secret rules.
* Also update `docs/wiki/application-overview.md`, `architecture.md`, and
  `local-development.md` for the two environment concepts, catalog-ID
  partitioning, non-cascading retention, and Desktop AppHost parameter.
* `.github/copilot-instructions.md:49-50` already makes `docs/wiki` the
  implementation source of truth and requires updates before a plan completes.
* `.github/instructions/configuration.instructions.md:23-38` requires SQL
  storage, Blazor operator flows, write-only secrets, and explicit persistence
  services. The planned model complies with these rules.
* `.github/instructions/work-packages.instructions.md:57-58` requires Wiki
  updates before a numbered plan completes, but has no planning-time Wiki
  impact-assessment requirement.
* `.github/prompts/generate-delivery-plan.prompt.md:47-94` requires Wiki work
  in the delivery plan, but does not require affected Wiki pages or a
  change/no-change decision as plan inputs.
* `.github/templates/delivery-plan.template.md` has no Wiki-impact assessment
  section.
* `.github/work-package-workflow-usage.md:36-47` references unavailable
  `.github/agents` assets. Reconcile that documentation independently of this
  feature.

## Recommended Delivery Sequence

1. Establish the immutable platform context.
   Add validated `Platform:Environment` options/context, extend platform
   vocabulary, wire `Desktop` through AppHost, remove platform mutation from
   the API/UI update request, and prove startup fails closed for missing or
   unknown values. This creates the non-negotiable safety boundary before
   changing data ownership.
2. Introduce catalog, selection, and defaults schema additively.
   Create versioned defaults-template, catalog, selection, and broker-scoped
   profile tables. In one migration, seed the first template, backfill `IG
   Demo`, snapshot its profiles, map legacy Live records to unavailable `IG
   Live`, and preserve legacy columns. Test fresh and upgraded databases.
3. Deliver catalog lifecycle and restart-applied selection end to end.
   Add administrator catalog/credential commands and operator-or-administrator
   selection. Enforce role boundaries, normalization uniqueness, lifecycle,
   capability, rowversion, warning acknowledgement, and atomic defaults copy.
   Expose applied/pending status, update the centered header, and replace the
   `/configuration` selectors.
4. Move runtime routing and data isolation to catalog IDs.
   Convert adapters, credentials, reconciliation, Account Details, Account
   Preferences, snapshots, runtime state, retries, and projections. Each
   broker operation must resolve the applied context and validate capability
   before credential access or network I/O. Backfill and validate foreign keys,
   indexes, and record counts before removing legacy enum columns in a later
   migration.
5. Add safe retirement and complete the purge inventory.
   Implement server-bound preview tokens, exact typed-name confirmation,
   rowversion checks, selected/applied rejection, credential purge, and only
   the approved mutable-data purge. Retain historical/audit rows under the
   existing retention service and record a retirement audit.
6. Separate production schema deployment from application replicas.
   Produce and test the EF migration bundle and deployment identity flow for
   Test/Live. Document database provisioning per platform environment and the
   portable Azure Container Apps/Kubernetes container contract. Retain local
   AppHost migrations only where deliberately supported.
7. Finish Wiki and planning-governance updates.
   Update affected Wiki pages as each behavioral slice lands. Add a scoped
   planning-time Wiki-impact rule, prompt input, and template section; repair
   stale workflow-asset references separately.

## Migration Hazards

* The current singleton configuration row owns unrelated concerns. Moving only
  broker fields first can leave profiles, restart state, and credentials split
  across incompatible ownership models.
* Existing startup applies migrations in every API replica. Do not deploy the
  additive migration into multi-replica Test/Live until bundle/job ownership is
  in place.
* Scalar broker strings appear in composite unique indexes, leases, query
  filters, cursor validation, and `SingleOrDefault` queries. Every affected
  index and query needs catalog-ID isolation before legacy columns are removed.
* Legacy `Live` data must not become selectable or trigger credential reads as
  a side effect of migration. Backfill it only as an unavailable catalog record.
* Existing account-details child cascade is valid beneath a retrieval record;
  adding catalog cascades would violate the selected retention model.
* Data Protection key-ring availability is required to read existing protected
  credentials during migration and retirement. Do not rotate, delete, or
  re-encrypt blindly while moving credential keys.
* Current runtime state and restart semantics assume one configuration row. A
  transition must preserve pending selected versus applied state or fail closed
  before any broker work.
* A catalog ID does not provide isolation between Desktop, Development, Test,
  and Live. Provision a distinct database per platform environment.

## Purge Inventory Requiring Design Confirmation

The following is a planning classification, not a completed deletion policy.
Confirm it in the technical specification before implementation:

| Table or concern | Proposed normal-retirement treatment | Reason |
| --- | --- | --- |
| `ProtectedCredentials` | Purge | Secret material is mutable and must not survive retirement |
| Broker-scoped schedule, retry, notification profiles | Purge | Mutable configuration copied from defaults |
| `AuthRuntimeStates`, `AuthRetryCycles`, `IgProofData` | Purge | Current runtime projections and retry state |
| Latest `IgLoginSnapshots` | Purge candidate | Current projection, but distinguish from daily retained history |
| `AccountPreferencesCurrentStates`, `AccountPreferencesOperations` | Purge | Mutable current state and in-flight/idempotency records |
| `AccountPreferencesDesiredStateAudits`, `TrailingStopsPreferenceObservations` | Retain | Operational/account history subject to retention |
| `AccountDetailsRetrievals` and child accounts | Retain | Historical account data already has retention semantics |
| `OperationalEvents`, `ConfigurationAudits`, `NotificationRecords` | Retain | Lifecycle and operator evidence subject to retention |

## Validation

Run commands from the repository root. Docker is required for the integration,
functional, and browser suites.

```powershell
dotnet restore TNC.Trading.Platform.slnx
dotnet build TNC.Trading.Platform.slnx --no-restore --configuration Release
dotnet test test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/TNC.Trading.Platform.Application.UnitTests.csproj --configuration Release --no-restore
dotnet test test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/TNC.Trading.Platform.Infrastructure.UnitTests.csproj --configuration Release --no-restore
dotnet test test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/TNC.Trading.Platform.Api.UnitTests.csproj --configuration Release --no-restore
dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj --configuration Release --no-restore
dotnet test test/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.UnitTests/TNC.Trading.Platform.AppHost.UnitTests.csproj --configuration Release --no-restore
dotnet test test/TNC.Trading.Platform.Architecture/TNC.Trading.Platform.Architecture.IntegrationTests/TNC.Trading.Platform.Architecture.IntegrationTests.csproj --configuration Release --no-restore
dotnet test test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/TNC.Trading.Platform.Infrastructure.IntegrationTests.csproj --configuration Release --no-restore
dotnet test test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/TNC.Trading.Platform.Api.IntegrationTests.csproj --configuration Release --no-restore
dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/TNC.Trading.Platform.Web.FunctionalTests.csproj --configuration Release --no-restore
dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/TNC.Trading.Platform.Web.E2ETests.csproj --configuration Release --no-restore
```

The CI workflow at `.github/workflows/ci.yml:20-135` uses this same staged
ordering: fast unit/architecture tests, Docker-backed integration and
functional tests, then Playwright/Keycloak browser tests. Add focused test
cases before broad runs for each slice:

* Immutable context: valid Desktop, missing/unknown values, and no UI/API
  mutation path.
* Additive migration: fresh seed, upgrade backfill, defaults snapshot,
  normalized-name uniqueness, and no partial catalog creation.
* Selection: policy/role checks, acknowledgement, concurrency, applied versus
  pending display, restart application, and no credential/network access for
  unavailable records.
* Isolation: two catalog IDs of the same kind, credentials/profiles/account
  data separation, query/index/lease behavior, and protected API projections.
* Retirement: preview-token binding/replay/staleness, selected/applied
  rejection, typed-name confirmation, exact purge counts, retained history,
  and no subsequent credential write or selection.
* Hosting: AppHost Desktop injection and a container-contract test that does
  not depend on Azure Container Apps-specific APIs.

## Open Decisions And Scoped Assumptions

The following are not blockers for phases 1-3 when recorded as explicit
assumptions. They become blockers only before the indicated delivery phase.

| Item | Scoped assumption for planning | Must resolve before |
| --- | --- | --- |
| Broker vocabulary and provider compatibility | Catalog supports `Demo`, `Live`, and `Training`; only `IG Demo` is available. `IG Live` and Training records are creatable but unavailable. No IG Live or Training provider calls are implemented. | Runtime routing availability in phase 4 |
| Database provisioning | One isolated SQL database exists per platform environment. Desktop uses AppHost SQL Server. Test/Live use externally provisioned SQL and a migration bundle once per deployment identity. | Production deployment phase 6 |
| Kubernetes packaging | The portable contract is image plus `Platform__Environment`, external SQL connection, secret references, health endpoints, and ordinary configuration. No Kubernetes manifests/chart are in scope unless separately requested. | Any Kubernetes deployment work |
| Purge inventory | Apply the proposed table classification above; do not add cascade deletes. Treat mixed latest/history data, especially `IgLoginSnapshots`, as a design review checkpoint. | Retirement implementation in phase 5 |
* No production endpoint URL is administrator-entered. Endpoint-profile keys
  remain a server-controlled allow-list.
* The first defaults template disables notifications. Creation copies the
  active version atomically without selecting the new broker or creating
  credentials.
* The Azure Container Apps preference does not create a dependency on its
  runtime APIs. The same configuration contract remains usable by a future
  Kubernetes host.

## Findings

Pending repository verification.

## Delivery Sequence

Pending repository verification.

## Validation

Pending repository verification.

## Open Decisions And Scoped Assumptions

Pending repository verification.
