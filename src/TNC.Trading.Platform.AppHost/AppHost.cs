using Aspire.Hosting.ApplicationModel;

var builder = DistributedApplication.CreateBuilder(args);

var enableInfrastructureContainers = string.Equals(
    builder.Configuration["AppHost:EnableInfrastructureContainers"],
    bool.TrueString,
    StringComparison.OrdinalIgnoreCase);
var acsEndpoint = builder.Configuration["NotificationTransports:AzureCommunicationServices:Endpoint"];
var acsSenderAddress = builder.Configuration["NotificationTransports:AzureCommunicationServices:SenderAddress"];
var acsConnectionString = builder.Configuration["NotificationTransports:AzureCommunicationServices:ConnectionString"];

IResourceBuilder<IResourceWithConnectionString>? platformDatabase = null;
IResourceBuilder<IResourceWithEndpoints>? mailpit = null;

if (enableInfrastructureContainers)
{
    var sqlPassword = builder.AddParameter("sql-password", secret: true);
    var sql = builder.AddSqlServer("sql", sqlPassword)
        .WithDataVolume();

    platformDatabase = sql.AddDatabase("platformdb");

    mailpit = builder.AddContainer("mailpit", "axllent/mailpit", "v1.27")
        .WithHttpEndpoint(targetPort: 8025, name: "http")
        .WithEndpoint(targetPort: 1025, name: "smtp")
        .WithUrlForEndpoint("http", _ => new()
        {
            Url = "/",
            DisplayText = "Mailpit UI"
        });
}

var api = builder.AddProject<Projects.TNC_Trading_Platform_Api>("api")
    .WithExternalHttpEndpoints()
    .WithUrlForEndpoint("https", _ => new()
    {
        Url = "/scalar/v1",
        DisplayText = "Scalar UI"
    });

if (platformDatabase is not null)
{
    api.WithReference(platformDatabase)
        .WaitFor(platformDatabase);
}

if (mailpit is not null)
{
    var mailpitSmtpEndpoint = mailpit.Resource.GetEndpoint("smtp");

    api.WaitFor(mailpit)
        .WithEnvironment("NotificationTransports__Smtp__Host", mailpitSmtpEndpoint.Property(EndpointProperty.IPV4Host))
        .WithEnvironment("NotificationTransports__Smtp__Port", mailpitSmtpEndpoint.Property(EndpointProperty.Port))
        .WithEnvironment("NotificationTransports__Smtp__SenderAddress", "platform@local.test")
        .WithEnvironment("NotificationTransports__Smtp__EnableSsl", bool.FalseString);
}

if (!string.IsNullOrWhiteSpace(acsEndpoint))
{
    api.WithEnvironment("NotificationTransports__AzureCommunicationServices__Endpoint", acsEndpoint);
}

if (!string.IsNullOrWhiteSpace(acsSenderAddress))
{
    api.WithEnvironment("NotificationTransports__AzureCommunicationServices__SenderAddress", acsSenderAddress);
}

if (!string.IsNullOrWhiteSpace(acsConnectionString))
{
    api.WithEnvironment("NotificationTransports__AzureCommunicationServices__ConnectionString", acsConnectionString);
}

builder.AddProject<Projects.TNC_Trading_Platform_Web>("web")
    .WithReference(api)
    .WaitFor(api)
    .WithExternalHttpEndpoints()
    .WithUrlForEndpoint("https", _ => new()
    {
        Url = "/status",
        DisplayText = "Operator UI"
    });

builder.Build().Run();
