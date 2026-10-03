# Research: diagnostic logging for market-category collection

This report builds on the [3 October collection failure research](market-category-collection-failures-research-2026-10-03.md).
It recommends changes to API logging, not changes to IG validation or collection
behavior. The original 09:19 UTC telemetry was not available in the later
Aspire session; the exact provider response and crypto failure branch remain
unknown. No live collection was retriggered for this report.

## Findings

| Evidence | Diagnostic or volume consequence |
| --- | --- |
| The gateway returns `InvalidCollection`/`IncompleteCollection` for multiple metadata, page, and row checks without a specific reason ([gateway checks](../src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgMarketCategoryInstrumentsGateway.cs#L82-L205), [row validation](../src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgMarketCategoryInstrumentsGateway.cs#L399-L508)). Its exception handlers also turn JSON, HTTP, timeout, and provider conditions into failure values ([gateway catches](../src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgMarketCategoryInstrumentsGateway.cs#L210-L242)). | `COMMODITIES`' persisted `InvalidResponse` does not identify the failing check, page, row, or field. Logging only exceptions would miss ordinary validation returns. |
| The coordinator maps both invalid and incomplete gateway results to `InvalidResponse`, and unmapped categories, including `Unauthorized` and `Rejected`, to `UnexpectedFailure` ([mapping](../src/TNC.Trading.Platform.Application/Features/MarketCategoryInstruments/MarketCategoryInstrumentCycleCoordinator.cs#L842-L854)). A publication exception also becomes `UnexpectedFailure`, but its error log has no structured category, run, slot, or collection ID ([publication catch](../src/TNC.Trading.Platform.Application/Features/MarketCategoryInstruments/MarketCategoryInstrumentCycleCoordinator.cs#L648-L703)). | `CRYPTOCURRENCY` cannot be classified as provider/session versus database/publication failure from the saved code alone; even a retained exception is difficult to join to the saved attempt. |
| The collector sleeps for **at most 30 seconds** between checks ([collector loop](../src/TNC.Trading.Platform.Api/Hosting/MarketCategoryInstrumentCollector.cs#L15-L30), [delay](../src/TNC.Trading.Platform.Api/Hosting/MarketCategoryInstrumentCollector.cs#L137-L153)) and writes **two Information records after every tick**, including `NotAttempted` details ([tick logs](../src/TNC.Trading.Platform.Api/Hosting/MarketCategoryInstrumentCollector.cs#L117-L135)). | During a continuously idle run, approximately 120 checks/hour produce **240 routine records/hour** or **5,760/day**, before other sources. Actual rate varies with work duration and schedule. These records do not explain an individual failed category. |
| API `Default` logging is `Information`, with only `Microsoft.AspNetCore` reduced to `Warning` in both [base settings](../src/TNC.Trading.Platform.Api/appsettings.json#L39-L45) and [development settings](../src/TNC.Trading.Platform.Api/appsettings.Development.json#L2-L8). [Service defaults](../src/TNC.Trading.Platform.ServiceDefaults/Extensions.cs#L97-L140) export logs through OpenTelemetry when OTLP is configured and enable HTTP/server tracing; only health **traces** are filtered. | Other enabled Information sources can also reach the sink; the source mix and relative volume need measurement before further filtering. Trace filtering is not a log filter. Do not claim that health requests or SQL/HTTP client logs are the dominant source without sampling log categories first. |
| Other normal-path Information logs include [recording an auth operational event](../src/TNC.Trading.Platform.Application/Features/ReconcilePlatformAuthentication/PlatformAuthenticationReconcilerSideEffects.cs#L57-L79) and [startup initialization](../src/TNC.Trading.Platform.Infrastructure/Infrastructure/Startup/PlatformStartupInitializer.cs#L35-L54). Auth and account-preference supervisors run on one-second delays ([auth delay](../src/TNC.Trading.Platform.Api/Hosting/PlatformAuthenticationSupervisor.cs#L75-L79), [preferences loop](../src/TNC.Trading.Platform.Api/Hosting/AccountPreferencesReconciliationSupervisor.cs#L16-L31)), but only log exceptions at this layer. | Do not attribute one-second *log* volume to these supervisors without evidence; review downstream categories if a sink sample implicates them. Preserve operational/audit records independently of diagnostic log-level changes. |

## Recommended diagnostic events

1. **Correlate at the collection boundary.** Carry a stable attempt identity
   (environment, trading day, slot, category code, lease owner/fence and, when
   present, full-run ID) into gateway and publication diagnostics. Add a
   `BeginScope` or explicit structured properties at the coordinator boundary;
   do not rely solely on the HTTP request scope in
   [API middleware](../src/TNC.Trading.Platform.Api/Program.cs#L69-L94):
   scheduled work has no incoming request. Include `Activity.Current` trace ID
   if available; use the saved attempt key as the fallback join key. A
   collection ID is allocated only **after** a complete gateway result
   ([publication boundary](../src/TNC.Trading.Platform.Application/Features/MarketCategoryInstruments/MarketCategoryInstrumentCycleCoordinator.cs#L648-L676)),
   so it cannot identify failed gateway calls unless created earlier.
2. **Emit a bounded reason for gateway failure, not a page-by-page success
   narrative.** Return a typed internal diagnostic reason alongside the safe
   failure category (or emit one structured event at the rejection point).
   Distinguish `MissingMetadata`, `InvalidPageNumber`, `PageSizeMismatch`,
   `TotalPagesMismatch`, `TotalResultsMismatch`, `PageRowCountMismatch`,
   `ResultLimitExceeded`, `DuplicateEpic`, `DuplicateName`, `InvalidField`
   (field name only), `InvalidExpiry`, `JsonMalformed`, `HttpStatus`,
   `SessionUnauthorized`, `Timeout`, and `RequestBudgetOrScheduleClosed`.
   Record requested page, expected/actual numeric metadata, zero-based row
   index, provider operation (`session`/`instruments`), safe HTTP status code
   and retryability **only where relevant**. Make reason selection
   deterministic in the grouped checks and validate it in tests. Do not log
   entire instruments, response bodies, URLs with query strings, credentials,
   CST/security tokens, request headers, or untrusted field values.
3. **Record the final failed attempt once, at the coordinator boundary.**
   When [CompleteCategoryAttemptAsync](../src/TNC.Trading.Platform.Application/Features/MarketCategoryInstruments/MarketCategoryInstrumentCycleCoordinator.cs#L704-L731)
   finishes an attempt, emit one structured `Warning` for a non-retryable
   gateway/validation outcome with `Stage=Gateway`, safe code, actual failure
   category and internal reason. Transient failures that will retry can be
   `Debug`; emit the final retry-exhausted failure at `Warning` instead.
   Expected schedule close/lease loss should be `Debug` or recorded only in
   existing attempt state unless an invariant is violated. Avoid logging the
   same failed attempt in both gateway and coordinator at Warning/Error.
   Keep the UI/persisted safe status separate from internal diagnostics.
4. **Enrich the existing publication error**, retaining its exception stack
   once at `Error` but adding `Stage=Publication`, category, environment,
   trading day/slot, full-run ID, collection ID, catalogue revision and
   attempt identity. The [writer's validation and transaction](../src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfMarketCategoryInstrumentSnapshotStore.cs#L37-L115)
   can reject provenance/lease/revision or SQL writes; include a bounded
   publication phase/reason where known. For a database exception, preserve
   exception type and SQL error number for operators, not SQL parameters or
   provider payloads. Do not infer publication failure just because the UI
   shows `UnexpectedFailure`. A publication failure must continue to roll
   back and preserve last-good data.
5. **Keep the failure signal actionable.** Use stable property names and
   reason codes in tests and dashboards; alert on final category failures
   grouped by stage/reason rather than counting periodic ticks. Retain the
   persisted attempt/full-run history as the low-volume record of success and
   use metrics/traces for throughput and latency. Keep high-cardinality
   category/attempt identifiers in logs or traces, not metric labels.

## Recommended volume reduction

1. Remove or demote **both** unconditional collector tick `Information` logs
   to `Debug` (prefer remove: saved attempts and full-run stage state already
   capture actual outcomes). Keep unexpected tick exceptions at `Error` and
   failed stage/lease warnings with run context; do not write an additional
   generic tick summary for the same failure. This directly eliminates the
   estimated 240/hour idle collector messages without hiding failures.
2. Sample exported logs by `CategoryName`/level over a representative idle
   period and one failed collection before touching framework filters. Keep
   `Warning` and above for the API and dependencies. If Information entries
   from `System.Net.Http.HttpClient`, `Microsoft.EntityFrameworkCore.Database.Command`,
   or other identified categories dominate, set **specific category** minima
   to `Warning` in base/development logging settings rather than globally
   suppressing warnings or disabling OpenTelemetry. Check that HTTP status,
   SQL error, and exception diagnostics remain visible after filtering.
3. Review normal-path auth/event and startup Information entries only if the
   sample shows they materially contribute; do not remove the persisted
   operational events or audit trail. Health-check tracing can be tuned
   separately from logs in service defaults.

## Verification and decision point

- With the collector idle for at least 10 minutes, count logs by
  `CategoryName` and severity before/after: the two collector Information
  messages per tick should drop to **zero** at normal settings, while
  `Warning`/`Error` entries still export to Aspire/OTLP.
- With sanitized fixtures for each invalid metadata, page and row case,
  assert **one** final failed-attempt event with stage, reason, category and
  saved-attempt correlation; no raw IG payload or credentials. Test retry
  exhaustion, unauthorized/rejected provider responses, cancellation/lease
  loss, and a publication exception independently.
- On the next naturally due failure, join the structured event to
  `InstrumentCollectionCategoryAttempts` by environment/day/slot/category
  and to the full run when present. For commodities, capture the exact
  rejection reason; for crypto, establish `Gateway` versus `Publication`
  before modifying validation, status mapping or storage constraints.
  Verify a subsequent successful collection still publishes a complete
  listing and keeps the last-good snapshot on failure.

**Confidence:** high that unconditional collector tick logging explains a
substantial amount of idle API noise and that existing safe codes lose
diagnostic detail; unquantified for other categories, and insufficient
historical telemetry to identify either failure's precise cause.
