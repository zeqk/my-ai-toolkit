---
name: alba-integration-tests
description: Use when adding .NET integration, end-to-end, or API tests, or when Alba, WebApplicationFactory, Testcontainers, multitenancy, or Wolverine message verification is mentioned. Do not use for unit tests or UI tests.
---

# .NET integration tests with Alba

Follow the target project's conventions. Treat references and templates as examples, not guarantees that their APIs match the installed versions.

## 0. Verify installed APIs before writing tests

API names in this skill and its templates are references only. Alba and Wolverine have changed APIs between major versions.

1. Read the target framework and resolved package versions from the test/API `.csproj`, `Directory.Packages.props`, and, if needed, `obj/project.assets.json`. Check `Alba`, applicable `WolverineFx*` packages (such as `WolverineFx.Http` only if used and `WolverineFx.EntityFrameworkCore`), `Testcontainers.*`, `Moq`, and xUnit.
2. Before writing tests, verify each API against those exact versions using IntelliSense, package XML docs, assembly inspection, or a minimal compile snippet. At minimum, check:
   - Alba: `AlbaHost.For<T>`, `Scenario`, `ReadAsJsonAsync<T>`, `WithRequestHeader`, `WithClaim`.
   - Wolverine: `DisableAllExternalWolverineTransports`, `TrackedHttpCall` (if present in the support layer), `ExecuteAndWaitAsync`, `Sent`, `Received`, `Executed`, `NoRoutes`, `SingleMessage<T>`, `MessagesOf<T>`, envelope access, and `TenantId`.
3. Never invent methods. If an API does not exist, find the equivalent for the installed version or consult the version-matched JasperFx documentation; tell the user what differs.
4. Build generated tests with `dotnet build` and fix compile errors before delivery.

### Versions this skill was compiled against

These are smoke-test versions for the templates, not required package versions. Re-verify against each target project.

| Component | Version |
|---|---|
| .NET | `net10.0` |
| Alba | `9.0.0` |
| WolverineFx | `6.30.0` |
| Testcontainers.PostgreSql | `4.17.0` |
| xUnit v3 | `3.0.1` |
| Moq | Not included in the smoke-test build; verify the selected version before use |

The templates use xUnit v3 lifecycle signatures and xUnit assertions; adapt lifecycle signatures if the target project uses another xUnit version. The isolated compile validates template compilation only, not database setup, migrations, or Docker execution. See [api-verification.md](references/api-verification.md) for verification steps and official links.

## 1. Discover the target before writing tests

- Locate the API entry project and confirm `Program` is accessible from tests. Prefer an existing test-host entry point; adding `public partial class Program { }` requires prior explicit user authorization.
- Identify the database engine, ORM, endpoint style (ASP.NET Core Minimal APIs, controllers, or Wolverine.Http), authentication, and existing test/assertion conventions. Detect Wolverine messaging separately from the endpoint framework.
- Identify required reference data and endpoint prerequisites, their owning databases/modules, and existing test seed helpers.
- Reuse an existing integration-test project and fixture; do not duplicate infrastructure.
- Inspect the existing CI test job, .NET SDK, test runner, and Docker access. Ensure generated tests are included in the CI test command without requiring local developer configuration.
- Detect tenant resolution (claim/header/route/host), tenant-specific database configuration, and Wolverine tenant detection. For one database per tenant, follow [multitenancy.md](references/multitenancy.md).
- For a modular monolith, identify every `DbContext`, migration assembly, and schema. Migrate and reset each module.
- Add only missing dependencies: `Alba`, `Testcontainers.<Database>`, xUnit, and `Moq` when mocks are needed. Do not add `WolverineFx.Http` merely to test Minimal APIs/controllers or track Wolverine messages; add Wolverine packages only when actually required.

## 2. Fixture and isolation

Use one async fixture and one pinned-image Testcontainers database per xUnit collection, not one container per test. Configure the real API host with the container connection string, disable external Wolverine transports, and apply each module schema once. Dispose the host and container asynchronously.

Expose the fixture with `[CollectionDefinition]` + `ICollectionFixture<T>`. A test base class resets state between tests with provider-specific cleanup (for example, a test-only `TRUNCATE`/delete routine), including relevant schemas and an explicit policy for persisted inbox/outbox data. Tests must be repeatable and independent.

After reset, restore any required shared baseline; arrange each test's additional prerequisites before the request using the real test database or an appropriate API. Persist and commit them before acting. Seed explicitly per tenant when applicable; never depend on another test or production data. See [fixture-setup.md](references/fixture-setup.md#initial-data-and-seeding).

See [fixture-setup.md](references/fixture-setup.md), [AppFixture.cs](templates/AppFixture.cs), and [IntegrationTestBase.cs](templates/IntegrationTestBase.cs).

## CI execution

Generated tests must run unattended both locally and in CI. Use a runner with the matching .NET SDK and a working Docker-compatible daemon; let Testcontainers provision its own database with dynamically allocated ports. Do not rely on fixed host ports, a manually started database, production credentials, or live external services.

Integrate the test project into the existing CI test job and fail the job on test or fixture failure. Validate with the same restore/build/test commands and environment as CI when available; if CI or Docker is unavailable, report the limitation rather than claiming CI compatibility. See [fixture-setup.md](references/fixture-setup.md#running-in-ci).

## 3. Test patterns

- Use Arrange / Act / Assert, one behavior per test, descriptive English names, and the target project's conventions.
- Use Alba scenarios against the real in-process HTTP pipeline; assert status, relevant headers, and typed response content.
- Cover success, validation failure (400/422), missing resource (404), and applicable authentication/authorization behavior.
- For a workflow, use one test that extracts typed data, an ID, or `Location` from each response and passes it to the next request. Do not share state across tests or depend on execution order.
- In multitenant flows, send the same tenant explicitly on every step.
- Replace outbound calls to external APIs, SDKs, email/payment providers, and other external services with configured Moq test doubles by default. Verify relevant calls and arguments; keep the API pipeline and owned persistence real. See [endpoint-tests.md](references/endpoint-tests.md).

See [endpoint-tests.md](references/endpoint-tests.md), [chained-scenarios.md](references/chained-scenarios.md), and their templates.

## 4. Wolverine tracking

When Wolverine is used, wrap the HTTP request in the installed tracking API (`TrackedHttpCall` or `ExecuteAndWaitAsync` around `Scenario`) whenever it triggers Wolverine work. This applies equally to Minimal APIs, controllers, and Wolverine.Http endpoints. Assert message type and relevant content, and include a negative case where an invalid/failed request emits no message.

Never use `Thread.Sleep` or arbitrary delays. Disabling external transports allows assertions about tracked emission/routing/handling, but **does not** prove delivery to a real broker. Read [wolverine-tracking.md](references/wolverine-tracking.md).

## 5. Authentication and multitenancy

Use Alba claims/headers or a test authentication scheme according to installed APIs and the application's actual mechanism. Do not weaken production auth. Do not infer the tenant from request data when production derives it from an authenticated claim.

For one database per tenant, use the two-database fixture and mandatory isolation cases in [multitenancy.md](references/multitenancy.md) and [TenantIsolationTests.cs](templates/TenantIsolationTests.cs). Never rely on an implicit default tenant.

## General rules and final checklist

- Avoid modifying production source or configuration. If a change is necessary, describe the exact change, reason, alternatives and impact, then obtain explicit user authorization **before** editing; silence is not consent. This includes exposing `Program`, making members virtual, adding wrappers, or changing production DI. If authorization is denied or unavailable, leave production untouched and report the blocked tests. See [endpoint-tests.md](references/endpoint-tests.md#when-a-dependency-cannot-be-mocked).
- Follow the target project's conventions without overriding the authorization requirement; keep approved changes minimal and report them.
- Base tests on xUnit. Use xUnit `Assert` and Alba scenario assertions; do not add a separate assertion library. Use Moq to stub/mock outbound external dependencies, and verify interactions when they are part of the behavior; do not replace the real database or HTTP pipeline under test.
- Implement database cleanup with provider-specific commands or the application's existing cleanup mechanism.
- Tests require the appropriate .NET SDK and a Docker-compatible daemon, but no other locally installed services.
- Before delivery:
  - [ ] APIs were checked against installed versions and the test project builds.
  - [ ] No production changes were made without explicit prior user authorization.
  - [ ] The shared fixture uses one container per collection.
  - [ ] External Wolverine transports are disabled.
  - [ ] State is reset between tests (per tenant when applicable).
  - [ ] Required initial data is seeded after reset, committed, and isolated for each test and tenant.
  - [ ] Success and at least one error case are covered.
  - [ ] Outbound external services are replaced with Moq test doubles; relevant calls/arguments (and required no-call cases) are verified.
  - [ ] Message assertions use tracking, without manual waits.
  - [ ] Multitenant tests send the tenant explicitly and verify isolation.
  - [ ] `dotnet test` passes.
  - [ ] CI includes the test project and provides the required SDK and Docker; the CI-equivalent run passes, or any verification limitation is reported.
