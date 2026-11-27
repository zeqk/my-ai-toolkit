# Multitenancy with one database per tenant

Use this only when the application routes each tenant to its own database. If tenants share a database, adapt setup/reset to the actual isolation model.

## One container, multiple databases

- Start one Testcontainers database container with a pinned image for the whole collection.
- Before host startup, create at least two tenant databases (for example `tenant_a` and `tenant_b`) inside that container. Do not use one container per tenant.
- Register test tenants through the production mechanism: configuration, registry table, or another service; point each tenant to its own database.
- If tenant resolution or registration calls an external service, replace that outbound dependency with a configured Moq test double. Keep production tenant-resolution logic and database routing under test, and verify the requested tenant lookup/arguments where relevant.
- Configure a separate main database too if Wolverine/persistence uses one.
- Apply each module's and each `DbContext`'s migrations to every tenant database, respecting migration assemblies and schemas. Fail setup if any migration is missing.
- In CI, create and migrate these databases inside the job's own container. Obtain its connection details dynamically and avoid fixed host ports or a shared pre-provisioned tenant registry. See [CI guidance](fixture-setup.md#running-in-ci).

## Requests and reset

- Send the tenant through the actual resolver (claim/header/route/subdomain) on every request. Implement `ScenarioAsTenant(tenantId, configure)` in the test base class.
- If production uses a header, configure the scenario with `scenario.WithRequestHeader("X-Tenant-Id", tenantId)` (replace with the actual header name).
- Never rely on an implicit default tenant. Assert the application's defined rejection for missing or unknown tenants.
- Implement provider-specific cleanup for each tenant database. Include module schemas and explicitly decide how to handle Wolverine inbox/outbox/envelope tables. Reset each tenant independently.
- After reset, restore required baseline data and seed scenario-specific prerequisites into the explicitly selected tenant database. Do not copy tenant A's scenario data into tenant B unless the test requires it; verify isolation using the independently seeded state. See [initial data and seeding](fixture-setup.md#initial-data-and-seeding).

## Wolverine and isolation

If outbox/inbox is persisted, verify the tenant on the tracked envelope through `TenantId` if that API exists in the installed version. Also verify an observable effect in the correct tenant's storage.

Required tests:

1. Data created by tenant A is not visible to tenant B (404 or an empty result, according to the contract).
2. Requests with missing and unknown tenants return the application's defined rejection.
3. A message emitted as tenant A is not processed using tenant B's database/context; verify the envelope tenant and a tenant-scoped effect.

The claim helper in the template is illustrative. If production uses a header or another mechanism, adapt it to match.
