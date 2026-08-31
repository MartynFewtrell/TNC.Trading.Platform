namespace TNC.Trading.Platform.Application.Features.AccountPreferences;

internal sealed class UpdateAccountPreferencesValidator
{
    public IReadOnlyList<string> Validate(UpdateAccountPreferencesRequest request)
    {
        var errors = new List<string>();
        if (request.TrailingStopsEnabled is null) errors.Add("TrailingStopsEnabled is required.");
        if (request.TargetAccountId is not null && string.IsNullOrWhiteSpace(request.TargetAccountId)) errors.Add("TargetAccountId is required.");
        if (request.TargetAccountId is not null && string.IsNullOrWhiteSpace(request.Actor)) errors.Add("Actor is required.");
        return errors;
    }
}