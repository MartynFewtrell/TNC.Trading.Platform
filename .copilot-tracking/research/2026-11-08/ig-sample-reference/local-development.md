# Local development

## Purpose
Explain how to build and run the application locally, using only repo-contained evidence.

## Key points (TL;DR)
- Open `IGWebApiDotNetSample.sln` in Visual Studio and build/run `SampleWPFTrader`.
- Set `environment`, `username`, `password`, `apikey` in `SampleWPFTrader/App.config`.
- This is a .NET Framework WPF app (targeting .NET Framework 4.6.1).

## Details
## Prerequisites
- Visual Studio (the README suggests VS 2015 or later).
- .NET Framework developer tooling capable of building:
  - `SampleWPFTrader` targeting .NET Framework 4.6.1
  - `IGWebApiClient` targeting .NET 4.5 portable profile
- NuGet package restore for `packages.config` projects.

## Configure credentials
Edit `SampleWPFTrader/App.config` in the `IgWebApiConnection` section:
- `environment` = `demo` or `live`
- `username`
- `password`
- `apikey`

## Build and run (Visual Studio)
1. Open `IGWebApiDotNetSample.sln`.
2. Restore NuGet packages (if not automatic).
3. Ensure `SampleWPFTrader` is the startup project.
4. Build and run.

## Smoke test checklist
- Home tab shows status updates and accounts after login.
- Selecting each tab loads REST data (positions/orders/watchlists/browse).
- Streaming updates populate L1 prices and charts where streaming is enabled.

## Evidence (repo references)
- `README.md` (lines 13–33): getting started steps and config keys.
- `SampleWPFTrader/SampleWPFTrader.csproj` (lines 8–13): WPF app targeting .NET Framework 4.6.1.
- `IGWebApiDotNetSample.sln` (lines 6–9): solution contains both projects.

## Open questions / assumptions (if any)
- Assumption: NuGet restore will populate required packages and `packages/3rdPartyDlls` already contains required DLLs.
  - Missing evidence: No automated build script or CI definition exists.
  - Risk/impact: Build failures may require manual assembly reference repair (as suggested by README).