using Microsoft.Extensions.Configuration;

namespace TNC.Trading.Platform.Api.UnitTests;

public class NotificationProviderTests
{
    [Fact]
    public async Task RecordedNotificationProvider_ReturnsRecordedProviderName()
    {
        var providerType = ApiReflection.GetType("TNC.Trading.Platform.Api.Infrastructure.Notifications.RecordedNotificationProvider");
        var provider = Activator.CreateInstance(providerType, ApiReflection.CreateNullLogger(providerType))!;
        var message = ApiReflection.Create(
            "TNC.Trading.Platform.Api.Infrastructure.Notifications.NotificationMessage",
            "AuthFailure",
            "owner@example.com",
            "Summary");

        var result = await ApiReflection.InvokeAsync(provider, "DispatchAsync", message, CancellationToken.None);

        Assert.Equal("Recorded", ApiReflection.GetProperty<string>(result!, "Status"));
        Assert.Equal("RecordedOnly", ApiReflection.GetProperty<string>(result!, "ProviderName"));
    }

    [Fact]
    public async Task SmtpNotificationProvider_WithoutConfiguration_ReturnsSkipped()
    {
        var providerType = ApiReflection.GetType("TNC.Trading.Platform.Api.Infrastructure.Notifications.SmtpNotificationProvider");
        var configuration = new ConfigurationBuilder().Build();
        var provider = Activator.CreateInstance(providerType, configuration, ApiReflection.CreateNullLogger(providerType))!;
        var message = ApiReflection.Create(
            "TNC.Trading.Platform.Api.Infrastructure.Notifications.NotificationMessage",
            "AuthFailure",
            "owner@example.com",
            "Summary");

        var result = await ApiReflection.InvokeAsync(provider, "DispatchAsync", message, CancellationToken.None);

        Assert.Equal("Skipped", ApiReflection.GetProperty<string>(result!, "Status"));
        Assert.Equal("Smtp", ApiReflection.GetProperty<string>(result!, "ProviderName"));
    }

    [Fact]
    public async Task AzureCommunicationServicesEmailNotificationProvider_WithoutConfiguration_ReturnsSkipped()
    {
        var providerType = ApiReflection.GetType("TNC.Trading.Platform.Api.Infrastructure.Notifications.AzureCommunicationServicesEmailNotificationProvider");
        var configuration = new ConfigurationBuilder().Build();
        var provider = Activator.CreateInstance(providerType, configuration, ApiReflection.CreateNullLogger(providerType))!;
        var message = ApiReflection.Create(
            "TNC.Trading.Platform.Api.Infrastructure.Notifications.NotificationMessage",
            "AuthFailure",
            "owner@example.com",
            "Summary");

        var result = await ApiReflection.InvokeAsync(provider, "DispatchAsync", message, CancellationToken.None);

        Assert.Equal("Skipped", ApiReflection.GetProperty<string>(result!, "Status"));
        Assert.Equal("AzureCommunicationServicesEmail", ApiReflection.GetProperty<string>(result!, "ProviderName"));
    }
}
