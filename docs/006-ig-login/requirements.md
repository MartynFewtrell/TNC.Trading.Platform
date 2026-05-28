# IG Login Requirements

This document defines the work-package requirements for establishing `IG` login and session continuity as a distinct delivery slice within the platform.

## 1. Summary

- **Work item**: IG login
- **Work folder**: `./docs/006-ig-login/`
- **Business requirements**: `../business-requirements.md`
- **Owner**: TNC Trading
- **Date**: 2026-05-28
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

This work package isolates `IG` login and authenticated session continuity into a dedicated delivery slice so the platform can establish broker connectivity safely and make the resulting state observable for later trading capabilities. It aligns primarily to `BR2` and `BR12`, with traceability to `UC2`, `SAR2`, `SAR8`, and `NFR1` through `NFR2` in `../systems-analysis.md`.

The work package focuses on authenticating to the supported `IG` environment for the current release, keeping the broker session usable for platform operation, detecting session loss, and restoring session continuity without exposing secrets. It also requires the operator to be able to review the current login or session state so later work packages can depend on a stable authentication foundation.

## 3. Scope

### 3.1 In scope

- Authentication to the supported `IG` environment for this release.
- Establishment of an authenticated `IG` session suitable for sustained platform operation.
- Detection of invalid, expired, or unusable `IG` sessions.
- Safe restoration of `IG` session continuity after session loss.
- Observable `IG` login and session state for operator review.
- Recording of authentication and session state transitions without exposing secrets.
- Enforcement of current environment restrictions so forbidden live actions remain blocked.

### 3.2 Out of scope

- Instrument discovery and tracked-instrument management.
- Market data ingestion and freshness gating.
- Strategy runtime and trade intent generation.
- Order placement, amendment, cancellation, or confirmation.
- Risk controls beyond those required to keep login and session handling safe.
- End-of-day flattening, reconciliation, or reporting.
- Broad notification-policy design outside the needs of this login-focused slice.

## 4. Functional Requirements

| ID | Requirement | Rationale | Acceptance criteria | Notes/Constraints |
| --- | --- | --- | --- | --- |
| FR1 | The platform shall authenticate to `IG` by using the configured credentials for the selected supported environment. | Sustained broker connectivity depends on successful authentication. | Given valid configuration and connectivity, the platform can establish an authenticated `IG` session and make that state observable. | Aligns to `BR2`, `UC2`, and `SAR2`. |
| FR2 | The platform shall expose the current `IG` login and session status for operator review. | Later capabilities depend on knowing whether broker connectivity is healthy. | The operator can determine whether the platform is signed in, degraded, or disconnected, together with the relevant environment context. | Aligns to `UC2` and `SAR2`. |
| FR3 | The platform shall detect invalid, expired, or unusable `IG` sessions. | Session failures must be identified before dependent capabilities rely on them. | When an `IG` session becomes invalid, expires, or is rejected, the platform detects the condition and updates the observable session state. | Aligns to `BR2` and `SAR2`. |
| FR4 | The platform shall restore `IG` session continuity after session failure without corrupting platform state. | Safe recovery is required for resilient operation. | After session loss, the platform can re-authenticate successfully and return to a working session state while preserving environment context and prior records. | Aligns to `BR2` and `SAR2`. |
| FR5 | The platform shall record authentication and session state transitions without exposing secrets. | Auditability is required, but secret material must remain protected. | Login success, failure, expiry, invalid-session, and recovery events are recorded without exposing credentials, tokens, or other secret values. | Aligns to `BR12`, `SAR2`, and `NFR1`. |
| FR6 | The platform shall prevent forbidden live-environment login actions when they are not allowed by the active platform environment. | Current release safeguards must reduce accidental live usage. | In environments where `IG` live use is not allowed, the live option remains blocked and no forbidden login attempt is executed. | Aligns to `BR1` and `SAR8`. |
| FR7 | The platform shall block `IG`-dependent actions when no working `IG` session is available. | Dependent capabilities must not proceed on an unusable broker session. | When the `IG` session is unavailable or degraded, actions that require broker login are visibly blocked until the session is restored. | Supports safe downstream behavior. |

## 5. Non-Functional Requirements

| ID | Category | Requirement | Measure/Target | Acceptance criteria |
| --- | --- | --- | --- | --- |
| NF1 | Reliability/Availability | `IG` login handling shall support recovery from expected session loss conditions. | Expired or invalid sessions are detected and can be re-established safely. | Validation demonstrates failure detection and recovery without corrupting session state. |
| NF2 | Observability | The platform shall provide enough visibility for the operator to understand current `IG` login state. | Current session state, environment context, and last notable transition are visible. | Validation demonstrates that the operator can review the current broker-auth state without inspecting raw logs. |
| NF3 | Security | Authentication handling shall not leak secrets in records or operator-facing outputs. | No credentials, tokens, or equivalent secret values appear in logs, records, notifications, API responses, or UI views. | Validation demonstrates that sensitive values are absent from generated outputs. |

## 6. Security Requirements

| ID | Category | Requirement | Acceptance criteria |
| --- | --- | --- | --- |
| SR1 | Authentication/Authorization | The platform shall authenticate only by using configured credentials for the selected supported environment. | Successful authentication uses the configured environment-specific credentials; unsupported live authentication paths remain blocked where required. |
| SR2 | Data Protection | The platform shall keep credentials, session tokens, and equivalent sensitive data out of logs, records, notifications, and operator views. | Inspection of runtime outputs and stored records confirms that sensitive authentication material is not exposed. |
| SR3 | Threats/Abuse Cases | When `IG` login cannot be established or maintained, the platform shall fail safely. | The platform reports the degraded or disconnected state clearly and prevents `IG`-dependent actions until a working session is restored. |

## 7. Interfaces and Integration Requirements

| ID | Requirement | System | Contract | Acceptance criteria | Notes |
| --- | --- | --- | --- | --- | --- |
| IR1 | The platform shall integrate with `IG` authentication capabilities needed to establish and maintain a working session. | `IG` | API | Given valid credentials and connectivity, the platform can establish and later restore a working session. | Aligns to `UC2` and `SAR2`. |
| IR2 | The platform shall expose current `IG` login and session state through the operator-facing surface. | Operator surface | API and UI | The operator can review the current broker-auth state and any blocked condition caused by missing login. | Supports safe operation. |

## 8. Testing Requirements

| ID | Requirement | Acceptance criteria | Notes |
| --- | --- | --- | --- |
| TR1 | Requirements coverage shall verify successful `IG` login. | Tests demonstrate that valid configuration produces a working authenticated session. | Traces to `FR1`. |
| TR2 | Requirements coverage shall verify session failure detection. | Tests demonstrate detection of invalid, expired, or rejected sessions and the resulting state transition. | Traces to `FR3`. |
| TR3 | Requirements coverage shall verify safe session recovery. | Tests demonstrate that the platform can restore a working session after failure without losing environment context. | Traces to `FR4`. |
| TR4 | Requirements coverage shall verify secret-safe outputs. | Tests or inspections confirm that credentials and tokens do not appear in runtime or persisted outputs. | Traces to `FR5`, `NF3`, and `SR2`. |
| TR5 | Requirements coverage shall verify live-environment safeguards. | Tests demonstrate that forbidden live login actions remain blocked in constrained environments. | Traces to `FR6` and `SR1`. |
| TR6 | Requirements coverage shall verify blocked `IG`-dependent actions when no working session exists. | Tests demonstrate that dependent actions remain unavailable while the broker-auth state is degraded or disconnected. | Traces to `FR7` and `SR3`. |

## 9. Operational Requirements

| ID | Requirement | Acceptance criteria | Notes |
| --- | --- | --- | --- |
| OR1 | The platform shall surface current `IG` login state for operator review during runtime. | The operator can determine the current broker-auth state and whether `IG`-dependent actions are currently blocked. | Supports safe operation. |
| OR2 | The platform shall record notable authentication and session state transitions for later review. | Login success, failure, invalid-session, expiry, recovery, and blocked live-attempt events are retained with environment context and without secrets. | Supports reviewability and troubleshooting. |

## 10. Assumptions, Risks, and Dependencies

### 10.1 Assumptions

- An `IG` account and the required credentials for the supported environment are available.
- The project already has a configuration mechanism suitable for supplying `IG` authentication settings.
- Later work packages will consume the session state produced by this work package rather than redefining it.

### 10.2 Risks

- **Connectivity risk**: transient broker or network issues could interrupt session continuity.
  - **Mitigation**: detect failure clearly, preserve observable session state, and support safe re-authentication.
- **Secrets exposure risk**: authentication diagnostics could leak sensitive values.
  - **Mitigation**: record only redacted state transitions and exclude secret material from all outputs.
- **Environment misuse risk**: an unsafe environment choice could lead to forbidden login attempts.
  - **Mitigation**: retain explicit environment checks and block forbidden live login paths.

### 10.3 Dependencies

- `../business-requirements.md`
- `../systems-analysis.md`
- `IG` authentication and session capabilities
