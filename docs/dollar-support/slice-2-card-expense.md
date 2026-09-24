# Slice 2 — Card expense in USD (installments, statements, accrual)

> Read `00-overview.md` and `slice-1-foundation-debit-expense.md` first. Slice 1 built the
> currency spine (`Currency.Usd`, `Currency.FromCode`, the two-column `Money` mapping,
> `formatMoney`, Dashboard partitioning). This slice reuses all of it and extends currency to
> the **Financing** money tables so a **credit-card** purchase can be recorded in USD, with the
> **exact same installment division** it has in ARS.

## Intent

- Record a card purchase in ARS **or** USD from Load Expense (card mode).
- Currency propagates from the `PaymentPlan` down to every `Installment`, the accrued
  `MonthlyStatement`, and the posted ledger entries — all one currency.
- **Installment division is byte-for-byte identical regardless of currency** (this is the
  proof the slice exists to give). USD is just a tag; the split math is untouched.
- Statements, recent-purchases, and card-debt-by-cycle display each currency separated.

## API changes

### 1. Financing money tables get `CurrencyCode`
Each of these has a `Money` field mapped by a converter that invents `Currency.Reference`
today. Apply the same **two-column mapping** established in Slice 1 (keep `<Name>MinorUnits`,
add a sibling `CurrencyCode`), and one additive migration (`DEFAULT 'ARS'`) in the **Financing**
context covering all of them:

- `Domain/PaymentPlan.cs` — `Total` (config `PaymentPlanConfiguration.cs`). This is the
  **source of truth**: currency chosen at creation lives here and is copied everywhere else.
- `Domain/Installment.cs` — `Amount` (`InstallmentConfiguration.cs`).
- `Domain/MonthlyStatement.cs` — `AmountDue` (`MonthlyStatementConfiguration.cs`).
- `Domain/CreditCard.cs` — `CarriedCreditBalance` (`CreditCardConfiguration.cs`).

### 2. Thread currency through plan creation
- `CreatePaymentPlanHandler.cs` (+ `CreatePaymentPlanCommand` DTO + host endpoint): add
  `CurrencyCode`. Build the plan `Total` with `Currency.FromCode(command.CurrencyCode)`.
- The installment-division logic (however it splits `Total` across `installmentCount`) is
  **unchanged** — it operates on `MinorUnits`; the currency simply rides along. Each generated
  `Installment.Amount` inherits the plan currency. **Do not fork the math per currency.**
- The accrual path (`OnPaymentPlanCreated` / statement accrual, `MonthlyStatement.AmountDue`)
  builds its `Money` from the plan/installment currency, not `Currency.Reference`.
- The card-liability postings (ledger entries) use the plan currency (single-currency
  transaction). A card-liability account is now poly-currency exactly like a bank account.

### 3. Card read-views + reporting partition by currency
- `Financing/.../ReadViews/vw_card_future_schedule.sql` — replace `'ARS' AS CurrencyCode` with
  the real column; add to `GROUP BY` if it aggregates.
- `Ledger/.../ReadViews/vw_card_liability_accrued.sql` — same.
- `Reporting/Sql/card_due_by_month.sql` (`CardDueByMonthQuery.cs`) — this `SUM`s over a
  `UNION ALL` of the accrued + future views. Add `CurrencyCode` to the `SELECT`/`GROUP BY` on
  **both** sides and the outer aggregate, so a card's debt-by-cycle is reported **per currency**.
- `GetCardLiabilityHandler.cs` — return liability **per currency**.

## Client changes

### 1. Card-mode currency selector
- `features/financing/pages/load-expense-page/load-expense-page.{ts,html}` — the `currency`
  control added in Slice 1 now feeds the **card** and **creditor** branches too: include
  `currencyCode` in the `CreatePaymentPlan` payload. Installment-count/split UI is unchanged.

### 2. Separated display across Financing views
Thread each row's `currencyCode` into `formatMoney(value, code)` (migrate these off the
`formatArs` shim):
- `statements-page/statements-table.{ts,html}` — statement amount due, grouped/labeled per
  currency (a card can now have an ARS statement and a USD statement).
- `statement-page/statement-page.html` + `installments-table.{ts,html}`.
- `recent-purchases-page/recent-purchases-table.{ts,html}` (`pendingLabel` too).
- Dashboard **card-debt-by-cycle** block (`dashboard-page.{ts,html}`) — `cycleByCard` must
  group by `currencyCode` as well as card, and render each currency separated. This is the
  second Dashboard reducer (after out-of-pocket in Slice 1) that must not blend currencies.

## Test plan

### API (xUnit)
- `CreatePaymentPlanHandler` with `CurrencyCode = "USD"`: plan `Total`, every generated
  `Installment.Amount`, and the accrued `MonthlyStatement.AmountDue` are all `Currency.Usd`.
- **Division-parity test**: same amount + same `installmentCount` produces **identical**
  `MinorUnits` per installment for ARS and USD (only the `Currency` differs). This is the
  headline assertion.
- Card liability query returns ARS and USD liabilities separately for a card that has both.
- `card_due_by_month` reporting: a card with an ARS purchase and a USD purchase yields two
  per-currency cycle rows, never one blended row.

### Client (Karma)
- `load-expense-page.spec.ts` — card mode sends `currencyCode`.
- `statements-table.spec.ts` / `recent-purchases-table.spec.ts` — USD rows render as USD.
- `dashboard-page.spec.ts` — card-debt-by-cycle with mixed currencies renders separated
  totals (regression against blending).

## Verification (end to end, before Slice 3)
1. Load a **USD** card purchase in N installments; load an **ARS** one with the same amount/N.
2. Confirm the per-installment amounts are numerically identical; only the currency label
   differs.
3. Confirm statements, recent-purchases, and Dashboard card-debt-by-cycle show ARS and USD
   **separated**, never blended.
4. Existing ARS card data unchanged.
5. Suites green; lint + prod build clean.

## Doc-sync checklist
- `app/api/.claude/CLAUDE.md` — phase bump; Financing money tables carry `CurrencyCode`;
  installment division currency-agnostic; card views partition by currency.
- `app/client/.claude/CLAUDE.md` — card-mode currency selector; Financing views + card-debt
  Dashboard block partition by currency.
- `app/api/docs/DESIGN.md` — note the single-currency-per-plan invariant.
- Record the Financing migration id.
