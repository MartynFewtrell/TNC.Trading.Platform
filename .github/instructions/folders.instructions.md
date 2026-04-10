---
description: 'Enforces a consistent repo folder structure so new files land in the correct area and structure drift is avoided.'
applyTo: '**/*'
---

# Folder structure rules

## Overview

These instructions keep the repository layout consistent by ensuring new files and new projects are added in the right place, and existing boundaries are not eroded over time.

## Scope

- These rules apply when creating new files or folders, moving files or folders, or introducing new projects.

## Instructions

### MUST

- Preserve the current top-level folder layout; do not introduce new top-level folders without an explicit architectural reason.
- When adding or reorganizing code, prefer the following top-level folders for this repository: `.github/`, `docs/`, `infra/`, `src/`, and `test/`. If some of these folders do not yet exist, create them as needed rather than introducing alternative top-level roots.
- Prefer placing product code under `src/` and automated tests under `test/` (or the closest existing equivalent), creating those folders when necessary.
- When moving files, update all references to preserve build/test viability (solution/project references, namespaces/imports, tooling paths).

- Create Copilot instruction files (`*.instructions.md`) under `./.github/instructions/`.
- Create Copilot prompt files (`*.prompt.md`) under `./.github/prompts/`.
- Create template markdown files (`*.template.md` and `*.templates.md`) under `./.github/templates/`.
- Verify the target folder exists (or locate it) before creating any new `*.instructions.md` or `*.prompt.md` file.

### SHOULD

- Mirror production structure in tests (or follow the established test layout) so mapping between code and tests is obvious.
- Keep shared utilities in the established shared/common area rather than duplicating per feature.
- Keep configuration files close to the component they configure unless the repository already centralizes that config type.

### MUST NOT

- MUST NOT create parallel duplicate roots (for example, a second `Services`, `Core`, or `Common` root) when an established location exists.
- MUST NOT move files across layers without maintaining boundaries and updating references.
- MUST NOT add new projects/solutions that bypass the repository's existing build/test orchestration.

- MUST NOT create `*.instructions.md` files outside `./.github/instructions/`.
- MUST NOT create `*.prompt.md` files outside `./.github/prompts/`.
- MUST NOT create `*.template.md` or `*.templates.md` files outside `./.github/templates/`.

## Output and Validation (optional)

- Folder changes MUST be accompanied by updated references.
- Validate by building the relevant solution/projects and ensuring tests still run in CI.

## References (optional)

- None