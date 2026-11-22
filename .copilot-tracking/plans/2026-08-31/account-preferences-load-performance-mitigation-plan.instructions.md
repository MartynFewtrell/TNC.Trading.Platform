---
description: 'Plans the account preferences load-delay and false provider-unavailable mitigation.'
applyTo: '.copilot-tracking/changes/2026-08-31/account-preferences-load-performance-mitigation-changes.md'
---
<!-- markdownlint-disable-file -->
# Implementation Plan: Account Preferences Load Performance Mitigation

## Overview

Correct the fresh-database account preferences contract and replace broad HTTP retries so the page promptly renders an accurate unconfigured state without masking genuine dependency failures.

## Objectives

### User Requirements

* Mitigate the issues identified in the account preferences load-performance research. Source: user request and `.copilot-tracking/research/2026-08-31/account-preferences-load-performance-research.md` (Lines 3-18)
* Reduce the 5 to 10 second initial page delay. Source: `.copilot-tracking/research/2026-08-31/account-preferences-load-performance-research.md` (Lines 111-124)
* Prevent a fresh database from being reported as an unavailable preferences provider without hiding genuine failures. Source: `.copilot-tracking/research/2026-08-31/account-preferences-load-performance-research.md` (Lines 194-231)

### Derived Objectives

* Represent configured and unconfigured SQL query results explicitly in Application. Derived from: null current state is valid, while the current dual-field response cannot distinguish absence from a provider failure.
* Make API status and Problem Details mappings exhaustive. Derived from: unhandled deterministic categories currently fall through to a retryable 503 response.
* Replace the inherited standard resilience pipeline for `PlatformApiClient` with one testable client-specific pipeline. Derived from: the default handler retries deterministic 503 responses and can replay unsafe operations.
* Isolate fresh-state closed-box tests from persistent SQL volumes and shared provider request history. Derived from: retained SQL rows and an append-only ledger can mask the defect.
* Preserve current state-before-history sequencing until healthy latency measurements justify progressive rendering. Derived from: concurrency does not fix the false warning or repeated current-state attempts.
* Update affected wiki pages before completion. Derived from: repository documentation is the implemented-behavior source of truth.

## Context Summary

### Project Files

* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/GetAccountPreferencesHandler.cs` - Converts a missing SQL projection to a legacy failure.
* `src/TNC.Trading.Platform.Application/Features/AccountPreferences/GetAccountPreferencesResponse.cs` - Carries ambiguous legacy outcome and nullable state fields.
* `src/TNC.Trading.Platform.Api/Features/Platform/GetAccountPreferencesEndpointHandler.cs` - Discards SQL state when producing the HTTP result.
* `src/TNC.Trading.Platform.Api/Features/Platform/AccountPreferencesEndpointMapping.cs` - Maps most failure categories to the provider-unavailable 503 fallback.
* `src/TNC.Trading.Platform.Web/Program.cs` - Registers `PlatformApiClient` without overriding global resilience defaults.
* `src/TNC.Trading.Platform.AppHost/AppHostInfrastructureRegistration.cs` - Always gives SQL a persistent lifetime and data volume.
* `test/Shared/AccountPreferences/ControllableIgProvider.cs` - Exposes a shared append-only request ledger.
* `docs/wiki/` - Holds the required implementation and operator documentation.

### References

* `.copilot-tracking/research/2026-08-31/account-preferences-load-performance-research.md` - Primary root-cause and mitigation research.
* `.copilot-tracking/research/subagents/2026-08-31/account-preferences-mitigation-plan-verification-research.md` - Current path, symbol, test, documentation, and validation verification.
* `https://learn.microsoft.com/dotnet/core/resilience/http-resilience` - HTTP resilience configuration semantics.
* `https://aspire.dev/testing/manage-app-host/` - Aspire AppHost testing guidance.
* `https://aspire.dev/fundamentals/persist-data-volumes/` - Aspire volume persistence behavior.

### Standards References

* `.github/copilot-instructions.md` - Requires feature-local ownership, documented tests, and affected wiki updates.
* `.github/instructions/architecture.instructions.md` - Assigns query meaning to Application and HTTP translation to API.
* `.github/instructions/csharp.instructions.md` - Requires explicit nullable outcomes and RFC 9457 Problem Details.
* `.github/instructions/aspire-tests.instructions.md` - Requires deterministic closed-box test isolation.
* `.github/instructions/playwright.instructions.md` - Requires web-first assertions and accessibility-first locators.
* `.github/instructions/tests.instructions.md` - Defines test naming, documentation, and isolation conventions.

## Implementation Checklist

### [x] Implementation Phase 1: Define the Application query contract

<!-- parallelizable: false -->

* [x] Step 1.1: Add an explicit configured or unconfigured account preferences query-state type and update the response contract.
  * Details: `.copilot-tracking/details/2026-08-31/account-preferences-load-performance-mitigation-details.md` (Lines 12-34)
* [x] Step 1.2: Update the handler to return successful unconfigured state for a missing SQL row and configured state for a populated row without calling IG.
  * Details: `.copilot-tracking/details/2026-08-31/account-preferences-load-performance-mitigation-details.md` (Lines 35-53)
* [x] Step 1.3: Add Application tests for missing and populated current-state rows and run the focused Application suite.
  * Details: `.copilot-tracking/details/2026-08-31/account-preferences-load-performance-mitigation-details.md` (Lines 54-74)

### [x] Implementation Phase 2: Correct the API contract

<!-- parallelizable: false -->

* [x] Step 2.1: Map configured and unconfigured query states to explicit successful API responses.
  * Details: `.copilot-tracking/details/2026-08-31/account-preferences-load-performance-mitigation-details.md` (Lines 79-101)
* [x] Step 2.2: Replace the broad failure fallback with exhaustive status mappings and correct overloaded IG gateway classifications.
  * Details: `.copilot-tracking/details/2026-08-31/account-preferences-load-performance-mitigation-details.md` (Lines 102-141)
* [x] Step 2.3: Add endpoint-handler, exhaustive Problem Details, retry-signaling, and gateway-classification tests.
  * Details: `.copilot-tracking/details/2026-08-31/account-preferences-load-performance-mitigation-details.md` (Lines 142-168)

### [x] Implementation Phase 3: Specialize Web transport resilience

<!-- parallelizable: true -->

* [x] Step 3.1: Extract `PlatformApiClient` registration into a reusable Web composition extension and replace inherited resilience handlers.
  * Details: `.copilot-tracking/details/2026-08-31/account-preferences-load-performance-mitigation-details.md` (Lines 173-192)
* [x] Step 3.2: Configure retries for safe transport failures and selected safe statuses, require `Retry-After` for 429 and 503, and disable unsafe-method retries.
  * Details: `.copilot-tracking/details/2026-08-31/account-preferences-load-performance-mitigation-details.md` (Lines 193-215)
* [x] Step 3.3: Add registration-level physical-attempt tests and run the focused Web unit suite.
  * Details: `.copilot-tracking/details/2026-08-31/account-preferences-load-performance-mitigation-details.md` (Lines 216-237)

### [x] Implementation Phase 4: Make closed-box state deterministic

<!-- parallelizable: true -->

* [x] Step 4.1: Add `AppHost:UsePersistentSqlState`, retaining persistence locally and omitting both volume and persistent lifetime in test mode.
  * Details: `.copilot-tracking/details/2026-08-31/account-preferences-load-performance-mitigation-details.md` (Lines 242-263)
* [x] Step 4.2: Disable SQL persistence in both shared AppHost test start paths and add AppHost unit coverage.
  * Details: `.copilot-tracking/details/2026-08-31/account-preferences-load-performance-mitigation-details.md` (Lines 264-283)
* [x] Step 4.3: Add a lock-protected provider-ledger reset operation and repair the shared WireMock compile blocker before focused validation.
  * Details: `.copilot-tracking/details/2026-08-31/account-preferences-load-performance-mitigation-details.md` (Lines 284-306)

### [ ] Implementation Phase 5: Prove user-visible behavior

<!-- parallelizable: false -->

* [x] Step 5.1: Add a successful unconfigured presenter or bUnit rendering test without changing state-before-history sequencing.
  * Details: `.copilot-tracking/details/2026-08-31/account-preferences-load-performance-mitigation-details.md` (Lines 311-328)
* [x] Step 5.2: Add a fresh-install functional test proving 200 Unconfigured and no initial IG preferences request.
  * Details: `.copilot-tracking/details/2026-08-31/account-preferences-load-performance-mitigation-details.md` (Lines 329-346)
* [x] Step 5.3: Strengthen the E2E smoke test to prove loading completes, Unconfigured is visible, and the provider-unavailable alert is absent.
  * Details: `.copilot-tracking/details/2026-08-31/account-preferences-load-performance-mitigation-details.md` (Lines 347-369)

### [ ] Implementation Phase 6: Align documentation

<!-- parallelizable: false -->

* [ ] Step 6.1: Update API, runtime, operator, local-development, testing, and affected architecture wiki pages with the delivered contract and evidence.
  * Details: `.copilot-tracking/details/2026-08-31/account-preferences-load-performance-mitigation-details.md` (Lines 374-400)
* [ ] Step 6.2: Validate wiki links and remove claims not proven by the final test suite.
  * Details: `.copilot-tracking/details/2026-08-31/account-preferences-load-performance-mitigation-details.md` (Lines 401-418)

### [ ] Implementation Phase 7: Final validation and runtime proof

<!-- parallelizable: false -->

* [ ] Step 7.1: Run focused suites, repairing the pre-existing Infrastructure test call-site blocker before using that suite as a gate.
  * Details: `.copilot-tracking/details/2026-08-31/account-preferences-load-performance-mitigation-details.md` (Lines 423-446)
* [ ] Step 7.2: Run solution build and test validation, recording unrelated package advisories separately.
  * Details: `.copilot-tracking/details/2026-08-31/account-preferences-load-performance-mitigation-details.md` (Lines 447-461)
* [ ] Step 7.3: Capture an Aspire trace proving one current-state request, one subsequent history request, and no IG request from the initial GET.
  * Details: `.copilot-tracking/details/2026-08-31/account-preferences-load-performance-mitigation-details.md` (Lines 462-478)
* [ ] Step 7.4: Fix isolated validation defects and report blockers requiring new research rather than widening this implementation.
  * Details: `.copilot-tracking/details/2026-08-31/account-preferences-load-performance-mitigation-details.md` (Lines 479-489)

## Parallelization Rationale

* Phase 3 can run alongside Phases 1 and 2 because it modifies Web composition and transport tests rather than Application or API contracts.
* Phase 4 can run alongside Phases 1 through 3 because it modifies AppHost and shared test infrastructure; its compile-blocker repair is confined to the shared provider test double.
* Phase 5 is sequential because its functional and E2E assertions depend on the corrected contract, transport policy, and deterministic harness.
* Phases 6 and 7 are sequential because documentation and final proof must describe and validate the delivered behavior.

## Planning Log

See `.copilot-tracking/plans/logs/2026-08-31/account-preferences-load-performance-mitigation-log.md` for discrepancy tracking, implementation paths considered, decisions, and suggested follow-on work.

## Dependencies

* .NET SDK selected by `global.json`
* Docker with Aspire-managed SQL Server, Keycloak, API, Web, and controllable IG resources
* `Microsoft.Extensions.Http.Resilience` APIs resolved by the Web project
* SQL Server integration-test connection for the Infrastructure suite
* Playwright browser dependencies for E2E validation

## Success Criteria

* A fresh migrated database returns HTTP 200 with explicit Unconfigured state and causes zero IG preferences GET calls. Traces to: user requirement and research fresh-install contract.
* The initial page leaves loading state and renders Unconfigured without a provider-unavailable warning. Traces to: user requirement.
* An unsignaled deterministic 503 produces one physical attempt, while selected safe transient failures remain retryable and unsafe methods are never automatically replayed. Traces to: retry-amplification root cause.
* Every account preferences failure category has an intentional HTTP mapping and stable Problem Details semantics. Traces to: API boundary research.
* Closed-box tests use ephemeral SQL state and reset provider request history per scenario. Traces to: test-isolation research.
* Focused and solution-level validation passes, or pre-existing and unrelated blockers are explicitly reported with evidence. Traces to: repository quality conventions.
* A post-fix Aspire trace proves one current-state request, one history request, and no initial IG request. Traces to: research Potential Next Research.
* Affected `docs/wiki/` pages match the implemented API, runtime, operator, local-development, and test behavior. Traces to: repository documentation convention.