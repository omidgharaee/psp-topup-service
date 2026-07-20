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

---

## ADR-0007 — Result pattern over exceptions for business errors

**Status:** Accepted
**Date:** 2026-07-20

### Context
In a payment system, most "errors" (invalid amount, bad state, duplicate
request) are expected, recoverable business outcomes — not exceptional
conditions. Using exceptions for them obscures the happy path, defeats
flow-analysis tooling, and makes the API surface harder to reason about.

### Decision
Business-rule violations return `Result` / `Result<TValue>` from
`PSP.TopupService.SharedKernel.Results`. Each carries an `Error` value object
with a stable code, message and `ErrorType` category. Only truly unexpected,
non-recoverable conditions throw (these flow through the global exception
middleware). Domain exceptions still exist (`BusinessException`,
`ConcurrencyException`, `NotFoundException`, `InfrastructureException`) for the
few cases where a Result cannot be propagated.

### Consequences
- All command handlers in Application return `Result<T>`.
- The API layer maps `ErrorType` to HTTP status (Validation→400, Conflict→409,
  NotFound→404, Unauthorized→401, Forbidden→403, Unavailable→503, Failure→500).
- Result is immutable; chaining helpers (`Map`, `Bind`, `Ensure`) keep handlers
  declarative.

---

## ADR-0008 — Aggregate root + domain-event base class

**Status:** Accepted
**Date:** 2026-07-20

### Context
The domain needs a consistent way to model identity, audit fields, soft delete,
optimistic concurrency and domain-event raising on every aggregate.

### Decision
`BaseEntity` (SharedKernel) provides `Id`, `CreatedOnUtc`, `ModifiedOnUtc`,
`CreatedBy/ModifiedBy`, `IsDeleted/DeletedOnUtc`, `RowVersion`, and an
internal domain-event collection. `AggregateRoot<TId>` derives from it for the
typed-id case. Domain events are dispatched post-persist by an
`IDomainEventDispatcher` whose implementation lives in Infrastructure.

### Consequences
- Aggregates are loaded/saved by their root; children are never persisted directly.
- Soft-delete is built-in; Persistence applies a global query filter.
- `RowVersion` is mapped by EF Core to PostgreSQL `xmin` for optimistic concurrency.
- 32 unit tests cover the Result, BaseEntity and ValueObject behaviour.

---

## ADR-0009 — Rich domain model with explicit state machine for Topup

**Status:** Accepted
**Date:** 2026-07-20

### Context
A topup transaction flows through many asynchronous steps (payment, topup,
reversal) coordinated over a message broker. If the aggregate allowed ad-hoc
state mutation, race conditions and duplicate events could push it into an
invalid state (e.g. "complete" a topup whose payment never settled).

### Decision
Model `TopupTransaction` as a rich aggregate root with:
- An explicit `TopupStatus` state machine with guarded transitions. Every state
  change goes through a behaviour method (`MarkPaymentInitiated`,
  `MarkPaymentCompleted`, `StartTopupAttempt`, `MarkTopupCompleted`,
  `MarkTopupFailed`, `InitiateReversal`, `MarkPaymentReversed`).
- Illegal transitions return a failed `Result` with a stable error code — they
  never throw and never mutate the aggregate.
- Each legal transition raises a typed domain event, captured in the aggregate's
  event collection and dispatched post-persist via the outbox.
- Child entities (`TopupAttempt`, `ReverseRecord`) are mutated only through the
  root. Attempts are append-only and numbered for retry tracing.
- Value objects (`MobileNumber`, `Money`, `TransactionReference`) validate at
  construction; an aggregate can never hold an invalid value.

### Consequences
- The aggregate is the single source of truth — duplicate consumer messages
  become no-ops because the transition is already terminal.
- Optimistic concurrency (`Version` / EF Core `xmin`) protects against
  concurrent writers racing on the same row.
- 48 domain unit tests pin every transition and invariant; the full happy-path
  produces events in a deterministic order.
