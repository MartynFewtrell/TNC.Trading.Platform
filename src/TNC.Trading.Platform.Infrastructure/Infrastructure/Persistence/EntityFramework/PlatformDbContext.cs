using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework.Entities;

namespace TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework;

internal sealed class PlatformDbContext(DbContextOptions<PlatformDbContext> options) : DbContext(options)
{
    internal DbSet<PlatformConfigurationEntity> PlatformConfigurations => Set<PlatformConfigurationEntity>();
    internal DbSet<BrokerEnvironmentEntity> BrokerEnvironments => Set<BrokerEnvironmentEntity>();
    internal DbSet<BrokerEnvironmentSelectionEntity> BrokerEnvironmentSelections => Set<BrokerEnvironmentSelectionEntity>();
    internal DbSet<BrokerEnvironmentDefaultsEntity> BrokerEnvironmentDefaults => Set<BrokerEnvironmentDefaultsEntity>();
    internal DbSet<BrokerEnvironmentScheduleProfileEntity> BrokerEnvironmentScheduleProfiles => Set<BrokerEnvironmentScheduleProfileEntity>();
    internal DbSet<BrokerEnvironmentRetryProfileEntity> BrokerEnvironmentRetryProfiles => Set<BrokerEnvironmentRetryProfileEntity>();
    internal DbSet<BrokerEnvironmentNotificationProfileEntity> BrokerEnvironmentNotificationProfiles => Set<BrokerEnvironmentNotificationProfileEntity>();
    internal DbSet<BrokerEnvironmentRetirementTokenEntity> BrokerEnvironmentRetirementTokens => Set<BrokerEnvironmentRetirementTokenEntity>();
    internal DbSet<BrokerEnvironmentRetirementAuditEntity> BrokerEnvironmentRetirementAudits => Set<BrokerEnvironmentRetirementAuditEntity>();

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

    internal DbSet<TrailingStopsPreferenceObservationEntity> TrailingStopsPreferenceObservations => Set<TrailingStopsPreferenceObservationEntity>();
    internal DbSet<AccountPreferencesCurrentStateEntity> AccountPreferencesCurrentStates => Set<AccountPreferencesCurrentStateEntity>();
    internal DbSet<AccountPreferencesDesiredStateAuditEntity> AccountPreferencesDesiredStateAudits => Set<AccountPreferencesDesiredStateAuditEntity>();
    internal DbSet<AccountPreferencesOperationEntity> AccountPreferencesOperations => Set<AccountPreferencesOperationEntity>();

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

        modelBuilder.Entity<BrokerEnvironmentEntity>(entity =>
        {
            entity.HasKey(item => item.BrokerEnvironmentId);
            entity.HasIndex(item => item.NormalizedName).IsUnique();
            entity.Property(item => item.Name).HasMaxLength(128).IsRequired();
            entity.Property(item => item.NormalizedName).HasMaxLength(128).IsRequired();
            entity.Property(item => item.Provider).HasMaxLength(32).IsRequired();
            entity.Property(item => item.Kind).HasMaxLength(32).IsRequired();
            entity.Property(item => item.Lifecycle).HasMaxLength(32).IsRequired();
            entity.Property(item => item.Availability).HasMaxLength(32).IsRequired();
            entity.Property(item => item.AvailabilityReason).HasMaxLength(512);
            entity.Property(item => item.EndpointProfile).HasMaxLength(128).IsRequired();
            entity.Property(item => item.ConcurrencyToken).IsRowVersion();
        });

        modelBuilder.Entity<BrokerEnvironmentSelectionEntity>(entity =>
        {
            entity.HasKey(item => item.SelectionId);
            entity.HasIndex(item => item.SelectionId).IsUnique();
            entity.HasOne<BrokerEnvironmentEntity>().WithMany().HasForeignKey(item => item.SelectedBrokerEnvironmentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<BrokerEnvironmentEntity>().WithMany().HasForeignKey(item => item.AppliedBrokerEnvironmentId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<BrokerEnvironmentDefaultsEntity>(entity =>
        {
            entity.HasKey(item => item.BrokerEnvironmentDefaultsId);
            entity.HasIndex(item => item.Version).IsUnique();
            entity.Property(item => item.TradingDaysCsv).HasMaxLength(128).IsRequired();
            entity.Property(item => item.WeekendBehavior).HasMaxLength(64).IsRequired();
            entity.Property(item => item.BankHolidayExclusionsJson).IsRequired();
            entity.Property(item => item.TimeZone).HasMaxLength(64).IsRequired();
            entity.Property(item => item.NotificationProvider).HasMaxLength(128).IsRequired();
            entity.Property(item => item.NotificationEmailTo).HasMaxLength(320);
        });

        modelBuilder.Entity<BrokerEnvironmentScheduleProfileEntity>(entity =>
        {
            entity.HasKey(item => item.BrokerEnvironmentId);
            entity.Property(item => item.TradingDaysCsv).HasMaxLength(128).IsRequired();
            entity.Property(item => item.WeekendBehavior).HasMaxLength(64).IsRequired();
            entity.Property(item => item.BankHolidayExclusionsJson).IsRequired();
            entity.Property(item => item.TimeZone).HasMaxLength(64).IsRequired();
            entity.HasOne<BrokerEnvironmentEntity>().WithMany().HasForeignKey(item => item.BrokerEnvironmentId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<BrokerEnvironmentRetryProfileEntity>(entity =>
        {
            entity.HasKey(item => item.BrokerEnvironmentId);
            entity.HasOne<BrokerEnvironmentEntity>().WithMany().HasForeignKey(item => item.BrokerEnvironmentId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<BrokerEnvironmentNotificationProfileEntity>(entity =>
        {
            entity.HasKey(item => item.BrokerEnvironmentId);
            entity.Property(item => item.Provider).HasMaxLength(128).IsRequired();
            entity.Property(item => item.EmailTo).HasMaxLength(320);
            entity.HasOne<BrokerEnvironmentEntity>().WithMany().HasForeignKey(item => item.BrokerEnvironmentId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<BrokerEnvironmentRetirementTokenEntity>(entity =>
        {
            entity.HasKey(item => item.TokenId);
            entity.HasIndex(item => item.TokenHash).IsUnique();
            entity.HasOne<BrokerEnvironmentEntity>().WithMany().HasForeignKey(item => item.BrokerEnvironmentId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<BrokerEnvironmentRetirementAuditEntity>(entity =>
        {
            entity.HasKey(item => item.BrokerEnvironmentRetirementAuditId);
            entity.HasOne<BrokerEnvironmentEntity>().WithMany().HasForeignKey(item => item.BrokerEnvironmentId).OnDelete(DeleteBehavior.Restrict);
            entity.Property(item => item.Name).HasMaxLength(128).IsRequired();
            entity.Property(item => item.Actor).HasMaxLength(128).IsRequired();
            entity.Property(item => item.PurgeCountsJson).IsRequired();
            entity.Property(item => item.RetainedCountsJson).IsRequired();
        });

        modelBuilder.Entity<ProtectedCredentialEntity>(entity =>
        {
            entity.HasKey(item => item.CredentialId);
            entity.HasIndex(item => new { item.BrokerEnvironment, item.CredentialType }).IsUnique();
            entity.HasIndex(item => new { item.BrokerEnvironmentId, item.CredentialType }).IsUnique().HasFilter("[BrokerEnvironmentId] IS NOT NULL");
            entity.HasOne<BrokerEnvironmentEntity>().WithMany().HasForeignKey(item => item.BrokerEnvironmentId).OnDelete(DeleteBehavior.Restrict);
            entity.Property(item => item.BrokerEnvironment).HasMaxLength(32);
            entity.Property(item => item.CredentialType).HasMaxLength(64);
            entity.Property(item => item.ProtectionKind).HasMaxLength(64);
        });

        modelBuilder.Entity<AuthRuntimeStateEntity>(entity =>
        {
            entity.HasKey(item => item.AuthRuntimeStateId);
            entity.HasIndex(item => item.BrokerEnvironmentId);
            entity.HasOne<BrokerEnvironmentEntity>().WithMany().HasForeignKey(item => item.BrokerEnvironmentId).OnDelete(DeleteBehavior.Restrict);
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
            entity.HasIndex(item => item.BrokerEnvironmentId);
            entity.HasOne<BrokerEnvironmentEntity>().WithMany().HasForeignKey(item => item.BrokerEnvironmentId).OnDelete(DeleteBehavior.Restrict);
            entity.Property(item => item.CycleType).HasMaxLength(64);
            entity.Property(item => item.PlatformEnvironment).HasMaxLength(32);
            entity.Property(item => item.BrokerEnvironment).HasMaxLength(32);
            entity.Property(item => item.RetryPhase).HasMaxLength(64);
        });

        modelBuilder.Entity<IgLoginSnapshotEntity>(entity =>
        {
            entity.HasKey(item => item.IgLoginSnapshotId);
            entity.HasIndex(item => new { item.BrokerEnvironmentId, item.SnapshotKind, item.TradingDay });
            entity.HasOne<BrokerEnvironmentEntity>().WithMany().HasForeignKey(item => item.BrokerEnvironmentId).OnDelete(DeleteBehavior.Restrict);
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
            entity.HasIndex(item => item.BrokerEnvironmentId).IsUnique().HasFilter("[BrokerEnvironmentId] IS NOT NULL");
            entity.HasOne<BrokerEnvironmentEntity>().WithMany().HasForeignKey(item => item.BrokerEnvironmentId).OnDelete(DeleteBehavior.Restrict);
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
            entity.HasIndex(item => new { item.BrokerEnvironmentId, item.RetrievedAtUtc, item.AccountDetailsRetrievalId });
            entity.HasOne<BrokerEnvironmentEntity>().WithMany().HasForeignKey(item => item.BrokerEnvironmentId).OnDelete(DeleteBehavior.Restrict);
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

        modelBuilder.Entity<TrailingStopsPreferenceObservationEntity>(entity =>
        {
            entity.HasKey(item => item.TrailingStopsPreferenceObservationId);
            entity.HasIndex(item => new { item.BrokerEnvironmentId, item.PlatformEnvironment, item.ObservedAtUtc, item.TrailingStopsPreferenceObservationId }).HasDatabaseName("IX_TrailingStops_Catalog_Platform_Observed");
            entity.HasOne<BrokerEnvironmentEntity>().WithMany().HasForeignKey(item => item.BrokerEnvironmentId).OnDelete(DeleteBehavior.Restrict);
            entity.Property(item => item.BrokerEnvironment).HasMaxLength(32).IsRequired();
            entity.Property(item => item.PlatformEnvironment).HasMaxLength(32).IsRequired();
            entity.Property(item => item.AccountId).HasMaxLength(64);
            entity.Property(item => item.ObservationKind).HasMaxLength(64).IsRequired();
            entity.Property(item => item.Source).HasMaxLength(64).IsRequired();
            entity.Property(item => item.Actor).HasMaxLength(128);
            entity.Property(item => item.CorrelationId).HasMaxLength(64).IsRequired();
            entity.HasIndex(item => new { item.BrokerEnvironment, item.PlatformEnvironment, item.ObservedAtUtc, item.TrailingStopsPreferenceObservationId });
        });

        modelBuilder.Entity<AccountPreferencesCurrentStateEntity>(entity =>
        {
            entity.HasKey(item => item.AccountPreferencesCurrentStateId);
            entity.HasIndex(item => new { item.PlatformEnvironment, item.BrokerEnvironmentId, item.AccountId }).IsUnique().HasFilter("[BrokerEnvironmentId] IS NOT NULL");
            entity.HasOne<BrokerEnvironmentEntity>().WithMany().HasForeignKey(item => item.BrokerEnvironmentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(item => new { item.PlatformEnvironment, item.BrokerEnvironment, item.AccountId }).IsUnique();
            entity.Property(item => item.PlatformEnvironment).HasMaxLength(32).IsRequired();
            entity.Property(item => item.BrokerEnvironment).HasMaxLength(32).IsRequired();
            entity.Property(item => item.AccountId).HasMaxLength(64);
            entity.Property(item => item.DesiredActor).HasMaxLength(128);
            entity.Property(item => item.VerificationStatus).HasMaxLength(32).IsRequired();
            entity.Property(item => item.FailureSummary).HasMaxLength(512);
            entity.Property(item => item.CorrelationId).HasMaxLength(64);
            entity.Property(item => item.ConcurrencyToken).IsRowVersion();
        });

        modelBuilder.Entity<AccountPreferencesDesiredStateAuditEntity>(entity =>
        {
            entity.HasKey(item => item.AccountPreferencesDesiredStateAuditId);
            entity.HasIndex(item => new { item.PlatformEnvironment, item.BrokerEnvironmentId, item.AccountId, item.NewRevision });
            entity.HasOne<BrokerEnvironmentEntity>().WithMany().HasForeignKey(item => item.BrokerEnvironmentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(item => new { item.AccountPreferencesCurrentStateId, item.NewRevision }).IsUnique();
            entity.HasIndex(item => new { item.PlatformEnvironment, item.BrokerEnvironment, item.AccountId, item.NewRevision });
            entity.Property(item => item.PlatformEnvironment).HasMaxLength(32).IsRequired();
            entity.Property(item => item.BrokerEnvironment).HasMaxLength(32).IsRequired();
            entity.Property(item => item.AccountId).HasMaxLength(64).IsRequired();
            entity.Property(item => item.Actor).HasMaxLength(128).IsRequired();
            entity.Property(item => item.ChangeType).HasMaxLength(64).IsRequired();
            entity.Property(item => item.CorrelationId).HasMaxLength(64).IsRequired();
        });

        modelBuilder.Entity<AccountPreferencesOperationEntity>(entity =>
        {
            entity.HasKey(item => item.AccountPreferencesOperationId);
            entity.HasIndex(item => new { item.PlatformEnvironment, item.BrokerEnvironmentId, item.IdempotencyKey }).IsUnique().HasFilter("[BrokerEnvironmentId] IS NOT NULL");
            entity.HasOne<BrokerEnvironmentEntity>().WithMany().HasForeignKey(item => item.BrokerEnvironmentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(item => new { item.PlatformEnvironment, item.BrokerEnvironment, item.IdempotencyKey }).IsUnique();
            entity.Property(item => item.IdempotencyKey).HasMaxLength(128).IsRequired();
            entity.Property(item => item.PlatformEnvironment).HasMaxLength(32).IsRequired();
            entity.Property(item => item.BrokerEnvironment).HasMaxLength(32).IsRequired();
            entity.Property(item => item.AccountId).HasMaxLength(64).IsRequired();
            entity.Property(item => item.Actor).HasMaxLength(128).IsRequired();
            entity.Property(item => item.CorrelationId).HasMaxLength(64).IsRequired();
            entity.Property(item => item.Phase).HasMaxLength(32).IsRequired();
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
