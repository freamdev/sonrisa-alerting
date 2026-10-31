# Project context — Alerts / newsletter-style notification service

## Product goal

Build a small Docker-runnable web application in which authenticated users subscribe to **topics** (e.g. Finance, Politics, Weather) and independently choose **delivery channels** (initially Email and Slack). When a new external news item for a subscribed topic arrives, notify the user through **all of that user's enabled delivery channels**. The external provider is a **mock REST API**, not a real news service. Add a small admin UI.

## Non-negotiable subscription semantics

**No user-level topic/channel pairs.** Store a user's topic choices and channel choices independently. Example: User Alice subscribes to Finance and Weather, and enables Email and Slack. Every eligible Finance/Weather event goes to both Email and Slack. Disabling Slack stops Slack for **all** of Alice's subscribed topics. Do not create a subscription matrix or a per-topic channel selector. Zero selected channels means no deliveries; zero selected topics means no deliveries.

## Roles / authorization

- Users can sign in and manage only their own subscribed topics and selected delivery channels (and any personal delivery addresses/settings explicitly agreed with the developer).
- An admin **is also a regular user** and retains all personal settings/view capabilities.
- Admins may open admin-only functions to list/manage users, **delete users**, and globally enable/disable topics and channels. Original brief also includes enabling/disabling users; implement a global user enabled flag unless the developer explicitly changes it.
- Global disabling of a topic or channel prevents processing/sending through it regardless of personal preferences; enabling globally does **not** automatically subscribe anyone. Disabling a user prevents deliveries and access according to the policy agreed in Phase 1. Deleting users requires clear cascade/history policy agreed before implementing.
- All permissions must be enforced by the server, not Angular route guards alone.

## Domain and processing

- Normalize external REST payloads into a minimal internal `AlertEvent` model (external unique event ID, topic, title, body/content, timestamp); do not leak external source DTOs into delivery senders.
- Source identity check is intentionally simple: enforce uniqueness on the external event ID (or source + ID if multiple sources are later agreed). Do not add elaborate dedupe, content similarity, watermarking or a message broker.
- A simple ASP.NET Core `BackgroundService` polls the mock REST source on a configurable interval. Make error handling safe and polling stoppable.
- Processing steps: poll -> normalize/validate -> ignore previously seen IDs -> match enabled topic selections and enabled users -> expand across globally enabled and personally selected channels -> create/record deliveries -> send through channel abstraction -> record outcomes.
- Record failed delivery attempts and retry, with **at most three total send attempts per delivery**; after the third failure, record a final failure and stop. No endless retries. Keep implementation simple. Distinguish source polling failures from delivery attempts.
- A channel interface enables later add-ons without modifying topic matching. Initial adapters: Email and Slack; optional fake sender for tests.

## Technology and scope

- Backend: .NET / ASP.NET Core, EF Core and ASP.NET Core Identity, simple modular monolith with separable domain/application/infrastructure concerns.
- Frontend: simple Angular SPA for personal and admin views; avoid unnecessary frameworks or elaborate state management.
- Database: relational SQL DB. **Provider is not yet specified**; ask the developer to select SQL Server vs PostgreSQL (or approved alternative) at project initialization, before binding EF migrations and Compose to one provider.
- Runtime: Docker and Docker Compose; official DB image; easy documented fresh checkout startup; environment-based configuration/secrets.
- Logging/operations: structured logging and `/health` endpoint, no elaborate monitoring stack.
- Tests: automated unit tests for payload normalization, topic/channel matching, authorization boundaries/policies, failure/retry and simple duplicate-ID handling. Use focused provider-client fakes. Full E2E and real-provider integration tests are **out of scope**.
- Build with existing libraries where possible. No microservices, Kafka/RabbitMQ, distributed scheduler, event streaming, complex event taxonomy, realtime updates, analytics, dashboards, enterprise-scale HA, or real news feeds.

## Unresolved choices — ask at the phase that needs them

1. SQL database provider: **Phase 0/1**. Do not quietly decide based on preference.
2. Login delivery and local dev bootstrap (cookie authentication with same-origin deployment is a sensible default; confirm account creation/seed admin, and disabled-user sign-in behavior): **Phase 1**.
3. External source JSON example/REST contract, event topic mapping and timestamp meaning: **Phase 4 or 6**, before implementing mapping, if not already specified. For a mock, propose a minimal contract and get agreement.
4. Slack destinations: per-user Slack DM vs channel/webhook; who configures credentials and destinations. Email SMTP/dev mail sink choice: **Phase 5**. This affects whether Slack can truly reach an individual user's selection, so must be agreed explicitly.
5. Delete-user semantics and handling of old delivery records: **Phase 2 or 7**, before implementing deletes.
6. Whether admin disabled user may still sign in (or cannot): **Phase 1**. Regardless, disabled users must not receive notifications.

These are **blocking choices**, not invitations for the agent to expand scope. Ask a concise question and pause; do not substitute a more complex feature.

## Recommended minimal persistent concepts

- Identity user: id, identity fields, isEnabled, role(s), optional destination fields agreed later.
- Topic: id, stable key, display name, isEnabled.
- Channel: id, stable key (`email`, `slack`), isEnabled.
- UserTopicSelection: userId + topicId unique.
- UserChannelSelection: userId + channelId unique.
- AlertEvent: id, externalEventId unique, topicId, title, body, occurredAt/receivedAt.
- Delivery: eventId, userId, channelId, status, attemptCount, lastError, attempt timestamps. Unique event+user+channel; no requirement for a per-topic channel subscription.

Model names can vary but semantics cannot. Use FK/unique constraints. Avoid storing unnecessary copies of credentials in delivery rows.
