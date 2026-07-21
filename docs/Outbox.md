# Transactional Outbox

The Topup service uses the **transactional outbox** pattern to guarantee that
a state change in the database and the intent to publish a message cannot
diverge. No code path publishes directly to RabbitMQ.

## Why

A naive `SaveChanges(); publish();` sequence loses the publish if the process
crashes between the two. The outbox writes the message in the **same**
transaction as the business change, so either both commit or neither does. A
background worker then drains the table.

## Tables

- **`outbox_messages`** — the queue. Each row carries:
  - `id` (Guid, becomes the broker MessageId),
  - `type` (`$type` discriminator, e.g. `AdviceRequested`),
  - `payload` (`jsonb` integration-event body),
  - `correlation_id`,
  - `status` (`Pending` / `InProgress` / `Published` / `DeadLettered`),
  - `attempt_count`, `max_attempts`,
  - `locked_until_utc` (lease; also used to schedule retries),
  - `processed_on_utc`, `last_error`, `dead_letter_after_utc`.

- **`inbox_messages`** — the consumer idempotency table. The unique index on
  `(message_id, consumer)` makes a re-delivered message fail to insert, which
  is the signal that the message was already processed.

See [Database.md](Database.md) for the full schema and indexes.

## Worker drain loop

`OutboxPublisherWorker` (BackgroundService) runs every `PollingInterval`:

1. **Reclaim expired leases** — `UPDATE ... SET status=Pending WHERE
   status=InProgress AND locked_until_utc < now`.
2. **Lease a batch** — atomically, in a single SQL statement:

   ```sql
   WITH next AS (
     SELECT id FROM outbox_messages
     WHERE status = 0 AND (locked_until_utc IS NULL OR locked_until_utc < now())
     ORDER BY occurred_on_utc
     LIMIT @batchSize
     FOR UPDATE SKIP LOCKED
   )
   UPDATE outbox_messages
   SET status = 1, locked_until_utc = @until, attempt_count = attempt_count + 1
   FROM next
   WHERE outbox_messages.id = next.id
   RETURNING outbox_messages.id
   ```

   `FOR UPDATE SKIP LOCKED` guarantees two workers never lease the same row.
3. **Publish each row** via MassTransit. The broker `MessageId` is the outbox
   `id`, so accidental re-publishes collapse at the broker.
4. **Mark outcome** — `Published` on success; on failure increment
   `attempt_count` and either re-queue (`Pending`, with an
   `attempt_count * 5s` cool-down) or dead-letter when `MaxAttempts` is
   reached.

## Scheduled retries (Saga compensation)

The outbox doubles as a delay queue: `locked_until_utc` is set to a future
instant, so the lease query naturally skips not-yet-due rows. This is how the
advice retry saga (ADR-0017) and the scheduled retries of `AdviceRequestedEvent`
work without a separate scheduler.

## Wire contract

`OutboxMessageSerializer` writes a typed `IntegrationEnvelope`:

```json
{
  "$type": "AdviceRequested",
  "payload": { "topupId": "...", "adviceAttempt": 1, ... },
  "correlationId": "...",
  "version": 1,
  "occurredOnUtc": "2026-07-20T10:00:00Z"
}
```

`IntegrationEventMapper` rebuilds the strongly-typed `IIntegrationEvent` from
the envelope before handing the message to MassTransit, so consumers receive a
real object rather than a JSON string.

See [SequenceDiagram.md](SequenceDiagram.md) for the drain loop and idempotency
sequences, and [DecisionLog.md](DecisionLog.md) ADR-0012, ADR-0013, ADR-0017,
ADR-0020 for the rationale.
