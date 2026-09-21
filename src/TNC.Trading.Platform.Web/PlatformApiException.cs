using System.Net;

namespace TNC.Trading.Platform.Web;

internal sealed class PlatformApiException(
    string message,
    HttpStatusCode statusCode,
    string type,
    string failureCategory,
    string title,
    string detail) : HttpRequestException(message, null, statusCode)
{
    public string ProblemType { get; } = type;
    public string FailureCategory { get; } = failureCategory;
    public string Title { get; } = title;
    public string Detail { get; } = detail;
}