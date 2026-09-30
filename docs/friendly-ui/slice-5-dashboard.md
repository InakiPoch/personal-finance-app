# Slice 5 — Dashboard: clear labels, actions on top, "Due this month"

> Read `00-overview.md` and `slice-4-subscription-names.md` first (slice 4 makes the spending list
> show "Netflix Subscription"). API Phase 54 / client Phase 51.

## Goal

1. **Relabel** the flow tabs and the expense list so each number says what it counts (D4: numbers
   unchanged).
2. **Quick actions move to the top** (D5).
3. New **"Due this month"** card: what you have to pay this month on **cards + creditors**, ARS and USD
   separately, overdue included (D6).
4. Glossary on every dashboard string touched (D1): "Card debt by cycle" → "Card bills by month",
   "Accrued" → "Charged to the card", "Future" → "Upcoming", "Pay a statement" → "Pay a card bill".

Final layout (D5):

```
This month
[+ Record an expense] [+ Record income] [Recent Credit Card Purchases ›]
┌ Due this month ───────────────────────┐
│ ARS 540.000            USD 32         │
│ Cards 480.000 · Creditors 60.000      │   ← per currency
│ ▸ Visa 300.000 · Master 180.000 · Juan 60.000   (expandable breakdown)
└───────────────────────────────────────┘
[Spent from bank & cash | Money received]   Month [2026-09]
  ARS 210.000
  "Your share of what you paid from bank accounts and cash. Card purchases count when you pay the card bill."
  Where your money went
   Groceries ............ 80.000
   Netflix Subscription .. 12.000
Card bills by month
Active subscriptions
```

## Current state (verified 2026-09-29)

`C/features/reports/pages/dashboard-page/dashboard-page.{html,ts}`:

- Header `:2-5` — eyebrow "Personal ledger" (remove per glossary) + h1 "This month".
- Flow section `:6-104` — tab buttons "Out of pocket" / "Income" (`flowSide()` signal, default `'out'`,
  ts:90), month `<input type="month">` (`:35-46`).
  - Out sub-label `:61-64`: *"Cash and debit only. Card purchases and money owed to you are left out — this is what actually left your pocket."*
  - Expense list `:65-84` — untitled, Out tab only; label + amount + bar. Empty: "No movements recorded this month."
  - Income sub-label `:99-101`: *"Real money that arrived in a bank or cash account this month."*
- "Card debt by cycle" `:105-204` — link "Pay a statement", legend "Accrued — hits the bill now" /
  "Future — committed, not yet accrued", per-card rows "Accrued X / Future Y", expandable purchases.
  **Bug:** expanded purchase amounts always formatted as ARS (`:191`) even on USD cards — fix here.
- "Active subscriptions" `:205-252`.
- Quick actions `:253-268` — `<nav aria-label="Quick actions">`: "+ Record an expense", "Recent purchases"
  (renamed by slice 1 to "Recent Credit Card Purchases"), "+ Record income".
- Services injected ts:203-205: `ReportsService`, `FinancingService`, `SubscriptionsService`.

What the numbers mean (unchanged):
- Spent = `GET …/monthly-expenses` → `vw_ledger_monthly_expenses`: debits − credits on Expense accounts
  **excluding** Receivable and CardPurchases kinds → your share, bank/cash only, by calendar UTC month.
- Received = `GET …/monthly-incomes` → `vw_ledger_monthly_incomes`.

Due-this-month data (nothing sums it today):
- Card installments: `financing_installments` (+ `financing_payment_plans.CardId`). An installment is due in
  `DueCycle = Cycle + 1 month` (`F/Domain/BillingCycle.cs`). **Card installments get `PaidOnUtc` stamped when
  paid**, both by per-installment pay and by `PayStatementHandler.cs:70` — so "unpaid" = `PaidOnUtc IS NULL`.
- Creditor installments: `F/Application/Queries/GetCreditorPayables/GetCreditorPayablesHandler.cs:35-55`
  computes `DueNow` = Σ `(Amount − paid payments)` of unpaid installments with `DueCycle ≤ currentDueCycle`,
  where `currentDueCycle = BillingCycleCalculator.ResolveCycle(today, PaymentPlan.CreditorCutoffDay).DueCycle`
  (cutoff 26 — so after the 26th, next month's cuotas already count as "due now"). That rule stays on the
  Owed to Creditors page only; the dashboard uses the current calendar month instead (see Post-implementation notes).
- **Bug:** that handler sums `Amount.MinorUnits` across currencies (`:49-55`) — ARS and USD mixed in one number.

## Design decisions

- **One Financing query computes both parts.** Financing owns card installments, creditor installments,
  creditor payments and the `BillingCycle` rules, so the total is computed where the rules live — no SQL
  duplicate of the creditor "due now" rule in Reporting. The creditor due-now predicate (cutoff 26) is
  extracted into an internal helper (`CreditorDueNowHelper`) used by `GetCreditorPayablesHandler` only; the
  dashboard deliberately uses the current calendar month (overdue included), so the two can differ around/after
  the 26th.
- Card rule: unpaid (`PaidOnUtc == null`), non-reversed card installments with
  `DueCycle ≤ (today.Year, today.Month)`. Overdue ones included (older due months).
- **Fix the currency mixing in creditor payables** at the same time: the new helper works per currency, and
  `CreditorPayableRow` gains `CurrencyCode`, emitting **one row per (creditor, currency)**. The Owed to
  Creditors page then shows a creditor twice if they lent in both currencies — honest, and rare. (Alternative
  considered: per-currency lists inside one row — more DTO churn for the same info.)

## API changes

1. Contracts `Financing.Contracts/Queries/GetDueThisMonthQuery.cs`:
   ```csharp
   public sealed record DueThisMonthRow(string Kind /* "card" | "creditor" */, Guid SourceId, string SourceName,
                                        string CurrencyCode, long AmountMinorUnits);
   public sealed record DueThisMonthResponse(IReadOnlyList<DueThisMonthRow> Rows);
   public sealed record GetDueThisMonthQuery() : IQuery<DueThisMonthResponse>;
   ```
   Rows only where `AmountMinorUnits > 0`. The client sums per currency and per kind.
2. Handler `F/Application/Queries/GetDueThisMonth/GetDueThisMonthHandler.cs` (ctor `FinancingDbContext, TimeProvider`).
3. Extract `CreditorDueNow` (internal static) from `GetCreditorPayablesHandler`; add `CurrencyCode` to
   `CreditorPayableRow` and group by `(CreditorId, Currency)`. Per-account breakdown also per currency.
4. Host: `GET /v1/financing/due-this-month` (`H/ApiRoutes.cs` Financing group, `H/EndpointExtensions.cs`),
   DTO + mapping following the existing creditor-payables endpoint.

## Client changes

1. `financing-service.ts` → `dueThisMonth()`; type `DueThisMonthRow` in `C/features/financing/types/`.
2. `creditor-payable-row.ts` + `creditor-payables-table.html` — add `currencyCode`, format amounts with it.
3. Dashboard:
   - Remove the "Personal ledger" eyebrow.
   - Move the quick-actions `<nav>` right under the header; style as three prominent buttons/chips
     (primary: "+ Record an expense"; secondary: "+ Record income"; link: "Recent Credit Card Purchases ›").
   - New "Due this month" card: one big number per currency; sub-line "Cards X · Creditors Y" per currency;
     expandable breakdown by source name. Empty state: "Nothing to pay this month." Link "Pay a card bill"
     → `/financing/statements`, "Pay a creditor" → `/financing/creditor-payables`.
   - Tabs: "Out of pocket" → **"Spent from bank & cash"**; "Income" → **"Money received"**;
     `aria-label` → "Show money spent or received".
   - Sub-labels: spent → *"Your share of what you paid from bank accounts and cash this month. Card purchases
     count when you pay the card bill; money others owe you isn't included."*; received → *"Money that came
     into your bank accounts or cash this month."*
   - Expense list gets a visible heading **"Where your money went"**; empty → "Nothing spent from bank or cash
     this month."
   - Card section: "Card debt by cycle" → **"Card bills by month"**; legend "Charged to the card — on your next
     bill" / "Upcoming — installments not charged yet"; row text "Charged X / Upcoming Y"; link "Pay a card bill".
   - Fix `:191` to format with the row's currency.

## Test plan

API (Financing tests):
- Due this month — card: unpaid installment due this month counted; due last month (overdue) counted; due next
  month excluded; paid excluded; reversed excluded; ARS and USD in separate rows.
- Due this month — creditor: partial payment → remaining counted; due by the current calendar month
  (overdue included), NOT the cutoff-26 rule (fake `TimeProvider`).
- Creditor payables: a creditor with ARS + USD installments → two rows, amounts not mixed.
- Host: route returns 200 with the DTO shape (follow existing endpoint tests).

Client:
- Dashboard spec: quick actions render before the Due card; Due card sums per currency and splits cards vs
  creditors; empty state; new tab labels and list heading; card section labels; USD purchase formatted as USD.
- Creditor payables table spec: currency shown per row.
- Financing service spec: `dueThisMonth()` hits `GET financing/due-this-month`.

## Steps

- [x] 1. API prod — query + handler + helper extraction + payables currency + endpoint. Build clean.
- [x] 2. API tests — as above. All green.
- [x] 3. Client prod — service/types, payables currency, dashboard rework. Lint + prod build clean.
- [x] 4. Client specs — as above. Green.
- [x] 5. Doc-sync — API `TASK.md` Phase 54 / client Phase 51; API `CLAUDE.md` (new endpoint, creditor payables
      now per currency); client `CLAUDE.md` (dashboard section order).

## Post-implementation notes

- **Hotfix (user correction):** the original plan shared the creditor cutoff-26 rule with the dashboard. "Due this
  month" means what must be paid by the current calendar month (overdue included), so the creditor part of
  `GetDueThisMonthHandler` uses the calendar month and diverges from Owed to Creditors "Due now" around/after the 26th.
  `CreditorDueNowHelper` serves `GetCreditorPayables` only.
- **Reverted attempt:** a 2x2 viewport grid layout for the dashboard was tried and reverted at the user's request;
  the single column stays.
- Shipped: Financing 212, Api 64 (`DueThisMonthTests`, OpenAPI-only), Architecture 15, client specs 396; lint + prod build clean; no migration.

## Follow-up: month-driven dashboard (API Phase 54.1 / client Phase 51.1)

The month picker moved to the right end of the quick-actions row and now drives every card; h1 is "This month" or "<Month> <Year>". No migration.

API
- `GET /v1/financing/due-this-month?month=yyyy-MM` (optional). Current/omitted month = unchanged rule. Any other month = "Rule X": installments due on or before M (cards via `BillingCycle.DueCycle`, creditors via calendar cycle ordinal) excluding amounts paid before 00:00 UTC on the 1st of M. A past month still counts installments paid during/after it; a future month excludes what is already paid today. Creditor payments are filtered in memory (SQLite cannot compare `DateTimeOffset`).
- New `GET /v1/subscriptions/by-month?month=yyyy-MM` (month required; malformed -> `MonthQueryHelper.Parse` -> `FormatException` -> 400 via `GlobalExceptionHandler`). Row = active-subscription fields + `dueDate` (`RecurrenceRule.CurrentOccurrence`, anchor day clamped) + status paid|overdue|upcoming: future = upcoming, current = `subscriptions/active` rule, past = Ledger lookup.
- New Ledger query `FindPaidSubscriptionIdsQuery(ids, month)`: paid iff a non-reversal transaction with a `SubscriptionReference` was posted in that UTC month and has no storno. Subscriptions calls it through `IQueryBus`, deliberately NOT `ILedgerApi` (adding a method there would break five test fakes).
- Limitations: `SubscriptionTemplate` stores no creation/cancel date, so active templates show in every month (even before they existed) and cancelled ones never show in past months; a late payment counts as paid in the month it was posted. Fix = a migration adding dates (not done).

Client
- Due card copy: current month "How much do I owe in total for this month?" / "Nothing to pay this month."; other months "How much would I owe that month, based only on installments?" / "Nothing to pay.".
- Card bills are filtered client-side to the selected month's cycle. Deviation: already-charged rows carry no cycle in the view, so they show only for the current month; a past month shows Charged 0 and only matching Upcoming rows. Per-card expanded purchases are NOT month-filtered.
- Subscriptions come from by-month (overdue first; future month shows "Future payments — not charged yet"). Categories sort currency first, then amount descending. Late responses for a no-longer-selected month are dropped.
- Layout: container `max-w-6xl`; header, quick actions and Due card full width; below, a 2-column grid at lg+ (left money flow, right Card bills over Subscriptions, Subscriptions fills the column height); single column below lg. (Supersedes the single-column note above; the 2x2 viewport grid stays reverted.)

Tests: Financing 219, Subscriptions 67, Ledger 70, Api 69, Architecture 15; client specs 418; lint + prod build clean.
