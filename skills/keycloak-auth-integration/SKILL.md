---
name: keycloak-auth-integration
description: 'Add Keycloak authentication/authorization to a project with an ASP.NET Core backend and an Angular frontend. Use when the user asks to integrate Keycloak, add JWT bearer auth, wire up keycloak-angular/keycloak-js, protect API endpoints with roles, add an Angular auth guard, or bootstrap Angular with runtime Keycloak config from the backend.'
---

# Keycloak Authentication Integration

Wires up Keycloak as the identity provider for a solution with an ASP.NET Core API and an Angular SPA: JWT bearer validation on the backend, `keycloak-angular` + `keycloak-js` on the frontend, and a route guard for role-based access.

## When to Use

- Adding Keycloak login/authorization to a new or existing ASP.NET Core + Angular project.
- Backend needs to validate Keycloak-issued JWTs and expose role/claim information to handlers.
- Frontend needs to initialize Keycloak, attach bearer tokens to API calls, and guard routes by role.
- Frontend needs to bootstrap its Keycloak config (URL/realm/client id) from the backend at startup instead of hardcoding it.

This skill is framework/architecture agnostic — it does not assume any particular backend project layout (vertical slice, layered, etc.), mediator library, or API style (minimal APIs or MVC controllers). Adapt file locations and endpoint syntax to the target project's conventions.

## Prerequisites

- A running Keycloak realm with a confidential/public client configured for the SPA (and optionally a separate client/audience for the API).
- Backend: .NET project that can reference `Microsoft.AspNetCore.Authentication.JwtBearer`.
- Frontend: Angular project that can install `keycloak-angular` and `keycloak-js`.

## Procedure

### 1. Backend — JWT Bearer authentication

1. Add the `Microsoft.AspNetCore.Authentication.JwtBearer` package to the API project.
2. Create an options class holding Keycloak connection settings (authority, audience, issuer, metadata address, client id) bound from configuration. See [backend-dotnet.md](./references/backend-dotnet.md#options) for the template.
3. Create an extension method that:
   - Registers `AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(...)` using the options above.
   - Sets `RequireHttpsMetadata = false` only in Development (Keycloak often runs over HTTP locally).
   - Calls `builder.Services.AddAuthorization()`.
   - Registers a `IClaimsTransformation` implementation that flattens Keycloak's `realm_access.roles` and `resource_access.<client>.roles` claims into standard `ClaimTypes.Role` claims, since ASP.NET Core's role checks don't understand Keycloak's nested claim shape.
   - See [backend-dotnet.md](./references/backend-dotnet.md#extension-method) and [backend-dotnet.md](./references/backend-dotnet.md#claims-transformation).
4. Call the extension method from the app startup (`builder.AddKeycloakAuthentication()` or equivalent name) before `builder.Build()`, and add `app.UseAuthentication()` / `app.UseAuthorization()` to the middleware pipeline in the right order.
5. Add appsettings configuration under a dedicated section (e.g. `Auth`) with `Authority`, `Audience`, `Issuer`, `MetadataAddress`, `ClientId` — see [backend-dotnet.md](./references/backend-dotnet.md#configuration).
6. (Optional) Add a small read-only endpoint that returns the public Keycloak config (url, realm, client id) derived from the same options, so the frontend can bootstrap without duplicating config. Implement it as a minimal API route or as a controller action, matching how the rest of the project exposes endpoints. See [backend-dotnet.md](./references/backend-dotnet.md#config-endpoint). Mark it anonymous.
7. (Optional) Add a scoped "current user" service that wraps `ClaimsPrincipal` to expose helpers like `GetUserId()`, `GetEmail()`, `IsInRole(role)`.
8. Protect endpoints with the project's existing endpoint style: `.RequireAuthorization()` on minimal API route builders, or `[Authorize]`/`[Authorize(Roles = "...")]` on controllers/actions, plus custom `IAuthorizationRequirement`/`IAuthorizationHandler` pairs as needed. Keep custom authorization handlers project-specific — do not assume a particular domain requirement pattern.

### 2. Frontend — keycloak-angular bootstrap

1. Install dependencies: `npm install keycloak-angular keycloak-js`.
2. If the Keycloak config (url/realm/clientId) is fetched from the backend at runtime (recommended so environments don't hardcode it), fetch it once before bootstrapping Angular and pass it into the app config factory. See [frontend-angular.md](./references/frontend-angular.md#bootstrap).
3. In the application config, call `provideKeycloak({...})` with the fetched config and sensible `initOptions` (e.g. `onLoad: 'check-sso'` with a `silentCheckSsoRedirectUri`).
4. Register the bearer-token interceptor:
   - Build an `IncludeBearerTokenCondition` with `createInterceptorCondition` matching the API's URL pattern (e.g. `/^\/api(\/.*)?$/i`).
   - Provide it via `INCLUDE_BEARER_TOKEN_INTERCEPTOR_CONFIG`.
   - Add `includeBearerTokenInterceptor` to `provideHttpClient(withInterceptors([...]))`.
5. See the full provider wiring in [frontend-angular.md](./references/frontend-angular.md#app-config).

### 3. Frontend — auth guard

1. Create a `CanActivateFn` guard built with `createAuthGuard` from `keycloak-angular`, which gives access to `authData: AuthGuardData` (`authenticated`, `grantedRoles`).
2. Inside the guard:
   - Redirect unauthenticated users to the login/landing route.
   - If the route has no required role in its `data`, allow any authenticated user.
   - If the route requires a role, check it against `grantedRoles.realmRoles` and/or `grantedRoles.resourceRoles` (resource/client roles), and against the injected `Keycloak` instance (`keycloak.hasRealmRole(...)`) for cases needing a direct check.
   - Redirect to a "forbidden"/"not found" route when the role check fails.
3. See the full guard template in [frontend-angular.md](./references/frontend-angular.md#auth-guard).
4. Apply the guard via route `data: { role: '...' }` and `canActivate: [authGuard]` on protected routes.

## Verification

- Backend starts and an anonymous request to a protected endpoint returns 401; a request with a valid Keycloak-issued bearer token for the right audience returns 200.
- Decoding the validated `ClaimsPrincipal` server-side shows flattened `role` claims matching the realm/client roles configured in Keycloak.
- Frontend redirects unauthenticated users to login, and authenticated API calls to the backend include an `Authorization: Bearer <token>` header.
- Navigating to a route requiring a role the current user lacks redirects away instead of rendering the page.

## Notes

- Do not hardcode Keycloak URLs/realm/client id in frontend source when they can differ per environment — prefer fetching them from a backend config endpoint or environment files, consistently with how the rest of the target project handles environment config.
- Keep domain-specific authorization concepts out of this generic flow; add them as project-specific extensions on top of the base wiring described here.
