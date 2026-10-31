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
