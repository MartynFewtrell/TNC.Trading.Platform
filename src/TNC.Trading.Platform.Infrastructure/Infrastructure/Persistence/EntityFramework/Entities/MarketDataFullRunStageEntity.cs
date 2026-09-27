namespace TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework.Entities;

internal sealed class MarketDataFullRunStageEntity
{
    public Guid RunId { get; set; }
    public string Stage { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public int Attempts { get; set; }
    public DateTimeOffset? LastSuccessAtUtc { get; set; }
    public string? SafeReasonCode { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
