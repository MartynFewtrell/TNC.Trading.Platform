---
description: 'Offline Phase 1 implementation evidence and unresolved authorization gates for IG navigation migration.'
ms.date: 2026-11-08
---

# IG navigation backend migration execution log

## Gate decision

Phase 1 remains incomplete. The initial implementation covered offline
transport and internal investigation preparation. At that time, actual broker
HTTP attempts were **0**, no broker authorization had been supplied, and no
AppHost, collector, database maintenance or rollout had been started. The IG
sample repository was not modified.

The [delivery plan](ig-navigation-backend-migration-plan.md) remains authoritative.
Do not advance provider rollout or declare contract semantics verified from
synthetic tests. Phases 2-7 and the remaining Phase 1 evidence decisions are
pending; production discovery, listing pointers and hydration are unchanged.

## Aspire runtime observation (2026-11-14)

The user authorized starting the local development/test AppHost and any
normally due Demo collector requests. Aspire started the AppHost; SQL Server,
Keycloak and API reached healthy/running states. The AppHost was stopped after
inspection.

The API ran the existing scheduled paginated collector, not the navigation
probe. Its sanitized logs recorded one listing failure for `COMMODITIES`:
page zero initially returned 15 rows of 290, then the restart returned 9 rows
when 15 were expected. The gateway reported three HTTP calls and
`PageRowCountMismatch`. The persisted full-run stages show Categories
Succeeded, Listings Failed with `ListingStageIncomplete`, and Details
Succeeded. The database recorded eight request-allowance reservations after
AppHost startup; reservation count is not treated as an exact HTTP-send count.

No navigation root/child evidence was captured. The internal probe still has
no API, UI or Aspire resource-command invocation path, and no separate probe
approval reference or root scope had been provided during that earlier
observation.

## Authorized navigation probe (2026-11-14)

The user approved a confirmed, non-public Aspire resource command, supplied
authorization reference `testprobe`, and selected ordinal-first root choice
for an availability-only probe. The AppHost started with SQL Server, Keycloak,
and API healthy. The Aspire command returned `EndpointUnsupported` after
**2 HTTP attempts**. The root response provided no shape, so the child request
was not sent. No node IDs, market details, raw provider body, tokens or
credentials were retained.

Read-only SQL checks showed a completed diagnostic full run with outcome
`Idle`, no safe failure code, and all three full-run stages still `Pending`.
In the run's five-second interval, the rate-reservation table recorded two
reservations for each shared scope (`Account` and `App`), consistent with the
two HTTP sends. These reservation rows are not themselves an exact send
counter; the Aspire command result reports the two attempts. No listing
publication was attempted. The AppHost was stopped after inspection.

The outcome classifies the root request as unsupported under the gateway's
safe failure mapping. The HTTP status code is not exposed by the sanitized
command result, so no more specific status can be claimed. With no root shape,
absent/null/empty/mixed response semantics and exact business-category root
mappings remain undecided. Keep provider rollout blocked and discovery on the
existing paginated path.

## Implemented scope

* Application owns `ProbeMarketNavigationHandler`, its request/response and
  validator, the root/node gateway port and a sequential probe attempt scope.
* Infrastructure supplies candidate v1 root/node requests with escaped opaque
  IDs, protected credentials, v2 sessions and at most one reauthentication per
  fetch. Session creation for each fetch is deliberate for this bounded probe,
  not a calibrated production request-cost model.
* Every send passes existing shared pacing, applied-context revalidation and
  durable allowance reservation. Reauthentication shares the attempt ceiling.
  Failed reservations are not reported as HTTP sends. Authority loss fails
  the probe; caller cancellation propagates without retry.
* The internal entry point requires an explicit authorization reference,
  matching Demo profile/environment and a pre-admitted manual diagnostic lease
  with matching owner/fence. The authorization reference is correlation data,
  not a security credential: the caller must independently enforce approval,
  own admission and always complete/release its diagnostic lease.
* Session account identity must match the approved account-context SHA-256
  hash of the trimmed, invariant-uppercase account ID. Neither account ID nor
  session material is returned. No root mapping is inferred by display name.
* Root and child shapes preserve absent/null/array distinctions. Markets and
  nodes are independently counted/observed. Market objects are shape evidence
  only, not validated stable instrument identity or coverage evidence.
* The caller can select an exact root ID; otherwise Application chooses the
  ordinal-first root for the availability probe only. This is not business
  category mapping policy or automatic selection for collection.
* Registered probe HTTP clients have default retries and loggers removed.
  Shared OpenTelemetry HTTP tracing excludes navigation paths so node URLs
  are not exported. Metrics introduce no node or EPIC labels.
* A Development-only internal API route is invoked through a confirmed
  Aspire HTTP resource command. AppHost supplies a random per-process
  capability key; ordinary startup and other environments cannot invoke the
  route. The command accepts an approval reference and optional exact node ID.
  Its response is sanitized, and it does not add a UI, hosted invocation,
  configuration editor, staging schema or publication path. Node identities
  in internal shape results remain restricted evidence, not general logs or
  metric labels.

## Investigation limits

Defaults: 10 HTTP attempts, 65,536 response bytes, 120 seconds total capture.
Validator bounds: 3-10 attempts, 256-1,048,576 bytes, 1-300 seconds capture.
Configured shared pacing and the existing lease window remain authoritative.
The registered client has a 15-second HTTP send timeout; linked capture and
caller cancellation also bound streamed body reads and pacing waits.
Error parsing uses the existing 16-KiB safe-code parser. JSON depth is 16.
These are probe safety limits, not production crawl calibration.

The approved run used the applied Demo profile and validated the active
account context without retaining the account ID or hash. No root shape,
mapping revision or coverage-policy revision was selected. Do not retry or
change endpoints without a new investigation decision and bounded approval.

## Changed files

Paths are relative to the repository root.

* Application: eight files under
  [MarketNavigation](../../../src/TNC.Trading.Platform.Application/Features/MarketNavigation/ProbeMarketNavigationHandler.cs#L6)
  defining the handler, validator, request, response, port, fetch result, shape
  and attempt scope.
* Infrastructure:
  [gateway](../../../src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgMarketNavigationGateway.cs#L14),
  [parser](../../../src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgMarketNavigationShapeParser.cs#L6)
  and [composition](../../../src/TNC.Trading.Platform.Infrastructure/Infrastructure/DependencyInjection/PlatformInfrastructureServiceCollectionExtensions.cs).
* Probe orchestration:
  [admission handler](../../../src/TNC.Trading.Platform.Application/Features/MarketNavigation/RunMarketNavigationProbeHandler.cs),
  [internal endpoint](../../../src/TNC.Trading.Platform.Api/Features/Platform/MarketNavigationProbeCommandEndpointMapping.cs)
  and [Aspire command registration](../../../src/TNC.Trading.Platform.AppHost/AppHostProjectRegistration.cs).
* Shared [tracing defaults](../../../src/TNC.Trading.Platform.ServiceDefaults/Extensions.cs#L127).
* Existing Infrastructure unit project:
  [probe tests](../../../test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/Integrations/Ig/IgMarketNavigationGatewayTests.cs#L13),
  [parser tests](../../../test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/Integrations/Ig/IgMarketNavigationShapeParserTests.cs#L7)
  and [pipeline regression](../../../test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/IgCollectionHttpPipelineTests.cs#L25).
* API and AppHost command authorization/registration tests.
* Delivery plan, this execution log and
  [testing guidance](../../../docs/wiki/testing-and-quality.md),
  [runtime guidance](../../../docs/wiki/runtime-behavior.md).

## Validation

All tests use in-process fake HTTP, credentials, contexts and budgets. No SQL,
Docker or IG dependency is used. Existing deterministic pacing clock is reused.

From the repository root:

```powershell
dotnet test test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/TNC.Trading.Platform.Infrastructure.UnitTests.csproj --filter 'FullyQualifiedName~IgMarketNavigation|FullyQualifiedName~IgCollectionHttpPipelineTests|FullyQualifiedName~IgProviderRequestThrottleTests' --verbosity minimal --no-restore
dotnet build src/TNC.Trading.Platform.ServiceDefaults/TNC.Trading.Platform.ServiceDefaults.csproj --no-restore --verbosity minimal
```

Initial validation passed 36 focused tests and the shared-defaults build.
The 2026-11-14 rerun, including `IgProviderRequestThrottleTests`, passed all
41 selected tests, verifying per-send reservations and attempt limits,
reauthentication bounds, safe failure classifications, malformed/oversized
response rejection, and shared pacing behavior. The initial parser check
passed 11 tests. A subsequent test edit introduced a duplicate method
declaration; compilation caught it, it was removed, and the same check passed.
For the bounded command path, the AppHost build passed, four API command-key
tests passed, three AppHost registration tests passed, and 35
`IgMarketNavigation` Infrastructure tests passed. Existing NU1902/NU1903
cryptography dependency warnings remain unresolved; no package versions
changed in this increment.

Not run: SQL compatibility/publication tests, solution-wide delivery gates,
closed-box restart tests, shadow collection, Demo cutover or rollback
rehearsal. Trace exclusion was compiled, not verified against a running
exporter. Full phase acceptance is not claimed.

## Wiki decisions and pending evidence

Testing and runtime guidance now describe the Development-only Aspire command
and its diagnostic lease boundary. The root attempt produced no shape, so
response semantics and category mappings remain pending. Production
pagination, listing pointers, and later-phase wiki decisions remain unchanged.

The next evidence gate requires a documented resolution for the unsupported
root endpoint, followed by a separately authorized bounded attempt if a
supported route is identified. Capture a root and selected-child shape before
deciding absent/null/empty/mixed semantics or initial exact-ID category
mappings. A failed or exhausted probe remains incomplete; no probe outcome
can publish a listing.