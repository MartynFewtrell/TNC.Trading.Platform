using System.Diagnostics;
using Microsoft.AspNetCore.OpenApi;
using Scalar.AspNetCore;
using TNC.Trading.Platform.Api.Authentication;
using TNC.Trading.Platform.Api.Features.Platform;
using TNC.Trading.Platform.Api.Features.UpdatePlatformConfiguration;
using TNC.Trading.Platform.Application.Services;
using TNC.Trading.Platform.Infrastructure.Persistence;
using TNC.Trading.Platform.Infrastructure.Platform;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.AddServiceDefaults();
builder.AddPlatformApiAuthentication();
builder.Services.AddDataProtection();
builder.Services.AddSingleton<TimeProvider>(_ => PlatformTimeProviderFactory.Create(builder.Configuration));
builder.Services.AddPlatformApplication();
builder.Services.AddPlatformInfrastructure(builder.Configuration, builder.Environment);
builder.Services.AddScoped<UpdatePlatformConfigurationValidator>();

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
    await dbContext.Database.EnsureCreatedAsync();

    var configurationService = scope.ServiceProvider.GetRequiredService<PlatformConfigurationService>();
    await configurationService.ApplyStartupConfigurationAsync(CancellationToken.None);

    var retentionProcessor = scope.ServiceProvider.GetRequiredService<OperationalRecordRetentionProcessor>();
    await retentionProcessor.ApplyAsync(CancellationToken.None);

    var coordinator = scope.ServiceProvider.GetRequiredService<PlatformStateCoordinator>();
    await coordinator.TickAsync(CancellationToken.None);
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
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapDefaultEndpoints();

await app.RunAsync();
