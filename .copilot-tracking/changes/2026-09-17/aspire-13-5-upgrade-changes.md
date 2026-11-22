<!-- markdownlint-disable-file -->
# Release Changes: Aspire 13.5 Upgrade

**Related Plan**: aspire-13-5-upgrade-plan.instructions.md
**Implementation Date**: 2026-09-17

## Summary

Implementation tracking for the Aspire 13.5 upgrade, advisory remediation, and distributed runtime validation.

## Changes

### Added

* Temporary `AngleSharp` 1.5.0 override in the Web unit-test project to remediate the bUnit dependency advisory.

### Modified

* Updated the AppHost, Keycloak preview integration, and four Aspire testing references to the 13.5.4 cohort.
* Updated confirmed OpenAPI, Data Protection, and EF Core advisory parents to 10.0.12.
* Updated AppHost test configuration packages to 10.0.11 for Aspire 13.5 restore compatibility.
* Updated the infrastructure integration test `Microsoft.Data.SqlClient` dependency to 6.1.6 for EF Core 10.0.12 compatibility.
* Updated Web unit-test bUnit assertions to enumerate collection results while preserving the existing assertions and behavior.

### Removed

* None.

## Additional or Deviating Changes

* Aspire 13.5 restore required the AppHost test configuration package cohort at 10.0.11; the original plan did not list these packages.
* EF Core 10.0.12 required `Microsoft.Data.SqlClient` 6.1.6 in the infrastructure integration test project; this was limited to the affected test dependency.

## Validation

* Restore and solution build passed.
* Aspire resolves to 13.5.4, Keycloak to 13.5.4-preview.1.26464.4, and MessagePack to 2.5.302.
* AngleSharp resolves to 1.5.0, Microsoft.OpenApi to 2.12.0, and System.Security.Cryptography.Xml to 10.0.12.
* No NU1902, NU1903, or NU1904 warnings remain.
* Web unit tests passed 90/90 and AppHost unit tests passed 40/40.
* API integration, Web functional, and Web E2E suites remain blocked by Docker-backed Aspire startup cancellation and Keycloak readiness failures.

## Runtime Validation

* Aspire CLI self-update completed on the stable channel at version 13.5.4.
* Docker Desktop is available and an isolated `aspire run --non-interactive` reached the Aspire dashboard.
* Keycloak realm import was observed in the AppHost logs.
* Full resource health, OIDC browser flow, Scalar/OpenAPI acceptance, and full solution test completion remain pending because concurrent AppHost instances contended for the required fixed Keycloak port 8080; the test process then timed out waiting for Keycloak readiness.

## Feature Assessment

* Adopted VS Code/Aspire extension and dashboard validation for this upgrade.
* Deferred custom resource commands, file input, experimental terminals, certificate/progress interaction APIs, TypeScript AppHost, Azure/Kubernetes/Radius additions, Redis modules, and CLI bundling because no concrete repository need was identified.

## Release Summary

The package upgrade and advisory remediation are complete. The solution restores and builds without NU1902, NU1903, or NU1904 warnings, with Aspire 13.5.4 and MessagePack 2.5.302 resolved. Focused unit coverage passes, while Docker-backed regression and user-facing runtime acceptance remain blocked by fixed-port contention and require a clean single-AppHost test run.

## Release Summary

Pending completion of the implementation phases.
