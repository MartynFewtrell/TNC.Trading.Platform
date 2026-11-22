---
description: 'Repair the failed test build, remediate vulnerable package closures, and validate a clean solution build.'
applyTo: 'global.json, src/**/*.csproj, test/**/*.csproj, test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/AccountPreferencesSqlIntegrationTests.cs'
---
<!-- markdownlint-disable-file -->
# Implementation Plan: Failed Build Remediation

## Overview

Repair the stale Infrastructure SQL integration-test call, update verified direct package roots to remove every vulnerable transitive closure, move off the preview SDK notice, and validate a literal zero-warning solution build.

## Delivery Controls

* Status: Approved for implementation.
* Inputs: User-attached failed-build-remediation-research.md; .copilot-tracking/research/2026-09-14/failed-build-remediation-research.md; .copilot-tracking/research/subagents/2026-09-14/failed-build-remediation-confirmation.md; .copilot-tracking/research/subagents/2026-09-14/failed-build-package-remediation.md.
* Delivery model: One coordinated remediation change with package-family upgrades sequenced before compatibility validation.
* Branching: Use the repository's normal feature branch and review process.
* Behavior-preservation boundary: Keep the current state-store interface and Entity Framework persistence semantics unchanged; preserve existing test and runtime behavior while upgrading dependency closures.
* Rollback/backout: Revert the specific package-family commit or the test-call change if its focused compatibility suite fails. Do not retain a compatibility overload, optional contract parameters, or warning suppression as a rollback mechanism.

## Objectives

### User Requirements

* Remediate the compiler error and package warnings as one piece of work to obtain a clean build. Source: user request, 2026-09-14.

### Derived Objectives

* Preserve the current seven-argument application-owned contract rather than adding legacy overloads or optional arguments. Derived from: .copilot-tracking/research/2026-09-14/failed-build-remediation-research.md (lines 128-168).
* Keep due work eligible for the test's existing claim assertion by passing a past-due timestamp. Derived from: .copilot-tracking/research/subagents/2026-09-14/failed-build-remediation-confirmation.md (lines 75-91).
* Upgrade verified direct package roots as coordinated families and prove their resolved transitive closures contain no vulnerable package. Derived from: .copilot-tracking/research/subagents/2026-09-14/failed-build-package-remediation.md (lines 85-160).
* Eliminate `NETSDK1057` by selecting a supported stable .NET 10 SDK after confirming its current version through Microsoft Learn. Derived from: package research lines 74-82 and repository SDK policy.

## Context Summary

### Project Files

* test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/AccountPreferencesSqlIntegrationTests.cs:240 - stale five-argument call that produces CS7036.
* src/TNC.Trading.Platform.Application/Features/AccountPreferences/IAccountPreferencesCurrentStateStore.cs:9 - current seven-argument port owned by the application layer.
* src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfAccountPreferencesCurrentStateStore.cs:28-33 - adapter signature and persistence mapping for snapshot ID and due time.
* test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/SqlServerDatabaseFixture.cs:16-26 - Docker-discovered SQL Server integration-test prerequisite.
* global.json:2-4 - preview SDK pin producing `NETSDK1057`.
* src/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.csproj; src/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.csproj; src/TNC.Trading.Platform.ServiceDefaults/TNC.Trading.Platform.ServiceDefaults.csproj - direct production package roots.
* test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/TNC.Trading.Platform.Api.IntegrationTests.csproj; test/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.UnitTests/TNC.Trading.Platform.AppHost.UnitTests.csproj; test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/TNC.Trading.Platform.Web.E2ETests.csproj; test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/TNC.Trading.Platform.Web.FunctionalTests.csproj; test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj - direct test package roots.

### References

* .copilot-tracking/research/2026-09-14/failed-build-remediation-research.md - primary diagnosis, warning inventory, and remediation recommendation.
* .copilot-tracking/research/subagents/2026-09-14/failed-build-remediation-confirmation.md - current contract confirmation, commands, and integration prerequisites.
* .copilot-tracking/research/subagents/2026-09-14/failed-build-package-remediation.md - verified targets, ownership chains, and package-specific compatibility risks.

### Standards References

* .github/copilot-instructions.md - use focused automated validation and preserve test naming conventions.
* .github/instructions/architecture.instructions.md - preserve inward-owned ports and existing adapter boundaries.
* .github/instructions/tests.instructions.md - ensure integration tests are deterministic and clean up external state.

## Execution Gates

| Gate | When | Required actions | If failures occur |
| --- | --- | --- | --- |
| Baseline | Before editing | Run the focused integration-test build to confirm the documented CS7036 failure. | Stop and investigate if the failure differs from the researched call-site mismatch. |
| Pre-completion | After all source, SDK, and package changes | Restore, verify zero vulnerable resolved packages, run the zero-warning solution build, then run focused and solution test suites. | Repair package-family compatibility failures within scope; back out the failing family if a safe local repair is not available. |

## Planned Work Items

| Work item | Description | Traceability | Dependencies | Validation | Rollback/Backout | User instructions |
| --- | --- | --- | --- | --- | --- | --- |
| WI-01: Restore test contract alignment | Replace the obsolete five-argument test call with the seven-argument contract shape. | UR-01; primary research lines 63-98. | Restored assets. | Focused build and filtered SQL test. | Revert the test-call change if it breaks due-work behavior. | Ensure Docker SQL Server is running before the filtered test. |
| WI-02: Upgrade the resolved package roots | Apply verified Aspire, ASP.NET/EF, WireMock, and AngleSharp package targets. | UR-01; package research lines 85-160. | WI-01 complete; stable SDK selected. | Restore plus vulnerability graph inspection. | Revert the incompatible package family as one change. | Make no package substitutions outside verified targets without repeat research. |
| WI-03: Eliminate preview SDK diagnostics | Replace the preview SDK pin with the Microsoft Learn-confirmed supported stable .NET 10 SDK. | UR-01; package research lines 74-82. | Supported SDK installed in the environment. | `dotnet --info`; zero-warning solution build. | Restore the prior global.json only if the stable SDK cannot build `net10.0`. | Install the selected SDK before running validation. |

## Implementation Checklist

### [x] Implementation Phase 1: Repair the stale test contract

<!-- parallelizable: false -->

* [x] Step 1.1: Replace the five-argument `NudgeAuthenticationAsync` invocation with the current seven-argument shape.
  * Details: .copilot-tracking/details/2026-09-14/failed-build-remediation-details.md (lines 8-35)
* [x] Step 1.2: Use an explicit `authentication-snapshot-1` and one past `authenticatedAtUtc` value for both timestamp parameters.
  * Details: .copilot-tracking/details/2026-09-14/failed-build-remediation-details.md (lines 8-35)
* [x] Step 1.3: Build the owning integration-test project with errors-only output.
  * Details: .copilot-tracking/details/2026-09-14/failed-build-remediation-details.md (lines 36-48)

### [x] Implementation Phase 2: Upgrade direct package roots

<!-- parallelizable: false -->

* [x] Step 2.1: Update `Aspire.AppHost.Sdk`, `Aspire.Hosting.SqlServer`, and every `Aspire.Hosting.Testing` root to `13.5.3`, including matching Keycloak `13.5.3-preview.1.26425.3`.
* [x] Step 2.2: Update direct ASP.NET Core and EF Core roots to `10.0.12`, update both WireMock roots to `2.15.0`, and add test-only `AngleSharp` `1.8.1`.
* [x] Step 2.3: Restore and verify no package is returned by the solution vulnerability graph.
  * Details: .copilot-tracking/details/2026-09-14/failed-build-remediation-details.md (lines 53-90)

### [x] Implementation Phase 3: Remove preview SDK diagnostics

<!-- parallelizable: false -->

* [x] Step 3.1: Confirm the latest supported .NET 10 SDK on Microsoft Learn and update global.json from preview `10.0.200`.
* [x] Step 3.2: Confirm the selected SDK is installed and eliminates `NETSDK1057`.
  * Details: .copilot-tracking/details/2026-09-14/failed-build-remediation-details.md (lines 93-114)

### [ ] Implementation Phase 4: Validate a clean build and compatibility

<!-- parallelizable: false -->

* [ ] Step 4.1: Run the zero-warning solution build and verify zero compiler and warning diagnostics.
* [ ] Step 4.2: Run the filtered SQL integration test, Web unit, functional, E2E, AppHost, API integration, and complete solution test suites.
* [ ] Step 4.3: Update relevant docs/wiki guidance if SDK or local dependency runtime behavior changes; otherwise record why a wiki update is not needed in the planning log.
  * Details: .copilot-tracking/details/2026-09-14/failed-build-remediation-details.md (lines 117-158)

## Planning Log

See .copilot-tracking/plans/logs/2026-09-14/failed-build-remediation-log.md for discrepancy tracking, implementation paths considered, and suggested follow-on work.

## Dependencies

* Supported stable .NET 10 SDK installed before updating global.json
* NuGet.org access for restore and package graph validation
* Docker and a running `mcr.microsoft.com/mssql/server:2022-latest` container for the filtered SQL integration test

## Success Criteria

* The integration-test project builds without CS7036 at `AccountPreferencesSqlIntegrationTests.cs:240`.
* `dotnet list TNC.Trading.Platform.slnx package --include-transitive --vulnerable` returns no vulnerable package.
* `dotnet build TNC.Trading.Platform.slnx --no-restore -warnaserror` succeeds with no diagnostics, including no `NETSDK1057`.
* Targeted compatibility suites and the full solution test suite pass when Docker prerequisites are available.

## Cross-Cutting Validation

* Restore and security: `dotnet restore TNC.Trading.Platform.slnx`; then `dotnet list TNC.Trading.Platform.slnx package --include-transitive --vulnerable`.
* Build: `dotnet build TNC.Trading.Platform.slnx --no-restore -warnaserror`.
* Tests: Run focused SQL, Web unit, functional/E2E, AppHost/API integration, then `dotnet test TNC.Trading.Platform.slnx --no-build`.
* Manual checks: Confirm upgraded Keycloak starts locally and Docker fixtures complete cleanup.

## Acceptance Checklist

* [ ] The stale invocation provides every current state-store parameter in the declared order.
* [ ] The past due timestamp preserves the due-work claim scenario.
* [ ] Restore graph contains no vulnerable packages and build output contains no warnings or information diagnostics.
* [ ] Focused and complete compatibility test suites pass in sequence.
* [ ] Relevant `docs/wiki/` guidance reflects any stable-SDK or local Keycloak behavior changes, or the plan records why none changed.
