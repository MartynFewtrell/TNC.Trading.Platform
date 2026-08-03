using Microsoft.EntityFrameworkCore;
using TNC.Trading.Platform.Application.Features.TriggerManualAuthRetry.Ports;
using TNC.Trading.Platform.Application.Services;
using TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework;

namespace TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework;

internal sealed class ManualAuthRetryCommitter(
    PlatformDbContext dbContext,
    IPlatformRuntimeStateStore runtimeStateStore,
    IPlatformRetryCycleStore retryCycleStore,
    IPlatformEventStore eventStore,
    IPlatformIgLoginSnapshotStore loginSnapshotStore,
    IPlatformIgProofDataStore proofDataStore) : IManualAuthRetryCommitter
{
    public async Task CommitAsync(ManualAuthRetryCommitIntent intent, CancellationToken cancellationToken)
    {
        await using var transaction = dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false)
            : null;

        try
        {
            if (intent.LoginSnapshot is not null)
            {
                await loginSnapshotStore.CaptureSuccessfulSnapshotAsync(intent.LoginSnapshot, cancellationToken).ConfigureAwait(false);
            }

            if (intent.ProofData is not null)
            {
                await proofDataStore.SaveAsync(intent.Configuration.BrokerEnvironment, intent.ProofData, cancellationToken).ConfigureAwait(false);
            }

            await retryCycleStore.UpsertAsync(intent.RetryCycle, cancellationToken).ConfigureAwait(false);
            await eventStore.AddAsync(intent.Event, cancellationToken).ConfigureAwait(false);
            await runtimeStateStore.SaveAsync(intent.State, cancellationToken).ConfigureAwait(false);

            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            }

            throw;
        }
    }
}