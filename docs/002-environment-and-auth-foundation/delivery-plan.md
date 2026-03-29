# Delivery Plan

This document describes how work package 002-environment-and-auth-foundation will be delivered in a single buildable, testable change set so the platform can safely select an IG environment, store operator-managed configuration in SQL Server, update supported configuration through the Blazor UI, maintain broker connectivity only during the configured trading schedule, establish and recover an IG demo session, remain observable while degraded, and notify the project owner on supported auth and blocked-live events.

## Summary

- **Source**: See `requirements.md` for canonical work metadata, scope, and requirement identifiers. See `../business-requirements.md` for project-level business context and `technical-specification.md` for the implementation design.
- **Status**: draft
- **Inputs**:
  - `../business-requirements.md`
  - `requirements.md`
  - `technical-specification.md`

## Description of work

This plan delivers the environment and auth foundation defined by work package 002 as one PR containing a sequence of validated implementation tasks. The plan covers explicit broker-environment selection, SQL-backed operator-managed configuration, secure `IG` credential handling, trading-schedule configuration with a start-of-day, end-of-day, permitted trading days, weekend treatment, and configurable bank-holiday exclusions, trading-schedule-aware connection management, configuration update flows in the Blazor Web App, Test-platform live safeguards, durable environment-scoped auth/session records, IG demo session establishment and recovery, degraded-state retry supervision during active trading periods, operator visibility and manual retry in the Blazor Web App, notification delivery through the email abstraction, and the required validation and rollback steps. Runtime switching between demo and live, IG live authentication, and later trading capabilities remain out of scope. For local desktop validation, notification delivery will be exercised through Mailpit configured as an Aspire integration, while the implementation still preserves the production-oriented notification provider abstraction described in the technical specification.

## Delivery approach

- **Delivery model**: single PR
- **Branching**: implement on `002-environment-and-auth-foundation` and merge as one PR only after all tasks, validations, and rollback notes are complete
- **Dependencies**: IG demo authentication/session endpoints; SQL Server for configuration and persistence; protected credential storage/key management for `IG` secrets; Aspire AppHost orchestration; Mailpit desktop integration through Aspire for local notification validation; production notification-provider configuration
- **Key risks**:
  - Retry-state complexity could create incorrect degraded or recovery transitions; mitigate with isolated retry-cycle logic and unit/integration coverage before UI completion
  - Configuration-management complexity could introduce invalid settings or unsafe update paths; mitigate with SQL-backed validation, optimistic concurrency, audit trails, and secret-safe write-only credential updates
  - Trading-schedule gating could incorrectly suppress intended connectivity or keep retry activity running outside the permitted schedule; mitigate with explicit schedule validation, schedule-aware state modeling, and focused unit/integration coverage
  - External dependency failures from IG, SQL Server, or notification dispatch could hide the true platform state; mitigate with durable event recording, degraded-state projections, and explicit failure handling
  - Secret leakage through logs, records, or notifications could breach `BR12`/`SR2`/`SR5`; mitigate with redaction helpers, scrubbed persistence payloads, and validation that inspects outputs
  - UI and background-session orchestration could drift from API state; mitigate with API-first status/read-model contracts and functional tests against the operator surface
  - Single-PR delivery increases blast radius; mitigate with strict execution gates and task sequencing that keeps the branch buildable throughout

## Delivery Plan

### Execution gates (required)

Before starting *any* work item, and again before marking a work item as complete, run the build + test suite and resolve any failures.

| Gate | When | Required actions | If failures occur |
| --- | --- | --- | --- |
| Baseline | Before starting any work item | Run build and all tests listed in **Cross-cutting validation** | Fix or revert until build/tests are green before continuing |
| Pre-completion | Before completing a work item | Re-run build and all tests listed in **Cross-cutting validation** | Fix failures before marking the work item complete |

### Planned work items

| Work item | Description | Traceability (requirements) | Traceability (spec sections) | Dependencies | Validation | Rollback/Backout | User instructions |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Work Item 1: End-to-end environment and auth foundation | Deliver SQL-backed configuration management and validation, secure `IG` credential handling, environment-scoped persistence, trading-schedule-aware IG demo auth/session supervision, degraded-state retry handling during active trading periods, operator UI visibility, configuration editing, manual retry, notifications, AppHost wiring, and requirement coverage in one PR | `FR1`-`FR22`, `NF1`-`NF5`, `SR1`-`SR5`, `DR1`, `IR1`-`IR5`, `TR1`-`TR13`, `OR1`-`OR8` | `technical-specification.md` §3.1, §3.3, §4, §5.1, §5.2, §5.3, §5.4, §5.5, §6, §7, §8, §9 | IG demo access, SQL Server, protected credential storage/key management, Mailpit via Aspire for local validation, production notification configuration | `dotnet build`; `dotnet test`; verify SQL-backed configuration loading and updates, trading-schedule configuration and enforcement, next-start application of startup-fixed settings, startup validation, degraded auth visibility, retry behavior, manual retry behavior, retry-limit notification content, per-cycle re-notification after failed manual retry, blocked live handling, secret-safe configuration/notification/event outputs, and one manual end-to-end Aspire AppHost run with API + Blazor UI + SQL Server + Mailpit | Revert the PR; remove new API/UI registrations, configuration-management changes, trading-schedule gating, persistence changes, and notification wiring; disable auth supervision and notification configuration if partial backout is required before revert | Provide the SQL bootstrap connection, initial configuration seed including trading-schedule values, secure `IG` credentials, and Mailpit-enabled Aspire local wiring before end-to-end validation |

### Work Item 1 details

- [ ] Work Item 1: End-to-end environment and auth foundation
  - [x] Build and test baseline established
  - [x] Task 1: Implement SQL-backed configuration management, environment selection, and live-use guardrails
    - [x] Step 1: Add SQL-backed platform, broker, retry, notification, and protected credential configuration models
    - [x] Step 2: Add startup loading and validation for explicit broker-environment selection from SQL Server
    - [x] Step 3: Add trading-schedule configuration for start-of-day, end-of-day, permitted trading days, weekend treatment, and bank-holiday exclusions
    - [x] Step 4: Add secure `IG` credential storage and write-only update handling
    - [x] Step 5: Add Test-platform live-option visibility and active-use blocking
    - [x] Step 6: Add status fields for platform environment, broker environment, trading-schedule state, and live-option availability
  - [x] Task 2: Implement durable auth/session state and redacted operational records
    - [x] Step 1: Add SQL-backed persistence for configuration, current auth state, retry cycles, operational events, configuration-audit records, and notification records
    - [x] Step 2: Add IG demo authentication contracts and response sanitization
    - [x] Step 3: Persist auth success, failure, degraded startup, retry activity, retry exhaustion, manual retry, expiry, recovery, trading-schedule transition, and blocked-live events with environment context
    - [x] Step 4: Persist configuration change audit records without exposing secrets
    - [x] Step 5: Ensure logs, records, notifications, API responses, and UI views never expose secrets
  - [x] Task 3: Implement session supervision, degraded startup, and retry-cycle behavior
    - [x] Step 1: Add trading-schedule gate and hosted session supervisor for startup auth and session-validity checks
    - [x] Step 2: Add scheduled connect and disconnect behavior around trading periods, non-trading weekends, and configured bank holidays
    - [x] Step 3: Add initial exponential retry behavior and transition to periodic retry during active trading periods
    - [x] Step 4: Add retry-cycle state tracking, retry attempt number, retry phase, next scheduled retry time, and out-of-schedule state
    - [x] Step 5: Add manual retry after retry exhaustion and automatic retry-budget reset after failed manual retry during the configured trading schedule
  - [x] Task 4: Implement operator API and Blazor UI behavior
    - [x] Step 1: Add `GET /api/platform/status`
    - [x] Step 2: Add `GET /api/platform/configuration`
    - [x] Step 3: Add `PUT /api/platform/configuration`
    - [x] Step 4: Add `POST /api/platform/auth/manual-retry`
    - [x] Step 5: Add `GET /api/platform/events?category=auth&environment=demo`
    - [x] Step 6: Add Blazor status page, configuration page, trading-schedule visibility, degraded-state messaging, retry progress, blocked reasons, and manual-retry interaction
    - [x] Step 7: Keep non-auth-dependent UI available while visibly blocking auth-dependent actions
  - [x] Task 5: Implement notification delivery and AppHost orchestration
    - [x] Step 1: Add notification dispatcher and provider abstraction
    - [x] Step 2: Add Azure Communication Services Email provider mapping for production-oriented notification delivery
    - [x] Step 3: Add local Mailpit Aspire integration for desktop notification validation
    - [x] Step 4: Add failure, retry-limit, recovery, blocked-live, and new failure-cycle notification flows
    - [x] Step 5: Extend AppHost for API, Blazor UI, SQL Server, Mailpit, and bootstrap configuration wiring
    - [x] Step 6: Add retention handling for 90-day operational records
  - [ ] Task 6: Add requirement-driven validation coverage
    - [x] Step 1: Add unit tests for validators, configuration rules, trading-schedule gating, retry timing, retry cycles, redaction, protected credential handling, and notification mapping
    - [x] Step 2: Add integration tests for configuration load/update behavior, trading-schedule enforcement, auth success/failure, degraded startup, retry transitions, manual retry, persistence, and notification dispatch behavior
    - [x] Step 3: Add functional tests under `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/002-environment-and-auth-foundation`
    - [x] Step 4: Add minimal E2E coverage for API and UI working together under Aspire, including configuration editing and trading-schedule enforcement
    - [x] Step 5: Confirm functional test names follow `002_FRx_point_of_test`
    - [ ] Step 6: Confirm local notification validation can be observed in Mailpit
    - [ ] Step 7: Run one manual end-to-end verification through the full Aspire AppHost stack before PR completion
  - [ ] Build and test validation

  - **Files**:
    - `src/TNC.Trading.Platform.Api/Configuration/*`: SQL-backed broker, trading-schedule, retry, notification, protected credential, and persistence settings plus validation
    - `src/TNC.Trading.Platform.Api/Infrastructure/Persistence/*`: configuration, current state, retry cycles, trading-schedule events, configuration-audit, and notification records
    - `src/TNC.Trading.Platform.Api/Infrastructure/Ig/*`: demo-only auth adapter and sanitization
    - `src/TNC.Trading.Platform.Api/Infrastructure/Auth/*`: trading-schedule gate, supervisor, retry scheduling, and degraded-state transitions
    - `src/TNC.Trading.Platform.Api/Infrastructure/Notifications/*`: dispatcher and provider abstraction
    - `src/TNC.Trading.Platform.Api/Features/GetPlatformStatus/*`: operator status read model
    - `src/TNC.Trading.Platform.Api/Features/GetPlatformConfiguration/*`: configuration read model
    - `src/TNC.Trading.Platform.Api/Features/UpdatePlatformConfiguration/*`: configuration update command slice
    - `src/TNC.Trading.Platform.Api/Features/TriggerManualAuthRetry/*`: manual retry command slice
    - `src/TNC.Trading.Platform.Web/*`: status UI, configuration UI, trading-schedule visibility, degraded-state messaging, retry progress, manual retry, and blocked-action messaging
    - `src/TNC.Trading.Platform.AppHost/AppHost.cs`: orchestration for API, UI, SQL Server, Mailpit, and bootstrap wiring
    - `test/TNC.Trading.Platform.Api/*`: unit and integration coverage
    - `test/TNC.Trading.Platform.Web/*`: functional and E2E coverage
    - `docs/002-environment-and-auth-foundation/delivery-plan.md`: finalized delivery plan
  - **Work Item Dependencies**: SQL bootstrap connectivity, seeded SQL-backed configuration, protected `IG` credential storage/key management, Mailpit, and notification settings; internal sequencing should follow Tasks 1 through 6
  - **User Instructions**: provide the SQL bootstrap connection and initial configuration seed including trading-schedule values; manage supported configuration through the Blazor UI after startup; expect startup-fixed changes such as platform environment and broker environment selection to apply on the next platform start; update demo `IG` credentials through the secure write-only UI flow; run locally with Mailpit configured through Aspire for notification verification; complete one manual end-to-end Aspire AppHost run before PR sign-off; expect IG live to remain visible but unavailable in Test mode

## Cross-cutting validation

- **Build**: `dotnet build`
- **Unit tests**: `dotnet test`
- **Integration tests**: `dotnet test`
- **Manual checks**:
  - Start the platform with an explicit broker environment and confirm the status surface shows platform environment, broker environment, configured trading schedule, current in-schedule or out-of-schedule state, live-option availability, auth state, retry phase, retry attempt number, and next scheduled retry time when applicable
  - Review supported configuration in the Blazor UI, update non-secret configuration values, and confirm the changes persist to SQL Server and are reflected by the platform
  - Review trading-schedule configuration in the Blazor UI, update the start-of-day and end-of-day values, trading days, weekend treatment, and bank-holiday exclusions, and confirm the platform reflects the configured in-schedule or out-of-schedule state correctly
  - Update a startup-fixed setting and confirm the UI indicates the change applies on the next platform start rather than switching environments at runtime
  - Update `IG` credential values through the secure write-only UI flow and confirm the UI/API do not reveal currently stored secret values
  - Validate that the platform maintains an `IG` connection only during the configured trading schedule and does not maintain or retry the connection on non-trading weekends, configured bank holidays, or outside the configured trading hours
  - Validate degraded startup by using invalid or unavailable demo credentials and confirm the UI remains available while auth-dependent actions are blocked with a visible reason
  - Validate that Test-platform live attempts are blocked, recorded, and surfaced with environment context
  - Validate that failure, recovery, and blocked-live notifications include event type, timestamp, environment context, and concise summary without secrets
  - Validate that retry-limit notifications also include manual-retry guidance, last retry attempt count, last scheduled delay attempted, and the periodic retry delay now in effect, without secrets
  - Validate that manual retry becomes available only after initial retry exhaustion, starts immediately without confirmation, and remains unavailable while a retry is already in progress or while the trading schedule is inactive
  - Validate that locally generated notification emails appear in the Mailpit web UI exposed by Aspire
  - Run one manual end-to-end verification through the full Aspire AppHost stack (`API + Blazor UI + SQL Server + Mailpit`) before marking the PR complete
- **Security checks**:
  - Inspect logs, configuration responses, status responses, event records, and notifications to confirm no raw credentials, API keys, access tokens, or session tokens are exposed
  - Review SQL-backed configuration and secret-loading paths to confirm supported configuration is database-managed while `IG` credentials remain protected at rest and are never returned in plaintext
  - Verify live-auth paths are blocked by design in this work package

## Acceptance checklist

- [ ] Work item aligns with `../business-requirements.md`.
- [ ] All referenced `FRx` requirements are implemented and validated.
- [ ] All referenced `NFx` requirements have measurements or checks.
- [ ] All referenced `SRx` security requirements are implemented and validated.
- [ ] Docs updated under `./docs/002-environment-and-auth-foundation/`.
- [ ] Rollback/backout plan documented for each work item.

## Notes

- This draft assumes one PR on branch `002-environment-and-auth-foundation`.
- Because the scope spans configuration, persistence, background processing, API, UI, notification transport, and tests, the branch should remain buildable after each task even though merge happens once.
- Only the SQL bootstrap connection and protection material should remain outside the SQL-backed operator configuration store.
- Validation commands default to repo-root `dotnet build` and `dotnet test`, per repository guidance.
- Functional tests should follow the `002_FRx_point_of_test` naming convention under the work-package folder.
- Local development notification verification uses Mailpit through Aspire AppHost integration.
- PR completion requires one manual end-to-end verification through the full Aspire AppHost stack.
- Operator-facing end-user authentication is unchanged by this work package; the scope here is broker authentication and the operator surface needed to observe and control retry behavior.
