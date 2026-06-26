<!-- markdownlint-disable-file -->
# Task Research: Repository Overview

Establish a first-use orientation for the TNC.Trading.Platform repository so a new HVE user can understand its purpose, structure, major projects, testing shape, and likely implementation boundaries.

## Task Implementation Requests

* Explain what this repository is and how it is organized.
* Identify the main projects, supporting documentation, and testing layout.
* Highlight notable conventions, technologies, and likely development workflow entry points.

## Scope and Success Criteria

* Scope: Repository-level orientation based on source layout, solution structure, top-level docs, and nearby architecture documents. Excludes deep implementation details of individual features unless required to explain structure.
* Assumptions:
  * The repository root is the workspace root.
  * Existing documentation and file structure are representative of the intended architecture.
  * A high-level overview is more useful than exhaustive inventory for first-use orientation.
* Success Criteria:
  * Describe the repository purpose and major bounded areas.
  * Summarize how runtime projects and test projects relate.
  * Identify useful starting points for future HVE work.

## Outline

1. Gather top-level repository and documentation evidence.
2. Inspect solution, source, and test layout.
3. Identify conventions, architecture cues, and recommended entry points.
4. Consolidate into a first-use overview.

## Potential Next Research

* Confirm the local developer workflow by reading the local development guide and, if needed, validating the default AppHost startup path.
  * Reasoning: The repository overview already identifies the runtime topology; the next likely question is how contributors build and run it locally.
  * Reference: README.md:47-62, docs/wiki/local-development.md.

* Map feature folders to work-package documents when a future task is requirements-driven rather than code-first.
  * Reasoning: The repository is organized around work packages 001 to 006 with matching docs that likely explain why specific slices exist.
  * Reference: README.md:64-110.

## Research Executed

### File Analysis

* README.md
  * States the repository purpose, implemented scope, non-implemented scope, solution structure, and documentation entry points. Evidence: README.md:1-110.
* TNC.Trading.Platform.slnx
  * Lists six runtime projects and eight test projects, confirming the main solution inventory. Evidence: TNC.Trading.Platform.slnx:3-27.
* global.json
  * Pins the SDK family to .NET 10.0.200 with `latestFeature` roll-forward. Evidence: global.json:2-4.
* docs/README.md
  * Defines the documentation hierarchy: business requirements, systems analysis, and the implementation wiki. Evidence: docs/README.md:1-17.
* docs/wiki/architecture.md
  * Describes the implemented architecture, runtime topology, request flow, project boundaries, and persistence responsibilities. Evidence: docs/wiki/architecture.md:3-223.
* .copilot-tracking/research/subagents/2026-06-26/solution-and-projects-research.md
  * Provides deeper evidence for project responsibilities, runtime seams, and strong debugging entry points.

### Code Search Results

* `Current status|Solution structure|Getting started|Project documentation`
  * Matches in README.md confirm that the root README is the best first-stop orientation document. Evidence: README.md:5, README.md:30, README.md:47, README.md:64.
* `Architectural style|Solution structure|Runtime topology|Application-layer responsibilities|Infrastructure responsibilities|AppHost composition responsibilities|Persistence model`
  * Matches in docs/wiki/architecture.md confirm the wiki architecture guide is the best second-stop document for implementation details. Evidence: docs/wiki/architecture.md:5, docs/wiki/architecture.md:16, docs/wiki/architecture.md:44, docs/wiki/architecture.md:129, docs/wiki/architecture.md:157, docs/wiki/architecture.md:186, docs/wiki/architecture.md:196.
* `sdk|version|rollForward`
  * Matches in global.json confirm the repository targets .NET 10 SDK 10.0.200. Evidence: global.json:2-4.
* Solution project path searches
  * Matches in TNC.Trading.Platform.slnx confirm the runtime and test project layout. Evidence: TNC.Trading.Platform.slnx:3-8, TNC.Trading.Platform.slnx:12-26.

### External Research

* None required for repository overview.

### Project Conventions

* Standards referenced: Research-only Task Researcher workflow, repository README, implementation wiki architecture guidance.
* Instructions followed: Task Researcher mode

## Key Discoveries

### Project Structure

The repository is a .NET 10 trading platform under active development for algorithmic day-trading capabilities against IG APIs, but the currently delivered scope is narrower than that end goal. The implemented solution focuses on authentication, authorization, operator visibility, IG test-login supervision, configuration management, and operational history rather than live trading, market discovery, or order execution. Evidence: README.md:1-28.

At the solution level, the codebase is split into six runtime projects under `src/` and eight test projects under `test/`. The runtime projects are Api, AppHost, Application, Infrastructure, ServiceDefaults, and Web. The test suite is layered by slice, covering API unit and integration tests, AppHost unit tests, Application unit tests, Infrastructure unit tests, and Web unit, functional, and end-to-end tests. Evidence: TNC.Trading.Platform.slnx:3-27.

The repository also contains a substantial documentation set under `docs/`, including business requirements, systems analysis, an implementation wiki, and work-package folders that correspond to the incremental delivery history of the platform. Evidence: docs/README.md:1-17, README.md:64-110.

### Implementation Patterns

The architecture is a small distributed application composed by .NET Aspire AppHost. AppHost is the composition root for local development; it starts and wires the API, the Blazor UI, SQL Server, Keycloak, Mailpit, and related environment settings. Evidence: docs/wiki/architecture.md:5-15, docs/wiki/architecture.md:44-57, .copilot-tracking/research/subagents/2026-06-26/solution-and-projects-research.md.

The backend follows a thin-endpoint pattern. The API is a Minimal API host whose endpoints delegate to application-layer handlers, while Application contains the main orchestration and business-state logic, especially around platform auth supervision and trading-schedule-aware state coordination. Infrastructure owns EF Core persistence, credential protection, notifications, and the outbound IG adapter. Evidence: docs/wiki/architecture.md:95-123, docs/wiki/architecture.md:129-184, .copilot-tracking/research/subagents/2026-06-26/solution-and-projects-research.md.

The operator experience is delivered through a Blazor Server web app that calls the API through a typed client seam. Shared authentication and authorization rules live in the Application layer, while host-specific auth wiring is applied in Web and Api. Evidence: docs/wiki/architecture.md:61-91, docs/wiki/architecture.md:118-123, .copilot-tracking/research/subagents/2026-06-26/solution-and-projects-research.md.

### Complete Examples

```text
src/
  TNC.Trading.Platform.AppHost/          Aspire composition root for local runtime
  TNC.Trading.Platform.Api/              Minimal API control-plane backend
  TNC.Trading.Platform.Application/      Application logic and orchestration
  TNC.Trading.Platform.Infrastructure/   Persistence and external integrations
  TNC.Trading.Platform.ServiceDefaults/  Shared hosting, health, telemetry, resilience
  TNC.Trading.Platform.Web/              Blazor Server operator UI

test/
  TNC.Trading.Platform.Api/              API unit and integration tests
  TNC.Trading.Platform.AppHost/          AppHost unit tests
  TNC.Trading.Platform.Application/      Application unit tests
  TNC.Trading.Platform.Infrastructure/   Infrastructure unit tests
  TNC.Trading.Platform.Web/              Web unit, functional, and E2E tests
```

### API and Schema Documentation

The best repository-native API documentation sources are the root README for the currently exposed endpoints and docs/wiki/api-reference.md for full request and response details. The README explicitly calls out the current local surface area, including `/api/platform/status`, `/api/platform/ig-login/history`, configuration and auth-administration endpoints, `/`, and the health checks. Evidence: README.md:54-62.

### Configuration Examples

```text
global.json
  sdk.version = 10.0.200
  sdk.rollForward = latestFeature

Key local runtime components documented in architecture.md
  AppHost -> SQL Server (`platformdb`)
  AppHost -> Keycloak
  AppHost -> Mailpit
  Web -> Api
  Api -> SQL/notifications/IG demo integration
```

## Technical Scenarios

### Repository Orientation

For a first-time HVE pass, the most effective way to understand this repository is to treat it as a documentation-led, layered .NET Aspire application.

Start at the root README to understand what is currently implemented and what is still intentionally out of scope. Then move to the implementation wiki, especially the architecture guide, to understand runtime topology and project responsibilities. After that, pick one of three code entry points depending on task type: AppHost for startup and environment issues, Api/Application for backend behavior, or Web for operator-flow work.

**Requirements:**

* Explain repository purpose and structure.
* Identify key projects and supporting documents.
* Recommend practical entry points for future work.

**Preferred Approach:**

* Use a three-step onboarding path: README for scope, architecture wiki for structure, then code entry at AppHost, Api/Application, or Web depending on the task.

```text
Repository orientation path
1. README.md
   - What the platform currently does
   - What is not implemented yet
   - High-level solution and docs map
2. docs/wiki/architecture.md
   - Runtime topology and project responsibilities
   - Request flow and persistence model
3. Code entry points by task
   - Startup and environment: src/TNC.Trading.Platform.AppHost/
   - Backend behavior: src/TNC.Trading.Platform.Api/ and src/TNC.Trading.Platform.Application/
   - UI behavior: src/TNC.Trading.Platform.Web/
```

**Implementation Details:**

This repository is intentionally not just a loose set of projects. It has a coherent structure:

* AppHost is the local runtime composition root and the first place to investigate environment, dependency, and local orchestration problems.
* Api is the protected HTTP control plane.
* Application is the main business-orchestration layer and likely the primary place for behavioral changes.
* Infrastructure owns persistence, secrets, notifications, and IG integration seams.
* Web is the operator-facing Blazor Server experience.
* ServiceDefaults holds cross-cutting hosting concerns such as health checks, OpenTelemetry, resilience, and service discovery.

The documentation quality is unusually strong for a first pass. The root README is current and aligned with the implementation wiki, so HVE work here should generally start from the docs before drilling into code. The work-package folders under docs also suggest a repository culture that traces implementation back to requirements and technical specifications.

```text
Recommended first file per task type

Repository scope
  README.md

Architecture and boundaries
  docs/wiki/architecture.md

Run/startup problems
  src/TNC.Trading.Platform.AppHost/AppHost.cs

Backend feature work
  src/TNC.Trading.Platform.Api/Program.cs
  src/TNC.Trading.Platform.Api/Features/Platform/
  src/TNC.Trading.Platform.Application/Services/

Persistence and integrations
  src/TNC.Trading.Platform.Infrastructure/Infrastructure/

UI work
  src/TNC.Trading.Platform.Web/Program.cs
  src/TNC.Trading.Platform.Web/Components/Pages/
  src/TNC.Trading.Platform.Web/PlatformApiClient.cs
```

#### Considered Alternatives

Alternative 1: Start from the solution file and inspect every project first.
Rejected because it is slower and lower-signal than using the existing docs. The repository already has a strong README and architecture guide that explain the same boundaries with less effort. Evidence: README.md:30-110, docs/wiki/architecture.md:3-223.

Alternative 2: Start from runtime entry points only and ignore docs.
Rejected because the repository is organized around work packages and documented architecture. Skipping the docs would miss the distinction between delivered scope and long-term platform goals, which matters when interpreting what code is present versus intentionally absent. Evidence: README.md:1-28, docs/README.md:1-17.

Alternative 3: Treat this as a conventional monolith.
Rejected because the runtime topology is explicitly a small distributed application with Aspire-managed composition, a separate Blazor UI, a separate API, and external local dependencies like Keycloak and SQL Server. Evidence: docs/wiki/architecture.md:5-15, docs/wiki/architecture.md:44-57.
