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
