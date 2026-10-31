# Infrastructure boundary

This folder implements Identity authentication, EF Core/PostgreSQL persistence and the application selection contract. Dependencies point toward Application and Domain, never from them to concrete adapters. `Program.cs` is the composition root.

The existing `Identity/AlertsIdentityDbContext` owns Identity and the Phase 02 domain tables in one migration chain. Catalog configuration seeds fixed topic/channel reference data; `SelectionService` reads/updates independent user choices. See `docs/phase02-data.md` for schema, deletion policy and validation. Container setup belongs to Phase 03; source and sender adapters remain later-phase work.
