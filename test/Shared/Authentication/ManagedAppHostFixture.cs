using Aspire.Hosting;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.Configuration;

namespace TNC.Trading.Platform.TestShared.Authentication;

/// <summary>
/// Owns one randomized, closed-box AppHost instance for a test collection.
/// </summary>
public sealed class ManagedAppHostFixture : IAsyncLifetime
{
    private static readonly TimeSpan InitializationTimeout = TimeSpan.FromSeconds(90);
    private readonly IReadOnlyDictionary<string, string?> configuration;
    private IDistributedApplicationTestingBuilder? builder;
    private DistributedApplication? application;

    public ManagedAppHostFixture(IReadOnlyDictionary<string, string?>? configuration = null)
    {
        this.configuration = configuration ?? new Dictionary<string, string?>();
    }

    public Uri WebEndpointUri { get; private set; } = null!;

    public Uri ApiEndpointUri { get; private set; } = null!;

    public Uri KeycloakEndpointUri { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        using var timeout = new CancellationTokenSource(InitializationTimeout);
        var token = timeout.Token;

        try
        {
            builder = await DistributedApplicationTestingBuilder
                .CreateAsync<Projects.TNC_Trading_Platform_AppHost>(
                    configuration
                        .Where(setting => setting.Value is not null)
                        .Select(setting => $"--{setting.Key}={setting.Value}")
                        .ToArray(),
                    cancellationToken: token)
                .ConfigureAwait(false);
            application = await builder.BuildAsync(token).ConfigureAwait(false);
            await application.StartAsync(token).ConfigureAwait(false);

            await WaitForResourceHealthyAsync("keycloak", token).ConfigureAwait(false);
            await WaitForResourceHealthyAsync("api", token).ConfigureAwait(false);
            await WaitForResourceHealthyAsync("web", token).ConfigureAwait(false);
            WebEndpointUri = application.GetEndpoint("web", "https");
            ApiEndpointUri = application.GetEndpoint("api", "https");
            KeycloakEndpointUri = application.GetEndpoint("keycloak", "http");
        }
        catch
        {
            await DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    public HttpClient CreateApiClient() => CreateHttpClient("api", "https");

    public HttpClient CreateWebClient() => CreateHttpClient("web", "https");

    public Task WaitForResourceHealthyAsync(string resourceName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceName);
        return application?.ResourceNotifications.WaitForResourceHealthyAsync(resourceName, cancellationToken)
            ?? throw new InvalidOperationException("The managed AppHost fixture has not been initialized.");
    }

    public async Task DisposeAsync()
    {
        var currentApplication = application;
        var currentBuilder = builder;

        if (currentApplication is null && currentBuilder is null)
        {
            return;
        }

        await AppHostCleanup.DisposeAsync(
            async () =>
            {
                if (currentApplication is not null)
                {
                    await currentApplication.StopAsync().ConfigureAwait(false);
                }
            },
            async () =>
            {
                try
                {
                    if (currentApplication is not null)
                    {
                        await currentApplication.DisposeAsync().ConfigureAwait(false);
                    }
                }
                finally
                {
                    application = null;
                }
            },
            async () =>
            {
                try
                {
                    if (currentBuilder is not null)
                    {
                        await currentBuilder.DisposeAsync().ConfigureAwait(false);
                    }
                }
                finally
                {
                    builder = null;
                }
            }).ConfigureAwait(false);
    }

    private HttpClient CreateHttpClient(string resourceName, string? endpointName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceName);
        if (application is null)
        {
            throw new InvalidOperationException("The managed AppHost fixture has not been initialized.");
        }

        return endpointName is null
            ? application.CreateHttpClient(resourceName)
            : application.CreateHttpClient(resourceName, endpointName);
    }
}