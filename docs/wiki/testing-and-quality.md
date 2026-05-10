# Testing and quality

This document explains how the current solution is validated, what each test suite covers, and what kinds of regressions the repository is already protecting against.

## Testing strategy summary

The repository uses multiple test levels so the current control-plane behavior is validated from unit level up to browser-driven flows.

## Test projects

| Project | Test type | Focus |
| --- | --- | --- |
| `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests` | Unit | Retry timing, schedule evaluation, auth-state behavior, and application logic. |
| `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests` | Unit | Configuration persistence, secret protection, notification providers, redaction, and retention behavior. |
| `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests` | Unit | API-level validation behavior. |
| `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests` | Integration | API contracts and AppHost-backed service behavior. |
| `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests` | Unit | Web authentication policy registration, claim mapping, and authenticated operator context behavior. |
| `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests` | Functional | Requirement-driven UI behavior and rendered HTML outcomes. |
| `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests` | End-to-end | Browser-based flows across the Blazor UI and API. |

## What the tests already cover

### Application behavior

The unit tests cover:

- retry-delay calculation and backoff caps
- missing-credential degraded behavior
- retry-state visibility rules
- blocked-live safety behavior
- notification suppression and retry-cycle updates
- schedule evaluation

### Infrastructure behavior

The unit tests cover:

- SQL-backed configuration seeding and update behavior
- restart-required behavior for startup-fixed changes
- protected credential storage and rotation
- secret redaction in audits and operational data
- notification provider fallback and dispatch recording
- retention cleanup for operational records

### API behavior

The API tests cover:

- health endpoints
- anonymous `401` behavior for protected endpoints
- invalid issuer, invalid audience, invalid signature, expired, and no-role bearer-token fail-closed behavior
- viewer, operator, and administrator bearer-token access behavior across status, configuration, manual-retry, events, and administrator auth-summary endpoints
- persisted operator auth audit-event recording through the protected API boundary for sign-in, sign-out, access-denied, and token-acquisition-failure outcomes
- secret-safe responses
- role-boundary enforcement across protected API routes

### UI behavior

The Web unit, functional, and end-to-end tests cover:

- shared authorization policy registration for viewer, operator, and administrator routes
- anonymous, no-role, and elevated-role operator-context mapping
- theme-mode parsing and Radzen Software theme selection for the shared UI shell
- delegated-scope token evaluation and navigation recovery decisions for protected UI flows
- lower-level protected-route redirect decisions and auth-audit helper behavior
- public landing-page behavior
- local sign-in surface behavior in lightweight automated runs
- route-first anonymous challenge behavior for protected status, configuration, and administrator surfaces
- compact functional role-matrix coverage for `local-viewer`, `local-operator`, `local-admin`, and `local-norole` across `/status`, `/configuration`, and `/administration/authentication`
- sign-out, post-sign-out denial, and deterministic session-loss recovery through the Blazor host
- delegated-scope recovery redirects for higher-privilege UI areas
- protected configuration access after sign-in
- no-role access-denied routing
- administrator-only browser access to the auth-administration page
- one retained Aspire dashboard plus real Keycloak smoke that verifies dashboard startup and the protected `/status` path without fixed-port assumptions

Focused manual validation still complements the automated suite for the refreshed shared shell, theme switching, remembered browser preference, header presentation, and narrower-width layout behavior.

## Quality characteristics currently protected

The automated suite already checks important non-functional expectations:

- environment safety for Test versus Live selection
- write-only secret handling
- redaction of sensitive values from records and responses
- persisted auth audit history for sign-in, sign-out, denied access, and token-acquisition failures without exposing tokens
- stable health endpoints for orchestration
- restart-required behavior for startup-fixed configuration changes
- durable operational history when SQL-backed persistence is used
- degraded-state visibility instead of silent failure

## Test naming and traceability

The repository uses descriptive test names and requirement comments.

In the current suites, many tests include comments that trace directly to:

- functional requirements such as `FR7`, `FR12`, and `FR20`
- security requirements such as `SR2` and `SR5`
- testing requirements such as `TR3` and `TR12`

This makes it easier to connect implementation behavior back to the work-package requirements in `docs/002-environment-and-auth-foundation/requirements.md`.

## Running the tests

From the repository root:

```powershell
 dotnet test
```

If a local environment hits intermittent MSBuild child-node exits during the repository-wide run, use serialized execution:

```powershell
 dotnet test -m:1
```

To build first:

```powershell
 dotnet build
 dotnet test
```

## Local test behavior notes

Many integration, functional, and end-to-end tests run through the Aspire AppHost.

The auth-focused test setup currently opts into a synthetic test-only runtime by setting:

- `AppHost__UseSyntheticRuntime=true`
- `Authentication__Test__EnableInteractiveSignIn=true` only in the Web auth suites that need the synthetic interactive sign-in surface

This keeps the suites lightweight while still exercising the distributed application shape. In this mode, AppHost switches the Web and API hosts to the synthetic test authentication provider and explicit in-memory persistence instead of starting the supported Docker plus Keycloak local runtime.

For Web auth scenarios, the synthetic interactive sign-in surface is enabled only as explicit test-harness composition when the synthetic runtime is selected. API integration tests continue to use signed synthetic bearer tokens directly without enabling that interactive Web harness.

For protected-route and sign-out functional coverage, the test suites now prefer deterministic cookie-container control and redirect assertions instead of adding arbitrary waits or broader browser-only scenarios.

The retained real-infrastructure auth smoke stays intentionally narrow. It verifies that the Aspire dashboard starts, discovers the AppHost-started Web UI endpoint from the runtime listener set instead of `launchSettings.json`, and then exercises one real Keycloak sign-in journey to the protected `/status` surface.

## Manual validation areas still worth checking

Automated tests cover a large part of the current control plane, but manual review is still useful for:

- checking AppHost dashboard links and service startup output
- checking local Mailpit behavior when infrastructure containers are enabled
- checking Scalar UI in development
- reviewing generated logs during degraded and recovery scenarios

## Quality checklist for future changes

When extending the application, keep these areas protected:

- do not expose stored secrets in UI, API, logs, or records
- preserve Test-platform live safety rules
- keep degraded-state UI available
- keep health endpoints stable
- maintain environment tagging in events and notifications
- keep retry behavior observable and deterministic

## Related documents

- [Application overview](application-overview.md)
- [Local development guide](local-development.md)
- [Runtime behavior](runtime-behavior.md)
