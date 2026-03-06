---
description: 'Standardize iterative work documentation under `./docs/00x-work/` so each increment has requirements, a technical specification, and a delivery plan.'
applyTo: 'docs/**/*.md'
---

# Iterative Work Documentation Instructions

## Overview

These instructions define how to document each incremental unit of work so it can be delivered iteratively and reviewed consistently. They are for contributors creating or updating work items under `./docs/`.

## Scope

These rules apply when adding or updating documentation for a unit of work. They are primarily concerned with content under `./docs/00x-work/`.

## Instructions

### MUST

- You MUST maintain a project-level business requirements document at `./docs/business-requirements.md`.
  - This document defines the business context, desired outcomes, and high-level requirements for the overall initiative.
  - It is the foundation upon which work packages are defined.
- You MUST create a dedicated folder under `./docs/` for each unit of work, named `00x-work` where `00x` is a zero-padded sequence number (e.g. `001`) and `work` is a brief description of the task (for example: `001-add-order-endpoint`).
- You MUST include a `requirements.md` in each `./docs/00x-work/` folder.
- You MUST ensure each work package `requirements.md` aligns with and links to `../business-requirements.md`.
- You MUST produce a technical specification from the requirements and store it as `technical-specification.md` in the same `./docs/00x-work/` folder.
- You MUST create a delivery plan based on both the requirements and technical specification and store it as `delivery-plan.md` in the same `./docs/00x-work/` folder.
- You MUST keep the `00x` number monotonically increasing (do not reuse a prior number for a different work item).
- You MUST keep each work item's documentation self-contained within its `./docs/00x-work/` folder.

### SHOULD

- You SHOULD keep titles, headings, and filenames consistent across work items to make diffs and reviews predictable.
- You SHOULD keep the `work` portion of the folder name short but descriptive.

### MUST NOT

- You MUST NOT store requirements/spec/plan for a unit of work outside its `./docs/00x-work/` folder.
- You MUST NOT combine multiple unrelated units of work into a single `00x-work` folder.
- You MUST NOT place project-level business requirements inside a `./docs/00x-work/` folder.

## Output and Validation (optional)

- Expected artifacts: a project-level `./docs/business-requirements.md` plus one or more `./docs/00x-work/` folders containing `requirements.md`, `technical-specification.md`, and `delivery-plan.md`.
- Validate success by confirming that:
  - `./docs/business-requirements.md` exists at the project level.
  - each `./docs/00x-work/requirements.md` file links to `../business-requirements.md` and aligns with the project-level requirements.
  - each `./docs/00x-work/` folder exists, is correctly numbered/named, and contains the required documents.

## Notes (optional)

- This file enforces the repo’s iterative delivery documentation workflow.
