# Phase 02 persistence and validation

Phase 02 adds domain persistence to the existing Identity context and migration chain. No selection HTTP endpoints, routing, senders, polling, UI, Compose or health endpoint are implemented.

## Schema and decisions

| Table | Identity, constraints and purpose |
| --- | --- |
| `Topics` | Integer ID; unique code (max 64), display name (max 128), global `IsEnabled`. Seeds: 1 finance, 2 politics, 3 weather. |
| `Channels` | Integer ID; unique code (max 64), display name (max 128), global `IsEnabled`. Seeds: 1 email, 2 slack. |
| `UserTopicSelections` | Composite primary key `(UserId, TopicId)`; independent topic choices. |
| `UserChannelSelections` | Composite primary key `(UserId, ChannelId)`; independent channel choices. |
| `AlertEvents` | Bigint ID; globally unique, case-sensitive external ID (max 256), topic FK, title (max 500), body, `OccurredAt`, `ReceivedAt`. |
| `Deliveries` | Bigint ID; unique `(EventId, UserId, ChannelId)`, status (`Pending`, `Succeeded`, `Failed`), attempts constrained to 0..3, nullable safe error summary (max 512), created/last-attempt/completed timestamps. |

User IDs remain Identity strings and `IsEnabled` remains the existing Phase 01 field. FKs have supporting indexes; deliveries also have a status/attempt-count index for later processing. Times use PostgreSQL `timestamp with time zone`; supply UTC `DateTimeOffset` values to Npgsql. External IDs use simple exact equality; no content deduplication is introduced.

Developer-approved deletion policy (2026-10-31): hard-delete the account and cascade its topic choices, channel choices and delivery records. Shared events, topics and channels survive. Referenced topics/channels/events use restrictive deletion to preserve integrity; global disabling is available in the schema. No deletion API is added in this phase.

EF `HasData` puts fixed catalog reference data in the migration, available in every environment including Development. Reapplying migrations or restarting the host does not insert duplicates or reset enablement. Seeds create no personal selections. Development accounts retain their existing opt-in credential-based bootstrap. Migrations remain explicit, never automatic on startup.

`ISelectionService` is a scoped application contract backed by EF `SelectionService`. It reads topic/channel IDs and independently adds/removes one choice. Sequential repeated adds/removes are idempotent. Missing users/catalog entries throw `KeyNotFoundException`; blank user IDs throw `ArgumentException`. Global flags do not alter stored choices or automatically subscribe users. The service is internal: future HTTP endpoints must derive the current user ID from the authenticated principal and enforce authorization/CSRF. No HTTP route accepts arbitrary user IDs now. Concurrent duplicate adds are protected by the database key and can surface a persistence conflict; HTTP conflict handling belongs with the later API.

Delivery state is storage only. A new CLR delivery is Pending with zero attempts. The database enforces valid status values and the numeric attempt range; the later routing phase must enforce state transitions and at most three actual sends. `LastError` is reserved for a safe code/summary, never raw exception messages, credentials, destinations or provider response bodies. No external data is written there in this phase.

## Exact manual validation

From the repository root, use .NET 10 and a fresh PostgreSQL database. An optional disposable database:

```powershell
$env:POSTGRES_PASSWORD = 'Local!aA1' + [guid]::NewGuid().ToString('N')
docker run --rm --detach --name alerts-phase02-validation --publish 127.0.0.1:55433:5432 --env POSTGRES_PASSWORD --env POSTGRES_DB=alerts postgres:17
docker exec alerts-phase02-validation pg_isready -U postgres -d alerts
$env:ConnectionStrings__Alerts = "Host=127.0.0.1;Port=55433;Database=alerts;Username=postgres;Password=$env:POSTGRES_PASSWORD"
```

Wait until `pg_isready` reports accepting connections. If needed, set the shell-local CLI home/cache from the root README. Then:

```powershell
dotnet restore Alerts.sln
dotnet tool restore
dotnet build Alerts.sln --no-restore
dotnet test Alerts.sln --no-build
dotnet format Alerts.sln --verify-no-changes --no-restore
dotnet ef database update --project src/Alerts.Api --no-build
dotnet ef database update --project src/Alerts.Api --no-build
dotnet ef migrations has-pending-model-changes --project src/Alerts.Api --no-build
New-Item -ItemType Directory -Force .local | Out-Null
dotnet ef migrations script --idempotent --project src/Alerts.Api --no-build --output .local/phase02-migrations.sql
Get-Content -Raw .local/phase02-migrations.sql | docker exec --interactive alerts-phase02-validation psql -v ON_ERROR_STOP=1 -U postgres -d alerts
Get-Content -Raw .local/phase02-migrations.sql | docker exec --interactive alerts-phase02-validation psql -v ON_ERROR_STOP=1 -U postgres -d alerts
Get-Content -Raw docs/phase02-validation.sql | docker exec --interactive alerts-phase02-validation psql -U postgres -d alerts
```

Expect two migrations on a fresh DB, “already up to date” on the second update, no pending model changes, and `PASS: seeds, independent selections, uniqueness, FKs, attempt/status limits, deletion/history` from the SQL check. A fresh DB may log a failed migration-history lookup before creating the history table successfully. The SQL check is a manual validation artifact, uses `ON_ERROR_STOP`, and rolls back its changes. It expects exactly the five shipped catalog entries. Expected uniqueness/FK/check violations are caught; an unexpected result raises an error. It demonstrates independent adds/removes and proves account deletion removes only that user's rows while preserving another user's delivery and shared records. PostgreSQL sequences can advance despite rollback, which is harmless.

The automated `SelectionTests.SelectionsAreIndependentRepeatablePersistedAndIsolatedByUser` demonstrates the application service across separate EF context instances, repeated add/remove operations, zero selections and user isolation. Other unit tests cover invalid references, catalog bootstrap and global flags. InMemory tests do not establish relational guarantees; the SQL check above does.

To verify authentication with this schema, follow the credential/bootstrap and API commands in [Phase 01 validation](phase01-auth.md), using the Phase 02 connection string/container above. Expect regular user login 204, `me` 200 and admin probe 403; admin login 204, `me` 200 and probe 200. The existing automated authentication suite also remains required.

After stopping any host you started, remove only the optional disposable database:

```powershell
docker stop alerts-phase02-validation
Remove-Item Env:POSTGRES_PASSWORD,Env:ConnectionStrings__Alerts -ErrorAction SilentlyContinue
```

This container has no persistent volume. Existing databases should be backed up before applying migrations; the migration's `Down` removes the domain tables and their data, so rollback is not a data-preservation operation.
