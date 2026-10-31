# Phase 07 - Personal and admin HTTP APIs

Phase 07, including the Minimal API boundary split, was human approved on 2026-10-31. No Angular implementation is included. Start the local Development stack using the root README and Phase 03 guide. Development-only OpenAPI JSON is at `GET /openapi/v1.json`; import it into an API client. Outside Development the route is absent. It uses the [supported ASP.NET Core OpenAPI generator](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/openapi/aspnetcore-openapi?view=aspnetcore-10.0); no Swagger UI dependency is needed.

The Minimal API HTTP boundary is split into `PersonalSettingsEndpoints` for `/api/me` and `AdministrationEndpoints` for `/api/admin`, registered independently in `Program.cs`. Shared catalog reads, HTTP metadata/error helpers and cache filtering live in `ManagementEndpointSupport.cs`; request/response records live in `ManagementContracts.cs`. This organization preserves the routes, wire contracts and security policies described below.

## Contract

All management routes require a current enabled Identity session. Every PUT/DELETE additionally requires the antiforgery cookie and `X-CSRF-TOKEN`. Obtain a fresh token from `GET /api/auth/csrf` after login. GET responses use `Cache-Control: no-store`. Admins retain all personal capabilities.

| Method | Route | Behavior |
|---|---|---|
| GET | `/api/me/topics` | All topics: `id`, stable `code`, `displayName`, `isEnabled` |
| GET | `/api/me/channels` | All channels with the same safe fields |
| GET | `/api/me/selections` | Independent `topicIds` and `channelIds` arrays |
| PUT / DELETE | `/api/me/topics/{id}` | Add/remove own topic; no body |
| PUT / DELETE | `/api/me/channels/{id}` | Add/remove own channel; no body |
| GET | `/api/admin/users` | Users: only `id`, `email`, `isEnabled` |
| PUT | `/api/admin/users/{id}/enabled` | Body: `{"isEnabled":false}` or `true` |
| DELETE | `/api/admin/users/{id}` | Hard-delete user with approved cascades |
| GET | `/api/admin/topics` | List all global topics |
| GET | `/api/admin/channels` | List all global channels |
| PUT | `/api/admin/topics/{id}/enabled` | Set global flag with explicit boolean body |
| PUT | `/api/admin/channels/{id}/enabled` | Set global flag with explicit boolean body |

The server derives personal ownership from the authenticated session; a supplied `userId` cannot redirect the operation. No route edits another user's preferences, changes roles, creates accounts/catalogs or changes stable codes. No topic/channel pair exists in the request model. Seed IDs: Finance=1, Politics=2, Weather=3; Email=1, Slack=2. Read catalogs for their actual current state.

Example `GET /api/me/topics` response:

```json
[{"id":1,"code":"finance","displayName":"Finance","isEnabled":false},{"id":2,"code":"politics","displayName":"Politics","isEnabled":true},{"id":3,"code":"weather","displayName":"Weather","isEnabled":true}]
```

Example selections response: `{"topicIds":[1,3],"channelIds":[1,2]}`. These are independent lists; each eligible selected topic routes through every enabled selected channel. Empty either list means no deliveries.

Disabled items remain visible with `isEnabled:false`; clients should mark them unavailable. Existing selections remain stored. Repeating an existing PUT is a 204 even when disabled, but adding a new selection while disabled returns 409 `{"error":"item_disabled"}`. Removal is allowed while disabled; repeating removal is a 204. Re-enabling does not auto-select anything or retroactively route old events. Global flags are enforced by dispatch, independent of catalog visibility.

Mutations return 204 on success; unknown IDs return 404 `{"error":"not_found"}`. Nonpositive catalog IDs return 400 `invalid_id`; missing/null `isEnabled` returns 400 `is_enabled_required`. Malformed integer/JSON/boolean input receives the framework's 400 response. Missing/invalid CSRF returns 400 with `{"error":"Invalid CSRF token."}`. Unauthenticated/disabled/deleted sessions receive 401; regular users receive 403 on admin routes. Admin self-disable and self-delete return 409 `self_disable_forbidden` and `self_delete_forbidden`. Identity update/delete conflicts return 409 `user_change_conflict`. Custom errors use an `error` field; framework/auth failures need not contain JSON.

User deletion cascades personal selections, deliveries and Identity-dependent records. Shared events, topics, channels and other users remain. This is irreversible. Disablement blocks subsequent authenticated requests and login; it does not cancel an already executing request/send. Settings operations and dispatch follow the existing single-process behavior, without distributed coordination. Admins may manage other admins; no additional last-admin rule was requested. User lists are intentionally unpaginated for this small MVP.

## API-client validation

Use only a disposable development user for the final deletion step. With `.env` configured for seeded admin/user accounts, run `docker compose --profile channels up --build --detach --wait --wait-timeout 180`. Do not print resolved secret configuration. Keep source polling disabled until preferences/destinations are configured.

In PowerShell, log in to two separate cookie sessions. Supply the same credentials you configured when the accounts were created:

```powershell
$base = 'http://localhost:5165'
$userSession = New-Object Microsoft.PowerShell.Commands.WebRequestSession
$adminSession = New-Object Microsoft.PowerShell.Commands.WebRequestSession
function Login-Api($session, $email) {
    $password = [System.Net.NetworkCredential]::new('', (Read-Host "Password for $email" -AsSecureString)).Password
    $token = (Invoke-RestMethod "$base/api/auth/csrf" -WebSession $session).token
    $body = @{email=$email;password=$password} | ConvertTo-Json
    Invoke-WebRequest -UseBasicParsing "$base/api/auth/login" -Method Post -WebSession $session `
        -Headers @{'X-CSRF-TOKEN'=$token} -ContentType application/json -Body $body | Select-Object StatusCode
}
function Set-Api($session, $method, $path, $body = $null) {
    $token = (Invoke-RestMethod "$base/api/auth/csrf" -WebSession $session).token
    $parameters = @{Uri="$base$path";Method=$method;WebSession=$session;UseBasicParsing=$true;Headers=@{'X-CSRF-TOKEN'=$token}}
    if ($null -ne $body) { $parameters.ContentType='application/json'; $parameters.Body=($body | ConvertTo-Json) }
    try { Invoke-WebRequest @parameters | Select-Object StatusCode }
    catch { if ($null -eq $_.Exception.Response) { throw }; [int]$_.Exception.Response.StatusCode }
}
Login-Api $userSession 'user@example.test'   # 204; use your configured email
Login-Api $adminSession 'admin@example.test' # 204; use your configured email
Invoke-RestMethod "$base/api/me/topics" -WebSession $userSession
Invoke-RestMethod "$base/api/me/channels" -WebSession $userSession
Set-Api $userSession Put '/api/me/topics/1'   # 204
Set-Api $userSession Put '/api/me/topics/3'   # 204
Set-Api $userSession Put '/api/me/channels/1' # 204
Set-Api $userSession Put '/api/me/channels/2' # 204
Invoke-RestMethod "$base/api/me/selections" -WebSession $userSession # [1,3] / [1,2]
Set-Api $userSession Delete '/api/me/channels/2' # 204; topicIds unchanged
Set-Api $userSession Put '/api/admin/topics/1/enabled' @{isEnabled=$false} # 403
Set-Api $adminSession Put '/api/me/topics/3' # 204; admin has personal access
Set-Api $adminSession Put '/api/admin/topics/1/enabled' @{isEnabled=$false} # 204
Invoke-RestMethod "$base/api/me/topics" -WebSession $userSession # Finance visible, disabled
Set-Api $userSession Delete '/api/me/topics/1' # 204
Set-Api $userSession Put '/api/me/topics/1'    # 409
Set-Api $adminSession Put '/api/admin/topics/1/enabled' @{isEnabled=$true} # 204
Set-Api $userSession Put '/api/me/topics/1'    # 204
$users = Invoke-RestMethod "$base/api/admin/users" -WebSession $adminSession
$users # only id/email/isEnabled
$selfId = (Invoke-RestMethod "$base/api/auth/me" -WebSession $adminSession).id
Set-Api $adminSession Put "/api/admin/users/$selfId/enabled" @{isEnabled=$false} # 409
Set-Api $adminSession Delete "/api/admin/users/$selfId" # 409
```

Also call `GET /api/admin/users` as the regular user (403), attempt a PUT without CSRF (400), try an unknown ID (404), and verify the other account's selections are unchanged. Admin global channel controls use the same enablement body as topics. For a disposable target:

```powershell
$testUserId = ($users | Where-Object email -eq 'user@example.test').id # verify your disposable target
if (!$testUserId) { throw 'Test user missing' }
Set-Api $adminSession Put "/api/admin/users/$testUserId/enabled" @{isEnabled=$false} # 204
# Existing user's GET /api/me/selections and login now receive 401.
Set-Api $adminSession Put "/api/admin/users/$testUserId/enabled" @{isEnabled=$true} # 204
Login-Api $userSession 'user@example.test' # 204 again
```

To verify real dispatch suppression, use the fixture publishing steps in [Phase 06](phase06-source-worker.md), with a new external ID for each case and an enabled, selected topic/channel/user. Disable that topic, channel or user through its admin API, publish a new event, and check no delivery was created for the disabled recipient/channel. Re-enable, publish a different ID, and verify one eligible delivery. Configure Email/Mailpit to avoid real providers. Existing stored selections must remain unchanged. Restore flags after checking. Known IDs do not reroute; old pending deliveries retain existing recovery behavior.

Before deleting the disposable user, inspect its dependent rows using your PostgreSQL client. This Compose query shows the development user's data without exposing credential columns (replace the example email locally):

```powershell
$sql = @'
SELECT u."Id", u."Email",
 (SELECT count(*) FROM "UserTopicSelections" t WHERE t."UserId"=u."Id") AS topic_selections,
 (SELECT count(*) FROM "UserChannelSelections" c WHERE c."UserId"=u."Id") AS channel_selections,
 (SELECT count(*) FROM "Deliveries" d WHERE d."UserId"=u."Id") AS deliveries
FROM "AspNetUsers" u WHERE u."Email"='user@example.test';
SELECT count(*) AS shared_events FROM "AlertEvents";
SELECT count(*) AS topics FROM "Topics";
SELECT count(*) AS channels FROM "Channels";
'@
$sql | docker compose exec -T db psql -U alerts -d alerts --set ON_ERROR_STOP=1 -f -
Set-Api $adminSession Delete "/api/admin/users/$testUserId" # 204; irreversible test-user deletion
$sql | docker compose exec -T db psql -U alerts -d alerts --set ON_ERROR_STOP=1 -f -
```

After deletion, the target is absent and its selections/deliveries are removed; shared counts and other accounts remain. The old user session receives 401. Inspect by the captured ID if independently checking orphan counts. A seed-enabled restart may recreate the deleted development account with a different ID; finish deletion checks before restarting, or disable seeding.

## Automated and executed validation

```powershell
dotnet restore Alerts.sln
dotnet build Alerts.sln --no-restore
dotnet test Alerts.sln --no-build
dotnet format Alerts.sln --verify-no-changes --no-restore
# Focused review:
dotnet test tests/Alerts.UnitTests --no-build --filter FullyQualifiedName~ManagementTests
```

Tests use TestServer/real Identity cookies and isolated EF InMemory stores, without network providers or E2E suites. They cover every management route's authorization, every mutation's CSRF, session ownership for user/admin, independent/idempotent preferences, disabled catalog visibility/new-selection rejection/removal, validation, safe user fields, self-action guards, user enable/delete session enforcement, cascade model configuration, API flags affecting real dispatcher eligibility, and Development-only OpenAPI generation.

On 2026-10-31 an isolated `alerts-phase07` Compose stack with fresh PostgreSQL, local Mailpit, API port 15175 and Mailpit port 18035 passed the API checks above. Polling a new event with a disabled topic stored the event with zero deliveries/messages; a new ID after enablement created one delivery/Mailpit message. HTTP deletion returned 204, then the existing deleted-user session returned 401. Actual SQL counts changed from users=2/selections=1+1/deliveries=1 to users=1/selections=0+0/deliveries=0, retaining events=2/topics=3/channels=2. This is manual local validation, not an added provider integration suite. See the implementation status log for final automated results and limitations.

Phase 07 approval is recorded in `codex_alerts_plan/IMPLEMENTATION_STATUS.md`. Phase 08 remains NOT STARTED and requires a separate explicit implementation request.
