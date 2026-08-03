using TNC.Trading.Platform.Application.Services;

namespace TNC.Trading.Platform.Application.UnitTests;

public sealed class OperationalDataSensitivityPolicyTests
{
    /// <summary>
    /// Trace: Clean Architecture migration Phase 3 Step 3.2 operational sensitivity policy.
    /// Verifies: credential metadata names are classified as sensitive by the Application-owned policy.
    /// Expected: an API-key metadata field receives the sensitive classification without invoking serialization or persistence.
    /// Why: every operational adapter must share the same secret-safe decision before choosing its masking mechanism.
    /// </summary>
    [Fact]
    public void Classify_ShouldMarkCredentialMetadataSensitive_WhenOperationalDataIsEvaluated()
    {
        var classification = OperationalDataSensitivityPolicy.Classify("ApiKeyMetadata");

        Assert.Equal(OperationalDataSensitivity.Sensitive, classification);
    }

    /// <summary>
    /// Trace: Clean Architecture migration Phase 3 Step 3.2 operational sensitivity policy.
    /// Verifies: representative credential, token, authorization, connection, and protected-value fields retain their existing classification.
    /// Expected: every established sensitive fragment produces the sensitive classification wherever it appears in a field name.
    /// Why: moving policy ownership must preserve the exact fields currently removed from persisted operational JSON.
    /// </summary>
    [Theory]
    [InlineData("Identifier")]
    [InlineData("UserPassword")]
    [InlineData("AccessToken")]
    [InlineData("AuthorizationHeader")]
    [InlineData("DatabaseConnectionString")]
    [InlineData("CredentialProtectedValue")]
    [InlineData("NonSecret")]
    public void Classify_ShouldMarkEstablishedSensitiveCategories_WhenOperationalDataIsEvaluated(string fieldName)
    {
        var classification = OperationalDataSensitivityPolicy.Classify(fieldName);

        Assert.Equal(OperationalDataSensitivity.Sensitive, classification);
    }

    /// <summary>
    /// Trace: Clean Architecture migration Phase 3 Step 3.2 operational sensitivity policy.
    /// Verifies: ordinary operational metadata and unknown names remain visible.
    /// Expected: fields without an established sensitive fragment receive the non-sensitive classification.
    /// Why: the redactor must remain useful for diagnosis and must not broaden masking during this ownership-only refactor.
    /// </summary>
    [Theory]
    [InlineData("Summary")]
    [InlineData("CorrelationId")]
    [InlineData("Provider")]
    [InlineData("UnknownMetadata")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Classify_ShouldMarkUnknownAndOrdinaryMetadataNonSensitive_WhenOperationalDataIsEvaluated(string? fieldName)
    {
        var classification = OperationalDataSensitivityPolicy.Classify(fieldName);

        Assert.Equal(OperationalDataSensitivity.NonSensitive, classification);
    }

    /// <summary>
    /// Trace: Clean Architecture migration Phase 3 Step 3.2 operational sensitivity policy.
    /// Verifies: sensitivity fragment matching remains case-insensitive using ordinal comparison.
    /// Expected: a mixed-case token field receives the sensitive classification.
    /// Why: serializers and callers can supply different property-name casing without weakening secret protection.
    /// </summary>
    [Fact]
    public void Classify_ShouldIgnoreCasing_WhenSensitiveMetadataIsEvaluated()
    {
        var classification = OperationalDataSensitivityPolicy.Classify("aCcEsStOkEnMetadata");

        Assert.Equal(OperationalDataSensitivity.Sensitive, classification);
    }
}