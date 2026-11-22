---
applyTo: 'src/**/*.cs, test/**/*.cs, docs/wiki/**/*.md'
---
<!-- markdownlint-disable-file -->
# Implementation Plan: Trailing Stops Features

## Overview

Implement an Operator-only Account Preferences vertical slice that reads and confirms IG trailing-stops updates, records verified provider observations in SQL history, and exposes the live control and history in the web application.

## Objectives

### User Requirements

* Create a plan for the trailing-stops features based on the supplied research. Source: user request and `.copilot-tracking/research/2026-06-06/ig-account-preferences-web-ui-integration-research.md`.

### Derived Objectives

* Keep live mutable preferences separate from Account Details persisted retrieval history. Derived from: `RefreshAccountDetailsHandler.cs:6-89`.
* Preserve IG as live authority while retaining append-only verified observations. Derived from: supplied research, Retrospective Trailing-Stops Evidence.
* Require Operator authorization for read and update. Derived from: supplied research, Technical Scenarios.
* Use Test/Live in new feature surfaces while preserving legacy provider-key compatibility. Derived from: supplied research, Environment Limitation.

## Context Summary

### Project Files

* `src/TNC.Trading.Platform.Application/Features/AccountDetails/IAccountDetailsGateway.cs:3-11` - application-owned provider port precedent.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgAccountDetailsGateway.cs:13-103` - session, retry, and error-redaction precedent.
* `src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs:29-50` - route registration and policy composition.
* `src/TNC.Trading.Platform.Web/Components/Pages/Configuration/Configuration.razor:1-232` - Operator settings interaction pattern.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/PlatformDbContext.cs:26-147` - EF composition point.

### References

* `.copilot-tracking/research/2026-08-17/trailing-stops-features-research.md` - planning synthesis.
* `.copilot-tracking/research/subagents/2026-08-17/trailing-stops-feature-plan-context-research.md` - verified architecture evidence.
* `https://labs.ig.com/reference/accounts-preferences.html` - provider contract.

## Implementation Checklist

### [x] Implementation Phase 1: Confirm Product Guards and Compatibility

<!-- parallelizable: false -->

* [x] Step 1.1: Configure and document 90-day observation retention, using the existing operational retention duration for initial deletion behavior; defer archive/export evaluation.
  * Details: `.copilot-tracking/details/2026-08-17/trailing-stops-features-details.md` (Lines 11-30)
* [x] Step 1.2: Implement and test backward-compatible legacy `Demo` provider-key to `Test` application-status mapping without rewriting historical records.
  * Details: `.copilot-tracking/details/2026-08-17/trailing-stops-features-details.md` (Lines 32-50)
* [x] Step 1.3: Confirm Test-only support, no real-trading or monetary-exposure authorization, and non-secret Test-account endpoint availability.
  * Details: `.copilot-tracking/details/2026-08-17/trailing-stops-features-details.md` (Lines 52-68)

### [x] Implementation Phase 2: Add Application Contracts and Behavior

<!-- parallelizable: false -->

* [x] Step 2.1: Create `Features/AccountPreferences` models, ports, typed outcomes, Get/Update/List requests, responses, handlers, and update validator.
  * Details: `.copilot-tracking/details/2026-08-17/trailing-stops-features-details.md` (Lines 72-99)
* [x] Step 2.2: Require PUT followed by authoritative GET before confirmed success; reconcile uncertain writes with GET and never blind-retry PUT.
  * Details: `.copilot-tracking/details/2026-08-17/trailing-stops-features-details.md` (Lines 101-117)
* [x] Step 2.3: Append a bounded, allow-listed operational event for each failed or indeterminate provider operation while never creating a false or nullable observation row.
  * Details: `.copilot-tracking/details/2026-08-17/trailing-stops-features-details.md` (Lines 119-132)
* [x] Step 2.4: Add handler and validator unit tests for Test behavior, typed failures, observations, repeated values, reconciliation, and sanitized failure telemetry.
  * Details: `.copilot-tracking/details/2026-08-17/trailing-stops-features-details.md` (Lines 134-151)
* [x] Step 2.5: Run the focused Application unit test command.

### [x] Implementation Phase 3: Add IG Boundary and Observation Persistence

<!-- parallelizable: false -->

* [x] Step 3.1: Implement/register `IgAccountPreferencesGateway` with Version 1, v2 session headers, strict parsing, one 401 replay, redaction, and typed outcomes.
  * Details: `.copilot-tracking/details/2026-08-17/trailing-stops-features-details.md` (Lines 140-159)
* [x] Step 3.2: Add append-only observation entity/store, DbContext mapping, additive migration, deterministic keyset history query, and approved retention behavior.
  * Details: `.copilot-tracking/details/2026-08-17/trailing-stops-features-details.md` (Lines 161-185)
* [x] Step 3.3: Add infrastructure unit and SQL integration tests for gateway, migration, ordering, persistence, and retention.
  * Details: `.copilot-tracking/details/2026-08-17/trailing-stops-features-details.md` (Lines 187-206)

### [x] Implementation Phase 4: Add Operator API and Web Experience

<!-- parallelizable: false -->

* [x] Step 4.1: Map Operator-only live GET/PUT routes with nullable Boolean input, validation, and stable Problem Details mappings.
  * Details: `.copilot-tracking/details/2026-08-17/trailing-stops-features-details.md` (Lines 215-234)
* [x] Step 4.2: Map an Operator-only history GET route with validated page size and deterministic keyset cursor response.
  * Details: `.copilot-tracking/details/2026-08-17/trailing-stops-features-details.md` (Lines 236-251)
* [x] Step 4.3: Add client models/calls, presenter, Operator page, navigation, live-state controls, feedback, and paged history.
  * Details: `.copilot-tracking/details/2026-08-17/trailing-stops-features-details.md` (Lines 253-274)
* [x] Step 4.4: Add API and bUnit coverage for policy, validation, mappings, loading, saving, feedback, cursor paging, and history.
  * Details: `.copilot-tracking/details/2026-08-17/trailing-stops-features-details.md` (Lines 276-294)

### [x] Implementation Phase 5: Document and Validate Delivery

<!-- parallelizable: false -->

* [x] Step 5.1: Update the affected wiki source-of-truth pages for API, operator workflow, runtime, architecture, and testing behavior.
  * Details: `.copilot-tracking/details/2026-08-17/trailing-stops-features-details.md` (Lines 276-293)
* [x] Step 5.2: Run focused tests, Docker-backed SQL integration tests, `dotnet build TNC.Trading.Platform.slnx`, and relevant solution tests; apply isolated fixes only.
  * Details: `.copilot-tracking/details/2026-08-17/trailing-stops-features-details.md` (Lines 295-313)
* [x] Step 5.3: Report blockers requiring new research, including retention, endpoint availability, or Live enablement.

## Planning Log

See `.copilot-tracking/plans/logs/2026-08-17/trailing-stops-features-log.md` for discrepancies, paths considered, and follow-on work.

## Dependencies

* Approved initial 90-day observation retention policy.
* Legacy Demo compatibility decision.
* Protected IG Test credentials and Docker-backed SQL integration-test environment.
* No automated test calls real IG.

## Success Criteria

* Operators can read and confirmed-update the live IG preference through a dedicated feature, without changing Account Details ownership.
* Verified observations are append-only, deterministic to query, and retained by approved policy; unknown provider state is never stored as false.
* Tests prove authorization, provider contract, persistence, error handling, and UI states without live broker dependencies.
* Wiki documentation reflects delivered API, runtime, operator, and quality behavior.