---
title: Aspire 13.5.4 Upgrade Scope Research
description: Verified package ownership, advisory remediation scope, validation commands, and repository constraints for the Aspire 13.5.4 upgrade.
ms.date: 2026-09-17
ms.topic: reference
---

## Status

Complete. The declared package owners, advisory-parent servicing scope, test-only
override location, validation commands, and repository constraints are verified.

## Upgrade Scope

Update the Aspire cohort atomically. The primary research identifies `13.5.4` as
the stable target and `13.5.4-preview.1.26464.4` as the matching Keycloak
preview target. Do not mix `13.2.3` and `13.5.4` Aspire packages.

| Project file | Current declaration | Required target |
| --- | --- | --- |
| `src/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.csproj:1` | `Aspire.AppHost.Sdk/13.2.3` | `13.5.4` |
| `src/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.csproj:4` | `Aspire.Hosting.Keycloak 13.2.3-preview.1.26217.6` | `13.5.4-preview.1.26464.4` |
| `src/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.csproj:5` | `Aspire.Hosting.SqlServer 13.2.3` | `13.5.4` |
| `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/TNC.Trading.Platform.Api.IntegrationTests.csproj:11` | `Aspire.Hosting.Testing 13.2.3` | `13.5.4` |
| `test/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.UnitTests/TNC.Trading.Platform.AppHost.UnitTests.csproj:11` | `Aspire.Hosting.Testing 13.2.3` | `13.5.4` |
| `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/TNC.Trading.Platform.Web.E2ETests.csproj:11` | `Aspire.Hosting.Testing 13.2.3` | `13.5.4` |
| `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/TNC.Trading.Platform.Web.FunctionalTests.csproj:11` | `Aspire.Hosting.Testing 13.2.3` | `13.5.4` |

The AppHost remains a composition-only root at
`src/TNC.Trading.Platform.AppHost/AppHost.cs:3-11`. Preserve its Keycloak realm
import and persistent resource composition, implemented at
`src/TNC.Trading.Platform.AppHost/AppHostInfrastructureRegistration.cs:17-36`.

## Advisory Remediation Scope

Move these direct advisory parents to `10.0.12`:

| Project file | Direct advisory parent |
| --- | --- |
| `src/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.csproj:11` | `Microsoft.AspNetCore.OpenApi` |
| `src/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.csproj:20` | `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore` |
| `src/TNC.Trading.Platform.ServiceDefaults/TNC.Trading.Platform.ServiceDefaults.csproj:13` | `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore` |
| `src/TNC.Trading.Platform.ServiceDefaults/TNC.Trading.Platform.ServiceDefaults.csproj:17` | `Microsoft.AspNetCore.OpenApi` |
| `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/TNC.Trading.Platform.Infrastructure.IntegrationTests.csproj:12` | `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore` |

Keep the adjacent EF Core packages in the same servicing cohort:

* `src/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.csproj:21`, `:25`, and `:26`: `Microsoft.EntityFrameworkCore.Design`, `InMemory`, and `SqlServer`
* `src/TNC.Trading.Platform.ServiceDefaults/TNC.Trading.Platform.ServiceDefaults.csproj:14`: `Microsoft.EntityFrameworkCore.SqlServer`
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/TNC.Trading.Platform.Infrastructure.IntegrationTests.csproj:14`: `Microsoft.EntityFrameworkCore.SqlServer`

The other direct `10.0.5` declarations found in API, Web, and AppHost unit
tests are not identified as advisory parents by the primary research. Do not
expand this servicing change without a fresh restore or advisory scan.

## AngleSharp Override

Add the temporary direct `AngleSharp` reference at version `1.5.0` or later only
to `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj`.
That project declares `bunit.web 1.40.0` at line 15, which brings the vulnerable
transitive `AngleSharp 1.2.0`. Remove the override when bUnit publishes a stable
release with a patched minimum dependency.

## Validation Sequence

The existing local development guide uses these repository-root commands:

```powershell
dotnet build
dotnet test
dotnet test -m:1
dotnet run --project src/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.csproj
aspire run
```

Sources: `docs/wiki/local-development.md:91`, `:99`, `:105`, `:195`, and `:201`.
The focused authentication commands are in
`docs/wiki/testing-and-quality.md:298-300`; baseline focused architecture and
API unit checks are at `docs/wiki/testing-and-quality.md:51-52`.

For this change, use the primary research's staged order:

```powershell
dotnet restore TNC.Trading.Platform.slnx
dotnet build TNC.Trading.Platform.slnx --no-restore
dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj --no-restore
dotnet test test/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.UnitTests/TNC.Trading.Platform.AppHost.UnitTests.csproj --no-restore
dotnet test test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/TNC.Trading.Platform.Api.IntegrationTests.csproj --no-restore
dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/TNC.Trading.Platform.Web.FunctionalTests.csproj --no-restore
dotnet test test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/TNC.Trading.Platform.Web.E2ETests.csproj --no-restore
```

Run `dotnet list TNC.Trading.Platform.slnx package --include-transitive` after
the build to confirm the resolved graph no longer contains the baseline
vulnerable packages. Preserve the AppHost unit tests' publish-mode environment
inspection at
`test/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.UnitTests/AppHostEnvironmentWiringTests.cs:212,249,322-323`.

## Repository Constraints

* `global.json:1-6` pins SDK `10.0.200`; all affected projects target `net10.0`.
* `.github/instructions/aspire.instructions.md` requires an Aspire AppHost
	composition root, shared service defaults, externalized configuration, and
	Aspire documentation checks before new feature use.
* `.github/instructions/auth.instructions.md` requires local Keycloak to be
	Aspire-orchestrated, use a stable local port, preserve realm imports, and keep
	OIDC/OAuth 2.0 compatibility.
* `.github/instructions/aspire-tests.instructions.md` requires AppHost tests to
	use `Aspire.Hosting.Testing`, remain closed-box, dispose launched resources,
	and avoid fixed ports by default.
* `.github/instructions/tests.instructions.md` requires unit-first validation
	and separate unit, integration, functional, and E2E project boundaries.
* `docs/wiki/local-development.md:10-27` makes Docker-managed infrastructure,
	including Keycloak, mandatory for the supported local runtime.

## Remaining Gaps And Questions

* Reconfirm the requested NuGet versions and advisory graph immediately before
	implementation with restore, because the primary research was collected on
	2026-09-17.
* Determine whether an internet-exposed deployment ran the affected Data
	Protection version. If so, assess the advisory's key-rotation guidance.
* No user decision is required to implement the scoped package update. Central
	Package Management, Aspire CLI bundling, and new Aspire 13.5 feature adoption
	remain intentionally out of scope.