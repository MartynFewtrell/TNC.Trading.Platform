---
title: Account Details Delivery Options
description: Research comparison and delivery recommendation for retrieving and presenting IG demo account details
author: GitHub Copilot
ms.date: 2026-08-07
ms.topic: concept
keywords:
  - IG Markets
  - account details
  - architecture
  - persistence
---

## Research Scope

Evaluate delivery approaches for an Account Details feature that calls
`GET /gateway/deal/accounts` against the IG demo environment, persists both
returned accounts with a retrieval timestamp, presents a new Account Details
UI page, and permits an authorized user to request an update.

The supplied representative payload is a functional contract: it contains an
`accounts` array with `accountId`, `name`, `alias`, `status`, `type`,
`preferred`, nested `balance` (`balance`, `deposit`, `profitLoss`,
`available`), `currency`, `canTransferFrom`, and `canTransferTo`.

The research compares an expanded login-proof snapshot, a current-state
per-account upsert table, and immutable retrieval snapshots with account rows.
It also covers IG session use without token persistence, request coalescing,
result semantics, authorization, transactionality, indexes, retention,
migration impact, vertical slices, routes, UI behavior, and tests.

## Functional Contract

Each successful retrieval targets the existing demo base address and invokes
`GET /gateway/deal/accounts` with IG API version `1`. The supplied payload is
the delivery contract, not an optional enrichment: persist every item in the
`accounts` array, including both returned accounts, and stamp the common
retrieval date in UTC.

Persist these fields per account: `accountId`, `name`, `alias`, `status`,
`type`, `preferred`, `balance.balance`, `balance.deposit`,
`balance.profitLoss`, `balance.available`, `currency`, `canTransferFrom`, and
`canTransferTo`. Treat money as `decimal`, Booleans as `bit`, and retrieval
time as `datetimeoffset`. Do not persist CST, X-SECURITY-TOKEN, API key,
identifier, password, or a raw payload that could accidentally expand the
stored sensitive surface.

## Local Evidence

* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgBrokerAuthenticationGateway.cs:18-86`
  creates a fresh IG session from protected credentials, gets CST and
  X-SECURITY-TOKEN response headers, then uses them only in memory.
* The same gateway already calls `GET accounts` with Version `1` at
  `.../IgBrokerAuthenticationGateway.cs:113-132`, but the local DTO only reads
  ID, account name, preferred flag, and balance at lines `222-230`.
* `src/TNC.Trading.Platform.Application/Features/PlatformAuthentication/Ports/BrokerAuthenticationProof.cs:3-7`
  is intentionally a four-field proof for the preferred account and open
  position count. It cannot satisfy the supplied two-account contract without
  turning authentication evidence into a business read model.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfPlatformIgProofDataStore.cs:11-65`
  is a unique current-state upsert per broker environment. Its supporting
  unique index is configured at
  `.../Persistence/EntityFramework/PlatformDbContext.cs:82-90`.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfPlatformIgLoginSnapshotStore.cs:13-47`
  maintains a replaceable latest row plus one retained daily successful
  snapshot. This shows the repository already differentiates current state
  from retained history.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/ManualAuthRetryCommitter.cs:16-58`
  wraps related persistence writes in a relational transaction. The new
  retrieval header and both account rows should use the same pattern.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/SqlPlatformReconciliationLease.cs:12-42`
  uses SQL Server `sp_getapplock` to prevent concurrent ownership across
  replicas. Reuse that pattern with an Account Details-specific resource.
* `src/TNC.Trading.Platform.Application/Features/ReconcilePlatformAuthentication/ReconcilePlatformAuthenticationHandler.cs:5-25`
  also has a process-local `SemaphoreSlim`; it is not enough by itself for a
  scaled API deployment.
* `src/TNC.Trading.Platform.Api/Features/Platform/PlatformEndpoints.cs:30-47`
  assigns `Viewer` to read routes and `Operator` to state-changing routes.
  `src/TNC.Trading.Platform.Application/Authentication/PlatformAuthenticationDefaults.cs:64-113`
  defines the matching roles, policies, and delegated scopes.
* `src/TNC.Trading.Platform.Web/Components/Pages/IgLogin/IgLoginHistory.razor:1-115`
  is the closest viewer-protected, API-backed page pattern.
  `src/TNC.Trading.Platform.Web/Components/Layout/MainLayout.razor:18-34`
  is the corresponding primary navigation registration point.
* `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Operations/Retention/OperationalRecordRetentionProcessor.cs:15-76`
  already performs SQL set-based retention deletion, with a test-provider
  fallback. `.../Application/Services/OperationalRecordRetentionPolicy.cs:5-24`
  defaults operational retention to 90 days.

## Official IG Evidence

* [IG Labs Accounts reference](https://labs.ig.com/reference/accounts) is the
  authoritative endpoint reference for the accounts resource. The fetch
  connector returned HTTP 500 on 2026-08-07, so field verification relies on
  the supplied representative payload as directed and should be rechecked
  against the live reference before implementation.
* [IG Labs Getting Started](https://labs.ig.com/gettingstarted) confirms that
  demo credentials and a demo API key are supported for safe API testing, and
  that API key quotas apply.
* [IG Labs REST API reference](https://labs.ig.com/rest-trading-api-reference.html)
  is the official entry point for session and endpoint version details.

## Option Comparison

| Option | Fit | Advantages | Material drawbacks | Decision |
| --- | --- | --- | --- | --- |
| A. Expand `BrokerAuthenticationProof` and `IgProofData` | Low | Smallest apparent migration; account data arrives during login | Couples a non-critical, nullable login proof to required account delivery; only has one current row; loses the second account and historic retrievals; a manual page refresh would need to trigger authentication reconciliation | Reject |
| B. New current-state table with `(BrokerEnvironment, AccountId)` upserts | Medium | Simple current page query; small footprint; preserves both current accounts | Cannot answer which two accounts were returned together; no audit/history of balances; deletion and disappearance semantics become complex | Reject as the sole model |
| C. Immutable retrieval header plus immutable account rows | High | Represents an atomic response, preserves both accounts and their common date stamp, supports current-page projection and later history, makes refresh outcomes auditable | Adds two tables, a migration, retention, and a dedicated retrieval workflow | Recommend |

## Recommended Approach

Implement option C as a separate Account Details vertical slice. The current
page reads the newest successful retrieval for the active demo broker
environment. Every successful user-triggered update stores one immutable
retrieval header and exactly all response accounts in one transaction. This
maintains a current read experience without falsely modelling a historical
response as a mutable account record.

Do not add these fields to `BrokerAuthenticationProof`, and do not have the
page invoke `IBrokerAuthenticationGateway.AuthenticateAndCollectProofAsync`.
Create a dedicated application port, for example `IAccountDetailsGateway`,
and an Infrastructure IG implementation. It may extract a private shared
IG-session helper from the current authentication gateway, but the public
authentication contract remains unchanged.

### Infrastructure Session Handling

For every refresh, Infrastructure should:

1. Load the existing protected demo credentials using
   `IProtectedCredentialService`.
2. POST a new IG `session`, validate the response and CST/X-SECURITY-TOKEN
   headers, then GET `accounts` at Version `1` using those headers.
3. Parse and validate all required account values into an application DTO.
   Reject an empty or malformed accounts array rather than inserting a partial
   retrieval.
4. Keep session tokens only in local variables and `HttpRequestMessage`
   headers. Never include them in DTOs, entities, event metadata, logs, or
   errors. Dispose requests and responses; if IG's session lifecycle requires
   it, make a best-effort token-authenticated session deletion in `finally`.
5. Map expected failures to a typed result. Reuse the existing failure
   vocabulary where it applies: rejected credentials, forbidden, rate limited,
   unreachable, timeout, malformed response, and unexpected response.

`ProtectedCredentialService` already decrypts only the three long-lived
credentials (`.../Credentials/DataProtection/ProtectedCredentialService.cs:75-113`).
There is no repository pattern for persisting IG session tokens, and none
should be introduced.

### Concurrency and Result Semantics

Use an `IAccountDetailsRefreshCoordinator` in Application and a SQL Server
lease named `TNC.Trading.Platform.AccountDetailsRefresh.Demo` in
Infrastructure. A singleton keyed in-process task can coalesce simultaneous
requests reaching the same API instance. The SQL application lock remains the
authoritative cross-replica guard.

When another replica owns the lease, do not create a second IG session. Return
an explicit `RefreshInProgress` outcome, mapped to HTTP `409 Conflict`, with
the latest persisted retrieval timestamp where available. A same-instance
caller joins the in-flight task and receives its completed response. This is
useful coalescing without pretending that independent replicas can share a
memory task.

Successful refreshes return `200 OK` with the persisted retrieval. Map
expected IG availability failures to problem details: `429` for rate limiting,
`503` for unreachable or rejected upstream session establishment, `504` for a
timeout, and `502` for malformed or unexpected IG responses. Invalid platform
state, such as a non-demo target, returns `409`. The GET route returns `200`
with `retrieval: null` when no successful retrieval has occurred; it must not
perform an IG call.

### Authorization and UI Behavior

Add a viewer-protected page at `/account-details`, linked next to IG login
history. It loads the latest persisted account retrieval and displays:

* The UTC retrieval date and an empty state when no data has been stored
* One compact, accessible account section per returned account
* Identity and status fields, account preference, currency, transfer flags,
  and the four balance values with explicit labels
* A visible failure or in-progress message without clearing previously shown
  data

The page requires `PlatformViewer` and calls GET with `platform.viewer`. Show
the `Update account details` command only to an Operator or Administrator. The
POST requires `PlatformOperator` and `platform.operator`; a Viewer receives
the normal API authorization denial and cannot infer broker data by triggering
an update. Disable the command locally while it runs. On success, replace the
page projection with the response. On `409`, retain data and show the supplied
in-progress or blocked reason.

### Route and Operation Names

Use a read-oriented resource name that does not expose IG transport terms:

* `GET /api/platform/account-details`:
  `GetAccountDetailsHandler`, `GetAccountDetailsRequest`, and
  `GetAccountDetailsResponse`
* `POST /api/platform/account-details/refresh`:
  `RefreshAccountDetailsHandler`, `RefreshAccountDetailsRequest`, and
  `RefreshAccountDetailsResponse`
* Application ports: `IGetAccountDetailsReader`,
  `IAccountDetailsGateway`, `IAccountDetailsSnapshotStore`, and
  `IAccountDetailsRefreshLease`

The POST is deliberately a command route because it causes a new broker
session, an external read, and durable writes. It must not be hidden behind a
GET or page load.

### SQL Schema and Retention

Outline the migration as two tables under the existing EF Core model:

```text
AccountDetailsRetrievals
  AccountDetailsRetrievalId uniqueidentifier PK
  BrokerEnvironment nvarchar(32) NOT NULL
  RetrievedAtUtc datetimeoffset NOT NULL
  AccountCount int NOT NULL
  TriggeredBy nvarchar(128) NULL
  TriggerSource nvarchar(32) NOT NULL       -- Manual

AccountDetailsAccounts
  AccountDetailsAccountId uniqueidentifier PK
  AccountDetailsRetrievalId uniqueidentifier NOT NULL FK cascade delete
  AccountId nvarchar(64) NOT NULL
  Name nvarchar(256) NOT NULL
  Alias nvarchar(256) NULL
  Status nvarchar(64) NOT NULL
  AccountType nvarchar(64) NOT NULL
  IsPreferred bit NOT NULL
  Balance decimal(19,5) NOT NULL
  Deposit decimal(19,5) NOT NULL
  ProfitLoss decimal(19,5) NOT NULL
  Available decimal(19,5) NOT NULL
  Currency nvarchar(16) NOT NULL
  CanTransferFrom bit NOT NULL
  CanTransferTo bit NOT NULL
```

Create `IX_AccountDetailsRetrievals_BrokerEnvironment_RetrievedAtUtc` in
descending retrieval order for latest reads and retention deletion. Create a
unique `UX_AccountDetailsAccounts_Retrieval_AccountId` to prevent duplicate
rows inside one response, plus an index on `AccountId` only if a confirmed
per-account history query is introduced. Persist the header and all rows in a
single transaction after the IG request completes. No row becomes visible if
the response has fewer than the expected parsed accounts or any insert fails.

Use the existing `Retention:OperationalRecordsDays` setting initially, with
the same 90-day default. Add set-based deletion of expired retrieval headers;
the foreign key cascades to account rows. Retention must never delete the
latest successful retrieval for a broker environment, even when its date is
older than the cutoff, otherwise the page regresses to empty during a broker
outage. Document this exception and add it to the retention query. A separate
account-details retention setting can be added later only if retention needs
different operational ownership.

Migration impact is additive: add entity classes, two `DbSet` properties,
model mappings, a generated EF migration, and the model snapshot. Existing
proof and login-history data require no backfill because their retained fields
do not satisfy the full account contract.

### Vertical Slices

1. Add Application contracts, account DTOs, typed refresh outcomes, latest
   reader, snapshot store, and coordinator. Keep one top-level C# type per
   file under a new `Features/AccountDetails` slice.
2. Add Infrastructure's IG session-backed account gateway, EF entities/store,
   cross-replica refresh lease, registrations, migration, and retention.
3. Add API request/response mappings and the viewer GET/operator POST routes
   to `PlatformEndpoints`.
4. Add the Web API client methods, view models/presenter, `/account-details`
   page, scoped command, and viewer navigation item.
5. Add test coverage and update the applicable wiki runtime, authorization,
   persistence, and local-operation pages before declaring delivery complete.

## Test Matrix

| Layer | Required checks |
| --- | --- |
| Application | Validate supplied payload mapping for both accounts; reject missing required fields, duplicate IDs, empty results, and Live; verify all typed outcomes and same-process coalescing; assert no external call for GET |
| Infrastructure IG | Assert a new session is created per refresh; GET accounts uses Version `1`, CST, X-SECURITY-TOKEN, and API key; map HTTP/network/JSON failures; verify session headers never enter persisted or logged payloads |
| Infrastructure EF | Persist retrieval plus exactly two account rows atomically; latest ordering by environment/date; rollback leaves no header or rows; uniqueness and retained-latest behavior; SQL retention and in-memory fallback |
| API | Viewer can GET and Operator can POST; Viewer cannot POST; unauthenticated requests fail; map success, empty state, blocked target, in-progress, rate limit, timeout, and malformed upstream outcomes to the specified HTTP contract |
| Web | Viewer sees latest/empty/error states and no update command; Operator sees and can use update; update disables while pending; `409` preserves shown data; values, UTC stamp, and both accounts render with stable test IDs |
| Integration/E2E | Start API with test auth and scripted IG HTTP responses; execute POST then GET; prove a second replica or competing request does not make a second accounts call; validate migration against SQL Server |

Use existing focused test anchors:

* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/Integrations/Ig/IgBrokerAuthenticationGatewayTests.cs`
* `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/EfPlatformIgLoginSnapshotStoreTests.cs`
* `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/TriggerManualAuthRetry/TriggerManualAuthRetryHandlerTests.cs`
* `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Features/GetIgLoginHistory/GetIgLoginHistoryHandlerTests.cs`
* `test/TNC.Trading.Platform.Web/TNC.Trading.Platform.Web.UnitTests/IgLoginHistoryTests.cs`

## Risks and Mitigations

| Risk | Mitigation |
| --- | --- |
| IG reference fields or version changes | Revalidate the representative-payload mapper against the IG reference before implementation and keep deserialization explicit |
| API quota consumption from repeated updates | Operator-only POST, button disabling, in-process coalescing, cross-replica lease, and explicit `429` behavior |
| Session-token disclosure | Keep CST/X-SECURITY-TOKEN transport-local; prohibit raw request/response persistence and redact error diagnostics |
| Historical table growth | Reuse operational retention, cascade deletes, set-based SQL deletion, and retain the newest success |
| Inconsistent partial account response | Parse and validate before opening the persistence transaction, then commit the header and all rows atomically |
| Cross-replica races | Use `sp_getapplock`, not only a static `SemaphoreSlim`; report remote work as in progress rather than issuing a duplicate IG login |

## Clarifying Questions

* Is a manually refreshed historical retrieval required to show the initiating
  operator name, or is a generic `Manual` trigger source sufficient? The
  recommended schema allows either without changing the account contract.
* Should the page eventually expose historical retrieval navigation, or only
  retain history for audit and recovery? The recommended implementation reads
  only the latest retrieval, keeping the initial UI narrow.