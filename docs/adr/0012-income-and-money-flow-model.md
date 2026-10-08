# 0012 — Income model, Money Flow filter and the global double-reversal guard

**Status**: accepted (2026-09-24/25, API Phases 45–47)

**Decision**: Income = `Dr Bank/Cash / Cr Income` (one lazily created Income account). Manual only: no categories, no recurrence, no future dates, no net figure. Money Flow's outcome filter equals the monthly-expenses filter; reversed pairs are hidden; undo exists on income rows only. The double-reversal guard is global (`Ledger.TransactionAlreadyReversed`, 409), not per type. Categories for debit/cash expenses are Expense accounts get-or-created by trimmed, case-insensitive name (no category entity).

**Rejected**: income categories, recurring incomes, a net figure, a per-type guard, a categories table.

**Known gaps**: the guard is check-then-insert (upgrade path: unique filtered index on `OriginalTransactionId`); a storno is dated `UtcNow`, so Money Flow and Out of pocket diverge for cross-month reversals.
