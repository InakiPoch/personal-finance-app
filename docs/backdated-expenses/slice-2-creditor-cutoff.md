# Slice 2 — Creditor cutoff + display-only paid

> Read `00-overview.md` first, and ship `slice-1-card-backdating.md` green before this. This
> slice is **creditor (card-less) mode only**.

## Goal

Creditor-financed purchases (a) honor a **26th** closing day, uniformly, using the same
`ResolveCycle` the cards use; and (b) recognize their elapsed cuotas as **paid** — but
**display-only** (`PaidOnUtc` stamp), with **no ledger movement**. After this slice, loading a
back-dated creditor purchase reads "N/M paid · next: <month>" on Recent Purchases exactly like a
card, without inventing a creditor ledger.

## Why this way

- **Creditors ignore the cutoff today.** `PaymentPlan.Create`'s creditor branch uses
  `new BillingCycle(purchaseDate.Year, purchaseDate.Month)` (raw purchase month), so a purchase
  on the 28th lands in the wrong cycle. Routing it through `ResolveCycle(purchaseDate, 26)` gives
  creditors the same on/after-cutoff behavior as cards.
- **Uniform, not back-dated-only.** Applying the cutoff to *all* creditor purchases keeps one
  code path and one mental model. Consequence (intended): a fresh creditor buy after the 26th now
  rolls to the next cycle. Call this out in the test so it's a documented choice, not a surprise.
- **Display-only paid, no ledger.** A plain creditor purchase has **no ledger footprint for the
  holder's own debt** anywhere today — not at accrual, not at payment; there is no "pay creditor"
  command at all. Creditor debt lives purely as `Installment` schedule rows. So recognizing
  elapsed cuotas as paid means stamping `PaidOnUtc` on those rows — nothing to post, no bank to
  pick. A full creditor ledger (accrual + payment postings) is a **separate initiative**, not
  part of this one.

## The mechanics

**(a) Cutoff.** In `Domain/PaymentPlan.cs`:
- Add a named constant, e.g. `private const int CreditorCutoffDay = 26;`
- Replace the creditor branch of the first-cycle selection:
  ```
  var firstCycle = cardId is not null
      ? BillingCycleCalculator.ResolveCycle(purchaseDate, cutoffDay!.Value)
      : BillingCycleCalculator.ResolveCycle(purchaseDate, CreditorCutoffDay);   // was: new BillingCycle(purchaseDate.Year, purchaseDate.Month)
  ```

**(b) Display-only paid stamp.** In the **creditor branch** of `CreatePaymentPlanHandler.cs`,
after `PaymentPlan.Create`:
```
today        = clock.today
currentMonth = BillingCycle(today.Year, today.Month)
for each installment:
    if installment.DueCycle < currentMonth:        # ordinal compare, same as Slice 1
        installment.MarkPaid(paidAt = due date of DueCycle)   # PaidOnUtc only — NO ledger, NO accrual
```
- Do **not** set `AccruedOnUtc` / `StatementId` (creditors never accrue; Gate 1 is card-only and
  won't touch them).
- Do **not** touch `SplitAccruedOnUtc` — for split creditor plans, Gate 3
  (`accrueDueCreditorSplitReceivablesAsync`) must keep posting the co-borrower receivables at
  `DueCycle` exactly as today. A back-dated split creditor plan will have past due cycles, so
  Gate 3 fires for the co-borrowers on the next tick — that's correct and unchanged.

## Steps

1. **Cutoff constant + branch.** Edit `Domain/PaymentPlan.cs` per (a) above.
2. **Handler stamp.** Edit `Application/Commands/CreatePaymentPlan/CreatePaymentPlanHandler.cs`
   creditor branch per (b). Reuse the clock abstraction introduced in Slice 1.
3. **API tests.** Add creditor cases (same in-memory SQLite harness as Slice 1):
   - **Cutoff shift:** a creditor purchase dated on the 27th of a month → first cuota's close
     cycle is the *next* month (documents the uniform-cutoff decision). A purchase on the 26th or
     earlier → current month.
   - **Back-dated creditor plan** (e.g. Jun 15, 3 cuotas, today Sep 7): `paidInstallmentCount`
     matches the strictly-past due cuotas; the current-month cuota is unpaid; **assert zero
     ledger transactions were posted** (no accrual, no payment).
   - **Fully-elapsed creditor plan** → all cuotas `PaidOnUtc` set → "Fully paid".
   - **Split creditor plan, back-dated:** the holder's cuotas get `PaidOnUtc`; co-borrower
     receivables still accrue via Gate 3 unchanged (no regression).
4. **Client.** No new UI — Recent Purchases already renders from `PaidOnUtc`. Verify the creditor
   path still submits without a bank selector (the Slice-1 selector is card-only). Add/adjust a
   `load-expense-page.spec.ts` case if needed to confirm the bank selector stays hidden for
   `mode === 'creditor'`.

## Gate

Run the verification gate from `00-overview.md`. All green, lint + prod build clean, before
Slice 3 (optional).

## Manual smoke (optional)

Load a back-dated creditor purchase → Recent Purchases shows the right paid count and next month,
and the ledger shows **no** new transactions for it (only co-borrower receivables if it was a
split). A fresh creditor purchase dated after the 26th now shows its first payment one month
later than it used to.
