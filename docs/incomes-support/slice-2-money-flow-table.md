# Slice 2: Money Flow table (the accounting view)

> Read `00-overview.md` and `slice-1-record-income-and-dashboard-toggle.md` first. **Slice 1 must be
> green**: this slice reads `ledger_transactions.Description` and the Income account it introduced.

## Intent

A monthly, accounting-style table where every row is one movement of **my** money:

| Date | Description | Account | Income | Outcome |
|------|-------------|---------|--------|---------|
| 24/09/2026 | Salary September | Galicia | **+$ 850.000,00** (green) | — |
| 22/09/2026 | Groceries at Coto | Galicia | — | **−$ 45.300,00** (red) |

The footer has one line per currency present in the month, with the Income total and the Outcome total, and no net figure.

## Row semantics (the heart of the slice)

A row = **one non-reversed, non-reversal ledger transaction** that moves my money:
- **Outcome row**: the transaction has a debit on an Expense-type account whose Kind is not
  `Receivable`/`CardPurchases`. That is **exactly** the filter of `vw_ledger_monthly_expenses`, so the
  amount is *my share* for split expenses (the receivable legs are excluded). Paid subscriptions show up
  here too.
- **Income row**: the transaction credits an Income-type account (Slice 1).
- **Excluded naturally** (both sums are 0): card purchase accruals (CardPurchases kind), statement and
  installment payments (Dr liability / Cr bank), party settlements (Dr bank / Cr receivable). This is
  deliberate (overview decision 4). The user records those as manual debit expenses when relevant.
- **Reversed pairs are hidden**: exclude any transaction that *is* a reversal
  (`OriginalTransactionId IS NOT NULL`) and any transaction that *has been* reversed (a row exists
  whose `OriginalTransactionId` = its Id).
- **Description**: `COALESCE(t.Description, <expense account name>, 'Income')`. Historic rows get
  their category name (overview decision 10).
- **Account**: the name of the "funding side" leg, meaning the entry whose account is neither Expense-type,
  Income-type nor Receivable-kind (the Bank/Cash account, or a card liability for a card-funded subscription).

**Known, accepted difference from Out of pocket:** `ReverseTransaction` dates the storno
`DateTimeOffset.UtcNow` (`Endpoints/Ledger/ReverseTransaction.cs:11`). If an expense from August is
reversed in September, Out of pocket shows +X in August and −X in September, while Money Flow hides
both. Same-month reversals reconcile exactly. Document this in the view's header comment; don't
"fix" Out of pocket in this initiative.

## Step 1: API production

### 1a. View `vw_ledger_money_flow`
- New file `src/Modules/Ledger/PersonalFinance.Ledger/Infrastructure/Persistence/ReadViews/vw_ledger_money_flow.sql`:
  ```sql
  -- One row per live (non-reversed, non-reversal) transaction that moves my money.
  -- Outcome filter == vw_ledger_monthly_expenses (my share only). Cross-month reversals: see slice-2 doc.
  CREATE VIEW vw_ledger_money_flow AS
  SELECT
      t.Id AS TransactionId,
      t.PostedOnUtc AS PostedOnUtc,
      strftime('%Y-%m', t.PostedOnUtc) AS Month,
      COALESCE(t.Description,
               MAX(CASE WHEN a.Type = 'Expense' AND a.Kind NOT IN ('Receivable', 'CardPurchases') THEN a.Name END),
               'Income') AS Description,
      MAX(CASE WHEN a.Type NOT IN ('Expense', 'Income') AND a.Kind <> 'Receivable' THEN a.Name END) AS AccountName,
      SUM(CASE WHEN a.Type = 'Income' AND e.Direction = 'Credit' THEN e.AmountMinorUnits ELSE 0 END) AS IncomeMinorUnits,
      SUM(CASE WHEN a.Type = 'Expense' AND a.Kind NOT IN ('Receivable', 'CardPurchases') AND e.Direction = 'Debit'
               THEN e.AmountMinorUnits ELSE 0 END) AS OutcomeMinorUnits,
      e.CurrencyCode AS CurrencyCode
  FROM ledger_transactions t
  INNER JOIN ledger_entries e  ON e.TransactionId = t.Id
  INNER JOIN ledger_accounts a ON a.Id = e.AccountId
  WHERE t.OriginalTransactionId IS NULL
    AND NOT EXISTS (SELECT 1 FROM ledger_transactions r WHERE r.OriginalTransactionId = t.Id)
  GROUP BY t.Id, e.CurrencyCode
  HAVING IncomeMinorUnits > 0 OR OutcomeMinorUnits > 0;
  ```
  Transactions are single-currency (a Slice-1 dollar-support invariant), so `GROUP BY t.Id, e.CurrencyCode`
  yields one row per transaction. Verify the exact enum string values stored for `Type`/`Kind` and
  `Direction` against `vw_ledger_monthly_expenses.sql` (they are `'Expense'`, `'Debit'`, …) and against
  `AccountKind.cs` for the liability/bank kinds.
- **Migration** `AddMoneyFlowView` (Ledger context), hand-authored: `Up()` → `ReadViewSqlHelper.Load("vw_ledger_money_flow.sql")`,
  `Down()` → `DROP VIEW vw_ledger_money_flow`.

### 1b. Report `GET /v1/reports/money-flow?month=YYYY-MM`
- `src/Reporting/PersonalFinance.Reporting/Sql/money_flow.sql`:
  ```sql
  SELECT TransactionId, PostedOnUtc, Description, AccountName, IncomeMinorUnits, OutcomeMinorUnits, CurrencyCode
  FROM vw_ledger_money_flow
  WHERE Month = $month
  ORDER BY PostedOnUtc DESC, TransactionId DESC;
  ```
  `month` is **required** here (the table is always monthly). A missing or malformed month → 400/422,
  handled the way `GetMonthlyExpenses` validates its param.
- `src/Reporting/PersonalFinance.Reporting/Reports/MoneyFlowQuery.cs` (clone the `MonthlyExpensesQuery` structure). Row:
  `MoneyFlowRow(Guid TransactionId, DateOnly Date, string Description, string AccountName, string Kind /* "Income" | "Outcome" */, long AmountMinorUnits, string CurrencyCode)`.
  Map `Kind`/`AmountMinorUnits` from whichever of the two sums is > 0. Keep this mapping in C#, not SQL, so it's unit-testable.
  Register it in `ReportingModule.cs`.
- Host: `Endpoints/Reporting/GetMoneyFlow.cs` + a route constant in `ApiRoutes.cs` + a mapping in the Reporting group of `EndpointExtensions.cs`.
  `TransactionId` is in the payload **on purpose**: Slice 3's Undo needs it.

## Step 2: API tests

`tests/PersonalFinance.Reporting.Tests` (real migrations; use `ReportingIntegrationFixture` helpers to seed via the real commands):
- **Reconciliation:** seed debit expenses (ARS + USD), a split debit expense and a paid subscription in one month.
  Σ outcome per currency from money-flow == Σ `monthly-expenses` per currency for the same month.
- **Split = my share:** a $10,000 split 50/50 → one Outcome row of $5,000.
- **Income row:** Slice-1 income → Kind `Income`, AccountName = the bank's name, its description.
- **Reversal hiding:** an expense + its reversal (same month) → neither appears; an income + its reversal → neither appears.
- **Exclusions:** a card purchase accrual and a statement payment in the month → no rows for them.
- **Description fallback:** a transaction posted with `Description = null` against category "Groceries" → Description "Groceries".
- **Month filter + ordering:** rows from other months excluded; newest first.
- Api (WAF): `GET /v1/reports/money-flow` without `month` → 4xx; with a month → 200 and the expected shape.

## Step 3: client production

- Type `features/ledger/types/money-flow-row.ts`:
  `{ transactionId: string; date: string; description: string; accountName: string; kind: 'Income' | 'Outcome'; amountMinorUnits: Money; currencyCode: CurrencyCode }`.
- `ReportsService.moneyFlow(month)` → `GET reports/money-flow?month=`. (It's a Reporting endpoint, so it lives in `ReportsService`
  even though the page lives in the ledger feature. The dashboard already follows this split.)
- **Money Flow page** (container) `features/ledger/pages/money-flow-page/money-flow-page.{ts,html}`, route `money-flow` in `ledger.routes.ts`:
  - The month `<input type="month">` defaults to the current month (the same idiom as `dashboard-page.ts:37`) and refetches on change.
  - It owns `loadStatus` (loading/error/empty/ready, the SYSTEM.md states checklist). Empty copy: "No money moved this month."
  - `footerTotals`: computed per currency → `{ currencyCode, income: Money, outcome: Money }[]`, only for currencies present.
  - A "Record income" link → `/ledger/incomes/new`.
- **Money Flow table** (presentational) `money-flow-table.{ts,html}`: `input<MoneyFlowRow[]>()` rows + `input()` footer totals.
  Copy `transactions-table`'s `<thead>`, right-aligned tabular-nums money columns and staggered row animation.
  - Income cell: `kind === 'Income'` → `+` + `formatMoney(amount, currencyCode)` in `text-ledger`; otherwise `—` in the neutral muted text token.
  - Outcome cell: `kind === 'Outcome'` → `−` (U+2212 minus, not a hyphen) + `formatMoney(...)` in `text-negative`; otherwise `—` muted.
  - Both use `[font-variant-numeric:tabular-nums_lining-nums]`. The sign is also conveyed in text (`+`/`−`), not only by color (a11y).
  - `<tfoot>`: one row per currency, the Income total in green, the Outcome total in red, and no net.
- Nav: add `{ label: 'Money Flow', path: '/ledger/money-flow' }` to `navItems` in `src/app/app.ts`.
- Record income page (Slice 1): on success, navigate to `/ledger/money-flow` instead of the Dashboard.
- **`app/client/docs/SYSTEM.md`**: add a "Signed amounts" rule. `--ledger` (green) and `--negative` (red) may be used for signed
  money figures in ledger-style tables: always paired with an explicit `+`/`−` sign, the empty counterpart cell is a neutral `—`,
  and never used as decoration. This extends, and doesn't contradict, the existing "semantic only" rule (lines ~50, 65–74).

## Step 4: client specs

- `reports-service.spec.ts`: `moneyFlow` GETs with the `month` param.
- `money-flow-table.spec.ts`: an income row renders `+`, the green class and `—` in the outcome cell; an outcome row renders `−` (U+2212),
  the red class and `—` in the income cell; a USD row formats as USD (assert against the real `formatMoney` output, per the dollar-support gotcha
  that ARS and USD both render `$`); the footer renders one line per currency.
- `money-flow-page.spec.ts`: the default month = current; a month change refetches; the empty state; the error state; footer totals are computed
  per currency and never summed across currencies.
- `record-income-page.spec.ts`: success now navigates to `/ledger/money-flow`.
- Lint, prod build, test, and report the new count.

## Step 5: doc-sync

- API `CLAUDE.md` Phase 46 + `TASK.md` + `DESIGN.md`: the view semantics (my-share outcomes, hidden reversed pairs, the cross-month caveat).
- Client `CLAUDE.md` + `TASK.md` + `DESIGN.md`: the Money Flow page and table, the nav entry, and the SYSTEM.md rule.

## Verification (end to end, before Slice 3)
1. In one month: an income, a plain debit expense, a split debit expense, a USD expense, a card purchase and a statement payment.
2. Money Flow shows the income (green), the three debit expenses (red; the split at my share), **no** card rows; the footer shows ARS and USD separately.
3. The outcome total per currency equals the Dashboard's Out of pocket figure for that month.
4. Reverse an expense from the Transactions page → it disappears from Money Flow.
5. A pre-Slice-1 expense shows its category name as the description.
