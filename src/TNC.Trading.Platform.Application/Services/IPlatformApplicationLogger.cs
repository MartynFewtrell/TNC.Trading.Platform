namespace TNC.Trading.Platform.Application.Services;

internal interface IPlatformApplicationLogger
{
    void LogWarning(string message);

    void LogWarning(Exception exception, string message);

    void LogError(Exception exception, string message);

    void LogInformation(string message, params object?[] arguments);
}