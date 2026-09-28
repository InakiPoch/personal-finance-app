# Slice 1 — Foundation: payment records + partial pay on one installment + undo-last

> Read `00-overview.md` first (current model, decisions D1–D14, cadence, gotchas).
> API Phase 48 / client Phase 43. This slice carries the whole data-model change. Slices 2–4
> only add new ways of *creating* payment rows.

## Goal

Pressing **Pay** on a creditor installment opens a centered dialog over a dimmed background with two
choices: **Pay in full** (pays what's remaining) or **Pay a custom amount** (0 < x ≤ remaining).
A partial payment leaves the installment unpaid, and the row shows "$X left · paid $Y of $Z".
**Undo** removes the last payment. Every creditor figure (detail groups, payables Due now /
Total / per-account) is computed from *remaining* amounts.

## Why a payments table (intent)

A single "paid amount" column can't do D4 (undo *the last* payment), and Slice 4 needs to
remember *which* payment came from a party and which ledger settlement to reverse. A row per
payment gives both. `Installment.PaidOnUtc` stays, but its meaning narrows to **"fully paid"**
(D11). That way, card installments (which also use it) and every existing `IsPaid` reader keep
working unchanged.

## API changes

### Domain

**New entity** `F/Domain/CreditorInstallmentPayment.cs`:

```csharp
public sealed class CreditorInstallmentPayment {
    public Guid Id { get; private set; }
    public Guid InstallmentId { get; private set; }
    public long AmountMinorUnits { get; private set; }
    public DateTimeOffset PaidOnUtc { get; private set; }
    public Guid? PartyId { get; private set; }              // Slice 4
    public Guid? SettlementTransactionId { get; private set; } // Slice 4
    // internal factory, EF ctor — mirror the style of the other Financing entities
}
```

The payment has no currency column: it always uses the installment's currency.

**`F/Domain/Installment.cs`** gets a `Payments` collection (private backing list, read-only view,
the same pattern `PaymentPlan` uses for `Installments`) and three members:

- `long PaidMinorUnits` = Σ `Payments.AmountMinorUnits`. `long RemainingMinorUnits` = `AmountMinorUnits - PaidMinorUnits`.
- `Result<CreditorInstallmentPayment> ApplyPayment(long amountMinorUnits, DateTimeOffset now, Guid? partyId = null, Guid? settlementTransactionId = null)`:
  - `IsReversed` → `InstallmentAlreadyReversed`; `RemainingMinorUnits == 0` → `InstallmentAlreadyPaid`.
  - `amount <= 0` → **new** `FinancingErrors.InvalidPaymentAmount` (400).
  - `amount > RemainingMinorUnits` → **new** `FinancingErrors.PaymentExceedsRemaining` (400). Never capped (D3).
  - Adds the row. If `RemainingMinorUnits` is now 0, set `PaidOnUtc = now` (via the existing `MarkPaid`).
- `Result<CreditorInstallmentPayment> UndoLastPayment()`:
  - With no payments → **new** `FinancingErrors.NoPaymentToUndo` (409). The user-visible behavior
    change from today's no-op success is intentional: the button only shows when payments exist.
  - Removes the row with the latest `PaidOnUtc` (tie-break: `Id`, which is a v7 Guid, so it's time-ordered).
  - If `PaidOnUtc` was set, clear it (existing `ClearPayment`).
  - Returns the removed row. Slice 4 needs its `SettlementTransactionId`.

Keep the old `MarkPaid` / `ClearPayment`. The card handlers still use them.

### Persistence

- New `CreditorInstallmentPaymentConfiguration.cs` → table `financing_creditor_installment_payments`,
  FK `InstallmentId` → `financing_installments` (cascade), index on `InstallmentId`.
  In `InstallmentConfiguration.cs`, wire `HasMany(i => i.Payments).WithOne().HasForeignKey(p => p.InstallmentId)`.
  Match how `PaymentPlanConfiguration` maps `Installments` (backing field access mode).
- Migration `AddCreditorInstallmentPayments`:
  - Create the table.
  - **Backfill** in `Up` with hand-written SQL: one row per installment that has `PaidOnUtc IS NOT NULL`
    and whose plan has `CreditorId IS NOT NULL`, with `AmountMinorUnits = installment.AmountMinorUnits`
    and `PaidOnUtc = installment.PaidOnUtc`, and with `PartyId` / `SettlementTransactionId` NULL.
    Generate the Guid in SQL. **First check how EF stores Guids in this SQLite DB** (look at an existing
    row: uppercase TEXT `XXXXXXXX-XXXX-…`) and format `hex(randomblob(16))` with `substr` to match.
  - `Down` drops the table.
  - Then run `dotnet ef database update` manually (see gotchas).

### Every writer of a creditor `PaidOnUtc` must go through `ApplyPayment`

Otherwise "remaining" (computed from rows) and "paid" (the stamp) drift apart. There are exactly three:

| File | Today | Change |
|---|---|---|
| `F/Application/Commands/PayCreditorInstallment/PayCreditorInstallmentHandler.cs` (~l.39) | `MarkPaid(now)` | `ApplyPayment(command.AmountMinorUnits ?? installment.RemainingMinorUnits, now)` |
| `F/Application/Commands/PayCreditorFullDebt/PayCreditorFullDebtHandler.cs` (~l.40) | `MarkPaid(now)` per unpaid | `ApplyPayment(installment.RemainingMinorUnits, now)` per installment with remaining > 0. The count still means "newly fully settled". |
| `F/Application/Commands/CreatePaymentPlan/CreatePaymentPlanHandler.cs` (~l.118, back-dated creditor stamp) | `MarkPaid(cutoffInstant)` | `ApplyPayment(installment.AmountMinorUnits, cutoffInstant)` |

The card-side `MarkPaid` calls (`PayInstallmentHandler`, `PayStatementHandler`, the card branch of
`CreatePaymentPlanHandler` ~l.185) stay **untouched**.

All loads of creditor installments in those handlers need `.Include(i => i.Payments)`.

### Commands / endpoints

- `PayCreditorInstallmentCommand(Guid InstallmentId, long? AmountMinorUnits = null)`. `null` = pay remaining.
- `UnpayCreditorInstallmentCommand` keeps its name and route. The handler now calls `UndoLastPayment()`.
- Host `H/Financing/PayCreditorInstallment.cs`: accepts body `PayCreditorInstallmentRequestDto(long? AmountMinorUnits)`.
  The client always sends a body (`{ "amountMinorUnits": null }` for full), so there's no empty-body special case.
- `H/ErrorHttpStatusHelper.cs`: `InvalidPaymentAmount` / `PaymentExceedsRemaining` → 400, `NoPaymentToUndo` → 409.
- Update the endpoint `.WithDescription(...)` texts in `H/EndpointExtensions.cs`. They still say
  "display-only stamp". The GET creditor-payables description is already stale (it says "no per-installment
  paid flag"), so fix it too.

### Read queries (everything on remaining)

- **`GetCreditorDetailHandler`**: project each installment's paid sum as a correlated subquery
  (`context.Set<CreditorInstallmentPayment>().Where(p => p.InstallmentId == i.Id).Sum(p => (long?)p.AmountMinorUnits) ?? 0`).
  - Add `PaidMinorUnits`, `RemainingMinorUnits` and `HasPayments` to the `CreditorInstallmentRow`
    contract + `CreditorDetailDto` mapping (`H/Mapping/FinancingMappingExtensions.cs`).
  - Group `Outstanding` = Σ remaining of non-reversed rows.
  - Status logic is unchanged (paid only when fully paid, D5).
  - **Add `string CurrencyCode` to `CreditorPurchaseGroup`** (from `plan.Currency`). Today neither the
    creditor detail nor the payables contract carries a currency, and the client formats everything
    with `formatArs`, even though creditor plans can be USD since dollar-support. The dialog needs
    the currency, and Slice 3's currency selector depends on it. The client switches the detail page
    to `formatMoney(value, group.currencyCode)`.
  - **Out of scope (known limitation, leave it):** `GetCreditorPayablesHandler` sums a creditor's ARS
    and USD installments into one number. Fixing that means a per-currency payables row, which is its
    own change. Note it in the doc-sync as a follow-up.
- **`GetCreditorPayablesHandler`**: `TotalOwed` = Σ remaining, and `DueNow` = Σ remaining where
  `DueCycle ≤ current`. **D14 fix:** the per-account breakdown (~l.62-68) must also sum remaining.
  Today it sums every non-reversed installment, paid included.
  - `NextDueDate` currently picks the earliest installment *including paid ones*. Change it to the
    earliest installment with remaining > 0. It's the same class of bug, and it becomes visible once
    partials exist.

## Client changes

Paths under `C/features/financing/`.

- **Types**:
  - `types/creditor-installment-row.ts` adds `paidMinorUnits: Money`, `remainingMinorUnits: Money` and `hasPayments: boolean`.
  - The `CreditorPurchaseGroup` type adds `currencyCode: CurrencyCode`.
  - New `types/pay-creditor-installment.ts` → `{ amountMinorUnits: Money | null }`.
- **`financing-service.ts`**: `payCreditorInstallment(installmentId, body: PayCreditorInstallment)`
  posts `body` instead of `{}`.
- **New shared component** `components/creditor-pay-dialog/` (`.ts/.html/.spec.ts`):
  - Native `<dialog>`, opened with `showModal()` via a `viewChild` + `effect` on an `open` input. The
    browser handles the dimmed `::backdrop`, focus trap and Esc. Close on Esc/backdrop click → `cancel` output.
  - Inputs:
    - `mode: 'installment' | 'expense' | 'full-debt'` (only `installment` in this slice, but model the union now since the next two slices need it)
    - `title` (e.g. "Cuota 3/5 · Store X")
    - `remainingMinorUnits: Money`
    - `currency`
    - `busy`
    - `error: string | null`
  - Body:
    - two radio choices, "Pay in full ($remaining)" and "Pay a custom amount"
    - the custom choice reveals the amount input (the same markup as `load-expense-page` amount: serif `$`, `type="number" step="0.01" inputmode="decimal"`)
    - validators `positiveAmount`, `atMostTwoDecimals`, plus a local `max(remaining)` check with the message "Can't exceed $X remaining"
  - Output `confirm: { amountMinorUnits: Money | null }` (`null` = full), converted with `toMinorUnits`.
  - Styling: panel classes from SYSTEM.md, centered; `::backdrop` = ink at ~40% opacity.
  - Add a short **Dialog** entry to `app/client/docs/SYSTEM.md`.
- **`creditor-purchases-table`**:
  - Pay shows while `!isReversed && remainingMinorUnits > 0`; Undo shows while `hasPayments && !isReversed`.
  - The amount cell for a row with `paidMinorUnits > 0 && remaining > 0` becomes "$remaining left" plus
    a muted sub-line "paid $paid of $amount".
  - Emit the whole row on `payClick` (the page needs remaining and label for the dialog), not just the id.
- **`creditor-detail-page`**:
  - Pay opens the dialog (a signal `payTarget: CreditorInstallmentRow | null`).
  - `confirm` → `payCreditorInstallment(id, { amountMinorUnits })` via the existing `runMutation` → close dialog → refetch.
  - Map the new error codes in `payErrorText()`.
  - Leave the "Pay full debt" inline confirm alone in this slice. Slice 3 moves it into the dialog.

## Tests

**API** (`app/api/tests/PersonalFinance.Financing.Tests/`, in-memory SQLite + `EnsureCreated` +
fixed `TimeProvider`, harness copied from `PayCreditorInstallmentHandlerTests.cs`):

- Domain `Installment`:
  - partial → remaining drops, not paid
  - exact remainder → `PaidOnUtc` set
  - over → `PaymentExceedsRemaining`
  - 0/negative → `InvalidPaymentAmount`
  - reversed → `InstallmentAlreadyReversed`
  - undo last removes the newest row and clears `PaidOnUtc`
  - undo with none → `NoPaymentToUndo`
- `PayCreditorInstallmentHandler`:
  - null amount pays remaining after an earlier partial
  - partial then full
  - the existing 10 facts still green (adjust unpay-no-op → `NoPaymentToUndo`)
- `PayCreditorFullDebtHandler`: a partly paid installment gets exactly its remaining; the count is correct.
- `CreatePaymentPlanHandler` back-dated creditor plan: stamped installments have one full payment row each.
- `GetCreditorDetailHandler` / `GetCreditorPayablesHandler`:
  - partial reflected in paid/remaining/outstanding/DueNow/Total
  - **per-account breakdown excludes paid amounts** (D14 regression test)
  - `NextDueDate` skips fully paid

**Client** (Jasmine/TestBed zoneless, spies returning `of(...)`):

- `creditor-pay-dialog.spec.ts`:
  - full → emits `null`
  - custom → emits minor units
  - over-remaining/zero/3-decimals invalid, confirm disabled
  - Esc/cancel emits `cancel`
  - Stub `HTMLDialogElement.prototype.showModal` if the headless browser lacks it (Brave has it)
- `creditor-purchases-table.spec.ts`: partial row renders "left / paid of"; Undo visible on a partly paid row.
- `creditor-detail-page.spec.ts`: Pay opens the dialog; confirm calls the service with the body and refetches; error mapped.
- `financing-service.spec.ts`: the pay call posts the body.

## Done when

- API tests green and client `ng test` + `ng lint` + prod build clean.
- The dev DB is migrated, and previously paid creditor installments still show paid and can be undone.
- Manually:
  - pay $15.000 of a $40.000 cuota → "$25.000 left", and payables Due now drops by 15.000
  - Undo → back to $40.000
  - Pay in full → paid

## Doc-sync (step 5)

- `app/api/.claude/CLAUDE.md` + `TASK.md` → Phase 48
- `app/client/.claude/CLAUDE.md` + `TASK.md` → Phase 43
- api `docs/DESIGN.md` (creditor payment model: display-only rows, PaidOnUtc = fully paid)
- client `docs/DESIGN.md` + `docs/SYSTEM.md` (Dialog)
- both PRDs (partial creditor payments)
