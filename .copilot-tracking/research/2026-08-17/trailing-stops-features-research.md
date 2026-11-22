<!-- markdownlint-disable-file -->
# Task Research: Trailing Stops Features

## Status

Planning-ready, subject to the decisions in the planning log.

## User Requirement

* Create an implementation plan for trailing-stops features from the supplied IG account-preferences research.

## Consolidated Findings

* IG `GET /accounts/preferences` and `PUT /accounts/preferences` use Version 1 and expose only the account-level `trailingStopsEnabled` Boolean. IG remains authoritative for the live value.
* A dedicated Operator-only Account Preferences feature is the correct owner. Account Details displays persisted retrieval history and remains unchanged.
* Use application-owned query and command contracts, a narrow `IAccountPreferencesGateway`, an IG adapter using the current v2 session pattern, thin API routes, and a Blazor Operator page.
* Append each strictly validated provider observation to typed SQL history. Failed or indeterminate operations must not write a Boolean observation. An uncertain PUT requires fresh GET reconciliation, not a blind write retry.
* New feature terminology is `Test` and `Live`. Legacy persisted `Demo` provider keys require a deliberate compatibility strategy.
* Test is the only supported delivery status. Updating a preference does not authorize real trading or monetary exposure. Live is deferred.

## Evidence

* `.copilot-tracking/research/2026-06-06/ig-account-preferences-web-ui-integration-research.md` - provider contract, selected architecture, error handling, and boundaries.
* `.copilot-tracking/research/subagents/2026-08-17/trailing-stops-feature-plan-context-research.md` - verified repository paths, migration/retention precedents, and validation commands.
* `src/TNC.Trading.Platform.Application/Features/AccountDetails/RefreshAccountDetailsHandler.cs:6-89` - provider-backed command and persisted-history separation.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgAccountDetailsGateway.cs:13-103` - session replay and redacted IG boundary pattern.
* `src/TNC.Trading.Platform.Api/Features/Platform/AccountDetailsEndpointMapping.cs:10-36` - normalized upstream Problem Details mapping.
* `src/TNC.Trading.Platform.Web/Components/Pages/Configuration/Configuration.razor:1-232` - Operator form/state/feedback precedent.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Operations/Retention/OperationalRecordRetentionProcessor.cs:9-105` - SQL and in-memory retention precedent.

## Planning Constraints

* The initial product decision retains trailing-stops preference observations for 90 days. Apply the existing operational retention duration to this dedicated history, and defer archive/export evaluation to follow-on work.
* This plan selects backward-compatible legacy `Demo` to application `Test` mapping as the first delivery path; a data migration remains follow-on work.
* First delivery includes a compact paged history section on the dedicated Account Preferences page, matching the retrospective-analysis requirement without changing Account Details.
* No automated test calls real IG. SQL integration tests require the Docker-backed test environment.