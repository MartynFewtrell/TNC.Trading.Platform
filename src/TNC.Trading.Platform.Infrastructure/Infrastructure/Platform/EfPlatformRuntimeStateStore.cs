using Microsoft.EntityFrameworkCore;
using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Services;
using TNC.Trading.Platform.Infrastructure.Persistence;

namespace TNC.Trading.Platform.Infrastructure.Platform;

internal sealed class EfPlatformRuntimeStateStore(PlatformDbContext dbContext) : IPlatformRuntimeStateStore
{
    public async Task<PlatformRuntimeState> GetOrCreateAsync(CancellationToken cancellationToken)
    {
        var entity = await dbContext.AuthRuntimeStates.SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (entity is null)
        {
            entity = new AuthRuntimeStateEntity
            {
                TradingScheduleStatus = "Inactive",
                SessionStatus = PlatformSessionStatus.Unknown.ToString(),
                IsDegraded = false,
                RetryPhase = AuthRetryPhase.None.ToString(),
                AutomaticAttemptNumber = 0,
                RetryLimitReached = false
            };

            dbContext.AuthRuntimeStates.Add(entity);
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return Map(entity);
    }

    public async Task SaveAsync(PlatformRuntimeState state, CancellationToken cancellationToken)
    {
        var entity = await dbContext.AuthRuntimeStates.SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (entity is null)
        {
            entity = new AuthRuntimeStateEntity();
            dbContext.AuthRuntimeStates.Add(entity);
        }

        entity.PlatformEnvironment = state.PlatformEnvironment;
        entity.BrokerEnvironment = state.BrokerEnvironment;
        entity.TradingScheduleStatus = state.TradingScheduleStatus;
        entity.SessionStatus = state.SessionStatus.ToString();
        entity.IsDegraded = state.IsDegraded;
        entity.BlockedReason = state.BlockedReason;
        entity.RetryPhase = state.RetryPhase.ToString();
        entity.AutomaticAttemptNumber = state.AutomaticAttemptNumber;
        entity.NextRetryAtUtc = state.NextRetryAtUtc;
        entity.RetryLimitReached = state.RetryLimitReached;
        entity.CurrentRetryCycleId = state.CurrentRetryCycleId;
        entity.EstablishedAtUtc = state.EstablishedAtUtc;
        entity.ExpiresAtUtc = state.ExpiresAtUtc;
        entity.LastValidatedAtUtc = state.LastValidatedAtUtc;
        entity.LastTransitionAtUtc = state.LastTransitionAtUtc;

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static PlatformRuntimeState Map(AuthRuntimeStateEntity entity)
    {
        return new PlatformRuntimeState
        {
            PlatformEnvironment = entity.PlatformEnvironment,
            BrokerEnvironment = entity.BrokerEnvironment,
            TradingScheduleStatus = entity.TradingScheduleStatus,
            SessionStatus = Enum.Parse<PlatformSessionStatus>(entity.SessionStatus, ignoreCase: true),
            IsDegraded = entity.IsDegraded,
            BlockedReason = entity.BlockedReason,
            RetryPhase = Enum.Parse<AuthRetryPhase>(entity.RetryPhase, ignoreCase: true),
            AutomaticAttemptNumber = entity.AutomaticAttemptNumber,
            NextRetryAtUtc = entity.NextRetryAtUtc,
            RetryLimitReached = entity.RetryLimitReached,
            CurrentRetryCycleId = entity.CurrentRetryCycleId,
            EstablishedAtUtc = entity.EstablishedAtUtc,
            ExpiresAtUtc = entity.ExpiresAtUtc,
            LastValidatedAtUtc = entity.LastValidatedAtUtc,
            LastTransitionAtUtc = entity.LastTransitionAtUtc
        };
    }
}
