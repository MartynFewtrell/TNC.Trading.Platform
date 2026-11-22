---
title: Aspire and Microsoft Testing Strategy Research
description: Official-source research on testing .NET Aspire distributed applications.
author: GitHub Copilot
ms.date: 2026-09-15
ms.topic: research
keywords:
  - .NET Aspire
  - distributed application testing
  - integration testing
  - end-to-end testing
---

## Research Scope

Research current official guidance from Aspire documentation and Microsoft Learn for
unit and component tests, hosted distributed integration tests, end-to-end tests,
readiness, isolation, configuration, databases, external fakes, authentication,
observability, and CI. No product or test code was changed.

## Status

Complete on 2026-09-15. Assertions marked **Verified** are directly supported by an
official source listed below. Assertions marked **Recommendation** are a practical
testing strategy inferred from that guidance for this repository.

## Executive Strategy

**Recommendation:** Use three deliberately different test layers. Keep the largest
share as fast isolated tests, exercise each service's HTTP and dependency-injection
composition in process, and reserve Aspire-hosted tests for a small number of
cross-process workflows with real infrastructure.

| Layer | Primary purpose | Preferred mechanism | Real dependencies |
|-------|-----------------|---------------------|-------------------|
| Unit and component | Business rules, validation, mappings, failure policy | Test project and fakes | No |
| Service integration | HTTP pipeline, service registration, authorization branches | `WebApplicationFactory<TEntryPoint>` | Replaceable |
| Distributed integration | AppHost wiring, service discovery, container resources, migrations, real authentication journeys | `Aspire.Hosting.Testing` | Yes, where material |
| Browser end-to-end | User-visible browser workflows, redirects, cookies, JavaScript, accessibility-sensitive paths | Playwright against an Aspire-hosted application | Yes, selectively |

**Verified:** Aspire testing is closed-box integration testing. It launches the
AppHost and dependent resources as separate processes, so tests cannot directly
replace a service in an application's dependency-injection container. The official
guidance explicitly directs isolated, in-memory, or mocked-component scenarios to
`WebApplicationFactory<T>` instead.

## Verified Findings

### Unit and Component Tests

**Verified:** Good unit tests are fast, isolated, repeatable, self-checking, and
timely. Microsoft recommends avoiding infrastructure dependencies in unit tests,
using explicit dependencies and fakes instead, separating unit and integration test
projects, and favoring an Arrange-Act-Assert structure.

**Recommendation:** Put Application-domain behaviors, configuration validation,
schedule calculation, retry classification, and adapter error translation in this
layer. Drive time, random values, environment access, and outbound transport through
explicit dependencies so they can be controlled without Docker or Aspire.

**Recommendation:** Add component-level Blazor or endpoint tests only where they
provide cheaper coverage than a browser or AppHost test. Treat them as in-process
tests; they do not prove deployed resource wiring.

### Service Integration and External Fakes

**Verified:** `Microsoft.AspNetCore.Mvc.Testing` supplies
`WebApplicationFactory<TEntryPoint>` to bootstrap an ASP.NET Core test server.
`ConfigureTestServices` and `WithWebHostBuilder` support replacing registered
services for a test. Microsoft demonstrates an in-memory configuration provider and
an authentication handler that returns a controlled claims principal.

**Verified:** Microsoft says to prefer a focused set of significant infrastructure
scenarios and choose a unit test when either unit or integration coverage would test
the behavior adequately. For a single in-process ASP.NET Core service, a
`WebApplicationFactory` test can use a test database, alternate settings, and faked
outbound interfaces without running the whole distributed topology.

**Recommendation:** Introduce fakes at an owned adapter boundary, for example an
`IIgClient`, outbound `HttpMessageHandler`, clock, notifier, or message publisher.
Use those fakes in `WebApplicationFactory` tests. Do not try to substitute these
from an Aspire-hosted test because separate processes make that unsupported.

### Aspire-Hosted Distributed Integration

**Verified:** `Aspire.Hosting.Testing` provides
`DistributedApplicationTestingBuilder`. The official template creates the builder
with `CreateAsync<TAppHost>()`, configures its services, builds and starts the
application, creates an `HttpClient` for a named resource, waits for that resource
to become healthy, and asserts through its public endpoint.

**Verified:** The testing builder starts the AppHost in a background thread and
manages the lifecycle. Disposing the application or builder cleans up resources. By
default it disables the dashboard and randomizes proxied resource ports, allowing
multiple application instances to run concurrently.

**Verified:** Resource logs are redirected to `DistributedApplication` by default.
The testing builder's service collection can configure logging and HTTP resilience
for the test host. App settings from the application are not automatically copied to
the test project, so test logging filters must be configured explicitly when needed.

**Recommendation:** Create one small shared fixture per distributed test class or
collection only when the tests can safely share all mutable resources. Otherwise
create separate app instances and separately named data stores. Always dispose with
`await using`.

### Resource Readiness

**Verified:** The official Aspire test pattern calls
`app.ResourceNotifications.WaitForResourceHealthyAsync("resource-name", token)`
before issuing the request. Its example also places bounded `WaitAsync` calls around
build and start operations.

**Recommendation:** Treat health as a prerequisite, not an assertion based on a
fixed delay. Give every hosted test an explicit cancellation token and a bounded
timeout. Wait for the externally exercised resource after `StartAsync`, then use
request-level assertions to prove the relevant downstream dependency path.

**Recommendation:** Use the AppHost's `WaitFor` relationship to express runtime
startup ordering, but retain test-side health waits for the resource that the test
will call. Startup order alone does not prove a user-relevant request can succeed.

### Isolation and Parallelism

**Verified:** Aspire randomizes ports by default to support concurrent application
instances. Passing `DcpPublisher:RandomizePorts=false` disables that behavior.

**Verified:** xUnit runs test classes in parallel by default but runs test methods
within one class sequentially. Microsoft recommends isolating real database writes
with a transaction where product code does not manage transactions; tests that must
commit explicit transactions require cleanup and no parallel access to the same
database. Separate databases permit parallel write tests.

**Recommendation:** Leave random ports enabled in normal local and CI runs. Never
share a writable database, Keycloak realm/client/user namespace, or configured
external endpoint between parallel hosted tests unless every test has a proven reset
mechanism. Put unavoidable shared-resource tests in an explicitly non-parallel test
collection rather than disabling all test parallelism.

### Configuration Overrides and Secrets

**Verified:** Aspire tests may influence application behavior through environment
variables or configuration settings, but cannot inspect application internal state.
The testing builder accepts AppHost command-line configuration arguments; the
official example sets `DcpPublisher:RandomizePorts=false`. `WebApplicationFactory`
can add an in-memory configuration provider or override the environment.

**Verified:** EF Core guidance says not to store production connection strings in
source control or embed them in migration bundles. Supply them from the deployment
system's secret store and set the environment explicitly when building or running a
migration artifact.

**Recommendation:** Keep deterministic non-secret test defaults in test code or a
test-only configuration source. Inject CI secrets through the CI secret store as
environment variables, use dedicated least-privilege test credentials, and log only
the configuration keys selected, never secret values. Fail early with a clear skip
or validation error when an opt-in live credential is absent.

### Database Tests and Migrations

**Verified:** EF Core documents that real database systems are suitable for testing
and that Docker images or Testcontainers can simplify setup in CI. It recommends
thinking explicitly about test isolation: read-only tests can share a database,
writes can roll back a transaction, and tests that commit their own transactions
must reset state and prevent concurrent interference. Multiple databases restore
parallelism for writing tests.

**Verified:** The EF Core migrations guidance says to inspect and test migrations
before production application. For local development and testing,
`dotnet ef database update` is appropriate. Aspire applications should use the
Aspire EF Core migrations integration for local coordination and publishing
migration bundles or scripts. EF Core 9 and later use migration locking when EF
applies migrations, but raw SQL scripts are outside that lock.

**Recommendation:** For Aspire-hosted integration tests, run the real database
container and the same migration path used locally. Seed only data required by the
scenario, use a unique database name or reset strategy per parallel writer, and add
a CI check that detects pending model changes. Do not use EF InMemory to validate
SQL Server behavior; use the real provider for persistence-critical tests.

### Authentication and Keycloak

**Verified:** For in-process ASP.NET Core integration tests, Microsoft demonstrates
replacing authentication with a test `AuthenticationHandler` and making the
default authenticate and challenge schemes match it. This is suitable for fast
authorization policy and claims-mapping coverage. Disabling automatic redirects
allows tests to verify the initial challenge response and `Location` header.

**Verified:** Aspire-hosted tests cannot replace an application's authentication
registration through the test process's dependency-injection container because the
application runs separately.

**Recommendation:** Use two auth test tracks. First, cover authorization rules,
claims interpretation, and challenge behavior with a fake handler in service tests.
Second, add a narrow serial suite that starts the repository's Aspire-orchestrated
Keycloak resource and proves the real OIDC discovery, authorization-code redirect,
token validation, role mapping, and sign-out boundary. Create a dedicated realm,
client, redirect URI, users, and role names for the test suite; reset them from a
versioned realm import or Keycloak Admin API before each isolated run.

**Gap:** The searched official Aspire testing pages describe closed-box testing but
do not prescribe a Keycloak-specific `Aspire.Hosting.Testing` fixture, realm reset
API, or browser login flow. The Keycloak lifecycle, realm import contract, test
accounts, and any UI selector strategy must be confirmed against this repository's
AppHost and Keycloak configuration before implementation.

### End-to-End and Browser Coverage

**Verified:** Microsoft recommends a browser automation tool such as Playwright for
.NET when testing SPAs. Aspire-hosted tests are already end-to-end at the service
boundary when they call a public resource endpoint and traverse its real
dependencies, but they do not exercise browser rendering or JavaScript.

**Recommendation:** Keep browser tests small and user-workflow oriented: an
anonymous challenge, successful sign-in, one protected trading/configuration task,
validation feedback, and sign-out. Start the AppHost with
`DistributedApplicationTestingBuilder`, wait for the web resource to be healthy,
then direct Playwright to the resolved public endpoint. Capture traces, screenshots,
and browser video only on failure to manage CI artifact volume.

### Observability and Diagnostics

**Verified:** Aspire documents configuring test-host logging through
`builder.Services.AddLogging` and setting filters for application, ASP.NET Core,
and Aspire hosting categories. Resource logs are redirected to the distributed
application. The official sample adds a standard resilience handler to test HTTP
clients.

**Recommendation:** Use per-test correlation data in request headers and test data
keys. On failure, retain structured AppHost/resource logs, distributed traces, the
Aspire dashboard export when enabled, database diagnostic output, and browser
artifacts. Keep the dashboard disabled by default; enable it only for diagnosis or
an opt-in developer test profile.

### CI Execution

**Verified:** The Aspire testing builder cleans up resources on disposal and uses
random ports by default, both of which support concurrent test execution. EF Core
documents generating migration bundles in CI and running migrations as a one-shot
job only after the database is healthy. Microsoft documents `dotnet test` as the
test entry point and notes that framework-specific runner arguments must be routed
only to compatible test projects in mixed runner solutions.

**Recommendation:** Split CI into stages: restore/build; fast unit and service
tests; Aspire-hosted integration tests on a Docker-capable runner; then browser and
real-Keycloak tests. Publish TRX/JUnit results and failure diagnostics as artifacts.
Cache NuGet packages and pre-pull pinned container images, but never cache mutable
database or Keycloak test data between runs. Make live external-provider tests
opt-in and protected from pull-request execution.

## Practical Code Patterns

### Bounded Aspire-Hosted HTTP Test

**Verified pattern:** This is a condensed adaptation of Aspire's documented flow;
resource names and project marker types are placeholders.

```csharp
private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

[Fact]
public async Task GetWebResourceRoot_ReturnsOk_WhenDependenciesAreHealthy()
{
    using var timeout = new CancellationTokenSource(DefaultTimeout);
    var builder = await DistributedApplicationTestingBuilder
        .CreateAsync<Projects.TNC_Trading_Platform_AppHost>(timeout.Token);

    await using var app = await builder.BuildAsync(timeout.Token);
    await app.StartAsync(timeout.Token);

    await app.ResourceNotifications.WaitForResourceHealthyAsync(
        "web", timeout.Token);

    using var client = app.CreateHttpClient("web");
    using var response = await client.GetAsync("/", timeout.Token);

    response.EnsureSuccessStatusCode();
}
```

### Publish-Mode Environment Contract Test

**Verified pattern:** Aspire's documentation builds resource environment values with
`DistributedApplicationOperation.Publish`, which permits assertions on unresolved
binding expressions rather than requiring running endpoint resolution.

```csharp
var builder = await DistributedApplicationTestingBuilder
    .CreateAsync<Projects.TNC_Trading_Platform_AppHost>();
var resource = builder.CreateResourceBuilder<ProjectResource>("web");

var configuration = await ExecutionConfigurationBuilder.Create(resource.Resource)
    .WithEnvironmentVariablesConfig()
    .BuildAsync(
        new DistributedApplicationOperation.Publish(),
        NullLogger.Instance,
        CancellationToken.None);

var variables = configuration.EnvironmentVariables.ToDictionary();
Assert.Contains("API_HTTPS", variables.Keys);
```

**Recommendation:** Keep these environment-contract tests separate from hosted
tests. They are cheap checks of AppHost wiring and should use publish mode; do not
assert a hard-coded local port because Aspire intentionally randomizes ports.

## Implementation Priorities for This Repository

1. Preserve and expand isolated Application and Infrastructure tests for rules and
   edge cases; do not move those cases into Aspire-hosted tests.
2. Use `WebApplicationFactory` service tests for API/UI authorization paths,
   configuration validation, and faked IG boundary failure handling.
3. Add a small AppHost-referencing test project using `Aspire.Hosting.Testing` for
   public workflows that prove Web to API to SQL Server wiring and health.
4. Make database state unique or resettable before enabling hosted-test parallelism.
5. Add a serial real-Keycloak browser/auth contract suite after documenting its
   realm bootstrap and cleanup mechanism.
6. Publish test logs, traces, and browser artifacts on failure, with secrets
   redacted at source.

## Official Sources

* [Aspire testing overview](https://aspire.dev/testing/overview/)
* [Aspire: write your first test](https://aspire.dev/testing/write-your-first-test/)
* [Aspire EF Core migrations integration](https://aspire.dev/integrations/databases/efcore/migrations/)
* [ASP.NET Core integration tests](https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests?view=aspnetcore-10.0)
* [Unit testing best practices](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-best-practices)
* [Order unit tests and parallelism](https://learn.microsoft.com/en-us/dotnet/core/testing/order-unit-tests)
* [Testing with dotnet test](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-with-dotnet-test)
* [EF Core: testing against the production database system](https://learn.microsoft.com/en-us/ef/core/testing/testing-with-the-database)
* [EF Core: applying migrations](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying)

## Gaps and Clarifying Questions

* Confirm whether current AppHost tests already use `Aspire.Hosting.Testing` and
  whether their project marker type can be referenced by a new distributed suite.
* Confirm the Keycloak AppHost resource name, realm import source, test client,
  redirect URIs, and safe account-reset mechanism before automating real login.
* Confirm whether database migrations execute through Aspire's EF Core migration
  integration or application startup, because this determines the appropriate
  readiness and migration assertions.
* Confirm the CI runner's Docker availability, CPU/memory capacity, and artifact
  retention policy before selecting hosted-test parallelism and browser diagnostics.

## Research Limitations

The current official Aspire testing pages were available at `aspire.dev` and the
corresponding older Microsoft Learn Aspire testing URLs redirect there. Several
legacy or guessed Aspire paths for orchestration, Keycloak, and CI returned 404, so
this record does not claim unverified product-specific features from those paths.
The Keycloak and CI recommendations are therefore explicitly marked as repository
recommendations, backed by the verified process-isolation and general testing
guidance above.
