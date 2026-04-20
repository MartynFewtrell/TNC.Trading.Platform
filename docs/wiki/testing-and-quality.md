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
- viewer, operator, and administrator bearer-token access behavior
- status, configuration, and administrator auth-summary endpoints
- secret-safe responses
- role-boundary enforcement across protected API routes

### UI behavior

The functional and end-to-end tests cover:

- public landing-page behavior
- local sign-in surface behavior in lightweight automated runs
- protected configuration access after sign-in
- no-role access-denied routing
- administrator-only browser access to the auth-administration page

## Quality characteristics currently protected

The automated suite already checks important non-functional expectations:

- environment safety for Test versus Live selection
- write-only secret handling
- redaction of sensitive values from records and responses
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

To build first:

```powershell
 dotnet build
 dotnet test
```

## Local test behavior notes

Many integration, functional, and end-to-end tests run through the Aspire AppHost.

The auth-focused test setup commonly disables infrastructure containers by setting:

- `AppHost__EnableInfrastructureContainers=false`

This keeps the suites lightweight while still exercising the distributed application shape. In this mode, AppHost switches the Web and API hosts to the local test authentication provider instead of starting Keycloak.

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
