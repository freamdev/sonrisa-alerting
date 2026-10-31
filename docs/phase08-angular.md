# Phase 08 — Angular UI and same-origin integration

Status: **AWAITING HUMAN VALIDATION**. Phase 08 only; Phase 09 has not started.

The Angular 22 standalone app uses HttpClient, routing, forms and signals, without a UI toolkit or state library. `/personal` has independent Topics and Receive via checkbox groups. Each change saves immediately through one idempotent PUT/DELETE; success is announced only after the response, and failed writes restore the checkbox. A disabled selected item can be removed; an unselected disabled item cannot be added. Refresh/revisit settings to see another administrator's changes.

`/admin` lists users and global catalogs. It supports user enablement, explicit confirmed permanent deletion, and global topic/channel enablement. Your own disable/delete buttons are unavailable. Admins also retain `/personal`. Route guards and hidden navigation are UX conveniences; existing server policies, current enabled-account checks and CSRF enforcement remain the authority. No backend behavior/schema changed.

Login/logout use the existing HttpOnly same-origin cookies. Before **every** mutation, including login, the service fetches `/api/auth/csrf` and sends `X-CSRF-TOKEN`. No passwords, sessions or CSRF tokens are persisted in browser storage. Password fields are cleared after submission. HTTP 401 clears the displayed session; error messages explain sign-in, authorization, validation/conflict, network and server failures using live status/alert regions. No automatic mutation retry is performed.

## Docker Compose

From the repository root, copy `.env.example` to `.env` only if you do not already have one. Replace password placeholders, set `DevelopmentSeed__Enabled=true`, and configure the disposable development admin/user credentials. Keep polling disabled until preferences and destinations are ready.

```powershell
docker compose --env-file .env.example --profile channels config --quiet
docker compose --profile channels up --build --detach --wait --wait-timeout 180
docker compose --profile channels ps --all
curl.exe --fail http://localhost:4200/health
```

Open **http://localhost:4200**. `FRONTEND_PORT` defaults to 4200. Nginx serves the built SPA, falls back to `index.html` for deep links, and proxies `/api/` and `/health` to the backend. Use the frontend origin throughout a browser session; the API's loopback port 5165 remains available for existing guides. Both Angular and cookies use relative same-origin requests; CORS/token storage is unnecessary. Compose remains the existing local HTTP Development environment. Production TLS/deployment work is outside Phase 08.

Mailpit is at http://localhost:8025 with the `channels` profile. Email uses the Identity email. Slack still uses the server-owned per-user webhook mapping from [Phase 05](phase05-channels.md); no destination UI was added. Configure only a controlled test destination or the approved Development local mock, never real recipients for routine validation.

## Local Angular development

Install Node **24.15 or newer within major 24** (validated with 24.22.2) and npm. Node 18 is unsupported. This implementation used a checksum-verified portable runtime in ignored `.local/toolchain/node-v24.22.2-win-x64`; it did not change the system installation. Angular's requirements are in the [official compatibility table](https://angular.dev/reference/versions).

With the backend running at http://localhost:5165 (for example through Compose):

```powershell
cd frontend
npm.cmd ci
npm.cmd start
```

Open http://localhost:4200. `proxy.conf.json` forwards `/api/**` and `/health` to port 5165. To use another backend port, copy this file locally and run `npm.cmd start -- --proxy-config PATH_TO_LOCAL_PROXY.json`. To avoid a Compose frontend port conflict, use `npm.cmd start -- --port 4201` and open that port, or start only the backend services with `docker compose --profile channels up --build --detach --wait api mailpit`.

If using the portable runtime in this workspace, first run from the repository root:

```powershell
$env:PATH = "$PWD\.local\toolchain\node-v24.22.2-win-x64;$env:PATH"
$env:npm_config_cache = "$PWD\.local\npm-cache"
```

## Automated checks

```powershell
npm.cmd --prefix frontend ci
npm.cmd --prefix frontend test
npm.cmd --prefix frontend run build
npm.cmd --prefix frontend run format:check
dotnet build Alerts.sln --no-restore
dotnet test Alerts.sln --no-build
dotnet format Alerts.sln --verify-no-changes --no-restore
git -c safe.directory=D:/Temp/Sonrisa diff --check
```

Use the root README's CLI home/cache settings if needed. Frontend tests use the Angular CLI Vitest/jsdom runner: CSRF sequencing, safe errors/session expiry, independent preference mutations, disabled-item controls, failed-write rollback, role navigation/guards, login/logout, global enablement payloads and deletion confirmation/cancellation. Backend tests retain all security/dispatch coverage. No E2E or external-provider integration suite was introduced.

## Browser acceptance — pending developer validation

1. Open the frontend, sign in as the regular user. Confirm user status, personal settings, and no Administration link. Try `/admin` directly; expect return to personal settings. Through an API client, the same user's `/api/admin/users` must still return 403.
2. Select Finance and Weather under Topics, and Email and Slack under Receive via. Check each Saved response and reload the page to verify persistence. There must be two independent groups, with no per-topic channel grid.
3. Configure the user's Slack test mapping following Phase 05 (obtain the application user ID through the documented safe SQL/API session check). Enable `NewsSource__Enabled=true` and recreate the API only after destinations/preferences are ready. Publish a **fresh external ID** using the fixture instructions in [Phase 06](phase06-source-worker.md). Observe one Email in Mailpit and one Slack test message.
4. Uncheck Slack. Publish a different fresh event ID in Finance or Weather. Observe Email only; topic choices and Email remain selected. Repolling the same ID must not duplicate messages.
5. Sign out, then sign in as admin. Confirm both Personal settings and Administration routes. Make a personal choice as admin. Disable Email globally in Administration and publish another fresh event for the regular user's selected topic: no Email is sent. Re-enable it afterward. The personal list must show disabled global status on reload, without silently clearing stored choices.
6. In Administration, disable/re-enable a disposable other user. A disabled user's existing requests and login must fail. Confirm your own disable/delete buttons are unavailable. Open Delete for the disposable user, cancel once, then reopen and confirm permanent deletion. Verify the account disappears; its session becomes unauthorized and dependent selections/deliveries are removed per the Phase 07 policy. Do not delete an account you need to retain. Seeding can recreate a deleted seeded account on API restart.
7. Verify errors: wrong credentials show a sign-in error; temporarily unavailable API shows a connection/server error; a concurrently disabled new choice shows a conflict and restores the checkbox. Keyboard-tab through controls and verify label association and visible focus. Reload `/personal` and `/admin` directly to exercise SPA fallback.
8. Provide explicit Phase 08 approval or corrections. Approval does not authorize Phase 09 without a separate request.

## Executed local validation

On 2026-10-31, isolated `alerts-phase08` Compose built successfully and reached healthy status with its own PostgreSQL volume, generated ignored credentials, API port 15185, frontend 14208 and Mailpit 18045. HTTP requests through the **frontend origin** verified SPA root/deep links, cookie login/logout, CSRF rejection (400), regular-user admin denial (403), independent [Finance, Weather]/[Email, Slack] choices, preference persistence, admin personal access, global channel changes, disposable-user deletion and session invalidation.

Polling three distinct local fixture IDs produced: both channels → one Mailpit email and one accepted local Slack mock request; Slack removed → second email and no additional Slack call; Email globally disabled → no further email or Slack call. The Slack mock's final count was exactly 1 and Mailpit's was 2. This was manual HTTP/Compose validation using ignored temporary scripts and a local receiver, not a browser/E2E suite or a real Slack delivery claim. The disposable user was deleted only within the new test database. Services were stopped after validation; the project volume and ignored artifacts remain for inspection.

Automated browser validation could not run: Computer Use stopped because it could not determine the current Windows browser URL confidently enough to enforce policy. No further browser input was attempted. Browser layout, interactive flow and final acceptance remain for the developer using the steps above.
