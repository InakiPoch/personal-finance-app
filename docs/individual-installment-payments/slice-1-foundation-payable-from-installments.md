# Slice 1 — Foundation: paid-state + payable-from-installments

> Read [00-overview.md](00-overview.md) first. This is the **first** slice. It installs all the
> plumbing and reshapes the existing `PayStatement` handler, but it deliberately adds **no new
> user-facing payment path** — that is Slice 2.

## Intent

Two things, both prerequisites for individual payment:

1. Give an `Installment` its own **paid state** (`PaidOnUtc` + `MarkPaid`), and make a
   statement's paid status **derived** from its installments.
2. Make `PayStatement` charge the **installment-derived payable**
   (Σ accrued, non-reversed, unpaid) instead of the stored `MonthlyStatement.AmountDue`, and
   mark each settled installment as paid.

Because nothing is payable individually yet, every installment on an open statement is unpaid,
so the payable equals what is charged today — **except** on statements that contain a reversed
cuota, where this is a deliberate correction (see below). This isolates the only risky change
before any new endpoint exists.

## Why the payable must change (keep this — it is the whole point of doing it first)

`MonthlyStatement.AmountDue` is only ever incremented (`MonthlyStatement.cs:30`); reversal only
flips `Installment.IsReversed` (`Installment.cs:49`) and never reduces `AmountDue`. But reversing
an accrued installment **does** reduce the ledger `CardLiability` (storno
`Dr CardLiability / Cr CardPurchases`, `ReverseTransactionHandler.cs:38`). So charging the stored
`AmountDue` on a statement with a reversed cuota over-debits the liability and **overpays from
the bank**. Summing accrued, non-reversed, unpaid installments matches the ledger liability and
fixes this. Precedent: `GetCreditorPayablesHandler` already filters `IsReversed == false` when
summing what is owed — copy that predicate shape.

## API changes (`app/api/`)

All paths under `src/Modules/Financing/PersonalFinance.Financing/` unless noted.

### 1. Domain — `Domain/Installment.cs`
- Add `public DateTimeOffset? PaidOnUtc { get; private set; }`.
- Add `public bool IsPaid => PaidOnUtc is not null;`.
- Add `public Result MarkPaid(DateTimeOffset paidOnUtc)` — guard: if `IsPaid`, return
  `Result.Failure(FinancingErrors.InstallmentAlreadyPaid)`; else set `PaidOnUtc = paidOnUtc`.
  Mirror the shape of the existing `MarkAccrued` / `MarkSplitAccrued` / `MarkReversed`.
- Register `InstallmentAlreadyPaid` in `FinancingErrors` (same file/pattern as
  `InstallmentAlreadyReversed`).

### 2. Domain — `Domain/MonthlyStatement.cs`
- Add a derived helper for full settlement — *fully paid* iff **every accrued, non-reversed
  installment `IsPaid`**. Keep the existing `PaidOnUtc` field as the "fully settled" stamp; it is
  now set by the handler at the moment the last unpaid cuota settles, not treated as the source
  of truth for individual cuotas.
- Do **not** try to keep `AmountDue` in sync with reversals here — leave it as the historical
  accrual total; the payable is computed at pay time from installments.

### 3. Handler — `Application/Commands/PayStatement/PayStatementHandler.cs`
- Load the statement's installments (join `Installment` on `StatementId`, as
  `GetMonthlyStatementHandler` does).
- Compute `payable = Σ installment.Amount where IsAccrued && !IsReversed && !IsPaid`.
- If `payable` is zero (nothing left to pay) → return the existing "already paid" style failure.
- Call `installment.MarkPaid(command.PaidOnUtc)` for each such installment.
- Set the statement's `PaidOnUtc` (all its cuotas are now paid).
- Pass `payable` (not `statement.AmountDue`) into `StatementPaymentCalculator.Build(...)`.

### 4. Calculator — `Application/Commands/PayStatement/StatementPaymentCalculator.cs`
- Net `CarriedCreditBalance` against the **payable** argument. Legs stay identical in shape:
  `Dr CardLiability(payable) / Cr Bank(payable - creditApplied) / Cr CardCredit(creditApplied)`.
- The signature already takes an `amountDue` money argument — pass the payable through it; no new
  parameter needed. (Rename the parameter to `payable` for clarity if you like.)

### 5. Persistence
- Map the new column in the `Installment` EF configuration under
  `Infrastructure/Persistence/Configurations/` (nullable `PaidOnUtc`).
- Add migration `AddInstallmentPaidOnUtc` in `Infrastructure/Persistence/Migrations/`
  (latest existing migration timestamp = `20260906212513`). **Nullable column, no backfill**
  (null = unpaid, which is correct for all historical rows on unpaid statements; rows on already
  fully-paid statements can stay null — statement-level `PaidOnUtc` still marks them settled and
  no code reads installment `PaidOnUtc` for historical paid statements in this slice).

### 6. Query projection — `Application/Queries/GetMonthlyStatement/GetMonthlyStatementHandler.cs`
- Extend the per-installment projection + the DTO row (`MonthlyStatementInstallmentRow`) with
  `IsPaid` and `PaidOnUtc`, so the client can render paid state in Slice 2. (No behavior depends
  on it yet in Slice 1 — this is wiring.)

## Client changes (`app/client/`) — wiring only, no visible change

- `src/app/features/financing/types/monthly-statement-installment.ts`: add
  `isPaid: boolean;` and `paidOnUtc: string | null;` to `MonthlyStatementInstallment` so it keeps
  matching the API DTO. No template change in this slice.

## Tests

`tests/PersonalFinance.Financing.Tests` — reuse the existing in-memory SQLite +
`ThrowingConnectionFactory` harness (see existing `PayStatement`/`GetPendingShares` tests).

1. **Reversal-free parity:** a statement with N unpaid, non-reversed accrued installments →
   `PayStatement` posts the exact same legs/amount as before this slice (assert the
   `PostTransactionCommand` lines).
2. **Reversal correction (the important one):** a statement with one reversed accrued cuota →
   payable equals Σ of the non-reversed unpaid cuotas, i.e. **less** than stored `AmountDue`;
   assert bank is credited that reduced amount (guards the overpay).
3. **Paid stamping:** after `PayStatement`, every settled installment has `PaidOnUtc` set and the
   statement `PaidOnUtc` is set.
4. **Carried-credit netting** still works against the payable (existing behavior, now driven by
   the summed payable).

## Done when

- `dotnet build` green; `dotnet test --project tests/PersonalFinance.Financing.Tests` green
  (run Ledger tests too — the ledger amount path changed).
- Migration applies cleanly on a fresh DB.
- Existing `PayStatement` behavior is unchanged for reversal-free statements; the new reversal
  test proves the corrected charge.
- Client `pnpm ng lint` + `pnpm ng test` stay green (type addition only).

## Notes for the next slice

Slice 2 relies on: `Installment.MarkPaid`, `IsPaid`, the installment-derived payable in
`PayStatement`, and the `isPaid`/`paidOnUtc` fields now flowing through the statement-detail
query. Do not proceed until this slice is green.
