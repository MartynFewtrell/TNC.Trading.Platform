# Local development guide

This document explains how to build, run, validate, and troubleshoot the current application locally.

## Prerequisites

- .NET SDK installed from the version pinned by `global.json`
- Docker Desktop because AppHost now requires Docker-managed infrastructure for the supported local runtime, including Keycloak for operator authentication

## Current local runtime mode

The supported local runtime uses Docker-managed infrastructure.

The AppHost remains the single local composition root, but its responsibilities are now split into focused support files for infrastructure registration, project registration, and shared environment wiring. The top-level `AppHost.cs` stays limited to builder creation, composition calls, and `Build().Run()`.

AppHost starts:

- SQL Server
- the `platformdb` database
- Mailpit for local SMTP capture
- Keycloak with the imported local development realm

This mode is required because Keycloak is part of the local authentication stack and the in-memory SQL option is no longer a supported application runtime.

The distributed auth test suites use the real Aspire-managed AppHost and Keycloak runtime. There is no supported synthetic AppHost runtime path for local application startup or AppHost-backed distributed validation, although some lower-level unit tests still use dedicated test helpers that do not go through AppHost.

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

Those links are part of the manual validation surface for this repository. After AppHost starts, confirm that each dashboard link resolves and that the Web UI endpoint matches the runtime listener output rather than a fixed launch-settings assumption.

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

The auth work package now also includes a dedicated Web unit test project for policy registration, direct `PlatformApiClient` boundary coverage, and bUnit component coverage for the refreshed Blazor shell and operator pages. It is included in the repository-wide `dotnet test` run.

The AppHost-backed Web functional and Web end-to-end auth suites validate the delivered Docker plus Keycloak topology directly. The API integration suite still prefers real Keycloak-issued bearer tokens for protected-route coverage, but it also retains a narrow synthetic slice that temporarily switches only the API project to the test auth provider so invalid JWT and claim-shape negatives can reach the API boundary deterministically. Web functional and browser suites use real sign-in helpers that discover listener URLs from AppHost runtime output instead of relying on fixed launch-settings ports, and each auth collection now reuses one AppHost-plus-Keycloak runtime so the retained distributed checks stay narrower and less flaky.

The retained real-runtime auth matrix is intentionally small:

- one Web E2E sign-in smoke from listener discovery to protected UI content
- one Web functional post-sign-out fail-closed smoke
- one Web functional insufficient-role route-denial smoke
- one Web functional sign-out CSRF negative

### Manual validation

Use the AppHost dashboard links or the runtime listener URLs emitted in AppHost output, then walk through this validation sequence:

1. Confirm the AppHost links for the Web UI, API, Keycloak, Mailpit, and Scalar UI all resolve.
2. Verify API liveness and readiness from the AppHost-exposed API listener.
3. Open the Web UI root route and confirm the signed-out browser is redirected to the Keycloak-backed sign-in flow.
4. Sign in with a seeded local account and confirm the expected protected Web experience loads for that role.
5. Exercise one protected API route and one protected Web route through the real AppHost-managed runtime.
6. Open Mailpit and confirm the UI is reachable.

Verify these paths through the AppHost-exposed service URLs:

- AppHost dashboard links for the Web UI, API, Keycloak, Mailpit, and Scalar UI
- API liveness: `GET /health/live`
- API readiness: `GET /health/ready`
- Web UI entry route: `GET /`
- protected API status: `GET /api/platform/status`
- protected Web status page: `GET /status`
- protected Web configuration page: `GET /configuration`

In development, also check the Scalar link from AppHost.

When validating the Web UI manually, prefer the runtime listener URLs surfaced by the AppHost dashboard or console output rather than assuming `launchSettings.json` ports.

For AppHost-backed distributed validation, use the same Docker plus Keycloak local runtime that the automated integration, functional, and end-to-end suites use. There is no supported synthetic AppHost runtime path for local startup or for AppHost-backed manual checks.

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
13. the shared header theme toggle applies light and dark theme changes immediately.
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

### AppHost-backed auth tests fail to find the Web listener

- confirm AppHost reached the running state and emitted the Web authentication entry URL in its console output
- confirm Docker, Keycloak, and SQL containers are healthy before rerunning the suite
- rerun after stopping stale AppHost processes so the real-runtime test helpers can discover the current listener set cleanly

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
