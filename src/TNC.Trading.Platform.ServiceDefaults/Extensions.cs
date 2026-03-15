using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ServiceDiscovery;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Microsoft.Extensions.Hosting;

// Adds common Aspire services: service discovery, resilience, health checks, and OpenTelemetry.
// This project should be referenced by each service project in your solution.
// To learn more about using this project, see https://aka.ms/dotnet/aspire/service-defaults
public static class Extensions
{
    private const string DefaultReadinessEndpointPath = "/health/ready";
    private const string DefaultLivenessEndpointPath = "/health/live";

    /// <summary>
    /// Adds the shared cross-cutting defaults used by platform services.
    /// </summary>
    /// <typeparam name="TBuilder">The host builder type.</typeparam>
    /// <param name="builder">The host builder instance.</param>
    /// <returns>The original builder for fluent chaining.</returns>
    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.ConfigureOpenTelemetry();

        builder.AddDefaultHealthChecks();

        builder.Services.AddServiceDiscovery();

        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            // Turn on resilience by default
            http.AddStandardResilienceHandler();

            // Turn on service discovery by default
            http.AddServiceDiscovery();
        });

        // Uncomment the following to restrict the allowed schemes for service discovery.
        // builder.Services.Configure<ServiceDiscoveryOptions>(options =>
        // {
        //     options.AllowedSchemes = ["https"];
        // });

        return builder;
    }

    /// <summary>
    /// Configures OpenTelemetry logging, metrics, and tracing defaults.
    /// </summary>
    /// <typeparam name="TBuilder">The host builder type.</typeparam>
    /// <param name="builder">The host builder instance.</param>
    /// <returns>The original builder for fluent chaining.</returns>
    public static TBuilder ConfigureOpenTelemetry<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        var readinessEndpointPath = GetReadinessEndpointPath(builder.Configuration);
        var livenessEndpointPath = GetLivenessEndpointPath(builder.Configuration);

        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation();
            })
            .WithTracing(tracing =>
            {
                tracing.AddSource(builder.Environment.ApplicationName)
                    .AddAspNetCoreInstrumentation(tracing =>
                        // Exclude health check requests from tracing
                        tracing.Filter = context =>
                            !context.Request.Path.StartsWithSegments(readinessEndpointPath)
                            && !context.Request.Path.StartsWithSegments(livenessEndpointPath)
                    )
                    // Uncomment the following line to enable gRPC instrumentation (requires the OpenTelemetry.Instrumentation.GrpcNetClient package)
                    //.AddGrpcClientInstrumentation()
                    .AddHttpClientInstrumentation();
            });

        builder.AddOpenTelemetryExporters();

        return builder;
    }

    private static TBuilder AddOpenTelemetryExporters<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        var useOtlpExporter = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

        if (useOtlpExporter)
        {
            builder.Services.AddOpenTelemetry().UseOtlpExporter();
        }

        // Uncomment the following lines to enable the Azure Monitor exporter (requires the Azure.Monitor.OpenTelemetry.AspNetCore package)
        //if (!string.IsNullOrEmpty(builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
        //{
        //    builder.Services.AddOpenTelemetry()
        //       .UseAzureMonitor();
        //}

        return builder;
    }

    /// <summary>
    /// Adds baseline health checks including a liveness self-check.
    /// </summary>
    /// <typeparam name="TBuilder">The host builder type.</typeparam>
    /// <param name="builder">The host builder instance.</param>
    /// <returns>The original builder for fluent chaining.</returns>
    public static TBuilder AddDefaultHealthChecks<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddHealthChecks()
            // Add a default liveness check to ensure app is responsive
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);

        return builder;
    }

    /// <summary>
    /// Maps baseline readiness and liveness endpoints for hosted services.
    /// </summary>
    /// <param name="app">The web application instance.</param>
    /// <returns>The original web application for fluent chaining.</returns>
    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        var readinessEndpointPath = GetReadinessEndpointPath(app.Configuration);
        var livenessEndpointPath = GetLivenessEndpointPath(app.Configuration);

        app.MapGet(readinessEndpointPath, async (HealthCheckService healthCheckService, CancellationToken cancellationToken) =>
            {
                var report = await healthCheckService.CheckHealthAsync(_ => true, cancellationToken);

                return report.Status == HealthStatus.Healthy
                    ? Results.Ok(new { status = "Healthy" })
                    : Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
            })
            .WithName("HealthReadiness")
            .WithOpenApi(operation =>
            {
                operation.Summary = "Readiness health check";
                operation.Description = "Returns 200 when the service is ready to serve traffic, otherwise 503.";
                return operation;
            });

        app.MapGet(livenessEndpointPath, async (HealthCheckService healthCheckService, CancellationToken cancellationToken) =>
            {
                var report = await healthCheckService.CheckHealthAsync(check => check.Tags.Contains("live"), cancellationToken);

                return report.Status == HealthStatus.Healthy
                    ? Results.Ok(new { status = "Healthy" })
                    : Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
            })
            .WithName("HealthLiveness")
            .WithOpenApi(operation =>
            {
                operation.Summary = "Liveness health check";
                operation.Description = "Returns 200 when the service process is alive, otherwise 503.";
                return operation;
            });

        return app;
    }

    private static string GetReadinessEndpointPath(IConfiguration configuration) =>
        configuration["Health:Path:Readiness"] ?? DefaultReadinessEndpointPath;

    private static string GetLivenessEndpointPath(IConfiguration configuration) =>
        configuration["Health:Path:Liveness"] ?? DefaultLivenessEndpointPath;
}
