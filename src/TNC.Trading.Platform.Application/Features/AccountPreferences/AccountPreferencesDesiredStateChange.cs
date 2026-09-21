using TNC.Trading.Platform.Application.Configuration;

namespace TNC.Trading.Platform.Application.Features.AccountPreferences;

internal sealed record AccountPreferencesDesiredStateChange(
    PlatformEnvironmentKind PlatformEnvironment,
    BrokerEnvironmentKind BrokerEnvironment,
    string AccountId,
    bool TrailingStopsEnabled,
    long? ExpectedRevision,
    string Actor,
    DateTimeOffset ChangedAtUtc,
    string CorrelationId);

internal sealed record AccountPreferencesDesiredStateCommitResult(bool Committed, long Revision, AccountPreferencesCurrentState State);
internal sealed record AccountPreferencesReconciliationCompletion(bool Applied, AccountPreferencesCurrentState? State);