using System.Xml.Linq;

namespace TNC.Trading.Platform.Architecture.IntegrationTests.ProjectGraph;

public sealed class ProjectGraphLoader
{
    public ProjectGraph LoadProductionGraph(string repositoryRoot)
    {
        var productionRoot = Path.Combine(repositoryRoot, "src");
        var projectFiles = Directory.EnumerateFiles(productionRoot, "*.csproj", SearchOption.AllDirectories);
        return Load(projectFiles);
    }

    public ProjectGraph Load(IEnumerable<string> projectFiles)
    {
        var normalizedProjectFiles = projectFiles
            .Select(ProjectGraph.NormalizePath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var projects = normalizedProjectFiles.Select(projectPath =>
        {
            var document = XDocument.Load(projectPath, LoadOptions.PreserveWhitespace);
            var references = document
                .Descendants("ProjectReference")
                .Select(reference => reference.Attribute("Include")?.Value)
                .Where(reference => !string.IsNullOrWhiteSpace(reference))
                .Select(reference => ProjectGraph.NormalizePath(Path.Combine(Path.GetDirectoryName(projectPath)!, reference!)))
                .ToArray();

            return new ProjectNode(projectPath, references);
        });

        return new ProjectGraph(projects);
    }
}
