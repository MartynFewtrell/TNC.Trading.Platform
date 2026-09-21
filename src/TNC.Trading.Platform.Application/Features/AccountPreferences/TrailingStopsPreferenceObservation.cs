using TNC.Trading.Platform.Application.Configuration;

namespace TNC.Trading.Platform.Application.Features.AccountPreferences;

internal sealed record TrailingStopsPreferenceObservation(
    Guid Id,
    bool TrailingStopsEnabled,
    DateTimeOffset ObservedAtUtc,
    DateTimeOffset RecordedAtUtc,
    PlatformEnvironmentKind PlatformEnvironment,
    BrokerEnvironmentKind BrokerEnvironment,
    string? AccountId,
    string ObservationKind,
    string Source,
    string? Actor,
    string CorrelationId);