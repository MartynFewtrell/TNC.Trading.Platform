using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace TNC.Trading.Platform.AppHost.UnitTests;

public sealed class AppHostInfrastructureRegistrationTests
{
    /// <summary>
    /// Trace: FR1, FR2, NF2, TR1.
    /// Verifies: AppHost infrastructure registration keeps the expected SQL, database, Mailpit, and Keycloak resources in the distributed application model.
    /// Expected: the infrastructure resources are discoverable by name and the operator-facing Mailpit and Keycloak dashboard links remain present.
    /// Why: focused resource-model coverage should catch infrastructure registration drift before a full AppHost runtime start is required.
    /// </summary>
    [Fact]
    public void Create_ShouldRegisterExpectedInfrastructureResources_WhenBuilderIsConfigured()
    {
        var builder = CreateBuilder();

        var infrastructure = AppHostInfrastructureRegistration.Create(builder);
        var resources = builder.Resources.ToDictionary(resource => resource.Name, StringComparer.Ordinal);

        Assert.Equal("platformdb", infrastructure.PlatformDatabase.Resource.Name);
        Assert.Equal("mailpit", infrastructure.Mailpit.Resource.Name);
        Assert.Equal("keycloak", infrastructure.Keycloak.Resource.Name);
        Assert.Contains("sql", resources.Keys);
        Assert.Contains("platformdb", resources.Keys);
        Assert.Contains("mailpit", resources.Keys);
        Assert.Contains("keycloak", resources.Keys);

        Assert.Contains(
            infrastructure.Mailpit.Resource.Annotations,
            annotation => annotation is ResourceUrlsCallbackAnnotation);
        Assert.Contains(
            infrastructure.Keycloak.Resource.Annotations,
            annotation => annotation is ResourceUrlsCallbackAnnotation);

        var mailpitEndpointNames = infrastructure.Mailpit.Resource.Annotations
            .OfType<EndpointAnnotation>()
            .Select(annotation => annotation.Name)
            .ToArray();
        var keycloakEndpointNames = infrastructure.Keycloak.Resource.Annotations
            .OfType<EndpointAnnotation>()
            .Select(annotation => annotation.Name)
            .ToArray();

        Assert.Contains("http", mailpitEndpointNames);
        Assert.Contains("smtp", mailpitEndpointNames);
        Assert.Contains("http", keycloakEndpointNames);
    }

    private static IDistributedApplicationBuilder CreateBuilder()
    {
        return DistributedApplication.CreateBuilder(new DistributedApplicationOptions
        {
            DisableDashboard = true,
            AllowUnsecuredTransport = true
        });
    }
}
