<!-- markdownlint-disable-file -->

# .NET Aspire 13.5.4 Upgrade Research

## Scope

Assess the repository upgrade available for .NET Aspire 13.5.4. Inspect the SDK pin,
package version ownership, solution dependencies, and repository instructions. Identify
release compatibility, version alignment requirements, likely changes, risks, a recommended
sequence with validation steps, and the alternative of deferring.

## Status

Complete. The proposed change is a coordinated package patch upgrade. No production-code
migration is indicated by the 13.5.4 release notes or the application's current Aspire API use.

## Local Evidence

* `global.json:2-3` selects SDK `10.0.302` with `latestPatch` roll-forward. `dotnet --info`
	confirmed that exact SDK is installed and that the repository targets `net10.0`.
* The solution contains the AppHost, API, Web, ServiceDefaults, Application, and Infrastructure
	projects, plus eleven test projects: `TNC.Trading.Platform.slnx:5-31`.
* Central Package Management is not present: no `Directory.Packages.props` exists. Every relevant
	package version is owned by its project file.
* The AppHost uses `Aspire.AppHost.Sdk/13.5.3` and directly references
	`Aspire.Hosting.SqlServer/13.5.3` and `Aspire.Hosting.Keycloak/13.5.3-preview.1.26425.3`:
	`src/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.csproj:1-7`.
* The SDK auto-references `Aspire.Hosting/13.5.3`; the resolved AppHost graph also resolves
	the SQL Server and Keycloak hosting integrations at their direct versions. This was verified
	with `dotnet list src/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.csproj package --include-transitive`.
* Four test projects directly reference `Aspire.Hosting.Testing/13.5.3`:
	`test/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.UnitTests/TNC.Trading.Platform.AppHost.UnitTests.csproj:10`,
	`test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/TNC.Trading.Platform.Api.IntegrationTests.csproj:10`,
	`test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/TNC.Trading.Platform.Web.FunctionalTests.csproj:10`, and
	`test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/TNC.Trading.Platform.Web.E2ETests.csproj:10`.
* ServiceDefaults references `Microsoft.Extensions.Http.Resilience/10.1.0` and
	`Microsoft.Extensions.ServiceDiscovery/10.1.0`, rather than an Aspire package:
	`src/TNC.Trading.Platform.ServiceDefaults/TNC.Trading.Platform.ServiceDefaults.csproj:12-13`.
	These packages are on the .NET Extensions version train, and no `13.5.4` version exists.
* AppHost composition uses standard SQL Server and Keycloak integration APIs, including
	`AddSqlServer`, `AddKeycloak`, persistent/session lifetimes, realm import, project references,
	and resource waits: `src/TNC.Trading.Platform.AppHost/AppHostInfrastructureRegistration.cs:17-47`
	and `src/TNC.Trading.Platform.AppHost/AppHostProjectRegistration.cs:10-55`.
* ServiceDefaults calls `AddServiceDiscovery`, `AddStandardResilienceHandler`, and
	`AddServiceDiscovery` for default HTTP clients: `src/TNC.Trading.Platform.ServiceDefaults/Extensions.cs:31-42`.
* Distributed tests create the AppHost with `DistributedApplicationTestingBuilder`, use a
	90-second cancellation bound, wait for `keycloak`, `api`, and `web`, obtain named endpoints,
	and asynchronously dispose the application and builder:
	`test/Shared/Authentication/ManagedAppHostFixture.cs:14-53` and
	`test/Shared/Authentication/ManagedAppHostFixture.cs:174-231`.

## Repository Constraints

* `.github/instructions/aspire.instructions.md:20-43` requires AppHost composition only,
	a shared ServiceDefaults project, externalized configuration, and a locally startable full
	system. Its validation requirement is an AppHost start and healthy resources:
	`.github/instructions/aspire.instructions.md:63-66`.
* `.github/instructions/aspire-tests.instructions.md:21-47` requires closed-box tests,
	named resource endpoints, bounded readiness, async cleanup, and randomized ports.
* `.github/copilot-instructions.md:24-27` requires Docker-backed Keycloak under Aspire for
	local authentication. A Docker-capable host is therefore a validation prerequisite.

## External Evidence

* [Aspire 13.5.4 release](https://github.com/microsoft/aspire/releases/tag/v13.5.4)
	identifies this as a patch release from 13.5.3. Its fixes cover Kafka health-check producer
	leaks, DevTunnel region selection, an unused Azure environment dashboard resource, starter-app
	JavaScript port replacement, Homebrew metadata, and an experimental Radius diagnostic.
* [Aspire.AppHost.Sdk 13.5.4](https://www.nuget.org/packages/Aspire.AppHost.Sdk/13.5.4),
	[Aspire.Hosting.SqlServer 13.5.4](https://www.nuget.org/packages/Aspire.Hosting.SqlServer/13.5.4),
	and [Aspire.Hosting.Testing 13.5.4](https://www.nuget.org/packages/Aspire.Hosting.Testing/13.5.4)
	are published and support .NET 8.0 or later. `net10.0` is compatible.
* [Aspire.Hosting.Keycloak 13.5.4-preview.1.26464.4](https://www.nuget.org/packages/Aspire.Hosting.Keycloak/13.5.4-preview.1.26464.4)
	is the matching package for the release. There is no stable `Aspire.Hosting.Keycloak/13.5.4`.
	The integration remains preview and supports .NET 8.0 or later.
* [Keycloak integration documentation](https://aspire.dev/integrations/security/keycloak/)
	confirms `AddKeycloak`, `WithRealmImport`, automatic readiness health checks, and that the
	hosting integration is preview. It also states that realm import is local-development only
	and is unsupported by `aspire publish` or `aspire deploy`.
* [SQL Server hosting documentation](https://aspire.dev/integrations/databases/sql-server/sql-server-host/)
	documents the currently used `AddSqlServer`, `AddDatabase`, persistent lifetime, and
	automatic health-check APIs.
* [Aspire testing overview](https://aspire.dev/testing/overview/) confirms that
	`Aspire.Hosting.Testing` launches the AppHost as a separate process, randomizes proxied ports,
	disables the dashboard by default, and cleans up resources on disposal.
* [global.json overview](https://learn.microsoft.com/dotnet/core/tools/global-json) states that
	`latestPatch` selects the highest installed patch in the specified feature band. The current
	pin does not require a change for the package upgrade.

## Compatibility And Alignment

The upgrade is compatible with the repository's `net10.0` target and SDK `10.0.302`.
No SDK, target-framework, ServiceDefaults, API, or Web package update is required.

Update every direct Aspire package from the same 13.5.3 release train together:

| Owner | Current | Recommended |
| --- | --- | --- |
| AppHost SDK | `Aspire.AppHost.Sdk/13.5.3` | `Aspire.AppHost.Sdk/13.5.4` |
| SQL Server hosting | `Aspire.Hosting.SqlServer/13.5.3` | `Aspire.Hosting.SqlServer/13.5.4` |
| Keycloak hosting | `Aspire.Hosting.Keycloak/13.5.3-preview.1.26425.3` | `Aspire.Hosting.Keycloak/13.5.4-preview.1.26464.4` |
| AppHost unit tests | `Aspire.Hosting.Testing/13.5.3` | `Aspire.Hosting.Testing/13.5.4` |
| API integration tests | `Aspire.Hosting.Testing/13.5.3` | `Aspire.Hosting.Testing/13.5.4` |
| Web functional tests | `Aspire.Hosting.Testing/13.5.3` | `Aspire.Hosting.Testing/13.5.4` |
| Web E2E tests | `Aspire.Hosting.Testing/13.5.3` | `Aspire.Hosting.Testing/13.5.4` |

Do not alter `Microsoft.Extensions.ServiceDiscovery/10.1.0` or
`Microsoft.Extensions.Http.Resilience/10.1.0`. They are unrelated .NET Extensions packages,
not Aspire 13.x packages, and a 13.5.4 version is unavailable. The AppHost SDK supplies the
matching `Aspire.Hosting` transitive package; it should resolve to 13.5.4 after restore.

## Likely Changes And Risks

* Low expected source impact: the patch-release fixes do not overlap the repository's current
	AppHost API surface. No migration edit is anticipated.
* Medium release-management risk: Keycloak is preview-only. Update it to the matching preview
	build, keep it aligned with the stable 13.5.4 host packages, and run the real Docker-backed
	authentication flows before accepting the change.
* Medium operational risk: local Keycloak realm import and persistent state are exercised by the
	AppHost at `src/TNC.Trading.Platform.AppHost/AppHostInfrastructureRegistration.cs:32-45`.
	Validate a clean container start and an existing persistent local state separately. Do not
	treat `WithRealmImport` as a deployment mechanism.
* Low test-harness risk: the shared fixture relies on Aspire's named endpoints, readiness API,
	randomized ports, and disposal behavior. Its existing tests are the correct first regression
	boundary, but Docker availability and local image pulls can make them slower or environment-sensitive.
* No immediate security motivation is visible in the Aspire 13.5.4 notes. Existing unrelated
	package warnings must not be attributed to this patch upgrade.

## Recommended Upgrade Sequence

1. Confirm Docker is running and capture a clean baseline with
	 `dotnet build .\TNC.Trading.Platform.slnx`.
2. Change only the seven direct Aspire references listed in the alignment table. Keep all
	 non-Aspire package versions unchanged.
3. Restore and inspect the resolved graph:
	 `dotnet restore .\TNC.Trading.Platform.slnx` and
	 `dotnet list src\TNC.Trading.Platform.AppHost\TNC.Trading.Platform.AppHost.csproj package --include-transitive`.
	 Confirm the AppHost SDK, `Aspire.Hosting`, SQL Server integration, Keycloak integration, and
	 all `Aspire.Hosting.Testing` references resolve to the recommended versions.
4. Build the solution:
	 `dotnet build .\TNC.Trading.Platform.slnx --no-restore`.
5. Run the focused graph unit tests first:
	 `dotnet test test\TNC.Trading.Platform.AppHost\TNC.Trading.Platform.AppHost.UnitTests\TNC.Trading.Platform.AppHost.UnitTests.csproj --no-restore`.
6. Run the Docker-backed integration boundaries, one project at a time, in this order:
	 AppHost unit tests, API integration tests, Web functional tests, then Web E2E tests. Preserve
	 randomized ports and use the fixture's named endpoint/readiness model.
7. Start `src\TNC.Trading.Platform.AppHost\TNC.Trading.Platform.AppHost.csproj` locally and
	 verify `sql`, `platformdb`, `mailpit`, `keycloak`, `api`, and `web` become healthy; confirm
	 Keycloak realm import, OIDC sign-in, API authentication, and database access.
8. Only after the preceding checks pass, run the full solution test suite. Record pre-existing
	 warnings separately from upgrade regressions.

## Deferral Assessment

Deferral is reasonable when a Docker-backed authentication test window is unavailable, because
the only integration that must remain prerelease is Keycloak and the patch does not fix a feature
used by this AppHost. Keep the current coherent 13.5.3 package set in that case; do not upgrade
only the SDK, SQL Server, or testing package while leaving the Keycloak preview package behind.

Upgrade now when the team can run the sequence above. The scope is contained, the expected
source impact is low, and 13.5.4 provides a coherent patch-level package set for the AppHost
and test harness.

## Follow-On Questions

None. The only execution-time prerequisite is access to a Docker-capable environment with the
existing Keycloak and SQL Server images available or pullable.

## Recommendations

Pending evidence collection.
