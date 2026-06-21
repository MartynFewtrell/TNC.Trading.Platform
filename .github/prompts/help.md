# Prompt workflow guide

This document explains what each prompt in `./.github/prompts/` is for, the intended order to use them, and how they fit together as a working delivery workflow.

## Primary workflow

Use this sequence when starting from an idea and driving it through analysis, work-package design, delivery, review, and mitigation.

```text
Idea
  -> generate-business-requirements
  -> generate-systems-analysis
   -> start-work-package
   -> research-work-package
  -> generate-requirements
  -> generate-technical-spec
  -> generate-delivery-plan
  -> execute-delivery
   -> review-work-package-delivery
  -> review-test-approach
  -> plan-test-mitigation
  -> execute-test-mitigation
  -> review-refactoring-approach
  -> plan-refactoring-mitigation
  -> execute-refactoring-mitigation
```

## Workflow stages

### 1. Project foundation

1. **`generate-business-requirements.prompt.md`**
   - Creates `./docs/business-requirements.md`.
   - Use first when a project or initiative starts from a business idea.
   - Produces the non-technical business baseline: goals, scope, stakeholders, and business requirements.

2. **`generate-systems-analysis.prompt.md`**
   - Creates `./docs/systems-analysis.md`.
   - Use after business requirements exist.
   - Refines business intent into system boundary, actors, use cases, business rules, system analysis requirements, quality attributes, and work-package candidates.

### 2. Work-package bootstrap and research

3. **`start-work-package.prompt.md`**
   - Starts a new work package from one repo-native entry point.
   - Use after project-level business requirements and systems analysis exist.
   - Verifies prerequisites, recommends the next `./docs/00x-work/` folder, and routes the operator into the correct next stage.

4. **`research-work-package.prompt.md`**
   - Creates a numbered research artifact in the target work-package folder.
   - Use when you want evidence-backed discovery before package requirements are authored.
   - Produces one authoritative recommended package direction grounded in current docs, code, and tests.

### 3. Work-package definition

5. **`generate-requirements.prompt.md`**
   - Creates a work-package `requirements.md` under `./docs/00x-work/`.
   - Use when you are turning one candidate package into a concrete unit of work.
   - Produces detailed `FRx`, `NFx`, and `SRx` requirements for that package.

6. **`generate-technical-spec.prompt.md`**
   - Creates `technical-specification.md` for the same work package.
   - Use after the work-package requirements are stable enough.
   - Produces the implementable technical design, traceability, testing strategy, configuration expectations, and solution structure.

7. **`generate-delivery-plan.prompt.md`**
   - Creates `plans/001-delivery-plan.md` for the work package.
   - Use after requirements and technical specification are in place.
   - Produces the incremental execution plan, validation gates, rollback guidance, and work-item ordering.

### 4. Delivery

8. **`execute-delivery.prompt.md`**
   - Executes the numbered delivery plan.
   - Use when the package is ready for implementation.
   - Implements the planned work in sequence, runs build and test gates, updates plan checkboxes, and aligns wiki documentation before completion.

### 5. General delivery review

9. **`review-work-package-delivery.prompt.md`**
   - Reviews the completed or in-progress work package for overall delivery conformance.
   - Use after delivery when you want one package-level validation pass before specialist hardening reviews.
   - Produces a numbered review report covering requirements alignment, plan conformance, validation evidence, repository-instruction fit, and wiki alignment.

### 6. Test review and mitigation loop

10. **`review-test-approach.prompt.md`**
   - Reviews the completed or in-progress work package from a testing perspective.
   - Use after delivery when you want an independent view of test coverage quality.
   - Produces a numbered test review report identifying missing coverage, weak tests, and risk areas.

11. **`plan-test-mitigation.prompt.md`**
   - Creates a mitigation plan from the test review report.
   - Use when the test review identifies meaningful gaps.
   - Produces a numbered mitigation plan with explicit work items, finding references, validation commands, and rollback guidance.

12. **`execute-test-mitigation.prompt.md`**
   - Executes the test mitigation plan.
   - Use after the mitigation plan is approved or ready to act on.
   - Strengthens tests, makes only minimal supporting code changes when required, validates the result, and updates the mitigation plan as work completes.

### 7. Refactoring review and mitigation loop

13. **`review-refactoring-approach.prompt.md`**
    - Reviews the completed or in-progress work package from a maintainability and structure perspective.
    - Use after delivery, or after test hardening, when you want an independent refactoring assessment.
    - Produces a numbered refactoring review report covering duplication, complexity, coupling, cohesion, and design risks.

14. **`plan-refactoring-mitigation.prompt.md`**
    - Creates a mitigation plan from the refactoring review report.
    - Use when the refactoring review identifies issues worth addressing.
    - Produces a numbered mitigation plan with prioritized refactoring work, safety boundaries, validation gates, and rollback guidance.

15. **`execute-refactoring-mitigation.prompt.md`**
    - Executes the refactoring mitigation plan.
    - Use after the mitigation plan is ready for implementation.
    - Applies the planned refactors, preserves expected behavior, updates tests and docs where needed, and marks mitigation items complete only after validation.

## Supporting and utility prompts

These prompts are useful alongside the primary workflow, but they are not normally part of the main package-delivery sequence.

### Prompt and instruction authoring

- **`generate-copilot-prompt.prompt.md`**
  - Supports maintenance of the existing prompt library and can help draft a reusable `*.prompt.md` file from the repository prompt template when a prompt-format artifact is explicitly required.
  - For new Copilot artifacts, prefer creating an Agent Skill under `./.github/skills/` with `SKILL.md`, in line with `./.github/copilot-instructions.md`.
  - Use this prompt only when extending or reorganizing the current prompt library, or when a prompt file is intentionally needed rather than a skill.
  - Best used outside the normal product-delivery flow.

- **`create-instructions.prompt.md`**
  - Creates a new `*.instructions.md` file for repository-specific coding or documentation rules.
  - Use when you need a new scoped rule set for a language, framework, folder, or file type.
  - Best used before or alongside implementation work when standards need to be formalized.

### Repository documentation reconstruction

- **`reverse-doc-suite.prompt.md`**
  - Reverse-engineers the repository into a full `/docs` documentation suite.
  - Use when the code exists but documentation is missing, stale, or incomplete.
  - This is an alternative documentation workflow, not a normal step in the work-package lifecycle.

## Recommended end-to-end usage patterns

### Pattern A: New feature or package from scratch

1. `generate-business-requirements`
2. `generate-systems-analysis`
3. `start-work-package`
4. `research-work-package`
5. `generate-requirements`
6. `generate-technical-spec`
7. `generate-delivery-plan`
8. `execute-delivery`
9. `review-work-package-delivery`
10. `review-test-approach`
11. `plan-test-mitigation`
12. `execute-test-mitigation`
13. `review-refactoring-approach`
14. `plan-refactoring-mitigation`
15. `execute-refactoring-mitigation`

### Pattern B: Existing package needs only delivery

1. `generate-delivery-plan` if no delivery plan exists
2. `execute-delivery`
3. optional `review-work-package-delivery`
4. optional review and mitigation loops

### Pattern C: Existing package needs quality hardening only

1. `review-work-package-delivery`
2. `review-test-approach`
3. `plan-test-mitigation`
4. `execute-test-mitigation`
5. `review-refactoring-approach`
6. `plan-refactoring-mitigation`
7. `execute-refactoring-mitigation`

### Pattern D: Repository standards tooling work

1. `create-instructions` when a new rule set is needed
2. `generate-copilot-prompt` when a new reusable prompt is needed
3. `reverse-doc-suite` when the repo documentation set needs to be reconstructed from code

## Prompt quick reference

| Prompt file | Primary output | When to use | Typical predecessor | Typical successor |
| --- | --- | --- | --- | --- |
| `generate-business-requirements.prompt.md` | `docs/business-requirements.md` | Start project-level discovery | Idea | `generate-systems-analysis` |
| `generate-systems-analysis.prompt.md` | `docs/systems-analysis.md` | Translate business intent into system analysis | `generate-business-requirements` | `start-work-package` |
| `start-work-package.prompt.md` | bootstrap brief | Start a repo-native work package | `generate-systems-analysis` | `research-work-package` |
| `research-work-package.prompt.md` | numbered work-package research artifact | Research one proposed work package | `start-work-package` | `generate-requirements` |
| `generate-requirements.prompt.md` | `docs/00x-work/requirements.md` | Define one work package | `research-work-package` or `generate-systems-analysis` | `generate-technical-spec` |
| `generate-technical-spec.prompt.md` | `docs/00x-work/technical-specification.md` | Define technical design for the package | `generate-requirements` | `generate-delivery-plan` |
| `generate-delivery-plan.prompt.md` | `docs/00x-work/plans/001-delivery-plan.md` | Create implementation plan | `generate-technical-spec` | `execute-delivery` |
| `execute-delivery.prompt.md` | Implemented code + updated plan | Deliver the package | `generate-delivery-plan` | `review-work-package-delivery` or review prompts |
| `review-work-package-delivery.prompt.md` | numbered delivery review report | Assess overall delivery conformance | `execute-delivery` | specialist review prompts |
| `review-test-approach.prompt.md` | numbered test review report | Assess test quality | `review-work-package-delivery` or `execute-delivery` | `plan-test-mitigation` |
| `plan-test-mitigation.prompt.md` | numbered test mitigation plan | Turn test findings into work | `review-test-approach` | `execute-test-mitigation` |
| `execute-test-mitigation.prompt.md` | hardened tests + updated mitigation plan | Implement test improvements | `plan-test-mitigation` | refactoring review or completion |
| `review-refactoring-approach.prompt.md` | numbered refactoring review report | Assess maintainability | `execute-delivery` or `execute-test-mitigation` | `plan-refactoring-mitigation` |
| `plan-refactoring-mitigation.prompt.md` | numbered refactoring mitigation plan | Turn refactoring findings into work | `review-refactoring-approach` | `execute-refactoring-mitigation` |
| `execute-refactoring-mitigation.prompt.md` | refactored code + updated mitigation plan | Implement maintainability improvements | `plan-refactoring-mitigation` | completion |
| `generate-copilot-prompt.prompt.md` | new `*.prompt.md` file | Add a new reusable prompt | none | none |
| `create-instructions.prompt.md` | new `*.instructions.md` file | Add new scoped repo rules | none | none |
| `reverse-doc-suite.prompt.md` | full `/docs` suite | Reconstruct docs from code | existing repo | docs review or maintenance |

## Practical guidance

### Core versus optional workflow assets

- Treat `start-work-package`, `research-work-package`, the baseline generation prompts, `execute-delivery`, and `review-work-package-delivery` as the core package flow.
- Treat the test and refactoring review loops as downstream hardening stages that you run when package quality or maintainability needs deeper scrutiny.
- Add extra agent specialization only when the core flow still leaves a concrete usability or consistency gap.

- Use `start-work-package` to enter the work-package flow from one consistent repo-native starting point.
- Use `research-work-package` when the package needs evidence-backed discovery before requirements are written.
- Use the **generation prompts** to create baseline package artifacts.
- Use the **execute prompts** to implement validated plans.
- Use `review-work-package-delivery` for the general package-level conformance review after delivery.
- Use the specialist **review prompts** to inspect testing and maintainability quality after the general delivery review.
- Use the **plan mitigation prompts** to turn review findings into explicit follow-up work.
- Use the **utility prompts** only when you are maintaining the prompt library, repository instructions, or documentation estate itself.

In short: bootstrap the package, research the package, define the work, design the work, plan the work, execute the work, then review and harden the work.
