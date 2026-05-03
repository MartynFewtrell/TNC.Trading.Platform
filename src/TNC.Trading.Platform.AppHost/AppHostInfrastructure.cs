using Aspire.Hosting.ApplicationModel;

namespace TNC.Trading.Platform.AppHost;

internal sealed record AppHostInfrastructure(
    IResourceBuilder<IResourceWithConnectionString>? PlatformDatabase,
    IResourceBuilder<IResourceWithEndpoints>? Mailpit,
    IResourceBuilder<IResourceWithEndpoints>? Keycloak);
