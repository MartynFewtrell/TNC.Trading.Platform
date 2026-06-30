<!-- markdownlint-disable-file -->
# Task Research: Copilot Instructions Recommendations

Identify suitable Copilot instruction files for this repository so future development guidance matches the codebase, architecture, quality requirements, and documentation workflow.

## Task Implementation Requests

* Review the repository structure, codebase patterns, and existing documentation.
* Recommend a focused set of Copilot instruction files with clear scope and applyTo patterns.
* Explain why each instruction is needed and how it should guide future work.

## Scope and Success Criteria

* Scope: Repository analysis for instruction-file recommendations, including existing docs, source layout, test layout, and any current customization artifacts. Excludes implementing the instruction files themselves unless separately requested.
* Assumptions: The repository primarily contains .NET application code, tests, documentation, and GitHub workflow configuration; recommendations should align to the current structure rather than speculative future technologies.
* Success Criteria:
  * The research identifies concrete repository conventions and development patterns that instructions should encode.
  * The document evaluates alternatives and selects a recommended instruction set.
  * The final guidance includes actionable file names, purpose, and applyTo coverage.

## Outline

1. Establish repository technologies, structure, and dominant workflows.
2. Identify existing guidance and where it is missing or implicit.
3. Evaluate candidate instruction files by scenario.
4. Select a recommended instruction set with rationale.

## Potential Next Research

* Inspect repository-specific Copilot customization files if any exist.
  * Reasoning: Existing customizations may already cover some scenarios or imply naming conventions.
  * Reference: .github/

## Research Executed

### File Analysis

* README.md
  * Confirms the repo is a .NET 10 trading platform with Aspire AppHost, Blazor UI, Keycloak, SQL Server, and a requirement-driven automated test strategy. It also states the one-top-level-type-per-file C# rule: README.md:1-45.
* docs/wiki/local-development.md
  * Defines AppHost as the supported local composition root, documents the canonical run/build/test commands, forbids root-level runtime capture artifacts, and states there is no supported synthetic AppHost runtime path for local startup: docs/wiki/local-development.md:7-49 and docs/wiki/local-development.md:141-188.
* docs/wiki/testing-and-quality.md
  * Defines the test pyramid, keeps distributed AppHost-backed coverage intentionally narrow, and records the current quality evidence model: docs/wiki/testing-and-quality.md:7-39 and docs/wiki/testing-and-quality.md:122-160 and docs/wiki/testing-and-quality.md:197-216.
* docs/wiki/architecture.md
  * Documents the layer responsibilities across AppHost, API, Application, Infrastructure, ServiceDefaults, and Web. These boundaries are explicit and stable enough to justify dedicated guidance: docs/wiki/architecture.md:7-13 and docs/wiki/architecture.md:109-170 and docs/wiki/architecture.md:235-242.
* .editorconfig
  * Captures the core C# style baseline and explicitly references Copilot instruction files as the home for the one-top-level-type rule: .editorconfig:15-17.
* .github/
  * No repository-local Copilot customization files exist yet. The only visible folder is `.github/workflows/`, and it is empty from current workspace evidence.
* .copilot-tracking/
  * The repository actively uses durable planning, review, and research artifacts, which implies that instructions should account for research-first and plan-backed work on larger changes.

### Code Search Results

* `.github` customization search
  * No `.github/copilot-instructions.md`, `.github/instructions/`, `.github/prompts/`, or `.github/agents/` were found.
* `*.instructions.md`
  * Only task-scoped plan instructions exist under `.copilot-tracking/plans/`, which proves instruction-like guidance exists today but is not reusable repository-wide.
* HVE workflow guidance
  * docs/006-refactor-app/hve-agent-recommendations-for-refactoring-workflow.md proposes bounded review, planning, implementation, and validation helper roles that align with the repo's current AI-assisted workflow.

### External Research

* None required initially; prioritize repository evidence.

### Project Conventions

* Standards referenced: HVE markdown and writing-style instructions for research document authoring
* Instructions followed: markdown.instructions.md, writing-style.instructions.md

## Key Discoveries

### Project Structure

The repository is a layered .NET 10 solution with six production projects and multiple test projects arranged by subsystem and test level. The primary runtime topology is Aspire AppHost plus Docker-managed infrastructure, with the Web project hosting a Blazor operator UI and the API project exposing protected control-plane endpoints.

The documentation set is unusually strong and already acts as the primary source of truth for current runtime, testing, and architecture behavior. Future Copilot guidance should point back to those wiki documents instead of duplicating them.

There is no repository-local Copilot customization baseline. That makes the first instruction set high leverage because it will establish the default repository-native guidance surface.

### Implementation Patterns

The dominant patterns that future instructions need to preserve are:

* Thin AppHost composition root with focused support files for infrastructure, project registration, and shared environment wiring.
* Clear architectural boundaries between Application, Infrastructure, Web, API, and ServiceDefaults.
* Prefer cheaper validation first, with distributed AppHost-backed suites kept narrow and milestone-oriented.
* Use docs/wiki as the current-state authority for runtime behavior, local development, and testing guidance.
* Avoid unsupported local workflow artifacts such as root-level log and dashboard captures.
* Treat larger refactoring work as research-and-plan-backed rather than purely opportunistic implementation.

### Complete Examples

```text
.github/
  copilot-instructions.md
  instructions/
    architecture-boundaries.instructions.md
    apphost-runtime.instructions.md
    dotnet-validation.instructions.md
    docs-sync.instructions.md
    testing-strategy.instructions.md
    refactoring-workflow.instructions.md
```

### API and Schema Documentation

The repository does not need a large number of granular instruction files immediately. The evidence supports one root bootstrap file plus a focused set of scenario files that map to stable, high-value development decisions.

### Configuration Examples

```text
applyTo candidates

.github/copilot-instructions.md
  Repository-wide bootstrap file

.github/instructions/architecture-boundaries.instructions.md
  applyTo: 'src/**/*.cs, test/**/*.cs'

.github/instructions/apphost-runtime.instructions.md
  applyTo: 'src/TNC.Trading.Platform.AppHost/**, test/**/AppHost*/**, test/Shared/**, docs/wiki/local-development.md, docs/wiki/runtime-behavior.md'

.github/instructions/dotnet-validation.instructions.md
  applyTo: 'src/**/*.cs, test/**/*.cs'

.github/instructions/docs-sync.instructions.md
  applyTo: 'src/**, test/**, README.md, docs/**/*.md'

.github/instructions/testing-strategy.instructions.md
  applyTo: 'test/**/*.cs, src/**/*.cs'

.github/instructions/refactoring-workflow.instructions.md
  applyTo: '.copilot-tracking/**, docs/006-refactor-app/**/*.md, src/**, test/**'
```

## Technical Scenarios

### Repository-Level Copilot Guidance

The repository needs a small instruction system that does three jobs well:

* bootstrap Copilot with the current source-of-truth documents, runtime model, and default commands
* preserve architecture, runtime, and testing constraints that are easy for an agent to violate accidentally
* encode the repository's preference for bounded research, plan-backed refactoring, and docs-aware delivery

**Requirements:**

* Recommendations must map to actual repository workflows.
* Guidance should be specific enough to improve future edits without over-constraining unrelated files.

**Preferred Approach:**

* Create one root `.github/copilot-instructions.md` plus six focused `.github/instructions/*.instructions.md` files covering architecture boundaries, AppHost runtime rules, .NET validation defaults, documentation sync, testing strategy, and refactoring workflow.

```text
.github/
  copilot-instructions.md
  instructions/
    architecture-boundaries.instructions.md
    apphost-runtime.instructions.md
    dotnet-validation.instructions.md
    docs-sync.instructions.md
    testing-strategy.instructions.md
    refactoring-workflow.instructions.md
```

**Implementation Details:**

Selected instruction files and purpose:

1. `.github/copilot-instructions.md`
   * Purpose: Repository bootstrap for every Copilot interaction.
   * Why it is needed: There is currently no repo-local entry point. This file should tell Copilot that the repo is a .NET 10 Aspire application, AppHost is the supported local composition root, docs/wiki contains the current-state authority, and `dotnet build`, `dotnet test`, and `dotnet test -m:1` are the canonical validation commands.
   * It should also call out the one-top-level-type-per-file rule, the importance of not generating root runtime artifacts, and the expectation that larger changes may need `.copilot-tracking` research or plan artifacts.

2. `.github/instructions/architecture-boundaries.instructions.md`
   * Purpose: Preserve the documented responsibilities of AppHost, API, Application, Infrastructure, ServiceDefaults, and Web.
   * Why it is needed: The architecture is explicit and stable, and cross-layer drift is a likely failure mode for future AI-assisted changes. This file should steer handlers into Application, persistence and external integration into Infrastructure, and thin endpoint/UI surfaces into API and Web.

3. `.github/instructions/apphost-runtime.instructions.md`
   * Purpose: Preserve the supported local and distributed runtime model.
   * Why it is needed: The repo explicitly rejects synthetic AppHost runtime paths for local startup, depends on Docker plus Keycloak, and forbids root-level output capture files. Those are easy mistakes for a general-purpose coding agent to make.

4. `.github/instructions/dotnet-validation.instructions.md`
   * Purpose: Standardize validation command selection and cadence.
   * Why it is needed: The repository has a clear preference for immediate focused validation, with `dotnet test -m:1` as an explicit fallback for intermittent MSBuild child-node issues. This should be available directly to Copilot instead of being scattered across docs and planning artifacts.

5. `.github/instructions/docs-sync.instructions.md`
   * Purpose: Tell Copilot when code changes must update docs.
   * Why it is needed: The repository depends heavily on current-state documentation, but no repo-local instruction currently tells an agent when to update README, local-development, testing-and-quality, runtime-behavior, operator-guide, or API-reference pages.

6. `.github/instructions/testing-strategy.instructions.md`
   * Purpose: Preserve the test pyramid and cost-aware coverage model.
   * Why it is needed: The repo intentionally keeps most confidence in unit and focused functional coverage, with a narrow real-runtime auth matrix. Without explicit instruction, an agent could over-expand expensive distributed suites or skip the cheaper seams the repo prefers.

7. `.github/instructions/refactoring-workflow.instructions.md`
   * Purpose: Encode when research, planning, review, and implementation should be split.
   * Why it is needed: The repository already uses `.copilot-tracking` plans, reviews, and research artifacts and has documented HVE-style workflow recommendations, but none of that is yet packaged as repo-local Copilot guidance.

```text
Recommended authoring order

1. .github/copilot-instructions.md
2. .github/instructions/dotnet-validation.instructions.md
3. .github/instructions/apphost-runtime.instructions.md
4. .github/instructions/architecture-boundaries.instructions.md
5. .github/instructions/testing-strategy.instructions.md
6. .github/instructions/docs-sync.instructions.md
7. .github/instructions/refactoring-workflow.instructions.md
```

#### Considered Alternatives

Alternative 1: Only create `.github/copilot-instructions.md`.

Rejected because the repository has several high-value, file-pattern-specific rules that would make a single root file noisy and less actionable. The AppHost runtime constraints, testing strategy, and docs-sync heuristics each deserve focused instructions.

Alternative 2: Create many project-specific instruction files, one per production and test project.

Rejected because the repository does not yet need that level of granularity. The current conventions cluster naturally around architecture, runtime, testing, validation, docs, and refactoring workflow. More files would increase maintenance cost before the baseline exists.

Alternative 3: Put the guidance only in docs and skip repo-local Copilot customization.

Rejected because the repository already has strong human-facing docs, but the central problem is that Copilot does not consume a repo-local bootstrap or scoped instruction set today. Converting key rules into `.github` instruction files is the direct fix.

## Selected Approach

Create a compact, repository-local Copilot customization set composed of one root bootstrap file and six focused instruction files:

* `.github/copilot-instructions.md`
* `.github/instructions/architecture-boundaries.instructions.md`
* `.github/instructions/apphost-runtime.instructions.md`
* `.github/instructions/dotnet-validation.instructions.md`
* `.github/instructions/docs-sync.instructions.md`
* `.github/instructions/testing-strategy.instructions.md`
* `.github/instructions/refactoring-workflow.instructions.md`

This is the best fit because it matches the real failure modes visible in the repository: architecture drift across layers, incorrect AppHost/runtime assumptions, missed doc updates, over- or under-validation, and ad hoc handling of larger refactoring work.

## Actionable Next Steps

* Create `.github/copilot-instructions.md` first and keep it short. It should point to source-of-truth docs and summarize the non-negotiable repo rules.
* Add the validation and AppHost runtime instruction files next because they capture the most frequently exercised operational guidance.
* Add architecture and testing strategy guidance once the bootstrap is in place.
* Add docs-sync and refactoring-workflow guidance to improve change completeness and reduce drift in larger work.
