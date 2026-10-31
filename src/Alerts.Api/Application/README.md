# Application boundary and intended contracts

This folder defines application contracts and access policies without concrete database, REST, Email or Slack dependencies. Phase 02 adds `ISelectionService` for reading and independently updating topic/channel choices, implemented by Infrastructure. Internal callers supply a user ID; future HTTP callers must use the authenticated principal and enforce authorization/CSRF.

- `INewsSource`: asynchronously obtain news from the mock REST source, honor cancellation and expose source failures separately from delivery failures. Agree the external JSON contract and normalization boundary in Phase 06 before defining signatures. The application ultimately consumes normalized `AlertEvent` values.
- `IChannelSender`: identify the supported channel and send a normalized alert to an agreed recipient destination, honoring cancellation and reporting success or failure. Agree destination and credential ownership in Phase 05. Sending once does not own retries.
- Alert routing service: accept a normalized alert, ignore known external IDs, match enabled users and independently selected topics/channels, and record deliveries and outcomes. Own the maximum of three total attempts per delivery in Phase 04; senders do not multiply attempts.

Source/sender signatures, fake senders, routing behavior and the retry engine remain later-phase work after their blocking decisions are agreed.
