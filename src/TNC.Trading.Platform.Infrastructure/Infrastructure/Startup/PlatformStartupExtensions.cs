using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace TNC.Trading.Platform.Infrastructure.Startup;

public static class PlatformStartupExtensions
{
    /// <summary>
    /// Applies the Infrastructure-owned schema, bootstrap configuration, and retention startup steps.
    /// </summary>
    /// <param name="app">The application whose registered Infrastructure services perform initialization.</param>
    /// <param name="cancellationToken">A token that cancels startup initialization.</param>
    /// <returns>A task that completes only after all required startup steps succeed.</returns>
    public static async Task InitializePlatformAsync(this WebApplication app, CancellationToken cancellationToken = default)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var initializer = scope.ServiceProvider.GetRequiredService<PlatformStartupInitializer>();
        await initializer.InitializeAsync(cancellationToken).ConfigureAwait(false);
    }
}