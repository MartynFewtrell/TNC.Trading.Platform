<!-- markdownlint-disable-file -->
# Task Research: IG Account Preferences Web UI Integration

Research how the IG REST account-preferences endpoints should be integrated with the platform's web account experience. This is a research-only document; no product implementation is included.

## Task Implementation Requests

* Investigate IG `GET /accounts/preferences` and `PUT /accounts/preferences` from the IG Labs REST companion.
* Determine the correct integration into the web UI Account Details experience.
* Recommend an implementation approach consistent with local application, infrastructure, API, Blazor, authorization, and test conventions.

## Scope and Success Criteria

* Scope: IG API contract, application Test/Live status terminology, existing Account Details and configuration flows, provider boundary patterns, authorization, error handling, live UI ownership, SQL-backed preference history, and test impact.
* Exclusions: product code changes, live IG account calls, changing broker-environment behavior, and external account-type availability verification.
* Assumptions:
  * The existing repository v2 IG session authentication model remains the current integration standard.
  * `trailingStopsEnabled` is the only preference supplied by the specified provider endpoints.
  * `Test` and `Live` are the application's two statuses. IG's `demo-api.ig.com` endpoint name is provider terminology, not an application status.
  * IG remains the live authority for the current preference; the database stores verified historical observations for retrospective analysis.
  * The requested outcome is a recommendation for a future implementation.
* Success Criteria:
  * Establish the provider contract and its operational constraints with official-source evidence.
  * Identify the current local call chain and explain whether Account Details is the correct ownership surface.
  * Select one implementation design with alternatives, a UI/API/application/infrastructure shape, and focused tests.

## Outline

1. IG API contract and provider operational behavior.
2. Existing platform account-details, configuration, and IG-boundary evidence.
3. Technical scenario analysis and evaluated alternatives.
4. Selected integration approach, implementation outline, and verification plan.

## Potential Next Research

* Verify account-type, regional, and Test-environment availability of `/accounts/preferences` against the configured IG non-production endpoint.
  * Reasoning: the official reference does not state availability across account types or regional entities.
  * Reference: https://labs.ig.com/reference/accounts-preferences.html
* Capture non-production allowance response headers and error codes.
  * Reasoning: provider documentation does not specify a retry interval or endpoint-specific allowance.
  * Reference: https://labs.ig.com/rest-trading-api-guide.html
* Decide the preference-history retention period and any archive/export obligation.
  * Reasoning: the existing 90-day operational retention default may be insufficient for retrospective trading analysis.
  * Reference: `.copilot-tracking/research/subagents/2026-08-17/trailing-stops-preference-audit-history-research.md`

## Research Executed

### File Analysis

* `src/TNC.Trading.Platform.Web/Components/Pages/AccountDetails/AccountDetails.razor:1-187`
  * `/account-details` is Viewer-authorized and renders persisted account-retrieval history. Its refresh control is conditional on Operator scope.
* `src/TNC.Trading.Platform.Web/Components/Pages/AccountDetails/AccountDetailsPagePresenter.cs:5-64`
  * The presenter owns historical retrieval loading and refresh-state/error presentation, rather than a live mutable broker setting.
* `src/TNC.Trading.Platform.Web/PlatformApiClient.cs:39-56` and `src/TNC.Trading.Platform.Web/PlatformApiClient.cs:118-153`
  * The web client already separates Viewer read operations from Operator mutation operations by bearer-token scope.
* `src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs:39-52`
  * Existing platform endpoint registration distinguishes configuration reads/writes and account-detail reads/refreshes by policy.
* `src/TNC.Trading.Platform.Application/Features/AccountDetails/RefreshAccountDetailsHandler.cs:9-87`
  * Account-details refresh coordinates a provider retrieval and persists a snapshot, confirming that it is unsuitable as the source of truth for an immediately effective IG preference.
* `src/TNC.Trading.Platform.Application/Features/AccountDetails/IAccountDetailsGateway.cs:3-14`
  * The application owns an inward-facing provider port and typed result/failure contract.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgAccountDetailsGateway.cs:17-80`
  * The existing IG boundary creates a v2 session, sends `X-IG-API-KEY`, `CST`, `X-SECURITY-TOKEN`, and endpoint `Version`, then retries a request once after `401`.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgBrokerAuthenticationGateway.cs:178-216`
  * Provider diagnostic parsing is bounded and does not disclose raw provider bodies or credentials.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/DependencyInjection/PlatformInfrastructureServiceCollectionExtensions.cs:91-102`
  * The current account-details IG client is registered against IG's provider-owned `demo-api.ig.com` endpoint and its adapter hard-codes the legacy `BrokerEnvironmentKind.Demo`; the application status vocabulary should instead be Test and Live.
* `src/TNC.Trading.Platform.Application/Configuration/PlatformEnvironmentKind.cs:3-7` and `src/TNC.Trading.Platform.Application/Configuration/BrokerEnvironmentKind.cs:3-7`
  * Application status already has `Test` and `Live`, while the separately persisted broker-environment key uses legacy `Demo` and `Live` values. Renaming that persisted key requires a migration or backward-compatible read strategy, not a display-only replacement.
* `src/TNC.Trading.Platform.Application/Services/TradingScheduleGate.cs:22-27`
  * Current safety logic blocks a Test application status only when combined with the legacy Live broker key. The clarified Test policy requires a broader, explicit prohibition on real order placement and monetary exposure.
* `src/TNC.Trading.Platform.Web/Components/Pages/Configuration/Configuration.razor:32-187` and `src/TNC.Trading.Platform.Web/Components/Pages/Configuration/ConfigurationPagePresenter.cs:20-47`
  * The configuration page is the closest local Operator mutation pattern: `EditForm`, disabled save state, accessible feedback, and presenter-to-client mapping.
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/AccountDetailsTests.cs:12-143`
  * Existing Account Details coverage verifies rendering of saved data and refresh failure behavior; it does not establish a live settings interaction.
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/Integrations/Ig/IgAccountDetailsGatewayTests.cs:12-76`
  * Current boundary tests cover headers, one unauthorized replay, malformed payload rejection, and provider-data redaction.
* `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/AccountDetailsEndpointMappingTests.cs:35-96`
  * Existing endpoint tests establish platform mappings for `429`, `502`, `503`, and `504` provider-related outcomes.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/PlatformDbContext.cs:27-29` and `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfAccountDetailsSnapshotStore.cs:24-56`
  * Account-details history establishes the closest persistence precedent: immutable success-only records, transactional graph persistence, deterministic environment-scoped ordering, and retrieval history rather than a mutable current row.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/Entities/OperationalEventEntity.cs:3-27` and `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfPlatformEventStore.cs:14-60`
  * Generic operational events are append-oriented and suitable for bounded, redacted provider-failure telemetry, but their JSON detail shape and 50-row operational feed are not a typed retrospective preference-history model.
* `src/TNC.Trading.Platform.Application/Services/OperationalRecordRetentionPolicy.cs:5-21` and `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Operations/Retention/OperationalRecordRetentionProcessor.cs:17-90`
  * Current operational retention defaults to 90 days and has SQL and in-memory deletion paths; preference-history retention requires an explicit, potentially longer business decision.

### External Research

* IG Labs REST reference: [Accounts preferences](https://labs.ig.com/reference/accounts-preferences.html)
  * Documents `GET /accounts/preferences` and `PUT /accounts/preferences` at `Version: 1`.
  * `GET` returns `trailingStopsEnabled: Boolean`; `PUT` accepts that Boolean and documents `{ "status": "SUCCESS" }` as the success outcome.
  * No `dealingEnabled` field is documented for this resource.
* IG Labs: [REST Trading API guide](https://labs.ig.com/rest-trading-api-guide.html)
  * Defines JSON `4xx`/`5xx` error payloads, v2 session credentials, and provider allowance errors that can be `403`, including public API key/account allowance codes.
* IG Labs: [REST companion](https://labs.ig.com/companion/api-rest-companion-release/index.html)
  * The requested companion documentation indexes the account-preferences reference.

### Project Conventions

* Standards referenced: `.github/copilot-instructions.md`; `.github/instructions/architecture.instructions.md:49-116`; `.github/instructions/csharp.instructions.md:23-65`; `.github/instructions/configuration.instructions.md:19-45`; `.github/instructions/radzen-blazor.instructions.md:28-69`; and `.github/instructions/tests.instructions.md:20-108`.
* Instructions followed: feature-oriented application design; application-owned outbound ports; thin API endpoints; typed, redacted boundary failures; Radzen `EditForm` interactions; and focused xUnit coverage with requirement traceability comments.

## Key Discoveries

### IG Account-Preferences Contract

`GET /accounts/preferences` returns the live account preference:

```json
{ "trailingStopsEnabled": false }
```

`PUT /accounts/preferences` receives the desired Boolean value and returns a provider outcome:

```json
{ "trailingStopsEnabled": true }

{ "status": "SUCCESS" }
```

Both requests should explicitly send `Version: 1`. In the repository's existing v2 session model, send `X-IG-API-KEY`, `CST`, and `X-SECURITY-TOKEN`; send JSON `Accept` on both methods and JSON `Content-Type` for `PUT`. The setting allows a user to define trailing-stop rules for trade operations; it is neither a per-position trailing-stop indicator nor an operation that changes existing deals.

Provider session expiry can yield `401`. Refresh the session and replay at most once. Treat allowance-specific `403` error codes as a platform rate-limit result (`429`), rather than assuming only provider `429` means rate limiting. Avoid blind write retries: a timeout or connection break leaves the final provider state unknown, so reload the authoritative value before offering an operator another update.

### Account Details Is Not the Correct Write Owner

The present Account Details feature stores and displays historical IG account snapshots. Combining a live capability switch with that history would create two incompatible meanings for one page: a time-bound retrieval record and an immediately effective provider command. The existing Viewer read/conditional Operator refresh model would also make the mutation state unnecessarily complex.

The preference should remain account-oriented in navigation but be implemented as its own live feature and page. Because enabling trailing stops changes trading capability, this research selects an Operator-only page and API surface instead of exposing the broker setting to Viewer users.

### Environment Limitation

The application has two statuses: Test and Live. Test connects to IG's live-like test data and may behave like a trading account for account and market interactions, but it must not place real trades or create monetary exposure. Live is a future, explicitly configured status in which real trades can be placed and money can be earned or lost.

IG's `demo-api.ig.com` hostname and any provider documentation that says “Demo” identify the provider's test endpoint. They do not define a third application status and should not be presented as one in UI, API, or application terminology. Existing broker-environment records and code currently use `Demo` as a persisted technical key. A future implementation must either migrate those records to `Test` or support backward-compatible reads while presenting Test consistently; it must not silently reinterpret historical data.

The account-preferences feature should operate only for the selected supported application status. It must not imply that Live is configured or that Test can place real trades. Live preference integration is deferred until future Live credentials, endpoint selection, explicit trade authorization, and monetary-risk controls are configured and tested.

### Retrospective Trailing-Stops Evidence

`TrailingStopsEnabled` is an on/off account-level switch, so retrospective interpretation of trading behavior needs a durable record of what the platform actually observed at relevant points in time. Persist every successfully validated value in a dedicated append-only SQL history. This is evidence of the provider state at the observation time, not a local source of truth for the current setting.

Each row should retain the Boolean value, `ObservedAtUtc`, `RecordedAtUtc`, application Test/Live status, legacy/provider environment key where required for historical partitioning, optional provider account ID, observation kind, operation source, actor when operator initiated, correlation ID, optional command ID, and a success outcome. Retain repeated equal observations: a timeline transition is derived by comparing adjacent observations ordered by observed time and identity, rather than claiming a provider-side change that the platform did not observe.

Do not record failure rows with a nullable Boolean. A failed or indeterminate provider operation means the value is unknown, not false. Store only a bounded, redacted operational failure event. After a `PUT` whose completion is uncertain, perform a fresh `GET`; append an observation only when that read succeeds.

## Technical Scenarios

### IG Account Preferences

The product needs an operator to inspect and set IG's current `trailingStopsEnabled` account preference without treating it as cached account-history data. This setting can be read and updated in Test; changing it must not be interpreted as permission for Test to place real trades or incur a monetary gain or loss.

**Requirements:**

* Read and change only IG's documented preference field using API version 1.
* Keep IG credentials, tokens, raw response bodies, and arbitrary provider error codes out of browser-visible models.
* Require the existing Operator policy for read and update because the value controls a trading capability.
* Preserve typed, testable boundary failure handling and use RFC 9457 Problem Details at the platform API boundary.
* Safely distinguish omitted JSON input from an explicit `false` update.
* Present the application status as Test or Live, never Demo. Retain “demo” only for provider-specific hosts, IDs, or externally documented contract values.
* Permit Test only for its live-like, non-monetary use case; defer Live until its real-trading configuration and controls are explicitly delivered.
* Persist every strictly validated IG preference observation in local SQL, including repeated equal values, so later trading analysis can establish what the platform observed.
* Keep live reads authoritative; never substitute the latest local history row during an IG outage.

#### Considered Alternatives

| Alternative | Evidence and advantages | Rejection rationale |
| --- | --- | --- |
| Add an editable panel to `/account-details` | Account-related information is already visible there. | `AccountDetails` is a persisted snapshot/history feature (`RefreshAccountDetailsHandler.cs:9-87`), while this value is live and mutable. Combining them weakens the page's data ownership and authorization model. |
| Persist desired state locally and reconcile with IG | Could offer a cached view during provider outages. | It creates a second source of truth, reconciliation/auditing requirements, stale-state risk, and a migration without an offline-workflow requirement. |
| Use IG OAuth session v3 for this endpoint | It is a supported IG authentication model. | The platform uses v2 session credentials at its current provider boundaries. A single endpoint does not justify an application-wide auth-model migration or account-id selection work. |
| Blindly retry failed `PUT` requests | May conceal transient network failures. | A timeout makes completion indeterminate. A state-setting request is idempotent in intent, but the correct recovery is a fresh `GET`, not repeated unobserved writes. |
| Extract a generic IG client before adding this feature | Could reduce token/header duplication. | Extract only a small session helper if it demonstrably simplifies both adapters. A generic provider abstraction is premature at two concrete call sites. |

**Selected Approach:**

Implement a dedicated, Operator-only live `/account-preferences` page backed by direct, feature-specific query and command operations. This preserves Account Details as history, has no local desired-state shadow, follows the existing configuration mutation experience, and retains a narrow IG boundary.

```text
Web /account-preferences (Operator)
    -> PlatformApiClient
        -> GET|PUT /api/platform/account-preferences (Operator)
            -> Get|UpdateAccountPreferencesHandler
                -> IAccountPreferencesGateway
                    -> IgAccountPreferencesGateway
                        -> IG GET|PUT /accounts/preferences (Version: 1)
                  -> ITrailingStopsPreferenceObservationStore
                    -> SQL append-only observation history
```

**Implementation Details:**

Create `src/TNC.Trading.Platform.Application/Features/AccountPreferences/` with one top-level type per file:

* `AccountPreferences` record with `bool TrailingStopsEnabled`.
* `IAccountPreferencesGateway` returning a typed success/failure outcome.
* `TrailingStopsPreferenceObservation` record and `ITrailingStopsPreferenceObservationStore`, which owns persistence of completed, verified provider observations.
* `GetAccountPreferencesRequest`, response, and handler.
* `UpdateAccountPreferencesRequest`, response, validator, and handler.
* A read-only, paged preference-history query for retrospective analysis.

Create `IgAccountPreferencesGateway` in the IG integration folder. It should resolve protected credentials for the selected supported environment, create a v2 session, make either endpoint call with the required headers, strictly parse the Boolean response or `SUCCESS` status, and normalize failures. Replay once only after `401`. Allow-listed diagnostic data may be logged server-side; do not propagate it to web/API payloads.

Use `Test` and `Live` in all new application, web, API, and documentation models. During Test, the gateway may target IG's provider-named test/demo endpoint but must not expose “Demo” as an application setting. Do not implement Live endpoint selection, real order placement, or monetary-risk behavior as part of this preference feature; those require future explicit configuration and controls.

Implement `TrailingStopsPreferenceObservations` as a dedicated additive EF Core table, not as an `OperationalEvents`, configuration-audit, or account-details child record. The table should have an identity primary key and a non-unique history index on provider/environment scope, `ObservedAtUtc`, and the identity key. Do not add a uniqueness constraint for state or day because equal consecutive reads remain useful evidence. Record a row only after a strict successful IG `GET`; after a successful `PUT`, confirm with a fresh `GET` and save it as `UpdateConfirmed`. Save post-write recovery reads as `ReconciliationObserved`.

The history reader must use deterministic newest-first keyset pagination and show that each entry is an observed IG value, not a guarantee of the setting between observations. Keep it separate from the live preference control, either as a compact history panel or a dedicated read-only history view. Never fall back to the latest observation if IG is unreachable because that would create a misleading second authority.

Add the table to `PlatformDbContext`, create an additive migration, include it in SQL and in-memory retention processing, and introduce a dedicated preference-history retention policy. The present 90-day operational default is not automatically appropriate for trading-behavior analysis; select and document a longer retention or archive/export policy before implementing deletion.

Add thin platform endpoints:

```text
GET /api/platform/account-preferences  -> Operator
PUT /api/platform/account-preferences  -> Operator
```

Use a nullable HTTP input field to avoid an omitted request field becoming `false` during model binding, then validate it before creating the non-nullable update command:

```csharp
public sealed record UpdateAccountPreferencesHttpRequest(bool? TrailingStopsEnabled);
```

Return the platform's stable preference model after a confirmed update rather than surfacing IG's `status` value. Map omitted/invalid API input to `400`; classified provider allowance exhaustion to `429`; malformed provider JSON or a malformed success outcome to `502`; provider rejection, entitlement, or unavailable session recovery to `503`; and provider timeout to `504`. Do not map a provider-side `403` directly to a platform-user authorization result.

The page should present the current value as a labelled trailing-stops toggle, loading/saving disabled states, and accessible success/error feedback. After an indeterminate update, reload from IG and state the reconciled outcome. Add a navigation entry only if the product navigation convention calls for an account-preferences destination; do not otherwise change Account Details.

**Test Plan:**

* Application: successful query/update, update validation, Test status behavior, future Live deferral, one append for each successful observation, repeated-equal-value retention, and uncertain-write reconciliation outcome.
* Infrastructure: HTTP method/path, `Version: 1`, required headers, JSON content, strict response parsing, one `401` replay, allowance-code classification, redaction, no blind retry after an indeterminate write, and atomic observation persistence where paired with a command audit.
* API: Operator policy, input-to-command mapping, validation Problem Details, and typed upstream status mappings.
* Web: initial load, toggle state, disabled save, success state replacement, failure feedback, Operator authorization behavior, and a paged observation timeline with derived transitions.
* SQL integration: additive migration/index creation, deterministic history ordering when timestamps match, retention handling, and history surviving a context restart.

No automated test should call real IG. An authenticated functional/Aspire test is appropriate only if acceptance criteria require end-to-end policy/browser validation, using a controllable provider boundary.

## Actionable Next Steps

1. Define Test as the only currently supported status for this feature, including an explicit prohibition on real trade placement and monetary exposure.
2. Decide the preference-history retention and archive/export policy needed for retrospective trading analysis.
3. Decide whether every live preference read, only explicit reads/updates, or a scheduled reconciliation cadence should create observations; this research recommends every successfully validated read.
4. Decide whether legacy persisted `Demo` broker-environment records migrate to Test or receive a backward-compatible mapping before this feature is implemented.
5. Confirm access to the endpoint for the configured Test IG account and capture real non-secret allowance/error behavior.
6. Implement the new AccountPreferences and trailing-stops observation-history vertical slices, including additive migration and retention support.
7. Add the focused test layers described above, including Test/Live terminology and test comments that trace the provider and product requirements.
8. Update the affected `docs/wiki/` source-of-truth documentation as part of implementation completion.

## References

* [IG Labs REST companion](https://labs.ig.com/companion/api-rest-companion-release/index.html)
* [IG Labs accounts-preferences reference](https://labs.ig.com/reference/accounts-preferences.html)
* [IG Labs REST Trading API guide](https://labs.ig.com/rest-trading-api-guide.html)
* `.copilot-tracking/research/subagents/2026-06-06/ig-account-preferences-api-and-ui-research.md`
* `.copilot-tracking/research/subagents/2026-06-06/ig-account-preferences-design-alternatives-research.md`
* `.copilot-tracking/research/subagents/2026-08-17/trailing-stops-preference-audit-history-research.md`
