using Xunit;

namespace IntegrationTests;

[Collection("integration")]
public sealed class ChainedEndpointsTests(AppFixture fixture) : IntegrationTestBase(fixture)
{
    [Fact]
    public async Task Create_update_and_read_resource_completes_business_flow()
    {
        const string tenantId = "tenant-test";

        var createdResponse = await ScenarioAsTenant(tenantId, scenario =>
        {
            scenario.Post.Json(new ChainedCreateResourceRequest("Before")).ToUrl("/resources");
            scenario.StatusCodeShouldBe(201);
        });

        var created = await createdResponse.ReadAsJsonAsync<ChainedResourceResponse>();
        Assert.NotNull(created);
        var id = created.Id;

        await ScenarioAsTenant(tenantId, scenario =>
        {
            scenario.Get.Url($"/resources/{id}");
            scenario.StatusCodeShouldBe(200);
        });

        await ScenarioAsTenant(tenantId, scenario =>
        {
            scenario.Put.Json(new ChainedUpdateResourceRequest("After")).ToUrl($"/resources/{id}");
            scenario.StatusCodeShouldBe(204); // TODO: Match the endpoint contract.
        });

        var verifiedResponse = await ScenarioAsTenant(tenantId, scenario =>
        {
            scenario.Get.Url($"/resources/{id}");
            scenario.StatusCodeShouldBe(200);
        });

        var verified = await verifiedResponse.ReadAsJsonAsync<ChainedResourceResponse>();
        Assert.NotNull(verified);
        Assert.Equal("After", verified.Name);
    }
}

public sealed record ChainedCreateResourceRequest(string Name);
public sealed record ChainedUpdateResourceRequest(string Name);
public sealed record ChainedResourceResponse(Guid Id, string Name);
