# 0009 — Back-dated purchases settle elapsed installments at creation

**Status**: accepted (2026-09-07, API Phases 31–33)

**Decision**: When a purchase date is in the past, installments with `DueCycle < current month` are marked paid at creation. Card plans create real, historically dated ledger postings (accrue + pay) funded by a required "Paid from" bank (`BackdatedCardBankAccountRequired`, 422). Creditor plans only stamp `PaidOnUtc` (display-only). A current-month DueCycle is accrued and left pending. Future purchase dates are rejected (`FuturePurchaseDate`, 422). Plans expose `PendingAmountMinorUnits`.

**Why**: card liability cannot clear without ledger postings; Recent Purchases must be correct at save and the scheduler must skip settled rows.

**Rejected**: flag-only paid state for cards; waiting for the scheduler.
