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
