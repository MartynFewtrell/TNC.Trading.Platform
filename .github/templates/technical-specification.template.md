# Technical Specification Template

> Use this template to describe *how* the approved requirements will be implemented. This document should trace back to `FRx`, `NFx`, `SRx` (and optional `*Rx`) from `requirements.md` and feed into `delivery-plan.md`.

## 1. Summary

- **Source**: See `requirements.md` for canonical work metadata (work item, owner, dates, links) and identifiers (`FRx/NFx/SRx/...`).
- **Status**: <draft|review|approved>
- **Input**: `requirements.md`
- **Output**: `delivery-plan.md`

## 2. Problem and Context

### 2.1 Problem statement

<Restate the problem in 1-3 sentences.>

### 2.2 Assumptions

- <assumption>

### 2.3 Constraints

- <constraint>

## 3. Proposed Solution

### 3.1 Approach

<High-level technical approach and why this is the right choice.>

### 3.2 Alternatives considered

| Option | Summary | Pros | Cons | Decision rationale |
| ------ | ------- | ---- | ---- | ------------------ |
| A | <approach> | <pros> | <cons> | <why accept/reject> |
| B | <approach> | <pros> | <cons> | <why accept/reject> |

### 3.3 Architecture

<Describe components and boundaries. Include diagrams/links if available.>

- **Components**: <list>
- **Data flows**: <list>
- **Dependencies**: <list>

## 4. Requirements Traceability

Map requirements to implementation details so it's easy to verify coverage.

| Requirement ID | Requirement | Implementation notes | Validation approach |
| -------------- | ----------- | -------------------- | ------------------- |
| FR1 | <summary> | <what will be built/changed> | <tests/verification> |
| NF1 | <summary> | <what ensures this NFR> | <measurement/check> |
| SR1 | <summary> | <how security requirement is met> | <verification> |

Add rows for all `FRx`, `NFx`, `SRx`, `CRx`, `DRx`, `IRx`, `TRx`, `ORx` you intend to implement.

## 5. Detailed Design

Describe the implementation at a level that enables another developer to build it.

### 5.1 Public APIs / Contracts (optional)

| Area | Contract | Example | Notes |
| ---- | -------- | ------- | ----- |
| REST | <method + route> | <request/response> | <auth/versioning> |
| Event | <topic/event> | <schema> | <delivery semantics> |

### 5.2 Data Model (optional)

| Entity/Concept | Fields | Constraints | Notes |
| -------------- | ------ | ----------- | ----- |
| <name> | <fields> | <keys/uniqueness> | <notes> |

### 5.3 Implementation Plan (technical steps)

| Step | Change | Files/Modules | Notes |
| ---- | ------ | ------------- | ----- |
| 1 | <change> | <paths> | <notes> |
| 2 | <change> | <paths> | <notes> |

### 5.4 Error Handling

| Scenario | Expected behavior | Instrumentation |
| -------- | ------------------ | --------------- |
| <error case> | <response/retry/fail> | <logs/metrics> |

### 5.5 Configuration

| Setting | Purpose | Default | Location |
| ------ | ------- | ------- | -------- |
| <name> | <what it controls> | <value> | <file/env/secret store> |

## 6. Security Design

Describe how the solution meets `SRx` requirements.

- **AuthN/AuthZ**: <approach>
- **Secrets**: <approach>
- **Data protection**: <encryption at rest/in transit>
- **Threat model notes**: <abuse cases / mitigations>

## 7. Observability

| Signal | What | Where | Notes |
| ------ | ---- | ----- | ----- |
| Logs | <events> | <sink> | <PII rules> |
| Metrics | <counters/timers> | <sink> | <alerts> |
| Traces | <spans> | <tool> | <sampling> |

## 8. Testing Strategy

| Test type | Coverage | Location | Notes |
| --------- | -------- | -------- | ----- |
| Unit | <what> | <path> | <notes> |
| Integration | <what> | <path> | <notes> |
| E2E | <what> | <path> | <notes> |

## 9. Rollout Plan

| Phase | Action | Success criteria | Rollback |
| ----- | ------ | ---------------- | -------- |
| 1 | <deploy/feature flag> | <signals> | <how to rollback> |

## 10. Open Questions

- <question>

## 11. Appendix (optional)

- <links, diagrams, ADRs, PRs>
