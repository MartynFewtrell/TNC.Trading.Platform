using System.Net;
using Bunit;
using TNC.Trading.Platform.Web.Components.Pages;

namespace TNC.Trading.Platform.Web.UnitTests;

public sealed class BrokerEnvironmentsTests
{
    /// <summary>
    /// Trace: catalog integrity remediation.
    /// Verifies: administrators receive actionable guidance when the broker catalog endpoint returns no records.
    /// Expected: the catalog panel identifies the empty state and directs the operator to restart the platform.
    /// Why: a blank catalog obscures a failed seed or altered database and invites an incorrect manual replacement.
    /// </summary>
    [Fact]
    public void Render_ShouldShowRecoveryGuidance_WhenCatalogIsEmpty()
    {
        using var context = new PlatformComponentTestContext(
            "local-admin",
            null,
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, Array.Empty<BrokerEnvironmentViewModel>()));

        var cut = context.Render<BrokerEnvironments>();

        cut.WaitForAssertion(() =>
            Assert.Contains(
                "Restart the platform to restore the required IG Demo entry",
                cut.Find("[data-testid='broker-environments-empty-catalog']").TextContent,
                StringComparison.Ordinal));
    }

    /// <summary>
    /// Trace: catalog integrity remediation.
    /// Verifies: lifecycle is rendered alongside availability for catalog entries.
    /// Expected: the administrator can distinguish the valid Active lifecycle from operational availability.
    /// Why: conflating these values previously obscured the executable-state defect.
    /// </summary>
    [Fact]
    public void Render_ShouldShowLifecycleAndAvailability_WhenCatalogContainsIgDemo()
    {
        var catalog = new[]
        {
            new BrokerEnvironmentViewModel(
                Guid.NewGuid(), "IG Demo", "Ig", "Demo", "Active", "Available", null, "IgDemo", true, false, null)
        };
        using var context = new PlatformComponentTestContext(
            "local-admin",
            null,
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, catalog));

        var cut = context.Render<BrokerEnvironments>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Lifecycle: Active", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("Availability: Available", cut.Markup, StringComparison.Ordinal);
        });
    }

    /// <summary>
    /// Trace: broker environment catalog administration.
    /// Verifies: retired broker environments are hidden by default and can be revealed from the Catalog header.
    /// Expected: active entries remain visible, retired entries appear only after selecting Show retired environments.
    /// Why: the catalog remains focused on usable environments without preventing administrators from reviewing retired records.
    /// </summary>
    [Fact]
    public void Render_ShouldHideRetiredEnvironmentsUntilRequested_WhenCatalogContainsActiveAndRetiredEntries()
    {
        var catalog = new[]
        {
            new BrokerEnvironmentViewModel(
                Guid.NewGuid(), "IG Demo", "Ig", "Demo", "Active", "Available", null, "IgDemo", true, false, null),
            new BrokerEnvironmentViewModel(
                Guid.NewGuid(), "Retired IG Demo", "Ig", "Demo", "Retired", "Unavailable", null, "IgDemo", true, false, null)
        };
        using var context = new PlatformComponentTestContext(
            "local-admin",
            null,
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, catalog));

        var cut = context.Render<BrokerEnvironments>();

        cut.WaitForAssertion(() =>
        {
            var catalogHeader = cut.Find("[data-testid='broker-environment-catalog-header']");
            Assert.Contains("Catalog", catalogHeader.TextContent, StringComparison.Ordinal);
            Assert.NotNull(catalogHeader.QuerySelector("#show-retired-environments"));
            Assert.Contains("IG Demo", cut.Markup, StringComparison.Ordinal);
            Assert.DoesNotContain("Retired IG Demo", cut.Markup, StringComparison.Ordinal);
        });

        cut.Find("#show-retired-environments").Change(true);

        cut.WaitForAssertion(() =>
            Assert.Contains("Retired IG Demo", cut.Markup, StringComparison.Ordinal));
    }

    /// <summary>
    /// Trace: broker environment catalog administration.
    /// Verifies: catalog entries use the shared accordion treatment and action buttons use the primary-action style.
    /// Expected: the IG Demo entry is initially collapsed, and both administrator actions have the same class as Create environment.
    /// Why: the catalog must remain easy to scan while preserving a consistent, recognizable action hierarchy.
    /// </summary>
    [Fact]
    public void Render_ShouldUseCollapsedCatalogAccordionAndPrimaryActions_WhenCatalogContainsIgDemo()
    {
        var catalog = new[]
        {
            new BrokerEnvironmentViewModel(
                Guid.NewGuid(), "IG Demo", "Ig", "Demo", "Active", "Available", null, "IgDemo", true, false, null)
        };
        using var context = new PlatformComponentTestContext(
            "local-admin",
            null,
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, catalog));

        var cut = context.Render<BrokerEnvironments>();

        cut.WaitForAssertion(() =>
        {
            var catalogSection = Assert.Single(cut.FindAll("details.platform-accordion-section"));
            Assert.DoesNotContain("open", catalogSection.OuterHtml, StringComparison.Ordinal);
            Assert.All(
                cut.FindAll("button").Where(button =>
                    button.TextContent is "Preview retirement" or "Replace credentials"),
                button => Assert.Contains("platform-primary-action", button.ClassList, StringComparer.Ordinal));
        });
    }

    /// <summary>
    /// Trace: broker environment catalog administration.
    /// Verifies: the create form explains the required catalog identifiers and their current supported IG Demo values.
    /// Expected: administrators see accurate input guidance before creating a broker environment.
    /// Why: a record with unsupported provider capability remains unavailable and should not be created accidentally.
    /// </summary>
    [Fact]
    public void Render_ShouldShowCreationGuidance_WhenBrokerEnvironmentPageLoads()
    {
        using var context = new PlatformComponentTestContext(
            "local-admin",
            null,
            _ => PlatformWebTestData.CreateJsonResponse(HttpStatusCode.OK, Array.Empty<BrokerEnvironmentViewModel>()));

        var cut = context.Render<BrokerEnvironments>();

        cut.WaitForAssertion(() =>
            Assert.Contains(
                "The currently supported IG Demo identifiers are provider Ig, kind Demo, and endpoint profile IgDemo.",
                cut.Find("[data-testid='broker-environment-creation-guidance']").TextContent,
                StringComparison.Ordinal));
    }
}
