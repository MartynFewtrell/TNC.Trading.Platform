using System.Net;

namespace TNC.Trading.Platform.Web.UnitTests;

internal sealed class RecordingHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage>? responseFactory = null) : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> responseFactory = responseFactory
        ?? (_ => new HttpResponseMessage(HttpStatusCode.Accepted));

    public int CallCount { get; private set; }

    public string? LastAuthorizationHeader { get; private set; }

    public string? LastContent { get; private set; }

    public string? LastRequestUri { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        CallCount++;
        LastRequestUri = request.RequestUri?.ToString();
        LastAuthorizationHeader = request.Headers.Authorization?.ToString();
        LastContent = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken);

        return responseFactory(request);
    }
}
