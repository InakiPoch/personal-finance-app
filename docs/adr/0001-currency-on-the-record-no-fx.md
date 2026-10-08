# 0001 — Currency lives on the record; accounts are poly-currency; no FX

**Status**: accepted (2026-09-21, implemented 2026-09-24/25)

**Decision**: `Currency` is a closed set (`ARS`, `USD`). Each ledger entry, plan, subscription and split carries its own currency. One account holds entries in several currencies; balances are per (account, currency); every transaction is single-currency. Totals are always partitioned by currency and never converted.

**Why**: double-entry must balance per currency; FX rates and net worth are out of scope.

**Rejected**: per-currency account trees; a blended net-worth figure with FX rates.

**Consequences**: EF maps Money as two columns, `<Name>MinorUnits` + `CurrencyCode TEXT NOT NULL DEFAULT 'ARS'` (SQLite cannot add NOT NULL without a default; history backfills to ARS) instead of inventing `Currency.Reference` on read. Legacy subscription entries were flipped ARS→USD once, by a migration scoped to `SubscriptionReferenceId` (irreversible).
