namespace TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework.Entities;

internal sealed class IgProofDataEntity
{
    public int IgProofDataId { get; set; }

    public string BrokerEnvironment { get; set; } = string.Empty;

    public string? PreferredAccountName { get; set; }

    public string? PreferredAccountId { get; set; }

    public decimal? Balance { get; set; }

    public int OpenPositionCount { get; set; }

    public DateTimeOffset RetrievedAtUtc { get; set; }
}
