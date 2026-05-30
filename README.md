# TNC.Trading.Platform

TNC.Trading.Platform is a .NET 10 trading platform under active development for safe, iterative delivery of algorithmic day-trading capabilities against IG APIs.

## Current status

The repository currently provides the operator authentication and authorization foundation, the refreshed operator UI and AppHost validation work, and the `IG` Test login status/history capability delivered through work packages 003 to 006.

- .NET Aspire AppHost for local orchestration of the API, Blazor operator UI, SQL Server, Keycloak, Mailpit, and supporting services
- Aspire-managed Keycloak local identity composition with seeded development users for the supported local runtime
- Minimal API backend for protected platform status, retained `IG` login history, configuration, event history, manual auth-retry actions, authentication audit capture, and admin auth summary data
- Blazor-based operator UI with sign-in-first navigation, a shared operator shell, protected status, retained `IG` login history, configuration, and authentication-administration surfaces, plus sign-in, sign-out, and access-denied flows
- Backend-maintained `IG` Test login supervision with trading-schedule-aware startup/runtime behavior, retry state visibility, blocked-live safeguards, and secret-safe operational records
- Secret-safe persistence of the latest successful `IG` login payload plus one retained first-successful snapshot per trading day for 90 days
- SQL-backed operator-managed configuration for the supported local runtime
- Protected handling for `IG` credentials with write-only update flows and environment-scoped operational records
- Shared `Viewer`, `Operator`, and `Administrator` role policies across the Web UI and API
- Delegated bearer-token propagation from the Blazor host to the protected API
- Trading-schedule-aware auth-state supervision, retry scheduling, notification recording, and retained operational history
- Shared service defaults for health checks, OpenTelemetry, and hosting concerns
- Requirement-driven unit, integration, functional, and end-to-end automated test coverage

The following capabilities are not implemented yet:

- `IG` live-environment enablement and live trading execution
- market discovery and pricing integration
- order placement and trade lifecycle management
- strategy execution and risk controls

## Solution structure

- [`src/TNC.Trading.Platform.AppHost`](src/TNC.Trading.Platform.AppHost/) - local orchestration entry point for running the API, Blazor UI, and supporting local services
- [`src/TNC.Trading.Platform.Api`](src/TNC.Trading.Platform.Api/) - minimal HTTP API for platform status, retained `IG` login history, configuration, events, manual retry, authentication audit, OpenAPI, and health endpoints
- [`src/TNC.Trading.Platform.Application`](src/TNC.Trading.Platform.Application/) - application-layer features, handlers, models, and orchestration services for platform configuration and auth supervision
- [`src/TNC.Trading.Platform.Infrastructure`](src/TNC.Trading.Platform.Infrastructure/) - persistence, credential protection, notifications, broker integration, and infrastructure wiring
- [`src/TNC.Trading.Platform.ServiceDefaults`](src/TNC.Trading.Platform.ServiceDefaults/) - shared defaults for service configuration and hosting
- [`src/TNC.Trading.Platform.Web`](src/TNC.Trading.Platform.Web/) - Blazor-based operator UI for current status, retained `IG` login history, configuration, authentication administration, and operational visibility
- [`test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests`](test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/) - API unit tests for validation, configuration rules, retry behavior, and endpoint-facing contracts
- [`test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests`](test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/) - closed-box integration tests for status, retained `IG` login history, configuration, auth-state, retry, and notification behavior through the AppHost
- [`test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests`](test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/) - application-layer unit tests for scheduling, retry, configuration, and auth coordination logic
- [`test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests`](test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/) - infrastructure unit tests for storage, protection, notifications, retention, and external-integration support
- [`test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests`](test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/) - functional tests for the Blazor operator experience, including protected route, sign-out, and current-versus-history status flows
- [`test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests`](test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/) - end-to-end tests for browser-level validation across the API and Blazor UI

The repository now follows the current guideline that non-generated C# code keeps one top-level type per file with a matching file name, which improves Solution Explorer navigation and code review clarity.

## Getting started

For local prerequisites, build steps, run commands, and validation guidance, see the [Local development guide](docs/local-development.md).

For a broader view of the implemented platform, including the application overview, architecture, operator experience, runtime behavior, API surface, testing guidance, and local development workflow, start with the [documentation index](docs/README.md).

The current local implementation is started with the AppHost and exposes:

- the Blazor operator UI
- `GET /api/platform/status`
- `GET /api/platform/ig-login/history`
- configuration, event-history, manual-retry, auth-audit, and administrator-authentication API endpoints
- `GET /`
- `GET /health/live`
- `GET /health/ready`
- Scalar UI in development via the AppHost service link

## Project documentation

### Core project documents

- [Business requirements](docs/business-requirements.md)
- [Systems analysis](docs/systems-analysis.md)
- [Local development guide](docs/local-development.md)

### Work package 001: Project scaffolding and DevEx

- [Requirements](docs/001-project-scaffolding-and-devex/requirements.md)
- [Technical specification](docs/001-project-scaffolding-and-devex/technical-specification.md)
- [Delivery plan](docs/001-project-scaffolding-and-devex/delivery-plan.md)

### Work package 002: Environment and auth foundation

- [Requirements](docs/002-environment-and-auth-foundation/requirements.md)
- [Technical specification](docs/002-environment-and-auth-foundation/technical-specification.md)
- [Delivery plan](docs/002-environment-and-auth-foundation/delivery-plan.md)

### Work package 003: Authentication and authorisation

- [Requirements](docs/003-authentication-and-authorisation/requirements.md)
- [Technical specification](docs/003-authentication-and-authorisation/technical-specification.md)
- [Delivery plan](docs/003-authentication-and-authorisation/plans/001-delivery-plan.md)

### Work package 004: UI update and refactor

- [Requirements](docs/004-ui-update-and-refactor/requirements.md)
- [Technical specification](docs/004-ui-update-and-refactor/technical-specification.md)
- [Delivery plan](docs/004-ui-update-and-refactor/plans/001-delivery-plan.md)

### Work package 005: Refactor AppHost

- [Requirements](docs/005-refactor-app-host/requirements.md)
- [Technical specification](docs/005-refactor-app-host/technical-specification.md)
- [Delivery plan](docs/005-refactor-app-host/plans/001-delivery-plan.md)

### Work package 006: IG login

- [Requirements](docs/006-ig-login/requirements.md)
- [Technical specification](docs/006-ig-login/technical-specification.md)
- [Delivery plan](docs/006-ig-login/plans/001-delivery-plan.md)

### Domain reference

- [Day trading with IG APIs](docs/ig-day-trading-with-ig-apis.md)
