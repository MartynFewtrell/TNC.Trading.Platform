using Microsoft.Extensions.Logging;
using TNC.Trading.Platform.Application.Services;

namespace TNC.Trading.Platform.Api.Hosting;

internal sealed class PlatformApplicationLogger(ILogger<PlatformApplicationLogger> logger) : IPlatformApplicationLogger
{
    public void LogWarning(string message) => logger.LogWarning("{Message}", message);

    public void LogWarning(Exception exception, string message) => logger.LogWarning(exception, "{Message}", message);

    public void LogError(Exception exception, string message) => logger.LogError(exception, "{Message}", message);

    public void LogInformation(string message, params object?[] arguments) => logger.LogInformation(message, arguments);
}