---
title: Copilot Instructions Implementation Research
description: Implementation-focused research for creating repository-local Copilot instruction files in .github using repository evidence and Microsoft Learn guidance.
author: GitHub Copilot
ms.date: 2026-06-28
ms.topic: reference
keywords:
  - github copilot
  - custom instructions
  - dotnet
  - aspire
  - testing
  - planning
estimated_reading_time: 7
---
<!-- markdownlint-disable-file -->

## Task Focus

Plan the implementation of a repository-local Copilot instruction set under .github for the TNC.Trading.Platform repository. The plan must stay aligned with the existing repository recommendation research and incorporate Microsoft Learn best-practice guidance where it materially affects file structure, scope, validation, or governance.

## Source Inputs

* .copilot-tracking/research/2026-06-28/copilot-instructions-recommendations-research.md
* .copilot-tracking/research/subagents/2026-06-28/copilot-instructions-microsoft-learn-research.md
* README.md
* docs/wiki/architecture.md
* docs/wiki/local-development.md
* docs/wiki/testing-and-quality.md
* .editorconfig

## Implementation-Relevant Findings

### Repository-specific direction already established

* The selected repository-local customization shape is one root .github/copilot-instructions.md file plus six focused .github/instructions/*.instructions.md files.
* The recommended files are architecture-boundaries, apphost-runtime, dotnet-validation, docs-sync, testing-strategy, and refactoring-workflow.
* The repository already treats docs/wiki as the source of truth for runtime, testing, and architecture behavior.
* AppHost is the supported local composition root and root-level runtime capture artifacts are not part of the supported workflow.
* Larger refactoring work already uses .copilot-tracking planning and research artifacts.

### Microsoft Learn guidance that should influence the implementation plan

* Microsoft supports a layered instruction model: one repo-wide .github/copilot-instructions.md file plus narrower .github/instructions/*.instructions.md files with applyTo frontmatter for scoped behavior. Source: Customize chat responses and set context. <https://learn.microsoft.com/visualstudio/ide/copilot-chat-context?view=visualstudio#use-custom-instructions>
* Microsoft recommends instruction files stay declarative, be version-controlled, be split by domain or language when useful, and be validated after changes by confirming that Copilot actually applied them. Source: Quickstart: Use custom instructions to align GitHub Copilot with your T-SQL conventions. <https://learn.microsoft.com/sql/tools/visual-studio-code-extensions/github-copilot/custom-instructions?view=sql-server-ver17#patterns-and-best-practices>
* Microsoft .NET program-organization guidance supports project-level separation of concerns and dependency control, which justifies architecture-boundary instructions that keep responsibilities separated between Application, Infrastructure, Web, Api, AppHost, and ServiceDefaults. Source: Program organization. <https://learn.microsoft.com/dotnet/csharp/fundamentals/program-structure/program-organization>
* Microsoft recommends organizing namespaces and folders by feature or responsibility, which supports targeted instruction wording that preserves the repository's layer and feature boundaries without turning the instruction files into generic style guides. Source: Program organization. <https://learn.microsoft.com/dotnet/csharp/fundamentals/program-structure/program-organization#organize-namespaces-by-feature,-not-by-type-kind>
* Microsoft unit-testing guidance favors fast, isolated, repeatable tests and warns against relying on coverage percentages alone. That aligns with the repository's preference for cheap validation first and narrow expensive AppHost-backed suites. Source: Unit testing best practices for .NET. <https://learn.microsoft.com/dotnet/core/testing/unit-testing-best-practices>
* Microsoft integration-testing guidance says to prefer unit tests where either approach would work and reserve integration tests for infrastructure-significant scenarios. That supports the testing-strategy and dotnet-validation instruction design. Source: Integration tests in ASP.NET Core. <https://learn.microsoft.com/aspnet/core/test/integration-tests?view=aspnetcore-10.0>
* Microsoft Aspire guidance treats AppHost as orchestration-focused and ServiceDefaults as the shared home for observability and resilience defaults. That directly supports a dedicated AppHost runtime instruction file. Sources: Azure Functions with Aspire, solution structure. <https://learn.microsoft.com/azure/azure-functions/dotnet-aspire-integration#solution-structure>; OpenTelemetry in Aspire. <https://learn.microsoft.com/dotnet/core/diagnostics/observability-with-otel#opentelemetry-in-aspire>
* Microsoft Well-Architected ADR guidance supports keeping durable rationale for repository conventions in the repo's documentation source of truth, which is relevant if the implementation adds or updates supporting documentation that explains instruction governance. Source: Maintain an architecture decision record (ADR). <https://learn.microsoft.com/azure/well-architected/architect-role/architecture-decision-record>

## Selected Planning Direction

Plan for exactly seven .github files in the first rollout:

* .github/copilot-instructions.md
* .github/instructions/architecture-boundaries.instructions.md
* .github/instructions/apphost-runtime.instructions.md
* .github/instructions/dotnet-validation.instructions.md
* .github/instructions/docs-sync.instructions.md
* .github/instructions/testing-strategy.instructions.md
* .github/instructions/refactoring-workflow.instructions.md

The rollout should be staged. The root bootstrap file comes first, operational guidance files follow, then boundary and change-completeness files, and the work ends with scope validation and representative Copilot-application checks.

## Planning Constraints

* The implementation plan should keep the first release compact rather than introducing per-project instruction files.
* Instruction wording should point to existing repository docs instead of duplicating large sections of wiki content.
* Validation should include representative checks that each instruction file applies only to the intended file patterns.
* The plan should leave room for later follow-on work such as a ServiceDefaults-specific instruction file only if actual implementation friction reveals the need.

## Implementation Risks That The Plan Should Address

* Overlapping applyTo patterns could make instructions noisy or contradictory if scopes are not clearly separated.
* A monolithic bootstrap file would be harder to maintain and less testable than a root file plus narrower domain files.
* Instructions that restate general C# style instead of repository-specific constraints would add maintenance cost with little value.
* Without explicit validation steps, the repository could add instruction files that look correct in markdown but are not actually applied by Copilot in the intended contexts.

## Recommended Validation Model For The Plan

* Validate file structure first: required frontmatter, clear descriptions, and intentionally narrow applyTo scopes.
* Validate content second: each instruction file should encode repository-specific rules that are not already fully enforced by analyzers.
* Validate behavior third: run representative Copilot checks on at least one matching file per instruction to confirm the instruction is attached or referenced in context, consistent with Microsoft's guidance.
