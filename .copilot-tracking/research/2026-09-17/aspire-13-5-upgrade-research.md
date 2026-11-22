<!-- markdownlint-disable-file -->
# Task Research: Aspire 13.5 Upgrade

Research the upgrade path from the repository's current .NET Aspire and related package versions to Aspire 13.5, resolve existing build warnings, and identify relevant Aspire 13.5 capabilities to adopt.

## Task Implementation Requests

* Upgrade the Aspire AppHost, hosting integrations, and test harness to the Aspire 13.5 release train.
* Identify every current build warning and establish a supported remediation path to a warning-free build.
* Assess Aspire 13.5 features against the existing AppHost, ServiceDefaults, local Keycloak, SQL Server, Mailpit, API, and Web topology.
* Provide an implementation-ready sequence and validation gates.

## Scope and Success Criteria

* Scope: Project/package configuration, build diagnostics, AppHost and ServiceDefaults code paths, Aspire CLI usage, and official Aspire 13.5 documentation. No product changes were made.
* Assumptions: The workspace is the intended deployment target; current NuGet availability was verified on 2026-09-17; package versions must be reconfirmed immediately before implementation.
* Success criteria:
  * Aspire is upgraded as a compatible cohort with no mixed 13.2/13.5 references.
  * The 99-warning baseline is remediated without `NoWarn`, audit-level changes, or advisory suppression.
  * New features are adopted only where a concrete repository need exists.

## Outline

1. Current baseline and warning attribution.
2. Official Aspire 13.5 requirements, upgrade process, and breaking changes.
3. Alternatives and selected package strategy.
4. Relevant feature adoption and implementation validation.

## Research Executed

### File Analysis

* `global.json:1-6` pins SDK `10.0.200` with `latestFeature` roll-forward. All 16 solution projects target `net10.0`.
* `src/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.csproj:1-7` directly owns the Aspire AppHost SDK, SQL Server and Keycloak integrations.
* Four test projects directly own `Aspire.Hosting.Testing`; see the API integration, AppHost unit, Web E2E, and Web functional project files at line 11.
* `src/TNC.Trading.Platform.AppHost/AppHost.cs:3-11` is composition-only. Registration methods compose SQL Server, Mailpit, Keycloak realm import, API and Web resources, endpoint wiring, and readiness dependencies.
* `src/TNC.Trading.Platform.ServiceDefaults/Extensions.cs:34-206` owns telemetry, health checks, service discovery, resilience, and SQL-backed data-protection conventions. It does not reference an Aspire package.
* API and Web invoke ServiceDefaults in their `Program.cs` files; Web uses `https+http://api`, so AppHost references and service-discovery behavior are an explicit upgrade gate.
* No central package-management, lock-file, or checked-in CI workflow exists. Direct versions are owned in project files. No checked-in source, scripts, or docs invoke the removed `aspire ps --resources` or `--include-hidden` flags.

### Build Analysis

* `dotnet build TNC.Trading.Platform.slnx --no-restore` completed successfully in 4.3 seconds with 99 warning occurrences across 22 distinct `NU1902`, `NU1903`, and `NU1904` advisory messages.
* The warnings reduce to five resolved package families: `MessagePack 2.5.192` (11 advisories), `AngleSharp 1.2.0` (one), `Microsoft.OpenApi 2.0.0` (one), `System.Security.Cryptography.Xml 10.0.5` (eight), and `Microsoft.AspNetCore.DataProtection 10.0.5` (one).
* `dotnet nuget why` traced MessagePack to the 13.2.3 Aspire AppHost graph; OpenAPI and Data Protection/XML Crypto originate from directly owned .NET `10.0.5` parents; AngleSharp is transitive from `bunit.web 1.40.0` in the Web unit-test project.

### External Research

* Official Aspire release and upgrade documentation: [Aspire 13.5 release notes](https://aspire.dev/whats-new/aspire-13-5/), [Upgrade Aspire](https://aspire.dev/whats-new/upgrade-aspire/), and [aspire update command](https://aspire.dev/reference/cli/commands/aspire-update/).
  * C# AppHosts require the .NET 10 SDK; applications may target .NET 8 or later. The repository already satisfies this requirement.
  * Aspire explicitly warns against mixing release trains because a mixed graph can produce `MissingMethodException` or `TypeLoadException` at runtime.
  * The recommended CLI process is `aspire update --self`, then `aspire update`; `--yes --non-interactive` is available for automated usage.
* NuGet version and dependency evidence: [AppHost SDK versions](https://api.nuget.org/v3-flatcontainer/aspire.apphost.sdk/index.json), [SQL Server integration versions](https://api.nuget.org/v3-flatcontainer/aspire.hosting.sqlserver/index.json), [Keycloak integration versions](https://api.nuget.org/v3-flatcontainer/aspire.hosting.keycloak/index.json), [testing package versions](https://api.nuget.org/v3-flatcontainer/aspire.hosting.testing/index.json), [Aspire Hosting 13.5.4 manifest](https://api.nuget.org/v3-flatcontainer/aspire.hosting/13.5.4/aspire.hosting.nuspec), and [StreamJsonRpc 2.25.29 manifest](https://api.nuget.org/v3-flatcontainer/streamjsonrpc/2.25.29/streamjsonrpc.nuspec).
  * Aspire 13.5.4 resolves its RPC path through `MessagePack 2.5.302`, replacing vulnerable `2.5.192`.
* Advisory remediation evidence: [OpenAPI 10.0.12 manifest](https://api.nuget.org/v3-flatcontainer/microsoft.aspnetcore.openapi/10.0.12/microsoft.aspnetcore.openapi.nuspec), [Data Protection EF 10.0.12 manifest](https://api.nuget.org/v3-flatcontainer/microsoft.aspnetcore.dataprotection.entityframeworkcore/10.0.12/microsoft.aspnetcore.dataprotection.entityframeworkcore.nuspec), [OpenAPI advisory](https://github.com/advisories/GHSA-v5pm-xwqc-g5wc), [Data Protection advisory](https://github.com/advisories/GHSA-9mv3-2cwr-p262), and [AngleSharp advisory](https://github.com/advisories/GHSA-pgww-w46g-26qg).

### Project Conventions

* Standards referenced: `.github/copilot-instructions.md`, `.github/instructions/aspire.instructions.md`, `.github/instructions/dotnet-stack.instructions.md`, and `.github/instructions/auth.instructions.md`.
* The selected direction preserves an AppHost that is composition-only, local Keycloak orchestrated by Aspire, shared ServiceDefaults behavior, and the pinned SDK in `global.json`.

## Key Discoveries

### Current Package Baseline

| Package family | Current | Recommended target |
| --- | --- | --- |
| `Aspire.AppHost.Sdk`, `Aspire.Hosting.SqlServer`, `Aspire.Hosting.Testing` | `13.2.3` | `13.5.4` |
| `Aspire.Hosting.Keycloak` | `13.2.3-preview.1.26217.6` | `13.5.4-preview.1.26464.4` |
| Direct ASP.NET Core/EF Core packages involved in advisories | `10.0.5` | `10.0.12` |
| Test-only `AngleSharp` override | absent | `1.5.0` or later within bUnit compatibility |

The stable Aspire packages must be upgraded together in the AppHost and each of the four test projects. Keycloak remains a preview integration, so it must move to the matching 13.5 preview instead of a mismatched stable package. No target-framework or ServiceDefaults Aspire package change is required.

### Warning Remediation Map

| Warning family | Cause | Selected remediation |
| --- | --- | --- |
| MessagePack, 11 advisories | Aspire `13.2.3` hosting/RPC dependency graph | Upgrade full Aspire cohort to 13.5.4; do not directly pin MessagePack |
| Microsoft.OpenApi, one advisory | `Microsoft.AspNetCore.OpenApi 10.0.5` | Update direct parent references to 10.0.12 |
| DataProtection/XML Crypto, nine advisories | Data Protection EF Core `10.0.5` parents | Update direct Data Protection and paired EF Core references to 10.0.12 |
| AngleSharp, one advisory | Latest stable bUnit retains `AngleSharp 1.2.0` | Add a temporary direct `AngleSharp >= 1.5.0` reference only to Web unit tests |

The direct Data Protection owners are `src/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.csproj:20`, `src/TNC.Trading.Platform.ServiceDefaults/TNC.Trading.Platform.ServiceDefaults.csproj:13`, and `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/TNC.Trading.Platform.Infrastructure.IntegrationTests.csproj:12`. Keep their paired EF Core packages on the same `10.0.12` servicing version. Update `Microsoft.AspNetCore.OpenApi` in the API and ServiceDefaults projects.

### Aspire 13.5 Feature Assessment

**Recommend later, feature-specific adoption:**

* Stable custom-resource command arguments can safely support a future operator migration, reconciliation, or diagnostics command. Any command must first define authorization, audit logging, secret handling, idempotency, and input validation. Source: [Custom resource commands](https://aspire.dev/fundamentals/custom-resource-commands/).
* Stable file input can support a future local configuration/import workflow, but uploaded content must be treated as untrusted, size-limited, content-validated, and stored using application-controlled names. Source: [Interaction service](https://aspire.dev/extensibility/interaction-service/).
* Update and use the VS Code Aspire extension to validate resource controls, logs, health, and dashboard behavior after upgrade. In 13.5 VS Code no longer auto-opens the dashboard by default. Source: [Aspire VS Code extension](https://aspire.dev/get-started/aspire-vscode-extension/).

**Do not adopt in this upgrade:** experimental terminals, experimental certificate configuration/progress interaction APIs, TypeScript AppHost migration, Azure/Kubernetes/Radius additions, Redis modules, and CLI bundling. The current AppHost has no corresponding need; each would widen the change beyond a dependency and warning-remediation release.

## Technical Scenarios

### Coordinated Upgrade and Clean Build

**Requirements:**

* No mixed Aspire version train.
* Zero vulnerability warnings without suppression.
* Preserve Keycloak realm import, stable port `8080`, persistence behavior, API/Web discovery, and Docker-backed local workflow.

**Preferred approach:** Update the complete Aspire cohort to `13.5.4` (matching `13.5.4-preview.1.26464.4` for Keycloak), service direct ASP.NET Core/EF Core advisory parents to `10.0.12`, and add the narrow Web-unit-test-only `AngleSharp` override. This is the smallest publisher-aligned change set that addresses all identified warnings.

```text
AppHost SDK/integrations + Aspire.Hosting.Testing references -> Aspire 13.5.4 cohort
Direct OpenAPI/Data Protection/EF Core parents -> .NET 10.0.12 servicing cohort
bunit.web transitively vulnerable AngleSharp -> temporary Web unit-test override >= 1.5.0
```

**Implementation details:**

1. Update all direct Aspire references atomically. Either inspect and accept the edits proposed by `aspire update --yes --non-interactive`, or make the explicit package edits manually. Do not use both approaches.
2. Update the direct ASP.NET Core/EF Core families noted above to `10.0.12`. Do not use standalone `Microsoft.OpenApi`, Data Protection, XML Crypto, or MessagePack pins to mask outdated parents.
3. Add a direct AngleSharp test-project reference meeting the `1.5.0` fixed-version minimum. Remove it when bUnit publishes a stable release with a patched dependency minimum.
4. Keep `AspireUseCliBundle` unset and do not introduce Central Package Management. Both are reasonable separate governance decisions, but neither is necessary for the upgrade and each substantially increases the review surface.

```powershell
aspire update --self
# Review the proposed edits. Use this command only when manual package edits are not used.
aspire update --yes --non-interactive
dotnet restore TNC.Trading.Platform.slnx
dotnet build TNC.Trading.Platform.slnx --no-restore
dotnet list TNC.Trading.Platform.slnx package --include-transitive
```

#### Considered Alternatives

* **Aspire cohort only:** necessary but rejected because OpenAPI, Data Protection/XML Crypto, and AngleSharp advisories remain.
* **Direct transitive pins for every warning:** rejected because it overrides tested parent dependency cohorts, duplicates dependency knowledge, and would leave vulnerable parent packages in place. In particular, direct MessagePack pinning under Aspire's RPC stack is unsupported.
* **Central Package Management or CLI bundle now:** deferred. The repository has no established policy for either; adding them is unrelated migration work rather than a prerequisite for secure upgrades.

### Runtime and Regression Validation

**Requirements:** Validate both source compatibility and distributed runtime behavior, particularly preview Keycloak and the ServiceDefaults discovery path.

**Preferred approach:** Run a staged check sequence after restore, then use focused unit tests before Docker-backed AppHost integration/E2E testing.

```powershell
dotnet build TNC.Trading.Platform.slnx --no-restore
dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj --no-restore
dotnet test test/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.UnitTests/TNC.Trading.Platform.AppHost.UnitTests.csproj --no-restore
dotnet test test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/TNC.Trading.Platform.Api.IntegrationTests.csproj --no-restore
dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/TNC.Trading.Platform.Web.FunctionalTests.csproj --no-restore
dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/TNC.Trading.Platform.Web.E2ETests.csproj --no-restore
aspire run
```

The acceptance checks are:

* Build and restore report no `NU1902`, `NU1903`, or `NU1904` warnings without suppression.
* `dotnet nuget why` no longer resolves baseline vulnerable versions.
* AppHost tests retain `GetEnvironmentVariableValuesAsync(DistributedApplicationOperation.Publish)` because this repository has observed run-mode resolution can hang.
* With Docker available, SQL Server, Mailpit, Keycloak, API, and Web are healthy; Keycloak realm import, stable authority/port, OIDC sign-in, persistent state, and authenticated browser flow succeed.
* API and Web successfully resolve `https+http://api`; health endpoints, Scalar/OpenAPI, and launch-profile/dashboard behavior work as expected.

## Potential Next Research

* Confirm whether a deployment used the vulnerable Data Protection version while internet-exposed.
  * Reasoning: The [Data Protection advisory](https://github.com/advisories/GHSA-9mv3-2cwr-p262) advises review of protected artifacts and potential key rotation when affected deployments were exposed.
* Re-evaluate Central Package Management after this bounded update.
  * Reasoning: Direct versions across multiple projects make version skew likely, but adoption needs an explicit repository-wide ownership, update-cadence, transitive-pinning, and lock-file policy.
