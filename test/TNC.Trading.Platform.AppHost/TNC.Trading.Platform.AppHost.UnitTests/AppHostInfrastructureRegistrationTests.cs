using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using System.Reflection;

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
        Assert.Equal(ContainerLifetime.Persistent, GetContainerLifetime(resources["sql"]));

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

    /// <summary>
    /// Trace: FR1, FR2, NF2, TR1.
    /// Verifies: the AppHost can disable persistent Keycloak state for repeatable authentication test runs.
    /// Expected: when the AppHost configuration opts out of persistent Keycloak state, the Keycloak resource uses a session lifetime.
    /// Why: AppHost-backed authentication tests need fresh realm imports so runtime listener callback changes are applied deterministically.
    /// </summary>
    [Fact]
    public void Create_ShouldUseSessionLifetimeForKeycloak_WhenPersistentKeycloakStateIsDisabled()
    {
        var builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions
        {
            DisableDashboard = true,
            AllowUnsecuredTransport = true
        });
        builder.Configuration["AppHost:UsePersistentKeycloakState"] = bool.FalseString;

        var infrastructure = AppHostInfrastructureRegistration.Create(builder);

        Assert.Equal(ContainerLifetime.Session, GetContainerLifetime(infrastructure.Keycloak.Resource));
    }

    /// <summary>
    /// Trace: NF2, TR1.
    /// Verifies: the local AppHost default retains durable SQL state.
    /// Expected: SQL uses a persistent lifetime and includes a data-volume annotation.
    /// Why: local development must preserve operator configuration between AppHost runs.
    /// </summary>
    [Fact]
    public void Create_ShouldPersistSqlState_WhenPersistenceIsNotConfigured()
    {
        var builder = CreateBuilder();
        var infrastructure = AppHostInfrastructureRegistration.Create(builder);
        var sqlResource = builder.Resources.Single(resource => resource.Name == "sql");

        Assert.Equal(ContainerLifetime.Persistent, GetContainerLifetime(sqlResource));
        Assert.NotEmpty(sqlResource.Annotations);
    }

    /// <summary>
    /// Trace: NF2, TR1.
    /// Verifies: test AppHost composition can use ephemeral SQL state.
    /// Expected: SQL uses a session lifetime and has no data-volume annotation.
    /// Why: closed-box fresh-state tests must not inherit rows from retained SQL volumes.
    /// </summary>
    [Fact]
    public void Create_ShouldUseEphemeralSqlState_WhenPersistenceIsDisabled()
    {
        var builder = CreateBuilder();
        builder.Configuration["AppHost:UsePersistentSqlState"] = bool.FalseString;
        var infrastructure = AppHostInfrastructureRegistration.Create(builder);
        var sqlResource = builder.Resources.Single(resource => resource.Name == "sql");
        var persistentBuilder = CreateBuilder();
        AppHostInfrastructureRegistration.Create(persistentBuilder);
        var persistentSqlResource = persistentBuilder.Resources.Single(resource => resource.Name == "sql");

        Assert.Equal(ContainerLifetime.Session, GetContainerLifetime(sqlResource));
        Assert.True(persistentSqlResource.Annotations.Count > sqlResource.Annotations.Count);
    }

    private static IDistributedApplicationBuilder CreateBuilder()
    {
        return DistributedApplication.CreateBuilder(new DistributedApplicationOptions
        {
            DisableDashboard = true,
            AllowUnsecuredTransport = true
        });
    }

    private static ContainerLifetime GetContainerLifetime(IResource resource)
    {
        ArgumentNullException.ThrowIfNull(resource);

        var directLifetime = resource.GetType()
            .GetProperty("Lifetime", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?
            .GetValue(resource);
        if (directLifetime is ContainerLifetime containerLifetime)
        {
            return containerLifetime;
        }

        foreach (var annotation in resource.Annotations)
        {
            var lifetime = annotation.GetType()
                .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(property => property.PropertyType == typeof(ContainerLifetime))?
                .GetValue(annotation);

            if (lifetime is ContainerLifetime annotationLifetime)
            {
                return annotationLifetime;
            }
        }

        throw new InvalidOperationException($"No container lifetime metadata was found for resource '{resource.Name}'.");
    }

}
