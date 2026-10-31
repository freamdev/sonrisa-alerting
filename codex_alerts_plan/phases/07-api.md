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
