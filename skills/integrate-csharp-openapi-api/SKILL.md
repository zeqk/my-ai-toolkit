---
name: integrate-csharp-openapi-api
description: "Integrate an external HTTP API into a C# or .NET project from an OpenAPI definition using NSwag.MSBuild. Use when generating a typed C# client, adding an .nswag configuration, wiring a generated client into dependency injection, configuring API authentication, or updating a client after an OpenAPI contract changes."
argument-hint: "OpenAPI definition location, target project, API name, authentication requirements"
---

# Integrate a C# OpenAPI API

Generate and integrate a typed C# HTTP client from an OpenAPI definition while keeping generated code reproducible, configuration-driven, and separate from handwritten behavior.

## When to Use

- Add an integration with an external REST API that supplies OpenAPI 3.x or Swagger 2.0.
- Replace manual request/response DTOs and `HttpClient` calls with a generated typed client.
- Regenerate an existing NSwag client after a provider changes its OpenAPI contract.
- Configure a generated API client with base URL, authentication, resilience, and health checks.

## Inputs to Establish

Before changing code, identify:

1. The OpenAPI definition source: a committed local file, an authenticated download step, or a stable HTTPS URL.
2. The target C# project and its target framework.
3. The API's base URL(s), authentication scheme, required headers, timeout/retry expectations, and whether an API health endpoint exists.
4. The API operations and data types the application actually needs.
5. Existing project conventions for options binding, typed `HttpClient` registration, outbound authentication handlers, and error handling.

Do not place credentials, API keys, tokens, or production endpoints in source control. Use the project's established secret/configuration mechanism.

## Procedure

### 1. Inspect the Contract and Existing Conventions

- Validate that the definition parses and contains the intended operations, schemas, server URLs, security schemes, and response statuses.
- Compare a nearby HTTP integration in the target project before selecting client lifetime, naming, namespace, authentication, logging, and resilience patterns.
- Decide whether the definition is versioned alongside the project or retrieved through an authenticated download step. The selected approach must be reproducible and must not put credentials in version-controlled files.

### 2. Add the Required Build Dependency

Add `NSwag.MSBuild` to the target `.csproj`. Mark it private so it remains a build tool rather than a transitive runtime dependency:

```xml
<PackageReference Include="NSwag.MSBuild">
  <PrivateAssets>all</PrivateAssets>
  <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
</PackageReference>
```

Use the NSwag executable property that matches the target framework, such as `$(NSwagExe_Net100)` for `net10.0`. Check NSwag.MSBuild documentation or the installed package props if the project targets a different framework; do not assume an executable property name.

### 3. Create a Versioned NSwag Configuration

Create an `.nswag` file close to the integration, for example `Infrastructure/ExternalApi/external-api.nswag`.

- Use `documentGenerator.fromDocument.url` to point to the OpenAPI file or URL.
- Use `openApiToCSharpClient` with a stable `namespace`, `className`, and generated output path.
- Set `injectHttpClient` to `true` so dependency injection owns `HttpClient` construction.
- Prefer `useBaseUrl: false` when the base address is supplied through typed `HttpClient` registration.
- Select `SystemTextJson` unless the target project has a deliberate alternative.
- Enable nullable reference types and required-property behavior to match the project and contract expectations.
- Keep generated output under a clearly named `*.generated.cs` path. Do not hand-edit it.
- Avoid speculative settings. Add options only when required by the contract or project conventions.

A minimal shape is:

```json
{
  "runtime": "Net100",
  "documentGenerator": {
    "fromDocument": {
      "url": "external-api-openapi.json"
    }
  },
  "codeGenerators": {
    "openApiToCSharpClient": {
      "namespace": "YourProject.Infrastructure.ExternalApi",
      "className": "ExternalApiClient",
      "injectHttpClient": true,
      "useBaseUrl": false,
      "jsonLibrary": "SystemTextJson",
      "output": "ExternalApiClient.generated.cs"
    }
  }
}
```

Replace `Net100` and `$(NSwagExe_Net100)` with values compatible with the target framework and installed NSwag.MSBuild version.

### 4. Generate as Part of the Build

Add an MSBuild target that runs NSwag before compilation and includes the generated source explicitly when the project does not automatically include it. Keep the input definition, `.nswag` file, and output paths relative to the project directory.

```xml
<Target Name="GenerateExternalApiClient" BeforeTargets="CoreCompile;PrepareResource">
  <Exec
    ConsoleToMSBuild="true"
    Command="$(NSwagExe_Net100) run Infrastructure/ExternalApi/external-api.nswag /variables:Configuration=$(Configuration)">
    <Output TaskParameter="ExitCode" PropertyName="NSwagExitCode" />
    <Output TaskParameter="ConsoleOutput" PropertyName="NSwagOutput" />
  </Exec>

  <ItemGroup>
    <Compile Include="Infrastructure/ExternalApi/ExternalApiClient.generated.cs" />
  </ItemGroup>
</Target>
```

Adjust the target's `BeforeTargets` and `<Compile Include>` only after checking the target project's compile-item conventions. If default compile items already include the generated file, do not add a duplicate include. Build failures must expose generation errors; do not suppress NSwag exit codes or use placeholder generated clients.

### 5. Keep Custom Behavior Outside Generated Code

Use a partial class in a separate handwritten file for supported NSwag extension points, such as request preparation, response processing, serialization settings, or provider-specific error enrichment.

```csharp
namespace YourProject.Infrastructure.ExternalApi;

public partial class ExternalApiClient
{
    partial void ProcessResponse(HttpClient client, HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
        {
            ReadResponseAsString = true;
        }
    }
}
```

Confirm the generated method/property names before implementing a partial hook, because they depend on the NSwag version and configuration. Do not modify `*.generated.cs`; regeneration must preserve all handwritten changes.

### 6. Bind Options and Register a Typed Client

Create an options class for non-secret settings such as `BaseAddress` and use the project's standard configuration binding and validation approach. Register the generated class as a typed `HttpClient` and set its `BaseAddress` from validated configuration.

```csharp
services.AddHttpClient<ExternalApiClient>(client =>
{
    client.BaseAddress = new Uri(options.BaseAddress);
});
```

Fail fast when required configuration is missing, malformed, or unsafe. Do not substitute empty strings, mock URLs, or disabled authentication in normal runtime code.

### 7. Add Authentication and Cross-Cutting Outbound Behavior

- For API key or bearer token headers, use a `DelegatingHandler` (or the project's equivalent) registered on the typed client.
- Acquire and cache tokens according to the provider's documented flow; keep client secrets out of logs and source control.
- Add correlation headers, telemetry, retry/circuit-breaker policies, timeouts, and health checks through the project's existing HTTP infrastructure.
- Do not embed authentication flows in generated code. Keep them in handlers or dedicated services.

### 8. Consume the Generated Client Behind Application Code

Inject the typed generated client into the service, handler, controller, or endpoint that owns the use case. Translate provider DTOs and exceptions at the integration boundary when the rest of the application should not depend on provider-specific contracts.

Pass `CancellationToken` to generated async methods. Handle expected API statuses deliberately and preserve diagnostic details without exposing credentials or raw provider failures to end users.

### 9. Generate and Verify

Perform these checks in order:

1. Run the NSwag generation command or build the target project; confirm the generated file is created or updated without errors.
2. Compile the target project to catch contract or partial-class mismatches.
3. Review the generated diff after contract updates. Verify only intended operation, DTO, nullability, serialization, and exception changes are introduced.

Scope this workflow to the integration and its build verification. Do not add automated coverage unless the request explicitly includes it.

## Decision Points

- **Definition is a remote protected URL:** choose either a versioned sanitized snapshot or an authenticated reproducible download step; do not embed credentials in the `.nswag` file.
- **API requires custom authentication:** attach a dedicated `DelegatingHandler` to the typed client; do not alter generated source.
- **The project has implicit `Compile` items:** omit the explicit `<Compile Include>` to avoid duplicate compile errors.
- **The provider's error response needs richer diagnostics:** add a partial `ProcessResponse` hook or boundary-level exception translation, then compile.
- **The OpenAPI document changes incompatibly:** regenerate first, compile, and update handwritten boundary mappings before accepting the contract update.

## Completion Criteria

- `NSwag.MSBuild` is referenced by the target project with private assets.
- The OpenAPI source, `.nswag` configuration, and generated-output ownership are clear and reproducible.
- The generated client is recreated during the normal build or through a documented deterministic command.
- No handwritten changes exist in generated files.
- The generated client uses a typed `HttpClient` with validated base-address configuration.
- Authentication, secrets, resiliency, and error translation live outside generated code.
- A successful generation command and target-project build validate the integration.
