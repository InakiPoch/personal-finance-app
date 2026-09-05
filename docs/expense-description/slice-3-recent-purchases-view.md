# Slice 3 — "Recent purchases" view

> **Read this whole file before writing any code.** Self-contained; assumes no prior
> context. Depends on **Slice 1** (the `PaymentPlan.Description` field). Independent of
> Slice 2's endpoint, though it reuses the same patterns. This is the final slice.

---

## 1. Why this exists (context & intent)

Slice 1 gave every expense a human `Description`; Slice 2 let the user drill into a
card's debt. This slice adds the simplest, most direct answer to *"what have I been
buying?"* — a **standalone chronological list of recent purchases** (loaded expenses),
each with its description, independent of card grouping or debt state. It's a browseable
history, not a debt view: it lists purchases whether or not they're paid off.

### Locked design decisions

| # | Decision | Answer |
|---|----------|--------|
| D1 | Content | A flat, **newest-first** list of `PaymentPlan`s (all of them, not filtered by outstanding), each showing description + context. |
| D2 | Row fields | `description`, card name, `purchaseDate`, `totalMinorUnits`, `installmentCount`, `isCreditorPayment`. |
| D3 | Module boundary | **Financing-only.** New read query + host endpoint. No migration, no Ledger edge, no `IFinancingApi` change (dispatch via `IQueryBus`). |
| D4 | Surface | A **new client page** with its own route and a nav entry. |

---

## 2. Data model

Source: all `PaymentPlan` rows (Financing). Join to `CreditCard` for the card **name**
(`PaymentPlan.CardId` → `CreditCard.Name`). No installment/statement logic — this is not
a debt view, so no "outstanding" filter.

| Field | Source |
|-------|--------|
| `planId` | `PaymentPlan.Id` |
| `description` | `PaymentPlan.Description` (Slice 1) |
| `cardName` | `CreditCard.Name` (lookup by `CardId`) |
| `purchaseDate` | `PaymentPlan.PurchaseDate` |
| `totalMinorUnits` | `PaymentPlan.Total` |
| `installmentCount` | `PaymentPlan.InstallmentCount` |
| `isCreditorPayment` | `PaymentPlan.CreditorId is not null` |

Order newest-first. `PaymentPlan.Id` is a `Guid.CreateVersion7()` (time-ordered) in this
codebase, so ordering by `Id` descending is a safe newest-first proxy and avoids the
SQLite `DateTimeOffset` ordering trap; alternatively project then order by `PurchaseDate`
descending in memory (never order a `DateTimeOffset` server-side).

> Optional: cap to a sensible page size (e.g. most recent 100) rather than unbounded.
> Keep it simple — a `limit` is fine; pagination is out of scope.

---

## 3. API — new Financing read query + host endpoint

Same additive-CQRS pattern as Slice 2 / `GetCardStatements`: query in
`Financing.Contracts`, internal handler in `FinancingModule`, host dispatches via
`IQueryBus`. **No `IFinancingApi` edge, no schema change, no migration** → RNF-9 green.

### 3.1 Contracts
`Financing.Contracts/Queries/ListRecentPurchasesQuery.cs`:
```csharp
public sealed record ListRecentPurchasesQuery(int Limit = 100) : IQuery<RecentPurchasesResponse>;
public sealed record RecentPurchasesResponse(IReadOnlyList<RecentPurchaseRow> Rows);
public sealed record RecentPurchaseRow(
    Guid PlanId,
    string Description,
    string CardName,
    DateOnly PurchaseDate,
    long TotalMinorUnits,
    int InstallmentCount,
    bool IsCreditorPayment);
```

### 3.2 Handler
`Application/Queries/ListRecentPurchases/ListRecentPurchasesHandler.cs` — query
`PaymentPlans`, join `CreditCards` for the name, project rows, order newest-first
(by `Id` desc or in-memory by `PurchaseDate` desc), take `Limit`. Register in
`FinancingModule.Register`.

### 3.3 Host endpoint
- `Endpoints/Financing/GetRecentPurchases.cs` — dispatch `new ListRecentPurchasesQuery()`
  via `IQueryBus.AskAsync`, map to DTO, return `Ok<RecentPurchasesDto>`.
- `ApiRoutes.cs` — add `Financing.Purchases = "/payment-plans"` (a `GET` on the same
  base as the existing `POST /v1/financing/payment-plans`; add the `MapGet` alongside the
  `MapPost`), **or** a clearer `Financing.RecentPurchases = "/purchases/recent"`. Prefer
  `GET /v1/financing/payment-plans` for REST symmetry with the create route; pick one and
  keep it consistent with the DTO/mapping naming.
- DTO `Endpoints/DTOs/RecentPurchasesDTO.cs` + mapping in `FinancingMappingExtensions.cs`
  (build DTOs via the mapping method). Wire with `.Produces<RecentPurchasesDto>(200)` and
  `.WithTags("Financing")`.

Response: `{ rows: [{ planId, description, cardName, purchaseDate, totalMinorUnits,
installmentCount, isCreditorPayment }] }`.

---

## 4. Client — new page + route + nav

### 4.1 Type
`features/financing/types/recent-purchase-row.ts` — mirror the API row (`Money` for
`totalMinorUnits`, `IsoDate` for `purchaseDate`).

### 4.2 Service
`features/financing/financing-service.ts` — add
`recentPurchases(): Observable<RecentPurchaseRow[]>` calling the chosen route and
unwrapping the `rows` envelope.

### 4.3 Page component
`features/financing/pages/recent-purchases-page/` — new standalone component
(`recent-purchases-page.ts/.html/.css`) following the existing feature-page conventions
(Signals-first; load rows in a resource/effect or on init; loading/empty/error states).
Render a list: description as the headline, `formatArs(totalMinorUnits)`, card name,
`purchaseDate`, installment count, and a subtle creditor marker. Style strictly per
`docs/SYSTEM.md` ("warm homebanking", tokens only — no hardcoded hex), consistent with
the other restyled pages (e.g. transactions/statements pages).

### 4.4 Route + navigation
- Register the route (the app keeps routes in the router config; add a `recent-purchases`
  path pointing at the new page — follow how the other feature pages are registered).
- Add a **nav entry** in the app shell (`app.html` / nav-shell) so the page is reachable —
  the client's known weak spot is discoverability, so wire it into the global nav and,
  if there's a Dashboard hub of links, add it there too.

---

## 5. Testing

### 5.1 API (xUnit v3, in-memory SQLite handler test)
`ListRecentPurchasesHandler` tests:
- Returns rows newest-first.
- Each row carries the plan's `description` and the correct `cardName`.
- `isCreditorPayment` reflects `CreditorId`.
- `Limit` caps the count.
- Optional `Api.Tests`: OpenAPI presence under `Financing` with `200`.

### 5.2 Client (Karma/Jasmine, zoneless)
`recent-purchases-page.spec.ts`:
- On init the page calls `financingService.recentPurchases()` and renders the rows
  (each showing its description), newest-first.
- Empty state renders when the list is empty.
- Mock the service with a Jasmine spy returning a fixed `RecentPurchaseRow[]`.

Run:
```bash
dotnet test --solution PersonalFinance.sln                                  # app/api/
CHROME_BIN=/usr/bin/brave pnpm ng test --watch=false --browsers=ChromeHeadless  # app/client/
pnpm ng lint && pnpm ng build
```

---

## 6. Verification — Slice 3 done when

- [ ] `GET` recent-purchases returns all purchases newest-first with `description` + `cardName`, capped by `Limit`.
- [ ] No new EF migration, no `IFinancingApi` change; `PersonalFinance.Architecture.Tests` green.
- [ ] A new "Recent purchases" page exists, is reachable from the global nav, and lists purchases with their descriptions per SYSTEM.md.
- [ ] `dotnet test --solution` green (higher count); client `ng test` + `ng lint` + `ng build` green.

Manual end-to-end: run the API, load a couple of expenses with distinct descriptions,
open the Recent purchases page, confirm they appear newest-first with the right text.

---

## 7. Out of scope for this slice

- Any Ledger change; the debt/drill-down concerns of Slice 2.
- Editing/deleting purchases from this view (read-only list).
- Pagination, search, or filtering (a simple `Limit` cap is enough).
- Surfacing description on statements or the `/v1/ledger/transactions` feed.
