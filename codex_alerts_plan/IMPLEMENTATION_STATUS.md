# Implementation status / human review log

**Current phase:** 01 — AWAITING HUMAN VALIDATION. Phase 00 remains APPROVED. No subsequent phase has been started.

Append for each phase:

## Phase NN — <title>
- Status: `AWAITING HUMAN VALIDATION` / `APPROVED` / `CHANGES REQUESTED`
- Scope delivered:
- Changed files:
- Tests executed and outcomes:
- Manual verification steps and results:
- Known limitations / tradeoffs:
- Developer questions / decisions:
- Developer approval (quote or reference; do not fabricate):

Only developer-provided approval may change the phase to `APPROVED`.

## Phase 00 — Scaffold and architecture contract

- Status: `APPROVED` — human validated on 2026-10-31.
- Scope delivered: buildable .NET 10 solution with one ASP.NET Core backend, Domain/Application/Infrastructure folder boundaries, one xUnit project, empty Angular placeholder, documented vocabulary and intended contracts, nonsecret configuration example, build/run instructions and SDK selection. The host has no application endpoints. No later-phase behavior is implemented.
- Changed files:
  - Root: `Alerts.sln`, `global.json`, `.gitignore`, `.env.example`, `README.md`.
  - Backend: `src/Alerts.Api/Alerts.Api.csproj`, `Program.cs`, `appsettings.json`, `appsettings.Development.json`, `Properties/launchSettings.json`, and boundary documents at `Domain/README.md`, `Application/README.md`, `Infrastructure/README.md`.
  - Tests: `tests/Alerts.UnitTests/Alerts.UnitTests.csproj`, `HostSmokeTests.cs`.
  - Frontend placeholder: `frontend/.gitkeep`.
  - Planning records: `codex_alerts_plan/PROJECT_CONTEXT.md`, this status log.
- Tests executed and outcomes:
  - .NET SDK `10.0.403`; shell-local `DOTNET_CLI_HOME` and `NUGET_PACKAGES` point under ignored `.local/` because the default CLI home is not writable in this sandbox.
  - `dotnet restore Alerts.sln`: initial attempt failed with NU1301 because the sandbox blocked NuGet network access. Retried with approved network escalation; passed.
  - `dotnet build Alerts.sln --no-restore`: passed, zero warnings and zero errors.
  - `dotnet test Alerts.sln --no-build`: passed, 1 test, 0 failures, 0 skipped. Smoke test builds/disposes the host and checks command-line configuration and logging DI; it opens no network listener.
  - `dotnet format Alerts.sln --verify-no-changes --no-restore`: passed, exit 0.
  - `git -c safe.directory=D:/Temp/Sonrisa diff --check`: passed. Git requires a command-local safe-directory override because the checkout has a different owner; no global Git setting was changed. The environment also emits a warning about an unreadable user Git ignore file.
- Manual verification steps and results:
  1. From `D:\Temp\Sonrisa`, use .NET SDK 10.0.403 or a later stable .NET 10 SDK. If needed in a restricted environment, set `$env:DOTNET_CLI_HOME = Join-Path $PWD '.local/dotnet'` and `$env:NUGET_PACKAGES = Join-Path $PWD '.local/nuget/packages'` first.
  2. Run `dotnet restore Alerts.sln`, `dotnet build Alerts.sln --no-restore`, `dotnet test Alerts.sln --no-build`, and `dotnet format Alerts.sln --verify-no-changes --no-restore`. All passed in this environment following the restore retry.
  3. Run `dotnet run --project src/Alerts.Api --launch-profile http`. In another terminal run `curl.exe -i http://localhost:5165/`; expect HTTP 404 because the host has no endpoints. The equivalent startup/HTTP check was executed successfully with `--no-build`; the host was then stopped. Stop your manual run with Ctrl+C.
  4. Review the tree and boundaries in root `README.md` and the three backend boundary documents. Confirm independent topic/channel preferences and the intended source/sender/routing responsibilities.
  5. Review `.env.example` and appsettings files: only nonsecret defaults and a commented placeholder connection string are supplied. No database is required or contacted.
- Known limitations / tradeoffs:
  - Folder boundaries in one backend project are conventions, not compile-time dependency restrictions. This follows the accepted small-project layout.
  - Contracts and vocabulary are documented rather than compiled interfaces/entities, avoiding premature source payload, recipient, identifier and schema decisions. No fake senders or placeholder behavior are supplied.
  - PostgreSQL is agreed but no provider package, schema or migrations are added yet. `/health` and Compose remain Phase 03 work.
  - Angular 22 remains an empty folder placeholder. Node.js 18.14.2 must be upgraded before Phase 08; recheck supported versions and perform the update if feasible, otherwise remind the developer. No Node upgrade is needed for this phase.
  - Automated coverage is deliberately limited to skeleton host composition. Product unit tests arrive with product behavior in later phases.
- Developer questions / decisions:
  - Developer: "Database: PostgresSQL" — recorded as PostgreSQL.
  - Developer: "Versions: .NET 10 LTS and Angular22 are good, remind me of the node update at the appropriate time if you cannot handle it on your own".
  - Developer: "Layout: Acceptable" — root `D:\Temp\Sonrisa`, one backend project with three boundary folders and one unit-test project.
  - No blocking Phase 00 questions remain. Authentication/bootstrap, disabled-user access, deletion/history, delivery destinations and mock payload decisions remain for their assigned phases.
- Developer approval (2026-10-31): "phase 00 is valid, update the implementation status to validated and wait further instructions before starting phase 01". Phase 00 is human validated and approved. No commit created. Phase 01 has not been started; wait for explicit further instructions.

## Phase 01 — Authentication and authorization

- Status: `AWAITING HUMAN VALIDATION` — implementation and local verification completed on 2026-10-31; no phase approval recorded.
- Scope delivered:
  - ASP.NET Core Identity with PostgreSQL EF persistence, `ApplicationUser.IsEnabled`, Admin role and minimal Identity-only migration. EF CLI is pinned in a repository-local tool manifest. Domain schema remains Phase 02 work.
  - Same-origin cookie login/logout, CSRF token endpoint, protected session/user proof (`GET /api/auth/me`) and server-protected admin probe (`GET /api/admin/probe`). No public registration route or account management features.
  - HttpOnly/SameSite=Strict cookies; Secure outside Development and on HTTPS in Development. Nonpersistent login, eight-hour configured ticket lifetime, sliding expiration disabled, Identity password hashing and five-failure/five-minute lockout.
  - Login rejects disabled accounts. Every authenticated request checks persisted enablement and Identity security stamps/current roles, invalidating disabled/deleted accounts and changed security stamps immediately on the next request. Admin retains normal user access.
  - Opt-in Development-only seeded admin/user credentials from explicit configuration/environment. Reruns preserve passwords, role assignments and enablement; unexpected existing roles fail bootstrap rather than promoting accounts. No hardcoded shipped credentials.
- Changed files:
  - Host/packages: `src/Alerts.Api/Program.cs`, `src/Alerts.Api/Alerts.Api.csproj`, `dotnet-tools.json`.
  - API/policies: `src/Alerts.Api/AuthEndpoints.cs`, `src/Alerts.Api/Application/AccessPolicies.cs`.
  - Identity infrastructure: `src/Alerts.Api/Infrastructure/Identity/ApplicationUser.cs`, `AlertsIdentityDbContext.cs`, `EnabledSignInManager.cs`, `EnabledCookieEvents.cs`, `IdentitySetup.cs`, `DevelopmentAccountSeeder.cs`.
  - Generated schema: `src/Alerts.Api/Infrastructure/Identity/Migrations/20261031111316_InitialIdentity.cs`, its `.Designer.cs`, and `AlertsIdentityDbContextModelSnapshot.cs`.
  - Tests: `tests/Alerts.UnitTests/Alerts.UnitTests.csproj`, `AuthTests.cs`, `DevelopmentSeedTests.cs`; existing host smoke test retained.
  - Documentation/configuration: `.env.example`, `README.md`, `docs/phase01-auth.md`, `codex_alerts_plan/PROJECT_CONTEXT.md`, this log.
- Tests executed and outcomes:
  - SDK `10.0.403`, runtime `10.0.14`; Identity/EF/test packages and EF tool `10.0.14`, Npgsql EF provider `10.0.3`. Shell-local CLI home/cache under ignored `.local/`.
  - `dotnet restore Alerts.sln`: initial sandbox attempt failed with NU1301/socket access denial; approved network retry passed. EF tool install had the same network restriction and passed on approved retry.
  - `dotnet build Alerts.sln --no-restore`: passed with zero warnings/errors.
  - `dotnet test Alerts.sln --no-build`: 12 passed, zero failed/skipped. Coverage includes anonymous denial, role boundaries, admin user access, cookie attributes, CSRF login/logout, logout, wrong/unknown login, lockout, disabled sessions/login, deleted users, changed security stamp, role removal, seed repeatability and Development/configuration restrictions.
  - Initial HTTP test run failed on sandbox-restricted Windows EventLog access; the in-process fixture now clears logging providers and uses ephemeral data-protection keys. The full suite subsequently passed. No product behavior was bypassed.
  - `dotnet format Alerts.sln --verify-no-changes --no-restore`: passed after formatting.
  - `git -c safe.directory=D:/Temp/Sonrisa diff --check`: passed. Command-local ownership override only; the existing unreadable user Git-ignore warning remains environmental.
  - `dotnet ef migrations add InitialIdentity --project src/Alerts.Api --output-dir Infrastructure/Identity/Migrations`: generated successfully. `dotnet ef migrations script --idempotent --project src/Alerts.Api --output .local/identity-migration.sql`: passed.
  - Manual PostgreSQL 17 validation in a temporary local Docker container: `dotnet ef database update --project src/Alerts.Api --no-build` applied successfully to a fresh database; repeat reported already up to date. Initial EF history-table lookup logged a missing-table command before successful initialization (normal fresh-database bootstrap). `dotnet ef migrations has-pending-model-changes --project src/Alerts.Api --no-build`: no model changes.
  - No E2E suite or external-provider integration tests introduced. In-process HTTP tests use real framework auth with EF InMemory only as an isolated test store; they do not claim relational guarantees.
- Manual verification steps and results:
  - Exact setup/seed, migration, PowerShell API, CSRF/logout, SQL-disable and cleanup commands: `docs/phase01-auth.md`. Build/test/format commands above are repeatable from the repository root. Configure `ConnectionStrings__Alerts`, migrate explicitly, set `DevelopmentSeed__Enabled=true` plus admin/user emails and unique passwords, then `dotnet run --project src/Alerts.Api --launch-profile http`.
  - Executed actual seeded backend HTTP checks against PostgreSQL on localhost: anonymous `me` 401; regular-user login 204, `me` 200, admin probe 403; admin login 204, `me` 200, admin probe 200.
  - Directly changed the disposable regular user's `IsEnabled` to false in PostgreSQL: existing-session `me` returned 401; fresh login returned 401. No user-management API was added for this validation.
  - Backend stopped and disposable validation container removed. Generated validation passwords were confined to ignored `.local/` files/environment and removed after validation. No credentials committed or returned in API/log output.
- Known limitations / tradeoffs:
  - Per-request security-stamp/role/enablement checks incur database reads by design. Disablement affects the next request, not an already executing request. Database unavailability does not permit authenticated access.
  - Development permits localhost HTTP for repeatable API validation; deployment requires HTTPS and appropriate hosting/key-persistence configuration in the assigned runtime phase. No cross-origin deployment or CORS policy is supplied.
  - Migrations are explicit; bootstrap does not silently migrate. Development seed credentials must be supplied again on seed-enabled startups; existing passwords are never reset. Seed failures can leave partially created development records, requiring configuration/database review before retry.
  - No public registration, admin invitation/account CRUD, domain models, Compose, health endpoint, frontend or delivery behavior. Remaining product phases are untouched.
- Developer questions / decisions:
  - Requested filename `phases/01-skeleton.md` was absent; developer confirmed using the existing `phases/01-auth.md` ("Scope: yes").
  - Accounts: "that is right, user registration will be invited/added by admin only the MVP does not include user registration". Development admin/user seeds approved; invitation/admin management remains later work.
  - Authentication: "approved" — same-origin cookie approach.
  - Disabled user: "disabling a user should block new requests as well as login processes."
  - No blocking Phase 01 questions remain. These decisions authorize implementation; they are not milestone acceptance.
- Developer approval: **Pending.** Request explicit validation or Phase 01 feedback. Stop here; no subsequent phase is authorized or implemented, and no git commit was created.
