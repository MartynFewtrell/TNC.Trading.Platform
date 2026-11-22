---
title: Aspire 13.5 Official Upgrade Research
description: Official-source assessment of upgrading this repository's .NET Aspire AppHost and ServiceDefaults projects to Aspire 13.5.
author: GitHub Copilot
ms.date: 2026-09-17
ms.topic: reference
keywords:
  - .NET Aspire
  - Aspire 13.5
  - upgrade
  - AppHost
  - ServiceDefaults
---

## Research Scope

* Study official Aspire 13.5 release and upgrade guidance.
* Establish supported .NET SDK/runtime compatibility and compatible Aspire package versions.
* Identify required package upgrades, breaking changes, deprecated APIs, and applicable features.
* Inspect the repository only enough to map findings to the existing AppHost and ServiceDefaults patterns.

## Status

Complete. Research was limited to the Aspire 13.5 release/upgrade guidance,
official Aspire documentation, NuGet.org package metadata, and the repository's
AppHost/ServiceDefaults composition.

## Executive Findings

* The repository can target Aspire 13.5 without a .NET target-framework change.
  The official prerequisites state that C# AppHosts require the .NET 10 SDK and
  Aspire can run applications targeting .NET 8 or later. The repository pins
  `10.0.200` and all relevant projects target `net10.0`.
* Upgrade all Aspire packages as one coordinated family. Aspire documents a
  known runtime failure when 13.5 SDK/core packages are mixed with 13.4 hosting
  integrations. The same discipline should be applied to the repository's
  current 13.2.3 AppHost SDK, integrations, and testing packages.
* No known 13.5 breaking API is used by the current AppHost. The relevant
  AppHost code constructs SQL Server, Keycloak, Mailpit, and project resources,
  then uses established reference and readiness wiring. It has no custom
  resource commands, interaction-service use, terminal resources, Azure
  resource provisioning, or deprecated `PublishAsConnectionString` calls.
* ServiceDefaults is intentionally a custom shared project, not an
  `Aspire.ServiceDefaults` package consumer. It uses stable .NET 10 service
  discovery, HTTP resilience, OpenTelemetry, health-check, and Data Protection
  APIs. An Aspire 13.5 package upgrade should not alter it.

## Official Compatibility

### .NET SDK and Runtime

The official [Aspire prerequisites](https://aspire.dev/get-started/prerequisites/)
state: "The .NET 10.0 SDK is required for C# AppHosts. However, Aspire can run
applications targeting .NET 8.0 or later."

Repository evidence:

* global.json:3 specifies `10.0.200` with `latestFeature` roll-forward.
* src/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.csproj:12
  targets `net10.0`.
* src/TNC.Trading.Platform.ServiceDefaults/TNC.Trading.Platform.ServiceDefaults.csproj:4
  targets `net10.0`.

Conclusion: the project satisfies the documented SDK and runtime baseline for
Aspire 13.5. No change to `global.json` or any target framework is recommended
as part of this upgrade.

### Aspire Package Family

The [13.5 release notes](https://aspire.dev/whats-new/aspire-13-5/#-upgrade-to-aspire-135)
say stable packages use `13.5.0`, preview integrations use a matching
`13.5.0-preview.*` version, and warn: "Don't mix Aspire 13.4 and 13.5
packages." The same release line has later servicing versions in the official
NuGet flat-container feeds. At research time, the newest 13.5 entries are:

| Package | Current repository version | Compatible 13.5 servicing version | Official source |
| --- | --- | --- | --- |
| `Aspire.AppHost.Sdk` | `13.2.3` | `13.5.4` | [NuGet version index](https://api.nuget.org/v3-flatcontainer/aspire.apphost.sdk/index.json) |
| `Aspire.Hosting.SqlServer` | `13.2.3` | `13.5.4` | [NuGet version index](https://api.nuget.org/v3-flatcontainer/aspire.hosting.sqlserver/index.json) |
| `Aspire.Hosting.Keycloak` | `13.2.3-preview.1.26217.6` | `13.5.4-preview.1.26464.4` | [NuGet version index](https://api.nuget.org/v3-flatcontainer/aspire.hosting.keycloak/index.json) |
| `Aspire.Hosting.Testing` | `13.2.3` | `13.5.4` | [NuGet version index](https://api.nuget.org/v3-flatcontainer/aspire.hosting.testing/index.json) |
| `Aspire.Cli` | Not pinned in repository | `13.5.4` | [NuGet version index](https://api.nuget.org/v3-flatcontainer/aspire.cli/index.json) |

The package metadata confirms that `Aspire.Hosting.SqlServer 13.5.4`,
`Aspire.Hosting.Keycloak 13.5.4-preview.1.26464.4`, and
`Aspire.Hosting.Testing 13.5.4` are Microsoft Aspire packages from the same
servicing release commit. `Aspire.Hosting` targets .NET 8.0 or higher, but its
NuGet documentation explicitly says AppHost projects should reference the
AppHost package/SDK rather than `Aspire.Hosting` directly.

Repository evidence:

* src/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.csproj:1,
  :4-5 contains the SDK, Keycloak, and SQL Server package references.
* test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/TNC.Trading.Platform.Api.IntegrationTests.csproj:11,
  test/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.UnitTests/TNC.Trading.Platform.AppHost.UnitTests.csproj:11,
  test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/TNC.Trading.Platform.Web.E2ETests.csproj:11,
  and test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/TNC.Trading.Platform.Web.FunctionalTests.csproj:11
  reference `Aspire.Hosting.Testing 13.2.3`.

Recommendation: upgrade all five package categories to the listed 13.5.4
versions in one pull request and restore/build the whole solution. Keep the
Keycloak package on its matching preview version because that integration is
still preview; do not replace it with a different stable package.

### ServiceDefaults Dependencies

ServiceDefaults uses `Microsoft.NET.Sdk` and sets `IsAspireSharedProject=true`,
but does not reference an Aspire NuGet package. Its package references are
independent .NET/OpenTelemetry libraries:

* `Microsoft.Extensions.Http.Resilience 10.1.0` and
  `Microsoft.Extensions.ServiceDiscovery 10.1.0`
* ASP.NET Core and EF Core packages `10.0.5`
* OpenTelemetry packages `1.15.1` through `1.15.3`

Official NuGet version feeds show later 10.x servicing versions for the
Microsoft.Extensions packages and later OpenTelemetry versions. Those updates
are not required by Aspire 13.5 and should be assessed as a separate dependency
maintenance task, preserving the existing package-version grouping and testing
their behavior independently.

Repository evidence: src/TNC.Trading.Platform.ServiceDefaults/TNC.Trading.Platform.ServiceDefaults.csproj:1,
:4, :7, and :13-22.

## Official Upgrade Procedure

The [Upgrade Aspire](https://aspire.dev/whats-new/upgrade-aspire/) guidance
recommends the following sequence:

1. Run `aspire update --self` to update the CLI.
2. Run `aspire update` from the repository root to update the Aspire SDK,
   integrations, and templates.
3. Review breaking changes, update the optional VS Code Aspire extension, and
   run the application with `aspire run`.

The [aspire update command reference](https://aspire.dev/reference/cli/commands/aspire-update/)
adds that the command resolves diamond dependencies and applies package edits
before one final `dotnet restore`. For non-interactive automation, use
`aspire update --yes --non-interactive`.

For this repository, inspect the planned package edits before accepting them.
If manual version edits are used, apply the exact coordinated versions in the
package table and include all four testing projects in the same change.

The AppHost SDK default remains `AspireUseCliBundle=false` for existing
projects. New 13.5 templates set it to `true`, but the release notes state that
existing projects are unaffected unless they opt in. Do not enable the CLI
bundle as part of the package-only upgrade. Evaluate it separately for whether
repository-managed CLI reproducibility is desired.

## Breaking Changes and Deprecations

| 13.5 change | Repository assessment | Required action |
| --- | --- | --- |
| Hosting-context `ServiceProvider` renamed to `Services` | No AppHost hosting-context use found. General application DI uses of `scope.ServiceProvider` are unrelated | No migration identified |
| `PublishAsConnectionString` obsolete; use `AddConnectionString` in publish-mode app model code | No use found | No migration identified |
| `aspire ps --resources` and `--include-hidden` removed | No repository script/use found | Audit developer and CI documentation outside source before relying on these flags |
| `Aspire.Hosting.GitHub.Models` deprecated | No package or source use found | No migration identified |
| Proxyless endpoint allocation timing changed | No explicit proxyless endpoint configuration found | Smoke-test API/Web endpoint discovery after upgrade |
| `TerminalOptions.Shell` removed and dimensions validated | No terminal feature use found | No migration identified |
| `DevTunnelRegion` member names normalized | No Dev Tunnels use found | No migration identified |
| Dashboard AI Assistant removed | No repository integration found | Inform users only if they relied on the dashboard chat feature |
| VS Code no longer auto-opens dashboard | AppHost launch profile still has `launchBrowser=true` for HTTPS | Verify the existing profile and VS Code workflow open the desired browser/dashboard surface |
| `DotnetProjectResource` moved and is experimental | No `AddDotnetProject` or type use found | No migration identified |

Source: [Aspire 13.5 breaking changes](https://aspire.dev/whats-new/aspire-13-5/#breaking-changes).

## Repository Feature Mapping

### Current AppHost Shape

The composition entry point uses `DistributedApplication.CreateBuilder(args)`,
then delegates infrastructure, project registration, and environment wiring:

* src/TNC.Trading.Platform.AppHost/AppHost.cs:3-11
* src/TNC.Trading.Platform.AppHost/AppHostInfrastructureRegistration.cs:16-36
  creates persistent SQL Server, persistent Mailpit, and Keycloak with a realm
  import and configurable state lifetime.
* src/TNC.Trading.Platform.AppHost/AppHostProjectRegistration.cs:11-45 creates
  API and Web project resources, configures public HTTP endpoints, resource
  references, readiness dependencies, and UI URLs.
* src/TNC.Trading.Platform.AppHost/AppHostEnvironmentWiring.cs:130-181 applies
  auth, mail, notification, and Keycloak readiness wiring.

This is a conventional C# AppHost using APIs that remain supported in 13.5.

### Current ServiceDefaults Shape

ServiceDefaults adds OpenTelemetry, a liveness check, configuration/pass-through
service discovery, default HTTP resilience, and OTLP exporting:

* src/TNC.Trading.Platform.ServiceDefaults/Extensions.cs:34-52
* src/TNC.Trading.Platform.ServiceDefaults/Extensions.cs:102-151
* src/TNC.Trading.Platform.ServiceDefaults/Extensions.cs:173-206

The official `Microsoft.Extensions.ServiceDiscovery` package documentation says
`AddServiceDiscovery` registers the configuration provider and can be attached
to default `HttpClient` configuration. It also confirms that Aspire supplies
development/test service-discovery configuration only for explicitly referenced
projects. The repository's `WithReference` usage therefore remains material to
local service discovery.

Source: [Microsoft.Extensions.ServiceDiscovery 10.0.0](https://www.nuget.org/packages/Microsoft.Extensions.ServiceDiscovery/10.0.0).

## Applicable 13.5 Features

### Recommended for Follow-up Evaluation

* Stable command arguments can make a future operator-controlled migration,
  reconciliation, or diagnostic command available from the dashboard and CLI.
  `CommandOptions.Arguments` creates named CLI options and validates inputs
  before command execution. Apply only after designing authorization, secret
  handling, audit logging, and idempotency for the specific operation.
* The stable `InputType.File` can support a future local configuration/import
  workflow. Official guidance requires treating every uploaded file as
  untrusted, limiting `MaxFileSize`, validating content rather than extension,
  and using an application-controlled file name.
* The VS Code Aspire extension exposes discovered AppHost resource controls,
  logs, health, and custom commands. Use it to verify the post-upgrade local
  developer workflow, including the new dashboard-opening behavior.

Sources:

* [Custom resource commands](https://aspire.dev/fundamentals/custom-resource-commands/)
* [Interaction service](https://aspire.dev/extensibility/interaction-service/)
* [Aspire Visual Studio Code extension](https://aspire.dev/get-started/aspire-vscode-extension/)

### Rejected for This Upgrade

* `WithTerminal()` is experimental (`ASPIRETERMINAL001`), does not automatically
  attach a debugger, and applies to interactive process/container resources.
  The current AppHost has no TUI or shell resource, so do not add it.
* HTTPS certificate configuration is experimental
  (`ASPIRECERTIFICATES001`) and applies only in run mode, not published
  artifacts. Do not introduce it without a demonstrated local TLS requirement.
  The current Keycloak integration can already participate in Aspire's default
  local certificate behavior.
* `PromptProgressAsync` remains experimental (`ASPIREINTERACTION001`). Prefer
  stable command progress through `CommandOptions.Progress` if a future command
  needs an indeterminate progress dialog.
* Cross-scope existing Azure resource APIs, Kubernetes persistent volumes,
  Azure Container Apps unique naming/VNet features, Radius, Redis modules, and
  TypeScript AppHost improvements do not map to current resources or deployment
  code. Do not introduce them speculatively.
* Do not migrate the C# AppHost to TypeScript. TypeScript support is now GA, but
  it does not solve a repository problem and would create unrelated change.

Sources:

* [WithTerminal](https://aspire.dev/app-host/with-terminal/)
* [Certificate configuration](https://aspire.dev/app-host/certificate-configuration/)
* [Aspire 13.5 release notes](https://aspire.dev/whats-new/aspire-13-5/)

## Validation Implications

Perform these checks after the coordinated package upgrade:

1. Restore and build `TNC.Trading.Platform.slnx` with the pinned .NET 10 SDK.
   Compilation should expose any hosting-context API rename that source search
   did not reach.
2. Run targeted AppHost unit tests and API/Web integration or functional tests.
   They consume `Aspire.Hosting.Testing` and must verify the test harness uses
   the same 13.5 family as the AppHost.
3. Start the AppHost with `aspire run` and verify SQL Server, Mailpit, Keycloak,
   API, and Web become healthy. Confirm persistent SQL/Mailpit data remains
   usable and the configured Keycloak realm imports successfully.
4. Verify API and Web service discovery and HTTP resilience by exercising their
   AppHost `WithReference` paths. Check the public API Scalar URL and operator
   UI URLs registered by the AppHost.
5. Exercise the HTTPS and HTTP launch profiles. The profile includes explicit
   dashboard OTLP, MCP, and resource-service endpoint URLs at
   src/TNC.Trading.Platform.AppHost/Properties/launchSettings.json:7-27.
   Confirm the expected dashboard/browser behavior because VS Code no longer
   auto-opens the dashboard by default in 13.5.
6. Search scripts, CI configuration, and developer docs for removed
   `aspire ps --resources` or `--include-hidden` flags before rollout.

The 13.5 release notes identify mixed versions as a runtime risk, specifically
`MissingMethodException` and `TypeLoadException`. Treat any such error during
startup as evidence of an incomplete Aspire-family upgrade before investigating
application code.

## Sources

* [What is new in Aspire 13.5](https://aspire.dev/whats-new/aspire-13-5/)
* [Upgrade Aspire](https://aspire.dev/whats-new/upgrade-aspire/)
* [aspire update command](https://aspire.dev/reference/cli/commands/aspire-update/)
* [Aspire prerequisites](https://aspire.dev/get-started/prerequisites/)
* [Install Aspire CLI](https://aspire.dev/get-started/install-cli/)
* [Aspire AppHost SDK versions](https://api.nuget.org/v3-flatcontainer/aspire.apphost.sdk/index.json)
* [Aspire Keycloak hosting versions](https://api.nuget.org/v3-flatcontainer/aspire.hosting.keycloak/index.json)
* [Aspire SQL Server hosting versions](https://api.nuget.org/v3-flatcontainer/aspire.hosting.sqlserver/index.json)
* [Aspire hosting testing versions](https://api.nuget.org/v3-flatcontainer/aspire.hosting.testing/index.json)
* [Aspire CLI versions](https://api.nuget.org/v3-flatcontainer/aspire.cli/index.json)
* [Aspire Keycloak 13.5.4-preview.1.26464.4 metadata](https://api.nuget.org/v3-flatcontainer/aspire.hosting.keycloak/13.5.4-preview.1.26464.4/aspire.hosting.keycloak.nuspec)
* [Aspire SQL Server 13.5.4 metadata](https://api.nuget.org/v3-flatcontainer/aspire.hosting.sqlserver/13.5.4/aspire.hosting.sqlserver.nuspec)
* [Aspire Hosting Testing 13.5.4 metadata](https://api.nuget.org/v3-flatcontainer/aspire.hosting.testing/13.5.4/aspire.hosting.testing.nuspec)

## Outstanding Gaps

* This research did not inspect developer scripts and documentation for removed
  `aspire ps` flags. Search them before executing the upgrade.
* The installed Aspire CLI version and VS Code Aspire extension version were
  not queried. Confirm that the CLI is at least 13.5.0 before using
  `aspire update`; update the extension if its resource controls are part of the
  expected workflow.
* The Keycloak integration remains preview. Confirm its 13.5.4-preview runtime
  behavior against the repository's realm import and persistent-state tests
  before merging an upgrade.
