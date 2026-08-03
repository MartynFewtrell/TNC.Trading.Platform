namespace TNC.Trading.Platform.Application.Features.UpdatePlatformConfiguration;

internal sealed class ConfigurationValidationException(IReadOnlyDictionary<string, string[]> errors) : Exception("Platform configuration validation failed")
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;
}