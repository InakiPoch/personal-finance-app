# USD (Dollar) support — Initiative overview

## Why this exists

The app is **single-currency (ARS) by construction** today. The goal is to let the user
**record payments in USD** across every money-entry point, pick the currency when loading
an expense or subscription, and see amounts **separated per currency** everywhere they are
displayed (the Dashboard shows out-of-pocket in ARS *and* in USD, never blended).

Behavior must be **identical regardless of currency**: same installment division, same party
split arrangement, same accrual — only the currency tag and the display change.

Existing data: **only subscriptions become USD** (they were always paid in dollars). Every
other existing record stays ARS.

This is delivered as **vertical slices (tracer bullets)**. Each slice is one API + Client +
Tests unit, independently testable, implemented and greened **one at a time, in order**,
before the next begins.

## The single most important discovery

**`Money` is already currency-aware.** This is not a domain-modeling problem — it is a
plumbing problem.

- `app/api/src/Shared/PersonalFinance.SharedKernel/Money.cs` —
  `readonly record struct Money(long MinorUnits, Currency Currency)`. Its arithmetic
  operators call `ensureSameCurrency` and **throw `InvalidOperationException`** on any
  mixed-currency add / subtract / compare. This is our **safety net**: the moment code sums
  a mixed set, it throws instead of silently producing garbage.
- `app/api/src/Shared/PersonalFinance.SharedKernel/Currency.cs` —
  `sealed record Currency(string Code, byte DecimalPlaces)` with **one** hardcoded value:
  `public static readonly Currency Reference = new("ARS", 2)`. No currency enum, no currency
  table.

What is missing everywhere is the **persisted currency**:

- **No currency column exists in the DB.** Every money column is a bare `long`
  (`<Name>MinorUnits`). Every EF value converter **invents** `Currency.Reference` on read:
  `.HasConversion(a => a.MinorUnits, v => Money.FromMinorUnits(v, Currency.Reference))`.
- **7 read-views emit `'ARS' AS CurrencyCode` as a literal string**, not data.
- Client: `app/client/src/app/core/types/currency-code.ts` is `type CurrencyCode = 'ARS'`
  (a one-member union). `app/client/src/app/core/money/money.ts` has a single hardcoded
  `formatArs` (`Intl.NumberFormat('es-AR', { style:'currency', currency:'ARS', ... })`).
  `dashboard-page.ts` reduces `amountMinorUnits` across rows **ignoring `currencyCode`** —
  this is the landmine that must be fixed in Slice 1.

## Settled design decisions (apply to every slice)

1. **No FX, ever.** Never convert USD↔ARS, never sum them into a shared total. Every amount
   is *tagged* with its currency; every aggregation is *partitioned* by currency; the UI
   shows each currency independently, side by side. A blended net-worth figure with an
   exchange rate is explicitly **out of scope** (a possible later feature).

2. **Currency lives on the record/entry (not the account).** Each aggregate root
   (`PaymentPlan`, `SubscriptionTemplate`, `ExpenseSplit`) carries **one** `CurrencyCode`,
   propagated down to its installments and its ledger entries. A single ledger transaction is
   **one currency** — so double-entry still balances *within* that currency
   (`Σdebits == Σcredits` per currency). **Accounts are poly-currency**: a bank/cash account
   can hold both ARS and USD entries, and balance queries `GROUP BY AccountId, CurrencyCode`.
   No parallel per-currency account trees are created.

3. **All four money-entry points get USD**, in this order of difficulty:
   Load Expense (debit-cash → card → creditor), Load Subscription, then Parties
   (shared expense + settle current account) last, because a party can end up with **two**
   current-account balances (one per currency).

4. **Existing-data migration.** Add `CurrencyCode` with `DEFAULT 'ARS'` to every money table
   (this backfills all history to ARS in one shot). Then flip subscriptions to USD:
   the **template** and **both legs** of every ledger transaction with
   `SubscriptionReferenceId IS NOT NULL` (reversals included — `Transaction.Reverse` copies
   the reference). Accepted consequence: the shared funding Bank/Cash account becomes
   poly-currency and shows separate ARS and USD balances.

5. **The closed set is `{ARS, USD}`.** `Currency.Usd = new("USD", 2)` (2 decimals, same as
   ARS). Extend the `Currency` singleton and the client union; **do not** build a currency
   table or a currency-management UI.

## The cross-cutting invariant (repeat in every slice)

> **Never sum a possibly-mixed-currency set without partitioning by currency first.**

On the API, an un-partitioned sum over mixed `Money` **throws** (the operators guard it).
On the client, `dashboard-page.ts` and every reducer that adds `amountMinorUnits` must
`GROUP BY currencyCode` first, or it will silently add dollars to pesos. Every read-view SUM
must add `CurrencyCode` to its `SELECT` and `GROUP BY` and stop emitting the `'ARS'` literal.

## The currency spine (built in Slice 1, reused by all)

- **API**: `Currency.Usd`; a lookup `Currency FromCode(string code)` (maps `"ARS"`/`"USD"`).
  The `Money` EF converter changes from *inventing* `Currency.Reference` to reading a stored
  sibling `CurrencyCode` column (a two-column mapping / owned value, one per money field).
  Commands/DTOs that carry an inbound `long` amount gain a `CurrencyCode` string.
- **Client**: widen `currency-code.ts` to `'ARS' | 'USD'`; replace `formatArs` with
  `formatMoney(value: Money, code: CurrencyCode)` in `core/money/money.ts` (USD → `en-US`
  `Intl.NumberFormat`). Thread a `currencyCode` alongside every displayed `Money`.

## Slice index (implement in this order)

| # | Doc | Scope | Proves |
|---|-----|-------|--------|
| 1 | `slice-1-foundation-debit-expense.md` | Currency spine + Ledger `Entry` currency + debit-cash expense end to end + Dashboard fix | The whole stack carries and separates currency |
| 2 | `slice-2-card-expense.md` | Financing money tables + card-mode expense + statements/recent/card-debt | Same installment division regardless of currency |
| 3 | `slice-3-subscriptions-and-migration.md` | Subscription currency + **the existing-data flip to USD** | Existing subscriptions read USD; instruments go poly-currency |
| 4 | `slice-4-parties.md` | ExpenseSplit currency + shared expense + settle + two-balance current account | Same party arrangement regardless of currency |

## Global display rule

Render **only currencies that have data** — suppress an empty `USD $0.00` / `ARS $0.00`.
A total or list shows an ARS figure and/or a USD figure, each independent, never blended.

## Commands & gotchas (from the repo)

- **API tests**: `dotnet test --solution PersonalFinance.sln` (or `--project` per module).
  *Known flake: `dotnet test` has misbehaved here before — fall back to `dotnet run` in the
  test project if the runner fails.* `global.json` pins the .NET 10 Microsoft.Testing.Platform
  runner; always pass `--solution` or `--project`, never a bare path.
- **Client**: from `app/client/` — `pnpm ng lint`, `pnpm ng build --configuration production`,
  and `CHROME_BIN=/usr/bin/brave pnpm ng test --watch=false --browsers=ChromeHeadless`.
- **EF migrations**: one `DbContext` per module (`Ledger`, `Financing`, `Subscriptions`,
  `Parties`), sharing one SQLite file. Each module is its own `--startup-project`/`--context`.
  Migrations live under `src/Modules/<M>/.../Infrastructure/Persistence/Migrations/`.
  SQLite can't add a `NOT NULL` column over existing rows without a default → use
  `CurrencyCode TEXT NOT NULL DEFAULT 'ARS'`. Read-view changes are hand-authored
  `DROP VIEW` + `ReadViewSqlHelper.Load(...)` migrations (many precedents exist).
- **Docs**: technical artifacts are English. Each slice ends with a doc-sync checklist
  (API `CLAUDE.md` phase bump, client `CLAUDE.md`, the relevant `DESIGN.md`/`PRD.md`).
