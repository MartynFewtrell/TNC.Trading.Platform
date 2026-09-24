# API reference

This document describes the current HTTP surface exposed by the platform API.

## Base URLs

In local Aspire runs, the API base URL is assigned by AppHost. The dashboard also exposes a link to Scalar UI in development.

The Blazor UI talks to the API over service discovery using the internal `https+http://api` address.

## Endpoint summary

| Method | Route | Purpose |
| --- | --- | --- |
| `GET` | `/` | Basic service metadata alias. |
| `GET` | `/metadata` | Basic service metadata. |
| `GET` | `/health/live` | Liveness endpoint. |
| `GET` | `/health/ready` | Readiness endpoint. |
| `GET` | `/api/platform/status` | Current runtime status, IG login state, and latest stored non-secret login payload for viewer-capable operators. |
| `GET` | `/api/platform/ig-login/history` | Retained daily first-successful non-secret login payloads within the 90-day retention window for viewer-capable operators. |
| `GET` | `/api/platform/account-details?cursor={opaque}` | Read the newest or adjacent historical saved account retrieval for Viewer-capable operators. |
| `POST` | `/api/platform/account-details/refresh` | Request a fresh retrieval from the configured IG test account for Operator-capable users. |
| `GET` | `/api/platform/broker-environments` | List catalog records for viewer-capable users. |
| `GET` | `/api/platform/broker-environments/status` | Return selected/applied broker IDs and restart state. |
| `POST` | `/api/platform/broker-environments` | Create a named broker catalog record (Administrator). |
| `POST` | `/api/platform/broker-environments/{id}/credentials` | Replace write-only credentials (Administrator). |
| `POST` | `/api/platform/broker-environments/selection` | Request restart-applied broker selection (Operator). |
| `POST` | `/api/platform/broker-environments/{id}/retirement-preview` | Create a server-bound retirement preview (Administrator). |
| `POST` | `/api/platform/broker-environments/{id}/retire` | Retire and purge a catalog record (Administrator). |
| `GET` | `/api/platform/configuration` | Current redacted schedule/profile configuration for operator-capable users. |
| `PUT` | `/api/platform/configuration` | Update schedule, retry, notification, and instrument-collection settings; platform identity is not mutable here. |
| `GET` | `/api/platform/market-categories` | Read the saved applied-environment category catalogue, shared interest, and latest collection status (Viewer). |
| `POST` | `/api/platform/market-categories/refresh` | Refresh the saved category catalogue inside the active schedule and allowance (Operator). |
| `GET` | `/api/platform/market-categories/{categoryCode}/instruments?pageSize=50&cursor={opaque}` | Read one page from the versioned saved instrument snapshot (Viewer). |
| `PUT` | `/api/platform/market-categories/{categoryCode}/interest` | Update shared category interest using the expected interest revision (Operator). |
| `GET` | `/api/platform/instrument-collection/status` | Read persisted collector schedule, budget, and safe outcomes (Viewer). |
| `POST` | `/api/platform/auth/manual-retry` | Trigger a manual retry cycle when allowed for operator-capable users. |
| `POST` | `/api/platform/auth/audit` | Persist operator authentication audit events for authenticated callers. |
| `GET` | `/api/platform/events` | Return redacted operational events for viewer-capable operators. |
| `GET` | `/api/platform/auth/administration` | Return the administrator-only auth summary surface. |

## General behavior

- JSON uses web defaults and camel-case field names.
- `/health/live`, `/health/ready`, `/`, and `/metadata` remain anonymous.
- protected endpoints require a bearer token issued for the platform API and return `401 Unauthorized` or `403 Forbidden` without browser redirects.
- Secret values are never returned by configuration or status endpoints.
- Validation failures on configuration updates return a validation-problem payload with the existing field keys and `400` status.
- Manual retry conflicts return `409 Conflict` when the current runtime state does not allow the action.
- Broker selection requires operator scope, acknowledgement, and the expected
  revision; stale revisions, unavailable/retired records, or a missing
  acknowledgement return a conflict or validation problem without changing
  the applied runtime.
- Retirement requires administrator scope plus an unexpired server-bound
  confirmation token, matching concurrency token, and exact normalized name.
  The selected or applied record cannot be retired.
- Catalog and credential responses expose presence/metadata only. Raw secrets
  are never returned, logged, or included in audit payloads.

## Account Details

Account Details retrieves real account and balance data from the configured IG
test account through the IG Demo endpoint (`demo-api.ig.com`). The hostname
identifies the real IG test environment; the response is not synthetic test
data. Live is deliberately unsupported. A future Live boundary must use an
explicit environment selection, separate credentials and endpoint
configuration, isolated snapshots, and independent operator guardrails.

`GET /api/platform/account-details` returns the newest successful immutable
retrieval. Passing the opaque `cursor` returned by a prior response reads the
adjacent snapshot using `(RetrievedAtUtc, RetrievalId)` keyset ordering. The
response contains only persisted account fields, retrieval time, trading day,
and older/newer cursors. It never contains IG credentials, CST, or
`X-SECURITY-TOKEN` values.

`POST /api/platform/account-details/refresh` is Operator-only and starts a
fresh session inside Infrastructure. It returns `200` with the saved
retrieval, `409` for unsupported environment or cross-replica refresh
contention, `429` for a recognized IG allowance response, `502` for malformed
provider data, `503` for an unavailable upstream, and `504` for timeout.
Existing saved data remains readable when a refresh fails. Viewer users can
read history but cannot invoke the refresh route.

## Market categories and saved instruments

All routes in this section operate on saved SQL data except for the
schedule-gated Operator category refresh. Category reads include
`interestRevision`, the applied environment, current categories with shared
interest and latest collection status, and dormant interested category codes.
New categories are not selected by default. `GET` performs no IG request.

The instruments route requires Viewer authorization. `pageSize` defaults to
`50` and accepts `1` through `100`; invalid codes, size, or cursors return
validation Problem Details. A successful response includes the state,
category code, snapshot version, platform UTC retrieval time, instrument rows,
and nullable `nextCursor`. `NeverCollected` is distinct from a complete empty
snapshot. Cursors are opaque, Data Protection-protected values bound to the
applied broker environment, exact category, snapshot version, and last EPIC.
An invalid cursor is a validation failure; a changed snapshot returns
`409 Conflict`, so the client must restart from the first page. A category not
in the saved catalogue returns `404 Not Found`. The API reads SQL only.

Operators update a category with:

```json
{
  "interested": true,
  "expectedRevision": 4
}
```

The update is shared within the applied broker environment and uses
optimistic concurrency. The response contains the new revision; a stale
revision returns `409 Conflict`, and a category that is not currently listed
returns `404 Not Found`. This write changes intent only and does not start
collection.

`GET /api/platform/instrument-collection/status` returns the current safe
collector state, applied broker environment, trading day, due slot/next
wake-up, request budget, and per-category outcomes. It never includes IG
credentials or raw provider diagnostics.

The Operator configuration GET/PUT contract includes
`instrumentUpdatesPerDay` (one to four) and
`approvedNonTradingDailyRequestAllowance`. The response reports current and
pending frequency, the next effective local trading day, the approved
allowance, request usage, collection outcome, and safe status. A higher
frequency requires a measured-capacity, environment-specific allowance; zero
or unset allowance pauses collection. The shared budget counts category,
session, and instrument calls. Saving settings does not invoke IG; frequency
changes take effect on the next local trading day.

The Operator category refresh returns `409 Conflict` when the applied
schedule is inactive or another cycle owns the prerequisite, and safe
`429`, `502`, `503`, or `504` Problem Details for allowance, provider-data,
availability, or timeout failures. The saved catalogue and last-good
instrument snapshots remain available after failure.

## GET /

Returns the same lightweight metadata payload as `GET /metadata`.

## GET /metadata

Returns lightweight service metadata.

### Example response

```json
{
  "service": "TNC.Trading.Platform.Api",
  "environment": "Development"
}
```

The auth event feed can include broker-auth supervision records and operator-session audit records. Operator audit examples include sign-in, sign-out, access-denied, and delegated-token acquisition failure events with redacted details.

## POST /api/platform/auth/audit

Persists an authenticated operator auth audit event through the API so the shared auth event history can retain Web sign-in lifecycle outcomes.

The HTTP adapter extracts the authenticated operator claims and correlation metadata before dispatching the Application auth-audit use case. A successful request preserves the existing `202 Accepted` response and event-feed location; unsupported event types remain validation problems and do not write an event.

### Request shape

```json
{
  "eventType": "OperatorSignOutCompleted",
  "path": "/authentication/sign-out",
  "scope": null
}
```

### Supported event types

- `OperatorSignInCompleted`
- `OperatorSignOutCompleted`
- `OperatorAccessDenied`
- `OperatorTokenAcquisitionFailed`

### Success response

- status: `202 Accepted`
- location: `/api/platform/events?category=auth`

### Behavior notes

- the endpoint requires an authenticated bearer token
- the API derives the operator identity from claims instead of trusting the request body
- persisted details include correlation data, path, and scope context only after redaction
- raw delegated tokens and other sensitive protocol values are not stored or returned

## GET /health/live

Returns HTTP `200 OK` when the service process is alive.

### Example response

```json
{
  "status": "Healthy"
}
```

## GET /health/ready

Returns HTTP `200 OK` when the service is ready to serve traffic.

### Example response

```json
{
  "status": "Healthy"
}
```

## GET /api/platform/status

Returns the current platform runtime state together with the current IG login projection, the latest stored non-secret successful login payload, and the latest read-only IG proof data snapshot.

### Response shape

```json
{
  "platformEnvironment": "Test",
  "brokerEnvironment": "Demo",
  "liveOptionVisible": true,
  "liveOptionAvailable": false,
  "tradingSchedule": {
    "startOfDay": "08:00:00",
    "endOfDay": "16:30:00",
    "tradingDays": ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday"],
    "weekendBehavior": "ExcludeWeekends",
    "bankHolidayExclusions": [],
    "timeZone": "UTC"
  },
  "tradingScheduleState": {
    "isActive": true,
    "reason": "Trading schedule is active."
  },
  "authState": {
    "sessionStatus": "Degraded",
    "isDegraded": true,
    "blockedReason": "IG demo credentials are incomplete."
  },
  "retryState": {
    "phase": "None",
    "automaticAttemptNumber": 0,
    "nextRetryAtUtc": null,
    "retryLimitReached": false,
    "manualRetryAvailable": false
  },
  "updatedAtUtc": "2026-04-01T10:00:00+00:00",
  "igLogin": {
    "currentState": "Active",
    "scheduleState": {
      "isActive": true,
      "reason": "Trading schedule is active."
    },
    "retryState": {
      "phase": "None",
      "automaticAttemptNumber": 0,
      "nextRetryAtUtc": null,
      "retryLimitReached": false,
      "manualRetryAvailable": false
    },
    "lastAttemptAtUtc": "2026-04-01T09:59:45+00:00",
    "lastSuccessfulLoginAtUtc": "2026-04-01T09:59:45+00:00",
    "latestSnapshotId": "22222222-2222-2222-2222-222222222222",
    "latestFailureSummary": null,
    "latestSnapshot": {
      "snapshotId": "22222222-2222-2222-2222-222222222222",
      "capturedAtUtc": "2026-04-01T09:59:45+00:00",
      "tradingDay": "2026-04-01",
      "currentAccountId": "configured-demo-session",
      "lightstreamerEndpoint": null,
      "sessionExpiresAtUtc": null,
      "responseHeaders": {
        "Version": "3"
      },
      "rawNonSecretPayloadJson": "{\"currentAccountId\":\"configured-demo-session\",\"lightstreamerEndpoint\":null,\"expiresAtUtc\":null,\"headers\":{\"Version\":\"3\"}}"
    },
    "latestProofData": {
      "preferredAccountName": "Demo Account",
      "preferredAccountId": "ACC12345",
      "balance": 5000.00,
      "openPositionCount": 2,
      "retrievedAtUtc": "2026-04-01T09:59:46+00:00"
  }
}
```

### Field notes

| Field | Meaning |
| --- | --- |
| `platformEnvironment` | Immutable deployment context: `Desktop`, `Development`, `Test`, or `Live`. |
| `brokerEnvironment` | Compatibility presentation of the applied broker catalog kind/name. |
| `appliedBrokerEnvironment` | The catalog record currently used by this process. |
| `selectedBrokerEnvironment` | The requested catalog record, which may differ until restart. |
| `restartRequired` | Indicates that selected broker configuration is not yet applied. |
| `selectionRevision` | Optimistic-concurrency revision for selection requests. |
| `liveOptionVisible` | Compatibility field indicating whether a live-kind option can be displayed. |
| `liveOptionAvailable` | Compatibility field indicating whether the provider capability allows use. |
| `tradingScheduleState.isActive` | Indicates whether runtime behavior is currently inside the configured schedule. |
| `authState.sessionStatus` | Current auth-related runtime state. |
| `retryState.phase` | Current retry phase, such as `None`, `InitialAutomatic`, or `Periodic`. |
| `retryState.manualRetryAvailable` | Indicates whether the manual retry command may currently be used. |
| `igLogin.currentState` | Current IG login label source used by the UI to distinguish active, retrying, failed, blocked, and out-of-schedule states. |
| `igLogin.scheduleState` | IG-specific copy of the current schedule context kept inside the status response so the UI can show current login state without another read call. |
| `igLogin.retryState` | IG-specific retry context used for the current login-state presentation. |
| `igLogin.latestSnapshot` | The latest stored successful non-secret IG login payload, including summary fields, non-secret response headers, and the raw non-secret JSON payload. |
| `igLogin.latestProofData` | The latest read-only IG Demo proof data snapshot, or `null` when no proof data has been captured yet. |
| `stateAvailability` | `Available` when persisted runtime state exists, or `Missing` when no runtime state row exists yet. Missing state does not create a row or trigger reconciliation. |
| `lastReconciledAtUtc` | The persisted timestamp of the latest validation/reconciliation represented by the status projection, or `null` when state is missing or has not been validated. |

Status and event reads are projection-only operations. They do not call IG,
write or insert records, dispatch notifications, change transitions, or run
reconciliation. Event results are ordered newest first and use event ID to
stabilize equal timestamps.

If no persisted runtime state exists, the status projection returns
`stateAvailability: "Missing"` with a null status and null
`lastReconciledAtUtc`; the read does not create state. A populated status
returns the persisted freshness timestamp without updating it. Event filters
are applied by the read projection and retain the existing newest-first,
event-ID tie-break ordering and 50-item limit.

### Secret-safety notes

- The `igLogin.latestSnapshot` object excludes credentials, session tokens, account-security tokens, and equivalent protected values.
- The `igLogin.latestProofData` object is read-only and excludes credentials, session tokens, account-security tokens, and any write-capable context.
- Only approved non-secret response headers are returned.
- The UI expands the latest payload locally from this response; there is no separate latest-payload endpoint.

### igLogin.latestProofData

When proof data has been successfully retrieved after a Demo session, the `latestProofData` field is populated:

```json
"igLogin": {
  "latestProofData": {
    "preferredAccountName": "Demo Account",
    "preferredAccountId": "ACC12345",
    "balance": 5000.00,
    "openPositionCount": 2,
    "retrievedAtUtc": "2025-01-15T10:30:00+00:00"
  }
}
```

When no proof data has been retrieved yet (for example, on first startup before an auth tick has completed), the field is `null`:

```json
"igLogin": {
  "latestProofData": null
}
```

The `latestProofData` object is read-only and derived from IG Demo account and position queries. It does not contain session tokens, credentials, or any write-capable context.

## GET /api/platform/ig-login/history

Returns retained daily first-successful non-secret IG login payloads within the 90-day retention window for the currently configured broker environment.

### Authorization

Requires a bearer token with the `viewer` scope or `Viewer`, `Operator`, or `Administrator` role.

### Response shape

```json
{
  "retainedSnapshots": [
    {
      "snapshotId": "33333333-3333-3333-3333-333333333333",
      "capturedAtUtc": "2026-04-01T09:55:00+00:00",
      "tradingDay": "2026-04-01",
      "currentAccountId": "retained-demo-session",
      "lightstreamerEndpoint": "https://demo-apd.marketdatasystems.com",
      "sessionExpiresAtUtc": null,
      "responseHeaders": {
        "Version": "3"
      },
      "rawNonSecretPayloadJson": "{\"currentAccountId\":\"retained-demo-session\",\"lightstreamerEndpoint\":\"https://demo-apd.marketdatasystems.com\",\"headers\":{\"Version\":\"3\"}}"
    }
  ]
}
```

### Field notes

| Field | Meaning |
| --- | --- |
| `retainedSnapshots` | Ordered list of retained daily first-successful snapshots, newest trading day first. May be empty when no successful login has been captured yet. |
| `snapshotId` | Unique identifier for the retained snapshot record. |
| `capturedAtUtc` | When the snapshot was captured. |
| `tradingDay` | The trading day the first successful login corresponds to. One retained entry per day. |
| `currentAccountId` | Non-secret account identifier returned by the broker login response. |
| `lightstreamerEndpoint` | Non-secret Lightstreamer endpoint returned by the broker, when supplied. |
| `sessionExpiresAtUtc` | Session expiry time when returned by the broker. |
| `responseHeaders` | Approved non-secret response headers from the broker login response. |
| `rawNonSecretPayloadJson` | Full non-secret JSON payload captured from the broker login response. |

### Secret-safety notes

- This endpoint never returns credentials, session tokens, account-security tokens, or equivalent protected values.
- Only the first successful non-secret payload of each trading day is retained.
- The endpoint returns at most one entry per trading day.
- Entries older than 90 days are removed by the shared retention processor.
- The current-state latest snapshot is served by `GET /api/platform/status`, not this endpoint.

## Broker environment and configuration routes

`GET /api/platform/broker-environments` is viewer-readable and returns catalog
metadata without credentials. `GET /api/platform/broker-environments/status`
returns the immutable platform environment, selected and applied broker records,
revision, and `restartRequired`.

`POST /api/platform/broker-environments/selection` is Operator-authorized. It
accepts `brokerEnvironmentId`, `expectedRevision`, and `acknowledged`. The
server validates lifecycle and provider capability, persists the selected ID,
and reports that the change applies only after restart. A running process never
changes its applied broker account or endpoint.

`POST /api/platform/broker-environments` and
`POST /api/platform/broker-environments/{id}/credentials` are Administrator-
authorized. Creation requires a uniquely normalized name and copies the active
server-owned defaults snapshot. Credential replacement is write-only.

Retirement is a two-step Administrator flow:
`POST /api/platform/broker-environments/{id}/retirement-preview` returns purge
and retained counts plus an expiring confirmation token;
`POST /api/platform/broker-environments/{id}/retire` requires that token, the
expected concurrency token, and the exact typed name. Selected/applied records,
stale tokens, name mismatches, unavailable records, and concurrency conflicts
are rejected without partial retirement. Mutable broker state and credentials
are purged; audit, event, notification, account-retrieval, and desired-state
history is retained under ordinary retention.

`GET /api/platform/configuration` remains the redacted schedule, retry, and
notification profile surface. `PUT /api/platform/configuration` cannot mutate
platform identity or broker selection. It validates profile invariants before
persistence and returns presence flags only for credentials.

The platform environment is one of `Desktop`, `Development`, `Test`, or `Live`
and is deployment-owned. Missing or unknown values fail startup closed.
## POST /api/platform/auth/manual-retry

Triggers manual retry when the current runtime state allows it.

### Success response

- status: `202 Accepted`
- location: `/api/platform/status`

```json
{
  "retryCycleId": "11111111-2222-3333-4444-555555555555"
}
```

### Conflict response

When the action is not currently allowed, the endpoint returns `409 Conflict`.
The Application handler returns a typed rejection and the API performs this
transport mapping. The current reasons and messages are:

| Reason | Message |
| --- | --- |
| `ScheduleInactive` | Manual retry is unavailable while the trading schedule is inactive. |
| `BlockedLive` | IG live is unavailable while the platform environment is Test. |
| `RetryLimitNotReached` | Manual retry becomes available only after the initial automatic retries are exhausted. |

```json
{
  "error": "Manual retry becomes available only after the initial automatic retries are exhausted."
}
```

## GET /api/platform/events

Returns redacted operational events.

### Query parameters

| Parameter | Meaning |
| --- | --- |
| `category` | Optional category filter, such as `auth`. |
| `environment` | Optional broker-environment filter, such as `Demo` or `Live`. |

## GET /api/platform/auth/administration

Returns the current auth-provider summary for administrator users.

### Example response

```json
{
  "provider": "Keycloak",
  "roleClaimType": "role",
  "apiAudience": "tnc-trading-platform-api"
}
```

### Example request

```text
GET /api/platform/events?category=auth&environment=Demo
```

### Response shape

```json
{
  "events": [
    {
      "eventId": 1,
      "category": "auth",
      "eventType": "AuthAttempted",
      "platformEnvironment": "Live",
      "brokerEnvironment": "Demo",
      "summary": "IG demo auth attempt started.",
      "details": "{\"environment\":\"Demo\",\"credentials\":\"[redacted]\"}",
      "occurredAtUtc": "2026-04-01T10:00:00+00:00"
    }
  ]
}
```

## Development-time API discovery

In development, the API also exposes:

- OpenAPI document mapping
- Scalar API reference UI through the AppHost service link

## Related documents

- [Operator guide](operator-guide.md)
- [Runtime behavior](runtime-behavior.md)
- [Architecture](architecture.md)

## Account Preferences

Account Preferences is an Operator-only control for the configured IG Test account. SQL owns the durable projection and IG supplies the account-bound confirmed value. The feature does not authorize real orders or monetary exposure, and Live is rejected before provider I/O.

`GET /api/platform/account-preferences` is a SQL-only projection. It returns desired and observed values, server-owned account identifiers, desired revision, verification status, last verification time, retry metadata, and a secret-safe failure summary. Statuses are `Unconfigured`, `Pending`, `InSync`, `VerificationFailed`, and `Unsupported`; `Drifted` is an intermediate condition during Check status. Legacy `trailingStopsEnabled` and `applicationStatus` aliases remain in the response.

`PUT /api/platform/account-preferences` requires an `Idempotency-Key` header and accepts only `trailingStopsEnabled` and optional `expectedRevision` in the body. The server derives the target account from the latest IG login snapshot and the audit actor from authenticated claims. It performs an account-bound IG read, conditional write when needed, and confirming readback before atomically persisting the desired state, audit, confirmed observation, projection, and journal completion. A matching repeated key replays the operation; incompatible reuse returns `409`.

`POST /api/platform/account-preferences/check-status` has no request body. It compares the persisted desired value with IG, updates IG when they differ, confirms the result, and returns the refreshed SQL projection. It can recover an earlier `RemoteApplied` save journal entry before comparing state. Stale revisions, account mismatch, and lease contention return `409`; an indeterminate save returns `503` with `Save outcome unknown`. A processed provider failure returns `200` with `VerificationFailed` and a safe warning in the projection.
The live routes map invalid input to `400`, recognized IG allowance exhaustion
to `429`, malformed provider data to `502`, provider rejection/unavailability
to `503`, and timeout to `504`. History returns `400` for invalid page size or
cursor. Provider diagnostics, credentials, session tokens, and raw responses
are never returned.

`GET /api/platform/account-preferences/observations?pageSize={size}&cursor={opaque}`
reads SQL observations only, using deterministic keyset ordering and an
adjacent cursor. The cursor is valid only for the same platform and broker
environment partition and is rejected when its membership or shape is invalid.
Observations use `Retention:OperationalRecordsDays`; archive/export is
deferred.