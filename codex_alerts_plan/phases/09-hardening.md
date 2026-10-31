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
