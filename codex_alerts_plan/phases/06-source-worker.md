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
