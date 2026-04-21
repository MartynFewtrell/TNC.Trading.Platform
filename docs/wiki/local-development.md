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
- the Web and API use the local test authentication provider
- external notification transports remain optional

### Container-assisted local mode

When `AppHost:EnableInfrastructureContainers=true` is set, AppHost can also start:

- SQL Server
- the `platformdb` database
- Mailpit for local SMTP capture
- Keycloak with the imported local development realm

Use this mode when you want durable local persistence, local notification validation, and real local OIDC sign-in.

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

## Keycloak admin console credentials

When infrastructure containers are enabled, AppHost starts Keycloak with an explicit admin console account.

- username: `keycloak-admin`
- password source: AppHost user secrets key `Parameters:keycloak-admin-password`

Set or replace the password from the repository root with:

```powershell
dotnet user-secrets --project src/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.csproj list
dotnet user-secrets --project src/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.csproj set "Parameters:keycloak-admin-password" "<your-password>"
```

## What AppHost exposes

When the application is running, AppHost exposes links for:

- the Blazor operator UI
- the API service
- Keycloak when infrastructure containers are enabled
- Scalar UI in development
- Mailpit UI when infrastructure containers are enabled

The operator UI entry point is `/` on the web application.

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
- Web UI landing page: `GET /`
- protected API status: `GET /api/platform/status`
- protected Web status page: `GET /status`
- protected Web configuration page: `GET /configuration`

In development, also check the Scalar link from AppHost.

### Local authentication validation

When Keycloak is enabled through AppHost infrastructure containers, validate these seeded local accounts with the shared local-only password `LocalAuth!123`:

- `local-admin`
- `local-operator`
- `local-viewer`
- `local-norole`

Expected behavior:

1. `/` stays public when signed out.
2. `local-viewer` can open `/status` but not operator or administrator-only areas.
3. `local-operator` can open `/status` and `/configuration`.
4. `local-admin` can open `/status`, `/configuration`, and `/administration/authentication`.
5. `local-norole` authenticates successfully but is routed to `/authentication/access-denied`.
6. signing out returns the operator to `/`.

## Useful local scenarios

### Explore the operator UI without Docker

1. Run AppHost in lightweight mode.
2. Open the web application link.
3. Open `/authentication/sign-in` and select a local test user.
4. Review `/status`, `/configuration`, or `/administration/authentication` based on the selected role.

### Validate durable configuration locally

1. Enable infrastructure containers.
2. Start AppHost.
3. Sign in through Keycloak as `local-operator` or `local-admin`.
4. Save configuration changes in `/configuration`.
5. Restart the application and verify the values persist.

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
