# Infrastructure boundary

This folder will implement application abstractions using EF Core/PostgreSQL, ASP.NET Core Identity, the mock REST client and Email/Slack adapters in their assigned phases. Dependencies point toward Application and Domain, never from them to concrete adapters. `Program.cs` is the composition root.

PostgreSQL was explicitly selected by the developer for this project. Phase 00 adds no database packages, DbContext, schema, migrations or connection attempts. Provider packages and mappings are introduced when persistence is implemented; container setup belongs to Phase 03.
