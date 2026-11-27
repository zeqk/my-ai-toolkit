# Chained HTTP scenarios

Use one test for a coherent business workflow; do not share state across tests or depend on test order.

Workflow shape:

1. `POST /resources` with JSON; assert status and deserialize a typed response or capture `Location`.
2. Extract the ID/URL.
3. `GET /resources/{id}` and assert the created representation.
4. `PUT /resources/{id}` and assert the update result.
5. `GET /resources/{id}` again and assert persisted state.

Use routes and types discovered in the target application. Assert behavior, not only 2xx statuses. Each step remains a real HTTP request; if it triggers async Wolverine work, wrap the relevant request in tracking before asserting dependent state.

In multitenant applications, send the same tenant explicitly on every step using the production mechanism (claim/header/host/route). Never rely on a default. See `templates/ChainedEndpointsTests.cs`.
