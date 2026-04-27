# Local development guide

This document explains how to build, run, validate, and troubleshoot the current application locally.

## Prerequisites

- .NET SDK installed from the version pinned by `global.json`
- Docker Desktop because AppHost now requires Docker-managed infrastructure for the supported local runtime, including Keycloak for operator authentication

## Current local runtime mode

The supported local runtime uses Docker-managed infrastructure.

AppHost starts:

- SQL Server
- the `platformdb` database
- Mailpit for local SMTP capture
- Keycloak with the imported local development realm

This mode is required because Keycloak is part of the local authentication stack and the in-memory SQL option is no longer a supported application runtime.

Synthetic authentication and in-memory persistence remain available only for isolated automated tests. They are not a supported local application runtime. The synthetic interactive Web sign-in surface is enabled only by explicit test-harness configuration in the automated Web auth suites that require it.

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

## Keycloak admin console credentials

AppHost starts Keycloak with an explicit admin console account for the supported local runtime.

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
- Keycloak
- Scalar UI in development
- Mailpit UI

The operator UI entry point is `/` on the web application.

## Validate

### Automated validation

Run the test suite from the repository root:

```powershell
dotnet test
```

The auth work package now also includes a dedicated Web unit test project for policy registration and operator-context mapping. It is included in the repository-wide `dotnet test` run.

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

For the supported local runtime, validate these seeded local accounts with the shared local-only password `LocalAuth!123`:

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
7. requesting `/status`, `/configuration`, or `/administration/authentication` while signed out redirects the operator to `/authentication/sign-in` with the intended `returnUrl` preserved.
8. after sign-out, requesting a protected route returns the operator to the sign-in entry point before protected content is available again.
9. the recent auth events view on `/status` shows redacted operator sign-in, sign-out, denial, and token-acquisition-failure audit events after those actions are exercised.

## Useful local scenarios

### Validate durable configuration locally

1. Start AppHost.
3. Sign in through Keycloak as `local-operator` or `local-admin`.
4. Save configuration changes in `/configuration`.
5. Restart the application and verify the values persist.

### Validate local SMTP capture

1. Start AppHost.
3. Open the Mailpit UI from the AppHost link.
4. Exercise a notification-producing scenario and inspect captured mail.

## Troubleshooting

### Readiness returns `503`

- inspect AppHost, API, and web logs
- confirm the API completed startup configuration and initial runtime tick
- confirm the SQL container started successfully

### Configuration does not persist across restarts

- confirm Docker is running and AppHost started SQL Server successfully
- confirm an external `platformdb` connection string is configured if you are not using the default AppHost-managed local runtime

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
