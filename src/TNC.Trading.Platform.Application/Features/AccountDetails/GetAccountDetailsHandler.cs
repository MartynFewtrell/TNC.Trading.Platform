using TNC.Trading.Platform.Application.Services;

namespace TNC.Trading.Platform.Application.Features.AccountDetails;

internal sealed class GetAccountDetailsHandler(
    PlatformConfigurationService configurationService,
    IAccountDetailsSnapshotStore snapshotStore)
{
    public async Task<GetAccountDetailsResponse> HandleAsync(GetAccountDetailsRequest request, CancellationToken cancellationToken)
    {
        var configuration = await configurationService.GetCurrentAsync(cancellationToken).ConfigureAwait(false);
        AccountDetailsSnapshot? snapshot;
        if (string.IsNullOrWhiteSpace(request.Cursor))
        {
            snapshot = await snapshotStore.GetLatestAsync(configuration.BrokerEnvironment, cancellationToken).ConfigureAwait(false);
        }
        else if (!AccountDetailsCursor.TryDecode(request.Cursor, out var cursor) || cursor is null)
        {
            return new GetAccountDetailsResponse(null, null, null);
        }
        else
        {
            snapshot = await snapshotStore.GetBeforeAsync(configuration.BrokerEnvironment, cursor, cancellationToken).ConfigureAwait(false);
        }

        if (snapshot is null)
        {
            return new GetAccountDetailsResponse(null, null, null);
        }

        var currentCursor = new AccountDetailsCursor(snapshot.RetrievedAtUtc, snapshot.RetrievalId);
        var older = await snapshotStore.GetBeforeAsync(configuration.BrokerEnvironment, currentCursor, cancellationToken).ConfigureAwait(false);
        var newer = await snapshotStore.GetAfterAsync(configuration.BrokerEnvironment, currentCursor, cancellationToken).ConfigureAwait(false);
        return new GetAccountDetailsResponse(
            snapshot,
            older is null ? null : new AccountDetailsCursor(older.RetrievedAtUtc, older.RetrievalId).Encode(),
            newer is null ? null : new AccountDetailsCursor(newer.RetrievedAtUtc, newer.RetrievalId).Encode());
    }
}
