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
        var endpoints = apiProject.Resource.Annotations
            .OfType<EndpointAnnotation>()
            .ToDictionary(annotation => annotation.Name, StringComparer.Ordinal);

        Assert.Equal("api", apiProject.Resource.Name);
        Assert.Contains(waitAnnotations, annotation => annotation.Resource.Name == "platformdb");
        Assert.Contains(waitAnnotations, annotation => annotation.Resource.Name == "mailpit");
        Assert.Contains(
            apiProject.Resource.Annotations,
            annotation => annotation is ResourceUrlsCallbackAnnotation);
        Assert.Contains("https", endpoints.Keys);
    }

    /// <summary>
    /// Trace: IG navigation migration Phase 1, bounded investigation gate.
    /// Verifies: the probe is exposed only as an explicitly invoked Aspire command when AppHost provides its ephemeral capability key.
    /// Expected: the command requires an approval reference, asks for confirmation, and does not become a highlighted routine action.
    /// Why: a local probe must not be callable by the scheduled collector or exposed as an unauthenticated API operation.
    /// </summary>
    [Fact]
    public void AddApiProject_ShouldRegisterConfirmedProbeCommand_WhenEphemeralCommandKeyIsProvided()
    {
        var builder = CreateBuilder();
        var infrastructure = AppHostInfrastructureRegistration.Create(builder);
        var apiProject = AppHostProjectRegistration.AddApiProject(builder, infrastructure, "ephemeral-test-key");

        var command = Assert.Single(
            apiProject.Resource.Annotations.OfType<ResourceCommandAnnotation>(),
            item => item.DisplayName == "Probe IG market navigation");

        Assert.False(command.IsHighlighted);
        Assert.Contains("up to 10", command.ConfirmationMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(command.Arguments, argument => argument.Name == "authorizationReference" && argument.Required);
        Assert.Contains(command.Arguments, argument => argument.Name == "nodeId" && !argument.Required);
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

        var webProject = AppHostProjectRegistration.AddWebProject(builder, apiProject, infrastructure);
        var waitAnnotations = webProject.Resource.Annotations.OfType<WaitAnnotation>().ToArray();
        var endpoints = webProject.Resource.Annotations
            .OfType<EndpointAnnotation>()
            .ToDictionary(annotation => annotation.Name, StringComparer.Ordinal);

        Assert.Equal("web", webProject.Resource.Name);
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
            AllowUnsecuredTransport = true
        });
    }
}
