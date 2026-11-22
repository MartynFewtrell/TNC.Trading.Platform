# Configuration

## Purpose
Describe all configuration inputs the app uses, where they are stored, and how they affect runtime behavior.

## Key points (TL;DR)
- Main runtime config is `SampleWPFTrader/App.config`.
- The `IgWebApiConnection` section provides `environment`, `username`, `password`, `apikey`.
- `environment` influences the REST base URL: `https://{env}api.ig.com`.
- There is no environment variable support or secret store integration in this repo.

## Details
## `IgWebApiConnection` settings
Location: `SampleWPFTrader/App.config`.

Keys:
- `environment`: expected values include `demo` and `live`.
- `username`: IG login identifier.
- `password`: IG login password (optionally encrypted client-side during login).
- `apikey`: IG API key.

## Base URL selection logic
`IgRestService` builds the base URL from `environment`:
- if `live`, it uses `https://api.ig.com`
- otherwise, it uses `https://{environment}-api.ig.com` (e.g., `demo-api.ig.com`)

## Assembly binding redirects
`SampleWPFTrader/App.config` includes binding redirects (notably for `System.Net.Http.*`) to align .NET Framework assembly versions.

## Evidence (repo references)
- `SampleWPFTrader/App.config` (lines 44–50): `IgWebApiConnection` keys.
- `IGWebApiClient/IGRestService.cs` (lines 33–37): environment-to-base-URL logic.
- `SampleWPFTrader/App.config` (lines 11–33): assembly binding redirects.

## Open questions / assumptions (if any)
- Assumption: Users edit `App.config` locally to set credentials; no alternate config sources are supported.
  - Missing evidence: No code reads environment variables or user profiles for these settings.
  - Risk/impact: A production-quality reimplementation should add secure configuration and secret handling.