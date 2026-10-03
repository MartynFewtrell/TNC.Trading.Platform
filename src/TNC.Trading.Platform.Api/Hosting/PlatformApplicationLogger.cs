using Microsoft.Extensions.Logging;
using TNC.Trading.Platform.Application.Services;

namespace TNC.Trading.Platform.Api.Hosting;

internal sealed class PlatformApplicationLogger(ILogger<PlatformApplicationLogger> logger) : IPlatformApplicationLogger
{
    public void LogWarning(string message, params object?[] arguments)
    {
        if (arguments.Length == 0)
        {
            logger.LogWarning("{Message}", message);
        }
        else
        {
            logger.LogWarning(message, arguments);
        }
    }

    public void LogWarning(Exception exception, string message) => logger.LogWarning(exception, "{Message}", message);

    public void LogError(Exception exception, string message, params object?[] arguments)
    {
        if (arguments.Length == 0)
        {
            logger.LogError(exception, "{Message}", message);
        }
        else
        {
            logger.LogError(exception, message, arguments);
        }
    }

    public void LogInformation(string message, params object?[] arguments) => logger.LogInformation(message, arguments);
}