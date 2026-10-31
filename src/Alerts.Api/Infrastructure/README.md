# Infrastructure boundary

This folder implements Identity authentication, EF Core/PostgreSQL persistence and the application selection contract. Dependencies point toward Application and Domain, never from them to concrete adapters. `Program.cs` is the composition root.

The existing `Identity/AlertsIdentityDbContext` owns Identity and the Phase 02 domain tables in one migration chain. Catalog configuration seeds fixed topic/channel reference data; `SelectionService` reads/updates independent user choices. See `docs/phase02-data.md` for schema, deletion policy and validation. Phase 04 adds `AlertDispatcher`, process-wide serialization, recording fake channel adapters and one startup pending-recovery scan. Events/deliveries commit together; attempts and outcomes persist with exponential delays and a three-attempt cap. See `docs/phase04-routing.md` for recovery semantics and limitations. Real providers and source polling remain later-phase work.
