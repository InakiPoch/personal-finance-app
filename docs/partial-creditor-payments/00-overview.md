# Partial creditor payments — overview

> Planning docs only (written 2026-09-28, branch `feat/partial-creditor-payments`). No code yet.
> Read this file first, then the slices **in order**. Each slice is a vertical tracer bullet
> (API + client + tests) and must be green before the next one starts.

## The problem

Creditor-financed expenses (a person/store lends you the money, you repay in cuotas) can
today only be paid **all-or-nothing**:

- **Pay** on one installment → stamps `Installment.PaidOnUtc` (display-only).
- **Pay full debt** on a creditor → stamps every unpaid installment of every purchase.

Real life is messier: you often hand the creditor *part* of a cuota, or a lump sum that
covers several cuotas and a bit of the next one, or a co-borrower (a *party*) gives you
their share and you pass it on. This initiative adds partial payments everywhere a
creditor can be paid, plus a new per-expense pay action.

## Current model (facts, verified 2026-09-28)

Paths: `F/` = `app/api/src/Modules/Financing/PersonalFinance.Financing`,
`H/` = `app/api/src/Bootstrap/PersonalFinance.Api/Endpoints`, `C/` = `app/client/src/app`.

**Creditor payment is display-only.** No bank account, no ledger posting, no integration
event. Only a timestamp.

- `F/Domain/Installment.cs` — `AmountMinorUnits` + `Currency`, `PaidOnUtc`, `IsPaid`
  (`PaidOnUtc != null`), `IsReversed`, `SplitAccruedOnUtc` / `IsSplitAccrued`, `DueCycle`.
  `MarkPaid(ts)` fails with `InstallmentAlreadyPaid` if already paid, and `ClearPayment()` nulls
  the stamp. **Card installments use the same `PaidOnUtc`.** Do not break the card side.
- `F/Domain/PaymentPlan.cs` — `CreditorId?`, `TotalMinorUnits`, `InstallmentCount`,
  `Description`, `PurchaseDate`, `SplitParticipants` (`PartyId`, `Weight`,
  `ReceivableAccountId`), `CreditorPayableAccountId?`. `CreditorCutoffDay = 26`.
- Commands (Contracts `…/Financing.Contracts/Commands/`):
  - `PayCreditorInstallmentCommand(Guid InstallmentId) : ICommand<Guid>`
  - `UnpayCreditorInstallmentCommand(Guid InstallmentId) : ICommand<Guid>`. It clears the stamp and
    is a no-op success on an unpaid installment.
  - `PayCreditorFullDebtCommand(Guid CreditorId) : ICommand<int>`. It returns the number newly
    settled and is idempotent.
- Handlers: `F/Application/Commands/{PayCreditorInstallment,UnpayCreditorInstallment,PayCreditorFullDebt}/`.
- Errors: `F/Domain/FinancingErrors.cs` — `InstallmentNotFound` (404),
  `InstallmentAlreadyReversed` / `InstallmentAlreadyPaid` / `NotACreditorInstallment` (409),
  `CreditorNotFound` (404). HTTP mapping: `H/ErrorHttpStatusHelper.cs`.
- Routes (`H/ApiRoutes.cs`, mapped in `H/EndpointExtensions.cs`), base `/v1/financing`:
  - `GET /creditor-payables` returns `CreditorPayablesDto`.
  - `GET /creditor-payables/{creditorId}` returns `CreditorDetailDto`.
  - `POST /creditor-installments/{id}/pay` and `/unpay`, with **no body**.
  - `POST /creditor-payables/{creditorId}/pay-full`, with **no body**.
- Read queries (LINQ, no SQL view):
  - `F/Application/Queries/GetCreditorPayables/GetCreditorPayablesHandler.cs`: `TotalOwed` and
    `DueNow` are based on `PaidOnUtc is null` (~l.45). **Known bug:** the per-account breakdown sums
    all non-reversed installments, *paid ones included*. It's fixed in Slice 1.
  - `F/Application/Queries/GetCreditorDetail/GetCreditorDetailHandler.cs`: groups by purchase.
    `Outstanding` = Σ unpaid (~l.68). Status is reversed / paid / overdue / due / future.
- **No partial-payment concept exists anywhere** (no paid-amount column, no payment records).

**Parties and creditor splits.**

- The split is created with the plan: `CreatePaymentPlanHandler` → `PaymentPlanCreatedIntegrationEvent`
  → Parties `OnPaymentPlanCreated` → sync `IFinancingApi.LinkSplitAsync`.
- **Receivables accrue per installment, not up front.** The scheduler
  `F/Application/Scheduling/AccrueInstallments.cs` → `accrueDueCreditorSplitReceivablesAsync`
  posts `Dr Receivable_party / Cr CreditorPayable` when an installment's due month arrives,
  then sets `SplitAccruedOnUtc` and calls `IPartiesApi.RecordSplitAccrualAsync`. (An older memory note
  said "full receivable up front". The code says per installment. Trust the code.)
- Per-installment party share = `PhantomPennyAllocator` over weights `[1 (holder), w1..wn]`, with
  participants ordered by `PartyId`. Reference: `CreditorSplitReceivableCalculator.BuildLines`
  (`F/Application/Commands/LinkPaymentPlanSplit/`). A plan may have **several** parties.
- A party paying you today goes through `POST /v1/parties/{id}/settlements` →
  `SettleCurrentAccountHandler` → `Dr Bank / Cr Receivable`, rejected with
  `SettlementExceedsBalance` if over the party's accrued balance. **It returns the ledger
  transaction id.** It's exposed on `IPartiesApi.SettleCurrentAccountAsync`. There is **no
  settlement-reversal command**, but `ILedgerApi.ReverseTransactionAsync(ReverseTransactionCommand(Guid OriginalTransactionId, DateTimeOffset ReversedOnUtc))`
  exists and has a global double-reversal guard (`Ledger.TransactionAlreadyReversed`, 409).
- Financing already depends on `IPartiesApi` (see `AccrueInstallments.cs`), so no new module edge
  is needed.

**Client.**

- `C/features/financing/pages/creditor-detail-page/`:
  - `creditor-detail-page.{ts,html}` — `onPay` / `onUndo` go through `runMutation`, and every
    mutation is followed by a **refetch** via `loadDetail`. "Pay full debt" uses an inline confirm
    (`confirmingFullDebt` signal).
  - `creditor-purchases-table.{ts,html}` — Pay/Undo buttons per row, with outputs
    `payClick` / `undoClick` (installmentId).
- `C/features/financing/financing-service.ts` (~l.70-82) — `creditorDetail`, `payCreditorInstallment`,
  `unpayCreditorInstallment`, `payCreditorFullDebt`, all posting `{}`.
- **No modal/dialog exists, and neither CDK nor Material is installed.** Earlier docs chose inline
  confirms on purpose. This initiative introduces the first dialog, using the **native `<dialog>`**
  element.
- Money input helpers: `C/core/money/money.ts` (`toMinorUnits`, `fromMinorUnits`, `formatMoney`),
  validators `C/features/financing/validation-helpers.ts` (`positiveAmount`, `atMostTwoDecimals`).
  Reference form: `load-expense-page` amount field.
- Bank-account picker precedent: `C/features/parties/pages/party-detail-page/party-detail-page.ts:67`
  (`bankAccounts` computed from the instrument registry).
- Design system: `app/client/docs/SYSTEM.md`. Panel = `rounded-card border border-rule bg-paper-raised p-6 shadow-card`.
  It says nothing about modals, so extend it (Slice 1 adds a short "Dialog" entry).

## Decisions (from the grilling session, all user-confirmed)

| # | Decision |
|---|---|
| D1 | **Three pay surfaces.** (a) per-installment card, (b) **new** per-expense "Pay expense", (c) existing "Pay full debt". All three accept full *or* partial. |
| D2 | **Waterfalls.** Per expense: fill that plan's installments in `Sequence` order. Full debt: fill across all the creditor's purchases, **oldest due month first** (tie-break: purchase date, then sequence). Worked example: 5 × $40.000 plan, pay $175.000 → cuotas 1-4 fully paid, cuota 5 gets $15.000 and has $25.000 left. |
| D3 | **Amount rules.** Must be > 0 and ≤ remaining. Equal to remaining = a full payment. Over remaining = validation error (400). **Never capped silently.** "Pay in full" = pay whatever is *remaining*. |
| D4 | **Undo** stays a per-installment button and removes the **last** payment on that installment only (stack; click again for the one before). No bulk undo for waterfalls. |
| D5 | **Display.** No payment history list. A partly paid row shows "$25.000 left" with "paid $15.000 of $40.000" underneath. Status stays due/overdue/future (**no new "partial" status**). Group and payables "outstanding" = Σ remaining. |
| D6 | **Timestamp** = server "now" (`TimeProvider`). No date picker. |
| D7 | **The holder's own payments stay display-only.** No bank, no ledger. |
| D8 | **Party path** (installment surface only) = "the party paid me, I pay the creditor". Choosing "Pay <party>'s part" + a "Received into" bank account posts the **existing settlement** (`Dr Bank / Cr Receivable`, same as the Parties page) **and** records a payment of that share on the installment. One Financing endpoint, sync `IPartiesApi` call, no new outbox event. |
| D9 | **Party share** = that party's PhantomPenny share of **this installment** only, always the full share (no partial party payment). Offered only on **split-accrued** installments. One option per party. Hidden once that party has paid on that installment. **Disabled with an explanation** when share > remaining. |
| D10 | **Undoing a party payment** also reverses its settlement ledger transaction (`ILedgerApi.ReverseTransactionAsync`), so the party owes that share again. |
| D11 | **Data model.** New table `financing_creditor_installment_payments` with columns `Id, InstallmentId, AmountMinorUnits, PaidOnUtc, PartyId?, SettlementTransactionId?`. `Installment.PaidOnUtc` stays the **"fully paid" marker**: set when Σ payments = amount, cleared when an undo drops below. Migration backfills one full-amount row per already-paid **creditor** installment. Card installments are untouched. |
| D12 | **Currency.** No FX. The partial full-debt dialog shows a currency selector **only when the creditor has more than one currency**, and the waterfall fills only purchases in that currency. Full "Pay full debt" settles everything, as today. |
| D13 | **UI.** One shared pay-dialog component built on native `<dialog>` + `showModal()` + `::backdrop` (dimmed background, centered card). Modes: `installment` / `expense` / `full-debt`. It replaces the inline "Pay full debt" confirm. |
| D14 | **Bug fix in Slice 1.** The payables per-account breakdown sums *remaining* amounts (today it includes paid installments). |

## Slice order

1. **`slice-1-foundation-partial-installment.md`**: payments table + migration/backfill, a partial
   free amount on one installment, undo-last, every read on "remaining", the D14 fix, and the dialog's
   first appearance.
2. **`slice-2-pay-expense.md`**: new per-expense pay (full/partial), with a pure waterfall allocator.
3. **`slice-3-partial-full-debt.md`**: partial "Pay full debt" through the same allocator, with oldest-due
   ordering and the currency selector.
4. **`slice-4-party-part.md`**: the party path (settle + pay) and an undo that also reverses the settlement.

Party goes last because it's the only slice that crosses modules and touches the ledger, and it
builds on a tested payments table and undo.

## Working cadence (the user's established pattern)

Each slice is built **one user-green-lit step at a time**:

1. API production code
2. API tests
3. Client production code
4. Client specs
5. Doc-sync (api/client `.claude/CLAUDE.md`, `.claude/TASK.md` new phase, `docs/DESIGN.md`, `docs/PRD.md`)

Stop after each step and report. **The user commits**, so never commit.

Phase numbering continues from **API Phase 47 / client Phase 42**:

| Slice | API phase | Client phase |
|---|---|---|
| 1 | 48 | 43 |
| 2 | 49 | 44 |
| 3 | 50 | 45 |
| 4 | 51 | 46 |

## Gotchas

- **`dotnet test` can be broken locally.** If it fails for tooling reasons, run each test project
  with `dotnet run --project tests/<Project>`. CI still uses `dotnet test`.
- **API tests build their schema with `EnsureCreated()` and never run migrations.** After adding a
  migration, run `dotnet ef database update` against the dev DB manually, or it silently drifts.
- Migrations: `dotnet ef migrations add <Name> --output-dir Infrastructure/Persistence/Migrations`,
  from the Financing project (design-time factory `FinancingDbContextFactory.cs`). The latest
  existing one is `20260924183744_AddFinancingCurrencyCode`.
- Client tests: `CHROME_BIN=/usr/bin/brave pnpm ng test --watch=false --browsers=ChromeHeadless`.
- Money is always **minor units (long)** on the wire and in the DB. Never floats.
- `UnpayCreditorInstallment` keeps its route and command name. Only its semantics change, to "undo
  last payment".
