<!-- markdownlint-disable-file -->
# Task Research: IG market collection failures

Investigate the reported IG Demo collection failures on 4 October 2026, correlate the logs with the saved-category UI, and recommend an evidence-based resolution.

## Task Implementation Requests

* Determine the causes of the `PageRowCountMismatch` and `error.public-api.exceeded-api-key-allowance` failures.
* Explain why the UI reports the observed collection and market-detail coverage states.
* Recommend a safe implementation and operational response.

## Scope and Success Criteria

* Scope: Repository collection, pagination, provider error handling, scheduling, persistence, diagnostics, and relevant IG API documentation.
* Assumptions: The supplied logs and screenshot represent the same Demo account and 4 October 2026 run; the active profile is confirmed as IG Demo; the API key is not unintentionally shared; a subsequent request using the same key successfully downloaded data; no credentials or live account changes are required for research.
* Success Criteria:
  * Trace both failures to concrete code paths and provider semantics.
  * Distinguish application defects from provider quota or entitlement behavior.
  * Recommend implementation changes with file-and-line evidence and validation steps.

## Outline

The incidents contain two independent provider failure classes:

* Commodities returned a successful HTTP response whose pagination metadata did not match the instrument rows.
* Cryptocurrency returned HTTP 403 while the market-detail session request reported `error.public-api.exceeded-api-key-allowance`.

The application is already designed to fail closed for both classes. The main remediation is operational and diagnostic: separate quota, entitlement, and authentication outcomes, then confirm the Demo key allowance and category entitlement before changing validation or retry behavior.

## Potential Next Research

* Capture a sanitized raw commodities response and the cryptocurrency 403 `errorCode` from the next run.
  * Reasoning: The supplied log proves the recorded classification, but not the exact provider payload or whether the 403 body was preserved for this incident.
* Query the active IG Demo application allowance through the provider-supported client-apps endpoint.
  * Reasoning: IG does not expose standard rate-limit headers, and Demo allowance limits can change.

## Research Executed

### File Analysis

* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgMarketCategoryInstrumentsGateway.cs:140-180,236-249,270-276,649-650`
  * The gateway permits one bounded replay when the first page is short, then validates stable totals and the expected row count for every page. It returns `IncompleteCollection` with reason `PageRowCountMismatch` rather than publishing incomplete instruments.
  * The reported `70` expected and `42` actual values are consistent with provider metadata promising more rows than the response array contained.
* `src/TNC.Trading.Platform.Application/Features/MarketCategoryInstruments/MarketCategoryInstrumentCycleCoordinator.cs:723-739,864-890`
  * A failed attempt is persisted before the final `Instrument category collection failed` warning is emitted. The warning is a terminal attempt summary, not the root cause.
  * HTTP 403 category responses are mapped to the safe persisted code `ProviderAccessDenied`; detailed provider diagnostics remain in structured logs.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgMarketDetailsGateway.cs:29-37,551-598,674-690`
  * The gateway extracts the bounded JSON `errorCode` from HTTP 403 responses. Known allowance codes, including `error.public-api.exceeded-api-key-allowance`, map to `AllowanceUnavailable` and are not retried within the active attempt.
  * Unknown 403 codes map to `Unauthorized`, which means HTTP status alone cannot distinguish quota exhaustion from entitlement or authentication failure.
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/Integrations/Ig/IgMarketCategoryInstrumentsGatewayTests.cs:145-153,454-456`
  * Tests preserve the fail-closed row-count and metadata consistency behavior.
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/Integrations/Ig/IgMarketDetailsGatewayTests.cs:400-430`
  * Tests verify known allowance codes and unknown 403 codes are classified separately and are not retried.
* `docs/wiki/operator-guide.md:105-120`
  * The operator guide explains that saved attempts remain visible and that Demo access or category permissions should be checked for provider 403 responses.
* `src/TNC.Trading.Platform.Web/Components/Pages/MarketCategories/MarketDetails.razor` and `src/TNC.Trading.Platform.Web/PlatformApiClient.cs:290`
  * The UI reads saved observations rather than live provider data.
* User-supplied raw response, `Pasted text #1.txt:1-9,28-37,1418-1423`
  * The HTTP 200 response declared `pageNumber=0`, `pageSize=150`, `totalPages=2`, and `totalResults=290` for the commodities instruments endpoint.
  * Counting the captured `"epic"` entries gives 70 instrument records in the pasted body. The response therefore advertises a 150-row first page and 290 total rows but supplies only 70 rows, independently confirming a provider response-shape problem.
  * The capture included a live-looking API key and account identifier in request headers. Those values are intentionally not copied into this document.

### Code Search Results

* `Instrument category collection failed`
  * The coordinator emits this after a failed category attempt has been classified and persisted.
* `PageRowCountMismatch`
  * The category gateway emits this when a page's actual instrument count disagrees with the count implied by provider metadata.
* `error.public-api.exceeded-api-key-allowance`
  * The market-details gateway includes this exact code in its known allowance set.

### External Research

* IG official developer guide: [REST Trading API guide](https://labs.ig.com/rest-trading-api-guide.html)
  * Documents the API-key, account, CST, and `X-SECURITY-TOKEN` headers and session-token lifetime behavior.
* IG official FAQ: [IG Labs FAQ](https://labs.ig.com/faq.html)
  * Documents token expiry and maintenance-related invalidation behavior.
* IG endpoint reference: [Category instruments](https://labs.ig.com/reference/categories-category-id-instruments.html)
  * Identifies the category instrument endpoint used by the repository. The page is JavaScript-rendered, so exact schema text was not independently extracted.
* Secondary operational reference: [trading-ig FAQ](https://trading-ig.readthedocs.io/en/latest/faq.html)
  * Describes the four allowance families, states that Demo limits are lower and can change, and notes that IG does not provide standard rate-limit-remaining headers. It identifies the client-apps endpoint as the way to inspect current allowance.
* Provider ecosystem evidence: [trading-ig issue 178](https://github.com/ig-python/trading-ig/issues/178), [issue 203](https://github.com/ig-python/trading-ig/issues/203), and [issue 365](https://github.com/ig-python/trading-ig/issues/365)
  * Shows that 403 responses also represent product or account entitlement failures and that the market-navigation surface has changed over time.

### Project Conventions

* The application uses safe, allow-listed provider error codes rather than persisting arbitrary provider response text.
* Collection is fail closed: partial or ambiguous provider data is not published as complete data.
* Saved collection attempts and the most recent successful collection are intentionally displayed separately.

## Key Discoveries

### Incident interpretation

The three reported messages are related in time but do not describe one failure:

| Evidence | Meaning | Confidence |
|---|---|---:|
| Commodities, `InvalidResponse`, `IncompleteCollection`, `PageRowCountMismatch`, expected `70`, actual `42` | IG returned internally inconsistent pagination metadata and rows. The application correctly rejected a potentially partial collection. | 9/10 |
| Cryptocurrency, `ProviderAccessDenied`, HTTP `403` | The category or market-detail request was rejected by IG. The safe category status alone does not identify quota versus entitlement. | 8/10 |
| Market-detail session HTTP `403`, `error.public-api.exceeded-api-key-allowance` | IG rejected that session request under an API-key allowance rule. A later successful request with the same key means the key was not continuously exhausted; the event may have been scoped, burst-based, or already recovered. | 10/10 |

### UI interpretation

The screenshot is consistent with persisted attempt state:

* `COMMODITIES` shows `Never collected`, a 4 October 09:00 failed attempt, `Failed - InvalidResponse`, and `NeverCollected` market-detail coverage. This means no complete category collection was published, so there are no usable instruments from which to collect details.
* `CRYPTOCURRENCY` shows a prior successful collection on 3 October, a failed 4 October attempt, `Failed - ProviderAccessDenied`, and `Incomplete` market-detail coverage. The prior saved category remains visible, while the current attempt is separately marked failed.
* Other categories show `No attempt this trading day` and `NotFollowed`, which indicates they were not selected for the current interest set, not that they failed.
* The UI text stating that collection results are saved attempts, not live errors, is important. Refreshing the page does not re-run the provider call.

### Root cause assessment

* Do not relax `PageRowCountMismatch` or accept the 42 rows as complete. Doing so would silently under-collect instruments.
* The additional raw response demonstrates the same failure mode independently: 70 returned rows versus metadata that implies 150 rows on page zero. It does not prove that the logged 70 versus 42 values came from this exact response, because the application request may use a different page-size policy or a later provider response.
* Do not treat every HTTP 403 as invalid credentials. The response body `errorCode` is the reliable discriminator between allowance, entitlement, and session/authentication problems.
* The allowance failure is expected provider behavior and is likely the immediate reason the cryptocurrency market-detail session could not proceed. Demo allowance limits are less predictable than Live limits.
* A subsequent successful request using the same key rules out a continuously exhausted API key. It does not rule out a short-lived allowance window, a burst/request-rate limit, a different account/application/trading allowance bucket, or request sequencing where the session call was denied while a later data call succeeded.
* The confirmed Demo profile and intentional single-key usage rule out an accidental Demo/Live profile mismatch and unintentional local key sharing as leading explanations for the 403. A later 403 with a non-allowance `errorCode` would still indicate entitlement or authentication rather than quota.
* The current application behavior is mostly correct. The main gap is operator-facing distinction and evidence capture, not the core fail-closed classifications.

### Security handling of the supplied capture

The pasted request contains a credential-like IG API key and an account identifier. Treat the API key as exposed: revoke or rotate it in the IG Demo application portal, update application configuration through the repository's secret-management path, and avoid sharing future captures with `X-IG-API-KEY`, `Authorization`, account identifiers, CST, or security tokens. The research conclusions do not require those values.

## Technical Scenarios

### Recommended remediation

Use a two-track response: explain the scoped or transient provider rejection, then improve observability without weakening data-integrity protections.

**Immediate operational actions**

1. Correlate the exact 403 timestamp with all requests made by the collector and the later successful download. Record endpoint, account, response code, provider `errorCode`, and elapsed time, without recording credentials.
2. Query the current allowance using IG's client-apps capability, or verify it in the provider portal. Record allowance type and reset timing without recording the key. Compare the relevant API-key, account, application, and trading counters.
3. Treat the successful later request as evidence of recovery or endpoint-specific allowance behavior. Do not rotate or replace the key solely because of this incident, and do not increase retries inside a possibly active allowance window.
4. Verify the Demo account has cryptocurrency/CFD entitlement and that the account and product region are correct. If the provider returns a non-allowance `errorCode`, classify it as entitlement or authentication and fix the account/profile rather than retrying.
5. Re-run collection after the request sequence is understood and verify the saved attempt changes to success before relying on market-detail coverage.
6. For commodities, capture a bounded, sanitized diagnostic of page number, requested page size, returned row count, `totalPages`, `totalResults`, and unique instrument identifiers. Compare the response across repeated runs.

**Preferred implementation changes**

* Preserve the existing fail-closed pagination checks and exact allowance-code allow-list.
* Add structured operator-facing fields for `FailureKind`, `ProviderErrorCode`, `HttpStatus`, `Operation`, `PageNumber`, `Expected`, and `Actual` wherever the saved-category status is rendered. Keep secrets and arbitrary response bodies out of persistence and normal logs.
* Rename or enrich the session warning so an allowance 403 is shown as API-key allowance exhaustion, while an unknown 403 remains session authorization failure.
* Add metrics grouped by environment, category, failure kind, and provider error code. Never group or label by the raw API key.
* Add a bounded raw-response capture path behind explicit diagnostic configuration for provider support investigations. Do not enable it by default.
* Verify the scheduler permits a later attempt after the provider allowance reset. A non-retryable decision should apply to the current attempt, not permanently suppress future scheduled runs.

**Validation plan**

* Unit-test known allowance codes, unknown 403 codes, entitlement-style 403 codes, and 429 throttling as distinct outcomes.
* Unit-test first-page short response replay and later-page row-count mismatch, including the exact `70` versus `42` shape.
* Add an integration test that persists a failed attempt and verifies the UI/API exposes the prior successful collection separately from the current failure.
* In Demo, run one controlled collection after quota reset and verify structured logs contain the provider code, HTTP status, and trace correlation without credentials.
* Confirm that a later scheduled run can retry after allowance recovery, while an entitlement denial remains non-retryable until configuration changes.
* After rotating the exposed key, repeat the raw commodities request with the new key and compare the number of `epic` records with `metadata.pageSize` and `metadata.totalResults`. Retain only redacted evidence.

#### Considered alternatives

* Relax row-count validation and publish the 42 commodities rows.
  * Rejected because it converts provider inconsistency into silent data loss.
* Retry all HTTP 403 responses aggressively.
  * Rejected because entitlement and invalid-session failures are not transient and retries consume more allowance.
* Treat the cryptocurrency status as invalid credentials without reading `errorCode`.
  * Rejected because the supplied provider code explicitly identifies API-key allowance exhaustion.
* Disable cryptocurrency and ignore the incident.
  * Acceptable only as a temporary operational containment measure. It does not address quota management or improve diagnostics, and it leaves stale saved state unexplained.

## Conclusion

The recommended resolution is not a broad code rewrite. Correlate the denied session request with the later successful request and allowance counters before changing credentials or retry policy. Check cryptocurrency entitlement if a later 403 carries a non-allowance code. Keep the commodities collection fail closed until IG returns coherent pagination. The durable product change should expose the already-known provider classification in the saved-category UI and telemetry, so operators can distinguish scoped allowance rejection from entitlement, authentication, and data-integrity failures without inspecting raw logs.

## Saved-status contract follow-up

The bounded contract trace at `.copilot-tracking/research/subagents/2026-10-04/saved-status-contract-research.md` confirms that category attempt persistence retains only a safe failure string; the reason, operation, page number, HTTP status, and row counts exist in the gateway failure and logs but do not survive in the saved status API. Market-detail target persistence stores only `failure.Kind.ToString()`, so the known allowance code also does not survive the run. The saved category status renders in `MarketCategories.razor`, while `MarketDetails.razor` renders a separate saved observation and coverage view. Structured metrics are not currently registered for these failure classes. A durable operator view requires deliberately bounded persistence, corresponding API and view-model changes, and cross-boundary tests; unknown 403 responses must not be presented as confirmed entitlement failures.
