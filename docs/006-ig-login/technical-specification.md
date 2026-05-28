# IG Login Technical Specification

This document describes how work package `006-ig-login` will be implemented so the platform can authenticate to the `IG` Test environment, maintain observable backend login state, persist approved non-secret login payload data, and expose both current and retained login information to the operator UI.

## 1. Summary

- **Source**: See `requirements.md` for canonical work metadata, scope, and requirement identifiers. See `../business-requirements.md` for project-level business context.
- **Status**: draft
- **Input**: `requirements.md`, `../business-requirements.md`, `../systems-analysis.md`, and `../002-environment-and-auth-foundation/requirements.md`
- **Output**: `plans/001-delivery-plan.md`

## 2. Problem and Context

### 2.1 Problem statement

The platform needs a startup-driven `IG` Test login that is maintained by the backend, remains observable to the operator, and safely retains the broker-returned non-secret login payload for current review and short-term historical troubleshooting. This work package extends the existing environment/auth foundation rather than redefining retry, schedule, or secret-handling behavior.

### 2.2 Assumptions

- The existing API, Blazor UI, status page, and platform state coordination flow are the preferred extension points.
- Work package `002-environment-and-auth-foundation` remains the source of truth for retry policy, trading-schedule gating, degraded-state behavior, and live-environment restrictions.
- `IG` credentials remain stored through the existing protected configuration workflow and are never returned to the operator after save.
- The `IG` login response contains a mix of protected and non-secret fields, and only the approved non-secret subset will be stored or displayed.
- SQL Server-backed persistence and existing operational-record retention mechanisms will be reused.
- The dedicated `IG` login history page will show retained daily historical payloads only; the latest successful payload will remain on `/status`.
- `/status` will show the latest successful login information as a compact summary with an expandable details section on the same page.
- The full latest non-secret payload will be included in the existing `GET /api/platform/status` response so the UI can expand and collapse details locally without an additional read call.

### 2.3 Constraints

- The implementation must satisfy `FR1`-`FR10`, `NF1`-`NF5`, `SR1`-`SR4`, `DR1`-`DR3`, `IR1`-`IR4`, `TR1`-`TR10`, and `OR1`-`OR3`.
- Retry and schedule behavior must be reused from `../002-environment-and-auth-foundation/requirements.md`.
- Unsupported live-environment login must remain blocked in constrained platform environments.
- Secrets, tokens, and equivalent protected authentication material must not be persisted, logged, serialized, or rendered.
- API changes should follow the repository’s Minimal API plus vertical-slice request/response approach.
- UI changes should align to the existing Blazor operator experience, with current-state information on `/status`, expandable latest-login details on the same page, and retained daily history on a dedicated page.
- The existing `Program.cs` structure should remain startup-oriented, with feature behavior implemented in dedicated slices and registration extensions.

## 3. Proposed Solution

### 3.1 Approach

Implement `IG` login as an extension of the current platform state supervision pipeline with four capabilities:

1. **Startup and maintenance login execution**  
   Extend the broker-auth workflow so startup login occurs automatically during permitted schedule periods and runtime session loss reuses the inherited recovery behavior.

2. **Sanitized login snapshot capture**  
   Map each successful `IG` login response into an application-owned, secret-safe login snapshot model with an explicit allow-list of non-secret fields.

3. **Current and retained projections**  
   Persist:
   - the latest successful non-secret login snapshot; and
   - the first successful non-secret login snapshot of each day for 90 days.

4. **Operator-facing read surfaces**  
   Extend the status surface for current login state and latest-success information, including expandable full latest-payload details on `/status`, and add a dedicated `IG` login history page for retained daily payload review.

This approach fits the existing repository because the current solution already has:
- a platform startup coordination pattern;
- an operator-facing status page and API;
- retry, schedule, degraded-state, and live-environment restrictions established in work package `002`;
- operational-record retention processing;
- Blazor UI and Minimal API surfaces that can be extended without introducing a new architectural path.

### 3.2 Alternatives considered

| Option | Summary | Pros | Cons | Decision rationale |
| ------ | ------- | ---- | ---- | ------------------ |
| A | Defer login payload handling to later trading or market-data work | Lowest short-term change count | Delays operator visibility and couples login to downstream features | Rejected because this work package explicitly requires current-state visibility and retained login review |
| B | Extend the existing platform status and supervision model with login snapshot capture and history | Reuses current architecture, keeps one source of truth, fits current UI/API surfaces | Requires careful contract extension | Accepted |
| C | Store and expose the raw broker login payload directly | Simple to persist | High secret-exposure risk and unstable consumer contract | Rejected because it conflicts with `SR2`, `SR3`, and `NF4` |

### 3.3 Architecture

The solution extends the current Application, Infrastructure, API, and Web layers.

- **Components**:
  - `IG` login client/adapter in Infrastructure
  - Login payload sanitizer and mapper
  - Application login supervision workflow integrated with `PlatformStateCoordinator`
  - Persistence for latest snapshot, daily retained snapshot history, and operational records
  - API slices for current status and login history
  - Blazor `/status` enhancements with expandable latest-login details plus a dedicated `IG` login history page

- **Data flows**:
  - Startup coordination checks inherited schedule rules.
  - If login is permitted, the broker adapter authenticates to `IG` Test.
  - Successful responses are sanitized, mapped, persisted, and projected into current status.
  - Failed or degraded outcomes follow inherited retry and schedule handling.
  - `GET /api/platform/status` returns current login state plus the full latest non-secret payload.
  - The UI expands or collapses latest-payload details locally on `/status`.
  - The UI reads retained history from a dedicated read endpoint.

- **Dependencies**:
  - `IG` Test login API
  - Existing platform configuration and credential storage
  - Existing auth foundation services for retry, schedule, degraded state, and live safeguards
  - SQL Server-backed persistence
  - Existing Blazor navigation and status surface

## 4. Requirements Traceability

| Requirement ID | Requirement | Implementation notes | Validation approach |
| -------------- | ----------- | -------------------- | ------------------- |
| FR1 | Automatic startup login | Extend startup coordination to invoke `IG` Test login during permitted schedule windows | Integration test |
| FR2 | Report failed login attempts | Reuse existing degraded/failure state model and surface it through status API/UI | Integration and functional tests |
| FR3 | Capture successful login response | Add sanitized response mapping into an internal snapshot model | Unit and integration tests |
| FR4 | Persist full non-secret login payload | Store latest snapshot and daily first-successful snapshot records | Integration test |
| FR5 | Present stored payload on UI | Show latest login information on `/status` with expandable full details; show retained daily payloads on dedicated history page | Functional/UI test |
| FR6 | Expose current login status on UI connect | Extend current platform status contract with login, schedule, and retry context | API and functional tests |
| FR7 | Retain secret-safe notable transitions | Reuse operational-record pattern for login/session transitions | Persistence and inspection tests |
| FR8 | Prevent unsupported live-environment login | Reuse platform-environment guard before any broker call | Unit/integration tests |
| FR9 | Allow retained historical review | Add history query and dedicated UI page for daily retained payloads | API and functional tests |
| FR10 | Maintain login and accurate current state | Integrate login supervision with inherited retry and schedule handling | Integration tests |
| NF1 | Reliable current state | Use a single application-owned status projection updated on each auth/session transition | State-transition tests |
| NF2 | Operator visibility | Extend API and UI for current state, latest summary/details, and retained history | Functional tests |
| NF3 | No secret exposure | Centralize allow-list mapping and redaction | Unit and output-inspection tests |
| NF4 | Stable retrieval contract | Expose platform-owned DTOs rather than raw broker payloads | Contract tests |
| NF5 | Clear UI communication | Separate current state, expandable latest details, and retained history views | Functional/UI tests |
| SR1 | Authenticate only against supported Test environment | Constrain target environment through existing configuration and guards | Integration tests |
| SR2 | Store/display only full non-secret payload | Persist and serialize only approved fields | Unit and integration tests |
| SR3 | Keep secret inputs out of summaries | Keep credentials/tokens outside snapshot entities and response models | Unit and integration tests |
| SR4 | Fail safely on unusable sessions | Replace stale success with failed/unavailable/out-of-schedule states promptly | Integration tests |
| DR1 | Retain full non-secret payload | Persist structured non-secret snapshot for retrieval | Persistence tests |
| DR2 | Distinguish summary data from secrets | Separate snapshot storage from credential/token handling | Data-shape inspection |
| DR3 | Retain first successful payload of each day for 90 days | Persist one daily retained snapshot and clean up via retention processing | Retention tests |
| IR1 | Integrate with `IG` login capabilities | Infrastructure adapter for broker login and response classification | Integration test |
| IR2 | Expose stored summary data to UI | Extend status contract to include full latest payload plus dedicated history read contract | API and UI tests |
| IR3 | Expose retained historical payloads | Add history endpoint and history page | API and functional tests |
| IR4 | Expose current backend-maintained status | Extend existing status endpoint contract | API and functional tests |
| TR1 | Verify automatic startup login | Cover startup success path | Integration test |
| TR2 | Verify failed-login handling | Cover rejected/unavailable login and safe visible failure state | Integration and functional tests |
| TR3 | Verify persistence of full non-secret payload | Cover successful snapshot storage and retrieval | Integration test |
| TR4 | Verify UI display of stored payload | Cover operator-visible rendering on `/status` and retained-history rendering on the dedicated page | Functional/UI test |
| TR5 | Verify secret-safe outputs | Inspect logs, persistence, API, and UI outputs | Unit and integration inspection |
| TR6 | Verify environment safeguards | Cover blocked live path | Unit/integration tests |
| TR7 | Verify retained history behavior | Cover latest vs retained daily history distinction | Integration and functional tests |
| TR8 | Verify backend-maintained status visibility | Cover status retrieval without UI-triggered login | Functional test |
| TR9 | Verify reused retry behavior | Assert inherited retry behavior remains in effect | Integration test |
| TR10 | Verify out-of-schedule behavior | Cover intentional out-of-schedule state distinct from failure | Integration and functional tests |
| OR1 | Surface current backend-maintained state | Extend runtime status projection and `/status` page | API/UI verification |
| OR2 | Retain secret-safe records | Persist and show approved login/session records only | Persistence and review tests |
| OR3 | Support operator review of retained history | Provide dedicated history retrieval and UI surface | Functional tests |

## 5. Detailed Design

### 5.1 Public APIs / Contracts

| Area | Contract | Example | Notes |
| ---- | -------- | ------- | ----- |
| REST | `GET /api/platform/status` | Returns current environment, schedule state, auth state, retry state, and the full latest non-secret login payload | Extends existing status surface |
| REST | `GET /api/platform/auth-events/{brokerEnvironment}` | Returns secret-safe auth/session operational records | Reuses current event review path |
| REST | `GET /api/platform/ig-login-history` | Returns retained daily first-successful login payloads and related metadata | New read slice |
| Internal | `CaptureIgLoginResultRequest` / `CaptureIgLoginResultResponse` | Sanitized successful login capture command | Application-owned contract |
| Internal | `GetIgLoginHistoryRequest` / `GetIgLoginHistoryResponse` | Retained history query | Follows vertical-slice pattern |

### 5.2 Data Model

| Entity/Concept | Fields | Constraints | Notes |
| -------------- | ------ | ----------- | ----- |
| `IgLoginSnapshot` | `Id`, `BrokerEnvironment`, `CapturedAtUtc`, `TradingDay`, `SnapshotKind`, `ClientId`, `CurrentAccountId`, `LightstreamerEndpoint`, `TimeZoneOffset`, `Accounts`, `Capabilities`, `RawNonSecretPayloadJson` | Stores approved non-secret fields only | `SnapshotKind` distinguishes latest and retained daily snapshot roles |
| `IgLoginStatusProjection` | `BrokerEnvironment`, `CurrentState`, `ScheduleState`, `RetryPhase`, `LastAttemptAtUtc`, `LastSuccessfulLoginAtUtc`, `LatestSnapshotId`, `LatestFailureSummary` | Single current-state projection | Powers status API/UI |
| `IgLoginHistoryItem` | `TradingDay`, `CapturedAtUtc`, `CurrentAccountId`, `AccountCount`, `ClientId`, `SummaryPayload` | One retained successful payload per day | Read-model projection |
| `OperationalRecord` extension | Existing operational-record fields plus login/session summaries | Secret-safe only | Reuses existing review pattern |

### 5.3 Implementation Plan (technical steps)

| Step | Change | Files/Modules | Notes |
| ---- | ------ | ------------- | ----- |
| 1 | Define explicit non-secret `IG` login response mapping | Application and Infrastructure contracts | Allow-list, not raw pass-through |
| 2 | Add persistence for latest and daily retained login snapshots | Infrastructure persistence and schema | 90-day retention |
| 3 | Extend startup/session supervision to capture successful login payloads | `PlatformStateCoordinator`, application services, broker auth workflow | Reuse inherited retry/schedule logic |
| 4 | Extend status query and response models with latest login information and expandable detail support | Application, API, Web status models | `/status` remains the current-state page |
| 5 | Add dedicated login history query, endpoint, and Blazor page | Application feature slice, API feature, Web page/navigation | Retained daily history shown here |
| 6 | Extend operational-record handling for notable login transitions | Infrastructure operational-record pipeline | Secret-safe summaries only |
| 7 | Add unit, integration, and functional tests | `test/` projects | Requirement-traceable comments required |
| 8 | Update delivery docs and affected wiki pages before completion | `docs/006-ig-login/` and `docs/wiki/` | Required by repo rules |

### 5.4 Error Handling

| Scenario | Expected behavior | Instrumentation |
| -------- | ------------------ | --------------- |
| Invalid credentials or rejected login | Mark current state failed/degraded, do not present an active session, reuse inherited retry logic | Secret-safe operational record |
| Broker unavailable during startup | Start in observable degraded state and continue inherited retry behavior | Status update plus retry instrumentation |
| Session invalid/expired after earlier success | Mark current state unusable, keep retained history, and attempt recovery per inherited policy | Transition record plus status update |
| Out-of-schedule transition | Mark state intentionally out of schedule, suppress login maintenance, do not classify as auth failure | Schedule-state update |
| Unexpected broker response fields | Ignore unmapped or protected fields from persistence and UI projection | Warning log without sensitive payload content |
| Retention cleanup | Remove retained daily snapshots older than 90 days | Retention processor record |

### 5.5 Configuration

| Setting | Purpose | Default | Location |
| ------ | ------- | ------- | -------- |
| Broker environment selection | Chooses supported `IG` environment | Existing configuration | SQL-backed operator-managed configuration |
| `IG` credentials | Broker login inputs | Existing stored values | Protected configuration storage |
| Trading schedule | Controls when login may be established/maintained/retried | Existing values from work package `002` | SQL-backed operator-managed configuration |
| Retry policy | Controls degraded-state retry behavior | Existing values from work package `002` | SQL-backed operator-managed configuration |
| Login history retention days | Controls retained daily snapshot cleanup | `90` | Application/infrastructure configuration |
| Live-environment restriction | Blocks unsupported live login | Existing safeguard enabled | Platform environment rules |

## 6. Security Design

Describe how the solution meets `SRx` requirements.

- **AuthN/AuthZ**: Operator access continues to use the existing local and hosted user-auth approach. Broker authentication to `IG` remains a backend concern using stored credentials and never becomes a user-driven login flow for this package.
- **Secrets**: Credentials, session tokens, and equivalent protected values remain outside snapshot entities, API models, logs, and UI components. Sanitization occurs before persistence or projection.
- **Data protection**: Transport uses existing HTTPS and API protections. Persisted login snapshot records contain approved non-secret fields only. Secret values remain handled by the existing secure configuration path.
- **Threat model notes**:
  - prevent stale success from appearing as current after failure;
  - prevent accidental protected-field persistence by using explicit field mapping;
  - prevent unsupported live-environment broker calls;
  - separate retained history from current-state reporting to reduce operator confusion.

## 7. Observability

| Signal | What | Where | Notes |
| ------ | ---- | ----- | ----- |
| Logs | Startup login attempts, success/failure classification, invalidation, retention actions | Structured application logs | No secrets or raw tokens |
| Metrics | Login successes, failures, retries, current degraded-state indicator, retained snapshot count | Existing telemetry pipeline | Supports later alerting |
| Traces | Broker login call, snapshot persistence, status update | Existing tracing pipeline where available | Sanitized attributes only |
| Status projection | Current login state, retry phase, schedule state, last success time, latest login information | `/api/platform/status` and `/status` page | Primary runtime operator signal |
| Operational records | Notable login/session transitions | Existing operational record store/UI | Supports troubleshooting |
| History projection | Retained daily first-successful payloads | Dedicated history endpoint and page | Supports review without raw log access |

## 8. Testing Strategy

| Test type | Coverage | Location | Notes |
| --------- | -------- | -------- | ----- |
| Unit | Payload sanitization, mapping, snapshot selection, secret exclusion | `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests` and `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests` | xUnit with requirement-traceable comments |
| Integration | Startup login capture, degraded handling, retry/schedule inheritance, persistence of latest and retained snapshots, retention cleanup, blocked live path | `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests` and supporting infrastructure tests | Validate platform-owned contracts |
| Functional | Operator can load current status, view latest login information, expand latest payload details, open dedicated history page, and distinguish failed vs out-of-schedule | `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests` | Use feature-based folder naming |
| E2E | Optional closed-box verification of end-to-end UI/API flow | `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests` | Add only if needed |

## 9. Rollout Plan

| Phase | Action | Success criteria | Rollback |
| ----- | ------ | ---------------- | -------- |
| 1 | Deliver mapping and persistence for login snapshots | Build passes and snapshot persistence tests pass | Revert snapshot schema/code |
| 2 | Extend status API/UI with latest login information | Operator can review current state and latest login information on `/status` | Remove new status fields/UI bindings |
| 3 | Add dedicated history API/UI page | Operator can review retained daily history distinctly from current state | Remove history slice while keeping status changes |
| 4 | Complete tests, docs, and wiki alignment | Requirement-linked tests pass and docs are aligned | Hold at previous phase |

## 10. Open Questions

- None.

## 11. Appendix (optional)

- Related work package: `../002-environment-and-auth-foundation/requirements.md`
- Project context: `../business-requirements.md`
- System context: `../systems-analysis.md`
- Likely extension points:
  - `src/TNC.Trading.Platform.Application/Services/PlatformStateCoordinator.cs`
  - `src/TNC.Trading.Platform.Application/Features/GetPlatformStatus/*`
  - `src/TNC.Trading.Platform.Web/Components/Pages/Status.razor`
  - `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/OperationalRecordRetentionProcessor.cs`
