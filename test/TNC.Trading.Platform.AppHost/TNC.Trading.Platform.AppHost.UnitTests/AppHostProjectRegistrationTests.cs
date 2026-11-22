using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Logging.Abstractions;

namespace TNC.Trading.Platform.AppHost.UnitTests;

public sealed class AppHostProjectRegistrationTests
{
    /// <summary>
    /// Trace: FR1, FR2, FR4, TR1.
    /// Verifies: API project registration preserves the expected API resource name, wait dependencies, and Scalar link in the application model.
    /// Expected: the API resource resolves to the API project, is named `api`, waits for the platform database and Mailpit, and exposes the `Scalar UI` link.
    /// Why: focused project-registration tests should catch resource-model drift before a broader AppHost composition run is needed.
    /// </summary>
    [Fact]
    public void AddApiProject_ShouldRegisterExpectedApiResource_WhenInfrastructureIsConfigured()
    {
        var builder = CreateBuilder();
        var infrastructure = AppHostInfrastructureRegistration.Create(builder);

        var apiProject = AppHostProjectRegistration.AddApiProject(builder, infrastructure);
        var waitAnnotations = apiProject.Resource.Annotations.OfType<WaitAnnotation>().ToArray();
        var endpoints = apiProject.Resource.Annotations
            .OfType<EndpointAnnotation>()
            .ToDictionary(annotation => annotation.Name, StringComparer.Ordinal);

        Assert.Equal("api", apiProject.Resource.Name);
        Assert.Equal(
            Path.GetFullPath(Path.Combine(Projects.TNC_Trading_Platform_AppHost.ProjectPath, "..", "TNC.Trading.Platform.Api", "TNC.Trading.Platform.Api.csproj")),
            Assert.Single(apiProject.Resource.Annotations.OfType<IProjectMetadata>()).ProjectPath);
        Assert.Contains(waitAnnotations, annotation => annotation.Resource.Name == "platformdb");
        Assert.Contains(waitAnnotations, annotation => annotation.Resource.Name == "mailpit");
        Assert.Contains(
            apiProject.Resource.Annotations,
            annotation => annotation is ResourceUrlsCallbackAnnotation);
        Assert.Contains("https", endpoints.Keys);
    }

    /// <summary>
    /// Trace: NF3, shared Data Protection key-ring startup.
    /// Verifies: the API readiness probe and its configured endpoint use the same path.
    /// Expected: default, empty, and customized paths produce a matching HTTP health check and API environment value.
    /// Why: Web must wait for API startup and database migrations before reading the shared key ring.
    /// </summary>
    [Theory]
    [InlineData(null, "/health/ready")]
    [InlineData("", "/health/ready")]
    [InlineData("custom/ready", "/custom/ready")]
    [InlineData("/custom/ready", "/custom/ready")]
    public async Task AddApiProject_ShouldConfigureMatchingReadinessProbe_WhenReadinessPathIsConfigured(
        string? configuredPath,
        string expectedPath)
    {
        var builder = CreateBuilder();
        builder.Configuration["Health:Path:Readiness"] = configuredPath;
        var infrastructure = AppHostInfrastructureRegistration.Create(builder);

        var apiProject = AppHostProjectRegistration.AddApiProject(builder, infrastructure);
        var executionConfiguration = await ExecutionConfigurationBuilder.Create(apiProject.Resource)
            .WithEnvironmentVariablesConfig()
            .BuildAsync(new DistributedApplicationExecutionContext(DistributedApplicationOperation.Publish), NullLogger.Instance);

        Assert.Contains(
            apiProject.Resource.Annotations.OfType<HealthCheckAnnotation>(),
            annotation => annotation.Key == $"api_http_{expectedPath}_200_check");
        Assert.Equal(
            expectedPath,
            Assert.Single(executionConfiguration.EnvironmentVariables, entry => entry.Key == "Health__Path__Readiness").Value);
    }

    /// <summary>
    /// Trace: FR1, FR2, FR4, TR1.
    /// Verifies: Web project registration preserves the expected Web resource name, API wait dependency, and operator link in the application model.
    /// Expected: the Web resource resolves to the Web project, is named `web`, waits for the API project, and exposes the `Operator UI` link.
    /// Why: the AppHost composition should keep the public operator surface wired to the protected API project after refactoring.
    /// </summary>
    [Fact]
    public void AddWebProject_ShouldRegisterExpectedWebResource_WhenApiProjectIsConfigured()
    {
        var builder = CreateBuilder();
        var infrastructure = AppHostInfrastructureRegistration.Create(builder);
        var apiProject = AppHostProjectRegistration.AddApiProject(builder, infrastructure);

        var webProject = AppHostProjectRegistration.AddWebProject(builder, apiProject, infrastructure);
        var waitAnnotations = webProject.Resource.Annotations.OfType<WaitAnnotation>().ToArray();
        var endpoints = webProject.Resource.Annotations
            .OfType<EndpointAnnotation>()
            .ToDictionary(annotation => annotation.Name, StringComparer.Ordinal);

        Assert.Equal("web", webProject.Resource.Name);
        Assert.Equal(
            Path.GetFullPath(Path.Combine(Projects.TNC_Trading_Platform_AppHost.ProjectPath, "..", "TNC.Trading.Platform.Web", "TNC.Trading.Platform.Web.csproj")),
            Assert.Single(webProject.Resource.Annotations.OfType<IProjectMetadata>()).ProjectPath);
        Assert.Contains(waitAnnotations, annotation => annotation.Resource.Name == "api");
        Assert.Contains(
            webProject.Resource.Annotations,
            annotation => annotation is ResourceUrlsCallbackAnnotation);
        Assert.Contains("https", endpoints.Keys);
    }

    private static IDistributedApplicationBuilder CreateBuilder()
    {
        return DistributedApplication.CreateBuilder(new DistributedApplicationOptions
        {
            DisableDashboard = true,
            AllowUnsecuredTransport = true,
            ProjectDirectory = Projects.TNC_Trading_Platform_AppHost.ProjectPath
        });
    }
}
