---
title: Repository conventions and workflows research
description: Verified repository evidence about technologies, architecture, testing, documentation, and workflow patterns for future Copilot instruction design
author: GitHub Copilot
ms.date: 2026-06-28
ms.topic: reference
---

## Research scope

Investigate repository conventions and workflows using repository evidence only, with emphasis on structure, technologies, architectural patterns, testing practices, documentation patterns, developer workflows, and any existing Copilot or customization artifacts.

## Research questions

1. What technologies and solution structure dominate the repository?
2. What architectural and coding conventions are explicitly documented?
3. How is testing structured, and which tools and runtime assumptions shape test workflows?
4. How is documentation organized and maintained?
5. What existing Copilot or customization artifacts already exist, and what gaps are visible from the repository evidence?

## Verified findings

### Repository structure and dominant stack

The repository is a .NET 10 solution organized into `src/` and `test/` project groups plus a large `docs/` documentation set. The root README explicitly describes the repository as a .NET 10 trading platform and calls out Aspire AppHost, a Blazor operator UI, SQL Server, Keycloak, Mailpit, and requirement-driven automated testing: README.md:3, README.md:9, README.md:21.

The SDK is pinned through `global.json` with `version` `10.0.200` and `rollForward` set to `latestFeature`, which indicates the repository expects a specific .NET 10 feature band rather than loose SDK drift: global.json:3-4.

The solution file groups six production projects under `src/` and eight test projects under `test/`: TNC.Trading.Platform.slnx:1-21. The production projects are API, AppHost, Application, Infrastructure, ServiceDefaults, and Web: TNC.Trading.Platform.slnx:2-8. The test projects are separated by subsystem and test level, including API unit and integration tests, AppHost unit tests, Application unit tests, Infrastructure unit tests, and Web unit, functional, and end-to-end tests: TNC.Trading.Platform.slnx:10-21.

Project files confirm the dominant application stack:

* Aspire AppHost with Keycloak and SQL Server hosting support: src/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.csproj:1-18.
* Minimal API with JWT bearer auth, EF Core InMemory and SQL Server, and Scalar OpenAPI UI: src/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.csproj:1-20.
* Blazor Server web host with OpenID Connect and Radzen UI components: src/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.csproj:1-16.
* Shared service defaults with service discovery, HTTP resilience, and OpenTelemetry packages: src/TNC.Trading.Platform.ServiceDefaults/TNC.Trading.Platform.ServiceDefaults.csproj:1-18.

### Architectural patterns and code conventions

The architecture wiki states that the solution uses a small distributed-application layout where Aspire AppHost composes local services, a Minimal API hosts the control-plane backend, a Blazor Server app provides the operator UI, and API endpoints remain thin and delegate to application handlers: docs/wiki/architecture.md:7-13.

The same document assigns clear responsibilities across layers. The Application project owns configuration and runtime models, feature handlers, trading-schedule evaluation, runtime orchestration, and auth supervision: docs/wiki/architecture.md:109-129. The Infrastructure project owns EF Core persistence, Data Protection-backed credential storage, SQL-backed configuration storage, runtime-state storage, notification providers, retention processing, and the IG REST adapter: docs/wiki/architecture.md:159-170. The ServiceDefaults project owns shared observability and hosting defaults such as OpenTelemetry logging, metrics, tracing, service discovery, HTTP resilience, and health endpoints: docs/wiki/architecture.md:235-242.

The local-development guide explicitly says AppHost is the single local composition root, but its responsibilities are split into focused support files for infrastructure registration, project registration, and shared environment wiring, while `AppHost.cs` remains limited to builder creation, composition calls, and `Build().Run()`: docs/wiki/local-development.md:14-22.

The repository also documents a C# file-organization rule. The root README states that non-generated C# code keeps one top-level type per file with a matching file name: README.md:45. The root `.editorconfig` references that same rule in comments ahead of the C# conventions section: .editorconfig:15-17.

The `.editorconfig` also captures a strong default C# style baseline: four-space indentation, CRLF endings, file-scoped namespaces, explicit braces, `var` discouraged, namespace-folder matching, async methods ending with `Async`, and private field naming rules for `_camelCase` and `s_camelCase`: .editorconfig:1-220.

### Testing strategy, tooling, and workflow assumptions

The testing wiki states that the repository uses multiple test levels from unit level up to browser-driven flows: docs/wiki/testing-and-quality.md:7. It also documents the intended test pyramid by project, including unit, integration, functional, and end-to-end suites: docs/wiki/testing-and-quality.md:11-19.

The test project files confirm the tooling mix:

* xUnit plus `coverlet.collector` for unit tests: test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/TNC.Trading.Platform.Api.UnitTests.csproj:1-21.
* bUnit for Web unit/component tests: test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj:1-24.
* `Aspire.Hosting.Testing` for AppHost-backed integration and functional flows: test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/TNC.Trading.Platform.Api.IntegrationTests.csproj:1-23 and test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/TNC.Trading.Platform.Web.FunctionalTests.csproj:1-23.
* Playwright for browser-level coverage: test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/TNC.Trading.Platform.Web.FunctionalTests.csproj:8-12 and test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/TNC.Trading.Platform.Web.E2ETests.csproj:1-22.

The testing wiki emphasizes that the expensive real-runtime auth matrix is intentionally narrow: one browser sign-in smoke, one functional sign-out smoke, one functional insufficient-role smoke, and one functional CSRF negative: docs/wiki/testing-and-quality.md:122. It also states that API and Web distributed suites reuse one AppHost-plus-Keycloak process per xUnit collection to reduce startup cost while preserving topology validation: docs/wiki/testing-and-quality.md:32-33.

The local-development guide defines the default automated workflow as `dotnet build` and `dotnet test`, with `dotnet test -m:1` as the documented serialized fallback when MSBuild child-node exits are intermittent: docs/wiki/local-development.md:32, docs/wiki/local-development.md:132-138.

The local-development guide also makes the supported runtime assumptions explicit. Docker Desktop is required, AppHost starts SQL Server, `platformdb`, Mailpit, and Keycloak, and there is no supported synthetic AppHost runtime path for local startup: docs/wiki/local-development.md:7-23.

The shared AppHost test helper shows how distributed tests discover live listeners instead of relying on fixed ports. It probes `/health/ready`, expects `/api/platform/status` to fail closed with `401`, and follows the Web sign-in redirect chain until it reaches the Keycloak OpenID Connect auth endpoint: test/Shared/Authentication/AppHostProcessHandle.cs:33-63 and test/Shared/Authentication/AppHostProcessHandle.cs:78-114 and test/Shared/Authentication/AppHostProcessHandle.cs:202-272.

The AppHost unit tests further prove that topology and auth-provider parity are treated as lower-cost validation targets. One composition test asserts that the resource graph contains `sql`, `platformdb`, `mailpit`, `keycloak`, `api`, and `web`, and that API and Web waits preserve the documented dependency graph: test/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.UnitTests/AppHostCompositionTests.cs:16-89. The environment-wiring tests assert that the delivered runtime stays Keycloak-backed by default, while the synthetic `Test` provider is isolated to the API branch only: test/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.UnitTests/AppHostEnvironmentWiringTests.cs:16-138 and test/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.UnitTests/AppHostEnvironmentWiringTests.cs:264-333.

Quality evidence is also part of the documented workflow. The testing wiki records both Coverlet and Stryker evidence sections rather than only describing test types: docs/wiki/testing-and-quality.md:145-160.

### Documentation patterns and maintenance style

The repository maintains an unusually strong documentation spine. The root README points readers to a documentation index, a local-development guide, and multiple work-package requirement, specification, and delivery-plan documents: README.md:49-111.

`docs/README.md` describes the `docs/` directory as containing project-level analysis, work-package documents, and the implementation wiki, and it prescribes a reading order of business requirements, systems analysis, then the wiki: docs/README.md:1-19.

`docs/wiki/README.md` describes the wiki as implementation-focused documentation for the application as it exists today and gives ordered reading paths for developers, operators/reviewers, and maintainers planning future work: docs/wiki/README.md:1-49.

The visible documentation pattern is requirement-driven and work-package-oriented. The root README enumerates work packages `001` through `006`, each with requirements, technical specification, and delivery plan artifacts: README.md:76-106. The wiki also preserves links back to broader project and planning documents instead of treating the wiki as the only source of truth: docs/wiki/README.md:33-42.

`.copilot-tracking/` is already in active use for plans, reviews, details, and research artifacts. Existing files include research, plan, review, and change records under dated folders, which suggests an established AI-assisted planning and evidence workflow rather than ad hoc one-off notes. Relevant examples include `.copilot-tracking/research/2026-06-28/copilot-instructions-recommendations-research.md`, `.copilot-tracking/plans/2026-06-26/refactoring-and-testability-opportunities-plan.instructions.md`, and multiple `.copilot-tracking/research/subagents/2026-06-28/*.md` artifacts.

### Existing Copilot and customization artifacts

There is no repository-local `.github` Copilot customization surface visible from the workspace file search. A file search over `.github/**/*` returned no files, and directory listing showed only `.github/workflows/`, with that folder currently empty. This means the repository evidence shows workflow intent in documentation and `.copilot-tracking/`, but not yet in committed repository-local Copilot instruction files.

The root `.editorconfig` comment explicitly references Copilot instruction files as the documented home for the one-top-level-type rule, which is a strong signal that repository maintainers expect some coding conventions to be captured in Copilot-specific guidance rather than only in code-style settings: .editorconfig:15-17.

The existing `.copilot-tracking/research/2026-06-28/copilot-instructions-recommendations-research.md` file is a planning artifact for instruction recommendations rather than an instruction file itself. It frames the work as reviewing repository structure, codebase patterns, and existing documentation to identify suitable instruction files: .copilot-tracking/research/2026-06-28/copilot-instructions-recommendations-research.md:1-38.

## Likely instruction categories implied by the evidence

These are categories implied by repository evidence only. They are not final recommendations.

* Repository-level .NET and solution-structure guidance: implied by the explicit .NET 10 pin, six-project layered solution, and one-top-level-type rule. Evidence: global.json:3-4, TNC.Trading.Platform.slnx:1-21, README.md:45, .editorconfig:15-17.
* Architecture and layering guidance for `src/`: implied by the documented AppHost, API, Application, Infrastructure, ServiceDefaults, and Web boundaries. Evidence: docs/wiki/architecture.md:7-13 and docs/wiki/architecture.md:109-170 and docs/wiki/architecture.md:235-242.
* AppHost and local-runtime workflow guidance: implied by the Docker plus Keycloak local runtime, AppHost composition-root rule, and guarded synthetic-auth override. Evidence: docs/wiki/local-development.md:7-23, test/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.UnitTests/AppHostCompositionTests.cs:16-89, test/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.UnitTests/AppHostEnvironmentWiringTests.cs:67-138.
* Testing guidance by test level: implied by the explicit unit, integration, functional, and E2E split plus the narrow retained real-runtime auth matrix. Evidence: docs/wiki/testing-and-quality.md:7-19 and docs/wiki/testing-and-quality.md:122, plus the test project files listed above.
* Web UI and component-testing guidance: implied by the Blazor Server plus Radzen stack and the presence of bUnit and Playwright test layers. Evidence: src/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.csproj:1-16, test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/TNC.Trading.Platform.Web.UnitTests.csproj:1-24, test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/TNC.Trading.Platform.Web.E2ETests.csproj:1-22.
* Documentation and planning-artifact guidance: implied by the structured docs hierarchy, work-package numbering, and active `.copilot-tracking/` usage. Evidence: docs/README.md:1-19, docs/wiki/README.md:1-49, README.md:76-106.

## Notable gaps and limits in the evidence

* The repository does not currently expose committed `.github` Copilot instruction, prompt, or agent files, so there is no in-repo customization baseline to extend or compare against.
* The `.github/workflows/` folder appears empty from the current workspace view, so CI workflow specifics are not available as repository evidence in this session.
* The evidence strongly documents build, run, manual validation, and testing workflows, but there is less direct evidence in the inspected files about release, deployment, or production-operations workflows.
* Existing `.copilot-tracking/` artifacts show active AI-assisted planning and research, but they do not by themselves define final repository conventions; they are evidence of process, not authoritative policy.

## Research status

Complete for the requested scope. The repository now has a verified evidence summary that can support a later instruction-file recommendation pass without relying on guesses.