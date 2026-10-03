# Research: selected market-category collection failures

This note investigates the `COMMODITIES` and `CRYPTOCURRENCY` failures visible
to a local operator on the Market categories page on 3 October 2026. It
recommends a diagnosis-first fix; it does not change collector behavior or
trigger another live IG collection.

## Observed behavior and evidence

- The supplied screen shows `COMMODITIES` as `Failed — InvalidResponse`, with
  `Never collected` and `NeverCollected` detail coverage. `CRYPTOCURRENCY` shows
  `Failed — UnexpectedFailure`, but retains a successful listing dated
  27 September and `Incomplete` detail coverage.
- A read-only query of the existing local `platformdb` table
  `InstrumentCollectionCategoryAttempts` confirmed one failed attempt per
  category on each of 28, 29, and 30 September and 3 October 2026. On
  3 October, `COMMODITIES` recorded `InvalidResponse` at
  `09:19:02.084 +00:00` and `CRYPTOCURRENCY` recorded `UnexpectedFailure`
  at `09:19:02.860 +00:00`, both in slot 0 with one attempt. The same table
  records successful `CRYPTOCURRENCY` attempts on 25–27 September.
  `MarketCategoryInstrumentCollectionRuns` retains successful crypto listings
  with 48 instruments, one page and an effective frequency of one per day;
  there is no successful commodities listing in that table.
- A fresh Aspire run on 3 October reported a healthy API and returned HTTP
  200 for `/scalar/v1`. Its **new** telemetry had no matching publication
  exception. The screenshot's earlier 09:19 UTC run predates this AppHost
  session, so its original exception stack and provider responses were not
  available from the new dashboard. No collection was manually retriggered,
  and the live IG response contents remain unverified.

These are two **different reported outcomes**, not evidence of an authorization
error caused by signing in as `local-operator`. The page displays saved status and last-good
data rather than fetching IG in the browser:
[runtime behavior](wiki/runtime-behavior.md#market-category-instrument-collection),
[page rendering](../src/TNC.Trading.Platform.Web/Components/Pages/MarketCategories/MarketCategories.razor#L69-L112).
An `Incomplete` detail state is a separate downstream coverage indicator,
not proof that the crypto listing request itself returned incomplete data.

## Failure paths

### COMMODITIES: listing rejected before publication

The coordinator translates either `InvalidCollection` or
`IncompleteCollection` to the persisted `InvalidResponse` label
([failure mapping](../src/TNC.Trading.Platform.Application/Features/MarketCategoryInstruments/MarketCategoryInstrumentCycleCoordinator.cs#L842-L854)).
The IG category-instruments gateway rejects missing or inconsistent pagination
metadata, wrong page counts or row counts, duplicates (including **duplicate
instrument names**, not only duplicate EPICs), malformed fields, expiry
formats, excessive results, and JSON parsing failures
([collection checks](../src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgMarketCategoryInstrumentsGateway.cs#L82-L205),
[instrument validation](../src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgMarketCategoryInstrumentsGateway.cs#L399-L508)).
Its per-category caps are 100 pages and 15,000 rows. Any such failure
prevents publication; the stored code does **not** identify which check
failed. The provider response from the affected run is unavailable, so
neither malformed IG data nor an over-restrictive application rule can yet
be asserted as the specific cause.

### CRYPTOCURRENCY: generic failure with an unresolved origin

One way to record `UnexpectedFailure` is for snapshot publication to throw
after the gateway has returned a complete result: the coordinator logs
`Instrument category snapshot publication failed` and saves that safe code
([publication catch](../src/TNC.Trading.Platform.Application/Features/MarketCategoryInstruments/MarketCategoryInstrumentCycleCoordinator.cs#L648-L703)).
But the same label is also the default mapping for gateway failures such as
`Unauthorized` or `Rejected`, and other unmapped failure categories
([failure mapping](../src/TNC.Trading.Platform.Application/Features/MarketCategoryInstruments/MarketCategoryInstrumentCycleCoordinator.cs#L842-L854),
[failure categories](../src/TNC.Trading.Platform.Application/Features/MarketCategoryInstruments/MarketCategoryInstrumentFailureCategory.cs#L1-L20)).
The persisted safe code alone cannot establish whether crypto reached SQL.
The snapshot writer validates provenance and rows, checks catalogue revision
and leases, and replaces current rows in a serializable transaction
([writer](../src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfMarketCategoryInstrumentSnapshotStore.cs#L20-L115),
[commit](../src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfMarketCategoryInstrumentSnapshotStore.cs#L158-L231)).
If the publication branch was reached, an invalid new field or expiry
timestamp, a stale lease/revision, or a database constraint/update error
could produce this label. Without the 09:19 logs, it would be speculation to
choose that branch or any particular exception.

There is also a **confirmed contract mismatch** worth testing during the
fix: the writer rejects `EffectiveUpdatesPerDay > 4`
([validation](../src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfMarketCategoryInstrumentSnapshotStore.cs#L305-L329)),
while the current runtime permits higher positive frequencies with a warning
([runtime behavior](wiki/runtime-behavior.md#market-category-instrument-collection)).
It does **not** explain the observed historical crypto failures: the saved
successful runs use frequency 1, and the failing runs' frozen frequency was
not verified. Do not treat this mismatch as the diagnosed cause.

## Recommended fix and verification

1. **Capture the next failure safely before changing validation.** On an
   eligible scheduled run, inspect the API Aspire structured logs for
   `Instrument category snapshot publication failed` and correlate the
   timestamp/category with the persisted attempt. If present, capture the
   exception type, message, inner exception and SQL error number for crypto;
   if absent, investigate the gateway's actual failure category (including
   session/HTTP status). Add structured category, run/collection ID, stage
   and bounded validation-reason logging where the generic code does not
   distinguish checks. Do not expose credentials, session tokens, or complete
   provider bodies in logs or the UI.
2. **For commodities, obtain a sanitized, bounded sample of the failed
   response's metadata and offending row/field** (or reproduce with a
   controlled provider fixture). Compare it to the documented
   [IG Labs category-instruments API](https://labs.ig.com/reference/categories-category-id-instruments.html)
   and the gateway's actual checks. If IG legitimately repeats instrument
   names, for example, remove the name-uniqueness restriction while retaining
   EPIC uniqueness and completeness checks; if the metadata is genuinely
   inconsistent, retain fail-closed behavior and report the precise reason.
   Do not simply accept partial pages or increase limits blindly.
3. **For crypto, fix the failure branch revealed by diagnostics.** If it is
   a gateway `Unauthorized` or `Rejected` result, handle the confirmed
   provider condition and give it a distinct safe status rather than
   reporting `UnexpectedFailure`. If it is a publication exception, adjust
   a validation/storage constraint only for legitimate provider values,
   address lease/revision races without weakening fencing, or correct the
   confirmed SQL mapping. Preserve the previous complete snapshot and
   transactional rollback on failure. Align the writer's frequency
   validation with the supported unbounded positive frequency separately
   if a regression test confirms that path.
4. Add focused gateway tests using a representative sanitized commodities
   payload and SQL-backed publication tests for repeated crypto collections,
   the identified exception boundary, and preservation of last-good data.
   Verify both categories subsequently show a successful attempt with a
   complete saved listing and that market-detail coverage advances according
   to its own stage. Do not equate a successful listing with successful
   market-detail collection.

**Confidence:** high that commodities failed gateway collection validation
and that the persisted attempts are repeatable; low as to the specific IG
field and whether crypto failed in the gateway or during publication until
contemporaneous diagnostics are captured. The recommendation is to improve
precise diagnosis and then make a narrow, tested fix rather than relaxing
fail-closed validation.
