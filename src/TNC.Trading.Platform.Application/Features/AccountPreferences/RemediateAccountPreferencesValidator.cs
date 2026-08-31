namespace TNC.Trading.Platform.Application.Features.AccountPreferences;

internal sealed class RemediateAccountPreferencesValidator
{
    public IReadOnlyList<string> Validate(RemediateAccountPreferencesRequest request)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(request.TargetAccountId)) errors.Add("TargetAccountId is required.");
        if (request.DesiredRevision <= 0) errors.Add("DesiredRevision must be positive.");
        if (string.IsNullOrWhiteSpace(request.Actor)) errors.Add("Actor is required.");
        if (string.IsNullOrWhiteSpace(request.CorrelationId)) errors.Add("CorrelationId is required.");
        return errors;
    }
}