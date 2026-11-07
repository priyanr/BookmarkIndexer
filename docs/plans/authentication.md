# Login with mws.priyan.in

## Goal
Run BookmarkIndexer on the k8s cluster at https://apps.priyan.in/bm behind a login with mws.priyan.in (OAuth2,
no discovery), switched by `Auth:Enabled`, the same way PersonalAI logs in.

## Affected existing features
None while `Auth:Enabled` is false (the default; local runs are unchanged). When true, all pages and API calls except
`/api/health` need a login.

## Stages
### Stage 1: API login
- Behaviour: `api/auth/status|login|exchange|refresh|logout`; with `Auth:Enabled` every other controller action
  needs a bearer token that mws signed (shared HMAC `Auth:JwtKey`) and that its introspection endpoint
  (`/oauth2/validate`) reports active for this client. Provider failures answer 503.
- Test seam: `OAuthClient` with a fake `HttpMessageHandler` (`Tests/Auth/OAuthClientTests.cs`).
- New types: `AuthSettings` (the `Auth` config section), `OAuthClient` (talks to the provider), `AuthController`,
  DTOs `AuthStatusDto`, `ExchangeCodeDto`, `RefreshTokenDto`, `TokenResponseDto`.
- Files touched: `Program.cs`, `appsettings.json`, `Directory.Packages.props`, `BookmarkIndexer.Web.csproj`.
- [x] done

### Stage 2: UI login and base path
- Behaviour: the UI logs in through the API, keeps the tokens in localStorage, refreshes them, has a Log out button
  and can be served under `NEXT_PUBLIC_BASE_PATH`.
- Test seam: `npm run build` and a manual run against mws.
- New files: `src/api/authApi.js`, `src/api/http.js` (shared `send` with the token), `src/app/AuthGate.js`,
  `src/app/auth/callback/page.js`.
- Files touched: `bookmarksApi.js`, `searchApi.js`, `layout.js`, `globals.css`, `next.config.js`, `ui/Dockerfile`.
- [x] done

## Out of scope
OIDC discovery, JWKS, PKCE (mws doesn't support it), end-session logout at the provider, user accounts or roles.
k8s manifests and scripts live in the Deployments repo (`BookmarkIndexer/`), not here.
