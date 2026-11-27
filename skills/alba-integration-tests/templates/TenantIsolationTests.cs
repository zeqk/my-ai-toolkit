using Alba;
using System.Security.Claims;
using Wolverine.Tracking;
using Xunit;

namespace IntegrationTests;

[Collection("multi-tenant-integration")]
public sealed class TenantIsolationTests(MultiTenantAppFixture fixture) : IAsyncLifetime
{
    private const string TenantA = "tenant_a";
    private const string TenantB = "tenant_b";

    public async ValueTask InitializeAsync()
    {
        await fixture.ResetTenantDatabaseAsync(TenantA);
        await fixture.ResetTenantDatabaseAsync(TenantB);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Resource_created_by_tenant_a_is_not_visible_to_tenant_b()
    {
        var created = await ScenarioAsTenant(TenantA, scenario =>
        {
            scenario.Post.Json(new TenantCreateResourceRequest("Tenant A data")).ToUrl("/resources");
            scenario.StatusCodeShouldBe(201);
        });

        var resource = await created.ReadAsJsonAsync<TenantResourceResponse>();
        Assert.NotNull(resource);

        await ScenarioAsTenant(TenantB, scenario =>
        {
            scenario.Get.Url($"/resources/{resource.Id}");
            scenario.StatusCodeShouldBe(404); // TODO: Match the isolation contract.
        });
    }

    [Theory]
    [InlineData(null)]
    [InlineData("unknown_tenant")]
    public async Task Missing_or_unknown_tenant_is_rejected(string? tenantId)
    {
        await fixture.Host.Scenario(scenario =>
        {
            if (tenantId is not null)
            {
                scenario.WithClaim(new Claim("tenant_id", tenantId));
            }

            scenario.Get.Url("/resources"); // TODO: Use a tenant-protected route.
            scenario.StatusCodeShouldBe(401); // TODO: Match the application's status.
        });
    }

    [Fact]
    public async Task Message_emitted_by_tenant_a_keeps_tenant_a_context()
    {
        var tracking = await fixture.Host.Services.ExecuteAndWaitAsync(async () =>
        {
            await ScenarioAsTenant(TenantA, scenario =>
            {
                scenario.Post.Json(new TenantCreateResourceRequest("Tenant A message"))
                    .ToUrl("/resources");
                scenario.StatusCodeShouldBe(201);
            });
        });

        // TODO: Verify SingleEnvelope<T>() and Envelope.TenantId in the installed version.
        Assert.Equal(
            TenantA,
            tracking.Sent.SingleEnvelope<TenantResourceCreated>().TenantId);
        await AssertMessageWasNotProcessedByTenantAsync(TenantB);
    }

    private Task<IScenarioResult> ScenarioAsTenant(string tenantId, Action<Scenario> configure)
    {
        return fixture.Host.Scenario(scenario =>
        {
            scenario.WithClaim(new Claim("tenant_id", tenantId));
            configure(scenario);
        });
    }

    private Task AssertMessageWasNotProcessedByTenantAsync(string tenantId)
    {
        // TODO: Assert an observable tenant-scoped effect in the application's storage.
        throw new NotImplementedException(
            $"Implement the tenant-scoped message assertion for {tenantId}.");
    }
}

public sealed record TenantResourceCreated(string TenantId, string Name);
public sealed record TenantCreateResourceRequest(string Name);
public sealed record TenantResourceResponse(Guid Id, string Name);
