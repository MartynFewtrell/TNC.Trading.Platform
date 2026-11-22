# Deployment and Infrastructure

## Purpose

Document how the repository is built and distributed, based only on checked-in
configuration.

## Key points (TL;DR)

* This is a WPF desktop executable, not a hosted web application.
* No container, cloud infrastructure, installer, release workflow, or
  production deployment configuration is present.
* The root solution builds the WPF executable and client library.
* Travis CI configuration points at `IGWebApiClient.sln`, which is not present
  in the tracked repository listing.

## Details

The project output type for `SampleWPFTrader` is `WinExe`; the client is a
library. No `Dockerfile`, infrastructure-as-code tree, deployment scripts, or
GitHub Actions workflow is checked in. The README describes building and running
from Visual Studio rather than publishing an installer.

The root `.travis.yml` declares C# and names a solution file that does not match
the tracked solution filename. There is no evidence that the configured CI
workflow currently succeeds.

For a new project, define packaging, signing, update delivery, credential
provisioning, supported Windows/.NET runtime, and rollout strategy separately.
Those are not specified by this sample.

## Evidence (repo references)

* `SampleWPFTrader/SampleWPFTrader.csproj:7-13`: desktop executable output.
* `IGWebApiClient/IGWebApiClient.csproj:7-14`: class library output.
* `IGWebApiDotNetSample.sln`: root solution and project membership.
* `.travis.yml`: CI solution setting.
* Tracked repository inventory: no `infra/`, container, installer, or modern
  workflow files.

## Open questions / assumptions (if any)

* Assumption: The app was intended to be run manually from Visual Studio.
  * Missing evidence: No release or installer artifact.
  * Risk/impact: Distribution and updates must be designed for the replica.
* Assumption: Travis configuration may be stale.
  * Missing evidence: No CI run history or `IGWebApiClient.sln` in the repository.
  * Risk/impact: CI status cannot be inferred from this configuration.
