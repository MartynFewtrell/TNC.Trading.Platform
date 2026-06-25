---
agent: 'Refactoring Mitigation Planner'
description: 'Starts the refactoring-planning stage from a completed review report, creates the mitigation plan, and stops before implementation begins.'
name: plan-refactoring-mitigation
model: 'gpt-5.4'
# tags: [refactoring, planning, iterative-work, maintainability, quality]
---

# Plan Mitigation for a Work Package Refactoring Review

## Purpose

Use the dedicated `Refactoring Mitigation Planner` agent to turn a completed work-package refactoring review into an execution-ready mitigation plan.

This stage must end after the plan is written and presented. Execution remains a separate, explicitly approved downstream action.

## When to use

- You have a completed refactoring review report for a work package and want a concrete remediation plan.
- You want to turn refactoring-review findings into a prioritized sequence of implementation, validation, and documentation work.
- You want a physical markdown plan in the work package without auto-starting implementation.

## Inputs

### Required

- Target work package folder under `./docs/00x-work/`.
- The work-package refactoring review report, typically `./docs/00x-work/work-package-refactoring-review-report.md`.
- Access to the relevant repository files under `src/`, `test/`, and `docs/`.

### Optional

- Specific findings, requirement areas, services, components, or risk areas to prioritize.
- Constraints on delivery shape (for example: single PR, phased refactoring, minimal public-surface changes, or behavior-preservation-first).
- A target file path if the final mitigation plan should be written to a specific location instead of the default work-package plan path.
- A preferred planning depth (`quick`, `standard`, or `deep`).

## Configuration variables (optional)

${PLAN_DEPTH="standard"} <!-- quick | standard | deep: controls how much detail to include in the mitigation plan -->

## Constraints

- MUST: Use `.github/templates/refactoring-mitigation-plan.template.md` as the output scaffold.
- MUST: Produce a physical numbered mitigation plan file.
- MUST: Stop after the plan is written and summarized.
- MUST NOT: Start implementation from this prompt.
- Output MUST be: a short planning summary plus the completed plan content on disk.

## Process

1. Route the task through the `Refactoring Mitigation Planner` agent.
2. Read the review report, target work-package documents, and only the repository files needed to make the plan executable.
3. Infer or confirm validation commands.
4. Write the numbered mitigation plan and stop.

## Output format

Return a short summary of the mitigation plan and create the physical plan file on disk. The agent owns the detailed planning rules and the stop-after-plan boundary.

## Examples (optional)

### Example request

Use `./docs/003-authentication-and-authorisation/work-package-refactoring-review-report.md` to create a mitigation plan for the refactoring issues it identifies and write the plan into the work package `plans/` folder with the next available sequence number.

### Example response (optional)

A markdown mitigation plan that maps review findings to prioritized refactoring work items, safety-net validation, behavior-preservation checks, and rollback guidance, and is saved under the work package `plans/` folder.
