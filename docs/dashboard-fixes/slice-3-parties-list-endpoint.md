# Slice 3 — Parties list endpoint (make created parties visible)

> Part of the **dashboard-fixes** initiative (3 slices). Do this **last**, after Slices 1 and 2 are green and committed. Full vertical: API (Parties module + host) + client (service, Parties page, load-expense split). Mirrors the existing **Creditors** list vertical end-to-end.

## Context — a created party is invisible everywhere

A `Party` created via `POST /v1/parties` persists to `parties_parties` (confirmed: `CreatePartyHandler` adds it and `SaveChangesAsync`, independent of any shared expense). Yet the user cannot see it — not on the Parties page, not in the load-expense split selector.

**Why:** the Parties module never got a plain "list all" endpoint. The only way the client enumerates parties today is `GET /v1/reports/parties/debt-summary` (`ReportsService.debtSummary()`), which the **Parties page** *and* the **load-expense split** both call. That report is built on `vw_current_account_timeline`:

```sql
FROM parties_parties p
INNER JOIN vw_receivable_account_movements m ON m.AccountId = p.ReceivableAccountId;
```

The **INNER JOIN** drops any party with zero ledger movements. A brand-new party has no shared expense yet → no movements → it never appears. The UI is even honest about it ("New parties appear in the list above once they take part in a shared expense") — but that is exactly the behaviour the user wants gone.

The registered Parties endpoints are: `POST /v1/parties`, `POST /v1/parties/shared-expenses`, `POST /v1/parties/{id}/settlements`, `GET /v1/parties/{id}/balance`, `GET /v1/parties/{id}/timeline`. **There is no `GET /v1/parties`.**

## Intent

Add a proper **`GET /v1/parties`** list endpoint **in the Parties module** — the same shape Instruments and Creditors already use ("list under the creation form"). Point both consumers at it:

- **Parties page:** list **every** party from the new endpoint, and **keep the balance column** by merging with `debt-summary` — parties with no movements show **"settled" / $0** (user's chosen behaviour).
- **Load-expense split:** source the split's party list from the new endpoint so **brand-new parties are immediately splittable**; the "Add participant" button (`[disabled]="parties().length === 0"`) then reflects real party existence.

This also removes the cross-module smell of Financing (load-expense) depending on a Reporting debt query just to name people.

## Reference — the pattern to copy (Creditors)

- API contract: `Financing.Contracts/Queries/ListCreditorsQuery.cs` → `CreditorRow`, `ListCreditorsResponse`, `ListCreditorsQuery`.
- Handler: `Application/Queries/ListCreditors/ListCreditorsHandler.cs` (`context.Creditors...ToListAsync()`, in-memory `OrderBy(Name, OrdinalIgnoreCase)`).
- Facade: `IFinancingApi.ListCreditorsAsync`; registered in the module.
- Host: `ApiRoutes.Creditors.List = "/"`, `Endpoints/Creditors/GetCreditors.cs`, `Endpoints/DTOs/CreditorsDTO.cs`, `CreditorMappingExtensions`, `group.MapGet(...).Produces<CreditorListDto>(200)`.
- Client: `creditors-service.ts` `list()` → `GET creditors`; `creditors-page` renders `@for(creditor of creditors())` under the form.

Parties is the same story, one module over.

## Steps — API (Parties module)

1. **Query contract** — new file `app/api/src/Modules/Parties/PersonalFinance.Parties.Contracts/Queries/ListPartiesQuery.cs`:
   ```csharp
   using PersonalFinance.Abstractions.Messaging;

   namespace PersonalFinance.Parties.Contracts.Queries;

   public sealed record PartyRow(Guid Id, string Name);
   public sealed record ListPartiesResponse(IReadOnlyList<PartyRow> Rows);
   public sealed record ListPartiesQuery() : IQuery<ListPartiesResponse>;
   ```

2. **Handler** — new file `app/api/src/Modules/Parties/PersonalFinance.Parties/Application/Queries/ListParties/ListPartiesHandler.cs`:
   ```csharp
   internal sealed class ListPartiesHandler(PartiesDbContext context)
       : IQueryHandler<ListPartiesQuery, ListPartiesResponse> {
       public async Task<ListPartiesResponse> HandleAsync(ListPartiesQuery query, CancellationToken cancellationToken) {
           var parties = await context.Parties
               .Select(party => new { party.Id, party.Name })
               .ToListAsync(cancellationToken);
           var rows = parties
               .OrderBy(party => party.Name, StringComparer.OrdinalIgnoreCase)
               .Select(party => new PartyRow(party.Id, party.Name))
               .ToList();
           return new ListPartiesResponse(rows);
       }
   }
   ```
   No ledger join → **every** party appears, debt or not. (Sort in memory with `OrdinalIgnoreCase`, matching the Creditors/Instruments handlers; server-side `OrderBy` on a string is fine, but keep parity with the precedent.)

3. **Facade** — add to `IPartiesApi` (Contracts): `Task<ListPartiesResponse> ListPartiesAsync(ListPartiesQuery query, CancellationToken cancellationToken);` and implement in `Infrastructure/PublicApi/PartiesApi.cs` as `queryBus.AskAsync(query, cancellationToken)`.

4. **Register** the handler in `PartiesModule.Register` alongside the existing query handlers (`GetCurrentAccountBalanceQuery`, `GetCurrentAccountTimelineQuery`):
   ```csharp
   services.AddScoped<IQueryHandler<ListPartiesQuery, ListPartiesResponse>, ListPartiesHandler>();
   ```
   All additions are `.Contracts`-only across the module edge → `PersonalFinance.Architecture.Tests` (RNF-9) stays green.

5. **Host wiring:**
   - `ApiRoutes.cs` — add `public const string List = "/";` to the `Parties` record (sits next to `Create = "/"`; a `GET` at `/` alongside the existing `POST` at `/`).
   - `Endpoints/Parties/GetParties.cs`:
     ```csharp
     public static class GetParties {
         public static async Task<Ok<PartiesListDto>> Handle(IPartiesApi parties, CancellationToken cancellationToken) {
             var response = await parties.ListPartiesAsync(new ListPartiesQuery(), cancellationToken);
             return TypedResults.Ok(response.ToPartiesListDto());
         }
     }
     ```
   - `Endpoints/DTOs/PartiesListDTO.cs` — `public sealed record PartyRowDto(string Id, string Name);` + `public sealed record PartiesListDto(IReadOnlyList<PartyRowDto> Rows);` (id serialized as string, matching the Creditors DTO convention).
   - Mapping extension (`Endpoints/Mapping/PartyMappingExtensions.cs` or a new `PartiesListMappingExtensions.cs`, honouring the C# style rule on grouping extensions per receiver): `ToPartiesListDto(this ListPartiesResponse response)` → maps each `PartyRow` to `PartyRowDto(row.Id.ToString(), row.Name)`.
   - `EndpointExtensions.MapPartiesEndpoints` — add:
     ```csharp
     group.MapGet(ApiRoutes.Parties.List, GetParties.Handle)
         .WithSummary("List registered parties.")
         .WithDescription("Returns every registered party, ordered by name.")
         .Produces<PartiesListDto>(StatusCodes.Status200OK);
     ```

## Steps — Client

6. **Service** — `app/client/src/app/features/parties/parties-service.ts`: add
   ```ts
   list(): Observable<Party[]> {
     return this.http
       .get<RowsEnvelope<Party>>('parties')
       .pipe(map((envelope: RowsEnvelope<Party>) => envelope.rows));
   }
   ```
   Add a `Party` type `{ id: string; name: string }` (a new `features/parties/types/party.ts`, matching `Instrument`/`Creditor` types). The baseUrl interceptor prepends `/v1`.

7. **Parties page** (`features/parties/pages/parties-page/parties-page.ts` + `.html`): load **both** `partiesService.list()` (full roster) and `reports.debtSummary()` (balances); merge by id into the rendered rows. Every party renders; a party with no matching debt row shows a **"settled"** label / `$0` in the balance column (do not drop it). Keep the existing routerLink to `/parties/{id}` and the row styling. Update the empty state to reflect "no parties yet" rather than "no parties with movements yet."

8. **Load-expense split** (`features/financing/pages/load-expense-page/load-expense-page.ts` — `loadParties()`): switch the source from `reportsService.debtSummary()` to `partiesService.list()`. The split only needs `id` + `name`; adjust the `parties()` signal type and every template/logic reference from the `PartyDebtRow` fields (`partyId`, `partyName`) to the `Party` fields (`id`, `name`). The "Add participant" button's `[disabled]="parties().length === 0"` now enables as soon as any party exists.

## Tests

- **API:**
  - `tests/PersonalFinance.Parties.Tests/ListPartiesHandlerTests.cs` (in-memory SQLite harness — the `OnPaymentPlanCreatedTests` precedent): create two parties (no movements), assert `ListPartiesQuery` returns both, name-ordered; assert a party with **zero** ledger movements still appears (the whole point).
  - `tests/PersonalFinance.Api.Tests/PartiesListTests.cs` (WAF): `POST /v1/parties` a party → `GET /v1/parties` includes it with the right `id`/`name`; OpenAPI presence under the Parties tag with `200`.
- **Client:**
  - `parties-page.spec.ts`: a party returned by `list()` but absent from `debtSummary()` still renders and shows "settled".
  - load-expense split spec: the split sources from `partiesService.list()`; "Add participant" is enabled when at least one party exists, disabled when none.

## Verify

- API (from `app/api/`): `dotnet test --solution PersonalFinance.sln` — green incl. `PersonalFinance.Architecture.Tests`. No EF migration (no schema change — `parties_parties` already exists).
- Client (from `app/client/`): `CHROME_BIN=/usr/bin/brave pnpm ng test --watch=false --browsers=ChromeHeadless`, `pnpm ng lint`, `pnpm ng build`.
- Live: `POST /v1/parties` a fresh party (no shared expense), then `GET /v1/parties` returns it; it now shows on the Parties page (as "settled") and is selectable in the load-expense split.

## Out of scope

- No change to `debt-summary` / `vw_current_account_timeline` (its INNER JOIN stays — it remains the balances source; the new endpoint is the roster source).
- No balance field on the new `GET /v1/parties` payload — balances come from the merge on the client (keeps the list endpoint a pure Parties concern, no Ledger coupling).
- No party edit/delete, no inline party creation from the expense form.
