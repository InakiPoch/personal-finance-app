# Slice 2b — Party's future monthly shares, visible on the Parties view

> Part of the **parties-card-split** initiative. Do this **after** Slice 1 is green and committed.
> Full vertical: Financing module query + host endpoint + client. **No EF migration.** Read `README.md`
> for the shared context.

## Context — the gap

A card-backed split already accrues the co-borrower's receivable **per billing cycle** server-side
(`AccrueInstallments.buildSplitLines` posts `Dr Receivable[party] / Cr CardLiability` when each cycle
closes, splitting with `PhantomPennyAllocator`). But **future, not-yet-accrued** shares are invisible:
the party timeline read-view (`vw_current_account_timeline`) INNER-JOINs **posted** movements only, so
before a cycle closes a freshly-split party shows "$0 / settled." The user wants the Parties view to show
what a party *will* owe each upcoming month (e.g. "Oct 2026 — $333.33", "Nov 2026 — $333.33", …) until
all N installments are paid, then settled.

## Intent

Expose a per-party projection of upcoming installment shares and render it on the party-detail page as a
**"Scheduled"** block, distinct from the posted timeline. The projection must be **byte-exact** with what
accrual will actually post.

## Key design decision — Financing query, NOT a Reporting SQL view

The obvious-looking option is a new SQL read-view consumed by Reporting (that is how every current
Reporting query works). **Reject it.** The per-installment split uses `PhantomPennyAllocator`
(largest-remainder allocation, holder at index 0 absorbs the phantom penny) in C#. **SQL cannot reproduce
that tie-breaking**, so a view-based projection would drift by a minor unit from real accrual on odd
splits — and a projection that doesn't match reality defeats the purpose.

Instead, compute in a **Financing query handler that reuses the exact allocator**. This also respects the
architecture: Financing already owns installments, weights, `PartyId`, cycle, and card name — it needs
**no** read of `parties_parties`. The **host** (composition root) exposes the query under a party-centric
URL and calls the Financing facade. **Reporting is untouched** (no RNF-6 "views-only" violation), and there
is **no schema change** (pure query over existing tables → no migration).

Reference the accrual logic to mirror it: `AccrueInstallments.buildSplitLines`
(`app/api/src/Modules/Financing/PersonalFinance.Financing/Application/Scheduling/AccrueInstallments.cs`,
lines 137–156).

## Data model (confirmed)

- `Installment` (`.../Financing/Domain/Installment.cs`) — no dedicated `DbSet`; reach it via
  `context.Set<Installment>()`. Fields: `PaymentPlanId`, `Sequence` (1-based; **not** `InstallmentNumber`),
  `Amount` (`Money`, column `AmountMinorUnits`), `CycleYear`, `CycleMonth`, `AccruedOnUtc` (`DateTimeOffset?`,
  null = not yet accrued), `StatementId` (`Guid?`), `IsReversed` (`bool`). `Cycle => new(CycleYear, CycleMonth)`.
- `PaymentPlanSplitParticipant` (`.../Financing/Domain/PaymentPlanSplitParticipant.cs`, table
  `financing_payment_plan_split_participants`): `PaymentPlanId`, `PartyId`, `ReceivableAccountId`, `Weight (long)`.
- `PaymentPlan` (`.../Financing/Domain/PaymentPlan.cs`): `CardId (Guid?)`, `Total (Money)`, `Description`,
  `InstallmentCount`, `Installments`, `SplitParticipants`.
- `CreditCard` (`.../Financing/Domain/CreditCard.cs`, table `financing_credit_cards`): `Name`, `CutoffDay`.
- `FinancingDbContext` exposes `PaymentPlans`, `CreditCards` DbSets (installments via `Set<Installment>()`).

## Steps — API (Financing module)

1. **Query contract** — new file
   `app/api/src/Modules/Financing/PersonalFinance.Financing.Contracts/Queries/GetFuturePartySharesQuery.cs`
   (mirror `ListCreditorsQuery`):
   ```csharp
   using PersonalFinance.Abstractions.Messaging;

   namespace PersonalFinance.Financing.Contracts.Queries;

   public sealed record FuturePartyShareRow(int CycleYear, int CycleMonth, long ShareMinorUnits, string CurrencyCode, string SourceLabel);
   public sealed record GetFuturePartySharesResponse(IReadOnlyList<FuturePartyShareRow> Rows);
   public sealed record GetFuturePartySharesQuery(Guid PartyId) : IQuery<GetFuturePartySharesResponse>;
   ```

2. **Handler** — new file
   `app/api/src/Modules/Financing/PersonalFinance.Financing/Application/Queries/GetFuturePartyShares/GetFuturePartySharesHandler.cs`.
   Logic (reusing the allocator so projection == accrual):
   - Load candidate installments with their plan + card name:
     ```csharp
     var pending = await (
         from installment in context.Set<Installment>()
         where installment.AccruedOnUtc == null
         where installment.IsReversed == false
         join plan in context.PaymentPlans on installment.PaymentPlanId equals plan.Id
         where plan.CardId != null
         where plan.SplitParticipants.Any(p => p.PartyId == query.PartyId)
         join card in context.CreditCards on plan.CardId equals card.Id
         select new { installment, plan, CardName = card.Name }
     ).ToListAsync(cancellationToken);
     ```
   - For each row, split the installment amount over `[holder, ...participants]` — **identical** to
     `buildSplitLines`:
     ```csharp
     var participants = row.plan.SplitParticipants.OrderBy(p => p.PartyId).ToList();
     long[] weights = [1L, .. participants.Select(p => p.Weight)];
     var shares = new PhantomPennyAllocator().Allocate(row.installment.Amount, weights);
     var index = participants.FindIndex(p => p.PartyId == query.PartyId);
     var share = shares[index + 1]; // holder is shares[0]
     if (share.MinorUnits <= 0) continue;
     ```
   - Emit `new FuturePartyShareRow(row.installment.CycleYear, row.installment.CycleMonth,
     share.MinorUnits, share.Currency.Code, $"{row.CardName} — {row.plan.Description}")`.
   - Return the rows ordered by `CycleYear`, then `CycleMonth`.
   - Keep EF-translatable parts in the query; do the allocation in memory after materialization
     (as `AccrueInstallments` does). Match the participant **ordering** (`OrderBy(PartyId)`) exactly.

3. **Facade** — add to `IFinancingApi`
   (`.../PersonalFinance.Financing.Contracts/IFinancingApi.cs`):
   `Task<GetFuturePartySharesResponse> GetFuturePartySharesAsync(GetFuturePartySharesQuery query, CancellationToken cancellationToken);`
   and implement in `.../PersonalFinance.Financing/Infrastructure/PublicApi/FinancingApi.cs` as
   `queryBus.AskAsync(query, cancellationToken)`.

4. **Register** the handler in `FinancingModule.Register` alongside the other query handlers:
   ```csharp
   services.AddScoped<IQueryHandler<GetFuturePartySharesQuery, GetFuturePartySharesResponse>, GetFuturePartySharesHandler>();
   ```
   All additions are `.Contracts`-only across the module edge → `PersonalFinance.Architecture.Tests` (RNF-9)
   stays green.

5. **Host wiring** (`app/api/src/Bootstrap/PersonalFinance.Api`):
   - `Endpoints/ApiRoutes.cs` — add `public const string FutureShares = "/{id}/future-shares";` to the
     `Parties` record (sits alongside the existing party routes).
   - `Endpoints/Parties/GetPartyFutureShares.cs`:
     ```csharp
     public static class GetPartyFutureShares {
         public static async Task<Ok<FuturePartySharesDto>> Handle(Guid id, IFinancingApi financing, CancellationToken cancellationToken) {
             var response = await financing.GetFuturePartySharesAsync(new GetFuturePartySharesQuery(id), cancellationToken);
             return TypedResults.Ok(response.ToFuturePartySharesDto());
         }
     }
     ```
     (The host is the composition root; calling `IFinancingApi` from a Parties-grouped endpoint is allowed.)
   - `Endpoints/DTOs/FuturePartySharesDTO.cs`:
     `public sealed record FuturePartyShareDto(int CycleYear, int CycleMonth, long ShareMinorUnits, string CurrencyCode, string SourceLabel);`
     + `public sealed record FuturePartySharesDto(IReadOnlyList<FuturePartyShareDto> Rows);`
   - Mapping extension `ToFuturePartySharesDto(this GetFuturePartySharesResponse response)`
     (follow the existing per-receiver mapping-extension grouping convention).
   - In `MapPartiesEndpoints`:
     ```csharp
     group.MapGet(ApiRoutes.Parties.FutureShares, GetPartyFutureShares.Handle)
         .WithSummary("A party's upcoming installment shares.")
         .WithDescription("Projected not-yet-accrued monthly shares for the party's card-split plans.")
         .Produces<FuturePartySharesDto>(StatusCodes.Status200OK);
     ```

## Steps — Client

6. **Type + service** —
   `app/client/src/app/features/parties/types/future-party-share.ts`:
   ```ts
   export type FuturePartyShare = {
     cycleYear: number;
     cycleMonth: number;
     shareMinorUnits: Money;
     currencyCode: string;
     sourceLabel: string;
   };
   ```
   In `app/client/src/app/features/parties/parties-service.ts`:
   ```ts
   futureShares(id: string): Observable<FuturePartyShare[]> {
     return this.http
       .get<RowsEnvelope<FuturePartyShare>>(`parties/${id}/future-shares`)
       .pipe(map(envelope => envelope.rows));
   }
   ```
   (The baseUrl interceptor prepends `/v1`. Match the `Money`-branded typing already used for amounts.)

7. **Party-detail page**
   (`app/client/src/app/features/parties/pages/party-detail-page/party-detail-page.ts` + `.html`):
   add `loadFutureShares(id)` beside the existing `loadTimeline(id)` (same `takeUntil(this.destroy$)`
   pattern, a `futureShares` signal + `futureSharesStatus` load-state signal). In the template render a
   **"Scheduled"** section below/next to the posted timeline: one row per `FuturePartyShare` showing the
   month (format `CycleYear`/`CycleMonth`, e.g. "Oct 2026"), the money, and `sourceLabel`, visually marked
   as upcoming/scheduled (not a posted movement). Show an empty/settled note when there are no future rows.

## Tests

- **API — handler** (`app/api/tests/PersonalFinance.Financing.Tests/GetFuturePartySharesHandlerTests.cs`,
  in-memory SQLite; reuse the `AccrueInstallments`/`LinkPaymentPlanSplitHandler` test harness):
  - A card-backed 3-installment plan split 50-50 with one party, none accrued → returns **3** rows,
    ordered by cycle, each share equal to `PhantomPennyAllocator` output (assert exact minor units).
  - An **odd-cent** installment → the projected share matches what `AccrueInstallments` posts (guard the
    "SQL would drift" rationale).
  - **Accrued** (`AccruedOnUtc != null`) and **reversed** installments are excluded.
  - **Non-card** (creditor-financed) plans and **non-split** plans are excluded.
  - Filtering: only the requested `PartyId`'s share is returned for a multi-party split.
- **API — WAF** (`app/api/tests/PersonalFinance.Api.Tests/PartyFutureSharesTests.cs`): create a card split
  via the real pipeline → `GET /v1/parties/{id}/future-shares` returns the scheduled rows; OpenAPI presence
  under the Parties tag with `200`.
- **Client** (`party-detail-page.spec.ts`): `futureShares()` rows render in the Scheduled block; a party
  with no future shares shows the settled/empty note. Posted (accrued) rows still come from the timeline.

## Verify

- API (from `app/api/`): `dotnet test --solution PersonalFinance.sln` — green incl.
  `PersonalFinance.Architecture.Tests`. **No EF migration** (no schema change).
- Client (from `app/client/`): `CHROME_BIN=/usr/bin/brave pnpm ng test --watch=false --browsers=ChromeHeadless`,
  `pnpm ng lint`, `pnpm ng build`.
- Live: create a Sept credit-card expense split with a party → the party-detail page shows Oct/Nov/Dec
  scheduled rows at the correct per-installment share. When October's cycle closes, that row becomes a
  posted timeline entry (and drops out of the future feed). After all N are paid, the party is settled.

## Out of scope

- Any change to accrual, `AccrueInstallments`, or the ledger — the server already posts correctly.
- A per-party badge on the Parties **list** page (the "settled" label until a cycle accrues). Optional
  follow-up; not required to satisfy the "view it as a future expense" goal on the detail page.
- Reporting/dashboard changes — the future feed is served from Financing, Reporting stays views-only.
