# Slice 3 — Partial "Pay full debt" (cross-purchase waterfall, oldest due first)

> Read `00-overview.md`, Slice 1 and Slice 2 first. This slice reuses `CreditorPaymentWaterfall.Allocate`
> (Slice 2), `ApplyPayment` (Slice 1), and the shared dialog. API Phase 50 / client Phase 45.

## Goal

The creditor-level **"Pay full debt"** button now opens the shared dialog in `full-debt` mode
instead of the inline "Settle every remaining cuota…" confirm:

- **Pay in full** behaves exactly like today: it settles every remaining cuota of every purchase, in
  all currencies.
- **Pay a custom amount** fills the creditor's installments across **all purchases, oldest due
  month first** (D2). Tie-break: purchase date, then `Sequence`. Each is paid in full until the
  money runs out, and the next one gets the leftover as a partial.
- **Currency (D12)**: there's no FX conversion, so a custom amount only covers purchases in **one**
  currency. The dialog shows a currency selector **only when the creditor's outstanding purchases
  use more than one currency**. Otherwise the single currency is implicit.

## Why oldest-due-first (intent)

That's how a real creditor would apply a lump sum: they'd clear the oldest debts first. It also
makes the payables "Due now" figure drop first, which is what the user sees. Per-expense ordering
(by `Sequence`) is Slice 2's job. This slice is the same allocator fed with a different ordering.

## API changes

- `PayCreditorFullDebtCommand(Guid CreditorId, long? AmountMinorUnits = null, string? CurrencyCode = null) : ICommand<int>`.
- `PayCreditorFullDebtHandler`:
  - **`AmountMinorUnits == null`**: today's behavior (Slice 1 already converted it to `ApplyPayment(remaining)`),
    covering all currencies. `CurrencyCode` is ignored.
  - **`AmountMinorUnits != null`**:
    - `CurrencyCode` is **required**. Missing or unknown → the existing `InvalidCurrencyCode` error
      from dollar-support (400).
    - Candidates = the creditor's non-reversed installments with remaining > 0 whose plan currency
      matches, ordered by `(DueCycle year, month)`, then `plan.PurchaseDate`, then `Sequence`.
    - `DueCycle` is a computed domain property. If it can't be translated to SQL, load the candidates
      and order in memory. Creditor debt is small.
    - Validate `0 < amount ≤ Σ remaining` (`InvalidPaymentAmount` / `PaymentExceedsRemaining`), then
      `CreditorPaymentWaterfall.Allocate` → `ApplyPayment`. One `SaveChangesAsync`.
    - Returns the newly fully-settled count.
- Host `H/Financing/PayCreditorFullDebt.cs`: body `PayCreditorFullDebtRequestDto(long? AmountMinorUnits, string? CurrencyCode)`.
  The client always sends a body. Update the endpoint description.

## Client changes

- `financing-service.ts`: `payCreditorFullDebt(creditorId, { amountMinorUnits, currencyCode })`,
  with a new type `pay-creditor-full-debt.ts`.
- `creditor-detail-page`:
  - Delete the inline confirm (`confirmingFullDebt` signal + its template block). "Pay full debt"
    now sets `payTarget = { kind: 'full-debt' }`.
  - `outstandingByCurrency` = computed over groups with outstanding > 0, as a
    `{ currencyCode, outstandingMinorUnits }[]`. It feeds the dialog.
- `creditor-pay-dialog` (`full-debt` mode):
  - New optional input `currencies: { currencyCode, outstandingMinorUnits }[]`.
  - If it has more than 1 entry, show a currency `<select>` above the custom amount. The selected
    currency sets the `remaining` used for the max check and the "$X" labels.
  - "Pay in full" copy lists every currency's total (e.g. "Pay everything: $120.000 + US$ 300").
  - `confirm` emits `{ amountMinorUnits, currencyCode }`. For full it's `{ null, null }`.
  - Hint: "Fills the oldest cuotas first, across all purchases."
- Keep the "{n} cuota(s) settled." message.

## Tests

**API** — `PayCreditorFullDebtHandlerTests.cs`:
- The existing 6 facts still pass.
- New facts:
  - partial across two purchases fills the oldest due cuota first, even if it belongs to the newer purchase
  - due-cycle ties broken by purchase date, then sequence
  - leftover lands as a partial on the next cuota
  - only the chosen currency is touched in a mixed ARS/USD creditor
  - amount without currency → `InvalidCurrencyCode`
  - over Σ remaining of that currency → `PaymentExceedsRemaining`, nothing persisted
  - `null` amount still settles all currencies

**Client**:
- dialog spec: currency select hidden with one currency, shown with two; switching currency changes the max
- page spec: the inline confirm is gone; Pay full debt opens the dialog; confirm sends amount + currency; refetch
- service spec: body shape

## Done when

- Everything is green.
- Manually, on a creditor with two purchases: a partial pay settles the oldest-due cuotas first, and payables Due now drops accordingly.
- "Pay in full" still clears everything.

## Doc-sync (step 5)

- Phase 50 (API) / 45 (client)
- DESIGN: full-debt ordering + currency rule
- client DESIGN: the inline confirm was replaced by the dialog
- PRD
