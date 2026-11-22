<!-- markdownlint-disable-file -->
---
title: Build Warnings Research
description: Evidence-based investigation of current solution build warnings
author: GitHub Copilot
ms.date: 2026-09-20
ms.topic: troubleshooting
keywords:
  - dotnet
  - build
  - warnings
---

## Research Scope

Investigate current `dotnet build` warnings in the workspace, capture exact diagnostics,
and recommend one resolution per warning without modifying product files.

## Command Results

The following serial command completed successfully on 2026-09-20 using SDK `10.0.302`
from `global.json`:

```powershell
dotnet build .\TNC.Trading.Platform.slnx --no-restore -m:1 -v:minimal
```

Result: `Build succeeded with 2 warning(s) in 5.6s`. All other solution projects
completed without warnings.

Dependency-path and availability checks used the following commands:

```powershell
dotnet nuget why .\test\TNC.Trading.Platform.Infrastructure\TNC.Trading.Platform.Infrastructure.IntegrationTests\TNC.Trading.Platform.Infrastructure.IntegrationTests.csproj SSH.NET --framework net10.0
dotnet nuget why .\test\TNC.Trading.Platform.Web\TNC.Trading.Platform.Web.UnitTests\TNC.Trading.Platform.Web.UnitTests.csproj AngleSharp --framework net10.0
dotnet list .\test\TNC.Trading.Platform.Infrastructure\TNC.Trading.Platform.Infrastructure.IntegrationTests\TNC.Trading.Platform.Infrastructure.IntegrationTests.csproj package --outdated --include-transitive
dotnet list .\test\TNC.Trading.Platform.Web\TNC.Trading.Platform.Web.UnitTests\TNC.Trading.Platform.Web.UnitTests.csproj package --outdated --include-transitive
```

## Warning Findings

### NU1903: SSH.NET 2024.2.0

* Build diagnostic: `test\TNC.Trading.Platform.Infrastructure\TNC.Trading.Platform.Infrastructure.IntegrationTests\TNC.Trading.Platform.Infrastructure.IntegrationTests.csproj : warning NU1903: Package 'SSH.NET' 2024.2.0 has a known high severity vulnerability, https://github.com/advisories/GHSA-q939-rpr3-3284`
* Build location: the diagnostic is attached to the project file and has no source line. Its controlling direct dependency is `Testcontainers.MsSql` at `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/TNC.Trading.Platform.Infrastructure.IntegrationTests.csproj:16`.
* Resolved dependency path: `Testcontainers.MsSql 4.8.1 -> Testcontainers 4.8.1 -> SSH.NET 2024.2.0`.
* Root cause: Testcontainers 4.8.1 requests SSH.NET 2024.2.0. The GitHub advisory [GHSA-q939-rpr3-3284](https://github.com/advisories/GHSA-q939-rpr3-3284) affects versions `<= 2025.1.0` and is fixed in `2026.0.0`. It permits a server-controlled recursive SCP download filename to write outside the requested local directory.
* Recommended resolution: upgrade `Testcontainers.MsSql` from `4.8.1` to `4.15.0` in the integration-test project, then restore, inspect the resolved graph, and run that test project. This upgrades the owning package and its companion `Testcontainers` package rather than forcing an untested leaf override.
* Alternative: add a direct `SSH.NET` reference at `2026.0.0` to override the transitive version. This is the smallest package-graph change, but it may be incompatible with Testcontainers' SSH use and leaves the parent package behind.
* Risk: Testcontainers 4.15.0 updates its dependency graph and could require test-fixture adjustments. A direct SSH.NET 2026.0.0 override has a potentially larger API/behavioral compatibility risk because it spans the 2025 to 2026 package line.

### NU1902: AngleSharp 1.2.0

* Build diagnostic: `test\TNC.Trading.Platform.Web\TNC.Trading.Platform.Web.UnitTests\TNC.Trading.Platform.Web.UnitTests.csproj : warning NU1902: Package 'AngleSharp' 1.2.0 has a known moderate severity vulnerability, https://github.com/advisories/GHSA-pgww-w46g-26qg`
* Build location: the diagnostic is attached to the project file and has no source line. Its controlling direct dependency is `bunit.web` at `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj:11`.
* Resolved dependency paths: `bunit.web 1.40.0 -> AngleSharp 1.2.0`; `bunit.web 1.40.0 -> AngleSharp.Css 1.0.0-beta.154 -> AngleSharp 1.2.0`; and `bunit.web 1.40.0 -> AngleSharp.Diffing 1.0.0 -> AngleSharp 1.2.0`.
* Root cause: bunit.web 1.40.0 requests AngleSharp 1.2.0. The GitHub advisory [GHSA-pgww-w46g-26qg](https://github.com/advisories/GHSA-pgww-w46g-26qg) affects versions `< 1.5.0` and is fixed in `1.5.0`. It can allow an mXSS parser/serializer bypass involving MathML `annotation-xml` HTML integration points.
* Recommended resolution: add a direct `AngleSharp` package reference at `1.5.0` to the web unit-test project, then restore and run the web unit-test project. This is the lowest patched release and bunit.web's package metadata specifies no upper bound for AngleSharp.
* Alternative: pin a newer AngleSharp release, currently `1.8.2` according to the outdated-package check, or wait for a bUnit release that updates its declared AngleSharp dependencies. The former increases change scope; the latter retains a known vulnerability until released.
* Risk: bunit.web 1.40.0 also requests older AngleSharp.Css and AngleSharp.Diffing packages. Although its AngleSharp declaration has no upper bound, the direct override must be validated with the component test suite for compile- and runtime compatibility.

## Applicable Guidance

* `.github/instructions/tests.instructions.md` applies to both affected test projects: changes must retain isolated, deterministic test boundaries and validate with `dotnet test`.
* `.github/instructions/csharp.instructions.md` identifies `dotnet build` and `dotnet test` as the standard validation commands. No C# source changes are proposed.
* `global.json` pins SDK `10.0.302` with `latestPatch` roll-forward; the build used that SDK.

No central package-management file (`Directory.Packages.props`) or repository `NuGet.Config` exists. The affected package versions are therefore maintained in the respective project files.

## Recommended Implementation Steps

1. Update `Testcontainers.MsSql` to `4.15.0` in `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/TNC.Trading.Platform.Infrastructure.IntegrationTests.csproj`.
2. Add `PackageReference Include="AngleSharp" Version="1.5.0"` to `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj`.
3. Run `dotnet restore`, then `dotnet build .\TNC.Trading.Platform.slnx -m:1 -v:minimal`; expected result is zero NU1902/NU1903 warnings.
4. Run `dotnet test` for the two changed test projects. Exercise any test fixtures that establish Testcontainers connections, and run the bUnit suite to validate the AngleSharp override.

## Follow-On Questions

None. The research identifies a complete, reproducible two-warning build set and
conservative remediation paths.