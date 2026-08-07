namespace TNC.Trading.Platform.Application.Features.AccountDetails;

internal interface IAccountDetailsDailyCapture
{
    Task<CaptureDailyAccountDetailsResponse> HandleAsync(CaptureDailyAccountDetailsRequest request, CancellationToken cancellationToken);
}
