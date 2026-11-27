# Testing one endpoint

Use the in-process Alba host to exercise real routing, middleware, authentication, serialization, validation, and persistence. Confirm the scenario API for the installed version.

The same Alba scenario syntax targets ASP.NET Core Minimal APIs, controller actions, and Wolverine.Http endpoints; use the actual application's routes and contracts. A Minimal API or controller may dispatch or publish through Wolverine without using `WolverineFx.Http`. When verifying that work, wrap the scenario in Wolverine tracking as described in [wolverine-tracking.md](wolverine-tracking.md), without changing the endpoint framework.

## Arrange / Act / Assert

- After cleanup, arrange and commit any required reference data and parent records in the real test database, then use their IDs in the request. See [initial data and seeding](fixture-setup.md#initial-data-and-seeding).
- In `Host.Scenario(...)`, configure the verb and URL (`Get`, `Post.Json`, `Put`, `Delete`, `ToUrl`) and any relevant headers/claims.
- Assert status and relevant headers. For successful responses, deserialize with `ReadAsJsonAsync<T>()` and assert meaningful values.
- Test one behavior per case and use a descriptive business-flow name.

Template: `templates/SingleEndpointTests.cs`.

## Minimum coverage

Cover a successful request, validation failure (400 or 422 per the API contract), and missing resource (404). Add unauthenticated/unauthorized cases when applicable. Do not force a status that contradicts the contract; report discovered differences.

For error statuses, explicitly configure the expected status in Alba so its default success expectation does not fail the intended case.

## Authentication

Use `WithClaim` or a test authentication stub only after verifying the installed authentication behavior. Adding a claim to a scenario is not always equivalent to a production-validated JWT. For JWT-only applications, configure a test scheme in the host or use the documented integration; do not add a production bypass. Validate `tenant_id` through the application's actual helper.

## Mocks with Moq

Replace outbound calls to external APIs, SDK clients, email/payment providers, cloud services, and other external systems with configured Moq test doubles by default. The integration test should exercise the real API, application logic, and owned persistence without making network calls or causing external side effects. Keep the real external integration only when that provider interaction itself is under test, using a controlled test environment and explicit scope.

Use the application's test-host service override hook to remove/replace the production registration before the host starts. Confirm the application resolves the mock, not the live client. The following illustrates a singleton registration; adapt the service lifetime and host API to the installed versions and repository conventions.

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using Xunit;

var expectedResourceId = resource.Id;
var gateway = new Mock<IExternalGateway>(MockBehavior.Strict);
gateway.Setup(x => x.SendAsync(
        It.Is<GatewayRequest>(request => request.ResourceId == expectedResourceId),
        It.IsAny<CancellationToken>()))
    .ReturnsAsync(new GatewayResponse(true)); // TODO: Match the real response type.

// In the Alba host's service-configuration callback, before the host starts:
services.RemoveAll<IExternalGateway>();
services.AddSingleton(gateway.Object);

// Act through the real HTTP pipeline, then verify the external interaction.
gateway.Verify(x => x.SendAsync(
    It.Is<GatewayRequest>(request => request.ResourceId == expectedResourceId),
    It.IsAny<CancellationToken>()), Times.Once);
```

Use `MockBehavior.Strict` when every expected call can be specified; otherwise configure a loose mock deliberately. Verify meaningful arguments and expected call counts (`Times.Once`, `Times.Never`, etc.), not just that some call occurred. For validation/rejection paths, verify that the external service was not called when that is the contract. Verify multiple distinct calls separately when order, arguments, or count matter.

Avoid sharing mutable mock setup/call history across parallel tests. Prefer a per-test host/mock, or reset and reconfigure a collection-scoped mock between serial tests. Do not mock the database, the HTTP pipeline, or internal application behavior that the test is intended to exercise. Add the `Moq` package if missing, and verify the selected Moq version and async signatures before relying on APIs.

## When a dependency cannot be mocked

- First inspect existing interfaces, injectable clients, and test-host overrides. Prefer replacing an existing seam in test configuration without changing production code. Moq can mock interfaces and overridable members of suitable classes; making a class itself `virtual` is not valid C#, and sealed classes or non-virtual members cannot be intercepted this way.
- If an application-owned class must be mockable and no existing seam suffices, propose making only the necessary eligible members `virtual` (and addressing a sealed class only if necessary). Explain the impact; obtain explicit user authorization before editing production code.
- If a third-party SDK's classes or methods cannot be mocked, propose an application-owned interface and thin wrapper around only the SDK calls the application needs. Its production implementation delegates to the SDK; consumers receive the interface via production DI, while the test host replaces it with a Moq mock and verifies relevant calls and arguments. Preserve existing behavior, including errors, cancellation, and authentication. Do not edit vendor code or introduce the wrapper and DI changes without explicit prior authorization.
- For any necessary production change, state the affected files, rationale, alternatives, and behavior impact, then wait for explicit approval. If approval is denied or unavailable, do not modify production or silently call a live external service; complete only unblocked work and report which tests remain blocked.
