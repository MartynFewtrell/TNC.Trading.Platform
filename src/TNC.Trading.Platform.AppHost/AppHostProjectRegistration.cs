using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Dotnet;

namespace TNC.Trading.Platform.AppHost;

internal static class AppHostProjectRegistration
{
    internal const string ApiProjectPath = @"..\TNC.Trading.Platform.Api\TNC.Trading.Platform.Api.csproj";
    internal const string WebProjectPath = @"..\TNC.Trading.Platform.Web\TNC.Trading.Platform.Web.csproj";

    internal static IResourceBuilder<DotnetProjectResource> AddApiProject(
        IDistributedApplicationBuilder builder,
        AppHostInfrastructure infrastructure)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var configuredReadinessPath = builder.Configuration["Health:Path:Readiness"];
        var readinessPath = string.IsNullOrWhiteSpace(configuredReadinessPath)
            ? "/health/ready"
            : "/" + configuredReadinessPath.TrimStart('/');

#pragma warning disable ASPIREDOTNETPROJECT001
        var apiProject = builder.AddDotnetProject("api", ApiProjectPath)
            .WithEnvironment("Health__Path__Readiness", readinessPath)
            .WithHttpHealthCheck(readinessPath, endpointName: "http")
            .WithUrlForEndpoint("https", url =>
            {
                url.Url = "/scalar/v1";
                url.DisplayText = "Scalar UI";
            })
            .WithAnnotation(new ResourceUrlsCallbackAnnotation(context =>
                context.Urls.RemoveAll(url => url.Endpoint?.EndpointName == "http")));
#pragma warning restore ASPIREDOTNETPROJECT001

        apiProject = apiProject
            .WithReference(infrastructure.PlatformDatabase)
            .WaitFor(infrastructure.PlatformDatabase)
            .WaitFor(infrastructure.Mailpit);

        return apiProject;
    }

    internal static IResourceBuilder<DotnetProjectResource> AddWebProject(
        IDistributedApplicationBuilder builder,
        IResourceBuilder<DotnetProjectResource> apiProject,
        AppHostInfrastructure infrastructure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(apiProject);
        ArgumentNullException.ThrowIfNull(infrastructure);

#pragma warning disable ASPIREDOTNETPROJECT001
        var webProject = builder.AddDotnetProject("web", WebProjectPath)
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
#pragma warning restore ASPIREDOTNETPROJECT001

        return webProject;
    }
}
