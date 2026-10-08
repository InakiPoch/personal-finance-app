# 0007 — A payment plan is card-backed XOR creditor-financed

**Status**: accepted (2026-09-05, API Phase 18)

**Decision**: `PaymentPlan.CardId` is nullable; exactly one of card or creditor must be present (`PlanNeedsCardOrCreditor`, `PlanCannotMixCardAndCreditor`). Load an Expense has three modes: Card, Debit/Cash, Creditor. Description is required on the plan (1–120 chars, single line) and lives only on `PaymentPlan`, never on the ledger.

**Why**: creditor debt must not appear on the user's card; the ledger stays category-agnostic.

**Rejected**: creditor as metadata on a card plan; a description column on ledger transactions.

**Note**: the two mixing codes fall through to the default HTTP 400; confirm that is intended.
