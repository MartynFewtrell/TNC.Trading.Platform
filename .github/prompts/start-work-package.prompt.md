---
agent: 'Work Package Bootstrapper'
description: 'Starts a new repo-native work package by checking project prerequisites, recommending the target docs folder, and routing the operator into the correct next stage.'
name: start-work-package
model: 'gpt-5.4'
# tags: [work-package, bootstrap, workflow]
---

# Start a New Work Package

## Purpose

Use the dedicated `Work Package Bootstrapper` agent to start a new work package in this repository.

This entry point verifies the project-level prerequisites, recommends the correct `./docs/00x-work/` folder, and prepares the next workflow stage without replacing the repository's existing artifact generators.

## When to use

- You have a new work-package idea and want one repo-native entry point instead of manually deciding which prompt to run first.
- You want to confirm that `./docs/business-requirements.md` and `./docs/systems-analysis.md` are in place before defining package-level artifacts.
- You want help selecting the next work-package folder name under `./docs/`.

## Inputs

### Required

- The user's initial idea for the work package.

### Optional

- A preferred package name or slug.
- A preferred target package number if the work item is already allocated.
- Candidate references from `./docs/systems-analysis.md` such as `UCx`, `SARx`, or `NFRx`.
- Whether the operator wants to continue into research immediately after bootstrap.

## Constraints

- MUST: Route the task through the `Work Package Bootstrapper` agent.
- MUST: Verify `./docs/business-requirements.md` and `./docs/systems-analysis.md` before proceeding.
- MUST: Preserve the established work-package artifact contract of `requirements.md`, `technical-specification.md`, and `plans/001-delivery-plan.md`.
- MUST: Recommend the next correct `./docs/00x-work/` folder when a new work package is being created.
- MUST: Stop before implementation begins.
- SHOULD: Recommend the research stage for non-trivial work packages.
- MUST NOT: Generate replacement baseline artifacts in this stage.

## Process

1. Route the request through the `Work Package Bootstrapper` agent.
2. Verify project-level prerequisite documents and existing work-package numbering.
3. Produce a concise bootstrap brief with the proposed work-package folder and recommended next stage.
4. If appropriate, offer the manual handoff into `research-work-package`.

## Output format

Return a concise bootstrap brief that includes:

- prerequisite status
- proposed `./docs/00x-work/` folder
- relevant upstream references
- recommended next prompt or handoff

## Examples (optional)

### Example request

Start a new work package for improving runtime configuration management.

### Example response (optional)

A short bootstrap brief confirming prerequisites, proposing a `./docs/00x-work/` folder, and recommending whether to continue into research or package requirements.