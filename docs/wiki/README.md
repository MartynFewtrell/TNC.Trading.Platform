# Documentation index

This documentation set describes the application as it exists today. It complements the business, systems-analysis, and work-package documents by focusing on the implemented solution, runtime behavior, and operator experience.

## Start here

If you are new to the repository, read these documents in order:

1. [Application overview](application-overview.md)
2. [Architecture](architecture.md)
3. [Operator guide](operator-guide.md)
4. [API reference](api-reference.md)
5. [Runtime behavior](runtime-behavior.md)
6. [Testing and quality](testing-and-quality.md)
7. [Local development guide](local-development.md)

## Current implementation summary

At this stage the platform provides:

- an Aspire AppHost that starts the API and Blazor operator UI
- an Aspire-managed Keycloak container for local operator sign-in when infrastructure containers are enabled
- a Minimal API backend with protected status, configuration, event-history, manual-retry, and admin-auth endpoints
- a Blazor Server operator UI with a public landing page plus protected status, configuration, and authentication-administration pages
- sign-in, sign-out, and access-denied flows with shared role enforcement across the UI and API
- delegated bearer-token propagation from the Blazor host to the protected API
- SQL-backed operator-managed configuration when a SQL connection is available
- an in-memory fallback for local runs without infrastructure containers
- protected storage for IG credentials using ASP.NET Core Data Protection
- auth-state supervision, retry scheduling, notification recording, and operational event history
- automated test-provider support for integration, functional, and end-to-end auth coverage without Docker
- health checks, OpenTelemetry wiring, and requirement-driven tests

The platform does not yet execute real trading workflows, market-data ingestion, or live IG integration. The current implementation is the environment, configuration, and auth foundation that later work packages will build on.

## Documentation map

| Document | Purpose |
| --- | --- |
| [Application overview](application-overview.md) | High-level explanation of what the solution does, what is implemented, and what is not implemented yet. |
| [Architecture](architecture.md) | Solution structure, component responsibilities, runtime topology, and persistence overview. |
| [Operator guide](operator-guide.md) | How the current Blazor UI works and what operators can do from each page. |
| [API reference](api-reference.md) | Reference for the current HTTP endpoints, request payloads, responses, and error cases. |
| [Runtime behavior](runtime-behavior.md) | Startup sequence, trading-schedule rules, auth-state transitions, retry policy, and notification behavior. |
| [Testing and quality](testing-and-quality.md) | Test-project layout, what each test suite validates, and how quality is checked. |
| [Local development guide](local-development.md) | Build, run, validate, and troubleshoot the current application locally. |

## Existing project and planning documents

These existing documents remain the source of truth for broader product intent and delivered work-package requirements:

- [Business requirements](../business-requirements.md)
- [Systems analysis](../systems-analysis.md)
- [Work package 001: Project scaffolding and DevEx](../001-project-scaffolding-and-devex/technical-specification.md)
- [Work package 002: Environment and auth foundation](../002-environment-and-auth-foundation/technical-specification.md)
- [IG domain reference](ig-day-trading-with-ig-apis.md)

## Suggested reading by role

### Developer

- [Application overview](application-overview.md)
- [Architecture](architecture.md)
- [Runtime behavior](runtime-behavior.md)
- [Local development guide](local-development.md)
- [Testing and quality](testing-and-quality.md)

### Operator or reviewer

- [Application overview](application-overview.md)
- [Operator guide](operator-guide.md)
- [Runtime behavior](runtime-behavior.md)
- [API reference](api-reference.md)

### Maintainer planning future work

- [Application overview](application-overview.md)
- [Architecture](architecture.md)
- [Runtime behavior](runtime-behavior.md)
- [Systems analysis](../systems-analysis.md)
- [Work package 002 technical specification](../002-environment-and-auth-foundation/technical-specification.md)
