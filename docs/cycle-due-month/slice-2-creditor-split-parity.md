# Slice 2 — Creditor-split parity (Issue 2)

> **Depends on:** Slice 1 (the `DueCycle` concept + the due-month split-accrual machinery).
> **Read `00-overview.md` first**, then `slice-1-card-due-month.md`.
> **Goal of this slice:** a creditor-financed ("owed to a creditor") split behaves **exactly like a
> card split** — **$0 owed now**, accrues at its due month, and is **visible in the future-shares
> schedule**.

## The intent, in one paragraph

A creditor-financed split is an installment plan by definition, yet today it reads as a single lump
**owed right now, settleable this month**, and never shows in the schedule. Two things cause that:
the Phase 23 hotfix books the **full co-borrower receivable up front**, and
`GetFuturePartySharesHandler` **excludes creditor plans** (`where plan.CardId != null`). We undo the
up-front booking, let the receivable **accrue at the due month** (reusing Slice 1's machinery), and
let creditor plans **into** the future-shares query. The result is card/creditor parity — the
user's own definition of "correct."

## Why undoing Phase 23 is safe now (important — read this)

Phase 23 booked the full receivable up front to kill a "co-borrower shows **Settled up**" bug. But
that bug's real cause was **not** missing money — it was that the co-borrower had `$0` owed **and no
visible future schedule**, so the only signal left was "$0 → settled." Slice 1 makes the schedule
**visible**. Once a $0-now party reads as "nothing due yet, N scheduled," reverting the up-front
booking no longer reintroduces the bug. **Do not ship this slice without Slice 1's future-shares
visibility in place** — that visibility is the safety net.

(Slice 3 additionally fixes the Parties *list* summary so it, too, stops saying "Settled up" for a
scheduled party. Slice 2's regression guard is limited to the *detail* schedule.)

## Code landscape

### Write path — unify creditor onto the shared rule
- `app/api/src/Modules/Financing/PersonalFinance.Financing/Domain/PaymentPlan.cs` → `Create`,
  creditor branch. Today:
  ```csharp
  var firstCycle = cardId is not null
      ? BillingCycleCalculator.ResolveCycle(purchaseDate, cutoffDay!.Value)
      : new BillingCycle(purchaseDate.Year, purchaseDate.Month).AddMonths(1); // creditor: purchase + 1
  ```
  Change the creditor branch to store the **purchase month** (drop `.AddMonths(1)`), so the uniform
  `DueCycle = +1` projection from Slice 1 yields first-payment = next month — with **no
  card-vs-creditor branching** in any reader. Creditors have no cutoff, so there is no
  before/after-closing distinction; first payment is always next month.
  > **Migration note:** existing creditor installments would shift by one month under this change.
  > The creditor feature is recent and was uncommitted at planning time — **confirm there is no
  > real creditor data** before deciding whether a data fix-up/migration is needed. If there is
  > data, write a migration that shifts stored creditor cycles back by one so `DueCycle` lands
  > correctly; if there is none, no migration.

### Split receivable — start at $0, accrue at due month
- `app/api/src/Modules/Parties/PersonalFinance.Parties/Application/EventHandlers/OnPaymentPlanCreated.cs`
  — creditor `AccruedReceivable` currently = full amount:
  ```csharp
  var accruedReceivable = integrationEvent.CardId is null
      ? participantShares.Aggregate(Money.Zero(currency), (r, s) => r + s.Share) // full, up front
      : Money.Zero(currency);
  ```
  Make the creditor branch start at `Money.Zero`, **mirroring the card branch**.
- `app/api/src/Modules/Financing/PersonalFinance.Financing/Application/Commands/LinkPaymentPlanSplit/LinkPaymentPlanSplitHandler.cs`
  — remove the up-front full-receivable ledger post (the `plan.CardId is null && SplitParticipants`
  block that posts "Creditor-financed split" on `PurchaseDate`). Keep the creditor-payable account
  provisioning it also does — only the up-front *receivable* post goes away.
- **Restore due-month accrual for creditors.** The retired
  `Application/Scheduling/AccrueCreditorSplitInstallments.cs` (recover from git;
  `git log --diff-filter=D` / `git show <sha>^:<path>`) trickle-posted creditor receivables per
  owed-month via `CreditorSplitReceivableCalculator.BuildLines(...)` +
  `RecordSplitAccrualAsync(...)`. Bring that pattern back, but **gate on `DueCycle` arrival** and,
  ideally, **unify it with Slice 1's split-accrual path** so cards and creditors share one
  due-month accrual mechanism rather than two parallel schedulers. Reuse
  `CreditorSplitReceivableCalculator` — do not reinvent the ledger lines
  (`Dr Receivable_k / Cr CreditorPayable`).

### Read path — let creditors in
- `app/api/.../Application/Queries/GetFuturePartyShares/GetFuturePartySharesHandler.cs` — remove the
  `where plan.CardId != null` filter so creditor installments surface. The current query joins
  `CreditCards` for `CardName`; make that **null-safe** for creditor plans (LEFT join / conditional
  projection; creditor plans have no card — supply the creditor name or a neutral label instead).

### Client
- Mostly free once the API returns creditor installments in the same shape. Verify:
  - `party-detail-page.ts` "Scheduled" block renders creditor installments identically to card ones
    (the source label may differ — creditor name vs card name).
  - Load-expense creditor mode (`load-expense-page.ts`) still submits correctly and the resulting
    party view shows the schedule.

## Testing

- Creditor Sept purchase, 3 installments → **$0 owed now**, schedule **Oct / Nov / Dec**, appears in
  future shares.
- On due-month arrival the co-borrower's posted balance grows by that installment's share (accrual
  works).
- **Regression guard:** a fresh creditor split does **not** read "Settled up" in the **detail**
  schedule (it shows the pending months). (List summary → Slice 3.)
- Card splits from Slice 1 still behave identically — parity holds both directions.
- Existing Parties/Financing suites stay green.

## Definition of done

- Loading a creditor-financed split produces the same on-screen story as a card split: $0 now,
  schedule ahead, accrues at the due month.
- No "Settled up" regression in the detail view.
- API + client build, all tests green, live smoke on a creditor split matches the card split.
- **Do not commit** — hand the slice to the user to commit.
