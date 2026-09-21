using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.AccountPreferences;

namespace TNC.Trading.Platform.Application.UnitTests.Features.AccountPreferences;

public sealed class AccountPreferencesEnvironmentPolicyTests
{
    /// <summary>
    /// Trace: DR-02/DD-01. Verifies legacy Demo storage is presented as Test without rewriting historical data.
    /// Expected: the feature-facing label is Test, preserving the existing provider partition.
    /// </summary>
    [Fact]
    public void ForAccountPreferences_ShouldReturnTest_WhenLegacyBrokerEnvironmentIsDemo()
    {
        Assert.Equal("Test", BrokerEnvironmentPresentation.ForAccountPreferences(BrokerEnvironmentKind.Demo));
    }

    /// <summary>
    /// Trace: DR-03. Verifies the account-preferences feature cannot execute against Live.
    /// Expected: Demo is supported and Live is rejected before any provider call, preventing unauthorized monetary exposure.
    /// </summary>
    [Fact]
    public void IsSupported_ShouldRequireTestPlatformAndDemoBroker_WhenCheckingFeatureEnvironment()
    {
        Assert.True(AccountPreferencesEnvironmentPolicy.IsSupported(PlatformEnvironmentKind.Test, BrokerEnvironmentKind.Demo));
        Assert.False(AccountPreferencesEnvironmentPolicy.IsSupported(PlatformEnvironmentKind.Live, BrokerEnvironmentKind.Demo));
        Assert.False(AccountPreferencesEnvironmentPolicy.IsSupported(PlatformEnvironmentKind.Test, BrokerEnvironmentKind.Live));
    }
}