using Alba;
using System.Security.Claims;
using Wolverine.Tracking;
using Xunit;

namespace IntegrationTests;

[Collection("integration")]
public abstract class IntegrationTestBase(AppFixture fixture) : IAsyncLifetime
{
    protected IAlbaHost Host => fixture.Host;

    public async ValueTask InitializeAsync()
    {
        await fixture.ResetDatabaseAsync();
        // TODO: Restore any required shared baseline removed by reset. Seed and commit
        // scenario-specific prerequisites in each test after this initialization.
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    protected Task<IScenarioResult> ScenarioAsTenant(
        string tenantId,
        Action<Scenario> configure)
    {
        return Host.Scenario(scenario =>
        {
            // Replace this claim with the application's actual tenant-resolution mechanism.
            scenario.WithClaim(new Claim("tenant_id", tenantId));
            configure(scenario);
        });
    }

    protected async Task<(ITrackedSession Tracking, IScenarioResult Response)> TrackedHttpCall(
        Action<Scenario> configure,
        int timeoutInMilliseconds = 5000)
    {
        IScenarioResult response = null!;
        var tracking = await Host.Services.ExecuteAndWaitAsync(async () =>
        {
            response = await Host.Scenario(configure);
        }, timeoutInMilliseconds);

        return (tracking, response);
    }
}
