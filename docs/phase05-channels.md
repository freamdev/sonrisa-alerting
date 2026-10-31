# Phase 05 Email and Slack adapters

Phase 05 was explicitly approved by the developer on 2026-10-31, with full-content/multiple-message Slack behavior deferred for post-MVP validation. Phases 00–04 remain approved. Runtime Email and Slack adapters replace the recording fakes; the fake adapter remains available to isolated tests. No polling worker, management API or frontend is added. Phase 06 has not started and requires a separate explicit request.

## Agreed delivery contract

Developer decisions on 2026-10-31:

- Email uses MailKit SMTP, with Mailpit for local development. The recipient is the current Identity `Email` of the delivery's application user, read at each attempt. There is no alternate recipient or email-confirmation requirement in this MVP.
- Slack uses an incoming webhook targeting a Slack channel, mapped to each application user ID in server-owned secret configuration. No global webhook fallback, DM API, OAuth installation or user-facing configuration screen is implemented.
- Two users mapped to the same Slack channel produce separate messages and separate persisted delivery records. **Before production, decide whether shared destinations should receive one message per event or one per eligible user, and clarify ownership, access and destination management.** The MVP deliberately retains one delivery per event/user/channel.
- Local mocked transports suffice for acceptance. Automated tests never call Slack or a real SMTP provider.
- **Post-MVP validation:** decide whether sending an alert's entire content across multiple Slack messages is acceptable behavior. Review readability, message volume and partial-send/retry handling before implementing it. The MVP continues to truncate content into a single Slack message; multi-message delivery is not implemented or approved by this follow-up.

Destinations and channel preferences are independent. A mapping alone does not subscribe a user. Global flags and both independent selections are checked by the existing dispatcher before every attempt.

## Repeatable acceptance checks

From the repository root in PowerShell:

```powershell
$env:DOTNET_CLI_HOME = Join-Path $PWD '.local/dotnet'
$env:NUGET_PACKAGES = Join-Path $PWD '.local/nuget/packages'
dotnet restore Alerts.sln
dotnet build Alerts.sln --no-restore
dotnet test Alerts.sln --no-build
dotnet format Alerts.sln --verify-no-changes --no-restore
dotnet test tests/Alerts.UnitTests --no-build --filter FullyQualifiedName~ControlledAdaptersProduceTwoPersistedSuccessesWithLocalClientFakes --logger 'console;verbosity=detailed'
dotnet test tests/Alerts.UnitTests --no-build --filter FullyQualifiedName~ChannelSenderTests --logger 'console;verbosity=detailed'
docker compose --env-file .env.example --profile channels config --quiet
```

The controlled invocation composes the runtime `EmailChannelSender` and `SlackChannelSender` through DI and calls the dispatcher with an internal normalized event. A recording SMTP transport captures the actual MIME message; an `HttpMessageHandler` fake captures the actual Slack POST JSON. It uses an isolated EF InMemory database and reopens a context to assert **two Succeeded delivery rows, one attempt each**. Output shows the Email recipient/subject/body and Slack payload without exposing a webhook. This is the complete local mocked demonstration approved for MVP acceptance; it does not claim to exercise SMTP sockets, a Mailpit container, PostgreSQL transactions or a real Slack service.

Other channel tests cover Identity email updates/isolation, header injection, plain-text content, missing/invalid destinations, invalid SMTP configuration, HTTPS webhook restrictions, explicit Development mock opt-in, provider rejection/timeouts, safe persisted errors and logs, caller cancellation, shared Slack destinations and the existing maximum of three attempts. Existing routing tests retain enablement, unknown sender, duplicate-ID and restart coverage.

## Environment and Mailpit setup

Copy `.env.example` to `.env` if you have not already done so; keep your existing secrets if you already have a local file. Set a unique database password and configure development accounts as described in `phase03-compose.md`. Add these local Email values:

```dotenv
Channels__Email__Host=mailpit
Channels__Email__Port=1025
Channels__Email__Security=None
Channels__Email__From=alerts@example.test
Channels__Email__Username=
Channels__Email__Password=
MAILPIT_UI_PORT=8025
```

```powershell
docker compose --profile channels up --build --detach --wait --wait-timeout 180
docker compose --profile channels ps --all
curl.exe --fail http://localhost:5165/health
```

Open `http://localhost:8025` for captured messages. Mailpit is a profile-enabled, versioned official image with only the Web UI published on loopback; SMTP port 1025 is available inside the Compose network. Its message storage is ephemeral. Without `--profile channels`, the API can start but the default Mailpit hostname is unavailable; an attempted email will fail safely. Start the profile before dispatching samples. Mailpit startup is not a delivery-readiness guarantee; ordinary failures use the existing retries.

For an SMTP relay, change host/port, choose exactly `StartTls` (default, requires TLS) or `SslOnConnect`, and supply both username/password when authentication is required. `None` is allowed only in Development for the local sink. Certificate validation remains enabled. Configure `From` for the relay. No SMTP transcript logger is installed. MailKit is pinned to 4.17.0; its cryptography dependency is explicitly pinned to patched 10.0.14 rather than the vulnerable transitive 10.0.0.

ASP.NET Core does not load `.env` during `dotnet run`: set the corresponding `$env:Channels__Email__Host` etc. in your shell/IDE. For host-run SMTP, expose Mailpit's SMTP port on loopback using a local Compose override and use `localhost` with that port. The tracked Compose file exposes no host SMTP port.

## Slack configuration

Use the actual application user ID (not email or Slack channel ID). Inspect IDs locally without printing password hashes:

```powershell
docker compose exec -T db psql -U alerts -d alerts -c 'SELECT "Id", "Email" FROM "AspNetUsers" ORDER BY "Email";'
```

Set this secret in the ignored `.env`, replacing the placeholders locally:

```dotenv
Channels__Slack__WebhooksJson='{"APPLICATION_USER_ID":"https://hooks.slack.com/services/REPLACE/LOCALLY/ONLY"}'
Channels__Slack__AllowLocalMock=false
```

Multiple user IDs can map to different URLs or the same URL. Compose forwards the JSON unchanged. For `dotnet run`, set `$env:Channels__Slack__WebhooksJson` to the same JSON string in the process environment. Restart/recreate the API after configuration changes; no live configuration management is provided.

Only HTTPS `hooks.slack.com/services/…` on the default port is allowed for external delivery. Credentials in URI user-info, query strings and fragments are rejected. Redirects and cookies are disabled. For a developer-run fake receiver, set `Channels__Slack__AllowLocalMock=true` **and** run in Development; HTTP loopback URLs or a Compose service named `slack-mock` are then allowed. That receiver must accept POST JSON and return a 2xx response. No fake receiver service is installed: the acceptance test supplies a complete in-process HTTP fake and requires no listener.

Incoming webhooks are secrets. Do not print resolved `docker compose config`, process environments, options objects or webhook mappings into shared logs. The sample commands use `config --quiet`. No tokens/URLs are copied into delivery records. Provider bodies/raw exceptions are neither persisted nor logged; default HttpClient URL logging is removed from the Slack client.

## Dispatch, formatting and failure behavior

Use the existing internal scoped invocation in `phase04-routing.md` with migrated data and eligible users/selections if manually exercising configured transports. There is no public ingestion endpoint or sample-seeding command in this phase. For acceptance without modifying application data or contacting providers, use the controlled mocked test above.

Email is MIME `text/plain`; subject control characters are removed to prevent header injection. No untrusted HTML is rendered. Slack uses `plain_text` blocks with escaped notification fallback text, markdown parsing disabled, and link/media unfurls disabled. Slack text is truncated with an ellipsis to fit one 3000-unit section without splitting a surrogate pair; Email retains full content. Both use the normalized title/body and UTC occurrence timestamp rather than source DTOs.

The dispatcher recognizes these fixed, sanitized codes: `destination_missing`, `destination_invalid`, `configuration_invalid`, `provider_rejected`, `provider_unavailable`. Unexpected sender exceptions still use `send_failed`. These are reasons, not raw provider diagnostics. SMTP authentication/connection errors and HTTP timeout/transport failures use `provider_unavailable`; SMTP command rejection and non-2xx HTTP responses use `provider_rejected`. SMTP QUIT failure after message acceptance does not trigger a resend.

All adapter failures, including missing configuration and provider 4xx/429 responses, use the already approved three-total-attempt policy with 1-second then 2-second delays. There is no nested client retry loop or permanent/transient classification in this MVP. HTTP timeout is 30 seconds; the SMTP socket timeout is 30 seconds. Caller cancellation propagates, conservatively consuming an already reserved attempt. An HTTP 2xx response counts as provider acceptance, not proof of end-user receipt. Final failures need operator correction and a future agreed requeue policy; settings changes do not reset attempts. Existing one-time startup recovery can send previously pending events through these adapters once eligible/configured.

To add a third channel, implement `IChannelSender` with its stable catalog code, resolve destinations inside its adapter, and register it through DI. Add catalog data using the migration conventions and tests. Matching stays unchanged: it uses enabled catalogs and independent user selections. The current behavior for an unknown code remains `sender_unavailable`, pending with no consumed attempt; disabled channels remain suppressed. Never register two senders with the same code.

The single-process serialization, sequential sends, conservative attempt reservations and possible duplicate external side effect after a crash remain the Phase 04 limitations. No exactly-once, production readiness or real-provider delivery is claimed. Phase 05 approval does not authorize starting a later phase; wait for a separate explicit request.

Provider references: [MailKit API](https://mimekit.net/docs/html/T_MailKit_Net_Smtp_SmtpClient.htm), [Slack incoming webhooks](https://api.slack.com/messaging/webhooks), [Slack formatting](https://docs.slack.dev/messaging/formatting-message-text/), [Mailpit Docker setup](https://mailpit.axllent.org/docs/install/docker/).
