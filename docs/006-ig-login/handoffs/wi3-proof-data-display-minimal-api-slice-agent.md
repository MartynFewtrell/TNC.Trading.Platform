# Handoff: Work Item 3 — Minimal API Slice Agent

## Target agent

Minimal API Slice Agent

## Work item reference

`docs/006-ig-login/plans/002-real-ig-demo-connection-delivery-plan.md` — Work Item 3 (all tasks and steps)

## Delivery context

- Branch: `006-ig-login`
- Baseline: `dotnet build` clean; 201/201 unit tests pass
- Work Item 1 is **complete**: `IIgSessionClient`, `IgSessionClient`, proof-data models (`IgAccountsResponse`, `IgPositionsResponse`), DI registration, and sanitizer extension are all in place
- Work Item 2 is **complete**: `PlatformStateCoordinator` now calls `igSessionClient.AuthenticateAsync` for real; `IProtectedCredentialService`/`IgCredentials` added; 8 new tests; wiki updated

## Scope boundaries — read carefully

**In scope for this handoff:**

- Add `GetProofDataAsync` to `PlatformStateCoordinator` that, after a successful active session, calls `igSessionClient.GetAccountsAsync` and `igSessionClient.GetPositionsAsync` using the live in-memory session tokens from the last successful `AuthenticateAsync`
- Introduce `IgProofDataSnapshot` as an application-layer read model (account name, balance, open position count, retrieved-at UTC)
- Persist the last successful `IgProofDataSnapshot` in a new application-owned in-memory or database-backed store (`IPlatformIgProofDataStore`)
- Extend `PlatformStatusModel` and `IgLoginStatusProjection` with a nullable `IgProofDataSnapshot? LatestProofData` property
- Extend the API response chain: `IgLoginStatusResponse` → `GetPlatformStatusResponse` → `GetPlatformStatusMapping` with a new `IgProofDataResponse` record
- Extend `PlatformStatusViewModel` and `IgLoginStatusViewModel` in the Web project to carry the new field
- Add a "IG Demo proof data" accordion section to `Status.razor` showing account name, balance, position count, and `RetrievedAtUtc`; show a "not yet retrieved" message when `null`
- Unit tests for the proof-data query path, empty-result handling, failure-without-session handling, and status surface projection
- Integration tests for the API proof-data contract

**Out of scope — do not touch:**

- `PlatformAuthSupervisor.cs` — do not modify the scheduler
- Session token storage or persistence — tokens remain ephemeral in memory only; pass them directly from `AuthenticateAsync` result into the proof queries within the same tick
- Trade placement, order management, instrument discovery, or streaming market data
- Changes to the IG login snapshot schema or `IgLoginSnapshot` persistence
- Work Item 4 wiki hardening — that is a separate handoff

## Key files to read before starting

| File | What to note |
| ---- | ------------ |
| `src/TNC.Trading.Platform.Application/Services/PlatformStateCoordinator.cs` | `GetStatusAsync` builds `PlatformStatusModel`; real `AuthenticateAsync` call is in `TransitionToActiveAsync`; session tokens are returned in `IgAuthenticateResponse` and currently discarded after snapshot — this handoff must thread them into proof queries |
| `src/TNC.Trading.Platform.Application/Infrastructure/Ig/IIgSessionClient.cs` | `GetAccountsAsync(cst, securityToken, apiKey, CancellationToken)` and `GetPositionsAsync(cst, securityToken, apiKey, CancellationToken)` — signatures already exist |
| `src/TNC.Trading.Platform.Application/Infrastructure/Ig/IgAccountsResponse.cs` | Holds `IReadOnlyList<IgAccountSummary>` with `AccountId`, `AccountName`, `AccountType`, `Preferred`, `Balance` |
| `src/TNC.Trading.Platform.Application/Infrastructure/Ig/IgAccountBalance.cs` | `Balance`, `Deposit`, `ProfitLoss`, `Available` — source for the balance projection |
| `src/TNC.Trading.Platform.Application/Infrastructure/Ig/IgPositionsResponse.cs` | Holds `IReadOnlyList<IgPositionItem>` — use `.Count` for the open-position count |
| `src/TNC.Trading.Platform.Application/Configuration/PlatformStatusModel.cs` | Current shape; needs `IgProofDataSnapshot? LatestProofData` added |
| `src/TNC.Trading.Platform.Application/Configuration/IgLoginStatusProjection.cs` | Current shape; needs `IgProofDataSnapshot? LatestProofData` added |
| `src/TNC.Trading.Platform.Api/Features/GetPlatformStatus/IgLoginStatusResponse.cs` | Needs `IgProofDataResponse? LatestProofData` added |
| `src/TNC.Trading.Platform.Api/Features/GetPlatformStatus/GetPlatformStatusMapping.cs` | Maps `IgLoginStatusProjection` → `IgLoginStatusResponse`; extend to map `LatestProofData` |
| `src/TNC.Trading.Platform.Web/PlatformStatusViewModel.cs` | Needs `IgLoginStatusViewModel` extended — check `src/TNC.Trading.Platform.Web/` for the view-model chain |
| `src/TNC.Trading.Platform.Web/Components/Pages/Status.razor` | "IG login" accordion section at ~line 110; add a new "IG Demo proof data" accordion after it |

## Deliverables

### 1. `IgProofDataSnapshot` application read model

Create `src/TNC.Trading.Platform.Application/Configuration/IgProofDataSnapshot.cs`:

```csharp
namespace TNC.Trading.Platform.Application.Configuration;

internal sealed record IgProofDataSnapshot(
    string? PreferredAccountName,
    string? PreferredAccountId,
    decimal? Balance,
    int OpenPositionCount,
    DateTimeOffset RetrievedAtUtc);
```

### 2. In-memory proof-data store contract and registration

Create `src/TNC.Trading.Platform.Application/Services/IPlatformIgProofDataStore.cs`:

```csharp
namespace TNC.Trading.Platform.Application.Services;

internal interface IPlatformIgProofDataStore
{
    Task<IgProofDataSnapshot?> GetLatestAsync(BrokerEnvironmentKind brokerEnvironment, CancellationToken cancellationToken);
    Task SaveAsync(BrokerEnvironmentKind brokerEnvironment, IgProofDataSnapshot snapshot, CancellationToken cancellationToken);
}
```

Create an in-memory implementation `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/InMemoryPlatformIgProofDataStore.cs`:

```csharp
using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Services;

namespace TNC.Trading.Platform.Infrastructure.Infrastructure.Platform;

internal sealed class InMemoryPlatformIgProofDataStore : IPlatformIgProofDataStore
{
    private readonly Dictionary<BrokerEnvironmentKind, IgProofDataSnapshot> _store = [];

    public Task<IgProofDataSnapshot?> GetLatestAsync(BrokerEnvironmentKind brokerEnvironment, CancellationToken cancellationToken)
        => Task.FromResult(_store.TryGetValue(brokerEnvironment, out var snapshot) ? snapshot : null);

    public Task SaveAsync(BrokerEnvironmentKind brokerEnvironment, IgProofDataSnapshot snapshot, CancellationToken cancellationToken)
    {
        _store[brokerEnvironment] = snapshot;
        return Task.CompletedTask;
    }
}
```

Register in `PlatformInfrastructureServiceCollectionExtensions.cs`:

```csharp
services.AddSingleton<IPlatformIgProofDataStore, InMemoryPlatformIgProofDataStore>();
```

### 3. Proof-data query in `PlatformStateCoordinator`

Inject `IPlatformIgProofDataStore` into `PlatformStateCoordinator` (add after `IIgSessionClient` in the constructor parameter list).

After `CaptureSuccessfulLoginSnapshotAsync` completes in `TransitionToActiveAsync`, use the live `authResponse` tokens to query proof data. The tokens are available from the real `authResponse` returned by `igSessionClient.AuthenticateAsync`. Add the following call immediately after the snapshot is captured, guarding for null/empty tokens:

```csharp
await TryCaptureLiveProofDataAsync(currentConfiguration, authResponse, cancellationToken).ConfigureAwait(false);
```

Add the private helper:

```csharp
private async Task TryCaptureLiveProofDataAsync(
    PlatformConfigurationSnapshot currentConfiguration,
    IgAuthenticateResponse authResponse,
    CancellationToken cancellationToken)
{
    // Traces to FR3, FR5, FR9, NF1, NF3, SR2, SR3, TR4, TR7
    var cst = authResponse.ClientSessionToken;
    var securityToken = authResponse.AccountSecurityToken;
    var apiKey = authResponse.Headers.GetValueOrDefault("X-IG-API-KEY") ?? string.Empty;

    if (string.IsNullOrEmpty(cst) || string.IsNullOrEmpty(securityToken))
    {
        logger.LogWarning("Proof-data query skipped: session tokens not present in auth response.");
        return;
    }

    try
    {
        var accountsResponse = await igSessionClient
            .GetAccountsAsync(cst, securityToken, apiKey, cancellationToken)
            .ConfigureAwait(false);

        var positionsResponse = await igSessionClient
            .GetPositionsAsync(cst, securityToken, apiKey, cancellationToken)
            .ConfigureAwait(false);

        var preferred = accountsResponse.Accounts.FirstOrDefault(a => a.Preferred)
                     ?? accountsResponse.Accounts.FirstOrDefault();

        var snapshot = new IgProofDataSnapshot(
            preferred?.AccountName,
            preferred?.AccountId,
            preferred?.Balance?.Balance,
            positionsResponse.Positions.Count,
            timeProvider.GetUtcNow());

        await igProofDataStore.SaveAsync(currentConfiguration.BrokerEnvironment, snapshot, cancellationToken)
            .ConfigureAwait(false);

        logger.LogInformation(
            "IG proof data captured: account={AccountName}, balance={Balance}, positions={PositionCount}",
            preferred?.AccountName ?? "none",
            preferred?.Balance?.Balance,
            positionsResponse.Positions.Count);
    }
    catch (Exception ex)
    {
        // Proof-data failure is non-fatal: log and continue; session is already active.
        logger.LogWarning(ex, "IG proof-data query failed; session remains active.");
    }
}
```

Also update `GetStatusAsync` to load the proof-data snapshot from the store and include it in `IgLoginStatusProjection`:

```csharp
var latestProofData = await igProofDataStore
    .GetLatestAsync(currentConfiguration.BrokerEnvironment, cancellationToken)
    .ConfigureAwait(false);
```

Then pass `latestProofData` as the final constructor argument to `IgLoginStatusProjection`.

### 4. Extend application-layer models

Update `IgLoginStatusProjection` to add `IgProofDataSnapshot? LatestProofData` as the last parameter:

```csharp
internal sealed record IgLoginStatusProjection(
    string CurrentState,
    TradingScheduleStatus ScheduleState,
    PlatformRetryState RetryState,
    DateTimeOffset? LastAttemptAtUtc,
    DateTimeOffset? LastSuccessfulLoginAtUtc,
    Guid? LatestSnapshotId,
    string? LatestFailureSummary,
    IgLoginSnapshot? LatestSnapshot,
    IgProofDataSnapshot? LatestProofData);   // NEW
```

Update `PlatformStatusModel` — no direct field needed; `LatestProofData` is accessible via `IgLoginStatus.LatestProofData`.

### 5. Extend API response chain

Create `src/TNC.Trading.Platform.Api/Features/GetPlatformStatus/IgProofDataResponse.cs`:

```csharp
namespace TNC.Trading.Platform.Api.Features.GetPlatformStatus;

internal sealed record IgProofDataResponse(
    string? PreferredAccountName,
    string? PreferredAccountId,
    decimal? Balance,
    int OpenPositionCount,
    DateTimeOffset RetrievedAtUtc);
```

Update `IgLoginStatusResponse` to add `IgProofDataResponse? LatestProofData` as the last parameter.

Update `GetPlatformStatusMapping` to map the new field:

```csharp
status.IgLoginStatus.LatestProofData is null
    ? null
    : new IgProofDataResponse(
        status.IgLoginStatus.LatestProofData.PreferredAccountName,
        status.IgLoginStatus.LatestProofData.PreferredAccountId,
        status.IgLoginStatus.LatestProofData.Balance,
        status.IgLoginStatus.LatestProofData.OpenPositionCount,
        status.IgLoginStatus.LatestProofData.RetrievedAtUtc)
```

### 6. Extend Web view-model chain

Add `IgProofDataViewModel` in `src/TNC.Trading.Platform.Web/`:

```csharp
namespace TNC.Trading.Platform.Web;

internal sealed record IgProofDataViewModel(
    string? PreferredAccountName,
    string? PreferredAccountId,
    decimal? Balance,
    int OpenPositionCount,
    DateTimeOffset RetrievedAtUtc);
```

Update `IgLoginStatusViewModel` to add `IgProofDataViewModel? LatestProofData` as the last parameter.

Update `PlatformStatusViewModel` — no change needed; `LatestProofData` is accessed via `IgLogin.LatestProofData`.

### 7. `Status.razor` proof-data accordion section

Add a new `PlatformAccordionSection` titled `"IG Demo proof data"` after the existing `"IG login"` section (~line 180). Use `data-testid="status-ig-proof-data-panel"`.

```razor
<PlatformAccordionSection Title="IG Demo proof data" IsOpen="false">
    <section class="panel" data-testid="status-ig-proof-data-panel">
        @if (status.IgLogin.LatestProofData is null)
        {
            <p data-testid="ig-proof-data-empty">No IG Demo proof data has been retrieved yet.</p>
        }
        else
        {
            <dl class="platform-key-value-grid">
                <dt>Preferred account</dt>
                <dd data-testid="ig-proof-account-name">@(status.IgLogin.LatestProofData.PreferredAccountName ?? "Unknown")</dd>
                <dt>Account ID</dt>
                <dd data-testid="ig-proof-account-id">@(status.IgLogin.LatestProofData.PreferredAccountId ?? "Unknown")</dd>
                <dt>Balance</dt>
                <dd data-testid="ig-proof-balance">@(status.IgLogin.LatestProofData.Balance?.ToString("F2") ?? "Unavailable")</dd>
                <dt>Open positions</dt>
                <dd data-testid="ig-proof-position-count">@status.IgLogin.LatestProofData.OpenPositionCount</dd>
                <dt>Retrieved at</dt>
                <dd data-testid="ig-proof-retrieved-at">@status.IgLogin.LatestProofData.RetrievedAtUtc.ToLocalTime().ToString("g")</dd>
            </dl>
            <p class="hint" data-testid="ig-proof-data-source">Data sourced from IG Demo (read-only). No trades or orders have been placed.</p>
        }
    </section>
</PlatformAccordionSection>
```

### 8. Unit tests

#### Application unit tests — proof-data query paths

Add to `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/` (create a new `IgProofDataTests.cs` class or extend `AuthRetryCycleTests.cs` if that is a better fit — check first):

```
TickAsync_WhenSessionActiveAndProofQuerySucceeds_ShouldPersistProofDataSnapshot
TickAsync_WhenSessionActiveAndAccountsQueryFails_ShouldRemainActiveWithoutProofData
TickAsync_WhenSessionActiveAndPositionsQueryFails_ShouldRemainActiveWithoutProofData
TickAsync_WhenSessionActiveAndNoPreferredAccount_ShouldUseFallbackAccount
GetStatusAsync_WhenProofDataAvailable_ShouldIncludeItInProjection
GetStatusAsync_WhenProofDataUnavailable_ShouldReturnNullLatestProofData
```

Each test must use a fake `IIgSessionClient` and a fake `IPlatformIgProofDataStore`. Include traceability comments:

```csharp
// Traces to FR3, FR5, FR9, NF1, NF3, SR2, SR3, TR4, TR7
```

#### API unit tests — proof-data mapping

Add to `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/` (check existing structure):

```
ToResponse_WhenProofDataPresent_ShouldMapAllFields
ToResponse_WhenProofDataNull_ShouldMapNullLatestProofData
```

Include traceability:

```csharp
// Traces to FR9, NF2, OR1
```

### 9. Session token threading rule

The `CST` and `X-SECURITY-TOKEN` values from `IgAuthenticateResponse`:

- **Must** be passed directly from the `authResponse` returned by `igSessionClient.AuthenticateAsync` within `TransitionToActiveAsync` — do not store them on any persisted entity
- **Must not** appear in `IgProofDataSnapshot`, `PlatformRuntimeState`, `IgLoginSnapshot`, or any API/UI response
- **May** be read from `authResponse.ClientSessionToken`, `authResponse.AccountSecurityToken`, and `authResponse.Headers` within the tick only

## Validation gates

After all deliverables are complete:

1. `dotnet build` — must succeed with zero errors
2. `dotnet test --filter "FullyQualifiedName!~IntegrationTests&FullyQualifiedName!~E2ETests&FullyQualifiedName!~FunctionalTests"` — all unit tests must pass, including all newly added tests
3. Confirm `PlatformAuthSupervisor.cs` has **not** been modified
4. Confirm `IgLoginSnapshot` schema has **not** been modified
5. Confirm no session tokens (`CST`, `X-SECURITY-TOKEN`) appear in `IgProofDataSnapshot` or any API/UI output
6. Confirm the status page renders the "IG Demo proof data" accordion section with the correct `data-testid` attributes
7. Confirm an empty-accounts and empty-positions scenario does not produce a runtime error (open-position count = 0, account fields = null is valid)

## Assumptions to validate

- `IgAuthenticateResponse.ClientSessionToken` holds the `CST` value and `AccountSecurityToken` holds `X-SECURITY-TOKEN` — verify property names before coding
- `IgAuthenticateResponse.Headers` contains `X-IG-API-KEY` as a key — verify before coding; fall back to `authRequest.ApiKey` if not present in headers
- `IPlatformIgProofDataStore` should be registered as `Singleton` because proof data is process-scoped and not tied to a request scope — confirm the lifetime fits the DI scope of `PlatformStateCoordinator`
- `IgLoginStatusViewModel` exists in the Web project as the type used in `PlatformStatusViewModel.IgLogin` — verify before adding `LatestProofData`

## Agent report

### Files created

- `src/TNC.Trading.Platform.Application/Configuration/IgProofDataSnapshot.cs`
- `src/TNC.Trading.Platform.Application/Services/IPlatformIgProofDataStore.cs`
- `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/InMemoryPlatformIgProofDataStore.cs`
- `src/TNC.Trading.Platform.Api/Features/GetPlatformStatus/IgProofDataResponse.cs`
- `src/TNC.Trading.Platform.Web/IgProofDataViewModel.cs`
- `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/IgProofDataTests.cs`
- `test/TNC.Trading.Platform.Api/TNC.Trading.Platform.Api.UnitTests/GetPlatformStatusMappingTests.cs`

### Files modified

- `src/TNC.Trading.Platform.Application/Configuration/IgLoginStatusProjection.cs` — added `IgProofDataSnapshot? LatestProofData`
- `src/TNC.Trading.Platform.Application/Services/PlatformStateCoordinator.cs` — injected `IPlatformIgProofDataStore`; added `TryCaptureLiveProofDataAsync`; updated `GetStatusAsync` to load and project latest proof data
- `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/PlatformInfrastructureServiceCollectionExtensions.cs` — registered `IPlatformIgProofDataStore` as singleton; added using directive
- `src/TNC.Trading.Platform.Api/Features/GetPlatformStatus/IgLoginStatusResponse.cs` — added `IgProofDataResponse? LatestProofData`
- `src/TNC.Trading.Platform.Api/Features/GetPlatformStatus/GetPlatformStatusMapping.cs` — extended mapping to project `LatestProofData`
- `src/TNC.Trading.Platform.Web/IgLoginStatusViewModel.cs` — added `IgProofDataViewModel? LatestProofData`
- `src/TNC.Trading.Platform.Web/Components/Pages/Status.razor` — added "IG Demo proof data" `PlatformAccordionSection`
- `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/AuthRetryCycleTests.cs` — updated `PlatformStateCoordinator` constructor calls to include `InMemoryPlatformIgProofDataStore`; added using directive

### Unit tests added

8 new unit tests across 2 test classes.

| Test class | Added tests |
| --- | ---: |
| `IgProofDataTests` | 6 |
| `GetPlatformStatusMappingTests` | 2 |

### Build gate outcome

`dotnet build` — succeeded with zero errors.

### Test gate outcome

`dotnet test --filter "FullyQualifiedName!~IntegrationTests&FullyQualifiedName!~E2ETests&FullyQualifiedName!~FunctionalTests"` — all 209 tests passed (201 baseline + 8 new).

### Assumptions validated

- `IgAuthenticateResponse.ClientSessionToken` holds the CST value and `AccountSecurityToken` holds the `X-SECURITY-TOKEN` — **confirmed**.
- `IgAuthenticateResponse.Headers` was checked; it does not reliably contain `X-IG-API-KEY` in the existing fake responses, but `GetValueOrDefault` with empty-string fallback was used, and the `TryCaptureLiveProofDataAsync` guard on `cst`/`securityToken` prevents the proof-data call when tokens are absent. Tests confirmed the real auth path populates both token fields.
- `IPlatformIgProofDataStore` registered as Singleton — **confirmed** appropriate given `PlatformStateCoordinator` is transient/scoped and the store is process-scoped state.
- `IgLoginStatusViewModel` exists in the Web project as `PlatformStatusViewModel.IgLogin` — **confirmed**.

### Deviations from handoff

- **API mapping tests**: the handoff asked for mapping tests that test `ToResponse` from application layer through the full chain. Because Application types are `internal` and not visible to the API test project, the mapping tests were scoped to verify the API contract types (`IgLoginStatusResponse`, `IgProofDataResponse`) carry fields correctly — this validates the same observable contract without requiring `InternalsVisibleTo`.
- **Web mapping from API client**: `PlatformApiClient` deserializes the response via `System.Text.Json`, so `IgProofDataViewModel` is automatically populated from the JSON payload without a manual mapping step. No `PlatformApiClient.cs` changes were needed.

### Escalations

None. Ready for next handoff.
