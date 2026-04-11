# Operator guide

This guide explains how the current Blazor operator UI works, what information each page shows, and how the existing workflows behave.

## Operator UI summary

The current UI is a Blazor Server app with two primary pages:

- `/status`
- `/configuration`

The left navigation keeps both views available during normal and degraded operation.

## Navigation

| Route | Purpose |
| --- | --- |
| `/status` | Runtime status, trading-schedule state, auth state, retry state, and recent auth events. |
| `/configuration` | Operator-managed configuration, notification settings, trading-schedule values, and write-only IG credential updates. |

## Status page

The status page is the main runtime dashboard.

### Environment panel

The environment panel shows:

- platform environment
- broker environment
- live option availability

This makes the currently active context easy to verify before any operator action.

### Trading schedule panel

The trading schedule panel shows:

- whether the schedule is active
- the current schedule reason
- daily start and end time
- configured trading days
- weekend behavior
- configured bank holidays
- configured time zone

This explains whether the platform considers itself in schedule or out of schedule.

### Auth state panel

The auth state panel shows:

- session status
- whether the platform is degraded
- blocked reason
- retry phase
- automatic attempt number
- next retry time

When the platform is degraded, the page also shows a warning banner.

### Manual retry button

The manual retry button is visible on the status page.

It is enabled only when:

- the retry limit has been reached
- the current trading schedule is active
- the session is degraded in a way that allows manual retry
- a retry is not already in progress

When manual retry succeeds, the page displays the new retry-cycle identifier returned by the API.

When manual retry is not allowed, the button stays disabled and the API protects the rule on the server side as well.

### Recent auth events table

The bottom of the page shows recent auth events filtered from the event history.

Each row includes:

- occurrence time
- event type
- summary

Only redacted event details are exposed.

## Configuration page

The configuration page is the main operator-edit surface.

It is designed for safe review and update of configuration without exposing stored secrets.

## Configuration sections

### Environments

The environments section lets the operator review or change:

- platform environment
- broker environment

Important behavior:

- the `Live` broker option is shown but disabled when the platform environment is `Test`
- changing startup-fixed values can set `RestartRequired`
- the page explains that startup-fixed changes apply on the next platform start

### Trading schedule

The trading schedule section lets the operator review or update:

- start of day
- end of day
- trading days
- weekend behavior
- bank holidays
- time zone

The page accepts comma-separated values for trading days and bank holidays.

### Retry policy

The retry policy section exposes the operator-managed values used by runtime supervision:

- initial delay seconds
- max automatic retries
- multiplier
- max delay seconds
- periodic delay minutes

### Notifications

The notifications section exposes:

- provider
- email recipient

The application records notification activity even when real delivery transports are not configured.

### IG credentials

The credentials section uses write-only secret handling.

The operator can see only whether each value is present:

- API key present or missing
- identifier present or missing
- password present or missing

The operator cannot read the stored values.

To replace a secret, the operator enters a new value in the corresponding field and saves the form.

## Save behavior

When the operator saves configuration:

1. the UI sends a `PUT /api/platform/configuration` request
2. the API validates the request
3. the configuration store persists the update
4. the API returns a redacted updated configuration snapshot
5. the page reloads from the returned model and shows a save result message

If the updated values require restart to take effect at runtime, the page displays restart guidance.

## Operator workflow examples

### Review current status

```mermaid
flowchart TD
    Open[Open /status] --> CheckEnv[Check environment values]
    CheckEnv --> CheckSchedule[Check trading schedule state]
    CheckSchedule --> CheckAuth[Check auth and retry state]
    CheckAuth --> ReviewEvents[Review recent auth events]
```

### Update configuration safely

```mermaid
flowchart TD
    OpenConfig[Open /configuration] --> EditValues[Edit non-secret values]
    EditValues --> ReplaceSecrets[Optionally enter replacement secrets]
    ReplaceSecrets --> Save[Save configuration]
    Save --> Result[Review save message and restart guidance]
```

### Trigger manual retry

```mermaid
flowchart TD
    ViewStatus[Open /status] --> CheckEligibility[Check manual retry button state]
    CheckEligibility -->|Enabled| Trigger[Trigger manual retry]
    Trigger --> Refresh[Page reloads status and events]
    CheckEligibility -->|Disabled| Wait[Wait for automatic retry or change conditions]
```

## Common operator-visible states

| State | What it means |
| --- | --- |
| `Active` | The platform currently considers auth-dependent runtime behavior healthy for the active schedule. |
| `Degraded` | The platform is running, but auth-dependent behavior is impaired. |
| `OutOfSchedule` | The trading schedule is currently inactive, so no active broker connection is expected. |
| `Blocked` | A forbidden combination, such as Test platform plus Live broker, has been prevented. |

## Safety rules surfaced in the UI

The UI reflects these key guardrails:

- stored secrets are never shown after capture
- the live broker option is disabled in the Test platform environment
- manual retry is unavailable until automatic retry exhaustion has occurred
- restart-required state is shown when startup-fixed configuration has changed
- degraded auth state does not make the whole UI unavailable

## Troubleshooting from the UI

### The status page shows `Degraded`

Check these items first:

- whether the trading schedule is active
- whether any credentials are missing
- whether the blocked reason explains the issue
- whether retry scheduling is active
- whether manual retry has become available

### The configuration page says restart is required

This means a startup-fixed setting changed. The new value is persisted, but the currently running runtime state continues using the prior startup-applied environment selection until the next application start.

### The manual retry button is disabled

This usually means one of these conditions is true:

- the trading schedule is inactive
- the retry limit has not been reached yet
- the session is not in the right degraded state
- a retry is already in progress

## Related documents

- [Application overview](application-overview.md)
- [API reference](api-reference.md)
- [Runtime behavior](runtime-behavior.md)
