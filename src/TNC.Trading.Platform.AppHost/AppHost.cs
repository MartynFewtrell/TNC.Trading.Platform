var builder = DistributedApplication.CreateBuilder(args);

var enableInfrastructureContainers = string.Equals(
    builder.Configuration["AppHost:EnableInfrastructureContainers"],
    bool.TrueString,
    StringComparison.OrdinalIgnoreCase);
var sessionLifetimeSeconds = builder.Configuration["Bootstrap:AuthSimulation:SessionLifetimeSeconds"] ?? "900";
var acsEndpoint = builder.Configuration["NotificationTransports:AzureCommunicationServices:Endpoint"];
var acsSenderAddress = builder.Configuration["NotificationTransports:AzureCommunicationServices:SenderAddress"];
var acsConnectionString = builder.Configuration["NotificationTransports:AzureCommunicationServices:ConnectionString"];

IResourceBuilder<IResourceWithConnectionString>? platformDatabase = null;

if (enableInfrastructureContainers)
{
    var sqlPassword = builder.AddParameter("sql-password", secret: true);
    var sql = builder.AddSqlServer("sql", sqlPassword)
        .WithDataVolume();

    platformDatabase = sql.AddDatabase("platformdb");

    builder.AddContainer("mailpit", "axllent/mailpit", "v1.27")
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
    .WithEnvironment("Bootstrap__PlatformEnvironment", "Test")
    .WithEnvironment("Bootstrap__BrokerEnvironment", "Demo")
    .WithEnvironment("Bootstrap__TradingSchedule__StartOfDay", "08:00")
    .WithEnvironment("Bootstrap__TradingSchedule__EndOfDay", "16:30")
    .WithEnvironment("Bootstrap__TradingSchedule__TradingDays__0", "Monday")
    .WithEnvironment("Bootstrap__TradingSchedule__TradingDays__1", "Tuesday")
    .WithEnvironment("Bootstrap__TradingSchedule__TradingDays__2", "Wednesday")
    .WithEnvironment("Bootstrap__TradingSchedule__TradingDays__3", "Thursday")
    .WithEnvironment("Bootstrap__TradingSchedule__TradingDays__4", "Friday")
    .WithEnvironment("Bootstrap__TradingSchedule__WeekendBehavior", "ExcludeWeekends")
    .WithEnvironment("Bootstrap__TradingSchedule__TimeZone", "UTC")
    .WithEnvironment("Bootstrap__AuthSimulation__SessionLifetimeSeconds", sessionLifetimeSeconds)
    .WithEnvironment("Bootstrap__RetryPolicy__InitialDelaySeconds", "1")
    .WithEnvironment("Bootstrap__RetryPolicy__MaxAutomaticRetries", "5")
    .WithEnvironment("Bootstrap__RetryPolicy__Multiplier", "2")
    .WithEnvironment("Bootstrap__RetryPolicy__MaxDelaySeconds", "60")
    .WithEnvironment("Bootstrap__RetryPolicy__PeriodicDelayMinutes", "5")
    .WithEnvironment("Bootstrap__NotificationSettings__Provider", enableInfrastructureContainers ? "Smtp" : "RecordedOnly")
    .WithEnvironment("Bootstrap__NotificationSettings__EmailTo", "operator@local.test")
    .WithEnvironment("Bootstrap__UpdatedBy", "apphost-bootstrap")
    .WithUrlForEndpoint("https", _ => new()
    {
        Url = "/scalar/v1",
        DisplayText = "Scalar UI"
    });

if (platformDatabase is not null)
{
    api.WithReference(platformDatabase)
        .WaitFor(platformDatabase);
    api.WithEnvironment("NotificationTransports__Smtp__Host", "mailpit")
        .WithEnvironment("NotificationTransports__Smtp__Port", "1025")
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
