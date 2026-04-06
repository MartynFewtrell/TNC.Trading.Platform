namespace TNC.Trading.Platform.Infrastructure.Persistence;

internal sealed class NotificationRecordEntity
{
    public long NotificationRecordId { get; set; }

    public DateTimeOffset DispatchedAtUtc { get; set; }

    public string NotificationType { get; set; } = string.Empty;

    public string PlatformEnvironment { get; set; } = string.Empty;

    public string BrokerEnvironment { get; set; } = string.Empty;

    public string Recipient { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public string DispatchStatus { get; set; } = string.Empty;

    public string Provider { get; set; } = string.Empty;

    public string? CorrelationId { get; set; }

    public Guid? RetryCycleId { get; set; }
}
