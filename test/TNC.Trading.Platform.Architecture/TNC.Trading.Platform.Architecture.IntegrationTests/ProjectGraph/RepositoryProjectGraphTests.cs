namespace TNC.Trading.Platform.Architecture.IntegrationTests.ProjectGraph;

public sealed class RepositoryProjectGraphTests
{
    /// <summary>
    /// Verifies that all production project references resolve and the repository graph is acyclic.
    /// The assertion reads the current source tree but intentionally does not encode project names, counts, or layer roles,
    /// so valid additions, removals, and refactoring renames remain compatible with the check.
    /// </summary>
    [Fact]
    public void Validate_RepositoryProductionGraph_ReturnsNoDiagnostics()
    {
        var repositoryRoot = FindRepositoryRoot();
        var graph = new ProjectGraphLoader().LoadProductionGraph(repositoryRoot);
        var diagnostics = new ProjectGraphValidator().Validate(graph);

        Assert.True(diagnostics.Count == 0, string.Join(Environment.NewLine, diagnostics.Select(diagnostic => diagnostic.Message)));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "TNC.Trading.Platform.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}
