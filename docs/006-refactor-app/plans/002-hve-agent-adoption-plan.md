# Refactor App HVE Agent Adoption Plan

## Summary

- **Source**: See `../requirements.md` for canonical work metadata and scope. See `../technical-specification.md` for the work-package implementation posture. See `../hve-agent-recommendations-for-refactoring-workflow.md` for the recommended target workflow and agent inventory.
- **Status**: draft
- **Inputs**:
  - `../../business-requirements.md`
  - `../requirements.md`
  - `../technical-specification.md`
  - `../hve-agent-recommendations-for-refactoring-workflow.md`
  - `.github/templates/delivery-plan.template.md`
  - `.github/templates/agent-template.md`
  - `.github/templates/prompt.template.md`

## Description of work

Add HVE-style workflow agents for the refactoring review and mitigation flow, and update the existing refactoring prompts so Review and Planning run as one continuous upstream stage while Execution remains a deliberate, explicitly approved downstream stage.

This plan is scoped to Copilot customization assets under `.github/agents/` and `.github/prompts/`. It does not include product-code refactoring work in `src/` or `test/`. The primary delivery goal is to make the existing refactoring workflow more structured, reusable, and easier to run repeatedly without changing the current workflow intent.

## Delivery approach

- **Delivery model**: single PR with staged commits
- **Branching**: use one feature branch, keep prompt and agent changes grouped by work item, and validate the integrated flow after each work item
- **Dependencies**:
  - existing refactoring prompts under `.github/prompts/`
  - agent templates under `.github/templates/`
  - existing agent directory under `.github/agents/`
  - recommendations in `../hve-agent-recommendations-for-refactoring-workflow.md`
- **Key risks**:
  - mismatched handoff names or prompt targets could leave the workflow disconnected
    - **Mitigation**: create agent files before updating prompt targets and validate exact agent names in frontmatter and handoffs
  - the planning agent could accidentally auto-start execution, removing the intended approval gate
    - **Mitigation**: explicitly configure the planning-to-execution handoff with `send: false` and validate stop behavior manually
  - overly broad agent instructions could recreate the current generic-agent problem
    - **Mitigation**: keep each top-level agent bounded to one stage and use helper agents for repeated narrow tasks

## Delivery Plan

### Execution gates (required)

Before starting *any* work item, and again before marking a work item as complete, run the build + test suite and resolve any failures.

| Gate | When | Required actions | If failures occur |
| --- | --- | --- | --- |
| Baseline | Before starting any work item | Run build and tests listed in **Cross-cutting validation**. Check editor diagnostics on changed `.agent.md` and `.prompt.md` files. | Fix or revert until build/tests and diagnostics are green before continuing. |
| Pre-completion | Before completing a work item | Re-run build and tests listed in **Cross-cutting validation**. Re-check diagnostics and manual workflow checks for the touched prompts and agents. | Fix failures before marking the work item complete. |

### Planned work items

| Work item | Description | Traceability (requirements) | Traceability (spec sections) | Dependencies | Validation | Rollback/Backout | User instructions |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Work Item 1: Add top-level workflow agents | Create dedicated reviewer, planner, and implementor agents so the refactoring workflow is no longer centered on a generic prompt target. | `FR4, OR1, NF1` | `2.3`, `3.1`, `4 (FR4, NF1)`, `5.2 Step 5` | Existing `.github/prompts/review-refactoring-approach.prompt.md`, `.github/prompts/plan-refactoring-mitigation.prompt.md`, `.github/prompts/execute-refactoring-mitigation.prompt.md` | Diagnostics clean, build/test baseline green, agent frontmatter and handoff metadata reviewed | Revert the new `.github/agents/*.agent.md` files and any prompt references added in this work item | Review the proposed top-level agent responsibilities before moving to helper agents or prompt rewiring |
| Work Item 2: Add first-wave helper agents and execution-log convention | Create the minimum supporting helper agents needed for HVE-style orchestration and define the durable execution artifact used by the implementation stage. | `FR4, OR1, NF1, NF3, TR1` | `3.1`, `4 (NF3, TR1)`, `5.2 Steps 4-5`, `8` | Work Item 1 | Diagnostics clean, build/test baseline green, helper agents are non-user-invocable where intended, execution-log location and ownership documented | Revert helper-agent files and remove the execution-log convention from the implementing agent instructions | Confirm that the first-wave helper set is sufficient and defer second-wave helpers to follow-on work unless a concrete gap remains |
| Work Item 3: Rewire prompts and enforce the automation boundary | Update the existing refactoring prompts so Review auto-continues into Planning, Planning stops after creating a plan, and Execution remains manually approved. | `FR4, OR1, NF2, TR1` | `2.3`, `3.1`, `4 (FR4, NF2, TR1, TR2)`, `8` | Work Items 1-2 | Diagnostics clean, build/test baseline green, manual workflow check confirms review-to-plan auto-flow and plan-to-execute stop gate | Revert prompt frontmatter and handoff changes to return prompts to the prior generic-agent routing | After the prompts are updated, manually inspect the generated plan before starting execution through the execution prompt |

### Work Item 1 details

- [ ] Work Item 1: Add top-level workflow agents
  - [ ] Build and test baseline established
  - [ ] Task 1: Define the top-level agent inventory and file layout
    - [ ] Step 1: Create `.github/agents/work-package-refactoring-reviewer.agent.md` from `.github/templates/agent-template.md`
    - [ ] Step 2: Create `.github/agents/refactoring-mitigation-planner.agent.md` from `.github/templates/agent-template.md`
    - [ ] Step 3: Create `.github/agents/refactoring-mitigation-implementor.agent.md` from `.github/templates/agent-template.md`
  - [ ] Task 2: Migrate the stage responsibilities out of the current generic prompt targets
    - [ ] Step 1: Move review-stage responsibilities from `.github/prompts/review-refactoring-approach.prompt.md` into the reviewer agent instructions
    - [ ] Step 2: Move planning-stage responsibilities from `.github/prompts/plan-refactoring-mitigation.prompt.md` into the planner agent instructions
    - [ ] Step 3: Move implementation-stage responsibilities from `.github/prompts/execute-refactoring-mitigation.prompt.md` into the implementor agent instructions
  - [ ] Task 3: Define the first top-level handoffs
    - [ ] Step 1: Configure reviewer-to-planner handoff metadata for automatic continuation into planning
    - [ ] Step 2: Configure implementor handoffs back to review and planning for re-review or plan revision flows
    - [ ] Step 3: Confirm the planner handoff metadata does not auto-start implementation
  - [ ] Relevant `docs/wiki/` pages assessed and updated if implementation guidance for repository workflow is intentionally promoted there
  - [ ] Build and test validation

  - **Files**:
    - `.github/agents/work-package-refactoring-reviewer.agent.md`: new reviewer agent for the refactoring review stage
    - `.github/agents/refactoring-mitigation-planner.agent.md`: new planner agent for mitigation-plan creation
    - `.github/agents/refactoring-mitigation-implementor.agent.md`: new implementor agent for approved mitigation plans
    - `.github/prompts/review-refactoring-approach.prompt.md`: prompt target and purpose alignment updates
    - `.github/prompts/plan-refactoring-mitigation.prompt.md`: prompt target and purpose alignment updates
    - `.github/prompts/execute-refactoring-mitigation.prompt.md`: prompt target and purpose alignment updates
  - **Work Item Dependencies**: None
  - **User Instructions**: Review the top-level agent names, handoff labels, and stage boundaries before approving the prompt rewiring work.

### Work Item 2 details

- [ ] Work Item 2: Add first-wave helper agents and execution-log convention
  - [ ] Build and test baseline established
  - [ ] Task 1: Add the minimum helper agents needed for the initial HVE-aligned flow
    - [ ] Step 1: Create `.github/agents/subagents/validation-command-resolver.agent.md` to infer build, test, and lint commands for the workflow
    - [ ] Step 2: Create `.github/agents/subagents/mitigation-phase-implementor.agent.md` to execute one bounded mitigation work item or implementation slice
    - [ ] Step 3: Mark helper agents as non-user-invocable unless a clear interactive use case is required
  - [ ] Task 2: Define the v1 read-only discovery strategy
    - [ ] Step 1: Use the existing `Explore` subagent as the default read-only discovery helper in v1 instead of immediately creating dedicated mapper and evidence-collector agents
    - [ ] Step 2: Record `Scope-to-Implementation Mapper` and `Refactoring Evidence Collector` as deferred second-wave agents unless implementation reveals a concrete need sooner
  - [ ] Task 3: Add the durable execution artifact convention
    - [ ] Step 1: Define where the implementation stage writes its lightweight execution log or change summary
    - [ ] Step 2: Document which agent owns creation and updates of that execution artifact
    - [ ] Step 3: Reference that artifact in the implementor agent so long-running mitigation work can pause and resume safely
  - [ ] Relevant `docs/wiki/` pages assessed and updated if repository workflow guidance is intentionally surfaced there
  - [ ] Build and test validation

  - **Files**:
    - `.github/agents/subagents/validation-command-resolver.agent.md`: new helper agent for validation command inference
    - `.github/agents/subagents/mitigation-phase-implementor.agent.md`: new helper agent for bounded implementation slices
    - `.github/agents/refactoring-mitigation-implementor.agent.md`: execution-log ownership and helper-agent usage updates
    - `docs/006-refactor-app/hve-agent-recommendations-for-refactoring-workflow.md`: update only if the delivered helper set or deferrals materially change the documented recommendation
  - **Work Item Dependencies**: Work Item 1
  - **User Instructions**: Approve the first-wave helper scope before adding any second-wave agents so the workflow stays minimal.

### Work Item 3 details

- [ ] Work Item 3: Rewire prompts and enforce the automation boundary
  - [ ] Build and test baseline established
  - [ ] Task 1: Update prompt targets to the new dedicated agents
    - [ ] Step 1: Change `.github/prompts/review-refactoring-approach.prompt.md` to target `Work Package Refactoring Reviewer`
    - [ ] Step 2: Change `.github/prompts/plan-refactoring-mitigation.prompt.md` to target `Refactoring Mitigation Planner`
    - [ ] Step 3: Change `.github/prompts/execute-refactoring-mitigation.prompt.md` to target `Refactoring Mitigation Implementor`
  - [ ] Task 2: Implement the automated Review to Planning flow
    - [ ] Step 1: Configure the reviewer agent handoff to the planning agent with automatic send behavior
    - [ ] Step 2: Ensure the planning prompt remains usable as a manual re-entry point when planning needs to be rerun
    - [ ] Step 3: Confirm blocked reviews do not auto-continue into planning
  - [ ] Task 3: Implement the Planning stop gate before Execution
    - [ ] Step 1: Configure the planning-to-execution handoff with `send: false`
    - [ ] Step 2: Update planner instructions so completion stops after the mitigation plan is written and presented
    - [ ] Step 3: Update implementor instructions so implementation starts only from explicit user action or an explicit non-auto-sent handoff
  - [ ] Task 4: Validate the integrated prompt and agent flow
    - [ ] Step 1: Check editor diagnostics on all changed `.prompt.md` and `.agent.md` files
    - [ ] Step 2: Manually verify that Review auto-flows into Planning for a safe work-package scenario
    - [ ] Step 3: Manually verify that Planning stops after plan creation and does not auto-start Execution
  - [ ] Relevant `docs/wiki/` pages assessed and updated if repository workflow guidance is intentionally surfaced there
  - [ ] Build and test validation

  - **Files**:
    - `.github/prompts/review-refactoring-approach.prompt.md`: target dedicated reviewer agent and support review-to-plan automation
    - `.github/prompts/plan-refactoring-mitigation.prompt.md`: target dedicated planner agent and enforce stop-after-plan behavior
    - `.github/prompts/execute-refactoring-mitigation.prompt.md`: target dedicated implementor agent and preserve explicit execution start
    - `.github/agents/work-package-refactoring-reviewer.agent.md`: finalize auto-handoff behavior
    - `.github/agents/refactoring-mitigation-planner.agent.md`: finalize stop gate and execution handoff behavior
    - `.github/agents/refactoring-mitigation-implementor.agent.md`: finalize explicit-entry behavior
  - **Work Item Dependencies**: Work Items 1-2
  - **User Instructions**: After this work item, inspect the generated mitigation plan before starting implementation through the execution prompt.

## Cross-cutting validation

- **Build**: `dotnet build TNC.Trading.Platform.slnx`
- **Unit tests**: `dotnet test TNC.Trading.Platform.slnx`
- **Integration tests**: Use the repo-wide `dotnet test TNC.Trading.Platform.slnx` run unless a narrower validation path is introduced for the customization assets
- **Manual checks**:
  - Confirm changed `.agent.md` and `.prompt.md` files have no editor diagnostics
  - Confirm reviewer-to-planner handoff is configured to auto-send
  - Confirm planner-to-implementor handoff is configured with `send: false`
  - Confirm the planner stops after creating the mitigation plan and does not auto-start execution
  - Confirm the execution prompt still requires explicit user start
- **Security checks**:
  - Verify no new agent or prompt introduces unnecessary tool exposure
  - Verify no instructions encourage secrets to be passed through prompt content or stored in workflow artifacts

## Acceptance checklist

- [ ] Work item aligns with `../business-requirements.md`.
- [ ] All referenced `FRx` requirements are implemented and validated.
- [ ] All referenced `NFx` requirements have measurements or checks.
- [ ] All referenced `SRx` security requirements are implemented and validated.
- [ ] Relevant `docs/wiki/` pages are updated to reflect the delivered implementation when repository workflow guidance is intentionally surfaced there.
- [ ] Affected wiki links resolve after documentation updates.
- [ ] Rollback/backout plan documented for each work item.

## Notes

- This plan intentionally adopts the first-wave agent set from `../hve-agent-recommendations-for-refactoring-workflow.md` and defers second-wave helper agents unless implementation reveals a concrete gap.
- The plan treats Review and Planning as one continuous upstream process, while preserving a human approval gate before Execution.
- If implementation shows the existing `Explore` subagent is not sufficient for read-only discovery, add a follow-on plan for `Scope-to-Implementation Mapper` and `Refactoring Evidence Collector` rather than expanding this plan midstream.