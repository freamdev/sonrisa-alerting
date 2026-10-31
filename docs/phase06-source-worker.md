# Phase 06: mock source and polling validation

Status: APPROVED. Developer approved Phase 06 manual validation on 2026-10-31: "phase 06 manual test is approved". No subsequent phase is implemented.

## Contract and operation

The developer approved an array returned by GET with string fields `id`, `topic`, `title`, `body`, and `timestamp`. IDs are globally unique, case-sensitive, nonblank, and at most 256 characters. Titles are nonblank and at most 500 characters; body must be a string (empty is allowed). Topic codes match the persisted catalog exactly, including disabled topics; existing dispatch policy controls eligibility. Timestamp means occurrence time, requires an explicit timezone (`Z` or `+/-HH:mm`), and is normalized to UTC. Receipt time is assigned by the dispatcher. Duplicate IDs keep the first persisted event and never reroute changed content.

`GET /dev/mock-news` exists **only in Development**, is anonymous, and returns the fixture file as JSON. It has no upload/mutation endpoint. Missing/unreadable fixtures return HTTP 503. The file is reread on each request. The shipped fixture contains Finance, Weather, a duplicate Finance ID, a blank ID and an unknown topic. Invalid items are individually skipped with index-only warnings; invalid JSON/non-array envelopes fail the poll. HTTP responses are limited to 1 MiB. No external payload, URL, response body or exception details are logged by the source/worker.

`NewsSource__Enabled` defaults to false, so configure selections and destinations before enabling it. Options: `Endpoint` (absolute HTTP/HTTPS), `IntervalSeconds` (1..86400, default 30), `TimeoutSeconds` (1..120, default 10), and `FixturePath` (default `Fixtures/news.json`, relative to the API content root or absolute). Compose explicitly polls `http://localhost:8080/dev/mock-news` and mounts the host fixture directory read-only. `dotnet run` defaults to port 5165; override the endpoint for other ports. The mock route is available in Development even with polling disabled.

One BackgroundService runs sequential cycles with a fresh scope and a delay after completion. It passes normalized events to the existing dispatcher and Email/Slack adapters. Source errors consume no delivery attempts. Source and dispatch-stage failures log a sanitized stage and allow the next cycle. Shutdown cancels HTTP, database/dispatch calls, and interval waits. A first self-poll can race HTTP listener startup; it is handled as a transient failure. Existing startup-only pending recovery and three-attempt delivery policy remain unchanged. No periodic pending rescan, distributed coordination, management API or frontend is added.

## Automated validation

From the repository root:

```powershell
$env:DOTNET_CLI_HOME = Join-Path $PWD '.local/dotnet'
$env:NUGET_PACKAGES = Join-Path $PWD '.local/nuget/packages'
dotnet build Alerts.sln --no-restore
dotnet test Alerts.sln --no-build
dotnet format Alerts.sln --verify-no-changes --no-restore
dotnet test tests/Alerts.UnitTests --no-build --filter FullyQualifiedName~PollingThroughHttpAndBothAdaptersSurvivesFailureAndIgnoresRepolls --logger 'console;verbosity=detailed'
docker compose --env-file .env.example --profile channels config --quiet
```

Run `dotnet restore Alerts.sln` first on a fresh checkout. There are no new packages or migrations. Tests cover normalization/rejection, UTC conversion, duplicate ingestion, HTTP failures/cancellation, worker continuation and stop, interval cancellation, disabled polling, option validation, fixture reload/503 and the Development-only route. The controlled sample runs the real worker, HTTP client, dispatcher and both adapters against EF InMemory and fake SMTP/HTTP transports. A 503 is followed by two fixture polls; only two events and two one-attempt deliveries are stored (Alice selects Finance and both channels). These are focused in-process tests, not E2E or external-provider suites.

## Manual Compose acceptance

Use a local development database; selection changes below intentionally persist. Configure an ignored `.env` from `.env.example` with unique DB/admin/user passwords, `DevelopmentSeed__Enabled=true`, `DevelopmentSeed__UserEmail=user@example.test`, `NewsSource__Enabled=false`, and `NewsSource__IntervalSeconds=2`. Keep the default Mailpit SMTP settings. Start with:

```powershell
docker compose --profile channels up --build --detach --wait --wait-timeout 180
Get-Content -Raw docs/phase06-select-user.sql | docker compose exec -T db psql -U alerts -d alerts --set ON_ERROR_STOP=1 -f -
```

The SQL selects Finance and Weather independently of Email for that seeded user, without replacing existing preferences. If using an existing database, inspect its prior selections, events and destinations first: existing IDs do not reroute, and existing Slack selections require configured webhooks. A newly created development database gives the clearest two-Email sample.

Change `NewsSource__Enabled=true` in `.env`, then:

```powershell
docker compose --profile channels up --detach --wait --wait-timeout 180
curl.exe --fail http://localhost:5165/dev/mock-news
Start-Sleep -Seconds 6
docker compose logs --tail 40 api
docker compose exec -T db psql -U alerts -d alerts -c 'SELECT "ExternalEventId", "TopicId" FROM "AlertEvents" ORDER BY "Id";'
docker compose exec -T db psql -U alerts -d alerts -c 'SELECT "EventId", "ChannelId", "Status", "AttemptCount" FROM "Deliveries" ORDER BY "Id";'
```

Expect two events, two successful Email deliveries with one attempt each, and two messages at `http://localhost:8025` on fresh data. Later polls report zero new events and leave counts unchanged. Optional Slack uses the approved per-user mapping in `phase05-channels.md`; the automated controlled sample verifies both adapters with fake transports.

Edit `src/Alerts.Api/Fixtures/news.json` to add new unique IDs for manual publishing. Keep the original IDs to demonstrate deduplication. Global/user/channel enablement and independent selections are enforced by the dispatcher. No recipients at first ingestion means no retroactive delivery on repoll.

Demonstrate failure/recovery without changing credentials or stopping the API:

```powershell
Move-Item -LiteralPath src/Alerts.Api/Fixtures/news.json -Destination src/Alerts.Api/Fixtures/news.paused
try {
    Start-Sleep -Seconds 5
    curl.exe -i http://localhost:5165/dev/mock-news # 503
    docker compose logs --tail 20 api # failed during source; host stays alive
} finally {
    Move-Item -LiteralPath src/Alerts.Api/Fixtures/news.paused -Destination src/Alerts.Api/Fixtures/news.json
}
Start-Sleep -Seconds 5
docker compose logs --tail 20 api # successful polls resume, zero new events
docker compose stop api
docker compose logs --tail 10 api # News polling stopped
```

Keep/restore the tracked fixture after experiments. No volume deletion is required. Production and Staging do not map the mock route; tests verify this on the actual application host. Polling is separately configurable outside Development but the built-in mock endpoint remains unavailable there.

## Evidence and limits

On 2026-10-31 all 103 tests passed. An isolated `alerts-phase06` Compose stack built and started with fresh PostgreSQL, seeded accounts, Mailpit and a separate fixture mount. Automatic polling stored two events, created two successful one-attempt Email deliveries, and Mailpit reported two messages. A missing fixture returned 503; restoration resumed polling without increasing event/delivery counts. Graceful stop was checked through the worker log. Slack acceptance used fake HTTP, not a live Slack destination. See the implementation status for exact executed commands and outcomes.

Single-process, sequential dispatch and the previously documented crash/send limitations remain. A persisted event whose dispatch is interrupted relies on startup/internal pending recovery; repeat source IDs do not initiate pending scans. Source fixtures are intentionally small; large feeds and provider guarantees are outside scope. Phase 06 is approved; Phase 07 requires a separate explicit implementation request.
