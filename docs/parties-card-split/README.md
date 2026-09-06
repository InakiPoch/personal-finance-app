# Parties card-split — fix the reconcile hang + surface future shares

> Initiative folder. Two implementation slices plus one verify-only note. Do them **in order**,
> each tested and committed before the next (one green step at a time). All artifacts are English.

## Why this exists

Creating a **credit-card expense split with a party** appeared to hang: after submit, the client
called `GET /v1/parties/{id}/balance` repeatedly (200 OK) until it stopped, and the view stayed on
*"still reconciling — the receivable is being posted, refresh shortly"* indefinitely, so the user
believed the expense was never created. The user also wants the Parties view to reflect that a
co-borrower owes their proportional share **per installment, per month** — recognized on each billing
cycle (buy Sept 6 → first owed month October) — until all N installments are paid, then settled.

## What the investigation found (ground truth as of branch `chore/client-sidejobs`)

1. **The expense IS created.** `load-expense-page.ts` sets `submitStatus = 'confirmed'` and renders the
   "Confirmed" panel *before* `reconcile()` runs. The balance poll is a **secondary** widget. The hang
   is therefore **cosmetic and client-only** — nothing is lost, the plan is persisted.

2. **Root cause of the hang.** `reconcile()` polls `getBalance` with the exit condition
   `balance !== prior`, at most 5 attempts × 800 ms ≈ 4 s, then sets status `'stalled'`. For a **card**
   split, no receivable posts synchronously:
   - `LinkPaymentPlanSplitHandler` posts the co-borrower receivable up front **only when `CardId is null`**
     (the creditor-financed path — commit `a4efb67`).
   - `AccrueInstallments` posts a card installment's receivable **only once its billing cycle closes**
     (next month).
   So a card split's balance cannot change inside the 4 s window → guaranteed stall. Debit/cash
   (`RegisterSharedExpenseHandler`) and creditor-financed both post up front, so their polls succeed.
   **Card is the lone gap.**

3. **The monthly accrual the user wants already works server-side.** `AccrueInstallments.buildSplitLines`
   posts `Dr Receivable[party] / Cr CardLiability` for each card-split installment when its cycle closes,
   splitting with `PhantomPennyAllocator`. First cycle for a Sept 6 purchase is **October**
   (`BillingCycleCalculator.ResolveCycle`), one share per month thereafter. **No accrual work needed.**

4. **The genuine gap:** those **future / not-yet-accrued** shares are not *visible* anywhere. The party
   timeline read-view (`vw_current_account_timeline`) INNER-JOINs posted movements only; the dashboard
   future schedule shows card-holder totals with no per-party breakdown. Before October a freshly-split
   party reads "$0 / settled." → **Slice 2b** adds the future view.

5. **All four dashboard complaints are already implemented on this branch** → **verify-only** (see
   `dashboard-verify.md`). If any still misbehaves on a clean build, escalate it to its own slice.

## Decisions locked with the user

- Accrual = proportional share per installment, recognized **by billing cycle** (already implemented).
- For **card** splits the client **must not poll** for a balance change — confirm and mark *scheduled*.
- Scope = **Slice 1 + Slice 2b** (the fullest option: loop fix *and* the Parties future view).

## Slices

| Order | Doc | Scope | Touches |
| --- | --- | --- | --- |
| 1 | `slice-1-reconcile-loop-fix.md` | Stop the reconcile poll for card splits | Client only |
| 2 | `slice-2b-party-future-shares.md` | Show a party's upcoming monthly shares | Financing + host + client |
| — | `dashboard-verify.md` | Confirm the 4 dashboard items on a fresh build | Verification only |

## Conventions to honor

- Modular monolith; modules communicate through `.Contracts` facades. `PersonalFinance.Architecture.Tests`
  (RNF-9) enforce the edges — keep cross-module additions `.Contracts`-only. The **host** is the
  composition root and may call any module facade.
- **Reporting reads SQL views only, never module facades** (RNF-6). Slice 2b deliberately puts its query
  in **Financing**, not Reporting, precisely to avoid this and to keep allocation in C# (see that doc).
- Test runner: API uses .NET 10 Microsoft.Testing.Platform — always `dotnet test --solution` or
  `--project`, never a bare path. Client Karma needs `CHROME_BIN=/usr/bin/brave`.
- The user commits; do not self-commit. No AI attribution; conventional commits.
