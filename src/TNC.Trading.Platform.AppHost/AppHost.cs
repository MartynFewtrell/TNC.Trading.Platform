var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.TNC_Trading_Platform_Api>("api")
    .WithExternalHttpEndpoints()
    .WithUrlForEndpoint("https", _ => new()
    {
        Url = "/scalar/v1",
        DisplayText = "Scalar UI"
    });

builder.Build().Run();
