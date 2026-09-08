# Slice 1 — Current-cycle outstanding (two-figure read model)

> Read `00-overview.md` first. This is the foundation slice: it fixes the primary complaint (the
> headline number) and defines what "outstanding" means for every later slice.

## Goal

Replace the single `outstandingMinorUnits` on each "Owed to creditors" row with **two** figures:

- **`DueNowMinorUnits`** — Σ `Installment.Amount` where the installment is unpaid
  (`PaidOnUtc is null`), non-reversed, belongs to a creditor plan, **and its `DueCycle` is at or
  before the current creditor billing cycle**. This folds in overdue arrears with the current
  cycle, exactly as the user asked ("carry unpaid cuotas from previous cycles").
- **`TotalOwedMinorUnits`** — Σ `Installment.Amount` over *all* unpaid, non-reversed creditor
  installments (future cuotas included). This is the whole remaining debt.

The client renders both: e.g. `Carlos — Due now: $45,000 · Total owed: $120,000`.

## Why this shape (intent)

The current handler sums *every* non-reversed installment and ignores `PaidOnUtc`. That answers
"how much did this creditor ever finance for me", not "what do I owe now". Two numbers, not one,
because the user explicitly wanted both the actionable near-term figure **and** the total, with
arrears surfaced in the near-term figure rather than buried. See `00-overview.md` Q2.

"Current creditor cycle" is uniform across all creditors — they share the 26th cutoff
(`PaymentPlan.CreditorCutoffDay = 26`). We resolve it once from the clock:
`ResolveCycle(today, 26).DueCycle`, and compare each installment's `DueCycle` to it by month
ordinal (`Year*12 + Month`, since `BillingCycle` has no `<` operator). Using `DueCycle` (close +
1) matches the whole codebase's "when the money moves" convention (Phase 25).

Paid installments are excluded from **both** sums. Even before any pay feature exists (Slice 3),
this matters: back-dated creditor purchases already stamp elapsed cuotas `PaidOnUtc` (Phase 32),
so excluding paid is immediately correct and testable.

## API changes

**File:** `app/api/src/Modules/Financing/PersonalFinance.Financing/Application/Queries/GetCreditorPayables/GetCreditorPayablesHandler.cs`

Current (grounded):

```csharp
internal sealed class GetCreditorPayablesHandler(FinancingDbContext context)
    : IQueryHandler<GetCreditorPayablesQuery, CreditorPayablesResponse> {
    public async Task<CreditorPayablesResponse> HandleAsync(GetCreditorPayablesQuery query, CancellationToken cancellationToken) {
        var installments = await (
            from installment in context.Set<Installment>()
            where installment.IsReversed == false
            join plan in context.PaymentPlans on installment.PaymentPlanId equals plan.Id
            where plan.CreditorId != null
            select new { plan.CreditorId, plan.CreditorAccountId, installment.Amount,
                         installment.CycleYear, installment.CycleMonth, plan.PurchaseDate }
        ).ToListAsync(cancellationToken);
        // ...
        var outstandingMinorUnits = group.Sum(row => row.Amount.MinorUnits);
        // ...
    }
}
```

Changes:

1. **Inject `TimeProvider`** into the ctor: `GetCreditorPayablesHandler(FinancingDbContext context, TimeProvider timeProvider)`.
   (`TimeProvider` is the codebase clock abstraction, already DI-registered — precedent
   `CreatePaymentPlanHandler`, `AccrueInstallments`.) Derive
   `var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);` and
   `var currentDueCycle = BillingCycleCalculator.ResolveCycle(today, PaymentPlan.CreditorCutoffDay).DueCycle;`
   and its ordinal `var currentOrdinal = currentDueCycle.Year * 12 + currentDueCycle.Month;`.
2. **Add `PaidOnUtc` to the projection** so the sum can exclude paid cuotas:
   `select new { …, installment.CycleYear, installment.CycleMonth, installment.PaidOnUtc, plan.PurchaseDate }`.
   Keep the `IsReversed == false` filter. (Do **not** try to filter `PaidOnUtc` server-side inside a
   value-converted-nullable comparison if it fights EF/SQLite — the current handler already
   materializes with `ToListAsync` then works in memory, so exclude paid in memory.)
3. **Compute both sums in the existing in-memory group** (replace the single
   `outstandingMinorUnits` line). Per group:
   ```csharp
   var unpaid = group.Where(row => row.PaidOnUtc is null).ToList();
   var totalOwedMinorUnits = unpaid.Sum(row => row.Amount.MinorUnits);
   var dueNowMinorUnits = unpaid
       .Where(row => {
           var dueCycle = new BillingCycle(row.CycleYear, row.CycleMonth).DueCycle;
           return dueCycle.Year * 12 + dueCycle.Month <= currentOrdinal;
       })
       .Sum(row => row.Amount.MinorUnits);
   ```
   Pass both into the `CreditorPayableRow` (see contract change). The `NextDueDate` and
   per-account `Accounts` breakdown logic stays as-is (`NextDueDate` already projects `DueCycle`
   before clamping — Phase 26 — leave it). **Consider** computing the account breakdown over
   `unpaid` too, so the per-account sub-line matches `TotalOwed`; note that in the doc but the
   headline figures are what this slice is judged on.

**File:** `…/PersonalFinance.Financing.Contracts/Queries/GetCreditorPayablesQuery.cs`

Replace `long OutstandingMinorUnits` on `CreditorPayableRow` with the two fields:

```csharp
public sealed record CreditorPayableRow(
    Guid CreditorId,
    string CreditorName,
    long DueNowMinorUnits,
    long TotalOwedMinorUnits,
    DateOnly? NextDueDate,
    IReadOnlyList<CreditorPayableAccountBreakdown> Accounts
);
```

(`CreditorPayableAccountBreakdown.OutstandingMinorUnits` can stay — it is the per-account total;
rename to `TotalOwedMinorUnits` only if you also split it, otherwise leave it to keep the slice
tight.)

**Host DTO + mapping:** `Endpoints/DTOs/CreditorPayablesDTO.cs` `CreditorPayableRowDto` and
`FinancingMappingExtensions.ToCreditorPayablesDto` must carry `dueNowMinorUnits` +
`totalOwedMinorUnits` (drop `outstandingMinorUnits`). **The host DTO must mirror the Contracts
change or the client never sees the new fields** (repeated lesson, Phases 28/30/33).

No new module edge, no schema change, no EF migration → `PersonalFinance.Architecture.Tests`
(RNF-9) stays green.

## Client changes

All under `app/client/src/app/features/financing/`.

1. **Type** `types/creditor-payable-row.ts`: replace `outstandingMinorUnits: Money` with
   `dueNowMinorUnits: Money` + `totalOwedMinorUnits: Money`.
2. **Service** `financing-service.ts`: `creditorPayables()` is a bare cast unwrapping `{ rows }` —
   no mapper, so no change beyond the type.
3. **Table** `pages/creditor-payables-page/creditor-payables-table.html`: the amount cell
   (currently `{{ formatArs(row.outstandingMinorUnits) }}` at ~line 46) becomes two values —
   render "Due now" as the primary/focal figure and "Total owed" as a muted sub-line (follow
   `docs/SYSTEM.md`: name the one focal element, demote the rest). Update the column header
   accordingly (e.g. "Amount" → "Due now / Total").

## Testing

**API** — `tests/PersonalFinance.Financing.Tests/GetCreditorPayablesHandlerTests.cs` (extend the
existing file; it already has the in-memory-SQLite harness). Construct the handler with a
`FixedTimeProvider` (see `AccrueInstallmentsTests` for the pattern) pinned to a known date. New
facts:

- **Arrears fold into DueNow:** a creditor with one overdue unpaid cuota (DueCycle < current) and
  one current-cycle unpaid cuota → `DueNowMinorUnits` = sum of both; `TotalOwedMinorUnits` = same
  plus any future cuotas.
- **Future excluded from DueNow, included in Total:** a cuota whose `DueCycle` > current is absent
  from `DueNowMinorUnits` but present in `TotalOwedMinorUnits`.
- **Paid excluded from both:** stamp one cuota `PaidOnUtc` (via `Installment.MarkPaid`) → it drops
  out of both sums.
- Keep/adjust the existing grouping / no-creditor-plan-excluded / card-plan-excluded facts for the
  new record shape.

**Client** — update the fixtures in `financing-service.spec.ts`, `creditor-payables-page.spec.ts`,
`creditor-payables-table.spec.ts` to the two-field row; add a table fact asserting both figures
render.

## Verification (before green-lighting Slice 2)

- `dotnet build PersonalFinance.sln -c Release` → 0W/0E; run
  `./tests/PersonalFinance.Financing.Tests/bin/Release/net10.0/PersonalFinance.Financing.Tests`
  (filter `-class "*CreditorPayables*"`) — new facts green, whole Financing suite green;
  `AccrualBoundaryTests` untouched.
- Client: `pnpm ng lint` clean, `pnpm ng test` all green, `pnpm ng build --configuration production`
  clean.
- Manual (handed to user): with a back-dated creditor purchase in the dev DB (some cuotas already
  `PaidOnUtc`), `GET /v1/financing/creditor-payables` shows a `dueNow` that includes overdue +
  current unpaid and excludes paid, and a `totalOwed` that adds future unpaid.

## Out of scope (later slices)

Detail drill-down (Slice 2), any pay/undo path (Slices 3–4). This slice only recomputes the
headline numbers.
