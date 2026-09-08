# Slice 3 — Pay a creditor cuota (display-only) + undo

> Read `00-overview.md`, Slice 1, Slice 2 first. This is the headline write feature. **Payment is
> a display-only `PaidOnUtc` stamp — no bank account, no ledger posting.** See `00-overview.md`
> Q1 for the full rationale; do not reintroduce a bank/ledger leg.

## Goal

Let the user settle **one** creditor installment from the Slice-2 detail page, and undo that
settlement (fat-finger recovery). Paying stamps `Installment.PaidOnUtc = now`; undo nulls it. Both
immediately move the Slice-1 figures (`DueNow` / `TotalOwed` drop / restore) and the Slice-2 status
badges.

## Why a new command, not an extension of `PayInstallment` (intent)

`PayInstallmentHandler` (card path) requires a `BankAccountId`, loads a `MonthlyStatement` + a
`CreditCard`, posts `Dr CardLiability / Cr Bank` to the ledger, and **explicitly rejects** anything
with a null `StatementId` (`InstallmentNotAccrued`, 409) — which is *every* creditor installment.
Creditor pay shares none of that machinery: no bank, no statement, no ledger. Forcing the two into
one handler would mean branching every step. A dedicated `PayCreditorInstallment` is a clean
guard → stamp → save, and keeps the card path untouched. This is the display-only mirror of Phase
32's `stampBackdatedCreditorInstallments`, now user-invokable.

Undo is trivial precisely *because* payment is display-only: there is no ledger transaction to
storno, so "unpay" just clears the stamp. That's why Q5 folds it in here rather than being its own
slice.

## API changes

No schema change, no EF migration (the `PaidOnUtc` column exists), no new module edge — the change
is a new Contracts command pair + handlers + host endpoints, all inside Financing + host. RNF-9
unaffected.

**Domain** — `Domain/Installment.cs`: add a `ClearPayment()` method (the inverse of the existing
`MarkPaid`) that sets `PaidOnUtc = null`. Keep it minimal and guard-symmetric with `MarkPaid`
(e.g. a no-op or a `Result` if already unpaid — match the codebase's method style; `MarkReversed`
/ `MarkPaid` are the templates). Place it per member-ordering rules.

**Pay command** — `…/Contracts/Commands/PayCreditorInstallmentCommand.cs`:

```csharp
public sealed record PayCreditorInstallmentCommand(Guid InstallmentId) : ICommand<Guid>;
```

No `BankAccountId`, no `PaidOnUtc` (stamp = now, one-click — see Q1). If you later want the user to
record a historical paid date, add an optional `DateTimeOffset? PaidOnUtc` then; not now.

**Pay handler** — `Application/Commands/PayCreditorInstallment/PayCreditorInstallmentHandler.cs`,
ctor `(FinancingDbContext context, TimeProvider timeProvider)` (no `ILedgerApi`):

1. Load the installment (`context.Set<Installment>()`, tracked). Null → `InstallmentNotFound` (404).
2. **Verify it is a creditor installment**: join to its `PaymentPlan` and require
   `plan.CreditorId != null`. If it belongs to a card plan → new
   `FinancingErrors.NotACreditorInstallment` (409). (This is the mirror of the card handler's
   `InstallmentNotAccrued` guard, from the other side.)
3. Guards (both 409): `IsReversed` → `InstallmentAlreadyReversed`; `IsPaid` →
   `InstallmentAlreadyPaid`. (Reuse the existing `FinancingErrors` codes — they already map to 409.)
4. `installment.MarkPaid(DateOnly? … )` — stamp with
   `timeProvider.GetUtcNow()`; `SaveChangesAsync`; return `installment.Id`.

**Undo command** — `…/Contracts/Commands/UnpayCreditorInstallmentCommand.cs`:

```csharp
public sealed record UnpayCreditorInstallmentCommand(Guid InstallmentId) : ICommand<Guid>;
```

**Undo handler** — `Application/Commands/UnpayCreditorInstallment/UnpayCreditorInstallmentHandler.cs`,
ctor `(FinancingDbContext context)`:

1. Load installment; null → `InstallmentNotFound` (404).
2. Require creditor plan (`plan.CreditorId != null`) → else `NotACreditorInstallment` (409).
3. `IsReversed` → `InstallmentAlreadyReversed` (409). If `!IsPaid`, treat as a no-op success (or a
   dedicated `InstallmentNotPaid` 409 — pick one and note it; a no-op is friendlier for a
   double-click). `installment.ClearPayment()`; `SaveChangesAsync`; return `installment.Id`.

**Validators** — mirror `PayInstallmentValidator`: empty `InstallmentId` → `InstallmentNotFound`.

**Errors + HTTP mapping** — `Domain/FinancingErrors.cs`: add `NotACreditorInstallment`
(`"Financing.NotACreditorInstallment"`). `Endpoints/ErrorHttpStatusHelper.cs`: add an explicit
**409** entry for `NotACreditorInstallment` (it matches no suffix heuristic, so it would fall to
400 without the line — same load-bearing lesson as `InstallmentNotAccrued` in Phase 29).
`InstallmentAlreadyPaid` / `InstallmentAlreadyReversed` already resolve to 409; `InstallmentNotFound`
to 404.

**Host** — two endpoints, mirroring `Endpoints/Financing/PayInstallment.cs`:
- `ApiRoutes.Financing.CreditorInstallmentPayment = "/creditor-installments/{id:guid}/pay"` and
  `CreditorInstallmentUnpayment = "/creditor-installments/{id:guid}/unpay"`.
- `Endpoints/Financing/PayCreditorInstallment.cs` + `UnpayCreditorInstallment.cs` — thin: map id →
  command, `commandBus.SendAsync<Guid>`, on failure `ProblemResultsHelper.From(result.Error)`, else
  `TypedResults.Ok(...)` (or `Created` for pay — pick `Ok`, since no resource is created, just a
  state change; note the choice). Request bodies are empty (no bank account, no date), so the DTO is
  just a result: `PayCreditorInstallmentResultDto(Guid InstallmentId)`.
- Register both handlers in `FinancingModule.Register`. Wire endpoints in
  `EndpointExtensions.MapFinancingEndpoints` with `.Produces<…ResultDto>(200)` + `.ProducesProblem`
  404/409, tag `"Financing"`.

## Client changes

All under `app/client/src/app/features/financing/`.

1. **Types**: `pay-creditor-installment-result.ts` (`{ installmentId: string }`). No request body
   type needed (empty POST).
2. **Service** `financing-service.ts`:
   `payCreditorInstallment(id): Observable<PayCreditorInstallmentResult>` →
   `POST financing/creditor-installments/${id}/pay` (empty body `{}`), and
   `unpayCreditorInstallment(id): Observable<…>` → `.../unpay`.
3. **Detail page** (Slice 2's `creditor-detail-page`): add per-installment **Pay** button (shown
   when `!isPaid && !isReversed`) and **Undo** on paid rows, mirroring `installments-table`'s
   per-row emit-up pattern — the presentational table emits `payClick(installmentId)` /
   `undoClick(installmentId)`, the container calls the service and re-fetches `creditorDetail(id)`
   on success (which refreshes status badges and the group outstanding). **No form, no bank
   selector** — a creditor pay takes no input. Disable buttons while a request is in flight
   (`paying` input signal, same as the card table). Add a "Paid" badge (reuse the green
   `installments__badge` treatment).
4. **Error copy**: map `Financing.NotACreditorInstallment`, `Financing.InstallmentAlreadyPaid`,
   `Financing.InstallmentAlreadyReversed`, `Financing.InstallmentNotFound` to friendly messages
   (key off `AppError.code`).

## Testing

**API** — `tests/PersonalFinance.Financing.Tests/PayCreditorInstallmentHandlerTests.cs` (in-memory
SQLite + `FixedTimeProvider`, seed a creditor plan via `SeedCreditorAsync` — the harness already
has it):

- Pays a creditor cuota → `PaidOnUtc` stamped to the clock, no ledger post (assert the
  `FakeLedgerApi.PostedTransactions` is untouched — or simpler, the handler takes no `ILedgerApi`
  so there's nothing to post).
- Rejects a **card** installment → `NotACreditorInstallment` (409).
- Rejects an already-paid cuota → `InstallmentAlreadyPaid`; a reversed cuota →
  `InstallmentAlreadyReversed`; an unknown id → `InstallmentNotFound`.
- **Undo** clears `PaidOnUtc`; unknown id → `InstallmentNotFound`; a card installment →
  `NotACreditorInstallment`.
- Round-trip: pay then undo → back to unpaid; the Slice-1 `GetCreditorPayables` figures reflect
  both (optional cross-check).

**Client** — `financing-service.spec.ts` (+2: pay/unpay URL + verb + empty body); detail-page /
table specs: Pay shown/hidden by status, emits the id, Undo on paid rows, `paying` disables
buttons, refetch on success.

## Verification (before green-lighting Slice 4)

- API: build Release, run the Financing binary (`-class "*PayCreditorInstallment*"`) + full suite
  green; RNF-9 green.
- Client: lint, test, prod build clean.
- Manual (handed to user): on a creditor detail page, Pay one cuota → its badge flips to Paid, the
  group outstanding and the list `DueNow`/`TotalOwed` drop by that amount; Undo → everything
  restores. No bank balance anywhere moves (display-only, by design).

## Out of scope (Slice 4)

Bulk "Pay full debt". This slice is one cuota at a time (+ undo).
