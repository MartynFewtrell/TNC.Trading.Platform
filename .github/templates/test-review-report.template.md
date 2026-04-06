# Work Package Test Review Report Template

> Use this template to review a work package under `./docs/00x-work/` and assess whether its current automated tests provide sufficient coverage, strength, and confidence. Ground findings in repository evidence and separate confirmed gaps from assumptions.

## Review scope

- **Work package**: `<./docs/00x-work/...>`
- **Review depth**: `<quick|standard|deep>`
- **Reviewer perspective**: `Senior Test Architect`
- **Reviewed artifacts**:
  - `docs/...`
  - `src/...`
  - `test/...`

## Executive summary

- **Overall test confidence**: `<high|medium|low>`
- **Overall coverage assessment**: `<strong|partial|weak>`
- **Top concerns**:
  1. `<concern>`
  2. `<concern>`
  3. `<concern>`

## Requirement coverage matrix

| Requirement / area | Existing coverage | Evidence | Gap assessment | Recommendation |
| --- | --- | --- | --- | --- |
| `FR1` | Covered / Partial / Missing | `test/...` | `<gap>` | `<recommendation>` |

## Existing test strengths

- `<strength>`

## Gaps in testing

### Missing coverage

- `<gap>`

### Weak or fragile tests

- `<issue>`

### Risks not adequately tested

- `<risk>`

## Recommendations to strengthen existing tests

1. `<specific improvement>`
2. `<specific improvement>`
3. `<specific improvement>`

## Recommendations for new tests

| Priority | Area | Test level | Recommendation | Reason |
| --- | --- | --- | --- | --- |
| High | `<area>` | Unit / Integration / E2E / Functional | `<recommendation>` | `<reason>` |

## Hardening recommendations

- `<determinism, data isolation, cleanup, traceability, resilience, or CI stability recommendation>`

## Assumptions and missing information

- `<assumption or missing artifact>`

## Suggested next steps

1. `<next action>`
2. `<next action>`
3. `<next action>`
