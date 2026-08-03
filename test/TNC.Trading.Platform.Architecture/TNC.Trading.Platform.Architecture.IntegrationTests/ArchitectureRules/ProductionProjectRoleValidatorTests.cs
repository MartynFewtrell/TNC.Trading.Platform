namespace TNC.Trading.Platform.Architecture.IntegrationTests.ArchitectureRules;

public sealed class ProductionProjectRoleValidatorTests
{
    /// <summary>
    /// Verifies the inward dependency rule using a deliberate local forbidden-reference probe.
    /// Expected: Application-to-Infrastructure produces a role diagnostic, proving the rule is active before the probe is discarded.
    /// Why: the repository must detect the dependency that the topology-neutral graph validator intentionally permits.
    /// </summary>
    [Fact]
    public void Validate_ShouldRejectInfrastructureReference_WhenApplicationReferencesInfrastructure()
    {
        using var repository = TemporaryRepository.Create("""
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup><ProjectReference Include="..\TNC.Trading.Platform.Infrastructure\TNC.Trading.Platform.Infrastructure.csproj" /></ItemGroup>
            </Project>
            """);

        var diagnostics = new ProductionProjectRoleValidator().Validate(repository.Path);

        Assert.Contains(diagnostics, diagnostic => diagnostic.Contains("must not reference production project", StringComparison.Ordinal));
    }

    /// <summary>
    /// Verifies that the Application role rejects an outward framework dependency.
    /// Expected: Microsoft.AspNetCore.App produces a framework diagnostic, protecting framework-neutral use cases during migration.
    /// Why: project acyclicity alone cannot detect framework leakage into the inner boundary.
    /// </summary>
    [Fact]
    public void Validate_ShouldRejectOuterFrameworkReference_WhenApplicationReferencesForbiddenFramework()
    {
        using var repository = TemporaryRepository.Create("""
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup><FrameworkReference Include="Microsoft.AspNetCore.App" /></ItemGroup>
            </Project>
            """);

        var diagnostics = new ProductionProjectRoleValidator().Validate(repository.Path);

        Assert.Contains(diagnostics, diagnostic => diagnostic.Contains("forbidden framework", StringComparison.Ordinal));
    }

    /// <summary>
    /// Verifies the temporary Web exception for shared authentication contracts.
    /// Expected: Web-to-Application is accepted while those contracts remain inward-owned; Phase 8 retires this allowance after parity tests.
    /// Why: safety rails must support the current migration state without freezing a future project layout.
    /// </summary>
    [Fact]
    public void Validate_ShouldAllowWebApplicationReference_WhenSharedAuthenticationContractsRemain()
    {
        using var repository = TemporaryRepository.Create("""
            <Project Sdk="Microsoft.NET.Sdk.Web">
              <ItemGroup><ProjectReference Include="..\TNC.Trading.Platform.Application\TNC.Trading.Platform.Application.csproj" /></ItemGroup>
            </Project>
            """, ProductionProjectRolePolicy.WebProjectName);

        var diagnostics = new ProductionProjectRoleValidator().Validate(repository.Path);

        Assert.Empty(diagnostics);
    }

    /// <summary>
    /// Verifies Domain activation is conditional and, when present, requires no outward production references.
    /// Expected: a Domain project referencing Application is rejected without requiring Domain to exist in the current solution.
    /// Why: an empty Domain layer must not be imposed, while a future Domain boundary must remain dependency-free.
    /// </summary>
    [Fact]
    public void Validate_ShouldRequireNoProductionReferences_WhenDomainProjectExists()
    {
        using var repository = TemporaryRepository.Create("""
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup><ProjectReference Include="..\TNC.Trading.Platform.Application\TNC.Trading.Platform.Application.csproj" /></ItemGroup>
            </Project>
            """, ProductionProjectRolePolicy.DomainProjectName);

        var diagnostics = new ProductionProjectRoleValidator().Validate(repository.Path);

        Assert.Contains(diagnostics, diagnostic => diagnostic.Contains("Domain project", StringComparison.Ordinal));
    }

    /// <summary>
    /// Verifies the Phase 4 provider boundary rejects HTTP clients, IG session headers, and retired provider response types in Application source.
    /// Expected: each deliberate protocol mention produces an Application source diagnostic.
    /// Why: provider transport and session mechanics must remain in Infrastructure after the atomic port-plus-adapter migration.
    /// </summary>
    [Fact]
    public void Validate_ShouldRejectProviderProtocolType_WhenApplicationMentionsHttpOrIgHeaders()
    {
        using var repository = TemporaryRepository.Create(
            """
            <Project Sdk="Microsoft.NET.Sdk" />
            """,
            source: """
                var client = new HttpClient();
                var sessionHeader = "CST";
                var securityHeader = "X-SECURITY-TOKEN";
                IgAuthenticateResponse? response = null;
                """);

        var diagnostics = new ProductionProjectRoleValidator().Validate(repository.Path);

        Assert.Equal(4, diagnostics.Count(diagnostic => diagnostic.Contains("provider protocol term", StringComparison.Ordinal)));
    }

    private sealed class TemporaryRepository : IDisposable
    {
        private TemporaryRepository(string path) => Path = path;

        public string Path { get; }

        public static TemporaryRepository Create(
            string projectFile,
            string projectName = ProductionProjectRolePolicy.ApplicationProjectName,
            string? source = null)
        {
            var path = Directory.CreateTempSubdirectory("architecture-role-").FullName;
            var projectDirectory = System.IO.Path.Combine(path, projectName);
            Directory.CreateDirectory(projectDirectory);
            File.WriteAllText(System.IO.Path.Combine(projectDirectory, $"{projectName}.csproj"), projectFile);
            if (source is not null)
            {
                File.WriteAllText(System.IO.Path.Combine(projectDirectory, "ProtocolProbe.cs"), source);
            }

            return new TemporaryRepository(path);
        }

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}