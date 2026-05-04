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

New developers should not be prompted for SQL Server or Keycloak passwords in the Aspire Dashboard during normal first-run startup. AppHost now relies on Aspire-managed local credentials for those infrastructure resources.

## Infrastructure credential handling

AppHost manages the local SQL Server and Keycloak infrastructure credentials through Aspire's local secret handling.

- You do not need to set a SQL Server or Keycloak admin password manually for normal local startup.
- The Keycloak admin console username is `keycloak-admin`.
- The Keycloak admin console password remains the Aspire-managed local Keycloak admin password, not the seeded operator password.
- The seeded Keycloak user password `LocalAuth!123` is unchanged and is used only for local operator sign-in validation.
- Infrastructure admin credentials and seeded local operator credentials are separate concerns.

## Reset previously persisted local state

If you already ran an older AppHost configuration that required explicit SQL Server or Keycloak passwords and local startup now fails, reset the persisted local infrastructure for the `sql` and `keycloak` AppHost resources, then start AppHost again.

Recommended reset sequence:

1. Stop the running AppHost.
2. Delete the persisted Docker resources created for the local `sql` and `keycloak` AppHost resources.
3. Start AppHost again so Aspire can recreate the resources with its default local credential handling.

You may also remove stale AppHost user-secrets entries for the old explicit password parameters if you previously created them:

```powershell
dotnet user-secrets --project src/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.csproj remove "Parameters:keycloak-admin-password"
dotnet user-secrets --project src/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.csproj remove "Parameters:sql-password"
```

Removing those user-secrets entries is optional because AppHost no longer reads them for normal local startup.

## What AppHost exposes

When the application is running, AppHost exposes links for:

- the Blazor operator UI
- the API service
- Keycloak
- Scalar UI in development
- Mailpit UI

The operator UI entry point is `/` on the web application. In a signed-out browser session, that route immediately redirects to sign-in.

Keycloak is exposed directly on its stable local port so browser-based authentication and the Keycloak admin console use the same origin as the Keycloak server itself. The AppHost dashboard Keycloak link opens the direct admin console endpoint at `http://localhost:8080/admin/master/console/`. Sign in there with the Keycloak admin username `keycloak-admin` and the Aspire-managed Keycloak admin password. Use `http://localhost:8080/` when you need the Keycloak server root instead.

## Current operator UI presentation

When the Web UI starts successfully, expect these presentation behaviors:

- the signed-in operator experience uses a compact top header plus collapsible left navigation
- the UI starts in dark theme until a browser-specific theme preference is stored
- the home page acts as an operator overview after sign-in
- the status and configuration pages use grouped accordion sections

## Validate

### Automated validation

Run the test suite from the repository root:

```powershell
dotnet test
```

If a local machine hits intermittent MSBuild child-node exits during repository-wide test execution, rerun the suite in serialized mode:

```powershell
dotnet test -m:1
```

The auth work package now also includes a dedicated Web unit test project for policy registration and operator-context mapping. It is included in the repository-wide `dotnet test` run.

### Manual validation

Verify these paths through the AppHost-exposed service URLs:

- API liveness: `GET /health/live`
- API readiness: `GET /health/ready`
- Web UI entry route: `GET /`
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

1. `/` redirects to sign-in when signed out.
2. `/` also redirects to sign-in when the browser carries a stale platform cookie without a usable delegated access token.
3. `/` requires a fresh sign-in challenge when the operator opens the UI entry route for a new browser visit, even if the browser still holds a previously issued platform session cookie.
4. `local-viewer` can open `/status` but not operator or administrator-only areas.
5. `local-operator` can open `/status` and `/configuration`.
6. `local-admin` can open `/status`, `/configuration`, and `/administration/authentication`.
7. `local-norole` authenticates successfully but is routed to `/authentication/access-denied`.
8. signing out returns the operator to `/`.
9. requesting `/status`, `/configuration`, or `/administration/authentication` while signed out redirects the operator to `/authentication/sign-in` with the intended `returnUrl` preserved.
10. after sign-out, requesting a protected route returns the operator to the sign-in entry point before protected content is available again.
11. the recent auth events view on `/status` shows redacted operator sign-in, sign-out, denial, and token-acquisition-failure audit events after those actions are exercised.
12. the shared header shows the signed-in operator name, a sign-out action, and an environment badge when status data is available.
13. the shared header theme toggle and the smaller configuration-page theme toggle both apply light and dark theme changes immediately.
14. reloading the same browser preserves the previously selected theme.

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
- if this started after switching from an older branch or setup, reset the persisted local `sql` and `keycloak` resources and retry

### Keycloak admin console shows a third-party iframe timeout

- open Keycloak through the direct local endpoint instead of an older proxied dashboard URL
- use the AppHost Keycloak link after restarting AppHost, or browse to `http://localhost:8080/admin/master/console/`
- if the problem persists after a branch change, reset the persisted local `keycloak` resource and retry

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
