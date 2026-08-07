using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework.Entities;

namespace TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework;

internal sealed class PlatformDbContext(DbContextOptions<PlatformDbContext> options) : DbContext(options)
{
    internal DbSet<PlatformConfigurationEntity> PlatformConfigurations => Set<PlatformConfigurationEntity>();

    internal DbSet<ProtectedCredentialEntity> ProtectedCredentials => Set<ProtectedCredentialEntity>();

    internal DbSet<AuthRuntimeStateEntity> AuthRuntimeStates => Set<AuthRuntimeStateEntity>();

    internal DbSet<AuthRetryCycleEntity> AuthRetryCycles => Set<AuthRetryCycleEntity>();

    internal DbSet<IgLoginSnapshotEntity> IgLoginSnapshots => Set<IgLoginSnapshotEntity>();

    internal DbSet<IgProofDataEntity> IgProofData => Set<IgProofDataEntity>();

    internal DbSet<OperationalEventEntity> OperationalEvents => Set<OperationalEventEntity>();

    internal DbSet<ConfigurationAuditEntity> ConfigurationAudits => Set<ConfigurationAuditEntity>();

    internal DbSet<NotificationRecordEntity> NotificationRecords => Set<NotificationRecordEntity>();

    internal DbSet<AccountDetailsRetrievalEntity> AccountDetailsRetrievals => Set<AccountDetailsRetrievalEntity>();

    internal DbSet<AccountDetailsAccountEntity> AccountDetailsAccounts => Set<AccountDetailsAccountEntity>();

    internal DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

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
            entity.Property(item => item.LatestFailureSummary).HasMaxLength(512);
        });

        modelBuilder.Entity<AuthRetryCycleEntity>(entity =>
        {
            entity.HasKey(item => item.RetryCycleId);
            entity.Property(item => item.CycleType).HasMaxLength(64);
            entity.Property(item => item.PlatformEnvironment).HasMaxLength(32);
            entity.Property(item => item.BrokerEnvironment).HasMaxLength(32);
            entity.Property(item => item.RetryPhase).HasMaxLength(64);
        });

        modelBuilder.Entity<IgLoginSnapshotEntity>(entity =>
        {
            entity.HasKey(item => item.IgLoginSnapshotId);
            entity.Property(item => item.BrokerEnvironment).HasMaxLength(32);
            entity.Property(item => item.SnapshotKind).HasMaxLength(64);
            entity.Property(item => item.CurrentAccountId).HasMaxLength(64);
            entity.Property(item => item.LightstreamerEndpoint).HasMaxLength(512);
            entity.HasIndex(item => new { item.BrokerEnvironment, item.SnapshotKind, item.TradingDay });
        });

        modelBuilder.Entity<IgProofDataEntity>(entity =>
        {
            entity.HasKey(item => item.IgProofDataId);
            entity.HasIndex(item => item.BrokerEnvironment).IsUnique();
            entity.Property(item => item.BrokerEnvironment).HasMaxLength(32);
            entity.Property(item => item.PreferredAccountName).HasMaxLength(256);
            entity.Property(item => item.PreferredAccountId).HasMaxLength(64);
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

        modelBuilder.Entity<AccountDetailsRetrievalEntity>(entity =>
        {
            entity.HasKey(item => item.AccountDetailsRetrievalId);
            entity.Property(item => item.BrokerEnvironment).HasMaxLength(32).IsRequired();
            entity.Property(item => item.TriggerSource).HasMaxLength(32).IsRequired();
            entity.Property(item => item.TriggeredBy).HasMaxLength(128);
            entity.HasIndex(item => new { item.BrokerEnvironment, item.RetrievedAtUtc, item.AccountDetailsRetrievalId });
            entity.HasIndex(item => new { item.BrokerEnvironment, item.TradingDay })
                .IsUnique()
                .HasFilter("[TriggerSource] = 'Automatic'");
            entity.HasMany(item => item.Accounts)
                .WithOne(item => item.Retrieval)
                .HasForeignKey(item => item.AccountDetailsRetrievalId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AccountDetailsAccountEntity>(entity =>
        {
            entity.HasKey(item => item.AccountDetailsAccountId);
            entity.Property(item => item.AccountId).HasMaxLength(64).IsRequired();
            entity.Property(item => item.AccountName).HasMaxLength(256).IsRequired();
            entity.Property(item => item.AccountAlias).HasMaxLength(256);
            entity.Property(item => item.Status).HasMaxLength(64).IsRequired();
            entity.Property(item => item.AccountType).HasMaxLength(64).IsRequired();
            entity.Property(item => item.Currency).HasMaxLength(16).IsRequired();
            entity.Property(item => item.Balance).HasPrecision(19, 5);
            entity.Property(item => item.Deposit).HasPrecision(19, 5);
            entity.Property(item => item.ProfitLoss).HasPrecision(19, 5);
            entity.Property(item => item.Available).HasPrecision(19, 5);
            entity.HasIndex(item => new { item.AccountDetailsRetrievalId, item.AccountId }).IsUnique();
        });
    }
}
