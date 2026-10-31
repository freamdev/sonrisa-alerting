# Alerts

A small modular monolith for topic-based notifications through independently selected delivery channels. Phases 00 through 04 are human approved. Phase 05 adds MailKit Email and per-user Slack channel-webhook adapters and is awaiting human validation. See [Phase 05 configuration and mocked demonstration](docs/phase05-channels.md) and [Phase 04 routing](docs/phase04-routing.md). No subsequent phase has started.

## Run with Docker Compose

Copy `.env.example` to `.env`, replace the database password, and optionally enable development seeding with your own admin/user passwords. Run `docker compose up --build`, then check `http://localhost:5165/health`. PostgreSQL readiness and successful one-shot migrations gate API startup. See [Phase 03 setup and validation](docs/phase03-compose.md) for configuration, authentication, reset and troubleshooting commands. This is a local Development environment.

## Decisions and layout

Developer decisions on 2026-10-31: PostgreSQL, .NET 10 LTS, Angular 22, and this repository root/layout. Requirements and the review log remain in `codex_alerts_plan/`.

```text
Alerts.sln
global.json
src/Alerts.Api/
  Program.cs                  # ASP.NET Core host / composition root
  Domain/                    # Persistence models and business vocabulary
  Application/               # Selection contract and access policies
  Infrastructure/            # EF selection service and Identity/context/migrations
tests/Alerts.UnitTests/       # Focused unit tests and host smoke test
frontend/.gitkeep             # Angular placeholder only
codex_alerts_plan/            # Requirements, phases, implementation status
```

One backend project keeps the application small. Folder boundaries separate domain concepts, application orchestration and infrastructure adapters; they are conventions rather than compiler-enforced project boundaries. The API host will compose them. The test project references the backend. See the three boundary documents for vocabulary and intended contracts.

## Build and test

Install .NET SDK 10.0.403 or a later stable .NET 10 SDK. `global.json` permits later .NET 10 feature bands, but not preview SDKs or another major. Initial restore needs access to NuGet.org. The backend uses PostgreSQL for Identity; automated tests need no database or Node installation.

From the repository root:

```powershell
dotnet restore Alerts.sln
dotnet build Alerts.sln --no-restore
dotnet test Alerts.sln --no-build
dotnet format Alerts.sln --verify-no-changes --no-restore
```

Follow [Phase 02 schema and validation](docs/phase02-data.md) to configure PostgreSQL, apply/rerun migrations and inspect independent selections, uniqueness and deletion behavior. [Phase 01 API validation](docs/phase01-auth.md) covers development accounts and user/admin, CSRF, logout and disabled-account checks at `http://localhost:5165`. `/health` provides anonymous API liveness (it does not check DB connectivity).

Tests cover host composition, authentication/authorization, catalog seeds and independent selection operations using an in-process TestServer, ephemeral encryption keys and isolated EF InMemory stores. They open no network listener and do not test PostgreSQL relational guarantees; the Phase 02 guide includes a rollback-only manual SQL check. There are no E2E or external-provider test suites.

If a restricted environment cannot write to the default .NET CLI home or NuGet cache, use these shell-local settings before the commands above (generated files are ignored):

```powershell
$env:DOTNET_CLI_HOME = Join-Path $PWD '.local/dotnet'
$env:NUGET_PACKAGES = Join-Path $PWD '.local/nuget/packages'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
```

## Configuration and future work

`appsettings.json` and `appsettings.Development.json` contain only logging/host settings. `.env.example` documents environment settings and credential placeholders. Compose reads `.env`; ASP.NET Core alone does not, so set variables in your shell or IDE for `dotnet run`. Console logs use JSON with bootstrap event IDs. Keep real credentials in environment variables or local secret storage, never in tracked files. Development seeding is opt-in and rejected outside Development; it never resets an existing password or re-enables an account.

Angular 22 is recorded for Phase 08 only. The detected Node.js 18.14.2 cannot support it. Before initializing Angular in Phase 08, upgrade Node to a version supported by Angular 22 (currently Node ^22.22.3, ^24.15.0 or ^26.0.0), recheck the [official compatibility table](https://angular.dev/reference/versions), and handle the update locally if authorized and feasible; otherwise remind the developer. No Angular CLI, npm dependencies or frontend pages are installed now.

Authentication uses same-origin cookies and CSRF protection, with no public registration. Admins retain ordinary user access. Disabled accounts cannot log in or use existing sessions. Domain persistence stores independent selections and event/delivery state. Deleting an account cascades its selections and deliveries while retaining shared events/catalogs. Internal routing uses Email/Slack adapters with at most three persisted attempts and 1-second/2-second retry delays. Email uses the current Identity email; server configuration maps application user IDs to Slack channel webhooks. Shared Slack destinations receive separate messages per eligible user; this policy must be clarified before production. Use `docker compose --profile channels up --build` to include the local Mailpit sink at `http://localhost:8025`. Admin-driven account creation/invitations, management APIs, source polling and frontend features remain in their assigned phases; the source payload decision remains deferred.

Read [the operating instructions](codex_alerts_plan/AGENTS.md), [project context](codex_alerts_plan/PROJECT_CONTEXT.md), [phase plan](codex_alerts_plan/PHASES.md), and [implementation status](codex_alerts_plan/IMPLEMENTATION_STATUS.md) before proceeding. Each phase requires explicit human validation; approval alone does not authorize the next phase.
