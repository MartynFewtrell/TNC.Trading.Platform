using System.Diagnostics;
using Microsoft.AspNetCore.OpenApi;
using Scalar.AspNetCore;
using TNC.Trading.Platform.Api.Authentication;
using TNC.Trading.Platform.Api.Features.Platform;
using TNC.Trading.Platform.Api.Features.UpdatePlatformConfiguration;
using TNC.Trading.Platform.Api.Hosting;
using TNC.Trading.Platform.Application.Features.ReconcilePlatformAuthentication;
using TNC.Trading.Platform.Infrastructure.DependencyInjection;
using TNC.Trading.Platform.Infrastructure.Startup;
using TNC.Trading.Platform.Infrastructure.Time;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.AddServiceDefaults();
builder.AddPlatformDataProtection();
builder.AddPlatformApiAuthentication();
builder.Services.AddSingleton<TimeProvider>(_ => TNC.Trading.Platform.Infrastructure.Time.PlatformTimeProviderFactory.Create(builder.Configuration));
builder.Services.AddPlatformApplication();
builder.Services.AddPlatformInfrastructure(builder.Configuration, builder.Environment);
builder.Services.AddScoped<UpdatePlatformConfigurationValidator>();
builder.Services.AddSingleton<IPlatformAuthenticationSupervisorDelay, PlatformAuthenticationSupervisorDelay>();
builder.Services.AddHostedService<PlatformAuthenticationSupervisor>();
builder.Services.AddHostedService<AccountPreferencesReconciliationSupervisor>();

var app = builder.Build();

try
{
    await app.InitializePlatformAsync(CancellationToken.None);

    await using var scope = app.Services.CreateAsyncScope();
    var reconcileHandler = scope.ServiceProvider.GetRequiredService<ReconcilePlatformAuthenticationHandler>();
    await reconcileHandler.HandleAsync(new ReconcilePlatformAuthenticationRequest(), CancellationToken.None);
}
catch (Exception startupException)
{
    app.Logger.LogCritical(startupException, "Fatal error during startup scope; the application will not start.");
    throw;
}

app.Logger.LogInformation(
    "Starting {ServiceName} in {EnvironmentName}",
    app.Environment.ApplicationName,
    app.Environment.EnvironmentName);

app.Use(async (context, next) =>
{
    var traceId = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;

    using var _ = app.Logger.BeginScope(new Dictionary<string, object?>
    {
        ["service.name"] = app.Environment.ApplicationName,
        ["deployment.environment"] = app.Environment.EnvironmentName,
        ["trace.id"] = traceId
    });

    await next();

    if (context.Request.Path.StartsWithSegments("/api/platform", StringComparison.Ordinal)
        && (context.Response.StatusCode == StatusCodes.Status401Unauthorized
            || context.Response.StatusCode == StatusCodes.Status403Forbidden))
    {
        app.Logger.LogWarning(
            "Protected API request denied with status code {StatusCode} for path {Path}",
            context.Response.StatusCode,
            context.Request.Path.Value);
    }
});

app.UseAuthentication();
app.UseAuthorization();

app.MapPlatformEndpoints();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
    app.MapScalarApiReference().AllowAnonymous();
}

app.MapDefaultEndpoints();

await app.RunAsync();
