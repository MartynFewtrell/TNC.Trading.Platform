# Handoff: Work Item 1 Tasks 2 & 3 — Broker Auth Integration Agent

## Target agent

Broker Auth Integration Agent

## Work item reference

`docs/006-ig-login/plans/002-real-ig-demo-connection-delivery-plan.md` — Work Item 1, Tasks 2 and 3

## Delivery context

- Branch: `006-ig-login`
- Baseline: `dotnet build` succeeds; 172/172 unit tests pass
- Work Item 1 Task 1 (documentation alignment) must be complete before this handoff is executed, but the code changes below do not depend on document content

## Scope boundaries — read carefully

**In scope for this handoff:**

- `IIgSessionClient` interface in the Application layer
- Proof-data response models in the Application layer
- `IgSessionClient` real typed `HttpClient` implementation in the Infrastructure layer
- Typed `HttpClient` DI registration
- Extending `IgAuthenticationResponseSanitizer` to redact `X-IG-API-KEY`
- Unit tests for request construction, header shape, redaction, and HTTP error classification

**Out of scope — do not touch:**

- `PlatformStateCoordinator.cs` — the simulated login stays until Work Item 2
- Proof-data query orchestration (calling `GetAccountsAsync` / `GetPositionsAsync` at runtime) — that is Work Item 3
- Any changes to existing snapshot persistence, API contracts, or Blazor pages

## Existing contracts — do not delete, only extend

All of the following files already exist. Read them before making changes.

| File | Purpose |
| ---- | ------- |
| `src/TNC.Trading.Platform.Application/Infrastructure/Ig/IgAuthenticateRequest.cs` | Takes `BrokerEnvironmentKind`, `ApiKey`, `Identifier`, `Password` |
| `src/TNC.Trading.Platform.Application/Infrastructure/Ig/IgAuthenticateResponse.cs` | Has `CurrentAccountId`, `LightstreamerEndpoint`, `ExpiresAtUtc`, `ClientSessionToken`, `AccountSecurityToken`, `Headers` |
| `src/TNC.Trading.Platform.Application/Infrastructure/Ig/IgAuthenticationResponseSanitizer.cs` | Redacts sensitive headers; needs `X-IG-API-KEY` added |
| `src/TNC.Trading.Platform.Application/Infrastructure/Ig/IgLoginSnapshotMapper.cs` | Maps response to `IgLoginSnapshot` |
| `src/TNC.Trading.Platform.Application/Infrastructure/Ig/SanitizedIgAuthenticateResponse.cs` | Sanitized response record |
| `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/PlatformInfrastructureServiceCollectionExtensions.cs` | DI registration — add typed `HttpClient` here |

## IG REST API reference

| Call | Method | Path | Required request headers | Version header | Notes |
| ---- | ------ | ---- | ------------------------ | -------------- | ----- |
| Create session | `POST` | `/session` | `X-IG-API-KEY: {apiKey}` | `Version: 3` | JSON body: `{ "identifier": "...", "password": "...", "encryptedPassword": false }` |
| Get accounts | `GET` | `/accounts` | `X-IG-API-KEY`, `CST`, `X-SECURITY-TOKEN` | `Version: 1` | Read-only |
| Get positions | `GET` | `/positions` | `X-IG-API-KEY`, `CST`, `X-SECURITY-TOKEN` | `Version: 2` | Read-only |

Session tokens are returned as **response headers**: `CST` and `X-SECURITY-TOKEN`. These must be captured from the response and stored ephemerally in memory only; they must never appear in logs, persisted records, API responses, or UI output.

Demo base URL: `https://demo-api.ig.com/gateway/deal/`

## Deliverable 1 — `IIgSessionClient` interface

Create `src/TNC.Trading.Platform.Application/Infrastructure/Ig/IIgSessionClient.cs`.

```csharp
namespace TNC.Trading.Platform.Application.Infrastructure.Ig;

internal interface IIgSessionClient
{
    Task<IgAuthenticateResponse> AuthenticateAsync(
        IgAuthenticateRequest request,
        CancellationToken cancellationToken);

    Task<IgAccountsResponse> GetAccountsAsync(
        string cst,
        string securityToken,
        string apiKey,
        CancellationToken cancellationToken);

    Task<IgPositionsResponse> GetPositionsAsync(
        string cst,
        string securityToken,
        string apiKey,
        CancellationToken cancellationToken);
}
```

## Deliverable 2 — Proof-data response models

Create the following files in `src/TNC.Trading.Platform.Application/Infrastructure/Ig/`.

### `IgAccountBalance.cs`

```csharp
namespace TNC.Trading.Platform.Application.Infrastructure.Ig;

internal sealed record IgAccountBalance(
    decimal Balance,
    decimal Deposit,
    decimal ProfitLoss,
    decimal Available);
```

### `IgAccountSummary.cs`

```csharp
namespace TNC.Trading.Platform.Application.Infrastructure.Ig;

internal sealed record IgAccountSummary(
    string AccountId,
    string AccountName,
    string AccountType,
    bool Preferred,
    IgAccountBalance? Balance);
```

### `IgAccountsResponse.cs`

```csharp
namespace TNC.Trading.Platform.Application.Infrastructure.Ig;

internal sealed record IgAccountsResponse(
    IReadOnlyList<IgAccountSummary> Accounts);
```

### `IgPositionItem.cs`

```csharp
namespace TNC.Trading.Platform.Application.Infrastructure.Ig;

internal sealed record IgPositionItem(
    decimal? ContractSize,
    string? CreatedDate,
    string? Currency,
    string? DealId,
    decimal? Size,
    string? Direction);
```

### `IgPositionsResponse.cs`

```csharp
namespace TNC.Trading.Platform.Application.Infrastructure.Ig;

internal sealed record IgPositionsResponse(
    IReadOnlyList<IgPositionItem> Positions);
```

## Deliverable 3 — `IgSessionClient` implementation

Create `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/Ig/IgSessionClient.cs`.

Requirements for the implementation:

- Takes an injected `HttpClient` (the typed client registered in DI)
- Uses `System.Text.Json` for serialisation/deserialisation with `JsonSerializerOptions` set to `PropertyNameCaseInsensitive = true`
- `AuthenticateAsync`:
  - Sends `POST /session` with `Content-Type: application/json`, `X-IG-API-KEY: {request.ApiKey}`, `Version: 3`
  - JSON body: `{ "identifier": "...", "password": "...", "encryptedPassword": false }`
  - Throws `HttpRequestException` (with the `HttpStatusCode` as context) for any non-2xx response
  - Reads `CST` and `X-SECURITY-TOKEN` from the **response headers** and maps them into the returned `IgAuthenticateResponse.ClientSessionToken`, `AccountSecurityToken`, and the `Headers` dictionary
  - Maps the JSON response body field `currentAccountId` to `IgAuthenticateResponse.CurrentAccountId` and `lightstreamerEndpoint` to `LightstreamerEndpoint`
  - Sets `ExpiresAtUtc` to `null` (session expiry is managed by the platform supervision layer)
- `GetAccountsAsync`:
  - Sends `GET /accounts` with `X-IG-API-KEY`, `CST`, `X-SECURITY-TOKEN`, `Version: 1`
  - Returns deserialized `IgAccountsResponse`; throws `HttpRequestException` on non-2xx
- `GetPositionsAsync`:
  - Sends `GET /positions` with `X-IG-API-KEY`, `CST`, `X-SECURITY-TOKEN`, `Version: 2`
  - The IG positions response wraps items in a `positions` array where each element has a nested `position` object; flatten into `IgPositionItem` during mapping
  - Returns deserialized `IgPositionsResponse`; throws `HttpRequestException` on non-2xx

## Deliverable 4 — Extend `IgAuthenticationResponseSanitizer`

In `src/TNC.Trading.Platform.Application/Infrastructure/Ig/IgAuthenticationResponseSanitizer.cs`, update `IsSensitiveHeader` to also return `true` when the header name contains `x-ig-api-key` (case-insensitive).

## Deliverable 5 — DI registration

In `src/TNC.Trading.Platform.Infrastructure/Infrastructure/Platform/PlatformInfrastructureServiceCollectionExtensions.cs`, add the typed `HttpClient` registration inside `AddPlatformInfrastructure`, before the `return services` statement:

```csharp
services.AddHttpClient<IIgSessionClient, IgSessionClient>(client =>
{
    client.BaseAddress = new Uri("https://demo-api.ig.com/gateway/deal/");
});
```

Add required `using` directives:
- `TNC.Trading.Platform.Application.Infrastructure.Ig`
- `TNC.Trading.Platform.Infrastructure.Infrastructure.Platform.Ig` (adjust to match the actual namespace of `IgSessionClient`)

## Deliverable 6 — Unit tests

### Location 1: Application unit tests

Create test class `src` → `test/TNC.Trading.Platform.Application/TNC.Trading.Platform.Application.UnitTests/Infrastructure/Ig/IgAuthenticationResponseSanitizerTests.cs`.

Required test cases (use `MethodName_StateUnderTest_ExpectedResult` naming):

```
Sanitize_WhenResponseHasCstHeader_ShouldRedactCstValue
Sanitize_WhenResponseHasXSecurityTokenHeader_ShouldRedactSecurityTokenValue
Sanitize_WhenResponseHasAuthorizationHeader_ShouldRedactAuthorizationValue
Sanitize_WhenResponseHasXIgApiKeyHeader_ShouldRedactApiKeyValue
Sanitize_WhenResponseHasNonSensitiveVersionHeader_ShouldPassThroughValue
Sanitize_WhenResponseHasClientSessionToken_ShouldIndicateTokenPresence
Sanitize_WhenResponseHasNoClientSessionToken_ShouldIndicateTokenAbsence
```

Each test must include a comment block with:
- What the test verifies
- Expected outcome
- Why the behaviour matters
- Requirement traceability (e.g., `// Traces to SR2, SR3, NF3`)

### Location 2: Infrastructure unit tests

Create test class `test/TNC.Trading.Platform.Infrastructure/TNC.Trading.Platform.Infrastructure.UnitTests/Infrastructure/Platform/Ig/IgSessionClientTests.cs`.

Use a fake `HttpMessageHandler` (implement a simple `DelegatingHandler` or `HttpMessageHandler` subclass inline — do not take a dependency on Moq or NSubstitute unless the project already uses one of them; check the existing test project references first).

Required test cases:

```
AuthenticateAsync_WhenCalledWithValidRequest_ShouldSendPostToSessionEndpoint
AuthenticateAsync_WhenCalledWithValidRequest_ShouldIncludeXIgApiKeyHeader
AuthenticateAsync_WhenCalledWithValidRequest_ShouldIncludeVersionThreeHeader
AuthenticateAsync_WhenCalledWithValidRequest_ShouldSerialiseIdentifierInBody
AuthenticateAsync_WhenCalledWithValidRequest_ShouldSetEncryptedPasswordFalseInBody
AuthenticateAsync_WhenResponseIncludesCstHeader_ShouldMapToClientSessionToken
AuthenticateAsync_WhenResponseIncludesXSecurityTokenHeader_ShouldMapToAccountSecurityToken
AuthenticateAsync_WhenServerReturnsNonSuccessStatus_ShouldThrowHttpRequestException
GetAccountsAsync_WhenCalled_ShouldSendGetToAccountsEndpoint
GetAccountsAsync_WhenCalled_ShouldIncludeVersionOneHeader
GetAccountsAsync_WhenServerReturnsNonSuccessStatus_ShouldThrowHttpRequestException
GetPositionsAsync_WhenCalled_ShouldSendGetToPositionsEndpoint
GetPositionsAsync_WhenCalled_ShouldIncludeVersionTwoHeader
GetPositionsAsync_WhenServerReturnsNonSuccessStatus_ShouldThrowHttpRequestException
```

Each test must include a comment block with what is verified, expected outcome, why it matters, and requirement traceability (e.g., `// Traces to IR1, SR2, NF3`).

## Validation gates

After all deliverables are complete:

1. Run `dotnet build` — must succeed with zero errors
2. Run `dotnet test --filter "FullyQualifiedName!~IntegrationTests&FullyQualifiedName!~E2ETests&FullyQualifiedName!~FunctionalTests"` — all unit tests must pass, including all newly added tests
3. Confirm no changes have been made to `PlatformStateCoordinator.cs`
4. Confirm no changes have been made to any API, Web, or persistence files
5. Confirm `IgAuthenticationResponseSanitizer` now lists `X-IG-API-KEY` as a sensitive header

## Completion report

When all deliverables are implemented and both validation gates pass, report back to the Execute Delivery orchestrator with:

- List of files created and files modified
- Unit test count added
- Build and test gate outcomes
- Any deviations from this handoff and the rationale

The orchestrator will then update the Work Item 1 checkboxes and prepare the Work Item 2 handoff.
