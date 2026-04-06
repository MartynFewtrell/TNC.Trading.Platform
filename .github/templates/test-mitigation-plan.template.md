# Work Package Test Mitigation Plan Template

> Use this template to plan how the issues identified in a work-package test review will be mitigated. Keep the plan actionable, traceable to the review findings, and aligned to the repository testing approach.

## Summary

- **Source review**: `<./docs/00x-work/work-package-test-review-report.md>`
- **Work package**: `<./docs/00x-work/>`
- **Status**: `<draft|review|approved|in-progress|done>`
- **Inputs**:
  - `work-package-test-review-report.md`
  - `requirements.md`
  - `technical-specification.md`
  - `delivery-plan.md`

## Description of work

<Describe what this mitigation plan will change in the test suite and any supporting implementation or documentation needed to close the review findings. Keep the scope specific to the review issues being addressed.>

## Mitigation approach

- **Delivery model**: `<single PR | multiple PRs | phased hardening>`
- **Branching**: `<strategy>`
- **Dependencies**: `<projects, services, infrastructure, or data needed>`
- **Key risks**:
  - `<risk + mitigation>`

## Review findings to address

| Finding ID | Review area | Review assessment | Source evidence | Planned mitigation |
| --- | --- | --- | --- | --- |
| `F1` | `<FRx / area>` | Missing / Partial / Weak | `<path or finding summary>` | `<what will be done>` |

## Mitigation Plan

### Execution gates (required)

Before starting *any* mitigation work item, and again before marking a work item as complete, run the build + test suite and resolve any failures.

| Gate | When | Required actions | If failures occur |
| --- | --- | --- | --- |
| Baseline | Before starting any work item | Run build and all tests listed in **Cross-cutting validation** | Fix or revert until build/tests are green before continuing |
| Pre-completion | Before completing a work item | Re-run build and all tests listed in **Cross-cutting validation** | Fix failures before marking the work item complete |

### Planned work items

| Work item | Description | Traceability (review findings) | Traceability (requirements) | Dependencies | Validation | Rollback/Backout | User instructions |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Work Item 1: `<brief title>` | `<what will be strengthened or added>` | `<F1, F2>` | `<FRx/NFx/SRx/TRx/...>` | `<dependencies and sequencing>` | `<build/test/verification criteria>` | `<how to revert if needed>` | `<instructions for reviewer/operator>` |

### Work Item 1 details

- [ ] Work Item 1: [Brief title]
  - [ ] Build and test baseline established
  - [ ] Task 1: [Strengthen or add specific tests]
    - [ ] Step 1: [Description]
    - [ ] Step 2: [Description]
    - [ ] Step N: [Description]
  - [ ] Task 2: [Make supporting changes if needed]
    - [ ] Step 1: [Description]
    - [ ] Step 2: [Description]
    - [ ] Step N: [Description]
  - [ ] Task N: [Additional mitigation task]
  - [ ] Build and test validation

  - **Files**:
    - `path/to/test-file.cs`: [Description of changes]
    - `path/to/supporting-file.cs`: [Description of changes]
  - **Work Item Dependencies**: [Dependencies and sequencing]
  - **User Instructions**: [Instructions for user/reviewer]

### Work Item N details (copy/paste)

Copy the **Work Item 1 details** section for each additional work item.

## Cross-cutting validation

- **Build**: `<command(s)>`
- **Unit tests**: `<command(s)>`
- **Integration tests**: `<command(s)>`
- **Functional tests**: `<command(s)>`
- **E2E tests**: `<command(s)>`
- **Manual checks**:
  - `<manual verification if needed>`
- **Security checks**:
  - `<secret redaction, auth safety, notification safety, or similar checks>`

## Acceptance checklist

- [ ] Every planned mitigation maps back to one or more findings in `work-package-test-review-report.md`.
- [ ] High-priority missing or weak coverage is addressed before lower-priority improvements.
- [ ] The plan prefers lower-level automated tests before higher-level tests where practical.
- [ ] Required validation steps are defined for each work item.
- [ ] Docs updated under `./docs/00x-work/`.
- [ ] Rollback/backout plan documented for each work item.

## Notes

- `<notes>`
