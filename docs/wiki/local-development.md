# Local development guide

This document explains how to build, run, validate, and troubleshoot the current application locally.

## Prerequisites

- .NET SDK installed from the version pinned by `global.json`
- Docker Desktop only if you want AppHost to start SQL Server and Mailpit containers

## Current local runtime modes

The application supports two useful local modes.

### Lightweight local mode

This is the default experience when infrastructure containers are not enabled.

- AppHost starts the API and Blazor UI
- the API uses the in-memory database provider
- external notification transports remain optional

### Container-assisted local mode

When `AppHost:EnableInfrastructureContainers=true` is set, AppHost can also start:

- SQL Server
- the `platformdb` database
- Mailpit for local SMTP capture

Use this mode when you want durable local persistence and local notification validation.

## Build

From the repository root:

```powershell
dotnet build
```

## Run

Start the distributed application from the repository root:

```powershell
dotnet run --project src/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.csproj
```

Optional alternative when the Aspire CLI is installed:

```powershell
aspire run
```

## Run with infrastructure containers

In PowerShell, enable the AppHost infrastructure switch before starting the application:

```powershell
$env:AppHost__EnableInfrastructureContainers = 'true'
dotnet run --project src/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.csproj
```

Clear the variable afterward if you want to return to the lightweight mode:

```powershell
Remove-Item Env:AppHost__EnableInfrastructureContainers
```

## What AppHost exposes

When the application is running, AppHost exposes links for:

- the Blazor operator UI
- the API service
- Scalar UI in development
- Mailpit UI when infrastructure containers are enabled

The operator UI entry point is `/status` on the web application.

## Validate

### Automated validation

Run the test suite from the repository root:

```powershell
dotnet test
```

### Manual validation

Verify these paths through the AppHost-exposed service URLs:

- API liveness: `GET /health/live`
- API readiness: `GET /health/ready`
- API status: `GET /api/platform/status`
- Web UI status page: `GET /status`
- Web UI configuration page: `GET /configuration`

In development, also check the Scalar link from AppHost.

## Useful local scenarios

### Explore the operator UI without Docker

1. Run AppHost in lightweight mode.
2. Open the web application link.
3. Review `/status` and `/configuration`.

### Validate durable configuration locally

1. Enable infrastructure containers.
2. Start AppHost.
3. Save configuration changes in `/configuration`.
4. Restart the application and verify the values persist.

### Validate local SMTP capture

1. Enable infrastructure containers.
2. Start AppHost.
3. Open the Mailpit UI from the AppHost link.
4. Exercise a notification-producing scenario and inspect captured mail.

## Troubleshooting

### Readiness returns `503`

- inspect AppHost, API, and web logs
- confirm the API completed startup configuration and initial runtime tick
- if SQL mode is enabled, confirm the SQL container started successfully

### Configuration does not persist across restarts

- confirm infrastructure containers are enabled or an external SQL connection string is configured
- remember that lightweight mode uses the in-memory provider only

### The UI shows degraded status

- check whether the trading schedule is active
- check whether credentials are present
- check whether the platform is blocked by a Test-plus-Live combination

### Notification delivery is skipped

- confirm the selected provider has its required settings
- use Mailpit in local container-assisted mode for SMTP-based validation
- remember that `RecordedOnly` intentionally records notifications without external delivery

## Related documents

- [Documentation index](README.md)
- [Operator guide](operator-guide.md)
- [Runtime behavior](runtime-behavior.md)
