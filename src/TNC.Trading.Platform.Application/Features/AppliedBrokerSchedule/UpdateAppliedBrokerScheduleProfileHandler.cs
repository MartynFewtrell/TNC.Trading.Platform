using TNC.Trading.Platform.Application.Configuration;

namespace TNC.Trading.Platform.Application.Features.AppliedBrokerSchedule;

internal sealed class UpdateAppliedBrokerScheduleProfileHandler(
    IAppliedBrokerScheduleProfileStore store,
    AppliedBrokerScheduleProfileValidator validator)
{
    public async Task<UpdateAppliedBrokerScheduleProfileResponse> HandleAsync(
        UpdateAppliedBrokerScheduleProfileRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        validator.Validate(request.TradingSchedule);
        return new(await store.SaveAppliedAsync(request.TradingSchedule, request.Actor, cancellationToken).ConfigureAwait(false));
    }
}
