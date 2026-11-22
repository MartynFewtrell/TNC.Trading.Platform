<!-- markdownlint-disable-file -->
# Implementation Details: Failed Build and Package Remediation

## Context Reference

Sources: user-attached failed-build-remediation-research.md; .copilot-tracking/research/2026-09-14/failed-build-remediation-research.md; .copilot-tracking/research/subagents/2026-09-14/failed-build-remediation-confirmation.md; and .copilot-tracking/research/subagents/2026-09-14/failed-build-package-remediation.md.

## Implementation Phase 1: Repair the stale test contract

<!-- parallelizable: false -->

### Step 1.1: Supply the complete state-store event data

Modify only the stale call in `AccountPreferencesSqlIntegrationTests`. Declare `authenticatedAtUtc` as `DateTimeOffset.UtcNow.AddMinutes(-1)`. Pass `"authentication-snapshot-1"`, then pass `authenticatedAtUtc` as both the authenticated and due timestamps before `fixture.CancellationToken`.

Do not change `IAccountPreferencesCurrentStateStore`, `EfAccountPreferencesCurrentStateStore`, or the production handler. A compatibility overload or optional contract parameters would conceal incomplete event data and unnecessarily expand the application-owned port.

Files:
* test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/AccountPreferencesSqlIntegrationTests.cs - update the invocation at line 240.

Planning-log references:
* ER-01 - package-family compatibility is validated after the coordinated updates.

Success criteria:
* The call provides the snapshot ID, authenticated time, due time, and cancellation token in the interface order.
* The due timestamp remains in the past when `ClaimDueWorkAsync` executes.
* No production interface or adapter files change.

Context references:
* src/TNC.Trading.Platform.Application/Features/AccountPreferences/IAccountPreferencesCurrentStateStore.cs (line 9) - required parameter ordering.
* src/TNC.Trading.Platform.Application/Features/AccountPreferences/NudgeAccountPreferencesVerificationHandler.cs (line 11) - current production call shape.
* .copilot-tracking/research/subagents/2026-09-14/failed-build-remediation-confirmation.md (lines 69-91) - exact replacement rationale.

Dependencies:
* None.

### Step 1.2: Build the owning test project

Run the cheapest compiler-level check after the source edit. It must finish before running the filtered suite because that suite uses `--no-build`.

Validation commands:
* `dotnet build .\test\TNC.Trading.Platform.Infrastructure\TNC.Trading.Platform.Infrastructure.IntegrationTests\TNC.Trading.Platform.Infrastructure.IntegrationTests.csproj --no-restore -clp:ErrorsOnly` - verify the repaired test project compiles and `CS7036` is absent.

Success criteria:
* Command exits successfully with no compiler errors.
* NuGet advisory warnings, if printed, are reported separately rather than treated as the failed compiler repair.

## Implementation Phase 2: Upgrade direct package roots

<!-- parallelizable: false -->

### Step 2.1: Update Aspire and ASP.NET/EF package families

Update every direct `Aspire.AppHost.Sdk`, `Aspire.Hosting.SqlServer`, and `Aspire.Hosting.Testing` reference from `13.2.3` to `13.5.3`. Update the matching Keycloak package to `13.5.3-preview.1.26425.3`. This resolves the `MessagePack` closure through `StreamJsonRpc 2.25.29`.

Update direct `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore`, ASP.NET Core OpenAPI, and nearby EF Core package declarations from `10.0.5` to `10.0.12`. Keep the related .NET package family aligned in every affected project to avoid downgrade or conflict warnings.

Files:
* src/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.csproj - update AppHost SDK, Keycloak, and Aspire.Hosting.SqlServer as one coordinated family.
* src/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.csproj - update DataProtection and aligned EF Core roots.
* src/TNC.Trading.Platform.ServiceDefaults/TNC.Trading.Platform.ServiceDefaults.csproj - update DataProtection and OpenAPI roots.
* src/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.csproj - update ASP.NET Core OpenAPI root.
* test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/TNC.Trading.Platform.Infrastructure.IntegrationTests.csproj - update its direct DataProtection root.
* test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/TNC.Trading.Platform.Api.IntegrationTests.csproj; test/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.UnitTests/TNC.Trading.Platform.AppHost.UnitTests.csproj; test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/TNC.Trading.Platform.Web.E2ETests.csproj; test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/TNC.Trading.Platform.Web.FunctionalTests.csproj - update `Aspire.Hosting.Testing`.

Planning-log references:
* ER-01 - Keycloak prerelease and coordinated package compatibility require focused validation.

Success criteria:
* Restore resolves `MessagePack 2.5.302`, `Microsoft.OpenApi` at least `2.12.0`, DataProtection `10.0.12`, and System.Security.Cryptography.Xml `10.0.12` through direct-root updates.
* No direct package declaration remains on the vulnerable root versions.

Dependencies:
* Step 1.2 completion.

### Step 2.2: Update WireMock and add the scoped AngleSharp override

Update `WireMock.Net` from `1.6.10` to `2.15.0` in Web E2E and functional test projects. This removes the observed Scriban.Signed, System.Linq.Dynamic.Core, and legacy OpenAPI transitive paths. Add direct test-only `AngleSharp` `1.8.1` to Web.UnitTests, leaving `bunit.web` at its stable `1.40.0` root.

Files:
* test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/TNC.Trading.Platform.Web.E2ETests.csproj - update WireMock.Net.
* test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/TNC.Trading.Platform.Web.FunctionalTests.csproj - update WireMock.Net.
* test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj - add test-only AngleSharp override.

Success criteria:
* Restore selects Scriban.Signed `7.2.5` or above and removes the affected Dynamic LINQ chain.
* The direct AngleSharp override resolves at `1.8.1` without downgrade warnings.

Dependencies:
* Step 2.1 completion.

### Step 2.3: Restore and prove the package graph

Restore the solution after all package declarations change, then inspect the resolved transitive graph rather than relying on declared root versions alone.

Validation commands:
* `dotnet restore .\TNC.Trading.Platform.slnx` - resolve the updated package closure.
* `dotnet list .\TNC.Trading.Platform.slnx package --include-transitive --vulnerable` - prove no resolved vulnerable package remains.

Success criteria:
* Restore exits successfully.
* The vulnerability command lists no packages and emits no `NU1901` through `NU1904` warnings.

Dependencies:
* Steps 2.1 and 2.2 completion.

## Implementation Phase 3: Replace the preview SDK pin

<!-- parallelizable: false -->

### Step 3.1: Select and pin a supported stable .NET 10 SDK

Before editing global.json, use Microsoft Learn to confirm the current supported stable .NET 10 SDK version, then ensure it is installed. Replace preview `10.0.200` with that version while preserving the existing roll-forward intent unless SDK documentation requires a different policy.

Files:
* global.json - replace the preview SDK version responsible for `NETSDK1057`.

Success criteria:
* `dotnet --info` reports the selected stable SDK for the repository.
* Subsequent restore and build output contains no `NETSDK1057`.

Dependencies:
* Supported .NET 10 SDK installed locally.

## Implementation Phase 4: Validate a clean build and compatibility

<!-- parallelizable: false -->

### Step 4.1: Execute the literal clean-build gate

Build with warnings treated as errors only after the restore graph is proven clean and the stable SDK is selected.

Validation commands:
* `dotnet build .\TNC.Trading.Platform.slnx --no-restore -warnaserror` - require zero compiler errors and zero warnings or informational diagnostics.

Success criteria:
* The command exits successfully.
* Output contains no `CS7036`, `NU1901` through `NU1904`, or `NETSDK1057`.

Dependencies:
* Implementation Phases 1 through 3 completion.

### Step 4.2: Run focused compatibility suites and full tests

Run the changed SQL integration test first. Then run Web.UnitTests to exercise the bUnit override; Web functional and E2E tests to prove WireMock and Aspire/Keycloak compatibility; AppHost and API integration suites for Docker-backed hosting; and finally the complete solution test suite.

Validation commands:
* `dotnet test .\test\TNC.Trading.Platform.Infrastructure\TNC.Trading.Platform.Infrastructure.IntegrationTests\TNC.Trading.Platform.Infrastructure.IntegrationTests.csproj --no-build --no-restore --filter "FullyQualifiedName~AccountPreferencesSqlIntegrationTests"`
* `dotnet test .\test\TNC.Trading.Platform.Web\TNC.Trading.Platform.Web.UnitTests\TNC.Trading.Platform.Web.UnitTests.csproj --no-build`
* `dotnet test .\test\TNC.Trading.Platform.Web\TNC.Trading.Platform.Web.FunctionalTests\TNC.Trading.Platform.Web.FunctionalTests.csproj --no-build`
* `dotnet test .\test\TNC.Trading.Platform.Web\TNC.Trading.Platform.Web.E2ETests\TNC.Trading.Platform.Web.E2ETests.csproj --no-build`
* `dotnet test .\TNC.Trading.Platform.slnx --no-build`

Success criteria:
* All focused and full test runs pass.
* Docker fixtures discover required SQL Server and Keycloak containers and clean up temporary state.

Dependencies:
* Step 4.1 completion.
* Docker daemon running and fixture prerequisites available.

### Step 4.3: Update documentation or record no-doc decision

Review docs/wiki for stable SDK and local Keycloak operator guidance. Update affected pages if package or SDK changes alter documented setup or runtime behavior. If the upgrade preserves all documented commands and behavior, record the no-doc-change rationale in `.copilot-tracking/plans/logs/2026-09-14/failed-build-remediation-log.md`.

Success criteria:
* Documentation remains accurate for the selected stable SDK and local dependency behavior.


## Dependencies

* Microsoft Learn-confirmed supported stable .NET 10 SDK.
* Docker-backed SQL Server for the focused integration suite.
* Existing restored assets for commands that use `--no-restore`.

## Success Criteria

* The stale test call aligns with the seven-argument application contract.
* Restore graph has no vulnerable packages and the solution builds with `-warnaserror` and no diagnostics.
* Focused compatibility suites and the complete solution test suite validate the coordinated upgrades.
