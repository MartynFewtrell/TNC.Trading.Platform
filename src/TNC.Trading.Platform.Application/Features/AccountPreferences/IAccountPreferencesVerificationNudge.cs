namespace TNC.Trading.Platform.Application.Features.AccountPreferences;

internal interface IAccountPreferencesVerificationNudge
{
    Task<NudgeAccountPreferencesVerificationResponse> HandleAsync(NudgeAccountPreferencesVerificationRequest request, CancellationToken cancellationToken);
}