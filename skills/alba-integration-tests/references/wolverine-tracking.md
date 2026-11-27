# Verifying Wolverine messages

Confirm the installed tracking API as described in [api-verification.md](api-verification.md). An ASP.NET Core Minimal API or controller can use Wolverine for messaging without `WolverineFx.Http`. Do not add that package or convert endpoints to Wolverine.Http to enable tracking; use the tracking API provided by the installed Wolverine packages.

## Wait for work; never sleep

Run the HTTP request inside `ExecuteAndWaitAsync` or an application helper such as `TrackedHttpCall`. Return both the Alba response and tracking session when needed. Do not use `Thread.Sleep`, polling delays, or arbitrary timeouts.

For example, the `TrackedHttpCall` helper in `templates/IntegrationTestBase.cs` wraps Alba's `Host.Scenario` with `Host.Services.ExecuteAndWaitAsync`. The same helper and `templates/WolverineMessagesTests.cs` apply when the actual route is a Minimal API or controller action that invokes or publishes a Wolverine message; adapt the route, contract, and assertions to the application. No `WolverineFx.Http` API is used by this helper.

`ExecuteAndWaitAsync` waits for observed work started within the action. Ensure the request starts the expected work; an endpoint that only enqueues work or uses another host may require wait conditions/multi-host tracking for the installed version. Avoid tests that pass vacuously without observing activity.

## Assertions

- Choose the collection that matches the behavior: sent/published, received, executed, `NoRoutes`, and (when applicable) failed/moved-to-error messages.
- Assert message type and relevant payload. Use `SingleMessage<T>()` for one and `MessagesOf<T>()` for multiple, after confirming installed semantics and return types.
- For a rejected/failed flow, assert the response and that no message was emitted.
- For tenant-specific persistence, inspect the envelope and `TenantId` if available in that version.
- Do not suppress tracking exceptions to make a test pass.

If the message is routed to a local handler, the test can verify the full cycle:

```csharp
using Xunit;

Assert.Equal("Example", tracking.Sent.SingleMessage<ResourceCreated>().Name);
Assert.Equal("Example", tracking.Received.SingleMessage<ResourceCreated>().Name);
Assert.Equal("Example", tracking.Executed.SingleMessage<ResourceCreated>().Name);
Assert.Empty(tracking.NoRoutes.MessagesOf<ResourceCreated>());
Assert.Empty(tracking.MovedToErrorQueue.MessagesOf<ResourceCreated>());
```

Do not assert `Received`/`Executed` for messages only published to an external broker. Test `MovedToErrorQueue` in a controlled failure case whose policy exhausts retries; do not trigger arbitrary errors in a success test.

See `templates/WolverineMessagesTests.cs`.

## Transport boundary

With `DisableAllExternalWolverineTransports()`, the test does not connect to or deliver messages to a real broker. It can verify captured emission and local handling/routing according to the test host. A tracked external send does not prove broker receipt. Use a broker-backed test when delivery, serialization, topology, or broker-specific retries are in scope.
