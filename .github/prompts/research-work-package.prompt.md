---
agent: 'Work Package Researcher'
description: 'Researches a proposed work package against project-level documents and repository evidence, then writes one authoritative research artifact for the package.'
name: research-work-package
model: 'gpt-5.4'
# tags: [work-package, research, docs]
---

# Research a Work Package

## Purpose

Use the dedicated `Work Package Researcher` agent to perform the HVE-style research phase for a proposed work package.

This stage narrows uncertainty before package-level requirements and design are written. It creates one physical research artifact in the target work-package folder and stops before requirements authoring or implementation begins.

## When to use

- You have a proposed work package and want evidence-backed research before drafting `requirements.md`.
- You want to inspect existing code, tests, documentation, and repository conventions so the package scope is grounded in the current implementation.
- You want one authoritative recommended direction rather than multiple speculative package definitions.

## Inputs

### Required

- The target work-package folder under `./docs/00x-work/`, or a bootstrap result that identifies that folder.

### Optional

- The initial package idea if the work-package folder is not yet descriptive enough.
- Specific risk areas or code surfaces to emphasize.
- Preferred research depth (`quick`, `standard`, or `deep`).
- A preferred output path if it does not overwrite an existing research artifact.

## Configuration variables (optional)

${RESEARCH_DEPTH="standard"} <!-- quick | standard | deep: controls how much repository evidence to collect before concluding -->

## Constraints

- MUST: Route the task through the `Work Package Researcher` agent.
- MUST: Use `.github/templates/work-package-research.template.md` as the output scaffold.
- MUST: Review `./docs/business-requirements.md` and `./docs/systems-analysis.md`.
- MUST: Create a physical research artifact in the target work-package folder before returning the final answer.
- MUST: Use the next available numbered file name such as `001-work-package-research.md`.
- MUST: Produce one recommended direction for the package.
- SHOULD: Prefer the smallest repository scan that still supports an evidence-backed recommendation.
- MUST NOT: Replace `requirements.md`, `technical-specification.md`, or `plans/001-delivery-plan.md` in this stage.
- MUST NOT: Start implementation changes.

## Process

1. Route the task through the `Work Package Researcher` agent.
2. Read the project-level prerequisites and the target work-package context.
3. Inspect the most relevant repository files needed to confirm scope, likely touch points, and validation anchors.
4. Write a numbered research artifact in the target work-package folder.
5. Return a concise summary and recommend the next workflow step.

## Output format

Return a single markdown report that follows `.github/templates/work-package-research.template.md`.

Also create the physical numbered research artifact inside the target work-package folder.

## Examples (optional)

### Example request

Research the proposed work package in `./docs/007-improve-runtime-configuration/` before requirements are drafted.

### Example response (optional)

A numbered research artifact grounded in repository evidence, with one recommended package direction and suggested next steps.