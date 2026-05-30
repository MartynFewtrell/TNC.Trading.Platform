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
| `GET` | `/api/platform/configuration` | Current redacted configuration snapshot for operator-capable users. |
| `PUT` | `/api/platform/configuration` | Update operator-managed configuration for operator-capable users. |
| `POST` | `/api/platform/auth/manual-retry` | Trigger a manual retry cycle when allowed for operator-capable users. |
| `POST` | `/api/platform/auth/audit` | Persist operator authentication audit events for authenticated callers. |
| `GET` | `/api/platform/events` | Return redacted operational events for viewer-capable operators. |
| `GET` | `/api/platform/auth/administration` | Return the administrator-only auth summary surface. |

## General behavior

- JSON uses web defaults and camel-case field names.
- `/health/live`, `/health/ready`, `/`, and `/metadata` remain anonymous.
- protected endpoints require a bearer token issued for the platform API and return `401 Unauthorized` or `403 Forbidden` without browser redirects.
- Secret values are never returned by configuration or status endpoints.
- Validation failures on configuration updates return a validation-problem payload.
- Manual retry conflicts return `409 Conflict` when the current runtime state does not allow the action.

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
  "igLoginStatus": {
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
    "igProofData": {
      "preferredAccountName": "Demo Account",
      "preferredAccountId": "ACC12345",
      "balance": 5000.00,
      "openPositionCount": 2,
      "retrievedAtUtc": "2026-04-01T09:59:46+00:00"
    }
  }
}
```

### Field notes

| Field | Meaning |
| --- | --- |
| `platformEnvironment` | Current platform environment, `Test` or `Live`. |
| `brokerEnvironment` | Current broker environment, `Demo` or `Live`. |
| `liveOptionVisible` | Indicates the live option should still be shown in the UI. |
| `liveOptionAvailable` | Indicates whether the live option may actually be used. |
| `tradingScheduleState.isActive` | Indicates whether runtime behavior is currently inside the configured schedule. |
| `authState.sessionStatus` | Current auth-related runtime state. |
| `retryState.phase` | Current retry phase, such as `None`, `InitialAutomatic`, or `Periodic`. |
| `retryState.manualRetryAvailable` | Indicates whether the manual retry command may currently be used. |
| `igLoginStatus.currentState` | Current IG login label source used by the UI to distinguish active, retrying, failed, blocked, and out-of-schedule states. |
| `igLoginStatus.scheduleState` | IG-specific copy of the current schedule context kept inside the status response so the UI can show current login state without another read call. |
| `igLoginStatus.retryState` | IG-specific retry context used for the current login-state presentation. |
| `igLoginStatus.latestSnapshot` | The latest stored successful non-secret IG login payload, including summary fields, non-secret response headers, and the raw non-secret JSON payload. |
| `igLoginStatus.igProofData` | The latest read-only IG Demo proof data snapshot, or `null` when no proof data has been captured yet. |

### Secret-safety notes

- The `igLoginStatus.latestSnapshot` object excludes credentials, session tokens, account-security tokens, and equivalent protected values.
- The `igLoginStatus.igProofData` object is read-only and excludes credentials, session tokens, account-security tokens, and any write-capable context.
- Only approved non-secret response headers are returned.
- The UI expands the latest payload locally from this response; there is no separate latest-payload endpoint.

### igLoginStatus.igProofData

When proof data has been successfully retrieved after a Demo session, the `igProofData` field is populated:

```json
"igLoginStatus": {
  "igProofData": {
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
"igLoginStatus": {
  "igProofData": null
}
```

The `igProofData` object is read-only and derived from IG Demo account and position queries. It does not contain session tokens, credentials, or any write-capable context.

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

## GET /api/platform/configuration

Returns the current redacted configuration snapshot.

### Response shape

```json
{
  "platformEnvironment": "Test",
  "brokerEnvironment": "Demo",
  "tradingSchedule": {
    "startOfDay": "08:00:00",
    "endOfDay": "16:30:00",
    "tradingDays": ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday"],
    "weekendBehavior": "ExcludeWeekends",
    "bankHolidayExclusions": [],
    "timeZone": "UTC"
  },
  "retryPolicy": {
    "initialDelaySeconds": 1,
    "maxAutomaticRetries": 5,
    "multiplier": 2,
    "maxDelaySeconds": 60,
    "periodicDelayMinutes": 5
  },
  "notificationSettings": {
    "provider": "RecordedOnly",
    "emailTo": "operator@local.test"
  },
  "credentials": {
    "hasApiKey": false,
    "hasIdentifier": false,
    "hasPassword": false
  },
  "restartRequired": false,
  "updatedAtUtc": "2026-04-01T10:00:00+00:00"
}
```

### Secret handling

The `credentials` object reports presence only. It never contains raw secret values.

## PUT /api/platform/configuration

Updates operator-managed configuration.

### Request shape

```json
{
  "platformEnvironment": "Live",
  "brokerEnvironment": "Demo",
  "tradingSchedule": {
    "startOfDay": "08:00:00",
    "endOfDay": "16:30:00",
    "tradingDays": ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday"],
    "weekendBehavior": "ExcludeWeekends",
    "bankHolidayExclusions": [],
    "timeZone": "UTC"
  },
  "retryPolicy": {
    "initialDelaySeconds": 1,
    "maxAutomaticRetries": 5,
    "multiplier": 2,
    "maxDelaySeconds": 60,
    "periodicDelayMinutes": 5
  },
  "notificationSettings": {
    "provider": "RecordedOnly",
    "emailTo": "owner@example.com"
  },
  "credentials": {
    "apiKey": "new-api-key",
    "identifier": "new-identifier",
    "password": "new-password"
  },
  "changedBy": "operator"
}
```

### Behavior notes

- Empty or omitted credential values do not reveal the existing stored values.
- Changing startup-fixed values can set `restartRequired` in the response.
- The response body is the same redacted configuration model returned by `GET /api/platform/configuration`.

### Validation rules

The current validator enforces these main rules:

- platform environment must be `Test` or `Live`
- broker environment must be `Demo` or `Live`
- trading schedule end time must be later than start time
- at least one trading day is required
- weekend behavior must be valid
- time zone is required
- initial retry delay must be at least `1`
- max automatic retries must be at least `1`
- multiplier must be at least `2`
- max delay must be greater than or equal to initial delay
- periodic delay minutes must be at least `1`
- notification provider is required
- `changedBy` is required
- `Test` platform plus `Live` broker is rejected

### Validation error example

```json
{
  "errors": {
    "BrokerEnvironment": [
      "IG live is visible but unavailable while the platform environment is Test."
    ]
  }
}
```

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
