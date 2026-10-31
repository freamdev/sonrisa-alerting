# Phase 04 routing validation

Phase 04 is awaiting human validation. It adds internal dispatch and fake senders only. No external DTO contract, HTTP dispatch endpoint, real Email/Slack I/O, source polling, management APIs or frontend is included.

## Run the acceptance checks

From the repository root in PowerShell:

```powershell
$env:DOTNET_CLI_HOME = Join-Path $PWD '.local/dotnet'
$env:NUGET_PACKAGES = Join-Path $PWD '.local/nuget/packages'
dotnet build Alerts.sln --no-restore
dotnet test Alerts.sln --no-build
dotnet format Alerts.sln --verify-no-changes --no-restore
dotnet test tests/Alerts.UnitTests --no-build --filter FullyQualifiedName~RoutingTests --logger 'console;verbosity=detailed'
```

Restore with `dotnet restore Alerts.sln` first on a fresh checkout. Existing cached dependencies suffice here; there are no new packages or migrations.

`ControlledDispatchShowsTwoPersistentDeliveriesAndFakeCalls` is the controlled programmatic invocation. It creates Alice, selects Finance independently of Email and Slack, calls `IAlertDispatcher.ProcessAsync`, then opens a new context to inspect delivery rows. The detailed output shows two `Succeeded` rows with one attempt each and one fake call for each channel. This test uses an isolated EF InMemory store, not the running Compose database.

Review the other `RoutingTests`: exact counts for empty/unrelated preferences, removing Slack across Finance and Weather, user isolation, global disablement, duplicate IDs, two failures then success, permanent failure at three calls, exponential waits, rechecking persisted flags/preferences between attempts, cancellation/restart, missing adapters and actual startup-service wiring. Duplicate-constraint exception handling is tested with a synthetic PostgreSQL exception; this is not a relational concurrency test. Existing Phase 02 PostgreSQL constraints remain unchanged.

## Internal invocation

Internal callers can resolve the scoped dispatcher from an application service scope:

```csharp
await using var scope = app.Services.CreateAsyncScope();
var dispatcher = scope.ServiceProvider.GetRequiredService<IAlertDispatcher>();
await dispatcher.ProcessAsync(new AlertEvent
{
    ExternalEventId = "controlled-sample-001",
    TopicId = 1,
    Title = "Controlled sample",
    Body = "Sample content",
    OccurredAt = DateTimeOffset.UtcNow
}, cancellationToken);
await dispatcher.RetryPendingAsync(cancellationToken);
```

Use the Application and Domain namespaces. Existing users must already have the independent selections. There is deliberately no public endpoint or CLI command that accepts arbitrary events. Phase 06 will map the agreed mock REST contract into this boundary.

With a configured, migrated local PostgreSQL database, inspect persisted outcomes using:

```powershell
docker compose exec -T db psql -U alerts -d alerts -c 'SELECT "Id", "ExternalEventId", "TopicId" FROM "AlertEvents" ORDER BY "Id";'
docker compose exec -T db psql -U alerts -d alerts -c 'SELECT "Id", "EventId", "UserId", "ChannelId", "Status", "AttemptCount", "LastError", "LastAttemptAt", "CompletedAt" FROM "Deliveries" ORDER BY "Id";'
```

These queries are read-only; they do not create sample data. Inspect the in-memory fake call lists through `IEnumerable<IChannelSender>` in the same process, as the controlled test does. Fake success means the fake accepted the call, not an external notification.

## Persistence and retry policy

- Validate a nonblank external ID (maximum 256), title (maximum 500), nonnull body, existing topic and nondefault UTC occurrence timestamp. Copy input and set receipt time internally. External JSON conversion remains Phase 06.
- Record each unique event even when it currently has no eligible recipients. Duplicate IDs return false and never reroute changed content. An explicit pending scan handles existing retries.
- On PostgreSQL, insert the event and all initially eligible deliveries in one transaction. Handle only the external-ID unique constraint as a duplicate; other persistence errors propagate. No migration/model change is needed.
- Check enabled user/topic/channel and both selections before every actual send, including after a retry delay. Temporarily ineligible deliveries stay pending without consuming attempts; re-enabling/reselecting can send older pending events at a later scan. Events that initially had no eligible delivery rows are not routed retroactively.
- Reserve each attempt in the DB before sender I/O. After failure, wait 1 second before attempt two and 2 seconds before attempt three. Due times derive from the persisted `LastAttemptAt`; elapsed time during shutdown counts. Tests inject a clock to avoid real waiting. Unknown sender exceptions are retryable in this fake-only phase; error storage/logs never include raw exception text or provider payloads.
- Success clears the safe error code and records completion. Third failure is terminal. An interrupted third reservation becomes final failure on recovery, with no fourth sender call. Missing adapters remain pending with `sender_unavailable` and no consumed attempt.
- `PendingDeliveryRecovery` runs one scan when the API starts. Internal callers can explicitly scan again; there is no periodic scan or source polling worker. Startup scan errors produce a safe log; pending rows remain persisted for a later scan/restart. Shutdown cancellation propagates to queries, waits and sender calls. The migration-only command does not start hosted services.
- A singleton gate serializes dispatcher operations within the supported single API process. Recovery reads pending rows in pages of 100. Sends/retries are sequential; a slow adapter delays later deliveries, while ordinary send exceptions are handled independently.
- This is not a multi-replica dispatcher. Multiple independently running dispatch processes are unsupported. DB reservation and external I/O cannot be atomic: a crash can consume an attempt without a send, or repeat an externally accepted send whose success was not persisted. Fake call records are process-local and reset at restart. Provider idempotency and distributed leases are outside this phase.

Developer decision on 2026-10-31: the proposed persisted retries, startup/explicit scans, conservative attempt reservations and pending-on-disable policy were approved, with exponential delays replacing the proposed fixed one-second interval. Phase 05 destination settings and Phase 06 REST payload decisions remain deferred.
