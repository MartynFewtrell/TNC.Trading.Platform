---
title: Failed Build Package Remediation Research
description: Evidence-based NuGet vulnerability ownership and remediation research for the failed-build objective
author: GitHub Copilot
ms.date: 2026-09-14
ms.topic: troubleshooting
keywords:
  - NuGet
  - vulnerability remediation
  - package ownership
  - build validation
---

## Research scope

Investigate all NuGet vulnerability warnings in the current solution. Determine direct
and transitive ownership, project/package paths, verifiable non-vulnerable target
versions, central package management status, and the build and test validation needed
to meet the zero-warning clean-build objective.

## Status

Complete. No product, test, or project files have been modified.

## Evidence log

* `global.json:2-4` pins SDK `10.0.200` with `latestFeature` roll-forward.
* `TNC.Trading.Platform.slnx:7-34` includes 16 source and test projects.
* `dotnet build TNC.Trading.Platform.slnx --no-restore -v:minimal` failed with
  `CS7036` at
  `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/AccountPreferencesSqlIntegrationTests.cs:240`.
  It also reported 127 warnings, including NuGet vulnerability warnings and the
  informational `NETSDK1057` preview-SDK notice.
* `dotnet list TNC.Trading.Platform.slnx package --include-transitive --vulnerable`
  restored successfully with 127 vulnerability warnings. It classified every listed
  vulnerable dependency as transitive.
* No `Directory.Packages.props`, `NuGet.config`, or `Directory.Build.*` package
  management file exists in the workspace. `dotnet nuget list source` reports
  enabled `nuget.org` and Visual Studio Offline Packages. Individual
  `PackageReference` elements are the version owners.
* Restore-asset chain extraction confirmed
  `Aspire.Hosting.AppHost/13.2.3 -> Aspire.Hosting/13.2.3 -> StreamJsonRpc/2.22.23 -> MessagePack/2.5.192`.
* NuGet.org's vulnerability base and current update feeds were queried on
  2026-09-14. Package manifests were inspected in `%TEMP%/tnc-package-research`,
  outside the workspace, to validate proposed root upgrade closures.

## Affected package paths

All paths below resolve as transitive packages in the affected project.

| Vulnerable package | Resolved version | Affected projects | Immediate path and direct owner |
| --- | --- | --- | --- |
| `Microsoft.OpenApi` | `2.0.0` | Api, ServiceDefaults, Web, Api.UnitTests, AppHost.UnitTests, Infrastructure.IntegrationTests, Web.UnitTests | `Microsoft.AspNetCore.OpenApi/10.0.5 -> Microsoft.OpenApi/2.0.0`; direct declarations are Api `TNC.Trading.Platform.Api.csproj:11` and ServiceDefaults `TNC.Trading.Platform.ServiceDefaults.csproj:17` |
| `Microsoft.AspNetCore.DataProtection` | `10.0.5` | Api.UnitTests, AppHost.UnitTests, Infrastructure.IntegrationTests, Infrastructure.UnitTests | `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore/10.0.5 -> Microsoft.AspNetCore.DataProtection/10.0.5`; direct declarations are Infrastructure `TNC.Trading.Platform.Infrastructure.csproj:20`, ServiceDefaults `TNC.Trading.Platform.ServiceDefaults.csproj:13`, and Infrastructure.IntegrationTests `TNC.Trading.Platform.Infrastructure.IntegrationTests.csproj:12` |
| `System.Security.Cryptography.Xml` | `10.0.5` | Api.UnitTests, AppHost.UnitTests, Infrastructure.IntegrationTests, Infrastructure.UnitTests | `Microsoft.AspNetCore.DataProtection/10.0.5 -> System.Security.Cryptography.Xml/10.0.5`; controlled by the DataProtection.EntityFrameworkCore roots above |
| `MessagePack` | `2.5.192` | AppHost, Api.IntegrationTests, AppHost.UnitTests, Web.E2ETests, Web.FunctionalTests | `Aspire.Hosting.AppHost/13.2.3 -> Aspire.Hosting/13.2.3 -> StreamJsonRpc/2.22.23 -> MessagePack/2.5.192`; AppHost uses `Aspire.AppHost.Sdk/13.2.3` at `TNC.Trading.Platform.AppHost.csproj:1`, while three tests directly use `Aspire.Hosting.Testing/13.2.3` at Api.IntegrationTests `:11`, AppHost.UnitTests `:11`, Web.E2ETests `:11`, and Web.FunctionalTests `:11` |
| `Scriban.Signed` | `5.5.0` | Web.E2ETests, Web.FunctionalTests | `WireMock.Net/1.6.10 -> Scriban.Signed/5.5.0`; direct owners are Web.E2ETests `TNC.Trading.Platform.Web.E2ETests.csproj:14` and Web.FunctionalTests `TNC.Trading.Platform.Web.FunctionalTests.csproj:14` |
| `System.Linq.Dynamic.Core` | `1.3.12` | Web.E2ETests, Web.FunctionalTests | `Handlebars.Net.Helpers.DynamicLinq/2.4.6 -> System.Linq.Dynamic.Core/1.3.12`; this dependency is introduced by the same direct `WireMock.Net/1.6.10` roots |
| `AngleSharp` | `1.2.0` | Web.UnitTests | `bunit.web/1.40.0 -> AngleSharp.Diffing/1.0.0 -> AngleSharp/1.2.0` (also `AngleSharp.Css/1.0.0-beta.154 -> AngleSharp/1.2.0`); direct owner is Web.UnitTests `TNC.Trading.Platform.Web.UnitTests.csproj:15` |

## Findings

* Twelve of the 16 solution projects restore one or more vulnerable packages. The
  Application, Infrastructure, Application.UnitTests, and Architecture.IntegrationTests
  projects have none in the current restore graph.
* No vulnerable package has a direct `PackageReference`; remediation should upgrade
  the direct roots first. Adding explicit transitive overrides is a fallback only when
  the root package has no compatible safe release.
* The compiler failure is independent of NuGet. The test call has five arguments at
  `AccountPreferencesSqlIntegrationTests.cs:240`, but the implementation requires
  seven at `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfAccountPreferencesCurrentStateStore.cs:28`.
* A clean build for this objective means zero compiler errors and zero `NU190x`
  warnings. `NETSDK1057` is an informational SDK notice, not a NuGet vulnerability;
  it must be separately eliminated only if the acceptance gate literally requires no
  warning or informational diagnostics.

## Remediation approach

### Phase 1: Restore coherence

1. Update the Aspire AppHost SDK, Keycloak, SQL Server, and all
  `Aspire.Hosting.Testing` declarations as one `13.5.3` family.
2. Update direct ASP.NET Core and EF Core declarations from `10.0.5` to `10.0.12`.
3. Update both direct `WireMock.Net` declarations to `2.15.0`.
4. Add the scoped `AngleSharp/1.8.1` test-only override because bUnit has no newer
  stable root package.

### Phase 2: Compile and package proof

1. Repair the independent `NudgeAuthenticationAsync` test call mismatch before
  judging the solution build.
2. Run `dotnet restore TNC.Trading.Platform.slnx`.
3. Run `dotnet list TNC.Trading.Platform.slnx package --include-transitive --vulnerable`.
  It must list no vulnerable packages in any project.
4. Run `dotnet build TNC.Trading.Platform.slnx --no-restore -warnaserror`.
  It must have zero compiler errors and zero warnings, including no `NU190x`.

### Phase 3: Compatibility validation

1. Run `dotnet test TNC.Trading.Platform.slnx --no-build` after the build gate.
2. Run Web.UnitTests to exercise the bUnit and AngleSharp override.
3. Run Web.FunctionalTests and Web.E2ETests with Docker available to validate
  WireMock 2.x and upgraded Aspire/Keycloak paths.
4. Run AppHost and API integration tests with their Docker-backed dependencies.

## Clean-build interpretation

The expanded objective requires a successful solution build with zero compiler errors
and no NuGet vulnerability warnings (`NU1901` through `NU1904`). `NETSDK1057` is an
informational preview-SDK notice from `global.json:2-4`; it neither causes the current
failure nor appears in the warning count. Eliminate it only if acceptance means literal
zero diagnostics, which requires moving to a supported stable SDK.

## Open constraints

* The current SDK and package graph are preview-era inputs. Prove compatibility against
  `net10.0` after each coordinated package-family update.
* Keycloak must use the matching published `13.5.3-preview.1.26425.3` version and pass
  local container startup validation.
* `WireMock.Net 2.x` may require test source updates.
* bUnit has no newer stable root than `1.40.0`; the AngleSharp update is a transitive
  override requiring focused runtime validation.
* `dotnet list ... --outdated` remains unsuitable as sole upgrade evidence because it
  terminates with `Sequence contains no matching element` after restore.

## Verified package targets

| Remediation path | Target and evidence | Compatibility assessment |
| --- | --- | --- |
| Aspire and MessagePack | Upgrade the AppHost SDK and `Aspire.Hosting.Testing` roots from `13.2.3` to `13.5.3`. `Aspire.Hosting/13.5.3` declares `StreamJsonRpc/2.25.29`, which declares `MessagePack/2.5.302`. NuGet's affected ranges end before `2.5.301`. Direct owners: AppHost `TNC.Trading.Platform.AppHost.csproj:1,4-5`; test roots at Api.IntegrationTests `:11`, AppHost.UnitTests `:11`, Web.E2ETests `:11`, and Web.FunctionalTests `:11`. | Coordinated version family. Keycloak remains prerelease, so restore and run its matching `13.5.3-preview.1.26425.3` package before acceptance. |
| Data Protection and XML | Upgrade direct `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore` declarations to `10.0.12`. Its manifest pins `Microsoft.AspNetCore.DataProtection/10.0.12`; NuGet marks Data Protection `[10.0.0, 10.0.6]` and XML advisory ranges through `10.0.9` as affected. Direct owners: Infrastructure `:20`, ServiceDefaults `:13`, Infrastructure.IntegrationTests `:12`. | Same `net10.0` package family. Align nearby direct EF Core `10.0.5` declarations to `10.0.12` to avoid downgrade/conflict warnings. |
| ASP.NET Core OpenAPI | Upgrade `Microsoft.AspNetCore.OpenApi` from `10.0.5` to `10.0.12`. Its manifest declares `Microsoft.OpenApi [2.12.0, 3.0.0)`, above NuGet's affected 2.x interval ending at `2.7.4`. Direct owners: Api `TNC.Trading.Platform.Api.csproj:11`, ServiceDefaults `TNC.Trading.Platform.ServiceDefaults.csproj:17`. | Manifest-verified. Do not add a separate OpenAPI override unless restore resolves below `2.12.0`. |
| WireMock test dependencies | Upgrade `WireMock.Net` from `1.6.10` to `2.15.0` in Web.E2ETests `TNC.Trading.Platform.Web.E2ETests.csproj:14` and Web.FunctionalTests `TNC.Trading.Platform.Web.FunctionalTests.csproj:14`. Its `WireMock.Net.Minimal/2.15.0` manifest declares `Scriban.Signed/7.2.5`, above ranges ending at `7.2.0`, and removes the observed Dynamic LINQ and legacy OpenAPI path. | Major-version upgrade. Compile and run both test projects. |
| bUnit and AngleSharp | `bunit.web/1.40.0` is the newest stable bUnit package but declares AngleSharp `1.2.0`. NuGet marks versions below `1.5.0` as vulnerable. Add a direct test-only `AngleSharp` override at stable `1.8.1` in Web.UnitTests. | The NuGet minimum-version declaration allows the override, but runtime validation is required. |

Safe lower bounds for fallback explicit overrides are `MessagePack 2.5.301` or
`3.1.7`, `Scriban.Signed 7.2.1`, `System.Linq.Dynamic.Core 1.6.0`,
`Microsoft.OpenApi 2.7.5`, `Microsoft.AspNetCore.DataProtection 10.0.7`,
`System.Security.Cryptography.Xml 10.0.10`, and `AngleSharp 1.5.0`.