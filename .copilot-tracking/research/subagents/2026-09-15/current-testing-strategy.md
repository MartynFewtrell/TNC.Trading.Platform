---
title: Current Testing Strategy Research
description: Research record for the repository's testing tiers, conventions, validation commands, and guidance gaps.
ms.date: 2026-09-15
ms.topic: concept
---

## Research Scope

Investigate the repository's current testing strategy and recently updated
test-related instructions and documentation. Identify intended testing tiers,
commands, conventions, conflicting or incomplete guidance, and supporting
workspace evidence. Do not modify product or test source.

## Status

Complete. This review was research-only. No product or test source was
changed, and no test command was run for this investigation.

## Intended Testing Strategy

The repository defines a unit-first testing pyramid with four separately
deployable suite types:

* Unit tests provide fast, in-process coverage for policy, use cases, mapping,
	validation, and component rendering where infrastructure is unnecessary.
* Integration tests exercise one service boundary with real dependencies.
* Functional tests prove documented acceptance criteria, organized by stable
	feature or surface name and traceable to requirement identifiers.
* Aspire-hosted end-to-end tests validate cross-service behavior through closed
	external boundaries.

The UI boundary is explicit: plain xUnit for in-process behavior, bUnit for
Blazor rendering and lifecycle, Aspire for topology and infrastructure, and
Playwright for browser behavior. The preferred interpretation is therefore to
put the largest share of confidence in unit and bUnit tests, retain focused
service integration tests, and keep real AppHost-plus-Keycloak browser coverage
to a small smoke matrix.

The source policy is consistent with that interpretation:

* .github/instructions/tests.instructions.md:28-64 defines xUnit, suite
	separation, unit/integration/E2E/functional boundaries, determinism, fixture
	ownership, and permitted xUnit collection use.
* .github/instructions/tests.instructions.md:66-102 assigns the four UI test
	mechanisms and requires feature-organized functional tests with requirement
	traceability.
* .github/instructions/architecture.instructions.md:76-108 requires
	independently testing policy/use cases and directly testing handlers and
	validators rather than relying on controller tests.
* .github/instructions/csharp.instructions.md:53-55 requires test updates for
	behavior changes and forbids Arrange/Act/Assert narration.
* .github/copilot-instructions.md:40-41 repeats the required test-method form
	and complete requirement-oriented test documentation.

## Current Suite Topology

All intended tiers exist in separate projects. The current inventory contains
Application, Infrastructure, AppHost, API, and Web unit projects; Infrastructure
and API integration projects; Web functional tests; Web E2E tests; and a
separate architecture-integration project. The current ownership map is listed
in docs/wiki/testing-and-quality.md:74-85.

The documented high-cost scope is intentionally narrow:

* API integration retains real Keycloak token coverage plus an isolated
	synthetic-token slice for selectively invalid JWT and claim scenarios.
* Web functional tests cover requirement-driven redirects, sign-out, CSRF, and
	rendered outcomes using a shared real AppHost-plus-Keycloak collection.
* Web E2E retains a browser sign-in smoke through the fixture-provided `web`
	endpoint.

Evidence: docs/wiki/testing-and-quality.md:82-101,
docs/wiki/testing-and-quality.md:309-323, and
docs/wiki/testing-and-quality.md:334-347.

The implementation supports the new Aspire model. The shared fixture creates
`DistributedApplicationTestingBuilder`, waits for named `keycloak`, `api`, and
`web` resources, uses named endpoints/HTTP clients, and asynchronously disposes
the application and builder. Evidence:
test/Shared/Authentication/ManagedAppHostFixture.cs:35-82 and
test/Shared/Authentication/ManagedAppHostFixture.cs:87-97. This aligns with
.github/instructions/aspire-tests.instructions.md:24-42 and 56.

The E2E project references `Aspire.Hosting.Testing` and
`Microsoft.Playwright.Xunit`; its browser test inherits `PageTest` and uses
web-first `Expect` assertions. Evidence:
test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/TNC.Trading.Platform.Web.E2ETests.csproj:10-24 and
test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/Authentication/PlatformDashboardAuthenticationE2ETests.cs:3-48.

The SQL integration fixture also now creates its own Testcontainers SQL Server,
creates a unique database, resets it, and disposes both database and container.
Evidence:
test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/SqlServerDatabaseFixture.cs:12-41,
test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/SqlServerDatabaseFixture.cs:57-105.
Its collection disables parallelization for the explicitly shared SQL boundary
at line 106, which is consistent with the collection-fixture exception in
.github/instructions/tests.instructions.md:64.

## Validation Commands And CI

The baseline local command is `dotnet test`; local documentation permits
`dotnet test -m:1` only as a workaround for intermittent MSBuild child-node
exits. Evidence: docs/wiki/local-development.md:217-226. The core test,
Aspire-test, C#, and architecture instructions all list `dotnet test`, while
the latter two also list `dotnet build`. Evidence:
.github/instructions/tests.instructions.md:107,
.github/instructions/aspire-tests.instructions.md:63-66,
.github/instructions/csharp.instructions.md:77-78, and
.github/instructions/architecture.instructions.md:162-164.

Focused distributed validation commands target API authentication integration,
Web functional authentication, and Web E2E authentication. Evidence:
docs/wiki/testing-and-quality.md:316-318. The Phase 1 architecture/API checks
are separately documented at docs/wiki/testing-and-quality.md:51-52.

CI enforces ordered stages: restore/build, fast project suites, Aspire/service
integration, browser/real-Keycloak, then an ordinary solution-wide test summary.
It writes TRX results per project/stage and uploads failure diagnostics.
Evidence: .github/workflows/ci.yml:35-69, .github/workflows/ci.yml:70-109,
.github/workflows/ci.yml:111-147, and .github/workflows/ci.yml:149-168.

## Recent Guidance Changes

The current working tree contains uncommitted changes to the test and Aspire
instructions, both testing wiki pages, and the shared test infrastructure.
Those changes remove process/listener discovery and fixed-port lease patterns in
favor of fixture-owned Aspire builders, named resources, randomized endpoints,
bounded readiness, and asynchronous cleanup. The replacement policy is present
in .github/instructions/tests.instructions.md:62-64,
.github/instructions/aspire-tests.instructions.md:24-42 and 56, and
docs/wiki/local-development.md:228-241.

The last committed changes touching the reviewed policy/wiki paths are
6d38c49 (2026-08-31), f4b3b3b (2026-08-30), 8863cd9 (2026-08-07), and 8df68d8
(2026-07-28). The newer endpoint-ownership policy is therefore working-tree
guidance at the time of this review, not yet committed history.

## Risks And Guidance Gaps

* Documentation drift remains in docs/wiki/testing-and-quality.md:170-171,
	which says the SQL fixture discovers a Docker container and credential through
	Docker metadata. The current fixture owns a Testcontainers instance instead
	(test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/SqlServerDatabaseFixture.cs:12-41).
	The stale narrative conflicts with the fixture-ownership rule at
	.github/instructions/tests.instructions.md:62-63.
* The same wiki contains contradictory legacy wording later in the file:
	docs/wiki/testing-and-quality.md:517 and 523 still describe runtime-listener
	discovery. This conflicts with its current named-resource guidance at lines
	95-99 and 309-323, local guidance at docs/wiki/local-development.md:241 and
	266, and the shared fixture implementation at
	test/Shared/Authentication/ManagedAppHostFixture.cs:46-51.
* The functional project references raw `Microsoft.Playwright`, rather than
	`Microsoft.Playwright.Xunit`, and its session helper manually creates
	Playwright/browser/page instances and calls `WaitForAsync`. Evidence:
	test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/TNC.Trading.Platform.Web.FunctionalTests.csproj:10-25 and
	test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/Authentication/RealAuthenticationSessionFactory.cs:2-51.
	This is a narrow bootstrap helper for HTTP-functional assertions, but it does
	not match the preferred Playwright conventions in
	.github/instructions/playwright.instructions.md:25-34. The policy should
	either add an explicit exception for cookie-bridging functional fixtures or
	migrate that browser interaction to `PageTest` and web-first assertions.
* The Playwright instruction requires `Test.StepAsync()` at
	.github/instructions/playwright.instructions.md:28. Current E2E examples use
	`PageTest` and `Expect`, but neither reviewed E2E class uses steps. Evidence:
	test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/Authentication/PlatformDashboardAuthenticationE2ETests.cs:7-48 and
	test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/AccountPreferences/AccountPreferencesE2ETests.cs:7-39.
	This is a convention-compliance gap, not a functional failure.
* The instructions call Aspire-hosted tests "closed-box integration" at
	.github/instructions/aspire-tests.instructions.md:2, 10, and 28, while the
	test taxonomy reserves a distinct E2E tier for cross-service Aspire tests at
	.github/instructions/tests.instructions.md:52-56. Current practice uses
	AppHost-backed API integration, Web functional, and Web E2E projects. That
	works, but the policy does not state the classification rule for a
	requirement-driven HTTP functional suite that starts the real AppHost.
* The wiki states that fixture-owned diagnostics belong under
	`TEST_ARTIFACTS_DIRECTORY` at docs/wiki/testing-and-quality.md:327, while CI
	defines and uploads that location at .github/workflows/ci.yml:19 and 96-108.
	This review found the environment-variable declaration and uploads, but did
	not establish a repository-wide fixture contract or verify every fixture
	writes sanitized diagnostics there.

## Preferred Interpretation

Treat `dotnet test TNC.Trading.Platform.slnx` as the ordinary full gate and use
the CI stage commands or focused project commands for diagnostic iteration. Do
not use `-m:1` to conceal resource contention; use it only for the documented
MSBuild child-node issue. For distributed suites, every externally mutable
dependency must be created/provisioned and disposed by its fixture, readiness
must use bounded named-resource checks, and clients/browsers must receive named
Aspire endpoint URIs.

Classify by assertion surface first, then by hosting topology: bUnit for
component rendering, Playwright E2E for browser-only behavior, and real AppHost
functional/integration tests only where lower tiers cannot prove the documented
requirement. Preserve the current small real-auth smoke matrix and move
duplicated route/policy assertions downward where feasible.

## Proposed Next Research

* Audit every remaining `runtime listener`, `process output`, Docker metadata,
	fixed-port, and lease reference under docs/ and test/ to produce a scoped
	documentation and cleanup list.
* Review all functional Playwright usage to decide whether a sanctioned
	cookie-bridging fixture exception is sufficient or a `PageTest` migration is
	warranted.
* Trace `TEST_ARTIFACTS_DIRECTORY` reads/writes and assess whether fixture
	diagnostics are consistently sanitized and CI-accessible.
* Sample test-method comments and names across each project to measure
	compliance with requirement traceability and the required naming convention.

## Clarifying Questions

None. The remaining decisions are policy choices that can be made from the
evidence above.