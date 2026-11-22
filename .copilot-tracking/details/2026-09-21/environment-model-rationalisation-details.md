<!-- markdownlint-disable-file -->
# Implementation Details: Environment Model Rationalisation

## Context Reference

Sources: `.copilot-tracking/research/2026-09-20/environment-model-rationalisation-research.md` and `.copilot-tracking/research/subagents/2026-09-21/environment-model-delivery-inventory-research.md`.

## Implementation Phase 1: Establish Immutable Platform Context

<!-- parallelizable: false -->

### Step 1.1: Define platform context contracts

Create an Application-owned `PlatformEnvironmentOptions` bound to `Platform:Environment`, an immutable `IPlatformEnvironmentContext`, and a validated implementation. Extend `PlatformEnvironmentKind` with `Desktop` and `Development`; use a closed allow-list and reject absent, unknown, or policy-invalid values during startup. Do not use `IHostEnvironment.EnvironmentName` as this domain value. Remove platform environment from mutable update/snapshot/startup-fixed configuration contracts and pass the context explicitly to callers.

Files:
* `src/TNC.Trading.Platform.Application/Configuration/PlatformEnvironmentKind.cs` - Add `Desktop` and `Development`.
* `src/TNC.Trading.Platform.Application/Configuration/PlatformEnvironmentOptions.cs` - Create binding/validation model.
* `src/TNC.Trading.Platform.Application/Configuration/IPlatformEnvironmentContext.cs` - Create the read-only context port.
* `src/TNC.Trading.Platform.Application/Configuration/PlatformConfigurationSnapshot.cs` - Remove mutable platform ownership.
* `src/TNC.Trading.Platform.Application/Configuration/PlatformConfigurationUpdate.cs` - Remove platform update input.
* `src/TNC.Trading.Platform.Application/Services/IPlatformConfigurationStore.cs` - Split immutable platform use from mutable configuration storage.

Discrepancy references:
* DD-01 in `.copilot-tracking/plans/logs/2026-09-21/environment-model-rationalisation-log.md`.

Success criteria:
* One Application abstraction exposes the immutable platform kind to all layers.
* No mutable request or persistence operation can change the platform kind.

Dependencies:
* None.

### Step 1.2: Wire and enforce deployment input

Register options with `ValidateOnStart` in API and Web composition. Supply `Platform__Environment=Desktop` through AppHost to both projects. Remove the bootstrap parser and SQL singleton responsibility for platform selection; retain only a transitional legacy read needed for broker-state backfill. Narrow the generic configuration API so it cannot accept platform input. Validation must finish before broker initialization or reconciliation.

Files:
* `src/TNC.Trading.Platform.Api/Program.cs` - Register context before startup initialization.
* `src/TNC.Trading.Platform.Web/Program.cs` - Register the Web context.
* `src/TNC.Trading.Platform.AppHost/AppHostEnvironmentWiring.cs` - Inject Desktop into API and Web.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Configuration/SqlServer/PlatformConfigurationBootstrapParser.cs` - Remove ongoing platform bootstrap ownership.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Configuration/SqlServer/SqlPlatformConfigurationStore.cs` - Stop committing platform changes.
* `src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs` - Remove platform-changing generic updates.
* `src/TNC.Trading.Platform.Api/appsettings.json` - Remove mutable platform bootstrap default.

Success criteria:
* Local Aspire supplies Desktop explicitly and fresh state displays Desktop independently of SQL values.
* Missing or invalid input prevents traffic and broker work.

Dependencies:
* Step 1.1 completion.

### Step 1.3: Validate immutable context

Add traced tests for valid Desktop, missing and unknown values, prohibition of platform update input, and AppHost injection. Update shell expectations only after the status model changes.

Files:
* `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests` - Context validation.
* `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests` - Startup and endpoint contract.
* `test/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.UnitTests` - Desktop injection.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/MainLayoutTests.cs` - Header regression.

Validation commands:
* `dotnet test test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/TNC.Trading.Platform.Application.UnitTests.csproj --configuration Release`
* `dotnet test test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/TNC.Trading.Platform.Api.UnitTests.csproj --configuration Release`
* `dotnet test test/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.UnitTests/TNC.Trading.Platform.AppHost.UnitTests.csproj --configuration Release`
* `dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj --configuration Release`

## Implementation Phase 2: Add Catalog, Selection, And Defaults Schema

<!-- parallelizable: false -->

### Step 2.1: Create additive catalog ownership

Define catalog identifier, provider, kind, lifecycle, availability, endpoint-profile, concurrency, selection, defaults, and profile contracts. Create EF entities/mappings for `BrokerEnvironments`, `BrokerEnvironmentSelections`, `BrokerEnvironmentDefaults`, and broker-scoped schedule/retry/notification profiles. Enforce unique normalized name in the database. Add nullable catalog-ID foreign keys beside scalar broker fields, configure restrictive behavior for retained history, and retain the account-details parent-child cascade only.

Files:
* `src/TNC.Trading.Platform.Application/Configuration` - Catalog, lifecycle, availability, selection, defaults, and profile contracts.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/Entities` - Catalog, selection, defaults, and profile entities.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/PlatformDbContext.cs` - DbSets, keys, indexes, concurrency, and delete behavior.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/Migrations` - Additive schema migration.

Success criteria:
* Multiple environments of the same kind are distinguished by catalog ID and normalized unique name.
* No catalog relationship cascades into audit/history records.

Dependencies:
* Phase 1 completion.

### Step 2.2: Seed and backfill through migration

Use fixed migration operations, not mutable `HasData` or runtime seeding, to insert defaults version one, `IG Demo`, and complete profile snapshots. Notifications default to disabled. Backfill legacy Demo rows; map legacy Live only to an unavailable `IG Live`. Convert legacy restart state into selected/applied IDs, failing closed if recovery is unsafe. Keep legacy columns until all dependent queries and indexes use catalog IDs.

Files:
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/Migrations/<timestamp>_AddBrokerEnvironmentCatalog.cs` - Schema/data migration.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Startup/PlatformStartupInitializer.cs` - Transitional safety, without overwriting profiles.

Discrepancy references:
* DR-01 in the Planning Log.

Success criteria:
* Fresh state has one active template, one available IG Demo, and complete version-stamped profiles.
* Upgrade preserves records and pending restart state without activating Live.

Dependencies:
* Step 2.1 completion.

### Step 2.3: Validate additive migration

Add SQL integration cases for fresh provisioning, upgrade/backfill, exact defaults, disabled notifications, same-kind isolation, normalized-name uniqueness, and failed duplicate creation with no orphan catalog/profile data.

Files:
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/PlatformSqlServerIntegrationTests.cs` - Fresh/upgrade schema and backfill.
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/AccountPreferencesSqlIntegrationTests.cs` - Scoped profiles and account isolation.

Validation commands:
* `dotnet test test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/TNC.Trading.Platform.Infrastructure.IntegrationTests.csproj --configuration Release`

## Implementation Phase 3: Deliver Catalog Lifecycle And Selection

<!-- parallelizable: false -->

### Step 3.1: Implement command handlers and policies

Create vertical slices for administrator catalog create/update/list, credential save/presence, and selection. Catalog creation accepts provider, kind, unique name/display name, and server-approved endpoint-profile key. Resolve and atomically copy the active defaults version; create no credentials and do not select the record. Derive capability from adapters/platform policy. The credential-save command must resolve the target catalog record and reject retired, unavailable, and authentication-incapable records before accepting or protecting secret material. Selection accepts catalog ID, revision, and warning acknowledgement, permits only available/capable targets, and writes selected ID plus restart-required state. Apply administrator policy to catalog/credentials and operator-or-administrator policy only to selection.

Files:
* `src/TNC.Trading.Platform.Application/Features/BrokerEnvironments` - Commands, queries, validation, policy, transaction, and results.
* `src/TNC.Trading.Platform.Application/Features/BrokerSelection` - Selection command, checks, concurrency, state contracts.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Configuration/SqlServer/SqlPlatformConfigurationStore.cs` - Replace with catalog, selection, profile stores.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Credentials/DataProtection/ProtectedCredentialService.cs` - Catalog-scoped write-only credentials.

Success criteria:
* Only administrators can manage catalog/credentials; operators can select.
* Credential saves for retired, unavailable, or authentication-incapable records are rejected before any protected credential row is written.
* Unavailable Live/Training records are visible but cannot select, retrieve credentials, or invoke adapters.

Dependencies:
* Phase 2 completion.

### Step 3.2: Replace API surface

Register dedicated route groups or endpoint extensions for status, catalog operations, credential writes, and selection. Status returns platform, applied broker, optional pending broker, restart state, availability, and no endpoint/secret. Return structured policy failures, `403` for role denial, and concurrency conflicts. Remove or narrow `PUT /api/platform/configuration` so it cannot mutate environment identity.

Files:
* `src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs` - Retain status only where appropriate.
* `src/TNC.Trading.Platform.Api/Features/BrokerEnvironments` - Catalog and credential endpoints.
* `src/TNC.Trading.Platform.Api/Features/BrokerSelection` - Selection endpoint.
* `src/TNC.Trading.Platform.Api/Program.cs` - Endpoint registration.

Success criteria:
* API projections are secret-free and reject browser-supplied capability/URL claims.

Dependencies:
* Step 3.1 completion.

### Step 3.3: Update operator and administrator UI

Change `PlatformShellContextProvider` and status models to represent applied, selected, pending, and availability state. Keep `PlatformEnvironmentBadge` and `PlatformHeader` display-only and centered. Replace Configuration dropdowns with visible broker records, unavailable reasons, confirmation acknowledgement, applied versus selected state, and restart messaging. Add a role-protected Broker Environments page for catalog, credentials, and retirement entry. Refresh shell status after selection.

Files:
* `src/TNC.Trading.Platform.Web/Components/Layout/PlatformShellContextProvider.cs` - Status refresh.
* `src/TNC.Trading.Platform.Web/Components/Layout/PlatformShellEnvironment.cs` - Applied/pending model.
* `src/TNC.Trading.Platform.Web/Components/Layout/PlatformHeader.razor` - Display-only header.
* `src/TNC.Trading.Platform.Web/Components/Layout/PlatformEnvironmentBadge.razor` - Applied/pending presentation.
* `src/TNC.Trading.Platform.Web/Components/Pages/Configuration/Configuration.razor` - Selection-only Settings.
* `src/TNC.Trading.Platform.Web/Components/Pages/BrokerEnvironments` - Administrator UI.
* `src/TNC.Trading.Platform.Web/PlatformApiClient.cs` - New contracts/client calls.

Success criteria:
* Viewers have no selection control; acknowledgement is required before selection; header refresh reflects pending/applied state.

Dependencies:
* Step 3.2 completion.

### Step 3.4: Validate lifecycle and selection

Cover policy enforcement, normalization, atomic defaults copying, stale selection conflict, acknowledgement, selected-versus-applied behavior, restart application, no-secret projections, role-specific browser visibility, and rejection of credentials for unavailable/authentication-incapable records before any protected row is written.

Files:
* `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests` - Command/policy tests.
* `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests` - Authorization/status/concurrency/projection tests.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests` - Header, Settings, client tests.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests` - Authorized flows.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests` - Browser flow and refresh.

Validation commands:
* `dotnet test test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/TNC.Trading.Platform.Api.IntegrationTests.csproj --configuration Release`
* `dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/TNC.Trading.Platform.Web.FunctionalTests.csproj --configuration Release`
* `dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/TNC.Trading.Platform.Web.E2ETests.csproj --configuration Release`

## Implementation Phase 4: Route Runtime Work By Applied Catalog ID

<!-- parallelizable: false -->

### Step 4.1: Create applied broker runtime context

Introduce a port resolving applied catalog ID and verifying lifecycle/capability per broker operation. Expose only server-controlled endpoint-profile/non-secret metadata. Refactor authentication, Account Details, Account Preferences, reconciliation, and operations to receive it. Move credentials to catalog IDs. Keep Demo as the sole executable provider; Live/Training must fail before credential retrieval or HTTP until adapter support exists.

Files:
* `src/TNC.Trading.Platform.Application/Features` - Replace enum operation inputs with applied context/catalog ID.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgBrokerAuthenticationGateway.cs` - Capability-first resolution.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgAccountDetailsGateway.cs` - Catalog-scoped Demo routing.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig` - Apply same change to Account Preferences gateway.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Credentials/DataProtection/ProtectedCredentialService.cs` - Catalog-ID protection scope.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Startup/PlatformStartupInitializer.cs` - Validate selected-to-applied transition before reconciliation.

Success criteria:
* No adapter uses browser input or enum-only identity as routing authority.
* Unavailable targets cause no credential lookup or network I/O.

Dependencies:
* Phase 3 completion.

### Step 4.2: Migrate dependent data access

Migrate the fourteen scalar broker columns, composite indexes, leases, query filters, and projections to nullable-then-required catalog IDs. Backfill through catalog mappings, preserve platform audit/policy data, and verify counts/foreign keys before a separately reviewed legacy-column removal migration. Preserve historical display names without catalog history cascades.

Files:
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/PlatformDbContext.cs` - Relationships/indexes/query mapping.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/Entities` - Catalog columns/history snapshots.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/Migrations` - Backfill and later removal.

Discrepancy references:
* DR-02 in the Planning Log.

Success criteria:
* Same-kind catalog records cannot share credentials, profiles, account data, runtime state, or leases.

Dependencies:
* Step 4.1 completion.

### Step 4.3: Validate routing and isolation

Use gateway spies and SQL fixtures to prove capability validation precedes credentials/HTTP. Add dual-catalog scenarios for credentials, profiles, Account Details, Account Preferences, runtime/retry state, and operations while retaining Demo regressions.

Files:
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/Integrations/Ig` - No-credential/no-network tests.
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests` - Isolation and foreign-key tests.
* `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests` - Context/capability policy.

Validation commands:
* `dotnet test test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/TNC.Trading.Platform.Infrastructure.UnitTests.csproj --configuration Release`
* `dotnet test test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/TNC.Trading.Platform.Infrastructure.IntegrationTests.csproj --configuration Release`

## Implementation Phase 5: Implement Retire And Purge

<!-- parallelizable: false -->

### Step 5.1: Approve the purge inventory

Review each table with product, retention, and data-protection owners. Confirm purge of protected credentials, scoped profiles, runtime/retry state, proof/current projections, current Account Preferences state, and in-flight operations. Confirm retention of configuration audits, operational events, notification records, desired-state audits, observations, account retrieval history, and child accounts. Resolve whether login snapshots are current-only or retained history. Record the final policy in Wiki before code; do not use cascade deletes.

Files:
* `docs/wiki/runtime-behavior.md` - Retention and force-purge boundary.
* `docs/wiki/architecture.md` - Non-cascading relationship/data classification.

Discrepancy references:
* DR-03 in the Planning Log.

Success criteria:
* Retirement has approved table-specific delete/retain policy.

Dependencies:
* Phase 4 completion.

### Step 5.2: Build server-bound retirement

Add an administrator-only preview that loads under concurrency control, rejects applied/selected records, calculates approved counts, and issues a short-lived one-time token bound to administrator, target, rowversion, and preview payload. Require token, rowversion, and exact normalized typed name. In one transaction re-check guards, purge approved mutable rows, write audit, mark `Retired`, invalidate token, and block later credentials/selection.

Files:
* `src/TNC.Trading.Platform.Application/Features/BrokerEnvironmentRetirement` - Preview, token, command, validators, counts.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework` - Transactional purge/token storage.
* `src/TNC.Trading.Platform.Api/Features/BrokerEnvironments` - Preview/retirement endpoints.
* `src/TNC.Trading.Platform.Web/Components/Pages/BrokerEnvironments` - Preview and typed confirmation.

Success criteria:
* Browser confirmation cannot substitute for server token, rowversion, or exact-name verification.
* Retired records retain evidence but cannot select or receive credentials.

Dependencies:
* Step 5.1 approval.

### Step 5.3: Validate retirement safety

Test role denial, applied/selected denial, stale rowversion, token replay/expiry/binding, typed-name mismatch, purge counts, retained history, audit creation, and inability to resume operations.

Files:
* `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests` - Guard/token policy.
* `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests` - Authorization/concurrency API.
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests` - Purge/retention transaction.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests` - Typed confirmation.

Validation commands:
* `dotnet test test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/TNC.Trading.Platform.Api.IntegrationTests.csproj --configuration Release`
* `dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/TNC.Trading.Platform.Web.E2ETests.csproj --configuration Release`

## Implementation Phase 6: Deploy Schema And Document Runtime Contract

<!-- parallelizable: false -->

### Step 6.1: Separate migration deployment from replicas

Build the EF migration bundle from Infrastructure as the selected deployment artifact. Add `infra/migrations/build-migration-bundle.ps1` to run `dotnet ef migrations bundle` into `artifacts/migrations/`, and `infra/migrations/run-migration-bundle.ps1` to run the bundle with the deployment-only SQL connection. Test/Live release orchestration invokes the run script once under a deployment identity before replicas start, and application replicas disable `MigrateAsync` there. Preserve local Desktop migration only if documented. Add an integration test that builds/runs the bundle against an upgrade fixture and asserts a Test/Live process takes the non-migrating startup path.

Files:
* `src/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.csproj` - EF bundle support when required.
* `infra/migrations/build-migration-bundle.ps1` - Create the versioned bundle in `artifacts/migrations/`.
* `infra/migrations/run-migration-bundle.ps1` - Run the bundle with a deployment-only connection.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Startup/PlatformStartupInitializer.cs` - Environment-specific ownership.
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/MigrationBundleDeploymentTests.cs` - Build/run bundle and non-migrating Test/Live startup coverage.
* `docs/wiki/local-development.md` - Local migration behavior.
* `docs/wiki/runtime-behavior.md` - Test/Live migration rule.

Success criteria:
* Test/Live never rely on every replica to migrate schema.

Validation commands:
* `pwsh infra/migrations/build-migration-bundle.ps1 -Configuration Release`
* `dotnet test test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/TNC.Trading.Platform.Infrastructure.IntegrationTests.csproj --configuration Release --filter FullyQualifiedName~MigrationBundleDeploymentTests`

Dependencies:
* Stable Phase 2 migrations and resolved deployment identity/provisioning.

### Step 6.2: Verify portable deployment inputs

Define/test the contract: container image, fixed `Platform__Environment`, external SQL connection, secret references, health endpoints, and ordinary configuration. Specify one isolated database per platform environment. Document Azure Container Apps as first target but prohibit its runtime APIs from platform/broker decisions. Do not create Kubernetes manifests or Helm charts in scope.

Files:
* `src/TNC.Trading.Platform.AppHost/AppHostEnvironmentWiring.cs` - Local contract example.
* `test/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.UnitTests` - Contract assertion.
* `docs/wiki/architecture.md` - Isolation/portability.
* `docs/wiki/local-development.md` - Desktop contract.

Success criteria:
* Any non-Azure host can provide equivalent inputs unchanged.

Dependencies:
* Step 6.1 completion.

## Implementation Phase 7: Update Wiki And Planning Governance

<!-- parallelizable: true -->

### Step 7.1: Update Wiki source of truth

Update the Wiki as slices land, then reconcile terminology/links before closure. Cover ownership, catalog partitioning/defaults, fail-closed capability, restart selection, lifecycle, secrecy, retention, API roles/conflicts, Desktop topology, and migration ownership. Correct the stale supported-runtime in-memory persistence claim.

Files:
* `docs/wiki/application-overview.md` - Environment concepts and persistence.
* `docs/wiki/architecture.md` - Ownership, defaults, relations, partitioning, portability.
* `docs/wiki/runtime-behavior.md` - Validation, lifecycle, retention, migration.
* `docs/wiki/operator-guide.md` - Settings and administration flows.
* `docs/wiki/api-reference.md` - Routes, policy, conflicts, acknowledgement, secrets.
* `docs/wiki/local-development.md` - Desktop parameter/bootstrap/topology.

Success criteria:
* Documentation matches routes, roles, deployment behavior, and retention policy.

Dependencies:
* Corresponding feature phases; retirement wording waits for Step 5.1.

### Step 7.2: Enforce Wiki assessment in planning assets

Add a scoped planning rule for environment, auth, configuration, accounts, topology, operator workflow, local development, or testing changes. Require relevant Wiki assessment and per-work-item change/no-change decision; lifecycle work must assess retention/audit. Make prompt input and template capture mandatory. Repair stale agent references independently; do not duplicate existing global source-of-truth policy.

Files:
* `.github/instructions/work-packages.instructions.md` - Scoped rule.
* `.github/prompts/generate-delivery-plan.prompt.md` - Wiki input/impact.
* `.github/templates/delivery-plan.template.md` - Assessment section.
* `.github/work-package-workflow-usage.md` - Stale references.

Success criteria:
* Relevant future plans prove Wiki and retention/audit impact was assessed.

Dependencies:
* None; final wording tracks completed behavior.

## Implementation Phase 8: Final Validation

<!-- parallelizable: false -->

### Step 8.1: Run full validation

Run focused phase tests first, then execute:

* `dotnet restore TNC.Trading.Platform.slnx`
* `dotnet build TNC.Trading.Platform.slnx --no-restore --configuration Release`
* `pwsh infra/migrations/build-migration-bundle.ps1 -Configuration Release`
* `dotnet test test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/TNC.Trading.Platform.Infrastructure.IntegrationTests.csproj --configuration Release --no-restore --filter FullyQualifiedName~MigrationBundleDeploymentTests`
* `dotnet test test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/TNC.Trading.Platform.Application.UnitTests.csproj --configuration Release --no-restore`
* `dotnet test test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/TNC.Trading.Platform.Infrastructure.UnitTests.csproj --configuration Release --no-restore`
* `dotnet test test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/TNC.Trading.Platform.Api.UnitTests.csproj --configuration Release --no-restore`
* `dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj --configuration Release --no-restore`
* `dotnet test test/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.UnitTests/TNC.Trading.Platform.AppHost.UnitTests.csproj --configuration Release --no-restore`
* `dotnet test test/TNC.Trading.Platform.Architecture/TNC.Trading.Platform.Architecture.IntegrationTests/TNC.Trading.Platform.Architecture.IntegrationTests.csproj --configuration Release --no-restore`
* `dotnet test test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/TNC.Trading.Platform.Infrastructure.IntegrationTests.csproj --configuration Release --no-restore`
* `dotnet test test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/TNC.Trading.Platform.Api.IntegrationTests.csproj --configuration Release --no-restore`
* `dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/TNC.Trading.Platform.Web.FunctionalTests.csproj --configuration Release --no-restore`
* `dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/TNC.Trading.Platform.Web.E2ETests.csproj --configuration Release --no-restore`

### Step 8.2: Triage and close

Correct only isolated validation/documentation defects. Create new research/plan work for broad migration compatibility, key-ring, purge-policy, or cloud-deployment redesign. Confirm every research acceptance case has test or operational evidence and no deprecated generic environment mutation remains.

## Dependencies

* Docker SQL Server/Keycloak and Playwright browsers for broad suites.
* Approved retirement inventory before Phase 5.
* Deployment database/identity convention before Phase 6.

## Success Criteria

* Implemented platform and broker models match the selected three-owner architecture.
* Catalog ID isolates credentials, profiles, and account/operational data.
* Deployment and Wiki strategy prevents unsafe activation, deletion, and replica migration races.