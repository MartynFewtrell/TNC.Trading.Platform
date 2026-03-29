using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.OpenApi;
using Scalar.AspNetCore;
using TNC.Trading.Platform.Api.Features.GetPlatformConfiguration;
using TNC.Trading.Platform.Api.Features.GetPlatformEvents;
using TNC.Trading.Platform.Api.Features.GetPlatformStatus;
using TNC.Trading.Platform.Api.Features.TriggerManualAuthRetry;
using TNC.Trading.Platform.Api.Features.UpdatePlatformConfiguration;
using TNC.Trading.Platform.Api.Infrastructure.Notifications;
using TNC.Trading.Platform.Api.Infrastructure.Persistence;
using TNC.Trading.Platform.Api.Infrastructure.Platform;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.AddServiceDefaults();
builder.Services.AddDataProtection();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddDbContext<PlatformDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("platformdb");
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        options.UseInMemoryDatabase("tnc-trading-platform");
        return;
    }

    options.UseSqlServer(connectionString);
});
builder.Services.AddScoped<ProtectedCredentialService>();
builder.Services.AddScoped<TradingScheduleGate>();
builder.Services.AddScoped<INotificationProvider, RecordedNotificationProvider>();
builder.Services.AddScoped<INotificationProvider, SmtpNotificationProvider>();
builder.Services.AddScoped<INotificationProvider, AzureCommunicationServicesEmailNotificationProvider>();
builder.Services.AddScoped<NotificationDispatcher>();
builder.Services.AddScoped<PlatformConfigurationService>();
builder.Services.AddScoped<PlatformStateCoordinator>();
builder.Services.AddScoped<OperationalRecordRetentionProcessor>();
builder.Services.AddScoped<GetPlatformStatusHandler>();
builder.Services.AddScoped<GetPlatformConfigurationHandler>();
builder.Services.AddScoped<UpdatePlatformConfigurationValidator>();
builder.Services.AddScoped<UpdatePlatformConfigurationHandler>();
builder.Services.AddScoped<TriggerManualAuthRetryHandler>();
builder.Services.AddScoped<GetPlatformEventsHandler>();
builder.Services.AddHostedService<PlatformAuthSupervisor>();
builder.Services.AddHostedService<OperationalRecordRetentionService>();

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
    await dbContext.Database.EnsureCreatedAsync();

    var configurationService = scope.ServiceProvider.GetRequiredService<PlatformConfigurationService>();
    await configurationService.GetCurrentAsync(CancellationToken.None);

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
});

app.MapGet("/", async (GetPlatformStatusHandler handler, CancellationToken cancellationToken) => Results.Ok(await handler.HandleAsync(new GetPlatformStatusRequest(), cancellationToken)))
.WithOpenApi();

app.MapGet("/api/platform/status", async (GetPlatformStatusHandler handler, CancellationToken cancellationToken) =>
        Results.Ok(await handler.HandleAsync(new GetPlatformStatusRequest(), cancellationToken)))
.WithOpenApi();

app.MapGet("/api/platform/configuration", async (GetPlatformConfigurationHandler handler, CancellationToken cancellationToken) =>
        Results.Ok(await handler.HandleAsync(new GetPlatformConfigurationRequest(), cancellationToken)))
.WithOpenApi();

app.MapPut("/api/platform/configuration", async (UpdatePlatformConfigurationRequest request, UpdatePlatformConfigurationHandler handler, CancellationToken cancellationToken) =>
    {
        try
        {
            var response = await handler.HandleAsync(request, cancellationToken);
            return Results.Ok(response);
        }
        catch (PlatformValidationException exception)
        {
            return Results.ValidationProblem(exception.Errors.ToDictionary(item => item.Key, item => item.Value));
        }
    })
.WithOpenApi();

app.MapPost("/api/platform/auth/manual-retry", async (TriggerManualAuthRetryHandler handler, CancellationToken cancellationToken) =>
    {
        try
        {
            var response = await handler.HandleAsync(new TriggerManualAuthRetryRequest(), cancellationToken);
            return Results.Accepted("/api/platform/status", response);
        }
        catch (InvalidOperationException exception)
        {
            return Results.Conflict(new { error = exception.Message });
        }
    })
.WithOpenApi();

app.MapGet("/api/platform/events", async (string? category, string? environment, GetPlatformEventsHandler handler, CancellationToken cancellationToken) =>
        Results.Ok(await handler.HandleAsync(new GetPlatformEventsRequest(category, environment), cancellationToken)))
.WithOpenApi();

app.MapGet("/metadata", (IHostEnvironment environment) => Results.Ok(new
{
    service = environment.ApplicationName,
    environment = environment.EnvironmentName
}))
.WithOpenApi();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapDefaultEndpoints();

await app.RunAsync();
