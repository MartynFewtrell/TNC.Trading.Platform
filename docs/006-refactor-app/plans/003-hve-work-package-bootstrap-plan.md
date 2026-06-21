# Refactor App HVE Work Package Bootstrap Plan

## Summary

- **Source**: See `../requirements.md` for canonical work metadata and scope. See `../technical-specification.md` for the work-package implementation posture. See `plans/002-hve-agent-adoption-plan.md` for the earlier refactoring-workflow-specific HVE adoption work that this plan should complement rather than replace.
- **Status**: in-progress
- **Inputs**:
  - `../../business-requirements.md`
  - `../requirements.md`
  - `../technical-specification.md`
  - `plans/002-hve-agent-adoption-plan.md`
  - `.github/prompts/help.md`
  - `.github/templates/agent-template.md`
  - `.github/templates/prompt.template.md`

## Description of work

Implement the next HVE-alignment layer for repository workflow assets so a new work package can be started, researched, documented, planned, implemented, and reviewed with less operator memory and more explicit phase boundaries.

This plan is scoped to Copilot customization assets and workflow documentation under `.github/` plus any related repository guidance updates. It does not include application or test refactoring under `src/` or `test/`.

The primary delivery goal is to add a repo-native HVE flow for new work-package setup that keeps the existing `docs/00x-work/` artifact model, while introducing the missing HVE-style bootstrap, research, and review stages.

## Delivery approach

- **Delivery model**: multiple PRs or one staged PR with reviewable work items, because the workflow should remain usable after each major step
- **Branching**: use one feature branch, keep prompt, agent, and skill changes grouped by work item, and validate each workflow boundary before moving forward
- **Dependencies**:
  - existing work-package prompts under `.github/prompts/`
  - existing agent patterns under `.github/agents/`
  - repository templates under `.github/templates/`
  - repository work-package guidance in `.github/instructions/work-packages.instructions.md`
  - existing prompt workflow guide in `.github/prompts/help.md`
- **Key risks**:
  - a new bootstrap flow could duplicate the existing generators instead of composing them
    - **Mitigation**: make the bootstrapper orchestrate and reuse the established requirements, technical-spec, and delivery-plan stages rather than replacing their document contracts
  - adding an HVE research step could create a second, competing artifact model
    - **Mitigation**: keep the research artifact repo-native and explicitly position it as an upstream input to work-package creation, not a replacement for `requirements.md` or `technical-specification.md`
  - a review-stage agent could overlap awkwardly with the existing test and refactoring review prompts
    - **Mitigation**: scope the new review stage to overall plan-and-delivery conformance, then retain the existing test and refactoring reviews as downstream specialist loops
  - introducing too many new assets at once could make the workflow harder, not easier, to adopt
    - **Mitigation**: implement the three core recommendations first and defer optional supporting assets unless they are still needed after the first integrated flow works

### Refactoring package addendum

- **Work package profile**: `mixed`
- **Behavior-preservation boundary**: `Existing work-package document structure under docs/00x-work and the current prompt outputs must remain compatible with repository standards while new HVE-aligned workflow assets are introduced.`
- **Expected follow-on artifacts**:
  - `<none>` unless implementation reveals gaps that require a dedicated follow-on plan for optional helper assets

## Delivery Plan

### Execution gates (required)

Before starting *any* work item, and again before marking a work item as complete, run the build + test suite and resolve any failures.

| Gate | When | Required actions | If failures occur |
| --- | --- | --- | --- |
| Baseline | Before starting any work item | Run the validation listed in **Cross-cutting validation** and check editor diagnostics on changed `.prompt.md`, `.agent.md`, and skill files. | Fix or revert until diagnostics and required validation are green before continuing. |
| Pre-completion | Before completing a work item | Re-run the validation listed in **Cross-cutting validation** and manually verify the touched workflow boundary. | Fix failures before marking the work item complete. |

### Planned work items

| Work item | Description | Traceability (requirements) | Traceability (spec sections) | Dependencies | Validation | Rollback/Backout | User instructions |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Work Item 1: Add the bootstrap entry point | Create a dedicated prompt and agent that start a new work package, enforce the existing `docs/00x-work/` baseline, and orchestrate baseline artifact creation in the right order. | `FR4, OR1, NF1, NF3, TR1` | `3.1`, `4 (FR4, NF1, NF3, TR1)`, `5.2 Steps 1-2, 5`, `8` | Existing generators for requirements, technical specification, and delivery plan | Diagnostics clean, workflow routing reviewed, manual bootstrap scenario confirms the operator can start from one entry point | Revert the new bootstrap prompt and agent and restore any help-document references changed in this work item | Review the bootstrap prompt inputs and approval boundaries before allowing it to become the preferred entry point |
| Work Item 2: Add the research stage for new work packages | Create a repo-native HVE research prompt and agent that inspect existing code, docs, and dependencies before package design, then write one authoritative research artifact for the new package. | `FR4, OR1, NF1, NF2, NF3, TR1, TR2` | `2.1`, `3.1`, `4 (FR4, NF1, NF2, NF3, TR1, TR2)`, `5.1`, `5.2 Steps 1, 4-5`, `8` | Work Item 1, existing repo documentation and search flows | Diagnostics clean, research artifact path and format reviewed, manual research run confirms evidence-backed output with no code changes | Revert the new research prompt and agent and remove any bootstrap integration to fall back to the current documentation-first workflow | Approve the research artifact location and output contract before making it a required upstream stage |
| Work Item 3: Add the general delivery review stage | Create a review prompt and agent that validate completed delivery against requirements, technical specification, delivery plan, instructions, and affected wiki pages before the specialist test and refactoring loops. | `FR4, OR1, NF1, NF2, TR1, TR2` | `2.3`, `3.1`, `4 (FR4, NF1, NF2, TR1, TR2)`, `5.2 Steps 4-5`, `8` | Existing delivery plan flow, existing test and refactoring review prompts | Diagnostics clean, manual review scenario confirms the new review complements rather than replaces specialist reviews | Revert the new review prompt and agent and restore help-document ordering if the review stage causes confusion or overlap | Confirm the review scope is plan-and-delivery conformance only, not a duplicate of test or refactoring review |
| Work Item 4: Add supporting specialization and workflow documentation | If still needed after the core flow works, add dedicated authoring agents and a shared context-loading skill, then update guidance so operators know when to use each stage. | `FR4, OR1, NF1, NF3, TR1` | `3.1`, `4 (FR4, NF1, NF3, TR1)`, `5.2 Step 5`, `8` | Work Items 1-3 | Diagnostics clean, operator guidance updated, manual walkthrough confirms the documented sequence is coherent | Revert new optional assets and keep the simpler core flow if added specialization does not materially improve usability | Defer this work item if the first integrated flow is already clear enough without extra specialization |

### Work Item 1 details

- [ ] Work Item 1: Add the bootstrap entry point
  - [x] Build and test baseline established
  - [x] Task 1: Create the repo-native bootstrap prompt and agent
    - [x] Step 1: Create `.github/agents/work-package-bootstrapper.agent.md` from `.github/templates/agent-template.md`
    - [x] Step 2: Create `.github/prompts/start-work-package.prompt.md` to route into the bootstrapper agent
    - [x] Step 3: Define the prompt and agent inputs around a work-package idea, target folder selection, and prerequisite checks for `docs/business-requirements.md` and `docs/systems-analysis.md`
  - [x] Task 2: Define the orchestration boundaries
    - [x] Step 1: Make the bootstrapper verify the project-level prerequisites before any work-package generation begins
    - [x] Step 2: Make the bootstrapper reuse the existing `generate-requirements`, `generate-technical-spec`, and `generate-delivery-plan` stages rather than redefining their outputs
    - [x] Step 3: Keep an explicit stop or approval boundary where the workflow should not auto-start implementation
  - [x] Task 3: Update operator guidance
    - [x] Step 1: Update `.github/prompts/help.md` to present `start-work-package` as the preferred entry point for a new work package
    - [x] Step 2: Document where the bootstrap flow hands off to the established artifact generators
    - [x] Step 3: Confirm the older direct-entry prompts remain usable as manual re-entry points
  - [x] Relevant `docs/wiki/` pages assessed and updated if repository workflow guidance is intentionally promoted there
  - [ ] Build and test validation

  - **Files**:
    - `.github/agents/work-package-bootstrapper.agent.md`: new top-level agent for starting a work package
    - `.github/prompts/start-work-package.prompt.md`: new prompt entry point for work-package bootstrap
    - `.github/prompts/help.md`: preferred workflow updates for the new entry point
  - **Work Item Dependencies**: None
  - **User Instructions**: Review the bootstrap entry conditions and handoff boundaries before making this the default recommendation.

### Work Item 2 details

- [ ] Work Item 2: Add the research stage for new work packages
  - [x] Build and test baseline established
  - [x] Task 1: Create the research prompt and agent
    - [x] Step 1: Create `.github/agents/work-package-researcher.agent.md` from `.github/templates/agent-template.md`
    - [x] Step 2: Create `.github/prompts/research-work-package.prompt.md` to route into the researcher agent
    - [x] Step 3: Define one authoritative research artifact format and output location that is compatible with this repository's work-package workflow
  - [x] Task 2: Scope the research phase correctly
    - [x] Step 1: Make the researcher inspect existing implementation, tests, documentation, and instruction files relevant to the proposed package
    - [x] Step 2: Require one evidence-backed recommended approach instead of multiple speculative designs
    - [x] Step 3: Make the research artifact an upstream input to package definition, not a replacement for `requirements.md` or `technical-specification.md`
  - [x] Task 3: Integrate the research stage into the documented flow
    - [x] Step 1: Update `.github/prompts/help.md` to place `research-work-package` between package start and package design
    - [x] Step 2: Define when `start-work-package` should recommend or hand off to research
    - [x] Step 3: Keep the research stage usable as a standalone manual entry point when package scope is still unclear
  - [x] Relevant `docs/wiki/` pages assessed and updated if repository workflow guidance is intentionally promoted there
  - [ ] Build and test validation

  - **Files**:
    - `.github/agents/work-package-researcher.agent.md`: new research-only agent for work-package discovery
    - `.github/prompts/research-work-package.prompt.md`: new prompt entry point for HVE-style package research
    - `.github/prompts/help.md`: sequence updates to include the research phase
  - **Work Item Dependencies**: Work Item 1
  - **User Instructions**: Approve the research artifact path and scope before requiring the research stage for all non-trivial work packages.

### Work Item 3 details

- [ ] Work Item 3: Add the general delivery review stage
  - [x] Build and test baseline established
  - [x] Task 1: Create the general delivery review prompt and agent
    - [x] Step 1: Create `.github/agents/work-package-reviewer.agent.md` from `.github/templates/agent-template.md`
    - [x] Step 2: Create `.github/prompts/review-work-package-delivery.prompt.md` to route into the reviewer agent
    - [x] Step 3: Define the review artifact scope around requirements, spec, plan, implementation alignment, repo instructions, and wiki updates
  - [x] Task 2: Keep the new review stage distinct from existing specialist reviews
    - [x] Step 1: Position the reviewer as a general delivery-conformance check before `review-test-approach` and `review-refactoring-approach`
    - [x] Step 2: Make the prompt guidance explicit that test-quality and refactoring-quality findings still belong in the downstream specialist loops
    - [x] Step 3: Define whether the review stage should emit a physical markdown artifact and where it should live
  - [x] Task 3: Update the documented operator flow
    - [x] Step 1: Insert `review-work-package-delivery` into `.github/prompts/help.md` after `execute-delivery`
    - [x] Step 2: Update sequence descriptions so the new review stage becomes the default general validation step
    - [x] Step 3: Preserve the existing test and refactoring mitigation flows as optional or follow-on hardening loops
  - [x] Relevant `docs/wiki/` pages assessed and updated if repository workflow guidance is intentionally promoted there
  - [ ] Build and test validation

  - **Files**:
    - `.github/agents/work-package-reviewer.agent.md`: new review-only agent for post-delivery conformance checks
    - `.github/prompts/review-work-package-delivery.prompt.md`: new prompt entry point for the general review phase
    - `.github/prompts/help.md`: sequence updates to include the delivery review stage
  - **Work Item Dependencies**: Work Items 1-2
  - **User Instructions**: Review the proposed review artifact location and severity model before this stage is made mandatory.

### Work Item 4 details

- [ ] Work Item 4: Add supporting specialization and workflow documentation
  - [ ] Build and test baseline established
  - [ ] Task 1: Decide whether extra specialization is still justified after the core flow works
    - [ ] Step 1: Evaluate whether dedicated authoring agents for requirements, technical specification, and delivery planning materially improve clarity over the current prompt-driven flow
    - [ ] Step 2: Evaluate whether a reusable context-loading skill would remove enough duplicated setup to justify the added asset
    - [ ] Step 3: Defer any optional asset whose value is still hypothetical
  - [ ] Task 2: Implement only the optional assets that remain justified
    - [ ] Step 1: If justified, create `.github/agents/work-package-requirements-author.agent.md`
    - [ ] Step 2: If justified, create `.github/agents/work-package-technical-spec-author.agent.md` and `.github/agents/work-package-delivery-planner.agent.md`
    - [ ] Step 3: If justified, add a reusable skill under `.github/skills/` to load work-package context and prerequisites for the bootstrap and research flows
  - [ ] Task 3: Consolidate documentation for the finished workflow
    - [ ] Step 1: Update `.github/prompts/help.md` with the final end-to-end sequence for new work-package setup
    - [ ] Step 2: Add or update repository guidance that explains which assets are core versus optional
    - [ ] Step 3: Confirm the documented sequence still respects the current `docs/00x-work/` structure and approval boundaries
  - [ ] Relevant `docs/wiki/` pages assessed and updated if repository workflow guidance is intentionally promoted there
  - [ ] Build and test validation

  - **Files**:
    - `.github/agents/work-package-requirements-author.agent.md`: optional dedicated requirements authoring agent if justified
    - `.github/agents/work-package-technical-spec-author.agent.md`: optional dedicated technical specification agent if justified
    - `.github/agents/work-package-delivery-planner.agent.md`: optional dedicated delivery planning agent if justified
    - `.github/skills/`: optional shared context-loading skill if justified
    - `.github/prompts/help.md`: final workflow guidance consolidation
  - **Work Item Dependencies**: Work Items 1-3
  - **User Instructions**: Treat this work item as conditional. Skip optional assets that do not materially improve the first integrated flow.

## Cross-cutting validation

- **Build**: `dotnet build TNC.Trading.Platform.slnx`
- **Unit tests**: `dotnet test TNC.Trading.Platform.slnx`
- **Integration tests**: Use the repo-wide `dotnet test TNC.Trading.Platform.slnx` run unless a narrower executable validation path becomes available for customization assets
- **Manual checks**:
  - Confirm changed `.prompt.md`, `.agent.md`, and optional skill files have no editor diagnostics
  - Confirm `start-work-package` preserves the existing `docs/00x-work/` artifact contract
  - Confirm `research-work-package` produces one authoritative research artifact without making code changes
  - Confirm `review-work-package-delivery` complements rather than replaces `review-test-approach` and `review-refactoring-approach`
  - Confirm direct use of the existing generation prompts remains possible as a manual re-entry path
- **Security checks**:
  - Verify no new prompt, agent, or skill introduces unnecessary tool exposure
  - Verify research and review artifacts do not encourage secrets or sensitive values to be stored in workflow outputs

## Acceptance checklist

- [ ] Work item aligns with `../business-requirements.md`.
- [ ] All referenced `FRx` requirements are implemented and validated.
- [ ] All referenced `NFx` requirements have measurements or checks.
- [ ] All referenced `SRx` security requirements are implemented and validated.
- [ ] Relevant `docs/wiki/` pages are updated to reflect the delivered implementation when repository workflow guidance is intentionally surfaced there.
- [ ] Affected wiki links resolve after documentation updates.
- [ ] Rollback/backout plan documented for each work item.

## Notes

- This plan intentionally implements the three core recommendations first: a bootstrap entry point, a research phase, and a general review phase.
- The existing test and refactoring review loops remain valuable and should stay downstream of the new general delivery review.
- Optional dedicated authoring agents and a shared context-loading skill are deliberately deferred until the core HVE-aligned flow proves where extra specialization is still needed.
- Work Items 1-3 are implemented in this run at the customization-asset level. Their final build-and-test validation checkboxes remain open because `dotnet test TNC.Trading.Platform.slnx` did not complete cleanly in the current environment.
- The observed validation blocker was environment-dependent test failure rather than a customization-file diagnostic issue: functional tests reported connection refusal on `localhost:5281`, and AppHost-backed integration output showed Keycloak readiness and SSL-health issues during the suite run.
- Work Item 4 remains intentionally deferred because the first integrated flow is coherent without adding extra specialization yet.