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
- MUST: Review `requirements.md` in the target work package.
- MUST: Review `technical-specification.md` when it exists in the target work package.
- SHOULD: Review the existing numbered plan files in the target work package `plans/` folder when they exist.
- MUST: Never overwrite an existing refactoring review report file unless the user explicitly requests overwrite behavior.
- MUST: Use an incremental three-digit numeric prefix for refactoring review report files, for example `001-work-package-refactoring-review-report.md`, `002-work-package-refactoring-review-report.md`, `003-work-package-refactoring-review-report.md`.
- MUST: Prefer the provided work-package artifacts and explicitly supplied paths before discovering additional repository files.
- MUST: Inspect the current implementation that relates to the work package and cite specific evidence using repository paths and, when practical, symbol names such as classes, methods, components, or test classes.
- MUST: Cite file-and-line evidence for material findings, risks, and recommendations unless stable line references are genuinely unavailable in the environment.
- MUST: Map documented requirements, responsibilities, and acceptance criteria to the current implementation and identify where the structure supports or undermines them.
- MUST: Identify maintainability issues such as duplication, excessive complexity, weak cohesion, tight coupling, mixed responsibilities, boundary leakage, brittle control flow, naming problems, dead code, and poor testability when supported by repository evidence.
- MUST: Ground recommendations in established design and refactoring principles, including separation of concerns, single responsibility, explicit dependencies, dependency inversion, and DRY.
- MUST: Prefer the smallest safe refactoring that resolves the confirmed issue.
- MUST: Call out where a proposed refactor could affect observable behavior, contracts, configuration, dependency injection registrations, rendered UI, persistence, or test expectations.
- MUST: Separate confirmed evidence from assumptions or missing-information notes.
- MUST: Give each significant finding a stable identifier such as `F1`, `F2`, and reuse those identifiers in recommendations and suggested next steps where practical.
- MUST: Keep recommendations implementation-oriented enough that they can be converted into work items without re-discovering the core issue.
- MUST NOT: Claim a problem exists unless you can point to the relevant file or symbol.
- MUST NOT: Invent undocumented requirements or pretend a refactoring is safe when key artifacts are missing.
- MUST NOT: Recommend speculative abstractions, unnecessary indirection, or large rewrites when a smaller localized refactor would address the evidence-backed problem.
- MUST NOT: Confuse intentional duplication across distinct business concepts with DRY violations.
- SHOULD: Limit repository scanning to the files needed to establish evidence for the documented scope, risks, and recommendations.
- SHOULD: Consider both production code and related tests when assessing refactoring safety and regression risk.
- SHOULD: Recommend preserving or improving test coverage when a refactor affects behavior-critical paths.
- SHOULD: Call out opportunities to simplify Blazor components when markup, state management, and service orchestration are mixed in ways that reduce maintainability.
- Output MUST be: a short review summary plus a single markdown report on disk with a clear overview, evidence-backed findings, and prioritized refactoring recommendations.

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
