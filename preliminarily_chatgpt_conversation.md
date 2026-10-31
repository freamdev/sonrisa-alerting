# Plan Alerting System
> *Last updated: 10/31/2026, 11:09:49 AM*

**You** *(10/31/2026, 10:56:25 AM)*

I received the following task from my manager:

"We want users to be able to set up alerts so they get notified when\
something important happens in the world — like breaking news, market\
movements, natural disasters, that kind of thing. Should work for both email\
and Slack. Make it flexible enough that we can add more channels later.\
We need an admin view too."



With the limited deadline I would like to stick to technologie wherever possible to ones I'm familiar with. .NET and some simple Angular frontend for the users/admin view.



As a step one here is my understanding and decesions based on this brief:

- Channels: email, slack, open implementation for later extendability
- Topics: type of news received, finance, political, weather
- Users: admin, regular
- One admin view, where the admin can enable disable channels, topics and users
- One user view, where the user can enable/disable certain channels and topics
- Database: SQL
- Automated testing: new message conversion, channel subscribing and message handling and authentication/authorization should be tested. E2E and integration tests are not feasable in the scope.



General rules of implementation

- The project should be easy to setup and should run in docker
- Scalability: In my opinion such a simple application need not to work with any microservice achitecture in mind
- We should use exsisting libraries whereever its possible instead of reinventing the wheel



In this chat I want to go over the planning the implementation phases that I'll accomplish using codex agents in a local enviroment. I want to set up each phase to end in a sensible milestone and the implementation agent to wait at the end of that milestone for human validation.



For this project real life news source wont be neccessary we'll assume they are provided to us tough some API, for the mock implementation we will assume a REST API.



Proposed stages:

1: Creating a simple .net web app with authentication

2: Creating the database storing the users and channel settings

3: Creating the docker image for the database and ensuring connections and migrations running between the app and the database

4: Creating the slack and email handling modules

5: Creating mock news sources and wiring them in to the message handling module

6a: Creating the frontend facing endpoints and models

6b: Crating the base angular application

7: Wire the backend and frontend applications together





based on this more detailed instructions let me know if anything seems missing, would need an update

---

**ChatGPT** *(10/31/2026, 10:56:35 AM)*

Your interpretation is broadly sound, and the technology choices fit the deadline. I would keep the monolith, .NET, Angular, SQL, and Docker approach.

The main thing I'd change is that the current stages focus heavily on infrastructure and UI, while the core domain behavior of an **alerting system** isn't yet explicit enough. In particular, I'd introduce the concepts of an incoming event, subscription, routing, and delivery before implementing Slack/email.

A few things look missing or worth tightening:

- **Subscriptions need to be a first-class concept.** A user doesn't merely enable a topic and a channel independently; you need to decide whether `"Finance + Email"` and `"Finance + Slack"` are separate subscriptions. I'd recommend they are. This makes later channels much easier.
- **Incoming news needs a normalized internal model.** Your external REST source shouldn't flow directly into Slack/email code. Something like `IncomingNews -> AlertEvent -> matching subscriptions -> deliveries`.
- **You need a background processing mechanism.** Alerts arrive independently of HTTP requests. ASP.NET Core `BackgroundService` is entirely reasonable for this project; you don't need RabbitMQ/Kafka.
- **Delivery failure behavior should be defined.** At minimum, log failed deliveries and retry a small number of times. Otherwise a temporary Slack/email failure silently loses notifications.
- **Idempotency/deduplication matters.** If the mock REST API returns the same news item twice, users shouldn't receive it twice.
- **Channel configuration is different from user subscriptions.** Admin enabling Slack globally is application configuration. A user selecting Slack for Finance is a subscription. I'd model those separately.
- **Authentication and authorization need explicit rules.** `Admin` and `User` roles are enough. ASP.NET Core Identity is probably preferable to building authentication yourself.
- **Slack/email credentials must stay outside the database/source code where appropriate.** Environment variables / Docker secrets/configuration are enough for this scope.
- **Observability should be part of the acceptance criteria.** Structured logs and `/health` are cheap and extremely useful in a Dockerized service.
- I'd use **Docker Compose**, rather than thinking of step 3 as "creating the Docker image for the database." Use an official SQL database image and compose the application, frontend and database together.

I would also slightly change the order of your milestones. A structure like this gives Codex agents much cleaner boundaries and gives you something meaningful to validate after every stage.

### Phase 0 — Solution skeleton and architecture contracts

Before implementation, establish the solution structure and a few domain interfaces.

For example:

```text
/backend
  Alerting.Api
  Alerting.Application
  Alerting.Domain
  Alerting.Infrastructure
  Alerting.Tests

/frontend
  alerting-web

docker-compose.yml
README.md
```

You don't necessarily need strict Clean Architecture, but keeping domain/application concepts separate from SMTP, Slack and EF Core will help enormously.

Define concepts such as:

```text
User
Topic
Channel
Subscription
AlertEvent
Delivery
```

And interfaces roughly equivalent to:

```csharp
INewsSource
IChannelSender
IAlertDispatcher
```

**Human validation milestone:** solution builds, architecture/domain terminology is agreed, no real feature implementation yet.

This is worth adding because otherwise individual coding agents may independently invent incompatible abstractions.

---

### Phase 1 — Authentication and authorization

Your existing phase 1 is good.

I'd use ASP.NET Core Identity with:

```text
User
Admin
```

roles.

Implement only enough UI/API to prove:

```text
anonymous -> rejected
user -> authenticated
admin -> authenticated + admin endpoint access
user -> forbidden from admin endpoint
```

You could initially use cookie authentication if Angular and ASP.NET Core are deployed together. There's no particular benefit to implementing your own JWT machinery unless deployment requirements demand it.

**Human validation milestone:** login works and authorization boundaries work.

---

### Phase 2 — Persistence and domain model

This is broader than just "users and channel settings."

I'd persist:

```text
Users
Topics
Channels
Subscriptions
AlertEvents / processed external event IDs
Deliveries
```

Something approximately like:

```text
User
  Id
  Enabled

Topic
  Id
  Name
  Enabled

Channel
  Id
  Type
  Enabled

Subscription
  UserId
  TopicId
  ChannelId
  Enabled

AlertEvent
  Id
  ExternalId
  TopicId
  Title
  Body
  CreatedAt

Delivery
  AlertEventId
  SubscriptionId
  Status
  AttemptCount
  LastAttemptAt
  Error
```

`Delivery` might look excessive at first, but it makes retrying, diagnostics and testing much cleaner.

Use EF Core migrations from the beginning.

**Human validation milestone:** schema reviewed, migration can create an empty database, basic repository/application operations are tested.

---

### Phase 3 — Dockerized local environment

Your current phase 3 fits here.

I'd make this:

```text
docker compose up
```

bring up at least:

```text
database
backend
```

and eventually frontend.

Use the official database image rather than building your own DB image.

Add:

```text
health check
database readiness
automatic or documented migrations
seed admin account / development seed data
environment-based configuration
```

One caution: I wouldn't blindly run production migrations every time the application starts. For the assignment/demo, automatic migrations are acceptable, but document that decision.

**Human validation milestone:** fresh checkout → one documented command → working backend/database.

---

### Phase 4 — Alert processing pipeline

This is the biggest missing phase in your current proposal.

Before Slack/email, implement:

```text
AlertEvent arrives
        ↓
classify/normalize topic
        ↓
find active subscriptions
        ↓
create deliveries
        ↓
dispatch through IChannelSender
```

Initially use a fake sender:

```csharp
FakeChannelSender
```

which just records or logs deliveries.

This lets you test the hardest business rules independently of Slack and SMTP.

Tests should cover things such as:

```text
disabled user -> no delivery
disabled topic -> no delivery
disabled channel -> no delivery
disabled subscription -> no delivery

Finance event
+ user subscribed Finance/Email
+ user subscribed Weather/Email
= exactly one Finance/Email delivery

same external event twice
= no duplicate notification
```

**Human validation milestone:** feed an event into the application and verify exactly which users/channels would receive it.

This is probably the most important checkpoint in the project.

---

### Phase 5 — Email and Slack adapters

Now implement:

```text
EmailChannelSender : IChannelSender
SlackChannelSender : IChannelSender
```

The dispatcher should not know anything about SMTP or Slack.

That gives you the extensibility your manager requested:

```text
TeamsChannelSender
SmsChannelSender
PushNotificationChannelSender
```

can eventually be added without modifying alert-routing logic.

I'd also define retry behavior here. You don't need a sophisticated queue. Something like 3 attempts with logged failures is enough for the project.

**Human validation milestone:** manually trigger one alert and see it delivered to both test email and test Slack destinations.

---

### Phase 6 — Mock REST news source and polling

Your proposed phase 5 comes after channel implementation, which is reasonable, but explicitly introduce an ingestion boundary:

```csharp
public interface INewsSource
{
    Task<IReadOnlyCollection<ExternalNewsItem>> GetNewItemsAsync(...);
}
```

Then:

```text
MockRestNewsSource
    ↓
NewsMapper
    ↓
AlertEvent
    ↓
AlertDispatcher
```

Use an ASP.NET Core `BackgroundService` to poll it.

For the demo, make the polling interval configurable.

Important test:

```text
same API item returned on polling cycle 1 and cycle 2
=> exactly one AlertEvent / notification
```

**Human validation milestone:** mock source produces an item and the complete backend pipeline sends the appropriate alerts without manual intervention.

At this point you have the actual product, just without its management UI.

---

### Phase 7 — Backend APIs for user/admin management

This corresponds to your `6a`.

I'd separate endpoints roughly as:

```text
/api/me/subscriptions
/api/me/channels
/api/me/topics

/api/admin/users
/api/admin/topics
/api/admin/channels
```

Admin:

```text
enable/disable user
enable/disable topic globally
enable/disable channel globally
```

User:

```text
view available topics/channels
enable/disable own subscriptions
```

Make sure ownership rules live in the application/backend, not Angular.

**Human validation milestone:** APIs can fully configure the system using Swagger/Postman without the Angular application.

---

### Phase 8 — Angular frontend

Then your `6b + 7` can largely become one frontend milestone.

I'd keep it deliberately boring.

User:

```text
Login

My Alerts
Finance     Email ✓   Slack ✓
Politics    Email ☐   Slack ✓
Weather     Email ✓   Slack ☐
```

Admin:

```text
Users
Topics
Channels
```

You don't need a big frontend architecture for this. Angular services + route guards + a handful of components are plenty.

**Human validation milestone:** entire application usable without Swagger/Postman.

---

### Phase 9 — Hardening/documentation

I'd reserve a small final milestone for the things that otherwise tend to be forgotten:

```text
README
docker-compose instructions
environment variable documentation
seed/demo setup
unit tests
failure/retry tests
health endpoint
structured logging
basic security review
```

Also document explicit non-goals:

```text
No production news provider
No distributed queue
No microservices
No high availability
No E2E suite
No production-grade Slack OAuth onboarding
```

That is useful because it makes deliberate scope decisions distinguishable from missing functionality.

One architectural picture I'd keep in mind throughout implementation is:

```text
                       ┌─────────────┐
                       │ Mock REST   │
                       │ News API    │
                       └──────┬──────┘
                              │
                       BackgroundService
                              │
                              ▼
                     ┌────────────────┐
                     │   INewsSource  │
                     └───────┬────────┘
                             │
                             ▼
                      ┌────────────┐
                      │ AlertEvent │
                      └──────┬─────┘
                             │
                      Alert Dispatcher
                             │
                   Find subscriptions
                    ┌────────┴────────┐
                    ▼                 ▼
             Email Sender       Slack Sender
                    │                 │
                    ▼                 ▼
                  Email             Slack

Angular ─────► ASP.NET API ─────► SQL Database
                    ▲
             User/Admin config
```

This is still a **modular monolith**, not a microservice architecture. For the scope you've described, I think that's exactly the right tradeoff.

Your testing scope also makes sense, but I'd revise your proposed test categories slightly. Rather than "new message conversion, channel subscribing and message handling and authentication/authorization", I'd explicitly make the unit-test targets **external-news → AlertEvent conversion, subscription matching/routing, disabled-user/topic/channel behavior, duplicate-event handling, delivery retry/failure handling, and authorization policies**. The Slack and email adapters themselves only need relatively small tests around mapping/configuration; most behavior should be tested against the channel abstraction.

The most important modification to your original seven stages is therefore **adding the alert-processing/domain milestone before implementing Slack/email**. That will give your Codex agents a much clearer architecture to work against and will substantially reduce the chance that the project turns into controllers directly calling Slack/email libraries.

---

**You** *(10/31/2026, 11:06:51 AM)*

1. **Subscriptions need to be a first-class concept.** A user doesn't merely enable a topic and a channel independently; you need to decide whether `"Finance + Email"` and `"Finance + Slack"` are separate subscriptions. I'd recommend they are. This makes later channels much easier. -> This would complicate the scope, the users can subscribe to topics and then subscribe in what format they want said topics, they cannot have topic/channel pairings. Think of it more like a newsletter that I want to receive in my slack
2. **Incoming news needs a normalized internal model.** Your external REST source shouldn't flow directly into Slack/email code. Something like `IncomingNews -> AlertEvent -> matching subscriptions -> deliveries`. -> That is self evident
3. **You need a background processing mechanism.** Alerts arrive independently of HTTP requests. ASP.NET Core `BackgroundService` is entirely reasonable for this project; you don't need RabbitMQ/Kafka. -> This should be covered in wiring but its good to define it, yes a simple background process will do fine.
4. **Delivery failure behavior should be defined.** At minimum, log failed deliveries and retry a small number of times. Otherwise a temporary Slack/email failure silently loses notifications. -> Log failiure and retry attempts, after 3 attempts give up.
5. **Idempotency/deduplication matters.** If the mock REST API returns the same news item twice, users shouldn't receive it twice. -> For the most part assume the external services work correctly, a simple unique event ID in the request API can be checked but nothing more complicated
6. **Channel configuration is different from user subscriptions.** Admin enabling Slack globally is application configuration. A user selecting Slack for Finance is a subscription. I'd model those separately. -> I don't see how is this not evident
7. **Authentication and authorization need explicit rules.** `Admin` and `User` roles are enough. ASP.NET Core Identity is probably preferable to building authentication yourself. -> Admins are regular users who have access to the admin view, where they can delete users and disable channels alltogether from processing events. Otherwise they have acces to the own personal view.
8. **Slack/email credentials must stay outside the database/source code where appropriate.** Environment variables / Docker secrets/configuration are enough for this scope. - Approved
9. **Observability should be part of the acceptance criteria.** Structured logs and `/health` are cheap and extremely useful in a Dockerized service. - Approved
10. I'd use **Docker Compose**, rather than thinking of step 3 as "creating the Docker image for the database." Use an official SQL database image and compose the application, frontend and database together. - Approved



the general Phase reorganization is acceptable, if there is nothing else I want you to generate explicit instructions list for the local codex agent. These instructions should contain every information relevant for a given phase, enough context or a pointer to a general context file to avoid drifting and explcit instructions on awaiting human interaction at the end of any phase and explicit instructions to ask questions to the developper in case of uncertanity.

---

**ChatGPT** *(10/31/2026, 11:07:01 AM)*

I’ll turn the agreed scope into a reusable Codex handoff: one shared project context, a set of phase-specific instructions, and a mandatory stop-and-review rule after every milestone. I’ll keep topic subscriptions and delivery-channel preferences independent, as you specified.

---

**ChatGPT** *(10/31/2026, 11:09:19 AM)*

bash -lc mkdir -p /mnt/data/codex_alerts_plan/phases && cat > /mnt/data/codex_alerts_plan/AGENTS.md <<'EOF'
# Codex operating instructions — Alerts application

Read `PROJECT_CONTEXT.md`, `PHASES.md`, and the requested `phases/NN-*.md` **before making changes**. These files define the scope and take precedence over architectural preferences or assumptions. Read the repository as it exists; do not assume an earlier phase was completed unless code and its human-approval record support that conclusion.

## Execution contract

1. Work on **only one explicitly assigned phase**. Do not start the next phase, even when it looks easy or logically connected. Do not silently implement future-phase work.
2. Before editing, give the developer a short plan, identify existing relevant code, and state any blocking questions. **Ask the developer** when there is uncertainty that changes behavior, security, data persistence, integration contracts, or architecture. Do not guess on such decisions; stop and wait for their response. For minor implementation details, choose the simplest option consistent with project context and document it.
3. Follow existing repository conventions and use supported .NET/Angular libraries instead of bespoke implementations. Prefer a modular monolith. Keep dependencies and code to the minimum needed for the assigned milestone.
4. Write appropriate automated **unit tests** within the phase. Do not introduce E2E suites or external-provider integration tests. Run the build/tests/format checks feasible locally. Report what actually ran, what failed, and what could not run.
5. Never commit credentials. Read secrets from environment/configuration, provide `.env.example` placeholders, and avoid logging passwords/tokens/webhook URLs.
6. At completion, update `IMPLEMENTATION_STATUS.md` with changes, test evidence, exact human-validation commands, known limitations, and open decisions. Mark the phase **AWAITING HUMAN VALIDATION**, not approved.
7. Finish with a succinct validation report: changed files, behavior implemented, commands + results, how the developer can verify manually, deviations, questions, and a **clear request for approval or feedback**.
8. **STOP. Do not continue to the next phase, mark approval, or treat silence as approval.** Resume only after the developer explicitly approves the milestone and explicitly requests the next phase. If corrections are requested, work only within the current phase and present it for review again.
9. If you discover a defect from an earlier phase, report it and ask whether to fix it within this phase before making substantial out-of-phase changes.

## Approval bookkeeping

`IMPLEMENTATION_STATUS.md` is a running handoff log. Agents may record `AWAITING HUMAN VALIDATION` and evidence, but **only explicit developer feedback** justifies recording `APPROVED`. A new agent should verify the previous phase has been approved before starting the next one; if approval is unclear, ask. Do not create a git commit unless the developer has asked you to.

## Source of truth

- `PROJECT_CONTEXT.md`: product behavior, domain semantics, technology and scope limits.
- `PHASES.md`: sequence, invariants and stop points.
- `phases/NN-*.md`: implementable checklist and acceptance criteria for a single milestone.
- Existing code and developer decisions: reconcile differences with the developer rather than silently overriding the written scope.
EOF
cat > /mnt/data/codex_alerts_plan/PROJECT_CONTEXT.md <<'EOF'
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
EOF
cat > /mnt/data/codex_alerts_plan/PHASES.md <<'EOF'
# Phase plan and launch protocol

## How to start one phase

Use a fresh Codex session or give the agent this exact directive, substituting NN and filename:

> Implement **Phase NN only** per `AGENTS.md`, `PROJECT_CONTEXT.md`, `PHASES.md`, and `phases/NN-<name>.md`. Read current code and `IMPLEMENTATION_STATUS.md` first. Explain your plan and ask any blocking questions before editing. At the end, execute feasible tests, document validation steps, mark `AWAITING HUMAN VALIDATION`, and **STOP for my explicit approval**. Do not begin another phase.

If resuming a paused phase, ask Codex to address only the review feedback for that phase.

## Milestones

| Phase | Scope | Manual acceptance gate |
|---|---|---|
| 00 | Skeleton, architecture, agreed decisions | Builds; boundaries/contracts reviewed |
| 01 | Identity, authentication, authorization | Login works; user denied admin action; admin has user view |
| 02 | Persisted domain data and EF migrations | Schema and data behavior inspected; tests pass |
| 03 | Docker Compose, local bootstrap, health | Clean checkout launches DB/backend in Compose |
| 04 | Alert routing, persistence, retry, fake sender | Deterministic matching and max-three attempts |
| 05 | Email and Slack senders | Controlled sample to each agreed test destination |
| 06 | Mock REST provider and BackgroundService polling | Event automatically ingested once and dispatched |
| 07 | Personal/admin HTTP APIs | Full management achievable using API client |
| 08 | Angular personal/admin UI and integration | End-to-end manual scenario through browser |
| 09 | Final test review, docs, security/operability | Fresh install and demo scenario validated |

## Cross-phase acceptance invariants

- Topic choices and channel choices remain **independent**; never implement topic/channel combinations as preferences.
- Admin is a normal account with additional privileges, not a separate account class.
- Global topic/channel/user enable flags are respected at send time.
- External duplicate event IDs do not cause repeated deliveries.
- Every delivery is attempted no more than **3 times total**. Final failure stays visible in logs/persistence.
- No real news integration, message broker or distributed architecture.
- Each milestone ends with human validation; agents do not advance automatically.
EOF
cat > /mnt/data/codex_alerts_plan/IMPLEMENTATION_STATUS.md <<'EOF'
# Implementation status / human review log

**Current phase:** 00 — NOT STARTED

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
EOF
cat > /mnt/data/codex_alerts_plan/phases/00-skeleton.md <<'EOF'
# Phase 00 — Scaffold and architecture contract

**Read first:** `../AGENTS.md`, `../PROJECT_CONTEXT.md`, `../PHASES.md` (paths from repository root if needed).

## Goal
Create a minimal, buildable repository skeleton that subsequent phase agents can extend predictably, without implementing product behavior.

## Before coding — required developer decisions
- Ask which relational SQL provider to target (SQL Server, PostgreSQL, or another approved option).
- Confirm .NET and Angular major versions, or propose current stable supported versions and ask for agreement if version compatibility is uncertain.
- Clarify where the repo root is and whether a solution already exists. Preserve working code.

## Work
- Create .NET solution with backend API, domain/application/infrastructure separation appropriate to a **small modular monolith**, and one unit-test project. Prefer fewer projects if simplicity warrants; explain layout.
- Add empty Angular folder placeholder only; Angular initialization belongs to Phase 08.
- Define or document domain vocabulary: `AlertEvent`, `Topic`, `Channel`, `UserTopicSelection`, `UserChannelSelection`, `Delivery`.
- Define intended interfaces/contracts at a minimal level (e.g. `INewsSource`, `IChannelSender`, alert routing service) without fake implementations that bake in future decisions. Avoid building the entire abstraction tree early.
- Add `.gitignore`, example config with **no secrets**, central README with build instructions, basic test build, and status log.
- Ensure `dotnet build` and `dotnet test` are runnable, with a minimal smoke test if appropriate.

## Do not do
No authentication implementation, database schema, Docker Compose, channel senders, background polling, or Angular pages.

## Acceptance / human validation
- Show repository tree and explain boundaries in a short paragraph.
- Build/tests pass (or document environment blockers) and example config contains no secrets.
- Developer approves database selection and overall project layout.
- Update status to `AWAITING HUMAN VALIDATION` and **STOP** for explicit human approval.
EOF
cat > /mnt/data/codex_alerts_plan/phases/01-auth.md <<'EOF'
# Phase 01 — Authentication and authorization

**Source of truth:** `AGENTS.md`, `PROJECT_CONTEXT.md`, `PHASES.md`.

## Goal
Create working backend Identity auth and roles, with minimal EF Core persistence required for Identity. No personal/admin feature APIs yet.

## Ask before implementation if undecided
- How are accounts created for the demo (seeded accounts vs registration)? Propose **seeded development admin and user**, no public registration for this assignment.
- Confirm cookie login with same-origin Angular/backend approach (preferred for simplicity) versus a genuine cross-origin requirement.
- Does `IsEnabled=false` block sign-in/requests or only notifications? Propose blocking sign-in plus future delivery checks.

## Work
- Use ASP.NET Core Identity; admin is an ordinary Identity user with an `Admin` role, and users have the base user capability. Seed role and demo accounts **development-only** with credentials from environment/explicit dev configuration, never hardcoded shipped passwords.
- Add secure login/logout/session-check endpoint or equivalent minimal Identity endpoints; choose straightforward auth suitable for browser use. If using cookies, configure secure/HttpOnly/SameSite and CSRF protection for state-changing requests as appropriate; no hand-rolled JWT/password hashing.
- Add protected `me` proof endpoint and protected admin-only probe endpoint to verify role checks.
- Add policies/handlers enabling later endpoints. Enforce admin authorization server-side. An admin can access all regular endpoints.
- Add minimal Identity EF migrations if necessary and document how migration/bootstrap works. Phase 02 owns remaining domain schema.
- Unit-test policy/role behavior and disabled-user guard logic. Never log or return credentials.

## Do not do
No user CRUD, subscription settings, frontend, Slack/email, news polling, or full database design.

## Acceptance / human validation
- Anonymous request denied; normal user can call `me` but not admin probe; admin can call both.
- Show repeatable API commands, setup/seed steps, and test results.
- Record the approved sign-in/disabled account behavior.
- Mark `AWAITING HUMAN VALIDATION`; **STOP**.
EOF
cat > /mnt/data/codex_alerts_plan/phases/02-data.md <<'EOF'
# Phase 02 — Domain persistence and migrations

**Source of truth:** `AGENTS.md`, `PROJECT_CONTEXT.md`, `PHASES.md`.

## Goal
Persist the independent topic and channel selections and the minimum event/delivery state using EF Core with the approved SQL provider.

## Developer question before coding
Resolve user deletion semantics (hard vs soft delete and what happens to delivery history). Propose a simple policy with referential integrity; do not choose silently.

## Work
- Add Topic (Finance, Politics, Weather seed entries), Channel (Email, Slack seed entries) with stable unique codes and global `IsEnabled`.
- Extend Identity User with `IsEnabled` (unless already in Phase 01).
- Add `UserTopicSelection` and `UserChannelSelection` as **independent many-to-many** choices. Unique `user+topic`, `user+channel`. **Never add a user-topic-channel preference table.**
- Add `AlertEvent` with externally supplied unique ID, topic, title, body, event time; add `Delivery` with event/user/channel identity, statuses (`Pending`, `Succeeded`, `Failed` or equivalent), attempts (0..3), safe error info and timestamps.
- Enforce DB uniqueness for external event ID and event+user+channel, FK constraints and appropriate indexes; decide appropriate delete behavior based on developer answer.
- Add EF Core migrations and development seed data idempotently. Provide straightforward application-level operations for reading/updating selections, with tests using fakes/in-memory domain where sensible. Avoid assuming EF InMemory gives relational constraint guarantees.
- Document schema and migration commands; ensure Phase 01 auth still works.

## Do not do
No Slack/email implementations, polling, UI, or actual routing engine yet.

## Acceptance / human validation
- Migrations apply to a fresh DB and are safe to rerun; seeded topics/channels are not duplicated.
- Demonstrate adding/removing an independent topic selection and channel selection.
- Explain how uniqueness prevents simple duplicates.
- Provide test evidence; mark `AWAITING HUMAN VALIDATION`; **STOP**.
EOF
cat > /mnt/data/codex_alerts_plan/phases/03-compose.md <<'EOF'
# Phase 03 — Docker Compose developer environment

**Source of truth:** `AGENTS.md`, `PROJECT_CONTEXT.md`, `PHASES.md`.

## Goal
A clean checkout can run the backend and approved SQL DB predictably using Docker Compose.

## Work
- Dockerize ASP.NET Core backend using a multi-stage build and non-secret config.
- Use **official** image for the selected SQL provider; add Compose service, database persistence volume, readiness/health checks, and relevant startup ordering/retry (do not assume `depends_on` alone means DB ready).
- Implement documented repeatable development migrations and dev seed workflow. App-start migrations are acceptable for **local demo only** if explicitly explained; avoid claiming they are production-safe.
- Add `/health` (liveness and optionally DB readiness) and structured logging with useful event IDs/outcomes, excluding secrets.
- Supply `.env.example`, clear setup/reset commands, safe defaults and troubleshooting for clean environment.
- Frontend not required in Compose yet; can be added Phase 08.
- Smoke-test startup and migrations with Docker if available, and record exact outcome.

## Do not do
No dispatch logic or real channel clients. No custom DB image, Kubernetes, external secrets manager or monitoring stack.

## Acceptance / human validation
- Document `docker compose up --build` and a clean-volume scenario; API health responds and DB schema exists.
- Authentication from Phase 01 works against composed DB.
- Status `AWAITING HUMAN VALIDATION`; **STOP**.
EOF
cat > /mnt/data/codex_alerts_plan/phases/04-routing.md <<'EOF'
# Phase 04 — Alert processing/routing with fake sender

**Source of truth:** `AGENTS.md`, `PROJECT_CONTEXT.md`, `PHASES.md`.

## Goal
Implement the core behavior independently of external providers, with deterministic tests and persistent delivery outcomes.

## Work
- Define a normalized internal event mapping boundary (external DTO conversion is Phase 06). Process a validated `AlertEvent` with an external event ID.
- On processing, use unique external ID check to ignore duplicates; handle unique constraint races gracefully without elaborate deduping.
- For each enabled user who selected the event's enabled topic, produce one delivery for **each globally enabled channel selected by that user**. Channel preferences apply to **all topics**. Do not introduce combinations.
- Enforce `IsEnabled` for user, topic and channel; check global flags again before an actual send so globally disabled channels are not called.
- Use `IChannelSender` or equivalent interface and a fake sender that records calls.
- Track pending/success/failure, error/log context (without secrets). Retry transient sending failures with **3 send attempts total maximum**, then mark final failure. Keep delay/backoff bounded, simple, and testable (no broker). Clarify whether retries are persisted/restarted on app launch before deciding; a minimal DB-backed retry scan is a reasonable proposal.
- Handle one failed user/channel independently of others. Use cancellation tokens.
- Write unit tests: one topic + two channels produces two deliveries; unrelated topic skipped; channel selection disabled skips **all** topics for that user; global topic/channel disabled; disabled user; duplicate event ID; sender throws twice then succeeds; sender throws three times then permanently fails; retry never exceeds cap.

## Do not do
No real Slack/email API, REST polling worker, subscription admin endpoints or frontend.

## Acceptance / human validation
- Provide a controlled programmatic/test invocation of the dispatcher and show delivery rows and fake calls.
- Tests pass with exact expected counts/attempts.
- Status `AWAITING HUMAN VALIDATION`; **STOP**.
EOF
cat > /mnt/data/codex_alerts_plan/phases/05-channels.md <<'EOF'
# Phase 05 — Email and Slack channel adapters

**Source of truth:** `AGENTS.md`, `PROJECT_CONTEXT.md`, `PHASES.md`.

## Before coding — BLOCKING decisions, ask developer
- Is Slack delivery to an individual Slack **user/DM** or to a configured Slack **channel via webhook**? How does each application user specify or get mapped to a destination? A global webhook is **not equivalent** to personalized notifications. Propose the simplest acceptable demo contract.
- Which email transport is acceptable (SMTP with dev mail catcher; SMTP relay; library)? What is the intended test destination and how should per-user email be represented?
- Are real external sends required for manual validation, or local/mock transports sufficient? Never invent credentials or assume connectivity.

## Work
- Implement a sender adapter per channel behind `IChannelSender`, with simple reusable message formatting from normalized events. Routing code never references Slack or SMTP classes.
- Email: approved SMTP/library, recipient from approved user identity settings, subject/body escaping, env-managed SMTP host/credentials, and local test transport where practical.
- Slack: approved webhook or API client, correctly scoped destinations/credentials, safe formatted message; never leak tokens/webhook URLs in logs.
- Validate missing destination/configuration as a delivery failure with clear sanitized reason; respect three-total-attempt policy from Phase 04.
- Register senders by stable channel code; unknown/disabled channels are rejected/suppressed predictably. Document adding a third sender without editing dispatcher matching rules.
- Unit-test message mapping, failure translation, destination choice and sender selection using client fakes; do not test against live providers automatically.

## Do not do
No OAuth app install flow unless specifically approved; no frontend configuration screens or real news feed.

## Acceptance / human validation
- Demonstrate one Email and one Slack notification using agreed test transports/destinations, or show complete local mocks where real credentials are unavailable.
- Explain credentials/environment setup and sanitization.
- Status `AWAITING HUMAN VALIDATION`; **STOP**.
EOF
cat > /mnt/data/codex_alerts_plan/phases/06-source-worker.md <<'EOF'
# Phase 06 — Mock REST news source and background polling

**Source of truth:** `AGENTS.md`, `PROJECT_CONTEXT.md`, `PHASES.md`.

## Before coding
If mock provider REST contract is unspecified, propose a tiny sample JSON schema with unique event ID, topic code, title, body, timestamp; ask developer to approve semantics and whether mock endpoint runs as development-only API route or separate lightweight container. Avoid adding a large second app.

## Work
- Implement `INewsSource` with typed HttpClient against configurable mock REST endpoint, cancellation, bounded timeout and clear error logging.
- Convert mock payload into normalized `AlertEvent` with validation (known topic, nonempty ID and title etc); reject malformed items predictably, without taking down the worker.
- Supply deterministic mock REST responses/data including two topics, a repeat ID and malformed/unknown-topic examples; enable easy manual event triggering or fixture swapping.
- Poll from a single `BackgroundService` at configurable interval; catch/log transient source failures and continue, respect shutdown cancellation, avoid overlapping polling cycles.
- Wire ingestion to Phase 04 dispatcher and Phase 05 adapters. DB external ID uniqueness prevents repeat notifications on repoll. Avoid Kafka, hosted scheduling infrastructure and elaborate provider guarantees.
- Tests: mapping accepted/rejected; repeated event ID processed only once; polling lifecycle/cancellation; source error doesn't crash service (fake HttpMessageHandler/news source).

## Do not do
No real news feeds, scraping, provider registration, frontend, sophisticated batching or distributed locks.

## Acceptance / human validation
- Start composed app + mock source, publish/provide a fixture once, verify automatic matching deliveries, repeat same event ID and verify no extra deliveries.
- Show behavior on mock HTTP failure and app stop.
- Status `AWAITING HUMAN VALIDATION`; **STOP**.
EOF
cat > /mnt/data/codex_alerts_plan/phases/07-api.md <<'EOF'
# Phase 07 — Personal and admin HTTP APIs

**Source of truth:** `AGENTS.md`, `PROJECT_CONTEXT.md`, `PHASES.md`.

## Goal
Expose all settings/administration through clean, documented ASP.NET Core endpoints, with no frontend dependency.

## Work
- Personal authenticated endpoints: read enabled/available topics and channels; read current user's selected topic IDs and selected channel IDs **separately**; add/remove topic selections; add/remove channel selections. User can only change own settings, including admin acting as a user.
- Admin-only endpoints: list users (minimal safe fields), disable/enable user, delete user per approved deletion policy, list/enable/disable topics globally, list/enable/disable channels globally. Avoid extra create/edit operations unless requested; seeded topics/channels are enough.
- Enforce global disabled selections at processing level, independently of UI visibility. Decide and document whether disabled global items remain visible with disabled status; prefer visible-but-unavailable for clarity.
- Use authenticated identity from server session, never trust a client-supplied user ID in personal endpoints. Validate input and stable IDs/codes, use predictable HTTP status codes/errors, and avoid returning credential fields.
- Add concise Swagger/OpenAPI documentation in development; write unit/controller policy tests for ownership/admin checks, enable/disable effects and user deletion semantics. Keep security at server boundary.

## Do not do
No full frontend or new delivery features. Don't add per-topic/channel pairing to request DTOs.

## Acceptance / human validation
- Via API client: regular user edits own topic+channel preferences; cannot access admin endpoint or edit another account; admin can do both personal and admin operations; global disable suppresses dispatch.
- Document sample requests/responses and test results.
- Status `AWAITING HUMAN VALIDATION`; **STOP**.
EOF
cat > /mnt/data/codex_alerts_plan/phases/08-angular.md <<'EOF'
# Phase 08 — Simple Angular UI and frontend/backend wiring

**Source of truth:** `AGENTS.md`, `PROJECT_CONTEXT.md`, `PHASES.md`.

## Goal
Provide the smallest usable Angular UI for login, independent personal topic/channel choices, and admin management.

## Work
- Scaffold Angular using agreed version and simple component/service structure. Use Angular HttpClient and routing/guards only as UX; server remains authority. No NgRx or complex UI toolkit unless already present.
- Personal page: two clearly separate checkbox groups — `Topics: Finance, Politics, Weather` and `Receive via: Email, Slack`. **No topic x channel grid**. Show global disabled topic/channel state and clear save/response feedback.
- Login/logout and current-user/admin status. Admin users see personal page **and** a separate admin route.
- Admin page: list users and enable/disable/delete with confirmation for destructive action; list topics and channels and global enable/disable toggles. Show errors (403, 401, validation, network) accessibly.
- Integrate with backend auth approach (cookie + same-origin or approved solution), CSRF handling, API base URL/proxy config; no tokens in browser storage unless specifically agreed.
- Add frontend service/component unit tests for key preference behavior and role-limited navigation as feasible within deadline. Ensure backend endpoints continue to enforce security.
- Add frontend to Docker Compose, ideally expose one origin and proxy API calls where appropriate; document local dev and Compose flow.

## Do not do
No visual design system, realtime updates, charts, notification inbox, per-topic channel selection, E2E tests or deployment platform.

## Acceptance / human validation
- In browser: login as user, enable Finance and Weather + Email and Slack independently, observe relevant alert in both; disable Slack and observe subsequent relevant alert only in Email.
- Login as admin: same personal view plus admin page, disable a channel globally, and confirm dispatch respects it; delete a test user with confirmation.
- Show local/Compose launch steps; status `AWAITING HUMAN VALIDATION`; **STOP**.
EOF
cat > /mnt/data/codex_alerts_plan/phases/09-hardening.md <<'EOF'
# Phase 09 — Final test sweep, docs and release-ready demo

**Source of truth:** `AGENTS.md`, `PROJECT_CONTEXT.md`, `PHASES.md`.

## Goal
Make the small demo reproducible, understandable, and safely bounded; do not expand the product.

## Work
- Review and fix within scope issues in normalized event conversion, independent subscription matching, admin permissions, user deletion, unique-ID idempotency, retry cap, failure logs, health checks and cancellation.
- Run feasible backend/frontend builds and unit test suites; document results. No requirement to add E2E or external-provider integration suites.
- Validate clean Docker Compose install and migrations, seeded demo accounts/config, mock provider path and sender test arrangements. No secrets in files/logs/screenshots.
- Finalize README: prerequisites, one-command startup, configuration matrix, initial account setup, migrations/reset, manual demo walkthrough, tests, troubleshooting and architecture overview.
- Explicitly document constraints: mock-only REST source, no topic/channel pair subscriptions, up to three delivery attempts, simple external ID uniqueness, no broker/microservices, no E2E/integration testing, nonproduction hardening.
- Provide a short risk list of known limitations (e.g. local-only deployment assumptions, multi-instance background worker not supported, actual Slack per-user delivery constraints as agreed).

## Do not do
No refactor into microservices, real feed connectors, monitoring platform, new channels, OAuth onboarding, or unrequested features.

## Acceptance / human validation
- Developer follows README on a fresh environment, logs in as both roles, configures preferences, exercises mock ingestion, observes email/Slack test outputs and admin global disable behavior.
- Give final changed-file/test summary, deviations and outstanding risks.
- Status `AWAITING HUMAN VALIDATION`; **STOP** until developer explicitly approves completion.
EOF
cd /mnt/data && zip -qr codex_alerts_plan.zip codex_alerts_plan && find /mnt/data/codex_alerts_plan -type f | sort

---

**Deep Research** *(10/31/2026, 11:09:27 AM)*

/mnt/data/codex_alerts_plan/AGENTS.md
/mnt/data/codex_alerts_plan/IMPLEMENTATION_STATUS.md
/mnt/data/codex_alerts_plan/PHASES.md
/mnt/data/codex_alerts_plan/PROJECT_CONTEXT.md
/mnt/data/codex_alerts_plan/phases/00-skeleton.md
/mnt/data/codex_alerts_plan/phases/01-auth.md
/mnt/data/codex_alerts_plan/phases/02-data.md
/mnt/data/codex_alerts_plan/phases/03-compose.md
/mnt/data/codex_alerts_plan/phases/04-routing.md
/mnt/data/codex_alerts_plan/phases/05-channels.md
/mnt/data/codex_alerts_plan/phases/06-source-worker.md
/mnt/data/codex_alerts_plan/phases/07-api.md
/mnt/data/codex_alerts_plan/phases/08-angular.md
/mnt/data/codex_alerts_plan/phases/09-hardening.md

---

**ChatGPT** *(10/31/2026, 11:09:28 AM)*

I've prepared a complete set of **local Codex agent instructions**, organized into 10 independently reviewable implementation milestones.

<box gap={3}>
  <row align="center" gap={3}>
    <icon name="file-archive" size="2xl" color="secondary"/>
    <box flex="1" gap={1}>
      **[Download Codex implementation instructions](sandbox:/mnt/data/codex_alerts_plan.zip)**
      <text color="secondary" size="xs">ZIP containing 14 Markdown files, ready to copy into your local repository.</text>
    </box>
  </row>
</box>

## 1. What's included

| File | Purpose |
|---|---|
| `AGENTS.md` | Mandatory rules for every Codex agent |
| `PROJECT_CONTEXT.md` | Product requirements, architecture decisions, and scope boundaries |
| `PHASES.md` | Implementation order and validation gates |
| `IMPLEMENTATION_STATUS.md` | Tracks milestone completion and developer approval |
| `phases/00–09` | Dedicated implementation instructions for each phase |

Each phase includes its objective, implementation requirements, exclusions, automated testing expectations, human validation criteria, and an explicit instruction to stop.

## 2. Implementation milestones

<box border radius="lg" padding={3} gap={3}>
  <list connector="solid" marker="number" gap={2}>
    <list-item>**Solution skeleton** — Architecture and project structure</list-item>
    <list-item>**Authentication** — Identity, login, and admin authorization</list-item>
    <list-item>**Database** — Domain models, independent preferences, migrations</list-item>
    <list-item>**Docker environment** — Compose, persistence, configuration, health</list-item>
    <list-item>**Alert processing** — Routing, retries, deduplication, fake senders</list-item>
    <list-item>**Delivery channels** — Email and Slack adapters</list-item>
    <list-item>**News ingestion** — Mock REST source and background polling</list-item>
    <list-item>**Management APIs** — Personal and admin endpoints</list-item>
    <list-item>**Angular frontend** — User/admin views and integration</list-item>
    <list-item>**Final validation** — Testing, documentation, Docker verification</list-item>
  </list>
</box>

## 3. Your clarified requirements are enforced

In particular:

- **Independent subscriptions:** Users select topics and delivery channels independently. No topic/channel pairings.
- **Admin behavior:** Admins retain the regular personal view, with additional administrative permissions, including user deletion.
- **Retry policy:** Maximum three total delivery attempts, with failures recorded.
- **Deduplication:** Simple uniqueness check against the external event ID.
- **Architecture:** Modular .NET monolith, EF Core, Angular, Docker Compose.
- **Testing:** Focused unit tests; no mandatory E2E or real-provider integration suite.
- **Human control:** Codex must stop after each milestone and receive explicit developer approval.

## 4. How to use it with Codex

Extract the ZIP into your repository and open Codex in that directory.

For the first milestone, submit:

<box border radius="lg" padding={3} gap={2}>
  <text color="secondary" weight="medium" size="xs">CODEX INITIAL PROMPT</text>
  Implement Phase 00 only, following `AGENTS.md`, `PROJECT_CONTEXT.md`, `PHASES.md`, and `phases/00-skeleton.md`.

  Review the existing repository, explain your implementation plan, and ask any blocking questions before coding.

  Complete the phase's acceptance criteria, run available tests, update `IMPLEMENTATION_STATUS.md`, and stop for my explicit validation. Do not implement any subsequent phases.
  <row justify="end">
    <button color="secondary" variant="outline" size="sm" onClick={()=>GenUI.copy("Implement Phase 00 only, following AGENTS.md, PROJECT_CONTEXT.md, PHASES.md, and phases/00-skeleton.md.\n\nReview the existing repository, explain your implementation plan, and ask any blocking questions before coding.\n\nComplete the phase's acceptance criteria, run available tests, update IMPLEMENTATION_STATUS.md, and stop for my explicit validation. Do not implement any subsequent phases.")}>
      <icon name="copy" size="xs"/> Copy prompt
    </button>
  </row>
</box>

For subsequent phases, change the phase number and filename. The shared instructions also require agents to check whether the previous milestone was approved.

## 5. Three decisions I've deliberately left open

There are a few questions that would otherwise force Codex to make product decisions on your behalf:

1. **Database provider:** SQL Server or PostgreSQL? The Phase 00 agent must ask before committing to either.
2. **Authentication/bootstrap:** How should demo users be created, and should disabling a user prevent login entirely? Phase 01 asks for confirmation.
3. **Slack delivery model:** Does each user receive messages through a Slack DM, or do they specify a destination/channel? This materially affects the implementation, so Phase 05 must ask before coding.

There is also a smaller question about whether deleting a user removes their delivery history. Phase 02 explicitly raises it.

These are intentionally not blockers to planning the overall project; they're decisions assigned to the earliest phase that needs them.

**The important property of this handoff is that each Codex session has a bounded task, sufficient shared context, a testable definition of completion, and a required human approval checkpoint.** That should make it practical to implement this project incrementally without agents quietly adding complexity beyond the agreed scope.

---