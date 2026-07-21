# PSP Topup Service

> Enterprise-grade Payment Service Provider (PSP) **Topup** service built on
> Clean Architecture / DDD principles with .NET 10, PostgreSQL and RabbitMQ.

A new `TOPUP` transaction type for an existing payment platform. It integrates
with a Payment service (via RabbitMQ) and the Hamrah-e-Aval (MCI) mobile topup
provider (via HTTP + Polly), using the **Transactional Outbox** pattern for
reliable messaging and a **Saga** for the two-phase Advice finalisation.

---

## Architecture

Clean Architecture with strict, machine-checked dependency rules:

```
Domain ← SharedKernel
Domain ← Application ← { Infrastructure, Persistence } ← Api
Contracts (no internal deps; shared with mocks)
```

- **Domain** — Rich aggregate (`TopupTransaction`), value objects, domain
  events, no EF Core / MediatR references.
- **Application** — CQRS handlers, validators, pipeline behaviours, abstractions.
- **Infrastructure** — MassTransit, MCI HTTP client (Polly), integration-event mapper.
- **Persistence** — EF Core + PostgreSQL, repositories, outbox / inbox state.
- **Api** — thin host, including background services (outbox).
- **Contracts** — integration events shared with the mock services.

See [docs/Architecture.md](docs/Architecture.md) and
[docs/DecisionLog.md](docs/DecisionLog.md) for the full rationale (21 ADRs).

## Tech stack

| Concern | Choice |
| --- | --- |
| Runtime | .NET 10, ASP.NET Core, Worker Service |
| Persistence | EF Core + PostgreSQL (`Npgsql.EntityFrameworkCore.PostgreSQL`) |
| Messaging | RabbitMQ + MassTransit |
| Resilience | Polly (timeout, retry, circuit breaker, fallback, wrap) |
| Logging | Serilog (structured, Persian, async console sink) |
| Validation | FluentValidation |
| CQRS | MediatR |
| Mapping | Mapster |
| API docs | Swashbuckle (Swagger UI) |
| Health | ASP.NET Core HealthChecks (PostgreSQL, RabbitMQ, mocks) |
| Versioning | Asp.Versioning.Mvc (URL segment: `/api/v1/...`) |
| Tests | xUnit + Testcontainers + NetArchTest.Rules |

## Solution layout

```
PSP.TopupService.sln
src/
├── PSP.TopupService.Api            # public HTTP API, outbox publisher + consumers
├── PSP.TopupService.Application    # CQRS, handlers, behaviours
├── PSP.TopupService.Domain         # Topup aggregate, value objects
├── PSP.TopupService.Infrastructure # MassTransit, MCI client, Polly
├── PSP.TopupService.Persistence    # EF Core, repositories, outbox/inbox
├── PSP.TopupService.Contracts      # integration events
├── PSP.TopupService.SharedKernel   # Result, BaseEntity, ValueObject
├── PSP.Mock.Payment.Api            # Payment-service simulator (RabbitMQ)
└── PSP.Mock.HamrahAval.Api         # MCI simulator (HTTP)
tests/
├── PSP.TopupService.UnitTests          # 143 tests
├── PSP.TopupService.IntegrationTests   # Testcontainers (PostgreSQL + RabbitMQ)
└── PSP.TopupService.ArchitectureTests  # 14 Clean-Architecture rules
docs/                                    # Architecture, Database, Sequence, Outbox, Logging, Deployment, DecisionLog, GitFlow
build/                                   # Directory.Build.props, packages.props, ruleset
.github/workflows/                       # build, test, publish
```

## How to run

### Prerequisites

- .NET 10 SDK
- PostgreSQL 14+ and RabbitMQ 3.11+ reachable on `localhost` (default ports)
- `dotnet-ef` tool for migrations

### 1. Apply the database migration

```bash
dotnet ef database update \
  --project src/PSP.TopupService.Persistence \
  --startup-project src/PSP.TopupService.Api
```

### 2. Start the dependencies

Start PostgreSQL and RabbitMQ (any method — `docker run`, a local install, or
the orchestrator of your choice). The default connection strings point at
`localhost:5432` and `localhost:5672`.

### 3. Run the three services

In three terminals (or via your IDE's multi-launch):

```bash
dotnet run --project src/PSP.TopupService.Api           # https://localhost:5001 + background services
dotnet run --project src/PSP.Mock.Payment.Api           # Payment simulator
dotnet run --project src/PSP.Mock.HamrahAval.Api        # MCI simulator
```

### 4. Create a topup

```bash
curl -X POST https://localhost:5001/api/v1/topups \
     -H "Content-Type: application/json" \
     -d '{"mobileNumber":"09121234567","amount":50000}'
# -> 202 Accepted; response carries transactionId + status:Pending
```

Swagger UI is available at `https://localhost:5001/swagger` in Development.

## The end-to-end flow

1. **Client** → `POST /api/v1/topups` → aggregate created in `Pending`,
   `TopupCreated` enqueued.
2. **Outbox background service (in API)** publishes the event; the Payment consumer advances the
   aggregate to `PaymentCompleted` and publishes `PaymentRequested`.
3. **Payment service** processes the payment and publishes `PaymentCompleted`.
4. **API** consumes it, calls MCI (`/topup`) with Polly:
   - **Success** → aggregate to `AdvicePending`, publishes `AdviceRequested`.
     The Payment service finalises the payment (`AdviceCompleted`), the
     aggregate reaches terminal **`Completed`** and publishes `TopupCompleted`.
   - **Terminal failure** → `ReverseRecord` initiated + `ReverseRequested`
     enqueued. The Payment service reverses the payment (`PaymentReversed`),
     the aggregate reaches terminal **`Reversed`**.

See [docs/SequenceDiagram.md](docs/SequenceDiagram.md) for the Mermaid
diagrams.

## Tests

```bash
# Unit + architecture (fast, no dependencies)
dotnet test --filter "Category!=Integration"

# Integration (Testcontainers: real PostgreSQL + RabbitMQ)
dotnet test tests/PSP.TopupService.IntegrationTests
```

Coverage:

- **Domain** — `TopupTransaction` state machine, value objects (80+ tests).
- **Application** — `CreateTopup`, `ProcessPaymentResult`, `PerformTopup`,
  `PerformAdvice`, `ApplyAdviceResult`, `ApplyReversalResult`, validators,
  pipeline behaviours.
- **Infrastructure** — integration-event mapper, HamrahAval client (Polly).
- **Persistence** — outbox serializer round-trips.
- **Architecture** — Clean-Architecture dependency rules (NetArchTest).
- **Integration** — Testcontainers-driven end-to-end flows.

## Build

```bash
dotnet build PSP.TopupService.sln -c Release
```

The build runs with `TreatWarningsAsErrors=true`, StyleCop, .NET analyzers
and Central Package Management (`Directory.Packages.props`). A clean build is
a hard gate — CI rejects PRs that introduce warnings.

## Git history

The repository follows GitFlow with feature branches merged to `develop` via
`--no-ff` (PR-style merge commits). See [docs/GitFlow.md](docs/GitFlow.md).

## Known improvements

- Authentication / authorization (currently anonymous).
- OpenTelemetry exporters (OTLP) — instrumentation is wired, sinks are not.
- Production log sinks (Seq / Elasticsearch / Datadog) — only console today.
- Multi-currency support — the domain carries currency but IRR is assumed.
- Rate limiting / throttling at the API edge.
