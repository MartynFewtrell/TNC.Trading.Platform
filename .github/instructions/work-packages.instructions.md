---
description: 'Standardize iterative work documentation under `./docs/00x-work/` so each increment has requirements, a technical specification, and one or more numbered plan files under a plans subfolder.'
applyTo: 'docs/**/*.md'
---

# Work package documentation instructions

## Overview

These instructions define how to document each incremental unit of work so it can be delivered iteratively and reviewed consistently. They are for contributors creating or updating work items under `./docs/`.

## Scope

These rules apply when adding or updating documentation for a unit of work. They are primarily concerned with content under `./docs/00x-work/`.

## Instructions

### MUST

- You MUST maintain a project-level business requirements document at `./docs/business-requirements.md`.
  - This document defines the business context, desired outcomes, and high-level requirements for the overall initiative.
  - It is the foundation upon which systems analysis and work packages are defined.

- You MUST NOT draft a new `./docs/00x-work/` work package unless the user or task explicitly requests one.

- You MUST maintain a project-level systems analysis document at `./docs/systems-analysis.md` before commencing any work packages.
  - This document refines the business requirements into system boundary/context, use cases, business rules, analysis-level requirements, and quality attributes.
  - It must remain implementation-agnostic and must not replace per-work-package requirements/specification/plan documents.
- You MUST create a dedicated folder under `./docs/` for each unit of work, named `00x-work` where `00x` is a zero-padded sequence number (e.g. `001`) and `work` is a brief description of the task (for example: `001-add-order-endpoint`).
- You MUST include a `requirements.md` in each `./docs/00x-work/` folder.
- You MUST ensure each work package `requirements.md` aligns with and links to `../business-requirements.md`.
- You MUST produce a technical specification from the requirements and store it as `technical-specification.md` in the same `./docs/00x-work/` folder.
- You MUST create a `plans/` subfolder inside each `./docs/00x-work/` folder when that work package needs one or more plans.
- You MUST create the initial delivery plan based on both the requirements and technical specification and store it as `plans/001-delivery-plan.md` in the same work-package folder.
- You MUST store any additional work-package plan documents in that same `plans/` subfolder and name them with the next zero-padded sequence prefix (for example `plans/002-some-refactor-plan.md`).
- You MUST treat `requirements.md`, `technical-specification.md`, and `plans/001-delivery-plan.md` as the baseline document set for every work package, including refactoring-focused work packages.
- You MUST use the baseline document set to define scope, traceability, constraints, and the initial delivery posture before creating refactoring-specific review or mitigation artifacts.
- When a work package is primarily about refactoring existing implementation, you MUST treat the refactoring workflow artifacts as first-class companion documents rather than ad hoc extras.
  - The review stage output belongs in the work-package root as a numbered `NNN-work-package-refactoring-review-report.md` file.
  - The mitigation planning stage output belongs in the `plans/` folder as a numbered `NNN-work-package-refactoring-mitigation-plan.md` file.
  - The implementation stage may create a sibling execution log in the `plans/` folder using the mitigation-plan stem plus `-execution-log.md`.
- You MUST require a refactoring review report before creating a refactoring mitigation plan when the work package depends on evidence-backed refactoring findings rather than only the initial delivery plan.
- You MUST distinguish planning artifacts from evidence-and-mitigation artifacts in refactoring work packages.
  - Planning artifacts: `requirements.md`, `technical-specification.md`, `plans/001-delivery-plan.md`
  - Evidence and mitigation artifacts: numbered refactoring review reports, numbered refactoring mitigation plans, execution logs, and similar review outputs
- Work package `requirements.md` documents MUST remain implementation-agnostic.
  - Refer to a data store for configuration rather than naming SQL Server directly in requirements-level documents.
- You MUST keep the `00x` number monotonically increasing (do not reuse a prior number for a different work item).
- You MUST keep each work item's documentation self-contained within its `./docs/00x-work/` folder and its `plans/` subfolder.
- You MUST update the relevant documentation under `./docs/wiki/` before marking any numbered plan complete when the implemented behavior, architecture, API surface, runtime behavior, operator workflow, local development guidance, or testing approach has changed.
- When asked to review a work package, you MUST create the review report as a physical markdown file within that work package, not only as chat output.
- When creating a delivery plan from refactoring advice, you MUST scope the plan to that refactor objective rather than to existing work-package docs unless explicitly instructed to reuse them.

### Refactoring-specific package profile

Use this profile when the primary goal of the work package is to improve maintainability, structure, cohesion, change safety, or testability of existing implementation while preserving intended behavior.

#### MUST

- Refactoring-specific work packages MUST still include the baseline document set.
- Refactoring-specific `requirements.md` files MUST make the behavior-preservation boundary explicit.
- Refactoring-specific `requirements.md` files MUST identify the target implementation surfaces and the out-of-scope areas that constrain cleanup sprawl.
- Refactoring-specific `technical-specification.md` files MUST describe the current structural problem, the intended owning boundaries, and the incremental slice strategy used to keep changes safe.
- Refactoring-specific `plans/001-delivery-plan.md` files MUST describe the initial package sequencing and MAY be followed by a refactoring review report and one or more mitigation plans when implementation should be driven by evidence from the current codebase.
- When a refactoring review report exists, the actionable implementation plan SHOULD move into a numbered refactoring mitigation plan rather than continuing to overload the initial delivery plan.

#### SHOULD

- Refactoring-specific work packages SHOULD make maintainability, testability, and regression-safety requirements first-class rather than treating them as implicit concerns.
- Refactoring-specific work packages SHOULD prefer review-driven mitigation plans when the implementation scope depends on findings that must be confirmed from the current codebase.
- Refactoring-specific work packages SHOULD keep the initial delivery plan high-level and use mitigation plans for evidence-backed implementation detail.

### SHOULD

- You SHOULD keep titles, headings, and filenames consistent across work items to make diffs and reviews predictable.
- You SHOULD keep the `work` portion of the folder name short but descriptive.
- You SHOULD ensure each work package `requirements.md` references relevant items from `../systems-analysis.md` (for example `UCx`, `SARx`, `NFRx`) where it helps traceability.

### MUST NOT

- You MUST NOT store requirements/spec/plan for a unit of work outside its `./docs/00x-work/` folder.
- You MUST NOT store work-package plan files at the root of a `./docs/00x-work/` folder once the `plans/` subfolder convention applies.
- You MUST NOT combine multiple unrelated units of work into a single `00x-work` folder.
- You MUST NOT place project-level business requirements inside a `./docs/00x-work/` folder.
- You MUST NOT place project-level systems analysis inside a `./docs/00x-work/` folder.

## Output and Validation (optional)

- Expected artifacts: project-level `./docs/business-requirements.md` and `./docs/systems-analysis.md` plus one or more `./docs/00x-work/` folders containing `requirements.md`, `technical-specification.md`, and one or more numbered plan files under `plans/`.
  - refactoring-focused work packages place review reports in the work-package root and mitigation plans in `plans/`.
  - optional execution logs live in `plans/` next to the mitigation plan they support.
- Validate success by confirming that:
  - `./docs/business-requirements.md` exists at the project level.
  - `./docs/systems-analysis.md` exists at the project level before any new `./docs/00x-work/` packages are created.
  - each `./docs/00x-work/requirements.md` file links to `../business-requirements.md` and aligns with the project-level requirements.
  - each `./docs/00x-work/` folder exists, is correctly numbered/named, and contains the required documents.
  - each work-package `plans/` subfolder contains numbered plan files in the intended sequence starting with `001-delivery-plan.md`.
  - refactoring-focused work packages place review reports in the work-package root and mitigation plans in `plans/`.
  - any completed plan that changed the implemented solution also updated the affected `./docs/wiki/` pages and kept their links working.

## Notes (optional)

- This file enforces the repo’s iterative delivery documentation workflow.

- For rules governing handoff files (the `handoffs/` subfolder, naming, required sections, and agent-report format), see `.github/instructions/handoffs.instructions.md`.
