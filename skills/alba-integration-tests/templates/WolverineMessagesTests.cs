using Xunit;

namespace IntegrationTests;

[Collection("integration")]
public sealed class WolverineMessagesTests(AppFixture fixture) : IntegrationTestBase(fixture)
{
    [Fact]
    public async Task Successful_request_emits_expected_message()
    {
        var (tracking, response) = await TrackedHttpCall(scenario =>
        {
            scenario.Post.Json(new WolverineCreateResourceRequest("Example")).ToUrl("/resources");
            scenario.StatusCodeShouldBe(201);
        });

        Assert.Equal(201, response.Context.Response.StatusCode);
        Assert.Equal("Example", tracking.Sent.SingleMessage<ResourceCreated>().Name);
        // TODO: Assert Received, Executed, NoRoutes, or error collections as needed,
        // after verifying their semantics in the installed Wolverine version.
    }

    [Fact]
    public async Task Invalid_request_does_not_emit_resource_created_message()
    {
        var (tracking, response) = await TrackedHttpCall(scenario =>
        {
            scenario.Post.Json(new WolverineCreateResourceRequest("")).ToUrl("/resources");
            scenario.StatusCodeShouldBe(400); // TODO: Match the API contract.
        });

        Assert.Equal(400, response.Context.Response.StatusCode);
        Assert.Empty(tracking.Sent.MessagesOf<ResourceCreated>());
    }
}

public sealed record WolverineCreateResourceRequest(string Name);
public sealed record ResourceCreated(string Name);
