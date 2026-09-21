using TNC.Trading.Platform.Application.Configuration;

namespace TNC.Trading.Platform.Application.Features.AccountPreferences;

internal sealed record AccountPreferencesCurrentState(
    Guid Id,
    PlatformEnvironmentKind PlatformEnvironment,
    BrokerEnvironmentKind BrokerEnvironment,
    string? AccountId,
    bool? DesiredTrailingStopsEnabled,
    long? DesiredRevision,
    string? DesiredActor,
    DateTimeOffset? DesiredChangedAtUtc,
    bool? ObservedTrailingStopsEnabled,
    string? ObservedAccountId,
    DateTimeOffset? ObservedAtUtc,
    string? AuthenticationSnapshotId,
    string? AttemptId,
    AccountPreferencesVerificationStatus VerificationStatus,
    DateTimeOffset? LastVerifiedAtUtc,
    DateTimeOffset? NextRetryAtUtc,
    int RetryCount,
    string? FailureSummary,
    string? CorrelationId,
    byte[] ConcurrencyToken);