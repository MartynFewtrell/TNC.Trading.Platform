---
agent: 'Refactoring Mitigation Implementor'
description: 'Starts the implementation stage for an approved refactoring mitigation plan, keeps progress and validation current, and maintains the execution log.'
name: execute-refactoring-mitigation
model: 'gpt-5.4'
# tags: [refactoring, mitigation-plan, execution, build, test, maintainability]
---

# Execute a Work Package Refactoring Mitigation Plan (`plans/00n-work-package-refactoring-mitigation-plan.md`)

## Purpose

Use the dedicated `Refactoring Mitigation Implementor` agent to execute an approved numbered mitigation plan under `plans/`.

This stage begins only from explicit user action. It must keep the mitigation plan and its durable execution log aligned with actual validated progress.

## When to use

- You have an approved or in-progress numbered mitigation plan under `./docs/00x-work/plans/`.
- You want the assistant to implement the planned refactoring work end to end with frequent build and test gates.
- You want mitigation progress tracked directly in the physical markdown plan and a sibling execution log.

## Inputs

### Required

- Path to the target numbered mitigation plan under `./docs/00x-work/plans/` (or paste its contents).

### Optional

- Path to the related `work-package-refactoring-review-report.md` in the same work folder.
- Paths or contents of `requirements.md`, `technical-specification.md`, and existing numbered plan files in the same work package.
- Project-level business requirements path or contents from `./docs/business-requirements.md`.
- Any execution constraints for this run, such as smallest-safe-refactor-first, behavior-preservation-first, minimal public-surface changes, no infrastructure-on validation, CI parity, or timebox limits.

## Constraints

- MUST: Start only from an explicit user request or manual handoff.
- MUST: Treat the mitigation plan as the source of truth.
- MUST: Create or resume the sibling execution log before substantive implementation work begins.
- MUST: Keep both the mitigation plan and execution log current as work is implemented and validated.
- MUST: Enforce the plan validation gates.
- MUST NOT: Start from the planning stage automatically.
- Output MUST be: a short execution summary plus updated on-disk plan and execution-log artifacts.

## Process

1. Route the task through the `Refactoring Mitigation Implementor` agent.
2. Read the target mitigation plan and create or resume the sibling execution log.
3. Establish the required validation baseline.
4. Execute plan work in order, updating the plan and execution log as each slice is validated.
5. Use the manual handoffs back to planning or review if implementation reveals a plan defect or a changed refactoring risk.

## Output format

Return a short execution summary that identifies completed work, validation outcomes, plan progress, and execution-log status. The agent owns the detailed execution behavior.

## Examples (optional)

### Example request

Execute `./docs/003-authentication-and-authorisation/plans/005-work-package-refactoring-mitigation-plan.md`.

### Example response (optional)

A summary of implemented refactoring work, the validation results, and an updated numbered mitigation plan file with completed items checked off.
