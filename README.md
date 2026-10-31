# Alerts

A small modular monolith for topic-based notifications through independently selected delivery channels. Phase 01 adds backend Identity authentication and authorization to the approved Phase 00 scaffold. It is awaiting human validation.

## Decisions and layout

Developer decisions on 2026-10-31: PostgreSQL, .NET 10 LTS, Angular 22, and this repository root/layout. Requirements and the review log remain in `codex_alerts_plan/`.

```text
Alerts.sln
global.json
src/Alerts.Api/
  Program.cs                  # ASP.NET Core host / composition root
  Domain/README.md            # Business vocabulary and invariants
  Application/README.md       # Intended use cases and contracts
  Infrastructure/Identity/    # Identity user, EF store, migration and development bootstrap
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

Follow [Phase 01 setup and API validation](docs/phase01-auth.md) to configure PostgreSQL, apply the migration, seed development accounts and run the host at `http://localhost:5165`. The document includes user/admin, CSRF, logout and disabled-account checks. `/health` is scheduled for Phase 03.

Tests cover host composition and authentication/authorization using an in-process TestServer, ephemeral encryption keys and an isolated EF InMemory test store. They open no network listener and do not test PostgreSQL relational guarantees. There are no E2E or external-provider test suites.

If a restricted environment cannot write to the default .NET CLI home or NuGet cache, use these shell-local settings before the commands above (generated files are ignored):

```powershell
$env:DOTNET_CLI_HOME = Join-Path $PWD '.local/dotnet'
$env:NUGET_PACKAGES = Join-Path $PWD '.local/nuget/packages'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
```

## Configuration and future work

`appsettings.json` and `appsettings.Development.json` contain only logging/host settings. `.env.example` documents environment settings and credential placeholders. ASP.NET Core does not load `.env` automatically: set variables in your shell or IDE. Keep real credentials in environment variables or local secret storage, never in tracked files. Development seeding is opt-in and rejected outside Development; it never resets an existing password or re-enables an account.

Angular 22 is recorded for Phase 08 only. The detected Node.js 18.14.2 cannot support it. Before initializing Angular in Phase 08, upgrade Node to a version supported by Angular 22 (currently Node ^22.22.3, ^24.15.0 or ^26.0.0), recheck the [official compatibility table](https://angular.dev/reference/versions), and handle the update locally if authorized and feasible; otherwise remind the developer. No Angular CLI, npm dependencies or frontend pages are installed now.

Authentication uses same-origin cookies and CSRF protection, with no public registration. Admins retain ordinary user access. Disabled accounts cannot log in or use existing sessions. Admin-driven account creation/invitations and management APIs remain later work. Domain persistence, Docker Compose, routing, senders, polling and frontend features remain in their assigned phases; deletion/history semantics, destination settings and source payload remain decisions for those phases.

Read [the operating instructions](codex_alerts_plan/AGENTS.md), [project context](codex_alerts_plan/PROJECT_CONTEXT.md), [phase plan](codex_alerts_plan/PHASES.md), and [implementation status](codex_alerts_plan/IMPLEMENTATION_STATUS.md) before proceeding. Each phase requires explicit human validation; approval alone does not authorize the next phase.
