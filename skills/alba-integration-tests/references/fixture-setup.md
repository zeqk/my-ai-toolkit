# Shared fixture: Alba, Testcontainers, and Wolverine

Adapt `templates/AppFixture.cs`; it is not plug-and-play because connection keys, contexts, migrations, and schemas are application-specific.

Alba hosts the application's existing ASP.NET Core pipeline regardless of whether endpoints are Minimal APIs, controllers, or Wolverine.Http. Configure and test the existing host as-is: an app can use Wolverine for messaging without using `WolverineFx.Http`, and the fixture must not add or enable that HTTP integration.

## Lifecycle

1. Use a fixed-version database image and one container per xUnit collection. Never use `latest`. Start it before the host; a Docker-compatible daemon is the only external service prerequisite.
2. Override the application's actual configuration keys with the container connection string before startup. Alba configuration APIs vary; verify the installed version.
3. In host service configuration, call the installed `DisableAllExternalWolverineTransports()` API. Local/in-memory handling remains according to configuration; broker delivery is out of scope.
4. If durable outbox/inbox is used, choose test durability deliberately. Solo mode can speed startup but alter behavior; do not disable durability when it is under test.
5. Apply migrations once during fixture initialization. In modular monoliths, enumerate each module's `DbContext`, migration assembly, and schema; prevent requests from racing schema setup.
6. Dispose the Alba host before stopping/disposing the container. Handle partially initialized resources.
7. Expose the fixture with `[CollectionDefinition]` and `ICollectionFixture<T>`. Put test classes in that collection.
8. Reset data, not schemas, between tests. Implement provider-specific cleanup per database/schema and define what happens to persisted Wolverine inbox, outbox, and envelope data.
9. Replace production registrations for outbound external services with Moq-backed test doubles before the host starts. Do not let tests call live APIs or trigger external side effects by default.

## Configuration and migrations

- Use the application's actual configuration, not an unrelated fake `DbContext`. If Wolverine uses a main database in addition to tenant databases, configure both.
- Prefer the real provider in Testcontainers. Do not replace production registrations without a test-specific reason.
- If the host provisions schemas at startup, confirm that setup completes before exposing the fixture; otherwise create scopes and migrate every context.
- Fail fixture initialization clearly if Docker, configuration, or migrations fail; do not silently skip database behavior.
- Confirm each external service interface resolves to its configured test double. Keep mock state isolated per test, or reset/reconfigure a shared mock only when tests using it cannot run concurrently.

`IntegrationTestBase` resets state before each test and restores a known baseline if required. Implement cleanup with the database provider or the application's existing test helper, including every relevant business schema. Explicitly decide whether Wolverine inbox/outbox tables are reset so stale messages cannot leak into later tests.

For one database per tenant, see [multitenancy.md](multitenancy.md).

## Initial data and seeding

- Discover the endpoint's prerequisites: reference/catalog rows, parent entities, foreign keys, tenant records, and any required configuration. Reuse the repository's existing seed helpers or factories; provide only the data the behavior needs, with deterministic test-owned values and valid relationships.
- Apply migrations before seeding. After every test reset, restore any baseline needed by all tests, including migration-owned reference rows if cleanup removes them. Alternatively preserve immutable reference data only when tests cannot mutate it; otherwise reset and restore it. Do not assume one-time fixture seeding survives per-test cleanup.
- Arrange scenario-specific records in the test after reset and before the request. Seed the real test database through the project's ORM, repository, provider commands, or a setup API as appropriate. A setup API may exercise extra behavior or emit messages; choose it deliberately and exclude setup side effects from the action's assertions.
- Save and commit seed data before invoking the endpoint, then dispose the seeding scope/connection so the host can read it. Do not rely on an uncommitted transaction in a separate context. Reuse generated IDs from the seed helper in the request and assertions.
- For database-per-tenant applications, write each baseline and scenario record to the explicitly selected tenant database; seed other tenants only when the scenario requires it. Coordinate cleanup and parallelism so no other test can mutate or remove the data mid-test.
- Never copy production data or require manually seeded infrastructure. Use the same deterministic setup locally and in CI. Fail explicitly if a seed cannot be created; do not mask missing prerequisites with mocks or silent defaults.

Illustrative EF Core pattern; replace the types, fields, and context resolution with the application's actual ones:

```csharp
// Arrange, after the test reset and migrations:
await using (var scope = Host.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); // TODO: Select the correct module/tenant context.
    db.Categories.Add(new Category { Id = categoryId, Name = "Test category" }); // TODO: Use valid project entities.
    await db.SaveChangesAsync();
}

// Act through Alba using categoryId, then assert the endpoint's result.
```

Import `Microsoft.Extensions.DependencyInjection` and ensure the resolved context targets the intended test database. If an explicit transaction is used, commit it before the request. Adapt the pattern for non-EF providers.

## Running in CI

- Discover the existing pipeline, SDK version, test platform, and test command. Include the integration-test project in the existing test job; do not create a duplicate pipeline or skip the tests in CI. Use `dotnet test <path-to-integration-test-project>` as a starting point, adapting arguments to the installed test platform and existing build configuration.
- Use a runner that can reach a working Docker-compatible daemon, pull the pinned database image and Testcontainers' required helper images, and expose container ports to the test process. Confirm image architecture and registry access. Containerized runners may need explicit daemon and network configuration; do not assume `localhost` or a Docker socket is shared with the host.
- Obtain connection strings and mapped ports from Testcontainers at runtime; do not hardcode host ports, machine paths, local services, or fixed container names. Keep concurrent jobs/collections isolated, and prevent parallel tests sharing mutable fixture state from resetting each other's data.
- Configure the host from test-owned settings and Moq doubles, not developer profiles, production secrets, or live services. Use CI secret storage only when a controlled external dependency is explicitly in scope; never print credentials in logs.
- Apply all migrations, seed only required data, and perform per-test cleanup in CI exactly as locally. Use readiness checks and bounded timeouts rather than arbitrary delays. Dispose resources even on failure; retain Testcontainers' supported resource cleanup mechanism rather than disabling it to mask runner problems.
- Make restore, build, and test failures fail the job. Preserve test results and actionable container/host diagnostics on failure without leaking secrets; adapt result reporting to the project's test platform.
- Run the same project and configuration in a Docker-enabled CI runner when available. If only a local CI-equivalent run is possible, say so; if Docker, registry access, or CI execution is unavailable, report the blocker instead of skipping tests or claiming success.
