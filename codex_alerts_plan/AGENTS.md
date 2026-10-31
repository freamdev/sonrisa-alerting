# Codex operating instructions — Alerts application

Read `PROJECT_CONTEXT.md`, `PHASES.md`, and the requested `phases/NN-*.md` **before making changes**. These files define the scope and take precedence over architectural preferences or assumptions. Read the repository as it exists; do not assume an earlier phase was completed unless code and its human-approval record support that conclusion.

## Execution contract

1. Work on **only one explicitly assigned phase**. Do not start the next phase, even when it looks easy or logically connected. Do not silently implement future-phase work.
2. Before editing, give the developer a short plan, identify existing relevant code, and state any blocking questions. **Ask the developer** when there is uncertainty that changes behavior, security, data persistence, integration contracts, or architecture. Do not guess on such decisions; stop and wait for their response. For minor implementation details, choose the simplest option consistent with project context and document it.
3. Follow existing repository conventions and use supported .NET/Angular libraries instead of bespoke implementations. Prefer a modular monolith. Keep dependencies and code to the minimum needed for the assigned milestone.
4. Write appropriate automated **unit tests** within the phase. Do not introduce E2E suites or external-provider integration tests. Run the build/tests/format checks feasible locally. Report what actually ran, what failed, and what could not run.
5. Never commit credentials. Read secrets from environment/configuration, provide `.env.example` placeholders, and avoid logging passwords/tokens/webhook URLs.
6. At completion, update `IMPLEMENTATION_STATUS.md` with changes, test evidence, exact human-validation commands, known limitations, and open decisions. Mark the phase **AWAITING HUMAN VALIDATION**, not approved.
7. Finish with a succinct validation report: changed files, behavior implemented, commands + results, how the developer can verify manually, deviations, questions, and a **clear request for approval or feedback**.
8. **STOP. Do not continue to the next phase, mark approval, or treat silence as approval.** Resume only after the developer explicitly approves the milestone and explicitly requests the next phase. If corrections are requested, work only within the current phase and present it for review again.
9. If you discover a defect from an earlier phase, report it and ask whether to fix it within this phase before making substantial out-of-phase changes.

## Approval bookkeeping

`IMPLEMENTATION_STATUS.md` is a running handoff log. Agents may record `AWAITING HUMAN VALIDATION` and evidence, but **only explicit developer feedback** justifies recording `APPROVED`. A new agent should verify the previous phase has been approved before starting the next one; if approval is unclear, ask. Do not create a git commit unless the developer has asked you to.

## Source of truth

- `PROJECT_CONTEXT.md`: product behavior, domain semantics, technology and scope limits.
- `PHASES.md`: sequence, invariants and stop points.
- `phases/NN-*.md`: implementable checklist and acceptance criteria for a single milestone.
- Existing code and developer decisions: reconcile differences with the developer rather than silently overriding the written scope.
