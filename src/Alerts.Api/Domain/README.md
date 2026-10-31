# Domain boundary and vocabulary

This folder contains dependency-free topic/channel, selection, event and delivery models. EF mappings and constraints live in Infrastructure; routing, state transitions and retry behavior remain Phase 04 work. See `docs/phase02-data.md` for schema details.

| Concept | Meaning |
| --- | --- |
| `AlertEvent` | Internal normalized news item: external unique event ID, topic, title, content and timestamp. External DTOs must not reach senders. |
| `Topic` | Stable topic key, display name and global enabled flag. |
| `Channel` | Stable delivery key (initially `email` or `slack`) and global enabled flag. |
| `UserTopicSelection` | Independent user-to-topic preference, unique by user and topic. |
| `UserChannelSelection` | Independent user-to-channel preference, unique by user and channel. |
| `Delivery` | One event/user/channel outcome with attempt count, timestamps and last error; unique by event, user and channel. At most three total send attempts. |

Topic selections and channel selections are independent. The future router must expand each eligible event across all of a user's selected, globally enabled channels. Zero topics or zero channels means no deliveries. Admin is a regular account with additional permissions. Disabled users, topics or channels must prevent delivery. Persistence enforces unique external event IDs and event/user/channel deliveries; send-time enforcement remains Phase 04 work.
