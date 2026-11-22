# Deployment & infrastructure

## Purpose
Describe how this application is deployed and what infrastructure it requires.

## Key points (TL;DR)
- This repo is a **desktop WPF client**; there is no server component or infrastructure-as-code in-repo.
- “Deployment” is essentially distributing the WPF executable and its dependencies.
- Runtime dependencies include .NET Framework and referenced DLLs (NuGet packages and third-party Lightstreamer DLLs).

## Details
## Environments
- Runtime environment selection (`demo` vs `live`) is a client-side configuration value used to select the IG REST host.

## Packaging/distribution
- The project output type is `WinExe`.
- The project references `System.Deployment` (commonly used by ClickOnce), but the repo does not include explicit ClickOnce manifests/config in the examined files.

## Infrastructure
- None in-repo (no `infra/`, no containerization, no CI/CD workflow files detected).

## Evidence (repo references)
- `SampleWPFTrader/SampleWPFTrader.csproj` (lines 8–12): `OutputType` is `WinExe`, target framework is .NET Framework 4.6.1.
- `SampleWPFTrader/SampleWPFTrader.csproj` (line 72): references `System.Deployment`.
- `IGWebApiClient/IGRestService.cs` (lines 33–37): environment affects REST base URL.

## Open questions / assumptions (if any)
- Assumption: The intended deployment is a developer-run sample (manual run from Visual Studio) rather than a packaged installer.
  - Missing evidence: No installer projects or release pipeline.
  - Risk/impact: A reimplementation might need explicit packaging (MSIX/ClickOnce) if distribution is required.