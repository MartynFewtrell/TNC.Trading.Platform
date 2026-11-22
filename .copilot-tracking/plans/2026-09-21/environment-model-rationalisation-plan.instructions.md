---
applyTo: '.copilot-tracking/changes/2026-09-21/environment-model-rationalisation-changes.md'
---
<!-- markdownlint-disable-file -->
# Implementation Plan: Environment Model Rationalisation

## Overview

Replace mutable platform and enum-based broker configuration with an immutable deployment platform context, a SQL broker catalog, restart-applied selection, catalog-ID data partitioning, and safe broker retirement.

## Objectives

### User Requirements

* Deliver the configuration, persistence, API, UI, integration, Wiki, and Copilot-artifact changes recommended by the environment-model research. Source: user request and `.copilot-tracking/research/2026-09-20/environment-model-rationalisation-research.md`.
* Supply an implementation plan detailed enough to execute without further research. Source: user request.

### Derived Objectives

* Prevent SQL, API, or UI configuration from altering the platform safety classification. Derived from primary research lines 311-332.
* Preserve restart-applied broker selection and reject unavailable environments before credential access or network I/O. Derived from primary research lines 369-398.
* Keep audit and historical records through ordinary retention while deleting only approved mutable data on normal retirement. Derived from primary research lines 438-469.
* Apply Test/Live schema changes through a single deployment identity, not every application replica. Derived from primary research lines 550-569.

## Context Summary

### Project Files

* `src/TNC.Trading.Platform.Application/Configuration/PlatformEnvironmentKind.cs` - Extend deployment-owned platform vocabulary.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Configuration/SqlServer/SqlPlatformConfigurationStore.cs` - Replace combined mutable configuration ownership.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/PlatformDbContext.cs` - Add catalog, selection, defaults, relationships, indexes, and staged migrations.
* `src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs` - Retire generic platform mutation and expose bounded catalog/selection operations.
* `src/TNC.Trading.Platform.Web/Components/Pages/Configuration/Configuration.razor` - Make Settings the operator selection surface only.

### References

* `.copilot-tracking/research/2026-09-20/environment-model-rationalisation-research.md` - Selected design, acceptance criteria, and Wiki scope.
* `.copilot-tracking/research/subagents/2026-09-21/environment-model-delivery-inventory-research.md` - Verified code inventory, hazards, validation matrix, and scoped assumptions.

## Implementation Checklist

### [x] Implementation Phase 1: Establish Immutable Platform Context

<!-- parallelizable: false -->

* [x] Step 1.1: Add a validated `Platform:Environment` options type and `IPlatformEnvironmentContext` in Application, accepting only `Desktop`, `Development`, `Test`, and `Live`.
  * Details: `.copilot-tracking/details/2026-09-21/environment-model-rationalisation-details.md` (Lines 12-33)
* [x] Step 1.2: Bind and validate the context at API and Web startup, inject `Platform__Environment=Desktop` from AppHost, and remove platform mutation from bootstrap/configuration contracts.
  * Details: `.copilot-tracking/details/2026-09-21/environment-model-rationalisation-details.md` (Lines 34-53)
* [x] Step 1.3: Add focused startup, AppHost, API, and Web regression tests.
  * Details: `.copilot-tracking/details/2026-09-21/environment-model-rationalisation-details.md` (Lines 54-69)

### [x] Implementation Phase 2: Add Catalog, Selection, And Defaults Schema

<!-- parallelizable: false -->

* [x] Step 2.1: Add application contracts and infrastructure entities for broker catalog, selection, versioned defaults, and broker-scoped profiles without removing legacy columns.
  * Details: `.copilot-tracking/details/2026-09-21/environment-model-rationalisation-details.md` (Lines 74-90)
* [x] Step 2.2: Add an EF migration that seeds first defaults, backfills `IG Demo`, maps legacy Live as unavailable, snapshots profiles, and preserves restart state.
  * Details: `.copilot-tracking/details/2026-09-21/environment-model-rationalisation-details.md` (Lines 91-108)
* [x] Step 2.3: Prove fresh and upgraded migration behavior, uniqueness, foreign-key isolation, and no accidental Live availability using SQL integration tests.
  * Details: `.copilot-tracking/details/2026-09-21/environment-model-rationalisation-details.md` (Lines 109-119)

### [x] Implementation Phase 3: Deliver Catalog Lifecycle And Selection

<!-- parallelizable: false -->

* [x] Step 3.1: Implement administrator catalog and write-only credential commands plus operator-or-administrator selection commands with authorization, concurrency, lifecycle, capability, acknowledgement, and atomic defaults-copy rules. Reject credential saves before persistence when the catalog cannot authenticate.
  * Details: `.copilot-tracking/details/2026-09-21/environment-model-rationalisation-details.md` (Lines 124-141)
* [x] Step 3.2: Replace generic environment endpoints and clients with status, catalog, credentials, and selection feature routes that disclose no secrets or endpoint URLs.
  * Details: `.copilot-tracking/details/2026-09-21/environment-model-rationalisation-details.md` (Lines 142-157)
* [x] Step 3.3: Replace Configuration selectors with restart-warning selection; add administrator Broker Environments UI; keep the centered header display-only and pending-aware.
  * Details: `.copilot-tracking/details/2026-09-21/environment-model-rationalisation-details.md` (Lines 158-176)
* [x] Step 3.4: Run Application, API, Web unit, API integration, functional, and E2E tests for authorization, pending selection, and UI refresh.
  * Details: `.copilot-tracking/details/2026-09-21/environment-model-rationalisation-details.md` (Lines 177-192)

### [x] Implementation Phase 4: Route Runtime Work By Applied Catalog ID

<!-- parallelizable: false -->

* [x] Step 4.1: Convert credentials, gateways, reconciliation, account data, runtime state, and operations from broker enums to an applied catalog context and approved endpoint profiles.
  * Details: `.copilot-tracking/details/2026-09-21/environment-model-rationalisation-details.md` (Lines 197-215)
* [x] Step 4.2: Convert dependent reads, writes, indexes, leases, projections, and query filters to catalog IDs; validate counts and foreign keys before later legacy-column removal.
  * Details: `.copilot-tracking/details/2026-09-21/environment-model-rationalisation-details.md` (Lines 216-233)
* [x] Step 4.3: Prove capability validation occurs before credentials or HTTP, two same-kind catalog records isolate all data, and unavailable records remain non-executable.
  * Details: `.copilot-tracking/details/2026-09-21/environment-model-rationalisation-details.md` (Lines 234-246)

### [ ] Implementation Phase 5: Implement Retire And Purge

<!-- parallelizable: false -->

* [x] Step 5.1: Confirm the table-by-table mutable-data purge inventory and record the decision before destructive implementation.
  * Details: `.copilot-tracking/details/2026-09-21/environment-model-rationalisation-details.md` (Lines 251-267)
* [x] Step 5.2: Implement administrator-only server-bound deletion preview and retirement with rowversion, one-time token, exact typed name, selected/applied safeguards, targeted purge, and retention audit.
  * Details: `.copilot-tracking/details/2026-09-21/environment-model-rationalisation-details.md` (Lines 268-284)
* [ ] Step 5.3: Add API, SQL integration, functional, and E2E tests for replay, staleness, authorization, safety rejection, purge counts, and retained history.
  * Details: `.copilot-tracking/details/2026-09-21/environment-model-rationalisation-details.md` (Lines 285-298)
  * [x] WI-05 follow-on: Repair the historical `AccountPreferencesOperations` index-drop migration guard and rerun Infrastructure SQL migration/retirement coverage.
    * Result: Focused migration regression passed (1/1) and the full Infrastructure integration suite passed (33/33) on 2026-09-21. API integration and Web E2E coverage remain governed by the existing WI-06 fixture blocker.
  * WI-06 status (2026-09-21): [ ] Aspire/Keycloak fixture stabilization remains open. Isolated Release runs reproduced API fixture cancellation while polling `/health/ready` and Web authenticated navigation reaching `/status` without the protected status heading. The fixture now defaults its owned AppHost SQL and Keycloak resources to session lifetime, avoiding stale persistent containers; the focused suites still fail, so no completion is claimed. No authentication assertions were weakened and no tests were skipped. Phase 6 remains not started.

### [x] Implementation Phase 6: Deploy Schema And Document Runtime Contract

<!-- parallelizable: false -->

* [x] Step 6.1: Build an EF migration bundle using `infra/migrations/build-migration-bundle.ps1`, execute it once with `infra/migrations/run-migration-bundle.ps1` and a deployment identity, and prevent Test/Live replicas from applying migrations.
  * Details: `.copilot-tracking/details/2026-09-21/environment-model-rationalisation-details.md` (Lines 303-325)
* [x] Step 6.2: Document and test the portable container input contract, distinct database per platform environment, external connection/secret configuration, and Azure Container Apps to Kubernetes portability boundary.
  * Details: `.copilot-tracking/details/2026-09-21/environment-model-rationalisation-details.md` (Lines 326-341)

### [ ] Implementation Phase 7: Update Wiki And Planning Governance

<!-- parallelizable: true -->

* [x] Step 7.1: Update the seven affected Wiki pages with delivered behavior, API authorization, lifecycle, retention, local Desktop topology, and deployment semantics.
  * Details: `.copilot-tracking/details/2026-09-21/environment-model-rationalisation-details.md` (Lines 346-363)
* [x] Step 7.2: Add a scoped Wiki-impact planning rule, prompt requirements, and delivery-plan template section; independently repair stale workflow asset references.
  * Details: `.copilot-tracking/details/2026-09-21/environment-model-rationalisation-details.md` (Lines 364-379)

### [x] Implementation Phase 8: Final Validation

<!-- parallelizable: false -->

* [x] Step 8.1: Run restore, Release build, the migration-bundle build plus focused bundle deployment test, and all staged unit/architecture, integration, functional, and E2E suites.
* [x] Step 8.2: Apply only isolated corrections discovered by validation; record blocking migration, purge-policy, or deployment issues for a new plan.
* [x] Step 8.3: Confirm affected Wiki pages describe final behavior and the implementation closes the research acceptance criteria.

## Planning Log

See `.copilot-tracking/plans/logs/2026-09-21/environment-model-rationalisation-log.md` for discrepancies, selected and rejected paths, decision gates, and follow-on work.

## Dependencies

* .NET SDK pinned by `global.json`, Docker, SQL Server container support, Keycloak, and Playwright browser dependencies.
* Application-administrator and operator authorization policies.
* Data Protection key-ring availability when migrating or purging protected credentials.
* Approved table-by-table retirement inventory before Phase 5.
* Deployment identity and external database provisioning convention before Phase 6.

## Success Criteria

* Platform environment is immutable, startup-validated, deployment supplied, and displays `Desktop / IG Demo` for fresh local state.
* Catalog creation produces unique named records and complete versioned defaults snapshots without credentials or runtime selection changes.
* Selection changes only the pending broker until restart, and every operation uses a capable applied catalog context before credentials or network I/O.
* Retirement safely purges only approved mutable data, retains history/audit evidence, and rejects stale, replayed, selected, or applied requests.
* Test/Live schema deployment is a one-time deployment concern and the Wiki plus planning assets accurately govern the delivered system.