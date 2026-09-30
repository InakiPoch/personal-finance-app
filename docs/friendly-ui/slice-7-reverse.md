# Slice 7 — Reverse a Transaction: say what it is and what undoing it does

> **DONE 2026-09-30** (API Phase 56 / client Phase 53). Open: From→to direction (debited→credited shipped; mock differs).

> Read `00-overview.md` first. Slice 1 already renamed the nav item/page title to "Reverse a Transaction";
> slice 4 renamed subscription accounts. Heavy API slice. API Phase 56 / client Phase 53.

## Goal

Today every row reads "Manual entry", "Installment accrual" or "Shared expense / split", with an amount and a
date — the user can't tell what a row is, and the confirm page shows a **raw GUID** plus a paragraph about
"the API posts a compensating entry". The user wants (D10):

- each row: **real description**, **type badge**, **from → to** accounts, amount, date;
- expandable **"If you reverse this"** panel explaining the consequences in plain words;
- the confirm page shows the same description + impact instead of the GUID.

```
Sep 28  [Card installment]  Notebook — installment 3 of 12 on Visa        ARS 40.000  ▸
        Visa purchases → What you owe on Visa
        ┌ If you reverse this ───────────────────────────────────────────────┐
        │ • Installment 3 of 12 of "Notebook" is cancelled.                    │
        │ • Your Visa bill goes down by ARS 40.000.                            │
        │ • Juan no longer owes you ARS 20.000 for this installment.           │
        └──────────────────────────────────────────────────── [Reverse…] ────┘
Sep 27  [Income]  Salary September                                      ARS 900.000  ▸
        Salary → Galicia checking
        If you reverse this: ARS 900.000 is removed from Galicia checking.
Sep 20  [Undo entry]  Undid: Groceries at Coto                          ARS 12.000   (locked)
```

## Current state (verified 2026-09-29)

Prefixes: `L/` = `app/api/src/Modules/Ledger/PersonalFinance.Ledger`, `R/` = `app/api/src/Reporting/PersonalFinance.Reporting`.

- Feed: `GET /v1/ledger/transactions` (`H/ApiRoutes.cs:8`) → `L/Application/Queries/GetTransactions/GetTransactionsHandler.cs`:
  loads all transactions + entries in memory, row = `(Id, PostedOnUtc, describe(t), Σ debit minor units, first
  entry currency, IsReversal, IsReversed, InstallmentReferenceId, SplitReferenceId)`. `describe()` `:51-65` is the
  generic text. Optional `AccountId`, `FromUtc`, `ToUtc` filters.
- `L/Domain/Transaction.cs:8-16` — `PostedOnUtc`, `OriginalTransactionId`, `Description?` (max 200; only set by
  RecordIncome and RecordDebitExpense), `SplitReference?`, `InstallmentReference?` (installment id),
  `SubscriptionReference?` (subscription template id — `SubscriptionChargeCalculator.cs:16`), `Entries`
  (AccountId, Direction, Amount). Reversals copy references and description (`:38-50`).
- `L/Domain/Account.cs` — `Name`, `Type`, `Kind` (Bank, Cash, CardLiability, CardPurchases, CardCredit, Receivable,
  Expense, Income, CreditorPayable… — verify the enum). Receivable accounts are per party.
- Reverse: `POST /v1/ledger/transactions/{id}/reversal` → `ReverseTransactionHandler.cs`: mirror storno; then
  `ReversalCalculator.Decide(hasInstallmentRef, InstallmentStatusResponse?, hasSplitRef)`
  (`L/Application/Commands/ReverseTransaction/ReversalCalculator.cs`): if the installment was **already paid**,
  post a compensating `Dr CardCredit / Cr CardLiability` (card credit on the next bill); mark the installment
  reversed in Financing; if split, `parties.CorrectExpenseSplitAsync` with the reversed receivable legs.
  Guards: `TransactionAlreadyReversed`, `CannotReverseAReversal`, `OriginalTransactionNotFound`.
- Existing cross-module read pattern to copy: `L/Infrastructure/Persistence/ReadViews/vw_ledger_money_flow.sql`
  (joins `ledger_transactions`, `ledger_entries`, `ledger_accounts`; `COALESCE(t.Description, expense account
  name, 'Income')`) queried by `R/Sql/money_flow.sql`. Tables: `financing_installments`,
  `financing_payment_plans`, `financing_credit_cards`, `financing_creditors` (verify), `subscriptions_templates`,
  `parties_expense_splits`, `parties_expense_split_participants`, `parties_parties`.
- Client: `C/features/ledger/pages/transactions-page/` (+ `transactions-table.{ts,html}`: columns Posted,
  Description, Amount, action; locked rows "Reversal entry"/"Already reversed" `:35-37`), types
  `C/features/ledger/types/transaction-row.ts`, service `ledger-service.ts:53`. Confirm page
  `C/features/ledger/pages/reverse-movement-page/` (route `transactions/:id/reverse`) shows `<code>{{ id }}</code>`
  and the jargon paragraph.

## Design decisions

- **Built at read time, in Reporting** (no backfill, old rows get good text too). Reporting is the module that
  may read across modules via views; Ledger alone can't see plan descriptions, card names, subscription names or
  party names.
- **Classification and impact text live in C#** (a pure `TransactionExplainer` in Reporting), not SQL: SQL
  returns legs + labels; C# groups legs per transaction, picks the kind, writes description / from→to / impact
  bullets. Pure ⇒ unit-testable without a DB.
- The impact must match what `ReverseTransactionHandler` really does. So the explainer's installment branch
  mirrors `ReversalCalculator.Decide`: needs "is the installment paid?" (from the Financing view), and the
  split branch lists each party's receivable leg (from the transaction's own legs — exactly what
  `CorrectExpenseSplitAsync` receives).
- Replace, don't duplicate: the client switches to the new endpoint; delete `GetTransactions` query/endpoint if
  nothing else uses it (grep `ledger/transactions` GET and `GetTransactionsQuery` first — the reversal POST route
  stays in Ledger).

### Kinds (badge) and texts

Implementer: confirm each posting shape by reading the posting handler before coding its branch.

| Kind (badge) | Detect | Description | Impact bullets |
|---|---|---|---|
| Undo entry | `OriginalTransactionId != null` | "Undid: <original description>" | locked, none |
| Card installment | `InstallmentReference` + card plan | "<plan> — installment n of N on <card>" | "Installment n of N of "<plan>" is cancelled." · unpaid: "Your <card> bill goes down by X." · paid: "X comes back as a credit on your next <card> bill." · + split lines |
| Creditor installment party share | `InstallmentReference` + creditor plan | per posting shape | per `ReversalCalculator` path |
| Card bill payment | Dr CardLiability / Cr Bank|Cash, no refs | "Paid <card> bill" | "Your <card> bill goes back up by X." · "X returns to <bank>." (**verify** whether reversing a statement payment un-marks the statement/installments — if it doesn't, say "The bill stays marked as paid." honestly, and flag it to the user as a follow-up) |
| Subscription | `SubscriptionReference` | "<subscription name>" | "X returns to <funding account>." · "This month's charge will be charged again on the next run." (**verify** the scheduler behaviour) |
| Shared expense | `SplitReference` | `Description` ?? expense account name | "X returns to <source>." · per party: "<party> no longer owes you Y." |
| Income | Income account credited | `Description` ?? income account name | "X is removed from <account>." |
| Expense | Expense debit + Bank/Cash credit | `Description` ?? category name | "X returns to <account>." |
| Party payment | Dr Bank / Cr Receivable | "<party> paid you" | "<party> owes you X again." · "X is removed from <bank>." |
| Other | fallback | "Manual entry" | "Every amount in this movement is undone." |

From → to = debited accounts' names → credited accounts' names (receivable accounts shown as "<party> (owes you)").
Glossary applies: never "liability", "accrual", "storno", "reversal" in texts.

## API changes

1. Views (one per owning module, follow existing ReadViews registration/migration pattern):
   - Ledger `vw_ledger_transaction_legs`: per entry — TransactionId, PostedOnUtc, Description, OriginalTransactionId,
     IsReversed (EXISTS reversal), InstallmentReferenceId, SplitReferenceId, SubscriptionReferenceId, AccountId,
     AccountName, AccountType, AccountKind, Direction, AmountMinorUnits, CurrencyCode.
   - Financing `vw_installment_labels`: InstallmentId, Sequence, InstallmentCount, PlanDescription, CardName (nullable),
     CreditorName (nullable), IsPaid, IsReversed.
   - Subscriptions `vw_subscription_names`: TemplateId, Name (all templates, not just active).
   - Parties: receivable account → party name if not already derivable from the account name (verify).
2. `R/Sql/transaction_feed.sql` (legs + labels, optional `$transactionId` filter, newest first).
3. `R/…/TransactionExplainer.cs` (pure) + query handler → `TransactionFeedRow(Id, PostedOnUtc, Kind, Description,
   FromAccounts, ToAccounts, AmountMinorUnits, CurrencyCode, IsUndoEntry, IsUndone, ImpactLines[])`.
4. Host: `GET /v1/reports/transactions` and `GET /v1/reports/transactions/{id}` (next to the other report routes in
   `H/ApiRoutes.cs`). Remove the old Ledger feed endpoint/query if unused.

## Client changes

- `ledger-service.ts` (or `reports-service.ts`, wherever report calls live) → new feed calls; update `TransactionRow`.
- Transactions table: columns Date · Type badge · What (description + from→to sub-line) · Amount · expand chevron;
  expanded row shows "If you reverse this" bullets and the **Reverse…** button (moves from the action column into the
  panel so the user reads the impact first). Locked rows: "Undo entry" / "Already undone".
- Page subtitle: "Pick a movement to see what undoing it would change."
- Confirm page: fetch `GET …/transactions/{id}`, show description, badge, amount, impact bullets; title "Reverse a
  Transaction"; drop the GUID and the jargon paragraph (replace with "The original stays in your history; an undo entry
  is added next to it."); button "Undo this transaction".
- Error sentences for `TransactionAlreadyReversed` / `CannotReverseAReversal` in glossary terms.

## Test plan

API:
- `TransactionExplainer` unit tests — one per kind in the table, including: paid vs unpaid card installment (text
  matches `ReversalCalculator.Decide` outcome), split with two parties, USD amount, undo entry locked, already-undone.
- Reporting integration (existing `ReportingIntegrationFixture`): feed returns descriptions with real plan/card/
  subscription/party names; `{id}` endpoint returns one row; 404 for unknown id.
- Old Ledger feed tests removed/moved if the query is deleted.

Client:
- Table spec: badge + description + from→to render; expand shows impact lines; Reverse button inside panel; locked rows.
- Confirm page spec: renders description and impact, no GUID; confirm posts reversal; error sentences.
- Service spec: new routes.

## Steps

- [x] 1. API prod — views + SQL + explainer + endpoints (+ old feed removal). Build clean.
- [x] 2. API tests — as above. All green.
- [x] 3. Client prod — feed types, table, confirm page. Lint + prod build clean.
- [x] 4. Client specs — as above. Green.
- [x] 5. Doc-sync — API `TASK.md` Phase 56 / client Phase 53; API `CLAUDE.md` (feed moved to Reporting, explainer is
      the single source of reversal wording); client `CLAUDE.md` if it documents the ledger feed.
