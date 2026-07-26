namespace TNC.Trading.Platform.Architecture.IntegrationTests.ProjectGraph;

public sealed class ProjectGraph
{
    private readonly IReadOnlyDictionary<string, ProjectNode> nodes;

    public ProjectGraph(IEnumerable<ProjectNode> projects)
    {
        nodes = projects.ToDictionary(
            project => NormalizePath(project.Path),
            project => new ProjectNode(
                NormalizePath(project.Path),
                project.References.Select(NormalizePath).ToArray()),
            StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyDictionary<string, ProjectNode> Nodes => nodes;

    public static string NormalizePath(string path) => Path.GetFullPath(path).Replace('\\', '/');
}
