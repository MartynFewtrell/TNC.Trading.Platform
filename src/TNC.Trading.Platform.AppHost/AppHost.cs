using System.Security.Cryptography;
using TNC.Trading.Platform.AppHost;

var builder = DistributedApplication.CreateBuilder(args);

var settings = AppHostSettings.FromConfiguration(builder.Configuration);
var infrastructure = AppHostInfrastructureRegistration.Create(builder);
var navigationProbeCommandKey = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
var apiProject = AppHostProjectRegistration.AddApiProject(
    builder,
    infrastructure,
    navigationProbeCommandKey);
var webProject = AppHostProjectRegistration.AddWebProject(builder, apiProject, infrastructure);

apiProject = AppHostEnvironmentWiring.ConfigureApiProject(apiProject, infrastructure, settings);
webProject = AppHostEnvironmentWiring.ConfigureWebProject(webProject);
(apiProject, webProject) = AppHostEnvironmentWiring.ConfigureAuthenticationProvider(apiProject, webProject, infrastructure.Keycloak, settings);

builder.Build().Run();
