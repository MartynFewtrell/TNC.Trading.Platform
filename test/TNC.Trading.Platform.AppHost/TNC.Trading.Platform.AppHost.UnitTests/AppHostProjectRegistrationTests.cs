using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace TNC.Trading.Platform.AppHost.UnitTests;

public sealed class AppHostProjectRegistrationTests
{
    /// <summary>
    /// Trace: FR1, FR2, FR4, TR1.
    /// Verifies: API project registration preserves the expected API resource name, wait dependencies, and Scalar link in the application model.
    /// Expected: the API resource is named `api`, waits for the platform database and Mailpit, and exposes the `Scalar UI` link.
    /// Why: focused project-registration tests should catch resource-model drift before a broader AppHost composition run is needed.
    /// </summary>
    [Fact]
    public void AddApiProject_ShouldRegisterExpectedApiResource_WhenInfrastructureIsConfigured()
    {
        var builder = CreateBuilder();
        var infrastructure = AppHostInfrastructureRegistration.Create(builder);

        var apiProject = AppHostProjectRegistration.AddApiProject(builder, infrastructure);
        var waitAnnotations = apiProject.Resource.Annotations.OfType<WaitAnnotation>().ToArray();
        var endpointNames = apiProject.Resource.Annotations
            .OfType<EndpointAnnotation>()
            .Select(annotation => annotation.Name)
            .ToArray();

        Assert.Equal("api", apiProject.Resource.Name);
        Assert.Contains(waitAnnotations, annotation => annotation.Resource.Name == "platformdb");
        Assert.Contains(waitAnnotations, annotation => annotation.Resource.Name == "mailpit");
        Assert.Contains(
            apiProject.Resource.Annotations,
            annotation => annotation is ResourceUrlsCallbackAnnotation);
        Assert.Contains("https", endpointNames);
    }

    /// <summary>
    /// Trace: FR1, FR2, FR4, TR1.
    /// Verifies: Web project registration preserves the expected Web resource name, API wait dependency, and operator link in the application model.
    /// Expected: the Web resource is named `web`, waits for the API project, and exposes the `Operator UI` link.
    /// Why: the AppHost composition should keep the public operator surface wired to the protected API project after refactoring.
    /// </summary>
    [Fact]
    public void AddWebProject_ShouldRegisterExpectedWebResource_WhenApiProjectIsConfigured()
    {
        var builder = CreateBuilder();
        var infrastructure = AppHostInfrastructureRegistration.Create(builder);
        var apiProject = AppHostProjectRegistration.AddApiProject(builder, infrastructure);

        var webProject = AppHostProjectRegistration.AddWebProject(builder, apiProject);
        var waitAnnotations = webProject.Resource.Annotations.OfType<WaitAnnotation>().ToArray();
        var endpointNames = webProject.Resource.Annotations
            .OfType<EndpointAnnotation>()
            .Select(annotation => annotation.Name)
            .ToArray();

        Assert.Equal("web", webProject.Resource.Name);
        Assert.Contains(waitAnnotations, annotation => annotation.Resource.Name == "api");
        Assert.Contains(
            webProject.Resource.Annotations,
            annotation => annotation is ResourceUrlsCallbackAnnotation);
        Assert.Contains("https", endpointNames);
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
