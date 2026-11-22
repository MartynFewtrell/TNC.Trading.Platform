namespace TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework.Entities;

internal sealed class InstrumentCollectionCategoryAttemptEntity
{
    public Guid BrokerEnvironmentId { get; set; }
    public DateOnly TradingDay { get; set; }
    public int ScheduledSlot { get; set; }
    public string CategoryCode { get; set; } = string.Empty;
    public int Attempts { get; set; }
    public string State { get; set; } = "Pending";
    public long LeaseFence { get; set; }
    public string? SafeError { get; set; }
    public string? FailureKind { get; set; }
    public string? ProviderErrorCode { get; set; }
    public string? ProviderErrorCodeParseOutcome { get; set; }
    public string? AuthenticationPhase { get; set; }
    public int? HttpStatus { get; set; }
    public string? Operation { get; set; }
    public int? PageNumber { get; set; }
    public int? Expected { get; set; }
    public int? Actual { get; set; }
    public int? HttpRequestCount { get; set; }
    public string? InitialPageEvidenceJson { get; set; }
    public string? RestartedPageEvidenceJson { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }
}
