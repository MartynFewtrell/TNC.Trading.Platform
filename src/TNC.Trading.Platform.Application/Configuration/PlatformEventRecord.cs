namespace TNC.Trading.Platform.Application.Configuration;

internal sealed record PlatformEventRecord(
    string Category,
    string EventType,
    PlatformEnvironmentKind PlatformEnvironment,
    BrokerEnvironmentKind BrokerEnvironment,
    string Severity,
    string Summary,
    object Details,
    string CorrelationId,
    Guid? RetryCycleId,
    DateTimeOffset OccurredAtUtc);
