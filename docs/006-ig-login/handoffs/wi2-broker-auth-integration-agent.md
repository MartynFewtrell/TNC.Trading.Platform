# Handoff: Work Item 2 — Broker Auth Integration Agent

## Target agent

Broker Auth Integration Agent

## Work item reference

`docs/006-ig-login/plans/002-real-ig-demo-connection-delivery-plan.md` — Work Item 2 (all tasks and steps)

## Delivery context

- Branch: `006-ig-login`
- Baseline: `dotnet build` clean; 193/193 unit tests pass
- Work Item 1 is **complete**: `IIgSessionClient`, `IgSessionClient`, proof-data models, DI registration, and sanitizer extension are all in place

## Scope boundaries — read carefully

**In scope for this handoff:**

- Replace the two simulated `IgAuthenticateResponse` construction sites in `PlatformStateCoordinator.cs` with real `IIgSessionClient.AuthenticateAsync` calls
- Extend `ProtectedCredentialService` to decrypt and return credential values for runtime use (read path only — the write path already exists)
- Inject `IIgSessionClient` and the extended `ProtectedCredentialService` into `PlatformStateCoordinator`
- Classify IG error responses (invalid credentials, rejected auth, timeout, throttle, unreachable) into the existing `TransitionToDegradedAsync` failure path
- Ensure ephemeral session tokens (`CST`, `X-SECURITY-TOKEN`) are held only in memory and never appear in persisted records, API responses, or logs
- Deterministic unit/integration tests using a fake `HttpMessageHandler` for all success, failure, and edge-case paths
- Opt-in real-IG smoke path documented and guarded by user secrets or environment variables

**Out of scope — do not touch:**

- `GetAccountsAsync` / `GetPositionsAsync` orchestration — that is Work Item 3
- Any changes to status API contracts, Blazor pages, or snapshot persistence schema
- `PlatformAuthSupervisor.cs` — scheduler-driven supervision behavior must remain unchanged

## Key files to read before starting

| File | What to note |
| ---- | ------------ |
| `src/TNC.Trading.Platform.Application/Services/PlatformStateCoordinator.cs` | Two simulated login sites (see below); injected dependencies; `TransitionToDegradedAsync` for failure routing |
| `src/TNC.Trading.Platform.Application/Infrastructure/Ig/IIgSessionClient.cs` | The interface to call — `AuthenticateAsync(IgAuthenticateRequest, CancellationToken)` |
| `src/TNC.Trading.Platform.Application/Infrastructure/Ig/IgAuthenticateRequest.cs` | Takes `BrokerEnvironmentKind`, `ApiKey`, `Identifier`, `Password` |
| `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/ProtectedCredentialService.cs` | Has `GetPresenceAsync` and `UpdateAsync`; needs a new `GetCredentialsAsync` decrypt method |
| `src/TNC.Trading.Platform.Application/Configuration/CredentialPresence.cs` | `IsComplete` = `HasApiKey && HasIdentifier && HasPassword` |
| `src/TNC.Trading.Platform.Application/Configuration/PlatformConfigurationSnapshot.cs` | Has `Credentials: CredentialPresence` and `BrokerEnvironment: BrokerEnvironmentKind` |
| `src/TNC.Trading.Platform.Application/Services/PlatformStateCoordinator.cs` (lines 170–180) | `TransitionToDegradedAsync` is the correct failure landing path |

## Deliverables

### 1. Replace simulated login sites with real broker auth

#### Site 1 — `TransitionToActiveAsync` (~line 338–407)

Current code (exact block to replace):

```csharp
var authAttemptCorrelationId = CreateCorrelationId();
await RecordAuthAttemptAsync(currentConfiguration, retryCycleId, authAttemptCorrelationId, cancellationToken).ConfigureAwait(false);
var simulatedResponse = new IgAuthenticateResponse(
    "configured-demo-session",
    null,
    null,
    wasDegraded ? "cst-token" : null,
    wasDegraded ? "security-token" : null,
    new Dictionary<string, string?>
    {
        ["CST"] = wasDegraded ? "cst-token" : null,
        ["X-SECURITY-TOKEN"] = wasDegraded ? "security-token" : null,
        ["Version"] = "3"
    });
var sanitizedAuthResponse = IgAuthenticationResponseSanitizer.Sanitize(simulatedResponse);
var successfulSnapshot = await CaptureSuccessfulLoginSnapshotAsync(currentConfiguration, now, simulatedResponse, cancellationToken).ConfigureAwait(false);
```

Replace with a real call:

```csharp
var authAttemptCorrelationId = CreateCorrelationId();
await RecordAuthAttemptAsync(currentConfiguration, retryCycleId, authAttemptCorrelationId, cancellationToken).ConfigureAwait(false);
var credentials = await protectedCredentialService.GetCredentialsAsync(currentConfiguration.BrokerEnvironment, cancellationToken).ConfigureAwait(false);
var authRequest = new IgAuthenticateRequest(currentConfiguration.BrokerEnvironment, credentials.ApiKey, credentials.Identifier, credentials.Password);
IgAuthenticateResponse authResponse;
try
{
    authResponse = await igSessionClient.AuthenticateAsync(authRequest, cancellationToken).ConfigureAwait(false);
}
catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException { InnerException: TimeoutException })
{
    await TransitionToDegradedAsync(currentConfiguration, currentState, ClassifyAuthFailure(ex), cancellationToken).ConfigureAwait(false);
    return;
}
var sanitizedAuthResponse = IgAuthenticationResponseSanitizer.Sanitize(authResponse);
var successfulSnapshot = await CaptureSuccessfulLoginSnapshotAsync(currentConfiguration, now, authResponse, cancellationToken).ConfigureAwait(false);
```

#### Site 2 — `CaptureSuccessfulLoginSnapshotAsync(bool includeTokenValues)` overload (~line 561–582)

This is the overload called from `AttemptImmediateRecoveryAsync`. The `bool` parameter overload currently builds a fake `IgAuthenticateResponse` internally. Replace its **entire body** so it calls the client directly:

```csharp
private async Task<IgLoginSnapshot> CaptureSuccessfulLoginSnapshotAsync(
    PlatformConfigurationSnapshot currentConfiguration,
    DateTimeOffset capturedAtUtc,
    bool wasDegraded,
    CancellationToken cancellationToken)
{
    var credentials = await protectedCredentialService.GetCredentialsAsync(currentConfiguration.BrokerEnvironment, cancellationToken).ConfigureAwait(false);
    var authRequest = new IgAuthenticateRequest(currentConfiguration.BrokerEnvironment, credentials.ApiKey, credentials.Identifier, credentials.Password);
    var authResponse = await igSessionClient.AuthenticateAsync(authRequest, cancellationToken).ConfigureAwait(false);
    return await CaptureSuccessfulLoginSnapshotAsync(currentConfiguration, capturedAtUtc, authResponse, cancellationToken).ConfigureAwait(false);
}
```

Note: callers of this overload already check `currentConfiguration.Credentials.IsComplete` before calling it, so no additional credential guard is needed here.

### 2. `ProtectedCredentialService.GetCredentialsAsync`

Add the following public method to `ProtectedCredentialService`:

```csharp
public async Task<IgCredentials> GetCredentialsAsync(BrokerEnvironmentKind brokerEnvironment, CancellationToken cancellationToken)
{
    var entities = await dbContext.ProtectedCredentials
        .Where(item => item.BrokerEnvironment == brokerEnvironment.ToString())
        .ToListAsync(cancellationToken)
        .ConfigureAwait(false);

    string? Decrypt(string type) =>
        entities.FirstOrDefault(e => string.Equals(e.CredentialType, type, StringComparison.Ordinal)) is { } entity
            ? protector.Unprotect(entity.ProtectedValue)
            : null;

    return new IgCredentials(
        Decrypt("ApiKey") ?? string.Empty,
        Decrypt("Identifier") ?? string.Empty,
        Decrypt("Password") ?? string.Empty);
}
```

Create `src/TNC.Trading.Platform.Application/Configuration/IgCredentials.cs`:

```csharp
namespace TNC.Trading.Platform.Application.Configuration;

internal sealed record IgCredentials(
    string ApiKey,
    string Identifier,
    string Password);
```

### 3. `ClassifyAuthFailure` helper in `PlatformStateCoordinator`

Add a private static helper to map `Exception` to a human-readable, secret-safe failure summary string. This string feeds directly into `TransitionToDegradedAsync`:

```csharp
private static string ClassifyAuthFailure(Exception ex)
{
    return ex switch
    {
        HttpRequestException { StatusCode: System.Net.HttpStatusCode.Unauthorized } =>
            "IG authentication failed: invalid or rejected credentials.",
        HttpRequestException { StatusCode: System.Net.HttpStatusCode.Forbidden } =>
            "IG authentication failed: access forbidden.",
        HttpRequestException { StatusCode: System.Net.HttpStatusCode.TooManyRequests } =>
            "IG authentication failed: request rate limit exceeded.",
        HttpRequestException { StatusCode: not null } =>
            "IG authentication failed: unexpected broker response.",
        TaskCanceledException or OperationCanceledException =>
            "IG authentication failed: request timed out.",
        HttpRequestException =>
            "IG authentication failed: broker is unreachable.",
        _ =>
            "IG authentication failed: unexpected error."
    };
}
```

### 4. `TransitionToDegradedAsync` overload for auth failures

The existing `TransitionToDegradedAsync` is called with a missing-credentials reason. Add a new private overload (or a shared path) that accepts an externally classified failure summary so auth failures from the real client flow through the same retry/degraded machinery. Review the existing `TransitionToDegradedAsync` implementation carefully and ensure the new failure summary parameter is used in place of `MissingCredentialsBlockedReason` without changing the existing missing-credentials path.

### 5. Inject dependencies into `PlatformStateCoordinator`

Update the primary constructor parameters:

```csharp
internal sealed class PlatformStateCoordinator(
    IConfiguration configuration,
    PlatformConfigurationService platformConfigurationService,
    IPlatformRuntimeStateStore runtimeStateStore,
    IPlatformIgLoginSnapshotStore igLoginSnapshotStore,
    IPlatformRetryCycleStore retryCycleStore,
    IPlatformEventStore eventStore,
    INotificationDispatcher notificationDispatcher,
    TradingScheduleGate tradingScheduleGate,
    IIgSessionClient igSessionClient,           // NEW
    ProtectedCredentialService protectedCredentialService, // NEW
    TimeProvider timeProvider,
    ILogger<PlatformStateCoordinator> logger)
```

### 6. Register `ProtectedCredentialService` as scoped in DI (if not already)

Check `PlatformInfrastructureServiceCollectionExtensions.cs` — `ProtectedCredentialService` is already registered as `AddScoped`. No change needed.

Check `PlatformApplicationServiceCollectionExtensions.cs` — `PlatformStateCoordinator` must be registered in a scope that receives both the new injected services. Confirm no change is needed.

### 7. Unit tests

#### Application unit tests — `PlatformStateCoordinator` auth paths

Create or extend tests in `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/` covering the real auth integration. The test project uses in-memory EF and the existing test infrastructure. Check the existing test class structure before deciding whether to add to an existing class or create a new one.

Required test cases:

```
TickAsync_WhenCredentialsCompleteAndIgReturnsSuccess_ShouldTransitionToActive
TickAsync_WhenCredentialsCompleteAndIgReturnsUnauthorized_ShouldTransitionToDegraded
TickAsync_WhenCredentialsCompleteAndIgIsUnreachable_ShouldTransitionToDegraded
TickAsync_WhenCredentialsCompleteAndIgReturnsSuccess_ShouldNotPersistTokenValues
TickAsync_WhenCredentialsCompleteAndIgTimesOut_ShouldTransitionToDegraded
TickAsync_WhenCredentialsCompleteAndIgReturnsTooManyRequests_ShouldTransitionToDegraded
TickAsync_WhenPlatformEnvironmentIsTestAndBrokerEnvironmentIsLive_ShouldRemainBlocked
```

Each test must use a fake `IIgSessionClient` (implement the interface inline — do not add Moq/NSubstitute unless already present). Include requirement traceability comments (`// Traces to FR1, FR2, SR2, SR3, TR1, TR2, TR5, TR6`).

#### Infrastructure unit tests — `ProtectedCredentialService.GetCredentialsAsync`

Add to the existing `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/` project:

```
GetCredentialsAsync_WhenAllCredentialsPresent_ShouldReturnDecryptedValues
GetCredentialsAsync_WhenCredentialMissing_ShouldReturnEmptyStringForMissingValue
```

Include requirement traceability comments (`// Traces to SR3, NF3`).

### 8. Session token handling rules

The session tokens (`CST` and `X-SECURITY-TOKEN`) returned by a successful `AuthenticateAsync` call:

- **Must** be held in `IgAuthenticateResponse.ClientSessionToken` and `AccountSecurityToken` in memory only during the current request/tick
- **Must** pass through `IgAuthenticationResponseSanitizer.Sanitize` before any use in persisted records, API responses, or log payloads
- **Must not** be stored on `PlatformRuntimeState` or any other persisted entity
- **May** be passed to `GetAccountsAsync` / `GetPositionsAsync` in Work Item 3, but that is deferred; in this work item simply discard the live token values after the snapshot is captured

## Validation gates

After all deliverables are complete:

1. Run `dotnet build` — must succeed with zero errors
2. Run `dotnet test --filter "FullyQualifiedName!~IntegrationTests&FullyQualifiedName!~E2ETests&FullyQualifiedName!~FunctionalTests"` — all unit tests must pass, including all newly added tests
3. Confirm `PlatformAuthSupervisor.cs` has **not** been modified
4. Confirm no session token values appear in any `PlatformRuntimeState` property or any persisted entity
5. Confirm the blocked-live test case (`PlatformEnvironmentKind.Test` + `BrokerEnvironmentKind.Live`) still passes

## Assumptions to validate

- `BrokerEnvironmentKind.Demo` is the correct enum value for the IG Demo environment (there is no `Test` value on this enum — verify before coding)
- `ProtectedCredentialService` is already accessible as a constructor parameter in the scope that creates `PlatformStateCoordinator`; if it is not, add it to the DI registration

## Agent report

### Files created

- `src/TNC.Trading.Platform.Application/Configuration/IgCredentials.cs`
- `src/TNC.Trading.Platform.Application/Configuration/IProtectedCredentialService.cs`

### Files modified

- `src/TNC.Trading.Platform.Application/Services/PlatformStateCoordinator.cs`
- `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/ProtectedCredentialService.cs`
- `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/PlatformInfrastructureServiceCollectionExtensions.cs`
- `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/SqlPlatformConfigurationStore.cs`
- `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/AuthRetryCycleTests.cs`
- `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/ProtectedCredentialServiceTests.cs`
- `docs/wiki/runtime-behavior.md`
- `docs/wiki/operator-guide.md`
- `docs/006-ig-login/handoffs/wi2-broker-auth-integration-agent.md`

### Unit tests added

Added 8 unit tests.

| Test class | Added tests |
| --- | ---: |
| `AuthRetryCycleTests` | 6 |
| `ProtectedCredentialServiceTests` | 2 |

### Build gate outcome

`dotnet build` succeeded in `3.1s`.

### Test gate outcome

`dotnet test --filter "FullyQualifiedName!~IntegrationTests&FullyQualifiedName!~E2ETests&FullyQualifiedName!~FunctionalTests"` passed. Test summary: `total: 201, failed: 0, succeeded: 201, skipped: 0, duration: 6.0s`.

### Assumptions validated

- `BrokerEnvironmentKind.Demo` confirmed as the IG Demo enum.
- DI scope was available, but direct concrete injection from Application was not viable without a circular reference; resolved with `IProtectedCredentialService`.

### Deviations from handoff

- Used `IProtectedCredentialService` instead of injecting `ProtectedCredentialService` directly into `PlatformStateCoordinator`. Required to keep Application independent from Infrastructure.

### Escalations

- None. Ready for next handoff.
