# API reference

This document describes the current HTTP surface exposed by the platform API.

## Base URLs

In local Aspire runs, the API base URL is assigned by AppHost. The dashboard also exposes a link to Scalar UI in development.

The Blazor UI talks to the API over service discovery using the internal `https+http://api` address.

## Endpoint summary

| Method | Route | Purpose |
| --- | --- | --- |
| `GET` | `/` | Alias for platform status. |
| `GET` | `/metadata` | Basic service metadata. |
| `GET` | `/health/live` | Liveness endpoint. |
| `GET` | `/health/ready` | Readiness endpoint. |
| `GET` | `/api/platform/status` | Current runtime status and retry state. |
| `GET` | `/api/platform/configuration` | Current redacted configuration snapshot. |
| `PUT` | `/api/platform/configuration` | Update operator-managed configuration. |
| `POST` | `/api/platform/auth/manual-retry` | Trigger a manual retry cycle when allowed. |
| `GET` | `/api/platform/events` | Return redacted operational events. |

## General behavior

- JSON uses web defaults and camel-case field names.
- Secret values are never returned by configuration or status endpoints.
- Validation failures on configuration updates return a validation-problem payload.
- Manual retry conflicts return `409 Conflict` when the current runtime state does not allow the action.

## GET /

Returns the same payload as `GET /api/platform/status`.

This gives a convenient default status route for quick inspection.

## GET /metadata

Returns lightweight service metadata.

### Example response

```json
{
  "service": "TNC.Trading.Platform.Api",
  "environment": "Development"
}
```

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

Returns the current platform runtime state.

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
  "updatedAtUtc": "2026-04-01T10:00:00+00:00"
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
