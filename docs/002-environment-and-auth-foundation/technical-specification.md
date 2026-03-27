# Technical Specification

This document describes how work package 002-environment-and-auth-foundation will be implemented so the platform can safely select an IG environment, manage operator-editable configuration in SQL Server, establish and recover an authenticated IG demo session, and expose the resulting operational state for later work packages.

## 1. Summary

- **Source**: See `requirements.md` for canonical work metadata, work-package scope, and requirement identifiers. See `../business-requirements.md` for project-level business context and `../systems-analysis.md` for the related system analysis decisions and constraints.
- **Status**: draft
- **Input**: `requirements.md`, `../business-requirements.md`, and `../systems-analysis.md`
- **Output**: `delivery-plan.md`

## 2. Problem and Context

### 2.1 Problem statement

The platform needs a safe and explicit foundation for choosing the intended IG environment, managing operator-editable configuration in `MS SQL Server`, and establishing and maintaining an authenticated IG demo session that later capabilities can trust. The foundation must preserve environment separation, protect secrets, expose degraded and recovered session states, support an initial aggressive retry sequence followed by continued periodic retry and manual restart, allow supported configuration updates through the Blazor UI, and provide enough operational visibility to keep the platform usable while auth-dependent actions remain blocked.

### 2.2 Assumptions

- The existing solution baseline remains an Aspire-orchestrated .NET 10 application with an ASP.NET Core API project as the current operator-facing service boundary.
- Operator-managed platform, broker, retry, and notification configuration is stored in SQL Server and loaded by the API at startup.
- IG demo authentication is performed with environment-specific credentials and session tokens held securely, with protected credential values stored as encrypted SQL-backed secrets or SQL-backed secret references resolved by the API.
- Runtime switching between IG demo and IG live remains out of scope for this work package; the selected broker environment is fixed at startup.
- A durable store is required for environment-scoped operational records because the work package must retain reviewable auth and notification events for 90 days.
- The notifications channel identified in `../systems-analysis.md` is email, and the concrete delivery settings will be provided through environment-specific configuration rather than hard-coded into the application.
- Email delivery in this work package will use Azure Communication Services Email behind a notification provider abstraction so the domain workflow is not coupled directly to a single transport implementation.
- Operator-facing user authentication is not changed by this work package; the scope here is broker authentication to IG and the operator surface needed to observe and control retry behavior.
- This work package will introduce a dedicated .NET 10 Blazor Web App using server interactivity for degraded-state visibility, retry progress, and manual-retry actions, while keeping broker-auth orchestration in the API service.
- The explicit initial `IG` environment selection is part of the SQL-backed configuration model, and the UI will present the active value as operational context while also supporting controlled configuration updates for subsequent startup.
- The Blazor Web App will include a configuration-management surface for supported non-secret settings and write-only `IG` credential updates.
- After the initial exponential retry sequence is exhausted, the platform will continue background reconnect attempts on a configurable periodic delay that is initially set to 5 minutes.

### 2.3 Constraints

- The implementation must satisfy `FR1`-`FR20`, `NF1`-`NF5`, `SR1`-`SR5`, `DR1`, `IR1`-`IR5`, `TR1`-`TR12`, and `OR1`-`OR7` from `requirements.md`.
- The platform must authenticate only to IG demo in this work package and must not attempt IG live authentication.
- In the Test platform environment, the IG live option must remain visible but unavailable, and blocked live attempts must be recorded and notified.
- Retry behavior must use the fixed initial exponential-backoff profile defined by the requirements: initial delay 1 second, multiplier x2, cap 60 seconds, default maximum 5 automatic retries after the initial failed startup attempt, followed by a configurable periodic retry delay initially set to 5 minutes.
- Operator-managed configuration must be persisted in SQL Server, and supported updates must flow through the Blazor UI and API rather than static file edits.
- The implementation must preserve the existing Aspire service-defaults approach for OpenTelemetry, health endpoints, and resilience defaults.
- Secrets, raw credentials, API keys, and session tokens must never be written to logs, records, notifications, or API responses.
- Stored `IG` credential values must remain protected at rest and must never be returned in plaintext by the UI or API after capture.
- Documentation and code layout must align with repo guidance for vertical slices, test placement under `test/`, and iterative work artifacts under `docs/002-environment-and-auth-foundation/`.

## 3. Proposed Solution

### 3.1 Approach

Implement the environment and broker-auth foundation as a two-project operator experience built around the existing backend service boundary:

1. **SQL Server-backed configuration management and startup validation** to require an explicit broker-environment choice from the configuration database, enforce Test-platform live restrictions before any live action is attempted, and manage separate environment-specific settings plus protected secret values or secret references.
2. **A session-supervision background workflow** to establish the IG demo session, detect invalid or expired sessions, drive automatic retry cycles with the required initial exponential backoff followed by periodic retry, expose degraded-state status, and allow an operator-triggered manual retry that resets the retry budget.
3. **A dedicated Blazor Web App with server interactivity** to expose the selected environment, configuration state, auth/session state, retry progress, blocked reasons, manual-retry availability, configuration update flows, and recorded auth-related events without exposing secrets.

The API will host the background session supervisor, configuration read/write endpoints, protected credential-update flows, and the persistence logic for SQL-backed configuration plus environment-scoped operational records. A new .NET 10 Blazor Web App with server interactivity will consume those APIs and remain available during degraded auth state so the operator can review status, update supported configuration, and trigger manual retry when appropriate. The selected broker environment will be loaded from the configuration database at startup, displayed by the UI as active context, and updated only through validated operator flows rather than direct file edits. Changes to startup-fixed settings such as platform environment and broker environment will be persisted for the next startup and will not introduce runtime environment switching in this work package. Server interactivity keeps the initial implementation simpler than a separate WebAssembly client, allows straightforward secure server-side access patterns for the UI application, and still provides the responsive operator experience required by the work package. Durable configuration, current state, and records should be stored in SQL Server to align with repository defaults for relational persistence and to support the 90-day retention requirement. Authentication and notification interactions with external systems should be isolated behind focused infrastructure components, with email dispatch routed through a notification provider abstraction backed by Azure Communication Services Email, so configuration updates, retry logic, redaction, retry-phase transitions, and state transitions remain testable.

### 3.2 Alternatives considered

| Option | Summary | Pros | Cons | Decision rationale |
| ------ | ------- | ---- | ---- | ------------------ |
| A | Extend only the existing API service and treat HTTP endpoints as the sole operator surface | Smallest change to the current solution; easiest backend-only delivery path | Does not provide the explicit full-platform UI experience called for by `FR13`, `FR15`, and `FR18`; would likely force later UI rework | Rejected because the chosen scope now includes a dedicated operator UI in this work package |
| B | Add a dedicated Blazor Web App with server interactivity alongside the API service and keep broker-auth orchestration in the API | Matches repo front-end preference for Blazor; gives a clear operator surface for degraded-state visibility and manual retry; keeps backend orchestration separate from presentation concerns; avoids separate client-distribution complexity | Adds a new project and UI testing scope in this work package | Accepted because it best satisfies the UI-related requirements while preserving a clean API-backed architecture |
| C | Introduce a separate broker-session worker service plus the API service and UI from the start | Clear runtime separation between background supervision, APIs, and UI; can scale independently later | Adds another project, hosting surface, and inter-service communication before the domain is proven; increases delivery overhead for an early work package | Rejected for now because the current scope is foundational and the solution is still small |
| D | Keep environment selection and auth state only in configuration and in-memory state, with minimal logging | Lowest initial implementation effort | Does not satisfy durable event retention, traceable retry behavior, reviewability, or the operator-UI requirements; restart would lose degraded-state history | Rejected because it cannot meet the auditability, observability, and retention requirements |

### 3.3 Architecture

The work package adds a broker-auth and configuration-management control plane to the existing API application and a dedicated Blazor Web App with server interactivity. SQL-backed configuration selects the platform environment, broker environment, retry settings, and notification settings at startup. A startup validator blocks unsupported combinations, especially IG live in the Test platform environment, and the application does not proceed without a configured explicit broker-environment value loaded from SQL Server. A hosted session supervisor then establishes the IG demo session, updates a persisted session-state projection, records state-transition events, and dispatches notifications when required. The supervisor operates in two retry phases while degraded: an initial exponential retry sequence and a continued periodic retry mode after that sequence is exhausted. The Blazor UI reads the current state and active configuration from the API, keeps degraded-state views available, presents the active broker environment as runtime context, offers controlled configuration editing, shows the current retry phase and next scheduled retry time, and exposes the manual-retry action when a retry is not already running.

- **Components**:
  - `Operator UI` .NET 10 Blazor Web App with server interactivity for environment visibility, degraded-state banners, retry progress, blocked-action messaging, and manual-retry interaction
  - `ConfigurationStore` for SQL-backed platform, broker, retry, and notification configuration plus change auditing
  - `ProtectedCredentialStore` for encrypted `IG` credential values or secret references resolved by the API
  - `EnvironmentConfiguration` loading and validation for platform environment, broker environment, and environment-specific IG settings sourced from SQL-backed configuration
  - `IgAuthenticationClient` for IG demo session establishment and session-validity checks
  - `IgSessionSupervisor` background service for startup auth, expiry detection, retry cycles, and recovery
  - `AuthStateStore` for current environment/session/retry status projections
  - `OperationalEventLedger` for append-only auth, retry, blocked-live, and notification records
  - `NotificationDispatcher` for failure, retry-limit reached, recovery, and blocked live notifications
  - `EmailNotificationProvider` abstraction with one Azure Communication Services Email implementation for this work package
  - `GetPlatformStatus`, `GetPlatformConfiguration`, `UpdatePlatformConfiguration`, and `TriggerManualAuthRetry` feature slices for operator visibility and control
- **Data flows**:
  - Startup reads platform and broker settings from SQL Server, validates the environment combination, and refuses to start without an explicit broker-environment choice supplied through the configuration store
  - The Blazor UI queries configuration endpoints to show active non-secret settings and secret placeholders, then submits validated configuration updates back to the API for persistence and audit, with startup-fixed changes marked for next-start application
  - On successful startup, the session supervisor authenticates to IG demo, records the outcome, and updates the current session projection
  - On auth/session failure, the supervisor marks the platform degraded, records the state transition, emits a failure notification once per retry cycle, and schedules the next retry attempt
  - When the initial automatic retry sequence is exhausted, the supervisor records the transition into periodic retry mode, emits a retry-limit notification, and continues scheduling reconnect attempts on the configured periodic delay until recovery or manual retry
  - The Blazor UI queries status endpoints to show environment, session state, retry attempt number, next scheduled retry time, blocked reasons, and manual-retry availability
  - When manual retry is triggered from the UI, the supervisor starts a new attempt immediately without waiting for the next periodic retry and resets the automatic retry budget if the manual attempt fails
  - Notification dispatch results are recorded without persisting secret delivery credentials or raw IG tokens
  - Configuration-change results are recorded without persisting returned secret values or raw decrypted credential material
- **Dependencies**:
  - IG REST authentication/session endpoints for demo access
  - IG streaming-session details as part of session-establishment metadata, where needed for future work packages
  - SQL Server for durable configuration, protected credential storage metadata, operational events, and notification history
  - Azure Communication Services Email configured externally for the notifications channel
  - Blazor Web App hosting integrated into the Aspire app model and connected to the API over internal service discovery or configured HTTP base address
  - Aspire AppHost orchestration, health endpoints, OpenTelemetry, and resilience defaults already present in the solution

## 4. Requirements Traceability

Map requirements to implementation details so it is easy to verify coverage.

| Requirement ID | Requirement | Implementation notes | Validation approach |
| -------------- | ----------- | -------------------- | ------------------- |
| FR1 | Require explicit IG environment selection for initial configuration and startup | Load the broker-environment choice from SQL-backed configuration, validate that the value is present, and present the active value in the UI | Configuration-loading tests and integration tests that verify startup fails when no broker environment is configured |
| FR2 | Make the selected IG environment observable during operation and in recorded activity | Include broker environment in the platform-status API, structured logs, operational-event records, and notifications | Integration tests for status responses and persistence tests that confirm environment-tagged records |
| FR3 | Keep configuration and recorded activity separated by selected IG environment | Use environment-scoped SQL-backed configuration records and persist environment context with every auth/session operational record | Persistence tests that confirm demo and live-tagged configuration and event records remain distinct and are queryable by environment |
| FR4 | Authenticate to IG demo and establish a working session | Implement an IG demo authentication client and session supervisor that performs startup authentication and persists the resulting state transition | Integration tests with an IG test double that verify successful auth creates an active session state |
| FR5 | Detect expired, invalid, or unusable IG demo sessions | Add session-validity checks and error classification in the supervisor so rejected or expired sessions trigger a degraded transition | Unit tests for failure classification and integration tests that simulate session rejection and verify degraded state |
| FR6 | Restore a working IG demo session after session failure without corrupting platform state | Reuse environment-scoped configuration, re-authenticate through the supervisor, and update only session-state projections and new ledger entries | Integration tests covering failure then recovery without environment-context loss |
| FR7 | Record authentication and session events without exposing secrets | Persist only redacted event payloads and structured summaries; protect stored credential values at rest; never store raw credentials or tokens in logs, events, API output, or UI views | Unit tests for redaction and output inspection tests for logs, records, API responses, and UI state |
| FR8 | In Test platform environment, show IG live as unavailable and prevent its selection or use | Expose live-option availability metadata in the operator status surface and block live actions through startup and command validators | Integration tests for Test-platform status and blocked live-attempt behavior |
| FR9 | Do not attempt IG live authentication in this work package | Restrict the authentication client and supervisor to demo flows only and reject live-auth paths before any IG call is made | Unit and integration tests that verify no live-auth request is issued |
| FR10 | Notify the project owner on auth/session failure detection and recovery | Emit notification commands on failure-state transition and on recovery transition with concise environment-tagged summaries through the notification provider abstraction | Notification pipeline tests and integration tests that verify failure and recovery notifications |
| FR11 | Notify the project owner on every blocked attempt to use IG live from Test platform environment | Record and notify each blocked live-use attempt with timestamp and environment context through the notification provider abstraction | Command tests and integration tests that verify one notification per blocked attempt |
| FR12 | Start in observable degraded state and continue retrying when startup auth cannot be established | Allow the API to finish startup, mark readiness as degraded for auth-dependent operations, and launch retry scheduling in the background across the initial exponential phase and the later periodic phase | Integration tests that verify degraded startup, visible state, and continued retry scheduling |
| FR13 | Keep the full platform UI available during degraded auth state while blocking auth-dependent actions with a visible reason | Render degraded status, blocked reasons, and auth-dependent action states in the Blazor Web App while enforcing command guards in the API | UI functional tests and API contract tests for blocked actions and visible reasons |
| FR14 | Use initial exponential backoff followed by periodic retry with default initial delay 1 second, default max automatic retries 5, cap 60 seconds, and periodic delay default 5 minutes | Implement a retry policy component with configurable initial delay, retry limit, multiplier, delay cap, and periodic retry delay | Unit tests for retry timing and integration tests that verify phase transition into periodic retry |
| FR15 | Allow operator-triggered manual retry after initial retry limit reached without restarting platform | Add a manual-retry command endpoint and a Blazor Web App action that is enabled during post-initial-limit degraded retry and starts retry immediately | Integration and UI functional tests for retry availability, immediate retry start, and no confirmation flow |
| FR16 | Reset the automatic retry budget when a manual retry fails after retry limit reached | Model retry cycles explicitly so a failed manual retry opens a fresh automatic cycle with the same policy values, including later periodic retry if needed | Unit tests for cycle reset logic and integration tests for resumed automatic retries |
| FR17 | Issue a fresh failure notification for a new retry cycle started by manual retry | Track retry-cycle identifiers and failure-notified state per cycle so each new cycle can notify once again | Unit tests for per-cycle notification state and integration tests for repeated failure-cycle notifications |
| FR18 | Show current retry attempt number and next scheduled retry time while retrying | Persist a retry-state projection that includes current attempt number, current retry phase, periodic delay, and next scheduled retry timestamp, expose it through the status API, and render it in the Blazor Web App | API contract tests and UI functional tests for retry-progress visibility across both retry phases |
| FR19 | Send an explicit retry-limit notification with manual-retry guidance | Emit a dedicated retry-limit notification containing exhaustion of the initial retry sequence, manual-retry guidance, last attempt count, last scheduled delay, and the periodic retry delay now in effect through the notification provider abstraction | Notification tests and integration tests that inspect retry-limit notification content |
| FR20 | Store operator-managed configuration in SQL Server and allow Blazor UI updates while keeping IG credentials secure | Add SQL-backed configuration entities, audited update commands, and write-only credential-management flows that return only redacted configuration data to the UI and distinguish startup-fixed changes that apply on the next startup | API, UI, and persistence tests that verify configuration review/update behavior, SQL persistence, next-start application of startup-fixed settings, and secret-safe credential handling |
| NF1 | Support sustained safe operation under session expiry and transient auth failure | Use supervised recovery, bounded initial retries, continued periodic retry, preserved environment context, and degraded-state gating instead of failing the whole app | Integration tests for expiry, invalid-session recovery, and continued degraded periodic retry state |
| NF2 | Expose enough environment, configuration, and session-state information for operator understanding | Provide current-environment, active non-secret configuration, session-state, retry-progress, retry phase, blocked-reason, and manual-retry availability read models plus durable records | API and persistence tests that verify each required signal is available during runtime and after events |
| NF3 | Provide a stable basis for later work packages | Keep environment, configuration, session, and retry behavior in isolated components and documented contracts that later packages can reuse | Design review plus integration tests against stable API contracts |
| NF4 | Protect secrets and avoid leaking sensitive values in storage and operational outputs | Centralize secret access through protected configuration/secret providers, encrypt or protect persisted credentials, and redact all outbound logging and persistence payloads | Automated redaction tests and manual review of stored data and structured outputs in integration runs |
| NF5 | Issue supported notifications immediately | Dispatch notifications synchronously from state transitions or through a minimal outbox processed without batching delays | Integration tests that verify notifications are recorded and dispatched on the triggering transition |
| SR1 | Only establish an IG session using valid configured credentials for the selected and supported environment | Bind environment-specific credential settings and restrict supported auth to IG demo only | Configuration and integration tests for demo credentials and blocked live flows |
| SR2 | Prevent sensitive authentication material from appearing in logs, records, notifications, API responses, UI views, or plaintext persisted configuration | Use redacted logging scopes, scrubbed persistence payloads, protected credential storage, and notification mappers that exclude sensitive fields | Unit tests for sanitization and integration tests that inspect persisted and emitted outputs |
| SR3 | Support credential rotation without losing reviewability of prior records | Store protected credential values or secret references separately from historical records so new auth attempts use current secrets while past events remain readable and current secret values remain undisclosed in the UI | Integration tests that change configured credentials between runs and verify records remain intact |
| SR4 | Fail safely when auth cannot complete or forbidden live action is attempted in Test | Transition to degraded state for auth failure and reject blocked live actions before any live call is attempted | Integration tests for degraded startup and blocked live-attempt flows |
| SR5 | Include useful notification context without exposing secrets | Build notifications from redacted state-transition models that include only event type, timestamp, environment context, and summary | Notification content tests for all supported event types |
| DR1 | Retain environment selection, configuration-change, and auth/session records for 90 days | Store configuration-audit records, operational events, and notification records with environment context and apply a 90-day retention policy | Persistence tests and retention-policy verification |
| IR1 | Integrate with IG authentication capabilities required for demo session continuity | Implement an IG authentication adapter around the documented session-establishment flow and unusable-session handling | Integration tests against an IG-compatible test double |
| IR2 | Use environment-specific IG integration settings consistent with the selected environment | Separate demo and live settings in configuration and pass the selected environment context through the auth adapter and state ledger | Configuration tests and persistence tests by environment |
| IR3 | Enforce Test-platform restrictions before attempting live integration actions | Validate platform-environment rules before any live-auth or live-use integration path executes | Integration tests that verify no external live call is made when blocked |
| IR4 | Integrate with the project notifications channel for auth/session and blocked-live events | Implement an email notification provider abstraction with an Azure Communication Services Email adapter driven by the notification dispatcher and environment-specific delivery settings | Notification integration tests that verify required event mapping and dispatch |
| IR5 | Integrate with SQL Server for operator-managed configuration and configuration-audit persistence | Implement SQL-backed configuration repositories and audited update flows consumed by the Blazor UI and API | Integration tests that verify configuration load, update, and audit persistence behavior |
| TR1 | Verify environment selection behavior | Add functional and integration tests for explicit selection, visible environment, and separated records/configuration | Requirement-driven test cases mapped to `FR1`-`FR3` |
| TR2 | Verify IG demo authentication and session lifecycle behavior | Add tests for session establishment, invalid-session detection, recovery, degraded startup, initial retry behavior, periodic retry behavior, manual retry, and resumed automatic retries | Requirement-driven test cases mapped to `FR4`-`FR6`, `FR12`, `FR14`-`FR16` |
| TR3 | Verify secrets protection behavior for auth/session flows | Add sanitization-focused unit and integration tests that inspect records, notifications, and logs | Requirement-driven test cases mapped to `FR7`, `SR2`, and `SR3` |
| TR4 | Verify Test-platform live-option restriction behavior | Add tests for visible-but-disabled live option, blocked use attempts, and recorded events | Requirement-driven test cases mapped to `FR8`, `SR4`, and `IR3` |
| TR5 | Verify that IG live authentication is not attempted | Add tests around command guards and integration-call suppression for live-auth paths | Requirement-driven test cases mapped to `FR9` and `SR1` |
| TR6 | Verify auth/session notification behavior | Add notification tests for failure and recovery state transitions and their secret-safe payloads | Requirement-driven test cases mapped to `FR10`, `NF5`, `SR5`, and `IR4` |
| TR7 | Verify blocked live-attempt notification behavior | Add tests that assert each blocked attempt produces a notification with the required context | Requirement-driven test cases mapped to `FR11`, `NF5`, `SR5`, and `IR4` |
| TR8 | Verify degraded-state UI behavior | Add functional tests for status visibility, blocked auth-dependent actions, and visible reason messages | Requirement-driven test cases mapped to `FR13` and `NF2` |
| TR9 | Verify re-notification behavior for a new retry cycle started by manual retry | Add retry-cycle tests that prove a new cycle can emit a fresh failure notification and later recovery | Requirement-driven test cases mapped to `FR17`, `NF5`, and `SR5` |
| TR10 | Verify degraded retry progress visibility | Add status-contract and functional tests for retry attempt number, retry phase, and next scheduled retry time updates | Requirement-driven test cases mapped to `FR18` and `NF2` |
| TR11 | Verify retry-limit notification behavior | Add tests that inspect retry-limit notification content, including manual-retry guidance, last retry details, and the periodic retry delay now in effect | Requirement-driven test cases mapped to `FR19`, `NF5`, `SR5`, and `IR4` |
| TR12 | Verify SQL-backed configuration management and secure credential updates | Add tests for configuration read/write endpoints, Blazor configuration UI flows, SQL persistence, audit trails, write-only secret updates, and redacted responses | Requirement-driven test cases mapped to `FR20`, `NF2`, `NF4`, `SR2`, `SR3`, and `IR5` |
| OR1 | Surface current environment and auth/session state during runtime | Provide the platform-status read model in the API and render degraded-state indicators in the Blazor operator UI | API contract tests and operator-surface functional tests |
| OR2 | Record notable auth/session state transitions for later review | Persist append-only operational events for auth success, failure, initial retries, transition into periodic retry, periodic retries, manual retry, exhaustion of initial retry sequence, expiry, and recovery | Persistence and integration tests over event history |
| OR3 | Record blocked live attempts in Test platform environment | Write blocked-live events to the ledger with environment context and timestamp | Command tests and persistence tests |
| OR4 | Notify the project owner on supported auth/session, retry-limit, continued periodic retry, recovery, blocked-live, and new failure-cycle events | Use the notification dispatcher with event-specific policies for failure, recovery, transition into periodic retry after initial retry exhaustion, and blocked live attempts through the notification provider abstraction | Notification pipeline tests and integration tests for each event type |
| OR5 | Keep the UI available during degraded state while clearly identifying blocked actions | Expose degraded-state metadata and blocked reasons through the API while keeping the Blazor Web App available and non-auth views callable | Functional tests for visible availability and blocked action messaging |
| OR6 | Show retry progress details while automatic or periodic retry is active | Publish retry attempt number, retry phase, and next scheduled retry time in the status API and refresh the Blazor Web App as retry state changes | Functional and integration tests for retry-progress transitions |
| OR7 | Let the operator review and update supported configuration from the Blazor UI without exposing stored IG secrets | Provide configuration read models, audited update commands, and write-only secret update controls in the Blazor Web App | Functional, API, and persistence tests for configuration review, update, and secret-safe rendering |

## 5. Detailed Design

Describe the implementation at a level that enables another developer to build it.

### 5.1 Public APIs / Contracts

| Area | Contract | Example | Notes |
| ---- | -------- | ------- | ----- |
| UI | `/status` page in the Blazor Web App | Shows platform environment, configured broker environment, current auth/session state, degraded banner, retry progress, current retry phase, live-option availability, and blocked reasons | Primary operator view for degraded-state visibility |
| UI | `/configuration` page in the Blazor Web App | Shows active non-secret configuration, write-only credential update fields, validation messages, last configuration-change details, and indicators for startup-fixed changes that apply on next restart | Primary operator view for SQL-backed configuration management |
| UI | Manual retry action in the Blazor Web App | Button is enabled only after retry exhaustion and issues the backend manual-retry command with no confirmation step | Supports `FR15` and `FR16` |
| REST | `GET /api/platform/status` | Response includes platform environment, broker environment, live-option availability, auth/session state, degraded flag, blocked reasons, retry state, manual-retry availability, and last transition timestamp | Primary operator read model for `FR2`, `FR13`, `FR18`, and `OR1` |
| REST | `GET /api/platform/configuration` | Response includes active non-secret configuration values, secret placeholders, and last-updated metadata | Supports `FR20` and `OR7` without returning stored secrets |
| REST | `PUT /api/platform/configuration` | Request contains supported configuration changes and optional write-only secret fields; response returns redacted updated configuration plus whether a restart is required for startup-fixed changes | Persists configuration to SQL Server and records an audit entry |
| REST | `POST /api/platform/auth/manual-retry` | Request has no body; response returns `202 Accepted` with the new retry-cycle identifier or `409 Conflict` when retry is not allowed | Command is available only after retry exhaustion and while no retry attempt is in progress |
| REST | `GET /api/platform/events?category=auth&environment=demo` | Returns recent redacted auth/session and blocked-live events with environment context | Supports operator review without exposing secrets |
| Internal event | `PlatformConfigurationUpdated` | `RetrySettingsChanged`, `NotificationSettingsChanged`, `BrokerEnvironmentChanged`, `CredentialsUpdated` | Internal application event used to persist configuration-audit entries and support observability |
| Internal event | `AuthStateTransitionRecorded` | `FailureDetected`, `RetryScheduled`, `RetryExhausted`, `Recovered`, `BlockedLiveAttempt` | Internal application event used to persist ledger entries and dispatch notifications |
| Internal contract | `IgAuthenticateRequest` / `IgAuthenticateResult` | Request uses configured demo credentials; result returns redacted session metadata, expiry, and streaming details | Keeps IG auth integration isolated from retry and notification logic |

### 5.2 Data Model

| Entity/Concept | Fields | Constraints | Notes |
| -------------- | ------ | ----------- | ----- |
| PlatformConfiguration | `ConfigurationId`, `PlatformEnvironment`, `BrokerEnvironment`, `NotificationSettings`, `RetrySettings`, `UpdatedAtUtc`, `UpdatedBy`, `Version`, `RestartRequired` | Active row is required before auth supervision starts; optimistic concurrency should protect updates | SQL-backed source of truth for operator-managed non-secret configuration |
| ProtectedCredential | `CredentialId`, `BrokerEnvironment`, `CredentialType`, `ProtectedValue`, `ProtectionMetadata`, `UpdatedAtUtc`, `UpdatedBy` | Stored value must be encrypted or represent a secret reference; existing value is never returned in plaintext | Used for `IG` API key, identifier, and password management |
| BrokerEnvironmentSelection | `PlatformEnvironment`, `BrokerEnvironment`, `LiveOptionVisible`, `LiveOptionAvailable`, `ConfiguredAtUtc`, `ConfigurationSource` | `BrokerEnvironment` is required; Test platform forces `LiveOptionAvailable = false` | Stored as current projection and echoed in status responses |
| IgSessionState | `BrokerEnvironment`, `SessionStatus`, `EstablishedAtUtc`, `LastValidatedAtUtc`, `ExpiresAtUtc`, `IsDegraded`, `BlockedReason`, `CurrentRetryCycleId` | Only demo sessions may reach `Active` in this work package | Projection of the current broker-auth state |
| AuthRetryCycle | `RetryCycleId`, `CycleType`, `RetryPhase`, `AutomaticAttemptNumber`, `NextRetryAtUtc`, `LastDelaySeconds`, `PeriodicDelayMinutes`, `MaxAutomaticRetries`, `RetryLimitReached`, `FailureNotificationSent` | Automatic attempts are counted after the initial failed startup attempt; `RetryPhase` distinguishes initial automatic retry from periodic retry | Distinguishes initial automatic cycle from manual-restart cycles and their later periodic phase |
| OperationalEvent | `EventId`, `OccurredAtUtc`, `EventType`, `PlatformEnvironment`, `BrokerEnvironment`, `Severity`, `Summary`, `DetailsJson`, `CorrelationId` | `DetailsJson` must contain only redacted payloads | Append-only ledger for auth, retry, blocked-live, and notification events |
| ConfigurationChangeAudit | `ChangeId`, `OccurredAtUtc`, `ConfigurationId`, `ChangedBy`, `ChangeType`, `Summary`, `DetailsJson` | `DetailsJson` must exclude plaintext secret values | Supports review of SQL-backed configuration changes |
| NotificationRecord | `NotificationId`, `EventId`, `NotificationType`, `Recipient`, `DispatchedAtUtc`, `DispatchStatus`, `Summary` | Stores no transport secrets or raw message credentials | Supports auditability for sent notifications |
| SecretReference | `Provider`, `Key`, `VersionHint` | Metadata only; secret value is never persisted in plaintext | Used when protected credentials are stored as secret references rather than encrypted values |

### 5.3 Implementation Plan (technical steps)

| Step | Change | Files/Modules | Notes |
| ---- | ------ | ------------- | ----- |
| 1 | Add SQL-backed platform, broker, retry, notification, and protected credential configuration models plus startup validation | `src/TNC.Trading.Platform.Api/Configuration/*`, `Program.cs` | Validation must fail fast for missing environment selection and unsupported Test+Live combinations; configuration is sourced from SQL Server in this work package |
| 2 | Add persistence for configuration, protected credentials, current auth state, retry cycles, operational events, notification records, and configuration-audit history | `src/TNC.Trading.Platform.Api/Infrastructure/Persistence/*` | Use SQL Server and environment-tagged tables or entities |
| 3 | Implement configuration-management APIs, auditing, and protected credential update flows | `src/TNC.Trading.Platform.Api/Features/GetPlatformConfiguration/*`, `src/TNC.Trading.Platform.Api/Features/UpdatePlatformConfiguration/*` | Secret update flows must be write-only and return redacted data |
| 4 | Implement the IG demo authentication client and response sanitization | `src/TNC.Trading.Platform.Api/Infrastructure/Ig/*` | Client must support demo auth only in this work package |
| 5 | Implement the hosted session supervisor and retry scheduler | `src/TNC.Trading.Platform.Api/Infrastructure/Auth/*` | Supervisor owns degraded startup, expiry detection, initial exponential retry, periodic retry after initial exhaustion, manual retry reset, and recovery transitions |
| 6 | Implement operator API feature slices for status and manual retry | `src/TNC.Trading.Platform.Api/Features/GetPlatformStatus/*`, `src/TNC.Trading.Platform.Api/Features/TriggerManualAuthRetry/*` | Vertical-slice request, response, handler, and validator pattern should be used |
| 7 | Add the Blazor Web App for status, configuration editing, degraded-state messaging, live-option visibility, and manual retry | `src/TNC.Trading.Platform.Web/*` | Use server interactivity and keep the UI usable while auth-dependent actions remain visibly blocked |
| 8 | Implement notification dispatch, provider abstraction, and ACS Email mapping | `src/TNC.Trading.Platform.Api/Infrastructure/Notifications/*` | Each supported event type needs a concise secret-safe summary and a transport-agnostic provider contract |
| 9 | Extend AppHost orchestration for the Blazor UI, SQL Server, and local configuration wiring | `src/TNC.Trading.Platform.AppHost/AppHost.cs` | Keep local developer flow under Aspire and reuse service-default observability |
| 10 | Add unit, integration, functional, and UI E2E requirement-coverage tests | `test/TNC.Trading.Platform.Api/*`, `test/TNC.Trading.Platform.Web/*` | Functional tests should follow `002_FRx_point_of_test` naming |

### 5.4 Error Handling

| Scenario | Expected behavior | Instrumentation |
| -------- | ------------------ | --------------- |
| Broker environment selection missing in SQL-backed configuration at startup | Application fails configuration validation before background auth starts | Structured startup log with configuration error and no secret values |
| Operator submits an invalid configuration update from the Blazor UI | Reject the update, return validation errors, keep the last known valid configuration active, and record the rejected attempt without secrets | Validation log, configuration-audit record, and UI validation state |
| Test platform configured with IG live | Mark live option visible but unavailable, reject the combination for active use, record the blocked attempt, and send a blocked-live notification when an operator attempts use | Warning log, blocked-live event, blocked-live notification |
| Initial IG demo authentication fails | Start the platform in degraded state, record failure, notify once for the cycle, and schedule automatic retry attempt 1 | Failure event, retry-scheduled event, degraded-state metric |
| Existing session expires or becomes invalid | Transition to degraded state, block auth-dependent actions, notify on failure transition, and restart retry scheduling | Failure event, retry metrics, status-read-model update |
| Initial automatic retry limit reached | Keep platform degraded, transition into periodic retry mode, record retry exhaustion, notify with manual-retry guidance and periodic retry details, and expose manual retry as available | Retry-exhausted event, retry-phase metric, notification record, gauge for manual-retry availability |
| Manual retry requested while retry is already running or while retry exhaustion has not occurred | Reject the command with an explicit reason, keep the UI available, and show the blocked reason without altering current state | Command warning log, API response with blocked reason, and UI feedback state |
| Protected credential update cannot be encrypted, stored, or resolved | Reject the credential update, keep the last known valid protected value, and surface a configuration-management error without exposing the submitted secret | Error log, configuration-audit failure event, and UI feedback state |
| Azure Communication Services Email notification dispatch fails | Record dispatch failure, log it, and keep the underlying auth/session event intact for later review | Notification-dispatch failure event and error log |
| Database persistence unavailable during auth transition | Do not pretend the platform is healthy; surface degraded state and log the persistence failure for operator review | Error log, degraded-state marker, health-check detail |

### 5.5 Configuration

| Setting | Purpose | Default | Location |
| ------ | ------- | ------- | -------- |
| `Platform:Environment` | Declares whether the platform is running in Test or Live mode | `Test` | SQL Server configuration store |
| `Broker:Ig:Environment` | Explicit startup selection of IG demo or live, shown as active context in the UI | None; must be supplied | SQL Server configuration store |
| `Broker:Ig:Demo:BaseUrl` | IG demo REST base URL | IG demo endpoint value from deployment config | SQL Server configuration store |
| `Broker:Ig:Live:BaseUrl` | IG live REST base URL for visibility and future use | IG live endpoint value from deployment config | SQL Server configuration store |
| `Broker:Ig:ApiKey` | IG API key | None | Protected SQL Server credential store or SQL-backed secret reference |
| `Broker:Ig:Identifier` | IG account identifier or username | None | Protected SQL Server credential store or SQL-backed secret reference |
| `Broker:Ig:Password` | IG password | None | Protected SQL Server credential store or SQL-backed secret reference |
| `Broker:Ig:Retry:InitialDelaySeconds` | Initial automatic retry delay after failure | `1` | SQL Server configuration store |
| `Broker:Ig:Retry:MaxAutomaticRetries` | Maximum automatic retries after the initial failed startup attempt | `5` | SQL Server configuration store |
| `Broker:Ig:Retry:Multiplier` | Exponential backoff multiplier | `2` | SQL Server configuration store |
| `Broker:Ig:Retry:MaxDelaySeconds` | Maximum delay cap for exponential backoff | `60` | SQL Server configuration store |
| `Broker:Ig:Retry:PeriodicDelayMinutes` | Delay between reconnect attempts after the initial retry sequence is exhausted | `5` | SQL Server configuration store |
| `Notifications:Email:To` | Project-owner email recipient for supported notifications | None | SQL Server configuration store |
| `Notifications:Provider` | Selects the notification provider implementation | `AzureCommunicationServicesEmail` | SQL Server configuration store |
| `Notifications:AzureCommunicationServices:Endpoint` | ACS resource endpoint for email delivery | None | SQL Server configuration store |
| `Notifications:AzureCommunicationServices:SenderAddress` | Verified sender address used for operational notifications | None | SQL Server configuration store |
| `Notifications:AzureCommunicationServices:ConnectionString` | ACS Email connection string for local or non-Azure hosted execution | None | Secret store |
| `Data:Sql:ConnectionString` | SQL Server connection for configuration, current state, and ledger persistence | None | Secret store or Aspire connection string binding |
| `Security:ProtectedConfiguration:KeyEncryptionKey` | Protects encrypted credential values stored in SQL-backed configuration | None | Secret store or platform key management service |
| `Retention:OperationalRecordsDays` | Retention window for auth/session, notification, and configuration-audit records | `90` | SQL Server configuration store |

## 6. Security Design

Describe how the solution meets the `SRx` requirements.

- **AuthN/AuthZ**: This work package handles broker authentication to IG, not end-user sign-in. The API authenticates only to IG demo using environment-specific configured credentials. In the Test platform environment, live usage is blocked before any live integration call is attempted. Operator-triggered commands such as manual retry must still pass through the platform's eventual operator authorization boundary, but that boundary is not redefined here.
- **UI access**: The new Blazor Web App with server interactivity is part of this work package's runtime surface. It consumes only redacted backend state and must keep non-auth-dependent pages and status components available during degraded broker-auth conditions.
- **Secrets**: Operator-managed non-secret configuration is stored in SQL Server. `IG` credentials are captured through write-only API/UI flows and persisted only as protected values or secret references. In Azure-hosted environments, the preferred access pattern is managed identity with Key Vault-backed protection or equivalent key management where supported by the deployment topology; local development can use externalized protection material. Persisted records keep secret references or redacted summaries instead of raw values. Logging helpers and notification mappers must scrub token-bearing headers and payload values.
- **Data protection**: Communication with IG and Azure Communication Services uses TLS-protected outbound connections. At-rest protection relies on the configured SQL Server deployment, encrypted credential storage or secret-reference resolution, and the external protection key source. API responses expose only redacted operational state and summary data.
- **Threat model notes**:
  - Missing or invalid credentials lead to degraded startup rather than a false healthy state.
  - Token leakage is mitigated by redaction at the integration boundary and by disallowing raw-response persistence.
  - Stored-secret exposure is mitigated by write-only credential update flows, protected storage, redacted configuration APIs, and audit logging.
  - Accidental live use in the Test platform environment is mitigated by configuration validation, operator-surface availability flags, command guards, event recording, and per-attempt notifications.
  - Manual retry abuse is limited by state guards that allow the command only when retry exhaustion has been reached and no retry is already running.

## 7. Observability

| Signal | What | Where | Notes |
| ------ | ---- | ----- | ----- |
| Logs | Startup configuration validation, configuration updates, auth success/failure, session expiry detection, retry scheduling, retry exhaustion, manual retry commands, blocked live attempts, notification dispatch outcomes | Structured application logs via existing OpenTelemetry logging | Must include platform and broker environment context and must exclude secrets |
| Metrics | Current degraded-state flag, auth retry attempts, current retry phase, periodic retry count, retry-limit reached count, auth recoveries, blocked live-attempt count, configuration-update count, notification dispatch failures | OpenTelemetry metrics exported through the existing service-defaults pipeline | Supports dashboards and alerts for sustained degraded state and configuration churn |
| Traces | Configuration read/write requests, outbound IG authentication calls, session-validation checks, Blazor Web App requests to the API, manual-retry command execution, notification dispatch operations, persistence writes for configuration changes and state transitions | OpenTelemetry tracing in the API and UI services | Health endpoints remain excluded per service defaults |

## 8. Testing Strategy

| Test type | Coverage | Location | Notes |
| --------- | -------- | -------- | ----- |
| Unit | Startup validators, configuration validators, retry policy timing, retry-cycle state transitions, redaction helpers, live-blocking rules, protected-credential handling, and notification mappers | `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests` | Add fast deterministic tests for edge cases, secret scrubbing, and configuration update rules |
| Integration | AppHost-based tests for configuration load/update behavior, startup auth success/failure, degraded startup, transition from initial retry to periodic retry, manual retry, persistence, and notification dispatch behavior | `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests` | Reuse the existing Aspire testing pattern already present in the repository |
| Functional | Requirement-driven UI and API tests for `FR1`-`FR20`, organized under the work-package folder | `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/002-environment-and-auth-foundation` | UI-driven functional tests should follow Playwright guidance and cover both configuration management and degraded retry visibility |
| E2E | Cross-project validation of the Blazor Web App and API working together under Aspire | `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests` | Add only the minimal flows needed for configuration editing, degraded-state visibility, periodic retry visibility, and manual retry |

## 9. Rollout Plan

| Phase | Action | Success criteria | Rollback |
| ----- | ------ | ---------------- | -------- |
| 1 | Deploy the API and Blazor Web App with SQL-backed configuration management, protected IG credential storage, explicit Test-platform and IG demo configuration, SQL persistence, and auth supervision enabled | Platform starts with a working operator UI, visible environment/auth status, editable supported configuration, and either an active IG demo session or a visible degraded state with retry progress that continues into periodic retry after the initial retry sequence is exhausted | Disable the new UI project and auth supervisor registration and revert configuration/database changes |
| 2 | Enable production-use notifications for auth/session, retry-limit, recovery, and blocked-live events | Supported events send timely email notifications with the required context and no secrets, and the UI reflects the corresponding state transitions | Disable notification dispatch configuration while retaining event recording |

## 10. Open Questions

None at this stage.

## 11. Appendix

- Related requirements: `docs/002-environment-and-auth-foundation/requirements.md`
- Project business context: `docs/business-requirements.md`
- Project systems analysis: `docs/systems-analysis.md`
- Existing solution baseline: `src/TNC.Trading.Platform.Api`, `src/TNC.Trading.Platform.AppHost`, and `src/TNC.Trading.Platform.ServiceDefaults`
- Relevant repo guidance: `.github/copilot-instructions.md`, `.github/instructions/docs-authoring.instructions.md`, `.github/instructions/iterative-work-docs.instructions.md`, `.github/instructions/authentication.instructions.md`, `.github/instructions/test-approach.instructions.md`, and `.github/instructions/architecture-guidelines.instructions.md`
