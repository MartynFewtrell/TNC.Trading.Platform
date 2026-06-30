using TNC.Trading.Platform.Web.Authentication;

namespace TNC.Trading.Platform.Web.Components.Pages;

internal sealed class ConfigurationPagePresenter(PlatformApiClient platformApiClient)
{
    public async Task<ConfigurationPageLoadResult> LoadAsync(CancellationToken cancellationToken)
    {
        try
        {
            var configuration = await platformApiClient.GetConfigurationAsync(cancellationToken);
            return ConfigurationPageLoadResult.Success(ConfigurationFormModelMapper.From(configuration));
        }
        catch (HttpRequestException exception)
        {
            return ConfigurationPageLoadResult.Failure($"Unable to load platform configuration: {exception.Message}");
        }
    }

    public async Task<ConfigurationPageSaveResult> SaveAsync(ConfigurationFormModel form, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(form);

        try
        {
            var updated = await platformApiClient.UpdateConfigurationAsync(
                ConfigurationFormModelMapper.ToRequest(form),
                cancellationToken);

            return ConfigurationPageSaveResult.Success(
                ConfigurationFormModelMapper.From(updated),
                updated.RestartRequired
                    ? "Configuration saved. Startup-fixed changes apply on the next platform start."
                    : "Configuration saved.");
        }
        catch (Exception exception) when (exception is FormatException or HttpRequestException or PlatformScopeChallengeRequiredException)
        {
            return ConfigurationPageSaveResult.Failure($"Configuration save failed: {exception.Message}");
        }
    }
}

internal sealed record ConfigurationPageLoadResult(ConfigurationFormModel? Form, string? ErrorMessage)
{
    public static ConfigurationPageLoadResult Success(ConfigurationFormModel form) => new(form, null);

    public static ConfigurationPageLoadResult Failure(string errorMessage) => new(null, errorMessage);
}

internal sealed record ConfigurationPageSaveResult(ConfigurationFormModel? UpdatedForm, string Message)
{
    public static ConfigurationPageSaveResult Success(ConfigurationFormModel updatedForm, string message) => new(updatedForm, message);

    public static ConfigurationPageSaveResult Failure(string message) => new(null, message);
}