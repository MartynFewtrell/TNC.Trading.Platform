# Work Package Refactoring Review Report Template

> Use this template to review a work package under `./docs/00x-work/` and assess whether its current implementation would benefit from targeted refactoring. Ground findings in repository evidence, distinguish confirmed issues from assumptions, and keep recommendations specific enough to feed directly into follow-on planning.

## Review scope

- **Work package**: `<./docs/00x-work/...>`
- **Review depth**: `<quick|standard|deep>`
- **Reviewer perspective**: `Senior Refactoring Architect`
- **Reviewed artifacts**:
  - `docs/...`
  - `src/...`
  - `test/...`

## Executive summary

- **Overall refactoring urgency**: `<high|medium|low>`
- **Overall maintainability assessment**: `<strong|mixed|weak>`
- **Top concerns**:
  1. `<concern>`
  2. `<concern>`
  3. `<concern>`

## Refactoring opportunity matrix

| Area / requirement | Current implementation evidence | Issue type | Impact assessment | Recommendation |
| --- | --- | --- | --- | --- |
| `FR1` / `<area>` | `src/...` | Duplication / Complexity / Coupling / Naming / Boundary leakage / Testability / Dead code | `<impact>` | `<recommendation>` |

## Existing implementation strengths

- `<strength>`

## Refactoring findings

### Structural and architectural issues

- `<finding>`

### Duplication and cohesion issues

- `<finding>`

### Complexity and readability issues

- `<finding>`

### Testability and regression-risk issues

- `<finding>`

## Recommendations to improve the current design

1. `<specific improvement>`
2. `<specific improvement>`
3. `<specific improvement>`

## Recommended refactoring work items

| Priority | Area | Refactoring type | Recommendation | Expected benefit | Risk / caution |
| --- | --- | --- | --- | --- | --- |
| High | `<area>` | Extract method / Extract component / Split class / Move responsibility / Introduce abstraction / Remove duplication / Simplify flow | `<recommendation>` | `<benefit>` | `<risk>` |

## Validation and safety recommendations

- `<build, tests, behavior-preservation, rollout, or regression-safety recommendation>`

## Assumptions and missing information

- `<assumption or missing artifact>`

## Suggested next steps

1. `<next action>`
2. `<next action>`
3. `<next action>`
