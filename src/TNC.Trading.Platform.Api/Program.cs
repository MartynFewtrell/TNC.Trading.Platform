using System.Diagnostics;
using Scalar.AspNetCore;
using Microsoft.AspNetCore.OpenApi;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.AddServiceDefaults();

var app = builder.Build();

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

app.MapGet("/", (IHostEnvironment environment) => Results.Ok(new
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

app.Run();
