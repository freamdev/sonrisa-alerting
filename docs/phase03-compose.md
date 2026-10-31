# Phase 03: Docker Compose development environment

Run these PowerShell commands from the repository root. Install Docker Desktop with Linux containers and Docker Compose v2 (tested v2.29.7). Docker must be running. First build needs Microsoft Container Registry, Docker Hub, NuGet.org and Ubuntu package repositories. No host .NET SDK is needed for Compose.

## Fresh checkout and accounts

```powershell
Copy-Item .env.example .env
# Edit locally before starting; never commit credentials.
notepad .env
docker compose config --quiet
docker compose up --build
```

Replace `POSTGRES_PASSWORD` with a unique local value. The connection string quotes the password; do not include double quotes or newlines. Single-quote `.env` values containing `$` or `#` to keep them literal. Shell variables override `.env`; clear stale variables if configuration is unexpected. `config --quiet` validates without printing resolved secrets; plain `config` and container inspection expose them.

Accounts are opt-in: set `DevelopmentSeed__Enabled=true`, replace both account password placeholders with separate passwords (12+ characters, upper/lowercase, digit, symbol), and keep emails distinct. No accounts are created by default. Existing seed passwords, roles and enabled flags are preserved. To disable later seeding set the flag false and run `docker compose up --detach` to recreate the API with its new environment. `restart` alone does not load configuration changes.

PostgreSQL 17 uses the official image and the `alerts_postgres-data` named volume. The DB has no published host port. The API binds only `127.0.0.1:5165`; change `API_PORT` in `.env` if occupied. The multi-stage image publishes Release output and runs as the runtime image's non-root app user. Secrets and local build artifacts are excluded from its build context.

The startup chain is DB health (`pg_isready`, bounded retries), then the one-shot `migrate` service, then the API only if migrations exit 0. `migrate` runs the existing EF migration chain with `--migrate-development` and exits without opening an HTTP listener or seeding accounts. The API then runs opt-in Development seeds. Failed migration/seed startup exits nonzero; fix configuration and rerun `up`. This is a local demo workflow, not a production migration/deployment strategy. Ordinary API startup does not migrate. The command is rejected outside Development. No EF tool/SDK is installed in the runtime image.

Startup conditions follow [Docker's startup-order documentation](https://docs.docker.com/compose/how-tos/startup-order/).

## Health, schema and authentication

In another terminal:

```powershell
docker compose ps --all
curl.exe --fail --include http://localhost:5165/health
docker compose exec -T db psql -U alerts -d alerts -c '\dt'
'SELECT * FROM "__EFMigrationsHistory";' | docker compose exec -T db psql -U alerts -d alerts
'SELECT count(*) FROM "AspNetUsers";' | docker compose exec -T db psql -U alerts -d alerts
docker compose logs --no-color migrate api
```

Expect DB/API healthy, `migrate` exited 0, HTTP 200 with `Healthy`, two migration rows, Identity/domain tables, and two users if seeding was enabled. `/health` is anonymous liveness using the [ASP.NET Core health middleware](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/health-checks?view=aspnetcore-10.0). It does not query PostgreSQL and may remain 200 during a later DB outage; authenticated requests still require the database. There is no DB readiness HTTP endpoint.

Run the **Repeatable API checks** in [Phase 01](phase01-auth.md) using the same emails/passwords entered in `.env` (enter via its hidden prompts). The default base URL is unchanged. Expect anonymous `me` 401, user login 204 / `me` 200 / admin probe 403, admin login 204 / `me` 200 / admin probe 200, and CSRF/logout checks as documented there. If `API_PORT` changed, change `$base` too.

Optional full schema validation, with all data changes rolled back:

```powershell
Get-Content -Raw docs/phase02-validation.sql | docker compose exec -T db psql -v ON_ERROR_STOP=1 -U alerts -d alerts
```

## Repeatability and clean-volume validation

```powershell
docker compose up --build --detach --wait --wait-timeout 180
docker compose run --rm migrate
docker compose restart api
curl.exe --fail http://localhost:5165/health
docker compose down
# Keeps DB volume. Recreate and verify accounts/preferences persist.
docker compose up --detach --wait --wait-timeout 180
```

`migrate` should exit 0 on every rerun. Catalogs/accounts must not duplicate. Restarting/recreating the API must preserve existing passwords and flags. After source changes, use `up --build`. Rerun migrations with API stopped if a future schema change requires it. Startup ordering does not automatically recover services after a later DB failure; restore DB service and retry the request.

**Destructive local reset:** these commands delete this project's database (accounts, selections, events, deliveries). Use only for disposable data.

```powershell
docker compose down --volumes
docker compose up --build --detach --wait --wait-timeout 180
curl.exe --fail http://localhost:5165/health
docker compose exec -T db psql -U alerts -d alerts -c '\dt'
```

Recheck login and two migration rows on the newly created volume. Changing `POSTGRES_PASSWORD` in `.env` does not change the existing DB role password: restore its old value, change it deliberately in PostgreSQL, or reset disposable volumes.

## Logs, troubleshooting and limitations

Console logs are JSON. Bootstrap events: 3001 `MigrationStarted`, 3002 `MigrationCompleted`, 3003 `BootstrapCompleted`, 3004 `BootstrapFailed`, with structured outcomes. Failure logs omit raw exceptions/configuration values. Compose suppresses EF provider logs below Critical to avoid raw database errors during credential/bootstrap failures. Do not enable sensitive-data logging. For failures check DB health, credentials and seed requirements first.

Compose runs Development over local HTTP for approved bootstrap/cookie behavior. It is not an Internet-facing deployment. Data-protection keys are container-local: recreating the API invalidates sessions/CSRF tokens, so log in again. Keys are not exported/shared. Framework warnings about unencrypted/container-local keys are expected. Production needs HTTPS and separately managed persistent key protection. Images follow specified tags rather than immutable digests. No frontend, dispatch, senders or polling services are included.

If Docker reports access denied, use a terminal/account with Docker Desktop daemon access. For pulls/build failures check proxy/registry access. If DB is unhealthy, inspect `docker compose logs db`. If migration/API exits, inspect `ps --all` and service logs. For event 3004 confirm Development environment, DB password matches its volume, migrations completed, valid seed inputs and expected existing seed roles. Fix the cause and rerun `up`; there is no infinite restart loop. Stop foreground Compose with Ctrl+C; `down` removes containers/network while retaining DB data.
