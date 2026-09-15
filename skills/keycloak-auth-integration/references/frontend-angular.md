# Frontend: Angular + keycloak-angular + keycloak-js

Code templates referenced from [SKILL.md](../SKILL.md). Adjust to the target project's Angular version and bootstrap style (standalone `ApplicationConfig`, module-based, etc.) — the snippets below use the standalone API.

## Install

```bash
npm install keycloak-angular keycloak-js
```

## App config

```ts
import {
    provideKeycloak,
    createInterceptorCondition,
    IncludeBearerTokenCondition,
    includeBearerTokenInterceptor,
    INCLUDE_BEARER_TOKEN_INTERCEPTOR_CONFIG
} from 'keycloak-angular';
import { ApplicationConfig } from '@angular/core';
import { provideHttpClient, withFetch, withInterceptors } from '@angular/common/http';
import { provideRouter } from '@angular/router';
import { appRoutes } from './app.routes';

// Only requests to the backend API should carry the bearer token.
const bearerTokenCondition = createInterceptorCondition<IncludeBearerTokenCondition>({
    urlPattern: /^\/api(\/.*)?$/i,
    bearerPrefix: 'Bearer'
});

export type KeycloakConfig = {
    url: string;
    realm: string;
    clientId: string;
};

export function createAppConfig(keycloakConfig: KeycloakConfig): ApplicationConfig {
    return {
        providers: [
            provideKeycloak({
                config: {
                    url: keycloakConfig.url,
                    realm: keycloakConfig.realm,
                    clientId: keycloakConfig.clientId
                },
                initOptions: {
                    onLoad: 'check-sso',
                    silentCheckSsoRedirectUri: window.location.origin + '/silent-check-sso.html'
                }
            }),
            {
                provide: INCLUDE_BEARER_TOKEN_INTERCEPTOR_CONFIG,
                useValue: [bearerTokenCondition]
            },
            provideRouter(appRoutes),
            provideHttpClient(withFetch(), withInterceptors([includeBearerTokenInterceptor]))
        ]
    };
}
```

`silent-check-sso.html` must exist in the app's `public`/assets folder for `check-sso` to work without a full page redirect:

```html
<!DOCTYPE html>
<html>
<body>
<script>parent.postMessage(location.href, location.origin);</script>
</body>
</html>
```

## Bootstrap

If Keycloak config is fetched from the backend at runtime (recommended so it's not baked into the build per environment), resolve it before bootstrapping the app:

```ts
import { bootstrapApplication } from '@angular/platform-browser';
import { AppComponent } from './app.component';
import { createAppConfig, KeycloakConfig } from './app.config';

fetch('/api/keycloak-config')
    .then((res) => res.json())
    .then((keycloakConfig: KeycloakConfig) =>
        bootstrapApplication(AppComponent, createAppConfig(keycloakConfig))
    );
```

If the config is static per environment instead, read it from Angular's `environment.ts` files and skip the fetch.

## Auth guard

```ts
import { ActivatedRouteSnapshot, CanActivateFn, Router, RouterStateSnapshot, UrlTree } from '@angular/router';
import { AuthGuardData, createAuthGuard } from 'keycloak-angular';
import Keycloak from 'keycloak-js';
import { inject } from '@angular/core';

const isAccessAllowed = async (
    route: ActivatedRouteSnapshot,
    _state: RouterStateSnapshot,
    authData: AuthGuardData
): Promise<boolean | UrlTree> => {
    const { authenticated, grantedRoles } = authData;
    const router = inject(Router);
    const keycloak = inject(Keycloak);

    if (!authenticated) {
        return router.parseUrl('/login');
    }

    const requiredRole = route.data['role'] as string | undefined;

    // No role required on this route: any authenticated user is allowed.
    if (!requiredRole) {
        return true;
    }

    const hasRealmRole = keycloak.hasRealmRole(requiredRole);
    const hasResourceRole = Object.values(grantedRoles.resourceRoles).some((roles) =>
        roles.includes(requiredRole)
    );

    if (hasRealmRole || hasResourceRole) {
        return true;
    }

    return router.parseUrl('/forbidden');
};

export const authGuard = createAuthGuard<CanActivateFn>(isAccessAllowed);
```

Apply it on routes:

```ts
{
    path: 'admin',
    component: AdminComponent,
    canActivate: [authGuard],
    data: { role: 'admin' }
}
```
