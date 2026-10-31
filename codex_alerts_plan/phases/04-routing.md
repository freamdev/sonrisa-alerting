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
