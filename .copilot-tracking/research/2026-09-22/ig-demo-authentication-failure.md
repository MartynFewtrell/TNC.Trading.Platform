---
title: IG Demo Authentication Failure Research
description: Evidence-based diagnosis and repair recommendation for the local IG Demo authentication failure
author: TNC Trading Platform
ms.date: 2026-09-22
ms.topic: troubleshooting
keywords:
  - IG
  - authentication
  - broker environment
  - research
estimated_reading_time: 4
---

## Scope

This research investigates the local IG Demo connection failure reported by the
operator UI. It covers the applied broker-environment data, the executable-state
guard, and the authentication path. It does not change application code,
configuration, credentials, or database data.

## Executive conclusion

The failure is caused by a contradictory local executable-state predicate, not by
IG connectivity, credentials, or the IG endpoint.

The applied `IG Demo` environment has lifecycle `Active` and availability
`Available`. This is the state seeded by the catalog, selected by the catalog
service, and used by test fixtures. However, the executable-state predicate requires
both lifecycle and availability to be `Available`. The predicate therefore rejects
the applied environment before credential retrieval or an outbound IG request.

## Observed operator state

The supplied UI captures show:

* A `Degraded` session state
* The blocked reason: `IG authentication failed: the applied broker environment is unavailable.`
* An `InitialAutomatic` retry phase with no successful automatic retry
* Repeated authentication-attempt and failure events

Read-only runtime data corroborates the UI:

* The applied record is `IG Demo`, provider `Ig`, broker environment `Demo`,
  lifecycle `Active`, availability `Available`, and has no availability reason.
* `RestartRequired` is `0`.
* `AuthRuntimeStates` is `Desktop` / `Demo` and `Degraded`.
* `BlockedReason` and `LatestFailureSummary` match the UI error exactly.
* A successful authentication remains recorded on 2026-09-21, while consecutive
  failures started on 2026-09-22.

## Evidence

### Conflicting state model

* [`AppliedBrokerEnvironmentContext.cs`](../../../src/TNC.Trading.Platform.Application/Configuration/AppliedBrokerEnvironmentContext.cs)
  line 12 defines `IsExecutable` as lifecycle `Available`, availability `Available`,
  and `CanAuthenticate`.
* [`20260921132615_AddBrokerEnvironmentCatalog.cs`](../../../src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/Migrations/20260921132615_AddBrokerEnvironmentCatalog.cs)
  lines 216-220 seed IG Demo with lifecycle `Active` and availability `Available`.
* [`SqlBrokerEnvironmentCatalogService.cs`](../../../src/TNC.Trading.Platform.Infrastructure/Infrastructure/Configuration/SqlServer/SqlBrokerEnvironmentCatalogService.cs)
  lines 70-76 selects an available broker environment and rejects only retired
  lifecycle records.
* [`PlatformSqlServerIntegrationTests.cs`](../../../test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.IntegrationTests/PlatformSqlServerIntegrationTests.cs)
  lines 132-140 and 201-203 use `Active` / `Available` as a usable fixture state.
* [`StaticAppliedBrokerEnvironmentContextResolver.cs`](../../../test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/StaticAppliedBrokerEnvironmentContextResolver.cs)
  lines 8-10 uses the same `Active` / `Available` combination.

### Authentication is blocked before IG is contacted

* [`SqlAppliedBrokerEnvironmentContextResolver.cs`](../../../src/TNC.Trading.Platform.Infrastructure/Infrastructure/Configuration/SqlServer/SqlAppliedBrokerEnvironmentContextResolver.cs)
  lines 9-18 reads the applied selection and supplies its lifecycle and availability
  to the executable predicate.
* [`IgBrokerAuthenticationGateway.cs`](../../../src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgBrokerAuthenticationGateway.cs)
  lines 21-32 rejects a non-executable context and produces the reported error.
* In the same gateway, credential lookup begins only at lines 48-50. Consequently,
  the current failure cannot be remediated by changing credentials, API keys,
  networking, or the IG endpoint.

## Assessment of alternatives

| Option | Assessment | Decision |
| --- | --- | --- |
| Change the predicate to recognise `Active` / `Available` | Aligns the guard with the seeded, selected, and tested catalog model | Recommended |
| Change the seed data to lifecycle `Available` | Contradicts the catalog model and conceals the defective guard | Reject |
| Change IG credentials, API keys, or endpoint | Those dependencies are not reached by the current execution path | Reject |
| Retry without a code change | The same deterministic local guard will reject the applied record | Reject |

## Recommended fix

Change `IsExecutable` so that a broker environment is executable only when all of
the following are true:

1. Its lifecycle is `Active`.
2. Its availability is `Available`.
3. It supports authentication.

Preserve rejection for `Unavailable` and `Retired` environments. Prefer a
single, named capability rule over repeated string comparisons as the model evolves.
This avoids future divergence between the catalog, selection service, and
authentication gateway.

## Regression coverage

Add focused coverage for the repair:

1. Verify that an applied `Active` / `Available` IG Demo environment is executable.
2. Verify that the authentication gateway proceeds beyond the environment guard to
   its credential and HTTP dependencies for that state.
3. Retain coverage that an `Active` / `Unavailable` environment is rejected.
4. Extend the SQL Server integration test to verify that the seeded IG Demo record is
   executable.

The current unavailable-environment coverage is in
[`IgBrokerAuthenticationGatewayTests.cs`](../../../test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/Integrations/Ig/IgBrokerAuthenticationGatewayTests.cs)
lines 107-128; it does not cover the valid `Active` / `Available` state.

## Post-fix validation

After deploying the code fix, trigger one controlled Demo authentication retry.
Confirm that the application attempts `POST /session` or, if the call fails, reports
an IG-originated failure. That outcome confirms the local environment guard no
longer blocks the connection and distinguishes any subsequently exposed upstream
authentication problem.
