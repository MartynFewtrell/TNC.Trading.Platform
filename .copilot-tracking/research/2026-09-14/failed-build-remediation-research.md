<!-- markdownlint-disable-file -->
# Task Research: Failed Build Remediation

The workspace `dotnet build` fails on one C# compiler error and emits 127
NuGet vulnerability warnings. This research separates the build-blocking defect
from the dependency-security backlog and selects a minimal, evidence-based
repair.

## Task Implementation Requests

* Research the failed project build.
* Recommend a way to resolve the compiler error and warnings.

## Scope and Success Criteria

* Scope: Diagnose the reported solution build failure and recommend remedies.
  Product-code changes are out of scope for this research task.
* Assumptions: The recorded `dotnet build` output represents the current
  workspace state, and the solution was restored before the `--no-restore`
  commands were executed.
* Success criteria:
  * Identify the exact blocking diagnostic, its controlling contract, and its
    root cause.
  * Recommend one minimal fix with focused validation commands.
  * Classify the warnings and provide a safe remediation path without claiming
    unverified package versions.

## Outline

1. Build evidence and diagnosis
2. Compiler-error remediation alternatives and recommendation
3. NuGet warning triage and remediation approach
4. Implementation and validation steps

## Research Executed

### Build Evidence

The following commands were executed from the workspace root on 2026-09-14:

```powershell
dotnet build .\TNC.Trading.Platform.slnx --no-restore
dotnet build .\test\TNC.Trading.Platform.Infrastructure\TNC.Trading.Platform.Infrastructure.IntegrationTests\TNC.Trading.Platform.Infrastructure.IntegrationTests.csproj --no-restore -clp:ErrorsOnly
```

The solution build exited with code `1`, reporting 127 warnings and one error.
The focused integration-test build also exited with code `1` and reproduced the
same error. It confirms that the failure belongs to that test project rather
than a solution-level orchestration problem.

### File Analysis

* test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/AccountPreferencesSqlIntegrationTests.cs:240 calls `NudgeAuthenticationAsync` with an obsolete five-argument shape.
* src/TNC.Trading.Platform.Application/Features/AccountPreferences/IAccountPreferencesCurrentStateStore.cs:9 declares the current seven-argument contract.
* src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfAccountPreferencesCurrentStateStore.cs:28 implements the contract, and lines 32-33 persist `authenticationSnapshotId` and map `dueAtUtc` to `NextRetryAtUtc`.
* src/TNC.Trading.Platform.Application/Features/AccountPreferences/NudgeAccountPreferencesVerificationHandler.cs:11 is the current production call site and supplies all seven arguments.

### Project Conventions

* Standards referenced: .github/copilot-instructions.md requires test names to
  follow the established `MethodName_StateUnderTest_ExpectedResult` convention
  and calls for focused automated validation.
* Instructions followed: .github/instructions/folders.instructions.md and the
  Task Researcher operating rules. This document is located under the required
  `.copilot-tracking/research/2026-09-14/` directory.

## Key Discoveries

### Build-Blocking Error

Both builds emitted the following diagnostic:

```text
test\TNC.Trading.Platform.Infrastructure\TNC.Trading.Platform.Infrastructure.IntegrationTests\AccountPreferencesSqlIntegrationTests.cs(240,21): error CS7036: There is no argument given that corresponds to the required parameter 'dueAtUtc' of 'EfAccountPreferencesCurrentStateStore.NudgeAuthenticationAsync(PlatformEnvironmentKind, BrokerEnvironmentKind, string, string, DateTimeOffset, DateTimeOffset, CancellationToken)'
```

The stale test invocation is equivalent to:

```csharp
await store.NudgeAuthenticationAsync(
  PlatformEnvironmentKind.Test,
  BrokerEnvironmentKind.Demo,
  "account-1",
  DateTimeOffset.UtcNow.AddMinutes(-1),
  fixture.CancellationToken);
```

The contract now requires a snapshot identifier, the authentication time, and an
explicit due time. The compiler interprets the fourth supplied value as the
required `authenticationSnapshotId` parameter, then cannot satisfy `dueAtUtc`.

### Root Cause

This is a test-source contract mismatch, not an Infrastructure behavior defect.
The current persistence implementation requires authentication provenance and a
separate retry due timestamp, and the production handler already provides both.
`git blame` attributed the stale call and current contract to commit `6d38c494`
from 2026-08-31, indicating that the test was incomplete at introduction rather
than that a subsequent contract migration was missed.

### Warning Inventory

The 127 warnings are dependency vulnerability advisories (`NU1902`, `NU1903`,
and `NU1904`), not compiler errors. Deduplicated occurrences are:

| Package and version | Severity counts | Projects | Warnings |
| --- | --- | ---: | ---: |
| AngleSharp 1.2.0 | 1 moderate | 1 | 1 |
| MessagePack 2.5.192 | 45 moderate, 10 high | 5 | 55 |
| Microsoft.AspNetCore.DataProtection 10.0.5 | 4 critical | 4 | 4 |
| Microsoft.OpenApi 2.0.0 | 7 high | 7 | 7 |
| Scriban.Signed 5.5.0 | 8 moderate, 16 high, 2 critical | 2 | 26 |
| System.Linq.Dynamic.Core 1.3.12 | 2 high | 2 | 2 |
| System.Security.Cryptography.Xml 10.0.5 | 32 high | 4 | 32 |

Twelve projects emit the warnings. The focused project alone shows one critical
DataProtection advisory, one high OpenApi advisory, and eight high
System.Security.Cryptography.Xml advisories. The installed SDK also emitted
`NETSDK1057`, an informational notice that it is a preview SDK.

## Technical Scenarios

### Restore the Failing Integration-Test Contract

The test should represent a complete authentication event and retain its
existing intention to expose state eligible for due-work claiming.

**Requirements:**

* Supply all parameters declared by `IAccountPreferencesCurrentStateStore`.
* Keep the state due in the past so `ClaimDueWorkAsync` remains eligible.
* Do not widen the production interface to support an obsolete test call.

**Considered Alternatives:**

| Alternative | Assessment | Decision |
| --- | --- | --- |
| Add a legacy five-argument overload | Compiles the test but expands the production contract solely for stale test data. | Rejected |
| Make the new parameters optional | Conceals missing domain data behind defaults with no verified meaning. | Rejected |
| Update the integration test with explicit event data | Aligns the test with the interface, implementation, and production handler. | Selected |

**Preferred Approach:**

Update only the call at
`test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/AccountPreferencesSqlIntegrationTests.cs:240`.

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

Using one past timestamp for both event time and due time is adequate for the
test's current assertion and avoids introducing unrelated timing behavior.
Alternatively, use a distinct earlier `authenticatedAtUtc` when the test needs
to assert ordering semantics in the future.

**Implementation Impact:**

* One test invocation changes.
* No production code or public API changes.
* The focused integration-test project should compile after the update.

### Remediate NuGet Vulnerability Warnings

The advisory warnings should be addressed after the compile error, as a
dependency upgrade work item. They have security significance but are not the
reason the build exits unsuccessfully today.

**Requirements:**

* Determine direct versus transitive dependency ownership before changing
  package versions.
* Upgrade packages to versions verified as non-vulnerable by the restore graph.
* Run compatibility tests for affected authentication, API documentation,
  serialization, templates, and XML cryptography surfaces.

**Considered Alternatives:**

| Alternative | Assessment | Decision |
| --- | --- | --- |
| Suppress `NU1902` through `NU1904` | Leaves known vulnerabilities untracked and masks security signals. | Rejected |
| Update every vulnerable package without graph analysis | Risks incompatible or ineffective transitive overrides. | Rejected |
| Inventory each affected project, then upgrade and test by dependency family | Establishes package ownership, captures safe target versions, and limits compatibility risk. | Selected |

**Preferred Approach:**

Use a dedicated dependency-remediation change. Begin with the critical
DataProtection and Scriban.Signed advisories, then high-severity packages. For
each affected project, identify the resolved dependency path before selecting a
version:

```powershell
dotnet list .\test\TNC.Trading.Platform.Infrastructure\TNC.Trading.Platform.Infrastructure.IntegrationTests\TNC.Trading.Platform.Infrastructure.IntegrationTests.csproj package --vulnerable --include-transitive
```

Repeat for the remaining warning-emitting projects, consolidate common package
version changes at the repository's existing dependency-management location,
and validate each family. This research did not verify safe replacement versions
or dependency paths, so no particular package version is recommended yet.

## Recommended Implementation Sequence

1. Update the stale `NudgeAuthenticationAsync` call in the integration test with
   an explicit snapshot ID and past-due timestamps.
2. Build the owning test project.
3. Run `AccountPreferencesSqlIntegrationTests`.
4. Build the solution to confirm no other compiler failures are exposed.
5. Create a separate dependency-security work item, inventory direct and
   transitive vulnerable dependencies, and update packages in validated groups.

## Validation Commands

```powershell
dotnet build .\test\TNC.Trading.Platform.Infrastructure\TNC.Trading.Platform.Infrastructure.IntegrationTests\TNC.Trading.Platform.Infrastructure.IntegrationTests.csproj --no-restore
dotnet test .\test\TNC.Trading.Platform.Infrastructure\TNC.Trading.Platform.Infrastructure.IntegrationTests\TNC.Trading.Platform.Infrastructure.IntegrationTests.csproj --no-build --filter FullyQualifiedName~AccountPreferencesSqlIntegrationTests
dotnet build .\TNC.Trading.Platform.slnx --no-restore
```

Expected result: the first error-free build should have no `CS7036`. The NuGet
advisories will remain until the separate package upgrades are completed.

## Evidence Sources

* Subagent diagnostic report:
  `.copilot-tracking/research/subagents/2026-09-14/build-failure-diagnostics.md`
* Contract and integration-prerequisite confirmation:
  `.copilot-tracking/research/subagents/2026-09-14/failed-build-remediation-confirmation.md`
* Current-state store contract:
  `src/TNC.Trading.Platform.Application/Features/AccountPreferences/IAccountPreferencesCurrentStateStore.cs:9`
* Entity Framework implementation:
  `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfAccountPreferencesCurrentStateStore.cs:28-33`
* Production call site:
  `src/TNC.Trading.Platform.Application/Features/AccountPreferences/NudgeAccountPreferencesVerificationHandler.cs:11`
* Failing test call:
  `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/AccountPreferencesSqlIntegrationTests.cs:240`

## Planning Confirmation

Focused follow-up research on 2026-09-14 confirmed that the target project is
a `net10.0` xUnit project and that the replacement needs an explicit snapshot
identifier plus two timestamps. It also established that the filtered SQL
integration suite uses a Docker-discovered
`mcr.microsoft.com/mssql/server:2022-latest` container. The fixture requires
the container to expose `MSSQL_SA_PASSWORD` and a published SQL Server port so
it can create, migrate, reset, and remove a unique database. The container was
present during research, but end-to-end connectivity remains unverified until
the compiler error is removed.

The implementation plan should therefore build the owning test project first,
then run the filtered integration suite, and finally build the solution. The
NuGet advisories remain a separate dependency-security work item because the
research does not yet establish direct versus transitive ownership or safe
compatible target versions.

## Potential Next Research

* Determine safe replacement versions and dependency paths for each vulnerable
  package family.
  * Reasoning: Existing evidence proves the warnings but not which upgrades are
    compatible with this solution.
  * Reference: `dotnet list <project> package --vulnerable --include-transitive`

## Package-Remediation Planning Update

Follow-up package research on 2026-09-14 established that all 127 advisory
warnings are transitive and that direct `PackageReference` declarations own
their resolution; this workspace has no central package-management file. It
verified a coordinated Aspire `13.5.3` family update, matching Keycloak
`13.5.3-preview.1.26425.3`, ASP.NET Core and EF Core `10.0.12` updates,
`WireMock.Net` `2.15.0` in the two web test projects, and a test-only
`AngleSharp` `1.8.1` override for bUnit. The updated objective is a literal
clean build: no compiler errors, no `NU1901` through `NU1904` advisories, and
no `NETSDK1057` preview-SDK notice. The plan must therefore select a supported
stable .NET 10 SDK compatible with the existing `net10.0` projects, after
confirming its version through Microsoft Learn during implementation.

See `.copilot-tracking/research/subagents/2026-09-14/failed-build-package-remediation.md`
for package chains, target versions, affected project files, compatibility
risks, and the required restore, build, test, and Docker-backed validation.