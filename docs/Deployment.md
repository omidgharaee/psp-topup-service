# Deployment

The Topup service is composed of three runnable projects plus PostgreSQL and
RabbitMQ dependencies. Docker was intentionally not bundled (per project
requirement); deployments rely on the host's container orchestrator.

## Components

| Component | Project | Purpose |
| --- | --- | --- |
| Topup API | `src/PSP.TopupService.Api` | Public HTTP API, outbox publisher + MassTransit consumers |
| Mock Payment | `src/PSP.Mock.Payment.Api` | Payment-service simulator (RabbitMQ only) |
| Mock HamrahAval | `src/PSP.Mock.HamrahAval.Api` | MCI topup simulator (HTTP) |
| PostgreSQL | external | `topup_transactions`, `outbox_messages`, `inbox_messages`, `audit_logs` |
| RabbitMQ | external | integration-event bus |


## Required infrastructure

- **PostgreSQL 14+** — connection string in `ConnectionStrings:TopupDatabase`.
- **RabbitMQ 3.11+** — connection in `RabbitMq` config block.
- **Network reachability** to the MCI provider (or `MockHamrahAval`) — set the
  `HamrahAval:BaseUrl`.

## Configuration

All runtime knobs live in `appsettings.json`, overridable by environment
variables (use `__` as the section separator, e.g.
`RabbitMq__Host=broker.prod`). Key sections:

- `ConnectionStrings:TopupDatabase`
- `RabbitMq` (host/port/credentials/virtual host)
- `HamrahAval` (HTTP client + Polly tuning + simulated failure rate)
- `Advice` (MaxRetries, RetryDelay, BackoffMultiplier)
- `OutboxPublisher` (PollingInterval, BatchSize, LeaseDuration)
- `MockPayment` (in the mock service)

## Database migrations

```bash
dotnet ef database update \
  --project src/PSP.TopupService.Persistence \
  --startup-project src/PSP.TopupService.Api
```

In production, run migrations as a one-shot Job before rolling the new version.

## CI / CD

GitHub Actions workflows in `.github/workflows/`:

- **build.yml** — restore + build on Ubuntu and Windows, treat-warnings-as-errors
  gate, upload build artifacts.
- **test.yml** — fans out to unit / architecture / integration jobs, publishes
  a TRX report via `dorny/test-reporter` and uploads coverage.
- **publish.yml** — on `v*.*.*` tag, publishes the three runnable projects and
  attaches the tarballs to a GitHub Release.

## Scaling

- **API** — stateless HTTP layer + outbox publisher. Scale horizontally behind a load balancer. 
  Multiple replicas share the same DB/RabbitMQ. The atomic outbox lease (`FOR UPDATE SKIP LOCKED`) prevents double-publish across replicas.
- **Consumers** — MassTransit's competing-consumer pattern lets multiple
  API replicas share a queue. Each consumer type gets its own kebab-case
  queue so a slow consumer does not block others.

## Health probes

| Endpoint | Checks |
| --- | --- |
| `/health/live` | none (process is up) |
| `/health/ready` | PostgreSQL, RabbitMQ, Mock.Payment, Mock.HamrahAval |
| `/health` | aggregate (all checks) |

Map the orchestrator's liveness probe to `/health/live` and readiness to
`/health/ready` so traffic is only routed when every dependency is reachable.

## Integration testing locally

`PSP.TopupService.IntegrationTests` uses **Testcontainers** to spin up a real
PostgreSQL + RabbitMQ, so the same engine runs in CI and locally. See the
project's fixtures for the wiring.
