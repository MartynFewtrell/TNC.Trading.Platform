---
applyTo: '.copilot-tracking/changes/2026-09-17/aspire-13-5-upgrade-changes.md'
---
<!-- markdownlint-disable-file -->
# Implementation Plan: Aspire 13.5 Upgrade

## Overview

Upgrade the Aspire hosting and testing cohort, service direct advisory parents, and prove a warning-free distributed application build without changing product behavior.

## Objectives

### User Requirements

* Update the application from its current Aspire release to Aspire 13.5, remedy build warnings, and assess relevant features. Source: user-provided `.copilot-tracking/research/2026-09-17/aspire-13-5-upgrade-research.md`

### Derived Objectives

* Keep every direct Aspire reference on one compatible release train because mixed versions can fail at runtime. Derived from: research sections "External Research" and "Coordinated Upgrade and Clean Build"
* Preserve the composition-only AppHost, Aspire-orchestrated Keycloak, and ServiceDefaults service-discovery path. Derived from: research section "Technical Scenarios" and `.github/instructions/aspire.instructions.md`
* Prove advisory remediation through restore, build, dependency inspection, focused tests, and Docker-backed runtime checks without suppression. Derived from: research section "Runtime and Regression Validation"

## Context Summary

### Project Files

* `global.json:1-6` - Pins the `10.0.200` SDK required by the .NET 10 Aspire AppHost.
* `src/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.csproj:1-7` - Owns the AppHost SDK and hosting integration versions.
* `src/TNC.Trading.Platform.AppHost/AppHost.cs:3-11` - Composition-only AppHost entry point that must retain its topology.
* `src/TNC.Trading.Platform.AppHost/AppHostInfrastructureRegistration.cs:17-36` - Configures persistent SQL Server, Mailpit, Keycloak realm import, and stable Keycloak port.
* `src/TNC.Trading.Platform.ServiceDefaults/TNC.Trading.Platform.ServiceDefaults.csproj:13-17` - Owns Data Protection, EF Core, and OpenAPI advisory-parent versions.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj:15` - Owns the bUnit dependency requiring the temporary AngleSharp override.
* `test/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.UnitTests/AppHostEnvironmentWiringTests.cs:212,249,322-323` - Retains publish-mode environment inspection.

### References

* `.copilot-tracking/research/2026-09-17/aspire-13-5-upgrade-research.md` - Primary package, advisory, feature, and validation evidence.
* `.copilot-tracking/research/subagents/2026-09-17/aspire-13-5-upgrade-scope-research.md` - Verified package ownership and implementation scope.
* `https://aspire.dev/whats-new/aspire-13-5/` - Aspire 13.5 release changes.
* `https://aspire.dev/whats-new/upgrade-aspire/` - Publisher upgrade guidance.

### Standards References

* `.github/copilot-instructions.md` - .NET SDK, Keycloak, AppHost, and validation expectations.
* `.github/instructions/aspire.instructions.md` - AppHost composition and distributed runtime requirements.
* `.github/instructions/auth.instructions.md` - Local Keycloak, realm import, stable port, and OIDC requirements.
* `.github/instructions/aspire-tests.instructions.md` - Closed-box AppHost testing requirements.
* `.github/instructions/tests.instructions.md` - Unit-first validation and test-boundary requirements.

## Implementation Checklist

### [x] Implementation Phase 1: Update the Aspire Cohort

<!-- parallelizable: false -->

* [x] Step 1.1: Before editing, run `dotnet restore TNC.Trading.Platform.slnx` and record the resolved package graph and advisory warning baseline.
  * Details: `.copilot-tracking/details/2026-09-17/aspire-13-5-upgrade-details.md` (Lines 11-29)
* [x] Step 1.2: Update the AppHost SDK, SQL Server integration, Keycloak preview integration, and all four `Aspire.Hosting.Testing` declarations atomically to the researched 13.5 cohort.
  * Details: `.copilot-tracking/details/2026-09-17/aspire-13-5-upgrade-details.md` (Lines 30-60)
* [x] Step 1.3: Build the solution without another restore and inspect direct/transitive packages to detect cohort skew or remaining vulnerable versions.
  * Details: `.copilot-tracking/details/2026-09-17/aspire-13-5-upgrade-details.md` (Lines 61-72)

### [x] Implementation Phase 2: Service Advisory Parents

<!-- parallelizable: false -->

* [x] Step 2.1: Update direct OpenAPI, Data Protection, and paired EF Core package declarations from `10.0.5` to `10.0.12` only in identified package-owner projects.
  * Details: `.copilot-tracking/details/2026-09-17/aspire-13-5-upgrade-details.md` (Lines 78-104)
* [x] Step 2.2: Add a test-project-only `AngleSharp` direct reference at `1.5.0` or newer, documenting it as temporary until bUnit raises its patched minimum dependency.
  * Details: `.copilot-tracking/details/2026-09-17/aspire-13-5-upgrade-details.md` (Lines 105-127)
* [x] Step 2.3: Restore and build the solution, confirming no `NU1902`, `NU1903`, or `NU1904` warning is suppressed or remains.
  * Details: `.copilot-tracking/details/2026-09-17/aspire-13-5-upgrade-details.md` (Lines 128-148)

### [ ] Implementation Phase 3: Focused Regression Tests

<!-- parallelizable: false -->

* [ ] Step 3.1: Run the Web unit, AppHost unit, API integration, Web functional, and Web E2E projects in the staged unit-first order. Web unit (90/90) and AppHost unit (40/40) pass; Docker-backed suites remain blocked by Aspire startup cancellation and Keycloak readiness.
  * Details: `.copilot-tracking/details/2026-09-17/aspire-13-5-upgrade-details.md` (Lines 154-174)
* [x] Step 3.2: Investigate failures locally, preserving publish-mode AppHost environment inspection and avoiding product-behavior changes unless a verified Aspire compatibility change requires one. A test-only bUnit assertion compatibility repair was applied in `ConfigurationTests.cs`.
  * Details: `.copilot-tracking/details/2026-09-17/aspire-13-5-upgrade-details.md` (Lines 175-190)

### [ ] Implementation Phase 4: Distributed Runtime and Final Validation

<!-- parallelizable: false -->

* [x] Step 4.1: Update the Aspire CLI with `aspire update --self`, record `aspire --version`, and confirm the CLI is compatible with the Aspire 13.5 AppHost before runtime testing. CLI updated to 13.5.4.
  * Details: `.copilot-tracking/details/2026-09-17/aspire-13-5-upgrade-details.md` (Lines 196-208)
* [ ] Step 4.2: Start the system through Aspire with Docker available, update the VS Code Aspire extension, and verify its resource controls, logs, health, and dashboard behavior. Docker is available and Aspire reaches the dashboard, but full resource health verification remains incomplete after the concurrent fixed-port test-host conflict.
  * Details: `.copilot-tracking/details/2026-09-17/aspire-13-5-upgrade-details.md` (Lines 210-228)
* [ ] Step 4.3: Verify Keycloak realm import, stable authority/port, OIDC sign-in, persistent state, health endpoints, and Scalar/OpenAPI behavior. Keycloak realm import was observed in logs; authenticated browser and endpoint acceptance remain incomplete.
  * Details: `.copilot-tracking/details/2026-09-17/aspire-13-5-upgrade-details.md` (Lines 230-242)
* [x] Step 4.4: Record the Aspire 13.5 feature-assessment outcome: adopt VS Code extension validation and explicitly defer features without a concrete repository need.
  * Details: `.copilot-tracking/details/2026-09-17/aspire-13-5-upgrade-details.md` (Step 4.4)
* [ ] Step 4.5: Run the full solution build and test suite, including targeted `dotnet nuget why` checks, fix only isolated upgrade regressions, and document blockers that require new research. Build passed; full test execution was blocked by Keycloak fixed-port contention and readiness timeout.
  * Details: `.copilot-tracking/details/2026-09-17/aspire-13-5-upgrade-details.md` (Step 4.5)
* [ ] Step 4.6: Assign the release owner to obtain the security operations disposition for historical Data Protection exposure and key rotation when warranted.
  * Details: `.copilot-tracking/details/2026-09-17/aspire-13-5-upgrade-details.md` (Step 4.6)

## Planning Log

See `.copilot-tracking/plans/logs/2026-09-17/aspire-13-5-upgrade-log.md` for discrepancy tracking, implementation paths considered, and suggested follow-on work.

## Dependencies

* .NET SDK `10.0.200`, selected through `global.json`
* Docker Desktop or equivalent Docker runtime for AppHost integration and browser acceptance checks
* Aspire CLI compatible with Aspire 13.5 for the `aspire run` acceptance check
* Access to NuGet feeds to reconfirm current package availability before modifying project files

## Success Criteria

* All direct Aspire declarations resolve to `13.5.4`, except Keycloak resolving to `13.5.4-preview.1.26464.4`. Traces to: primary research "Current Package Baseline"
* Restore and build produce zero `NU1902`, `NU1903`, and `NU1904` warnings without `NoWarn`, audit-level changes, or advisory suppression. Traces to: primary research "Warning Remediation Map"
* Focused test projects and the final solution test suite pass, with publish-mode AppHost inspection retained. Traces to: primary research "Runtime and Regression Validation"
* Docker-backed Aspire runtime retains healthy resource topology, OIDC flow, Keycloak realm import, `https+http://api` discovery, health endpoints, and Scalar/OpenAPI. Traces to: primary research "Runtime and Regression Validation"
* The VS Code Aspire extension validation is adopted and every other assessed Aspire 13.5 capability has a recorded defer/adopt decision. Traces to: primary research "Aspire 13.5 Feature Assessment"
