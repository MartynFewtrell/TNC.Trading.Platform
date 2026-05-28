using System.IO;
using System.Net.Sockets;

namespace TNC.Trading.Platform.Web.FunctionalTests.Authentication;

internal static class FunctionalHttpRequestRetry
{
    public static Task<HttpResponseMessage> GetAsync(HttpClient httpClient, string path)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        return SendAsync(() => httpClient.GetAsync(path));
    }

    public static Task<HttpResponseMessage> GetAsync(HttpClient httpClient, Uri requestUri)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(requestUri);

        return SendAsync(() => httpClient.GetAsync(requestUri));
    }

    public static Task<HttpResponseMessage> PostFormAsync(
        HttpClient httpClient,
        string path,
        IReadOnlyCollection<KeyValuePair<string, string>> formValues)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(formValues);

        return SendAsync(() => httpClient.PostAsync(path, CreateContent(formValues)));
    }

    public static Task<HttpResponseMessage> PostFormAsync(
        HttpClient httpClient,
        Uri requestUri,
        IReadOnlyCollection<KeyValuePair<string, string>> formValues)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(requestUri);
        ArgumentNullException.ThrowIfNull(formValues);

        return SendAsync(() => httpClient.PostAsync(requestUri, CreateContent(formValues)));
    }

    private static FormUrlEncodedContent CreateContent(IReadOnlyCollection<KeyValuePair<string, string>> formValues)
    {
        return new FormUrlEncodedContent(formValues);
    }

    private static async Task<HttpResponseMessage> SendAsync(Func<Task<HttpResponseMessage>> sendAsync)
    {
        ArgumentNullException.ThrowIfNull(sendAsync);

        try
        {
            return await sendAsync().ConfigureAwait(false);
        }
        catch (HttpRequestException exception) when (IsTransientTlsDisconnect(exception))
        {
            return await sendAsync().ConfigureAwait(false);
        }
    }

    private static bool IsTransientTlsDisconnect(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is SocketException socketException
                && socketException.SocketErrorCode == SocketError.ConnectionReset)
            {
                return true;
            }

            if (current is IOException ioException
                && ioException.Message.Contains("forcibly closed by the remote host", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
