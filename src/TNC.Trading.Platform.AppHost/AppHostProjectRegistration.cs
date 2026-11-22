using Aspire.Hosting.ApplicationModel;
using System.Net.Http.Json;

namespace TNC.Trading.Platform.AppHost;

internal static class AppHostProjectRegistration
{
    internal static IResourceBuilder<ProjectResource> AddApiProject(
        IDistributedApplicationBuilder builder,
        AppHostInfrastructure infrastructure,
        string? navigationProbeCommandKey = null)
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

        if (!string.IsNullOrWhiteSpace(navigationProbeCommandKey))
        {
            apiProject = apiProject
                .WithEnvironment("MarketNavigationProbe__CommandKey", navigationProbeCommandKey)
                .WithHttpCommand(
                    path: "/_internal/market-navigation/probe",
                    displayName: "Probe IG market navigation",
                    commandOptions: new HttpCommandOptions
                    {
                        EndpointSelector = () => apiProject.GetEndpoint("https"),
                        Description = "Run a nonpublishing Demo root/child probe. Each session, retry, and navigation send shares the 10-request ceiling and approved allowance.",
                        ConfirmationMessage = "Send up to 10 bounded HTTP requests to the configured IG Demo account? No listing will be published.",
                        Arguments =
                        [
                            new InteractionInput
                            {
                                Name = "authorizationReference",
                                Label = "Approval reference",
                                InputType = InputType.Text,
                                Required = true,
                                MaxLength = 128
                            },
                            new InteractionInput
                            {
                                Name = "nodeId",
                                Label = "Optional root node ID",
                                InputType = InputType.Text,
                                MaxLength = 128
                            }
                        ],
                        PrepareRequest = context =>
                        {
                            context.Request.Headers.Add(
                                "X-Market-Navigation-Probe-Key",
                                navigationProbeCommandKey);
                            context.Request.Content = JsonContent.Create(new
                            {
                                AuthorizationReference = context.Arguments.GetString("authorizationReference"),
                                NodeId = context.Arguments.GetString("nodeId")
                            });
                            return Task.CompletedTask;
                        },
                        ResultMode = HttpCommandResultMode.Json,
                        IsHighlighted = false
                    });
        }

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
