# TNC.Trading.Platform

TNC.Trading.Platform is a .NET 10 trading platform under active development for safe, iterative delivery of algorithmic day-trading capabilities against IG APIs.

## Current status

The repository currently provides the foundational platform scaffold and developer experience baseline.

- .NET Aspire AppHost for local orchestration
- Minimal API service with OpenAPI support and Scalar UI in development
- Shared service defaults for common hosting concerns
- Liveness and readiness health endpoints
- Aspire-based integration smoke tests for the API baseline

The following capabilities are not implemented yet:

- IG authentication and session management
- market discovery and pricing integration
- order placement and trade lifecycle management
- strategy execution and risk controls

## Solution structure

- [`src/TNC.Trading.Platform.AppHost`](src/TNC.Trading.Platform.AppHost/) - local orchestration entry point for running the current platform baseline
- [`src/TNC.Trading.Platform.Api`](src/TNC.Trading.Platform.Api/) - minimal HTTP API with OpenAPI, Scalar UI, and health endpoints
- [`src/TNC.Trading.Platform.Application`](src/TNC.Trading.Platform.Application/) - application-layer features, handlers, models, and orchestration services
- [`src/TNC.Trading.Platform.Infrastructure`](src/TNC.Trading.Platform.Infrastructure/) - persistence, notifications, platform stores, and infrastructure wiring
- [`src/TNC.Trading.Platform.ServiceDefaults`](src/TNC.Trading.Platform.ServiceDefaults/) - shared defaults for service configuration and hosting
- [`src/TNC.Trading.Platform.Web`](src/TNC.Trading.Platform.Web/) - Blazor-based operator UI for configuration, status, and operational events
- [`test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests`](test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/) - API unit tests for validation and endpoint-facing contract behavior
- [`test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests`](test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.IntegrationTests/) - closed-box integration tests that validate API health through the AppHost
- [`test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests`](test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/) - application-layer unit tests for scheduling, retry, and auth coordination logic
- [`test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests`](test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/) - infrastructure unit tests for storage, protection, notifications, and retention
- [`test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests`](test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.FunctionalTests/) - functional tests for the Blazor operator experience
- [`test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests`](test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.E2ETests/) - end-to-end tests for browser-level validation

The repository now follows the current guideline that non-generated C# code keeps one top-level type per file with a matching file name, which improves Solution Explorer navigation and code review clarity.

## Getting started

For local prerequisites, build steps, run commands, and validation guidance, see the [Local development guide](docs/local-development.md).

The current local baseline is started with the AppHost and exposes:

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

### Domain reference

- [Day trading with IG APIs](docs/ig-day-trading-with-ig-apis.md)
