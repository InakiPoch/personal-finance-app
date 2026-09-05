# Slice 2 — Card-debt drill-down (expand a card → its purchases)

> **Read this whole file before writing any code.** Self-contained; assumes no prior
> context. Depends on **Slice 1** being complete (the `PaymentPlan.Description` field
> must already exist and be populated). Build and test this slice fully before Slice 3.

---

## 1. Why this exists (context & intent)

The Dashboard has a **"Card Debt by Cycle"** block: it tells the user *how much* they
owe on each credit card this cycle, grouped by card. But it never says *what* that debt
is made of. The user wants to **expand a card** (like a dropdown/accordion) and see the
**individual purchases** behind its total — each with the human `Description` added in
Slice 1 — including creditor-tagged purchases.

### Critical geography (verified — do not re-investigate or "fix")

- A "loaded expense" is a Financing `PaymentPlan` on a credit card. It posts
  `Dr "{Card} Purchases" (Kind=CardPurchases) / Cr "{Card} Liability"` when its cuota
  accrues (`AccrueInstallments` scheduler).
- The Dashboard's **"Out of Pocket" / `vw_ledger_monthly_expenses`** block **deliberately
  excludes** `Kind IN ('Receivable','CardPurchases')` (Phase-10 RF-1 fix, US-1 AC2). So
  loaded expenses **do not appear there** — that block is debit/cash + subscriptions only.
  **Do not touch that block.**
- Loaded expenses appear in the **"Card Debt by Cycle"** block, grouped by card. **This is
  the block we make expandable.**
- **No Ledger change is needed.** Everything required to itemize a card's debt lives in
  the **Financing** module: `PaymentPlan` (with the Slice-1 `Description`) → its
  `Installment`s (each has cycle, `AccruedOnUtc`, `StatementId`, `Amount`, `IsReversed`)
  → grouped by card. Do **not** try to read the Ledger `Transaction` feed for this.

### Locked design decisions

| # | Decision | Answer |
|---|----------|--------|
| D1 | Drill-down target | The **"Card Debt by Cycle"** Dashboard block. |
| D2 | Row granularity | **One row per purchase** (`PaymentPlan`), not per cuota. |
| D3 | Scope | **All outstanding** purchases on the card (any cuota still accrued-unpaid or not-yet-accrued). |
| D4 | Ordering | Plans whose installments fall in the **current cycle sorted on top**; the rest after. |
| D5 | Creditor purchases | Included, flagged (`isCreditorPayment = CreditorId is not null`). |
| D6 | Module boundary | **Financing-only.** New read query + host endpoint. No migration, no Ledger edge, no `IFinancingApi` change (dispatch via `IQueryBus`, per the `GetCardStatements` precedent). |

---

## 2. Data model (what "outstanding" means)

For a given `cardId`, gather its `Installment`s (join `Installment.PaymentPlanId` →
`PaymentPlan`, filter `PaymentPlan.CardId == cardId`), excluding `IsReversed`. An
installment is **outstanding** when it is either:
- **not yet accrued** (`AccruedOnUtc == null`) — future schedule; or
- **accrued but unpaid** — `StatementId` points at a `MonthlyStatement` whose `IsPaid == false`.

Group the surviving installments by `PaymentPlanId`. Each group → one output row:

| Field | Source |
|-------|--------|
| `planId` | `PaymentPlan.Id` |
| `description` | `PaymentPlan.Description` (Slice 1) |
| `totalMinorUnits` | `PaymentPlan.Total` |
| `installmentCount` | `PaymentPlan.InstallmentCount` |
| `outstandingCount` | number of that plan's outstanding installments (for "cuota N of M" context) |
| `purchaseDate` | `PaymentPlan.PurchaseDate` |
| `isCreditorPayment` | `PaymentPlan.CreditorId is not null` |

**Ordering (D4):** a plan is "current-cycle" if any of its outstanding installments is in
the card's current billing cycle. Compute the current cycle via the existing billing-cycle
logic (`BillingCycleCalculator` / the card's `CutoffDay`, as `AccrueInstallments` uses
`installment.Cycle.IsClosedAsOf(today, card.CutoffDay)`). Sort current-cycle plans first,
then by `PurchaseDate` descending within each band.

> SQLite gotcha (repo-wide): never `OrderBy` a `DateTimeOffset` column server-side (EF →
> `NotSupportedException`). Project, `ToListAsync()`, then order in memory — the
> `GetCurrentAccountTimelineHandler` / `GetMonthlyStatementHandler` precedent.

---

## 3. API — new Financing read query + host endpoint

Pure additive CQRS on the **`GetCardStatements`** / **`GetMonthlyStatement`** precedent
(Phase 11/12): a query in `Financing.Contracts`, an internal handler registered in
`FinancingModule`, dispatched by the host through `IQueryBus` — **no `IFinancingApi`
edge, no schema change, no migration**, so `PersonalFinance.Architecture.Tests` (RNF-9)
stays green.

### 3.1 Contracts
`Financing.Contracts/Queries/GetCardPurchasesQuery.cs` (mirror `GetCardStatementsQuery`):
```csharp
public sealed record GetCardPurchasesQuery(Guid CardId) : IQuery<CardPurchasesResponse>;
public sealed record CardPurchasesResponse(Guid CardId, IReadOnlyList<CardPurchaseRow> Rows);
public sealed record CardPurchaseRow(
    Guid PlanId,
    string Description,
    long TotalMinorUnits,
    int InstallmentCount,
    int OutstandingCount,
    DateOnly PurchaseDate,
    bool IsCreditorPayment);
```

### 3.2 Handler
`Application/Queries/GetCardPurchases/GetCardPurchasesHandler.cs` — implements §2:
query installments for the card joined to plans, apply the outstanding filter
(unpaid statement or not-yet-accrued), group by plan, project rows, order in memory
(current-cycle first, then `PurchaseDate` desc). Unknown card → empty `Rows` (do **not**
404 — sibling consistency with `/cards/{id}/future-schedule` and `/cards/{id}/statements`).
Register it in `FinancingModule.Register`.

### 3.3 Host endpoint
- `src/Bootstrap/PersonalFinance.Api/Endpoints/Financing/GetCardPurchases.cs` — `Handle`
  dispatches `new GetCardPurchasesQuery(id)` via `IQueryBus.AskAsync` and maps to a DTO
  (mirror `GetCardStatements.cs`). Returns `Ok<CardPurchasesDto>`.
- `Endpoints/ApiRoutes.cs` — add `Financing.CardPurchases = "/cards/{id:guid}/purchases"`.
- Wire in `EndpointExtensions` alongside the other Financing card routes, with
  `.Produces<CardPurchasesDto>(200)` and appropriate OpenAPI `.WithTags("Financing")` /
  summary.
- DTO + mapping: `Endpoints/DTOs/CardPurchasesDTO.cs` (`CardPurchasesDto` +
  `CardPurchaseRowDto`) and a mapping extension in `FinancingMappingExtensions.cs`
  (build DTOs via the mapping method, never `new ...Dto{}` in the endpoint — house rule).

Route: **`GET /v1/financing/cards/{id}/purchases`** → `{ cardId, rows: [{ planId,
description, totalMinorUnits, installmentCount, outstandingCount, purchaseDate,
isCreditorPayment }] }`.

---

## 4. Client — make the card-debt rows expandable

The Dashboard is `features/reports/pages/dashboard-page/` (`.ts` + `.html`). The
"Card Debt by Cycle" section iterates `cycleByCard()` — one visible row per card
(`{ card, accrued, future, total }`), fed by `reports.cardDueByMonth()` (`CardDueRow[]`).
Each `CardDueRow` carries a `cardId: string | null`.

### 4.1 Type
`features/financing/types/card-purchase-row.ts` (new) — mirror the API contract:
```typescript
export type CardPurchaseRow = {
  planId: string;
  description: string;
  totalMinorUnits: Money;
  installmentCount: number;
  outstandingCount: number;
  purchaseDate: IsoDate;
  isCreditorPayment: boolean;
};
```

### 4.2 Service
`features/financing/financing-service.ts` — add
`cardPurchases(cardId: string): Observable<CardPurchaseRow[]>` calling
`GET financing/cards/{cardId}/purchases` and mapping the `rows` envelope (mirror
`reports-service.cardDueByMonth()` envelope handling).

### 4.3 Dashboard accordion
`dashboard-page.ts`:
- Track expansion state per card (a `signal<string | null>` for the open card id, or a
  `Set` signal) and a cache of fetched rows per card.
- On expand, if `cardId` is present, call `financingService.cardPurchases(cardId)` and
  store the rows; toggling collapses.

`dashboard-page.html`:
- Make each "Card Debt by Cycle" row a button/disclosure that toggles expansion (accessible:
  `aria-expanded`, `aria-controls`). Style per `docs/SYSTEM.md` — keep the warm-homebanking
  system; no new hardcoded hex, use tokens.
- When expanded, render the purchases: one line per `CardPurchaseRow` — description as the
  headline, `formatArs(totalMinorUnits)`, "cuota"/installment context from
  `outstandingCount`/`installmentCount`, a subtle marker when `isCreditorPayment`.
- Rows arrive already ordered by the API (current cycle first); render in received order.
- Guard the case where `cardId` is `null` (some rows may lack it) — those cards simply
  aren't expandable.

---

## 5. Testing

### 5.1 API (xUnit v3)
`GetCardPurchasesHandler` tests (in-memory SQLite harness — the
`OnPaymentPlanCreatedTests` / Reporting-fixture precedent, since this touches EF):
- Outstanding filter: a plan with a **paid** statement's installments is excluded; a plan
  with an unpaid/unaccrued installment is included.
- Grouping: a multi-cuota plan yields exactly **one** row with the right `outstandingCount`.
- Creditor flag: a plan created with a `CreditorId` has `IsCreditorPayment == true`.
- Ordering: a current-cycle plan sorts before an older one.
- Unknown card → empty rows (not 404).
- Optional host test in `Api.Tests`: OpenAPI presence under `Financing` with `200`, and
  unknown-card → `200` empty (the happy path needs the accrual scheduler, which is
  stripped from the `WebApplicationFactory` — put the full walk in `PersonalFinance.Api.http`,
  the Phase 11/12 precedent).

### 5.2 Client (Karma/Jasmine, zoneless)
`dashboard-page.spec.ts`:
- Clicking a card row calls the service with its `cardId` and renders the returned rows.
- Each rendered purchase shows its `description`.
- A second click collapses; a card row with `cardId == null` is not expandable.
- Mock the service with a Jasmine spy returning a fixed `CardPurchaseRow[]` (use
  `TestScheduler` only if you introduce timers; otherwise a plain `of(...)` spy).

Run:
```bash
dotnet test --solution PersonalFinance.sln                                  # app/api/
CHROME_BIN=/usr/bin/brave pnpm ng test --watch=false --browsers=ChromeHeadless  # app/client/
pnpm ng lint && pnpm ng build
```

---

## 6. Verification — Slice 2 done when

- [ ] `GET /v1/financing/cards/{id}/purchases` returns one row per outstanding purchase, with `description`, and unknown card → `200` empty.
- [ ] Paid-off purchases are excluded; creditor purchases are flagged; current-cycle purchases sort first.
- [ ] No new EF migration, no `IFinancingApi` change; `PersonalFinance.Architecture.Tests` green.
- [ ] On the Dashboard, a "Card Debt by Cycle" card expands to list its purchases with descriptions and collapses again.
- [ ] `dotnet test --solution` green (higher count); client `ng test` + `ng lint` + `ng build` green.

Manual end-to-end: run the API, ensure at least one card has accrued/pending installments
(let the `AccrueInstallments` scheduler run), then expand that card on the Dashboard and
confirm the purchases + descriptions match the DB.

---

## 7. Out of scope for this slice

- Any Ledger change or reading the `/v1/ledger/transactions` feed.
- The "Out of Pocket" / monthly-expenses block (untouched).
- Per-cuota breakdown inside the expansion (one row per purchase only).
- The standalone "Recent purchases" page (Slice 3).
- A "current cycle only" filter (the endpoint returns all outstanding; a filter can be layered later).
