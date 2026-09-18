using TNC.Trading.Platform.Application.Configuration;

namespace TNC.Trading.Platform.Application.Features.AccountPreferences;

internal sealed record AccountPreferencesOperation(
    Guid Id,
    string IdempotencyKey,
    PlatformEnvironmentKind PlatformEnvironment,
    BrokerEnvironmentKind BrokerEnvironment,
    string AccountId,
    long BaselineRevision,
    bool RequestedTrailingStopsEnabled,
    string Actor,
    string CorrelationId,
    AccountPreferencesOperationPhase Phase,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);