namespace TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework.Entities;

internal sealed class IgLoginSnapshotEntity
{
    public Guid IgLoginSnapshotId { get; set; }

    public string BrokerEnvironment { get; set; } = string.Empty;

    public DateTimeOffset CapturedAtUtc { get; set; }

    public DateOnly TradingDay { get; set; }

    public string SnapshotKind { get; set; } = string.Empty;

    public string CurrentAccountId { get; set; } = string.Empty;

    public string? LightstreamerEndpoint { get; set; }

    public DateTimeOffset? SessionExpiresAtUtc { get; set; }

    public string ResponseHeadersJson { get; set; } = "{}";

    public string RawNonSecretPayloadJson { get; set; } = "{}";
}
