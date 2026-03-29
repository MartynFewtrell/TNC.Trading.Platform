using Microsoft.EntityFrameworkCore;

namespace TNC.Trading.Platform.Api.Infrastructure.Persistence;

internal sealed class PlatformDbContext(DbContextOptions<PlatformDbContext> options) : DbContext(options)
{
    internal DbSet<PlatformConfigurationEntity> PlatformConfigurations => Set<PlatformConfigurationEntity>();

    internal DbSet<ProtectedCredentialEntity> ProtectedCredentials => Set<ProtectedCredentialEntity>();

    internal DbSet<AuthRuntimeStateEntity> AuthRuntimeStates => Set<AuthRuntimeStateEntity>();

    internal DbSet<AuthRetryCycleEntity> AuthRetryCycles => Set<AuthRetryCycleEntity>();

    internal DbSet<OperationalEventEntity> OperationalEvents => Set<OperationalEventEntity>();

    internal DbSet<ConfigurationAuditEntity> ConfigurationAudits => Set<ConfigurationAuditEntity>();

    internal DbSet<NotificationRecordEntity> NotificationRecords => Set<NotificationRecordEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PlatformConfigurationEntity>(entity =>
        {
            entity.HasKey(item => item.ConfigurationId);
            entity.Property(item => item.PlatformEnvironment).HasMaxLength(32);
            entity.Property(item => item.BrokerEnvironment).HasMaxLength(32);
            entity.Property(item => item.TradingDaysCsv).HasMaxLength(128);
            entity.Property(item => item.WeekendBehavior).HasMaxLength(64);
            entity.Property(item => item.TimeZone).HasMaxLength(64);
            entity.Property(item => item.NotificationProvider).HasMaxLength(128);
            entity.Property(item => item.NotificationEmailTo).HasMaxLength(320);
        });

        modelBuilder.Entity<ProtectedCredentialEntity>(entity =>
        {
            entity.HasKey(item => item.CredentialId);
            entity.HasIndex(item => new { item.BrokerEnvironment, item.CredentialType }).IsUnique();
            entity.Property(item => item.BrokerEnvironment).HasMaxLength(32);
            entity.Property(item => item.CredentialType).HasMaxLength(64);
            entity.Property(item => item.ProtectionKind).HasMaxLength(64);
        });

        modelBuilder.Entity<AuthRuntimeStateEntity>(entity =>
        {
            entity.HasKey(item => item.AuthRuntimeStateId);
            entity.Property(item => item.PlatformEnvironment).HasMaxLength(32);
            entity.Property(item => item.BrokerEnvironment).HasMaxLength(32);
            entity.Property(item => item.TradingScheduleStatus).HasMaxLength(64);
            entity.Property(item => item.SessionStatus).HasMaxLength(64);
            entity.Property(item => item.RetryPhase).HasMaxLength(64);
            entity.Property(item => item.BlockedReason).HasMaxLength(512);
        });

        modelBuilder.Entity<AuthRetryCycleEntity>(entity =>
        {
            entity.HasKey(item => item.RetryCycleId);
            entity.Property(item => item.CycleType).HasMaxLength(64);
            entity.Property(item => item.PlatformEnvironment).HasMaxLength(32);
            entity.Property(item => item.BrokerEnvironment).HasMaxLength(32);
            entity.Property(item => item.RetryPhase).HasMaxLength(64);
        });

        modelBuilder.Entity<OperationalEventEntity>(entity =>
        {
            entity.HasKey(item => item.EventId);
            entity.Property(item => item.Category).HasMaxLength(64);
            entity.Property(item => item.EventType).HasMaxLength(128);
            entity.Property(item => item.PlatformEnvironment).HasMaxLength(32);
            entity.Property(item => item.BrokerEnvironment).HasMaxLength(32);
            entity.Property(item => item.Severity).HasMaxLength(32);
            entity.Property(item => item.Summary).HasMaxLength(512);
            entity.Property(item => item.CorrelationId).HasMaxLength(64);
        });

        modelBuilder.Entity<ConfigurationAuditEntity>(entity =>
        {
            entity.HasKey(item => item.ConfigurationAuditId);
            entity.Property(item => item.PlatformEnvironment).HasMaxLength(32);
            entity.Property(item => item.BrokerEnvironment).HasMaxLength(32);
            entity.Property(item => item.ChangedBy).HasMaxLength(128);
            entity.Property(item => item.ChangeType).HasMaxLength(128);
            entity.Property(item => item.Summary).HasMaxLength(512);
            entity.Property(item => item.CorrelationId).HasMaxLength(64);
        });

        modelBuilder.Entity<NotificationRecordEntity>(entity =>
        {
            entity.HasKey(item => item.NotificationRecordId);
            entity.Property(item => item.NotificationType).HasMaxLength(128);
            entity.Property(item => item.PlatformEnvironment).HasMaxLength(32);
            entity.Property(item => item.BrokerEnvironment).HasMaxLength(32);
            entity.Property(item => item.Recipient).HasMaxLength(320);
            entity.Property(item => item.Summary).HasMaxLength(512);
            entity.Property(item => item.DispatchStatus).HasMaxLength(64);
            entity.Property(item => item.Provider).HasMaxLength(128);
            entity.Property(item => item.CorrelationId).HasMaxLength(64);
        });
    }
}

internal sealed class PlatformConfigurationEntity
{
    public int ConfigurationId { get; set; }

    public string PlatformEnvironment { get; set; } = string.Empty;

    public string BrokerEnvironment { get; set; } = string.Empty;

    public TimeOnly TradingHoursStart { get; set; }

    public TimeOnly TradingHoursEnd { get; set; }

    public string TradingDaysCsv { get; set; } = string.Empty;

    public string WeekendBehavior { get; set; } = string.Empty;

    public string BankHolidayExclusionsJson { get; set; } = "[]";

    public string TimeZone { get; set; } = "UTC";

    public int RetryInitialDelaySeconds { get; set; }

    public int RetryMaxAutomaticRetries { get; set; }

    public int RetryMultiplier { get; set; }

    public int RetryMaxDelaySeconds { get; set; }

    public int RetryPeriodicDelayMinutes { get; set; }

    public string NotificationProvider { get; set; } = string.Empty;

    public string? NotificationEmailTo { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public string UpdatedBy { get; set; } = string.Empty;

    public bool RestartRequired { get; set; }
}

internal sealed class ProtectedCredentialEntity
{
    public int CredentialId { get; set; }

    public string BrokerEnvironment { get; set; } = string.Empty;

    public string CredentialType { get; set; } = string.Empty;

    public string ProtectedValue { get; set; } = string.Empty;

    public string ProtectionKind { get; set; } = "DataProtection";

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public string UpdatedBy { get; set; } = string.Empty;
}

internal sealed class AuthRuntimeStateEntity
{
    public int AuthRuntimeStateId { get; set; }

    public string PlatformEnvironment { get; set; } = string.Empty;

    public string BrokerEnvironment { get; set; } = string.Empty;

    public string TradingScheduleStatus { get; set; } = string.Empty;

    public string SessionStatus { get; set; } = string.Empty;

    public bool IsDegraded { get; set; }

    public string? BlockedReason { get; set; }

    public string RetryPhase { get; set; } = string.Empty;

    public int AutomaticAttemptNumber { get; set; }

    public DateTimeOffset? NextRetryAtUtc { get; set; }

    public bool RetryLimitReached { get; set; }

    public Guid? CurrentRetryCycleId { get; set; }

    public DateTimeOffset? EstablishedAtUtc { get; set; }

    public DateTimeOffset? ExpiresAtUtc { get; set; }

    public DateTimeOffset? LastValidatedAtUtc { get; set; }

    public DateTimeOffset? LastTransitionAtUtc { get; set; }
}

internal sealed class AuthRetryCycleEntity
{
    public Guid RetryCycleId { get; set; }

    public string CycleType { get; set; } = string.Empty;

    public string PlatformEnvironment { get; set; } = string.Empty;

    public string BrokerEnvironment { get; set; } = string.Empty;

    public string RetryPhase { get; set; } = string.Empty;

    public int AutomaticAttemptNumber { get; set; }

    public DateTimeOffset? NextRetryAtUtc { get; set; }

    public int? LastDelaySeconds { get; set; }

    public int PeriodicDelayMinutes { get; set; }

    public int MaxAutomaticRetries { get; set; }

    public bool RetryLimitReached { get; set; }

    public bool FailureNotificationSent { get; set; }

    public DateTimeOffset StartedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}

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
