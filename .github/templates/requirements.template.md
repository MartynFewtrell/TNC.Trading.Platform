# Requirements Template

> Use this template to capture *what* needs to be delivered. The output of this document is a set of `FRx`, `NFx`, `SRx` (and optional `*Rx`) requirements used by `technical-specification.md` and `delivery-plan.md`.

## 1. Summary

- **Work item**: <short name>
- **Work folder**: `./docs/00x-work/`
- **Owner**: <name/team>
- **Date**: <yyyy-mm-dd>
- **Status**: <draft|review|approved>
- **Outputs**:
  - `technical-specification.md`
  - `delivery-plan.md`

### 1.1 Links

| Document | Path |
| --- | --- |
| Requirements | `requirements.md` |
| Technical specification | `technical-specification.md` |
| Delivery plan | `delivery-plan.md` |

## 2. Context

### 2.1 Background

<Why this work is needed. What problem is being solved?>

## 3. Scope

### 3.1 In scope

- <in-scope item>

### 3.2 Out of scope

- <out-of-scope item>

## 4. Functional Requirements

Use `FR1`, `FR2`, ... for functional requirements.

| ID  | Requirement | Rationale | Acceptance criteria | Notes/Constraints |
| --- | ----------- | --------- | ------------------- | ---------------- |
| FR1 | <requirement> | <why> | <testable statement(s)> | <optional> |
| FR2 | <requirement> | <why> | <testable statement(s)> | <optional> |

## 5. Non-Functional Requirements

Use `NF1`, `NF2`, ... for non-functional requirements.

| ID  | Category | Requirement | Measure/Target | Acceptance criteria |
| --- | -------- | ----------- | -------------- | ------------------- |
| NF1 | Performance | <requirement> | <e.g. p95 latency <= 200ms> | <testable statement(s)> |
| NF2 | Reliability/Availability | <requirement> | <e.g. 99.9% uptime> | <testable statement(s)> |
| NF3 | Maintainability/Supportability | <requirement> | <e.g. documented runbook + alarms> | <testable statement(s)> |
| NF4 | Observability | <requirement> | <logs/metrics/traces> | <testable statement(s)> |
| NF5 | Usability/Accessibility | <requirement> | <optional> | <testable statement(s)> |

## 6. Security Requirements

Use `SR1`, `SR2`, ... for security requirements.

| ID  | Category | Requirement | Acceptance criteria |
| --- | -------- | ----------- | ------------------- |
| SR1 | Authentication/Authorization | <requirement> | <testable statement(s)> |
| SR2 | Data Protection | <requirement> | <testable statement(s)> |
| SR3 | Secrets/Key Management | <requirement> | <testable statement(s)> |
| SR4 | Threats/Abuse Cases | <requirement> | <testable statement(s)> |


## 7. Data Requirements (optional)

Use `DR1`, `DR2`, ... for data requirements.

| ID  | Requirement | Source | Retention | Acceptance criteria | Notes |
| --- | ----------- | ------ | --------- | ------------------- | ----- |
| DR1 | <requirement> | <system/user> | <duration> | <testable statement(s)> | <optional> |

## 8. Interfaces and Integration Requirements (optional)

Use `IR1`, `IR2`, ... for integration requirements.

| ID  | Requirement | System | Contract | Acceptance criteria | Notes |
| --- | ----------- | ------ | -------- | ------------------- | ----- |
| IR1 | <requirement> | <name> | <API/event/file> | <testable statement(s)> | <optional> |

## 9. Testing Requirements

Use `TR1`, `TR2`, ... for testing requirements.

| ID  | Requirement | Acceptance criteria | Notes |
| --- | ----------- | ------------------- | ----- |
| TR1 | <requirement> | <testable statement(s)> | <optional> |

## 10. Operational Requirements (optional)

Use `OR1`, `OR2`, ... for operational requirements.

| ID  | Requirement | Acceptance criteria | Notes |
| --- | ----------- | ------------------- | ----- |
| OR1 | <requirement> | <testable statement(s)> | <optional> |

## 12. Assumptions, Risks, and Dependencies

### 12.1 Assumptions

- <assumption>

### 12.2 Risks

- <risk>
  - **Mitigation**: <mitigation>

### 12.3 Dependencies

- <dependency>

## 13. Open Questions

- <question>

## 14. Appendix (optional)

- <links, diagrams, PRDs, prior decisions>
