---
description: "Implementation plan for creating the first repository-local Copilot instruction set under .github with Microsoft Learn aligned scope, validation, and governance guidance"
applyTo: '.github/copilot-instructions.md, .github/instructions/**/*.instructions.md'
---
<!-- markdownlint-disable-file -->
# Implementation Plan: Copilot Instructions Rollout

## Overview

Create the first repository-local Copilot instruction set under .github as a compact seven-file rollout, staged so the bootstrap, operational rules, architectural boundaries, documentation sync, and workflow guidance are authored with explicit Microsoft Learn backed validation.

## Objectives

### User Requirements

* Create a plan for a new set of Copilot instruction files in .github based on the existing research recommendation. Source: user request on 2026-06-28.
* Ensure the plan includes reference to Microsoft Learn for best-practice advice wherever relevant. Source: user request on 2026-06-28.

### Derived Objectives

* Keep the first release to one root .github/copilot-instructions.md file plus six focused .github/instructions/*.instructions.md files because the repository research already selected that as the best-fit baseline. Derived from: .copilot-tracking/research/2026-06-28/copilot-instructions-recommendations-research.md (Lines 181-205).
* Use repo-wide versus scoped instruction separation because Microsoft guidance explicitly recommends layered custom instruction structure with targeted applyTo scopes. Derived from: .copilot-tracking/research/2026-06-28/copilot-instructions-implementation-research.md (Lines 42-51).
* End the rollout with representative Copilot applicability checks because Microsoft guidance recommends validating that instructions were actually applied, not only that the markdown files exist. Derived from: .copilot-tracking/research/subagents/2026-06-28/copilot-instructions-microsoft-learn-research.md.
* Preserve existing repository source-of-truth documents rather than duplicating wiki content inside instruction files. Derived from: .copilot-tracking/research/2026-06-28/copilot-instructions-recommendations-research.md (Lines 71-88) and .copilot-tracking/research/2026-06-28/copilot-instructions-implementation-research.md (Lines 67-85).
* Keep each instruction file declarative and domain-scoped because Microsoft guidance recommends splitting instructions by domain and avoiding overgrown or procedural files. Derived from: .copilot-tracking/research/2026-06-28/copilot-instructions-implementation-research.md (Lines 42-45).

## Context Summary

### Project Files

* .github/ - target folder for the new repository-local Copilot bootstrap and scoped instruction set.
* README.md - repository overview and one-top-level-type-per-file rule anchor.
* docs/wiki/architecture.md - stable project-boundary responsibilities for architecture guidance.
* docs/wiki/local-development.md - AppHost and supported local workflow authority.
* docs/wiki/testing-and-quality.md - test pyramid and validation strategy authority.
* .editorconfig - existing C# baseline and rule references.

### References

* .copilot-tracking/research/2026-06-28/copilot-instructions-recommendations-research.md - repository-specific recommendation for the seven-file instruction set.
* .copilot-tracking/research/2026-06-28/copilot-instructions-implementation-research.md - implementation-focused synthesis of repository evidence plus Microsoft Learn guidance.
* .copilot-tracking/research/subagents/2026-06-28/copilot-instructions-microsoft-learn-research.md - official Microsoft documentation references for instruction structure, .NET organization, testing, Aspire roles, and documentation governance.
* <https://learn.microsoft.com/visualstudio/ide/copilot-chat-context?view=visualstudio#use-custom-instructions> - Microsoft guidance for repo-wide and scoped custom instruction files.
* <https://learn.microsoft.com/sql/tools/visual-studio-code-extensions/github-copilot/custom-instructions?view=sql-server-ver17#patterns-and-best-practices> - Microsoft guidance for instruction authoring and validation best practices.
* <https://learn.microsoft.com/dotnet/csharp/fundamentals/program-structure/program-organization> - Microsoft guidance for project boundaries and separation of concerns.
* <https://learn.microsoft.com/dotnet/core/testing/unit-testing-best-practices> - Microsoft guidance for fast, isolated, repeatable tests and validation philosophy.
* <https://learn.microsoft.com/aspnet/core/test/integration-tests?view=aspnetcore-10.0> - Microsoft guidance for reserving integration tests for higher-cost infrastructure scenarios.
* <https://learn.microsoft.com/azure/azure-functions/dotnet-aspire-integration#solution-structure> - Microsoft guidance for AppHost and ServiceDefaults solution roles.
* <https://learn.microsoft.com/dotnet/core/diagnostics/observability-with-otel#opentelemetry-in-aspire> - Microsoft guidance reinforcing ServiceDefaults and Aspire observability responsibilities.
* <https://learn.microsoft.com/azure/well-architected/architect-role/architecture-decision-record> - Microsoft guidance for durable in-repo decision rationale and governance.

### Standards References

* c:\Users\martynfewtrell\.vscode\extensions\ise-hve-essentials.hve-core-3.2.2\.github\instructions\hve-core\markdown.instructions.md - markdown authoring requirements.
* c:\Users\martynfewtrell\.vscode\extensions\ise-hve-essentials.hve-core-3.2.2\.github\instructions\hve-core\writing-style.instructions.md - writing style requirements.
* c:\Users\martynfewtrell\.vscode\extensions\ise-hve-essentials.hve-core-3.2.2\.github\instructions\hve-core\prompt-builder.instructions.md - requirements for .instructions.md authoring patterns.

## Implementation Checklist

## Phase 1 Baseline Decisions

### Seven-file baseline and single-responsibility mapping

* `.github/copilot-instructions.md`: repository-wide bootstrap and universal non-negotiable rules only
* `.github/instructions/architecture-boundaries.instructions.md`: project responsibilities and dependency direction
* `.github/instructions/apphost-runtime.instructions.md`: AppHost, ServiceDefaults, and supported local runtime guidance
* `.github/instructions/dotnet-validation.instructions.md`: validation command choice, ordering, and cadence
* `.github/instructions/docs-sync.instructions.md`: documentation update triggers and source-of-truth routing
* `.github/instructions/testing-strategy.instructions.md`: test-pyramid guidance and cost-aware test selection
* `.github/instructions/refactoring-workflow.instructions.md`: research, planning, and bounded rollout workflow guidance

The baseline remains exactly seven files. No extra per-project, per-language, or speculative instruction files are part of Phase 1.

### Scoped `applyTo` baseline

* `.github/copilot-instructions.md`: no `applyTo`; repository bootstrap file
* `.github/instructions/architecture-boundaries.instructions.md`: `src/**/*.cs, test/**/*.cs`
* `.github/instructions/apphost-runtime.instructions.md`: `src/TNC.Trading.Platform.AppHost/**, src/TNC.Trading.Platform.ServiceDefaults/**, test/**/AppHost*/**, test/Shared/**, docs/wiki/local-development.md, docs/wiki/runtime-behavior.md`
* `.github/instructions/dotnet-validation.instructions.md`: `src/**/*.cs, test/**/*.cs`
* `.github/instructions/docs-sync.instructions.md`: `src/**, test/**, README.md, docs/**/*.md`
* `.github/instructions/testing-strategy.instructions.md`: `test/**/*.cs, src/**/*.cs`
* `.github/instructions/refactoring-workflow.instructions.md`: `.copilot-tracking/**, docs/006-refactor-app/**/*.md, src/**, test/**`

### Planned overlap model

* Overlap between `architecture-boundaries`, `dotnet-validation`, and `testing-strategy` on `src/**/*.cs` and `test/**/*.cs` is intentional because they govern different concerns: dependency boundaries, validation cadence, and test-selection behavior.
* Overlap between `apphost-runtime` and `docs-sync` on runtime documentation files is intentional because one defines runtime constraints and the other defines update obligations.
* Overlap between `refactoring-workflow` and `docs-sync` on `.copilot-tracking` and refactor documentation is intentional because one controls delivery workflow while the other controls documentation completeness.
* No overlap should cause contradictory normative guidance. Phase 5 validation must check for that explicitly.

### Validation model baseline

* Structural validation: confirm required frontmatter shape, filenames, and explainable `applyTo` values for every scoped instruction file.
* Content validation: confirm each file remains declarative, domain-scoped, repository-specific, and anchored to source-of-truth documents rather than copied wiki content.
* Behavior validation: confirm representative matching files cause the expected instruction file to appear in Copilot request references or attached context.
* Overlap validation: confirm overlapping instruction scopes remain complementary and non-contradictory across the combined `.github` instruction set.

### [x] Implementation Phase 1: Define the instruction system skeleton and validation model

<!-- parallelizable: false -->

* [x] Step 1.1: Confirm the seven-file baseline and map each file to a single responsibility
  * Details: .copilot-tracking/details/2026-06-28/copilot-instructions-rollout-details.md (Lines 25-48)
* [x] Step 1.2: Define scoped applyTo patterns before writing file content
  * Details: .copilot-tracking/details/2026-06-28/copilot-instructions-rollout-details.md (Lines 50-73)
* [x] Step 1.3: Define the validation model for the instruction rollout
  * Details: .copilot-tracking/details/2026-06-28/copilot-instructions-rollout-details.md (Lines 75-94)

### [ ] Implementation Phase 2: Author the bootstrap and operational instruction files

<!-- parallelizable: false -->

* [x] Step 2.1: Create .github/copilot-instructions.md as the short repository bootstrap
  * Details: .copilot-tracking/details/2026-06-28/copilot-instructions-rollout-details.md (Lines 100-117)
* [x] Step 2.2: Create .github/instructions/dotnet-validation.instructions.md
  * Details: .copilot-tracking/details/2026-06-28/copilot-instructions-rollout-details.md (Lines 119-138)
* [x] Step 2.3: Create .github/instructions/apphost-runtime.instructions.md
  * Details: .copilot-tracking/details/2026-06-28/copilot-instructions-rollout-details.md (Lines 140-157)
* [ ] Step 2.4: Validate the bootstrap and operational files together
  * Details: .copilot-tracking/details/2026-06-28/copilot-instructions-rollout-details.md (Lines 159-172)

### [ ] Implementation Phase 3: Author architecture and testing instruction files

<!-- parallelizable: true -->

* [x] Step 3.1: Create .github/instructions/architecture-boundaries.instructions.md
  * Details: .copilot-tracking/details/2026-06-28/copilot-instructions-rollout-details.md (Lines 178-195)
* [x] Step 3.2: Create .github/instructions/testing-strategy.instructions.md
  * Details: .copilot-tracking/details/2026-06-28/copilot-instructions-rollout-details.md (Lines 197-214)
* [ ] Step 3.3: Validate architecture and testing instructions against representative files
  * Details: .copilot-tracking/details/2026-06-28/copilot-instructions-rollout-details.md (Lines 216-229)

### [ ] Implementation Phase 4: Author documentation and workflow instruction files

<!-- parallelizable: true -->

* [x] Step 4.1: Create .github/instructions/docs-sync.instructions.md
  * Details: .copilot-tracking/details/2026-06-28/copilot-instructions-rollout-details.md (Lines 235-252)
* [x] Step 4.2: Create .github/instructions/refactoring-workflow.instructions.md
  * Details: .copilot-tracking/details/2026-06-28/copilot-instructions-rollout-details.md (Lines 254-271)
* [ ] Step 4.3: Validate documentation and workflow instructions together
  * Details: .copilot-tracking/details/2026-06-28/copilot-instructions-rollout-details.md (Lines 273-286)

### [ ] Implementation Phase 5: Final validation and rollout handoff

<!-- parallelizable: false -->

* [ ] Step 5.1: Run the full instruction-set validation pass
  * Details: .copilot-tracking/details/2026-06-28/copilot-instructions-rollout-details.md (Lines 292-306)
* [x] Step 5.2: Record rollout rationale in repository-facing documentation if the implementation introduces governance notes
  * Details: .copilot-tracking/details/2026-06-28/copilot-instructions-rollout-details.md (Lines 308-328)
* [x] Step 5.3: Prepare the follow-on backlog based on implementation friction
  * Details: .copilot-tracking/details/2026-06-28/copilot-instructions-rollout-details.md (Lines 330-337)

## Planning Log

See .copilot-tracking/plans/logs/2026-06-28/copilot-instructions-rollout-log.md for discrepancy tracking, implementation paths considered, and suggested follow-on work.

## Dependencies

* Repository recommendation research in .copilot-tracking/research/2026-06-28/copilot-instructions-recommendations-research.md
* Microsoft Learn guidance for Copilot instructions, .NET organization, testing, Aspire structure, and documentation governance
* Write access to .github/ and read access to representative source, test, docs, and .copilot-tracking files for validation
* VS Code Copilot chat request references as the standard evidence path for applicability checks, using one representative request per scoped instruction file

## Success Criteria

* The implementation produces one .github/copilot-instructions.md file and six scoped .github/instructions/*.instructions.md files with clear, non-overlapping primary responsibilities. Traces to: .copilot-tracking/research/2026-06-28/copilot-instructions-implementation-research.md (Lines 53-65).
* Each scoped instruction file uses an applyTo pattern that is intentional, explainable, and validated against representative matching files. Traces to: .copilot-tracking/research/2026-06-28/copilot-instructions-implementation-research.md (Lines 74-85).
* The rollout references Microsoft Learn guidance where it affects instruction structure, architecture guidance, testing philosophy, Aspire runtime roles, or governance. Traces to: .copilot-tracking/research/2026-06-28/copilot-instructions-implementation-research.md (Lines 42-51).
* Each instruction file remains declarative and domain-scoped instead of procedural or style-guide-heavy. Traces to: .copilot-tracking/research/2026-06-28/copilot-instructions-implementation-research.md (Lines 42-45).
* Final validation confirms both structural correctness and actual Copilot applicability by checking for expected instruction references in representative VS Code Copilot request context, rather than stopping at markdown authoring alone. Traces to: .copilot-tracking/research/subagents/2026-06-28/copilot-instructions-microsoft-learn-research.md and .copilot-tracking/details/2026-06-28/copilot-instructions-rollout-details.md (Lines 292-306).
