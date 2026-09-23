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
            .WithUrlForEndpoint("https", url =>
            {
                url.Url = "/scalar/v1";
                url.DisplayText = "Scalar UI";
            })
            .WithAnnotation(new ResourceUrlsCallbackAnnotation(context =>
                context.Urls.RemoveAll(url => url.Endpoint?.EndpointName == "http")));

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

        var webProject = builder.AddProject<Projects.TNC_Trading_Platform_Web>("web")
            .WithReference(apiProject)
            .WithReference(infrastructure.PlatformDatabase)
            .WaitFor(apiProject)
            .WithUrlForEndpoint("https", url =>
            {
                url.Url = "/";
                url.DisplayText = "Operator UI";
            })
            .WithAnnotation(new ResourceUrlsCallbackAnnotation(context =>
                context.Urls.RemoveAll(url => url.Endpoint?.EndpointName == "http")));

        return webProject;
    }
}
