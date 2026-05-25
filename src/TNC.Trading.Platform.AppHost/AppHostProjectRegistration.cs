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

        if (infrastructure.PlatformDatabase is not null)
        {
            apiProject = apiProject
                .WithReference(infrastructure.PlatformDatabase)
                .WaitFor(infrastructure.PlatformDatabase);
        }

        if (infrastructure.Mailpit is not null)
        {
            apiProject = apiProject.WaitFor(infrastructure.Mailpit);
        }

        return apiProject;
    }

    internal static IResourceBuilder<ProjectResource> AddWebProject(
        IDistributedApplicationBuilder builder,
        IResourceBuilder<ProjectResource> apiProject)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(apiProject);

        return builder.AddProject<Projects.TNC_Trading_Platform_Web>("web")
            .WithReference(apiProject)
            .WaitFor(apiProject)
            .WithExternalHttpEndpoints()
            .WithUrlForEndpoint("https", _ => new()
            {
                Url = "/",
                DisplayText = "Operator UI"
            });
    }
}