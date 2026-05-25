using System.Net;

namespace TNC.Trading.Platform.Web.UnitTests;

internal sealed class SequencedHttpMessageHandler(params Func<HttpRequestMessage, HttpResponseMessage>[] responseFactories) : HttpMessageHandler
{
    private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> responseFactories = new(responseFactories);

    public List<RecordedRequest> Requests { get; } = [];

    public int CallCount => Requests.Count;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var content = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken);

        Requests.Add(new RecordedRequest(
            request.Method,
            request.RequestUri?.ToString() ?? string.Empty,
            request.Headers.Authorization?.ToString(),
            content));

        if (responseFactories.Count == 0)
        {
            throw new InvalidOperationException("No response was configured for the HTTP request.");
        }

        return responseFactories.Dequeue()(request);
    }

    internal sealed record RecordedRequest(
        HttpMethod Method,
        string RequestUri,
        string? AuthorizationHeader,
        string? Content);
}
