# Refactoring Workflow Usage

This document explains how to use the refactoring review, mitigation planning, and mitigation execution workflow that is defined by the custom prompts and agents in this repository.

## Purpose

The workflow is designed to support repeatable refactoring work for a single work package.
It separates the process into three stages:

1. Review the current implementation and identify maintainability risks.
2. Turn the review into a concrete mitigation plan.
3. Execute the approved mitigation plan with validation and progress tracking.

The key workflow boundary is that Review and Planning are connected, but Execution remains a deliberate manual step.

This workflow supplements the normal work-package baseline documents. It does not replace them.

## Baseline Work Package Documents

Before using the refactoring workflow for a dedicated refactoring work package, the package should still have its baseline planning documents:

1. `requirements.md`
2. `technical-specification.md`
3. `plans/001-delivery-plan.md`

The refactoring workflow then layers additional evidence and execution artifacts on top of that baseline.

## Workflow Assets

The workflow is implemented with these prompts:

1. [review-refactoring-approach.prompt.md](./prompts/review-refactoring-approach.prompt.md)
2. [plan-refactoring-mitigation.prompt.md](./prompts/plan-refactoring-mitigation.prompt.md)
3. [execute-refactoring-mitigation.prompt.md](./prompts/execute-refactoring-mitigation.prompt.md)

Those prompts target these top-level agents:

1. [work-package-refactoring-reviewer.agent.md](./agents/work-package-refactoring-reviewer.agent.md)
2. [refactoring-mitigation-planner.agent.md](./agents/refactoring-mitigation-planner.agent.md)
3. [refactoring-mitigation-implementor.agent.md](./agents/refactoring-mitigation-implementor.agent.md)

The implementation stage can also use these helper subagents internally:

1. [validation-command-resolver.agent.md](./agents/subagents/validation-command-resolver.agent.md)
2. [mitigation-phase-implementor.agent.md](./agents/subagents/mitigation-phase-implementor.agent.md)
3. `Explore` for read-only discovery

## Recommended Operator Flow

Use the workflow in this order:

1. Run `/review-refactoring-approach` for the target work package.
2. Let the workflow continue into planning if the review completes successfully.
3. Inspect the generated mitigation plan before any implementation starts.
4. Run `/execute-refactoring-mitigation` only when you want implementation to begin.

This flow keeps evidence gathering, planning, and code changes clearly separated.

## Stage 1: Review

Start with [review-refactoring-approach.prompt.md](./prompts/review-refactoring-approach.prompt.md).

### What to provide

- The target work package folder under `./docs/00x-work/`
- Optional risk areas to emphasize
- Optional review depth or output path

### What it does

- Reads the work-package artifacts, starting with `requirements.md`
- Inspects related implementation and test files
- Produces a physical refactoring review report in the work-package folder
- Assigns stable finding identifiers such as `F1` and `F2`

### Handoff behavior

The review agent auto-hands off to the planner when the review is complete and usable.
If the review is blocked or incomplete, it should stop instead of continuing.

## Stage 2: Planning

Planning runs through [plan-refactoring-mitigation.prompt.md](./prompts/plan-refactoring-mitigation.prompt.md), either from the review handoff or as a manual re-entry point.

### What it does

- Consumes the completed refactoring review report
- Preserves the review finding identifiers in the plan
- Creates a new numbered mitigation plan in the target `plans/` folder
- Infers validation commands when repository context makes them clear
- Defines work items, validation gates, rollback guidance, and documentation updates

### Handoff behavior

The planner exposes a handoff to execution, but it is configured with `send: false`.
That means the execution prompt is prepared for the user, but implementation does not auto-start.

## Stage 3: Execution

Execution starts only when you manually run [execute-refactoring-mitigation.prompt.md](./prompts/execute-refactoring-mitigation.prompt.md) or use the manual planner handoff.

### What it does

- Reads the approved numbered mitigation plan
- Executes work items in sequence
- Updates plan checkboxes as work is completed and validated
- Runs the validation gates defined by the plan
- Uses helper subagents for narrow implementation slices or validation-command inference when needed

### Durable execution log

The implementation stage maintains a lightweight execution log next to the mitigation plan.

Use this naming convention:

1. Mitigation plan: `006-work-package-refactoring-mitigation-plan.md`
2. Execution log: `006-work-package-refactoring-mitigation-plan-execution-log.md`

The execution log should capture:

- The active plan path
- The current work item
- Completed steps
- Validation commands and outcomes
- Blockers
- The next intended action

This supports pause-and-resume execution without losing state.

## Typical End-to-End Example

For a work package under `./docs/006-refactor-app/`:

1. Run `/review-refactoring-approach` and point it at `./docs/006-refactor-app/`.
2. Review the generated refactoring review report.
3. Allow the auto-handoff into planning.
4. Review the generated mitigation plan in `./docs/006-refactor-app/plans/`.
5. Start `/execute-refactoring-mitigation` only after you are satisfied with the plan.
6. Monitor the mitigation plan and its sibling execution log as the implementation progresses.

## When to Use Manual Re-entry

Run the planning prompt directly when:

- A review report already exists and you want a fresh mitigation plan
- A previous plan needs to be regenerated from the same review
- Review completed earlier and you are resuming later

Run the execution prompt directly when:

- The mitigation plan is already approved
- You want to resume a previously started implementation run
- You want the implementor to continue from the current execution log state

## Safety Boundaries

This workflow is intended to preserve a clear approval boundary before code changes start.

The expected behavior is:

1. Review may auto-continue into Planning.
2. Planning must stop after creating the mitigation plan.
3. Execution must start only from explicit user action.

If the workflow ever skips directly from planning into implementation without user approval, treat that as a workflow defect and correct the agent handoff configuration.

## Files Produced by the Workflow

Depending on the stage, the workflow typically creates or updates:

1. A numbered refactoring review report in the target work-package folder
2. A numbered refactoring mitigation plan in the target `plans/` folder
3. A sibling execution log during implementation
4. The mitigation plan itself as checklist progress is updated during execution

For a refactoring-focused work package, the combined document set typically becomes:

1. `requirements.md`
2. `technical-specification.md`
3. `plans/001-delivery-plan.md`
4. `NNN-work-package-refactoring-review-report.md`
5. `plans/NNN-work-package-refactoring-mitigation-plan.md`
6. `plans/NNN-work-package-refactoring-mitigation-plan-execution-log.md`

## Operational Notes

- Keep the work package narrow when you start the workflow.
- Review the mitigation plan before execution rather than treating it as auto-approved.
- Prefer rerunning Review or Planning when the repository context changed materially.
- Use the execution log to resume work after interruptions instead of reconstructing state from memory.

## Related Files

- [refactoring-workflow-usage.md](./refactoring-workflow-usage.md)
- [copilot-instructions.md](./copilot-instructions.md)
- [review-refactoring-approach.prompt.md](./prompts/review-refactoring-approach.prompt.md)
- [plan-refactoring-mitigation.prompt.md](./prompts/plan-refactoring-mitigation.prompt.md)
- [execute-refactoring-mitigation.prompt.md](./prompts/execute-refactoring-mitigation.prompt.md)