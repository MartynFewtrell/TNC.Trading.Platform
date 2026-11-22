# Local Development

## Purpose

Provide source-supported setup guidance and report build limitations observed
in this workspace.

## Key points (TL;DR)

* The documented workflow is to open `IGWebApiDotNetSample.sln` in Visual Studio,
  restore legacy NuGet dependencies, configure IG credentials, then build and
  run the WPF project.
* The WPF project targets .NET Framework 4.6.1; the client is a Portable Class
  Library Profile259 targeting .NET 4.5.
* A `dotnet build` attempt failed because the .NET Framework targeting pack and
  legacy Portable Class Library targets are unavailable in this environment.
* No test project or test command is present.

## Details

### Prerequisites

The README recommends Visual Studio 2015 or later. A compatible Windows Visual
Studio/MSBuild installation with the .NET Framework 4.6.1 Developer Pack and
legacy PCL tooling is required for this historical solution. Restore NuGet
packages declared in the two `packages.config` files. The repository also
contains some third-party DLLs under `packages/3rdPartyDlls`.

### Setup and run

1. Open `IGWebApiDotNetSample.sln` in a compatible Visual Studio installation.
2. Restore NuGet packages for the solution.
3. Set `environment` to `demo` and configure valid credentials and API key in
   `SampleWPFTrader/App.config`. Keep real credentials out of commits.
4. Build the solution and start `SampleWPFTrader`.
5. Authenticate against the intended IG environment. Begin with a demo account.

The nested `SampleWPFTrader/SampleWPFTrader.sln` contains only the WPF project,
while the root solution is the one that lists both projects. Prefer the root
solution when building the repository.

### Validation in this workspace

`dotnet build IGWebApiDotNetSample.sln --no-restore` was attempted. It failed
because the .NET Framework 4.6.1 reference assemblies were unavailable and
`Microsoft.Portable\v4.5\Microsoft.Portable.CSharp.targets` was missing. This is
an environment/toolchain limitation; no source files were changed to work
around it.

## Evidence (repo references)

* `README.md`: Visual Studio and configuration steps.
* `IGWebApiDotNetSample.sln`: complete solution entry point.
* `SampleWPFTrader/SampleWPFTrader.csproj:12-13`: WPF target framework.
* `IGWebApiClient/IGWebApiClient.csproj:12-18,263`: PCL target/profile and
  legacy build targets.
* `IGWebApiClient/packages.config` and `SampleWPFTrader/packages.config`:
  dependency manifests.
* Build output for the command above: missing framework targeting pack and PCL
  targets.

## Open questions / assumptions (if any)

* Assumption: A compatible Visual Studio installation can build the original
  project files after package restore.
  * Missing evidence: No successful build log or CI artifact was available.
  * Risk/impact: The repository may require additional old SDKs or package
    versions beyond those declared.
