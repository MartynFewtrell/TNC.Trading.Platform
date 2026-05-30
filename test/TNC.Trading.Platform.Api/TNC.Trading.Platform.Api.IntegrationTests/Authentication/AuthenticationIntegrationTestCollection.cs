namespace TNC.Trading.Platform.Api.IntegrationTests.Authentication;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class AuthenticationIntegrationTestCollection : ICollectionFixture<SyntheticTokenIntegrationTestFixture>
{
    public const string Name = "Authentication Integration";
}
