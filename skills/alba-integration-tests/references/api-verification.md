# Verify APIs against installed versions

Do this before writing tests or copying templates. Do not infer versions from online examples, current documentation, or another project.

## Find the resolved versions

1. Inspect the test/API `.csproj` files and the nearest `Directory.Packages.props`.
2. Check resolved versions with `dotnet list <test-project.csproj> package --include-transitive` or `obj/project.assets.json`. A versionless package reference does not establish the resolved version.
3. Check each applicable package separately: `WolverineFx`, `WolverineFx.Http` only if present/used, `WolverineFx.EntityFrameworkCore`, persistence/transport packages, and test packages. Confirm which installed package supplies tracking. Do not add `WolverineFx.Http` just because ASP.NET Core endpoints use Wolverine messaging.
4. Record database provider, Testcontainers, xUnit, and Moq versions too. If a package is absent, mark it absent rather than validated.

## Confirm symbols and behavior

Use the exact restored assemblies and versioned XML docs. Search package XML docs in the NuGet cache, inspect public members with reflection/an assembly browser, or compile a minimal snippet. For extension methods and overloads, confirm the namespace, argument types, return type, and execution timing—not just the method name.

Check these as applicable:

| Package | APIs / behavior |
|---|---|
| Alba | `AlbaHost.For<T>`, configuration overrides, `Scenario`, `ReadAsJsonAsync<T>`, `WithRequestHeader`, `WithClaim`, and their effect on authentication |
| Wolverine | `DisableAllExternalWolverineTransports`, tracking entry point (`TrackedHttpCall` if provided by a helper, otherwise `ExecuteAndWaitAsync`), `Sent`, `Received`, `Executed`, `NoRoutes`, `SingleMessage<T>` / `MessagesOf<T>`, envelope access, and `TenantId` |
| Testcontainers | Database-specific builder/container, image configuration, connection-string accessor, lifecycle, and major-version compatibility |
| Moq | Mock setup/verification APIs and async callback signatures for the selected version |
| xUnit | Async lifecycle signatures and assertion APIs for the selected version |

Then build the test project and run the relevant filter. Fix incompatibilities and explain any API substitutions to the user.

## Do not overstate what a test proves

`DisableAllExternalWolverineTransports()` avoids connecting to external brokers. Depending on the transport and version, messages directed externally may be dropped; tracking does not prove broker delivery. Use a broker-backed test when broker behavior is under test.

With EF Core and modular persistence, verify migrations/schema for every context. A successful host startup does not prove the correct database or tenant was initialized.

## Official references

- [Alba: Getting started](https://jasperfx.github.io/alba/guide/gettingstarted)
- [Alba: writing scenarios](https://jasperfx.github.io/alba/guide/scenarios/writingscenarios)
- [Wolverine: integration testing HTTP](https://wolverinefx.io/guide/http/integration-testing.html)
- [Testcontainers for .NET](https://dotnet.testcontainers.org/)

Prefer documentation/source matching the installed tag. The default branch may describe a newer, incompatible major version.

For example, WolverineFx `6.30.0` XML docs expose `ExecuteAndWaitAsync` as extensions on `IHost` and `IServiceProvider`; the template helper uses `Host.Services` rather than assuming `IAlbaHost` implements `IHost`. Testcontainers `4.17.0` marks the parameterless builder obsolete; pass a pinned image to the builder constructor. The templates were isolated-build checked with Alba `9.0.0`, Testcontainers.PostgreSql `4.17.0`, WolverineFx `6.30.0`, and xUnit v3 `3.0.1`.
