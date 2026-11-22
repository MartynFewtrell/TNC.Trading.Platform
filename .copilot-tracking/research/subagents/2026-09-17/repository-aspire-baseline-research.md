---
title: Repository Aspire 13.5 Baseline Research
description: Current Aspire, .NET, dependency, and build-warning baseline for planning an Aspire 13.5 upgrade
author: GitHub Copilot
ms.date: 2026-09-17
ms.topic: reference
keywords:
  - .NET Aspire
  - upgrade
  - dependency inventory
  - build warnings
---

## Research Status

Status: Complete

## Scope

Investigate the current workspace baseline for an Aspire 13.5 upgrade without
modifying product files. The investigation covers applicable repository rules,
SDK and solution composition, package/version ownership, AppHost and
ServiceDefaults implementation, build warnings, compatible dependency
baselines, and upgrade risks.

## Instruction-Derived Constraints

* `.github/copilot-instructions.md` requires Aspire guidance to use
  <https://aspire.dev/> as the primary reference, specifies Keycloak in Aspire
  for local authentication, and requires the SDK version in `global.json` to be
  respected.
* `.github/instructions/aspire.instructions.md` requires the AppHost to remain
  composition-only, orchestrate required local resources, and use shared
  ServiceDefaults for cross-cutting service behavior.
* `.github/instructions/dotnet-stack.instructions.md` requires use of the
  configured SDK, ServiceDefaults/AppHost patterns, health checks, telemetry,
  and container-friendly configuration.
* `.github/instructions/auth.instructions.md` requires the Keycloak Aspire
  integration for local authentication, stable local Keycloak port assignment,
  and realm import where applicable.
* `.github/instructions/csharp.instructions.md` and
  `.github/instructions/tests.instructions.md` establish C# and testing
  conventions relevant to any later upgrade changes.

## SDK and Solution Inventory

`global.json:1-6` pins the SDK to `10.0.200` and sets `rollForward` to
`latestFeature`. All sixteen solution projects target `net10.0`.

`TNC.Trading.Platform.slnx:1-30` includes these production projects:

* `src/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.csproj`
* `src/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.csproj`
* `src/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.csproj`
* `src/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.csproj`
* `src/TNC.Trading.Platform.ServiceDefaults/TNC.Trading.Platform.ServiceDefaults.csproj`
* `src/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.csproj`

The solution also includes ten test projects: architecture integration;
API integration and unit; AppHost unit; application unit; infrastructure
integration and unit; and Web E2E, functional, and unit tests.

## Package and Version Management

There is no `Directory.Packages.props`, `Directory.Build.props`,
`Directory.Build.targets`, `NuGet.config`, or `packages.lock.json` in the
workspace. Versions are directly owned by each project file, so all related
references must be updated explicitly and atomically.

### Aspire Cohort

| Dependency | Current version | Required update scope | Compatible 13.5 baseline |
|------------|-----------------|-----------------------|--------------------------|
| `Aspire.AppHost.Sdk` | `13.2.3` | AppHost SDK declaration | `13.5.4` |
| `Aspire.Hosting.SqlServer` | `13.2.3` | AppHost package reference | `13.5.4` |
| `Aspire.Hosting.Keycloak` | `13.2.3-preview.1.26217.6` | AppHost package reference | `13.5.4-preview.1.26464.4` |
| `Aspire.Hosting.Testing` | `13.2.3` | API integration, AppHost unit, Web E2E, and Web functional tests | `13.5.4` |

Source evidence:

* `src/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.csproj:1-7`
  owns the AppHost SDK, SQL Server integration, Keycloak integration, and
  references to API and Web.
* `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/TNC.Trading.Platform.Api.IntegrationTests.csproj:11,27`
  owns an Aspire testing package and references the AppHost.
* `test/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.UnitTests/TNC.Trading.Platform.AppHost.UnitTests.csproj:11,26-27`
  owns an Aspire testing package and references the AppHost and ServiceDefaults.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/TNC.Trading.Platform.Web.E2ETests.csproj:11,23`
  owns an Aspire testing package and references the AppHost.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/TNC.Trading.Platform.Web.FunctionalTests.csproj:11,23`
  owns an Aspire testing package and references the AppHost.

NuGet package pages published on 2026-09-15 identify `13.5.4` as the current
stable version of [Aspire.AppHost.Sdk](https://www.nuget.org/packages/Aspire.AppHost.Sdk),
[Aspire.Hosting.SqlServer](https://www.nuget.org/packages/Aspire.Hosting.SqlServer),
and [Aspire.Hosting.Testing](https://www.nuget.org/packages/Aspire.Hosting.Testing).
The [Aspire.Hosting.Keycloak](https://www.nuget.org/packages/Aspire.Hosting.Keycloak)
page identifies `13.5.4-preview.1.26464.4` as current and still prerelease.
The hosting integrations and testing package target .NET 8 or higher, therefore
are compatible with this repository's `net10.0` target.

### Non-Aspire Adjacent Baseline

ServiceDefaults is not version-coupled to the Aspire cohort, but it owns the
runtime conventions most exposed to an Aspire upgrade. Its direct references
are recorded in `src/TNC.Trading.Platform.ServiceDefaults/TNC.Trading.Platform.ServiceDefaults.csproj:11-22`:

* `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore` and
  `Microsoft.EntityFrameworkCore.SqlServer` `10.0.5`
* `Microsoft.Extensions.Http.Resilience` and
  `Microsoft.Extensions.ServiceDiscovery` `10.1.0`
* `Microsoft.AspNetCore.OpenApi` `10.0.5`
* OpenTelemetry exporter/hosting `1.15.3`, ASP.NET Core instrumentation
  `1.15.2`, and HTTP/runtime instrumentation `1.15.1`

Resolved AppHost dependencies show that the existing Aspire 13.2.3 graph also
brings `Aspire.Hosting` `13.2.3`, `MessagePack` `2.5.192`,
`Microsoft.Extensions.*` `10.0.5`, and `Azure.Identity` `1.18.0`
transitively. The direct Keycloak integration is the source of the
`MessagePack` vulnerability warnings below. The AppHost graph was inspected
with `dotnet list ... --include-transitive`.

Other direct production package baselines are `Microsoft.AspNetCore.*` and EF
Core `10.0.5`, `Azure.Identity` `1.19.0`, `Azure.Communication.Email` `1.1.0`,
`Scalar.AspNetCore` `2.13.8`, and `Radzen.Blazor` `10.3.2`; see the respective
production project files at their package-reference line ranges surfaced by
the project inventory command below. They do not need to move solely because
Aspire is upgraded.

## AppHost and ServiceDefaults Inventory

The AppHost remains composition-only as required by the repository guidance:

* `src/TNC.Trading.Platform.AppHost/AppHost.cs:1-12` creates the distributed
  builder, delegates infrastructure/project/environment registration, and runs
  the application.
* `src/TNC.Trading.Platform.AppHost/AppHostInfrastructureRegistration.cs:6-44`
  composes persistent SQL Server (`sql`) and database (`platformdb`), persistent
  Mailpit, and Keycloak at stable port `8080`. Keycloak uses
  `WithEndpointProxySupport(false)`, selectable session/persistent lifetime,
  `WithRealmImport("./Realms")`, and an admin-console URL annotation.
* `src/TNC.Trading.Platform.AppHost/AppHostProjectRegistration.cs:7-49` defines
  API and Web projects, external endpoints, references, and readiness waits.
* `src/TNC.Trading.Platform.AppHost/AppHostEnvironmentWiring.cs:6-188` supplies
  auth, SMTP, ACS, and Keycloak configuration plus waits for Keycloak.
* `src/TNC.Trading.Platform.AppHost/Properties/launchSettings.json:4-30`
  has fixed AppHost, dashboard OTLP/MCP, and resource-service endpoints.
* `.aspire/settings.json:1-3` points Aspire tooling to the AppHost project.

The shared defaults implementation is at
`src/TNC.Trading.Platform.ServiceDefaults/Extensions.cs:34-192`. It configures
OpenTelemetry logging/metrics/tracing, health checks, service discovery,
standard HTTP resilience, OTLP export, SQL-backed data-protection keys, and
readiness/liveness endpoints. API and Web invoke the shared methods at
`src/TNC.Trading.Platform.Api/Program.cs:14-17,82` and
`src/TNC.Trading.Platform.Web/Program.cs:9-18,76` respectively. The Web API
clients use the Aspire service-discovery URI `https+http://api` at lines 15 and
18, which makes ServiceDefaults service discovery behavior upgrade-critical.

AppHost test coverage directly exercises distributed-application resource
metadata. In particular,
`test/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.UnitTests/AppHostInfrastructureRegistrationTests.cs:14-98`
checks resource names, lifetimes, endpoint annotations, and session Keycloak
mode. `AppHostEnvironmentWiringTests.cs:212,249,322-323` uses
`GetEnvironmentVariableValuesAsync(DistributedApplicationOperation.Publish)`;
this publish-mode operation should remain in use because the repository memory
records run-mode environment-value resolution as capable of hanging.

The current Aspire Keycloak documentation confirms the existing stable `8080`
port, `AddKeycloak`, `WithRealmImport`, and `WaitFor` patterns. It additionally
states that `WithRealmImport` is development-only and unsupported by
`aspire publish` or `aspire deploy`; production needs a custom image, an
initialization process, or infrastructure-as-code realm seeding.

## Build Warning Baseline

Command run from the repository root:

```powershell
dotnet build TNC.Trading.Platform.slnx --no-restore
```

Result: build succeeded in 4.3 seconds with 99 warnings. The exact distinct
warning messages follow; many occur repeatedly in multiple projects because
the dependency graph is shared.

```text
warning NU1902: Package 'AngleSharp' 1.2.0 has a known moderate severity vulnerability, https://github.com/advisories/GHSA-pgww-w46g-26qg
warning NU1902: Package 'MessagePack' 2.5.192 has a known moderate severity vulnerability, https://github.com/advisories/GHSA-2f33-pr97-265q
warning NU1902: Package 'MessagePack' 2.5.192 has a known moderate severity vulnerability, https://github.com/advisories/GHSA-2x83-8g95-xh59
warning NU1902: Package 'MessagePack' 2.5.192 has a known moderate severity vulnerability, https://github.com/advisories/GHSA-cj9g-3mj2-g8vv
warning NU1902: Package 'MessagePack' 2.5.192 has a known moderate severity vulnerability, https://github.com/advisories/GHSA-cxmj-83gh-fp49
warning NU1902: Package 'MessagePack' 2.5.192 has a known moderate severity vulnerability, https://github.com/advisories/GHSA-q2h6-ghwm-5qm8
warning NU1902: Package 'MessagePack' 2.5.192 has a known moderate severity vulnerability, https://github.com/advisories/GHSA-qhmf-xw27-6rqr
warning NU1902: Package 'MessagePack' 2.5.192 has a known moderate severity vulnerability, https://github.com/advisories/GHSA-v72x-2h86-7f8m
warning NU1902: Package 'MessagePack' 2.5.192 has a known moderate severity vulnerability, https://github.com/advisories/GHSA-w567-gjr2-hm5j
warning NU1902: Package 'MessagePack' 2.5.192 has a known moderate severity vulnerability, https://github.com/advisories/GHSA-wfr3-xj75-pfwh
warning NU1903: Package 'MessagePack' 2.5.192 has a known high severity vulnerability, https://github.com/advisories/GHSA-hv8m-jj95-wg3x
warning NU1903: Package 'MessagePack' 2.5.192 has a known high severity vulnerability, https://github.com/advisories/GHSA-vh6j-jc39-fggf
warning NU1903: Package 'Microsoft.OpenApi' 2.0.0 has a known high severity vulnerability, https://github.com/advisories/GHSA-v5pm-xwqc-g5wc
warning NU1903: Package 'System.Security.Cryptography.Xml' 10.0.5 has a known high severity vulnerability, https://github.com/advisories/GHSA-23rf-6693-g89p
warning NU1903: Package 'System.Security.Cryptography.Xml' 10.0.5 has a known high severity vulnerability, https://github.com/advisories/GHSA-37gx-xxp4-5rgx
warning NU1903: Package 'System.Security.Cryptography.Xml' 10.0.5 has a known high severity vulnerability, https://github.com/advisories/GHSA-6588-8gv4-xfgh
warning NU1903: Package 'System.Security.Cryptography.Xml' 10.0.5 has a known high severity vulnerability, https://github.com/advisories/GHSA-8q5v-6pqq-x66h
warning NU1903: Package 'System.Security.Cryptography.Xml' 10.0.5 has a known high severity vulnerability, https://github.com/advisories/GHSA-cvvh-rhrc-wg4q
warning NU1903: Package 'System.Security.Cryptography.Xml' 10.0.5 has a known high severity vulnerability, https://github.com/advisories/GHSA-g8r8-53c2-pm3f
warning NU1903: Package 'System.Security.Cryptography.Xml' 10.0.5 has a known high severity vulnerability, https://github.com/advisories/GHSA-mmjf-rqrv-855v
warning NU1903: Package 'System.Security.Cryptography.Xml' 10.0.5 has a known high severity vulnerability, https://github.com/advisories/GHSA-w3x6-4m5h-cxqf
warning NU1904: Package 'Microsoft.AspNetCore.DataProtection' 10.0.5 has a known critical severity vulnerability, https://github.com/advisories/GHSA-9mv3-2cwr-p262
```

The `MessagePack` warnings originate in the resolved AppHost graph. The
`Microsoft.OpenApi` warning appears in API, Web, and ServiceDefaults. The
remaining package-to-project paths require a post-upgrade `dotnet list package
--include-transitive` comparison before attributing remediation to Aspire.

## Commands and Tools Run

* Read the required repository instructions and relevant C#, authentication,
  configuration, test, refactoring, and Markdown guidance.
* `dotnet build TNC.Trading.Platform.slnx --no-restore`
* `dotnet list src/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.csproj package --include-transitive`
* `dotnet list src/TNC.Trading.Platform.ServiceDefaults/TNC.Trading.Platform.ServiceDefaults.csproj package --include-transitive | Select-String 'Microsoft.Extensions.Http.Resilience|Microsoft.Extensions.ServiceDiscovery|OpenTelemetry|Microsoft.AspNetCore.OpenApi|Microsoft.OpenApi|DataProtection|EntityFrameworkCore'`
* `dotnet package search Aspire.Hosting.Keycloak --prerelease --take 20`
* `dotnet package search Aspire.AppHost.Sdk --exact-match --take 10`
* `dotnet package search Aspire.Hosting.Testing --exact-match --take 10`
* Enumerated `*.csproj`, project/configuration/lock/version-management files,
  package references, AppHost and ServiceDefaults source, and Aspire test API
  usages with workspace search tools.
* Consulted <https://aspire.dev/integrations/security/keycloak/>,
  <https://aspire.dev/testing/overview/>, and the NuGet package pages linked
  above. The generic Aspire 13 upgrade URLs attempted during research returned
  HTTP 404 and were not used as evidence.

## Preliminary Upgrade Risks

* **Cohort skew:** direct version ownership across five project files makes it
  easy to update the AppHost SDK but miss one or more `Aspire.Hosting.Testing`
  test projects. Mixed Aspire patch versions should be avoided.
* **Preview dependency:** Keycloak remains prerelease in the 13.5 train. Update
  it to the matching preview patch and treat its package/API compatibility as a
  distinct test gate.
* **Resource-model changes:** the AppHost relies on `AddKeycloak`, SQL database
  references, endpoint proxy configuration, resource annotations, custom URL
  callbacks, and `WaitFor`. Its direct unit tests should catch source/API
  changes before a full runtime launch.
* **Authentication regression:** the stable Keycloak URL/port is part of OIDC
  cookie and authority behavior. Verify browser sign-in, token validation, and
  realm import against fresh and persistent Keycloak state.
* **Service-discovery regression:** Web clients rely on `https+http://api` and
  shared service discovery/resilience. Verify API discovery and health endpoints
  from the AppHost after the upgrade.
* **Existing vulnerable transitives:** the current 99-warning baseline can hide
  regression warnings. Compare post-upgrade warnings by distinct package and
  advisory, not only by total count. Aspire 13.5 may remove or change the
  AppHost's vulnerable `MessagePack` transitive, but that cannot be asserted
  until restore resolves the new graph.
* **Deployment limitation:** Keycloak `WithRealmImport` is development-only per
  current Aspire documentation. Do not rely on it for a published/deployed
  AppHost; this is a pre-existing deployment risk to track independently.

## Outstanding Gaps and Recommended Follow-Up

* Perform the upgrade in one cohort change, restore, and capture the resolved
  transitive graph for `13.5.4` and Keycloak `13.5.4-preview.1.26464.4`.
* Run AppHost unit tests, then AppHost-backed API integration and Web E2E/
  functional tests with Docker available. Validate SQL, Mailpit, Keycloak,
  health endpoints, `https+http://api`, and authenticated browser flow.
* Compare the post-upgrade warning list with this baseline and separately plan
  remediation for vulnerabilities not resolved by the Aspire cohort.
* Confirm the intended rollout target: latest available 13.5 patch (`13.5.4`)
  was used as the compatibility baseline; use a fixed `13.5.0` target only if
  release-management policy requires that specific patch.