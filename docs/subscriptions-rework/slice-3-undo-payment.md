# Slice 3 — Undo a payment

> Read `00-overview.md`, `slice-1-explicit-pay-foundation.md`, and `slice-2-pay-live-period.md` first.
> This slice reverses what Slice 2 does.

## Goal / Why

A mis-clicked Pay must be recoverable (decision 6). Undo:

- **reverses the pay-time ledger transaction** (removing that `X` from out-of-pocket);
- steps the current period **back one month** (`LastPaidPeriod` and `NextDueDate` both regress);
- flips the row's status **paid → overdue/upcoming** (whatever it was before the pay).

Undo is deliberately its own slice because — unlike the display-only creditor undo (which just nulls a
stamp) — it must reverse a **real ledger movement**, and that needs care.

## ⚠️ Fact-find before implementing (do this first)

Slice 2 posts a real transaction. Undo must reverse it. Confirm, in code, the exact reversal mechanism
before writing anything:

1. **How does the Ledger reverse a posted transaction?** Look at how Financing reverses an installment
   (the statement `Reverse` path — the `IsReversed` flow referenced by `installments-table`) and what
   `ILedgerApi` exposes. Determine whether reversal is (a) a dedicated `ReverseTransaction`-style API, or
   (b) a compensating `PostTransactionCommand` with the legs swapped (`Dr Funding / Cr Expense`).
2. **How is the subscription's pay transaction located for reversal?** The pay posting carries a
   `SubscriptionReferenceId`, and the Ledger stores its own `SubscriptionReference`. Confirm whether you
   reverse *by transaction id* (then the aggregate must remember the last pay transaction id — a new
   nullable field set in Slice 2's pay handler) or *by subscription reference + most-recent* lookup.
3. **Which is safer/idempotent here?** Prefer the approach Financing already uses, so out-of-pocket and
   the ledger stay consistent.

**If reversal turns out to require the aggregate to store the last pay transaction id, that is a small
add-back to Slice 2's `MarkCurrentPeriodPaid`/pay handler** — note it, and adjust Slice 2 before this
slice (Slice 2 must still be green). Record the finding at the top of the implementation.

## API changes

### 1. Command + handler

- Contract command: `UnpaySubscriptionCommand(Guid SubscriptionId) : ICommand<Guid>`.
- Handler `UnpaySubscriptionHandler` — ctor `(SubscriptionsDbContext context, ILedgerApi ledger)`
  (no `TimeProvider` needed — reversal uses no clock, mirroring `UnpayCreditorInstallmentHandler`, unless
  the fact-find shows the reversal posting needs a date, in which case add `TimeProvider`). Flow:
  1. Load template; not found → `Subscriptions.SubscriptionNotFound`.
  2. Nothing to undo (current calendar month **not** paid, i.e. no payment to reverse) →
     `Subscriptions.SubscriptionNotPaid` (a fresh conflict code — see error mapping).
  3. Reverse the last pay transaction via the mechanism confirmed in the fact-find. If reversal fails,
     return its error and do not mutate the aggregate.
  4. `RevertLastPayment()` (added in Slice 1) → steps `LastPaidPeriod` back one month (or `null`) and
     `NextDueDate` back one month.
  5. Save; return `template.Id`.
- Register in `SubscriptionsModule.cs`.

### 2. Endpoint

- `POST /v1/subscriptions/{id:guid}/unpay` — route constant in `ApiRoutes.cs`, mapped in
  `EndpointExtensions.cs`, mirroring `UnpayCreditorInstallment.cs`. Return `TypedResults.Ok(...)`
  (reuse `PaySubscriptionResultDto` from Slice 2, matching how `UnpayCreditorInstallment` reuses the pay
  DTO). `.Produces(200).ProducesProblem(404).ProducesProblem(409)`.

### 3. Error mapping (`ErrorHttpStatusHelper.cs`)

- `Subscriptions.SubscriptionNotPaid` does **not** match a suffix rule → add an **explicit** 409 line
  (like `NotACreditorInstallment` had to be added explicitly).

## Client changes

- `subscriptions-service.ts`: add `unpay(id: string): Observable<PaySubscriptionResult>` → `POST
  subscriptions/{id}/unpay`.
- `pages/subscriptions-page/subscriptions-page.html`: per-row **Undo** button, shown when
  `status === 'paid'` (mirrors the creditor Pay/Undo gating). On success reload `listActive()`; errors via
  the existing alert state.

## Testing

**API** (`PersonalFinance.Subscriptions.Tests`, in-memory SQLite + `FakeLedgerApi`):

1. **Undo after pay restores prior state**: seed → Pay (from Slice 2) → Undo. Assert the ledger shows the
   pay reversed (per the confirmed mechanism — e.g. `FakeLedgerApi` recorded a reversal / compensating
   posting), `LastPaidPeriod` and `NextDueDate` are back to their pre-pay values, and status returns to
   **overdue** (or **upcoming**).
2. **Out-of-pocket drops back**: the net ledger effect of Pay + Undo is zero for that subscription's
   expense account (assert via the captured postings, or via `MonthlyExpensesHandler` if wired into the
   test).
3. **Undo with nothing to undo → 409** (`SubscriptionNotPaid`); no reversal posted.
4. **Unknown id → 404**.
5. **Reversal failure**: if `ILedgerApi` reversal fails, the aggregate is **not** mutated and the error
   propagates.

**API contract** (`PersonalFinance.Api.Tests`): `POST /v1/subscriptions/{id}/unpay` present in OpenAPI;
404 problem shape for a missing id.

**Client**:
- `subscriptions-service.spec.ts`: `unpay()` POSTs the right URL.
- `subscriptions-page.spec.ts`: Undo visible only when `paid`; click calls the service and reloads.

## Out of scope

- Bulk undo (there is none — one period at a time, mirroring the creditor decision).
- The Dashboard block (Slice 4).

## Verification

1. `cd app/api && dotnet build`; run `PersonalFinance.Subscriptions.Tests` + `PersonalFinance.Api.Tests`.
2. `cd app/client && pnpm ng lint && pnpm ng build --configuration production`; run client tests.
3. End-to-end: Pay a sub → **Paid**, `X` in out-of-pocket; Undo → back to **Overdue/Upcoming**, the `X`
   is gone from out-of-pocket, `NextDueDate` back one month. Undo again → 409.
4. Slice is green before starting Slice 4.
