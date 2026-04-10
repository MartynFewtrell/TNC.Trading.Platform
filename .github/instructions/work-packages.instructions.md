---
description: 'Standardize iterative work documentation under `./docs/00x-work/` so each increment has requirements, a technical specification, and a delivery plan.'
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
- You MUST create a delivery plan based on both the requirements and technical specification and store it as `delivery-plan.md` in the same `./docs/00x-work/` folder.
- Work package `requirements.md` documents MUST remain implementation-agnostic.
  - Refer to a data store for configuration rather than naming SQL Server directly in requirements-level documents.
- You MUST keep the `00x` number monotonically increasing (do not reuse a prior number for a different work item).
- You MUST keep each work item's documentation self-contained within its `./docs/00x-work/` folder.
- When asked to review a work package, you MUST create the review report as a physical markdown file within that work package, not only as chat output.
- When creating a delivery plan from refactoring advice, you MUST scope the plan to that refactor objective rather than to existing work-package docs unless explicitly instructed to reuse them.

### SHOULD

- You SHOULD keep titles, headings, and filenames consistent across work items to make diffs and reviews predictable.
- You SHOULD keep the `work` portion of the folder name short but descriptive.
- You SHOULD ensure each work package `requirements.md` references relevant items from `../systems-analysis.md` (for example `UCx`, `SARx`, `NFRx`) where it helps traceability.

### MUST NOT

- You MUST NOT store requirements/spec/plan for a unit of work outside its `./docs/00x-work/` folder.
- You MUST NOT combine multiple unrelated units of work into a single `00x-work` folder.
- You MUST NOT place project-level business requirements inside a `./docs/00x-work/` folder.
- You MUST NOT place project-level systems analysis inside a `./docs/00x-work/` folder.

## Output and Validation (optional)

- Expected artifacts: project-level `./docs/business-requirements.md` and `./docs/systems-analysis.md` plus one or more `./docs/00x-work/` folders containing `requirements.md`, `technical-specification.md`, and `delivery-plan.md`.
- Validate success by confirming that:
  - `./docs/business-requirements.md` exists at the project level.
  - `./docs/systems-analysis.md` exists at the project level before any new `./docs/00x-work/` packages are created.
  - each `./docs/00x-work/requirements.md` file links to `../business-requirements.md` and aligns with the project-level requirements.
  - each `./docs/00x-work/` folder exists, is correctly numbered/named, and contains the required documents.

## Notes (optional)

- This file enforces the repo’s iterative delivery documentation workflow.
