---
description: 'Standardizes how agent handoff files are authored, named, and stored under each work-package handoffs folder so every sub-agent receives unambiguous instructions and reports findings in a consistent, traceable format.'
applyTo: 'docs/**/*.md'
---

# Agent handoff instructions

## Overview

These instructions define how to create, name, and complete agent handoff files. They apply to any contributor or orchestrating agent that needs to delegate a work item or task to a sub-agent. Handoff files give sub-agents a self-contained brief and provide a structured place for the sub-agent to report back, keeping delivery auditable and resumable.

## Scope

Applies to: `docs/**/*.md`

- These rules cover handoff file creation, naming, placement, structure, and the agent-report section that sub-agents must complete.
- They complement the work-package rules in `work-packages.instructions.md`; when both apply, prefer the more specific scope.

## Instructions

### MUST

- Create a `handoffs/` subfolder inside the relevant `./docs/00x-work/` folder before writing the first handoff for that work package.
- Store every handoff file inside that `handoffs/` subfolder — never at the work-package root or elsewhere in the repo.
- Name each handoff file using the pattern: `wi{N}-{short-kebab-description}-{agent-role}.md`
  - `{N}` is the work item number (no zero-padding for single-digit items).
  - `{short-kebab-description}` is a concise, lowercase, hyphen-separated summary of the task (for example `task1`, `broker-auth`, `proof-data-display`).
  - `{agent-role}` is the target agent's role in lowercase kebab-case (for example `documentation-specialist`, `broker-auth-integration-agent`, `frontend-blazor-agent`).
  - Examples: `wi1-task1-documentation-specialist.md`, `wi2-broker-auth-integration-agent.md`, `wi3-proof-data-display-frontend-agent.md`
- Base every new handoff file on `.github/templates/handoff.template.md` — replace all placeholder text; do not leave template comments in the final file.
- Include all of the following sections in every handoff file, in this order:
  1. `## Target agent` — the name of the agent role receiving this handoff
  2. `## Work item reference` — the delivery plan path and work item / task scope
  3. `## Delivery context` — branch name, build/test baseline, and any prerequisite work items
  4. `## Scope boundaries — read carefully` — explicit in-scope and out-of-scope lists
  5. `## Key files to read before starting` — table of relevant files with a "what to note" column
  6. `## Deliverables` — one numbered H3 section per deliverable; each must be specific enough to act on without further research
  7. `## Validation gates` — the exact `dotnet build` and filtered `dotnet test` commands plus any handoff-specific confirmation checks
  8. `## Assumptions to validate` — items the agent must verify before coding
  9. `## Agent report` — the structured report section the sub-agent fills in on completion (see below)
- Make deliverable sections specific: include exact file paths, interface and class names, method signatures, and verbatim code blocks wherever the shape of the output matters.
- State explicit out-of-scope exclusions for every file, component, or behavior the agent must not touch.
- In `## Validation gates`, always include both the build gate and the unit-test-only filter gate:
  - `dotnet build` must succeed with zero errors.
  - `dotnet test --filter "FullyQualifiedName!~IntegrationTests&FullyQualifiedName!~E2ETests&FullyQualifiedName!~FunctionalTests"` must pass, including all newly added tests.
- When the sub-agent finishes work, it MUST complete every subsection of `## Agent report` before the handoff is considered done:
  - `### Files created` — list every new file path
  - `### Files modified` — list every modified file path
  - `### Unit tests added` — count and test class table
  - `### Build gate outcome` — literal outcome of `dotnet build`
  - `### Test gate outcome` — literal outcome of the filtered `dotnet test`
  - `### Assumptions validated` — confirm or refute each assumption from the handoff
  - `### Deviations from handoff` — any divergence and rationale; write "None" if the implementation matched exactly
  - `### Escalations` — blocking issues for the orchestrator; write "None. Ready for next handoff." when clear
- After a sub-agent completes the `## Agent report`, the orchestrating agent MUST update the corresponding work item checkboxes in the delivery plan before issuing the next handoff.

### SHOULD

- Add a "what to note" annotation to every row in the `## Key files to read before starting` table so the agent knows exactly what to look for in each file.
- Include verbatim "replace this block" and "replace with this block" code snippets when the handoff requires replacing existing code, so there is no ambiguity about the target.
- Keep the `## Delivery context` baseline current: record the actual unit test count at the time the handoff is written so the agent can confirm the baseline before starting.
- List prerequisites (earlier work items or tasks that must be complete) explicitly in `## Delivery context` even when they seem obvious.
- Cross-reference requirement IDs (for example `FR1`, `SR2`, `NF3`) in deliverable descriptions and test requirement-traceability comments so changes stay traceable to the work-package requirements.

### MUST NOT

- MUST NOT store a handoff file outside the `handoffs/` subfolder of the relevant `./docs/00x-work/` folder.
- MUST NOT leave template placeholder text (text wrapped in `{}` or `<!-- … -->` comment blocks) in a final handoff file.
- MUST NOT omit the `## Agent report` section — even if a handoff is being pre-authored before the agent runs, the section must be present (with placeholder text) so the sub-agent knows where to fill in results.
- MUST NOT issue the next handoff until the previous handoff's `## Agent report` is complete and the delivery plan checkboxes have been updated.
- MUST NOT include implementation details that belong to a later work item in the current handoff's deliverables or scope.

## Output and Validation (optional)

- Expected artifacts: one `*.md` file per handoff, inside `docs/{00x-work}/handoffs/`, named according to the `wi{N}-{description}-{role}.md` pattern.
- Validate by confirming:
  - The file is inside `docs/{00x-work}/handoffs/` (not at the root or in `plans/`).
  - The file name matches `wi{N}-{short-kebab-description}-{agent-role}.md`.
  - All nine required sections are present and in the prescribed order.
  - No template placeholder text (`{}` or `<!-- … -->` markers) remains.
  - The `## Agent report` section is present (may be unfilled before agent execution).

## References (optional)

- `.github/templates/handoff.template.md` — the canonical scaffold for all handoff files
- `.github/instructions/work-packages.instructions.md` — work-package folder and naming rules
- `.github/instructions/docs.instructions.md` — general Markdown authoring conventions

## Notes (optional)

- The `handoffs/` subfolder is distinct from the `plans/` subfolder: plans describe what to build; handoffs describe how to delegate individual slices of that work to specific agents and capture the results.
- When a work item is split across multiple agents or tasks, create one handoff file per agent/task slice rather than combining them into a single file.
