# Incomes support — overview

> Planning docs only (written 2026-09-25, branch `feat/incomes-support`). No code has been written.
> Read this file first, then the slice docs **in order**. Each slice is a vertical tracer bullet
> (API + client + tests + doc-sync) and must be green before the next one starts.

## The problem

The app only tracks money going **out**. The Dashboard "Out of pocket" tile sums Expense-account
debits per month and currency. There is no way to record money coming **in**, and there is no view
that lists individual money movements. The Dashboard only shows per-category totals, and the
Transactions page (`features/ledger/pages/transactions-page/`) shows raw ledger rows with
synthetic descriptions and no account names.

## The goal

1. Register **incomes**: real money arriving in one of my Bank/Cash accounts.
2. See income next to out of pocket on the Dashboard through a **toggle** on the existing tile,
   so the Dashboard doesn't stack another tile.
3. A new **Money Flow** page: an accounting-style monthly table where each row is one movement of
   *my* money. Income shows as a green `+amount` and outcome as a red `−amount`, and the empty cell
   of each row shows a neutral `—`.

## Settled decisions (grilling session 2026-09-25) and why

| # | Decision | Why |
|---|----------|-----|
| 1 | An **income** is only real money into a Bank/Cash account. It is **not** a card refund (reversals already cover that) and **not** a party paying back a receivable (`SettleCurrentAccount` settles a debt; it isn't a gain). | Keeps "income" meaning *money I gained*. |
| 2 | Income fields: amount, currency (ARS/USD), bank/cash account, date (back-dating allowed, **no future dates**), required description. **No categories.** | YAGNI. Categories can come later if a "by source" breakdown is ever wanted. |
| 3 | Manual one-off entries only. No recurrence, no scheduler. | Salary doesn't arrive every month, and the subscriptions auto-charge scheduler was just removed because it double-charged (`docs/subscriptions-rework/`). |
| 4 | Money Flow **outcomes are exactly the set Out of pocket counts**: debit/cash expenses plus paid subscriptions, counting only **my share** of a split expense. Card, creditor and party payments are **not** auto-listed; the user records them as manual debit expenses when paid from bank or cash. | The two "what I spent this month" numbers must reconcile. Manual entry is also more flexible, since those debts aren't always paid by debit or cash. |
| 5 | Month picker, the same as the Dashboard (`<input type="month">`). | Same monthly mental model. |
| 6 | Undo = a ledger **reversal** (append-only), available on **income rows only**. Reversed pairs (original + storno) are **hidden** from Money Flow and its totals. Add a guard so a transaction can't be reversed twice. | The ledger stays append-only. Expense reversal already has a page (`reverse-movement-page`). A new Undo button would expose the double-reversal hole. |
| 7 | Dashboard: the existing Out of pocket tile gets a **segmented toggle `Out of pocket \| Income`**. The default is Out of pocket and the choice is **not** remembered. The month picker is shared. The Income side shows **per-currency totals only**. | A cleaner Dashboard with no stacked tiles. There are no income categories to list. |
| 8 | Recording an income happens on a dedicated **Record income** page, `/ledger/incomes/new`, linked from a Dashboard quick action and (from Slice 2) from the Money Flow page. | Mirrors Load Expense and keeps the table page read-mostly. |
| 9 | **Money Flow** page at `/ledger/money-flow`, in the nav. Columns: Date · Description · Account · Income · Outcome. The footer has one line per currency with the Income total and the Outcome total, and **no net figure**. SYSTEM.md gains a "Signed amounts" rule allowing `--ledger` (green) and `--negative` (red) on signed money. | This is the user's requested accounting table. A net figure is speculative for now. |
| 10 | A **`Transaction.Description` column** is added (Ledger migration) and persisted from now on. Rows written before it fall back to the **category (expense account) name**. | See "Verified gap" below. |

## Verified gap: descriptions are dropped today

The user thought the expense "legend" (the description typed on Load Expense) was already stored.
It isn't:

- `Ledger/Domain/Transaction.cs` has no Description property.
- `RecordDebitExpenseHandler`'s **non-split** branch validates `command.Description` and then drops it.
- The **split** branch forwards it to Parties' `RegisterSharedExpenseCommand`. That command passes it as
  `PostTransactionCommand.Description`, but `PostTransactionHandler` ignores it too, and
  `ExpenseSplit` has no Description field. So split descriptions are lost as well.
- `PostTransactionCommand` *already has* `string? Description = null` (the contract is ready), and
  `SettleCurrentAccountHandler` already sends `"Settlement from {party}"` there.

**The fix (Slice 1):** add `Description` to `Transaction`, persist it in `PostTransactionHandler` and
in `RecordDebitExpenseHandler`'s non-split post. That one change repairs debit expenses, split
expenses, settlements and the new incomes at once. Historic rows can't be backfilled; Money Flow
shows their category name instead.

## Ledger postings (double entry)

```
Record income  (new)           Dr  Bank/Cash account   amount
                               Cr  "Income" account    amount     (single lazily-created account, Type=Income, Kind=Income)

Debit expense  (existing)      Dr  <Category> Expense  amount
                               Cr  Bank/Cash account   amount

Split debit    (existing)      Dr  <Category> Expense  my share     ← only this counts as outcome (decision 4)
                               Dr  Receivable_k        party share
                               Cr  Bank/Cash account   total

Undo           (existing)      mirrored storno, OriginalTransactionId = original.Id
```

`AccountType.Income` and `AccountKind.Income` **already exist**
(`Ledger.Contracts/AccountType.cs`, `AccountKind.cs`), and `Account.Create` accepts them. Nothing
uses them yet. Types are stored as strings, so the account itself needs **no migration**.

## Slices (strict order)

| Slice | Doc | Delivers | Migrations |
|-------|-----|----------|------------|
| 1 | `slice-1-record-income-and-dashboard-toggle.md` | `Transaction.Description` persistence, Record income command/endpoint/page, monthly incomes report, Dashboard toggle | Ledger: `AddTransactionDescription`, `AddMonthlyIncomesView` |
| 2 | `slice-2-money-flow-table.md` | `vw_ledger_money_flow` view + `GET /v1/reports/money-flow`, Money Flow page + nav + footer, SYSTEM.md "Signed amounts" | Ledger: `AddMoneyFlowView` |
| 3 | `slice-3-undo-income.md` | Double-reversal guard (409), Undo on income rows | none |

Each slice is implemented **one green-lit step at a time**, the user's established cadence:
**(1) API production → (2) API tests → (3) client production → (4) client specs → (5) doc-sync.**
Stop after each step and wait for the user's go-ahead. The user commits their own work, so never commit.

## Shared context for every slice

- **API** (`app/api/`, .NET 10 modular monolith; read `app/api/.claude/CLAUDE.md`): Ledger module =
  `src/Modules/Ledger/PersonalFinance.Ledger{,.Contracts}`. Host endpoints live in
  `src/Bootstrap/PersonalFinance.Api/Endpoints/`. Errors map to HTTP in `Endpoints/ErrorHttpStatusHelper.cs`.
  Reporting (`src/Reporting/PersonalFinance.Reporting/`) is plain ADO.NET, and each query loads embedded SQL
  through `ReportingSqlHelper.Load("x.sql")`. Reporting SQL may only query `vw_*` views. Views live in
  each module's `Infrastructure/Persistence/ReadViews/` and are created by migrations
  (`ReadViewSqlHelper.Load(...)` in `Up()`; the prior SQL goes inlined in `Down()`).
- **Tests (API)**: xUnit v3, one project per module under `tests/`. Handler tests use in-memory SQLite
  (template: `tests/PersonalFinance.Ledger.Tests/RecordDebitExpenseHandlerTests.cs`). Reporting and Api
  tests run real migrations (`ReportingIntegrationFixture`, `ApiWebApplicationFactory`).
  **Gotcha:** `dotnet test --solution` reports "Zero tests ran" in this shell, so run each test project
  directly (`dotnet run --project tests/PersonalFinance.Ledger.Tests`). Baseline: **368** API tests (Phase 44).
- **Client** (`app/client/`, Angular 20, zoneless; read `app/client/.claude/CLAUDE.md` and
  `docs/SYSTEM.md`): features are lazy-loaded (`app.routes.ts` → `features/<f>/<f>.routes.ts`), and the nav
  is the `navItems` array in `src/app/app.ts`. Money is `Money` (branded minor units, `core/types/money.ts`),
  and the currency always travels in a sibling `currencyCode: CurrencyCode` (`'ARS' | 'USD'`). Format with
  `formatMoney(value, code)` (`core/money/money.ts`); there is no money pipe. Tables use a page (container,
  owns `loadStatus`) plus a presentational `*-table` component (pattern: `transactions-page/transactions-table`).
- **Tests (client)**: Karma + Jasmine, `provideZonelessChangeDetection()`, `HttpTestingController` for
  services, URLs built from `environment`.
  `CHROME_BIN=/usr/bin/brave pnpm ng test --watch=false --browsers=ChromeHeadless`, plus `pnpm ng lint` and
  `pnpm ng build --configuration production`. Baseline: **303** client tests.

## Out of scope (deliberately)

- Income categories and a "by source" breakdown (decision 2).
- Recurring incomes or any scheduler (decision 3).
- A net (income − outcome) figure on the Dashboard or in the footer (decisions 7, 9).
- Auto-listing card statement, installment, creditor or party payments as outcomes (decision 4).
- Backfilling descriptions of historic transactions. It's impossible because the data was never stored.
- Undo for expense rows from Money Flow (decision 6). `reverse-movement-page` already covers expenses.
- A unique DB index on `OriginalTransactionId` (Slice 3 guards in the handler instead; see there).
