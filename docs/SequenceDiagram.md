# Sequence Diagrams

The topup flow is **fully asynchronous** after the initial API call. Every
transition is driven by a RabbitMQ event and applied transactionally via the
outbox.

## 1. Create topup (synchronous API → outbox)

```mermaid
sequenceDiagram
    autonumber
    participant C as Client
    participant API as Topup.Api
    participant App as Application
    participant DB as PostgreSQL
    participant W as Topup.Worker
    participant MQ as RabbitMQ

    C->>API: POST /api/v1/topups
    API->>App: CreateTopupCommand
    App->>DB: INSERT topup_transactions (Pending) + outbox_messages (TopupCreated)
    DB-->>App: ok
    App-->>API: 202 Accepted (transactionId)
    API-->>C: 202 Accepted

    note over W,MQ: Asynchronous phase
    loop every PollingInterval
        W->>DB: SELECT ... FOR UPDATE SKIP LOCKED (Pending)
        W->>MQ: publish TopupCreated
        W->>DB: UPDATE outbox_messages SET status=Published
    end
```

## 2. Payment + Topup + Advice happy path

```mermaid
sequenceDiagram
    autonumber
    participant T as TopupService
    participant MQ as RabbitMQ
    participant P as Payment (Mock.Payment)
    participant M as MCI (Mock.HamrahAval)

    note over T: Status: PaymentCompleted
    T->>MQ: PaymentRequested (via outbox)
    MQ->>P: PaymentRequested
    P-->>MQ: PaymentCompleted (succeeded=true)
    MQ->>T: PaymentCompleted
    note over T: MarkPaymentCompleted

    note over T: PerformTopupCommand via consumer
    T->>M: POST /topup (Polly: timeout+retry+cb)
    M-->>T: 200 + reference
    note over T: MarkTopupSucceeded -> AdvicePending
    T->>MQ: AdviceRequested (via outbox)

    MQ->>P: AdviceRequested
    P-->>MQ: AdviceCompleted
    MQ->>T: AdviceCompleted
    note over T: MarkAdviceCompleted -> Completed (terminal)
    T->>MQ: TopupCompleted (public terminal event)
```

## 3. Topup failure → reverse saga

```mermaid
sequenceDiagram
    autonumber
    participant T as TopupService
    participant MQ as RabbitMQ
    participant P as Payment (Mock.Payment)
    participant M as MCI (Mock.HamrahAval)

    T->>M: POST /topup (Polly exhausted)
    M-->>T: 503 (or all retries failed)
    note over T: MarkTopupFailed<br/>(stays TopupInProgress)
    note over T: InitiateReversal (ReverseRecord.Initiated)
    T->>MQ: ReverseRequested (via outbox)

    MQ->>P: ReverseRequested
    P-->>MQ: PaymentReversed (reversalReference)
    MQ->>T: PaymentReversed
    note over T: MarkPaymentReversed -> Reversed (terminal)
    T->>MQ: PaymentReversed (public terminal event)
```

## 4. Advice retry (Saga compensation)

```mermaid
sequenceDiagram
    autonumber
    participant T as TopupService
    participant MQ as RabbitMQ
    participant P as Payment (Mock.Payment)

    note over T: Status: AdvicePending
    loop attempt = 0..MaxRetries
        T->>MQ: AdviceRequested (processAfterUtc = now + backoff)
        MQ->>P: AdviceRequested
        alt success
            P-->>MQ: AdviceCompleted
            MQ->>T: AdviceCompleted
            note over T: MarkAdviceCompleted -> Completed (terminal)
        else failure / timeout / silence
            note over T: MarkAdviceAttemptFailed<br/>(retry counter++)
            note over T: if counter >= MaxRetries:<br/>flag AdviceFailed (terminal, manual)
        end
    end
```

## 5. Outbox publisher drain loop

```mermaid
sequenceDiagram
    autonumber
    participant W as Worker
    participant DB as PostgreSQL
    participant MQ as RabbitMQ

    loop every PollingInterval
        W->>DB: UPDATE outbox SET status=InProgress<br/>WHERE id IN (SELECT FOR UPDATE SKIP LOCKED)
        DB-->>W: leased batch
        loop each row
            W->>MQ: publish (MessageId = outbox.id)
            alt ack
                W->>DB: UPDATE status=Published
            else nack / exception
                W->>DB: UPDATE attempt_count++, status=Pending OR DeadLettered
            end
        end
    end
```

## 6. Consumer idempotency (consume-local)

```mermaid
sequenceDiagram
    autonumber
    participant MQ as RabbitMQ
    participant Cons as Consumer
    participant DB as PostgreSQL

    MQ->>Cons: message (MessageId)
    Cons->>DB: INSERT inbox_messages (id = consumer:MessageId)
    alt insert succeeds
        DB-->>Cons: ok (first time)
        Cons->>Cons: dispatch MediatR command
        Cons->>DB: business change (same transaction)
    else unique violation (sqlstate 23505)
        DB-->>Cons: conflict
        note over Cons: skip — already processed
    end
```
