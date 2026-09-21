namespace TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework.Entities;

internal sealed class AccountPreferencesCurrentStateEntity
{
    public Guid AccountPreferencesCurrentStateId { get; set; }
    public string PlatformEnvironment { get; set; } = null!;
    public string BrokerEnvironment { get; set; } = null!;
    public string? AccountId { get; set; }
    public bool? DesiredTrailingStopsEnabled { get; set; }
    public long? DesiredRevision { get; set; }
    public string? DesiredActor { get; set; }
    public DateTimeOffset? DesiredChangedAtUtc { get; set; }
    public bool? ObservedTrailingStopsEnabled { get; set; }
    public string? ObservedAccountId { get; set; }
    public DateTimeOffset? ObservedAtUtc { get; set; }
    public string? AuthenticationSnapshotId { get; set; }
    public string? AttemptId { get; set; }
    public string VerificationStatus { get; set; } = null!;
    public DateTimeOffset? LastVerifiedAtUtc { get; set; }
    public DateTimeOffset? NextRetryAtUtc { get; set; }
    public int RetryCount { get; set; }
    public string? FailureSummary { get; set; }
    public string? CorrelationId { get; set; }
    public byte[] ConcurrencyToken { get; set; } = [];
}