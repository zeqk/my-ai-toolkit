using Xunit;

namespace IntegrationTests;

[Collection("integration")]
public sealed class SingleEndpointTests(AppFixture fixture) : IntegrationTestBase(fixture)
{
    [Fact]
    public async Task Create_resource_returns_created_resource()
    {
        var request = new SingleCreateResourceRequest("Example");

        var response = await Host.Scenario(scenario =>
        {
            scenario.Post.Json(request).ToUrl("/resources"); // TODO: Use the actual route.
            scenario.StatusCodeShouldBe(201);
        });

        var body = await response.ReadAsJsonAsync<SingleResourceResponse>();
        Assert.NotNull(body);
        Assert.Equal("Example", body.Name);
    }

    [Fact]
    public async Task Create_resource_with_invalid_input_returns_validation_error()
    {
        var response = await Host.Scenario(scenario =>
        {
            scenario.Post.Json(new SingleCreateResourceRequest("")).ToUrl("/resources");
            scenario.StatusCodeShouldBe(400); // TODO: Match the API contract (400 or 422).
        });

        Assert.Equal(400, response.Context.Response.StatusCode);
    }

    [Fact]
    public async Task Get_missing_resource_returns_not_found()
    {
        await Host.Scenario(scenario =>
        {
            scenario.Get.Url("/resources/00000000-0000-0000-0000-000000000001");
            scenario.StatusCodeShouldBe(404);
        });
    }

    // TODO: Add authorized/unauthorized cases if this endpoint requires authentication.
}

public sealed record SingleCreateResourceRequest(string Name);
public sealed record SingleResourceResponse(Guid Id, string Name);
