<!-- markdownlint-disable-file -->
# Unit-Test Project Routing Research

## Research Questions

* Identify the existing compatible API unit-test project for deterministic in-memory testing of `test/Shared/Authentication/ManagedAppHostFixture.cs`.
* Establish its project path, test conventions, shared-source linkage, and the minimal route to access an internal helper without placing a deterministic test in an Aspire integration project.

## Findings

### Exact Compatible Unit-Test Project

The existing compatible project is
`test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/TNC.Trading.Platform.Api.UnitTests.csproj`.
It is a `net10.0` xUnit project with only test packages and a direct project reference
to the API project: lines 1-21. The API production assembly grants this test assembly
access to API internals through
`[assembly: InternalsVisibleTo("TNC.Trading.Platform.Api.UnitTests")]` at
`src/TNC.Trading.Platform.Api/Properties/InternalsVisibleTo.cs:1-3`.

This is the appropriate project for an in-process deterministic delegate test. The
repository test rule requires a unit test to run in-process and in-memory, and directs
contributors to prefer unit tests when infrastructure is unnecessary:
`.github/instructions/tests.instructions.md:18-19,49-50`. The planned helper has only
three delegate inputs, `Func<Task>` and `Func<ValueTask>`, and explicitly has no Docker,
AppHost, or mutable fixture dependency:
`.copilot-tracking/details/2026-09-15/managed-app-host-fixture-cleanup-details.md:11-24`.

### Planning Reference Discrepancy

The current cleanup plan does not reference an API *unit-test* project for the
deterministic test. It instead specifies a new `ManagedAppHostFixtureTests.cs` under
the API integration project's `Authentication` folder, at
`.copilot-tracking/details/2026-09-15/managed-app-host-fixture-cleanup-details.md:81-102`,
and its focused command targets
`test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/TNC.Trading.Platform.Api.IntegrationTests.csproj`
at lines 109-119. That placement conflicts with the stated unit-test boundary because
the cases use only in-memory delegates and do not start an AppHost.

### Folder and Namespace Conventions

The API unit-test project currently keeps test classes at its project root, not in an
`Authentication` subfolder. Its test classes use the file-scoped namespace
`TNC.Trading.Platform.Api.UnitTests`, for example:

* `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/PlatformEndpointHandlerTests.cs:9-11`
* `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/PlatformApiAuthenticationServiceCollectionExtensionsTests.cs:9-11`

Tests use xUnit `[Fact]`, the `MethodName_StateUnderTest_ExpectedResult` naming pattern,
and XML comments that state traceability, verification, expected result, and operational
reason. Representative evidence is
`PlatformEndpointHandlerTests.cs:14-32` and
`PlatformApiAuthenticationServiceCollectionExtensionsTests.cs:12-47`. The repository
requires these conventions at `.github/instructions/tests.instructions.md:29-42`.

The API integration project uses a feature folder and a different namespace for
AppHost-related tests: `Authentication` at
`test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/Authentication/RealAuthenticationIntegrationTestFixture.cs:1-5`,
with namespace `TNC.Trading.Platform.Api.IntegrationTests.Authentication`. Its project
references the AppHost and Aspire test package at
`test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/TNC.Trading.Platform.Api.IntegrationTests.csproj:1-25`.

### Shared-Source Status

The API unit-test project does not currently link any shared test source. Its only
project reference is the API production project, and it contains no `<Compile Include>`
items: `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/TNC.Trading.Platform.Api.UnitTests.csproj:16-20`.

In contrast, the API integration project links
`test/Shared/Authentication/ManagedAppHostFixture.cs` and
`test/Shared/Authentication/KeycloakReadinessPolicy.cs` into its own assembly:
`test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/TNC.Trading.Platform.Api.IntegrationTests.csproj:27-33`.
The same fixture source is linked by the Web functional and E2E projects, rather than
being provided by a shared test-support assembly:
`test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/TNC.Trading.Platform.Web.FunctionalTests.csproj:23-30` and
`test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/TNC.Trading.Platform.Web.E2ETests.csproj:23-29`.

### Recommended Route

Keep the deterministic tests in the API unit-test project at its existing root, using
namespace `TNC.Trading.Platform.Api.UnitTests`. Extract the planned `internal` delegate
helper from `ManagedAppHostFixture.cs` into one dependency-free source file under
`test/Shared/Authentication/`, then add one linked `<Compile Include>` item for that
file to the API unit-test project. Continue compiling the same file into the API
integration project, where `ManagedAppHostFixture` calls it.

This is the minimal access route because linked source becomes part of each consuming
test assembly, so the API unit test can directly call the helper while it remains
`internal`. `InternalsVisibleTo` cannot grant access to an internal helper compiled into
the API integration assembly; the existing friend declaration exposes only API production
internals to `TNC.Trading.Platform.Api.UnitTests`.

Do not link the entire `ManagedAppHostFixture.cs` file into the unit-test project. The
fixture imports `Aspire.Hosting` and `Aspire.Hosting.Testing` and owns concrete
`IDistributedApplicationTestingBuilder` and `DistributedApplication` instances:
`test/Shared/Authentication/ManagedAppHostFixture.cs:1-14`. Linking it would require
the unit-test project to acquire the Aspire package and AppHost project reference already
present in the integration project, despite the delegate tests needing neither. A narrow
helper file preserves the unit/integration boundary and requires no shared test assembly,
new public API, Docker, Keycloak, SQL Server, or running AppHost.

## Status

Complete. The existing API unit-test project is a compatible destination, and the
source-link route identifies the smallest boundary-preserving way to retain an internal
helper without placing deterministic coverage in an Aspire integration project.

## References

* `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/TNC.Trading.Platform.Api.UnitTests.csproj:1-21`
* `src/TNC.Trading.Platform.Api/Properties/InternalsVisibleTo.cs:1-3`
* `.copilot-tracking/details/2026-09-15/managed-app-host-fixture-cleanup-details.md:11-24,81-119`
* `.github/instructions/tests.instructions.md:18-19,29-50`
* `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/TNC.Trading.Platform.Api.IntegrationTests.csproj:1-33`
* `test/Shared/Authentication/ManagedAppHostFixture.cs:1-14,66-79`
