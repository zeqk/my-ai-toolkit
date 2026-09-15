# Backend: ASP.NET Core + JwtBearer + Keycloak

Code templates referenced from [SKILL.md](../SKILL.md). Adjust namespaces and folder location to match the target project's conventions.

## Configuration

`appsettings.json`:

```json
{
  "Auth": {
    "Authority": "https://<keycloak-host>/realms/<realm-name>",
    "Audience": "<api-audience-or-client-id>",
    "Issuer": "https://<keycloak-host>/realms/<realm-name>",
    "MetadataAddress": "https://<keycloak-host>/realms/<realm-name>/.well-known/openid-configuration",
    "ClientId": "<spa-client-id>"
  }
}
```

## Options

```csharp
public class AuthOptions
{
    public static string SectionName => "Auth";

    public string Authority { get; set; }
    public string Audience { get; set; }
    public string ClientId { get; set; }
    public string Issuer { get; set; }
    public string MetadataAddress { get; set; }
}
```

## Extension method

```csharp
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

public static class AuthExtensions
{
    public static WebApplicationBuilder AddKeycloakAuthentication(this WebApplicationBuilder builder)
    {
        builder.Services.AddOptions<AuthOptions>().Bind(builder.Configuration.GetSection(AuthOptions.SectionName));
        var authOptions = builder.Configuration.GetSection(AuthOptions.SectionName).Get<AuthOptions>();

        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MetadataAddress = authOptions.MetadataAddress;
                options.Audience = authOptions.Audience;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = authOptions.Issuer
                };
                // Keycloak commonly runs over HTTP locally; require HTTPS metadata everywhere else.
                options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
            });

        builder.Services.AddAuthorization();

        builder.Services.AddTransient<IClaimsTransformation, KeycloakClaimsTransformer>();

        return builder;
    }
}
```

Wire it up in the entry point:

```csharp
builder.AddKeycloakAuthentication();
// ...
var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
```

## Config endpoint

Exposes the public Keycloak config (url, realm, client id) so the frontend can bootstrap without duplicating it. Implement whichever way matches the project's existing endpoint style.

Minimal API:

```csharp
public static class AuthExtensions
{
    public static RouteHandlerBuilder MapKeycloakConfigEndpoint(this WebApplication app)
    {
        var authOptions = app.Configuration.GetSection(AuthOptions.SectionName).Get<AuthOptions>();
        return app.MapGet("api/keycloak-config", () =>
        {
            var config = new
            {
                Url = authOptions.Authority.Split("/realms")[0],
                Realm = authOptions.Authority.Split('/').LastOrDefault(),
                ClientId = authOptions.ClientId
            };
            return Results.Ok(config);
        }).AllowAnonymous();
    }
}
```

```csharp
app.MapKeycloakConfigEndpoint(); // call after app.UseAuthorization()
```

MVC controller:

```csharp
[ApiController]
[Route("api/[controller]")]
public class KeycloakConfigController(IOptions<AuthOptions> authOptions) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public IActionResult Get()
    {
        var options = authOptions.Value;
        return Ok(new
        {
            Url = options.Authority.Split("/realms")[0],
            Realm = options.Authority.Split('/').LastOrDefault(),
            ClientId = options.ClientId
        });
    }
}
```

## Claims transformation

ASP.NET Core's role checks (`ClaimTypes.Role`, `[Authorize(Roles = ...)]`, `User.IsInRole(...)`) expect flat role claims. Keycloak emits roles nested inside `realm_access` and `resource_access` JSON claims, so they must be flattened once per request.

```csharp
using Microsoft.AspNetCore.Authentication;
using Newtonsoft.Json; // or System.Text.Json, keep consistent with the rest of the project
using System.Security.Claims;

public class KeycloakClaimsTransformer : IClaimsTransformation
{
    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        var identity = (ClaimsIdentity)principal.Identity;
        if (!identity.IsAuthenticated)
        {
            return Task.FromResult(principal);
        }

        // "realm_access": { "roles": ["role-a", "role-b"] }
        var realmAccessClaim = identity.FindFirst("realm_access");
        if (realmAccessClaim is not null)
        {
            var realmAccess = JsonConvert.DeserializeObject<Dictionary<string, string[]>>(realmAccessClaim.Value);
            foreach (var role in realmAccess.GetValueOrDefault("roles") ?? [])
            {
                identity.AddClaim(new Claim(ClaimTypes.Role, role));
            }
        }

        // "resource_access": { "<client-id>": { "roles": ["role-a"] } }
        var resourceAccessClaim = identity.FindFirst("resource_access");
        if (resourceAccessClaim is not null)
        {
            var resourceAccess = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, string[]>>>(resourceAccessClaim.Value);
            foreach (var (resource, resourceRoles) in resourceAccess)
            {
                foreach (var role in resourceRoles.GetValueOrDefault("roles") ?? [])
                {
                    identity.AddClaim(new Claim(ClaimTypes.Role, $"{resource}:{role}"));
                }
            }
        }

        return Task.FromResult(principal);
    }
}
```

## Current user helper (optional)

Keep this minimal — only add helpers the project actually needs.

```csharp
using System.Security.Claims;

public interface ICurrentUser
{
    string GetUserId();
    string GetEmail();
    bool IsInRole(string role);
}

public sealed class CurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    private ClaimsPrincipal User => httpContextAccessor.HttpContext?.User;

    public string GetUserId() => User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User?.FindFirstValue("sub");
    public string GetEmail() => User?.FindFirst(ClaimTypes.Email)?.Value;
    public bool IsInRole(string role) => User?.IsInRole(role) ?? false;
}
```

Custom authorization requirements/handlers are project-specific — implement them alongside the domain, not as part of this generic setup.
