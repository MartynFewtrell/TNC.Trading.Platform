namespace TNC.Trading.Platform.Api.Infrastructure.Platform;

internal sealed class PlatformValidationException(IReadOnlyDictionary<string, string[]> errors) : Exception("Platform validation failed")
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;
}
