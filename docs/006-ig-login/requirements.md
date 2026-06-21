# IG Login Requirements

> Use this document to define *what* must be delivered for work package `006-ig-login`.

## 1. Summary

- **Work item**: IG login
- **Work folder**: `./docs/006-ig-login/`
- **Business requirements**: `../business-requirements.md`
- **Owner**: TNC Trading
- **Date**: 2026-05-28
- **Status**: active
- **Outputs**:
  - `technical-specification.md`
  - `plans/001-delivery-plan.md`
  - `plans/002-real-ig-demo-connection-delivery-plan.md`

### 1.1 Links

| Document | Path |
| --- | --- |
| Business requirements | `../business-requirements.md` |
| Systems analysis | `../systems-analysis.md` |
| Requirements | `requirements.md` |
| Technical specification | `technical-specification.md` |
| Initial delivery plan | `plans/001-delivery-plan.md` |
| Prior auth foundation requirements | `../002-environment-and-auth-foundation/requirements.md` |
| IG API companion sample | `https://labs.ig.com/companion/api-rest-companion-release/index.html` |

## 2. Context

### 2.1 Background

This work package establishes authenticated access to the `IG` Test environment so the platform can safely connect to broker services and make the resulting broker session information available to the operator. It aligns primarily to `BR2` and `BR12` in `../business-requirements.md`, and to `UC2`, `UC9`, `SAR2`, `SAR8`, `NFR1`, `NFR2`, and `NFR3` in `../systems-analysis.md`.

This work package has been clarified to target real IG Demo connectivity rather than a simulation-only outcome. The platform must make real outbound calls to `https://demo-api.ig.com/gateway/deal`, establish a genuine authenticated session, and retrieve read-only proof data (such as account context and open positions) to prove real Demo connectivity safely, without placing trades.

The user has identified a concrete need to authenticate against the `IG` Test environment, capture the information returned from a successful login, store the required non-secret login response details, and make that stored information available for display on the UI. For this work package, the stored and displayed field set is the full non-secret login payload returned by `IG`, excluding credentials, secret/session tokens, and any equivalent protected authentication material. The platform must initiate login automatically on startup, maintain the `IG` login afterward, and expose the resulting status to the UI when it connects. The platform must also retain the latest successful payload and keep the first successful login payload of each day for 90 days. Retry behavior for failed startup or runtime login must follow the earlier auth foundation work package rather than being redefined here, and login maintenance must also follow the earlier trading-schedule rules for when the backend is allowed to maintain an `IG` login. This work package therefore covers startup login, maintained session state, failed login handling, observable status, and persistence of broker-returned account/session metadata that is safe to retain.

## 3. Scope

### 3.1 In scope

- Automatic authentication to the `IG` Test environment during backend startup by using the platform's configured `IG` credentials.
- Ongoing maintenance of the `IG` login state after startup during permitted trading-schedule periods defined by the earlier auth foundation work package.
- Validation and handling of successful and failed `IG` login attempts.
- Reuse of the previously defined platform retry behavior for failed startup or runtime login conditions.
- Reuse of the previously defined trading-schedule rules for when broker login may be established, maintained, retried, or intentionally inactive.
- Capture of the successful login response returned by `IG`.
- Storage of the latest successful full non-secret login response payload and retention of the first successful login payload of each day for 90 days.
- UI display of stored login, session, account, and account-list summary information after a successful login.
- Real outbound IG Demo REST API calls using a typed `HttpClient` registered for the Demo base URL `https://demo-api.ig.com/gateway/deal`.
- Read-only proof-data retrieval after successful login (for example, account/session context or open positions snapshot) to prove real Demo connectivity without trade placement.
- UI access to current `IG` login status when the UI connects, including distinction between active, retrying, failed, and out-of-schedule states.
- UI access to retained daily historical non-secret login payloads within the 90-day retention window.
- Secret-safe recording of login outcomes and notable session state changes.
- Enforcement of existing safeguards that prevent unsupported live-environment use.

### 3.2 Out of scope

- Defining a new retry policy for login failures.
- Defining a new trading-schedule policy for when broker login is allowed.
- Placing, amending, cancelling, or confirming trades.
- Instrument discovery, market data subscriptions, or pricing freshness enforcement.
- Strategy creation, execution, or trading automation.
- Manual UI-initiated login as the primary login flow for this work package.
- Simulation-only login paths — these are replaced by real IG Demo REST calls in this work package.
- Live `IG` environment enablement.
- Reporting features unrelated to login/session/account summary visibility.

## 4. Functional Requirements

| ID | Requirement | Rationale | Acceptance criteria | Notes/Constraints |
| --- | --- | --- | --- | --- |
| FR1 | The platform shall authenticate to the `IG` Test environment automatically during backend startup by using configured `IG` credentials and environment settings. | The platform cannot consume `IG` capabilities without a valid authenticated session, and the chosen operating model is startup-driven login. | When the backend starts with valid Test-environment credentials and connectivity during a permitted trading-schedule period, it establishes a successful `IG` login without requiring a manual UI-triggered login action. | Aligns to `BR2`, `UC2`, and `SAR2`. |
| FR2 | The platform shall detect and clearly report unsuccessful startup or runtime login attempts to the `IG` Test environment. | Operators need a safe and observable failure mode when authentication does not succeed. | Given invalid credentials, rejected authentication, or unavailable `IG` login, the platform marks the login attempt as failed, does not present the session as active, and exposes a user-visible failure state without exposing secrets. | Error detail must remain secret-safe. |
| FR3 | The platform shall capture the broker-returned login response from a successful `IG` Test-environment login. | The returned broker context is needed for later platform behavior and operator visibility. | After a successful login, the platform captures the returned response payload required for the work package and makes it available to downstream platform logic without requiring another login immediately. | Applies only to non-secret response content retained by the platform. |
| FR4 | The platform shall persist the full non-secret login response information from successful `IG` logins. | The user has requested that login information be stored and available after login. | After a successful login, the platform stores all non-secret fields returned by the login response, preserves the latest successful payload, and retains the first successful payload of each day for later retrieval. | Stored data excludes credentials, session tokens, and equivalent protected values. |
| FR5 | The platform shall present the stored full non-secret `IG` login response information on the UI. | The operator needs to review the authenticated account and session context visually. | After a successful login, the UI displays the stored non-secret login response information for the current Test-environment session without exposing credentials, raw secret tokens, or other protected values. | Includes account summary, account list, client and environment context, and other non-secret response fields. |
| FR6 | The platform shall expose the current `IG` login status to the UI when the UI connects. | The UI needs to reflect backend login health and current environment state when it becomes available to the operator. | When the UI connects, it can determine whether the backend is currently signed in, attempting sign-in, signed out, failed to log in, or intentionally out of schedule for the `IG` Test environment. | Environment and schedule context must be visible. |
| FR7 | The platform shall retain a secret-safe record of notable login and session state transitions. | Login outcomes must be reviewable without leaking sensitive information. | Successful login, failed login, startup login attempt, sign-out or session invalidation, out-of-schedule transition, and recovery-related state changes that occur within this work package are recorded without storing credentials or secret token values. | Aligns to `BR12`, `SAR2`, and `NFR3`. |
| FR8 | The platform shall prevent unsupported live-environment login execution in constrained environments. | Existing environment safeguards must remain intact while Test-environment login is introduced. | In platform environments where `IG` live usage is not allowed, the platform blocks live-environment login execution and continues to allow the Test-environment flow only. | Aligns to `BR1` and `SAR8`. |
| FR9 | The platform shall allow the operator to review retained historical successful non-secret login payloads. | Historical broker-login context supports review and troubleshooting without requiring raw secret data. | The operator can retrieve and distinguish the latest successful payload from the retained first successful payload of each day within the configured retention window. | Daily history is limited to one retained successful payload per day. |
| FR10 | The platform shall maintain the `IG` login after startup and keep the current session state accurate over time. | The chosen operating model requires the backend to remain logged in and expose a trustworthy current status to the UI. | After startup login succeeds, the platform keeps the session usable or transitions the visible state promptly when the session becomes invalid, expired, unavailable, or intentionally out of schedule. | Failed-login retry behavior and trading-schedule behavior shall reuse the previously defined logic from `../002-environment-and-auth-foundation/requirements.md` rather than redefining it here. |

## 5. Non-Functional Requirements

| ID | Category | Requirement | Measure/Target | Acceptance criteria |
| --- | --- | --- | --- | --- |
| NF1 | Reliability/Availability | `IG` login handling shall produce a dependable and accurate current login state. | The current login state reflects the latest known outcome of backend startup login and subsequent session maintenance. | Validation shows the platform does not report a healthy `IG` login after a failed or invalid attempt, and failed-login handling follows the previously defined retry behavior. |
| NF2 | Observability | The platform shall make current `IG` login and stored login-summary information visible to the operator. | Current environment, login state, schedule state, latest stored non-secret login summary, and retained daily history are viewable on the operator-facing UI when it connects. | Validation shows the operator can confirm whether the backend is active, retrying, failed, or out of schedule without reading raw logs. |
| NF3 | Security | The platform shall not expose credentials, session tokens, or equivalent sensitive authentication material in UI, logs, or persisted records. | Zero occurrences of secret authentication material in operator-visible or persisted outputs. | Validation confirms protected values are excluded or redacted in all outputs created by this work package. |
| NF4 | Maintainability/Supportability | The stored login-response information shall be retrievable through a stable platform contract suitable for later work packages. | Later features can consume the stored full non-secret login summary without depending on raw broker payload formatting. | Validation shows the platform can read back the stored login summary through an application-facing contract or projection. |
| NF5 | Usability/Accessibility | The login-related UI shall communicate account/session summary information clearly enough for operator review. | The operator can identify login success or failure, distinguish current state from retained daily history, and distinguish failed state from out-of-schedule state without ambiguity. | Validation shows the UI labels or layout distinguish login status, schedule status, current summary data, and historical summary data. |

## 6. Security Requirements

| ID | Category | Requirement | Acceptance criteria |
| --- | --- | --- | --- |
| SR1 | Authentication/Authorization | The platform shall authenticate only against the supported `IG` Test environment for this work package unless a platform environment explicitly permits another environment. | Successful authentication requests target the configured Test environment, and unsupported live-environment login paths are blocked where required. |
| SR2 | Data Protection | The platform shall store and display only the full non-secret login response payload. | Inspection of persisted records and UI responses confirms that credentials, secret tokens, and equivalent protected values are not stored or displayed, while non-secret response fields remain available. |
| SR3 | Secrets/Key Management | The platform shall continue to use configured secret inputs without copying those secret values into operator-facing or persisted login summary data. | Stored login summary data contains only approved non-secret response fields, and secret inputs remain outside the stored summary. |
| SR4 | Threats/Abuse Cases | When `IG` login is unsuccessful or the session becomes unusable, the platform shall fail safely and avoid presenting stale success information as current. | Failed or unusable login/session states are shown as failed or unavailable, while intentional out-of-schedule inactivity is shown distinctly and not presented as an auth failure. |

## 7. Data Requirements

| ID | Requirement | Source | Retention | Acceptance criteria | Notes |
| --- | --- | --- | --- | --- | --- |
| DR1 | The platform shall retain the full non-secret `IG` login response payload required for UI display and operator review. | `IG` login response | 90 days unless superseded by a project-level retention policy update. | After a successful login, the full non-secret field set can be retrieved for display and review during the retention period. | Includes all returned non-secret fields such as account summary, account list, client identifier, environment context, and capability flags. |
| DR2 | The platform shall distinguish stored login-summary data from secret authentication material. | Platform-managed login/session records | Same as the associated login-summary record. | Persisted login/session records separate allowed non-secret payload fields from protected secrets, and secret values are not queryable through the summary display path. | Protected values remain excluded even when the rest of the non-secret payload is stored. |
| DR3 | The platform shall retain the first successful non-secret login payload of each day within the same 90-day retention window. | Platform-managed login/session records | 90 days unless superseded by a project-level retention policy update. | The platform preserves the latest successful payload plus the first successful payload of each day, and those retained daily payloads remain retrievable for the retention period. | Daily history is limited to one retained successful payload per day. |

## 8. Interfaces and Integration Requirements

| ID | Requirement | System | Contract | Acceptance criteria | Notes |
| --- | --- | --- | --- | --- | --- |
| IR1 | The platform shall integrate with `IG` login capabilities required to authenticate against the Test environment and receive the successful login response payload. | `IG` | API | Given valid Test-environment credentials, the platform authenticates successfully and receives the expected broker response needed for this work package. | Sample reference: IG API companion page. |
| IR2 | The platform shall expose stored login-summary data to the operator-facing UI through a platform-owned read contract. | Operator-facing UI | API and UI | After a successful login, the UI can retrieve and display the stored full non-secret login payload. | The UI must not depend on secret broker response fields. |
| IR3 | The platform shall expose retained historical successful login payloads through a platform-owned read contract for operator review. | Operator-facing UI | API and UI | The UI can retrieve the latest successful payload and retained daily first-successful payloads distinctly. | Historical payload access remains limited to non-secret data only. |
| IR4 | The platform shall expose current backend-maintained `IG` login status to the UI when the UI connects. | Operator-facing UI | API and UI | On connection, the UI can retrieve the current backend login state, environment context, and schedule context without triggering a new login. | Supports startup-login operating model. |

## 9. Testing Requirements

| ID | Requirement | Acceptance criteria | Notes |
| --- | --- | --- | --- |
| TR1 | Requirements coverage shall verify automatic startup `IG` Test-environment login. | Tests demonstrate that valid Test-environment credentials produce a successful backend startup login state and capture the required response data without a manual UI trigger. | Traces to `FR1` and `FR3`. |
| TR2 | Requirements coverage shall verify failed-login handling. | Tests demonstrate that invalid or rejected login attempts result in a failed state and do not expose secrets. | Traces to `FR2`, `FR6`, and `SR4`. |
| TR3 | Requirements coverage shall verify persistence of the full non-secret login payload. | Tests demonstrate that after successful login the full non-secret response is stored and can be retrieved later. | Traces to `FR4`, `DR1`, and `DR2`. |
| TR4 | Requirements coverage shall verify UI display of the stored full non-secret login payload. | Tests demonstrate that the operator-facing UI shows the stored approved non-secret login payload after successful login. | Traces to `FR5` and `IR2`. |
| TR5 | Requirements coverage shall verify secret-safe outputs. | Tests or inspections confirm that credentials, session tokens, and equivalent protected values do not appear in persisted summary data, API responses, logs, or UI output. | Traces to `NF3`, `SR2`, and `SR3`. |
| TR6 | Requirements coverage shall verify environment safeguards. | Tests demonstrate that unsupported live-environment login execution remains blocked where required. | Traces to `FR8` and `SR1`. |
| TR7 | Requirements coverage shall verify retained history of successful non-secret login payloads. | Tests demonstrate that the latest successful payload and the first successful payload of each day can be retrieved distinctly within the retention window. | Traces to `FR9`, `DR3`, and `IR3`. |
| TR8 | Requirements coverage shall verify backend-maintained login status visibility to the UI. | Tests demonstrate that when the UI connects it can retrieve the current backend login state and does not need to trigger login itself. | Traces to `FR6`, `FR10`, and `IR4`. |
| TR9 | Requirements coverage shall verify that failed-login recovery behavior continues to follow the earlier retry policy rather than a new work-package-specific retry policy. | Tests demonstrate that failed startup or runtime login conditions use the previously established retry behavior from the earlier auth foundation work package. | Traces to `FR10` and the reused retry behavior. |
| TR10 | Requirements coverage shall verify that out-of-schedule inactivity follows the earlier trading-schedule rules and is shown distinctly from failed login. | Tests demonstrate that when the backend is outside the permitted trading schedule, the UI shows an intentional out-of-schedule state rather than an auth failure. | Traces to `FR6`, `FR10`, and `SR4`. |

## 10. Operational Requirements

| ID | Requirement | Acceptance criteria | Notes |
| --- | --- | --- | --- |
| OR1 | The platform shall surface the current backend-maintained `IG` Test-environment login state during runtime. | The operator can determine whether the backend is currently logged in, attempting login, not logged in, in a failed login state, or out of schedule for the Test environment. | Supports operator review. |
| OR2 | The platform shall retain secret-safe login/session summary records for later review. | The operator can review the stored full non-secret login payload and notable login-state transitions without exposing secrets. | Supports auditability and troubleshooting. |
| OR3 | The platform shall allow operator review of retained historical successful login payloads within the retention window. | The operator can review prior successful non-secret login payloads without confusing them with the current login state. | Supports operational review and troubleshooting. |

## 11. Assumptions, Risks, and Dependencies

### 11.1 Assumptions

- A valid IG Demo account, API key, and network connectivity to `https://demo-api.ig.com/gateway/deal` are available for development and validation.
- The platform already has, or will provide in this work package, an operator-facing UI capable of showing login-related information.
- The successful `IG` login response contains both protected values and non-secret account/session summary values, and the full non-secret subset is in scope for storage and display.
- Retaining one historical payload per day for 90 days is sufficient for operator review needs.
- Backend startup is the correct point to initiate login for this work package, and the UI acts as a status/view consumer rather than the primary login initiator.
- Retry behavior for failed startup or runtime login is already defined in the earlier auth foundation work package and should be reused rather than restated here.
- Trading-schedule behavior for when broker login is allowed is already defined in the earlier auth foundation work package and should be reused rather than restated here.

### 11.2 Risks

- **Broker contract risk**: the actual `IG` login response may differ from the sample payload used during requirements drafting.
  - **Mitigation**: confirm the exact response contract during implementation and keep the stored/displayed field set explicitly controlled.
- **Secrets exposure risk**: protected login/session material could be accidentally stored or shown if the response is handled too broadly.
  - **Mitigation**: validate all persisted and displayed fields against the rule that the full non-secret payload is allowed, but all credentials, secret/session tokens, and equivalent protected values remain excluded.
- **State ambiguity risk**: previously stored login-summary information could be mistaken for proof of an active current session.
  - **Mitigation**: display current login state separately from stored login-summary data and make stale or failed states explicit.
- **Startup dependency risk**: backend startup health may be affected if `IG` is unavailable when login is attempted.
  - **Mitigation**: reuse the earlier retry behavior, surface an explicit login state to the UI, and keep failure handling observable without exposing secrets.
- **External dependency risk**: the IG Demo API may reject, throttle, or be unreachable during development or validation.
  - **Mitigation**: deterministic automated tests use fake `HttpMessageHandler` implementations so `dotnet test` remains reliable; real-IG validation is explicit and opt-in, gated by user secrets or environment variables that are not committed to source control.

### 11.3 Dependencies

- `../business-requirements.md`
- `../systems-analysis.md`
- `../002-environment-and-auth-foundation/requirements.md`
- `IG` Test-environment login availability and account access
- A platform-owned persistence mechanism for non-secret login summary data
- A platform-owned operator-facing UI surface for displaying login/session summary information

## 12. Open Questions

- None at this stage.

## 13. Appendix

- IG API companion sample login page: `https://labs.ig.com/companion/api-rest-companion-release/index.html`
- Example successful login payload supplied by the user, including account summary, account list, current account identifier, lightstreamer endpoint, client identifier, timezone information, account-availability flags, and dealing capability flags.
