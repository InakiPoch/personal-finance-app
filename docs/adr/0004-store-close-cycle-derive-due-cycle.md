# 0004 — Store the close cycle; derive DueCycle in readers

**Status**: accepted (2026-09-06, API Phase 25)

**Decision**: Installments keep the statement **close** cycle (`CycleYear/CycleMonth`). `DueCycle = Cycle + 1 month` is derived (`BillingCycle.DueCycle`, never stored). Statement surfaces key on Cycle; payment-facing surfaces (due-now, schedules, party shares, next payment) key on DueCycle.

**Why**: statements stay honest and no card-path migration is needed; accrual-boundary tests stay green.

**Rejected**: storing the due cycle (migration, breaks statement identity); display-only +1 for splits (label and money diverge by ~10 days).
