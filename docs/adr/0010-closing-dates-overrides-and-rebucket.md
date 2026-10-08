# 0010 — Card closing dates: usual day + per-month overrides, with re-bucketing

**Status**: accepted (2026-09-30, API Phase 55)

**Decision**: A `CreditCard` has a usual closing day plus per-month `ClosingOverride` rows (`financing_card_closing_overrides`). The resolver (`ClosingDayOf`) clamps to month length. An edit recomputes the first-installment cycle of open plans. "Closed" means *charged* (a `MonthlyStatement` exists), not "date passed". A locked month rejects edits (409 `ClosingMonthLocked`); if any installment of a moved plan is already charged the whole edit is refused (409 `ClosingChangeMovesChargedPurchase`). Charged installments never move. An override's day lies inside its cycle month.

**Why**: banks shift closing dates; history must not change.

**Limits**: a bank closing on the 1st of the next month cannot be represented (would need a full date). Creditor cutoff 26 is out of scope.
