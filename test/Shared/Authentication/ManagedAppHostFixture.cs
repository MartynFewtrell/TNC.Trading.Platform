using Aspire.Hosting;
using Aspire.Hosting.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace TNC.Trading.Platform.TestShared.Authentication;

/// <summary>
/// Owns one randomized, closed-box AppHost instance for a test collection.
/// </summary>
public sealed class ManagedAppHostFixture : IAsyncLifetime
{
    private static readonly TimeSpan InitializationTimeout = TimeSpan.FromSeconds(90);
    private readonly IReadOnlyDictionary<string, string?> configuration;
    private IDistributedApplicationTestingBuilder? builder;
    private DistributedApplication? application;

    public ManagedAppHostFixture(IReadOnlyDictionary<string, string?>? configuration = null)
    {
        var fixtureConfiguration = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["AppHost:UsePersistentKeycloakState"] = bool.FalseString,
            ["AppHost:UsePersistentSqlState"] = bool.FalseString
        };

        if (configuration is not null)
        {
            foreach (var setting in configuration)
            {
                fixtureConfiguration[setting.Key] = setting.Value;
            }
        }

        this.configuration = fixtureConfiguration;
    }

    public Uri WebEndpointUri { get; private set; } = null!;

    public Uri ApiEndpointUri { get; private set; } = null!;

    public Uri KeycloakEndpointUri { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        using var timeout = new CancellationTokenSource(InitializationTimeout);
        var token = timeout.Token;

        try
        {
            builder = await DistributedApplicationTestingBuilder
                .CreateAsync<Projects.TNC_Trading_Platform_AppHost>(
                    configuration
                        .Where(setting => setting.Value is not null)
                        .Select(setting => $"--{setting.Key}={setting.Value}")
                        .ToArray(),
                    cancellationToken: token)
                .ConfigureAwait(false);
            application = await builder.BuildAsync(token).ConfigureAwait(false);
            await application.StartAsync(token).ConfigureAwait(false);

            await WaitForResourceHealthyAsync("keycloak", token).ConfigureAwait(false);
            await WaitForResourceHealthyAsync("api", token).ConfigureAwait(false);
            await WaitForResourceHealthyAsync("web", token).ConfigureAwait(false);
            WebEndpointUri = application.GetEndpoint("web", "https");
            ApiEndpointUri = application.GetEndpoint("api", "https");
            KeycloakEndpointUri = application.GetEndpoint("keycloak", "http");
        }
        catch
        {
            await DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    public HttpClient CreateApiClient() => CreateHttpClient("api", "https");

    public HttpClient CreateWebClient() => CreateHttpClient("web", "https");

    public async Task ResetAccountPreferencesAsync(string sessionAccountId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionAccountId);
        if (application is null)
        {
            throw new InvalidOperationException("The managed AppHost fixture has not been initialized.");
        }

        var connectionString = await application.GetConnectionStringAsync("platformdb", cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The managed AppHost fixture did not expose the platformdb connection string.");
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            DECLARE @demoBrokerEnvironmentId UNIQUEIDENTIFIER;
            SELECT @demoBrokerEnvironmentId = [BrokerEnvironmentId] FROM [BrokerEnvironments] WHERE [NormalizedName] = 'IG DEMO';
            UPDATE [IgLoginSnapshots]
            SET [CurrentAccountId] = @sessionAccountId,
                [CapturedAtUtc] = DATEADD(second, 1, SYSUTCDATETIME())
            WHERE [BrokerEnvironment] = 'Demo';
            DELETE FROM [TrailingStopsPreferenceObservations]
            WHERE [BrokerEnvironmentId] = @demoBrokerEnvironmentId;
            DELETE audit
            FROM [AccountPreferencesDesiredStateAudits] AS audit
            INNER JOIN [AccountPreferencesCurrentStates] AS state
                ON state.[AccountPreferencesCurrentStateId] = audit.[AccountPreferencesCurrentStateId]
            WHERE state.[BrokerEnvironmentId] = @demoBrokerEnvironmentId;
            DELETE FROM [AccountPreferencesOperations]
            WHERE [BrokerEnvironmentId] = @demoBrokerEnvironmentId;
            UPDATE [AccountPreferencesCurrentStates]
            SET [AccountId] = @sessionAccountId,
                [DesiredTrailingStopsEnabled] = 0,
                [DesiredRevision] = 1,
                [DesiredActor] = 'test-fixture',
                [DesiredChangedAtUtc] = SYSUTCDATETIME(),
                [ObservedTrailingStopsEnabled] = 0,
                [ObservedAccountId] = @sessionAccountId,
                [ObservedAtUtc] = SYSUTCDATETIME(),
                [AuthenticationSnapshotId] = NULL,
                [AttemptId] = NULL,
                [VerificationStatus] = 'InSync',
                [LastVerifiedAtUtc] = SYSUTCDATETIME(),
                [NextRetryAtUtc] = NULL,
                [RetryCount] = 0,
                [FailureSummary] = NULL,
                [CorrelationId] = NULL
            WHERE [BrokerEnvironmentId] = @demoBrokerEnvironmentId;
            IF @@ROWCOUNT = 0
            BEGIN
                INSERT INTO [AccountPreferencesCurrentStates]
                ([AccountPreferencesCurrentStateId], [PlatformEnvironment], [BrokerEnvironment], [BrokerEnvironmentId], [AccountId], [DesiredTrailingStopsEnabled], [DesiredRevision], [DesiredActor], [DesiredChangedAtUtc], [ObservedTrailingStopsEnabled], [ObservedAccountId], [ObservedAtUtc], [VerificationStatus], [LastVerifiedAtUtc], [RetryCount])
                VALUES (NEWID(), 'Test', 'Demo', @demoBrokerEnvironmentId, @sessionAccountId, 0, 1, 'test-fixture', SYSUTCDATETIME(), 0, @sessionAccountId, SYSUTCDATETIME(), 'InSync', SYSUTCDATETIME(), 0);
            END;
            """;
        command.Parameters.AddWithValue("@sessionAccountId", sessionAccountId);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task SetAccountPreferencesSessionAccountAsync(string sessionAccountId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionAccountId);
        if (application is null)
        {
            throw new InvalidOperationException("The managed AppHost fixture has not been initialized.");
        }

        var connectionString = await application.GetConnectionStringAsync("platformdb", cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The managed AppHost fixture did not expose the platformdb connection string.");
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE [IgLoginSnapshots]
                        SET [CurrentAccountId] = @sessionAccountId,
                                [CapturedAtUtc] = DATEADD(second, 1, SYSUTCDATETIME())
                        WHERE [BrokerEnvironment] = 'Demo'
                            AND [SnapshotKind] = 'Latest';
            """;
        command.Parameters.AddWithValue("@sessionAccountId", sessionAccountId);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task SetAccountPreferencesTargetAccountAsync(string targetAccountId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetAccountId);
        if (application is null)
        {
            throw new InvalidOperationException("The managed AppHost fixture has not been initialized.");
        }

        var connectionString = await application.GetConnectionStringAsync("platformdb", cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The managed AppHost fixture did not expose the platformdb connection string.");
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE [AccountPreferencesCurrentStates]
            SET [AccountId] = @targetAccountId,
                [ObservedAccountId] = @targetAccountId
            WHERE [PlatformEnvironment] = 'Test' AND [BrokerEnvironment] = 'Demo';
            """;
        command.Parameters.AddWithValue("@targetAccountId", targetAccountId);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task SetAccountPreferencesAccountSwitchAsync(string sessionAccountId, string targetAccountId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionAccountId);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetAccountId);
        if (application is null)
        {
            throw new InvalidOperationException("The managed AppHost fixture has not been initialized.");
        }

        var connectionString = await application.GetConnectionStringAsync("platformdb", cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The managed AppHost fixture did not expose the platformdb connection string.");
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE [IgLoginSnapshots]
            SET [CurrentAccountId] = @sessionAccountId,
                [CapturedAtUtc] = DATEADD(second, 1, SYSUTCDATETIME())
            WHERE [BrokerEnvironment] = 'Demo' AND [SnapshotKind] = 'Latest';
            UPDATE [AccountPreferencesCurrentStates]
            SET [AccountId] = @targetAccountId,
                [ObservedAccountId] = @targetAccountId
            WHERE [PlatformEnvironment] = 'Test' AND [BrokerEnvironment] = 'Demo';
            """;
        command.Parameters.AddWithValue("@sessionAccountId", sessionAccountId);
        command.Parameters.AddWithValue("@targetAccountId", targetAccountId);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task WaitForResourceHealthyAsync(string resourceName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceName);
        return application?.ResourceNotifications.WaitForResourceHealthyAsync(resourceName, cancellationToken)
            ?? throw new InvalidOperationException("The managed AppHost fixture has not been initialized.");
    }

    public async Task DisposeAsync()
    {
        var currentApplication = application;
        var currentBuilder = builder;

        if (currentApplication is null && currentBuilder is null)
        {
            return;
        }

        await AppHostCleanup.DisposeAsync(
            async () =>
            {
                if (currentApplication is not null)
                {
                    await currentApplication.StopAsync().ConfigureAwait(false);
                }
            },
            async () =>
            {
                try
                {
                    if (currentApplication is not null)
                    {
                        await currentApplication.DisposeAsync().ConfigureAwait(false);
                    }
                }
                finally
                {
                    application = null;
                }
            },
            async () =>
            {
                try
                {
                    if (currentBuilder is not null)
                    {
                        await currentBuilder.DisposeAsync().ConfigureAwait(false);
                    }
                }
                finally
                {
                    builder = null;
                }
            }).ConfigureAwait(false);
    }

    private HttpClient CreateHttpClient(string resourceName, string? endpointName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceName);
        if (application is null)
        {
            throw new InvalidOperationException("The managed AppHost fixture has not been initialized.");
        }

        return endpointName is null
            ? application.CreateHttpClient(resourceName)
            : application.CreateHttpClient(resourceName, endpointName);
    }
}