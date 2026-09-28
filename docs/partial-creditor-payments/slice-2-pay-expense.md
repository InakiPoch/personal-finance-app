# Slice 2 — Pay one expense (full or partial, sequence waterfall)

> Read `00-overview.md` and `slice-1-foundation-partial-installment.md` first. Slice 1 must be
> green: this slice relies on `Installment.ApplyPayment`, the payments table, `RemainingMinorUnits`,
> the `CurrencyCode` on `CreditorPurchaseGroup`, and the shared `creditor-pay-dialog`.
> API Phase 49 / client Phase 44.

## Goal

Each purchase group on the creditor detail page gets a **"Pay expense"** button. It opens the
shared dialog in `expense` mode:

- **Pay in full** pays every remaining cuota of that purchase.
- **Pay a custom amount** fills that purchase's installments **in `Sequence` order**. Each is paid in
  full until the money runs out, and the next one gets the leftover as a partial (D2).

Worked example (the user's): a $200.000 plan with 5 × $40.000 cuotas, pay $175.000 → cuotas 1-4
paid, cuota 5 has "$25.000 left · paid $15.000 of $40.000".

This button didn't exist before. The user assumed it did, so it's a new surface (D1).

## Why a pure allocator (intent)

Slice 3's partial "Pay full debt" is the exact same algorithm with a different ordering (oldest
due month across purchases). So write the waterfall **once**, as a pure function over
already-ordered `(installmentId, remaining)` pairs. Each handler owns only its query + ordering.
Being pure also makes it trivially unit-testable with no DB.

## API changes

### Allocator

`F/Application/Commands/CreditorPaymentWaterfall.cs` (internal static):

```csharp
/// Splits an amount across installments in the given order: each takes min(remaining, left).
/// Caller guarantees 0 < amount <= Σ remaining and filters out reversed/fully-paid rows.
internal static IReadOnlyList<(Guid InstallmentId, long AmountMinorUnits)> Allocate(
    IEnumerable<(Guid InstallmentId, long RemainingMinorUnits)> ordered, long amountMinorUnits);
```

It stops as soon as the amount is used up, so installments that get nothing are not in the output.

### Command / handler

- Contracts: `PayCreditorExpenseCommand(Guid PaymentPlanId, long? AmountMinorUnits = null) : ICommand<int>`.
  `null` = pay everything remaining. It returns the count of installments **newly fully settled**
  (the same meaning as `PayCreditorFullDebt`'s `SettledCount`, so the client can reuse its message).
- Handler `F/Application/Commands/PayCreditorExpense/PayCreditorExpenseHandler.cs`, ctor
  `(FinancingDbContext, TimeProvider)`:
  1. Load the plan with `Installments` → `Payments`. Missing → existing `PaymentPlanNotFound` (404).
     A plan whose `CreditorId` is null → existing `NotACreditorInstallment` (409). Reuse it rather
     than adding a near-duplicate code. If the message reads wrong, reword it to cover plans too.
  2. `candidates` = installments with `!IsReversed && RemainingMinorUnits > 0`, ordered by `Sequence`.
     If there are none, it's a no-op success returning 0 (idempotent, like pay-full).
  3. `amount = command.AmountMinorUnits ?? Σ remaining`. `amount <= 0` → `InvalidPaymentAmount`;
     `amount > Σ remaining` → `PaymentExceedsRemaining` (both 400, from Slice 1).
  4. `Allocate(...)` → `ApplyPayment(piece, now)` on each. Count those that became paid.
     `SaveChangesAsync` once, so it's all-or-nothing.
- Host:
  - Route `POST /v1/financing/creditor-purchases/{paymentPlanId}/pay` (add it to `ApiRoutes.cs` next to
    the other creditor routes).
  - Body `PayCreditorExpenseRequestDto(long? AmountMinorUnits)`.
  - Response `PayCreditorExpenseResultDto(int SettledCount)`.
  - Map it in `EndpointExtensions.cs` with summary/description.

## Client changes

- `financing-service.ts`: `payCreditorExpense(planId, { amountMinorUnits }) → Observable<PayCreditorExpenseResult>`,
  with the new types `pay-creditor-expense.ts` / `pay-creditor-expense-result.ts`.
- `creditor-purchases-table`:
  - A **"Pay expense"** button in each group header, shown when `group.outstandingMinorUnits > 0`,
    `[disabled]="paying()"`.
  - New output `payExpenseClick: CreditorPurchaseGroup`.
- `creditor-detail-page`:
  - `payTarget` becomes a small discriminated union: `{ kind: 'installment', row } | { kind: 'expense', group }`.
  - The dialog gets `mode`, `title` (`group.description`), `remainingMinorUnits` (`group.outstandingMinorUnits`)
    and `currency` from the target.
  - `confirm` routes to the right service call and reuses the "{n} cuota(s) settled." message
    (`lastSettledCount`) for the expense case.
- `creditor-pay-dialog`: `expense` mode only changes copy ("Pay the whole expense ($X)" / "Pay part of it").
  Add a one-line hint under the custom input: "Fills cuotas in order; the last one may be partly paid."

## Tests

**API**:

- `CreditorPaymentWaterfallTests.cs` (pure):
  - the user's example (5×40.000, 175.000 → 4 full + 15.000)
  - an exact multiple (80.000 → 2 full, nothing else)
  - a first row already partly paid (remaining 25.000 → takes 25.000 first)
  - amount = Σ → all full
- `PayCreditorExpenseHandlerTests.cs` (in-memory SQLite harness):
  - partial → rows + `PaidOnUtc` on the first N
  - null → everything remaining, count correct
  - over → `PaymentExceedsRemaining`, nothing persisted
  - card plan → `NotACreditorInstallment`
  - unknown → `PaymentPlanNotFound`
  - reversed installments skipped
  - nothing remaining → 0
  - undo (Slice 1) on the partly paid cuota removes only its partial row

**Client**:

- table spec: the button shows only with outstanding > 0, and it emits the group
- page spec: expense confirm calls `payCreditorExpense` with the body, refetches and shows the settled count
- service spec: route + body
- dialog spec: expense-mode copy

## Done when

- Everything is green (API tests, client tests, lint, prod build).
- Manually, on a real 5-cuota creditor plan: a partial pay matches the worked example, and payables Due now / Total move by exactly the amount paid.

## Doc-sync (step 5)

- Phase 49 (API) / 44 (client) in the `CLAUDE.md` + `TASK.md` files
- DESIGN (waterfall allocator + new route)
- PRD (per-expense payment)
