---
title: Build Failure Diagnostics
description: Evidence-based investigation of the failed workspace dotnet build.
author: GitHub Copilot
ms.date: 2026-09-14
ms.topic: troubleshooting
keywords:
  - dotnet build
  - build diagnostics
  - TNC Trading Platform
estimated_reading_time: 5
---

## Research Scope

Investigate the failed workspace `dotnet build`. Record the exact errors and
warnings, owning source and project paths, line references, verified root cause,
minimal remedy, alternatives, proposed snippets, and validation commands.
Product code is out of scope for modification.

## Status

Complete. No product code was edited. The error was reproduced from both the
solution and its owning integration-test project.

## Evidence Log

The following commands were run from the workspace root on 2026-09-14:

```powershell
dotnet build .\TNC.Trading.Platform.slnx --no-restore
dotnet build .\test\TNC.Trading.Platform.Infrastructure\TNC.Trading.Platform.Infrastructure.IntegrationTests\TNC.Trading.Platform.Infrastructure.IntegrationTests.csproj --no-restore -clp:ErrorsOnly
```

The solution command exited with code `1` in 4.96 seconds and reported `127`
warnings and `1` error. The focused command exited with code `1` in 1.4 seconds
and reported the same compiler error. It also reported 11 dependency warnings:
10 from the target project and one from its ServiceDefaults project reference.

The compiler diagnostic emitted by both commands was:

```text
test\TNC.Trading.Platform.Infrastructure\TNC.Trading.Platform.Infrastructure.IntegrationTests\AccountPreferencesSqlIntegrationTests.cs(240,21): error CS7036: There is no argument given that corresponds to the required parameter 'dueAtUtc' of 'EfAccountPreferencesCurrentStateStore.NudgeAuthenticationAsync(PlatformEnvironmentKind, BrokerEnvironmentKind, string, string, DateTimeOffset, DateTimeOffset, CancellationToken)'
```

`git blame` attributes both the failing call and the current interface method
signature to commit `6d38c494`, authored on 2026-08-31. `git log` identifies the
commit as `feat: Add Account Preferences Current State and Desired State Audits
tables`.

## Findings

### Build-Blocking Error

The only build-stopping diagnostic is `CS7036` in
test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/AccountPreferencesSqlIntegrationTests.cs:240.
The call supplies the old five-argument form:

```csharp
await store.NudgeAuthenticationAsync(
  PlatformEnvironmentKind.Test,
  BrokerEnvironmentKind.Demo,
  "account-1",
  DateTimeOffset.UtcNow.AddMinutes(-1),
  fixture.CancellationToken);
```

The current interface requires seven arguments, including
`authenticationSnapshotId`, `authenticatedAtUtc`, and `dueAtUtc`:

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

Evidence for the owning contract and implementation:

* src/TNC.Trading.Platform.Application/Features/AccountPreferences/IAccountPreferencesCurrentStateStore.cs:9 declares the seven-argument contract
* src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfAccountPreferencesCurrentStateStore.cs:28 implements that contract
* src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfAccountPreferencesCurrentStateStore.cs:32-33 persists the snapshot ID and assigns `dueAtUtc` to `NextRetryAtUtc`
* src/TNC.Trading.Platform.Application/Features/AccountPreferences/NudgeAccountPreferencesVerificationHandler.cs:11 is a current production call site that supplies all arguments

### Verified Root Cause

The integration test and method contract were introduced by the same commit, but
the test supplies five arguments while the current contract requires seven. The
compiler binds the fourth test argument
(`DateTimeOffset`) to `authenticationSnapshotId` (`string`), cannot bind the
required `dueAtUtc` parameter, and emits `CS7036`.

This is a test-source contract mismatch. There is no evidence of a failure in the
Application, Infrastructure, or ServiceDefaults production projects: each built
successfully during the focused build.

### Minimal Recommended Remedy

Update only the failing integration test call to model the authentication event
required by the current contract. Use a named snapshot identifier, an
authentication time, and a retry time that remains in the past so the existing
"due work" assertion retains its purpose.

```csharp
var dueAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1);
await store.NudgeAuthenticationAsync(
  PlatformEnvironmentKind.Test,
  BrokerEnvironmentKind.Demo,
  "account-1",
  "authentication-snapshot-1",
  dueAtUtc,
  dueAtUtc,
  fixture.CancellationToken);
```

This is the smallest localized remedy. It supplies the two missing conceptual
values and preserves the test's existing claim that a state with past-due retry
time is returned by `ClaimDueWorkAsync`.

### Alternatives Evaluated

| Alternative | Result | Evidence-based assessment |
| --- | --- | --- |
| Restore a legacy five-argument overload in the interface and implementation | Would compile the current call | Not recommended. The current production call supplies authentication provenance and an explicit due time; adding an overload changes production API surface solely to accommodate one stale test |
| Make new parameters optional | Would permit the old invocation | Not recommended. A default snapshot ID or timestamp has no verified domain meaning and weakens the explicit current contract |
| Update the integration test call | Recommended | Matches the declared interface, implementation, and production handler without changing product code |
| Suppress warnings or treat them as the root cause | Rejected | The build summary has one error, all warnings are `NU` vulnerability advisories, and compilation stops on `CS7036` |

### Warning Inventory

The solution summary reports `127 Warning(s)`. The build transcript contained
each warning twice, so this inventory deduplicates by project, diagnostic code,
package, version, severity, and advisory URL. It has exactly 127 unique warning
occurrences, matching the build summary.

| Package and version | Diagnostic severity counts | Unique advisories | Affected projects | Warning occurrences |
| --- | --- | ---: | ---: | ---: |
| AngleSharp 1.2.0 | 1 moderate | 1 | 1 | 1 |
| MessagePack 2.5.192 | 45 moderate, 10 high | 11 | 5 | 55 |
| Microsoft.AspNetCore.DataProtection 10.0.5 | 4 critical | 1 | 4 | 4 |
| Microsoft.OpenApi 2.0.0 | 7 high | 1 | 7 | 7 |
| Scriban.Signed 5.5.0 | 8 moderate, 16 high, 2 critical | 13 | 2 | 26 |
| System.Linq.Dynamic.Core 1.3.12 | 2 high | 1 | 2 | 2 |
| System.Security.Cryptography.Xml 10.0.5 | 32 high | 8 | 4 | 32 |

The 12 warning-emitting project paths and their unique warning counts are:

* src/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.csproj: 1
* src/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.csproj: 11
* src/TNC.Trading.Platform.ServiceDefaults/TNC.Trading.Platform.ServiceDefaults.csproj: 1
* src/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.csproj: 1
* test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/TNC.Trading.Platform.Api.IntegrationTests.csproj: 11
* test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/TNC.Trading.Platform.Api.UnitTests.csproj: 10
* test/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.UnitTests/TNC.Trading.Platform.AppHost.UnitTests.csproj: 21
* test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/TNC.Trading.Platform.Infrastructure.IntegrationTests.csproj: 10
* test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/TNC.Trading.Platform.Infrastructure.UnitTests.csproj: 9
* test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/TNC.Trading.Platform.Web.E2ETests.csproj: 25
* test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/TNC.Trading.Platform.Web.FunctionalTests.csproj: 25
* test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj: 2

The focused build directly confirms these warnings for the failing test project:

* `NU1904` for Microsoft.AspNetCore.DataProtection 10.0.5, critical advisory `GHSA-9mv3-2cwr-p262`
* `NU1903` for Microsoft.OpenApi 2.0.0, high advisory `GHSA-v5pm-xwqc-g5wc`
* Eight `NU1903` warnings for System.Security.Cryptography.Xml 10.0.5, each high severity

These warnings are independent dependency-security remediation work. They are
not compile errors and did not cause the failed build. The build also emitted
`NETSDK1057`, an informational notice that the installed .NET SDK is preview.

## Proposed Validation

After applying the test-only remedy, run the narrow build first:

```powershell
dotnet build .\test\TNC.Trading.Platform.Infrastructure\TNC.Trading.Platform.Infrastructure.IntegrationTests\TNC.Trading.Platform.Infrastructure.IntegrationTests.csproj --no-restore
```

Then run the affected test class and the full solution build:

```powershell
dotnet test .\test\TNC.Trading.Platform.Infrastructure\TNC.Trading.Platform.Infrastructure.IntegrationTests\TNC.Trading.Platform.Infrastructure.IntegrationTests.csproj --no-build --filter FullyQualifiedName~AccountPreferencesSqlIntegrationTests
dotnet build .\TNC.Trading.Platform.slnx --no-restore
```

The expected result is no `CS7036` and a successful build. Existing NuGet
vulnerability warnings are expected to remain until package remediation is
performed separately.

## Remaining Uncertainty

The compile failure, its owner, and the narrow test-only fix are verified. The
evidence does not establish which safe target versions or dependency paths should
be used to remediate every vulnerability warning. Determine those separately with
`dotnet list <project> package --vulnerable --include-transitive`, followed by
compatibility testing for each proposed package update.

## Follow-On Questions

No user input is needed to fix the compile error. Package-warning remediation
requires a separate dependency update decision because this investigation did not
validate replacement versions.
