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
