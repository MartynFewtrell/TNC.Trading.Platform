---
title: Decision Reconciliation Research
status: complete
ms.date: 2026-09-21
---

## Research Scope

Reconcile `.copilot-tracking/research/2026-09-20/environment-model-rationalisation-research.md` with the approved environment-model decisions:

* Isolate one database per application environment
* Permit catalog creation for all broker/trading environment kinds
* Seed `IG Demo` only as the initial name, with user-created names thereafter
* Prefer Azure Container Apps while preserving future Kubernetes portability
* Scope schedule, retry, and notification profiles to broker environments and initialize them from defaults
* Retain audit and history during normal deletion while purging credentials and mutable data

## Primary Research Reviewed

* `.copilot-tracking/research/2026-09-20/environment-model-rationalisation-research.md`
* Repository evidence cited by that document, especially the configuration,
  persistence, AppHost, and Wiki surfaces named in its Evidence Log

## Exact Contradictions and Anomalies

### Resolved Decisions Still Presented as Open

The primary document's `Decisions Needed Before Delivery` section asks for
approval of database isolation, catalog eligibility, the `IG Demo` seeded
display name, Azure hosting target, broker scoping of profiles, and normal
deletion retention. All six are now approved. Keeping them in that section
misstates the delivery baseline and creates a risk that a plan reopens settled
choices.

### Database Isolation Is Too Narrow

The primary document recommends a separate database for each Test and Live
platform deployment and later states `Use isolated Test/Live databases as the
default.` The approved scope is one database per application environment,
which also covers local/Desktop and future application environments. Test and
Live are examples, not the complete set.

### Catalog Creation Is Incorrectly Gated by Availability

The primary document asks whether the catalog initially permits Demo only and
states that Training and Live records are unavailable until adapters exist. The
availability constraint is correct for selection and broker work, but it must
not prevent an administrator from creating a catalog record of any supported
kind. The approved decision permits creation of all broker/trading environment
kinds. A created Live or Training record may remain unavailable and
non-selectable until capability policy permits it.

### `IG Demo` Is a Seed, Not a Naming Convention

The document correctly proposes one seeded available `IG Demo` record, but its
creation language risks treating the seed name as a prescribed name. The
approved decision is narrower: `IG Demo` is the initial seed only. An
administrator supplies the unique name for every environment they create,
including Demo, Live, and Training kinds.

### Deployment Target Is Missing from the Selected Approach

The primary document leaves Azure hosting as an unresolved choice and only
mentions Azure configuration in a configuration example. The approved target
is Azure Container Apps. The selected approach must make ACA the first
deployment implementation while keeping the application contract portable to
Kubernetes. The domain model, container image, SQL database, secret delivery,
non-secret deployment parameters, and health endpoints must not depend on
ACA-only runtime APIs.

### Profile Scope Is Described but Not Made Decisive

The document says that `BrokerEnvironmentDefaults` should initialize schedule,
retry, and notification values at creation, and its required changes migrate
related settings to catalog IDs. However, the earlier decision list still
frames broker scoping as undecided. The approved model requires each profile
to be scoped to `BrokerEnvironmentId`, created from server-owned defaults, and
read through the applied broker environment. There is no remaining
platform-wide profile fallback after migration.

### Deletion Model Is Aligned, but Requires a Clearer Retention Boundary

The proposed retire-and-credential-purge flow agrees with the approval. The
document should explicitly call this normal deletion, not only retirement, so
the operator contract is unambiguous: normal deletion removes credentials and
mutable per-environment data, preserves audit/history, then retires the
catalog record. A physical history purge remains a separate offline retention
procedure.

## Required Corrections to the Primary Research

1. Replace `Decisions Needed Before Delivery` with `Approved Delivery
   Decisions`, preserving the six approved statements as constraints rather
   than questions.
2. Replace every `Test/Live` database-isolation recommendation with `one
   database per application environment`, including local/Desktop and future
   environments. State that catalog IDs partition data inside one application
   environment database and do not substitute for deployment-database
   isolation.
3. Change catalog rules so creation accepts every defined broker/trading kind.
   Separate `creation permitted` from `available for selection` and `capable
   of broker work`.
4. State that bootstrap seeds one record named `IG Demo`; all later names come
   from the administrator and must be uniquely normalized. Do not reserve the
   seed name as a kind label or required display name.
5. Add Azure Container Apps as the preferred deployment target. Specify a
   portable container deployment contract: immutable `Platform__Environment`,
   external SQL Server, standards-based health endpoints, environment-based
   configuration, and secret references. Keep application behavior independent
   of ACA revisions, Dapr, ingress annotations, and other ACA-only APIs so a
   Kubernetes deployment can provide the same contract later.
6. Make schedule, retry, and notification profiles catalog-ID scoped in the
   schema, API projections, lifecycle commands, and runtime resolution. Seed
   them from the versioned server-owned defaults on creation and purge their
   mutable instances during normal deletion.
7. Rename the deletion contract to `normal deletion (retire and purge)` and
   state the retained set precisely: audit records, event/history records,
   notification history, and historical account records remain subject to the
   existing retention policy. Credentials and mutable settings/current
   projections are removed.
8. Update acceptance tests so they prove creation of all kinds, user-selected
   names, profile-default initialization, per-application-environment database
   isolation, ACA-portable configuration, and retained-history/purged-mutable
   deletion behavior.

## Reconciled Selected Approach

Use three ownership boundaries within each independently provisioned
application environment database:

| Concern | Source of truth | Reconciled behavior |
| --- | --- | --- |
| Application environment | Deployment configuration | Immutable `Platform:Environment`, supplied by local composition or Azure Container Apps and validated at startup |
| Broker environment catalog | SQL Server | Administrators can create any broker/trading kind with their own unique name; the sole bootstrap seed is `IG Demo` |
| Broker selection and profiles | SQL Server | Selection is restart-applied; schedule, retry, and notification profiles are scoped to the catalog ID and initialized from versioned server defaults |

Each application environment receives a separate database. SQL catalog IDs
then isolate broker-specific credentials, mutable configuration, current
projections, and account data within that database. A broker record can be
created before its provider adapter is available. Capability and availability
checks occur only when selecting it or attempting broker work, before any
credential access or network call.

Azure Container Apps is the preferred initial host. The deployable unit stays
portable: a container image consumes the same immutable environment parameter,
SQL connection, secret references, and health contract that a future Kubernetes
platform can supply. No environment safety or broker-routing decision may rely
on an ACA resource name or API.

Normal deletion is an administrator-confirmed retire-and-purge command. It
rejects selected or applied targets, purges credentials and all mutable
catalog-ID-scoped data including schedule/retry/notification profiles, writes
the lifecycle audit, and retains audit/history records under normal retention.

## Remaining Decisions

* Define the authoritative set of broker/trading kinds and provider-to-kind
  compatibility rules. The approval permits all kinds to be created, but does
  not define their final vocabulary or which providers can use each one.
* Define the first Kubernetes target and infrastructure abstraction boundary.
  The portability constraint is sufficient for application design, but it does
  not select Helm, Kustomize, an IaC tool, or a managed Kubernetes service.
* Define database provisioning, naming, and access policy for each application
  environment, including whether local/Desktop uses a persistent developer
  database or an isolated per-run database.
* Define the exact mutable-data inventory for deletion migrations, especially
  records currently represented by enum columns, so purge behavior can be
  verified table by table.

## Research Status

Complete. The approved decisions resolve every decision gate in the primary
research. The remaining items are implementation-design choices, not blockers
to adopting the reconciled model.
