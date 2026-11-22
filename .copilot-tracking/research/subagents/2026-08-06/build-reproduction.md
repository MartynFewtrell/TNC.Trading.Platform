---
title: Build Reproduction 2026-08-06
description: Verified investigation of the current solution build failure and its preliminary root cause
author: GitHub Copilot
ms.date: 2026-08-06
ms.topic: troubleshooting
---

# Build Reproduction

## Scope

Research only. No product files were edited. The investigation used the current worktree at `d:\Repos\TNC.Trading\TNC.Trading.Platform` and stopped at the first actionable build failure.

## Repository entry points

* `global.json:2-5` pins SDK version `10.0.200` and uses `latestFeature` roll-forward.
* `TNC.Trading.Platform.slnx:1-41` contains six product projects and the listed test projects.
* `src/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.csproj:1-19` is an SDK Web project targeting `net10.0`; it has project references to Application and ServiceDefaults.

The installed SDK reported by the reproduction command was `10.0.200`, matching `global.json`.

## Reproduction commands

All commands ran from the repository root in PowerShell.

```powershell
Set-Location 'd:\Repos\TNC.Trading\TNC.Trading.Platform'
git status --short
git diff --stat
dotnet --version
dotnet build .\TNC.Trading.Platform.slnx --nologo --verbosity:minimal
```

Observed results:

* `dotnet --version` returned `10.0.200`.
* The solution build exited with code `1`.
* The summary was `Build failed with 1 error(s) and 210 warning(s) in 5.5s`.
* Most warnings were NuGet vulnerability warnings. They did not explain the build stop.

The error-only reproduction was:

```powershell
Set-Location 'd:\Repos\TNC.Trading\TNC.Trading.Platform'
$output = dotnet build .\TNC.Trading.Platform.slnx --nologo --verbosity:minimal 2>&1
$exitCode = $LASTEXITCODE
$output | Where-Object { $_ -match '(?i)error|Build failed' }
"EXIT_CODE=$exitCode"
```

First and only actionable diagnostic:

```text
src\TNC.Trading.Platform.Web\obj\Debug\net10.0\Microsoft.CodeAnalysis.Razor.Compiler\Microsoft.NET.Sdk.Razor.SourceGenerators.RazorSourceGenerator\Components\Pages\Administration\Administration_razor.g.cs(133,33): error CS0111: Type 'Administration' already defines a member called 'BuildRenderTree' with the same parameter types [src\TNC.Trading.Platform.Web\TNC.Trading.Platform.Web.csproj]
Build FAILED.
1 Error(s)
EXIT_CODE=1
```

The generated `obj` path is diagnostic output, not the owning source. The owning source is the pair of Razor files documented below.

## Narrowing to the owning project

```powershell
Set-Location 'd:\Repos\TNC.Trading\TNC.Trading.Platform'
dotnet build '.\src\TNC.Trading.Platform.Web\TNC.Trading.Platform.Web.csproj' --nologo --verbosity:minimal 2>&1 |
  Where-Object { $_ -match '(?i)error|Build failed' }
"EXIT_CODE=$LASTEXITCODE"
```

This independently reproduced the same `CS0111` diagnostic for `Administration` and exited with code `1`. This rules out the solution file as the controlling failure.

## Source evidence

The worktree currently contains both of these paths:

* `src/TNC.Trading.Platform.Web/Components/Pages/Administration.razor`
* `src/TNC.Trading.Platform.Web/Components/Pages/Administration/Administration.razor`

The tracked parent file is modified from 63 lines to an empty file. A direct check reported `PARENT_LENGTH=3` and `PARENT_LINES=0`; its remaining bytes are the original file's line-ending residue. The untracked nested file contains the full page:

* `src/TNC.Trading.Platform.Web/Components/Pages/Administration/Administration.razor:1` declares namespace `TNC.Trading.Platform.Web.Components.Pages`.
* `src/TNC.Trading.Platform.Web/Components/Pages/Administration/Administration.razor:2` declares route `/administration/authentication`.
* `src/TNC.Trading.Platform.Web/Components/Pages/Administration/Administration.razor:7-9` defines the page title and heading.
* The nested filename `Administration.razor` causes Razor to generate the `Administration` component type in the same namespace as the parent file. The empty parent file still remains a Razor component input, so the compiler reports duplicate generated `BuildRenderTree` members.

The relevant worktree evidence from `git status --short` is:

```text
 M src/TNC.Trading.Platform.Web/Components/Pages/Administration.razor
 D src/TNC.Trading.Platform.Web/Components/Pages/Configuration.razor
 D src/TNC.Trading.Platform.Web/Components/Pages/ConfigurationFormModel.cs
 D src/TNC.Trading.Platform.Web/Components/Pages/ConfigurationFormModelMapper.cs
 D src/TNC.Trading.Platform.Web/Components/Pages/ConfigurationPagePresenter.cs
 D src/TNC.Trading.Platform.Web/Components/Pages/Home.razor
 D src/TNC.Trading.Platform.Web/Components/Pages/HomePagePresenter.cs
 D src/TNC.Trading.Platform.Web/Components/Pages/IgLoginHistory.razor
 D src/TNC.Trading.Platform.Web/Components/Pages/Status.razor
 D src/TNC.Trading.Platform.Web/Components/Pages/StatusPagePresenter.cs
?? src/TNC.Trading.Platform.Web/Components/Pages/Administration/
?? src/TNC.Trading.Platform.Web/Components/Pages/Configuration/
?? src/TNC.Trading.Platform.Web/Components/Pages/Home/
?? src/TNC.Trading.Platform.Web/Components/Pages/IgLogin/
?? src/TNC.Trading.Platform.Web/Components/Pages/Status/
```

This indicates an in-progress page reorganization. The current failure is specifically the Administration duplicate; the other page moves were not inspected because the request was to trace the first actionable diagnostic only.

## Preliminary root-cause recommendation

Complete the Administration page move so exactly one Razor file produces the `Administration` component in namespace `TNC.Trading.Platform.Web.Components.Pages`. The likely correction is to remove the stale parent `Components/Pages/Administration.razor` after confirming the nested page is the intended replacement, or rename/remove the nested component if the parent path is intended to remain. Do not keep both component inputs, even when one is empty.

## Cheapest discriminating validation

Without changing product files, the closest validation is the already-run Web-project build, which fails with the duplicate component. After the intended move is applied, the cheapest confirmation is:

```powershell
Set-Location 'd:\Repos\TNC.Trading\TNC.Trading.Platform'
dotnet build '.\src\TNC.Trading.Platform.Web\TNC.Trading.Platform.Web.csproj' --nologo --verbosity:minimal --no-restore
```

Expected result: exit code `0` and no `CS0111` for `Administration`. If the error remains, inspect the generated Razor inputs under `src/TNC.Trading.Platform.Web/obj/Debug/net10.0/.../Components/Pages/Administration/` and search the Web project for another `Administration.razor` or partial `Administration` type.

An additional non-mutating MSBuild item query was attempted:

```powershell
dotnet msbuild '.\src\TNC.Trading.Platform.Web\TNC.Trading.Platform.Web.csproj' -getItem:RazorComponent -nologo
```

It exited `0` but did not emit Administration item lines in this SDK/project configuration, so it was not used as evidence beyond confirming MSBuild itself can evaluate the project.

## Blockers and residual context

* The worktree is not clean and contains broad, pre-existing-looking page moves, deletions, and untracked directories. No changes were reverted or overwritten.
* The report does not claim the other page moves are build-safe because investigation stopped at the first compiler failure as requested.
* The build emitted 210 warnings, including package vulnerability warnings and nullable warnings. They are separate follow-up concerns after the duplicate Razor component is corrected.
