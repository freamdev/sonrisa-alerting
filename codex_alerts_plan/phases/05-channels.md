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
