---
title: Target Architecture Alternatives Research
description: Evidence-based Phase 2 comparison and recommendation for the TNC.Trading.Platform target architecture
author: GitHub Copilot
ms.date: 2026-07-26
ms.topic: concept
---

## Research scope

* Evaluate retaining and tightening the current layered architecture with
  Clean, Hexagonal, and Vertical Slice influences.
* Evaluate evolution toward a business-module modular monolith.
* Evaluate additional services, microservices, or worker processes.
* Account for the single-owner part-time context, delivered control-plane
  scope, future safety-sensitive trading, tests, operational burden,
  transaction boundaries, coordinator coupling, and scaling constraints.
* Score the six requested current architecture classifications and the current
  architecture overall.
* Produce a prioritized improvement backlog that separates immediate defects
  from structural evolution.

## Status

Complete.

## Evidence inputs

* `.copilot-tracking/research/subagents/2026-07-26/solution-dependencies-research.md`
* `.copilot-tracking/research/subagents/2026-07-26/implementation-boundaries-research.md`
* `.copilot-tracking/research/subagents/2026-07-26/runtime-quality-research.md`

## Working recommendation

The current evidence favors an incremental business-module modular monolith as
the target direction, while retaining the existing process topology and
Clean/Hexagonal dependency direction. This hypothesis would be weakened if
future workloads require independent scaling now, or if the current domain has
too little cohesion to define stable module ownership without speculative
abstraction.

The evidence did not weaken the hypothesis. Independent workload scaling is a
future concern, not a delivered requirement, and the existing control-plane,
broker-authentication, operational-history, and notification responsibilities
already provide practical starting boundaries. The selected approach is
therefore **an evolutionary business-module modular monolith, preserving the
current Web/API process boundary and reserving worker extraction for measured
runtime needs**.

This is a target direction rather than a claim that the current solution is a
modular monolith. Immediate correctness and operational defects must be fixed
before structural reorganization.

## Decision summary

Retain these current strengths:

* One operator-facing Blazor Server process and one backend deployment unit
* Aspire orchestration, SQL Server, Keycloak, Mailpit, service discovery, and
  shared telemetry defaults
* Application-owned output ports and Infrastructure adapters
* Thin HTTP mapping and operation-specific request, response, and handler
  contracts
* Closed-box AppHost, Keycloak, functional, and browser tests

Change the code ownership model incrementally:

* Organize backend behavior by business capability rather than extending the
  shared `PlatformStateCoordinator` and technical-layer folders
* Give each module explicit contracts, use cases, policy, persistence ownership,
  and composition registration while retaining one deployable API initially
* Make transaction and idempotency boundaries explicit for every state-changing
  workflow
* Keep cross-module calls in process until asynchronous behavior or independent
  workload isolation provides a measured benefit
* Extract a worker process before adding API replicas or sustained market-data,
  strategy, or execution loops; do not split each module into a network service
  by default

The recommendation fits the documented single owner and part-time constraint
(`docs/business-requirements.md:49-62,112-119`) while preparing for future market
data, strategy, risk, order, traceability, and recovery requirements
(`docs/business-requirements.md:35-47,69-98`).

## Current architecture classification confidence

These scores measure how strongly the implemented architecture supports each
classification, where 0 means no material evidence and 10 means a close,
consistent implementation of the style. They are classification-fit scores,
not probabilities and not quality scores. A half-point precision reflects
representative rather than exhaustive symbol inspection.

| Classification       | Score | Confidence rationale |
|----------------------|------:|----------------------|
| Layered              |   8.5 | Product projects clearly separate hosts, Application, Infrastructure, and shared hosting concerns. The project graph is acyclic and points Infrastructure inward (`src/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.csproj:11-13`; `src/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.csproj:3-7`). API startup reaches concrete Infrastructure behavior and Application also carries shared Web auth concerns, so the layers are not strict (`src/TNC.Trading.Platform.Api/Program.cs:23-55`; `src/TNC.Trading.Platform.Application/Authentication/PlatformAuthorizationPolicyRegistration.cs:1-35`). |
| Clean                |   7.0 | Application has no source-project dependency and owns ports implemented by Infrastructure (`src/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.csproj:1-13`; `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/PlatformInfrastructureServiceCollectionExtensions.cs:46-64`). ASP.NET Core coupling, friend assemblies, concrete EF startup access, and no independent domain center prevent a textbook classification. |
| Onion                |   4.5 | Policy and ports sit inward of EF, HTTP, email, and IG adapters, but there are no explicit domain, application-service, and infrastructure rings. The absence of an independent domain model and the Application framework reference weaken this label (`src/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.csproj:8-10`). |
| Hexagonal            |   7.0 | Configuration, state, events, notifications, credentials, and IG access are represented by inward-owned ports with outward adapters (`src/TNC.Trading.Platform.Application/Services/IPlatformConfigurationStore.cs:5-16`; `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/PlatformInfrastructureServiceCollectionExtensions.cs:46-64`). The API audit path assembles a use case from output ports and some ports are broad stores, so adapter discipline is inconsistent (`src/TNC.Trading.Platform.Api/Features/Platform/RecordAuthAuditEventEndpointHandler.cs:11-55`). |
| Vertical Slice       |   6.0 | Operation-named API/Application folders and request-response-handler contracts are real, and most endpoints dispatch one handler (`src/TNC.Trading.Platform.Application/Features/GetPlatformStatus/GetPlatformStatusHandler.cs:1-12`; `src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs:57-97`). Most behavior then converges on shared services and a 13-dependency coordinator, while audit bypasses the pattern (`src/TNC.Trading.Platform.Application/Services/PlatformStateCoordinator.cs:9-32`; `src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs:105-122`). |
| Modular Monolith     |   2.5 | The backend is one main deployable and internal visibility limits public surface area, but projects are technical layers rather than business modules. One context and one coordinator span configuration, auth, runtime state, retry, events, snapshots, and notifications (`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/PlatformDbContext.cs:5-114`; `src/TNC.Trading.Platform.Application/Services/PlatformStateCoordinator.cs:9-32`). No module contracts, local composition roots, or enforced module dependencies were found. |

The concise current classification is **a Clean/Hexagonal-inspired layered
monolith with vertical-slice entry points, plus separate Web and Aspire host
processes**. Calling it Onion or Modular Monolith without qualification would
overstate the evidence.

## Overall current architecture score

The overall current architecture score is **6.3 out of 10**. This score rates
fitness for the delivered control-plane foundation and credible evolution
toward safety-sensitive trading. It does not average the six style-fit scores,
because conformity to more architecture labels is not inherently better.

| Criterion                       | Weight | Score | Weighted result | Rationale |
|---------------------------------|-------:|------:|----------------:|-----------|
| Dependency and boundary integrity |    15% |   8.0 |            1.20 | Acyclic project graph, inward-owned ports, and HTTP separation are strong; host and framework leakage remain. |
| Use-case and module cohesion       |    15% |   5.5 |            0.83 | Thin entry points are offset by the shared coordinator, pass-through service, and API-owned audit orchestration. |
| Correctness and safety semantics   |    20% |   5.5 |            1.10 | Demo/live safeguards, authorization, secret protection, audit, and deterministic state policy are valuable. The likely proof API-key defect and destructive development startup are material. |
| Transactions and recoverability    |    15% |   5.0 |            0.75 | Some configuration, credential, and audit writes share one EF commit, but multi-store coordinator work partially commits and controlled migrations or production recovery are absent. |
| Testability and verification       |    15% |   7.5 |            1.13 | Ports, deterministic collaborators, real Keycloak integration, AppHost tests, functional tests, and one E2E smoke provide a strong foundation. Fake construction masks one real adapter mismatch and historical mutation sensitivity is low. |
| Operational readiness              |    10% |   5.5 |            0.55 | Aspire and OpenTelemetry are strong locally. Dependency readiness, deployment, key-ring, migration, backup, alert, and multi-instance controls are not evidenced. |
| Team fit and evolutionary capacity |    10% |   7.5 |            0.75 | One deployment unit limits operational burden and current abstractions support nearby features. Six production and eight test projects are already substantial for one part-time owner, and future trading should not expand the coordinator unchanged. |
| Total                              |   100% |       |        **6.30** | Sound control-plane foundation with specific defects and known structural limits. |

Score interpretation:

* 0.0 to 2.9: unsuitable foundation or architecture largely absent
* 3.0 to 4.9: material redesign required before dependable growth
* 5.0 to 6.9: workable foundation with significant corrective work
* 7.0 to 8.4: strong fit with contained, understood gaps
* 8.5 to 10.0: unusually complete and validated for the assessed scope

The score is deliberately below 7 because safety-sensitive growth requires
transaction, concurrency, migration, and operational guarantees that the
current design does not yet provide. It is above 6 because dependency direction,
security foundations, deterministic policy, and test topology provide credible
assets to evolve rather than replace.

## Architecture alternatives

### Evaluation criteria

Options are scored from 0 to 10 against the following decision weights. The
weights favor safety and sustainable ownership over theoretical distribution.

| Criterion                         | Weight | Decision concern |
|-----------------------------------|-------:|------------------|
| Single-owner operational fit      |    20% | Can one part-time owner build, run, diagnose, and recover it? |
| Fit to delivered scope            |    15% | Does it match a control plane and Demo broker-auth foundation rather than an imagined full platform? |
| Safety, consistency, and recovery |    20% | Can transaction, idempotency, ordering, and recovery rules be made explicit? |
| Business cohesion and evolvability |   15% | Can market data, strategy, risk, execution, and reconciliation evolve without one coordinator becoming the system? |
| Scaling and workload isolation    |    10% | Can continuous and high-throughput workloads scale or fail independently when needed? |
| Migration cost and delivery risk  |    10% | Can the target be reached incrementally without a rewrite? |
| Test continuity                   |    10% | Can current deterministic and closed-box tests remain useful? |

### Option 1: Tighten the current layered architecture

This option keeps the technical-layer project model and improves vertical-slice
consistency, coordinator decomposition, host isolation, transactions, tests,
and operations.

Benefits:

* Lowest structural migration cost and smallest near-term cognitive burden
* Preserves all current tests and project references with limited movement
* Appropriate for the delivered control plane and one-instance local topology
* Allows immediate defects to be corrected without waiting for a target-model
  refactor

Costs and limits:

* Technical layers remain the primary ownership unit, so future business
  capabilities still share Application and Infrastructure projects
* Coordinator decomposition can produce more technical services without
  establishing business ownership
* One EF model and broad store ports continue to encourage cross-feature
  coupling
* It postpones rather than answers where market data, strategy, risk,
  execution, and reconciliation belong

Assessment: viable as a near-term stabilization step, but insufficient as the
long-term architecture on its own.

### Option 2: Evolve to a business-module modular monolith

This option retains one backend deployment unit while introducing explicit
business modules. Each module owns its use cases, policy, ports, persistence
mapping, tests, and public contracts. Cross-module collaboration remains local
and explicit, with asynchronous messages introduced only where ordering,
recovery, or workload decoupling requires them.

Practical initial modules should follow delivered behavior rather than the
entire future roadmap:

* Control Plane: operator configuration, run controls, environment safeguards,
  and status projection
* Broker Connectivity: IG credentials, session lifecycle, retry policy, and
  read-only broker proof data
* Operations: audit events, notifications, retention, and operational history

Future modules become concrete only when their requirements and invariants are
implemented: Market Data, Strategy Runtime, Risk, Execution, and Reconciliation.
Risk must remain an explicit precondition at the execution boundary rather than
an optional downstream subscriber.

Benefits:

* Establishes business ownership before safety-sensitive trading multiplies
  invariants and failure modes
* Retains one deployment, one local debugging surface, and one operational unit
  for the single owner
* Supports local ACID transactions where one module owns a change while making
  cross-module consistency choices visible
* Preserves current ports, adapters, handler contracts, and most tests as
  ingredients within modules
* Creates credible extraction boundaries if a workload later needs a worker or
  service

Costs and limits:

* Requires controlled movement of code and tests, explicit dependency rules,
  and removal of shared-service shortcuts
* A poorly executed refactor could create folder-only modules while retaining
  the same coordinator and context coupling
* Module-local persistence ownership must not be confused with immediately
  assigning each module a separate database
* In-process modules do not independently scale; that is intentional until
  evidence supports extraction

Assessment: best balance of safety, evolution, migration cost, and part-time
operability.

### Option 3: Add services, microservices, or worker processes

This option ranges from one additional background worker to independently
deployed services with separate data ownership and network contracts. These
variants should not be treated as equivalent.

A worker process is a plausible later extraction for the one-second supervisor,
market-data subscriptions, strategy evaluation, or order reconciliation. It
can isolate lifecycle and scaling from HTTP without forcing every business
boundary across a network. Before extraction, work claiming, leases or a
single-active-owner rule, idempotent commands, durable checkpoints, and an
outbox or equivalent durable handoff are required.

Multiple microservices are not justified now. They would add deployment,
versioning, authentication, telemetry correlation, retries, message delivery,
data ownership, backup, and incident-recovery work before the corresponding
independent scaling or team-autonomy need exists. Distributed transactions and
eventual consistency would make current partial-commit concerns harder, not
automatically safer.

Benefits:

* Strong process isolation and independent scaling for proven heterogeneous
  workloads
* A worker can prevent each HTTP replica from running the supervisor
* Service boundaries can isolate failures and release cadence when modules and
  operational capability are mature

Costs and limits:

* Highest operational burden for one part-time owner
* Requires production infrastructure and delivery capabilities not currently
  evidenced: deployment manifests, durable messaging, secret management,
  dashboards, alerts, backups, rollback, and disaster recovery
* Replaces in-process and database transactions with distributed consistency
  problems
* Multiplies integration-test environments and failure modes
* Current boundaries are not mature enough to guarantee a clean extraction

Assessment: reject broad microservice decomposition now. Treat a worker as a
conditional future deployment of a mature module, not as the current target
architecture.

### Weighted comparison

| Criterion                         | Weight | Tightened layers | Modular monolith | Services or workers |
|-----------------------------------|-------:|-----------------:|-------------------:|--------------------:|
| Single-owner operational fit      |    20% |              9.0 |                8.0 |                 3.5 |
| Fit to delivered scope            |    15% |              9.0 |                8.5 |                 4.0 |
| Safety, consistency, and recovery |    20% |              6.5 |                8.5 |                 6.5 |
| Business cohesion and evolvability |   15% |              6.0 |                9.0 |                 8.5 |
| Scaling and workload isolation    |    10% |              4.0 |                6.5 |                 9.0 |
| Migration cost and delivery risk  |    10% |              9.5 |                7.0 |                 3.5 |
| Test continuity                   |    10% |              9.0 |                8.0 |                 5.0 |
| Weighted total                    |   100% |         **7.60** |           **8.08** |            **5.63** |

Option 1 scores well because it is cheap and appropriate today. Option 2 wins
because it adds durable business ownership and clearer consistency boundaries
without accepting distributed-system costs. Option 3's scaling advantage is
real but premature. The worker variant may be selected independently later
when a trigger is met.

## Selected target architecture

Adopt option 2 through four controlled stages.

### Stage 0: Stabilize current behavior

Correct the IG proof credential path, remove destructive database recreation,
define configuration-update outcome semantics, add controlled schema migration,
and establish an explicit single-supervisor rule. Structural movement before
these fixes would obscure whether behavior changed because of the refactor or
because known defects remain.

### Stage 1: Establish module ownership in process

Define module contracts and dependency rules around delivered capabilities.
Move one end-to-end use case at a time, including handler, policy, port,
adapter, mapping, and tests. Keep the current API host as the composition root
and avoid creating a project per small feature. A module may initially be a
namespace and folder boundary backed by architecture tests; separate assemblies
are warranted only when they improve enforcement.

The first useful extraction is Broker Connectivity from the shared coordinator.
Its session lifecycle, retry cycle, IG adapter, proof enrichment, and broker
state form a cohesive operational capability. Control Plane can then consume a
status projection and issue explicit commands rather than causing a tick from a
query.

### Stage 2: Make data and workflow guarantees explicit

Assign each table and write use case to one module owner, even while one SQL
database and physical `DbContext` remain. Introduce explicit transaction
coordination for same-module writes. For cross-module workflows, define whether
the contract is synchronous and atomic, asynchronous and eventually consistent,
or compensating. Add durable outbox/inbox behavior only for workflows that
cross an asynchronous boundary; do not introduce messaging as decoration.

Configuration update should return a truthful durable outcome. Either commit
configuration and reconciliation atomically where feasible, or commit the
desired configuration and represent reconciliation as a durable pending/failed
state that can be retried idempotently. Returning a generic failure after the
configuration has committed is not an acceptable long-term contract
(`src/TNC.Trading.Platform.Application/Features/UpdatePlatformConfiguration/UpdatePlatformConfigurationHandler.cs:9-13`).

### Stage 3: Extract a worker only on evidence

Extract a module into a worker when at least one trigger is observed:

* The API must scale horizontally while exactly one supervisor or broker session
  owner must remain active
* Market-data or strategy processing has materially different CPU, memory,
  latency, connection, or restart characteristics from HTTP
* A continuous workload causes API deployment or failure coupling that violates
  an explicit availability target
* Load measurements show the shared process cannot meet both control-plane and
  trading latency objectives

Worker extraction prerequisites are durable work ownership, idempotency keys,
concurrency control, restart checkpoints, bounded retries, poison-work handling,
trace correlation, health semantics, and closed-box recovery tests. A broader
microservice split requires additional evidence of independent scaling,
security, release cadence, or data ownership that outweighs its operational
cost.

## Transaction, coupling, and scaling implications

The current single database is an asset for controlled evolution, not proof of
poor architecture. Configuration, protected credential changes, and audit
creation already share one EF commit in the configuration adapter
(`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/SqlPlatformConfigurationStore.cs:45-115`; `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/ProtectedCredentialService.cs:27-45,64-91`). Preserve local ACID behavior when one module owns the invariant.

The problem is implicit commit ownership. Event-store and other adapter methods
commit individually, so coordinator workflows become incrementally durable
(`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/EfPlatformEventStore.cs:38-56`). Module use cases should own commit semantics; repositories should not silently decide the workflow boundary.

The coordinator should not be split mechanically into several services with the
same dependency graph. Extract cohesive decisions and workflows behind module
contracts, then remove calls that make reads mutate state. Status currently
ticks before projecting data (`src/TNC.Trading.Platform.Application/Services/PlatformStateCoordinator.cs:34-77`), coupling read latency and availability to persistence and IG behavior.

Horizontal API scaling is currently unsafe. Every API instance would run the
one-second supervisor (`src/TNC.Trading.Platform.Application/Services/PlatformAuthSupervisor.cs:7-43`; `src/TNC.Trading.Platform.Application/Services/PlatformAuthSupervisorRunner.cs:11-50`), while runtime state has no evidenced distributed lease or concurrency token and proof data is process-local. Until Stage 3 prerequisites exist, document and enforce one API/supervisor instance rather than implying scale-out support.

## Prioritized improvement backlog

Effort estimates assume one developer familiar with the solution. Urgency uses
Now, Before durable shared use, Before trading execution, or Trigger-based.

### Immediate correctness and operational defects

| Priority | Improvement | Benefit | Effort | Urgency | Architectural rationale and evidence |
|---------:|-------------|---------|--------|---------|--------------------------------------|
| P0 | Correct the IG proof API-key contract and add a test that composes the real auth adapter response with proof enrichment. | Restores delivered account and position proof behavior and prevents fake construction from masking adapter-contract drift. | S | Now | Enrichment reads `X-IG-API-KEY` from response headers, but authentication does not add the request key; the fake does (`src/TNC.Trading.Platform.Application/Services/PlatformIgProofDataEnricher.cs:12-18`; `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/Ig/IgSessionClient.cs:48-64`; `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/IgProofDataTests.cs:300-309`). |
| P0 | Remove `EnsureDeletedAsync` from normal Development startup and prove restart persistence. | Prevents unexpected local data loss and makes persistent SQL resources truthful. | S | Now | API startup deletes SQL during Development despite documented persistence (`src/TNC.Trading.Platform.Api/Program.cs:24-41`; `docs/wiki/local-development.md:180-186`). |
| P0 | Define and implement truthful configuration-update and reconciliation outcome semantics. | Prevents retries against an ambiguously partially committed command and gives operators a recoverable state. | M | Now | Configuration commits before coordinator reconciliation, whose stores may commit separately (`src/TNC.Trading.Platform.Application/Features/UpdatePlatformConfiguration/UpdatePlatformConfigurationHandler.cs:9-13`; `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/EfPlatformEventStore.cs:38-56`). |
| P1 | Introduce EF migrations and a tested startup migration policy. | Enables controlled, reviewable schema evolution and rollback planning. | M | Before durable shared use | `PlatformDbContext` spans durable operational records, but startup uses `EnsureCreatedAsync` and no migration files were found (`src/TNC.Trading.Platform.Api/Program.cs:24-41`; `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/PlatformDbContext.cs:5-114`). |
| P1 | Enforce and document one active supervisor, then add concurrency control before any second instance. | Prevents duplicate IG calls, events, notifications, and state overwrites. | M | Before scale-out | Every API process hosts the loop and no distributed ownership is evidenced (`src/TNC.Trading.Platform.Application/Services/PlatformAuthSupervisor.cs:7-43`; `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/EfPlatformRuntimeStateStore.cs:8-73`). |
| P1 | Add SQL and identity-provider readiness checks with dependency-specific semantics. | Distinguishes a responsive process from one able to serve protected control-plane operations. | S | Before durable shared use | Shared defaults register only self liveness; readiness has no reviewed SQL or Keycloak check (`src/TNC.Trading.Platform.ServiceDefaults/Extensions.cs:111-156`). |
| P1 | Configure persistent, protected Data Protection keys and test restart/replacement behavior. | Keeps stored broker credentials decryptable across host replacement and future multiple instances. | M | Before durable shared use | Credentials use Data Protection, but no external key-ring persistence was found (`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/ProtectedCredentialService.cs:8-67`). |
| P1 | Audit and remediate current NuGet vulnerability and version-skew warnings. | Reduces known dependency risk and establishes a clean package baseline before structural change. | S-M | Now | The verified solution build succeeded with vulnerability warnings and 73 total warnings; Aspire and telemetry versions are also skewed across projects. |
| P2 | Reconcile wiki claims about real IG connectivity and development durability. | Prevents architecture and operating decisions from using contradictory behavior descriptions. | S | Now | `docs/wiki/application-overview.md:29-55` describes delivered IG integration while lines 95-100 and 143-154 deny it; local durability guidance conflicts with startup deletion. |

### Structural evolution

| Priority | Improvement | Benefit | Effort | Urgency | Architectural rationale and evidence |
|---------:|-------------|---------|--------|---------|--------------------------------------|
| P1 | Separate read projections from state-changing ticks and commands. | Restores predictable CQRS semantics, lowers read latency coupling, and simplifies status availability. | M | Before trading execution | Status reads currently invoke `TickAsync` and can persist or call external systems (`src/TNC.Trading.Platform.Application/Services/PlatformStateCoordinator.cs:34-77`). |
| P1 | Define the Control Plane, Broker Connectivity, and Operations module contracts and ownership map. | Creates practical business boundaries for delivered behavior and blocks further coordinator growth. | M | Before major new capabilities | One coordinator and one context currently span these concerns (`src/TNC.Trading.Platform.Application/Services/PlatformStateCoordinator.cs:9-32`; `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/PlatformDbContext.cs:5-114`). |
| P1 | Move auth audit orchestration into an Application/module use case. | Makes transport thin and aligns authorization/audit behavior with one testable command boundary. | S | During first module extraction | API currently loads configuration, reads claims, constructs the event, and persists through a port (`src/TNC.Trading.Platform.Api/Features/Platform/RecordAuthAuditEventEndpointHandler.cs:11-55`). |
| P1 | Replace hidden store-level commits with explicit use-case transaction ownership. | Makes atomicity, retry, and partial-failure behavior reviewable. | L | Before trading execution | Stores call `SaveChangesAsync` independently, while multi-store workflows coordinate above them (`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/EfPlatformEventStore.cs:38-56`; `src/TNC.Trading.Platform.Application/Services/PlatformStateCoordinator.cs:85-146`). |
| P1 | Add architecture tests for module dependencies and current inward dependency rules. | Prevents folder-only modularity and catches future reverse references cheaply. | S | With first module | The production graph is currently acyclic, but no architecture-test project or dependency analyzer was found. |
| P2 | Move restart policy and other business rules inward, and make Application auth policy definitions framework-neutral where practical. | Reduces EF and ASP.NET Core leakage into policy ownership. | M | During module extraction | Restart policy depends on an EF entity, while Application directly configures `AuthorizationOptions` (`src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/PlatformConfigurationRestartPolicy.cs:6-13`; `src/TNC.Trading.Platform.Application/Authentication/PlatformAuthorizationPolicyRegistration.cs:1-35`). |
| P2 | Split the cross-feature Web API client by module capability while preserving shared HTTP/auth plumbing. | Limits route, DTO, and error-handling growth as business modules appear. | S-M | When the next module lands | `PlatformApiClient` currently covers status, configuration, retries, events, administration, and login history (`src/TNC.Trading.Platform.Web/PlatformApiClient.cs:9-123`). |
| P2 | Reclassify Application tests that instantiate Infrastructure or replace adapters with module-owned fakes. | Makes test names and project boundaries accurately signal unit versus adapter coverage. | S-M | During test movement | Application unit tests reference Infrastructure and use concrete EF adapters (`test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/TNC.Trading.Platform.Application.UnitTests.csproj:18-25`). |
| P2 | Raise behavioral sensitivity for state, transaction, idempotency, and failure-ordering paths. | Increases confidence that safety rules survive refactoring. | M-L | Before trading execution | Historical evidence reports Application mutation score 18.80% and low API coverage (`docs/wiki/testing-and-quality.md:147-177`). |
| P3 | Design a worker extraction proof for broker supervision or future market data, including lease, checkpoint, idempotency, and outbox requirements. | Provides a low-risk path to independent lifecycle and scaling without premature microservices. | M | Trigger-based | API and supervisor currently share one process; extraction becomes useful before API scale-out or heterogeneous continuous workloads. |
| P3 | Reassess service decomposition only after module boundaries and production operations are proven. | Ensures network boundaries respond to measured scaling, isolation, or release needs. | L | Trigger-based | No production deployment, durable messaging, backup, rollback, or disaster-recovery artifacts were found in the evidence pass. |

## Rejected shortcuts

* Do not create a separate project or service for every current feature handler.
  That increases navigation and deployment cost without creating cohesive
  business ownership.
* Do not move the current coordinator wholesale into a worker. That changes its
  process but preserves its coupling and adds distributed failure modes.
* Do not assign each proposed module a separate database now. One SQL database
  supports local transactions and part-time operability; ownership can be
  enforced logically before physical separation.
* Do not introduce an event bus until a workflow has a justified asynchronous
  boundary and defined delivery, ordering, idempotency, and recovery semantics.
* Do not treat broad microservice decomposition as a scalability fix while the
  current supervisor lacks single-owner coordination.

## External reference points

The recommendation relies primarily on repository evidence. These references
are useful implementation guardrails rather than proof of the current design:

* [Microsoft guidance on domain analysis and microservice boundaries](https://learn.microsoft.com/azure/architecture/microservices/model/domain-analysis)
  supports deriving boundaries from business capabilities and bounded contexts
  rather than technical layers.
* [Microsoft guidance on interservice communication](https://learn.microsoft.com/azure/architecture/microservices/design/interservice-communication)
  describes the coupling, latency, resiliency, and partial-failure costs added
  by network boundaries.
* [.NET hosted services guidance](https://learn.microsoft.com/aspnet/core/fundamentals/host/hosted-services)
  defines the lifecycle model currently used by the supervisor and the basis
  for deciding when background work belongs in a separate host.
* [EF Core transactions guidance](https://learn.microsoft.com/ef/core/saving/transactions)
  provides the relevant mechanics once use-case transaction ownership is
  defined.

## Assumptions and decision triggers

* The verified operating model remains one part-time owner and one internal
  operator. A staffed team with independently released capabilities would
  change the operational-cost weighting.
* The near-term deployment remains one API instance. A requirement for multiple
  API replicas raises active-worker coordination to P0 and may accelerate a
  worker extraction.
* Future live execution is not delivered. Before live trading, risk validation,
  intent idempotency, broker confirmation, reconciliation, durable audit,
  kill-switch behavior, and recovery tests require explicit architecture and
  acceptance evidence.
* Market-data throughput and strategy latency have not been measured. Service
  or worker boundaries should follow measurements and provider-connection
  constraints rather than forecast volume alone.
* One SQL database remains acceptable until module ownership, load, compliance,
  availability, or recovery requirements justify physical separation.

## Research limitations

* The analysis synthesizes the three completed evidence documents and checks
  source only through their exact references. No runtime, load, concurrency, or
  production deployment experiment was performed.
* Scores are calibrated architectural judgments, not measured reliability or
  security certifications.
* Initial module names are working ownership boundaries. Detailed domain
  modeling is required before locking namespaces, assemblies, schemas, or
  public contracts.
* No approved production target, replica count, recovery objective, or market
  data volume was available. The recommendation therefore uses explicit
  extraction triggers instead of inventing those requirements.

## Clarifying questions

No question blocks the recommendation. The following inputs should be resolved
before Stage 3 or live trading design:

* Is the intended production API replica count exactly one, or must the HTTP
  control plane scale independently?
* What recovery point and recovery time objectives apply to configuration,
  audit, order intent, broker confirmation, and reconciliation records?
* What sustained market-data subscriptions, strategy evaluation rate, and
  order latency targets are expected?
* Which Azure production services are approved for SQL, secrets, Data
  Protection keys, telemetry, deployment, messaging, backup, and restore?
* Is proof data best-effort current state, or must it be durable and shared
  across process replacement?

## Recommended next research

* [ ] Reproduce the real IG Demo proof path with sanitized outbound-header
  capture.
* [ ] Run a two-instance supervisor experiment against one SQL database and
  measure duplicate work and conflicts.
* [ ] Produce a use-case and data-ownership map for the three initial modules.
* [ ] Define transaction, idempotency, and recovery invariants for configuration
  reconciliation and future order execution.
* [ ] Measure future market-data and strategy workload assumptions before
  selecting a worker transport or deployment model.
* [ ] Research the approved Azure production topology and operational ownership
  once deployment constraints are available.