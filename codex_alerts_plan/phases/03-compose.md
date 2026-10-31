# Phase 03 — Docker Compose developer environment

**Source of truth:** `AGENTS.md`, `PROJECT_CONTEXT.md`, `PHASES.md`.

## Goal
A clean checkout can run the backend and approved SQL DB predictably using Docker Compose.

## Work
- Dockerize ASP.NET Core backend using a multi-stage build and non-secret config.
- Use **official** image for the selected SQL provider; add Compose service, database persistence volume, readiness/health checks, and relevant startup ordering/retry (do not assume `depends_on` alone means DB ready).
- Implement documented repeatable development migrations and dev seed workflow. App-start migrations are acceptable for **local demo only** if explicitly explained; avoid claiming they are production-safe.
- Add `/health` (liveness and optionally DB readiness) and structured logging with useful event IDs/outcomes, excluding secrets.
- Supply `.env.example`, clear setup/reset commands, safe defaults and troubleshooting for clean environment.
- Frontend not required in Compose yet; can be added Phase 08.
- Smoke-test startup and migrations with Docker if available, and record exact outcome.

## Do not do
No dispatch logic or real channel clients. No custom DB image, Kubernetes, external secrets manager or monitoring stack.

## Acceptance / human validation
- Document `docker compose up --build` and a clean-volume scenario; API health responds and DB schema exists.
- Authentication from Phase 01 works against composed DB.
- Status `AWAITING HUMAN VALIDATION`; **STOP**.
