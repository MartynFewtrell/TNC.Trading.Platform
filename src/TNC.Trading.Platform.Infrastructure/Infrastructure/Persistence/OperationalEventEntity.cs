namespace TNC.Trading.Platform.Infrastructure.Persistence;

internal sealed class OperationalEventEntity
{
    public long EventId { get; set; }

    public DateTimeOffset OccurredAtUtc { get; set; }

    public string Category { get; set; } = string.Empty;

    public string EventType { get; set; } = string.Empty;

    public string PlatformEnvironment { get; set; } = string.Empty;

    public string BrokerEnvironment { get; set; } = string.Empty;

    public string Severity { get; set; } = "Information";

    public string Summary { get; set; } = string.Empty;

    public string DetailsJson { get; set; } = string.Empty;

    public string? CorrelationId { get; set; }

    public Guid? RetryCycleId { get; set; }
}
