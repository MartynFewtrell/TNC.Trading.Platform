---
applyTo: 'src/**/*.cs, src/**/*.razor, test/**/*.cs'
---
<!-- markdownlint-disable-file -->
# Implementation Plan: IG Login 403 Degraded Health

## Overview

Prevent undecryptable IG Demo credential rows from reaching IG login, then make the independently verified session protocol correction.

## Objectives

### User Requirements

* Resolve the IG login issue from the attached research. Source: user request and `.copilot-tracking/research/2026-08-06/ig-login-403-degraded-health-research.md`.
* Stop the refactor regression from presenting valid operator credentials as an IG `403` and degraded authentication. Source: research Executive Conclusion.

### Derived Objectives

* Separate secret-row presence from active-process decryptability without exposing secret material. Derived from: research Data Protection Regression Mechanism.
* Enforce the same credential-readiness rule for reconciliation and manual retry. Derived from: implementation-surface research lines 46-53.
* Keep the IG v2 and `Accept` correction separate until the exact v2 contract is confirmed. Derived from: research IG Request Compatibility Finding and Planning Log DD-01.

## Context Summary

### Project Files

* `src/TNC.Trading.Platform.Application/Configuration/IProtectedCredentialService.cs` owns the inner credential port.
* `src/TNC.Trading.Platform.Application/Configuration/CredentialPresence.cs` currently treats rows as complete.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Credentials/DataProtection/ProtectedCredentialService.cs` maps unprotect failure to an empty value.
* `src/TNC.Trading.Platform.Application/Features/ReconcilePlatformAuthentication/PlatformAuthenticationReconciler.cs` controls automatic authentication and degraded state.
* `src/TNC.Trading.Platform.Application/Features/TriggerManualAuthRetry/TriggerManualAuthRetryHandler.cs` has a direct broker-call bypass.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgBrokerAuthenticationGateway.cs` owns the Demo session request and proof calls.

### References

* `.copilot-tracking/research/2026-08-06/ig-login-403-degraded-health-research.md` establishes the incident diagnosis and operational recovery.
* `.copilot-tracking/research/subagents/2026-08-06/ig-login-implementation-surface-research.md` verifies current contracts, tests, and validation commands.
* <https://labs.ig.com/rest-trading-api-guide.html> documents IG request versions, media types, environments, and restricted diagnostics.

### Standards References

* `.github/copilot-instructions.md` requires protected configuration and focused validation.
* `.github/instructions/architecture.instructions.md` requires an inner-owned port and application-level policy decision.
* `.github/instructions/configuration.instructions.md` requires write-only secret handling.
* `.github/instructions/tests.instructions.md` requires documented deterministic xUnit tests.

## Implementation Checklist

### [x] Implementation Phase 1: Credential Usability Contract And Storage

<!-- parallelizable: false -->

* [x] Step 1.1: Extend the application credential projection with per-field redacted usability, aggregate readiness, and an aggregate re-entry signal while preserving constructor compatibility.
  * Details: `.copilot-tracking/details/2026-08-06/ig-login-403-degraded-health-details.md` (Lines 12-38)
* [x] Step 1.2: Make `ProtectedCredentialService` calculate usability from active-provider unprotect results and retain nonblank per-field reprotection.
  * Details: `.copilot-tracking/details/2026-08-06/ig-login-403-degraded-health-details.md` (Lines 39-56)
* [x] Step 1.3: Add unreadable, partial-save, and complete three-secret re-save infrastructure regression tests.
  * Details: `.copilot-tracking/details/2026-08-06/ig-login-403-degraded-health-details.md` (Lines 57-73)
* [x] Step 1.4: Run focused credential and retry-cycle tests.
  * Details: `.copilot-tracking/details/2026-08-06/ig-login-403-degraded-health-details.md` (Lines 74-79)

### [x] Implementation Phase 2: Authentication Guards And Remediation

<!-- parallelizable: false -->

* [x] Step 2.1: Replace presence-only automatic-authentication gates with the readiness predicate and persist a fixed redacted blocked reason before outbound calls.
  * Details: `.copilot-tracking/details/2026-08-06/ig-login-403-degraded-health-details.md` (Lines 84-106)
* [x] Step 2.2: Guard manual retry after its current intent commit, preserving the audit sequence while preventing the broker call.
  * Details: `.copilot-tracking/details/2026-08-06/ig-login-403-degraded-health-details.md` (Lines 107-123)
* [x] Step 2.3: Test automatic and manual paths for zero gateway calls, no automatic `AuthAttempted` event, and redacted degraded remediation.
  * Details: `.copilot-tracking/details/2026-08-06/ig-login-403-degraded-health-details.md` (Lines 124-140)
* [x] Step 2.4: Add two-field, allowlisted diagnostics for IG session `403` responses and carry them only in `FailureDetected` event details.
  * Details: `.copilot-tracking/details/2026-08-06/ig-login-403-degraded-health-details.md` (Lines 141-164)
* [x] Step 2.5: Add sentinel-driven gateway, event-persistence, manual-retry, and port-contract diagnostics tests.
  * Details: `.copilot-tracking/details/2026-08-06/ig-login-403-degraded-health-details.md` (Lines 165-184)
* [x] Step 2.6: Run focused infrastructure and application retry/diagnostic tests.
  * Details: `.copilot-tracking/details/2026-08-06/ig-login-403-degraded-health-details.md` (Lines 186-193)

### [ ] Implementation Phase 3: Configuration Projection And Operator Recovery

<!-- parallelizable: false -->

* [x] Step 3.1: Project per-field safe usability and aggregate re-entry state through configuration API mappings and Web view models.
  * Details: `.copilot-tracking/details/2026-08-06/ig-login-403-degraded-health-details.md` (Lines 198-222)
* [x] Step 3.2: Add an operator message requiring all three Demo secrets in one write-only save; preserve blank secret form values.
  * Details: `.copilot-tracking/details/2026-08-06/ig-login-403-degraded-health-details.md` (Lines 223-240)
* [x] Step 3.3: Add API mapping and bUnit message behavior coverage, including no-secret rendering assertions.
  * Details: `.copilot-tracking/details/2026-08-06/ig-login-403-degraded-health-details.md` (Lines 241-265)
* [ ] Step 3.4: Validate a complete active-key-ring re-save using only usability booleans and existing `SecretsUpdated` audit metadata.
  * Details: `.copilot-tracking/details/2026-08-06/ig-login-403-degraded-health-details.md` (Lines 266-281)
* [x] Step 3.5: Run focused API/Web behavior tests and build the API and Web projects.
  * Details: `.copilot-tracking/details/2026-08-06/ig-login-403-degraded-health-details.md` (Lines 282-290)

### [x] Implementation Phase 4: IG Session Protocol Follow-Up

<!-- parallelizable: false -->

* [x] Step 4.1: Confirm the exact IG `/session` v2 contract before code change.
  * Details: `.copilot-tracking/details/2026-08-06/ig-login-403-degraded-health-details.md` (Lines 295-314)
* [x] Step 4.2: Change only the session request to `Version: 2` and explicit JSON `Accept`, retaining the CST/header-token proof flow.
  * Details: `.copilot-tracking/details/2026-08-06/ig-login-403-degraded-health-details.md` (Lines 315-330)
* [x] Step 4.3: Add request-capture coverage for headers, JSON content, and unchanged proof calls.
  * Details: `.copilot-tracking/details/2026-08-06/ig-login-403-degraded-health-details.md` (Lines 331-346)
* [x] Step 4.4: Run focused gateway tests.
  * Details: `.copilot-tracking/details/2026-08-06/ig-login-403-degraded-health-details.md` (Lines 347-352)

### [ ] Implementation Phase 5: Final Validation And Operational Verification

<!-- parallelizable: false -->

* [x] Step 5.1: Run focused suites, solution build, and solution tests; isolate any pre-existing build failure.
  * Details: `.copilot-tracking/details/2026-08-06/ig-login-403-degraded-health-details.md` (Lines 357-365)
* [ ] Step 5.2: Confirm the actual `403` is the IG session route, then perform one full three-secret re-save when remediation is required.
  * Details: `.copilot-tracking/details/2026-08-06/ig-login-403-degraded-health-details.md` (Lines 366-369)
* [ ] Step 5.3: For a remaining provider `403`, capture only allowlisted IG diagnostics.
  * Details: `.copilot-tracking/details/2026-08-06/ig-login-403-degraded-health-details.md` (Lines 370-373)

## Planning Log

See `.copilot-tracking/plans/logs/2026-08-06/ig-login-403-degraded-health-log.md` for discrepancies, selected path, and follow-on work.

## Dependencies

* .NET SDK `10.0.200` from `global.json` and restored dependencies.
* Active SQL-backed Data Protection key-ring access for operational re-save verification.
* Authorized IG Demo account/API key and the IG REST reference for post-deployment checks.

## Success Criteria

* An unreadable credential row cannot call IG, emits no automatic `AuthAttempted`, and persists redacted remediation. Traces to: research Scenario 1.
* Complete nonblank three-secret re-save produces usable values from a fresh provider using the shared key ring. Traces to: research test recommendations.
* Automatic reconciliation and manual retry use the same readiness predicate. Traces to: subagent research lines 46-53.
* API/UI remediation never returns secret values, ciphertext, exceptions, or key material. Traces to: configuration instructions.
* A session `403` carries only `errorCode` and `X-REQUEST-ID` into failure-event details, with sentinel tests proving no broader provider data crosses the boundary. Traces to: research Scenario 3.
* The optional protocol correction is tested as v2 plus JSON `Accept` while preserving header-token proof requests. Traces to: research Scenario 2.