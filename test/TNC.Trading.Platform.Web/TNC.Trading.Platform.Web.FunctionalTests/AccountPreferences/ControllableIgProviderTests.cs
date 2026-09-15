using System.Net;
using System.Net.Sockets;
using TNC.Trading.Platform.TestShared.AccountPreferences;

namespace TNC.Trading.Platform.Web.FunctionalTests.AccountPreferences;

public sealed class ControllableIgProviderTests
{
    /// <summary>
    /// Verifies the provider creates a replacement listener after an occupied candidate port rejects the first bind.
    /// Expected: the next candidate starts successfully and serves the provider's existing preferences route.
    /// Why: failed HttpListener instances are disposed by the platform and cannot be reused for deterministic retries.
    /// </summary>
    [Fact]
    public async Task Start_ShouldRetryWithFreshListener_WhenFirstCandidatePortIsOccupied()
    {
        var firstCandidatePort = GetFreePort();
        var secondCandidatePort = GetFreePort();
        using var occupyingListener = new HttpListener();
        occupyingListener.Prefixes.Add($"http://127.0.0.1:{firstCandidatePort}/");
        occupyingListener.Start();

        var candidatePorts = new[] { firstCandidatePort, secondCandidatePort };
        var candidateIndex = 0;
        await using var provider = ControllableIgProvider.Start(() => candidatePorts[candidateIndex++]);

        using var httpClient = new HttpClient();
        var response = await httpClient.GetAsync(new Uri(provider.BaseUri, "gateway/deal/preferences"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("{\"enabled\":false}", await response.Content.ReadAsStringAsync());
    }

    private static int GetFreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }
}