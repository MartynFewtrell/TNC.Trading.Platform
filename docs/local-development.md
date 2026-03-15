# Local development guide

This document provides project-wide local development commands for build, run, and validation.

## Prerequisites

- .NET SDK installed (pinned by `global.json`)
- Docker Desktop installed and running

## Build

From the repository root:

```powershell
dotnet build
```

## Run

Start the local orchestrated baseline from the repository root:

```powershell
dotnet run --project src/TNC.Trading.Platform.AppHost/TNC.Trading.Platform.AppHost.csproj
```

Optional alternative (requires Aspire CLI):

```powershell
aspire run
```

## Validate

1. Run automated tests:

```powershell
dotnet test
```

2. Verify service health from the API service endpoint exposed by the Aspire dashboard:
   - `GET /health/live`
   - `GET /health/ready`

## Logs and troubleshooting

- Review console logs from AppHost and service processes.
- If readiness fails (`503`), inspect service startup logs for dependency or configuration issues.
- Do not place secrets in tracked files or log secret values.
