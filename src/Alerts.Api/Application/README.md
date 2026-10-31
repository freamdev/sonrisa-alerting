# Application boundary and intended contracts

This folder will orchestrate use cases using domain concepts and abstractions. It must not depend on concrete database, REST, Email or Slack adapters. Contracts are documented rather than compiled in Phase 00 to avoid fixing unapproved payload, destination and persistence choices.

- `INewsSource`: asynchronously obtain news from the mock REST source, honor cancellation and expose source failures separately from delivery failures. Agree the external JSON contract and normalization boundary in Phase 06 before defining signatures. The application ultimately consumes normalized `AlertEvent` values.
- `IChannelSender`: identify the supported channel and send a normalized alert to an agreed recipient destination, honoring cancellation and reporting success or failure. Agree destination and credential ownership in Phase 05. Sending once does not own retries.
- Alert routing service: accept a normalized alert, ignore known external IDs, match enabled users and independently selected topics/channels, and record deliveries and outcomes. Own the maximum of three total attempts per delivery in Phase 04; senders do not multiply attempts.

No interfaces, fake implementations, routing behavior, retry engine or persistence are introduced yet. Later phases can refine signatures after their blocking decisions are agreed.
