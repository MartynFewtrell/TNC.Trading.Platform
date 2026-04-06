namespace TNC.Trading.Platform.Application.Configuration;

internal sealed record OperationalEventModel(
    long EventId,
    string Category,
    string EventType,
    PlatformEnvironmentKind PlatformEnvironment,
    BrokerEnvironmentKind BrokerEnvironment,
    string Summary,
    string Details,
    DateTimeOffset OccurredAtUtc);
