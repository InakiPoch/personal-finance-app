# Slice 1: Record an income + Dashboard `Out of pocket | Income` toggle (the tracer bullet)

> Read `00-overview.md` first. This slice drives **one** thin path through every layer:
> ledger domain → migration → command → endpoint → reporting view/query → client service →
> form page → Dashboard. Once it is green, "money in" exists end to end, and Slices 2–3 build
> on top of it.

## Why this is the tracer

Recording an income is the smallest possible "money in" path. It is one balanced two-leg
transaction, **Dr Bank/Cash / Cr Income**, with no installments, splits or accruals. Seeing its
total on the Dashboard proves the write path and the read path together. The slice also carries the
`Transaction.Description` fix. It has to land here, because the income's description is its only
label and Slice 2's table depends on it.

## Intent

- Persist a transaction description (fixes the verified gap in `00-overview.md`).
- Record an income into a Bank/Cash account in ARS or USD, back-dated or today, never in the future.
- Show monthly income per currency on the Dashboard through a toggle on the existing tile.

## Step 1: API production

### 1a. `Transaction.Description` (Ledger domain + persistence)
- `src/Modules/Ledger/PersonalFinance.Ledger/Domain/Transaction.cs`:
  - Add `public string? Description { get; private set; }`.
  - Add an optional `string? description = null` parameter to `Post(...)` and `build(...)`, and set it in `build`.
  - `Reverse(...)` copies `original.Description` onto the storno (harmless, and it reads well in the
    Transactions page). Keep `Reverse`'s signature otherwise unchanged.
- `Infrastructure/Persistence/Configurations/TransactionConfiguration.cs`: `builder.Property(t => t.Description).HasMaxLength(200);` (nullable).
  Check the max length against `RecordDebitExpenseValidator` / the client form; if the form has no
  limit, 200 is fine.
- **Migration** `AddTransactionDescription` (Ledger context): `AddColumn Description TEXT NULL` on
  `ledger_transactions`. There is no backfill, because historic descriptions were never stored.
  Use the migration command from the "EF Core migrations" section of `app/api/.claude/CLAUDE.md`.
- `Application/Commands/PostTransaction/PostTransactionHandler.cs`: pass `command.Description?.Trim()` into
  `Transaction.Post(...)`. **This single line repairs split expenses (`RegisterSharedExpenseHandler`
  already sends it) and settlements (`SettleCurrentAccountHandler` already sends
  `"Settlement from {party}"`).**
- `Application/Commands/RecordDebitExpense/RecordDebitExpenseHandler.cs`: in the non-split branch, pass
  `command.Description.Trim()` to `Transaction.Post(...)`.
- Subscriptions: **no change.** `SubscriptionChargeCalculator` posts without a description, and the
  per-subscription expense account name is already a meaningful fallback.

### 1b. The "Income" account (lazy, single)
- New `Application/IncomeAccountProvisioning.cs`, mirroring `ExpenseCategoryProvisioning.cs` in the same
  folder:
  - `GetOrCreateAsync(LedgerDbContext, CancellationToken) → Result<Guid>`.
  - Look for the first account with `Type == AccountType.Income && Kind == AccountKind.Income`, and if none
    exists, `Account.Create("Income", AccountType.Income, AccountKind.Income)`, add it and save it.
  - There's **no migration** for this, because enums are stored as strings and `Income` already exists
    (`Ledger.Contracts/AccountType.cs`, `AccountKind.cs`; `Domain/Account.cs:36` accepts the pair).

### 1c. `RecordIncomeCommand`
- `PersonalFinance.Ledger.Contracts/Commands/RecordIncomeCommand.cs`:
  `public sealed record RecordIncomeCommand(long AmountMinorUnits, Guid TargetAccountId, DateOnly ReceivedOn, string Description, string CurrencyCode = "ARS") : ICommand<Guid>;`
  It goes through the command bus only, the same as `RecordDebitExpenseCommand`. Don't add it to `ILedgerApi`,
  since no other module needs it.
- `Application/Commands/RecordIncome/RecordIncomeValidator.cs` (static, cloned from `RecordDebitExpenseValidator`):
  - amount > 0 → `NonPositiveEntryAmount`
  - blank description → new `LedgerErrors.InvalidIncomeDescription`
  - `Guid.Empty` account → `AccountNotFound`
  - currency not ARS/USD → `InvalidCurrencyCode`
- `Application/Commands/RecordIncome/RecordIncomeHandler.cs` (ctor: `LedgerDbContext`, `TransactionWriter`, `TimeProvider`):
  1. Validate.
  2. **Future-date guard:** `ReceivedOn > DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime)` →
     new `LedgerErrors.IncomeDateInFuture`. It lives in the handler because the validator is static and has no
     clock. `TimeProvider` is already registered (`InfrastructureServiceCollectionExtensions.cs:26`), and
     Subscriptions handlers are the precedent for injecting it.
  3. Load the target account. If it's missing → `AccountNotFound`. If `Kind` is not Bank/Cash →
     `SourceAccountNotSpendable` (reuse it; the name is slightly off but the meaning, "not a Bank/Cash
     account", is identical. Rename only if a reviewer insists).
  4. Get or create the Income account.
  5. `Transaction.Post([Dr target, Cr income], ReceivedOn at 00:00 UTC, description: command.Description.Trim())`.
     This uses the same date→instant conversion as `RecordDebitExpenseHandler`.
  6. `writer.PersistAsync(...)` and return the transaction id.
- `Domain/LedgerErrors.cs`: add `InvalidIncomeDescription` and `IncomeDateInFuture` (both validation → 422;
  check how `ErrorHttpStatusHelper` classifies Ledger errors and add explicit entries if it maps by code).

### 1d. Host endpoint `POST /v1/ledger/incomes`
- `src/Bootstrap/PersonalFinance.Api/Endpoints/Ledger/RecordIncome.cs`: clone `RecordDebitExpense.cs`,
  with DTO `RecordIncomeDto(long AmountMinorUnits, Guid TargetAccountId, DateOnly ReceivedOn, string Description, string CurrencyCode = "ARS")`.
  Returns 201 with the id, mirroring the expense endpoint's result shape.
- Register it in `Endpoints/EndpointExtensions.cs` in the Ledger group (next to line ~39, where `expenses` is mapped) and
  add the route constant to `ApiRoutes.cs`.

### 1e. Monthly incomes view + report
- New view `src/Modules/Ledger/PersonalFinance.Ledger/Infrastructure/Persistence/ReadViews/vw_ledger_monthly_incomes.sql`,
  the mirror image of `vw_ledger_monthly_expenses.sql` (Income is credit-positive):
  ```sql
  CREATE VIEW vw_ledger_monthly_incomes AS
  SELECT
      strftime('%Y-%m', t.PostedOnUtc) AS Month,
      SUM(CASE WHEN e.Direction = 'Credit' THEN e.AmountMinorUnits ELSE -e.AmountMinorUnits END) AS AmountMinorUnits,
      e.CurrencyCode AS CurrencyCode
  FROM ledger_entries e
  INNER JOIN ledger_accounts a     ON a.Id = e.AccountId
  INNER JOIN ledger_transactions t ON t.Id = e.TransactionId
  WHERE a.Type = 'Income' AND a.Kind = 'Income'
  GROUP BY strftime('%Y-%m', t.PostedOnUtc), e.CurrencyCode;
  ```
  Reversals net out automatically, because the storno debits Income, exactly like Out of pocket.
- **Migration** `AddMonthlyIncomesView` (Ledger context), hand-authored: `ReadViewSqlHelper.Load(...)` in
  `Up()` and `DROP VIEW vw_ledger_monthly_incomes` in `Down()`. Follow the existing view-migration precedent
  (e.g. `RebuildReceivableAccountMovementsView`). The view file must be an embedded resource like its siblings; check the csproj glob.
- Reporting:
  - `src/Reporting/PersonalFinance.Reporting/Sql/monthly_incomes.sql` (clone `monthly_expenses.sql`, without the Category column):
    `SELECT Month, AmountMinorUnits, CurrencyCode FROM vw_ledger_monthly_incomes WHERE ($month IS NULL OR Month = $month) ORDER BY Month DESC, CurrencyCode;`
  - `Dashboards/MonthlyIncomesQuery.cs`: clone `MonthlyExpensesQuery.cs`, with row `MonthlyIncomeRow(Month, AmountMinorUnits, CurrencyCode)`.
    Register it wherever `MonthlyExpensesQuery` is registered (`ReportingModule.cs`).
  - Host: `Endpoints/Reporting/GetMonthlyIncomes.cs` (clone `GetMonthlyExpenses.cs`) → `GET /v1/reports/monthly-incomes?month=YYYY-MM`,
    mapped in the Reporting group (`EndpointExtensions.cs` ~line 253).

## Step 2: API tests

- `tests/PersonalFinance.Ledger.Tests/RecordIncomeHandlerTests.cs` (in-memory SQLite harness copied from
  `RecordDebitExpenseHandlerTests.cs`; use a fake `TimeProvider`, and check for an existing one in the test projects before writing one):
  - posts Dr Bank / Cr Income, balanced, with the chosen currency (ARS and USD facts);
  - the Income account is created once and reused on the second income;
  - the description is persisted and trimmed;
  - a Credit-card/liability or Expense account as the target → `SourceAccountNotSpendable`;
  - a future `ReceivedOn` → `IncomeDateInFuture`; today → OK; back-dated → `PostedOnUtc` equals that date;
  - validator facts: non-positive amount, blank description, EUR.
- `RecordDebitExpenseHandlerTests`: +1 fact, a non-split expense now persists `Description`.
- Split/settlement path: +1 fact in Parties or Ledger tests, a `PostTransactionCommand` with a Description persists it.
- Reporting (`tests/PersonalFinance.Reporting.Tests`, real migrations): two incomes in one month (ARS + USD)
  → two rows; an income in another month is excluded by the `month` filter; an income + its reversal → the net is 0.
- Api (`tests/PersonalFinance.Api.Tests`, WAF): `POST /v1/ledger/incomes` → 201, then `GET /v1/reports/monthly-incomes?month=`
  returns it; a future date → 422.
- Run each test project directly (see the overview gotcha). Report the new totals against the baseline of 368.

## Step 3: client production

- Types (`features/ledger/types/`): `record-income.ts`:
  `{ amountMinorUnits: Money; targetAccountId: string; receivedOn: string /* YYYY-MM-DD */; description: string; currencyCode: CurrencyCode }`.
  `features/reports/types/monthly-income-row.ts`: `{ month: string; amountMinorUnits: Money; currencyCode: CurrencyCode }`.
- `LedgerService.recordIncome(body)` → `POST ledger/incomes` (next to `recordDebitExpense`, `ledger-service.ts:23`).
- `ReportsService.monthlyIncomes(month)` → `GET reports/monthly-incomes?month=` (clone `monthlyExpenses`).
- **Record income page** `features/ledger/pages/record-income-page/record-income-page.{ts,html}`, route
  `incomes/new` in `ledger.routes.ts` (full URL `/ledger/incomes/new`):
  - Typed reactive form: amount + currency `<select>` beside the `$` glyph (copy the idiom from `load-expense-page`),
    "Received in" `<select>` of bank/cash instruments (copy `bankAndCashInstruments()` from
    `load-expense-page.ts:99`, fed by `InstrumentsService`), date (`type="date"`, default today, `max` = today),
    description (required).
  - Submit → `recordIncome` → navigate to the Dashboard (`/reports`). Slice 2 changes this to Money Flow.
  - The SYSTEM.md states checklist applies (submitting/error states), using the same markup and tokens as Load Expense.
- **Dashboard toggle** (`features/reports/pages/dashboard-page/dashboard-page.{ts,html}`):
  - `protected readonly flowSide = signal<'out' | 'in'>('out');` (the default is out, and nothing is persisted).
  - On init and on month change, fetch `monthlyIncomes(month)` alongside `monthlyExpenses(month)`, with the same month signal.
  - `incomeTotalsByCurrency`: same shape as `monthlyTotalsByCurrency` (ts ~118), including the ARS-0 fallback when there are no rows.
    Extract the shared per-currency summing into one local helper instead of duplicating it.
  - Tile header: a segmented control, two `<button type="button" [attr.aria-pressed]>`s "Out of pocket" and "Income",
    styled like the existing mode selector in `load-expense-page` (sr-only radios + `peer` also works). The hero
    figures render the totals of the selected side. The category list only renders on the `out` side.
  - Quick actions: add "Record income" → `/ledger/incomes/new`.

## Step 4: client specs

- `ledger-service.spec.ts`: `recordIncome` POSTs the body to `${environment.apiBaseUrl}/ledger/incomes` (build the URL the way existing specs do).
- `reports-service.spec.ts`: `monthlyIncomes` GETs with the `month` param.
- `record-income-page.spec.ts`: an invalid form doesn't submit; a valid submit sends the exact payload (USD selected →
  `currencyCode: 'USD'`); a future date is rejected client-side; success navigates.
- `dashboard-page.spec.ts`: the default side shows out-of-pocket totals; clicking "Income" shows income totals per
  currency (ARS + USD separated, never summed); the category list is hidden on the income side; no income rows →
  ARS 0; changing the month refetches both series.
- Lint, prod build, test. Report the new count against the baseline of 303.

## Step 5: doc-sync

- `app/api/.claude/CLAUDE.md`: new Phase 45 entry (description persistence, Income account, `POST /v1/ledger/incomes`,
  `vw_ledger_monthly_incomes`, `GET /v1/reports/monthly-incomes`, the migrations, and the test counts).
- `app/api/.claude/TASK.md`: the Phase 45 checklist, following the existing format.
- `app/api/docs/DESIGN.md` (Spanish) + `docs/PRD.md`: the income decision (Dr Bank / Cr Income, single account, no categories).
- `app/client/.claude/CLAUDE.md` + client `TASK.md` / `docs/DESIGN.md` / `docs/PRD.md`: the Record income page and the Dashboard toggle.

## Verification (end to end, before Slice 2)
1. Run the API and the client. Record an ARS income and a USD income into a bank account, and a back-dated one into last month.
2. On the Dashboard, toggle to Income: the current month shows two separated per-currency totals; last month shows the back-dated one.
3. Record a debit expense with a description, then check `ledger_transactions.Description` in `personalfinance.db`.
4. The Out of pocket side is unchanged.
