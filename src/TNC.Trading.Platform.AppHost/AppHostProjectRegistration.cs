using Aspire.Hosting.ApplicationModel;

namespace TNC.Trading.Platform.AppHost;

internal static class AppHostProjectRegistration
{
    internal static IResourceBuilder<ProjectResource> AddApiProject(
        IDistributedApplicationBuilder builder,
        AppHostInfrastructure infrastructure)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var apiProject = builder.AddProject<Projects.TNC_Trading_Platform_Api>("api")
            .WithExternalHttpEndpoints()
            .WithUrlForEndpoint("https", _ => new()
            {
                Url = "/scalar/v1",
                DisplayText = "Scalar UI"
            });

        apiProject = apiProject
            .WithReference(infrastructure.PlatformDatabase)
            .WaitFor(infrastructure.PlatformDatabase)
            .WaitFor(infrastructure.Mailpit);

        return apiProject;
    }

    internal static IResourceBuilder<ProjectResource> AddWebProject(
        IDistributedApplicationBuilder builder,
        IResourceBuilder<ProjectResource> apiProject,
        AppHostInfrastructure infrastructure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(apiProject);
        ArgumentNullException.ThrowIfNull(infrastructure);

        return builder.AddProject<Projects.TNC_Trading_Platform_Web>("web")
            .WithReference(apiProject)
            .WithReference(infrastructure.PlatformDatabase)
            .WaitFor(apiProject)
            .WithExternalHttpEndpoints()
            .WithUrlForEndpoint("https", _ => new()
            {
                Url = "/",
                DisplayText = "Operator UI"
            });
    }
}
