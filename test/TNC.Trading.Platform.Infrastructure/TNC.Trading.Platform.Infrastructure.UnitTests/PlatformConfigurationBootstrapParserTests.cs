using Microsoft.Extensions.Configuration;
using TNC.Trading.Platform.Infrastructure.Platform;

namespace TNC.Trading.Platform.Infrastructure.UnitTests;

public class PlatformConfigurationBootstrapParserTests
{
    [Fact]
    public void Parse_ShouldDefaultNotificationProviderToRecordedOnly_WhenSmtpHostIsMissing()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Bootstrap:BrokerEnvironment"] = "Demo"
            })
            .Build();

        var bootstrap = PlatformConfigurationBootstrapParser.Parse(configuration);

        Assert.Equal("RecordedOnly", bootstrap.NotificationSettings.Provider);
        Assert.Equal("bootstrap", bootstrap.UpdatedBy);
    }

    [Fact]
    public void ResolveNotificationProvider_ShouldPreferConfiguredBootstrapProvider_WhenProvided()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Bootstrap:NotificationSettings:Provider"] = "AzureCommunicationServicesEmail",
                ["NotificationTransports:Smtp:Host"] = "mailpit"
            })
            .Build();

        var provider = PlatformConfigurationBootstrapParser.ResolveNotificationProvider(configuration);

        Assert.Equal("AzureCommunicationServicesEmail", provider);
    }
}