<!-- markdownlint-disable-file -->

# Aspire tunnel proxy timing and cleanup research

## Scope

Investigate why `aspire-container-network-tunnelproxy-*` containers began appearing recently despite earlier test use, establish their ownership and lifecycle, and assess whether `ManagedAppHostFixture` cleanup or test-runner cancellation left containers running.

## Initial live observations

At 2026-09-15, `docker ps` reported five running containers named `aspire-container-network-tunnelproxy-*`. All use `dcptun_developer_ms:0.25.13` and were created between 09:28 and 09:49 local time. Their Docker labels identify five distinct creator processes: PIDs `22092`, `2940`, `26408`, `10044`, and `15044`.

The labels also report local proxy port bindings for TCP ports `15049` and `15050`. The containers are therefore per-orchestration tunnel-proxy resources, not application, Keycloak, or SQL Server containers.

## Leading hypothesis and discriminating checks

The likely timing trigger is commit `3ef847e` (`Refactor authentication tests to use ManagedAppHostFixture`), which appears at `HEAD` and introduced or expanded direct AppHost orchestration in several real-authentication test fixtures. The immediate checks are:

* Read `test/Shared/Authentication/ManagedAppHostFixture.cs` and all consuming fixture disposal methods to confirm whether they await complete host disposal.
* Inspect the live container `creatorProcessId` values and process command lines to determine whether hosts are still active, already exited, or were cancelled.
* Compare commit `3ef847e` with its parent and package restore assets to identify whether the version of Aspire/DCP or test support changed at the same time.

## Verified fixture lifecycle

`ManagedAppHostFixture` is a new file in commit `3ef847e`; it did not exist in that commit's parent. It creates an `IDistributedApplicationTestingBuilder`, builds and starts a `DistributedApplication`, waits for `keycloak`, `api`, and `web` to become healthy, and exposes resource endpoints. See `test/Shared/Authentication/ManagedAppHostFixture.cs:10-45`.

Its `DisposeAsync` awaits `application.StopAsync()`, then `application.DisposeAsync()`, and then `builder.DisposeAsync()`. It clears fields after each completed disposal. See `test/Shared/Authentication/ManagedAppHostFixture.cs:58-75`. Its initialization catch also calls and awaits this cleanup before rethrowing. See `test/Shared/Authentication/ManagedAppHostFixture.cs:42-46`.

The consuming fixtures also await the shared fixture cleanup:

* Functional authentication fixture: `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/Authentication/RealAuthenticationFunctionalTestFixture.cs:41-56`.
* E2E authentication fixture: `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/Authentication/RealAuthenticationE2ETestFixture.cs:37-52`.
* Real Keycloak API integration fixture: `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/RealAuthenticationIntegrationTestFixture.cs:26-29`.
* Synthetic-token API integration fixture: `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/SyntheticTokenIntegrationTestFixture.cs:24-27`.

Therefore the current source has a correctly awaited normal xUnit `IAsyncLifetime.DisposeAsync` path. This evidence does not prove that xUnit invoked it for the five observed runs, nor that Aspire 13.5.3 removed every DCP-owned resource when it was invoked.

## Test-support timing evidence

Commit `3ef847e` (`Refactor authentication tests to use ManagedAppHostFixture`) is `HEAD`. It replaces the older direct `SharedAppHostProcessHandle` approach in the functional and E2E authentication fixtures, which used `RealAppHostProcessFactory` or `AppHostProcessFactory` together with `KeycloakPortLease`, with the new in-process `DistributedApplicationTestingBuilder` path.

The same commit also replaces the direct process-hosted real-authentication and synthetic-token API fixtures. The pre-change functional fixture acquired a `KeycloakPortLease`, started an AppHost child process, and disposed that process handle. The current code starts the distributed application in the xUnit testhost process through Aspire Hosting Testing.

This is a direct, verified explanation for why the proxy containers are newly visible: the recent test refactor changed the orchestration mechanism from manually spawned AppHost processes to Aspire's testing builder. It created the shared fixture in `3ef847e`, so this mechanism could not have been used by earlier commits.

## Live Docker and process evidence

Each proxy is attached to `aspire-persistent-network-1d2c2e33-TNC.Trading.Platform` and has no mounts. Docker labels identify unique creator PIDs and start timestamps:

| Creator PID | Creator start (BST) | Proxy started (UTC) | Container |
| --- | --- | --- | --- |
| 22092 | 2026-09-15 09:28:38.292 | 2026-09-15 08:28:40.761 | `aspire-container-network-tunnelproxy-58a7a8iqr8` |
| 2940 | 2026-09-15 09:31:44.563 | 2026-09-15 08:31:47.066 | `aspire-container-network-tunnelproxy-5r0s3qrg94` |
| 26408 | 2026-09-15 09:34:17.107 | 2026-09-15 08:34:19.390 | `aspire-container-network-tunnelproxy-sudmn0pouo` |
| 10044 | 2026-09-15 09:47:12.492 | 2026-09-15 08:47:14.899 | `aspire-container-network-tunnelproxy-hv23ia3t1c` |
| 15044 | 2026-09-15 09:49:54.768 | 2026-09-15 08:49:57.205 | `aspire-container-network-tunnelproxy-0qjkjinmjg` |

At inspection time, none of the five labelled creator PIDs existed in Windows process metadata. All five containers nevertheless remained `Up` about six hours later. A still-running `dotnet vstest.console.dll` process was observed, but it was PID 15756, not a labelled creator process. The five containers represent five completed or externally terminated orchestration attempts rather than one shared long-running owner.

## Package evidence

The AppHost currently uses `Aspire.AppHost.Sdk/13.5.3`, `Aspire.Hosting.SqlServer/13.5.3`, and `Aspire.Hosting.Keycloak/13.5.3-preview.1.26425.3`: `src/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.csproj:1-7`.

The three test projects that link `ManagedAppHostFixture` each directly reference `Aspire.Hosting.Testing/13.5.3`:

* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/TNC.Trading.Platform.Web.FunctionalTests.csproj:10-15`.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/TNC.Trading.Platform.Web.E2ETests.csproj:10-15`.
* `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/TNC.Trading.Platform.Api.IntegrationTests.csproj:10-15`.

No `packages.lock.json` files exist. Current generated restore assets resolve Aspire-related packages, and Docker identifies the active tunnel image as `dcptun_developer_ms:0.25.13`.

## Dependency and orchestration timing

Commit `52bfba4c336f9d5dd70cdf41ec3de078ec9cd01a` was committed on 2026-09-14 17:28:34 +01:00, before the 2026-09-15 proxy creation window. It changed the SDK from `Aspire.AppHost.Sdk/13.2.3` to `Aspire.AppHost.Sdk/13.5.3`, updated the Keycloak and SQL hosting packages to the matching `13.5.3` family, upgraded the pinned .NET SDK from `10.0.200` to `10.0.302`, and added `<AspireUseCliBundle>true</AspireUseCliBundle>`. The current values are in `global.json:1-6` and `src/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.csproj:1-19`.

The local NuGet cache contains `Aspire.Hosting.Testing` versions `13.0.0`, `13.1.2`, `13.2.3`, and `13.5.3`, plus `Aspire.Hosting.Orchestration.win-x64` versions `9.5.0`, `13.1.2`, `13.2.3`, and `13.5.3`. Version `13.5.3` was restored on 2026-09-14; the Docker proxy image observed during the affected runs was `dcptun_developer_ms:0.25.13`.

This establishes two correlated, recent prerequisites for the observed containers:

* `52bfba4` selected the current Aspire CLI bundle and DCP stack.
* `3ef847e` made real-authentication tests create in-process Aspire testing applications, producing a DCP orchestration run for each fixture instance.

The repository does not contain a package lock file or a pre-upgrade captured Docker inventory, so it cannot prove which of these two changes first caused DCP to use `dcptun_developer_ms`. The fixture refactor is the direct reason that the newly selected testing-builder orchestration path was exercised by the affected tests.

## Persistent resources versus proxies

The AppHost explicitly makes SQL persistent only when `AppHost:UsePersistentSqlState` is not `false`, and makes Mailpit persist unconditionally. Keycloak is session-scoped when the tests pass `AppHost:UsePersistentKeycloakState=false`. See `src/TNC.Trading.Platform.AppHost/AppHostInfrastructureRegistration.cs:5-48`.

The proxy containers are not application resources declared by `AppHostInfrastructureRegistration` or `AppHostProjectRegistration`. They are DCP-created tunnel clients. Docker inspection identifies the command as `/usr/local/bin/dcptun_c client`; the command arguments configure local control and data ports `15049` and `15050`. Their no-restart configuration excludes Docker restart policy as the reason they are still running.

All five proxies share `aspire-persistent-network-1d2c2e33-TNC.Trading.Platform`, which also contains the intentionally persistent `sql-1d2c2e33`, `mailpit-1d2c2e33`, and `keycloak-1d2c2e33` containers. Network membership does not make the proxies persistent by application intent. Instead, it shows that DCP created the tunnel clients on the same network used by the persistent resource set.

## Cleanup assessment

### Normal fixture path

There is no direct evidence that a `ManagedAppHostFixture.DisposeAsync` call was skipped during normal xUnit execution. The fixture and all current consumers await cleanup, and initialization failures call cleanup before rethrowing. This rules out a plainly missing `await` or missing normal disposal implementation.

### Failure and cancellation path

`ManagedAppHostFixture.DisposeAsync` is sequential and does not use `try/finally` or aggregate cleanup. If `application.StopAsync()` throws, then neither `application.DisposeAsync()` nor `builder.DisposeAsync()` runs. See `test/Shared/Authentication/ManagedAppHostFixture.cs:71-88`. This is a credible cleanup defect because the builder owns the DCP orchestration session, but it is not proven to have occurred in the five observed runs.

The former `AppHostProcessHandle.DisposeAsync` had materially different behavior: it killed the child process tree, awaited process exit, separately attempted application and builder disposal, attempted all environment cleanup, and aggregated exceptions rather than abandoning the remaining cleanup. See `test/Shared/Authentication/AppHostProcessHandle.cs:179-238`. Commit `3ef847e` bypasses this defensive path for the affected fixtures.

If a testhost is forcibly cancelled or terminated, xUnit cannot guarantee it will invoke `IAsyncLifetime.DisposeAsync`; any DCP-owned proxy can be orphaned in that situation. At observation time, all five labelled creator PIDs had already exited, and no active child testhost process belonging to them was found. The only live VSTest process was `dotnet ... vstest.console.dll` under the VS Code service host, started at 09:31, with no listed testhost child. That is insufficient to prove a cancellation, but it confirms no creator remains available to later clean up the proxies.

Read-only Docker event querying over the creation interval returned no recorded proxy stop, die, or destroy event. Docker does not provide durable historical events beyond its available event buffer, so this absence supports but cannot prove that no cleanup command was issued.

## Verified explanation and confidence

The five containers are orphaned DCP tunnel-proxy containers, one for each distinct Aspire orchestration creator process. They were created 2 to 3 seconds after their labelled creator processes began and remained running after those five creator processes disappeared.

The containers appeared recently because the repository changed both the Aspire/DCP bundle (`52bfba4`) and real-authentication test orchestration (`3ef847e`). The second change introduced `ManagedAppHostFixture` and caused the affected tests to run the new Aspire testing-builder lifecycle. This is high-confidence timing and ownership evidence.

The root cause of persistence is not provable from the retained local evidence. The viable causes are:

1. Testhost cancellation or abrupt process termination prevented xUnit fixture disposal.
2. `DistributedApplication.StopAsync()` threw or was cancelled, and `ManagedAppHostFixture` consequently skipped its remaining disposal calls.
3. Aspire/DCP `13.5.3` did not remove the tunnel proxy after an otherwise completed builder/application disposal.

Cause 2 is a code-level vulnerability; causes 1 and 3 remain operational/runtime hypotheses. No evidence supports intentional proxy persistence, a Docker restart policy, or a currently live owning AppHost process.

## Remedial options

1. Harden `ManagedAppHostFixture.DisposeAsync` so `application.DisposeAsync()` and `builder.DisposeAsync()` execute from `finally` blocks even when `StopAsync()` fails, preserving and reporting the first cleanup failure after all cleanup has been attempted. Add a focused test that makes stop fail and verifies builder disposal still executes.
2. Add test-run logging around fixture initialization and disposal, recording the testhost process ID, DCP-related resource identifiers, and completion status. This makes a future container's `creatorProcessId` attributable to a named test run without relying on transient Windows process state.
3. Run the four AppHost-backed test collections in a controlled command with normal completion, then inspect `docker ps` before and after. Repeat once with deliberate test-run cancellation. This discriminates normal Aspire cleanup from cancellation behavior without changing application code.
4. If normal completion reproduces the orphan, update or report against the Aspire `13.5.3` DCP bundle with the five container IDs, labels, `dcptun_developer_ms:0.25.13`, and this lifecycle evidence. Consider an explicit test-only cleanup hook only after confirming the product-supported Aspire disposal contract is insufficient.
5. For current workstation hygiene, a human may safely remove only stopped or positively identified orphan tunnel-proxy containers after confirming no AppHost/test run is active. This research did not stop or remove containers.

## Remaining gaps

* No retained TRX/test-run log maps the five creator PIDs to individual tests or records whether the test runner was cancelled.
* Docker event history could not independently establish a historical stop/destroy attempt for all five containers.
* No baseline inventory exists from Aspire `13.2.3`, and no package lock file pins or records prior DCP tunnel-image behavior.
* A controlled normal-completion versus cancellation reproduction is required to select among the three persistence causes with high confidence.