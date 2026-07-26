---
title: Copilot Instructions Microsoft Learn Research
description: Research findings from official Microsoft documentation for planning a repository-local Copilot instruction set for a .NET 10 and Aspire repository
author: GitHub Copilot
ms.date: 2026-06-28
ms.topic: reference
keywords:
  - github copilot
  - custom instructions
  - dotnet
  - aspire
  - testing
  - architecture
estimated_reading_time: 8
---

## Research scope

* GitHub Copilot custom instructions and repository instruction structure and scoping
* .NET code style and project organization guidance relevant to architecture-boundary instructions
* Testing and validation best practices for .NET
* Documentation maintenance, architecture documentation, and inner source or repository guidance where relevant

## Research status

Complete

## Findings

### 1. GitHub Copilot custom instructions structure and scoping

Microsoft guidance supports a layered instruction approach instead of a single monolithic repository file.

* Visual Studio guidance says repository instructions can live in `.github/copilot-instructions.md` for repo-wide behavior, and more targeted `.github/instructions/*.instructions.md` files can be scoped with `applyTo` glob patterns for file types, folders, frameworks, or tasks. Source: Customize chat responses and set context. <https://learn.microsoft.com/visualstudio/ide/copilot-chat-context?view=visualstudio#use-custom-instructions>
* The same guidance says targeted instruction files should use YAML frontmatter with `description` and `applyTo`, followed by markdown instruction content. Source: Customize chat responses and set context. <https://learn.microsoft.com/visualstudio/ide/copilot-chat-context?view=visualstudio#use-custom-instructions>
* Microsoft SQL tooling guidance states that matching instruction files are automatically applied across Copilot surfaces, including ask, edit, agent, and inline completions. That is useful evidence for planning repository-local rules that must influence both chat and code generation. Source: Quickstart: Use custom instructions to align GitHub Copilot with your T-SQL conventions. <https://learn.microsoft.com/sql/tools/visual-studio-code-extensions/github-copilot/custom-instructions?view=sql-server-ver17>
* Microsoft’s explicit best practices for instruction authoring are directly relevant to this repository: one file per language or domain, keep instructions declarative, version control the instruction files, and test after changes by confirming the instructions are applied. Source: Quickstart: Use custom instructions to align GitHub Copilot with your T-SQL conventions. <https://learn.microsoft.com/sql/tools/visual-studio-code-extensions/github-copilot/custom-instructions?view=sql-server-ver17#patterns-and-best-practices>
* Microsoft guidance also recommends verifying that instructions were actually attached to requests by inspecting Copilot debug output or references, which is relevant to any rollout or validation plan for the new instruction set. Source: Quickstart: Use custom instructions to align GitHub Copilot with your T-SQL conventions. <https://learn.microsoft.com/sql/tools/visual-studio-code-extensions/github-copilot/custom-instructions?view=sql-server-ver17#see-the-before-after-contrast>

Implication for this repository:

* Use a small repo-wide `.github/copilot-instructions.md` only for universal rules.
* Put architecture-boundary, testing, docs, and possibly frontend or infrastructure guidance into separate targeted `.github/instructions/*.instructions.md` files.
* Keep each file narrow enough that its `applyTo` scope is explainable and testable.

### 2. .NET code style and project organization guidance for architecture-boundary instructions

Microsoft guidance supports instructions that enforce separation by project responsibility, explicit dependency boundaries, namespace and folder alignment, and modern C# conventions.

* The .NET program organization guidance describes the hierarchy of solution, project, assembly, namespace, and type, and says projects should be split for concrete reasons such as shared reuse, separation of concerns, and dependency control. Source: Program organization. <https://learn.microsoft.com/dotnet/csharp/fundamentals/program-structure/program-organization>
* The same article explicitly recommends using projects to separate concerns such as data access, business logic, and presentation layers, and notes that a project can only use types from projects it explicitly references. This is strong backing for instruction text about allowed dependency direction between Web, Application, Infrastructure, Api, AppHost, and ServiceDefaults projects. Source: Program organization, Projects and assemblies. <https://learn.microsoft.com/dotnet/csharp/fundamentals/program-structure/program-organization#projects-and-assemblies>
* Microsoft recommends matching namespaces to folder structure and keeping them in sync. That supports repository instructions that forbid namespace drift and ask generated code to follow the existing folder hierarchy. Source: Program organization, Projects and assemblies. <https://learn.microsoft.com/dotnet/csharp/fundamentals/program-structure/program-organization#projects-and-assemblies>
* Microsoft also recommends organizing namespaces by feature or responsibility rather than by type kind, and defaulting to `internal` for nonshared implementation details. Both are useful for architecture-boundary instructions because they help keep features cohesive and shrink unintended public surface area. Source: Program organization, Organize namespaces by feature, not by type kind. <https://learn.microsoft.com/dotnet/csharp/fundamentals/program-structure/program-organization#organize-namespaces-by-feature,-not-by-type-kind>
* The C# coding conventions guidance recommends modern language features, clear and simple code, specific exceptions, async and await for I/O-bound work, and disciplined `var` usage only when the type is obvious. Source: Common C# code conventions. <https://learn.microsoft.com/dotnet/csharp/fundamentals/coding-style/coding-conventions>
* Microsoft naming guidance recommends PascalCase for types and public members, camelCase for locals and parameters, underscore-prefixed private fields, meaningful names, and clear namespaces and assembly names. Source: C# identifier naming rules and conventions. <https://learn.microsoft.com/dotnet/csharp/fundamentals/coding-style/identifier-names#naming-conventions>

Implication for this repository:

* Architecture instructions can legitimately tell Copilot to preserve project boundaries and avoid adding references across layers without a concrete dependency reason.
* File-scoped instructions for `src/TNC.Trading.Platform.Application/**`, `src/TNC.Trading.Platform.Infrastructure/**`, `src/TNC.Trading.Platform.Web/**`, and `src/TNC.Trading.Platform.Api/**` should express different allowed responsibilities.
* Style instructions should stay small and defer to `.editorconfig` or analyzers where possible instead of duplicating every coding rule in prose.

### 3. Testing and validation best practices for .NET

Microsoft guidance strongly favors explicit separation of test types, fast unit tests, restrained integration testing, and tests as executable documentation.

* The .NET testing overview distinguishes unit tests from integration tests and states that unit tests should not cover infrastructure concerns while integration tests often do. Source: Testing in .NET. <https://learn.microsoft.com/dotnet/core/testing/>
* Unit testing best practices define good unit tests as fast, isolated, repeatable, self-checking, and timely. Source: Unit testing best practices for .NET. <https://learn.microsoft.com/dotnet/core/testing/unit-testing-best-practices#characteristics-of-good-unit-tests>
* Microsoft recommends avoiding infrastructure dependencies in unit tests, keeping unit and integration tests in separate projects, using clear naming conventions, minimizing logic inside tests, and treating tests as readable documentation of behavior. Source: Unit testing best practices for .NET. <https://learn.microsoft.com/dotnet/core/testing/unit-testing-best-practices#best-practices>
* Microsoft also cautions that code coverage alone is not a quality signal and that overly ambitious coverage targets can become counterproductive. Source: Unit testing best practices for .NET. <https://learn.microsoft.com/dotnet/core/testing/unit-testing-best-practices#characteristics-of-good-unit-tests>
* ASP.NET Core integration testing guidance recommends using integration tests only for important infrastructure scenarios, preferring unit tests where either approach would work, and separating unit and integration tests into different projects. Source: Integration tests in ASP.NET Core. <https://learn.microsoft.com/aspnet/core/test/integration-tests?view=aspnetcore-10.0>
* The same integration guidance notes that test apps commonly replace runtime registrations in `WebApplicationFactory` setup and seed small datasets, which is relevant if repository instructions later include preferred patterns for API and web integration tests. Source: Integration tests in ASP.NET Core. <https://learn.microsoft.com/aspnet/core/test/integration-tests?view=aspnetcore-10.0#integration-tests-sample>

Implication for this repository:

* Repository instructions should tell Copilot to add or update the narrowest relevant tests first.
* Unit-test instructions should forbid hidden infrastructure coupling and prefer behavior-focused naming.
* Validation instructions should emphasize targeted test execution for the touched slice before broad test runs.
* Any plan should avoid claiming raw coverage percentages as the primary acceptance metric.

### 4. .NET Aspire-specific guidance relevant to repo-local instructions

Microsoft Learn material on Aspire supports keeping orchestration concerns localized and reusing ServiceDefaults intentionally.

* Aspire-related guidance consistently treats the AppHost project as the orchestration entry point and the ServiceDefaults project as the shared place for telemetry, health checks, service discovery, and resilience defaults. Sources: Azure Functions with Aspire, solution structure. <https://learn.microsoft.com/azure/azure-functions/dotnet-aspire-integration#solution-structure>; .NET observability with OpenTelemetry, OpenTelemetry in Aspire. <https://learn.microsoft.com/dotnet/core/diagnostics/observability-with-otel#opentelemetry-in-aspire>
* Microsoft explicitly recommends using ServiceDefaults to share common configuration and not duplicating direct monitoring setup in downstream projects when Aspire should own that concern. Source: Azure Functions with Aspire, solution structure. <https://learn.microsoft.com/azure/azure-functions/dotnet-aspire-integration#solution-structure>
* Microsoft documentation for Aspire-related integrations repeatedly describes ServiceDefaults as the place for default resilience and telemetry behavior, even outside full Aspire orchestration. Source: .NET observability with OpenTelemetry, OpenTelemetry in Aspire. <https://learn.microsoft.com/dotnet/core/diagnostics/observability-with-otel#opentelemetry-in-aspire>
* Aspire training materials include explicit testing goals and mention Aspire testing templates, which supports repository instructions that require preserving AppHost and test project patterns instead of inventing bespoke orchestration or end-to-end scaffolding. Source: Use databases in a .NET Aspire project. <https://learn.microsoft.com/training/modules/use-databases-dotnet-aspire-app/>

Implication for this repository:

* Add targeted instructions for `src/TNC.Trading.Platform.AppHost/**` that treat AppHost as orchestration-only.
* Add targeted instructions for `src/TNC.Trading.Platform.ServiceDefaults/**` that treat ServiceDefaults as the shared home for observability, health, resilience, and cross-service defaults.
* Avoid instructions that encourage business logic in AppHost or duplicated service-default setup in leaf services.

### 5. Documentation maintenance, architecture records, and repository guidance

Microsoft guidance supports keeping architecture decisions and repository documentation in a visible, structured source of truth inside the repo.

* Azure Well-Architected guidance says ADRs should start early, be maintained through the workload lifespan, remain append-only, include context, options, decision outcome, tradeoffs, status, and rationale, and be stored openly with the workload documentation repository. Source: Maintain an architecture decision record (ADR). <https://learn.microsoft.com/azure/well-architected/architect-role/architecture-decision-record>
* The same ADR guidance explicitly says the documentation repository should be the single source of truth for decisions and related assets. Source: Maintain an architecture decision record (ADR), Workload documentation repository. <https://learn.microsoft.com/azure/well-architected/architect-role/architecture-decision-record#workload-documentation-repository>
* Microsoft Learn documentation guidance for GitHub repositories stresses discoverable directory organization, consistent templates, metadata, and reusable content patterns. While aimed at docs repos, it is still useful support for keeping repo documentation structured and easy to navigate. Source: Git and GitHub essentials for Microsoft Learn documentation. <https://learn.microsoft.com/contribute/content/git-github-fundamentals#github>
* Microsoft Learn training for InnerSource emphasizes discoverable repositories, robust READMEs, templates, and transparency, which is relevant to planning supporting repository docs around instruction usage and contribution workflows. Source: Manage an InnerSource program by using GitHub. <https://learn.microsoft.com/training/modules/manage-innersource-program-github/>

Implication for this repository:

* The implementation plan can justify keeping Copilot instruction design and governance documentation in-repo, close to architecture and workflow documents.
* Supporting docs should explain scope, ownership, change process, and validation method for instruction files.
* If the team wants durable rationale for instruction boundaries, ADR-style records fit Microsoft guidance better than undocumented conventions.

## Recommended citations by target document

### Cite in the implementation plan

Use the most decision-shaping sources in the high-level plan.

* Customize chat responses and set context
  * Use for the core repository versus targeted instruction file model and `applyTo` scoping.
  * URL: <https://learn.microsoft.com/visualstudio/ide/copilot-chat-context?view=visualstudio#use-custom-instructions>
* Quickstart: Use custom instructions to align GitHub Copilot with your T-SQL conventions
  * Use for best practices such as one file per domain, declarative rules, version control, and validation after changes.
  * URL: <https://learn.microsoft.com/sql/tools/visual-studio-code-extensions/github-copilot/custom-instructions?view=sql-server-ver17#patterns-and-best-practices>
* Program organization
  * Use for project boundaries, dependency control, separation of concerns, and namespace or folder alignment.
  * URLs: <https://learn.microsoft.com/dotnet/csharp/fundamentals/program-structure/program-organization> and <https://learn.microsoft.com/dotnet/csharp/fundamentals/program-structure/program-organization#projects-and-assemblies>
* Unit testing best practices for .NET
  * Use for the testing philosophy that should shape instruction rollout and validation expectations.
  * URL: <https://learn.microsoft.com/dotnet/core/testing/unit-testing-best-practices>
* Integration tests in ASP.NET Core
  * Use for the distinction between unit and integration testing and for limiting costly integration coverage.
  * URL: <https://learn.microsoft.com/aspnet/core/test/integration-tests?view=aspnetcore-10.0>
* Maintain an architecture decision record (ADR)
  * Use for documenting instruction governance and change rationale in the repository documentation set.
  * URL: <https://learn.microsoft.com/azure/well-architected/architect-role/architecture-decision-record>
* Azure Functions with Aspire, solution structure, plus OpenTelemetry in Aspire
  * Use for the AppHost and ServiceDefaults architecture roles that justify separate scoped instruction files in an Aspire repository.
  * URLs: <https://learn.microsoft.com/azure/azure-functions/dotnet-aspire-integration#solution-structure> and <https://learn.microsoft.com/dotnet/core/diagnostics/observability-with-otel#opentelemetry-in-aspire>

### Cite in the details file

Use these sources when writing the fuller rationale, examples, and enforcement details.

* Common C# code conventions
  * URL: <https://learn.microsoft.com/dotnet/csharp/fundamentals/coding-style/coding-conventions>
* C# identifier naming rules and conventions
  * URL: <https://learn.microsoft.com/dotnet/csharp/fundamentals/coding-style/identifier-names#naming-conventions>
* Program organization, Organize namespaces by feature, not by type kind
  * URL: <https://learn.microsoft.com/dotnet/csharp/fundamentals/program-structure/program-organization#organize-namespaces-by-feature,-not-by-type-kind>
* Testing in .NET
  * URL: <https://learn.microsoft.com/dotnet/core/testing/>
* Unit testing best practices for .NET, specific best-practice sections on naming, avoiding logic in tests, separate projects, and coverage caution
  * URL: <https://learn.microsoft.com/dotnet/core/testing/unit-testing-best-practices#best-practices>
* Integration tests in ASP.NET Core, integration sample guidance
  * URL: <https://learn.microsoft.com/aspnet/core/test/integration-tests?view=aspnetcore-10.0#integration-tests-sample>
* Git and GitHub essentials for Microsoft Learn documentation
  * URL: <https://learn.microsoft.com/contribute/content/git-github-fundamentals#github>
* Manage an InnerSource program by using GitHub
  * URL: <https://learn.microsoft.com/training/modules/manage-innersource-program-github/>
* Use databases in a .NET Aspire project
  * URL: <https://learn.microsoft.com/training/modules/use-databases-dotnet-aspire-app/>

## Suggested instruction-set design directions supported by the research

These are not implementation edits. They are planning directions supported by the sources above.

* Keep one concise root instruction file for universal repository constraints.
* Create targeted instruction files for AppHost, ServiceDefaults, application core, infrastructure, API or web, tests, and documentation.
* Phrase architecture rules in terms of responsibilities and forbidden dependency directions, not general style advice.
* Phrase testing rules in terms of test type, scope, validation order, and readable naming.
* Keep durable rationale and governance for the instruction set in repository documentation, ideally with ADR-style records for significant decisions.

## Open questions

* Microsoft Learn material directly describing GitHub Copilot repository instructions in VS Code is still scattered across product pages. The strongest structure and scoping guidance found here comes from Visual Studio and Microsoft SQL tooling pages rather than a single consolidated .NET or VS Code Learn article.