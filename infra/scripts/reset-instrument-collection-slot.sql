-- Operator-only retry of an IG Demo slot with failed instrument collection.
-- Connect to platformdb on the intended local SQL Server before running.
-- Edit the trading day and zero-based slot below; do not reset UsedRequestBudget.
-- Apply pending migrations and restart the API with the snapshot publication fix first.
-- Run only while the configured trading window is still open.

DECLARE @TradingDay date = '2026-09-25';
DECLARE @ScheduledSlot int = 0;
DECLARE @BrokerEnvironmentId uniqueidentifier;
DECLARE @LockResource nvarchar(255);
DECLARE @LockResult int;
DECLARE @NowUtc datetimeoffset = TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00');

SET XACT_ABORT ON;

IF DB_NAME() <> N'platformdb'
    THROW 51000, 'Connect to platformdb before resetting an instrument slot.', 1;

IF @ScheduledSlot NOT BETWEEN 0 AND 3
    THROW 51001, 'The scheduled slot must be between 0 and 3.', 1;

BEGIN TRY
    BEGIN TRANSACTION;

    IF (SELECT COUNT(*)
        FROM dbo.BrokerEnvironmentSelections AS s
        JOIN dbo.BrokerEnvironments AS e
            ON e.BrokerEnvironmentId = s.AppliedBrokerEnvironmentId
        WHERE e.Name = N'IG Demo'
          AND e.Provider = N'Ig'
          AND e.Kind = N'Demo'
          AND e.EndpointProfile = N'IgDemo') <> 1
        THROW 51002, 'Exactly one applied IG Demo broker environment is required.', 1;

    SELECT @BrokerEnvironmentId = e.BrokerEnvironmentId
    FROM dbo.BrokerEnvironmentSelections AS s
    JOIN dbo.BrokerEnvironments AS e
        ON e.BrokerEnvironmentId = s.AppliedBrokerEnvironmentId
    WHERE e.Name = N'IG Demo'
      AND e.Provider = N'Ig'
      AND e.Kind = N'Demo'
      AND e.EndpointProfile = N'IgDemo';

    SET @LockResource = CONCAT(
        N'InstrumentCycle/',
        LOWER(REPLACE(CONVERT(varchar(36), @BrokerEnvironmentId), '-', '')),
        N'/',
        CONVERT(char(8), @TradingDay, 112),
        N'/',
        @ScheduledSlot);

    EXEC @LockResult = sys.sp_getapplock
        @Resource = @LockResource,
        @LockMode = N'Exclusive',
        @LockOwner = N'Transaction',
        @LockTimeout = 30000;

    IF @LockResult < 0
        THROW 51003, 'Could not acquire the instrument cycle lock.', 1;

    IF NOT EXISTS (
        SELECT 1
        FROM dbo.InstrumentCollectionCycleStates WITH (UPDLOCK, HOLDLOCK)
        WHERE BrokerEnvironmentId = @BrokerEnvironmentId
          AND TradingDay = @TradingDay
          AND ScheduledSlot = @ScheduledSlot
          AND Outcome IN (N'Failed', N'Completed')
          AND (LeaseExpiresAtUtc IS NULL OR LeaseExpiresAtUtc <= @NowUtc))
        THROW 51004, 'The slot must be Failed or Completed with failed categories, exist, and have no active lease.', 1;

    IF EXISTS (
        SELECT 1
        FROM dbo.InstrumentCollectionCycleStates
        WHERE BrokerEnvironmentId = @BrokerEnvironmentId
          AND (TradingDay > @TradingDay
               OR (TradingDay = @TradingDay AND ScheduledSlot > @ScheduledSlot)))
        THROW 51005, 'A later slot exists; retrying an older slot would not be scheduled.', 1;

    IF EXISTS (
        SELECT 1
        FROM dbo.InstrumentCollectionCategoryAttempts
        WHERE BrokerEnvironmentId = @BrokerEnvironmentId
          AND TradingDay = @TradingDay
          AND ScheduledSlot = @ScheduledSlot
          AND State = N'Succeeded')
       OR EXISTS (
        SELECT 1
        FROM dbo.MarketCategoryInstrumentCollectionRuns
        WHERE BrokerEnvironmentId = @BrokerEnvironmentId
          AND TradingDay = @TradingDay
          AND ScheduledSlot = @ScheduledSlot)
        THROW 51006, 'This slot already published a collection; do not replay it.', 1;

    IF EXISTS (
        SELECT 1
        FROM dbo.InstrumentCollectionCycleStates
        WHERE BrokerEnvironmentId = @BrokerEnvironmentId
          AND TradingDay = @TradingDay
          AND ScheduledSlot = @ScheduledSlot
          AND Outcome = N'Completed')
       AND (NOT EXISTS (
            SELECT 1
            FROM dbo.InstrumentCollectionCategoryAttempts
            WHERE BrokerEnvironmentId = @BrokerEnvironmentId
              AND TradingDay = @TradingDay
              AND ScheduledSlot = @ScheduledSlot
              AND State = N'Failed')
            OR EXISTS (
            SELECT 1
            FROM dbo.InstrumentCollectionCategoryAttempts
            WHERE BrokerEnvironmentId = @BrokerEnvironmentId
              AND TradingDay = @TradingDay
              AND ScheduledSlot = @ScheduledSlot
              AND State <> N'Failed'))
        THROW 51009, 'A completed slot can only be reset when all its category attempts failed.', 1;

    IF NOT EXISTS (
        SELECT 1
        FROM dbo.InstrumentCollectionSettings
        WHERE BrokerEnvironmentId = @BrokerEnvironmentId
          AND ApprovedNonTradingDailyRequestAllowance >
              (SELECT COALESCE(SUM(UsedRequestBudget), 0)
               FROM dbo.InstrumentCollectionCycleStates
               WHERE BrokerEnvironmentId = @BrokerEnvironmentId
                 AND TradingDay = @TradingDay))
        THROW 51007, 'No approved daily request budget remains for this trading day.', 1;

    SELECT TradingDay, ScheduledSlot, Outcome, CategoryPrerequisite,
           CategoryPrerequisiteAttempts, UsedRequestBudget, LeaseFence
    FROM dbo.InstrumentCollectionCycleStates
    WHERE BrokerEnvironmentId = @BrokerEnvironmentId
      AND TradingDay = @TradingDay
      AND ScheduledSlot = @ScheduledSlot;

    UPDATE dbo.InstrumentCollectionCategoryAttempts
    SET State = N'Pending',
        Attempts = 0,
        LeaseFence = 0,
        SafeError = NULL,
        UpdatedAtUtc = @NowUtc
    WHERE BrokerEnvironmentId = @BrokerEnvironmentId
      AND TradingDay = @TradingDay
      AND ScheduledSlot = @ScheduledSlot;

    UPDATE dbo.InstrumentCollectionCycleStates
    SET Outcome = N'Pending',
        CategoryPrerequisite = N'Pending',
        CategoryPrerequisiteSafeError = NULL,
        CategoryPrerequisiteAttempts = 0,
        CategoryPrerequisiteLeaseFence = 0,
        LeaseOwner = NULL,
        LeaseExpiresAtUtc = NULL
    WHERE BrokerEnvironmentId = @BrokerEnvironmentId
      AND TradingDay = @TradingDay
      AND ScheduledSlot = @ScheduledSlot
      AND Outcome IN (N'Failed', N'Completed');

    IF @@ROWCOUNT <> 1
        THROW 51008, 'The slot changed before it could be reset.', 1;

    COMMIT TRANSACTION;

    SELECT TradingDay, ScheduledSlot, Outcome, CategoryPrerequisite,
           CategoryPrerequisiteAttempts, UsedRequestBudget, LeaseFence
    FROM dbo.InstrumentCollectionCycleStates
    WHERE BrokerEnvironmentId = @BrokerEnvironmentId
      AND TradingDay = @TradingDay
      AND ScheduledSlot = @ScheduledSlot;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
