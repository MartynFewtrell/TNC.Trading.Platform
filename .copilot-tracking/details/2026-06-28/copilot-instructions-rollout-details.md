---
title: Copilot Instructions Rollout Details
description: Step-by-step implementation details for adding the first repository-local Copilot instruction set under .github.
author: GitHub Copilot
ms.date: 2026-06-28
ms.topic: how-to
keywords:
  - github copilot
  - custom instructions
  - dotnet
  - aspire
  - planning
estimated_reading_time: 8
---
<!-- markdownlint-disable-file -->

## Context Reference

Sources: .copilot-tracking/research/2026-06-28/copilot-instructions-recommendations-research.md, .copilot-tracking/research/2026-06-28/copilot-instructions-implementation-research.md, .copilot-tracking/research/subagents/2026-06-28/copilot-instructions-microsoft-learn-research.md, README.md, docs/wiki/architecture.md, docs/wiki/local-development.md, docs/wiki/testing-and-quality.md, .editorconfig

## Implementation Phase 1: Define the instruction system skeleton and ownership rules

<!-- parallelizable: false -->

### Step 1.1: Confirm the seven-file baseline and map each file to a single responsibility

Use the existing repository research as the source of truth for the initial file list, then translate that recommendation into an implementation inventory with one clear job per file. Keep the root bootstrap file short and reserve repository-specific operational or architectural rules for scoped files.

Files:
* .github/copilot-instructions.md - repository-wide bootstrap and non-negotiable rules
* .github/instructions/architecture-boundaries.instructions.md - layer responsibilities and dependency direction
* .github/instructions/apphost-runtime.instructions.md - AppHost runtime and supported local workflow guidance
* .github/instructions/dotnet-validation.instructions.md - validation command selection and cadence
* .github/instructions/docs-sync.instructions.md - documentation update triggers and source-of-truth mapping
* .github/instructions/testing-strategy.instructions.md - test pyramid and cost-aware validation behavior
* .github/instructions/refactoring-workflow.instructions.md - when to use research, planning, and bounded rollout workflow

Success criteria:
* Each planned instruction file has a unique primary responsibility
* The root file is explicitly limited to universal repository guidance
* No per-project or speculative extra instruction files are introduced in the first rollout

Context references:
* .copilot-tracking/research/2026-06-28/copilot-instructions-recommendations-research.md (Lines 120-205) - selected instruction set and rationale
* .copilot-tracking/research/2026-06-28/copilot-instructions-implementation-research.md (Lines 27-56) - implementation-focused Microsoft Learn alignment

Dependencies:
* Existing repository recommendation research accepted as the baseline

### Step 1.2: Define scoped applyTo patterns before writing file content

Author the applyTo patterns up front so instruction scope is intentional. Follow Microsoft's layered guidance: use the repo-wide file for rules that apply everywhere and targeted instruction files for domains where narrow matching is explainable and testable.

Planned scope decisions:
* .github/copilot-instructions.md - no applyTo frontmatter because it is the repo-wide bootstrap file
* .github/instructions/architecture-boundaries.instructions.md - src/**/*.cs, test/**/*.cs
* .github/instructions/apphost-runtime.instructions.md - src/TNC.Trading.Platform.AppHost/**, src/TNC.Trading.Platform.ServiceDefaults/**, test/**/AppHost*/**, test/Shared/**, docs/wiki/local-development.md, docs/wiki/runtime-behavior.md
* .github/instructions/dotnet-validation.instructions.md - src/**/*.cs, test/**/*.cs
* .github/instructions/docs-sync.instructions.md - src/**, test/**, README.md, docs/**/*.md
* .github/instructions/testing-strategy.instructions.md - test/**/*.cs, src/**/*.cs
* .github/instructions/refactoring-workflow.instructions.md - .copilot-tracking/**, docs/006-refactor-app/**/*.md, src/**, test/**

Success criteria:
* Every scoped instruction file has an intentionally narrow and testable applyTo value
* The phase records where patterns overlap and why that overlap is acceptable
* AppHost and ServiceDefaults files are included in the runtime guidance scope because Microsoft Aspire guidance treats them as distinct orchestration concerns

Context references:
* .copilot-tracking/research/2026-06-28/copilot-instructions-recommendations-research.md (Lines 89-110) - candidate applyTo coverage
* .copilot-tracking/research/2026-06-28/copilot-instructions-implementation-research.md (Lines 58-69) - layered scoping best practices

Dependencies:
* Step 1.1 completion

### Step 1.3: Define the validation model for the instruction rollout

Set the validation method before implementation starts. Validation should check structure, content quality, and applied behavior. Microsoft guidance explicitly supports verifying that instructions were attached to Copilot requests, so the rollout should not stop at markdown authoring alone.

Validation model:
* Structural validation: verify frontmatter, filenames, and applyTo values for all .instructions.md files
* Content validation: verify each file stays declarative and domain-scoped, contains repository-specific rules, references source-of-truth docs, and avoids generic analyzer-level restatement
* Behavior validation: for each scoped instruction file, open a representative matching file, run a scoped Copilot chat request in VS Code, and confirm the expected instruction file appears in the request references or attached context for that interaction
* Change validation: review the combined .github instruction set for contradictory overlap before merge

Success criteria:
* The implementation sequence includes explicit post-authoring validation work
* The validation model proves both file correctness and actual Copilot applicability through request-reference evidence captured from representative VS Code Copilot interactions

Context references:
* .copilot-tracking/research/2026-06-28/copilot-instructions-implementation-research.md (Lines 44-56) - risk model and recommended validation sequence
* .copilot-tracking/research/subagents/2026-06-28/copilot-instructions-microsoft-learn-research.md (Lines 19-24) - Microsoft guidance to validate applied instructions

Dependencies:
* Step 1.2 completion

## Implementation Phase 2: Author the bootstrap and operational instruction files

<!-- parallelizable: false -->

### Step 2.1: Create .github/copilot-instructions.md as the short repository bootstrap

Author the root bootstrap file as the entry point for all future Copilot sessions in the repository. Keep it concise. It should identify the repo as a .NET 10 Aspire solution, point to docs/wiki as the current-state authority, name AppHost as the supported local composition root, capture the one-top-level-type-per-file rule, and call out the canonical validation commands.

Files:
* .github/copilot-instructions.md - concise repo bootstrap guidance

Success criteria:
* The root file stays short and universal rather than absorbing detailed domain rules
* The root file remains declarative and does not absorb procedural guidance better owned by scoped instruction files
* It points Copilot toward the existing docs instead of duplicating large sections of runtime or testing guidance
* It names the default build and test commands, including the serial dotnet test fallback already used in the repo

Context references:
* .copilot-tracking/research/2026-06-28/copilot-instructions-recommendations-research.md (Lines 134-144) - required bootstrap content
* .copilot-tracking/research/2026-06-28/copilot-instructions-implementation-research.md (Lines 27-38) - Microsoft best practice for repo-wide versus scoped instructions

Dependencies:
* Phase 1 complete

### Step 2.2: Create .github/instructions/dotnet-validation.instructions.md

Author a targeted validation instruction file for C# source and test files. Focus on validation order rather than full testing philosophy: prefer the narrowest relevant build or test command first, escalate to broader validation only when the touched slice requires it, and note the repository's known dotnet test -m:1 fallback for MSBuild child-node instability.

Files:
* .github/instructions/dotnet-validation.instructions.md - validation workflow and command guidance

Discrepancy references:
* None

Success criteria:
* The file gives actionable validation defaults without repeating the entire testing strategy file
* The file stays declarative and domain-scoped by expressing validation expectations and command preferences rather than procedural implementation recipes
* The guidance is compatible with Microsoft .NET testing advice to keep feedback fast and focused

Context references:
* .copilot-tracking/research/2026-06-28/copilot-instructions-recommendations-research.md (Lines 154-157) - repository validation rationale
* .copilot-tracking/research/subagents/2026-06-28/copilot-instructions-microsoft-learn-research.md (Lines 52-60) - fast isolated tests and narrow validation support

Dependencies:
* Step 2.1 completion

### Step 2.3: Create .github/instructions/apphost-runtime.instructions.md

Author the runtime instruction file that preserves the supported local and distributed workflow. Keep AppHost orchestration-focused, treat ServiceDefaults as the shared home for resilience and observability defaults, forbid unsupported synthetic runtime paths, and tell Copilot not to generate root-level runtime capture artifacts during local workflow changes.

Files:
* .github/instructions/apphost-runtime.instructions.md - AppHost and ServiceDefaults runtime rules

Success criteria:
* The file preserves the repository's supported local startup and runtime model
* The file stays declarative and domain-scoped by encoding supported and unsupported runtime patterns instead of step-by-step environment procedures
* It reflects Microsoft Aspire guidance about AppHost and ServiceDefaults responsibilities
* It clearly distinguishes orchestration rules from general architecture rules

Context references:
* .copilot-tracking/research/2026-06-28/copilot-instructions-recommendations-research.md (Lines 149-153) - repository runtime constraints
* .copilot-tracking/research/subagents/2026-06-28/copilot-instructions-microsoft-learn-research.md (Lines 63-72) - Microsoft Aspire guidance for AppHost and ServiceDefaults

Dependencies:
* Step 2.1 completion

### Step 2.4: Validate the bootstrap and operational files together

Review the three authored files as a set. Confirm that the root file stays universal, the validation file owns command and validation-sequencing behavior, and the runtime file owns orchestration and local-environment behavior.

Validation commands:
* Manual frontmatter and scope review for .github/instructions/dotnet-validation.instructions.md and .github/instructions/apphost-runtime.instructions.md
* Representative Copilot applicability checks using one C# file and one AppHost file, with acceptance based on expected instruction references appearing in the VS Code Copilot request context

Success criteria:
* No repository-wide rule is duplicated across all three files without need
* Representative files show the expected instruction references in context

Dependencies:
* Steps 2.2 and 2.3 completion

## Implementation Phase 3: Author architecture and testing instruction files

<!-- parallelizable: true -->

### Step 3.1: Create .github/instructions/architecture-boundaries.instructions.md

Encode the stable layer responsibilities already documented in the wiki. The file should steer business orchestration into Application, infrastructure and persistence into Infrastructure, thin request translation into Api, UI and page orchestration into Web, and environment wiring into AppHost and ServiceDefaults. It should also discourage unnecessary new cross-project references.

Files:
* .github/instructions/architecture-boundaries.instructions.md - project boundary and dependency direction guidance

Success criteria:
* The file expresses responsibilities and forbidden drift patterns clearly
* The file stays declarative and domain-scoped by describing allowed responsibilities and dependency direction without expanding into general style rules
* It uses repository terminology and aligns with Microsoft guidance about separation of concerns and dependency control
* It avoids turning into a generic C# style guide

Context references:
* .copilot-tracking/research/2026-06-28/copilot-instructions-recommendations-research.md (Lines 145-148) - repository architectural rationale
* .copilot-tracking/research/subagents/2026-06-28/copilot-instructions-microsoft-learn-research.md (Lines 30-48) - Microsoft program organization guidance

Dependencies:
* Phase 2 complete

### Step 3.2: Create .github/instructions/testing-strategy.instructions.md

Encode the test pyramid and cost-aware coverage model. Tell Copilot to prefer cheap unit and focused functional checks where they can prove behavior, preserve broad AppHost-backed suites as milestone or contract coverage, and avoid expanding expensive integration coverage when lower-cost seams would work.

Files:
* .github/instructions/testing-strategy.instructions.md - testing philosophy and test-selection guidance

Success criteria:
* The file captures the repository's quality model instead of generic testing advice
* The file stays declarative and domain-scoped by stating test-selection and coverage-shape expectations rather than procedural test-authoring scripts
* It aligns with Microsoft guidance to keep unit tests fast and isolated and to reserve integration tests for infrastructure-significant scenarios
* It complements rather than duplicates the dotnet-validation file

Context references:
* .copilot-tracking/research/2026-06-28/copilot-instructions-recommendations-research.md (Lines 161-164) - repository test strategy rationale
* .copilot-tracking/research/subagents/2026-06-28/copilot-instructions-microsoft-learn-research.md (Lines 52-60) - Microsoft testing best practices

Dependencies:
* Phase 2 complete

### Step 3.3: Validate architecture and testing instructions against representative files

Use at least one application-layer file, one infrastructure file, and one test project file to confirm that the intended instructions would apply cleanly and reinforce each other without conflict.

Validation commands:
* Manual scope review for .github/instructions/architecture-boundaries.instructions.md and .github/instructions/testing-strategy.instructions.md
* Representative Copilot applicability checks using src/TNC.Trading.Platform.Application/**, src/TNC.Trading.Platform.Infrastructure/**, and test/**/*.cs files, with acceptance based on expected instruction references appearing in the VS Code Copilot request context

Success criteria:
* The architecture file does not leak into generic test-writing concerns
* The testing file reinforces cheaper validation and test-type boundaries without restating architecture rules

Dependencies:
* Steps 3.1 and 3.2 completion

## Implementation Phase 4: Author documentation and workflow instruction files

<!-- parallelizable: true -->

### Step 4.1: Create .github/instructions/docs-sync.instructions.md

Author the docs-sync file so Copilot updates the repository's human-facing guidance when code changes alter runtime behavior, local-development setup, testing expectations, operator flows, or public API shape. The file should point back to README.md and docs/wiki as the source-of-truth map.

Files:
* .github/instructions/docs-sync.instructions.md - documentation update triggers and target-page mapping

Success criteria:
* The file names the highest-value docs that must stay aligned with code
* The file stays declarative and domain-scoped by defining documentation update triggers and target documents rather than procedural documentation workflows
* It teaches Copilot to update current-state docs when behavior changes instead of leaving documentation drift behind
* It uses repository documents as anchors rather than creating speculative new docs requirements

Context references:
* .copilot-tracking/research/2026-06-28/copilot-instructions-recommendations-research.md (Lines 158-160) - repository docs-sync need
* .copilot-tracking/research/subagents/2026-06-28/copilot-instructions-microsoft-learn-research.md (Lines 74-85) - Microsoft guidance for open repository documentation and ADR-style rationale

Dependencies:
* Phase 3 complete

### Step 4.2: Create .github/instructions/refactoring-workflow.instructions.md

Author the workflow file that tells Copilot when to shift from direct edits to research, planning, review, and bounded implementation slices. Use the repository's existing .copilot-tracking practice as the baseline and keep the guidance focused on larger or riskier changes rather than every small edit.

Files:
* .github/instructions/refactoring-workflow.instructions.md - larger-change workflow guidance

Success criteria:
* The file distinguishes lightweight tasks from research-and-plan-backed work
* The file stays declarative and domain-scoped by defining decision thresholds for planning and review rather than prescribing a universal step-by-step process for every edit
* It reinforces the repository's existing planning artifacts without making them mandatory for trivial edits
* It stays specific to this repository's workflow rather than becoming a generic planning manifesto

Context references:
* .copilot-tracking/research/2026-06-28/copilot-instructions-recommendations-research.md (Lines 165-168) - refactoring workflow rationale
* .copilot-tracking/research/subagents/2026-06-28/copilot-instructions-microsoft-learn-research.md (Lines 74-85) - Microsoft documentation and governance support

Dependencies:
* Phase 3 complete

### Step 4.3: Validate documentation and workflow instructions together

Review the last two files as a set. Confirm that docs-sync focuses on change completeness and source-of-truth updates, while refactoring-workflow focuses on decision thresholds for research and planning.

Validation commands:
* Manual scope review for .github/instructions/docs-sync.instructions.md and .github/instructions/refactoring-workflow.instructions.md
* Representative Copilot applicability checks using docs/**/*.md, src/**/*.cs, test/**/*.cs, and .copilot-tracking/** files, with acceptance based on expected instruction references appearing in the VS Code Copilot request context

Success criteria:
* The two files complement each other without duplicating responsibilities
* Representative files show the intended instruction references for docs and planning-related work

Dependencies:
* Steps 4.1 and 4.2 completion

## Implementation Phase 5: Final validation and rollout handoff

<!-- parallelizable: false -->

### Step 5.1: Run the full instruction-set validation pass

Review the complete .github instruction set for consistency, overlap, and maintenance quality. This is the point where the team confirms the rollout matches both repository evidence and Microsoft guidance.

Validation commands:
* Manual frontmatter and filename review for all .github/instructions/*.instructions.md files
* Combined overlap review across .github/copilot-instructions.md and all scoped instruction files
* Representative Copilot applicability checks for each scoped file using at least one matching file path per instruction, with acceptance based on expected instruction references appearing in the VS Code Copilot request context

Success criteria:
* All instruction files have clear scope boundaries and no contradictory rules
* All instruction files remain declarative and domain-scoped, consistent with Microsoft custom-instruction best practices
* The full set remains compact enough to maintain as a first repository-local baseline

Dependencies:
* Phase 4 complete

### Step 5.2: Record rollout rationale in repository-facing documentation if the implementation introduces governance notes

If the implementation chooses to add a short note explaining how to maintain the instruction set, keep that note aligned with Microsoft guidance for open repository documentation and durable decision rationale. The note can live in an existing repo doc rather than introducing a large governance document.

Files:
* README.md - optional brief note if maintainers need a discoverable entry point
* docs/wiki/architecture.md - optional rationale touchpoint if architecture-boundary governance needs visibility
* docs/wiki/local-development.md - optional note if local workflow guidance changes materially

Discrepancy references:
* DD-01

Success criteria:
* Supporting documentation remains minimal and only changes when the rollout actually adds maintainership guidance
* Governance rationale stays close to the existing repository documentation set

Context references:
* .copilot-tracking/research/subagents/2026-06-28/copilot-instructions-microsoft-learn-research.md (Lines 74-85) - ADR and open documentation guidance

Dependencies:
* Step 5.1 completion

### Step 5.3: Prepare the follow-on backlog based on implementation friction

Close the rollout by recording whether any missing scopes or repeated conflicts suggest future instruction files. Do not expand the initial file set unless the first rollout proves a real gap.

Success criteria:
* Any future candidates such as a ServiceDefaults-specific or frontend-component-specific instruction file are captured as follow-on work rather than added speculatively
* The initial rollout remains the compact seven-file baseline recommended by the research

Dependencies:
* Step 5.2 completion

## Dependencies

* Existing repository research on Copilot instruction recommendations
* Microsoft Learn guidance on Copilot custom instructions, .NET organization, testing, Aspire structure, and architecture documentation
* Access to representative source, test, docs, and .copilot-tracking files for scope validation

## Success Criteria

* The implementation plan sequences creation of .github/copilot-instructions.md and six scoped .github/instructions/*.instructions.md files with non-overlapping primary responsibilities.
* The plan uses Microsoft Learn guidance wherever it materially affects file structure, instruction scoping, validation, or governance.
* The rollout ends with explicit validation that the authored instructions are structurally correct and actually apply to representative matching files.
