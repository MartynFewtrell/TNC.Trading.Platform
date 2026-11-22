---
applyTo: 'src/**/*.cs, src/**/*.razor, test/**/*.cs, docs/wiki/**/*.md'
---
<!-- markdownlint-disable-file -->
# Implementation Plan: Account Details

## Overview

Deliver an Account Details vertical slice that captures real data from the configured IG test account as immutable SQL snapshots, exposes Viewer history and Operator refresh, and automatically records the first successful login capture for each configured trading day.

## Objectives

### User Requirements

* Retrieve real account details from the configured IG test account through `GET /gateway/deal/accounts` and store every returned account and balance with a common date stamp. Source: user request and `.copilot-tracking/research/2026-08-07/account-details-research.md` (Lines 6-24)
* Add an Account Details UI page that shows saved data and provides an update option. Source: user request and research (Lines 6-24)

### Derived Objectives

* Preserve Clean Architecture by placing account contracts and ports in Application, IG and EF Core adapters in Infrastructure, thin routes in API, and token-free rendering in Web. Derived from: research (Lines 36-48)
* Retain immutable, environment-scoped retrieval headers and child account rows, rather than extending the login proof or maintaining only current state. Derived from: research (Lines 121-176)
* Capture automatically only after durable successful login in both active authentication paths, while isolating automatic-capture failure from login success. Derived from: subagent research (Lines 20-38)
* Use the configured trading-schedule time zone for `TradingDay`, opaque composite-keyset cursors, and a `409 Conflict` in-progress outcome for concurrent manual refresh. Derived from: research (Lines 212-267) and architecture addendum

## Context Summary

### Project Files

* src/TNC.Trading.Platform.Application/Features/ReconcilePlatformAuthentication/PlatformAuthenticationReconciler.cs - reconciler-owned durable login success path
* src/TNC.Trading.Platform.Application/Features/TriggerManualAuthRetry/TriggerManualAuthRetryHandler.cs - distinct direct manual-retry success path
* src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgBrokerAuthenticationGateway.cs - existing proof-only, session-header IG mechanics
* src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/PlatformDbContext.cs - EF DbSets and mappings extension point
* src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs - Viewer GET and Operator POST route extension point
* src/TNC.Trading.Platform.Web/Components/Pages/IgLogin/IgLoginHistory.razor - closest saved-history page precedent

### References

* .copilot-tracking/research/2026-08-07/account-details-research.md - primary product, provider, persistence, API, UI, and validation research
* .copilot-tracking/research/subagents/2026-08-07/account-details-architecture-research.md - verified active hooks, registrations, baseline behavior, tests, and commands
* https://labs.ig.com/reference/accounts.html - revalidate the IG test-account `/accounts` provider contract immediately before implementation

### Standards References

* .github/copilot-instructions.md - repository architecture, authentication, documentation, and validation rules
* .github/instructions/architecture.instructions.md - vertical-slice and dependency-direction requirements
* .github/instructions/auth.instructions.md - Keycloak authorization and protected provider-boundary requirements
* .github/instructions/configuration.instructions.md - protected IG configuration requirements
* .github/instructions/radzen-blazor.instructions.md - Web component conventions
* .github/instructions/tests.instructions.md - test coverage conventions

## Implementation Checklist

Delivery rule: after every significant implementation slice, run the narrowest affected build and test command before making further changes. Each phase below has a mandatory build-and-test gate; do not begin the next phase while its gate fails. Repair only the current phase's isolated failures, then rerun the same gate.

### [x] Implementation Phase 1: Application Operations and Durable Login Hooks

<!-- parallelizable: false -->

* [x] Step 1.1: Create `Features/AccountDetails/` request, response, handler, record, outcome, and inward-owned port types for read, manual refresh, automatic daily capture, snapshot storage, gateway, and environment lease.
  * Details: .copilot-tracking/details/2026-08-07/account-details-details.md (Lines 12-36)
* [x] Step 1.2: Add configured-time-zone trading-day calculation and invoke best-effort daily capture after durable new-success completion in both reconciler and direct manual-retry paths.
  * Details: .copilot-tracking/details/2026-08-07/account-details-details.md (Lines 36-62)
* [x] Step 1.3: Add Application tests for validation, no-provider read behavior, automatic capture timing, duplicate-day avoidance, failure isolation, cursor traversal, and coalescing semantics.
  * Details: .copilot-tracking/details/2026-08-07/account-details-details.md (Lines 63-78)
* [x] Step 1.4: Build the Application project and run the Application unit-test project successfully before beginning Infrastructure work.
  * Details: .copilot-tracking/details/2026-08-07/account-details-details.md (Lines 79-92)

### [x] Implementation Phase 2: Infrastructure Provider, Persistence, and Retention

<!-- parallelizable: false -->

* [x] Step 2.1: Implement a dedicated, fresh-session IG accounts gateway that validates the entire response before exposing Application account records and keeps credentials and session tokens transport-local.
  * Details: .copilot-tracking/details/2026-08-07/account-details-details.md (Lines 97-119)
* [x] Step 2.2: Add immutable retrieval/account EF entities, mappings, store, transaction, environment-scoped SQL lease, DI registrations, and an additive migration with deterministic/keyset and automatic-daily indexes.
  * Details: .copilot-tracking/details/2026-08-07/account-details-details.md (Lines 120-140)
* [x] Step 2.3: Extend retention with set-based SQL and test-provider paths that remove expired snapshots while preserving each environment's newest success; do not alter the startup initial-schema baseline table list.
  * Details: .copilot-tracking/details/2026-08-07/account-details-details.md (Lines 141-161)
* [x] Step 2.4: Add Infrastructure tests for provider headers/error normalization, atomic persistence and rollback, indexes, keyset reads, lease behavior, migration startup, and retention.
  * Details: .copilot-tracking/details/2026-08-07/account-details-details.md (Lines 162-176)
* [x] Step 2.5: Build the Infrastructure project and run the Infrastructure unit-test project successfully before beginning API work.
  * Details: .copilot-tracking/details/2026-08-07/account-details-details.md (Lines 177-190)

### [x] Implementation Phase 3: API Contract and Authorization

<!-- parallelizable: false -->

* [x] Step 3.1: Register Account Details handlers and map Viewer `GET /api/platform/account-details?cursor=` plus Operator `POST /api/platform/account-details/refresh` routes with typed HTTP outcome translations.
  * Details: .copilot-tracking/details/2026-08-07/account-details-details.md (Lines 195-214)
* [x] Step 3.2: Add API tests for Viewer reads, denied Viewer refresh, Operator refresh, cursor navigation, empty retrieval, and `409`, `429`, `502`, `503`, and `504` outcomes.
  * Details: .copilot-tracking/details/2026-08-07/account-details-details.md (Lines 215-229)
* [x] Step 3.3: Build the API project and run API unit tests successfully before beginning Web work.
  * Details: .copilot-tracking/details/2026-08-07/account-details-details.md (Lines 230-243)

### [x] Implementation Phase 4: Web History and Operator Refresh

<!-- parallelizable: false -->

* [x] Step 4.1: Add typed Account Details GET/POST client methods and presentation state that holds the current saved retrieval during pending or failed refreshes.
  * Details: .copilot-tracking/details/2026-08-07/account-details-details.md (Lines 248-262)
* [x] Step 4.2: Create the Viewer-protected `/account-details` page and navigation item, render identity/status/transfer fields and balances, expose accessible cursor controls, and conditionally render a disabled-in-flight Operator refresh command.
  * Details: .copilot-tracking/details/2026-08-07/account-details-details.md (Lines 263-282)
* [x] Step 4.3: Add bUnit coverage for empty, latest, older/newer, pending, failed refresh, and access-aware command states.
  * Details: .copilot-tracking/details/2026-08-07/account-details-details.md (Lines 283-297)
* [x] Step 4.4: Build the Web project and run Web unit tests successfully before beginning hosted validation.
  * Details: .copilot-tracking/details/2026-08-07/account-details-details.md (Lines 298-311)

### [~] Implementation Phase 5: Integration, Documentation, and Final Validation

<!-- parallelizable: false -->

* [~] Step 5.1: Add SQL-backed integration and AppHost/Playwright coverage for migration, authorization, automatic first-daily capture, cross-replica contention, refresh, and history navigation.
  * Details: .copilot-tracking/details/2026-08-07/account-details-details.md (Lines 316-331)
* [x] Step 5.2: Update the owning `docs/wiki/` pages with real IG test-account scope, future Live boundary, protected credentials, capture timing, persistence/retention, authorization, API, concurrency, and migration guidance.
  * Details: .copilot-tracking/details/2026-08-07/account-details-details.md (Lines 332-345)
* [~] Step 5.3: Run focused tests, full build, affected integration/functional suites, and resolve only isolated validation regressions.
  * Details: .copilot-tracking/details/2026-08-07/account-details-details.md (Lines 346-376)

## Planning Log

See .copilot-tracking/plans/logs/2026-08-07/account-details-log.md for discrepancy tracking, implementation paths considered, and suggested follow-on work.

## Dependencies

* .NET SDK 10.0.200 from global.json
* Docker, SQL Server, and Keycloak through the existing Aspire AppHost for hosted validation
* Existing protected IG test-account configuration and credentials
* Current IG `/accounts` response verification before implementing the wire DTO

## Success Criteria

* A validated complete IG test-account response creates exactly one immutable, environment-scoped retrieval and all account children in one transaction. Traces to: primary research (Lines 137-176)
* The first successful login per configured trading day triggers at most one automatic capture, while a failed capture does not invalidate login and retries after later success. Traces to: primary research (Lines 212-239)
* Viewer users can load newest and adjacent historical saved snapshots, while only Operators can invoke refresh. Traces to: implementation details (Lines 248-297)
* No credential, CST, X-SECURITY-TOKEN, raw payload, or provider session crosses Infrastructure. Traces to: primary research (Lines 101-119)
* Every significant implementation slice and each completed phase has a successful narrow build-and-test gate before downstream work begins; focused and final validation commands complete for all changed Application, Infrastructure, API, Web, hosted-test, and documentation surfaces. Traces to: implementation details (Lines 79-92, 177-190, 230-243, 298-311, and 346-376)