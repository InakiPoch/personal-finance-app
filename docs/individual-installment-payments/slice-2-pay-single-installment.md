# Slice 2 — Pay a single installment

> Read [00-overview.md](00-overview.md) and finish [slice-1](slice-1-foundation-payable-from-installments.md)
> (green) first. This slice delivers the **headline feature**: settle one purchase's cuota,
> leave the rest owed.

## Intent

Add a dedicated "pay this installment" path end-to-end. Because Slice 1 already made
`PayStatement` charge only the unpaid, non-reversed installments, there is **no double-pay
window**: after paying a cuota individually, "Pay full statement" charges only the remaining
cuotas.

## Ledger shape (Option A — confirmed)

An individual cuota payment posts a **plain**:

```
Dr CardLiability(installment.Amount)
Cr Bank(installment.Amount)
```

**No carried-credit netting** on this path — carried credit is a card-level pool and stays on
the full-statement path only. Do not touch `StatementPaymentCalculator` here.

## API changes (`app/api/`)

### 1. Contracts — `PersonalFinance.Financing.Contracts/Commands/PayInstallmentCommand.cs`
```csharp
public sealed record PayInstallmentCommand(Guid InstallmentId, Guid BankAccountId, DateTimeOffset PaidOnUtc) : ICommand<Guid>;
```

### 2. Handler — `Application/Commands/PayInstallment/PayInstallmentHandler.cs` (+ `PayInstallmentValidator.cs`)
- Validator: `InstallmentId` and `BankAccountId` not `Guid.Empty` (mirror `PayStatementValidator`).
- Handler flow:
  1. Load the installment (with its parent plan's `CardId` and the statement it accrued into).
  2. Guard: must be `IsAccrued && !IsReversed && !IsPaid`, else a domain failure that maps to
     409 (not-accrued / reversed / already-paid). Not found → 404.
  3. Resolve the card's `LiabilityAccountId`.
  4. Post `Dr CardLiability(amount) / Cr Bank(amount)` via
     `ILedgerApi.PostTransactionAsync(new PostTransactionCommand(lines, command.PaidOnUtc), ...)`
     using `PostTransactionLine(accountId, DebitOrCredit, Money)`.
  5. `installment.MarkPaid(command.PaidOnUtc)`.
  6. If, after this, **all** the statement's accrued non-reversed installments are paid → set the
     statement's `PaidOnUtc` (reuse the derived helper added in Slice 1).
  7. Save; return `installment.Id`.

### 3. Host — `src/Bootstrap/PersonalFinance.Api/`
- `Endpoints/ApiRoutes.cs`: add
  `public const string InstallmentPayment = "/installments/{id:guid}/pay";`
  (no collision — distinct literal segment).
- `Endpoints/Financing/PayInstallment.cs`: `POST /v1/financing/installments/{id}/pay`, returns
  `201 Created` + `{ installmentId }`; `ProblemDetails` on 404/409. Mirror
  `Endpoints/Financing/PayStatement.cs`.
- DTOs (`Endpoints/Financing/DTOs/`): `PayInstallmentDto(Guid BankAccountId, DateTimeOffset PaidOnUtc)`
  and `PayInstallmentResultDto(Guid InstallmentId)`.
- `Endpoints/Mapping/FinancingMappingExtensions.cs`: DTO → command, id → result DTO (mirror the
  `PayStatement` mappings).

## Client changes (`app/client/`)

All under `src/app/features/financing/` unless noted.

### 1. Service — `financing-service.ts`
- Add `payInstallment(id: string, body: PayInstallment): Observable<PayInstallmentResult>` →
  `POST /v1/financing/installments/{id}/pay`. Mirror the existing `payStatement`.

### 2. Types — `types/`
- `pay-installment.ts`: `PayInstallment { bankAccountId: string; paidOnUtc: string; }`.
- `pay-installment-result.ts`: `PayInstallmentResult { installmentId: string; }`.
  (Mirror `pay-statement.ts` / `pay-statement-result.ts`.)

### 3. Statement page — `pages/statement-page/statement-page.{ts,html}`
- Keep the current bank-account + paid-on-date form fields, but treat them as **one shared
  selector** used by both actions.
- Relabel the existing "Record payment" button → **"Pay full statement."** Its handler still
  calls `payStatement`.
- Pass the shared bank/date down to `InstallmentsTable` (or emit an event up) so a per-row Pay
  can reuse them. On any successful payment, re-fetch the statement (`getStatement`) so the UI
  reflects the new paid state.

### 4. Installments table — `pages/statement-page/.../installments-table.{ts,html}`
- Add a per-row **Pay** button. Hide/disable it when the row `isPaid` or `isReversed`.
- Render paid rows distinctly (e.g. a "Paid" chip like the existing "Reversed" state).
- On click, emit the `installmentId`; the page calls `payInstallment` with the shared bank/date.
- Money via `core/money/money.ts` (`formatArs` for display, `toMinorUnits` for any input) — never
  floats.

## Tests

### API (`tests/PersonalFinance.Financing.Tests`)
- Pay one cuota → only that installment gets `PaidOnUtc`; statement **not** fully paid; ledger
  posts exactly `Dr CardLiability(amount) / Cr Bank(amount)` (no credit leg).
- After paying one cuota individually, `PayStatement` charges only the **remaining** cuotas
  (assert amount = Σ still-unpaid) — proves no double-pay.
- Paying the last unpaid cuota sets the statement's `PaidOnUtc`.
- Guards: paying an already-paid / reversed / not-yet-accrued cuota → 409; unknown id → 404.

### Client (`CHROME_BIN=/usr/bin/brave`)
- Service issues the correct POST body/URL.
- Per-row Pay button visibility by `isPaid` / `isReversed`.
- "Pay full statement" relabel present; both actions reuse the shared bank/date.

## Done when

- API: `dotnet build` + `dotnet test --project tests/PersonalFinance.Financing.Tests` (+ Ledger)
  green.
- Client: `pnpm ng lint` + `pnpm ng test --watch=false --browsers=ChromeHeadless` green.
- **Manual E2E:** create a multi-cuota card purchase, let a cuota accrue into a statement, open
  the statement, pay that one cuota → it shows Paid and the statement's owed total drops by that
  amount; the other cuotas remain owed; "Pay full statement" then settles only the rest.
