# Integration-event dispatch: two modes, one dispatcher

`IIntegrationEventDispatcher` (`PersonalFinance.Infrastructure.Messaging`) resolves every
`IIntegrationEventHandler<TEvent>` for an event's runtime type and awaits them sequentially.
It is deliberately **mode-agnostic** — it does not know or care whether a durable row backs the
call. The "mode" is a property of *who calls the dispatcher and what they did first*, not of the
dispatcher itself. This note exists so later phases don't add a second dispatcher for the "other"
mode.

## Mode 1 — Durable (Outbox-backed)

- **Path:** producer command → `IOutboxWriter.Add(event)` enlists an `OutboxMessage` in the
  *same* `SaveChangesAsync` transaction as the state change (RNF-3) → `OutboxWorker` drains the
  table on a timer → deserializes → `IIntegrationEventDispatcher.DispatchAsync` → `MarkProcessedAsync`.
- **Guarantee:** at-least-once. A handler crash leaves the row unprocessed; it retries next tick.
  Consumers dedupe via the inbox (`<module>_inbox_consumed`, key `(MessageId, Consumer)`, RNF-2).
- **Who uses it:** only `PaymentPlanCreatedIntegrationEvent` (D8) — the single genuine
  transactional dual-write in this system (Financing writes the plan *and* must reliably tell
  Parties). Nothing else earns the durability cost.

## Mode 2 — Direct (in-process)

- **Path:** a scheduler (`AccrueInstallments`, `RenewDueSubscriptions`) finishes its own
  transaction, then calls `IIntegrationEventDispatcher.DispatchAsync` synchronously in the same
  process. No persistence, no Outbox row.
- **Guarantee:** none beyond "the handlers ran before the tick returned". If the process dies
  mid-dispatch, the event is simply lost — correctness is restored on the next scheduler tick,
  which is idempotent by construction (D6: it re-scans for work not yet done, it does not replay
  a log).
- **Who uses it:** `InstallmentAccruedIntegrationEvent`, `SubscriptionRenewedIntegrationEvent`.
  These are clock-triggered, not transaction-triggered, so an Outbox row would be the wrong tool
  (D6 is explicitly about not conflating the two).

## Why not two dispatchers

The fan-out logic (resolve handlers by runtime type, invoke `HandleAsync`, await) is identical
in both modes. Splitting it would duplicate that code and invite drift. The durability decision
belongs one level up — at the call site — where the D6/D8 distinction actually lives.

## Mapping

| Integration event | Mode | Trigger | Rationale |
| --- | --- | --- | --- |
| `PaymentPlanCreatedIntegrationEvent` | Durable | Financing command (with split payload) | D8 — transactional dual-write, must not be lost |
| `InstallmentAccruedIntegrationEvent` | Direct | `AccrueInstallments` scheduler | D6 — clock-triggered, scheduler is idempotent |
| `SubscriptionRenewedIntegrationEvent` | Direct | `RenewDueSubscriptions` scheduler | D6 — clock-triggered, scheduler is idempotent |
| `TransactionPostedIntegrationEvent` | — | Ledger (informational) | No subscriber; Reporting reads `vw_*` views (D5) |
