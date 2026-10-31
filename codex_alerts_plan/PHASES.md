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
