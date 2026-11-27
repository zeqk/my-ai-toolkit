using Alba;
using JasperFx.CommandLine;
using Microsoft.AspNetCore.Hosting;
using Testcontainers.PostgreSql;
using Wolverine;
using Xunit;

namespace IntegrationTests;

public sealed class AppFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder("postgres:18.3")
        .WithDatabase("integration_tests")
        .WithUsername("postgres")
        .WithPassword("integration_tests")
        .Build();

    public IAlbaHost Host { get; private set; } = null!;
    public string ConnectionString => _database.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        await _database.StartAsync();
        JasperFxEnvironment.AutoStartHost = true;

        var hostBuilder = AlbaHost.For<global::Program>(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.DisableAllExternalWolverineTransports();
                // TODO: Replace outbound external-service registrations with Moq test doubles.
            });
        });

        // TODO: Replace/add the actual application connection-string keys.
        // Apply configuration overrides before starting the host.
        hostBuilder = hostBuilder.WithConfiguration(
            "ConnectionStrings:TestDatabase",
            ConnectionString);
        Host = await hostBuilder;

        // TODO: Apply each module DbContext migration once.
        await ApplyModuleMigrationsAsync();
    }

    private static Task ApplyModuleMigrationsAsync()
    {
        throw new NotImplementedException("Configure the application's DbContext migrations.");
    }

    public Task ResetDatabaseAsync()
    {
        // TODO: Implement provider-specific cleanup after migrations, including all
        // module schemas and an explicit policy for Wolverine inbox/outbox tables.
        throw new NotImplementedException("Configure the application's database reset.");
    }

    public async ValueTask DisposeAsync()
    {
        if (Host is not null)
        {
            await Host.StopAsync();
            await Host.DisposeAsync();
        }

        await _database.DisposeAsync();
    }
}

[CollectionDefinition("integration")]
public sealed class IntegrationTestCollection : ICollectionFixture<AppFixture>;
