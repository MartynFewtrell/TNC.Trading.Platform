# Assumptions & gaps

## Purpose
Explicitly list what cannot be proven from the repository and what should be verified when reimplementing.

## Key points (TL;DR)
- External API contracts (IG REST + streaming schemas) are not included; DTOs and field names are the only source of truth.
- Error handling and resiliency (retries, reconnection) are minimal.
- No automated tests or CI pipeline exists to lock behavior.

## Details
## External API contract uncertainty
- REST DTOs imply expected payloads, but there are no captured JSON samples or API specs in-repo.
- Lightstreamer field names and item formats are hard-coded; schema evolution is unknown.

## Security gaps (sample-quality)
- Credentials and API key are stored in plaintext `App.config`.
- There is no token storage hardening, no secure secret store integration.

## Runtime resilience gaps
- REST uses synchronous `.Result` calls inside async wrappers.
- Lightstreamer `IConnectionListener` callbacks are empty; no reconnect/backoff.

## Versioning uncertainty
- Some endpoints are called with `VERSION=1` and others with `VERSION=2`; the rationale is not explained in-repo.

## What to verify next (concrete tasks)
- Capture real REST request/response pairs for each endpoint used by the UI.
- Capture Lightstreamer subscriptions and update payload examples for:
  - `ACCOUNT:<accountId>`
  - `TRADE:<accountId>` (especially JSON payload for `OPU/WOU/CONFIRMS`)
  - `L1:<epic>`
  - `CHART:<epic>:1MINUTE`
- Confirm which environments are supported beyond `demo` and `live`.

## Evidence (repo references)
- `SampleWPFTrader/App.config` (lines 44–50): plaintext credentials configuration.
- `IGWebApiClient/IGStreamingApi.cs` (lines 610–772): hard-coded item name patterns and field lists.
- `IGWebApiClient/IGRestApiClient.cs` (lines 105–119, 155–186, 420–631): endpoint list and version usage.
- `IGWebApiClient/IGRestService.cs` (lines 154–186): synchronous `.Result` calls.

## Open questions / assumptions (if any)
- Assumption: This repo’s behavior matches IG’s expected client usage patterns for the time it was written.
  - Missing evidence: Repo is marked deprecated and may be out of date.
  - Risk/impact: A clean-room reimplementation may need to modernize API usage and dependencies.