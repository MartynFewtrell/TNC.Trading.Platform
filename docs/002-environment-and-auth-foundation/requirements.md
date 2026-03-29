# Requirements

## 1. Summary

- **Work item**: Environment and auth foundation
- **Work folder**: `./docs/002-environment-and-auth-foundation/`
- **Business requirements**: `../business-requirements.md`
- **Owner**: TNC Trading
- **Date**: 2026-03-27
- **Status**: draft
- **Outputs**:
  - `technical-specification.md`
  - `delivery-plan.md`

### 1.1 Links

| Document | Path |
| --- | --- |
| Business requirements | `../business-requirements.md` |
| Requirements | `requirements.md` |
| Technical specification | `technical-specification.md` |
| Delivery plan | `delivery-plan.md` |
| Systems analysis | `../systems-analysis.md` |

## 2. Context

### 2.1 Background

This work package establishes the foundation for safe environment selection, operator-managed configuration stored in a durable data store, schedule-aware broker connectivity, and authenticated session continuity with `IG`. It supports `BR1`, `BR2`, and `BR12` by making the selected trading environment explicit and observable, separating environment-specific configuration and records, storing operator-managed configuration data in a durable data store, holding `IG` credentials securely, allowing configuration changes through the Blazor UI, limiting broker connectivity to the configured trading schedule, and ensuring the platform can establish and maintain an authenticated `IG` session suitable for sustained operation during permitted trading periods. This package aligns primarily to `UC1`, `UC2`, `SAR1`, `SAR2`, and includes the relevant platform-environment constraint from `SAR8` so that in the Test platform environment the `IG` live option is visible but unavailable. For this work package, environment selection is limited to initial configuration and startup rather than runtime switching after setup, authentication support is limited to `IG` demo, notifications are included for auth/session issues, retry exhaustion events, and blocked live-environment attempts, the platform starts in an observable degraded state if an `IG` demo session cannot be established at startup while the configured trading schedule is active, the full platform UI remains available while auth-dependent actions are blocked, degraded-state retry timing uses an initial exponential backoff sequence with a default initial delay of 1 second, a default maximum of 5 automatic retries after the initial failed startup attempt, and a fixed standard backoff profile that doubles each retry and is capped at 60 seconds, and after that initial retry sequence is exhausted the platform continues attempting to reconnect on a periodic basis using a configurable delay with an initial default of 5 minutes while the configured trading schedule remains active. The operator can review and update supported configuration data in the Blazor UI, including the start and end of the configured trading day, permitted trading days, weekend treatment, and bank-holiday exclusions, and sensitive `IG` credential fields must be updated through a secure write-only flow that never reveals the currently stored secret values. Outside the configured trading schedule the platform does not maintain an `IG` connection and does not treat the absence of a broker session as a degraded auth condition. The operator can still trigger a manual retry from the platform UI or operator surface while the configured trading schedule is active, and that manual retry resets the automatic retry budget and resumes the same retry policy if it fails again, with a fresh failure notification for that new retry cycle and no extra confirmation step before the manual retry begins. Retry-limit notifications explicitly tell the operator that a manual retry action is available and that periodic retry will continue. Recovery notifications remain concise and do not include retry-attempt counts or whether recovery occurred during the initial automatic cycle or a manual-restart cycle.

## 3. Scope

### 3.1 In scope

- Explicit selection of the target `IG` environment for initial configuration and startup.
- Visibility of the selected environment during operation and in recorded operational events.
- Separation of environment-specific configuration and recorded activity.
- Storage of operator-managed environment, retry, and notification configuration data in a durable data store.
- Update of supported configuration data through the Blazor UI with durable persistence to the configuration data store.
- Configuration of the trading schedule using a start-of-day time, end-of-day time, permitted trading days, weekend treatment, and configurable bank-holiday exclusions.
- Secure storage and update of `IG` credentials without exposing existing secret values in the UI, API, logs, notifications, or records.
- Authentication to `IG` demo for platform operation.
- Maintenance of the `IG` connection only during the configured trading schedule.
- Detection of invalid or expired `IG` demo sessions.
- Safe restoration of a working `IG` demo session without corrupting platform state.
- Startup behavior that allows the platform to enter an observable degraded state and continue retrying when an `IG` demo session cannot be established initially.
- Initial exponential backoff retry behavior for degraded startup/auth recovery with a default initial delay of 1 second, a default maximum of 5 automatic retries after the initial failed startup attempt, and a fixed retry profile that doubles each retry delay and caps delay at 60 seconds.
- Continued periodic retry attempts after the initial exponential retry sequence is exhausted, using a configurable delay with an initial default of 5 minutes.
- Availability of the full platform UI during degraded startup/auth state, with auth-dependent actions blocked.
- Visibility of the current retry attempt number and next scheduled retry time while the platform is retrying in degraded startup/auth state.
- Operator-triggered manual retry from the platform UI or operator surface after the automatic retry limit is reached.
- Reset of the automatic retry budget when an operator-triggered manual retry is initiated.
- Fresh auth/session failure notification behavior for a new retry cycle started after manual retry.
- Explicit retry-limit notifications that include manual retry guidance.
- Immediate start of manual retry when the operator triggers it, without a separate confirmation step.
- Recording authentication and session state transitions without exposing secrets.
- Notification of `IG` demo authentication/session failure and recovery conditions.
- Visibility of the `IG` live option in the Test platform environment while preventing its selection or use.
- Notification of blocked attempts to use the `IG` live environment from the Test platform environment.

### 3.2 Out of scope

- Runtime switching between `IG` demo and live environments after initial setup.
- Authentication to `IG` live in this work package.
- Instrument discovery and tracked instrument management that depend on a working `IG` session.
- Market data ingestion and freshness gating.
- Strategy lifecycle and runtime behavior.
- Order placement, amendment, cancellation, and confirmation.
- Notifications solely for the act of triggering a manual retry.
- Retry-attempt counts in recovery notifications.
- Recovery notification detail about whether recovery occurred in the initial automatic cycle or a manual-restart cycle.
- Direct display of currently stored `IG` credential values in the Blazor UI or API responses.
- Automatic trading-calendar feeds or market-calendar logic beyond configurable trading days, weekend treatment, and bank-holiday exclusions.
- Risk controls beyond those required to protect safe environment/auth operation.
- End-of-day flattening and reporting features.

## 4. Functional Requirements

Use `FR1`, `FR2`, ... for functional requirements.

| ID | Requirement | Rationale | Acceptance criteria | Notes/Constraints |
| --- | --- | --- | --- | --- |
| FR1 | The platform shall require an explicit `IG` environment selection for initial configuration and startup. | Reduces accidental use of the wrong broker environment and supports `BR1`. | Before automated operation begins, the operator can determine which `IG` environment is selected; the selected environment is stored in the startup configuration data store; the platform does not proceed without an explicit environment choice. | Aligns to `BR1`, `UC1`, `SAR1`. |
| FR2 | The platform shall make the selected `IG` environment observable during operation and in recorded activity. | Environment visibility is required for safe operation and later review. | The currently selected environment is visible in the running platform context; authentication/session-related records include the environment context; review of records can distinguish demo activity from live activity. | Aligns to `BR1`, `SAR1`. |
| FR3 | The platform shall keep configuration and recorded activity separated by selected `IG` environment. | Prevents cross-environment confusion and supports safe auditability. | Configuration associated with one `IG` environment is not silently reused for the other environment; environment-scoped configuration records in the configuration data store remain distinguishable and separately retrievable; recorded activity can be filtered or identified by environment; startup in one environment does not merge prior records into the wrong environment context. | Aligns to `BR1`, `SAR1`. |
| FR4 | The platform shall authenticate to `IG` demo and establish a working session suitable for platform operation. | Sustained platform operation depends on authenticated access. | Given valid `IG` demo credentials and connectivity, the platform can establish a working authenticated session; session establishment success or failure is recorded; the established session state is observable. | Aligns to `BR2`, `UC2`, `SAR2`. Live authentication is out of scope for this work package. |
| FR5 | The platform shall detect expired, invalid, or unusable `IG` demo sessions. | Session failures must be identified before dependent capabilities can operate safely. | When an existing `IG` demo session becomes invalid, expired, or rejected by `IG`, the platform detects the condition and records the state transition; session state changes are visible for operational review. | Aligns to `BR2`, `SAR2`. |
| FR6 | The platform shall restore a working `IG` demo session after session failure without corrupting platform state. | Safe recovery is necessary for sustained operation and later capabilities. | After a session expiry or invalid-session condition, the platform can re-establish a working `IG` demo session using valid credentials; recovery attempts and outcomes are recorded; recovery does not lose the platform’s environment context. | Aligns to `BR2`, `UC2`, `SAR2`. |
| FR7 | The platform shall record authentication and session events without exposing secrets. | Auditability is required, but credentials and tokens must remain protected. | Authentication success, failure, expiry, degraded-startup state, retry attempts, and recovery events are recorded; no logs, notifications, records, API responses, or UI views produced by this package include raw secrets or tokens; stored `IG` credentials remain protected at rest and are never returned in plain text after capture. | Aligns to `BR2`, `BR12`, `SAR2`. |
| FR8 | In the Test platform environment, the platform shall show the `IG` live option as unavailable and prevent its selection or use. | Supports the platform-environment safeguard defined in `SAR8` and reduces accidental live trading risk. | When operating in the Test platform environment, the `IG` live option is visible but cannot be selected for active use; attempts to use it are prevented; prevented attempts are recorded with environment context. | Aligns to `BR1`, `UC1`, `SAR8`. |
| FR9 | The platform shall not attempt `IG` live authentication as part of this work package. | Keeps scope aligned to the chosen initial-release boundary while preserving the live-environment safeguard. | No platform flow in this work package initiates `IG` live authentication; any attempted path toward live authentication is blocked or deferred by design; the resulting behavior is testable and documented. | Constrains this work package to `IG` demo authentication only. |
| FR10 | The platform shall notify the project owner when `IG` demo authentication/session failure is first detected and when the session is successfully recovered. | The chosen scope includes operator notification for auth/session issues so the project owner can intervene or confirm recovery. | When `IG` demo authentication fails initially, the platform enters degraded startup, or an established session later becomes unusable, a notification is issued on the failure state transition; when the session is recovered, a recovery notification is issued; notifications include event type, timestamp, environment context, and a concise summary; recovery notifications do not include retry-attempt counts or whether recovery occurred in the initial automatic cycle or a manual-restart cycle; notifications do not expose secrets. | Aligned to the state-transition notification approach in `../systems-analysis.md`. |
| FR11 | The platform shall notify the project owner on every blocked attempt to use the `IG` live environment from the Test platform environment. | The chosen scope includes immediate awareness of each forbidden live-use attempt for review and intervention. | Each blocked live-environment attempt in the Test platform environment produces a notification with event type, timestamp, environment context, and a concise summary; the notification does not expose secrets. | Extends the `SAR8` safeguard with per-attempt notification behavior for this work package. |
| FR12 | When an `IG` demo session cannot be established during startup, the platform shall start in an observable degraded state and continue retrying until a working session is established, using an initial automatic retry sequence followed by periodic retry attempts if the initial sequence is exhausted. | Allows operator visibility and recovery without requiring repeated manual restarts while still bounding the initial aggressive retry behavior. | If initial session establishment fails, the platform still starts; the startup auth failure state is visible; retry attempts continue automatically using the configured retry policy; if a working session is established, the platform transitions out of the degraded state and records the recovery; if the maximum initial automatic retries are reached without success, the degraded condition remains visible and the platform continues periodic retry attempts until recovery or operator intervention. | This work package defines degraded startup behavior only for auth/session establishment. |
| FR13 | While the platform is in degraded startup/auth state, the full platform UI shall remain available, but auth-dependent actions shall be blocked with a visible reason. | Preserves operator access to visibility and non-authenticated areas while preventing unsafe or unusable actions that require a working `IG` session. | During degraded startup/auth state, the operator can access the platform UI; any action requiring a working `IG` session is visibly unavailable or blocked; blocked actions communicate that auth/session state is the reason; non-auth-dependent UI areas remain accessible. | Applies only to degraded auth/session state in this work package. |
| FR14 | Automatic retry of `IG` demo session establishment from degraded startup/auth state shall use an initial exponential backoff sequence with a default initial delay of 1 second, a default maximum of 5 automatic retries after the initial failed startup attempt, a fixed retry profile that doubles each retry delay up to a maximum delay of 60 seconds, and then a periodic retry delay that is configurable and initially set to 5 minutes. | Provides a controlled retry strategy that reduces repeated immediate failures while allowing the project owner to tune the most important retry controls. | Retry behavior uses exponential backoff rather than a fixed interval for the initial retry sequence; the default initial delay is 1 second; the default maximum automatic retries is 5 after the initial failed startup attempt; maximum retry count can be changed without changing code; each retry doubles the prior delay until the delay reaches 60 seconds; after the initial retry sequence is exhausted, the platform continues retrying using a periodic delay; the periodic delay can be changed without changing code and is initially set to 5 minutes; retry attempts and any eventual recovery remain observable. | The fixed multiplier is `x2`; the fixed cap is 60 seconds; the initial periodic retry delay is 5 minutes. |
| FR15 | After the configured initial automatic retry limit is reached, the operator shall be able to trigger a manual retry from the platform UI or operator surface without restarting the platform or waiting for the next periodic retry attempt. | Allows recovery action without requiring a full restart and fits the chosen degraded-state operator experience. | After the initial retry sequence is exhausted and periodic retry is active, the platform exposes a manual retry action; invoking the action immediately starts a new auth/session establishment attempt without an additional confirmation step; the attempt outcome is visible and recorded; the action remains unavailable only when a retry is already in progress; triggering the action does not itself emit a notification. | Applies after the initial automatic retry limit is reached. |
| FR16 | If an operator-triggered manual retry fails after the initial automatic retry limit was previously reached, the platform shall reset the automatic retry budget and resume the same configured retry policy. | Provides a predictable recovery model after manual intervention rather than leaving the platform in a permanently degraded periodic-only state after one failed manual retry. | When a manual retry is triggered after the initial retry sequence is exhausted and that retry does not establish a working session, the platform begins a new automatic retry cycle using the same default initial delay, doubling retry profile, 60-second cap, configured maximum automatic retries, and periodic retry delay; the new retry cycle is visible and recorded. | Applies only after a manual retry is initiated from the post-initial-retry-limit state. |
| FR17 | When a manual retry starts a new automatic retry cycle and that new cycle fails again, the platform shall issue a fresh auth/session failure notification for that new cycle and still issue a recovery notification if recovery later occurs. | Treats the new cycle as a new operator-visible failure event and preserves consistent state-transition notification behavior. | After a manual retry starts a new retry cycle, the first failure state in that new cycle produces a new auth/session failure notification; if that cycle later recovers, a recovery notification is issued; notifications remain secret-safe and include event type, timestamp, environment context, and concise summary. | Applies only to retry cycles started after a manual retry. |
| FR18 | While the platform is retrying in degraded startup/auth state, the operator shall be able to see both the current retry attempt number and the next scheduled retry time. | Improves operator awareness and reduces ambiguity about recovery progress during degraded operation. | During degraded retry operation, the UI or operator surface shows the current retry attempt number and the next scheduled retry time; the attempt number reflects automatic retries after the initial failed startup attempt while the initial retry sequence is active; when periodic retry is active, the UI or operator surface indicates that periodic retry mode is active and still shows the next scheduled retry time; displayed values update as retry activity progresses and reflect recovery when retrying stops. | Applies while automatic or periodic retry is active. |
| FR19 | When any retry cycle reaches the configured initial automatic retry limit and the platform remains in degraded state, the platform shall send an explicit retry-limit notification that tells the operator a manual retry action is available and that periodic retry will continue. | Alerts the project owner that the initial aggressive recovery sequence has been exhausted while clarifying that background recovery attempts continue. | When a retry cycle reaches the configured initial automatic retry limit without establishing a working `IG` demo session, the platform issues a notification stating that the initial automatic retries are exhausted, the platform remains in degraded state, periodic retry will continue, and a manual retry action is available; the notification includes event type, timestamp, environment context, concise summary, the last automatic retry attempt count reached, the last scheduled delay attempted before the initial retry sequence was exhausted, and the periodic retry delay now in effect; the notification does not expose secrets. | Applies to the initial automatic retry cycle and any later retry cycle restarted by manual retry. Distinct from initial failure and recovery notifications. |
| FR20 | The platform shall store operator-managed configuration data in a durable data store and allow supported configuration updates through the Blazor UI while keeping `IG` credentials secure. | Centralized durable configuration supports safe operations, later extensibility, and operator-driven changes without redeployment. | Supported configuration values for platform environment, broker environment, retry policy, and notification settings are loaded from the configuration data store; the Blazor UI allows an operator to review and update supported configuration values and persist the changes to the configuration data store; startup-fixed settings, including platform environment and broker environment selection, are updated for subsequent startup rather than runtime switching; `IG` credential values are captured and updated through a secure write-only workflow; the UI and API never disclose currently stored secret values; configuration changes are recorded for later review. | Aligns to `BR1`, `BR2`, `BR12`, `UC1`, `UC2`. |
| FR21 | The platform shall support a configurable trading schedule defined by a start-of-day time, end-of-day time, permitted trading days, weekend treatment, and bank-holiday exclusions. | Operator-controlled trading schedule settings are required so broker connectivity aligns with intended trading periods. | The operator can review the configured trading start-of-day and end-of-day values, permitted trading days, weekend treatment, and bank-holiday exclusions in the platform UI; supported changes can be persisted for later use without changing code; the active trading-schedule configuration is available for runtime review. | Applies to the platform trading schedule for this work package only. |
| FR22 | The platform shall maintain an `IG` demo connection only during the configured trading schedule. | Avoids maintaining unnecessary broker connectivity outside intended trading periods and keeps the auth state aligned with operational expectations. | During configured trading hours on permitted trading days and excluding configured bank holidays, the platform can establish, maintain, retry, and manually re-establish the `IG` demo session as required by other requirements in this package; outside the configured trading schedule, including non-trading weekends and configured bank holidays, the platform does not maintain an `IG` demo connection, stops or suppresses auth retry activity, and does not present the lack of an active broker session as a degraded auth failure solely because the trading schedule is inactive; the current in-schedule or out-of-schedule state is visible to the operator. | Applies only to the configured trading schedule in this work package. |

## 5. Non-Functional Requirements

Use `NF1`, `NF2`, ... for non-functional requirements.

| ID | Category | Requirement | Measure/Target | Acceptance criteria |
| --- | --- | --- | --- | --- |
| NF1 | Reliability/Availability | The environment-selection and auth foundation shall support sustained safe operation under normal `IG` demo session expiry and transient session failure conditions during the configured trading schedule. | `IG` demo session expiry or invalid-session conditions are detectable and recoverable without corrupting environment context while the configured trading schedule is active; startup can proceed in a degraded state when auth is unavailable during active trading periods; recovery attempts continue periodically after the initial retry sequence is exhausted while the configured trading schedule remains active. | Demonstration shows the platform detects an invalid or expired `IG` demo session, or an initial startup auth failure while the configured trading schedule is active, records the event, and restores a working session while preserving the selected environment context, or remains in a visible degraded state while continuing periodic retry attempts after the configured initial retry limit is reached while the configured trading schedule remains active. |
| NF2 | Observability | The platform shall expose enough environment, configuration, trading-schedule, and session-state information for the project owner to understand current operating context. | Environment selection, active non-secret configuration, configured trading schedule, current in-schedule or out-of-schedule state, degraded startup state, blocked auth-dependent actions, retry behavior, current retry attempt number, retry mode, next scheduled retry time, manual retry availability, and session state are observable during operation and in retained records. | Demonstration shows the selected environment, active non-secret configuration, configured trading schedule, current in-schedule or out-of-schedule state, current auth/session state, retry behavior, current retry attempt number, current retry mode, next scheduled retry time, post-initial-retry-limit state, manual retry availability, and reason for blocked auth-dependent actions can be reviewed during runtime and after an auth-related event. |
| NF3 | Maintainability/Supportability | The foundation shall provide a stable basis for later work packages that depend on `IG` environment, operator-managed configuration, and session state. | Environment, configuration-management, and session behaviors are documented and testable; supported non-secret configuration can be updated without code changes or redeployment. | Requirements and downstream design can trace later work packages to the environment/auth foundation without redefining core environment, configuration, or session semantics. |
| NF4 | Security | Authentication and configuration-management behavior shall protect secrets and avoid leaking sensitive values in storage or operational outputs. | No secrets in plaintext persisted configuration, logs, records, notifications, API responses, or UI views produced by this package. | Review of outputs and stored configuration generated by auth success, failure, degraded startup, recovery, blocked live attempts, retry behavior, manual retry behavior, configuration update behavior, and related notifications confirms secrets are not exposed and protected values remain secured at rest. |
| NF5 | Operational responsiveness | Auth/session, retry-limit, and blocked live-attempt notifications shall be timely enough to support operator awareness. | Notification is issued immediately on supported failure, retry-limit reached, recovery, and blocked-attempt events. | Demonstration shows supported notifications are emitted without batching or undue delay. |

## 6. Security Requirements

Use `SR1`, `SR2`, ... for security requirements.

| ID | Category | Requirement | Acceptance criteria |
| --- | --- | --- | --- |
| SR1 | Authentication/Authorization | The platform shall only establish an `IG` session by using valid configured credentials for the selected and supported environment. | Authentication attempts use environment-appropriate credentials for supported flows; `IG` demo authentication fails safely when credentials are missing or invalid; unsupported `IG` live authentication is not attempted by this work package. |
| SR2 | Data Protection | The platform shall prevent sensitive authentication material from appearing in logs, records, notifications, API responses, UI views, or plaintext persisted configuration. | Raw credentials, API keys, access tokens, session tokens, and equivalent sensitive values are absent from recorded auth/session events and other operational outputs, and protected credential values are not stored or returned in plaintext. |
| SR3 | Secrets/Key Management | The platform shall support credential rotation without losing the ability to continue operating or review prior records. | Credentials can be updated independently of historical records through a secure management flow; after rotation, new `IG` demo authentication attempts use the updated credentials; prior auth/session records remain reviewable without exposing old secrets; the Blazor UI does not reveal currently stored credential values during update flows. |
| SR4 | Threats/Abuse Cases | The platform shall fail safely when authentication cannot be completed, session validity cannot be maintained, or a forbidden live-environment action is attempted from the Test platform environment. | When authentication is unavailable, the platform remains in an observable degraded state rather than reporting a healthy working session; when a forbidden live-environment action is attempted in the Test platform environment, the action is blocked and recorded for operator review. |
| SR5 | Notification security | Auth/session, retry-limit, and blocked live-attempt notifications shall contain operationally useful context without exposing secrets or sensitive token material. | Notifications include event type, timestamp, environment context, and concise summary, and exclude credentials, tokens, and other secret values. |

## 7. Data Requirements (optional)

Use `DR1`, `DR2`, ... for data requirements.

| ID | Requirement | Source | Retention | Acceptance criteria | Notes |
| --- | --- | --- | --- | --- | --- |
| DR1 | The platform shall retain environment selection, configuration-change, and auth/session state-transition records needed for personal/internal review. | Platform | 90 days | Records showing environment selection, supported configuration changes including trading-schedule changes, blocked forbidden environment actions, auth success/failure, degraded startup, initial retry activity, retry-limit reached, continued periodic retry activity, manual retry attempts, resumed retry cycles after failed manual retry, recovery, trading-schedule-based connection stop or suppression events, and related notification events remain available for 90 days. | Aligns to project retention guidance in `BR13` and `NFR3`. |

## 8. Interfaces and Integration Requirements (optional)

Use `IR1`, `IR2`, ... for integration requirements.

| ID | Requirement | System | Contract | Acceptance criteria | Notes |
| --- | --- | --- | --- | --- | --- |
| IR1 | The platform shall integrate with `IG` authentication capabilities required to establish and maintain a working `IG` demo session. | `IG` | API | Given valid `IG` demo credentials and connectivity, the platform can establish a session and detect when the session is no longer usable. | Aligns to `BR2`, `UC2`. |
| IR2 | The platform shall use environment-specific `IG` integration settings consistent with the selected environment. | `IG` | Configuration + API | Demo environment selection results in distinct integration behavior and records; environment context remains explicit. | Aligns to `BR1`, `SAR1`. |
| IR3 | The platform shall enforce Test-platform restrictions on `IG` live usage before attempting live integration actions. | `IG` | Configuration + API | In the Test platform environment, the platform does not initiate live-environment authentication or equivalent live-use actions; blocked attempts are recorded. | Aligns to `SAR8`. |
| IR4 | The platform shall integrate with the project’s notifications channel for auth/session failure, retry-limit, recovery, and blocked live-attempt events. | Notifications channel | Message delivery | When a supported auth/session or blocked live-attempt event occurs, the platform can issue a notification containing the required operational summary and environment context. | `../systems-analysis.md` identifies the notifications channel as email. |
| IR5 | The platform shall integrate with a durable data store for operator-managed configuration and configuration audit records. | Configuration data store | Data store + API + UI | Startup reads the current configuration from the data store; supported Blazor UI updates persist configuration changes to the data store; secret values are captured through a secure write-only flow and are never returned in plain text. | Supports durable configuration management for this work package. |

## 9. Testing Requirements

Use `TR1`, `TR2`, ... for testing requirements.

| ID | Requirement | Acceptance criteria | Notes |
| --- | --- | --- | --- |
| TR1 | Requirements coverage shall verify environment selection behavior. | Tests demonstrate explicit selection, visible current environment, and separated environment-specific records/configuration. | Trace to `FR1`-`FR3`. |
| TR2 | Requirements coverage shall verify `IG` demo authentication and session lifecycle behavior. | Tests demonstrate `IG` demo session establishment, invalid/expired session detection, safe recovery, degraded startup retry behavior, continued periodic retry behavior after the initial retry sequence is exhausted, manual retry behavior, resumed automatic retry behavior after failed manual retry, and behavior when the configured initial retry limit is reached. | Trace to `FR4`-`FR6`, `FR12`, `FR14`, `FR15`, `FR16`. |
| TR3 | Requirements coverage shall verify secrets protection behavior for auth/session flows. | Tests or inspections confirm that auth/session outputs do not expose secrets. | Trace to `FR7`, `SR2`, `SR3`. |
| TR4 | Requirements coverage shall verify Test-platform live-option restriction behavior. | Tests demonstrate that in the Test platform environment the `IG` live option is visible but unavailable, and attempted use is blocked and recorded. | Trace to `FR8`, `SR4`, `IR3`. |
| TR5 | Requirements coverage shall verify that `IG` live authentication is not attempted in this work package. | Tests demonstrate that supported flows authenticate only against `IG` demo and that live-auth paths are unavailable or blocked by design. | Trace to `FR9`, `SR1`. |
| TR6 | Requirements coverage shall verify auth/session notification behavior. | Tests demonstrate that failure-state transitions and successful recovery produce notifications with the required context and without secrets. | Trace to `FR10`, `NF5`, `SR5`, `IR4`. |
| TR7 | Requirements coverage shall verify blocked live-attempt notification behavior. | Tests demonstrate that each blocked live attempt in the Test platform environment produces a notification with the required context and without secrets. | Trace to `FR11`, `NF5`, `SR5`, `IR4`. |
| TR8 | Requirements coverage shall verify degraded-state UI behavior. | Tests demonstrate that the full platform UI remains available during degraded startup/auth state and that auth-dependent actions are blocked with a visible reason. | Trace to `FR13`, `NF2`. |
| TR9 | Requirements coverage shall verify re-notification behavior for a new retry cycle started by manual retry. | Tests demonstrate that when a manual retry starts a new retry cycle and that cycle fails again, a fresh failure notification is issued for the new cycle, and recovery is still notified if it occurs later. | Trace to `FR17`, `NF5`, `SR5`. |
| TR10 | Requirements coverage shall verify degraded retry progress visibility. | Tests demonstrate that while initial automatic retry or periodic retry is active, the operator can see the current retry attempt number or retry mode and next scheduled retry time, and that those values update correctly as retry state changes. | Trace to `FR18`, `NF2`. |
| TR11 | Requirements coverage shall verify retry-limit notification behavior. | Tests demonstrate that whenever the initial retry sequence is exhausted and the platform remains degraded, a retry-limit notification is issued with the required context, manual-retry guidance, last retry attempt count, last scheduled delay attempted, and periodic retry delay now in effect, without secrets. | Trace to `FR19`, `NF5`, `SR5`, `IR4`. |
| TR12 | Requirements coverage shall verify durable configuration-management and secure credential-update behavior. | Tests demonstrate that supported configuration values can be reviewed and updated through the Blazor UI, persisted to the configuration data store, reloaded by the platform, and audited; tests also confirm stored `IG` credentials remain protected, are updated through a write-only flow, and are never disclosed in API or UI responses. | Trace to `FR20`, `NF2`, `NF4`, `SR2`, `SR3`, `IR5`. |
| TR13 | Requirements coverage shall verify trading-schedule configuration and connection-window behavior. | Tests demonstrate that trading start-of-day and end-of-day values, permitted trading days, weekend treatment, and bank-holiday exclusions can be reviewed and updated, that the platform maintains an `IG` session only during the configured trading schedule, that auth retry activity is suppressed outside the configured trading schedule, and that the current in-schedule or out-of-schedule state is visible without secrets. | Trace to `FR21`, `FR22`, `NF1`, `NF2`. |

## 10. Operational Requirements (optional)

Use `OR1`, `OR2`, ... for operational requirements.

| ID | Requirement | Acceptance criteria | Notes |
| --- | --- | --- | --- |
| OR1 | The platform shall surface the current environment, trading-schedule state, and auth/session state for operator review during runtime. | The operator can determine the selected environment, configured trading schedule, whether the trading schedule is currently active, and whether the platform is healthy or in degraded startup/auth state without inspecting raw credentials or source code. | Supports safe operation. |
| OR2 | The platform shall record notable auth/session state transitions for later review. | Authentication success, failure, degraded startup, initial retry activity, retry-limit reached, continued periodic retry activity, manual retry attempts, resumed retry cycles after failed manual retry, expiry, recovery, and trading-schedule-based connection stop or suppression events are retained and can be reviewed with environment context. | Supports auditability and troubleshooting. |
| OR3 | The platform shall record blocked attempts to use the `IG` live environment from the Test platform environment. | A blocked attempt includes enough context for later review without exposing secrets. | Supports the safeguard in `SAR8`. |
| OR4 | The platform shall notify the project owner on supported auth/session failure, each retry-limit reached event that transitions the platform into periodic retry mode, recovery, each blocked live-attempt event, and each new failure cycle started after a manual retry. | Notifications are issued immediately for the supported events and can be reviewed against the corresponding recorded state transitions. | Limited in this work package to auth/session and blocked live-attempt events. |
| OR5 | The platform shall keep the UI available during degraded startup/auth state while clearly identifying auth-dependent actions that are blocked. | The operator can continue to access the UI and distinguish blocked auth-dependent actions from available actions. | Supports operator usability during degraded state. |
| OR6 | The platform shall show degraded retry progress details to the operator while initial automatic retry or periodic retry is active during the configured trading schedule. | The operator can see the current retry attempt number or retry mode and the next scheduled retry time without leaving the operator surface while retry activity is active during the configured trading schedule. | Supports runtime intervention awareness. |
| OR7 | The platform shall let the operator review and update supported configuration data from the Blazor UI without exposing stored `IG` secrets. | The operator can review active non-secret configuration in the UI, submit supported changes that persist to the configuration data store, update trading-schedule values including trading days, weekend treatment, and bank-holiday exclusions, update credential values through a secure write-only flow, and understand when a startup-fixed change will apply on the next platform start; the UI never reveals the currently stored secret values. | Supports operator-managed configuration and secure credential handling. |
| OR8 | The platform shall make trading-schedule-based connection state visible to the operator. | The operator can determine the configured trading start-of-day and end-of-day values, permitted trading days, weekend treatment, bank-holiday exclusions, whether the platform is currently inside or outside the configured trading schedule, and whether the absence of an active `IG` session is expected because the trading schedule is inactive. | Supports safe operation and operator understanding. |

## 11. Assumptions, Risks, and Dependencies

### 11.1 Assumptions

- An `IG` demo account and valid demo credentials are available.
- A durable configuration and operational data store is available for this work package.
- `IG` continues to provide the authentication/session capabilities needed for demo usage.
- The Blazor UI will be the operator surface for reviewing and updating supported configuration data.
- A configurable trading schedule defined by start-of-day, end-of-day, permitted trading days, weekend treatment, and bank-holiday exclusions is sufficient for this work package.
- Later work packages will consume this package’s environment and session state rather than redefine them.

### 11.2 Risks

- **Environment confusion risk**: Inadequate separation between demo and live contexts could lead to unsafe operation.
  - **Mitigation**: Require explicit selection, make the selected environment observable, separate environment-specific records and configuration, and block live usage in the Test platform environment.
- **Session continuity risk**: Session expiry or invalid-session handling may interrupt dependent capabilities.
  - **Mitigation**: Detect invalid sessions, record state transitions, restore a working session safely, and notify the project owner on supported auth/session events.
- **Secrets exposure risk**: Auth-related diagnostics could leak sensitive information.
  - **Mitigation**: Record only non-sensitive auth/session events and keep sensitive values out of logs, records, and notifications.
- **Configuration-management risk**: Operator-driven configuration changes could persist unsafe values or expose stored secrets.
  - **Mitigation**: Validate configuration changes before commit, audit each change, use write-only secret update fields, and protect persisted credentials at rest.
- **Trading-schedule risk**: Incorrect trading-schedule configuration could prevent intended broker connectivity or keep the platform disconnected when the operator expects trading activity.
  - **Mitigation**: Make trading-schedule values visible, validate start-of-day, end-of-day, permitted trading-day, weekend, and bank-holiday changes, and expose whether the platform is currently inside or outside the configured trading schedule.
- **Deferred live-auth risk**: Deferring `IG` live authentication may require later extension of environment/auth flows.
  - **Mitigation**: Keep environment selection and session semantics explicit and testable so live-auth support can be added later without redefining core behavior.
- **Notification coupling risk**: Including notifications here may overlap with the later notifications work package.
  - **Mitigation**: Limit notification scope in this package to auth/session and blocked live-attempt events and keep the behavior traceable and testable for reuse later.

### 11.3 Dependencies

- `IG` demo authentication/session endpoints and related account capabilities.
- A durable data store for configuration storage, configuration audit history, and operational records.
- Secure storage and retrieval of credentials/secrets used by the platform, including protected persistence for `IG` credentials.
- The project notifications channel described in `../systems-analysis.md`.
- Project-level business requirements in `../business-requirements.md`.
- Project-level systems analysis in `../systems-analysis.md`.

## 12. Open Questions

None at this stage.

## 13. Appendix (optional)

- Related business requirements: `BR1`, `BR2`, `BR12`
- Related use cases: `UC1`, `UC2`
- Related analysis requirements: `SAR1`, `SAR2`, `SAR8`
- Candidate work package reference: `002-environment-and-auth-foundation`