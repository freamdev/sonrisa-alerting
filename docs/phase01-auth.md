# Phase 01 setup and validation

Phase 01 covers Identity authentication/authorization. Phase 02 additionally supplies domain persistence; see [schema and migration validation](phase02-data.md). The developer approved development-only seeded admin/user accounts, same-origin cookies, no public registration, and blocking both login and subsequent requests when an account is disabled. Admin-added/invited users are a later management feature.

## Database and development bootstrap

Use an existing PostgreSQL instance with an empty `alerts` database and a login allowed to migrate it. Alternatively, this optional **disposable** local database needs Docker (no Compose configuration is introduced):

```powershell
$env:POSTGRES_PASSWORD = 'Local!aA1' + [guid]::NewGuid().ToString('N')
docker run --rm --detach --name alerts-phase01-dev --publish 127.0.0.1:55432:5432 --env POSTGRES_PASSWORD --env POSTGRES_DB=alerts postgres:17
$env:ConnectionStrings__Alerts = "Host=127.0.0.1;Port=55432;Database=alerts;Username=postgres;Password=$env:POSTGRES_PASSWORD"
```

Wait until `docker exec alerts-phase01-dev pg_isready -U postgres -d alerts` reports accepting connections. This container has no volume; stopping it removes the demo database. For an existing database, set `ConnectionStrings__Alerts` to your own connection string instead.

From the repository root, in the same PowerShell terminal:

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet restore Alerts.sln
dotnet tool restore
dotnet ef database update --project src/Alerts.Api
# Rerun to confirm it reports that the database is already up to date.
dotnet ef database update --project src/Alerts.Api
dotnet ef migrations has-pending-model-changes --project src/Alerts.Api

$env:DevelopmentSeed__Enabled = 'true'
$env:DevelopmentSeed__AdminEmail = 'admin@example.test'
$env:DevelopmentSeed__UserEmail = 'user@example.test'
# Choose separate passwords: at least 12 characters, upper/lowercase, digit and symbol.
$env:DevelopmentSeed__AdminPassword = [System.Net.NetworkCredential]::new('', (Read-Host 'Development admin password' -AsSecureString)).Password
$env:DevelopmentSeed__UserPassword = [System.Net.NetworkCredential]::new('', (Read-Host 'Development user password' -AsSecureString)).Password
dotnet run --project src/Alerts.Api --launch-profile http
```

Keep the credentials available through your local secret manager or environment in the terminal used for the API commands. Do not regenerate them when reusing an existing database: seeding does not change existing passwords, roles or enable flags. Missing/invalid seed configuration fails startup; existing accounts with unexpected roles also fail bootstrap instead of being promoted. Disable seeding after initial bootstrap with `DevelopmentSeed__Enabled=false`. Migrations are explicit and never run automatically on application startup. Phase 01 supplies Identity tables (including `AspNetUsers.IsEnabled`) and EF migration history; applying all current migrations also adds the Phase 02 domain tables.

If the default CLI home/cache is not writable, use the shell-local `.local` settings in the root README before running dotnet commands.

## Repeatable API checks

Leave the host running. In a second PowerShell terminal, supply the same development credentials through hidden prompts (or your local secret manager), then run the API commands:

```powershell
$base = 'http://localhost:5165'
$env:DevelopmentSeed__UserEmail = 'user@example.test'
$env:DevelopmentSeed__AdminEmail = 'admin@example.test'
$env:DevelopmentSeed__UserPassword = [System.Net.NetworkCredential]::new('', (Read-Host 'Development user password' -AsSecureString)).Password
$env:DevelopmentSeed__AdminPassword = [System.Net.NetworkCredential]::new('', (Read-Host 'Development admin password' -AsSecureString)).Password
$userSession = New-Object Microsoft.PowerShell.Commands.WebRequestSession
$adminSession = New-Object Microsoft.PowerShell.Commands.WebRequestSession

function Get-ApiStatus($path, $session) {
    try {
        (Invoke-WebRequest -UseBasicParsing -Uri "$base$path" -WebSession $session).StatusCode
    } catch {
        if ($null -eq $_.Exception.Response) { throw }
        [int]$_.Exception.Response.StatusCode
    }
}
function Invoke-Login($session, $email, $password) {
    $token = (Invoke-RestMethod -Uri "$base/api/auth/csrf" -WebSession $session).token
    $body = @{ email = $email; password = $password } | ConvertTo-Json
    try {
        (Invoke-WebRequest -UseBasicParsing -Uri "$base/api/auth/login" -Method Post `
            -WebSession $session -Headers @{ 'X-CSRF-TOKEN' = $token } `
            -ContentType 'application/json' -Body $body).StatusCode
    } catch {
        if ($null -eq $_.Exception.Response) { throw }
        [int]$_.Exception.Response.StatusCode
    }
}

Get-ApiStatus '/api/auth/me' $userSession                   # 401
Get-ApiStatus '/api/admin/probe' $userSession               # 401
Invoke-Login $userSession $env:DevelopmentSeed__UserEmail $env:DevelopmentSeed__UserPassword # 204
Get-ApiStatus '/api/auth/me' $userSession                   # 200
Get-ApiStatus '/api/admin/probe' $userSession               # 403
Invoke-Login $adminSession $env:DevelopmentSeed__AdminEmail $env:DevelopmentSeed__AdminPassword # 204
Get-ApiStatus '/api/auth/me' $adminSession                  # 200
Get-ApiStatus '/api/admin/probe' $adminSession              # 200
Invoke-RestMethod "$base/api/auth/me" -WebSession $adminSession # id, email, isAdmin=true
```

`GET /api/auth/me` is the session check. Login returns a generic 401 for unknown accounts, wrong passwords, lockouts and disabled accounts. Five incorrect passwords lock an account for five minutes. No registration route is mapped (`POST /api/auth/register` returns 404).

Login and logout both require a CSRF cookie plus the associated request token in `X-CSRF-TOKEN`. Retrieve a fresh token after changing the authenticated identity. Missing/invalid tokens return 400. Verify logout with the logged-in admin session:

```powershell
try {
    Invoke-WebRequest -UseBasicParsing "$base/api/auth/logout" -Method Post -WebSession $adminSession
} catch { [int]$_.Exception.Response.StatusCode } # 400; session still valid
$token = (Invoke-RestMethod "$base/api/auth/csrf" -WebSession $adminSession).token
(Invoke-WebRequest -UseBasicParsing "$base/api/auth/logout" -Method Post `
    -WebSession $adminSession -Headers @{ 'X-CSRF-TOKEN' = $token }).StatusCode # 204
Get-ApiStatus '/api/auth/me' $adminSession # 401
```

To prove immediate disabled-user enforcement, keep the user's authenticated session from above and execute this SQL in your database client. For the optional disposable container and the example email:

```powershell
'UPDATE "AspNetUsers" SET "IsEnabled" = false WHERE "Email" = ''user@example.test'';' |
    docker exec --interactive alerts-phase01-dev psql -U postgres -d alerts
Get-ApiStatus '/api/auth/me' $userSession # 401 with the existing cookie
Invoke-Login $userSession $env:DevelopmentSeed__UserEmail $env:DevelopmentSeed__UserPassword # 401
```

This direct database update is a validation step; user management APIs are not part of Phase 01. After this check, sign in again if you re-enable the account: the client's cookie has been cleared. Stop the host with Ctrl+C, then remove the optional disposable database with `docker stop alerts-phase01-dev`.

## Automated checks and security details

```powershell
dotnet build Alerts.sln --no-restore
dotnet test Alerts.sln --no-build
dotnet format Alerts.sln --verify-no-changes --no-restore
```

The tests use EF InMemory only as an isolated Identity store, not as evidence of PostgreSQL schema/constraint behavior. Focused in-process HTTP tests exercise real Identity password hashing, cookies, antiforgery and authorization without a socket listener. Seeding tests verify repeatability, role boundaries, configuration checks and the Development restriction. PostgreSQL migration/application behavior is validated manually.

Cookies are HttpOnly and SameSite=Strict, with an eight-hour ticket lifetime and sliding expiration disabled. Production cookies require HTTPS; Development permits HTTP on localhost (cookies still use Secure when accessed through HTTPS). Outside Development the host enables HTTPS redirection/HSTS; configure the HTTPS endpoint or trusted TLS proxy before deployment. No CORS/cross-origin login is configured. Passwords and CSRF tokens must stay out of URLs/logs.

Each authenticated request validates the Identity security stamp, current roles and current `IsEnabled` value against the database. This intentionally costs database reads to ensure disablement takes effect on the next request. It also rejects deleted accounts and invalidated security stamps. An in-flight request is not cancelled by a later disablement. Future protected endpoints must use the User/Admin policies, and state-changing cookie-authenticated endpoints must validate antiforgery tokens (the reusable `ValidateAntiforgeryFilter` is available). Production data-protection key persistence and Docker runtime configuration remain deployment work in their assigned phases.

Framework references: [Identity/cookie principal validation](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/cookie?view=aspnetcore-10.0), [ASP.NET Core antiforgery](https://learn.microsoft.com/en-us/aspnet/core/security/anti-request-forgery?view=aspnetcore-10.0), [Npgsql EF Core 10](https://www.npgsql.org/efcore/release-notes/10.0.html).
