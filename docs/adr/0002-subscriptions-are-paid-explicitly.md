# 0002 — Subscriptions are paid explicitly, never auto-charged

**Status**: accepted (2026-09-16/17, API Phases 38–40)

**Decision**: Paying a subscription posts one charge dated now and advances exactly one period; undo reverses through the ledger and steps back one period. `RenewSubscription` and the renewal scheduler were removed (`LastRenewalOnUtc` → `LastPaidPeriod`).

**Why**: auto-renew produced N×X in the current month after downtime.

**Rejected**: renewal scheduler; catch-up "pay all overdue" batch (deferred).

**Known limits**: templates store no creation/cancel date, so the by-month view shows active templates in every month and a late payment counts in the month it was posted. Reversing a subscription charge via the generic reverse feed does not roll back the paid period (use Undo on the subscription).
