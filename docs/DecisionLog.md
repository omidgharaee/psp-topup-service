# Decision Log (ADR)

Architecture Decision Records for the PSP Topup Service, in chronological order.
Each entry records **what** was decided, **why**, and the **consequences**.

---

## ADR-0001 — Adopt Clean Architecture with a separated Persistence layer

**Status:** Accepted
**Date:** 2026-07-20

### Context
The Topup service is a payment-critical component. It must remain testable in
isolation, allow swapping infrastructure (DB, broker) without touching business
logic, and be understandable to new engineers.

### Decision
Layer the system strictly:
`Domain ← Application ← {Infrastructure, Persistence} ← {Api, Worker}`.
Keep `Persistence` separate from `Infrastructure` so EF Core / data concerns do
not bleed into external-clients concerns. `Contracts` holds only integration
events/messages and is referenced by both the service and the mocks.

### Consequences
- Dependency direction is enforced at compile time and verified by NetArchTest.
- Controllers and consumers stay thin; all orchestration lives in Application.
- Domain has zero knowledge of EF Core or RabbitMQ.

---

## ADR-0002 — PostgreSQL instead of SQL Server

**Status:** Accepted
**Date:** 2026-07-20

### Context
The original spec defaulted to SQL Server. The team has chosen PostgreSQL.

### Decision
Use `Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.3 as the EF Core provider.
Integration tests use `Testcontainers.PostgreSql` so they run against a real,
identical engine.

### Consequences
- Connection strings use the `Host=...;Database=...` Npgsql format.
- Migrations must avoid SQL Server-only features.
- JSON columns map to `jsonb`, enabling efficient integration-event payload storage.

---

## ADR-0003 — Central Package Management (CPM)

**Status:** Accepted
**Date:** 2026-07-20

### Context
With 13 projects, declaring versions per-project causes drift and makes security
auditing (NU1903 / NU1902) hard to act on.

### Decision
Enable `<ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>`
in `Directory.Packages.props`. All `.csproj` declare `PackageReference` without
`Version`. Transitive pinning is enabled so vulnerable transitive packages
(e.g. `KubernetesClient` via HealthChecks.UI) can be pinned centrally.

### Consequences
- Single source of truth for every package version.
- Vulnerable transitive dependencies are pinned centrally to a fixed version.
- Per-project override is disabled (`EnablePackageVersionOverride=false`).

---

## ADR-0004 — Swashbuckle over Microsoft.AspNetCore.OpenApi

**Status:** Accepted
**Date:** 2026-07-20

### Context
`Microsoft.AspNetCore.OpenApi` ships a source generator that is incompatible
with `Microsoft.OpenApi` 3.x (`IOpenApiMediaType.Example` is readonly in 3.x),
producing `CS0200` compile errors.

### Decision
Remove `Microsoft.AspNetCore.OpenApi` and `Microsoft.OpenApi` direct references.
Use `Swashbuckle.AspNetCore` for OpenAPI/Swagger generation. Swashbuckle brings
`Microsoft.OpenApi` 2.x transitively, which is the version it is built against.

### Consequences
- No source generator conflict; builds are clean.
- Richer Swagger UI and broader ecosystem support (annotations, XML comments).
- We still get a security-warning-free dependency tree.

---

## ADR-0005 — Code-style enforcement via .editorconfig, not stylecop.json

**Status:** Accepted
**Date:** 2026-07-20

### Context
StyleCop.Analyzers 1.1.118 (the latest stable) fails to load `stylecop.json`
when referenced via `AdditionalFiles` from `Directory.Build.props` in lightweight
library projects (`SA0002`). The 1.2.x line that fixes this is pre-release only.

### Decision
Configure StyleCop and .NET code-style rules in `.editorconfig` and ruleset
(`build/PSP.ruleset`) instead of `stylecop.json`. EditorConfig is the modern,
tooling-agnostic approach and is enforced at build via `EnforceCodeStyleInBuild`.

### Consequences
- A small set of StyleCop layout rules that conflict with top-level statements
  (`SA1512`, `SA1515`, `SA1516`) are disabled globally.
- All other StyleCop and .NET analyzer rules run as build errors
  (`TreatWarningsAsErrors=true`).
- Documentation generation is still enabled (`GenerateDocumentationFile=true`),
  `CS1591` (missing XML doc) is silenced during the build-up phase.

---

## ADR-0006 — GitFlow branching model

**Status:** Accepted
**Date:** 2026-07-20

### Context
The codebase needs a predictable, reviewable history suitable for a regulated
payment product, with clear release boundaries and hotfix paths.

### Decision
Adopt GitFlow: `main` (production), `develop` (integration), `feature/*`,
`release/*`, `hotfix/*`. Every feature is developed on its own branch and merged
to `develop` via pull-request-style merge commits (`--no-ff`).

### Consequences
- History reads as a sequence of discrete, reviewable features.
- Releases are cut from `release/*` branches off `develop`.
- Hotfixes flow `main → hotfix/* → main & develop`.

See [GitFlow.md](GitFlow.md) for the full workflow and commit conventions.
