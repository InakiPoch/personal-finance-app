# Back-dated Expenses — Overview

> Initiative: recognize past purchases when loading an expense, so an old purchase reflects
> the installments already paid, the money already moved, and the amount still pending —
> instead of being treated as a brand-new purchase starting this cycle.

This folder holds one planning doc per vertical slice (tracer bullet). Each slice is
**API + Client + Testing**, tested and green before the next begins. Read this overview first,
then the slice you're implementing.

---

## The problem

When you load an expense today, the schedule is built from `purchaseDate`. A back-dated
purchase therefore gets *cycles that are already partly in the past*, but the app still shows
it as `0/N paid` and points "next payment" at a month that has already gone by. What the user
wants: loading a purchase from two months ago should immediately read like a purchase you've
been paying for two months.

**User story.** Today is 2026-09-07. I load a $3000 expense, 3 installments, purchase date
2026-06-15. I expect to see **2 of 3 paid, $2000 paid, $1000 pending this month.**

---

## What is already true (do NOT rebuild this)

The installment **cycle math already keys off `purchaseDate`, not "today".** Verified against
the user's examples with a card cutoff of ~20:

| Purchase date | `ResolveCycle` first close cycle | Cuota 1 due | As of Sep 7 |
|---|---|---|---|
| Jul 29 | day 29 > cutoff → **August** | September | "1/n, next September" ✅ |
| Jul 15 | day 15 ≤ cutoff → **July** | August (cuota 1), September (cuota 2) | "2/n" ✅ |

So the cycles are already correct. The gaps are elsewhere (below).

Relevant existing code (all under `app/api/src/Modules/Financing/PersonalFinance.Financing/`):

- `Domain/BillingCycleCalculator.cs` — `ResolveCycle(DateOnly purchaseDate, int cutoffDay)`:
  purchase day ≤ (clamped) cutoff → current month cycle, else next month.
- `Domain/BillingCycle.cs` — `(Year, Month)` record; `DueCycle => AddMonths(1)`;
  `IsClosedAsOf(today, cutoffDay)`.
- `Domain/PaymentPlan.cs` — `PaymentPlan.Create(...)` builds N `Installment`s at
  `firstCycle.AddMonths(i)`, `Sequence = i+1`. **Card branch** uses `ResolveCycle`; **creditor
  branch** currently uses the raw purchase month (this is gap #2).
- `Domain/Installment.cs` — `Sequence`, `Amount`, `CycleYear/CycleMonth`, and state flags
  `AccruedOnUtc`, `StatementId`, `PaidOnUtc`, `SplitAccruedOnUtc`, `IsReversed`; methods
  `MarkAccrued(now, statement)`, `MarkPaid(now)`.
- `Application/Scheduling/AccrueInstallments.cs` — 1-min scheduler. **Gate 1**
  (`accrueClosedCyclesAsync`, card-only via `plan.CardId != null`) posts
  `Dr CardExpense / Cr CardLiability` and rolls the cuota onto a `MonthlyStatement` once its
  cycle `IsClosedAsOf(today, cutoff)`. Idempotency guards: query filters `AccruedOnUtc == null`,
  in-loop re-check, and domain `MarkAccrued` refuses if already accrued.
- `Application/Commands/PayInstallment/PayInstallmentHandler.cs` — pays one **card** installment:
  `Dr card.LiabilityAccountId / Cr command.BankAccountId`, then `installment.MarkPaid`. Bank is
  an **explicit** command parameter — there is no default. Rejects installments that are not
  accrued / have no `StatementId` (so it rejects creditor installments).
- `Application/Commands/CreatePaymentPlan/CreatePaymentPlanHandler.cs` — the create vertical for
  card / creditor / split purchases. Resolves the card's `CutoffDay` (card mode) or `null`
  (creditor mode) and calls `PaymentPlan.Create`. **No ledger posting at creation today.**

Client: `app/client/src/app/features/financing/pages/load-expense-page/` already has a required
`purchaseDate` field and a three-way mode selector (`card` / `debit` / `creditor`). Recent
Purchases (`features/financing/pages/recent-purchases-page/`) already renders
`"{paidInstallmentCount}/{installmentCount} paid · next: {month}"` / `"Fully paid"` driven by
`PaidOnUtc` on the installments (delivered by the prior `individual-installment-payments`
initiative). So once `PaidOnUtc` is set correctly, the display "just works".

---

## The gaps this initiative closes

1. **Elapsed installments show unpaid.** Cuotas whose payment (due) month is already in the
   past must be recognized as **paid**. → Slice 1 (card), Slice 2 (creditor).
2. **Creditor purchases ignore the cutoff.** They use the raw purchase month; they must honor a
   **26th** closing day, uniformly, via `ResolveCycle`. → Slice 2.
3. **Future purchase dates are accepted.** Reject `purchaseDate > today`. → Slice 1 (shared
   validator).
4. **Pending $ not surfaced.** Show Σ(unpaid cuota amounts) somewhere. → Slice 3 (optional).

---

## Core rule (shared by both modes)

```
firstCloseCycle = ResolveCycle(purchaseDate, cutoff)      // cutoff = card.CutoffDay, or 26 for creditors
cuota k (1-based): closeCycle = firstCloseCycle + (k-1) months
                   dueCycle   = closeCycle + 1 month
currentMonth = (today.Year, today.Month)
```

| Cuota's `dueCycle` vs `currentMonth` | Treatment |
|---|---|
| **before** current month | **elapsed → auto-PAID** (Slice 1: real ledger; Slice 2: display-only) |
| **equal to** current month | **pending this month** — accrued & owed (card) / just owed (creditor), not paid |
| **after** current month | future — left untouched; the scheduler handles it later |

Check the user story: Jun 15, 3 cuotas, cutoff ~20 → cuota 1 due Jul, cuota 2 due Aug, cuota 3
due Sep. As of Sep 7: Jul + Aug `< Sep` → paid (2/3, $2000); Sep `== Sep` → pending ($1000). ✅

**Fully-elapsed edge:** if *every* cuota's `dueCycle < currentMonth` (e.g. a 3-cuota buy from
January loaded in September), all are marked paid → "Fully paid".

---

## Decisions (settled up-front — do not relitigate)

| # | Decision |
|---|----------|
| Elapsed state | Auto-marked **PAID**. |
| Card settlement | **Real ledger** postings, historically dated (Slice 1). |
| Creditor settlement | **Display-only** `PaidOnUtc` stamp, no ledger — matches the existing ledger-free creditor model. A full creditor ledger is a separate initiative. |
| Creditor cutoff | Hardcoded constant **`26`**, applied **uniformly** to all creditor purchases (a fresh creditor buy after the 26th now rolls to the next cycle — intended). |
| Card bank source | New **"Paid from" bank selector**, shown/required only when a card purchase is back-dated. No silent default. |
| Entry dates | **Historical months** — accrual at the close date, payment at the due date. Reporting stays truthful; a past-month bank balance going negative is acceptable (the app tracks, doesn't enforce, balances). |
| Future dates | **Rejected** (`purchaseDate > today`), validator + client. |
| Pending $ | Optional Slice 3; counts + next-month already suffice. |

---

## Slice order

1. **`slice-1-card-backdating.md`** — card: synchronous accrue + settle of elapsed cuotas,
   "Paid from" bank selector, future-date guard. *(Contains the shared validator change.)*
2. **`slice-2-creditor-cutoff.md`** — creditor: 26th cutoff via `ResolveCycle`, display-only
   `PaidOnUtc` stamp.
3. **`slice-3-pending-amount.md`** — OPTIONAL: surface pending $ on Recent Purchases.

Each slice: implement API → client → tests, then run the gate below and confirm green before
starting the next.

## Verification gate (run per slice)

- API: `cd app/api && dotnet test --solution PersonalFinance.sln` (currently 273 green; while
  iterating, `--project tests/PersonalFinance.Financing.Tests`).
- Client: `cd app/client && pnpm ng lint` and
  `CHROME_BIN=/usr/bin/brave pnpm ng test --watch=false --browsers=ChromeHeadless` and
  `pnpm ng build --configuration production`.
- `global.json` opts into .NET 10's Microsoft.Testing.Platform runner — always pass
  `--solution` or `--project`, never a bare path.
