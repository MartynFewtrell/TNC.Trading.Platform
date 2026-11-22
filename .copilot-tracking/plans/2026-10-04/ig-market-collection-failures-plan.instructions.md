---
applyTo: '.copilot-tracking/changes/2026-10-04/ig-market-collection-failures-changes.md'
---
<!-- markdownlint-disable-file -->
# Implementation Plan: IG market collection failures

## Overview

Diagnose the 4 October IG Demo category and detail failures, retain fail-closed pagination, and make safe, persisted failure classifications visible to operators.

## Objectives

### User requirements

* Plan the diagnosis and fix of current collection issues. Source: user request and `.copilot-tracking/research/2026-10-04/ig-market-collection-failures-research.md` (lines 5-25).

### Derived objectives

* Correlate quota-window and pagination evidence before changing retry/validation behavior. Demo credential handling is explicitly out of scope for this plan. Derived from research (lines 100-140) and user clarification.
* Preserve distinct saved success, failed attempt, and market-detail coverage while safely propagating available diagnostic fields across persistence, API and UI. Derived from research (lines 89-108, 175-177) and saved-status trace (lines 6-28).
* Verify later-run recovery, redaction and bounded telemetry without assuming all HTTP 403 results mean quota or entitlement. Derived from research (lines 99-108, 142-167).

## Context summary

### Project files

* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgMarketCategoryInstrumentsGateway.cs:133-180,236-250` enforces page integrity and bounded replay.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgMarketDetailsGateway.cs:29-37,551-598` classifies session allowance versus unknown 403.
* `src/TNC.Trading.Platform.Application/Features/MarketCategoryInstruments/MarketCategoryInstrumentCycleCoordinator.cs:713-745,862-880` logs detailed category errors but persists only a safe error.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfMarketCategoryInstrumentStatusReader.cs:45-104` reads attempts separately from last successful snapshots.
* `src/TNC.Trading.Platform.Web/Components/Pages/MarketCategories/MarketCategories.razor:81-144` renders saved category status; `src/TNC.Trading.Platform.Web/Components/Pages/MarketCategories/MarketDetails.razor:56-80` renders separate detail coverage.
* `docs/wiki/operator-guide.md:101-139,480-497` describes existing operator interpretation and allowance limitations.

### References and standards

* `.copilot-tracking/research/2026-10-04/ig-market-collection-failures-research.md` (lines 27-70, 87-175), primary incident analysis and preferred response.
* `.copilot-tracking/research/subagents/2026-10-04/saved-status-contract-research.md` (lines 6-28), persisted/read/API/UI seam and test inventory.
* `.github/copilot-instructions.md`, `.github/instructions/architecture.instructions.md`, `.github/instructions/auth.instructions.md`, `.github/instructions/configuration.instructions.md`, `.github/instructions/csharp.instructions.md`, `.github/instructions/tests.instructions.md`, `.github/instructions/aspire-tests.instructions.md`, `.github/instructions/docs.instructions.md` apply to implementation in their respective scopes. `https://aspire.dev/` is the primary Aspire reference when needed.

## Implementation checklist

### [x] Phase 1: Diagnose and contain

<!-- parallelizable: false -->

* [x] Step 1.1: Confirm that Demo credential handling is managed in-house and exclude key rotation or secret-store changes from this plan. Use only redacted telemetry and fixtures for diagnosis. Details: `.copilot-tracking/details/2026-10-04/ig-market-collection-failures-details.md` (lines 12-18).
* [x] Step 1.2: Correlate 403 and later success with allowance counters/reset; characterize commodities page metadata and distinguish the independent captures. Details: same details file (lines 20-26). Existing sanitized research establishes the reported provider allowance code and independently captured page shape; allowance counters/reset and a complete live timeline remain unavailable and are not inferred.
* [x] Gate: preserve fail-closed pagination and distinguish known allowance codes from unknown access denials; live provider state was not queried.

### [x] Phase 2: Confirm commodities behavior

<!-- parallelizable: true -->

* [x] Step 2.1: Test 70-of-150 with both coherent (successful) and inconsistent (failed) bounded replay; test the independent expected-70/actual-42 mismatch and publish boundary. Details: same details file (lines 32-38).
* [x] Step 2.2: Run focused gateway tests. Details: same details file (lines 40-42). Both gateway classes passed in the Infrastructure unit-test project.
Can proceed alongside phase 3 and offline phase 1. Do not run concurrent commands sharing build outputs.

### [x] Phase 3: Confirm allowance and access behavior

<!-- parallelizable: true -->

* [x] Step 3.1: Test exact known allowance 403, unknown/entitlement-style 403, 429, no immediate retry and later scheduled recovery. Details: same details file (lines 48-54).
* [x] Step 3.2: Run focused detail gateway tests. Details: same details file (lines 56-58). Gateway and scheduled-recovery tests passed in their unit-test projects.
Can proceed alongside phase 2 and offline phase 1; edit different gateway/test files.

### [x] Phase 4: Propagate safe saved diagnostics

<!-- parallelizable: false -->

* [x] Step 4.1: Introduce nullable, allow-listed attempt/target diagnostics and EF migration without fabricating historical data. Details: same details file (lines 64-70).
* [x] Step 4.2: Carry the safe fields through saved readers, API responses, view-models and category/detail UI without conflating category and target failures. Details: same details file (lines 72-78).
* [x] Step 4.3: Add low-cardinality metrics, accurate warning text and cross-layer SQL/API/UI regression tests. Details: same details file (lines 80-86).
* [x] Step 4.4: Update `docs/wiki/operator-guide.md` with implemented behavior and incident response. Details: same details file (lines 88-91).
Depends on phases 2 and 3; shared migration, models and validation prevent independent implementation. Live Demo execution depends on the user's in-house credential management and allowance investigation, while offline development does not.

### [ ] Phase 5: Validate and hand off

<!-- parallelizable: false -->

* [x] Step 5.1: Run focused suites then full project build, lint and tests, including SQL upgrade/fresh creation; fix isolated regressions. Details: same details file (lines 97-99). Full solution build passed with 0 warnings; all 1,040 solution tests passed with `-m:1`; SQL migration tests and EF idempotent script generation passed. New files pass targeted formatting; whole-solution formatting reports repository-wide existing diagnostics and is recorded in the planning log.
* [ ] Step 5.2: Reset the application data through the approved local reset path so the next application startup has no prior collection state suppressing the market-data download attempt. Preserve a recoverable backup or export first, confirm the reset scope excludes credentials and unrelated configuration, and verify startup logs show the scheduled collection beginning. Details: `.copilot-tracking/details/2026-10-04/ig-market-collection-failures-details.md` (lines 101-107). The user authorized editing data instead of destroying the database. A checksum-verified, copy-only backup of local `platformdb` was saved outside the container. The guarded IG Demo slot script reopened 2026-10-04 slot 0: the cycle and two category attempts are pending, slot coverage is clear, the seven consumed requests and historical full run remain intact. Startup scheduling has not been verified because starting the API would initiate live Demo calls before in-house credential/allowance readiness is confirmed.
* [ ] Step 5.3: Run a controlled Demo collection when the in-house operator confirms credentials are ready; prove conditional denials and next-slot recovery with deterministic tests, and report what the live run did not exercise. Details: same details file (lines 109-111). Blocked: in-house Demo credential readiness and suitable allowance conditions were not confirmed; no live provider call was made.
* [x] Record provider-dependent uncertainties and unrun environment checks; re-plan changes that exceed minor corrections. Live provider evidence and startup verification remain unavailable; the scoped slot reset and verified backup are recorded without changing provider policy.

## Planning log

See `.copilot-tracking/plans/logs/2026-10-04/ig-market-collection-failures-log.md` for discrepancy decisions, alternatives and follow-on work.

## Dependencies

* In-house Demo credential management and sanitized observability records; provider allowance data may be unavailable. No key rotation or secret-store implementation is planned here.
* Approved application-data reset procedure, backup/export location, and a disposable or recoverable local database. Do not use destructive commands against shared or production data.
* Existing .NET SDK/build, EF migration tooling and SQL Server; Docker-backed SQL and Aspire/Keycloak integration environment where tests require them.
* Use the existing test projects and repository scripts; check project path and test-platform flags before running commands.

## Success criteria

* A coherent replay following the observed 70-of-150 initial page succeeds, while inconsistent replay or the independent expected-70/actual-42 mismatch never publishes as complete. Traces to research (lines 29-35, 58-60, 100-102, 114-115).
* Known allowance 403 is distinguished from unknown 403 and 429 without aggressive retries; the next eligible scheduled attempt can succeed. Traces to research (lines 37-44, 103-108).
* Current failed attempt, prior successful collection and detail coverage remain independently readable in SQL/API/UI after restart, with exact allow-listed detail code and older rows with null diagnostic fields. Traces to saved-status trace (lines 6-28).
* No provider body, raw key or account identifier enters saved diagnostics, regular logs or metric labels; documentation matches the delivered operator flow. Credential rotation remains an external operational responsibility. Traces to research (lines 111-116, 142-167).
