# Business Requirements Template

> Use this template to capture the business context and high-level requirements for the overall project. This document describes *what* should be built and *why* (not the technical solution). It is the foundation for systems analysis and work packages under `./docs/00x-work/`.

## 1. Summary

- **Project**: <short name>
- **Document**: `./docs/business-requirements.md`
- **Owner**: <name/team>
- **Date**: <yyyy-mm-dd>
- **Status**: <draft|review|approved>
- **Outputs**:
  - `./docs/systems-analysis.md`
- Work packages under `./docs/00x-work/` with `requirements.md`, `technical-specification.md`, and numbered plan files under `plans/`

### 1.1 Links

| Document | Path |
| --- | --- |
| Business requirements | `./docs/business-requirements.md` |
| Systems analysis | `./docs/systems-analysis.md` |

## 2. Context

### 2.1 Background

<Why this project exists. What problem or opportunity is being addressed?>

### 2.2 Current state

<What happens today and why that is insufficient?>

### 2.3 Desired outcomes

<What outcomes the business expects to achieve?>

## 3. Goals and Success Measures

### 3.1 Goals

- <goal>

### 3.2 Success measures

| ID | Measure | Target | How it will be assessed | Notes |
| --- | --- | --- | --- | --- |
| SM1 | <measure> | <target> | <assessment> | <optional> |

## 4. Scope

### 4.1 In scope

- <in-scope item>

### 4.2 Out of scope

- <out-of-scope item>

## 5. Stakeholders and Users

### 5.1 Stakeholders

| Name/Group | Role | Responsibilities | Decision rights | Notes |
| --- | --- | --- | --- | --- |
| <name> | <role> | <responsibilities> | <what they approve/own> | <optional> |

### 5.2 Users

| User group | Description | Goals | Notes |
| --- | --- | --- | --- |
| <group> | <description> | <goals> | <optional> |

## 6. Business Requirements

Use `BR1`, `BR2`, ... for business requirements. These should be written at the capability/outcome level and avoid prescribing a technical implementation.

| ID | Requirement (capability/outcome) | Rationale | Priority | Acceptance criteria | Notes/Constraints |
| --- | --- | --- | --- | --- | --- |
| BR1 | <requirement> | <why> | <Must|Should|Could|Won't> | <testable statement(s)> | <optional> |

## 7. Constraints and Policies

### 7.1 Constraints

- <constraint>

### 7.2 Policies and compliance (if any)

- <policy/compliance requirement>

## 8. Assumptions, Risks, and Dependencies

### 8.1 Assumptions

- <assumption>

### 8.2 Risks

- <risk>
  - **Mitigation**: <mitigation>

### 8.3 Dependencies

- <dependency>

## 9. Work Package Outline (optional)

If you already know likely work packages, outline them here. Keep this section business-oriented; technical design belongs in each work package’s `technical-specification.md`.

| Candidate work package | Summary | Related BR IDs | Notes |
| --- | --- | --- | --- |
| <00x-name> | <summary> | <BRx list> | <optional> |

## 10. Open Questions

- <question>

## 11. Glossary (optional)

| Term | Definition |
| --- | --- |
| <term> | <definition> |
