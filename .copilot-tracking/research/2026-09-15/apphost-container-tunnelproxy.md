---
title: AppHost tunnelproxy research
description: Evidence about Aspire-created Docker networking and recent AppHost failures
ms.date: 2026-09-15
---

# AppHost tunnelproxy research

## Questions
- Why does running AppHost create `aspire-container-network-tunnelproxy`?
- Is it expected Aspire behavior or test-only infrastructure?
- Which recent code/config changes may explain it and other application failures?

## Findings

### Verified topology

* `src/TNC.Trading.Platform.AppHost/AppHost.cs:3-14` creates the distributed application and runs it; it does not add a tunnelproxy resource.
* `src/TNC.Trading.Platform.AppHost/AppHostInfrastructureRegistration.cs:22-52` registers only SQL, Mailpit, and Keycloak as infrastructure. The explicit container is `mailpit` at line 32; there is no tunnelproxy registration.
* `src/TNC.Trading.Platform.AppHost/AppHostProjectRegistration.cs:12-48` registers only `api` and `web`.
* `test/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.UnitTests/AppHostCompositionTests.cs:44-50` asserts the complete expected resource set: `sql`, `platformdb`, `mailpit`, `keycloak`, `api`, and `web`.
* Therefore `aspire-container-network-tunnelproxy` is not test-only application infrastructure and is not authored by this repository. Its name and appearance only when Docker-backed resources are run are consistent with an Aspire/DCP internal networking helper.

### Lifecycle and shutdown

* `artifacts/apphost-stdout.log:1-10` records Aspire `13.2.3`, dashboard startup, and a clean `Distributed application started` message. `artifacts/apphost-stderr.log` is empty.
* The AppHost source gives SQL a persistent lifetime by default and Mailpit a persistent lifetime at `AppHostInfrastructureRegistration.cs:25-35`; Keycloak is persistent unless configured otherwise at lines 41-45. A tunnelproxy disappearing on shutdown is therefore not evidence that these repository resources were accidentally made ephemeral.
* `test/Shared/Authentication/ManagedAppHostFixture.cs:71-117` explicitly stops and disposes the managed distributed application and then its builder. `test/Shared/Authentication/AppHostProcessHandle.cs:179-230` performs equivalent test cleanup for either a child process or managed application. A framework-owned helper disappearing during this disposal is expected cleanup behavior.

### Timeline

* `52bfba4` on 2026-09-14 updated SDK versions and Keycloak URLs to HTTPS.
* `3ef847e` on 2026-09-15 refactored authentication tests to use `ManagedAppHostFixture`; the fixture uses `DistributedApplicationTestingBuilder.CreateAsync` and `application.StartAsync` (`test/Shared/Authentication/ManagedAppHostFixture.cs:21-45`).
* `86514ba` on 2026-09-15 added `AppHostCleanup` and changed managed fixture disposal to stop, dispose the application, and dispose the builder (`test/Shared/Authentication/AppHostCleanup.cs:3-32`; `test/Shared/Authentication/ManagedAppHostFixture.cs:71-117`).

### Likely causes of new failures

* The strongest recent behavioral change is the test-host migration, not AppHost composition. `test/Shared/Authentication/RealAppHostProcessFactory.cs:55-109` starts Aspire in-process through `Aspire.Hosting.Testing`, while `StartAppHostProcess` at lines 11-53 launches `dotnet run`; these paths have different endpoint allocation and Docker cleanup behavior.
* Both test paths force `AppHost__EnableInfrastructureContainers=True` and disable persistent SQL/Keycloak state (`RealAppHostProcessFactory.cs:20-24` and `67-71`). That deliberately creates and removes fresh Docker resources, making an internal tunnelproxy visible during tests.
* The HTTPS change is another plausible source of application failures: AppHost launch settings expose HTTPS at `src/TNC.Trading.Platform.AppHost/Properties/launchSettings.json:5-16`, while tests resolve managed endpoints by name. Failures should be classified by whether they are endpoint/certificate, Keycloak readiness, or application behavior failures.
* `global.json:2-5` pins SDK `10.0.302`; the captured AppHost uses Aspire `13.2.3`. A package/tooling update should be checked in the project files and lock/restore output before attributing failures to application code.

### Recommended next checks

* Capture `docker ps -a` and `docker inspect` while AppHost is running, recording labels and network membership for the tunnelproxy; compare its labels to Aspire/DCP-managed resources.
* Run the AppHost composition unit tests and the smallest failing functional/E2E test separately, first with the managed fixture and then with `dotnet run`, to isolate test-host behavior from product behavior.
* Compare the failing test's endpoint scheme and authority with `application.GetEndpoint(...)` values; do not infer a fixed port from `launchSettings.json` for the managed test host.
* Inspect the full `3ef847e` and `86514ba` diffs for changed readiness waits and disposal exceptions, then correlate each failure with the captured test stdout/stderr rather than the tunnelproxy's presence alone.

### External reference

* Aspire documentation landing page: https://aspire.dev/

