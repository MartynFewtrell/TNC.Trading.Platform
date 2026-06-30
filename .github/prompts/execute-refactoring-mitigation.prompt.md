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
- MUST: Follow the repository instruction files under `/.github/instructions/` and `/.github/copilot-instructions.md` when implementing source changes, tests, or documentation.
- MUST: Execute work items in the order they appear in the mitigation plan.
- MUST: Treat the mitigation plan as the source of truth for the execution sequence.
- MUST: Prefer the mitigation plan and related work-package artifacts as the primary execution context before scanning unrelated repository areas.
- MUST: Create or resume the sibling execution log before substantive implementation work begins.
- MUST: Keep both the mitigation plan and execution log current as work is implemented and validated.
- MUST: Preserve observable behavior unless the mitigation plan explicitly authorizes a behavior change.
- MUST: Ensure any new or updated automated tests include comments that capture requirement traceability and explain what the test verifies, the expected outcome, and why the behavior matters.
- MUST: Enforce build and test gates.
  - Before starting **Work Item N**, run the build and tests defined by the plan’s **Cross-cutting validation** section.
  - After completing **Work Item N**, re-run the same build and tests.
  - If a successful baseline or post-work-item gate is the most recent action and no code, test, config, or documentation changes have occurred since that run, that result may serve as the next pre-work-item gate.
  - Additionally, run build and tests whenever a change is likely to break compilation or behavior, such as changing contracts, DI wiring, project files, namespaces, component boundaries, test harness configuration, auth configuration, or cross-service interfaces.
  - If the plan does not specify build or test commands, default to running `dotnet build` and `dotnet test` at the repo root first.
    - Only ask the user for exact commands if the defaults cannot be run or if they fail in a way that indicates repo-specific commands are required.

- MUST: Keep the numbered mitigation plan file updated as execution progresses.
  - After each Work Item, Task, or Step is completed, update the corresponding checkbox from `[ ]` to `[x]`.
  - If a checkbox has sub-steps, only check the parent when all children are checked.
  - Do not reorder plan steps while executing; if the plan is wrong or missing steps, record the issue and add a new step explicitly under the relevant Work Item.
- MUST: Keep `./docs/wiki/` aligned with the implemented solution before the mitigation plan is considered complete.
  - Update the relevant wiki pages when the mitigation changes user-visible behavior, implementation structure, architectural guidance, local development guidance, operator guidance, or the testing approach captured in the wiki.
  - If wiki pages change, validate their affected markdown links before finishing the mitigation plan.

- MUST: Drive execution autonomously.
  - Work through as many work items as possible without asking the user.
  - Only ask a question when execution is blocked by missing information or when a decision materially changes scope, sequencing, or risk.
  - Prefer making a safe default choice and recording it in the mitigation plan or execution notes rather than asking for confirmation.

- MUST: Prefer the smallest safe refactoring that resolves the confirmed issue.
- MUST: Prefer strengthening or adding lower-level safety-net tests before higher-level tests when the mitigation plan leaves room for choice.
- MUST: Keep supporting production changes focused on maintainability, clarity, separation of concerns, dependency clarity, duplication reduction, or testability improvements identified by the mitigation plan.
- SHOULD: Run targeted tests during implementation when they provide faster feedback, but do not treat them as a replacement for the required work-item gate runs.
- SHOULD: Read the related review report and work-package documents before expanding scope or appending new mitigation steps.
- SHOULD: For Blazor components, preserve rendering behavior and user interaction behavior while simplifying mixed markup, state management, and orchestration only when the plan calls for it.
- MUST NOT: Mark items as complete if they are not implemented and validated.
- MUST NOT: Skip build and test gates to save time.
- MUST NOT: Introduce speculative abstractions, large rewrites, or unrelated cleanup beyond what the mitigation plan requires.
- MUST NOT: Weaken existing coverage, remove assertions, or broaden waits merely to make tests pass after a refactor.
- MUST NOT: Start from the planning stage automatically.
- Output MUST be: a terse completion note plus updated on-disk plan and execution-log artifacts.

## Process

1. Route the task through the `Refactoring Mitigation Implementor` agent.
2. Read the target mitigation plan and create or resume the sibling execution log.
3. Establish the required validation baseline.
4. Execute plan work in order, updating the plan and execution log as each slice is validated.
5. Use the manual handoffs back to planning or review if implementation reveals a plan defect or a changed refactoring risk.

## Output format

Return:

- **Completed**: What was implemented and which work items were completed.
- **Validation**: The build and test commands run and their outcomes.
- **Plan update**: The updated numbered mitigation plan file content, or a concise diff or patch description if the full file is too large to include.
- **Outstanding items**: Any checklist entries intentionally left incomplete, plus the blocker or reason.
- **Execution log status**: The sibling execution log path and whether it was created or updated in this run.

## Examples (optional)

### Example request

Execute `./docs/003-authentication-and-authorisation/plans/005-work-package-refactoring-mitigation-plan.md`.

### Example response (optional)

A terse completion note, the validation results, and an updated numbered mitigation plan file with completed items checked off.
