namespace TNC.Trading.Platform.Architecture.IntegrationTests.ProjectGraph;

public sealed class ProjectGraphValidatorTests
{
    private readonly ProjectGraphValidator validator = new();

    /// <summary>
    /// Verifies the portable graph invariant that a reference must resolve to a discovered project.
    /// A missing target produces an actionable diagnostic because broken references prevent reliable composition.
    /// </summary>
    [Fact]
    public void Validate_MissingTargetReference_ReturnsMissingTargetDiagnostic()
    {
        var source = Path.Combine(Path.GetTempPath(), "source.csproj");
        var missing = Path.Combine(Path.GetTempPath(), "missing.csproj");
        var graph = new ProjectGraph([new ProjectNode(source, [missing])]);

        var diagnostics = validator.Validate(graph);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("MissingTarget", diagnostic.Code);
        Assert.Contains(ProjectGraph.NormalizePath(missing), diagnostic.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies that a direct two-project cycle is rejected with the complete cycle path.
    /// This guards the production graph invariant without assigning architectural roles to either project.
    /// </summary>
    [Fact]
    public void Validate_DirectCycle_ReturnsCompleteCycleDiagnostic()
    {
        var first = Path.Combine(Path.GetTempPath(), "first.csproj");
        var second = Path.Combine(Path.GetTempPath(), "second.csproj");
        var graph = new ProjectGraph(
        [
            new ProjectNode(first, [second]),
            new ProjectNode(second, [first])
        ]);

        var diagnostics = validator.Validate(graph);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("Cycle", diagnostic.Code);
        Assert.Contains("first.csproj", diagnostic.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("second.csproj", diagnostic.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies that cycles spanning more than one intermediate project are rejected.
    /// This protects transitive dependency integrity while permitting any valid project topology.
    /// </summary>
    [Fact]
    public void Validate_TransitiveCycle_ReturnsCompleteCycleDiagnostic()
    {
        var first = Path.Combine(Path.GetTempPath(), "first.csproj");
        var second = Path.Combine(Path.GetTempPath(), "second.csproj");
        var third = Path.Combine(Path.GetTempPath(), "third.csproj");
        var graph = new ProjectGraph(
        [
            new ProjectNode(first, [second]),
            new ProjectNode(second, [third]),
            new ProjectNode(third, [first])
        ]);

        var diagnostics = validator.Validate(graph);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("Cycle", diagnostic.Code);
        Assert.Contains("first.csproj", diagnostic.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("second.csproj", diagnostic.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("third.csproj", diagnostic.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies that a valid directed acyclic graph produces no diagnostics.
    /// This is the positive control for the invariant and keeps the check independent from layer names.
    /// </summary>
    [Fact]
    public void Validate_AcyclicGraph_ReturnsNoDiagnostics()
    {
        var graph = CreateValidGraph();

        var diagnostics = validator.Validate(graph);

        Assert.Empty(diagnostics);
    }

    /// <summary>
    /// Verifies that adding a valid project and renaming a project with updated references remains accepted.
    /// This guards the requirement that graph integrity does not freeze the current project set or names.
    /// </summary>
    [Fact]
    public void Validate_ValidAdditionAndRename_ReturnsNoDiagnostics()
    {
        var renamed = Path.Combine(Path.GetTempPath(), "renamed.csproj");
        var added = Path.Combine(Path.GetTempPath(), "added.csproj");
        var graph = new ProjectGraph(
        [
            new ProjectNode(renamed, [added]),
            new ProjectNode(added, [])
        ]);

        var diagnostics = validator.Validate(graph);

        Assert.Empty(diagnostics);
    }

    /// <summary>
    /// Verifies that removing an unreferenced project remains valid.
    /// This prevents the integrity suite from treating a historical project inventory as an architecture rule.
    /// </summary>
    [Fact]
    public void Validate_ValidRemovalOfUnreferencedProject_ReturnsNoDiagnostics()
    {
        var graph = new ProjectGraph(
        [new ProjectNode(Path.Combine(Path.GetTempPath(), "remaining.csproj"), [])]);

        var diagnostics = validator.Validate(graph);

        Assert.Empty(diagnostics);
    }

    private static ProjectGraph CreateValidGraph()
    {
        var root = Path.Combine(Path.GetTempPath(), "root.csproj");
        var middle = Path.Combine(Path.GetTempPath(), "middle.csproj");
        var leaf = Path.Combine(Path.GetTempPath(), "leaf.csproj");
        return new ProjectGraph(
        [
            new ProjectNode(root, [middle]),
            new ProjectNode(middle, [leaf]),
            new ProjectNode(leaf, [])
        ]);
    }
}
