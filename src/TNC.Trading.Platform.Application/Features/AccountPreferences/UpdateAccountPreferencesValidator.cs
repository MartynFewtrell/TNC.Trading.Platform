namespace TNC.Trading.Platform.Application.Features.AccountPreferences;

internal sealed class UpdateAccountPreferencesValidator
{
    public IReadOnlyList<string> Validate(UpdateAccountPreferencesRequest request) =>
        request.TrailingStopsEnabled is null ? ["TrailingStopsEnabled is required."] : [];
}