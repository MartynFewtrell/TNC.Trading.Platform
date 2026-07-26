namespace TNC.Trading.Platform.Architecture.IntegrationTests.ProjectGraph;

public sealed record ProjectNode(string Path, IReadOnlyList<string> References);
