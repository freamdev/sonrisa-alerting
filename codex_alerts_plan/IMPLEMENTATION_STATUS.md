# Implementation status / human review log

**Current phase:** 00 — APPROVED (human validated); awaiting instructions before Phase 01

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
