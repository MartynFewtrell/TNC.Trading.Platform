# IG Login Delivery Plan

This plan describes how work package `006-ig-login` will be delivered so the platform can establish `IG` login, expose broker-auth status, detect session loss, and recover session continuity safely.

## 1. Summary

- **Source**: See `../requirements.md` for the canonical work metadata and requirement identifiers. See `../technical-specification.md` for the implementation design.
- **Status**: draft
- **Inputs**:
  - `../../business-requirements.md`
  - `../requirements.md`
  - `../technical-specification.md`

## 2. Description of work

This plan delivers the focused broker-auth slice defined by work package `006-ig-login`. The work covers configuration-backed `IG` login, current broker-auth state projection, secret-safe recording of notable authentication and session transitions, operator-visible status, safe blocking of `IG`-dependent actions when no working session exists, and the validation needed to demonstrate successful login, failure detection, recovery, and forbidden live-path blocking.

## 3. Delivery approach

- **Delivery model**: single PR
- **Branching**: implement on `006-ig-login` and keep the branch buildable throughout delivery
- **Dependencies**:
  - `IG` authentication and session capabilities
  - existing platform configuration for broker-auth settings
  - existing operator-facing API and UI surfaces
  - existing persistence for operational records
- **Key risks**:
  - login failure classification could be too coarse and hide the real broker-auth condition
    - **Mitigation**: keep explicit broker-auth state transitions and cover them with focused tests
  - secret material could leak through diagnostics or event recording
    - **Mitigation**: centralize sanitization and inspect generated outputs during validation
  - forbidden live-path handling could regress current safety expectations
    - **Mitigation**: keep explicit environment guards and add targeted blocked-path validation

## 4. Delivery Plan

### 4.1 Execution gates

Before starting any work item, and again before marking a work item as complete, run the build and relevant automated tests and resolve any failures.

| Gate | When | Required actions | If failures occur |
| --- | --- | --- | --- |
| Baseline | Before starting any work item | Run build and the relevant automated tests listed in **Cross-cutting validation** | Fix or revert until validation is green before continuing |
| Pre-completion | Before completing a work item | Re-run build and the relevant automated tests listed in **Cross-cutting validation** | Fix failures before marking the work item complete |

### 4.2 Planned work items

| Work item | Description | Traceability | Dependencies | Validation | Rollback or Backout | User instructions |
| --- | --- | --- | --- | --- | --- | --- |
| Work Item 1: Establish broker-auth contracts and state | Add `IG` login contracts, broker-auth state projection, and secret-safe auth/session recording | `FR1`, `FR2`, `FR5`, `NF2`, `NF3`, `SR2`, `OR1`, `OR2` | Existing configuration and persistence foundations | `dotnet build`; targeted unit and integration tests for contracts, sanitization, and state visibility | Revert the added broker-auth contracts and state projection changes | Review that the delivered state model exposes only non-secret broker-auth information |
| Work Item 2: Implement login, detection, and recovery flow | Implement `IG` login, invalid-session detection, safe recovery, forbidden live-path blocking, and dependent-action blocking | `FR1`, `FR3`, `FR4`, `FR6`, `FR7`, `NF1`, `SR1`, `SR3`, `IR1` | Work Item 1 | `dotnet build`; targeted integration and functional tests for login, failure detection, blocked live paths, and recovery | Revert the workflow changes and restore the prior broker-auth behavior | Verify that forbidden live paths remain blocked and that dependent actions stay blocked when the session is unavailable |
| Work Item 3: Align operator visibility and validation | Expose the final broker-auth status through operator-facing surfaces, add requirement-driven tests, and update affected docs if behavior or workflow changes | `FR2`, `FR7`, `TR1` through `TR6`, `OR1`, `OR2` | Work Items 1 and 2 | `dotnet build`; relevant automated tests; documentation review | Revert UI, API, test, and documentation changes together if the delivered behavior is not ready | Confirm the operator-facing status reflects the delivered broker-auth states clearly |

### 4.3 Work Item 1 details

- [ ] Work Item 1: Establish broker-auth contracts and state
  - [ ] Build and test baseline established
  - [ ] Task 1: Add `IG` auth request and response contracts
  - [ ] Task 2: Add broker-auth state projection
  - [ ] Task 3: Add secret-safe auth and session-transition recording
  - [ ] Task 4: Add targeted validation for contracts and sanitization
  - [ ] Build and test validation

### 4.4 Work Item 2 details

- [ ] Work Item 2: Implement login, detection, and recovery flow
  - [ ] Build and test baseline established
  - [ ] Task 1: Implement `IG` login workflow for the supported environment
  - [ ] Task 2: Detect invalid, expired, or unusable sessions
  - [ ] Task 3: Restore session continuity safely
  - [ ] Task 4: Block forbidden live-login paths and dependent actions without a working session
  - [ ] Task 5: Add focused integration and functional validation
  - [ ] Build and test validation

### 4.5 Work Item 3 details

- [ ] Work Item 3: Align operator visibility and validation
  - [ ] Build and test baseline established
  - [ ] Task 1: Expose broker-auth status through operator-facing API and UI surfaces
  - [ ] Task 2: Add or update requirement-driven automated tests
  - [ ] Task 3: Update affected `docs/wiki/` pages if implementation changes delivered behavior or workflow
  - [ ] Task 4: Re-run final validation
  - [ ] Build and test validation

## 5. Cross-cutting validation

- **Build**: `dotnet build`
- **Automated tests**:
  - targeted unit tests for broker-auth result classification and sanitization
  - targeted integration tests for successful login, invalid-session detection, recovery, and blocked live paths
  - targeted functional tests for operator-visible broker-auth state and blocked dependent actions
- **Manual checks**:
  - review the operator-facing status surface and confirm the current broker-auth state is understandable
  - inspect representative records or logs and confirm no credentials or tokens are exposed
  - confirm that forbidden live-login paths remain blocked where the platform environment requires it
- **Security checks**:
  - verify that credentials, tokens, and equivalent secret values are absent from records, logs, notifications, API responses, and UI views
  - verify that dependent actions remain blocked when no working `IG` session exists

## 6. Acceptance checklist

- [ ] The delivery plan aligns with `../requirements.md` and `../technical-specification.md`.
- [ ] Build and relevant automated tests pass before completion.
- [ ] Secret-safe output handling is validated.
- [ ] The final broker-auth state is visible through the operator-facing surface.
- [ ] Affected `docs/wiki/` pages are updated before the work package is considered complete if implementation changes behavior or workflow.
