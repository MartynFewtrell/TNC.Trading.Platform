---
title: Authority Switching Defaults Best Practice Research
description: Research findings on broker-environment authority, switching warnings, and default initialization.
ms.date: 2026-09-21
ms.topic: architecture
---

## Research Scope

Evaluate the updated decisions that only administrators may add or remove broker
environments, operators may switch broker environments from Settings with
prominent warnings, and every broker environment must receive
`BrokerEnvironmentDefaults` during initial and later initialization.

## Status

Complete. The new decisions are compatible with the existing role model, but
they require corrections to the current proposal's authority, UI placement,
and initialization wording.

## Key Findings

* Only administrators may create or retire broker environments. The existing
	`PlatformAdministrator` policy is already administrator-only, while
	`PlatformOperator` admits both operators and administrators. Use the former
	for catalog creation, metadata changes, credentials, delete preview, and
	retirement; use the latter for environment selection. Viewers must have no
	selection command or Settings control.
* Selection belongs in Settings, not the header. The header remains an
	always-visible, display-only indicator of the applied broker environment.
	Operators select an available environment from Settings and must see a
	prominent confirmation that the change affects the next start, does not
	change the current process, and can route later broker work to a different
	account or endpoint.
* `BrokerEnvironmentDefaults` must be versioned template data and each
	environment must receive a copied, immutable-at-creation snapshot. Do not
	read an environment's settings dynamically from the default row after its
	creation, or later default edits will silently alter existing environments.
* A schema migration can create the defaults table and insert the first fixed
	template, backfill `IG Demo`, and create its scoped settings. It cannot
	initialize environments created after deployment. The administrator create
	command must atomically copy the active template into new schedule, retry,
	and notification rows in the same transaction as the catalog row.

## Exact Revisions To The Current Proposal

### Authority

Replace the `Catalog command`, `Credential command`, `Delete preview`, and
`Retire command` rows in the proposed API surface with administrator-only
operations. Retain administrator-only credentials because credential creation
is part of making a broker environment usable and the current proposal already
assigns it that boundary.

Replace this text in the `Selection command` row:

```text
Administrator-only catalog ID plus concurrency token
```

with:

```text
Operator-or-administrator catalog ID, concurrency token, and explicit warning
acknowledgement. Viewers are forbidden. The handler verifies availability,
capability, lifecycle, and restart safety server-side.
```

Replace the `User experience` statement that permits a compact header menu
with the following:

```text
The centered header is display-only for every role and shows the immutable
platform value with the applied broker name. In Settings, an operator or
administrator can select an available broker environment. Before confirmation,
Settings prominently identifies the currently applied environment and selected
target and warns that the new selection applies only after a successful restart,
may change the account and endpoint used by later broker work, and leaves the
current process unchanged. Viewers see the applied indicator but have neither
the Settings selector nor a selection API permission.
```

Replace the `Required feature changes` API/UI bullet with:

```text
API: use administrator authorization for catalog, metadata, credentials, delete
preview, and retirement; use the operator policy for selection. UI: add the
selector and warning confirmation to Settings, render the centered header as
applied/pending-aware display-only state, hide selector actions for viewers,
and add a separate administrator Broker Environments page.
```

Replace `Users can create any defined broker/trading kind` in the acceptance
tests with `Administrators can create any defined broker/trading kind`. Add
authorization tests that prove an operator receives `403` for catalog create
and retirement, a viewer receives `403` for selection, and an operator can
select an available environment but cannot bypass availability or capability
checks.

### Defaults And Initialization

Replace the paragraph beginning `There is no supported SQL loading script` with
the following:

```text
Introduce a versioned `BrokerEnvironmentDefaults` template and use it in two
separate paths. The EF migration creates the template table, loads one fixed,
deterministic initial template, creates/backfills `IG Demo`, and inserts the
matching scoped schedule, retry, and notification snapshot. The administrator
create handler reads the active template and copies it into the new broker
environment's scoped settings in the same database transaction as the catalog
insert. Each copied row records the template version and source, and later
template changes affect only later creations unless an explicit, audited apply-
defaults command is introduced. Neither path creates credentials or changes the
selected or applied environment.
```

Amend Migration Strategy step 2 to create the defaults template table before
the catalog and scoped settings depend on it. Replace steps 3 and 5 with:

```text
3. In the migration `Up`, insert the first fixed default-template version,
create/backfill `IG Demo`, and copy the template into its broker-scoped
schedule, retry, and notification rows. Use deterministic IDs and fixed
values, not configuration values or the current time.
5. The administrator create handler initializes every subsequent catalog record
by atomically copying the active default-template version into its scoped
settings. Enforce the unique normalized name in the database and return the
recorded default-template version in the create response.
```

Add these acceptance tests:

* A newly provisioned database contains exactly one active default-template
	version, `IG Demo`, and the scoped defaults copied from that version.
* An existing database is backfilled without overwriting any existing mutable
	profile or creating duplicate catalog/settings records.
* Every successful administrator creation produces exactly one complete,
	version-stamped set of defaults, with no credentials and no selection change.
* A concurrent duplicate create leaves neither an orphan catalog row nor a
	partial default profile.
* Changing the active template affects only a later environment creation; it
	does not silently alter `IG Demo` or any existing environment.

## Critical Caveats

* The phrase "EF migration must load BrokerEnvironmentDefaults" needs a
	deployment definition. `UseSeeding` and `UseAsyncSeeding` run with EF
	`Migrate`/`MigrateAsync`, tooling, and migration bundles, but not when a
	generated SQL script is executed externally. If DBA-reviewed SQL scripts are
	a supported production path, the migration `Up` must contain the fixed
	template/backfill data operations itself. Do not make this requirement depend
	only on `UseSeeding`.
* The current `PlatformConfigurationBootstrapParser` reads deployment
	configuration and derives a notification provider from SMTP configuration.
	That is not yet a server-owned, versioned default profile. The new template
	must specify deterministic fields and must not seed environment-specific
	recipients, secret values, or endpoint URLs from app settings. Decide whether
	a new environment starts with notifications disabled or with a centrally
	approved non-secret recipient policy.
* Do not use EF model-managed data (`HasData`) for operator-changeable templates
	or per-environment profile instances. Microsoft describes it as suitable for
	static, deterministic values; migration-driven updates can overwrite data
	changed outside migrations. A fixed initial template row in a reviewed
	migration is acceptable, but mutable template administration and all copied
	profile instances require normal application persistence.
* Existing startup code runs `MigrateAsync` from every API startup. EF Core 9+
	provides migration locking, but Microsoft still prefers a separate migration
	deployment step where high availability, least-privilege credentials, or
	review is important. Container deployments should run a migration bundle or
	one-shot migration job, not migrations in every replica.
* Keep creation and default copying atomic. EF Core's `SaveChanges` is
	transactional for relational providers, but a multi-save workflow requires
	an explicit transaction or a single unit of work. Do not return success until
	the catalog row and all required settings rows are committed.

## Recommended Approach

1. Preserve the existing `Viewer`, `Operator`, and `Administrator` policies.
	 Map catalog lifecycle and credentials to `PlatformAdministrator`; map the
	 selection command and Settings selector to `PlatformOperator`.
2. Replace the header selector with a display-only applied/pending indicator.
	 Put selection in Settings under an operator policy, with a server-validated
	 acknowledgement value and a confirmation dialog that repeats the selected
	 target, restart requirement, and account/endpoint impact.
3. Add a `BrokerEnvironmentDefaults` aggregate containing an immutable version,
	 deterministic profile values, lifecycle state, and audit fields. Add
	 broker-scoped profile rows that record `DefaultsVersionApplied`.
4. Make the migration establish the first template, `IG Demo`, and its profile
	 snapshot using reviewable fixed data/backfill operations. Make the catalog
	 create use case copy the active template atomically for every later broker
	 environment. Version changes create a new template version rather than
	 mutating existing snapshots.
5. Adopt a migration bundle or one-shot migration job for containerized shared
	 environments. Keep `UseSeeding` and `UseAsyncSeeding` idempotent only if the
	 deployment mechanism standardizes on EF-run migrations; otherwise retain
	 the critical first-template data operations in the migration itself.

## Evidence And Citations

* `src/TNC.Trading.Platform.Api/Authentication/PlatformApiAuthenticationServiceCollectionExtensions.cs`
	defines the distinct viewer, operator-or-administrator, and
	administrator-only policies.
* `src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs`
	currently applies the operator policy to configuration mutation and already
	has an administrator-only administration endpoint.
* `src/TNC.Trading.Platform.Web/Components/Pages/Configuration/Configuration.razor`
	is the existing operator-only configuration surface; it is the appropriate
	Settings placement after its environment controls are replaced.
* `src/TNC.Trading.Platform.Web/Components/Layout/PlatformHeader.razor`
	currently renders the environment indicator as display-only, which matches
	the revised requirement.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Configuration/SqlServer/PlatformConfigurationBootstrapParser.cs`
	provides current configuration-derived singleton defaults, proving that a
	separate versioned template is required.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Startup/PlatformStartupInitializer.cs`
	runs `MigrateAsync` before current bootstrap initialization.
* [Data Seeding - EF Core](https://learn.microsoft.com/en-us/ef/core/modeling/data-seeding)
	recommends `UseSeeding` and `UseAsyncSeeding` for general initial data,
	documents migration locking, states that SQL-script deployment does not run
	these delegates, and limits model-managed data to fixed deterministic data.
* [Applying Migrations - EF Core](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying)
	recommends a migration bundle for automated deployment, a reviewed SQL
	script where needed, a separate deployment identity, and a one-shot
	migration job rather than every container replica running migrations.
* [Managing Migrations - EF Core](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/managing)
	recommends `InsertData` for fixed known values and migration SQL for
	transformations based on existing rows. It also directs authors not to use
	the current `DbContext` or mutable entity CLR types for historical data
	migrations.
* [Transactions - EF Core](https://learn.microsoft.com/en-us/ef/core/saving/transactions)
	states that relational `SaveChanges` is transactional and documents explicit
	transaction control for multi-operation workflows.
* [Role-based authorization in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/security/authorization/roles?view=aspnetcore-10.0)
	documents role requirements and their application to Razor components and
	policies.
* [Policy-based authorization in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/security/authorization/policies?view=aspnetcore-10.0)
	documents named policy application to endpoints and Razor components, and
	that all requirements in a policy must succeed.

## Remaining Questions

* Does "only application administrators may add/remove" also prohibit
	administrators from delegating metadata edits or credential replacement to
	operators, or should those remain administrator-only as recommended here?
* Is "Settings" the existing `/configuration` page to be renamed, or a new
	Settings route with configuration as a child area?
* What precise default notification behavior is safe for newly created broker
	environments: disabled, recorded-only, or a centrally approved recipient?
* Which deployment mechanisms are mandatory for Test and Live: migration bundle
	only, DBA-reviewed SQL scripts, or both? The answer determines whether
	`UseSeeding` is supplementary or can be part of the required initialization
	path.
