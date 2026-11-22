---
title: Upgrade and Warning Remediation Alternatives
description: Research-backed alternatives for upgrading Aspire and eliminating NuGet vulnerability warnings
author: GitHub Copilot
ms.date: 2026-09-17
ms.topic: reference
keywords:
  - .NET Aspire
  - NuGet
  - vulnerability remediation
  - dependency management
---

## Research Status

Status: Complete

## Scope

Evaluate concrete Aspire 13.5 upgrade and NuGet vulnerability-warning
remediation alternatives for this repository without modifying product files.
This extends repository-aspire-baseline-research.md and
aspire-13-5-official-research.md in the same directory.

## Executive Decision

Select **coordinated Aspire plus direct servicing, with one narrow test-only
transitive override**. This is the smallest evidence-backed approach expected
to produce a warning-free restore/build without `NoWarn`, audit-level changes,
or vulnerability suppression:

* Update the complete Aspire cohort to the compatible 13.5 servicing versions.
* Update the directly owned ASP.NET Core Data Protection, OpenAPI, and EF Core
  package families together to the current verified .NET 10 servicing version.
* Add one direct `AngleSharp` reference to the Web unit-test project, at or
  above the advisory's fixed version, because the current bUnit package is
  itself the newest published stable version and retains the vulnerable
  transitive minimum.

Do not add Central Package Management or `AspireUseCliBundle` in this change.
They are useful policy decisions but are not prerequisites for a secure,
warning-free upgrade and each widens the review surface.

## Repository and CLI Findings

`global.json:1-6` pins .NET SDK `10.0.200`; the solution targets `net10.0`.
The previous Aspire research confirms that the SDK is compatible with Aspire
13.5 and that 13.5 requires a coordinated Aspire family update.

No `Directory.Packages.props`, `Directory.Build.props`, `Directory.Build.targets`,
`NuGet.config`, or lock files exist. Versions are directly owned in project
files. There are no `.github/workflows` files. The only checked-in script is
`.github/scripts/Invoke-CleanArchitectureQualityGate.ps1`, which does not use
the Aspire CLI.

A workspace search found no `aspire ps --resources` or
`aspire ps --include-hidden` invocation. The remaining Aspire commands are
supported: `.github/instructions/aspire.instructions.md:51-52` and
`.github/skills/run-aspire/SKILL.md:16-31` use `aspire run` and `aspire deploy`;
`docs/001-project-scaffolding-and-devex/plans/001-delivery-plan.md:144`,
`docs/005-refactor-app-host/plans/001-delivery-plan.md:199`, and
`docs/wiki/local-development.md:105` use `aspire run`. Therefore, the 13.5
removal of the two `aspire ps` flags needs no repository edit.

The AppHost has `Aspire.AppHost.Sdk/13.2.3` at
`src/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.csproj:1` and
direct Keycloak and SQL Server integrations at lines 4-5. Four test projects
directly use `Aspire.Hosting.Testing 13.2.3`:

* test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/TNC.Trading.Platform.Api.IntegrationTests.csproj:11
* test/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.UnitTests/TNC.Trading.Platform.AppHost.UnitTests.csproj:11
* test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/TNC.Trading.Platform.Web.E2ETests.csproj:11
* test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/TNC.Trading.Platform.Web.FunctionalTests.csproj:11

## Warning Attribution

The baseline build reports 22 distinct NU1902/NU1903/NU1904 advisory messages
(99 total warning occurrences). They reduce to five resolved package families.
`dotnet nuget why` was used against each affected project; the following are
the authoritative local paths.

| Warning family and count | Resolved version | Direct owner and dependency path | Remediation condition |
| --- | --- | --- | --- |
| `MessagePack`, 11 advisories | `2.5.192` | AppHost SDK, Keycloak, and SQL Server integration -> `Aspire.Hosting 13.2.3` / `StreamJsonRpc 2.22.23` -> MessagePack | Aspire 13.5.4 resolves `StreamJsonRpc 2.25.29` -> `MessagePack 2.5.302` |
| `AngleSharp`, 1 advisory | `1.2.0` | `bunit.web 1.40.0` at `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj:15` -> AngleSharp | Direct `AngleSharp >= 1.5.0` override in this test project |
| `Microsoft.OpenApi`, 1 advisory | `2.0.0` | API line 11 and ServiceDefaults line 17 `Microsoft.AspNetCore.OpenApi 10.0.5` -> OpenApi | Upgrade those parents to `10.0.12`, which requires `[2.12.0, 3.0.0)` |
| `System.Security.Cryptography.Xml`, 8 advisories | `10.0.5` | DataProtection EntityFrameworkCore 10.0.5 -> DataProtection 10.0.5 -> XML Crypto | Upgrade the parent Data Protection/EF family to `10.0.12` |
| `Microsoft.AspNetCore.DataProtection`, 1 advisory | `10.0.5` | Same three DataProtection EntityFrameworkCore direct owners | Upgrade the parent Data Protection/EF family to `10.0.12` |

The three direct Data Protection owners are
`src/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.csproj:20`,
`src/TNC.Trading.Platform.ServiceDefaults/TNC.Trading.Platform.ServiceDefaults.csproj:13`,
and
`test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/TNC.Trading.Platform.Infrastructure.IntegrationTests.csproj:12`.
Their paired direct EF references are at Infrastructure lines 21 and 25-26,
ServiceDefaults line 14, and integration-test line 14. Keeping that EF cohort
on one servicing version is required because DataProtection EntityFrameworkCore
10.0.12 declares DataProtection and EF Core 10.0.12 dependencies.

## Verified Version Evidence

The following are **current available versions observed on 2026-09-17**, not a
permanent "always latest" policy. Before implementation, refresh the source
indexes and keep every package family internally aligned.

| Package family | Current verified candidate | Evidence and compatibility |
| --- | --- | --- |
| Aspire stable | `Aspire.AppHost.Sdk`, `Aspire.Hosting.SqlServer`, and `Aspire.Hosting.Testing` `13.5.4` | Prior official research recorded the corresponding NuGet flat-container indexes and Aspire's instruction not to mix release trains |
| Aspire preview | `Aspire.Hosting.Keycloak` `13.5.4-preview.1.26464.4` | Matching 13.5 integration train; Keycloak remains preview |
| ASP.NET Core/EF Core 10 | `Microsoft.AspNetCore.OpenApi`, `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore`, `Microsoft.EntityFrameworkCore.Design`, `Microsoft.EntityFrameworkCore.InMemory`, and `Microsoft.EntityFrameworkCore.SqlServer` `10.0.12` | NuGet package metadata confirms net10.0 assets; the OpenAPI manifest requires `Microsoft.OpenApi [2.12.0, 3.0.0)` and the Data Protection EF manifest requires DataProtection/EF Core `10.0.12` |
| bUnit exception | `AngleSharp 1.5.0` minimum | The advisory fixes versions below `1.5.0`; `bunit.web 1.40.0` is the latest published stable version and its net9.0 group declares `AngleSharp 1.2.0` |

Do not pin `MessagePack 3.1.8` directly. It is a newer available package, but
the verified Aspire 13.5 `StreamJsonRpc 2.25.29` manifest deliberately depends
on the compatible 2.x servicing version `2.5.302`. The package page reports no
known-vulnerability banner for `2.5.302`; final confirmation remains a restore
and build gate.

## Alternatives

### A. Aspire Cohort Only

Update only the AppHost SDK/integrations and four testing-package references to
the 13.5 values above. This is necessary for the Aspire upgrade and removes the
11 MessagePack advisory messages through a supported upstream dependency path.

It does **not** meet the warning-free goal: the 11 warnings from AngleSharp,
OpenAPI, XML Crypto, and Data Protection remain. Source compatibility risk is
low, although Keycloak is preview and requires AppHost-backed testing. Use this
only when the release is explicitly limited to Aspire behavior rather than
security-warning remediation.

### B. Coordinated Aspire Plus Direct Servicing

Perform Alternative A plus these direct package changes:

* Set `Microsoft.AspNetCore.OpenApi` to `10.0.12` in API and ServiceDefaults
* Set all direct `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore` and
  direct EF Core Design/InMemory/SqlServer references to `10.0.12`

This uses publisher-supported, same-major servicing updates, preserves the
existing API and Data Protection programming model, and removes the OpenAPI,
Data Protection, and XML Crypto advisory paths. It deliberately avoids a
standalone `Microsoft.OpenApi` pin because the parent manifest supplies a
patched compatible range.

By itself, this leaves the bUnit AngleSharp warning. It is the best production
dependency posture but is not independently warning-free.

### C. Direct Transitive Pins and Overrides

Keep vulnerable parents in place and add direct pins in each consuming project:
`MessagePack 2.5.302`, `Microsoft.OpenApi >= 2.7.5`,
`Microsoft.AspNetCore.DataProtection >= 10.0.7`,
`System.Security.Cryptography.Xml >= 10.0.10`, and `AngleSharp >= 1.5.0`.

This can force a clean restore with fewer parent upgrades, but it duplicates
transitive knowledge across projects, bypasses tested upstream dependency
cohorts, and makes future upgrades harder to reason about. The Data Protection
and OpenAPI parent packages would remain on old servicing releases, and direct
MessagePack intervention is particularly undesirable because it lies under the
AppHost's RPC implementation. Do not select this as the primary strategy.

### Selected Hybrid

Implement Alternative B and add only the `AngleSharp >= 1.5.0` direct override
to the Web unit-test project. The bUnit manifest uses a minimum dependency
version rather than an exact version range, so NuGet's direct-dependency-wins
resolution can select the patched package. This is narrowly scoped to tests and
should be treated as temporary: remove it when bUnit publishes a compatible
release with a patched AngleSharp minimum.

## Compatibility and Risk Assessment

Aspire source/API risk is limited to the existing Keycloak resource, SQL Server,
Mailpit, project references, endpoint wiring, and testing harness. Prior
research found no 13.5-removed source API use. The release notes explicitly
warn against mixing Aspire release trains, which is why every direct
`Aspire.Hosting.Testing` reference must move together.

The .NET 10 changes are servicing updates, not major/minor API upgrades. They
still affect authentication cookies, durable Data Protection keys, EF provider
behavior, migrations/tooling, and OpenAPI output. The Data Protection advisory
states version `10.0.7` is the fix and advises key-ring/token review if an
affected deployment was exposed; `10.0.12` is a later current servicing release.

The bUnit override may expose a test-only compatibility issue in AngleSharp CSS
or Diffing packages. The known direct consumer uses bUnit rendering and markup
comparison, so the focused Web unit-test suite is the primary gate. No product
source uses AngleSharp directly.

## Central Package Management and CLI Bundle

Do **not** introduce Central Package Management now. It would add a new root
configuration file and migrate version ownership across every project while the
security objective only touches a bounded set of direct references. Consider it
as a separate repository-wide dependency-governance change after this upgrade;
that work should establish ownership, update cadence, transitive-pinning rules,
and lock-file policy first.

Do **not** set `AspireUseCliBundle=true` now. Aspire 13.5 templates opt new
projects into a bundled CLI, while existing projects remain unaffected unless
they choose it. The repository already uses the independently installed CLI in
docs and has no established version-pinning policy for a bundled binary. Evaluate
it separately if reproducible developer CLI provisioning is a concrete goal.

## Practical Implementation and Test Gates

Use the fixed versions above only after an owner approves the servicing level.
The implementation change should update project references atomically, then run:

```powershell
aspire update --self
aspire update --yes --non-interactive
dotnet restore TNC.Trading.Platform.slnx
dotnet build TNC.Trading.Platform.slnx --no-restore
dotnet list TNC.Trading.Platform.slnx package --include-transitive
dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj --no-restore
dotnet test test/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.UnitTests/TNC.Trading.Platform.AppHost.UnitTests.csproj --no-restore
dotnet test test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/TNC.Trading.Platform.Api.IntegrationTests.csproj --no-restore
dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/TNC.Trading.Platform.Web.FunctionalTests.csproj --no-restore
dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/TNC.Trading.Platform.Web.E2ETests.csproj --no-restore
aspire run
```

Before accepting `aspire update`, review its package-edit plan and confirm it
does not move unrelated direct dependencies. If manual edits are preferred, do
not run the update command; apply the selected package changes, then execute the
same restore/build/test sequence. Docker-backed tests require Docker, SQL Server,
and Keycloak availability. AppHost unit tests that inspect environment values
should retain `GetEnvironmentVariableValuesAsync(DistributedApplicationOperation.Publish)`.

Acceptance criteria:

* Restore and build have zero NU1902, NU1903, and NU1904 messages without
  warning suppression
* `dotnet nuget why` confirms the five vulnerable baseline versions no longer
  resolve
* AppHost tests validate the resource model, stable Keycloak port/realm import,
  and publish-mode environment wiring
* API/Web functional and E2E tests validate OIDC login, authenticated flows,
  `https+http://api` service discovery, health endpoints, OpenAPI/Scalar, SQL,
  and Mailpit
* A manual AppHost smoke test confirms healthy resources and the expected VS
  Code/dashboard browser workflow

## Sources

* [Aspire 13.5 release notes](https://aspire.dev/whats-new/aspire-13-5/)
* [Upgrade Aspire](https://aspire.dev/whats-new/upgrade-aspire/)
* [Aspire update command](https://aspire.dev/reference/cli/commands/aspire-update/)
* [Aspire Hosting 13.5.4 manifest](https://api.nuget.org/v3-flatcontainer/aspire.hosting/13.5.4/aspire.hosting.nuspec)
* [StreamJsonRpc 2.25.29 manifest](https://api.nuget.org/v3-flatcontainer/streamjsonrpc/2.25.29/streamjsonrpc.nuspec)
* [MessagePack 2.5.302](https://www.nuget.org/packages/MessagePack/2.5.302)
* [Microsoft.AspNetCore.OpenApi 10.0.12 manifest](https://api.nuget.org/v3-flatcontainer/microsoft.aspnetcore.openapi/10.0.12/microsoft.aspnetcore.openapi.nuspec)
* [Data Protection EntityFrameworkCore 10.0.12 manifest](https://api.nuget.org/v3-flatcontainer/microsoft.aspnetcore.dataprotection.entityframeworkcore/10.0.12/microsoft.aspnetcore.dataprotection.entityframeworkcore.nuspec)
* [Microsoft OpenAPI advisory GHSA-v5pm-xwqc-g5wc](https://github.com/advisories/GHSA-v5pm-xwqc-g5wc)
* [ASP.NET Core Data Protection advisory GHSA-9mv3-2cwr-p262](https://github.com/advisories/GHSA-9mv3-2cwr-p262)
* [XML Crypto advisory GHSA-23rf-6693-g89p](https://github.com/advisories/GHSA-23rf-6693-g89p)
* [AngleSharp advisory GHSA-pgww-w46g-26qg](https://github.com/advisories/GHSA-pgww-w46g-26qg)
* [bunit.web 1.40.0 manifest](https://api.nuget.org/v3-flatcontainer/bunit.web/1.40.0/bunit.web.nuspec)
* [bunit.web version index](https://api.nuget.org/v3-flatcontainer/bunit.web/index.json)

## Unresolved Items

* The selected versions are current as of the research date. Confirm available
  servicing versions and release-management policy immediately before editing.
* This research did not perform a temporary restore with the selected changes;
  the specified restore/build and AppHost-backed test gates remain required to
  prove zero warnings and runtime compatibility.
* Confirm whether deployments using the vulnerable Data Protection package were
  internet-exposed during the affected window. If so, follow the advisory's
  operational guidance for Data Protection key rotation and review of
  long-lived protected artifacts.