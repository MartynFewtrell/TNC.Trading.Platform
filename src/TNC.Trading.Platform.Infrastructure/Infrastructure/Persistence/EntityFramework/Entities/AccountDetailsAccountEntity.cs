namespace TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework.Entities;

internal sealed class AccountDetailsAccountEntity
{
    public Guid AccountDetailsAccountId { get; set; }
    public Guid AccountDetailsRetrievalId { get; set; }
    public string AccountId { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string? AccountAlias { get; set; }
    public string Status { get; set; } = string.Empty;
    public string AccountType { get; set; } = string.Empty;
    public bool IsPreferred { get; set; }
    public decimal Balance { get; set; }
    public decimal Deposit { get; set; }
    public decimal ProfitLoss { get; set; }
    public decimal Available { get; set; }
    public string Currency { get; set; } = string.Empty;
    public bool CanTransferFrom { get; set; }
    public bool CanTransferTo { get; set; }
    public AccountDetailsRetrievalEntity Retrieval { get; set; } = null!;
}