# Slice 1 — Currency spine + debit-cash expense in USD (the tracer bullet)

> Read `00-overview.md` first. This is the riskiest, highest-value slice: it builds the
> currency spine and drives **one** thin path (a debit-cash expense) through **every** layer
> — API domain → persistence → migration → handler → client type → formatter → selector →
> Dashboard fix → display. Get this green and the model is de-risked.

## Why debit-cash is the tracer

A debit-cash expense is the **thinnest real money path**: `RecordDebitExpense` posts a single
double-entry transaction (`Dr <category> Expense` / `Cr <Bank/Cash>`), with **no** installment,
statement, or accrual machinery. It appears in the transactions list and the Dashboard
out-of-pocket total. If USD flows correctly here, everything heavier is a repeat of the pattern.

## Intent

- Introduce `Currency.Usd` and a code→currency lookup in the SharedKernel.
- Persist currency on ledger `Entry` (the atomic money row of the whole system).
- Let a debit-cash expense be recorded in ARS **or** USD, chosen by the user.
- Fix the Dashboard's blind cross-currency sum and show out-of-pocket **separated** per
  currency.

## API changes

### 1. SharedKernel — the currency vocabulary
- `src/Shared/PersonalFinance.SharedKernel/Currency.cs`
  - Add `public static readonly Currency Usd = new("USD", 2);`
  - Add a lookup: `public static Currency FromCode(string code)` returning `Reference` for
    `"ARS"`, `Usd` for `"USD"`, throwing on anything else. Keep `Reference` as the ARS alias.
- `Money.cs` needs no structural change (it already holds a `Currency`). Confirm
  `Money.FromMinorUnits(long, Currency)` stays the construction path.

### 2. Ledger — persist currency on `Entry`
- `src/Modules/Ledger/PersonalFinance.Ledger/Domain/Entry.cs` — `Amount` is already `Money`,
  so it already carries a `Currency`. The change is **persistence**, not domain.
- `.../Infrastructure/Persistence/Configurations/EntryConfiguration.cs` — today maps
  `Amount` via a single-column converter that invents `Currency.Reference`. Change to a
  **two-column mapping**: keep `AmountMinorUnits` (`long`) and add `CurrencyCode` (`string`),
  reconstructing `Money.FromMinorUnits(minor, Currency.FromCode(code))`. Use `OwnsOne` /
  a `Property`-pair / a custom `IEntityTypeConfiguration` split — whichever matches the repo's
  existing converter idiom (check how other `Money` fields are mapped and stay consistent).
- **Migration** `AddEntryCurrencyCode` (Ledger context): add
  `CurrencyCode TEXT NOT NULL DEFAULT 'ARS'` to `ledger_entries`. Backfills all history to ARS.

### 3. Ledger — partition balances by currency
- `.../ReadViews/vw_ledger_balances.sql` — stop emitting `'ARS' AS CurrencyCode`; select the
  real `e.CurrencyCode` and add it to `GROUP BY`. A single account now yields **one row per
  currency**.
- `GetAccountBalanceHandler.cs` / any balance query handler — return a balance **per
  currency** (a list/dictionary keyed by code), not a single scalar. Build each `Money` with
  `Currency.FromCode(row.CurrencyCode)`.
- Rebuild-view migration (`DROP VIEW` + `ReadViewSqlHelper.Load(...)`), following existing
  precedent.

### 4. Thread currency through the debit-cash command
- `RecordDebitExpenseHandler.cs` (and its `RecordDebitExpenseCommand` DTO / host endpoint):
  add a `CurrencyCode` string to the request; build the posted `Money` with
  `Currency.FromCode(command.CurrencyCode)` instead of `Currency.Reference`. Both legs of the
  posted transaction use the **same** currency (single-currency transaction invariant).
- Confirm the ledger `PostTransaction` path carries the entry currency all the way to the
  `Entry` rows (it should, since `Entry.Amount` is `Money`).

## Client changes

### 1. Currency spine
- `core/types/currency-code.ts` — widen to `export type CurrencyCode = 'ARS' | 'USD'`.
- `core/money/money.ts` — replace `formatArs` with
  `formatMoney(value: Money, code: CurrencyCode)`. For `'USD'` use
  `new Intl.NumberFormat('en-US', { style:'currency', currency:'USD', minimumFractionDigits:2 })`;
  keep the `es-AR`/ARS formatter for `'ARS'`. Divide by 100 as today. Keep `fromMinorUnits`/
  `toMinorUnits` untouched. (If a big-bang rename of ~40 call sites is undesirable in this
  slice, keep a thin `formatArs = v => formatMoney(v, 'ARS')` shim and migrate call sites as
  each view is touched by its slice.)

### 2. Currency selector on Load Expense (debit mode)
- `features/financing/pages/load-expense-page/load-expense-page.{ts,html}` — add a
  `currency: FormControl<CurrencyCode>` (default `'ARS'`) next to the `amount` control. Render
  a small ARS/USD toggle or `<select>` beside the `$` glyph. On submit, include `currencyCode`
  in the `RecordDebitExpense` payload. **Only the debit branch** needs it wired in this slice
  (card/creditor come in Slice 2); the control can exist for all modes but only debit consumes
  it now.

### 3. Fix the Dashboard cross-currency landmine + separated display
- `features/reports/pages/dashboard-page/dashboard-page.ts` — `sumByLabel`, `monthlyTotal`,
  and any reducer over `amountMinorUnits` must **group by `currencyCode`** and produce a total
  **per currency**. The `Grouping` type must retain `currencyCode`.
- `dashboard-page.html` — render the out-of-pocket total as one figure per present currency
  (suppress empty ones). Transactions table
  (`features/ledger/pages/transactions-page/transactions-table.{ts,html}`) uses
  `formatMoney(row.amountMinorUnits, row.currencyCode)` per row.
- The reporting DTOs that already carry `currencyCode` (typed `CurrencyCode`) now legitimately
  vary — no code change beyond the union widening, but confirm the monthly-expenses query
  (below) supplies real per-currency rows.

### 4. Reporting query (out-of-pocket)
- `Reporting/Sql/monthly_expenses.sql` + `vw_ledger_monthly_expenses.sql` — add `CurrencyCode`
  to `SELECT`/`GROUP BY`, drop the `'ARS'` literal. `MonthlyExpensesQuery.cs` returns rows that
  now include a real currency; the client groups by it.

## Test plan

### API (xUnit)
- SharedKernel: `Currency.FromCode("USD")` → `Usd`; `"ARS"` → `Reference`; unknown → throws.
- `Money` operator guard: `arsMoney + usdMoney` throws (regression lock on the safety net).
- `RecordDebitExpenseHandler`: given `CurrencyCode = "USD"`, both posted entries have
  `Currency.Usd`; balance query returns a **USD** balance distinct from any ARS balance on the
  same account (poly-currency proof).
- Balance view/handler: an account with one ARS and one USD entry yields **two** balance rows.

### Client (Karma)
- `core/money/money.spec.ts` — add `formatMoney(x, 'USD')` facts (asserts `$`/`US$` + correct
  digits via the existing `digitsOf` helper; keep locale-robust assertions).
- `dashboard-page.spec.ts` — fixture with mixed ARS+USD rows asserts **two** separated totals
  and that they are **not** summed together (the core regression: today's code would blend them).
- `load-expense-page.spec.ts` — selecting USD sends `currencyCode: 'USD'` in the debit payload.

## Verification (end to end, before Slice 2)
1. Run API + client. Load a debit-cash expense in **USD**.
2. Confirm it appears in the transactions list formatted as USD.
3. Confirm the Dashboard out-of-pocket shows a **separate** USD figure alongside ARS, never
   a blended number.
4. Confirm an existing ARS expense still reads ARS (backfill correct).
5. API + client suites green; lint + prod build clean.

## Doc-sync checklist (close the slice)
- `app/api/.claude/CLAUDE.md` — phase bump + a line on the currency spine (`Currency.Usd`,
  `Currency.FromCode`, `Entry.CurrencyCode`, poly-currency balances).
- `app/client/.claude/CLAUDE.md` — `CurrencyCode` widened, `formatMoney` replaces `formatArs`,
  Dashboard now partitions by currency.
- `app/api/docs/DESIGN.md` + `app/client/docs/DESIGN.md` — the no-FX / currency-on-record /
  partition-by-currency decision.
- Note the migration id(s) added.
