-- Guarded reset for one failed IG Demo market-data slot.
--
-- Safe default: DryRun. Run this file through sqlcmd (or SSMS SQLCMD mode) and
-- pass every invocation value with -v. Never use a copied/ad-hoc SQL variant.
--
-- Dry-run example (replace values with the reviewed local target and scope):
-- sqlcmd -S . -d platformdb -b -i infra\scripts\reset-ig-demo-market-data-recovery.sql ^
--   -v ExpectedServer="<exact @@SERVERNAME; host must match SERVERPROPERTY(MachineName)>" ^
--      ExpectedDatabase="platformdb" EnvironmentId="00000000-0000-0000-0000-000000000000" ^
--      EndpointProfile="IgDemo" TradingDay="2026-10-04" ScheduledSlot="0" ^
--      RecoveryScope="FailedSlotRearm" RecoveryMode="DryRun" ExpectedCycleRows="-1" ^
--      ExpectedAttemptRows="-1" ExpectedCoverageRows="-1" ExpectedIntentRows="-1" ^
--      ExpectedUsedRequestBudget="-1" ApplyConfirmation=""
--
-- Apply only after reviewing the dry-run result, confirming writers are stopped,
-- and pinning every expected count/budget value to that result:
--   RecoveryMode="Apply"
--   ExpectedCycleRows="<exact count>" ExpectedAttemptRows="<exact count>"
--   ExpectedCoverageRows="<exact count>" ExpectedIntentRows="<exact count>"
--   ExpectedUsedRequestBudget="<exact used budget>"
--   ApplyConfirmation="APPLY-FAILED-SLOT|<database>|<environment-guid>|<yyyy-MM-dd>|<slot>"
--
-- The script is deliberately limited to FailedSlotRearm. It retains snapshots,
-- collection/detail/full-run history and children, settings, interests,
-- exclusions, reservations and budget consumption. It does not restart workers,
-- contact IG, or delete data outside the selected failed slot's retry markers.

:setvar RecoveryMode "DryRun"
:setvar RecoveryScope "FailedSlotRearm"
:setvar ExpectedServer "REPLACE_WITH_LOCAL_SQLSERVER_NAME"
:setvar ExpectedDatabase "platformdb"
:setvar EnvironmentId "00000000-0000-0000-0000-000000000000"
:setvar EndpointProfile "IgDemo"
:setvar TradingDay "2000-01-01"
:setvar ScheduledSlot "0"
:setvar ExpectedCycleRows "-1"
:setvar ExpectedAttemptRows "-1"
:setvar ExpectedCoverageRows "-1"
:setvar ExpectedIntentRows "-1"
:setvar ExpectedUsedRequestBudget "-1"
:setvar ApplyConfirmation ""

SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @Mode nvarchar(16) = N'$(RecoveryMode)';
DECLARE @Scope nvarchar(32) = N'$(RecoveryScope)';
DECLARE @ExpectedServer sysname = N'$(ExpectedServer)';
DECLARE @ExpectedDatabase sysname = N'$(ExpectedDatabase)';
DECLARE @EnvironmentId uniqueidentifier = TRY_CONVERT(uniqueidentifier, N'$(EnvironmentId)');
DECLARE @EndpointProfile nvarchar(128) = N'$(EndpointProfile)';
DECLARE @TradingDay date = TRY_CONVERT(date, N'$(TradingDay)', 23);
DECLARE @ScheduledSlot int = TRY_CONVERT(int, N'$(ScheduledSlot)');
DECLARE @ExpectedCycleRows int = TRY_CONVERT(int, N'$(ExpectedCycleRows)');
DECLARE @ExpectedAttemptRows int = TRY_CONVERT(int, N'$(ExpectedAttemptRows)');
DECLARE @ExpectedCoverageRows int = TRY_CONVERT(int, N'$(ExpectedCoverageRows)');
DECLARE @ExpectedIntentRows int = TRY_CONVERT(int, N'$(ExpectedIntentRows)');
DECLARE @ExpectedUsedRequestBudget int = TRY_CONVERT(int, N'$(ExpectedUsedRequestBudget)');
DECLARE @ApplyConfirmation nvarchar(512) = N'$(ApplyConfirmation)';
DECLARE @NowUtc datetimeoffset(7) = TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00');
DECLARE @MachineName sysname = CONVERT(sysname, SERVERPROPERTY('MachineName'));
DECLARE @ExpectedServerHost sysname =
    LEFT(@ExpectedServer, CHARINDEX(N'\', @ExpectedServer + N'\') - 1);
DECLARE @EnvironmentKey varchar(32);
DECLARE @FullRunLockResource nvarchar(255);
DECLARE @CycleLockResource nvarchar(255);
DECLARE @DetailLockResource nvarchar(255);
DECLARE @BudgetLockResource nvarchar(255);
DECLARE @LockResult int;
DECLARE @CycleRows bigint;
DECLARE @AttemptRows bigint;
DECLARE @CoverageRows bigint;
DECLARE @IntentRows bigint;
DECLARE @UsedRequestBudget bigint;
DECLARE @SelectedCycleUsedBudget int;
DECLARE @ApprovedDailyAllowance int;
DECLARE @PublishedCategoryRunRows bigint;
DECLARE @DiagnosticRowsChanged int;
DECLARE @CoverageRowsChanged int;
DECLARE @CycleRowsChanged int;

IF @Mode NOT IN (N'DryRun', N'Apply')
    THROW 51000, 'RecoveryMode must be DryRun or Apply.', 1;
IF @Scope <> N'FailedSlotRearm'
    THROW 51001, 'Only the explicitly scoped FailedSlotRearm operation is supported.', 1;
IF @ExpectedServer IN (N'', N'REPLACE_WITH_LOCAL_SQLSERVER_NAME')
    THROW 51002, 'Set ExpectedServer to the exact local SQL Server instance name.', 1;
IF @ExpectedDatabase NOT IN (N'platformdb')
   AND @ExpectedDatabase NOT LIKE N'TncTradingPlatformIntegration[_]%'
    THROW 51003, 'Only platformdb or a fixture-owned integration database is an approved reset target.', 1;
IF @ExpectedServer <> CONVERT(sysname, @@SERVERNAME)
   OR @ExpectedServerHost <> @MachineName
    THROW 51004, 'ExpectedServer must exactly identify this SQL Server instance on its own machine.', 1;
IF DB_NAME() <> @ExpectedDatabase
    THROW 51005, 'The connected database does not match ExpectedDatabase.', 1;
IF @EnvironmentId IS NULL OR @EndpointProfile <> N'IgDemo' OR @TradingDay IS NULL
   OR @ScheduledSlot IS NULL OR @ScheduledSlot < 0
    THROW 51006, 'Environment, IG Demo profile, ISO trading day and nonnegative slot are required.', 1;
IF @ExpectedCycleRows IS NULL OR @ExpectedAttemptRows IS NULL OR @ExpectedCoverageRows IS NULL
   OR @ExpectedIntentRows IS NULL OR @ExpectedUsedRequestBudget IS NULL
    THROW 51007, 'Expected row counts and budget must be valid integers.', 1;
IF @Mode = N'Apply'
   AND (@ExpectedCycleRows < 0 OR @ExpectedAttemptRows < 0 OR @ExpectedCoverageRows < 0
        OR @ExpectedIntentRows < 0 OR @ExpectedUsedRequestBudget < 0)
    THROW 51008, 'Apply requires exact nonnegative dry-run counts and budget.', 1;

IF OBJECT_ID(N'dbo.BrokerEnvironmentSelections', N'U') IS NULL
   OR OBJECT_ID(N'dbo.BrokerEnvironments', N'U') IS NULL
   OR OBJECT_ID(N'dbo.InstrumentCollectionSettings', N'U') IS NULL
   OR OBJECT_ID(N'dbo.InstrumentCollectionCycleStates', N'U') IS NULL
   OR OBJECT_ID(N'dbo.InstrumentCollectionCategoryAttempts', N'U') IS NULL
   OR OBJECT_ID(N'dbo.MarketCategoryInstrumentCollectionRuns', N'U') IS NULL
   OR OBJECT_ID(N'dbo.MarketDataFullRuns', N'U') IS NULL
   OR OBJECT_ID(N'dbo.MarketDataFullRunSlotCoverages', N'U') IS NULL
   OR OBJECT_ID(N'dbo.MarketDataFullRunIntents', N'U') IS NULL
   OR OBJECT_ID(N'dbo.MarketDetailCollectionRuns', N'U') IS NULL
    THROW 51009, 'Required market-data tables are missing; apply and verify migrations first.', 1;
IF COL_LENGTH(N'dbo.InstrumentCollectionCategoryAttempts', N'FailureKind') IS NULL
   OR COL_LENGTH(N'dbo.InstrumentCollectionCategoryAttempts', N'ProviderErrorCode') IS NULL
   OR COL_LENGTH(N'dbo.InstrumentCollectionCategoryAttempts', N'HttpStatus') IS NULL
   OR COL_LENGTH(N'dbo.InstrumentCollectionCategoryAttempts', N'Operation') IS NULL
   OR COL_LENGTH(N'dbo.InstrumentCollectionCategoryAttempts', N'PageNumber') IS NULL
   OR COL_LENGTH(N'dbo.InstrumentCollectionCategoryAttempts', N'Expected') IS NULL
   OR COL_LENGTH(N'dbo.InstrumentCollectionCategoryAttempts', N'Actual') IS NULL
    THROW 51010, 'Diagnostic columns are missing; refusing a partial or unsafe diagnostic reset.', 1;
IF (SELECT COUNT(*)
    FROM sys.foreign_key_columns AS fkc
    JOIN sys.foreign_keys AS fk ON fk.object_id = fkc.constraint_object_id
    WHERE fk.parent_object_id = OBJECT_ID(N'dbo.MarketDataFullRunSlotCoverages')
      AND fk.referenced_object_id = OBJECT_ID(N'dbo.MarketDataFullRuns')
      AND COL_NAME(fkc.parent_object_id, fkc.parent_column_id) IN (N'RunId', N'BrokerEnvironmentId')
      AND COL_NAME(fkc.referenced_object_id, fkc.referenced_column_id) IN (N'RunId', N'BrokerEnvironmentId')) <> 2
    THROW 51011, 'Expected full-run coverage foreign-key shape is not present.', 1;

SET @EnvironmentKey = LOWER(REPLACE(CONVERT(varchar(36), @EnvironmentId), '-', ''));
SET @FullRunLockResource = CONCAT(N'MarketDataFullRuns/', @EnvironmentKey);
SET @CycleLockResource = CONCAT(N'InstrumentCycle/', @EnvironmentKey, N'/', CONVERT(char(8), @TradingDay, 112), N'/', @ScheduledSlot);
SET @DetailLockResource = CONCAT(N'MarketDetails/', @EnvironmentKey, N'/', CONVERT(char(8), @TradingDay, 112), N'/', @ScheduledSlot);
SET @BudgetLockResource = CONCAT(N'InstrumentBudget/', @EnvironmentKey, N'/', CONVERT(char(8), @TradingDay, 112));

BEGIN TRY
    BEGIN TRANSACTION;

    EXEC @LockResult = sys.sp_getapplock
        @Resource = @FullRunLockResource, @LockMode = N'Exclusive',
        @LockOwner = N'Transaction', @LockTimeout = 5000;
    IF @LockResult < 0
        THROW 51012, 'Could not acquire the market-data full-run lock.', 1;

    EXEC @LockResult = sys.sp_getapplock
        @Resource = @CycleLockResource, @LockMode = N'Exclusive',
        @LockOwner = N'Transaction', @LockTimeout = 5000;
    IF @LockResult < 0
        THROW 51013, 'Could not acquire the instrument-cycle lock.', 1;

    EXEC @LockResult = sys.sp_getapplock
        @Resource = @DetailLockResource, @LockMode = N'Exclusive',
        @LockOwner = N'Transaction', @LockTimeout = 5000;
    IF @LockResult < 0
        THROW 51014, 'Could not acquire the market-detail lock.', 1;

    EXEC @LockResult = sys.sp_getapplock
        @Resource = @BudgetLockResource, @LockMode = N'Exclusive',
        @LockOwner = N'Transaction', @LockTimeout = 5000;
    IF @LockResult < 0
        THROW 51015, 'Could not acquire the instrument-budget lock.', 1;

    IF (SELECT COUNT(*)
        FROM dbo.BrokerEnvironmentSelections WITH (UPDLOCK, HOLDLOCK)
        JOIN dbo.BrokerEnvironments WITH (UPDLOCK, HOLDLOCK)
          ON BrokerEnvironments.BrokerEnvironmentId = BrokerEnvironmentSelections.AppliedBrokerEnvironmentId
        WHERE BrokerEnvironments.BrokerEnvironmentId = @EnvironmentId
          AND BrokerEnvironments.Name = N'IG Demo'
          AND BrokerEnvironments.Provider = N'IG'
          AND BrokerEnvironments.Kind = N'Demo'
          AND BrokerEnvironments.Lifecycle = N'Active'
          AND BrokerEnvironments.EndpointProfile = @EndpointProfile
          AND BrokerEnvironmentSelections.AppliedBrokerEnvironmentId = @EnvironmentId) <> 1
        THROW 51016, 'The requested environment is not the uniquely applied, active IG Demo profile.', 1;

    SELECT @CycleRows = COUNT_BIG(*)
    FROM dbo.InstrumentCollectionCycleStates WITH (UPDLOCK, HOLDLOCK)
    WHERE BrokerEnvironmentId = @EnvironmentId AND TradingDay = @TradingDay AND ScheduledSlot = @ScheduledSlot;

    SELECT @AttemptRows = COUNT_BIG(*)
    FROM dbo.InstrumentCollectionCategoryAttempts WITH (UPDLOCK, HOLDLOCK)
    WHERE BrokerEnvironmentId = @EnvironmentId AND TradingDay = @TradingDay AND ScheduledSlot = @ScheduledSlot;

    SELECT @CoverageRows = COUNT_BIG(*)
    FROM dbo.MarketDataFullRunSlotCoverages WITH (UPDLOCK, HOLDLOCK)
    WHERE BrokerEnvironmentId = @EnvironmentId AND TradingDay = @TradingDay
      AND ScheduledSlot = @ScheduledSlot
      AND ScheduleRevision = (
          SELECT ScheduleRevision
          FROM dbo.InstrumentCollectionCycleStates
          WHERE BrokerEnvironmentId = @EnvironmentId AND TradingDay = @TradingDay AND ScheduledSlot = @ScheduledSlot);

    SELECT @IntentRows = COUNT_BIG(*)
    FROM dbo.MarketDataFullRunIntents WITH (UPDLOCK, HOLDLOCK)
    WHERE BrokerEnvironmentId = @EnvironmentId;

    SELECT @UsedRequestBudget = COALESCE(SUM(CONVERT(bigint, UsedRequestBudget)), 0)
    FROM dbo.InstrumentCollectionCycleStates WITH (UPDLOCK, HOLDLOCK)
    WHERE BrokerEnvironmentId = @EnvironmentId AND TradingDay = @TradingDay;

    SELECT @SelectedCycleUsedBudget = UsedRequestBudget
    FROM dbo.InstrumentCollectionCycleStates WITH (UPDLOCK, HOLDLOCK)
    WHERE BrokerEnvironmentId = @EnvironmentId AND TradingDay = @TradingDay AND ScheduledSlot = @ScheduledSlot;

    SELECT @ApprovedDailyAllowance = ApprovedNonTradingDailyRequestAllowance
    FROM dbo.InstrumentCollectionSettings WITH (UPDLOCK, HOLDLOCK)
    WHERE BrokerEnvironmentId = @EnvironmentId;

    IF @CycleRows <> 1
        THROW 51017, 'Exactly one selected cycle state must exist.', 1;
    IF @Mode = N'Apply'
       AND (@CycleRows <> @ExpectedCycleRows OR @AttemptRows <> @ExpectedAttemptRows
            OR @CoverageRows <> @ExpectedCoverageRows OR @IntentRows <> @ExpectedIntentRows
            OR @UsedRequestBudget <> @ExpectedUsedRequestBudget)
        THROW 51018, 'Preflight counts or budget changed; rerun dry-run and review before applying.', 1;
    IF @Mode = N'DryRun'
       AND ((@ExpectedCycleRows >= 0 AND @CycleRows <> @ExpectedCycleRows)
            OR (@ExpectedAttemptRows >= 0 AND @AttemptRows <> @ExpectedAttemptRows)
            OR (@ExpectedCoverageRows >= 0 AND @CoverageRows <> @ExpectedCoverageRows)
            OR (@ExpectedIntentRows >= 0 AND @IntentRows <> @ExpectedIntentRows)
            OR (@ExpectedUsedRequestBudget >= 0 AND @UsedRequestBudget <> @ExpectedUsedRequestBudget))
        THROW 51019, 'Dry-run counts differ from the supplied expected values.', 1;

    IF EXISTS (
        SELECT 1 FROM dbo.InstrumentCollectionCycleStates
        WHERE BrokerEnvironmentId = @EnvironmentId AND TradingDay = @TradingDay AND ScheduledSlot = @ScheduledSlot
          AND (Outcome NOT IN (N'Failed', N'Completed')
               OR (LeaseOwner IS NOT NULL AND (LeaseExpiresAtUtc IS NULL OR LeaseExpiresAtUtc > @NowUtc))))
        THROW 51020, 'The selected cycle is not failed/completed or still has an active owner.', 1;
    IF EXISTS (
        SELECT 1 FROM dbo.InstrumentCollectionCycleStates
        WHERE BrokerEnvironmentId = @EnvironmentId
          AND (TradingDay > @TradingDay OR (TradingDay = @TradingDay AND ScheduledSlot > @ScheduledSlot)))
        THROW 51021, 'A later cycle exists; an older slot cannot be reopened.', 1;
    IF EXISTS (
        SELECT 1 FROM dbo.InstrumentCollectionCategoryAttempts
        WHERE BrokerEnvironmentId = @EnvironmentId AND TradingDay = @TradingDay AND ScheduledSlot = @ScheduledSlot
          AND (State NOT IN (N'Failed', N'Pending') OR (State = N'Pending' AND Attempts <> 0)))
        THROW 51022, 'Successful or previously attempted nonfailed categories make this slot unsafe to replay.', 1;
    IF EXISTS (
        SELECT 1 FROM dbo.InstrumentCollectionCycleStates AS cycle
        WHERE cycle.BrokerEnvironmentId = @EnvironmentId AND cycle.TradingDay = @TradingDay
          AND cycle.ScheduledSlot = @ScheduledSlot AND cycle.Outcome = N'Completed'
          AND NOT EXISTS (
              SELECT 1 FROM dbo.InstrumentCollectionCategoryAttempts AS attempt
              WHERE attempt.BrokerEnvironmentId = cycle.BrokerEnvironmentId
                AND attempt.TradingDay = cycle.TradingDay AND attempt.ScheduledSlot = cycle.ScheduledSlot
                AND attempt.State = N'Failed'))
        THROW 51023, 'A completed cycle can only be rearmed when at least one category failed.', 1;

    SELECT @PublishedCategoryRunRows = COUNT_BIG(*)
    FROM dbo.MarketCategoryInstrumentCollectionRuns WITH (UPDLOCK, HOLDLOCK)
    WHERE BrokerEnvironmentId = @EnvironmentId AND TradingDay = @TradingDay AND ScheduledSlot = @ScheduledSlot;
    IF @PublishedCategoryRunRows <> 0
        THROW 51024, 'A category collection already published for this slot; preserve it and refuse replay.', 1;

    IF EXISTS (
        SELECT 1 FROM dbo.MarketDataFullRuns WITH (UPDLOCK, HOLDLOCK)
        WHERE BrokerEnvironmentId = @EnvironmentId AND Status = N'Running')
        THROW 51025, 'An active market-data full run exists for this environment.', 1;
    IF EXISTS (
        SELECT 1 FROM dbo.MarketDetailCollectionRuns WITH (UPDLOCK, HOLDLOCK)
        WHERE BrokerEnvironmentId = @EnvironmentId AND TradingDay = @TradingDay AND ScheduledSlot = @ScheduledSlot
          AND (Status = N'Running' OR (LeaseOwner IS NOT NULL AND (LeaseExpiresAtUtc IS NULL OR LeaseExpiresAtUtc > @NowUtc))))
        THROW 51026, 'An active market-detail run exists for this environment/day/slot.', 1;
    IF EXISTS (
        SELECT 1
        FROM dbo.MarketDataFullRunSlotCoverages AS coverage WITH (UPDLOCK, HOLDLOCK)
        JOIN dbo.MarketDataFullRuns AS run WITH (UPDLOCK, HOLDLOCK) ON run.RunId = coverage.RunId
        JOIN dbo.InstrumentCollectionCycleStates AS cycle
          ON cycle.BrokerEnvironmentId = coverage.BrokerEnvironmentId
         AND cycle.TradingDay = coverage.TradingDay
         AND cycle.ScheduledSlot = coverage.ScheduledSlot
         AND cycle.ScheduleRevision = coverage.ScheduleRevision
        WHERE coverage.BrokerEnvironmentId = @EnvironmentId AND coverage.TradingDay = @TradingDay
          AND coverage.ScheduledSlot = @ScheduledSlot
          AND (run.Status <> N'Completed' OR run.Outcome <> N'Partial'
               OR run.EndpointProfile <> @EndpointProfile OR run.TradingDay <> @TradingDay))
        THROW 51027, 'Only matching completed partial-run coverage may be released.', 1;
    IF COALESCE(@ApprovedDailyAllowance, 0) <= @UsedRequestBudget
        THROW 51028, 'No approved daily allowance remains; reset cannot create budget.', 1;
    IF @Mode = N'Apply'
       AND @ApplyConfirmation <> CONCAT(
           N'APPLY-FAILED-SLOT|', @ExpectedDatabase, N'|', CONVERT(nvarchar(36), @EnvironmentId),
           N'|', CONVERT(char(10), @TradingDay, 23), N'|', @ScheduledSlot)
        THROW 51029, 'ApplyConfirmation does not match the exact database/environment/day/slot.', 1;

    CREATE TABLE #PreservedCounts (Name sysname NOT NULL PRIMARY KEY, PreservedRows bigint NOT NULL);
    INSERT #PreservedCounts (Name, PreservedRows)
    SELECT N'BrokerEnvironments', COUNT_BIG(*) FROM dbo.BrokerEnvironments
    UNION ALL SELECT N'BrokerEnvironmentSelections', COUNT_BIG(*) FROM dbo.BrokerEnvironmentSelections
    UNION ALL SELECT N'InstrumentCollectionSettings', COUNT_BIG(*) FROM dbo.InstrumentCollectionSettings
    UNION ALL SELECT N'MarketCategories', COUNT_BIG(*) FROM dbo.MarketCategories
    UNION ALL SELECT N'MarketCategoryCatalogStates', COUNT_BIG(*) FROM dbo.MarketCategoryCatalogStates
    UNION ALL SELECT N'MarketCategoryInterests', COUNT_BIG(*) FROM dbo.MarketCategoryInterests
    UNION ALL SELECT N'MarketCategoryInterestStates', COUNT_BIG(*) FROM dbo.MarketCategoryInterestStates
    UNION ALL SELECT N'MarketCategoryInstruments', COUNT_BIG(*) FROM dbo.MarketCategoryInstruments
    UNION ALL SELECT N'MarketCategoryInstrumentCatalogStates', COUNT_BIG(*) FROM dbo.MarketCategoryInstrumentCatalogStates
    UNION ALL SELECT N'MarketCategoryInstrumentCollectionRuns', COUNT_BIG(*) FROM dbo.MarketCategoryInstrumentCollectionRuns
    UNION ALL SELECT N'MarketCategoryInstrumentObservations', COUNT_BIG(*) FROM dbo.MarketCategoryInstrumentObservations
    UNION ALL SELECT N'MarketDetailCollectionRuns', COUNT_BIG(*) FROM dbo.MarketDetailCollectionRuns
    UNION ALL SELECT N'MarketDetailCurrent', COUNT_BIG(*) FROM dbo.MarketDetailCurrent
    UNION ALL SELECT N'MarketDetailEligibility', COUNT_BIG(*) FROM dbo.MarketDetailEligibility
    UNION ALL SELECT N'MarketDetailObservations', COUNT_BIG(*) FROM dbo.MarketDetailObservations
    UNION ALL SELECT N'MarketDetailRunMemberships', COUNT_BIG(*) FROM dbo.MarketDetailRunMemberships
    UNION ALL SELECT N'MarketDetailRunSources', COUNT_BIG(*) FROM dbo.MarketDetailRunSources
    UNION ALL SELECT N'MarketDetailRunTargets', COUNT_BIG(*) FROM dbo.MarketDetailRunTargets
    UNION ALL SELECT N'IgProviderRateReservations', COUNT_BIG(*) FROM dbo.IgProviderRateReservations
    UNION ALL SELECT N'MarketDataFullRuns', COUNT_BIG(*) FROM dbo.MarketDataFullRuns
    UNION ALL SELECT N'MarketDataFullRunCategories', COUNT_BIG(*) FROM dbo.MarketDataFullRunCategories
    UNION ALL SELECT N'MarketDataFullRunStages', COUNT_BIG(*) FROM dbo.MarketDataFullRunStages
    UNION ALL SELECT N'MarketDataFullRunItems', COUNT_BIG(*) FROM dbo.MarketDataFullRunItems
    UNION ALL SELECT N'MarketDataFullRunIntents', COUNT_BIG(*) FROM dbo.MarketDataFullRunIntents
    UNION ALL SELECT N'ProtectedCredentials', COUNT_BIG(*) FROM dbo.ProtectedCredentials
    UNION ALL SELECT N'DataProtectionKeys', COUNT_BIG(*) FROM dbo.DataProtectionKeys;

    SELECT N'PREVIEW' AS Result, @Mode AS Mode, @Scope AS Scope, @ExpectedServer AS ServerName,
           @ExpectedDatabase AS DatabaseName, @EnvironmentId AS BrokerEnvironmentId,
           @EndpointProfile AS EndpointProfile, @TradingDay AS TradingDay, @ScheduledSlot AS ScheduledSlot,
           @CycleRows AS CycleRows, @AttemptRows AS AttemptRows, @CoverageRows AS ReleasableCoverageRows,
           @IntentRows AS PreservedIntentRows, @UsedRequestBudget AS PreservedUsedRequestBudget,
           @ApprovedDailyAllowance AS ApprovedDailyAllowance, @PublishedCategoryRunRows AS PublishedCategoryRunRows;
    SELECT Name, PreservedRows FROM #PreservedCounts ORDER BY Name;

    IF @Mode = N'DryRun'
    BEGIN
        ROLLBACK TRANSACTION;
        RETURN;
    END;

    UPDATE dbo.InstrumentCollectionCategoryAttempts
    SET State = N'Pending',
        Attempts = 0,
        SafeError = NULL,
        FailureKind = NULL,
        ProviderErrorCode = NULL,
        HttpStatus = NULL,
        Operation = NULL,
        PageNumber = NULL,
        Expected = NULL,
        Actual = NULL,
        UpdatedAtUtc = @NowUtc
    WHERE BrokerEnvironmentId = @EnvironmentId AND TradingDay = @TradingDay AND ScheduledSlot = @ScheduledSlot;
    SET @DiagnosticRowsChanged = @@ROWCOUNT;
    IF @DiagnosticRowsChanged <> @AttemptRows
        THROW 51030, 'Attempt diagnostic rows changed after preflight.', 1;

    DELETE coverage
    FROM dbo.MarketDataFullRunSlotCoverages AS coverage
    JOIN dbo.InstrumentCollectionCycleStates AS cycle
      ON cycle.BrokerEnvironmentId = coverage.BrokerEnvironmentId
     AND cycle.TradingDay = coverage.TradingDay
     AND cycle.ScheduledSlot = coverage.ScheduledSlot
     AND cycle.ScheduleRevision = coverage.ScheduleRevision
    WHERE coverage.BrokerEnvironmentId = @EnvironmentId AND coverage.TradingDay = @TradingDay
      AND coverage.ScheduledSlot = @ScheduledSlot;
    SET @CoverageRowsChanged = @@ROWCOUNT;
    IF @CoverageRowsChanged <> @CoverageRows
        THROW 51031, 'Full-run coverage changed after preflight.', 1;

    UPDATE dbo.InstrumentCollectionCycleStates
    SET Outcome = N'Pending',
        CategoryPrerequisite = N'Pending',
        CategoryPrerequisiteSafeError = NULL,
        CategoryPrerequisiteAttempts = 0,
        LeaseOwner = NULL,
        LeaseExpiresAtUtc = NULL
    WHERE BrokerEnvironmentId = @EnvironmentId AND TradingDay = @TradingDay AND ScheduledSlot = @ScheduledSlot
      AND Outcome IN (N'Failed', N'Completed')
      AND (LeaseExpiresAtUtc IS NULL OR LeaseExpiresAtUtc <= @NowUtc);
    SET @CycleRowsChanged = @@ROWCOUNT;
    IF @CycleRowsChanged <> 1
        THROW 51032, 'The cycle changed or acquired an active owner during reset.', 1;

    IF EXISTS (
        SELECT 1 FROM dbo.InstrumentCollectionCycleStates
        WHERE BrokerEnvironmentId = @EnvironmentId AND TradingDay = @TradingDay AND ScheduledSlot = @ScheduledSlot
          AND (UsedRequestBudget <> @SelectedCycleUsedBudget
               OR Outcome <> N'Pending' OR CategoryPrerequisite <> N'Pending'
               OR CategoryPrerequisiteAttempts <> 0 OR LeaseOwner IS NOT NULL OR LeaseExpiresAtUtc IS NOT NULL))
        THROW 51033, 'The selected cycle did not reach the expected rearmed state.', 1;
    IF EXISTS (
        SELECT 1 FROM dbo.InstrumentCollectionCategoryAttempts
        WHERE BrokerEnvironmentId = @EnvironmentId AND TradingDay = @TradingDay AND ScheduledSlot = @ScheduledSlot
          AND (State <> N'Pending' OR Attempts <> 0 OR SafeError IS NOT NULL OR FailureKind IS NOT NULL
               OR ProviderErrorCode IS NOT NULL OR HttpStatus IS NOT NULL OR Operation IS NOT NULL
               OR PageNumber IS NOT NULL OR Expected IS NOT NULL OR Actual IS NOT NULL))
        THROW 51034, 'A selected category diagnostic was not fully cleared.', 1;
    IF EXISTS (
        SELECT 1 FROM dbo.MarketDataFullRunSlotCoverages
        WHERE BrokerEnvironmentId = @EnvironmentId AND TradingDay = @TradingDay AND ScheduledSlot = @ScheduledSlot
          AND ScheduleRevision = (
              SELECT ScheduleRevision FROM dbo.InstrumentCollectionCycleStates
              WHERE BrokerEnvironmentId = @EnvironmentId AND TradingDay = @TradingDay AND ScheduledSlot = @ScheduledSlot))
        THROW 51035, 'Selected full-run coverage remains after reset.', 1;
    IF (SELECT COALESCE(SUM(CONVERT(bigint, UsedRequestBudget)), 0)
        FROM dbo.InstrumentCollectionCycleStates
        WHERE BrokerEnvironmentId = @EnvironmentId AND TradingDay = @TradingDay) <> @UsedRequestBudget
        THROW 51036, 'Daily budget consumption changed during reset.', 1;

    CREATE TABLE #AfterCounts (Name sysname NOT NULL PRIMARY KEY, PreservedRows bigint NOT NULL);
    INSERT #AfterCounts (Name, PreservedRows)
    SELECT N'BrokerEnvironments', COUNT_BIG(*) FROM dbo.BrokerEnvironments
    UNION ALL SELECT N'BrokerEnvironmentSelections', COUNT_BIG(*) FROM dbo.BrokerEnvironmentSelections
    UNION ALL SELECT N'InstrumentCollectionSettings', COUNT_BIG(*) FROM dbo.InstrumentCollectionSettings
    UNION ALL SELECT N'MarketCategories', COUNT_BIG(*) FROM dbo.MarketCategories
    UNION ALL SELECT N'MarketCategoryCatalogStates', COUNT_BIG(*) FROM dbo.MarketCategoryCatalogStates
    UNION ALL SELECT N'MarketCategoryInterests', COUNT_BIG(*) FROM dbo.MarketCategoryInterests
    UNION ALL SELECT N'MarketCategoryInterestStates', COUNT_BIG(*) FROM dbo.MarketCategoryInterestStates
    UNION ALL SELECT N'MarketCategoryInstruments', COUNT_BIG(*) FROM dbo.MarketCategoryInstruments
    UNION ALL SELECT N'MarketCategoryInstrumentCatalogStates', COUNT_BIG(*) FROM dbo.MarketCategoryInstrumentCatalogStates
    UNION ALL SELECT N'MarketCategoryInstrumentCollectionRuns', COUNT_BIG(*) FROM dbo.MarketCategoryInstrumentCollectionRuns
    UNION ALL SELECT N'MarketCategoryInstrumentObservations', COUNT_BIG(*) FROM dbo.MarketCategoryInstrumentObservations
    UNION ALL SELECT N'MarketDetailCollectionRuns', COUNT_BIG(*) FROM dbo.MarketDetailCollectionRuns
    UNION ALL SELECT N'MarketDetailCurrent', COUNT_BIG(*) FROM dbo.MarketDetailCurrent
    UNION ALL SELECT N'MarketDetailEligibility', COUNT_BIG(*) FROM dbo.MarketDetailEligibility
    UNION ALL SELECT N'MarketDetailObservations', COUNT_BIG(*) FROM dbo.MarketDetailObservations
    UNION ALL SELECT N'MarketDetailRunMemberships', COUNT_BIG(*) FROM dbo.MarketDetailRunMemberships
    UNION ALL SELECT N'MarketDetailRunSources', COUNT_BIG(*) FROM dbo.MarketDetailRunSources
    UNION ALL SELECT N'MarketDetailRunTargets', COUNT_BIG(*) FROM dbo.MarketDetailRunTargets
    UNION ALL SELECT N'IgProviderRateReservations', COUNT_BIG(*) FROM dbo.IgProviderRateReservations
    UNION ALL SELECT N'MarketDataFullRuns', COUNT_BIG(*) FROM dbo.MarketDataFullRuns
    UNION ALL SELECT N'MarketDataFullRunCategories', COUNT_BIG(*) FROM dbo.MarketDataFullRunCategories
    UNION ALL SELECT N'MarketDataFullRunStages', COUNT_BIG(*) FROM dbo.MarketDataFullRunStages
    UNION ALL SELECT N'MarketDataFullRunItems', COUNT_BIG(*) FROM dbo.MarketDataFullRunItems
    UNION ALL SELECT N'MarketDataFullRunIntents', COUNT_BIG(*) FROM dbo.MarketDataFullRunIntents
    UNION ALL SELECT N'ProtectedCredentials', COUNT_BIG(*) FROM dbo.ProtectedCredentials
    UNION ALL SELECT N'DataProtectionKeys', COUNT_BIG(*) FROM dbo.DataProtectionKeys;
    IF EXISTS (SELECT Name, PreservedRows FROM #PreservedCounts EXCEPT SELECT Name, PreservedRows FROM #AfterCounts)
       OR EXISTS (SELECT Name, PreservedRows FROM #AfterCounts EXCEPT SELECT Name, PreservedRows FROM #PreservedCounts)
        THROW 51037, 'A protected table row count changed; rolling back the reset.', 1;

    SELECT N'APPLIED' AS Result, @Mode AS Mode, @Scope AS Scope, @EnvironmentId AS BrokerEnvironmentId,
           @TradingDay AS TradingDay, @ScheduledSlot AS ScheduledSlot,
           @DiagnosticRowsChanged AS DiagnosticRowsCleared, @CoverageRowsChanged AS CoverageRowsReleased,
           @CycleRowsChanged AS CycleRowsRearmed, @UsedRequestBudget AS PreservedUsedRequestBudget;
    SELECT Name, PreservedRows FROM #AfterCounts ORDER BY Name;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
