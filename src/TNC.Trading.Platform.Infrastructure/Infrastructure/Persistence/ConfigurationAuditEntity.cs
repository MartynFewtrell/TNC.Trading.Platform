namespace TNC.Trading.Platform.Infrastructure.Persistence;

internal sealed class ConfigurationAuditEntity
{
    public long ConfigurationAuditId { get; set; }

    public int ConfigurationId { get; set; }

    public string PlatformEnvironment { get; set; } = string.Empty;

    public string BrokerEnvironment { get; set; } = string.Empty;

    public DateTimeOffset OccurredAtUtc { get; set; }

    public string ChangedBy { get; set; } = string.Empty;

    public string ChangeType { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public string DetailsJson { get; set; } = string.Empty;

    public string? CorrelationId { get; set; }
}
