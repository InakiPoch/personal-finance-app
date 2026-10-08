# Business rules & API behavior

Current behavior, consolidated from the shipped feature slices. For terms see [`GLOSSARY.md`](GLOSSARY.md); for the *why* see [`adr/`](adr/). All routes are under `/v1`, hosted in `app/api/src/Bootstrap/PersonalFinance.Api` (`Endpoints/ApiRoutes.cs`).

## Endpoints

**Ledger / reports**
- `POST /ledger/expenses` (debit/cash expense, optional split, `currencyCode`) · `GET /expense-categories`
- `POST /ledger/incomes` · `POST /ledger/transactions/{id}/reversal` (201)
- `GET /reports/monthly-incomes?month=` · `GET /reports/money-flow?month=` (month required)
- `GET /reports/transactions[/{id}]` — rich feed with impact text · `GET /reports/card-due-by-month`

**Financing**
- `POST /financing/payment-plans` (card, creditor or split; description required)
- `GET /financing/cards/{id}/purchases|future-schedule|statements` · `GET /financing/statements/{id}`
- `GET /financing/purchases/recent` — includes `PendingAmountMinorUnits`, `PaidInstallmentCount`, `NextDueYear/Month`
- `POST /financing/statements/{id}/pay` · `POST /financing/installments/{id}/pay`
- `GET /financing/due-this-month?month=yyyy-MM&today=yyyy-MM-dd` → rows `{kind: card|creditor, sourceId, sourceName, currencyCode, amountMinorUnits}`, amount > 0 only
- `GET /financing/creditor-payables[/{creditorId}]` · `POST /financing/creditor-payables/{creditorId}/pay-full` (`amountMinorUnits` nullable, `currencyCode`)
- `POST /financing/creditor-installments/{id}/pay` (nullable `amountMinorUnits` = remaining) · `.../unpay` · `.../pay-party` (`partyId`, `bankAccountId`)
- `POST /financing/creditor-purchases/{paymentPlanId}/pay` (waterfall by Sequence)
- `POST|GET /creditors`
- `PUT /instruments/cards/{id}/closing-day` · `GET /instruments/cards/{id}/closing-dates` · `PUT|DELETE /instruments/cards/{id}/closing-dates/{year}/{month}`

**Subscriptions**: `POST /subscriptions`, `DELETE /subscriptions/{id}`, `GET /subscriptions/active`, `GET /subscriptions/by-month?month=`, `POST /subscriptions/{id}/pay|unpay`.

**Parties**: `GET /parties`, `GET /parties/{id}/balance|timeline|future-shares`, `GET /parties/pending-shares`, `POST /parties/{id}/settlements` (`currencyCode`, default ARS). `POST /parties/shared-expenses` was removed; splits go through Load an Expense (`?party=<id>`).

## Error code → HTTP status (`Endpoints/ErrorHttpStatusHelper.cs`)

- **404**: any `*NotFound` (Account, OriginalTransaction, Installment, PaymentPlan, Card, Statement, Creditor, Subscription, Party, Split).
- **409**: `Ledger.TransactionAlreadyReversed`, `Ledger.CannotReverseAReversal`, `StatementAlreadyPaid`, `InstallmentAlreadyPaid|AlreadyReversed|AlreadyAccrued|NotAccrued`, `NotACreditorInstallment`, `NoPaymentToUndo`, `PartyShareNotDue|AlreadyPaid`, `PartyNotInSplit`, `ClosingMonthLocked`, `ClosingChangeMovesChargedPurchase`, `Subscriptions.SubscriptionNotActive|NotPaid`, `Parties.SettlementExceedsBalance`.
- **422**: `*Invalid*`/`NonPositive*` codes — currency, closing/cutoff day, description (`BlankDescription`, `DescriptionTooLong`, `DescriptionMustBeSingleLine`), `FuturePurchaseDate`, `BackdatedCardBankAccountRequired`, `IncomeDateInFuture`, `InvalidIncomeDescription`, `SourceAccountNotSpendable`, `Ledger.Unbalanced`, `InvalidSplitWeights`, `UnknownFundingAccount`.
- **400**: `InvalidPaymentAmount`, `PaymentExceedsRemaining`, and any unmapped code (e.g. `PlanNeedsCardOrCreditor`, `PlanCannotMixCardAndCreditor`, `CreditorAccountRequired`, `CreditorAccountMismatch`).

## Credit cards

- `ResolveCycle(purchaseDate, cutoff)`: day ≤ cutoff → same month's close, else next month. Creditors use fixed cutoff **26**.
- Create-time: DueCycle < current month → installment paid; == current → accrued, pending; future → untouched. Future `purchaseDate` → 422.
- Back-dated card plans auto-accrue closed cycles and pay elapsed installments with real, historically dated ledger postings from the chosen "Paid from" bank. Creditor elapsed installments get a display-only `PaidOnUtc`.
- Accrual scheduler (1 min): card gate posts when the cycle is closed. Split receivable posts when DueCycle ≤ current month.
- **Pay statement**: payable = Σ(accrued, non-reversed, unpaid installments) — *not* `AmountDue`, which is increment-only. Zero payable → failure. Carried-credit netting applies; marks installments and statement `PaidOnUtc`.
- **Pay installment**: must be accrued, non-reversed, unpaid. Plain `Dr CardLiability / Cr Bank`, no netting. Whole installment only.
- **Closing dates**: usual day + per-month override (day clamped to month length; override day lies inside the cycle's month). Editing re-buckets open plans; a month with a `MonthlyStatement` is locked (409 `ClosingMonthLocked`); if any installment of a moved plan is already charged the whole edit is refused (409 `ClosingChangeMovesChargedPurchase`).
- Card purchases "outstanding" = non-reversed installments that are not accrued, or accrued with the statement unpaid.

## Creditors

- Debt is Financing-only and display-only: payments touch neither bank nor ledger. **Exception**: paying a party's share of an installment settles through `IPartiesApi.SettleCurrentAccountAsync` (`Dr Bank / Cr Receivable_party`) then applies the payment; undo reverses the settlement and fails without saving if the reversal fails. Share is only offered on split-accrued, non-reversed installments, one per party per installment.
- Remaining = Amount − Σ payments. Null amount = remaining. Over-remaining → 400; zero remaining → 409 `InstallmentAlreadyPaid`. Installment `PaidOnUtc` set when remaining reaches 0.
- Undo removes the latest payment row (`NoPaymentToUndo` otherwise). No bulk undo.
- Full-debt pay with an amount requires a currency and fills oldest DueCycle first (then PurchaseDate, Sequence); null amount settles everything in every currency. Idempotent: 0 settleable → 0, not an error.
- **Due now** uses cutoff 26 (arrears included); **Total owed** is all unpaid. A creditor/currency row is one line.

## Dashboard — Due this month

- Cards: unpaid, non-reversed installments with DueCycle ≤ selected month (overdue included). Creditors: remaining on installments due by the **calendar** month. "Rule X" for past/future months: due on or before M minus amounts paid.
- ARS and USD are never converted; totals are per currency. The client supplies `today` (host clock is UTC).
- Every dashboard card follows the month picker. Subscriptions by-month: paid if a non-reversal transaction with a subscription reference was posted that UTC month with no storno.

## Parties & splits

- Split receivable accrues per installment at DueCycle (card and creditor alike); the client shows a "scheduled" state and does not poll for balance change.
- Future shares are computed in Financing C# with `PhantomPennyAllocator` (no SQL view; Reporting reads `vw_*` views only).
- Parties list merges `GET /parties` with debt summary client-side; a movement-less party shows "settled / $0".

## Subscriptions

- Flat amount. Pay = `Dr <name> Subscription / Cr Funding` dated now, advances exactly one period. No auto-renew scheduler, no catch-up batch.
- Creation with an anchor ≤ today posts and marks that period paid. ARS→USD flip of legacy subscriptions was a one-time migration scoped by `SubscriptionReferenceId`.

## Incomes & money flow

- Income: target must be Bank/Cash, date ≤ today, description required; credit side is a single lazily created `Income` account. Manual only.
- Money Flow outcome = my share of expense debits (excludes Receivable and CardPurchases kinds); reversed pairs hidden. Double-reversal guard is global.

## Transaction feed (Reporting)

- Rows carry badge, description, from→to line and an expandable "If you reverse this" panel built by the pure `TransactionExplainer`, whose wording must mirror `ReversalCalculator.Decide`. Kinds: Undo entry, Card installment, Creditor installment party share, Card bill payment, Subscription, Shared expense, Income, Expense, Party payment, Other.

## Data conventions

- EF Money mapping: `<Name>MinorUnits` + `CurrencyCode TEXT NOT NULL DEFAULT 'ARS'`.
- API tests use `EnsureCreated()`, not migrations. Migrations are never auto-applied to a dev `personalfinance.db`; the container applies them on startup (see [`RELEASING.md`](RELEASING.md)).
