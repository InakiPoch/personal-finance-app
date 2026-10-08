# 0003 — Creditor debt is display-only; payments are per-row; party share is the one ledger-touching path

**Status**: accepted (2026-09-08, extended 2026-09-28, API Phases 34–37, 48–51)

**Decision**:
- Creditor debt lives only in Financing. Paying a creditor installment touches neither bank nor ledger.
- Each payment is a `CreditorInstallmentPayment` row. `Installment.PaidOnUtc` means *fully paid* (remaining = 0). Every creditor figure is computed from remaining. Undo removes the latest row.
- Over-remaining payment is a 400 (`PaymentExceedsRemaining`), never silently capped.
- A pure waterfall allocator serves per-expense (by Sequence) and full-debt (by DueCycle, PurchaseDate, Sequence) payments.
- Paying a **party's share** of an installment is the only ledger-touching creditor payment: a synchronous cross-module call to `IPartiesApi.SettleCurrentAccountAsync` with best-effort compensation (`ReverseTransactionAsync`) if applying the payment fails.

**Why**: creditors are a tracked-debt model; a creditor ledger is a separate future initiative. Per-payment rows are what make undo-last possible; the card path keeps working with the same `PaidOnUtc`.

**Rejected**: a bank-linked creditor pay; a single paid-amount column (cannot undo the last payment); bulk undo; an outbox saga for party-share (deferred, marked `// ponytail:`).
