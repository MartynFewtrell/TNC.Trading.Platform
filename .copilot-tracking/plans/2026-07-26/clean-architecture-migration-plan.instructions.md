---
applyTo: '.copilot-tracking/changes/2026-07-26/clean-architecture-migration-changes.md'
---
<!-- markdownlint-disable-file -->
# Implementation Plan: Clean Architecture Migration

## Overview

Incrementally purify Application, move policy and provider responsibilities to their correct boundaries, decompose the shared coordinator into tested vertical slices, and conditionally introduce Domain only when a cohesive policy cluster merits compile-time isolation, while requiring a passing solution build and complete test run after every significant refactor.

## Objectives

### User Requirements

* Create a detailed plan to move the current solution toward Clean Architecture principles. — Source: User request, 2026-07-26.
* Base the plan on `.copilot-tracking/research/2026-07-26/clean-architecture-migration-research.md`. — Source: User request, 2026-07-26.
* Enforce solution builds and complete passing test runs after every significant refactor. — Source: User request, 2026-07-26.
* Add tests throughout the refactor whenever behavior, boundaries, or regression risk require them. — Source: User request, 2026-07-26.

### Derived Objectives

* Preserve the existing inward, acyclic project graph and stable API, Web, AppHost, and ServiceDefaults behavior while responsibility placement changes incrementally. — Derived from: Primary research and repository refactoring rules.
* Retain Application and Infrastructure as the immediate architecture; create Domain only after a tested policy cluster passes a documented readiness gate. — Derived from: Primary research selected approach.
* Make status and event queries side-effect free before decomposing write workflows. — Derived from: Current query paths call the mutating reconciliation coordinator.
* Normalize the IG boundary before broad authentication handler extraction so provider protocol types are not copied into new use cases. — Derived from: Verification research sequencing recommendation.
* Treat each feature extraction, project/package reference change, provider boundary change, persistence semantic change, and mechanical move batch as an independently gated refactor. — Derived from: User validation requirement and regression-localization needs.
* Move existing adapter-backed Application tests to their correct boundary and add direct handler, validator, policy, architecture, SQL integration, and closed-box tests at the lowest useful level. — Derived from: Repository test instructions and current mixed test ownership.
* Update affected `docs/wiki/` pages within the phase that changes implemented behavior or architecture. — Derived from: Repository documentation source-of-truth requirement.

## Context Summary

### Current Architecture

```text
AppHost -> API, Web
API -> Application, Infrastructure, ServiceDefaults
Infrastructure -> Application
Web -> Application, ServiceDefaults
Application -> none
ServiceDefaults -> none
```

The assembly graph already points inward, but Application contains ASP.NET Core hosting and provider-shaped IG concerns, Infrastructure owns several business decisions, API directly executes EF/startup workflow mechanics, and most feature handlers delegate to a 13-dependency coordinator. The complete solution currently contains six production projects and nine test projects.

### Selected Migration Architecture

```text
Immediate state
  Application: framework-neutral policy, feature slices, use cases, and inward ports
  Infrastructure -> Application: EF/SQL, IG, encryption, notifications, and adapter translation

Conditional state after the extraction gate
  Domain: validated business state, invariants, values, and policies
  Application -> Domain: feature slices and use-case coordination
  Infrastructure -> Application: external mechanisms and translation
```

API, Web, AppHost, and ServiceDefaults remain stable except for narrow hosting and composition changes. Domain is not a prerequisite for starting or completing the migration.

### Affected Files Tree

```text
src/
  TNC.Trading.Platform.Application/
    Features/
      ReconcilePlatformAuthentication/
      TriggerManualAuthRetry/
      RecordAuthAuditEvent/
      UpdatePlatformConfiguration/
      GetPlatformStatus/
      GetPlatformEvents/
      PlatformAuthentication/Ports/
    Services/                         # coordinator and hosting concerns retired incrementally
    TNC.Trading.Platform.Application.csproj
  TNC.Trading.Platform.Domain/        # conditional; one cohesive policy cluster only
  TNC.Trading.Platform.Infrastructure/
    Persistence/EntityFramework/
    Integrations/Ig/
    Configuration/SqlServer/
    Credentials/DataProtection/
    Notifications/
    Operations/Retention/
    Startup/
    Time/
  TNC.Trading.Platform.Api/
    Hosting/
    Authentication/
    Program.cs
  TNC.Trading.Platform.Web/
    Authentication/
test/
  TNC.Trading.Platform.Architecture/
  TNC.Trading.Platform.Application/
  TNC.Trading.Platform.Domain/        # conditional
  TNC.Trading.Platform.Infrastructure/
  TNC.Trading.Platform.Api/
  TNC.Trading.Platform.Web/
.github/scripts/
  Invoke-CleanArchitectureQualityGate.ps1
docs/wiki/
  architecture.md
  application-overview.md
  runtime-behavior.md
  api-reference.md
  local-development.md
  operator-guide.md
  testing-and-quality.md
  ig-day-trading-with-ig-apis.md
```

### Design Patterns Applied

* Dependency inversion through inward-owned, operation-scoped ports
* Vertical slices with one request, response, handler, and validator when required
* CQRS semantics with non-mutating queries and explicit reconciliation commands
* Adapter translation that prevents EF, HTTP, provider, SDK, and UI representations from crossing inward
* Typed use-case outcomes for expected rejection paths
* Operation-specific commit intents instead of generic repositories or a universal unit of work
* Conditional Domain extraction based on behavior and invariants rather than project-diagram symmetry
* Test pyramid with direct policy/handler tests, adapter tests, real SQL integration tests, and minimal Aspire closed-box coverage

### Project Files

* `TNC.Trading.Platform.slnx` - Six current production and nine current test projects; registration point for conditional/new projects.
* `src/TNC.Trading.Platform.Application/Services/PlatformStateCoordinator.cs` - Shared workflow core to decompose operation by operation.
* `src/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.csproj` - Inner project currently coupled to the ASP.NET Core shared framework.
* `src/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.csproj` - Correct inward project reference but mixed mechanism and policy ownership.
* `src/TNC.Trading.Platform.Api/Program.cs` - Composition root currently reaches directly into EF, retention, and reconciliation mechanics.
* `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/TNC.Trading.Platform.Application.UnitTests.csproj` - Unit project currently references Infrastructure.

### References

* `.copilot-tracking/research/2026-07-26/clean-architecture-migration-research.md` - Primary architecture assessment and selected two-state migration.
* `.copilot-tracking/research/subagents/2026-07-26/clean-architecture-migration-plan-verification.md` - Current topology, exact anchors, test/gate verification, and sequencing refinements.
* `.copilot-tracking/details/2026-07-26/clean-architecture-migration-details.md` - File-level implementation and validation instructions.

### Standards References

* `.github/instructions/architecture.instructions.md` — Portable Clean Architecture, vertical-slice, CQRS, inward-port, and boundary-translation rules.
* `.github/instructions/refactoring.instructions.md` — Small behavior-preserving refactors and focused validation.
* `.github/instructions/tests.instructions.md` — xUnit project boundaries, naming, documentation, and test-pyramid rules.
* `.github/instructions/aspire-tests.instructions.md` — Closed-box distributed testing rules.
* `.github/instructions/csharp.instructions.md` — One type per file, naming, nullability, error handling, and test expectations.
* `.github/copilot-instructions.md` — Program composition, wiki source-of-truth, and repository-wide conventions.

## Mandatory Gate for Every Significant Refactor

Every phase and independently named subphase below must complete this sequence before subsequent migration work begins:

1. Add or update direct tests for changed behavior or boundaries.
2. Run the narrowest affected tests and adjacent architecture/adapter suites.
3. Run `dotnet build TNC.Trading.Platform.slnx`.
4. Run unfiltered `dotnet test TNC.Trading.Platform.slnx`.
5. Record exit codes and passed, failed, and skipped totals.
6. Stop when either full command fails; fix and repeat the same gate before continuing.

The complete suite includes Docker-backed Aspire API integration and Web functional/E2E tests. A serialized `dotnet test TNC.Trading.Platform.slnx -m:1` retry is allowed only for the documented MSBuild child-node failure and must be recorded. No phase may claim completion from unit tests or filtered solution tests alone.

Unless the details explicitly mark adjacent steps as one atomic compile-safe refactor, every numbered production-code step is independently gated. Phase 3 assigns concrete gates to six separate policy families; Phase 6 gates every feature extraction; Phase 9 assigns concrete gates to seven adapter-family moves; Phase 10 assigns concrete gates to six operational-hardening items. Affected wiki pages must be updated before the gate for the change they describe.

## Implementation Checklist

### [x] Implementation Phase 0: Resolve Semantics and Establish Baselines

<!-- parallelizable: false -->

* [x] Step 0.1: Record writer, consistency, freshness, migration, IG environment, durability, and shared host-contract decisions.
  * Details: `.copilot-tracking/details/2026-07-26/clean-architecture-migration-details.md` (Lines 58-93)
* [x] Step 0.2: Add the repeatable solution build and unfiltered complete-test quality gate.
  * Details: `.copilot-tracking/details/2026-07-26/clean-architecture-migration-details.md` (Lines 95-114)
* [x] Step 0.3: Capture a fully passing pre-refactor build, test, contract, and startup baseline.
  * Details: `.copilot-tracking/details/2026-07-26/clean-architecture-migration-details.md` (Lines 116-131)

### [x] Implementation Phase 1: Add Characterization and Architecture Safety Rails

<!-- parallelizable: false -->

* [x] Step 1.1: Characterize API, Web, startup, reconciliation, configuration, and retry behavior without freezing internals.
  * Details: `.copilot-tracking/details/2026-07-26/clean-architecture-migration-details.md` (Lines 137-159)
* [x] Step 1.2: Add role-aware architecture rules alongside the existing topology-neutral graph checks.
  * Details: `.copilot-tracking/details/2026-07-26/clean-architecture-migration-details.md` (Lines 161-180)
* [x] Step 1.3: Run focused checks and the mandatory full gate; update architecture/testing wiki pages.
  * Details: `.copilot-tracking/details/2026-07-26/clean-architecture-migration-details.md` (Lines 182-193)

### [x] Implementation Phase 2: Make Reconciliation Explicit and Queries Side-Effect Free

<!-- parallelizable: false -->

* [x] Step 2.1: Create the explicit reconcile command and selected writer serialization.
  * Gate: `phase-2-1-reconcile-command` must pass before Step 2.2.
  * Details: `.copilot-tracking/details/2026-07-26/clean-architecture-migration-details.md` (Lines 199-224)
* [x] Step 2.2: Move scheduler and hosted-service mechanics from Application to API.
  * Gate: `phase-2-2-api-supervision` must pass before Step 2.3.
  * Details: `.copilot-tracking/details/2026-07-26/clean-architecture-migration-details.md` (Lines 226-250)
* [x] Step 2.3: Replace status and event reconciliation calls with non-creating projection reads and explicit freshness outcomes.
  * Gate: `phase-2-3-query-purity` must pass before Step 2.4 or another refactor.
  * Details: `.copilot-tracking/details/2026-07-26/clean-architecture-migration-details.md` (Lines 252-280)
* [x] Step 2.4: Verify all three focused/full gate records and phase-local wiki updates.
  * Details: `.copilot-tracking/details/2026-07-26/clean-architecture-migration-details.md` (Lines 282-296)

### [x] Implementation Phase 8: Remove Hosting and Framework Concerns from Application

<!-- parallelizable: false -->

Execute this independent lane immediately after Phase 2 and the Phase 0 shared host-contract decision; it does not wait for database lifecycle or coordinator retirement.

* [x] Step 8.1: Move ASP.NET Core authentication and authorization registration into API and Web with parity tests.
  * Gate: `phase-8-1-host-auth-ownership` must pass before Step 8.2.
  * Details: `.copilot-tracking/details/2026-07-26/clean-architecture-migration-details.md` (Lines 738-761)
* [x] Step 8.2: Remove remaining DI/configuration/logging hosting dependencies and then remove `Microsoft.AspNetCore.App` from Application.
  * Gate: `phase-8-2-framework-reference-removal` must pass before another refactor.
  * Details: `.copilot-tracking/details/2026-07-26/clean-architecture-migration-details.md` (Lines 763-782)
* [x] Step 8.3: Verify both focused/full gate records and pre-gate architecture/application/testing wiki updates.
  * Details: `.copilot-tracking/details/2026-07-26/clean-architecture-migration-details.md` (Lines 784-799)

### [x] Implementation Phase 3: Move Validation and Business Policy Inward

<!-- parallelizable: false -->

* [x] Step 3.1: Enforce configuration invariants in Application while API retains transport validation.
  * Gate: `phase-3-1-configuration-invariants` must pass before policy movement.
  * Details: `.copilot-tracking/details/2026-07-26/clean-architecture-migration-details.md` (Lines 302-328)
* [x] Step 3.2: Move restart, retention, sensitivity, schedule, retry, and transition decisions inward one policy at a time with direct tests.
  * Gates: `phase-3-2-trading-schedule`, `phase-3-3-retry-timing`, `phase-3-4-auth-transitions`, `phase-3-5-restart-classification`, `phase-3-6-retention-policy`, and `phase-3-7-sensitivity-policy`, each passing before the next family begins.
  * Details: `.copilot-tracking/details/2026-07-26/clean-architecture-migration-details.md` (Lines 330-364)
* [x] Step 3.3: Evaluate trading-schedule and authentication-transition candidates against the Domain extraction gate.
  * Details: `.copilot-tracking/details/2026-07-26/clean-architecture-migration-details.md` (Lines 366-377)
* [x] Step 3.4: Conditionally create Domain and move exactly one complete policy cluster when the gate passes. Domain deferred because neither candidate passed all six readiness conditions.
  * Gate: `phase-3-domain-extraction` must pass after Domain, direct tests, architecture rules, and wiki updates land.
  * Details: `.copilot-tracking/details/2026-07-26/clean-architecture-migration-details.md` (Lines 379-399)
* [x] Step 3.5: Run a separate mandatory full gate for policy movement and, when executed, another gate for Domain extraction.
  * Details: `.copilot-tracking/details/2026-07-26/clean-architecture-migration-details.md` (Lines 401-417)

### [x] Implementation Phase 4: Normalize the IG Outbound Adapter

<!-- parallelizable: false -->

* [x] Step 4.1: Define the operation-scoped inward authentication/proof port as part of one atomic port-plus-adapter refactor.
  * Details: `.copilot-tracking/details/2026-07-26/clean-architecture-migration-details.md` (Lines 425-438)
* [x] Step 4.2: Implement and register its Infrastructure adapter so IG HTTP, JSON, tokens, headers, routing, and exceptions stay outward.
  * Details: `.copilot-tracking/details/2026-07-26/clean-architecture-migration-details.md` (Lines 440-460)
* [x] Step 4.3: Run IG adapter, Application, architecture, and mandatory full gates; update IG/runtime/testing documentation.
  * Gate: `phase-4-ig-boundary` must pass before coordinator decomposition.
  * Details: `.copilot-tracking/details/2026-07-26/clean-architecture-migration-details.md` (Lines 462-479)

### [x] Implementation Phase 5: Extract Manual Retry as the First Complete Write Slice

<!-- parallelizable: false -->

* [x] Step 5.1: Move manual retry policy and orchestration into its handler with typed outcomes and operation-specific commit intent.
  * Gate: `phase-5-1-manual-retry` must pass before test-boundary reclassification.
  * Details: `.copilot-tracking/details/2026-07-26/clean-architecture-migration-details.md` (Lines 485-516)
* [x] Step 5.2: Remove adapter-backed workflow tests from the Application unit boundary and add correct unit/integration coverage.
  * Gate: `phase-5-2-test-boundaries` must pass before another coordinator operation moves.
  * Details: `.copilot-tracking/details/2026-07-26/clean-architecture-migration-details.md` (Lines 518-537)
* [x] Step 5.3: Run handler/API/Infrastructure checks and the mandatory full gate before another coordinator operation moves.
  * Details: `.copilot-tracking/details/2026-07-26/clean-architecture-migration-details.md` (Lines 539-552)

### [x] Implementation Phase 6: Extract Remaining Operations One at a Time

<!-- parallelizable: false -->

* [x] Step 6.1: Extract auth audit recording and pass its focused plus complete gate.
  * Details: `.copilot-tracking/details/2026-07-26/clean-architecture-migration-details.md` (Lines 560-577)
* [x] Step 6.2: Extract configuration update while preserving atomic local writes, then pass its focused plus complete gate.
  * Details: `.copilot-tracking/details/2026-07-26/clean-architecture-migration-details.md` (Lines 579-593)
* [x] Step 6.3: Complete reconciliation extraction with failure, idempotency, cancellation, and concurrency tests, then pass its full gate.
  * Details: `.copilot-tracking/details/2026-07-26/clean-architecture-migration-details.md` (Lines 595-609)
* [x] Step 6.4: Complete status/event query extraction and pass strict no-write plus complete gates.
  * Details: `.copilot-tracking/details/2026-07-26/clean-architecture-migration-details.md` (Lines 611-624)
* [x] Step 6.5: Refine IG login history only if a concrete boundary violation remains; run a separate full gate when changed.
  * Details: `.copilot-tracking/details/2026-07-26/clean-architecture-migration-details.md` (Lines 626-638)
* [x] Step 6.6: Retire `PlatformStateCoordinator` only after all callers and coverage have migrated, then pass its full gate.
  * Details: `.copilot-tracking/details/2026-07-26/clean-architecture-migration-details.md` (Lines 640-658)

### [x] Implementation Phase 7: Clean Startup and Database Lifecycle

<!-- parallelizable: false -->

* [x] Step 7.1: Add Infrastructure-owned EF migrations and real SQL Server transaction/concurrency tests.
  * Gate: `phase-7-1-schema-migrations` must pass before startup composition changes.
  * Details: `.copilot-tracking/details/2026-07-26/clean-architecture-migration-details.md` (Lines 664-688)
* [x] Step 7.2: Replace API access to DbContext/retention mechanics with one Infrastructure startup composition surface.
  * Gate: `phase-7-2-startup-initializer` must pass before another refactor.
  * Details: `.copilot-tracking/details/2026-07-26/clean-architecture-migration-details.md` (Lines 690-713)
* [x] Step 7.3: Run SQL, API, AppHost, and mandatory full gates; update migration/readiness documentation.
  * Details: `.copilot-tracking/details/2026-07-26/clean-architecture-migration-details.md` (Lines 715-730)

### [x] Implementation Phase 9: Reorganize Infrastructure Mechanically

<!-- parallelizable: false -->

* [x] Step 9.1: Move one adapter family per batch, with compiler-assisted namespace updates and a mandatory full gate after every batch.
  * Gates: `phase-9-1-time`, `phase-9-2-ig-integration`, `phase-9-3-notifications`, `phase-9-4-credentials-configuration`, `phase-9-5-retention`, `phase-9-6-persistence-startup`, and `phase-9-7-dependency-injection`.
  * Update `docs/wiki/architecture.md` before every batch gate.
  * Details: `.copilot-tracking/details/2026-07-26/clean-architecture-migration-details.md` (Lines 805-848)

### [x] Implementation Phase 10: Complete Operational Hardening

<!-- parallelizable: false -->

* [x] Step 10.1: Prove key-ring, proof-data, notification, consistency, migration-recovery, and multi-replica semantics with focused tests and an independent full gate per hardening change.
  * Gates: `phase-10-1-data-protection-key-ring`, `phase-10-2-proof-data-durability`, `phase-10-3-notification-delivery`, `phase-10-4-configuration-reconciliation`, `phase-10-5-replica-concurrency`, and `phase-10-6-migration-recovery`.
  * Update affected runtime/local-development/operator/architecture/testing wiki pages before each hardening-item gate.
  * Details: `.copilot-tracking/details/2026-07-26/clean-architecture-migration-details.md` (Lines 854-889)

### [x] Implementation Phase 11: Final Architecture Verification and Handoff

<!-- parallelizable: false -->

* [x] Step 11.1: Verify project, source, boundary-data, adapter, composition, and test ownership rules.
  * Details: `.copilot-tracking/details/2026-07-26/clean-architecture-migration-details.md` (Lines 895-906)
* [x] Step 11.2: Run final architecture tests, solution build, complete test suite, AppHost startup, and representative manual checks.
  * Details: `.copilot-tracking/details/2026-07-26/clean-architecture-migration-details.md` (Lines 908-916)
* [x] Step 11.3: Reconcile wiki documentation and confirm every significant refactor has passing gate evidence.
  * Details: `.copilot-tracking/details/2026-07-26/clean-architecture-migration-details.md` (Lines 918-926)

## Planning Log

See `.copilot-tracking/plans/logs/2026-07-26/clean-architecture-migration-log.md` for migration-path selection, research deviations, unresolved implementation decisions, and follow-on work.

## Dependencies

* .NET SDK `10.0.200` from `global.json`.
* Docker Desktop and Aspire-managed SQL Server, Keycloak, and Mailpit.
* Playwright browser prerequisites for functional and E2E tests.
* Product/deployment decisions listed in Phase 0.
* Existing xUnit, Aspire testing, bUnit, and Playwright infrastructure.
* A clean passing baseline before the first production refactor.

## Success Criteria

* Application contains framework-neutral feature slices, use-case policy, and inward-owned ports; its direct tests need no Infrastructure. — Traces to: User Clean Architecture request and primary research.
* Infrastructure owns EF/SQL, IG, encryption, notifications, and external translation without owning business decisions. — Traces to: Primary research target responsibilities.
* API endpoints and startup remain composition/inbound adapters rather than owners of EF, provider, or business workflow. — Traces to: Primary research API findings.
* Status and event queries are side-effect free, and reconciliation is explicit, serialized, freshness-aware, and directly tested. — Traces to: Primary research Stage 1.
* Domain is created only when one cohesive invariant-owning cluster passes the documented gate, and then has zero outward dependencies. — Traces to: Primary research conditional target.
* Every operation has direct handler tests; every moved policy has direct in-process tests; provider and SQL behavior use appropriate adapter/integration tests. — Traces to: User additional-testing requirement.
* Every significant refactor passes `dotnet build TNC.Trading.Platform.slnx` and unfiltered `dotnet test TNC.Trading.Platform.slnx` before the next refactor begins. — Traces to: User full-validation requirement.
* API, Web, AppHost, and ServiceDefaults retain stable public/runtime behavior except where Phase 0 records an intentional contract change. — Traces to: User scope through primary research.
* Affected wiki pages match implemented architecture and runtime behavior before each phase is marked complete. — Traces to: Repository documentation requirement.