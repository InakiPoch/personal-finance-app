# Slice 2 — Creditor detail view (read-only, grouped by purchase)

> Read `00-overview.md` and Slice 1 first. This slice is **read-only**: see *what* you owe a
> creditor before Slice 3 lets you pay it. No writes, no ledger.

## Goal

A drill-down page for one creditor at `/financing/creditor-payables/:creditorId`, showing that
creditor's debt **grouped by purchase** (each `PaymentPlan` a section) with its installments
listed beneath — sequence, amount, due month, and paid/reversed status. The "Owed to creditors"
list rows become links into this page.

## Why grouped by purchase (intent)

The user said they want to see "the *things* I owe" — the couch, the trip, each split. A flat list
of cuotas loses that. There is no `MonthlyStatement` for creditors, so the card statement-page
model doesn't map; a creditor has *multiple* purchases, each with its own installment schedule.
Grouping by `PaymentPlan` mirrors how the debt was actually incurred and matches the Recent
Purchases mental model. See `00-overview.md` Q4.

This slice is deliberately read-only so it can be verified on its own. The per-row Pay/Undo buttons
land in Slice 3 and hang off the rows this slice renders.

## API changes

New pure additive CQRS read query on the `GetCardPurchases` / `GetCreditorPayables` precedent — no
schema change, no EF migration, no `IFinancingApi` edge (dispatch via `IQueryBus` from the host,
same as `GetMonthlyStatement`). RNF-9 unaffected.

**Contract** — `…/PersonalFinance.Financing.Contracts/Queries/GetCreditorDetailQuery.cs`:

```csharp
public sealed record GetCreditorDetailQuery(Guid CreditorId) : IQuery<CreditorDetailResponse>;

public sealed record CreditorDetailResponse(
    bool Found,                       // false → host maps 404 (mirror GetMonthlyStatement's Found flag)
    Guid CreditorId,
    string CreditorName,
    IReadOnlyList<CreditorPurchaseGroup> Purchases
);

public sealed record CreditorPurchaseGroup(
    Guid PlanId,
    string Description,
    DateOnly PurchaseDate,
    long TotalMinorUnits,             // Σ all non-reversed installment amounts of the plan
    long OutstandingMinorUnits,       // Σ unpaid, non-reversed
    IReadOnlyList<CreditorInstallmentRow> Installments
);

public sealed record CreditorInstallmentRow(
    Guid InstallmentId,
    int Sequence,
    int InstallmentCount,             // = Installments.Count of the plan, for "cuota N de M"
    long AmountMinorUnits,
    int DueYear,                      // DueCycle.Year
    int DueMonth,                     // DueCycle.Month
    bool IsPaid,
    bool IsReversed,
    string Status                     // "overdue" | "due" | "future" | "paid" | "reversed"
);
```

**Handler** — `Application/Queries/GetCreditorDetail/GetCreditorDetailHandler.cs`, ctor
`(FinancingDbContext context, TimeProvider timeProvider)`:

1. Load the creditor: `context.Creditors.FirstOrDefaultAsync(c => c.Id == query.CreditorId)`.
   Null → return `new CreditorDetailResponse(Found: false, query.CreditorId, "", [])`.
2. Load this creditor's plans + their installments:
   `from installment in context.Set<Installment>() join plan in context.PaymentPlans on
   installment.PaymentPlanId equals plan.Id where plan.CreditorId == query.CreditorId select
   new { plan.Id, plan.Description, plan.PurchaseDate, installment.Id (as InstallmentId),
   installment.Sequence, installment.Amount, installment.CycleYear, installment.CycleMonth,
   installment.PaidOnUtc, installment.IsReversed }`, then `ToListAsync`.
   (`Installment` has no root `DbSet` — use `context.Set<Installment>()`, the established precedent.)
3. In memory, resolve the current cycle exactly as Slice 1
   (`ResolveCycle(today, PaymentPlan.CreditorCutoffDay).DueCycle`, ordinal compare) and derive each
   row's `Status`:
   - `IsReversed` → `"reversed"`; else `PaidOnUtc is not null` → `"paid"`; else compare
     `DueCycle` ordinal to current: `< current` → `"overdue"`, `== current` → `"due"`, `> current`
     → `"future"`.
4. Group by `PlanId`; per group compute `TotalMinorUnits` (Σ non-reversed) and
   `OutstandingMinorUnits` (Σ unpaid non-reversed); order installments by `Sequence`; set
   `InstallmentCount` = group count. Order groups by `PurchaseDate` descending (newest purchase
   first, matching Recent Purchases).

Register in `FinancingModule.Register` beside `GetCreditorPayablesHandler`.

**Host** — `GET /v1/financing/creditor-payables/{creditorId}`:
- `ApiRoutes.Financing.CreditorPayableDetail = "/creditor-payables/{creditorId:guid}"` (sits under
  the existing list route `/creditor-payables`).
- `Endpoints/Financing/GetCreditorDetail.cs` — dispatch via `IQueryBus.AskAsync`; on `Found ==
  false` return an RFC-9457 `404` `Financing.CreditorNotFound` (the error code already exists —
  Phase 18); else `TypedResults.Ok(response.ToCreditorDetailDto())`. Shape mirrors
  `Endpoints/Financing/GetStatement.cs` (`Results<Ok<…Dto>, ProblemHttpResult>`).
- `Endpoints/DTOs/CreditorDetailDTO.cs` + `FinancingMappingExtensions.ToCreditorDetailDto`.
- Wire in `EndpointExtensions.MapFinancingEndpoints` with `.Produces<CreditorDetailDto>(200)` +
  `.ProducesProblem(404)`, tag `"Financing"`.

## Client changes

All under `app/client/src/app/features/financing/`.

1. **Types** under `types/`: `creditor-detail.ts` (`{ creditorId, creditorName, purchases }`),
   `creditor-purchase-group.ts`, `creditor-installment-row.ts` — one per file (repo convention).
2. **Service** `financing-service.ts`: `creditorDetail(creditorId): Observable<CreditorDetail>` →
   `GET financing/creditor-payables/${creditorId}` (bare cast, no envelope — the response is a
   single object, not a `{ rows }` list).
3. **Route** `financing.routes.ts`: add
   `{ path: 'creditor-payables/:creditorId', component: CreditorDetailPage }` after the list route.
4. **Page** `pages/creditor-detail-page/creditor-detail-page.{ts,html,spec.ts}` — container:
   reads `:creditorId` from the route, owns a `loadStatus` machine (`idle|loading|ready|error`) +
   a `detail` signal, fetches once in `ngOnInit`. Handles the 404 (`CreditorNotFound`) as a
   friendly "creditor not found" state.
5. **Presentational table(s)**: a section per purchase (description, date, outstanding-of-total),
   with an installments sub-table beneath (cuota N/M, due month via the `MONTH_LABELS` idiom from
   `recent-purchases-table.ts`, amount, a status badge). Styled to `docs/SYSTEM.md`. Leave a clear
   seam where Slice 3 adds the per-row Pay button + a header "Pay full debt" button.
6. **Link in** from the list: `creditor-payables-table.html` — make each creditor row (or its name)
   a `routerLink` to `/financing/creditor-payables/${row.creditorId}`.

## Testing

**API** — `tests/PersonalFinance.Financing.Tests/GetCreditorDetailHandlerTests.cs` (in-memory
SQLite + `FixedTimeProvider`, on the `GetCreditorPayablesHandlerTests` harness): unknown creditor
→ `Found == false`; two purchases group into two sections with correct `Total`/`Outstanding`;
installments ordered by sequence with correct `Status` (overdue/due/future/paid/reversed); a
card-backed plan for the same... (there is none — creditor plans only) — instead assert a
*second* creditor's plans don't leak in. Optionally a WAF fact in
`tests/PersonalFinance.Api.Tests/` for the 404 contract + OpenAPI presence (mirror
`StatementDetailTests`).

**Client** — `creditor-detail-page.spec.ts` (loads + renders groups; 404 → not-found state),
presentational table spec (renders sections + installment rows + status badges), and a
`creditor-payables-table.spec.ts` fact that the row links to the detail route.

## Verification (before green-lighting Slice 3)

- API: build Release, run the Financing test binary (`-class "*CreditorDetail*"`) + full suite
  green; RNF-9 green.
- Client: lint, test, prod build all clean.
- Manual (handed to user): click a creditor on "Owed to creditors" → detail page lists purchases
  grouped, each with its cuotas and correct status badges; an unknown id shows the not-found state.

## Out of scope (Slice 3+)

Any write — Pay, Undo, Pay full debt. This slice only *displays* the detail and wires navigation.
