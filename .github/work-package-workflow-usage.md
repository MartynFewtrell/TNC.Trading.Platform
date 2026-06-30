# Work Package Workflow Usage

This document explains how to use the new work-package bootstrap, research, planning, delivery, and review workflow that is defined by the custom prompts and agents in this repository.

## Purpose

The workflow is designed to support repeatable creation and delivery of a new work package while preserving the repository's existing `docs/00x-work/` artifact model.
It separates the process into six stages:

1. Bootstrap the work package and verify project-level prerequisites.
2. Research the proposed package against the current repository.
3. Define package requirements.
4. Produce the technical specification and delivery plan.
5. Execute the approved delivery plan.
6. Review the delivered package before specialist hardening loops begin.

The key workflow boundary is that bootstrap and research prepare the package, the baseline generation prompts define the package, delivery implements the package, and the new general review stage validates the package before the specialist testing and refactoring reviews take over.

This workflow supplements the existing project-level and work-package-level documentation. It does not replace the baseline work-package documents or the downstream specialist review loops.

## Baseline Work Package Documents

Before a new package is considered ready for delivery, it should still produce the repository's baseline work-package documents:

1. `requirements.md`
2. `technical-specification.md`
3. `plans/001-delivery-plan.md`

The new workflow adds a structured bootstrap and research path before those documents are authored, then adds a general delivery review stage after implementation.

## Workflow Assets

The workflow is implemented with these prompts:

1. [start-work-package.prompt.md](./prompts/start-work-package.prompt.md)
2. [research-work-package.prompt.md](./prompts/research-work-package.prompt.md)
3. [generate-requirements.prompt.md](./prompts/generate-requirements.prompt.md)
4. [generate-technical-spec.prompt.md](./prompts/generate-technical-spec.prompt.md)
5. [generate-delivery-plan.prompt.md](./prompts/generate-delivery-plan.prompt.md)
6. [execute-delivery.prompt.md](./prompts/execute-delivery.prompt.md)
7. [review-work-package-delivery.prompt.md](./prompts/review-work-package-delivery.prompt.md)

Those prompts target or rely on these top-level agents:

1. [work-package-bootstrapper.agent.md](./agents/work-package-bootstrapper.agent.md)
2. [work-package-researcher.agent.md](./agents/work-package-researcher.agent.md)
3. [work-package-reviewer.agent.md](./agents/work-package-reviewer.agent.md)

The workflow can also rely on these existing helpers where appropriate:

1. `Explore` for focused read-only discovery
2. [validation-command-resolver.agent.md](./agents/subagents/validation-command-resolver.agent.md) when delivery-review validation commands need help being inferred

## Recommended Operator Flow

Use the workflow in this order for a new non-trivial work package:

1. Run `/start-work-package` for the initial package idea.
2. Confirm the proposed `./docs/00x-work/` folder and prerequisite status.
3. Run `/research-work-package` for that target work package.
4. Review the generated research artifact.
5. Run `/generate-requirements` for the package.
6. Run `/generate-technical-spec` after the requirements are stable enough.
7. Run `/generate-delivery-plan` after the technical specification is in place.
8. Run `/execute-delivery` only when implementation should begin.
9. Run `/review-work-package-delivery` before entering specialist hardening loops.
10. Continue into `/review-test-approach` and `/review-refactoring-approach` only when deeper package hardening is needed.

This flow keeps package setup, package definition, package implementation, and package validation clearly separated.

## Stage 1: Bootstrap

Start with [start-work-package.prompt.md](./prompts/start-work-package.prompt.md).

### What to provide

- The initial work-package idea
- Optional preferred package name or number
- Optional references from `./docs/systems-analysis.md`

### What it does

- Verifies `./docs/business-requirements.md` and `./docs/systems-analysis.md`
- Inspects existing `./docs/00x-work/` folders
- Recommends the next correctly numbered work-package folder
- Produces a concise bootstrap brief and recommended next stage

### Handoff behavior

The bootstrapper can prepare a manual handoff to research, but it should not start implementation or replace the repository's existing requirements, specification, or delivery-plan generators.

## Stage 2: Research

Research runs through [research-work-package.prompt.md](./prompts/research-work-package.prompt.md).

### What to provide

- The target work-package folder under `./docs/00x-work/`
- Optional risk areas or implementation surfaces to emphasize
- Optional research depth or output path

### What it does

- Reads project-level business requirements and systems analysis
- Inspects the smallest relevant repository surfaces under `src/`, `test/`, and `docs/`
- Produces one numbered physical research artifact in the target work-package folder
- Recommends one package direction with explicit in-scope and out-of-scope boundaries

### Handoff behavior

Research stops after the research artifact is written and summarized.
Use the written artifact as the input to requirements authoring rather than relying on chat context.

## Stage 3: Work-Package Definition

Definition uses the existing baseline generation prompts:

1. [generate-requirements.prompt.md](./prompts/generate-requirements.prompt.md)
2. [generate-technical-spec.prompt.md](./prompts/generate-technical-spec.prompt.md)
3. [generate-delivery-plan.prompt.md](./prompts/generate-delivery-plan.prompt.md)

### What it does

- Turns the researched package direction into `requirements.md`
- Converts those requirements into `technical-specification.md`
- Produces `plans/001-delivery-plan.md` as the executable baseline implementation plan

### Operational note

The bootstrap and research stages do not replace these prompts.
They make the inputs stronger and the resulting package artifacts more grounded in the current repository.

## Stage 4: Delivery

Delivery starts with [execute-delivery.prompt.md](./prompts/execute-delivery.prompt.md).

### What it does

- Reads the approved numbered delivery plan
- Executes work items in sequence
- Updates plan checkboxes as work is completed and validated
- Applies the build and test gates defined by the plan
- Updates affected wiki documentation before the plan is considered complete

### Safety boundary

Delivery starts only when you explicitly run the execution prompt.
The earlier stages define and prepare the package, but they do not auto-start implementation.

## Stage 5: General Delivery Review

Review starts with [review-work-package-delivery.prompt.md](./prompts/review-work-package-delivery.prompt.md).

### What to provide

- The target work-package folder under `./docs/00x-work/`
- Optional specific work items, files, or validation evidence to emphasize
- Optional review depth or output path

### What it does

- Reads the target package's `requirements.md`, `technical-specification.md`, and numbered plan files
- Inspects the smallest supporting implementation, test, and documentation surface needed to confirm delivery conformance
- Reviews validation evidence and wiki alignment
- Produces a numbered physical delivery-review report in the target work-package folder

### Handoff behavior

The general delivery review stops after the review artifact is written and summarized.
It complements, but does not replace, the downstream specialist testing and refactoring reviews.

## Stage 6: Specialist Hardening Loops

After the general delivery review, move into the specialist loops only when deeper hardening is still needed:

1. [review-test-approach.prompt.md](./prompts/review-test-approach.prompt.md)
2. [plan-test-mitigation.prompt.md](./prompts/plan-test-mitigation.prompt.md)
3. [execute-test-mitigation.prompt.md](./prompts/execute-test-mitigation.prompt.md)
4. [review-refactoring-approach.prompt.md](./prompts/review-refactoring-approach.prompt.md)
5. [plan-refactoring-mitigation.prompt.md](./prompts/plan-refactoring-mitigation.prompt.md)
6. [execute-refactoring-mitigation.prompt.md](./prompts/execute-refactoring-mitigation.prompt.md)

These loops remain the correct place to pursue deeper testing improvements and maintainability improvements after the package has already passed the general delivery-conformance review.

## Typical End-to-End Example

For a new work package under `./docs/007-some-new-package/`:

1. Run `/start-work-package` with the package idea.
2. Confirm the proposed `./docs/007-some-new-package/` folder.
3. Run `/research-work-package` for `./docs/007-some-new-package/`.
4. Review the numbered research artifact created in that folder.
5. Run `/generate-requirements` for the new package.
6. Run `/generate-technical-spec` for the same package.
7. Run `/generate-delivery-plan` to create `plans/001-delivery-plan.md`.
8. Run `/execute-delivery` only after the plan is acceptable.
9. Run `/review-work-package-delivery` before moving into any specialist hardening loops.
10. Use the test and refactoring review loops only where the package still needs extra hardening.

## When to Use Manual Re-entry

Run the requirements prompt directly when:

- Research is already complete and you only need to define the package artifacts
- The package was already bootstrapped earlier and you are resuming later
- The package is intentionally simple and the repository context is already clear

Run the research prompt directly when:

- A target package folder already exists and you want a fresh research artifact
- You need to revisit package scope before changing requirements or design
- You are resuming package discovery after an interruption

Run the review prompt directly when:

- Delivery is already complete and you want a general package-level validation pass
- You want a package-conformance review before specialist hardening begins
- You are resuming review after package implementation changed materially

## Safety Boundaries

This workflow is intended to preserve clear approval boundaries between package discovery, package definition, implementation, and review.

The expected behavior is:

1. Bootstrap verifies prerequisites and recommends the next step.
2. Research produces one authoritative package-direction artifact and stops.
3. Baseline generation prompts create the package documents.
4. Delivery starts only from explicit user action.
5. General delivery review stops after producing the review artifact.
6. Specialist hardening loops remain separate follow-on stages.

If any earlier stage starts implementation or replaces the repository's baseline work-package document flow, treat that as a workflow defect and correct the relevant prompt or agent instructions.

## Files Produced by the Workflow

Depending on the stage, the workflow typically creates or updates:

1. A numbered research artifact in the target work-package folder
2. `requirements.md`
3. `technical-specification.md`
4. `plans/001-delivery-plan.md`
5. A numbered delivery review report in the target work-package folder
6. Optional downstream specialist review and mitigation artifacts when hardening work continues

For a typical non-trivial work package, the combined document set often becomes:

1. `NNN-work-package-research.md`
2. `requirements.md`
3. `technical-specification.md`
4. `plans/001-delivery-plan.md`
5. `NNN-work-package-delivery-review-report.md`
6. Optional specialist review and mitigation files

## Operational Notes

- Use `/clear` or start a new chat between major stages when the workflow changes from research to planning, or from planning to implementation, so each stage can operate on the written artifacts rather than stale chat context.
- Keep the work package narrow when you start the workflow.
- Prefer the research stage for non-trivial packages even when you think the path is obvious.
- Use the general delivery review before specialist hardening so the package-level gaps are separated from specialist quality concerns.
- Prefer rerunning research or review when repository context changed materially.

## Related Files

- [work-package-workflow-usage.md](./work-package-workflow-usage.md)
- [refactoring-workflow-usage.md](./refactoring-workflow-usage.md)
- [copilot-instructions.md](./copilot-instructions.md)
- [start-work-package.prompt.md](./prompts/start-work-package.prompt.md)
- [research-work-package.prompt.md](./prompts/research-work-package.prompt.md)
- [review-work-package-delivery.prompt.md](./prompts/review-work-package-delivery.prompt.md)