# Delivery Plan Template

> Use this template to plan *when* and *in what increments* the technical specification will be delivered. Each work item should reference relevant `FRx/NFx/SRx/...` and the sections in `technical-specification.md` that implement them.

## Summary

- **Source**: See `requirements.md` for canonical work metadata (work item, owner, dates, links) and scope.
- **Status**: <draft|review|approved|in-progress|done>
- **Inputs**:
  - `requirements.md`
  - `technical-specification.md`

## Description of work

<Describe what will be delivered and the boundaries of this plan. Keep it concrete and implementation-oriented.>

## Delivery approach

- **Delivery model**: <single PR | multiple PRs | feature-flagged rollout>
- **Branching**: <strategy>
- **Dependencies**: <teams/systems>
- **Key risks**: <risk + mitigation>

## Delivery Plan

### Execution gates (required)

Before starting *any* work item, and again before marking a work item as complete, run the build + test suite and resolve any failures.

| Gate | When | Required actions | If failures occur |
| --- | --- | --- | --- |
| Baseline | Before starting any work item | Run build and all tests listed in **Cross-cutting validation** | Fix or revert until build/tests are green before continuing |
| Pre-completion | Before completing a work item | Re-run build and all tests listed in **Cross-cutting validation** | Fix failures before marking the work item complete |

### Planned work items

The final plan may include one or more work items.

| Work item | Description | Traceability (requirements) | Traceability (spec sections) | Dependencies | Validation | Rollback/Backout | User instructions |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Work Item 1: <brief title> | <what will be implemented> | <FRx/NFx/SRx/...> | <links/section numbers> | <dependencies and sequencing> | <build/test/verification criteria> | <how to revert if needed> | <instructions for the user/operator/reviewer> |

### Work Item 1 details

- [ ] Work Item 1: [Brief title]
  - [ ] Build and test baseline established
  - [ ] Task 1: [Detailed explanation of what needs to be implemented]
    - [ ] Step 1: [Description]
    - [ ] Step 2: [Description]
    - [ ] Step N: [Description]
  - [ ] Task 2: [Detailed explanation...]
    - [ ] Step 1: [Description]
    - [ ] Step 2: [Description]
    - [ ] Step N: [Description]
  - [ ] Task N: [Detailed explanation...]
  - [ ] Build and test validation
  
  - **Files**:
    - `path/to/file1.ts`: [Description of changes]
  - **Work Item Dependencies**: [Dependencies and sequencing]
  - **User Instructions**: [Instructions for User]

### Work Item N details (copy/paste)

Copy the **Work Item 1 details** section for each additional work item.

## Cross-cutting validation

- **Build**: <command(s)>
- **Unit tests**: <command(s)>
- **Integration tests**: <command(s)>
- **Manual checks**: <if any>
- **Security checks**: <linting/scanning/review requirements>

## Acceptance checklist

- [ ] All referenced `FRx` requirements are implemented and validated.
- [ ] All referenced `NFx` requirements have measurements or checks.
- [ ] All referenced `SRx` security requirements are implemented and validated.
- [ ] Docs updated under `./docs/00x-work/`.
- [ ] Rollback/backout plan documented for each work item.

## Notes

- <notes>
