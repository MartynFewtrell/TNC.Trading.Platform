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

Automated auth tests deliberately use a different lifecycle from normal local development. They set `AppHost:UsePersistentKeycloakState=false`, use a volume-free session-scoped Keycloak container, and import the checked-in realm into clean state. This prevents stale realms, users, sessions, and client mutations from crossing test sessions. It does not remove or change the persistent local-development mode described below.

## Phase 0 migration decisions

The following are the migration baseline for local persistence and host
startup. They distinguish the current development behavior from the intended
deployment lifecycle.

* Infrastructure owns source-controlled EF Core migrations beside
	`PlatformDbContext`. The Infrastructure startup initializer applies them before
	bootstrap configuration and retention. It never deletes or resets a persistent
	local SQL resource.
* Existing local databases created without migration history require an explicit
	guarded transition. If compatibility cannot be proven, startup fails with
	operator guidance rather than silently deleting data or guessing a baseline.
	Migration failures follow the same boundary: prior migration history and
	conflicting schema objects are preserved, readiness remains unhealthy, and a
	corrected database can be retried by restarting the host.

The Infrastructure SQL integration suite creates and drops only a uniquely named
temporary database inside the already-running Docker SQL Server container. It
does not start the API, invoke startup reset behavior, or modify the persistent
`platformdb` database.
* Development reset behavior is disposable by design: Docker-backed SQL and
	Keycloak resources may be removed and recreated when local state is stale.
	Resetting local resources is not a production recovery procedure.
	A disposable local reset is appropriate only when the local database can be
	discarded. For a persistent or production database, preserve the SQL resource,
	correct the incompatible or partial schema through the approved operator
	procedure, and retry startup after the correction.
* API startup invokes one Infrastructure initializer for schema migration,
	bootstrap configuration, and retention, then dispatches the initial
	reconciliation. The initial reconciliation is blocking; the API-owned
	supervisor then continues once per second after hosted startup. A transient
	scheduled failure is logged and retried on the next tick.
* Reconciliation uses a SQL Server session-owned application lock so multiple
	API replicas sharing `platformdb` cannot run the write workflow concurrently.
	The lock is released when the SQL session closes; the integration suite proves
	contention and reacquisition with independent contexts.
* Saving `/configuration` commits the SQL-backed configuration, supplied
	protected credentials, and audit record before requesting reconciliation.
	Provider calls are outside that local transaction; a later supervisor tick
	retries recovery after an interrupted external effect.
* Shared ASP.NET Data Protection keys must use a persistent, access-controlled
	key ring in deployed environments so cookies and protected credentials remain
	decryptable across restarts. Local development may use Aspire-managed local
	persistence, but deleting that local state invalidates protected local data.
	* The API and Web share the SQL-backed `DataProtectionKeys` table in the
	`platformdb` resource. The default key lifetime is 90 days; set
	`DataProtection:KeyLifetimeDays` to a positive deployment-specific value when
	needed. Key rotation retains old keys for decryption, while deleting the
	local SQL volume invalidates protected credentials and platform cookies.
	* The latest IG Demo proof snapshot is stored in the SQL-backed `IgProofData`
	table, one row per broker environment. Replacing the API process preserves
	the last successful snapshot while the persistent SQL resource remains
	available because production DI selects the EF SQL proof-data adapter.
	Removing the SQL volume removes that snapshot and is a deliberate local-state
	reset.

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

Do not redirect AppHost stdout or stderr into repository-root files such as `apphost.out.log`, `apphost.err.log`, `dashboard.html`, or `dashboard-cookies.txt`. Those captures are not part of the supported workflow and they create local root artifacts that are easy to restage accidentally.

If you need persisted local output for troubleshooting, keep it in the terminal or write it to an ignored location such as `artifacts/local/` or a temporary folder outside the repository root.

New developers should not be prompted for SQL Server or Keycloak passwords in the Aspire Dashboard during normal first-run startup. AppHost now relies on Aspire-managed local credentials for those infrastructure resources.

## Infrastructure credential handling

AppHost manages the local SQL Server and Keycloak infrastructure credentials through Aspire's local secret handling.

- You do not need to set a SQL Server or Keycloak admin password manually for normal local startup.
- The Keycloak admin console username is `keycloak-admin`.
- The Keycloak admin console password remains the Aspire-managed local Keycloak admin password, not the seeded operator password.
- The seeded Keycloak user password `LocalAuth!123` is unchanged and is used only for local operator sign-in validation.
- Infrastructure admin credentials and seeded local operator credentials are separate concerns.

## IG Demo credentials and proof-data verification

To supply IG Demo credentials in local development, sign in as an operator and navigate to `/configuration`. Enter the IG API key, identifier, and password in the credential fields, then save the form. The credentials are stored using ASP.NET Core Data Protection and are never displayed in plaintext after saving.

After you save the credentials, the background supervisor attempts a real IG Demo auth on the next scheduled tick. In a local development run, this is typically within a few seconds.

To verify the connection:

1. Navigate to `/status`.
2. Confirm the **IG login** accordion shows a successful auth state and a recent `LastSuccessfulLoginAtUtc` timestamp.
3. Expand **IG Demo proof data** and confirm it shows real account data.

Proof-data queries are tied to auth events rather than a polling timer. They are low-frequency, read-only calls, so avoid manually triggering auth retries in rapid succession when the credentials are already correct and the session is active.

The status page reads the latest persisted snapshot on every projection read.
After an API restart, the last successful proof values remain visible until a
new successful query replaces them or the database state is reset.

If the **IG login** accordion shows a failed state, check that the credentials are correct and that the IG Demo API (`https://demo-api.ig.com`) is reachable from the local machine. Automated tests use controlled provider doubles and do not call real IG.

## Trailing-stops validation boundaries

The Account Preferences page presents the configured broker `Test` environment.
It reads desired state from SQL while account-bound verification observes IG
asynchronously. Apply the EF migration that creates
`AccountPreferencesCurrentStates` and its desired-state audit table before
testing. New and migrated rows begin `Unconfigured`; do not seed desired intent
from retained observations.
The stored provider key may remain the legacy `Demo` value so historical
partitions retain their lineage. This presentation mapping does not migrate or
rewrite existing observations.

Trailing-stops verification uses the typed `trailingStopsEnabled` provider
property. Set `Ig:AccountPreferencesBaseUrl` to the WireMock provider double to
exercise success, mismatch, timeout, malformed response, and account-mismatch
scenarios. The API starts and serves the SQL projection while the provider is
unavailable. Use verification retry for recovery and revision-bound remediation
for an explicit provider correction. Observation history is partitioned by
platform and broker environment, and cursors cannot cross partitions. Retention is controlled by
`Retention:OperationalRecordsDays`, with the 90-day default when the setting is
missing or non-positive.

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

The AppHost-backed Web functional and Web end-to-end auth suites validate the delivered Docker plus Keycloak topology directly. The API integration suite still prefers real Keycloak-issued bearer tokens for protected-route coverage, but it also retains a narrow synthetic slice that temporarily switches only the API project to the test auth provider so invalid JWT and claim-shape negatives can reach the API boundary deterministically. Web functional and browser suites now start the shared AppHost through Aspire-managed testing, discover the live Web listener from the managed runtime listener set instead of relying on fixed launch-settings ports, and force session-scoped Keycloak state for those auth collections so realm imports stay deterministic between runs.

The retained real-runtime auth matrix is intentionally small:

- one Web E2E sign-in smoke from listener discovery to protected UI content
- one Web functional post-sign-out fail-closed smoke
- one Web functional insufficient-role route-denial smoke
- one Web functional sign-out CSRF negative

Before any retained real-runtime assertion runs, the test harness waits for the Aspire Keycloak resource to become Healthy, verifies that the application realm discovery issuer exactly matches `http://localhost:8080/realms/tnc-trading-platform`, and completes a behavior-level readiness check. API tests prove token issuance with the expected test client, user, and scope. Web tests prove the real sign-in challenge reaches the Keycloak login page and the protected UI flow. The harness retries connection failures and temporary `404`, `429`, and `503` responses under one bounded deadline. Permanent HTTP or protocol failures, including `400`, `401`, malformed discovery data, and issuer mismatches, fail immediately.

The Web sign-in smoke also proves the imported wildcard localhost callback and origin configuration works with the runtime-discovered Web listener. The fixture does not need to mutate the Keycloak client for each randomized callback, which keeps the session state isolated.

Real-authentication test processes coordinate the fixed Keycloak port through the machine-local lease `%TEMP%\TNC.Trading.Platform\leases\keycloak-port-8080.lock`. The lease waits up to five minutes, then times out with diagnostics if another participating process still owns it. xUnit collections serialize fixtures only within one test assembly; the lease covers participating API, Web functional, and Web E2E processes on the same machine. It cannot coordinate another machine or an isolated container, and an independently running AppHost can still create a bind conflict on port 8080.

Fixture cleanup attempts every owned cleanup action, including stopping and waiting for external processes, disposing Aspire applications and builders, restoring environment overrides, and releasing the port lease. When cleanup itself fails, secondary exceptions are retained with the original failure so readiness errors do not hide resource leaks.

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

Mailpit shows provider delivery attempts, not a platform-level exactly-once
guarantee. A provider failure is persisted as a failed notification attempt
and a later reconciliation can produce another attempt. Repeating the same
notification identity can therefore create another captured message because
cross-process idempotency is not provided by the current local runtime.

## Troubleshooting

### Readiness returns `503`

- inspect AppHost, API, and web logs
- confirm the API completed startup configuration and initial runtime tick
- confirm the SQL container started successfully

### Configuration does not persist across restarts

- confirm Docker is running and AppHost started SQL Server successfully
- confirm an external `platformdb` connection string is configured if you are not using the default AppHost-managed local runtime
- if this started after switching from an older branch or setup, reset the persisted local `sql` and `keycloak` resources and retry

### Protected credentials cannot be decrypted after a reset

- confirm the persistent `sql` resource and its `platformdb` database are still present
- do not delete the SQL volume when preserving existing protected credentials is required
- if the key table or key material was intentionally lost, replace the IG credentials through the write-only configuration flow so new ciphertext is created

### Keycloak admin console shows a third-party iframe timeout

- open Keycloak through the direct local endpoint instead of an older proxied dashboard URL
- use the AppHost Keycloak link after restarting AppHost, or browse to `http://localhost:8080/admin/master/console/`
- if the problem persists after a branch change, reset the persisted local `keycloak` resource and retry

### AppHost-backed auth tests fail to find the Web listener

- confirm AppHost reached the running state and that the shared harness can rediscover the current Web runtime listener set
- confirm Docker, Keycloak, and SQL containers are healthy before rerunning the suite
- rerun after stopping stale AppHost processes so the real-runtime test helpers can discover the current listener set cleanly
- if Keycloak callback URLs still fail after a realm change, reset the persisted local `keycloak` resource or rerun with session-scoped Keycloak state so the latest realm import is applied

### AppHost-backed auth tests fail on Keycloak readiness or port 8080

- distinguish the three gates in the failure: Aspire resource health, exact application-realm issuer discovery, and behavior-level token or browser readiness
- treat connection failures and temporary `404`, `429`, or `503` responses as startup convergence; the harness retries them until its bounded deadline
- treat `400`, `401`, malformed discovery data, and issuer mismatches as configuration or realm defects; they fail immediately and should be investigated rather than retried
- stop any external AppHost or other process using port `8080` before rerunning the tests
- remember that the five-minute lease is machine-local and only coordinates processes using the repository test harness; it does not protect against an unrelated AppHost or a separate CI machine
- use the session-scoped, volume-free test mode when a stale realm, callback, user, or session is suspected; persistent local Keycloak state is not reconciled from the realm import on every startup
- run the focused authentication commands documented in [Testing and quality](testing-and-quality.md) before widening to the full solution suite

### The UI shows degraded status

- check whether the trading schedule is active
- check whether credentials are present
- check whether the platform is blocked by a Test-plus-Live combination

### Notification delivery is skipped

- confirm the selected provider has its required settings
- use Mailpit in local container-assisted mode for SMTP-based validation
- remember that `RecordedOnly` intentionally records notifications without external delivery
- inspect notification records and provider logs for `Failed`, `Skipped`, or `TimedOut` attempts before retrying

## Related documents

- [Documentation index](README.md)
- [Operator guide](operator-guide.md)
- [Runtime behavior](runtime-behavior.md)
