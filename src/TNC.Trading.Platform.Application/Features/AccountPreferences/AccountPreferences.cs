namespace TNC.Trading.Platform.Application.Features.AccountPreferences;

internal sealed record AccountPreferences(
    bool TrailingStopsEnabled,
    string ApplicationStatus,
    DateTimeOffset ObservedAtUtc);