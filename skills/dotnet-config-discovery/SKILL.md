---
name: dotnet-config-discovery
description: Analyzes a .NET / ASP.NET Core project (appsettings, environment variables, Options classes, service registration, external integrations, business parameters and custom infrastructure) and generates a Markdown configuration guide, deliverable to an operator with no knowledge of the codebase, listing every environment variable needed to deploy the application to production, with a functional description and default values. Also updates an existing guide after source code changes, preserving manual content and reporting added, removed, renamed and changed variables. Excludes standard .NET configuration and never includes secrets. Use when asked to document, inventory, hand over, refresh or update the configuration, settings or environment variables of a dotnet project for deployment.
---

# Deployment configuration guide (.NET)

Generates a Markdown document (default: `docs/configuration.md`) that serves as a **deliverable**: a person with no knowledge of how the application was built must be able to configure it and deploy it to production using only that document.

The analysis is performed on the source code, but the resulting document describes **only what the operator needs to configure**, in functional language, without internal development details.

The skill works in two modes:

- **Create mode:** the output document does not exist yet. Follow the [procedure](#procedure) and generate it from the template.
- **Update mode:** the output document already exists (for example, after source code changes). Follow the same analysis, then apply the [update rules](#updating-an-existing-document) to change only what is needed.

## Core rules

1. **Audience: external operator.** Write for someone who does not know the code, the architecture or the team's tooling. Explain what each value controls, its format, where to obtain it and what happens if it is missing.
2. **Only configuration needed for production.** Document the environment variables that must or may be set at deployment time: application and domain settings, external integrations, functional parameters and business rules, and custom infrastructure. Exclude standard .NET configuration (see [Exclusions](#exclusions)).
3. **No internal references.** The document must not mention:
   - class, interface, method, namespace, project or internal module names, nor repository paths or files;
   - internal libraries or frameworks (ORM, in-process messaging, dependency injection, etc.);
   - development-only tools or files: Aspire/AppHost, user-secrets, `launchSettings.json`, `appsettings.Development.json`, tests;
   - packaging or distribution (container, image, Dockerfile, orchestrator);
   - development decisions, conventions or history.
4. **Never include secrets.** Neither real values nor development values. Only state which variable is sensitive and how it must be provided (see [Secrets](#secrets-and-sensitive-values)).
5. **Evidence-based.** Do not invent variables, default values or behavior. Every variable must be backed by the analyzed code or configuration; traceability (file and line) is used during analysis and in the report to the user, **not** in the document.
6. **Read-only on code.** The skill does not modify code or configuration files; it only creates or updates the output document.

## Procedure

### 1. Survey the solution

- Locate the solution (`*.sln`, `*.slnx`) and projects (`*.csproj`). Identify the executable projects that get deployed (`Microsoft.NET.Sdk.Web`, `Microsoft.NET.Sdk.Worker`, `OutputType=Exe`). Local orchestration projects (Aspire AppHost), test projects and tooling are not deployed: use them only as discovery sources.
- Read `README`, `AGENTS.md`, `CLAUDE.md` and `docs/` to understand the application's purpose and domain vocabulary, so it can be described in functional terms.
- If there are several deployable executables, document each one in its own section.

### 2. Inventory configuration sources

Review these sources (excluding `bin/`, `obj/`, `node_modules/` and generated code). The **Usage** column states how each one is used:

| Source | Usage |
| --- | --- |
| `appsettings.json` | Default values shipped with the application. |
| `appsettings.Production.json` (or another production environment) | Also shipped with the application: its values are the effective defaults in production. |
| `appsettings.Development.json`, `launchSettings.json`, Aspire AppHost, user-secrets | **Discovery only**: find which variables must be supplied from outside the code (e.g. everything the AppHost injects through `WithEnvironment`, `WithReference` or `AddParameter` must be configured in production). Never use their values as defaults. |
| `Dockerfile`, `docker-compose*.yml`, `.env.example`, Kubernetes, Helm, Bicep/Terraform, CI/CD | Discover variables injected at deployment time and, if not sensitive, known production values. |
| Tests (`WebApplicationFactory`, `UseSetting`, `appsettings.Testing.json`) | Confirm keys and whether they are required. |
| Existing documentation | Functional descriptions, production values and procedures to obtain credentials. |

### 3. Detect configuration access in code

Search `*.cs` for the following patterns and follow each match to the final key:

```text
Configure<            AddOptions<           BindConfiguration(     .Bind(
GetSection(           GetValue<             GetConnectionString(   GetRequiredSection(
Configuration[        IConfiguration        Environment.GetEnvironmentVariable(
IOptions<             IOptionsSnapshot<     IOptionsMonitor<       ValidateOnStart
ValidateDataAnnotations   Validate(         SectionName            [ConfigurationKeyName(
[OptionsValidator]    IValidateOptions<     AddEnvironmentVariables(   AddAzureKeyVault(
```

For each match:

- **Resolve the full key.** Compose section prefixes (`GetSection("A").GetSection("B")` → `A:B`) and constants such as `SectionName`. Follow extension methods up to the startup point (`Program.cs`) to confirm the configuration is actually used by the deployed executable.
- **Expand Options classes.** Walk every public property with a setter (including nested classes, collections and dictionaries) and produce one variable per leaf key. Honor `[ConfigurationKeyName]`.
- **Client integrations that read implicit configuration.** Methods such as `AddNpgsqlDbContext("name")`, `AddRedisClient("name")`, `AddRabbitMQClient("name")` or `GetConnectionString("name")` require `ConnectionStrings__name`. Service discovery URLs (`https+http://service`) require `services__service__https__0` (or `http`). Document these variables with their functional meaning (e.g. "Connection string for the main database").
- **Dictionaries and collections.** Represent variable parts with uppercase placeholders: `Tenants__{TENANT}__PaymentGateway__ClientId`, `AllowedOrigins__{N}`. Explain the placeholder and how to add new entries.
- **Required.** A variable is required if there is `required`, `[Required]`, validation in `Validate(...)`/`IValidateOptions`, `ValidateOnStart`, `GetRequiredSection`, `?? throw`, `!` followed by direct use, or if its absence causes an obvious failure (e.g. `new Uri(config.Url)`). It is also required in production if the default value is an example, `localhost`, or a provider test environment (sandbox, staging, homologation).
- **Validations and formats.** Translate `[Range]`, `[Url]`, `[RegularExpression]`, `[MinLength]`, enums and manual validations into readable rules: "Number between 1 and 10", "One of: `Sandbox`, `Live`".
- **Functional effect.** Read where the options are consumed to describe what the application does with the value and what happens if it changes or is missing.

### 4. Detect configuration-dependent behavior

Review service registration and startup to identify features that are **enabled, disabled or changed** by configuration, and describe them in functional terms (without mentioning the internal mechanism):

- HTTP clients to external services with configurable URL, timeouts or credentials → "API URL of provider X".
- Conditional registrations (`if (config.Enabled)`, `if (!string.IsNullOrWhiteSpace(...))`) → "If set, the application's health check also verifies the availability of service X".
- Environment-dependent behavior (`IsDevelopment()`, `IsProduction()`, custom environment names): if the application needs a specific environment name to work correctly in production, state it.
- Databases, messaging, caches, storage, identity providers and telemetry with custom destinations: identify the required **external service** and the data the operator must obtain.

Do not document standard service registrations that do not depend on configuration.

### 5. Determine the default value

The effective value in production if the operator does not set the variable. Precedence (last wins):

1. Property initializer (`public int Retries { get; set; } = 3;`) or `[DefaultValue]`.
2. Fallback value in code (`GetValue<int>("X", 10)`, `config["X"] ?? "y"`).
3. `appsettings.json`.
4. `appsettings.Production.json`, if present.

Rules:

- No default value → `—`.
- Example or non-production value (`example.com`, `localhost`, `SomeValue`, `changeme`, sandbox/homologation URLs) → do not present it as a valid default: write `—` and mark the variable as required; if the production value is known from evidence (documentation, comments, deployment manifests), put it in the format/example column.
- Development values (AppHost, `appsettings.Development.json`, `launchSettings.json`) are never defaults.
- Express values in the format the environment variable expects: `true`/`false`, `00:05:00` for durations, numbers without thousands separators, full URLs.

### 6. Classify each setting

Assign each variable to a category with an operator-friendly name (omit empty categories):

| Category | Includes |
| --- | --- |
| General | General application parameters (public URL, instance name, global behavior). |
| Database and storage | Connection strings, schemas, file storage. |
| Authentication and security | Identity provider, audiences, allowed origins, configurable roles. |
| External integrations | Third-party APIs, payment gateways, tax services, email, external messaging, webhooks. One subsection per service. |
| Business parameters | Limits, deadlines, amounts, percentages, tolerances, toggleable features. |
| Per-customer / tenant configuration | Values repeated for each customer, organization or tenant. Explain what a tenant is in this context and how to add a new one. |
| Operations and monitoring | Health checks, retries, timeouts, telemetry with custom configuration. |

### 7. Write

- **Variable name:** environment variable form only (`:` → `__`; `PaymentGateway:BaseUrl` → `PaymentGateway__BaseUrl`). Include the custom prefix if any (`AddEnvironmentVariables("MYAPP_")`). Do not show the `:` form.
- **Description:** one or two sentences in functional language: what it controls, where the value comes from (e.g. "provided by the vendor when the account is created") and what happens if it is missing or wrong.
- **Types:** use readable names: Text, URL, Integer, Decimal, Yes/No (`true`/`false`), Duration (`hh:mm:ss`), List, Certificate (state the expected encoding, e.g. Base64 of a PFX).
- **Domain terms:** use business terminology; explain non-obvious terms once (e.g. "tenant").
- **External product names** (PostgreSQL, Keycloak, the payment provider, etc.) may be mentioned: they are services the operator must know about or procure.
- Order variables within each section by relevance (required first), then alphabetically.

### 8. Generate or update the document

- Default path: `docs/configuration.md` (honor a different path if the user specifies one). Write the document in the language of the repository's existing documentation; if there is no reference, use the user's language. Translate the template headings and labels accordingly.
- Only the block between `<!-- config-discovery:start -->` and `<!-- config-discovery:end -->` is managed by this skill; content outside the markers is never modified.
- If the file does not exist, create it from the template below.
- If the file exists and contains the markers, switch to update mode and follow [Updating an existing document](#updating-an-existing-document). If the markers are missing, ask before overwriting.

Use this template (example names are illustrative; replace them with the real ones):

````markdown
# <Application name> configuration guide

<!-- config-discovery:start -->

## Introduction

<One or two sentences on what the application does, in functional terms.>

The application is configured through environment variables. Conventions:

- `__` (double underscore) separates hierarchy levels, e.g. `PaymentGateway__BaseUrl`.
- Placeholders in braces, such as `{TENANT}`, are replaced by an identifier; the variable is repeated for each value.
- Yes/No is expressed as `true`/`false`; durations as `hh:mm:ss`.
- Variables marked as **sensitive** contain credentials and must be supplied through the deployment platform's secret mechanism, never in plain text.

## Required external services

| Service | Use in the application | Data to obtain |
| --- | --- | --- |
| PostgreSQL database | Main storage. | Server, database, user and password. |
| Payment provider | Online payment collection. | API URL, account credentials. |

## Minimum required configuration

Variables that must be set for the application to start and work in production:

- [ ] `ConnectionStrings__Main`
- [ ] `PaymentGateway__BaseUrl`
- [ ] `Tenants__{TENANT}__PaymentGateway__ClientSecret`

## Database and storage

| Environment variable | Description | Required | Sensitive | Default value | Format / example |
| --- | --- | --- | --- | --- | --- |
| `ConnectionStrings__Main` | Connection string for the main database. The application does not start without it. | Yes | Yes | — | `Host=<server>;Port=5432;Database=<database>;Username=<user>;Password=<password>` |

## External integrations

### Payment provider

Enables online payment collection and receiving its notifications.

| Environment variable | Description | Required | Sensitive | Default value | Format / example |
| --- | --- | --- | --- | --- | --- |
| `PaymentGateway__BaseUrl` | Provider API URL. | Yes | No | — | URL, e.g. `https://api.provider.com` |
| `PaymentGateway__Timeout` | Maximum wait time for each request to the provider. | No | No | `00:00:30` | Duration `hh:mm:ss` |
| `PaymentGateway__HealthCheckEnabled` | If `true`, the application's health check also verifies the provider's availability. | No | No | `false` | `true` / `false` |

## Per-customer / tenant configuration

<Explain what a tenant represents and that the block is repeated for each one, replacing `{TENANT}` with its identifier.>

| Environment variable | Description | Required | Sensitive | Default value | Format / example |
| --- | --- | --- | --- | --- | --- |
| `Tenants__{TENANT}__PaymentGateway__ClientId` | Client identifier assigned by the payment provider to this tenant. | Yes | No | — | Text |
| `Tenants__{TENANT}__PaymentGateway__ClientSecret` | Client secret assigned by the payment provider to this tenant. | Yes | Yes | — | Text |

## Secrets

The following variables contain credentials. They must be loaded into the platform's secret store and never written to files or repositories:

- `ConnectionStrings__Main`
- `Tenants__{TENANT}__PaymentGateway__ClientSecret`

## Variable template

Reference list of all required variables; replace each `<...>` with the corresponding value.

```text
ConnectionStrings__Main=<connection string>
PaymentGateway__BaseUrl=<provider API URL>
Tenants__<tenant-id>__PaymentGateway__ClientId=<client id>
Tenants__<tenant-id>__PaymentGateway__ClientSecret=<secret>
```

<!-- config-discovery:end -->
````

### 9. Report to the user

In the chat response (not in the document), report:

- Path of the generated document, total number of variables and how many are required and sensitive.
- Findings for the development team, with `file:line` references: defined keys that are unused, keys used without definition or default value, example values in shipped files, inconsistencies between environments, secrets committed to the repository (without reproducing their value, recommending rotation).
- Anything that could not be determined with certainty (unknown production values, inferred descriptions) so the team can complete it.
- In update mode, also the [change summary](#change-summary).

## Updating an existing document

Use this mode when the output document already exists. The goal is a document that matches the current code while keeping the diff minimal and preserving information added by people.

### Analysis

- **Always run the full analysis (steps 1–7)** on the current code. Do not limit it to the changed files: configuration can change indirectly (a renamed section constant, a moved registration, a new `appsettings.Production.json`).
- Use git only to focus attention and to explain changes, if the repository has history:
  - Find the last commit that touched the document: `git log -1 --format=%H -- <document>`.
  - Review what changed since then in code and configuration: `git diff <commit> -- '*.cs' '*.json' '*.yml' '*.yaml' '*.csproj'`, including uncommitted changes.
  - If the user names a branch, commit range or merge request, use that range instead.

### Compare

Parse the managed block of the existing document into an inventory: for each variable, its section, description, required, sensitive, default value and format/example; plus the external services table, the tenant explanation and the introductory paragraphs of each section.

Compare that inventory with the result of the fresh analysis and classify each variable:

| Classification | Action in the document |
| --- | --- |
| **Unchanged** | Keep the existing row exactly as is (wording, position, formatting). |
| **Changed** (default value, required, sensitive, type, format, allowed values or functional effect) | Update only the affected cells. Keep the rest of the description unless it is now incorrect. |
| **Added** | Write a new row following [step 7](#7-write) and insert it in the right section according to the ordering rule, without reordering existing rows. Create the section if it does not exist. |
| **Removed** | Delete it from every place it appears. |
| **Renamed** | A removed and an added variable that serve the same purpose (renamed property, section or connection string name). Update the row in place, keeping its manual content, and flag it as a breaking change for operators. |

### Preserve manual content

- Treat any information in the managed block that cannot be derived from the code as manual content: known production values, instructions to obtain credentials, clarifications, examples, section introductions written by people.
- Keep manual content as long as it is consistent with the current code.
- If manual content contradicts the current code (for example, a documented production value for a variable whose format changed), correct it according to the code and list it under **Needs review** in the change summary. Do not add review notes or comments to the document.
- Keep the document's language, terminology and style, even if they differ from the template.

### Keep derived sections consistent

After updating the tables, update the sections derived from them so they stay consistent: **Required external services** (add new services, remove those no longer used), **Minimum required configuration**, **Secrets** and **Variable template**.

### Minimal diff

- Do not reformat tables, rewrap text, rename headings or reorder existing content.
- If no differences are found, do not modify the file and report that the document is up to date.

### Change summary

In the chat response, include a table with every change made to the document, with the code evidence that motivated it:

| Change | Variable | Detail | Evidence |
| --- | --- | --- | --- |
| Added | `PaymentGateway__Timeout` | Optional, default `00:00:30`. | `src/.../PaymentGatewayOptions.cs:12` |
| Renamed | `Identity__Authority` → `Identity__MetadataAddress` | Operators must rename the variable in their deployments. | `src/.../IdentityProviderOptions.cs:8` |
| Removed | `Reports__LegacyMode` | No longer read by the application. | — |

Then list separately:

- **Impact on existing deployments:** new required variables, renamed or removed variables and default value changes that operators must apply before deploying the new version.
- **Needs review:** manual content that was corrected or could not be verified against the code.

## Exclusions

Do not document the following, unless the application gives them its own meaning or logic (in which case document only that part):

- `Logging__*`, `AllowedHosts`, `Kestrel__*`, `urls`, `https_port`, standard `HostFiltering` and `ForwardedHeaders`.
- Hosting and runtime variables: `ASPNETCORE_ENVIRONMENT`, `DOTNET_ENVIRONMENT`, `ASPNETCORE_URLS`, `ASPNETCORE_HTTP_PORTS`, `ASPNETCORE_HTTPS_PORTS`, `DOTNET_*`, `ASPNETCORE_*`. Exception: if the application requires a particular environment name to work correctly, state it in the General section.
- Standard telemetry (`OTEL_*`, `APPLICATIONINSIGHTS_CONNECTION_STRING`) without custom logic.
- Variables specific to development or local orchestration tools (`ASPIRE_*`, `DOTNET_DASHBOARD_*`, `DOTNET_LAUNCH_PROFILE`).
- Standard service registration (MVC/Minimal APIs, OpenAPI, ProblemDetails, CORS, compression, etc.) without custom configuration parameters.

## Secrets and sensitive values

Treat as sensitive any variable that meets any of these conditions:

- Its name contains `Password`, `Pwd`, `Secret`, `Token`, `ApiKey`, `Key` (as a credential), `Certificate`, `Cert`, `PrivateKey`, `ClientSecret`, `Credential`, `Signature`, `Salt`, `ConnectionString`.
- During development it is supplied through a secret mechanism (secret parameters, user-secrets, Key Vault/Secrets Manager, protected CI variables, Kubernetes `secretKeyRef`).
- Its value looks like a credential (long random strings, Base64, JWT, PEM, connection strings with `Password=`).

For sensitive variables:

- Mark **Sensitive: Yes**, write `—` as the default value and never include the value, even if it exists in a repository file.
- In **Format / example** use placeholders only (`<password>`), never realistic-looking values.
- Include them in the **Secrets** section and in the template with placeholders.

## Final check

Before delivering, verify that:

- [ ] All deployable executables and the registration methods invoked from their startup point were reviewed.
- [ ] Every variable the development environment injects from outside the code (AppHost, user-secrets, `launchSettings.json`) appears in the document or is justifiably excluded.
- [ ] Every registered Options class is expanded to leaf variables.
- [ ] Default values follow the precedence and include no development or example values.
- [ ] The document does not mention classes, files, paths, projects, internal libraries, Aspire, containers or development tools.
- [ ] No secret, password, token or connection string with credentials appears.
- [ ] No standard configuration from the exclusions list was included.
- [ ] A person with no knowledge of the development could complete the configuration using only the document.
- [ ] Markdown tables render correctly (same number of columns, `|` escaped in values).

In update mode, also verify that:

- [ ] Content outside the markers is unchanged.
- [ ] Unchanged variables keep their exact wording and position.
- [ ] Every variable in the minimum required list, secrets list and variable template exists in the tables, and vice versa.
- [ ] The change summary lists every change made, including those with impact on existing deployments.
