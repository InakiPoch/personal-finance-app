# Billing cycle "due month" reframe + creditor-split parity — Overview

> Read this first. It carries the shared model and the reasoning behind every slice.
> Each `slice-N-*.md` is self-contained and can be picked up cold, but they assume the
> model below. Build and test one slice green before starting the next.

## Why this work exists

A user loaded a **credit-card expense dated Sept 6, 3 installments, split with a party** and
expected the party "Scheduled" block to read **Oct / Nov / Dec**. It read **Sep / Oct / Nov**.
Digging in surfaced two separate problems:

1. **Issue 1 (priority) — the app shows the _statement-close_ month as if it were the _payment_
   month.** The user's north star: *"open the app and see what I need to pay **this** month."*
   Real-world card behavior they want:
   - Purchase **on/before** the card's closing day → first payment **next month**.
   - Purchase **after** the closing day → first payment **two months out**
     (Sept purchase after cutoff → November).
2. **Issue 2 — a creditor-financed ("owed to a creditor") split behaves nothing like a card
   split.** It shows as money **owed right now, settleable this month**, and never appears in the
   future-shares schedule. A card split correctly shows **$0 owed now** with installments scheduled
   ahead. The user wants creditor splits to behave **exactly like card splits**.

## What the code actually does today (root causes — verified read-only)

- **Every read path is verbatim.** Backend (`GetFuturePartyShares`, `AccrueInstallments`,
  `vw_card_future_schedule`, `card_due_by_month.sql`, all card queries) and client (party-detail,
  dashboard, statements) display the stored `(CycleYear, CycleMonth)` exactly. **The bug is not in
  any reader.**
- **The stored cycle means "statement-close month."**
  `Domain/BillingCycleCalculator.cs` → `ResolveCycle(purchaseDate, cutoffDay)`: purchase day
  `≤ cutoff` → purchase month; else next month. `Domain/BillingCycle.cs` XML doc + design D11
  confirm `CycleMonth` = statement-close, and payment is treated as due *the day after cutoff, same
  month*. **There is no separate due-date concept anywhere in the domain.**
- **The card and creditor write paths already disagree.** `Domain/PaymentPlan.cs` → `Create`:
  - card → `BillingCycleCalculator.ResolveCycle(...)` (statement-close, **no +1**)
  - creditor → `new BillingCycle(purchase.Year, purchase.Month).AddMonths(1)` (**purchase + 1**)
- **Card splits accrue at statement close.** `Application/Scheduling/AccrueInstallments.cs`
  (1-minute hosted scheduler, `RunOnStartup`) posts an installment when
  `installment.Cycle.IsClosedAsOf(today, card.CutoffDay)`, then calls `RecordSplitAccrualHandler`
  which grows `ExpenseSplit.AccruedReceivable` (starts at `Money.Zero` for card splits).
- **Creditor splits book the full receivable up front.** `LinkPaymentPlanSplitHandler.cs` posts one
  lump ledger transaction on the purchase date; `OnPaymentPlanCreated.cs` sets
  `AccruedReceivable = full amount` for creditor plans. This was the **Phase 23 hotfix** for a
  "co-borrower shows Settled up" bug. And `GetFuturePartySharesHandler` has
  `where plan.CardId != null`, which **excludes creditor plans entirely**. The retired
  `AccrueCreditorSplitInstallments` scheduler (recoverable from git) used to trickle-post creditor
  receivables per owed-month.
- **"Settled up" is posted-balance only.** `Reporting/Sql/debt_by_party.sql` over
  `vw_current_account_timeline` (INNER JOIN to receivable movements); client `parties-page.ts` →
  `balanceHint` returns `'Settled up'` on `netBalance == 0`. Future scheduled installments are
  invisible to it.

## Decisions (each grilled with the user, one fork at a time)

| # | Decision | Why |
|---|----------|-----|
| **Q1** | The number on screen means **"when the money moves" = payment-due month** | The user's whole mental model is "what do I pay this month" |
| **Q2** | **Keep the stored cycle honest (statement-close); derive `DueCycle = Cycle.AddMonths(1)` at the read/projection layer** | No card-path migration; `ResolveCycle` untouched; `AccrualBoundaryTests` stay green; statements stay honestly labeled |
| **Q3** | Creditor split must behave **like a card split**: $0 now, accrues at due month, visible in the schedule | The old "Settled up" bug's real cause was **missing future-shares visibility**, not missing money. First recommendation (keep full up-front booking) was **wrong** and corrected |
| **Q4** | The fix moves the **money**, not just the label — the future→owed flip (accrual timing) moves to the due month | A display-only `+1` becomes a lie the moment the statement closes and the balance flips to "owed" a month early |
| **Q5** | Make the Parties list/summary **schedule-aware** (its own slice) | A $0-now party with 3 pending installments should not read "Settled up" |

## The model — the invariant every slice upholds

- **STORED** `Installment.CycleYear/CycleMonth` = **statement-close cycle**. Unchanged. It is the
  `MonthlyStatement` grouping key. Honest, no card-path migration.
- **DERIVED** `DueCycle = Cycle.AddMonths(1)` = **the month the money moves.** One concept, one
  place.
- **Payment-facing surfaces key on `DueCycle`:** "what I pay this month", the party "Scheduled"
  block, dashboard due-by-month, card future schedule.
- **Statement-facing surfaces keep keying on `Cycle`:** statements list, monthly statement view.
- **The split co-borrower receivable accrues at `DueCycle`** (not at statement close) — the party
  owes $0 until the due month, schedule visible meanwhile.
- **Creditors unify under the same rule:** store the *cycle-close-equivalent* = **purchase month**
  (currently purchase + 1), so the uniform `+1` projection yields first-payment = next month with
  no card-vs-creditor branching in readers. Creditors have no cutoff, so no before/after-closing
  split for them — always next month.

## Slice order & dependencies

1. **Slice 1 — Card due-month** (Issue 1, priority). Introduces `DueCycle` and the payment-facing
   projection + split-accrual timing. Everything else builds on it.
2. **Slice 2 — Creditor-split parity** (Issue 2). *Depends on Slice 1.* Reuses the due-month
   accrual machinery; undoes Phase 23's up-front booking safely.
3. **Slice 3 — Schedule-aware summary** (Q5). *Depends on Slices 1 & 2.* Needs future-shares
   visibility to exist for both card and creditor first.

## Working agreements

- Each slice is a vertical tracer bullet: **API + Client + Testing**, built and tested green before
  the next begins.
- **The user commits each slice themselves — no self-commits.**
- Info-gathering is delegated to subagents; design/synthesis is done by the orchestrator.
- Technical artifacts (code, comments, tests, docs) are English; UI copy is English unless the
  existing view says otherwise.

## Verification (applies to every slice)

- **API** — from `app/api`: `dotnet build`, then `dotnet test --solution PersonalFinance.sln`
  (all green; watch `AccrualBoundaryTests` especially — it proves the write side is untouched).
- **Client** — from `app/client`: `pnpm ng lint`, `pnpm ng build`,
  `CHROME_BIN=/usr/bin/brave pnpm ng test --watch=false --browsers=ChromeHeadless`.
- **Live smoke (the original repro)** — run API + client, load a card expense dated today (Sept 6),
  3 installments, split with a party → "Scheduled" reads Oct/Nov/Dec, party owes $0 now. Repeat for
  a creditor-financed split → identical behavior. Confirm the statements view still labels the
  September statement as September.
