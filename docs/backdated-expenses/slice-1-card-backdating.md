# Slice 1 — Card back-dating

> Read `00-overview.md` first (context, the core rule, the decisions table). This slice is
> **card mode only**. Creditor mode is Slice 2.

## Goal

Loading a back-dated **credit-card** purchase auto-settles every elapsed cuota with real,
historically-dated ledger postings, funded from a bank the user picks; the current-month cuota
shows accrued-and-pending; future purchase dates are rejected. After this slice, the user story
(2/3 paid, $2000 paid, $1000 pending) is true in the ledger and on Recent Purchases for cards.

## Why this way

- The schedule already computes correct cycles (see overview). What's missing is that the
  elapsed cuotas are never *settled*, and the 1-min `AccrueInstallments` scheduler would only
  ever accrue them as **unpaid** liability — never pay them.
- We settle **synchronously at creation** (not by waiting for the scheduler) so Recent Purchases
  is correct the instant the expense is saved. Setting the state flags also makes the scheduler
  skip these rows, so there's no double-accrue.
- A card cuota that was accrued (`Cr CardLiability`) **cannot** just be flagged paid — the
  liability has to be cleared with a real `Dr CardLiability / Cr Bank`. That payment needs an
  explicit bank account (the pay path has no default), hence the new "Paid from" selector.
- Entries are **historically dated** (accrual at the close date, payment at the due date) so
  month-by-month reporting reflects reality.

## The mechanics (what the handler must do)

After `PaymentPlan.Create` in the **card branch** of
`Application/Commands/CreatePaymentPlan/CreatePaymentPlanHandler.cs`:

```
today        = clock.today (DateOnly)
currentMonth = BillingCycle(today.Year, today.Month)
cutoff       = card.CutoffDay

for each installment (in ascending Sequence):
    closeCycle = installment.Cycle
    dueCycle   = installment.DueCycle
    if not closeCycle.IsClosedAsOf(today, cutoff):
        break                      # nothing later can be closed either (cycles ascend)

    # (1) ACCRUE — mirror AccrueInstallments Gate 1, dated at the historical close date
    statement = MonthlyStatements.find(card.Id, closeCycle) ?? MonthlyStatement.Open(card.Id, closeCycle)
    statement.Accrue(installment)                                  # rolls AmountDue + raises event
    ledger.Post( Dr card.ExpenseAccountId / Cr card.LiabilityAccountId, amount=installment.Amount,
                 date = close date of closeCycle )                 # historical
    installment.MarkAccrued(accruedAt = close date, statement)     # sets AccruedOnUtc + StatementId

    # (2) PAY — only if the due month is strictly in the past
    if dueCycle < currentMonth:                                    # compare by (Year*12+Month) ordinal
        ledger.Post( Dr card.LiabilityAccountId / Cr command.BankAccountId, amount=installment.Amount,
                     date = due date of dueCycle )                 # historical
        installment.MarkPaid(paidAt = due date)                    # sets PaidOnUtc
        if statement.IsFullyPaidBy(statementInstallments): statement.MarkPaid(...)
```

Notes / gotchas:
- **Accrue the current-month cuota too** (its `closeCycle` has already closed), but **do not pay
  it** — it stays accrued-and-owed, i.e. "pending this month". That's what makes "$1000 pending"
  correct immediately rather than a minute later when the scheduler runs.
- **Ordinal comparison.** `BillingCycle` has no `<` operator; compare `Year*12 + Month`.
- **Historical dates.** `PostTransactionCommand` takes a date. "Close date" = the clamped cutoff
  day of `closeCycle` (`min(cutoff, DaysInMonth)`); "due date" = a representative day of
  `dueCycle` (e.g. the same clamped cutoff day, or the 1st — pick one and note it in the test).
- **Idempotency.** Because we set `AccruedOnUtc` + `StatementId` (and `PaidOnUtc` for the paid
  set), `AccrueInstallments` Gate 1 skips them (`AccruedOnUtc == null` filter + in-loop re-check
  + `MarkAccrued` refusing a second time). Verify no double-post in the test.
- **Splits.** If the plan is split, `SplitAccruedOnUtc` stays null and Gate 2 continues to handle
  the co-borrower receivable at `DueCycle` as today. This slice does not change split behavior;
  just don't set `SplitAccruedOnUtc`.
- **Reuse, don't reinvent** the posting shapes: accrual mirrors `AccrueInstallments.accrueClosedCyclesAsync`;
  payment mirrors `PayInstallmentHandler` (`Dr LiabilityAccountId / Cr BankAccountId`). If the
  overlap is large, consider extracting a small domain/application helper both call — but keep
  the scheduler behavior identical.

## Bank source ("Paid from") — new input

`PayInstallment` requires an explicit `BankAccountId`; there is no default. So the create
command must carry the bank to fund the retroactive payments.

- Add optional `Guid? BankAccountId` to `CreatePaymentPlanCommand` and the DTO.
- **Required when** the card purchase is back-dated *and* has at least one elapsed cuota
  (`dueCycle < currentMonth` for cuota 1). Otherwise ignored/optional. Enforce in
  `CreatePaymentPlanValidator`.

## Steps

1. **Contract + DTO.** Add `Guid? BankAccountId` to
   `Financing.Contracts/Commands/CreatePaymentPlanCommand.cs` and
   `Api/Endpoints/DTOs/CreatePaymentPlanDTO.cs`; map it in
   `Api/Endpoints/Mapping/FinancingMappingExtensions.cs`.
2. **Validator.** In `Application/Commands/CreatePaymentPlan/CreatePaymentPlanValidator.cs`:
   reject `PurchaseDate > today` (shared across all modes); require `BankAccountId` when a card
   purchase is back-dated with an elapsed cuota.
3. **Handler — accrue+settle pass.** Implement the mechanics above in the card branch of
   `CreatePaymentPlanHandler.cs`. Reuse `MonthlyStatement.Open`/`Accrue`,
   `Installment.MarkAccrued`/`MarkPaid`, `BillingCycle.IsClosedAsOf`, and the ledger post used by
   `PayInstallmentHandler`. Inject the same clock abstraction the scheduler uses (find how
   `AccrueInstallments` gets "today"; use that, don't hardcode `DateTime.Now`).
4. **API tests.** New handler tests (mirror the setup in `PayStatementHandlerTests` /
   `PayInstallmentHandlerTests` — in-memory SQLite `Filename=:memory:` + `EnsureCreated`, own
   `ThrowingConnectionFactory`). Cover:
   - Back-dated card plan (Jun 15, 3 cuotas, today Sep 7): cuotas 1 & 2 accrued **and** paid,
     cuota 3 accrued **and unpaid**; `paidInstallmentCount == 2`.
   - Ledger: two `Dr Expense/Cr Liability` at the close months + two `Dr Liability/Cr Bank` at
     the due months, all historically dated; bank credited twice.
   - Fully-elapsed plan (all cuotas due before this month) → all paid.
   - Fresh (today's) card purchase → nothing accrued/paid (unchanged behavior).
   - Future `purchaseDate` → validation error.
   - Back-dated card purchase with no `BankAccountId` → validation error.
   - Scheduler idempotency: running `AccrueInstallments` after creation does **not** re-accrue
     or double-post the pre-settled cuotas.
5. **Client — command type.** Add optional `bankAccountId` to
   `features/financing/types/create-payment-plan.ts`.
6. **Client — "Paid from" selector.** In
   `features/financing/pages/load-expense-page/load-expense-page.{ts,html}`, add a bank
   `<select>` shown when `mode === 'card'` && `purchaseDate` is before today; make it required in
   that condition; include it in the submit payload. Reuse the instrument list the **debit-mode**
   "Paid from" selector already loads (find its source service/query and reuse it).
7. **Client — future-date guard.** Add a client validator rejecting `purchaseDate > today`.
8. **Client tests.** In `load-expense-page.spec.ts`: selector is hidden for a today-dated card
   purchase, shown+required for a back-dated one, and `bankAccountId` is included in the posted
   body; future date blocks submit.

## Gate

Run the verification gate from `00-overview.md`. All API + client green, lint + prod build
clean, **before** starting Slice 2.

## Manual smoke (optional but recommended)

Run API + client. On a card with a known cutoff, load an expense dated ~2 months back, 3
installments, pick a bank. Recent Purchases should read "2/3 paid · next: <current month>"; the
chosen bank's balance should be down by two cuotas, dated in the two historical months; the
card statement list should now show the past-month statements.
