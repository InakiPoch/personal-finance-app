# 0008 — Card payments derive from installments, not `AmountDue`

**Status**: accepted (2026-09-07, API Phases 28–29)

**Decision**: A statement's payable is Σ(accrued, non-reversed, unpaid installments). Installments carry `PaidOnUtc`. Paying a single installment posts a plain `Dr CardLiability / Cr Bank` with no carried-credit netting; netting stays on the full-statement path.

**Why**: `AmountDue` is increment-only, but reversals reduce ledger liability, so paying `AmountDue` overcharged. The carried credit is a card-level pool, not per installment.

**Rejected**: decrementing `AmountDue` on reversal; netting per installment; fractional card-installment payments (full cuota only).

**Known gap**: reversing a card-bill payment does not un-mark installments paid. `GetCardPurchasesHandler`'s outstanding filter reads statement paid state, not `Installment.PaidOnUtc`, so a cuota paid individually on an open statement may still count as outstanding in the dashboard drill-down — verify before fixing.
