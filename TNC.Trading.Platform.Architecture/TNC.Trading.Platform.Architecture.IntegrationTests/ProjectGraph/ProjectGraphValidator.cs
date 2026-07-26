namespace TNC.Trading.Platform.Architecture.IntegrationTests.ProjectGraph;

public sealed class ProjectGraphValidator
{
    public IReadOnlyList<ProjectGraphDiagnostic> Validate(ProjectGraph graph)
    {
        var diagnostics = ValidateMissingTargets(graph).ToList();
        diagnostics.AddRange(ValidateCycles(graph));
        return diagnostics;
    }

    private static IEnumerable<ProjectGraphDiagnostic> ValidateMissingTargets(ProjectGraph graph)
    {
        foreach (var project in graph.Nodes.Values)
        {
            foreach (var reference in project.References)
            {
                if (!graph.Nodes.ContainsKey(reference))
                {
                    yield return new ProjectGraphDiagnostic(
                        "MissingTarget",
                        $"Project '{project.Path}' references missing project '{reference}'.");
                }
            }
        }
    }

    private static IEnumerable<ProjectGraphDiagnostic> ValidateCycles(ProjectGraph graph)
    {
        var states = new Dictionary<string, VisitState>(StringComparer.OrdinalIgnoreCase);
        var path = new List<string>();
        var reportedCycles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var project in graph.Nodes.Keys)
        {
            foreach (var diagnostic in Visit(project, graph, states, path, reportedCycles))
            {
                yield return diagnostic;
            }
        }
    }

    private static IEnumerable<ProjectGraphDiagnostic> Visit(
        string project,
        ProjectGraph graph,
        IDictionary<string, VisitState> states,
        IList<string> path,
        ISet<string> reportedCycles)
    {
        if (states.TryGetValue(project, out var state) && state == VisitState.Completed)
        {
            yield break;
        }

        if (states.TryGetValue(project, out state) && state == VisitState.Active)
        {
            var cycleStart = path.IndexOf(project);
            var cycle = path.Skip(cycleStart).Append(project).ToArray();
            var cycleKey = string.Join(" -> ", cycle);
            if (reportedCycles.Add(cycleKey))
            {
                yield return new ProjectGraphDiagnostic(
                    "Cycle",
                    $"Project reference cycle detected: {cycleKey}.");
            }

            yield break;
        }

        states[project] = VisitState.Active;
        path.Add(project);

        if (graph.Nodes.TryGetValue(project, out var node))
        {
            foreach (var reference in node.References)
            {
                foreach (var diagnostic in Visit(reference, graph, states, path, reportedCycles))
                {
                    yield return diagnostic;
                }
            }
        }

        path.RemoveAt(path.Count - 1);
        states[project] = VisitState.Completed;
    }

    private enum VisitState
    {
        Active,
        Completed
    }
}
