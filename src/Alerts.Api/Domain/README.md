# Domain boundary and vocabulary

This folder will contain business concepts without ASP.NET Core, EF Core, or external-provider dependencies. Phase 00 documents vocabulary only; identifiers, entities, validation and schema arrive in their assigned phases.

| Concept | Meaning |
| --- | --- |
| `AlertEvent` | Internal normalized news item: external unique event ID, topic, title, content and timestamp. External DTOs must not reach senders. |
| `Topic` | Stable topic key, display name and global enabled flag. |
| `Channel` | Stable delivery key (initially `email` or `slack`) and global enabled flag. |
| `UserTopicSelection` | Independent user-to-topic preference, unique by user and topic. |
| `UserChannelSelection` | Independent user-to-channel preference, unique by user and channel. |
| `Delivery` | One event/user/channel outcome with attempt count, timestamps and last error; unique by event, user and channel. At most three total send attempts. |

Topic selections and channel selections are independent. Each eligible event expands across all of a user's selected, globally enabled channels. Zero topics or zero channels means no deliveries. Admin is a regular account with additional permissions. Disabled users, topics or channels prevent delivery. Duplicate external event IDs must not create new deliveries. Persistence and enforcement belong to later phases.
