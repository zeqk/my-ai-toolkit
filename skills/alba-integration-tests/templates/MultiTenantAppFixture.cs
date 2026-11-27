using Alba;
using JasperFx.CommandLine;
using Microsoft.AspNetCore.Hosting;
using Npgsql;
using Testcontainers.PostgreSql;
using Wolverine;
using Wolverine.Tracking;
using Xunit;

namespace IntegrationTests;

public sealed class MultiTenantAppFixture : IAsyncLifetime
{
    private static readonly string[] TenantIds = ["tenant_a", "tenant_b"];
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18.3")
        .WithDatabase("postgres")
        .WithUsername("postgres")
        .WithPassword("integration_tests")
        .Build();

    public IAlbaHost Host { get; private set; } = null!;
    public IReadOnlyList<string> TestTenantIds => TenantIds;

    public string ConnectionStringFor(string tenantId) =>
        new NpgsqlConnectionStringBuilder(_postgres.GetConnectionString())
        {
            Database = tenantId
        }.ConnectionString;

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync();
        await CreateTenantDatabasesAsync();
        JasperFxEnvironment.AutoStartHost = true;

        var hostBuilder = AlbaHost.For<global::Program>(builder =>
        {
            builder.ConfigureServices(services =>
                services.DisableAllExternalWolverineTransports());
        });

        // TODO: Register tenants through the production mechanism. These configuration
        // keys are examples; the application may use a tenant registry/table instead.
        foreach (var tenantId in TenantIds)
        {
            hostBuilder = hostBuilder.WithConfiguration(
                $"Multitenancy:Tenants:{tenantId}:ConnectionString",
                ConnectionStringFor(tenantId));
        }

        // TODO: Configure any separate main database connection before host startup.
        Host = await hostBuilder;

        // TODO: Migrate each module DbContext in both tenant databases and create an
        // provider-specific cleanup per tenant after migrations.
        await ApplyTenantMigrationsAsync();
    }

    private static Task ApplyTenantMigrationsAsync()
    {
        throw new NotImplementedException(
            "Configure module migrations for each test tenant.");
    }

    private async Task CreateTenantDatabasesAsync()
    {
        await using var connection = new NpgsqlConnection(_postgres.GetConnectionString());
        await connection.OpenAsync();

        foreach (var tenantId in TenantIds)
        {
            // TenantIds are fixed test constants, not user input.
            await using var command = new NpgsqlCommand(
                $"CREATE DATABASE \"{tenantId}\"",
                connection);
            await command.ExecuteNonQueryAsync();
        }
    }

    public Task ResetTenantDatabaseAsync(string tenantId)
    {
        if (!TenantIds.Contains(tenantId, StringComparer.Ordinal))
        {
            throw new ArgumentOutOfRangeException(nameof(tenantId), tenantId, "Unknown test tenant.");
        }

        // TODO: Reset only this tenant database using provider-specific cleanup.
        throw new NotImplementedException("Configure the tenant-specific database reset.");
    }

    public async ValueTask DisposeAsync()
    {
        if (Host is not null)
        {
            await Host.StopAsync();
            await Host.DisposeAsync();
        }

        await _postgres.DisposeAsync();
    }
}

[CollectionDefinition("multi-tenant-integration")]
public sealed class MultiTenantIntegrationTestCollection : ICollectionFixture<MultiTenantAppFixture>;
