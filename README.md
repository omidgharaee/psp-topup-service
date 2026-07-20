# PSP Topup Service

> Enterprise-grade Payment Service Provider (PSP) **Topup** service built on Clean Architecture / DDD principles with .NET 10.

A new `TOPUP` transaction type for an existing payment platform. It integrates with a Bank
gateway and the Hamrah-e-Aval (MCI) mobile topup provider, using the **Transactional Outbox**
pattern for reliable messaging and **Polly** for resilience.

---

## Status

> ⚠️ This repository is under active development, implemented feature-by-feature following
> GitFlow. See [docs/GitFlow.md](docs/GitFlow.md) and [docs/DecisionLog.md](docs/DecisionLog.md).

| Layer | Status |
| --- | --- |
| Solution structure & Clean Architecture references | ✅ |
| SharedKernel, Domain, Application, Outbox/Inbox, Worker, API, Mocks | 🚧 in progress |

---

## Tech Stack

- **.NET 10** · **ASP.NET Core Web API** · **Worker Service**
- **Entity Framework Core** + **PostgreSQL** (`Npgsql.EntityFrameworkCore.PostgreSQL`)
- **RabbitMQ** + **MassTransit**
- **Polly** (retry, circuit breaker, timeout, fallback)
- **Serilog** (structured logging, Persian log messages)
- **FluentValidation** · **MediatR** (CQRS) · **Mapster**
- **OpenTelemetry-ready** · **HealthChecks UI**
- **xUnit** + **Testcontainers** (PostgreSQL) + **NetArchTest**
- Central Package Management (`Directory.Packages.props`)

---

## Solution Layout

```
PSP.TopupService.sln
src/
├── PSP.TopupService.Api            # ASP.NET Core API (thin controllers)
├── PSP.TopupService.Application    # CQRS, handlers, validators, behaviors
├── PSP.TopupService.Domain         # Rich domain model (DDD)
├── PSP.TopupService.Infrastructure # External clients (Bank, MCI), messaging
├── PSP.TopupService.Persistence    # EF Core, repositories, outbox/inbox state
├── PSP.TopupService.Contracts      # Integration events / messages (shared)
├── PSP.TopupService.SharedKernel   # Result, BaseEntity, value objects, exceptions
├── PSP.TopupService.Worker         # Outbox publisher background service
├── PSP.Mock.Bank.Api               # Bank simulator
└── PSP.Mock.HamrahAval.Api         # Hamrah-e-Aval simulator
tests/
├── PSP.TopupService.UnitTests
├── PSP.TopupService.IntegrationTests
└── PSP.TopupService.ArchitectureTests
docs/                               # Architecture, Sequence, Outbox, DecisionLog, ...
build/                              # Directory.Build.props, packages.props, stylecop
.github/workflows/                  # build / test / publish
```

See [docs/Architecture.md](docs/Architecture.md) for the dependency rule and
[docs/GitFlow.md](docs/GitFlow.md) for the branching strategy.
