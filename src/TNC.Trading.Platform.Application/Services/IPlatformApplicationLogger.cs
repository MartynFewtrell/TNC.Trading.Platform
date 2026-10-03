namespace TNC.Trading.Platform.Application.Services;

internal interface IPlatformApplicationLogger
{
    void LogWarning(string message, params object?[] arguments);

    void LogWarning(Exception exception, string message);

    void LogError(Exception exception, string message, params object?[] arguments);

    void LogInformation(string message, params object?[] arguments);
}