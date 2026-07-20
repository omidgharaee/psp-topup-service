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

---

## ADR-0010 — CQRS with a single-commit transactional pipeline

**Status:** Accepted
**Date:** 2026-07-20

### Context
Commands mutate the aggregate AND enqueue outbox messages. If those were
committed separately (state save, then publish) a crash between them could lose
the publish intent — leaving the system in a stuck state where the topup exists
but no one knows about it. Conversely, committing in every handler duplicates
policy and makes testing harder.

### Decision
Adopt MediatR CQRS with a five-stage pipeline behaviour registered in a fixed
order:

1. `UnhandledExceptionBehavior` — outermost safety net; logs unexpected errors
   with full correlation context.
2. `LoggingBehavior` — emits Persian structured entry/exit logs with correlation,
   trace and request ids.
3. `ValidationBehavior` — runs all FluentValidation validators and throws a
   typed `ValidationException` aggregating per-field errors.
4. `PerformanceBehavior` — warns when a request exceeds the SLA threshold.
5. `TransactionBehavior` — innermost; commits the unit of work after the handler
   returns, atomically persisting the aggregate state and the outbox message.

Commands never call `SaveChangesAsync` themselves; the behaviour owns the commit
so the transactional boundary is consistent and easy to test.

### Consequences
- One commit per command — the aggregate and the outbox message commit together
  or roll back together. No partial state, no lost events.
- Handlers stay focused on domain orchestration; cross-cutting concerns live in
  behaviours.
- Idempotency is enforced in the handler (key lookup before side effects) so
  duplicate API requests collapse to a replay result without re-publishing.
- 18 application unit tests cover the validator, the handler (create / replay /
  invalid) and the validation behaviour.

---

## ADR-0011 — PostgreSQL with EF Core and owned-type mapping

**Status:** Accepted
**Date:** 2026-07-20

### Context
The aggregate holds value objects (`MobileNumber`, `Money`,
`TransactionReference`) and child entities (`TopupAttempt`, `ReverseRecord`).
The schema must reflect the domain shape while staying efficient under load,
and it must support optimistic concurrency and soft delete.

### Decision
Use `Npgsql.EntityFrameworkCore.PostgreSQL` with:
- snake_case naming convention applied at the DbContext options level.
- Value objects as EF Core **owned types** (complex columns).
- Child entities (`TopupAttempt`, `ReverseRecord`) as **owned collection /
  owned single** of the aggregate root, so the aggregate is loaded/saved
  atomically by the repository.
- Optimistic concurrency via `IsRowVersion()` on `RowVersion`, mapped to
  PostgreSQL `xmin` by the Npgsql provider.
- Soft delete via a global query filter (`IsDeleted = false`).
- The outbox (`outbox_messages`) and inbox (`inbox_messages`) tables live in
  the same DbContext so they commit in the same transaction as the business
  change.

### Consequences
- One SQL transaction per command persists the aggregate, the outbox message,
  the inbox de-dup row and the audit log atomically.
- JSONB columns store the outbox payload and audit snapshots, supporting fast
  partial indexing and JSON queries.
- Partial indexes on `outbox_messages(status, occurred_on_utc)` and
  `inbox_messages(message_id, consumer)` make the publisher polling and consumer
  de-dup paths index-only scans.
- An `AuditSaveChangesInterceptor` writes the audit trail inside SaveChanges,
  so the trail cannot be bypassed by application code.

---

## ADR-0012 — Atomic lease for outbox publishing; consume-local for inbox

**Status:** Accepted
**Date:** 2026-07-20

### Context
With multiple Worker instances running, two could pick the same outbox row and
publish twice. Conversely, on the consumer side, the broker may redeliver a
message (network blip, consumer crash) and the business logic must run exactly
once.

### Decision
**Outbox lease** is a single atomic SQL statement:
`UPDATE outbox_messages SET status='InProgress' ... WHERE id IN (SELECT id ...
FOR UPDATE SKIP LOCKED LIMIT N) RETURNING id`. The `FOR UPDATE SKIP LOCKED`
clause guarantees no two workers ever lease the same row, even under concurrent
execution. Leases carry a `locked_until_utc` so a crashed worker's rows are
reclaimed by a periodic sweeper.

**Inbox consume-local**: before processing a message the consumer inserts an
`inbox_messages` row with id `{consumer}:{messageId}`. The unique index on
`(message_id, consumer)` makes a duplicate insert fail with sqlstate 23505,
which we translate to "already processed, skip". The inbox insert commits in
the SAME transaction as the business change.

### Consequences
- At-least-once delivery from the broker becomes effectively-once processing.
- Workers scale horizontally without coordination — the database is the lock.
- Dead-lettering is intrinsic: after `max_attempts` retries a row is marked
  `DeadLettered` for manual intervention, never silently dropped.
- 6 unit tests pin the envelope wire contract so consumer-side routing by
  `$type` cannot silently drift.
