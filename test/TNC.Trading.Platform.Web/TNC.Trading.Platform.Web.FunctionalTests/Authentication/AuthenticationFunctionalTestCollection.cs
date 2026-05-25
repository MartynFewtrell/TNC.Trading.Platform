using Xunit;

namespace TNC.Trading.Platform.Web.FunctionalTests.Authentication;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class AuthenticationFunctionalTestCollection : ICollectionFixture<RealAuthenticationFunctionalTestFixture>
{
    public const string Name = "Authentication Functional";
}