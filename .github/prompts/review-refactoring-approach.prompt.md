---
agent: 'Work Package Refactoring Reviewer'
description: 'Starts the refactoring-review stage for a work package, writing an evidence-backed review report and auto-continuing into mitigation planning when the review is complete.'
name: review-refactoring-approach
model: 'gpt-5.4'
# tags: [refactoring, review, iterative-work, maintainability, quality]
---

# Review a Work Package Refactoring Approach

## Purpose

Use the dedicated `Work Package Refactoring Reviewer` agent to review a target work package under `./docs/00x-work/`, inspect the related implementation, and produce an evidence-backed refactoring review report that is specific enough to feed directly into mitigation planning.

When the review reaches a usable conclusion, the workflow should auto-continue into the dedicated planning agent through the configured handoff. If the review is blocked, incomplete, or not yet safe to plan from, stop and report that state instead of continuing.

## When to use

- You want an independent maintainability review of a work package before implementation is considered complete.
- You want to understand whether the current implementation should be refactored to reduce duplication, complexity, coupling, or boundary leakage.
- You want the review stage to flow directly into mitigation planning without manually re-entering the context.

## Inputs

### Required

- Target work package folder under `./docs/00x-work/`.
- Access to the relevant repository files under `src/`, `test/`, and `docs/`.

### Optional

- Specific services, projects, components, or folders to prioritize.
- Known risk areas to emphasize (for example: authentication, authorization, configuration, Blazor component structure, service boundaries, duplication, or testability).
- A target file path if the final report should be written to a specific location instead of the default work-package report path.
- A preferred review depth (`quick`, `standard`, or `deep`).

## Configuration variables (optional)

${REVIEW_DEPTH="standard"} <!-- quick | standard | deep: controls how much detail to include when evaluating maintainability, design quality, and refactoring opportunities -->

## Constraints

- MUST: Use `.github/templates/refactoring-review-report.template.md` as the output scaffold.
- MUST: Produce a physical review report file.
- MUST: Hand off automatically to planning only when the review is complete and usable.
- MUST NOT: Start implementation from this prompt.
- Output MUST be: a short review summary plus the completed report content on disk.

## Process

1. Route the task through the `Work Package Refactoring Reviewer` agent.
2. Read the target work-package documents and inspect only the repository files needed to support evidence-backed findings.
3. Write the physical review report.
4. Auto-handoff to planning only if the review is complete and safe to plan from.

## Output format

Return a short summary of the review and create the physical review report on disk. The agent owns the full report structure and the review-to-plan handoff behavior.

## Examples (optional)

### Example request

Review the work package in `./docs/003-authentication-and-authorisation/` and assess whether the current implementation should be refactored before the work package is considered complete. Write the report into the work package folder.

### Example response (optional)

A markdown report that traces the work package scope to the current implementation, identifies evidence-backed refactoring opportunities, and recommends how to improve maintainability and change safety.
