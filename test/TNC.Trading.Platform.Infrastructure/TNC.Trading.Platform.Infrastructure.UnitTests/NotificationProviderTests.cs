using Microsoft.Extensions.Configuration;

namespace TNC.Trading.Platform.Infrastructure.UnitTests;

public class NotificationProviderTests
{
    [Fact]
    public async Task RecordedNotificationProvider_ReturnsRecordedProviderName()
    {
        var providerType = InfrastructureReflection.GetType("TNC.Trading.Platform.Infrastructure.Notifications.RecordedNotificationProvider");
        var provider = Activator.CreateInstance(providerType, InfrastructureReflection.CreateNullLogger(providerType))!;
        var message = InfrastructureReflection.Create(
            "TNC.Trading.Platform.Infrastructure.Notifications.NotificationMessage",
            "AuthFailure",
            "owner@example.com",
            "Summary");

        var result = await InfrastructureReflection.InvokeAsync(provider, "DispatchAsync", message, CancellationToken.None);

        Assert.Equal("Recorded", InfrastructureReflection.GetProperty<string>(result!, "Status"));
        Assert.Equal("RecordedOnly", InfrastructureReflection.GetProperty<string>(result!, "ProviderName"));
    }

    [Fact]
    public async Task SmtpNotificationProvider_WithoutConfiguration_ReturnsSkipped()
    {
        var providerType = InfrastructureReflection.GetType("TNC.Trading.Platform.Infrastructure.Notifications.SmtpNotificationProvider");
        var configuration = new ConfigurationBuilder().Build();
        var provider = Activator.CreateInstance(providerType, configuration, InfrastructureReflection.CreateNullLogger(providerType))!;
        var message = InfrastructureReflection.Create(
            "TNC.Trading.Platform.Infrastructure.Notifications.NotificationMessage",
            "AuthFailure",
            "owner@example.com",
            "Summary");

        var result = await InfrastructureReflection.InvokeAsync(provider, "DispatchAsync", message, CancellationToken.None);

        Assert.Equal("Skipped", InfrastructureReflection.GetProperty<string>(result!, "Status"));
        Assert.Equal("Smtp", InfrastructureReflection.GetProperty<string>(result!, "ProviderName"));
    }

    [Fact]
    public async Task AzureCommunicationServicesEmailNotificationProvider_WithoutConfiguration_ReturnsSkipped()
    {
        var providerType = InfrastructureReflection.GetType("TNC.Trading.Platform.Infrastructure.Notifications.AzureCommunicationServicesEmailNotificationProvider");
        var configuration = new ConfigurationBuilder().Build();
        var provider = Activator.CreateInstance(providerType, configuration, InfrastructureReflection.CreateNullLogger(providerType))!;
        var message = InfrastructureReflection.Create(
            "TNC.Trading.Platform.Infrastructure.Notifications.NotificationMessage",
            "AuthFailure",
            "owner@example.com",
            "Summary");

        var result = await InfrastructureReflection.InvokeAsync(provider, "DispatchAsync", message, CancellationToken.None);

        Assert.Equal("Skipped", InfrastructureReflection.GetProperty<string>(result!, "Status"));
        Assert.Equal("AzureCommunicationServicesEmail", InfrastructureReflection.GetProperty<string>(result!, "ProviderName"));
    }
}
