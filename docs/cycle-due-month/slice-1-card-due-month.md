# Slice 1 — Card due-month (Issue 1, priority)

> **Depends on:** nothing. This is the foundation.
> **Read `00-overview.md` first** for the shared model and the reasoning.
> **Goal of this slice:** every *payment-facing* card surface shows and behaves by the **due
> month** (`DueCycle = statement-close cycle + 1`), while statement identity stays honest.

## The intent, in one paragraph

Today the app stores an installment's **statement-close** cycle and shows that number everywhere,
including where the user reads "when do I pay." A Sept-6 purchase (cutoff 15) closes into the
September statement, so the schedule reads Sep/Oct/Nov. But the September statement is **paid in
October**. We are **not** changing what's stored (that would break statement identity, the
`AccrualBoundaryTests`, and force a migration). Instead we introduce a single derived concept —
`DueCycle = Cycle.AddMonths(1)` — and route every *payment-facing* surface through it, so the same
Sept-6 purchase reads Oct/Nov/Dec. We also move the moment a **split co-borrower's** debt becomes
real money to that due month, so the schedule and the posted balance never contradict each other.

## Why not just relabel the display (rejected)

A display-only `+1` is a lie with a ~10-day fuse. `AccrueInstallments` (1-min scheduler) posts the
installment the day the statement **closes** (`IsClosedAsOf(today, cutoff)`, mid-cycle-month). So a
Sept-6 split would show "$0, due October" until Sept 16, then the scheduler posts the co-borrower
receivable and the party suddenly **owes it in September**, dropping out of the future block. Label
says October, money moved in September. The user explicitly chose (Q4) to **move the money**, not
just the label.

## Code landscape (what to touch, what to leave)

### Domain — add the concept, change nothing stored
- `app/api/src/Modules/Financing/PersonalFinance.Financing/Domain/BillingCycle.cs` — add
  `DueCycle` (or a `ToDue()` / `AddMonths(1)` projection). `AddMonths` already exists here.
- `app/api/.../Domain/BillingCycleCalculator.cs` — **do not touch** `ResolveCycle`.
- `app/api/.../Domain/Installment.cs` — stored `CycleYear/CycleMonth` and `Cycle` stay as-is
  (statement-close). Optionally expose a convenience `DueCycle => Cycle.AddMonths(1)` for readers.
- `app/api/.../Domain/PaymentPlan.cs` — **card branch unchanged** in this slice (creditor branch is
  Slice 2).

### Payment-facing reads — project `DueCycle`
Route these through `DueCycle` (they currently read the stored cycle verbatim):
- `app/api/.../Application/Queries/GetFuturePartyShares/GetFuturePartySharesHandler.cs`
  (the party "Scheduled" block).
- `app/api/.../Application/Queries/GetCardFutureSchedule/GetCardFutureScheduleHandler.cs`.
- `app/api/.../Infrastructure/Persistence/ReadViews/vw_card_future_schedule.sql`.
- `app/api/src/Reporting/PersonalFinance.Reporting/Sql/card_due_by_month.sql`
  (dashboard "Card Debt by Cycle" / "what I pay this month").
- `GetCardPurchases` — review: its `row.CycleYear == currentCycle.Year && row.CycleMonth == ...`
  comparison decides "is this on the current statement." That is arguably **statement-facing**
  (which purchases are on the open statement), so likely leave it on the raw close-cycle. Decide
  per its UI meaning and note the call in the PR.

### Statement-facing reads — leave on the raw close-cycle
- `GetCardStatements`, `GetMonthlyStatement` — a statement's identity **is** its close month. Do not
  project. The September statement must keep saying September.

### The key subtlety — split accrual timing (this is the heart of the slice)
`app/api/.../Application/Scheduling/AccrueInstallments.cs` currently does everything under one gate:

```csharp
if (!installment.Cycle.IsClosedAsOf(today, card.CutoffDay)) continue;   // statement-close gate
// ... posts installment into its MonthlyStatement (statement grouping) ...
if (useSplit && partyPortionMinorUnits > 0) {
    await parties.RecordSplitAccrualAsync(...);   // co-borrower receivable — currently at close
}
```

Split the timing:
- **Statement grouping** (installment → `MonthlyStatement`) — may stay at `IsClosedAsOf` (the
  statement is honestly the close-month bill). Low risk; leave it unless verification below says
  otherwise.
- **Split co-borrower receivable** — gate on **due-month arrival** instead:
  `today >= first day of DueCycle` (i.e. `Cycle.AddMonths(1)` has begun). So the party owes $0 until
  the due month, and the installment stays visible in the future-shares block meanwhile.

> Implementation note: this may mean the split receivable posts in a *later* scheduler tick than the
> statement grouping. That's fine and intended. Keep the two gates independent and clearly named.

**Verify before finalizing:** does the user's own future-schedule view (`GetCardFutureSchedule`)
filter on `AccruedOnUtc == null`? If it does, an installment that accrues into its statement at
close would disappear from "future" a month before its due month. Ensure an unpaid-but-closed
installment still reads under its **due month** in the payment-facing views (project the cycle;
don't rely on `AccruedOnUtc` alone as the "is it future" signal for display).

### Client — display the projected month
- `app/client/src/app/features/parties/pages/party-detail-page/party-detail-page.ts` — `cycleLabel`
  uses `MONTH_LABELS[share.cycleMonth - 1]`; the `-1` is **array indexing, keep it**. The fix is
  that the API now hands it the **due** cycle, so no client arithmetic changes — just confirm the
  field it receives is the projected one.
- `app/client/src/app/features/.../dashboard-page/dashboard-page.ts` — `cycleByCard()` groups by
  cardId and shows cycles; it will show the due month once the API rows carry it.
- Card future-schedule component (financing feature) — same: verbatim display of the API's due
  cycle.
- Client type files carrying `cycleYear/cycleMonth` (`future-party-share`, `card-due-row`,
  future-schedule types): no shape change — the values they carry are now the due cycle.

## Testing

- **`AccrualBoundaryTests` — unchanged and green.** This is the proof that the write side / storage
  / `ResolveCycle` are untouched. If it goes red, the slice has drifted into the pipeline.
- **New unit/handler tests:**
  - Sept 6, cutoff 15 → payment-facing schedule reads **Oct / Nov / Dec**.
  - Sept 20, cutoff 15 → statement closes October → payment-facing schedule reads
    **Nov / Dec / Jan** (two months out).
  - Split co-borrower receivable is **$0 before the due month** and posts **on due-month arrival**.
  - Statements view still labels the **September** statement as September (statement-facing
    unchanged).
- **Client tests (Karma):** party-detail "Scheduled" block and dashboard render the due month for a
  known fixture.

## Definition of done

- Payment-facing card surfaces show the due month; statement surfaces still show the close month.
- A split co-borrower owes $0 until the due month, with the schedule visible meanwhile; the balance
  and the schedule agree.
- API + client build, all tests green (`AccrualBoundaryTests` included), live smoke on the Sept-6
  repro matches Oct/Nov/Dec.
- **Do not commit** — hand the slice to the user to commit.
