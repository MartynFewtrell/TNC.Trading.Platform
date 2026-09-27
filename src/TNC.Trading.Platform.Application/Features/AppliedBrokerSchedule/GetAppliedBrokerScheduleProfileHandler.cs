using TNC.Trading.Platform.Application.Configuration;

namespace TNC.Trading.Platform.Application.Features.AppliedBrokerSchedule;

internal sealed class GetAppliedBrokerScheduleProfileHandler(IAppliedBrokerScheduleProfileStore store)
{
    public async Task<GetAppliedBrokerScheduleProfileResponse> HandleAsync(
        GetAppliedBrokerScheduleProfileRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new(await store.GetAppliedAsync(cancellationToken).ConfigureAwait(false));
    }
}
