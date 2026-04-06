using Microsoft.EntityFrameworkCore;

namespace TNC.Trading.Platform.Infrastructure.Persistence;

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
