# IG Login Technical Specification

This document describes how work package `006-ig-login` will be implemented so the platform can establish `IG` login, keep session state observable, detect session loss, and restore broker-auth continuity safely.

## 1. Summary

- **Source**: See `requirements.md` for the canonical work metadata, scope, and requirement identifiers. See `../business-requirements.md` and `../systems-analysis.md` for the project-level context.
- **Status**: draft
- **Input**: `requirements.md`, `../business-requirements.md`, and `../systems-analysis.md`
- **Output**: `plans/001-delivery-plan.md`

## 2. Problem and Context

### 2.1 Problem statement

Later platform capabilities depend on a working and observable `IG` login state. The platform therefore needs a focused implementation slice that can authenticate to `IG`, project the resulting broker-auth state for operator review, detect session loss conditions, preserve safe behavior when the session is unavailable, and recover session continuity without exposing secrets.

### 2.2 Assumptions

- The current solution already has an operator-facing API and UI that can expose broker-auth state.
- `IG` authentication settings are supplied through the platform's existing configuration approach.
- Session continuity is a prerequisite for later market-data, order, and strategy work packages.
- Current platform-environment safeguards remain in force and must continue to prevent forbidden live login actions.

### 2.3 Constraints

- The implementation must satisfy `FR1` through `FR7`, `NF1` through `NF3`, `SR1` through `SR3`, `IR1` through `IR2`, `TR1` through `TR6`, and `OR1` through `OR2` from `requirements.md`.
- Secrets, credentials, tokens, and equivalent sensitive values must not be written to logs, notifications, records, API responses, or UI views.
- The delivered behavior must preserve current environment restrictions for forbidden live usage.
- Documentation for the work package must remain self-contained within `docs/006-ig-login/` and its `plans/` subfolder.

## 3. Proposed Solution

### 3.1 Approach

Implement `IG` login as a small broker-auth slice that combines four concerns:

1. **Configuration-backed authentication input** for the selected supported environment.
2. **A broker-auth service** that establishes an `IG` session and classifies login or session-failure outcomes.
3. **A session-supervision workflow** that tracks current broker-auth state, detects invalid or expired sessions, and re-authenticates when recovery is possible.
4. **Operator-facing read models** that expose current `IG` login state and blocked reasons for dependent capabilities.

This approach keeps `IG` login concerns isolated from later trading capabilities while still making the resulting session state reusable. The implementation should persist notable broker-auth state transitions as secret-safe operational records and expose a compact current-state projection for runtime review.

### 3.2 Alternatives considered

| Option | Summary | Pros | Cons | Decision rationale |
| --- | --- | --- | --- | --- |
| A | Keep `IG` login embedded inside future market-data or trading flows | Lowest short-term change count | Blurs responsibility boundaries and makes session health harder to observe independently | Rejected because login and session continuity need a reusable foundation |
| B | Deliver `IG` login as a focused broker-auth slice with an explicit state projection | Clear boundary, reusable state, easier validation, and safer downstream integration | Requires a small amount of extra state modeling up front | Accepted because it supports later work cleanly |
| C | Delay session-state visibility until a later operator-console package | Reduces immediate UI work | Leaves core broker-auth state harder to diagnose and review | Rejected because observability is part of the requirement |

### 3.3 Architecture

- **Components**:
  - `IgAuthenticationClient` for broker-auth requests and response classification.
  - `IgSessionSupervisor` for current session establishment, validation, loss detection, and recovery triggers.
  - `IgSessionStateStore` for the current broker-auth projection.
  - `OperationalEventRecorder` for secret-safe auth and session-transition records.
  - operator-facing status API and UI bindings for current broker-auth state.
- **Data flows**:
  - configuration provides the selected supported environment and required credentials.
  - the authentication client submits login requests to `IG` and returns sanitized results.
  - the session supervisor updates the current state projection and records notable transitions.
  - operator-facing endpoints and views read the current state projection and blocked reasons.
- **Dependencies**:
  - `IG` authentication and session APIs.
  - existing platform configuration and operator-facing surfaces.
  - existing persistence for operational state and records, where available.

## 4. Requirements Traceability

| Requirement ID | Requirement | Implementation notes | Validation approach |
| --- | --- | --- | --- |
| FR1 | Authenticate to `IG` with configured credentials | Bind environment-appropriate settings into a broker-auth client and perform sanitized login requests | Integration tests for successful login |
| FR2 | Expose current `IG` login and session status | Publish a current broker-auth read model through operator-facing API and UI surfaces | API and functional tests for state visibility |
| FR3 | Detect invalid, expired, or unusable sessions | Classify auth and session-validation failures into explicit broker-auth states | Unit and integration tests for failure detection |
| FR4 | Restore session continuity safely | Reuse the supervisor to re-authenticate and update state without corrupting context | Integration tests for failure then recovery |
| FR5 | Record state transitions without exposing secrets | Persist redacted auth and session event payloads only | Output inspection and sanitization tests |
| FR6 | Prevent forbidden live login actions | Guard unsupported live paths before any broker-auth request is attempted | Command and integration tests for blocked live paths |
| FR7 | Block `IG`-dependent actions without a working session | Publish a blocked reason and enforce it where login is required | Functional and API tests for blocked behavior |
| NF1 | Support safe recovery from session loss | Keep explicit broker-auth state and recovery flow isolated | Recovery-focused automated tests |
| NF2 | Provide operator visibility | Keep a compact current-state projection available during runtime | API and UI validation |
| NF3 | Avoid leaking secrets | Centralize redaction and avoid storing raw sensitive values | Automated output inspection |
| SR2 | Keep secret material out of outputs | Sanitize logs, records, and operator-facing responses | Security-focused test coverage |
| IR1 | Integrate with `IG` auth capabilities | Use a focused `IG` auth adapter with response classification | Integration tests against broker-auth flows |
| OR2 | Record notable auth transitions | Append secret-safe records for login success, failure, expiry, invalid-session, recovery, and blocked live attempts | Persistence and review tests |

## 5. Detailed Design

### 5.1 Public APIs and Contracts

| Area | Contract | Example | Notes |
| --- | --- | --- | --- |
| REST | `GET /api/platform/status` | Returns broker-auth state, environment context, and blocked reason for `IG`-dependent actions | Reuses the operator-facing status surface |
| REST | `POST /api/platform/auth/manual-retry` | Triggers a new broker-auth attempt when a manual retry flow is supported | Optional entry point if the existing platform surface already exposes it |
| Internal contract | `IgAuthenticateRequest` and `IgAuthenticateResult` | Encapsulates broker-auth request input and sanitized outcome details | Keeps `IG` details isolated from the rest of the platform |
| Internal event | `IgSessionStateChanged` | `Authenticated`, `AuthenticationFailed`, `SessionExpired`, `SessionInvalid`, `Recovered`, `LiveLoginBlocked` | Used for recording and runtime projection updates |

### 5.2 Data Model

| Entity or Concept | Fields | Constraints | Notes |
| --- | --- | --- | --- |
| IgSessionState | `Environment`, `Status`, `IsAvailable`, `BlockedReason`, `LastChangedAtUtc`, `LastSuccessfulLoginAtUtc` | Reflects the latest broker-auth state only | Runtime projection for operator visibility |
| IgSessionRecord | `OccurredAtUtc`, `Environment`, `EventType`, `Summary`, `Details` | Details must be redacted and secret-safe | Historical review record |
| IgCredentialBinding | `Environment`, `CredentialReference`, `UpdatedAtUtc` | Must not expose the secret value in read models | References the configured auth input |

### 5.3 Implementation Plan

| Step | Change | Files or Modules | Notes |
| --- | --- | --- | --- |
| 1 | Add `IG` auth request and response contracts plus sanitization | broker-auth infrastructure and application contracts | Keep broker-specific details isolated |
| 2 | Implement broker-auth state projection and transition recording | application and persistence layers | Use secret-safe event recording |
| 3 | Implement login, failure detection, and session recovery workflow | broker-auth service and supervision workflow | Preserve environment context throughout |
| 4 | Expose broker-auth status through operator-facing API and UI | API feature slice and UI components | Make blocked reasons visible |
| 5 | Add requirement-driven validation and update affected docs | tests and `docs/wiki/` if behavior changes during delivery | Required before completion |

### 5.4 Error Handling

| Scenario | Expected behavior | Instrumentation |
| --- | --- | --- |
| Invalid or expired session | Mark the session unavailable, record the transition, and block dependent actions | Secret-safe state-transition record |
| Authentication failure | Expose degraded broker-auth state and preserve the ability to review the failure | Secret-safe state-transition record |
| Forbidden live login path | Block the action before executing a broker-auth request and record the blocked attempt | Secret-safe blocked-action record |

### 5.5 Configuration

| Setting | Purpose | Default | Notes |
| --- | --- | --- | --- |
| Broker environment selection | Chooses the supported `IG` environment | Existing project configuration | Must respect platform-environment restrictions |
| `IG` credential inputs | Supplies the data required to authenticate | External configuration | Never returned in plaintext |
| Session validation settings | Controls how the platform decides a session is no longer usable | Existing project defaults | Must remain configurable without code changes where already supported |

## 6. Security Design

- **Authentication and authorization**: broker-auth requests use configured credentials for the selected supported environment only.
- **Secrets**: credentials and tokens are treated as sensitive inputs and are never emitted in logs, records, or operator views.
- **Threat model notes**:
  - broker-auth failure must not be mistaken for a healthy session
  - forbidden live login paths must be blocked before any external live call is made
  - recovery must not discard environment context or expose prior secret material

## 7. Observability

| Signal | What | Where | Notes |
| --- | --- | --- | --- |
| Current broker-auth state | Whether the platform is authenticated, degraded, or disconnected | Operator-facing status surface | Primary runtime signal |
| Last notable broker-auth transition | The most recent login, failure, expiry, invalid-session, recovery, or blocked-live event | Operational records and status summary | Supports review and troubleshooting |
| Blocked reason | Why `IG`-dependent actions are unavailable | Operator-facing API and UI | Supports safe operator understanding |

## 8. Testing Strategy

- Add focused unit tests for auth-result classification, state transitions, and sanitization.
- Add integration tests for successful login, session failure detection, safe recovery, and blocked live-login behavior.
- Add functional tests for operator-visible broker-auth status and blocked `IG`-dependent actions.
- Update affected wiki pages before the work package is considered complete if the delivered behavior or operator workflow changes.
