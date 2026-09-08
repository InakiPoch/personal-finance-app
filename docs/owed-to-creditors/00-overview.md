# Owed to Creditors — expansion initiative (overview)

## Why this exists

The "Owed to creditors" view (`/financing/creditor-payables`) today has three problems the
user hit in real use:

1. **The amount is wrong for the question being asked.** It shows the *historic* total owed to
   each creditor — every non-reversed installment ever, paid or not, past and future — when the
   user wants to know **what they owe right now**: this billing cycle plus anything overdue.
2. **There is no detail.** You see one number per creditor but cannot see *what* you owe them —
   which purchases, which cuotas.
3. **There is no way to pay.** Creditor debt only ever accumulates. The individual-installment
   payment feature that exists for credit-card statements
   (`docs/individual-installment-payments/`) has no creditor equivalent.

The end state the user wants: on each creditor, **"Due now $X · Total owed $Y"**; drill in to see
the purchases and cuotas grouped by purchase; and settle them — **individual cuotas** or the
**full debt** — one click each, reversibly.

## The one decision that shapes everything: payment is display-only

Creditor debt is **ledger-free for the holder** by deliberate design (API Phases 19/23/32).
There is no `MonthlyStatement`, creditor installments **never accrue** (`AccruedOnUtc` /
`StatementId` stay null forever), and there is **no ledger liability account** to pay from. The
card payment path (`PayInstallmentHandler`) posts a real `Dr CardLiability / Cr Bank` transaction
from a chosen bank account and **explicitly rejects** creditor installments
(`InstallmentNotAccrued`, 409).

We keep that philosophy. **"Paying" a creditor installment = stamping `Installment.PaidOnUtc`.**
No bank-account picker, no ledger posting, no money movement. This is exactly what the back-dated
creditor path already does (Phase 32 `stampBackdatedCreditorInstallments`). It is:

- **Consistent** with the existing creditor model.
- **Forward-compatible.** If creditors ever go on the ledger, the pay command only *adds* a
  posting; the read model (Σ unpaid installments) never changes. Nothing built here may assume
  creditors can *never* have a bank/ledger side.

The tradeoff the user accepted: paying a creditor does **not** reduce any bank balance in the app.
Creditors here are a tracked-debt "smart sticky note", not a real accounting liability.

## Settled design decisions

| # | Decision | Choice |
|---|----------|--------|
| 1 | Payment model | **Display-only** `PaidOnUtc` stamp. No bank, no ledger. Timestamp = now (`TimeProvider`), one-click, no date field. |
| 2 | The two figures | **`DueNow`** = Σ unpaid, non-reversed installments whose `DueCycle` ≤ current creditor cycle (this cycle **+ overdue arrears folded in**). **`TotalOwed`** = Σ *all* unpaid, non-reversed installments (future included). Paid excluded from both. |
| 3 | "Pay full debt" scope | **All remaining** unpaid, non-reversed installments → drives `TotalOwed` to $0. |
| 4 | Detail view | **Grouped by purchase** (each `PaymentPlan` a section, its installments beneath). |
| 5 | Undo | Null the stamp (trivial, no ledger to reverse). **Folded into the pay slice (Slice 3)**, not its own slice. |

The two-figure display: `Carlos — Due now: $45,000 · Total owed: $120,000` (where due-now = this
cycle $30k + overdue $15k folded together).

## The current code being changed (grounded facts)

- **Read model:** `app/api/src/Modules/Financing/PersonalFinance.Financing/Application/Queries/GetCreditorPayables/GetCreditorPayablesHandler.cs`
  — a flat `group.Sum(row => row.Amount.MinorUnits)` over every non-reversed creditor installment.
  Filters only `IsReversed == false` + `plan.CreditorId != null`. **No `PaidOnUtc` filter, no
  clock, no cycle logic.** Handler ctor is `(FinancingDbContext context)` only.
- **Contract:** `…/PersonalFinance.Financing.Contracts/Queries/GetCreditorPayablesQuery.cs` —
  `CreditorPayableRow(Guid CreditorId, string CreditorName, long OutstandingMinorUnits,
  DateOnly? NextDueDate, IReadOnlyList<CreditorPayableAccountBreakdown> Accounts)`.
- **Client:** `app/client/src/app/features/financing/`
  - type `types/creditor-payable-row.ts` — `{ creditorId, creditorName, outstandingMinorUnits: Money, nextDueDate: IsoDate | null, accounts }`
  - service `financing-service.ts` — `creditorPayables()` at ~line 61, unwraps `{ rows }`
  - page + table (co-located): `pages/creditor-payables-page/creditor-payables-page.{ts,html}`
    and `pages/creditor-payables-page/creditor-payables-table.{ts,html}` (amount at `.html:46`)
  - route `financing.routes.ts:13` — `{ path: 'creditor-payables', component: CreditorPayablesPage }`
  - nav `app.ts:20` — `{ label: 'Owed to creditors', path: '/financing/creditor-payables' }`

## Reusable building blocks (do not reinvent)

- **Cycle math:** `Domain/BillingCycleCalculator.ResolveCycle(DateOnly date, int cutoffDay)`,
  `Domain/BillingCycle.DueCycle` (= `AddMonths(1)`), `PaymentPlan.CreditorCutoffDay = 26`
  (`Domain/PaymentPlan.cs:19`, `internal const`). Per-installment: `Installment.Cycle`,
  `Installment.DueCycle` (`Domain/Installment.cs:19-20`).
- **Paid flag already exists:** `Installment.PaidOnUtc` (nullable, private setter),
  `Installment.IsPaid`, `Installment.MarkPaid(paidOnUtc)` (guarded — double-pay →
  `FinancingErrors.InstallmentAlreadyPaid`). Column shipped in migration
  `20260907152207_AddInstallmentPaidOnUtc`. **→ NO new EF migration anywhere in this initiative.**
- **Command skeleton to mirror** (guard → mutate → save, *minus* the ledger/bank legs):
  `Application/Commands/PayInstallment/PayInstallmentHandler.cs` + its validator. Registration
  pattern: `FinancingModule.cs` `Register`. Error → HTTP mapping: `Endpoints/ErrorHttpStatusHelper.cs`.
- **Client pay-UI pattern to mirror** (per-row button that emits up + a shared form): the card
  statement page `pages/statement-page/installments-table.{ts,html}` + `statement-page.ts` — but
  **drop the bank-account field**; a creditor pay needs no form input at all.

## Slice order (each is API + Client + Testing, tested before the next)

1. **Slice 1 — Current-cycle outstanding.** Replace the flat sum with `DueNow` + `TotalOwed`.
   Foundation: fixes the primary complaint and defines "outstanding" for every later slice.
2. **Slice 2 — Creditor detail view.** Read-only drill-down grouped by purchase. See before you pay.
3. **Slice 3 — Pay a cuota + undo.** `PayCreditorInstallment` / `UnpayCreditorInstallment`
   (display-only stamp / un-stamp) + per-row Pay/Undo buttons.
4. **Slice 4 — Pay full debt.** `PayCreditorFullDebt` stamps every remaining unpaid cuota.

## Cross-cutting conventions (from the repo)

- **API:** modular monolith, CQRS, `.Contracts`-only across module edges (RNF-9 — new queries /
  commands / `IFinancingApi` members are `.Contracts` additions, never a new module reference).
  C# style: `camelCase` privates, `if()` no space, brace on same line. Endpoint DTOs live in
  `Endpoints/DTOs/` (not `Endpoints/Financing/DTOs/`). Host DTO **must** mirror any new Contracts
  field or the client never sees it.
- **Client:** Angular 20, standalone, zoneless, OnPush, signals-first. Types mirror DTOs by hand
  (no codegen), one per file under `types/`. `Money` is branded minor-units. Container owns state
  + `loadStatus` machine; presentational tables take `input()` and emit `output()`. Error copy
  keys off `AppError.code`.
- **Testing:** API — `dotnet test` is broken in this environment (`--solution`/`--project` report
  "Zero tests ran", exit 5). **Build then run each test binary directly:**
  `dotnet build PersonalFinance.sln -c Release`, then
  `./tests/<Proj>/bin/Release/net10.0/<Proj>` (filter a class with `-class "*Name*"`). Client —
  `pnpm ng lint`, `CHROME_BIN=/usr/bin/brave pnpm ng test --watch=false --browsers=ChromeHeadless`,
  `pnpm ng build --configuration production`.
- **Commits:** the user commits their own work, one green-lit step at a time. Do not commit.
- **Docs to sync at the end of each slice** (mirror how prior initiatives recorded a phase):
  `app/api/.claude/CLAUDE.md` (new Phase entry), `app/api/docs/DESIGN.md`, `app/api/docs/PRD.md`,
  `app/client/.claude/CLAUDE.md`, and the relevant `TASK.md` files.
