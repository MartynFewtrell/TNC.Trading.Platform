using System.Xml.Linq;

namespace TNC.Trading.Platform.Architecture.IntegrationTests.ArchitectureRules;

public sealed class ProductionProjectRoleValidator
{
    private static readonly string[] ForbiddenApplicationProtocolTerms =
    [
        "HttpClient",
        "\"CST\"",
        "\"X-SECURITY-TOKEN\"",
        "IgAuthenticateResponse",
        "IIgSessionClient"
    ];

    private readonly ProductionProjectRolePolicy policy;

    public ProductionProjectRoleValidator(ProductionProjectRolePolicy? policy = null)
    {
        this.policy = policy ?? new ProductionProjectRolePolicy();
    }

    public IReadOnlyList<string> Validate(string productionRoot)
    {
        var projects = Directory.EnumerateFiles(productionRoot, "*.csproj", SearchOption.AllDirectories)
            .Select(Load)
            .ToArray();
        var diagnostics = new List<string>();

        foreach (var project in projects)
        {
            if (project.Name.Equals(ProductionProjectRolePolicy.ApplicationProjectName, StringComparison.OrdinalIgnoreCase))
            {
                ValidateApplication(project, diagnostics);
                ValidateApplicationSource(Path.GetDirectoryName(project.Path)!, diagnostics);
            }

            if (project.Name.Equals(ProductionProjectRolePolicy.InfrastructureProjectName, StringComparison.OrdinalIgnoreCase) &&
                !project.ProjectReferences.Contains(ProductionProjectRolePolicy.ApplicationProjectName, StringComparer.OrdinalIgnoreCase))
            {
                diagnostics.Add($"Infrastructure project '{project.Path}' must reference Application.");
            }

            if (project.Name.Equals(ProductionProjectRolePolicy.DomainProjectName, StringComparison.OrdinalIgnoreCase) &&
                project.ProjectReferences.Count > 0)
            {
                diagnostics.Add($"Domain project '{project.Path}' must not reference production projects.");
            }
        }

        return diagnostics;
    }

    private static void ValidateApplicationSource(string applicationRoot, ICollection<string> diagnostics)
    {
        foreach (var sourceFile in Directory.EnumerateFiles(applicationRoot, "*.cs", SearchOption.AllDirectories)
                     .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
                         && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)))
        {
            var source = File.ReadAllText(sourceFile);
            foreach (var forbiddenTerm in ForbiddenApplicationProtocolTerms.Where(source.Contains))
            {
                diagnostics.Add($"Application source '{sourceFile}' must not mention provider protocol term '{forbiddenTerm}'.");
            }
        }
    }

    private void ValidateApplication(ProjectFile project, ICollection<string> diagnostics)
    {
        foreach (var reference in project.ProjectReferences)
        {
            diagnostics.Add($"Application project '{project.Path}' must not reference production project '{reference}'.");
        }

        foreach (var framework in project.FrameworkReferences.Where(framework =>
                 policy.ForbiddenApplicationFrameworkReferences.Contains(framework)))
        {
            diagnostics.Add($"Application project '{project.Path}' must not reference forbidden framework '{framework}'.");
        }

        foreach (var package in project.PackageReferences.Where(package => policy.ForbiddenApplicationPackagePrefixes.Any(package.StartsWith)))
        {
            diagnostics.Add($"Application project '{project.Path}' must not reference outward package '{package}'.");
        }
    }

    private static ProjectFile Load(string path)
    {
        var document = XDocument.Load(path);
        var projectReferences = document.Descendants("ProjectReference")
            .Select(reference => Path.GetFileNameWithoutExtension(reference.Attribute("Include")?.Value ?? string.Empty))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToArray();
        var packageReferences = document.Descendants("PackageReference")
            .Select(reference => reference.Attribute("Include")?.Value)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Cast<string>()
            .ToArray();
        var frameworkReferences = document.Descendants("FrameworkReference")
            .Select(reference => reference.Attribute("Include")?.Value)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Cast<string>()
            .ToArray();

        return new ProjectFile(Path.GetFileNameWithoutExtension(path), path, projectReferences, packageReferences, frameworkReferences);
    }

    private sealed record ProjectFile(
        string Name,
        string Path,
        IReadOnlyList<string> ProjectReferences,
        IReadOnlyList<string> PackageReferences,
        IReadOnlyList<string> FrameworkReferences);
}