---
title: Failed-Build Remediation Confirmation
description: Confirmed contract mismatch, validation commands, and SQL integration test prerequisites.
ms.date: 2026-09-14
ms.topic: troubleshooting
---

## Status

Complete. The focused build reproduces one compiler error, `CS7036`, in the
Infrastructure SQL integration test project. The evidence supports a test-only
call-site repair.

## Scope

* Confirm the current `NudgeAuthenticationAsync` call shape in the failing SQL
	integration test
* Confirm the interface and Entity Framework implementation signatures
* Identify exact test and build commands available from project metadata
* Determine whether integration test infrastructure prerequisites can block
	targeted execution

## Confirmed Contract Mismatch

The focused build command below exited with code `1` on 2026-09-14 and emitted
`CS7036` at the call on
test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/AccountPreferencesSqlIntegrationTests.cs:240.

```powershell
dotnet build .\test\TNC.Trading.Platform.Infrastructure\TNC.Trading.Platform.Infrastructure.IntegrationTests\TNC.Trading.Platform.Infrastructure.IntegrationTests.csproj --no-restore -clp:ErrorsOnly
```

The current test call supplies five arguments:

```csharp
await store.NudgeAuthenticationAsync(
		PlatformEnvironmentKind.Test,
		BrokerEnvironmentKind.Demo,
		"account-1",
		DateTimeOffset.UtcNow.AddMinutes(-1),
		fixture.CancellationToken);
```

The current inner-owned store contract requires seven arguments in this order:

```csharp
Task<bool> NudgeAuthenticationAsync(
		PlatformEnvironmentKind platformEnvironment,
		BrokerEnvironmentKind brokerEnvironment,
		string accountId,
		string authenticationSnapshotId,
		DateTimeOffset authenticatedAtUtc,
		DateTimeOffset dueAtUtc,
		CancellationToken cancellationToken);
```

Evidence: src/TNC.Trading.Platform.Application/Features/AccountPreferences/IAccountPreferencesCurrentStateStore.cs:9.

The EF adapter implements the same signature at
src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfAccountPreferencesCurrentStateStore.cs:28.
It stores `authenticationSnapshotId` and assigns `dueAtUtc` to `NextRetryAtUtc`
at lines 32-33. `authenticatedAtUtc` is accepted but is not currently persisted.

The production handler already uses the full contract at
src/TNC.Trading.Platform.Application/Features/AccountPreferences/NudgeAccountPreferencesVerificationHandler.cs:11.
It supplies the request snapshot identifier, request authenticated time, and
the current UTC time as the due time.

## Precise Replacement

Replace only the stale test invocation with an explicit snapshot ID and one
past timestamp. A single variable keeps the time relationship deterministic and
preserves the test's due-work intent.

```csharp
var authenticatedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1);
await store.NudgeAuthenticationAsync(
		PlatformEnvironmentKind.Test,
		BrokerEnvironmentKind.Demo,
		"account-1",
		"authentication-snapshot-1",
		authenticatedAtUtc,
		authenticatedAtUtc,
		fixture.CancellationToken);
```

The test commits desired state immediately before the nudge at
AccountPreferencesSqlIntegrationTests.cs:239, then claims due work using the
current UTC time at line 242. A past `dueAtUtc` preserves the existing
assertion. Do not add a legacy overload or optional parameters; either would
weaken the application-owned contract solely for obsolete test data.

## Project Metadata And Commands

The project targets `net10.0` at
test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/TNC.Trading.Platform.Infrastructure.IntegrationTests.csproj:4.
It references `Microsoft.NET.Test.Sdk` 17.14.1, xUnit 2.9.3, and the Visual
Studio xUnit runner 2.8.2 at lines 15-17. It references Infrastructure and
ServiceDefaults at lines 25-26. The repository pins SDK 10.0.200 with
`latestFeature` roll-forward in global.json:3-4.

Use this validation sequence after the repair:

```powershell
dotnet build .\test\TNC.Trading.Platform.Infrastructure\TNC.Trading.Platform.Infrastructure.IntegrationTests\TNC.Trading.Platform.Infrastructure.IntegrationTests.csproj --no-restore -clp:ErrorsOnly
dotnet test .\test\TNC.Trading.Platform.Infrastructure\TNC.Trading.Platform.Infrastructure.IntegrationTests\TNC.Trading.Platform.Infrastructure.IntegrationTests.csproj --no-build --no-restore --filter "FullyQualifiedName~AccountPreferencesSqlIntegrationTests"
dotnet build .\TNC.Trading.Platform.slnx --no-restore -clp:ErrorsOnly
```

The first command is the cheapest check for the repair. The second requires the
first to succeed because it uses `--no-build`.

## Integration Test Prerequisites

The xUnit collection fixture initializes through Docker rather than a caller
provided connection string:

* It finds a running container using the exact image ancestor
	`mcr.microsoft.com/mssql/server:2022-latest` at
	test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/SqlServerDatabaseFixture.cs:18
* It reads `MSSQL_SA_PASSWORD` from container configuration at line 25 and
	discovers the published `1433/tcp` port at line 26
* It creates a uniquely named database at line 50 and drops it during disposal
	at line 101
* It applies a 45-second fixture operation timeout at lines 10-11 and disables
	parallel execution for the shared SQL Server collection at line 134

The required container was present during research: `docker ps` returned
container ID `edd8933fe761` for that image. Targeted test execution remains
blocked by the compiler error until the call is repaired. After compilation, it
can still fail if Docker inspection or port discovery fails, the container lacks
`MSSQL_SA_PASSWORD`, SQL Server rejects the `sa` connection, or database
creation and cleanup permissions are unavailable. Those conditions remain
unverified because compilation stopped before xUnit could initialize the fixture.

## Recommended Plan

1. Update the invocation at AccountPreferencesSqlIntegrationTests.cs:240 with
	 the exact seven-argument call above. Keep production contracts and adapters
	 unchanged.
2. Run the focused project build and confirm `CS7036` is absent.
3. Run the filtered SQL integration test suite and confirm the fixture creates,
	 migrates, resets, and removes its temporary database.
4. Build the solution to expose any unrelated remaining compiler failures.
5. Track the emitted NuGet vulnerability warnings separately. They are not the
	 compiler failure investigated here and need package-graph and compatibility
	 analysis before version changes.

## Follow-On Questions

* Should a future behavioral test assert persistence of `authenticatedAtUtc`?
	The current EF adapter receives it but does not map it to an entity field.
* Are the known NuGet advisory warnings in scope for the same remediation work
	item, or should they remain a dependency-security change?
