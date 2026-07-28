namespace TNC.Trading.Platform.Architecture.IntegrationTests.ArchitectureRules;

public sealed class ProductionProjectRolePolicy
{
    public const string ApplicationProjectName = "TNC.Trading.Platform.Application";
    public const string InfrastructureProjectName = "TNC.Trading.Platform.Infrastructure";
    public const string WebProjectName = "TNC.Trading.Platform.Web";
    public const string DomainProjectName = "TNC.Trading.Platform.Domain";

    public IReadOnlySet<string> ForbiddenApplicationFrameworkReferences { get; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Microsoft.AspNetCore.App"
        };

    public IReadOnlySet<string> ForbiddenApplicationPackagePrefixes { get; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Microsoft.AspNetCore.",
            "Microsoft.EntityFrameworkCore.",
            "Azure."
        };
}