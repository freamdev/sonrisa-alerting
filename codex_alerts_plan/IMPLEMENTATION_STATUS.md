# Implementation status / human review log

**Current phase:** 07 - AWAITING HUMAN VALIDATION. Phases 00 through 06 remain APPROVED. No subsequent phase is authorized or implemented. The post-MVP Slack full-content/multiple-message validation item remains deferred.

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

- Status: `APPROVED` — human tests confirmed by the developer on 2026-10-31.
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
- Developer approval (2026-10-31): "Phase 01 human tests are confirmed, update the implementation status". Phase 01 is human validated and approved. No subsequent phase is authorized or implemented, and no git commit was created. Await explicit instructions before starting Phase 02.

## Phase 02 — Domain persistence and migrations

- Status: `APPROVED` — manual testing completed and approved by the developer on 2026-10-31, including the separate-model-file review correction.
- Scope delivered:
  - Extended the existing PostgreSQL Identity context and migration chain with Topic, Channel, independent UserTopicSelection/UserChannelSelection, AlertEvent and Delivery. Existing `ApplicationUser.IsEnabled` and authentication behavior retained.
  - Fixed catalog seeds: Finance, Politics, Weather; Email and Slack. Stable unique codes and global enable flags. Seeds create no personal selections and do not reset catalog enablement on migration reruns/startup.
  - Composite selection primary keys, unique external event ID, unique event/user/channel delivery, FK indexes, delivery status/attempt index and DB checks for status values and attempts 0..3. UTC timestamps and bounded error-summary storage; no routing or retry engine yet.
  - Scoped internal `ISelectionService`/EF implementation for reading and independently adding/removing selections. Repeated sequential operations are idempotent; invalid user/catalog references are rejected. Global enablement does not rewrite personal preferences.
  - Approved hard-delete policy enforced through user FK cascades for personal selections and deliveries; shared events/topics/channels retained. Referenced shared records use restrictive deletion.
- Changed files:
  - Models/contracts: individual `src/Alerts.Api/Domain/Topic.cs`, `Channel.cs`, `UserTopicSelection.cs`, `UserChannelSelection.cs`, `AlertEvent.cs`, `Delivery.cs`, standalone `DeliveryStatus.cs`, and `src/Alerts.Api/Application/ISelectionService.cs`.
  - Persistence/DI: `src/Alerts.Api/Infrastructure/SelectionService.cs`, `Infrastructure/Identity/AlertsIdentityDbContext.cs`, `CatalogConfigurations.cs`, `IdentitySetup.cs`.
  - Migration: `src/Alerts.Api/Infrastructure/Identity/Migrations/20261031113846_DomainPersistence.cs`, its `.Designer.cs`, and the existing model snapshot.
  - Tests: `tests/Alerts.UnitTests/SelectionTests.cs` (five new tests).
  - Documentation: `docs/phase02-data.md`, `docs/phase02-validation.sql`, `docs/phase01-auth.md`, root `README.md`, Domain/Application/Infrastructure boundary READMEs, `codex_alerts_plan/PROJECT_CONTEXT.md`, this log.
- Tests executed and outcomes:
  - Existing cached dependencies/tool versions retained; no package changes or new restore required. Shell-local `DOTNET_CLI_HOME`/`NUGET_PACKAGES` under ignored `.local/`.
  - `dotnet build Alerts.sln --no-restore`: final run passed, zero warnings/errors. An initial misplaced using directive was corrected before the passing builds.
  - `dotnet test Alerts.sln --no-build`: final run passed, 17 tests, zero failures/skips. Includes all 12 existing auth/seed/host cases plus independent persistence across contexts, repeatability/user isolation, invalid references, global flags, catalog seeds and initial delivery state. EF InMemory verifies use cases only, not relational integrity.
  - `dotnet format Alerts.sln --no-restore` applied formatting; `dotnet format Alerts.sln --verify-no-changes --no-restore` passed, exit 0.
  - `dotnet ef migrations add DomainPersistence --project src/Alerts.Api --output-dir Infrastructure/Identity/Migrations --no-build`: generated successfully after supplying the required connection-string configuration; generation does not connect to the database.
  - Disposable PostgreSQL 17 container: `dotnet ef database update --project src/Alerts.Api --no-build` applied both migrations to a fresh DB. Repeat passed with already-up-to-date result; `dotnet ef migrations has-pending-model-changes --project src/Alerts.Api --no-build` reported no changes. The first history-table lookup logged a missing table before successful fresh initialization.
  - `dotnet ef migrations script --idempotent --project src/Alerts.Api --no-build --output .local/phase02-migrations.sql`: passed. Executed the generated SQL twice against the migrated database: both passed without duplicates.
  - `Get-Content -Raw docs/phase02-validation.sql | docker exec --interactive alerts-phase02-validation psql -U postgres -d alerts`: passed. Checks catalog seeds, independent adding/removing, unique codes/selections/events/deliveries, FKs, attempts -1/4 rejection and 3 acceptance, invalid status rejection, restrictive shared-record deletion and user cascades preserving another user's delivery. All row changes rolled back. An initial PostgreSQL array-type comparison in the validation script was corrected with explicit text casts, then the complete script passed.
  - Real seeded backend against migrated PostgreSQL: regular user login 204, `me` 200, admin probe 403; admin login 204, `me` 200, admin probe 200. Host stopped, disposable container removed and temporary connection-string file deleted. Random validation credentials were never committed or printed.
  - Docker/loopback checks used approved sandbox escalation; no Compose, E2E suite or external-provider integration tests introduced.
  - `git -c safe.directory=D:/Temp/Sonrisa diff --check`: passed. Ownership override remains command-local; existing user-ignore permission/line-ending warnings are environmental.
- Manual verification steps and results:
  - Exact PowerShell setup, migrate/rerun, build/test/format, idempotent SQL and rollback-only schema validation commands: `docs/phase02-data.md`. All substantive checks above executed successfully.
  - Inspect the migration/schema and independent selection tables; run `docs/phase02-validation.sql` against a fresh migrated validation DB. Expect its PASS notice. The application-service add/remove demonstration is `SelectionTests.SelectionsAreIndependentRepeatablePersistedAndIsolatedByUser`.
  - Repeat Phase 01 credential/bootstrap/API checks using the Phase 02 DB connection/container, as described in the Phase 02 guide. Existing disabled-session, lockout, CSRF and role-change checks remain covered by the automated auth suite.
- Known limitations / tradeoffs:
  - One existing Identity-named context owns the complete schema, minimizing churn and preserving the migration chain. Domain models have no EF/framework dependencies.
  - Catalog reference seeds apply in every environment through explicit migrations. They create no accounts or preferences; account bootstrap remains Development-only/opt-in.
  - Internal selection operations take user IDs; future HTTP endpoints must derive these from the principal and enforce authorization/CSRF. Concurrent duplicate adds can raise a DB uniqueness conflict; sequential reruns are idempotent.
  - DB constraints validate delivery storage only; state transitions, actual send attempt limits, safe provider-error mapping and enablement enforcement at send time remain Phase 04/05 work. No sender currently writes error data.
  - No APIs for selections/deletion, no actual event ingestion/routing, senders, polling, UI, health or Compose. No later phase started. Rolling back the domain migration drops domain data; migrations are explicit.
- Developer questions / decisions:
  - Developer (2026-10-31): "The plan and the user deletion policy is approved, proceed with the implementation". This approves implementation and hard-delete cascades for selections/deliveries while retaining shared events/topics/channels; it does not approve the completed milestone.
  - No unresolved blocking Phase 02 questions or scope deviations. Delivery destinations and external source contract remain future-phase decisions.
- Developer approval (2026-10-31): "The phase manual testing is done and approved, update the status files". Phase 02 is human validated and approved. No commit created; await explicit instructions before starting Phase 03.

### Phase 02 review correction — separate model files (2026-10-31)

- Developer requested separate files for persistence models and enums for readability.
- Replaced `Domain/PersistenceModels.cs` with `Topic.cs`, `Channel.cs`, `UserTopicSelection.cs`, `UserChannelSelection.cs`, `AlertEvent.cs`, `Delivery.cs`, and standalone `DeliveryStatus.cs`. Namespaces, members and persistence behavior are unchanged.
- Verification: `dotnet build Alerts.sln --no-restore` passed with zero warnings/errors; `dotnet test Alerts.sln --no-build` passed all 17 tests; `dotnet format Alerts.sln --verify-no-changes --no-restore` passed; `dotnet ef migrations has-pending-model-changes --project src/Alerts.Api --no-build` reported no model changes; `git -c safe.directory=D:/Temp/Sonrisa diff --check` passed.
- This correction was initially presented as `AWAITING HUMAN VALIDATION` and is included in the developer's subsequent Phase 02 approval above. No subsequent phase started.

## Phase 03 - Docker Compose developer environment

- Status: `APPROVED` - manual human testing completed and approved by the developer on 2026-10-31.
- Scope delivered:
  - Multi-stage .NET 10 Release image, minimal build context excluding secrets/generated artifacts, non-root runtime and curl liveness probe.
  - Official PostgreSQL 17, named persistent volume, no DB host port, loopback-only configurable API port. PostgreSQL health gates a one-shot migration service; successful migrations gate API startup. Bounded health retries and nonzero bootstrap failures avoid assuming ordinary depends_on implies readiness or restarting indefinitely.
  - Explicit `--migrate-development` command applies the existing EF migration chain without serving HTTP/seeding; rejected outside Development. Normal API startup retains existing opt-in account seeds. This workflow is local-demo-only, not a production deployment/migration strategy.
  - Anonymous `/health` liveness, JSON console logs and structured bootstrap events 3001..3004/outcomes. Failure logs omit raw exceptions/config values; Compose suppresses EF provider details below Critical. No secrets added to tracked configuration.
  - Setup, account/bootstrap, migration rerun, authentication, reset, troubleshooting and limitations guide. No dispatch, sender, source polling, management API or frontend work.
- Changed files:
  - Container/config: `Dockerfile`, `.dockerignore`, `compose.yaml`, `.env.example`.
  - Runtime: `src/Alerts.Api/Program.cs`, `src/Alerts.Api/Infrastructure/Identity/DevelopmentDatabaseMigrator.cs`.
  - Tests: `tests/Alerts.UnitTests/RuntimeTests.cs` (anonymous liveness response/no-store; production migration guard).
  - Documentation: `README.md`, `docs/phase03-compose.md`, this log.
- Tests executed and outcomes:
  - `dotnet build Alerts.sln --no-restore`: passed, zero warnings/errors, SDK 10.0.403 and existing cached dependencies; shell-local CLI home/NuGet cache under ignored `.local/`. No package changes.
  - `dotnet test Alerts.sln --no-build`: passed all 19 tests, zero failures/skips, including all 17 previous tests. In-process HTTP/EF InMemory coverage, no E2E or external-provider suite.
  - `dotnet format Alerts.sln --verify-no-changes --no-restore`: passed, exit 0.
  - `git -c safe.directory=D:/Temp/Sonrisa diff --check`: passed after removing a README trailing blank line. Existing user-ignore permission and line-ending warnings are environmental; no global Git configuration changed.
  - Docker initially denied daemon access inside sandbox; approved escalation worked. Docker Desktop 4.35.1 / Engine 27.3.1 / Compose v2.29.7, Linux containers.
  - Isolated project `alerts-phase03-validation`, ignored generated `.local/phase03.env`, API port 55165. Exact build/start command: `docker compose --env-file .local/phase03.env -p alerts-phase03-validation up --build --detach --wait --wait-timeout 180`. Passed: registry pulls, image restore/publish, fresh PostgreSQL volume, DB healthy, migrate exit 0, API healthy. An initial disposable test-env generation mistake was corrected and its volume reset before substantive validation; product config was unaffected.
  - Actual HTTP `/health`: 200 Healthy. Anonymous `me`: 401. User login (CSRF cookie/token): 204, `me`: 200, admin probe: 403. Admin login: 204, `me`: 200, admin probe: 200.
  - SQL through `docker compose ... exec -T db psql -v ON_ERROR_STOP=1 -U alerts -d alerts`: 14 tables, two migration-history rows, two accounts, three topics, two channels. Piped `docs/phase02-validation.sql`: PASS for catalog seeds, independent selections, unique/FK constraints, attempt/status bounds and deletion/history policy; rolled back.
  - `docker compose --env-file .local/phase03.env -p alerts-phase03-validation run --rm migrate`: passed on migrated DB. `down` without volumes followed by `up --detach --wait --wait-timeout 180`: passed; both original account passwords still logged in with correct role boundaries, no duplicate seeds.
  - Runtime `id -u`: 1654 (non-root). Checked migrate/API logs for JSON events 3001, 3002, 3003 and all three generated validation passwords: events present, passwords absent.
  - `docker compose ... run --rm --no-deps -e ASPNETCORE_ENVIRONMENT=Production migrate`: exit 1 with safe JSON failure event 3004. `docker compose ... run --rm --no-deps -e DevelopmentSeed__AdminPassword= api`: exit 1 with event 3004, before HTTP serving.
- Final clean-volume smoke test: isolated `down --volumes` followed by `up --build --detach --wait --wait-timeout 180` passed again; health, two migration rows and both role/login scenarios passed. Removed validation containers, network, volume and generated credential file afterward. Built image/cache retained.
- Manual verification / human acceptance commands:
  1. Follow `docs/phase03-compose.md` from the repo root: `Copy-Item .env.example .env`; replace the DB password; opt into seeds with two distinct emails and valid unique passwords to verify auth.
  2. `docker compose config --quiet`, `docker compose up --build`. In another terminal: `docker compose ps --all`, `curl.exe --fail --include http://localhost:5165/health`, `docker compose exec -T db psql -U alerts -d alerts -c '\dt'`. Expect healthy DB/API, migration exit 0, 200 Healthy and complete schema.
  3. Run the guide's migration-history/account count commands and Phase 01 Repeatable API checks using the same credentials; expect the HTTP statuses above. `docker compose run --rm migrate` must succeed again.
  4. `docker compose down`, then `docker compose up --detach --wait --wait-timeout 180`; verify original accounts/passwords persist. Recreated API requires new login/CSRF tokens.
  5. For disposable data only: `docker compose down --volumes`, then `docker compose up --build --detach --wait --wait-timeout 180`; recheck health, migration history/schema and authentication. This deletes all local application data. Use `docker compose down` to stop while preserving data.
- Known limitations / tradeoffs:
  - `/health` is process liveness, not DB readiness. DB health checks and successful migrations gate initial startup only; later outages can leave liveness healthy while authenticated requests fail. No DB readiness HTTP endpoint or automatic outage recovery is claimed.
  - Development/localhost HTTP only; no production HTTPS deployment, distributed locking strategy or production secret management. DB/account secrets live in ignored local environment configuration. Password quoting restriction (no double quotes/newlines) documented.
  - Data-protection keys are container-local and unencrypted at rest as framework warnings indicate; API recreation invalidates sessions/CSRF tokens. Production key persistence/protection requires a separate deployment decision.
  - Image tags are versioned but not immutable digests. Runtime installs curl for the health probe. Compose hides raw EF exception details for bootstrap secrecy; troubleshooting uses health, credential consistency and seed requirements.
  - Existing untracked Phase 02 conversation records left untouched. No commit created.
- Developer questions / decisions:
  - No blocking questions or scope deviations. Previous Phase 02 approval verified before coding. Explicit one-shot development migrations chosen to preserve the prior explicit migration workflow. Future source/destination decisions remain deferred to their assigned phases.
- Developer approval (2026-10-31): "manual human testing is done, approve the changes and edit the status files for the next phase, do not start phase 4". Phase 03 is human validated and approved. Phase 04 remains NOT STARTED and requires a separate explicit implementation request. No commit created.

## Phase 04 - Alert routing, persistence, retry and fake senders

- Status: `APPROVED` - explicitly approved by the developer on 2026-10-31.
- Scope delivered:
  - Validated internal `AlertEvent` dispatch boundary, input copying and internal receipt timestamps. External REST DTO conversion remains Phase 06.
  - PostgreSQL transaction for the event and complete initial delivery snapshot; known external IDs return false without new deliveries. Only the specific external-ID unique constraint is treated as a duplicate race.
  - Independent topic/channel joins for enabled users, topics and channels. Every actual attempt rechecks persisted enablement and both selections; no preference combinations added.
  - Channel abstraction and recording fake Email/Slack adapters. No external I/O or destination decisions introduced.
  - Persisted attempts/outcomes, safe error codes and structured outcome logs. Maximum three reserved attempts, with exponential delays of 1 second then 2 seconds. Retry due time derives from persisted timestamps and honors cancellation.
  - One startup recovery pass and explicit internal pending scan; no periodic source/retry polling worker. Pending scans read pages of 100. Singleton gate serializes the supported single-process dispatcher.
- Changed files:
  - Contracts: `src/Alerts.Api/Application/IAlertDispatcher.cs`, `IChannelSender.cs` (including immutable `DeliveryMessage`).
  - Infrastructure: `src/Alerts.Api/Infrastructure/AlertDispatcher.cs`, `DispatchGate.cs`, `FakeChannelSender.cs`, `PendingDeliveryRecovery.cs`, `RoutingSetup.cs`.
  - Composition: `src/Alerts.Api/Program.cs`.
  - Tests: `tests/Alerts.UnitTests/RoutingTests.cs` (22 cases, including parameterized variants).
  - Documentation: `docs/phase04-routing.md`, root `README.md`, Application/Infrastructure boundary READMEs and this log.
- Tests executed and outcomes:
  - Existing cached dependencies; no package additions, restore, schema changes or migrations. Shell-local `DOTNET_CLI_HOME`/`NUGET_PACKAGES` under ignored `.local/`.
  - `dotnet build Alerts.sln --no-restore`: passed, zero warnings/errors.
  - `dotnet test Alerts.sln --no-build`: final run passed all 41 tests, zero failures/skips (19 prior + 22 routing/recovery cases). Earlier run passed 39 before two additional recovery/adapter cases were added.
  - `dotnet format Alerts.sln --no-restore`: applied formatting. Final `dotnet format Alerts.sln --verify-no-changes --no-restore`: passed, exit 0.
  - `dotnet test tests/Alerts.UnitTests --no-build --filter FullyQualifiedName~ControlledDispatchShowsTwoPersistentDeliveriesAndFakeCalls --logger 'console;verbosity=detailed'`: passed. Output: delivery 1 / Alice / channel 1 / Succeeded / 1 attempt; delivery 2 / Alice / channel 2 / Succeeded / 1 attempt; fake Email calls 1, fake Slack calls 1. Rows were read through a newly opened EF context.
  - Unit coverage includes unrelated/empty selections, global topic/channel/user disablement, channel removal across two topics, user isolation, duplicate IDs across contexts/concurrent in-process calls, exact 1s/2s delays, twice-fail-then-success, terminal failure at three attempts, repeated scans staying capped, persisted flag/preference changes before retries, cancellation and restart, final reserved attempt recovery, missing adapter, actual hosted recovery wiring and synthetic PostgreSQL duplicate exception classification.
  - `git -c safe.directory=D:/Temp/Sonrisa diff --check`: passed. Existing user Git ignore permission and CRLF warnings remain environmental; no global Git configuration changed.
- Manual verification / human acceptance commands:
  1. Follow `docs/phase04-routing.md`. Run `dotnet build Alerts.sln --no-restore`, `dotnet test Alerts.sln --no-build`, and `dotnet format Alerts.sln --verify-no-changes --no-restore`.
  2. Run `dotnet test tests/Alerts.UnitTests --no-build --filter FullyQualifiedName~RoutingTests --logger 'console;verbosity=detailed'`. Inspect the controlled invocation's two persisted rows and fake-call counts, and the exact retry-count/delay assertions.
  3. Review the guide's internal scoped invocation, read-only PostgreSQL inspection commands and startup recovery behavior. The automated controlled sample uses an isolated EF InMemory database, not Compose data. There is no public ingestion endpoint in this phase.
  4. Confirm independent preferences, pending-on-disable/reselection behavior, 1s/2s exponential delays and three-attempt cap. Give explicit approval or Phase 04 corrections; no later phase may start without a separate instruction.
- Known limitations / tradeoffs:
  - Tests use EF InMemory; PostgreSQL transaction rollback and real concurrent uniqueness races were not exercised against a live DB in this phase. The duplicate exception branch is covered synthetically; relational constraints are unchanged from previously validated Phase 02. No E2E/external-provider suite or Docker rerun introduced.
  - Single API process only; the in-process gate does not coordinate independently running dispatchers. Sends/retries are sequential, so slow senders delay later deliveries.
  - Reserving before I/O conservatively consumes interrupted attempts. A crash after external acceptance but before outcome persistence can repeat an external side effect on recovery. No exactly-once or distributed-processing guarantee is claimed.
  - Currently disabled/deselected deliveries remain pending without consuming attempts; re-enabling/reselecting can deliver older events at a later scan. Initially ineligible events have no deliveries and are not routed retroactively.
  - Recovery is one startup scan plus explicit internal calls. A scan-level persistence failure is logged safely and needs a later scan/restart; there is no autonomous periodic recovery yet.
  - Fake calls are process-local; success means fake acceptance only. Real adapters, destinations and provider-specific permanent/transient error distinctions remain Phase 05. Fake-only sender exceptions are retried with safe generic error codes; missing adapters remain pending.
- Developer questions / decisions:
  - Prior Phase 03 human approval verified. Before coding, proposed persisted retries with startup/explicit scan, pre-send reservations and pending-on-disable behavior.
  - Developer (2026-10-31): "Instead of 1 second restart use some exponential delays between recalls, otherwise the plan is approved. Proceed with the implementation". Implemented bounded 1-second then 2-second exponential delays. This authorizes implementation, not completed-phase human acceptance.
  - No blocking questions remain and no subsequent phase was implemented. No commit created.
- Developer approval (2026-10-31): "phase 04 is approved, edit status files". Phase 04 is human approved. Phase 05 remains NOT STARTED and requires a separate explicit implementation request. No commit created.

## Phase 05 - Email and Slack channel adapters

- Status: `APPROVED` — developer explicitly approved the implementation on 2026-10-31, with Slack full-content/multiple-message behavior deferred for post-MVP validation.
- Scope delivered:
  - Runtime `IChannelSender` adapters for stable `email` and `slack` codes, replacing runtime recording fakes. Routing still uses normalized events and independent selections without referencing SMTP/Slack implementations. No schema/migration change.
  - MailKit SMTP, current Identity email lookup per attempt, control-character-safe subject and plain-text MIME body, environment-managed sender/host/port/TLS/authentication, 30-second socket timeout and cancellation. Optional profile-enabled Mailpit sink with loopback Web UI.
  - Per-application-user incoming Slack webhook mapping supplied as secret JSON configuration. HTTPS Slack host validation; explicit Development-only HTTP local mock opt-in. Redirects/cookies and default HttpClient URL logging disabled. Plain-text Slack blocks, escaped fallback, disabled parsing/unfurls and bounded content.
  - Fixed sanitized destination/configuration/provider errors persisted through the existing dispatcher. The approved three-total-attempt policy and 1s/2s delays remain unchanged; cancellation and enablement semantics remain unchanged. No adapter-owned retry loops.
  - Complete controlled local SMTP/HTTP fake demonstration through actual adapter DI, normalized-event dispatch and reopened delivery persistence. No external providers contacted, public ingestion endpoint, polling worker, management API, frontend or later-phase work added.
- Changed files:
  - Application: `src/Alerts.Api/Application/ChannelDeliveryError.cs`, `ChannelDeliveryException.cs`, boundary `README.md`.
  - Adapters: `src/Alerts.Api/Infrastructure/Channels/AlertMessageFormatter.cs`, `EmailOptions.cs`, `SlackOptions.cs`, `IEmailTransport.cs`, `MailKitEmailTransport.cs`, `EmailChannelSender.cs`, `SlackChannelSender.cs`.
  - Composition/failure mapping: `src/Alerts.Api/Infrastructure/RoutingSetup.cs`, `AlertDispatcher.cs`, `FakeChannelSender.cs`, Infrastructure `README.md`, `src/Alerts.Api/Program.cs`.
  - Dependencies/configuration: `src/Alerts.Api/Alerts.Api.csproj`, `.env.example`, `compose.yaml`.
  - Tests: new `tests/Alerts.UnitTests/ChannelSenderTests.cs`; existing startup recovery fixture in `RoutingTests.cs` explicitly supplies its test fake after runtime registration changed.
  - Documentation: `docs/phase05-channels.md`, `docs/phase04-routing.md`, root `README.md`, `codex_alerts_plan/PROJECT_CONTEXT.md`, this log.
- Tests executed and outcomes:
  - SDK 10.0.403, shell-local CLI home/NuGet cache under ignored `.local/`.
  - Initial `dotnet restore Alerts.sln` succeeded using available packages but flagged MailKit's transitive `System.Security.Cryptography.Pkcs` 10.0.0 with NU1902/NU1903 advisories. Explicitly pinned patched 10.0.14. Its first download was blocked by sandbox socket permissions; retried `dotnet restore Alerts.sln` with approved network escalation: passed without warnings.
  - `dotnet format Alerts.sln --no-restore`: applied formatting. Final `dotnet format Alerts.sln --verify-no-changes --no-restore`: passed, exit 0.
  - Final `dotnet build Alerts.sln --no-restore`: passed, zero warnings/errors. Two initial nullable warnings were corrected before this build.
  - Final `dotnet test Alerts.sln --no-build`: passed all 76 tests, zero failures/skips (41 existing + 35 adapter cases). An earlier run passed 75 before adding explicit SMTP-command rejection coverage.
  - `dotnet test tests/Alerts.UnitTests --no-build --filter FullyQualifiedName~ControlledAdaptersProduceTwoPersistedSuccessesWithLocalClientFakes --logger 'console;verbosity=detailed'`: passed. Output showed delivery 1 / Alice / Email / Succeeded / 1 attempt and delivery 2 / Alice / Slack / Succeeded / 1 attempt; SMTP fake captured `alice@example.test` and `[Alerts] Controlled sample`; HTTP fake captured the Slack POST with plain-text block. Secret webhook URL omitted from output.
  - Coverage includes header injection, plain-text MIME/Slack formatting, per-user destination choice, updated Identity email, missing/invalid users/destinations, bad SMTP settings before network access, malformed webhook JSON, HTTPS/host restrictions, Development-only mock opt-in, HTTP rejection/redirect/rate limit/server error, SMTP rejection, provider timeouts, cancellation, three-attempt cap and safe persisted errors/logs, and two users sharing a Slack destination.
  - `docker compose --env-file .env.example --profile channels config --quiet`: passed, exit 0. Docker emitted the existing inaccessible user config warning. No daemon operation, image pull, Compose startup or Mailpit socket send was performed in this phase; local mocks satisfy the agreed acceptance gate.
  - `git -c safe.directory=D:/Temp/Sonrisa diff --check`: passed. Existing CRLF and unreadable user-ignore warnings are environmental; no global Git configuration changed.
- Manual verification / human acceptance commands:
  1. Follow `docs/phase05-channels.md`. Run `dotnet restore Alerts.sln`, `dotnet build Alerts.sln --no-restore`, `dotnet test Alerts.sln --no-build`, and `dotnet format Alerts.sln --verify-no-changes --no-restore`.
  2. Run `dotnet test tests/Alerts.UnitTests --no-build --filter FullyQualifiedName~ControlledAdaptersProduceTwoPersistedSuccessesWithLocalClientFakes --logger 'console;verbosity=detailed'`. Expect the two persisted one-attempt successes and both captured formatted messages described above.
  3. Run `dotnet test tests/Alerts.UnitTests --no-build --filter FullyQualifiedName~ChannelSenderTests --logger 'console;verbosity=detailed'` to review destination, sanitization and failure assertions. These tests use isolated EF InMemory and client fakes, with no network listener/provider credentials required.
  4. Run `docker compose --env-file .env.example --profile channels config --quiet`. For optional live local-sink inspection, configure your ignored `.env` per the guide, run `docker compose --profile channels up --build --detach --wait --wait-timeout 180`, inspect `http://localhost:8025`, and use the documented internal dispatch invocation with eligible selections. Compose startup alone does not generate alerts. Do not print resolved secret configuration. Existing PostgreSQL inspection commands in the Phase 04 guide remain applicable.
  5. Review credentials, per-user webhook mapping and the shared-destination production decision. Give explicit Phase 05 approval or corrections; no later phase may start without a separate explicit request.
- Known limitations / tradeoffs:
  - Acceptance uses a fake SMTP transport and HTTP handler; real MailKit SMTP connection/authentication, Mailpit image startup and Slack delivery are not exercised automatically or claimed validated. No E2E/external-provider suite introduced.
  - Slack destinations are configured by server operators using application user IDs, not self-service. Configuration changes require API restart/recreation. No Slack DMs/OAuth, webhook provisioning or destination persistence/UI is implemented.
  - **Before production, clarify shared Slack channel deduplication and destination ownership/access/management.** For the MVP, separate eligible users intentionally create separate messages even when their webhooks target the same Slack channel.
  - All provider/configuration failures use the existing bounded retries, including 4xx/429 and missing configuration; no permanent/transient classification, Retry-After handling or resetting final failures. Success means provider acceptance, not guaranteed human receipt. Slack content is truncated to one section; Email retains full plain text.
  - Mailpit is optional and ephemeral. Its profile must be started when using the default Compose SMTP hostname; host-run SMTP requires a local loopback port override. `None` SMTP security and local HTTP mocks are Development-only. Production deployment remains outside this phase.
  - Existing single-process sequential dispatch, startup-only recovery, conservative attempt reservations and possible duplicate side effects after a crash remain the Phase 04 limitations. Previously pending eligible deliveries can now use configured adapters at startup.
- Developer questions / decisions:
  - Requested path `phases/04-channels.md` does not exist. Used the existing Phase 05 source of truth `codex_alerts_plan/phases/05-channels.md`, as explained before coding. Verified Phase 04 approval.
  - Proposed adapters, shared formatting, safe failure mapping, environment configuration and local mocked validation before coding; asked the three blocking transport/destination questions.
  - Developer (2026-10-31): "a webhook targetting a channel is fine, if multiple user sets the same channel we can let the system send multiple messages for now, write it up as a design decession that this needs to be cleared before production"; "Mailkit and Mailpit is perfect"; "local fake mocked systems are acceptedable for the MVP". Implemented the proposed server-owned per-user mapping and Identity email contract.
  - No blocking questions remain. No commit created and no subsequent phase implemented.
- Developer approval (2026-10-31): "rest of the implementation is approved, update status files". Phase 05 is human approved following the documentation follow-up below. Slack full-content/multiple-message validation remains deferred beyond MVP. Phase 06 requires a separate explicit implementation request. No commit created.

### Phase 05 documentation follow-up (2026-10-31)

- Developer requested post-MVP validation of whether sending the entire alert content across multiple Slack messages is acceptable behavior.
- Recorded this deferred decision in `PROJECT_CONTEXT.md` and `docs/phase05-channels.md`, including readability, message volume and partial-send/retry handling. It is not a Phase 05 acceptance blocker; current single-message truncation remains implemented.
- Documentation-only change; no runtime changes or test rerun. `git diff --check` passed. This follow-up was initially presented as `AWAITING HUMAN VALIDATION` and is included in the developer's subsequent Phase 05 approval above. No subsequent phase authorized or started.

## Phase 06 - Mock REST source and background polling

- Status: `APPROVED` — developer explicitly approved manual validation on 2026-10-31. No Phase 07 work or commit created.
- Scope delivered:
  - Approved GET-array contract mapped to normalized events through typed `HttpClient` and `INewsSource`; exact persisted topic-code lookup, required/bounded fields, explicit timestamp timezone converted to UTC. Individually malformed/unknown-topic items are skipped; invalid JSON/envelopes fail the poll.
  - Configurable opt-in single `BackgroundService`, fresh scopes, bounded HTTP timeout and 1 MiB response buffer, sequential cycles, safe stage-only failure logs and cancellation throughout. Default 30-second delay after each cycle; source failures do not consume delivery attempts.
  - Strictly Development-only anonymous `GET /dev/mock-news`, replaceable/reloaded deterministic fixture with Finance, Weather, duplicate ID, malformed and unknown-topic examples. Missing fixture returns 503. Compose mounts the fixture directory read-only and polls its internal listener when enabled.
  - Existing Phase 04 uniqueness/routing and Phase 05 adapters reused unchanged. No schema/package changes, management APIs, mutation endpoint, frontend, distributed processing or later phases.
- Changed files:
  - Application: `src/Alerts.Api/Application/INewsSource.cs`, boundary `README.md`.
  - Infrastructure: `src/Alerts.Api/Infrastructure/News/HttpNewsSource.cs`, `NewsSourceOptions.cs`, `NewsPollingWorker.cs`, `NewsSetup.cs`, Infrastructure `README.md`.
  - Host/configuration: `src/Alerts.Api/Program.cs`, `Alerts.Api.csproj`, `Fixtures/news.json`, root `compose.yaml`, `.env.example`.
  - Tests: new `NewsSourceTests.cs`, `DevelopmentNewsTests.cs`; extended `ChannelSenderTests.cs` with worker-through-real-adapters controlled validation and optional fixture configuration.
  - Documentation: root `README.md`, `docs/phase06-source-worker.md`, `docs/phase06-select-user.sql`, `codex_alerts_plan/PROJECT_CONTEXT.md`, this status log.
- Tests executed and outcomes:
  - Shell-local CLI home/NuGet cache under `.local/`; existing cached dependencies, no restore needed.
  - `dotnet format Alerts.sln --no-restore`: passed/applied formatting.
  - Final `dotnet format Alerts.sln --verify-no-changes --no-restore`: passed, exit 0. Final detailed controlled polling test: passed (1 test), output confirmed Email=1 / Slack=1, HTTP 503 recovery, duplicate suppression and clean stop. `git -c safe.directory=D:/Temp/Sonrisa diff --check`: passed, only existing CRLF advisory warnings.
  - `dotnet build Alerts.sln --no-restore`: passed, zero warnings/errors.
  - `dotnet test Alerts.sln --no-build`: final implementation run passed all 103 tests, zero failures/skips (76 prior + 27 added cases). Initial new malformed-JSON test used exact exception matching; changed to accept the derived JsonReaderException and reran successfully.
  - Tests cover accepted UTC/offset normalization, malformed neighbors and fields/lengths, unknown/case-mismatched topics, invalid envelopes, HTTP failure/timeout/cancellation, dedupe/delivery counts, worker failure continuation/no overlap and shutdown, disabled worker/interval cancellation, actual host Development/Production/Staging route registration, fixture reload/503 and option validation.
  - Controlled `PollingThroughHttpAndBothAdaptersSurvivesFailureAndIgnoresRepolls` uses the actual worker/client/dispatcher/Email/Slack adapters with EF InMemory and fake transports: first HTTP 503 recovered, two fixture polls, two events, two one-attempt successes, one SMTP message and one Slack POST, then clean cancellation.
  - `docker compose --env-file .env.example --profile channels config --quiet`: passed. Non-elevated daemon check was denied by Windows; approved elevated `docker info` succeeded (27.3.1). No automatic approval rejection.
  - Live isolated stack command: `docker compose --project-name alerts-phase06 --env-file .local/phase06/validation.env -f compose.yaml -f .local/phase06/compose.override.yaml --profile channels up --build --detach --wait --wait-timeout 180`: built and started successfully. Used generated ignored credentials, a separate fixture mount, fresh project-specific PostgreSQL volume, API port 15165 and Mailpit UI port 18025. A PowerShell 5 credential-generation setup error was corrected, and only this disposable validation stack/volume was recreated before acceptance; no existing application data was touched.
  - Live selection SQL (same statements as `docs/phase06-select-user.sql`) inserted two independent topics and Email for the seeded user. Publishing the fixture automatically stored `mock-finance-001` and `mock-weather-001`, created two `Succeeded` deliveries with one attempt each; Mailpit `/api/v1/messages` reported total 2.
  - Live failure/recovery: renamed the separate validation fixture temporarily; endpoint returned 503, logs showed source failures. Restored it; logs resumed `3 valid items, 0 new events`. PostgreSQL counts remained 2 events / 2 deliveries / 2 summed attempts after repeated polls.
  - `docker compose ... stop api` succeeded with `News polling stopped` log. Remaining temporary services were stopped; their isolated data and ignored validation files remain for inspection. No tracked fixture modification was left behind.
- Exact human-validation commands:
  1. Follow `docs/phase06-source-worker.md` for `.env`, opt-in seeding and destinations. Run `dotnet build Alerts.sln --no-restore`, `dotnet test Alerts.sln --no-build`, and `dotnet format Alerts.sln --verify-no-changes --no-restore` (restore first on fresh checkout).
  2. Run `dotnet test tests/Alerts.UnitTests --no-build --filter FullyQualifiedName~PollingThroughHttpAndBothAdaptersSurvivesFailureAndIgnoresRepolls --logger 'console;verbosity=detailed'` for the controlled worker and both adapters.
  3. Run `docker compose --profile channels up --build --detach --wait --wait-timeout 180` with polling disabled initially; apply `Get-Content -Raw docs/phase06-select-user.sql | docker compose exec -T db psql -U alerts -d alerts --set ON_ERROR_STOP=1 -f -` to the intended development test database. Then enable polling in `.env` and rerun Compose up.
  4. Inspect two fresh sample events/deliveries and Mailpit; repeat the fixture and confirm no extra rows/messages. Follow the guide's temporary fixture rename/restore and `docker compose stop api` commands to validate HTTP failure recovery and shutdown.
  5. Approve Phase 06 explicitly or request Phase 06 corrections. Approval alone does not authorize Phase 07.
- Known limitations / tradeoffs:
  - Polling is explicitly opt-in to allow selection/destination setup before first ingestion. First self-poll may race listener startup and recover next interval. All cycles/sends are sequential and single-process; slow adapters delay polling.
  - Existing startup-only pending recovery, conservative attempt reservations, no retroactive routing and potential crash-related duplicate external side effects remain unchanged. Repolling a known event does not rescan pending deliveries.
  - Live Compose exercised PostgreSQL/MailKit/Mailpit; Slack remains fake-HTTP validated, not a live external provider. Unit tests use EF InMemory; no E2E/external-provider suite introduced. Windows Docker operations required elevation; preexisting Git ignore/CRLF warnings remain environmental.
- Developer questions / decisions:
  - Requested `phases/04-source-worker.md` does not exist; used the actual Phase 06 source of truth `codex_alerts_plan/phases/06-source-worker.md`, explained before coding. Verified Phase 05 human approval.
  - Developer (2026-10-31): "Rest contract: approved" and "Mock hosting: a seperate API route in the same project is perfect, make sure its dev only." Implemented that contract and strict environment restriction. Minor implementation choices (opt-in flag, bounded settings, read-only fixture reload/503) are documented above.
  - No blocking questions or scope deviations remain. Human validation approved; no later phase started.
- Developer approval (2026-10-31): "phase 06 manual test is approved". Phase 06 is human approved. Phase 07 requires a separate explicit implementation request.
- Approval bookkeeping: documentation only; no runtime changes or test rerun. Git whitespace check passed.

## Phase 07 - Personal and admin HTTP APIs

- Status: `AWAITING HUMAN VALIDATION`. Implementation authorization and the decisions below are not completed-phase approval. No Phase 08 work or commit created.
- Scope delivered:
  - Authenticated `/api/me` catalogs, independent selection arrays, and idempotent topic/channel PUT/DELETE. Personal ownership always comes from the server-side Identity session. Admins retain personal access.
  - Admin-only safe user listing, enable/disable and hard deletion using Identity with the approved PostgreSQL cascade policy; global topic/channel listing and enable/disable. No user/catalog creation, role editing, destination UI or per-topic/channel preference matrix.
  - Disabled catalogs stay visible with their status. New disabled selections return 409; existing selections remain, repeat PUTs remain idempotent, and removal is allowed. Enablement never auto-subscribes users. Existing dispatcher enablement checks remain unchanged.
  - Prevented admin self-disable/delete; explicit boolean enablement validation, positive catalog IDs, safe custom errors/statuses, no-store management responses and CSRF protection on every mutation.
  - Development-only `/openapi/v1.json` via the supported Microsoft.AspNetCore.OpenApi 10.0.14 package; summaries, safe DTO schemas, mutation statuses and cookie/CSRF usage descriptions. No Swagger UI package.
- Changed files:
  - Host/API: `src/Alerts.Api/ManagementEndpoints.cs`, `Program.cs`, `Alerts.Api.csproj`.
  - Selection contract/implementation: `Application/SelectionUnavailableException.cs`, `Application/ISelectionService.cs`, `Infrastructure/SelectionService.cs`, `Application/README.md` under `src/Alerts.Api/`.
  - Tests: new `tests/Alerts.UnitTests/ManagementTests.cs`; `AuthTests.cs` adds optional catalog initialization before creating Identity users in the shared test host.
  - Documentation: `docs/phase07-api.md`, root `README.md`, `codex_alerts_plan/PROJECT_CONTEXT.md`, this log. No schema/migration/configuration/frontend changes.
- Tests executed and outcomes:
  - .NET SDK 10.0.403; shell-local CLI home/NuGet cache under ignored `.local/`. `dotnet restore Alerts.sln`: passed without warnings or escalation.
  - Final `dotnet build Alerts.sln --no-restore`: passed, zero warnings/errors. `dotnet format Alerts.sln --no-restore` applied formatting; final `dotnet format Alerts.sln --verify-no-changes --no-restore`: passed, exit 0.
  - Final `dotnet test Alerts.sln --no-build`: passed all 156 tests (103 prior + 53 new), zero failures/skips, approximately 2 minutes.
  - Initial 156-test run: 140 passed / 16 failed due to the new fixture initializing catalog seeds after Identity had already created the InMemory store. Corrected initialization order and the test's TestServer service lookup. A build attempted during that run failed on the Windows test-assembly lock; rerun after completion passed. Next suite run: 154 passed / 2 failed because an assertion expected the exact cache string rather than allowing Identity's additional `no-cache` directive. Changed it to assert the required `NoStore` flag, retaining the security behavior.
  - Focused OpenAPI Development/Production/Staging test: passed all 3 cases. Automated cases cover every management route's authentication, every admin role boundary, CSRF on each mutation, session-derived ownership for user/admin, independent/idempotent preferences, disabled catalog behavior, validation/statuses, safe user fields, self-action guards, enable/delete session enforcement, deletion cascade model metadata and API global flags affecting the real dispatcher. All use isolated test stores/client fakes; no E2E/external-provider suite added.
  - `docker compose --env-file .env.example --profile channels config --quiet`: passed. Final `git -c safe.directory=D:/Temp/Sonrisa diff --check`: passed, exit 0. Existing unreadable user config/ignore and CRLF warnings remain environmental.
  - Isolated live stack: `docker compose --project-name alerts-phase07 --env-file .local/phase07/validation.env -f compose.yaml -f .local/phase07/compose.override.yaml --profile channels up --build --detach --wait --wait-timeout 180`: built and started successfully after correcting the locally generated environment file and recreating only this newly created disposable stack/volume. API port 15175, Mailpit port 18035, separate fixture mount, fresh project-specific PostgreSQL volume and generated ignored credentials; no existing application data touched. Docker daemon access required approved escalation; no automatic approval rejection.
  - `powershell -NoProfile -ExecutionPolicy Bypass -File .local/phase07/check-api.ps1`: passed local API scenario, using a process-only script policy override after the shell initially refused to run the local validation script. Confirmed anonymous 401, regular-user admin 403, missing-CSRF 400, independent topic/channel changes, admin personal operations, self-action 409, disabled selection 409, user disable/re-enable/login behavior, and local polling/Mailpit suppression/recovery. No persistent PowerShell policy changed.
  - New disabled-topic event: stored with zero deliveries/Mailpit messages. Re-enabled topic with a new external ID: one delivery/Mailpit message. Selections configured exclusively through HTTP, with no setup SQL.
  - Real deletion: before HTTP DELETE, SQL counts were users=2, topic selections=1, channel selections=1, deliveries=1, shared events=2, topics=3, channels=2. DELETE returned 204; existing deleted-user session returned 401. Afterward: users=1, selections=0+0, deliveries=0; shared events=2/topics=3/channels=2 retained. SQL was passed through psql stdin after an initial Windows command-line quoting error. This verifies actual PostgreSQL cascades, which InMemory cannot prove.
  - Isolated `alerts-phase07` services stopped successfully with `docker compose ... --profile channels stop`. Its volume and ignored `.local/phase07` validation scripts/files remain for inspection; no tracked fixture changes. Temporary scripts are manual validation artifacts, not an installed E2E suite.
- Exact human-validation commands:
  1. Follow `docs/phase07-api.md`. Run `dotnet restore Alerts.sln`, `dotnet build Alerts.sln --no-restore`, `dotnet test Alerts.sln --no-build`, and `dotnet format Alerts.sln --verify-no-changes --no-restore` from the root.
  2. Run `dotnet test tests/Alerts.UnitTests --no-build --filter FullyQualifiedName~ManagementTests` for the focused API acceptance cases.
  3. Configure your local `.env` and disposable seeded accounts. Run `docker compose --profile channels up --build --detach --wait --wait-timeout 180`. Import `http://localhost:5165/openapi/v1.json` into an API client, or run the guide's PowerShell login and mutation examples with separate user/admin cookie sessions. Retrieve CSRF after login. Expect independent arrays, regular-user admin denial, admin personal access, safe catalog/user fields, disabled selection guards and self-action rejection.
  4. Use fresh fixture IDs per the guide/Phase 06 instructions to verify disabled topic/channel/user suppression and re-enable behavior. Inspect local Mailpit and PostgreSQL. Finally delete only a disposable test user through the API and verify dependent row removal/shared data retention with the documented SQL; confirm its previous session receives 401.
  5. Give explicit Phase 07 approval or corrections. Do not start Phase 08 without a separate explicit request.
- Known limitations / tradeoffs:
  - Minimal unpaginated user/catalog lists for this small MVP; no account creation/invitations, roles, catalogs, delivery-history APIs or destination management added beyond the specified operations. Admins may manage other admins; the agreed guard is self-action prevention.
  - InMemory tests check authorization/behavior and cascade model configuration; live isolated PostgreSQL validation supplies actual cascade evidence. No automated relational/external-provider suite introduced; live Slack remains outside this phase.
  - Existing in-flight requests/provider sends are not cancelled by later enablement changes/deletion. Concurrent requests are not a distributed transaction; existing single-process routing, pending recovery, retry and crash-side-effect limitations remain. Re-enable does not reroute already stored event IDs.
  - Development seeding can recreate a deleted seeded account with a new ID on restart; finish deletion checks before restarting or disable seeding. OpenAPI is generated JSON for import; no interactive documentation UI added.
- Developer questions / decisions:
  - Requested `phases/04-api.md` does not exist. Used actual `codex_alerts_plan/phases/07-api.md`, explained before coding, consistent with the explicit Phase 07 assignment. Verified Phase 06 human approval and clean initial worktree.
  - Explained the plan and existing relevant code before editing. Asked two blocking behavior questions and waited. Developer on 2026-10-31 selected "Reject new selections while disabled; allow removal (Recommended)" and "Prevent self-disable and self-delete (Recommended)". Implemented both and recorded them in project context.
  - No blocking questions or scope deviations remain. No subsequent phase implemented; no commit created.
- Developer approval: pending explicit Phase 07 human validation.
