# Refactor App Requirements

This document defines the work-package requirements for refactoring the main application surfaces so the platform remains easier to understand, safer to change, and more consistent for later operator-facing delivery work.

## 1. Summary

- **Work item**: Refactor App
- **Work folder**: `./docs/006-refactor-app/`
- **Business requirements**: `../business-requirements.md`
- **Owner**: TNC Trading
- **Date**: 2026-06-21
- **Status**: draft
- **Outputs**:
  - `technical-specification.md`
  - `plans/001-delivery-plan.md`

### 1.1 Links

| Document | Path |
| --- | --- |
| Business requirements | `../business-requirements.md` |
| Systems analysis | `../systems-analysis.md` |
| Requirements | `requirements.md` |
| Technical specification | `technical-specification.md` |
| Initial delivery plan | `plans/001-delivery-plan.md` |

## 2. Context

### 2.1 Background

The platform has already established baseline delivery slices for project scaffolding, environment and authentication foundations, UI updates, and AppHost refactoring. The next maintainability-focused slice is to refactor the application-facing code paths so operator-facing behavior remains stable while internal structure becomes easier to review, extend, and validate.

This work package is intended to reduce application-level change risk before later capability packages expand trading, monitoring, and operational behavior.

### 2.2 Traceability to systems analysis

This work package supports later delivery of operator-facing and monitoring capabilities described in `../systems-analysis.md`, especially `UC9`, `BR10`, `BR11`, and `NFR2`, by improving the maintainability and change safety of the application surfaces that present and orchestrate those behaviors.

## 3. Scope

### 3.1 In scope

- Refactoring application-facing code paths into clearer, more cohesive responsibilities.
- Preserving the current delivered operator behavior unless an explicit defect must be corrected.
- Improving structure where UI, API, and shared application concerns are currently harder to change safely.
- Updating work-package and wiki documentation affected by the refactor when implementation is completed.

### 3.2 Out of scope

- Introducing new business capabilities that are not required by the refactor.
- Replacing the established local authentication, authorization, or AppHost model.
- Broad architectural changes unrelated to the main application surfaces under review.
- Reworking stable code solely for stylistic preference without a maintainability or change-safety benefit.

## 4. Functional Requirements

| ID | Requirement | Rationale | Acceptance criteria | Notes/Constraints |
| --- | --- | --- | --- | --- |
| FR1 | The refactor must improve cohesion in the main application surfaces without changing intended operator-facing behavior. | The work package is intended to reduce change risk, not to introduce new runtime capability. | The delivered application structure separates responsibilities more clearly while preserving the current operator-visible behavior. | Keep changes as small and behavior-preserving as practical. |
| FR2 | Application-facing responsibilities must remain explicit at their owning boundaries. | Clear ownership reduces accidental coupling and makes later feature work easier to review. | Routing, UI composition, service orchestration, and shared helpers are easier to locate and reason about after the refactor. | Prefer moving behavior closer to its owning abstraction rather than adding more indirection. |
| FR3 | The refactor must preserve current authentication, authorization, and configuration flows across the touched application surfaces. | Maintainability work must not weaken runtime protections or operator workflows. | Existing protected flows and configuration-driven behavior remain functionally equivalent after the refactor. | This includes the local Keycloak-backed operator experience where relevant. |
| FR4 | Documentation for the work package must remain self-contained and ready to track implementation and validation. | The repository relies on work-package artifacts to document delivery intent and evidence. | `requirements.md`, `technical-specification.md`, and `plans/001-delivery-plan.md` exist and remain aligned. | Update affected `docs/wiki/` pages before completed implementation work is considered done. |

## 5. Non-Functional Requirements

| ID | Category | Requirement | Measure/Target | Acceptance criteria |
| --- | --- | ----------- | -------------- | ------------------- |
| NF1 | Maintainability | The touched application surfaces must become easier to review and change. | Clearer ownership and less mixed responsibility in the affected code paths. | The refactor yields smaller, more cohesive units with easier traceability from boundary to behavior. |
| NF2 | Reliability | The refactor must preserve current runtime behavior. | No intentional regression in application startup, routing, auth behavior, or operator workflows for the touched slice. | Focused validation confirms that current behavior remains intact. |
| NF3 | Testability | The touched application slice must remain or become easier to validate with focused tests. | Narrower validation can be applied to the touched slice after the refactor. | Tests or equivalent focused validation demonstrate preserved behavior. |

## 6. Testing Requirements

| ID | Requirement | Acceptance criteria | Notes |
| --- | --- | ------------------- | ----- |
| TR1 | The refactor must be validated with the narrowest available checks for the touched application slice. | Focused automated or executable validation covers the changed boundaries. | Prefer behavior-scoped validation before broader solution checks. |
| TR2 | Existing operator-facing behavior in the touched slice must remain covered after the refactor. | Validation confirms no unintended regression in routing, auth behavior, or application flow for the affected surfaces. | Update tests only where required to reflect structural changes, not to loosen expectations. |

## 7. Operational Requirements

| ID | Requirement | Acceptance criteria | Notes |
| --- | --- | ------------------- | ----- |
| OR1 | The refactor intent and delivered shape must be documented. | Work-package documents describe the intended refactor and any completed implementation updates the relevant wiki pages. | Applies when implementation is carried out. |

## 8. Assumptions, Risks, and Dependencies

### 8.1 Assumptions

- The current delivered application behavior is the baseline to preserve.
- The main opportunity is structural simplification rather than capability expansion.
- Focused validation exists for at least part of the touched slice.

### 8.2 Risks

- Application refactoring can accidentally move behavior across boundaries and create subtle regressions.
  - **Mitigation**: keep changes incremental and validate each slice at the closest executable boundary.
- Structural cleanup can expand in scope if ownership boundaries are not kept explicit.
  - **Mitigation**: constrain the package to the main application surfaces and preserve unrelated code paths.

### 8.3 Dependencies

- `../business-requirements.md`
- `../systems-analysis.md`
- `src/TNC.Trading.Platform.Api/`
- `src/TNC.Trading.Platform.Web/`
- `src/TNC.Trading.Platform.Application/`
