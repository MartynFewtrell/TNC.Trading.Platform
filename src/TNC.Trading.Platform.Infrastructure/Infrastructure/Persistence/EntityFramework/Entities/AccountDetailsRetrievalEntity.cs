namespace TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework.Entities;

internal sealed class AccountDetailsRetrievalEntity
{
    public Guid AccountDetailsRetrievalId { get; set; }
    public string BrokerEnvironment { get; set; } = string.Empty;
    public DateTimeOffset RetrievedAtUtc { get; set; }
    public DateOnly TradingDay { get; set; }
    public int AccountCount { get; set; }
    public string TriggerSource { get; set; } = string.Empty;
    public string? TriggeredBy { get; set; }
    public ICollection<AccountDetailsAccountEntity> Accounts { get; set; } = [];
}