# Market-category collection observability plan

This plan implements the diagnosis-first recommendations in the
[failure research](market-category-collection-failures-research-2026-10-03.md)
and [logging research](market-category-collection-logging-research-2026-10-03.md).
The historical provider response and publication exception were not retained;
neither COMMODITIES nor CRYPTOCURRENCY has a confirmed root cause yet.

## Scope and sequence

1. Extend the internal gateway failure contract with a stable, bounded reason
   and safe context (page, row, field, operation, HTTP status where applicable).
   Classify each validation rejection and provider/session failure without
   logging payloads, credentials, response bodies, headers, or URLs. Preserve
   existing retryability, safe persisted codes, and fail-closed behavior.
2. At the coordinator, emit one structured final gateway failure per saved
   attempt after retries, with environment, day, slot, category, lease fence
   and owner, full-run ID, safe code, failure category, reason, and trace ID.
   Suppress expected schedule/lease exits. Enrich the existing publication
   exception event with the same join keys, collection ID and revision.
   Collection IDs cannot correlate failures before publication.
3. Remove the two unconditional Information tick summaries from the API
   collector; retain exception errors and fenced-stage warnings. Initial
   delivery left global filters unchanged. Following the operator's observed
   high-volume successful EF Core `Executed DbCommand` messages, set the API
   and Web default minimum to Warning in base and development settings. This
   suppresses routine Information events from these services (including
   successful EF commands) while retaining Warning/Error diagnostics.
   AppHost startup/resource logs retain their existing level.
4. Add deterministic gateway and coordinator tests for representative
   metadata, page, row, session, retry, publication and idle-log cases.
   Build and run the affected unit tests without triggering a live IG run.
5. Update the implementation wiki with event fields, correlation workflow,
   privacy boundaries, and the distinction between listing and detail
   failures. On the next naturally due failure, compare the safe event with
   the saved category attempt before deciding whether provider validation or
   publication needs a behavioral fix.

## Acceptance checks

- Final gateway failures have a distinct `Stage=Gateway` and bounded
  `Reason`; publication exceptions have `Stage=Publication` and retain their
  stack. Saved safe codes and last-good snapshots are unchanged.
- A retried gateway failure produces only one Warning on exhaustion; an
  unauthorized/rejected result is distinguishable from publication failure.
- Idle collector ticks emit **zero** of the previous two routine Information
  records (formerly approximately 240/hour), even if their logger's minimum
  is independently lowered. Error and Warning logging stays enabled.
- At the configured Warning minimum for the API and Web, successful EF Core
  `Executed DbCommand` Information events are not exported; failed command
  Error events and final category failure warnings remain visible.
- Sanitized fixtures demonstrate that diagnostics contain no provider row
  values, session tokens or response bodies.

## Operator follow-up

The operator identified successful EF Core command logs as a high-volume
source; the Warning minimum now filters these without suppressing failures.
Sample exported logs by `CategoryName` and severity for at least ten idle
minutes and one naturally due failed collection. Join the final event to
`InstrumentCollectionCategoryAttempts` on environment/day/slot/category and
to the full run ID when present. Only add further source-specific filters
after measuring their contribution. Do not retrigger live IG collection to
manufacture diagnostic data.

The operator's ten-minute observation found an EF Core model warning for the
unrelated `IgProofData.Balance` column, not a category collection failure.
The proof-data model now explicitly uses `decimal(21,5)` and an additive
migration widens the existing `decimal(18,2)` SQL column without reducing its
16-digit integer range. SQL-backed upgrade and precision tests verify this
separate warning is resolved without changing collection rules.

On 3 October the persisted COMMODITIES and CRYPTOCURRENCY attempts failed at
09:19 UTC, before the observed AppHost started at 10:55 UTC. The running
dashboard therefore cannot contain their final-attempt diagnostics. These
failures have repeated on earlier trading days, but their precise causes
cannot be recovered from the safe codes alone. The status read now includes
each attempt's saved update time and the page distinguishes this from the
last successful snapshot and category catalogue refresh. Leave the collector
running through the next naturally due attempt; if it fails, inspect the
contemporaneous API Warning/Error by category and stage before proposing a
provider or publication fix. Do not manufacture another IG run.
