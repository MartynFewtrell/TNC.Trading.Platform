using TNC.Trading.Platform.Architecture.IntegrationTests.ProjectGraph;

namespace TNC.Trading.Platform.Architecture.IntegrationTests.ArchitectureRules;

public sealed class RepositoryArchitectureRuleTests
{
    /// <summary>
    /// Verifies topology-neutral graph integrity and repository-specific role boundaries for production projects.
    /// Expected: the current transitional topology has no missing references, cycles, or role violations.
    /// Why: later migration phases need an executable safety rail without freezing file inventories or requiring Domain prematurely.
    /// </summary>
    [Fact]
    public void Validate_RepositoryArchitecture_ReturnsNoDiagnostics()
    {
        var repositoryRoot = FindRepositoryRoot();
        var graph = new ProjectGraphLoader().LoadProductionGraph(repositoryRoot);
        var graphDiagnostics = new ProjectGraphValidator().Validate(graph);
        var roleDiagnostics = new ProductionProjectRoleValidator().Validate(Path.Combine(repositoryRoot, "src"));

        Assert.True(graphDiagnostics.Count == 0, string.Join(Environment.NewLine, graphDiagnostics.Select(diagnostic => diagnostic.Message)));
        Assert.Empty(roleDiagnostics);
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