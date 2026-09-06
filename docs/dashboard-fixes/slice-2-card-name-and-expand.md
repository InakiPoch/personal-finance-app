# Slice 2 — Card name on future rows + expand hardening (Dashboard)

> Part of the **dashboard-fixes** initiative (3 slices). Do this **after** Slice 1 is green and committed. Touches API (one view + one embedded query + one migration) and the client (`cycleByCard()` computed). Two reported bugs, **one root cause**.

## Context — two symptoms, one cause

On the Dashboard's "Card Debt by Cycle" block:

- **Bug A (issue #1):** some cards render with a **GUID as their name** instead of the card name. Only cards that have *future* (un-accrued) installments are affected; accrued-only cards look fine.
- **Bug B (issue #3):** expanding one card's purchases opens **every** row that belongs to the same physical card.

Both come from a single defect in the Reporting query, plus a fragile client grouping key.

### The Reporting query

`app/api/src/Reporting/PersonalFinance.Reporting/Sql/card_due_by_month.sql` unions two view surfaces:

```sql
SELECT 'Accrued' AS Bucket, CardAccountName AS Card, NULL AS CycleYear, NULL AS CycleMonth,
       AccruedLiabilityMinorUnits AS AmountMinorUnits, CurrencyCode, lower(CardId) AS CardId
FROM vw_card_liability_accrued
UNION ALL
SELECT 'Future' AS Bucket, CardId AS Card, CycleYear, CycleMonth,       -- ← CardId is a GUID
       SUM(AmountMinorUnits) AS AmountMinorUnits, CurrencyCode, lower(CardId) AS CardId
FROM vw_card_future_schedule
GROUP BY CardId, CycleYear, CycleMonth, CurrencyCode, lower(CardId)
ORDER BY CardId, Bucket, CycleYear, CycleMonth;
```

- **Accrued** half labels the row with `CardAccountName` (the Ledger CardLiability account's `Name` — a real name).
- **Future** half labels the row with `CardId` — the raw GUID. **This is Bug A.**

Phase 10 already added `lower(CardId) AS CardId` on *both* halves as a case-safe *join key*, but left the *display* label wrong for Future.

### The client grouping

`app/client/src/app/features/reports/pages/dashboard-page/dashboard-page.ts` — `cycleByCard()` (a `computed`) turns the flat `CardDueRow[]` (`{ bucket, card, cycleYear, cycleMonth, amountMinorUnits, currencyCode, cardId }`) into one visual row per card by **grouping on the label string `card`**, then attaches `cardId` from a `label → cardId` map. Because of Bug A, one physical card produces **two different labels** — the accrued row labelled with its name, the future row labelled with its GUID — so `cycleByCard()` emits **two rows** that nonetheless share the **same `cardId`**.

The template keys expand/collapse on that shared id:

`dashboard-page.html`
```html
@for(card of cycleByCard(); track card.card) {
  ...
  @if(card.cardId; as cardId) {
    <button ... [attr.aria-expanded]="expandedCardId() === cardId" (click)="toggleCardPurchases(cardId)"> ... </button>
  }
  ...
  @if(card.cardId; as cardId) {
    @if(expandedCardId() === cardId) { <div> ...purchases... </div> }
  }
}
```

Two rendered rows with the same `cardId` → clicking one sets `expandedCardId` to that id → **both** rows' `expandedCardId() === cardId` become true → both expand. **This is Bug B.**

## Intent

1. **API:** give future rows a real card **name** (fixes Bug A, even for future-only cards).
2. **Client:** group `cycleByCard()` by the stable **`cardId`** instead of by the label string, so accrued + future of one card always collapse into **one** rendered row regardless of label (definitively fixes Bug B, and hardens it against any future label mismatch — this is the "belt and suspenders" the user asked for).

Both are needed: the API fix alone would only merge the two rows if the Ledger account name and the card name were byte-identical strings, which is not guaranteed. Grouping by `cardId` removes that dependency; the API fix guarantees a friendly label for future-only cards (which have no accrued row to borrow a name from).

## Steps — API (fixes Bug A)

1. **Enrich the future view** — `app/api/src/Modules/Financing/PersonalFinance.Financing/Infrastructure/Persistence/ReadViews/vw_card_future_schedule.sql`:
   ```sql
   CREATE VIEW vw_card_future_schedule AS
   SELECT
       p.CardId           AS CardId,
       p.Id               AS PlanId,
       i.Id               AS InstallmentId,
       i.Sequence         AS Sequence,
       i.CycleYear        AS CycleYear,
       i.CycleMonth       AS CycleMonth,
       i.AmountMinorUnits AS AmountMinorUnits,
       c.Name             AS CardName,            -- NEW
       'ARS'              AS CurrencyCode
   FROM financing_installments i
   JOIN financing_payment_plans p ON p.Id = i.PaymentPlanId
   JOIN financing_credit_cards  c ON c.Id = p.CardId   -- NEW (card is guaranteed non-null by the WHERE below)
   WHERE i.AccruedOnUtc IS NULL
     AND i.IsReversed = 0
     AND p.CardId IS NOT NULL;
   ```
   `financing_credit_cards.Name` is the correct table/column (confirmed in `CreditCardConfiguration.cs`: `ToTable("financing_credit_cards")`, `Property(card => card.Name)`, default column name `Name`). The existing `WHERE p.CardId IS NOT NULL` already excludes card-less creditor-financed plans, so the inner `JOIN` to `financing_credit_cards` never drops a legitimate row.

2. **Fix the Reporting query display label** — `app/api/src/Reporting/PersonalFinance.Reporting/Sql/card_due_by_month.sql`, Future half only:
   - change `CardId AS Card` → `CardName AS Card`
   - keep `lower(CardId) AS CardId` (the join key) untouched
   - add `CardName` to the `GROUP BY` so the non-aggregated column is legal alongside `SUM(AmountMinorUnits)`:
     ```sql
     UNION ALL
     SELECT 'Future' AS Bucket, CardName AS Card, CycleYear AS CycleYear, CycleMonth AS CycleMonth,
            SUM(AmountMinorUnits) AS AmountMinorUnits, CurrencyCode AS CurrencyCode, lower(CardId) AS CardId
     FROM vw_card_future_schedule
     GROUP BY CardName, CardId, CycleYear, CycleMonth, CurrencyCode, lower(CardId)
     ORDER BY CardId, Bucket, CycleYear, CycleMonth;
     ```
   **No migration for this file** — `card_due_by_month.sql` is a Reporting *embedded ADO query string* loaded by `ReportingSqlHelper` at query time (Phase 7 pattern), not a database object. Editing the `.sql` is enough.

3. **Financing view-rebuild migration** — `vw_card_future_schedule` *is* a real DB view, so its definition change needs a drop-and-recreate migration. Follow the Phase-18 `RestoreCardFutureScheduleView` precedent:
   - `dotnet ef migrations add UpdateCardFutureScheduleView --project src/Modules/Financing/PersonalFinance.Financing --startup-project src/Modules/Financing/PersonalFinance.Financing --context FinancingDbContext`
   - The scaffold will be empty (a keyless `ToView` mapping change emits no DDL). Fill `Up` with `DROP VIEW IF EXISTS vw_card_future_schedule;` + the new `CREATE VIEW ...` (the SQL from step 1); fill `Down` with the drop + the *previous* `CREATE VIEW` (the version without `CardName`/the card join).
   - No table rebuild is involved (we're only changing a view), so the Phase-18 "EF defers the table rebuild past trailing `Sql()`" caveat does **not** apply — a single migration is fine here.
   - Apply: `dotnet ef database update --project src/Modules/Financing/PersonalFinance.Financing --startup-project src/Modules/Financing/PersonalFinance.Financing --context FinancingDbContext`.

## Steps — Client (fixes Bug B)

4. **Regroup `cycleByCard()` by `cardId`** in `dashboard-page.ts`. Current logic builds `order: string[]` of **labels** and maps each label to a `cardId`. Change it to key the grouping on `cardId`:
   - Group the accrued and future amounts by `row.cardId` (not `row.card`).
   - Choose one display label per `cardId`: **prefer the accrued row's label** (already correct per the user), **fall back to the future row's label** (now a real name after the API fix). Never prefer a GUID.
   - Emit one `CardCycle` per distinct `cardId` with `{ card: <chosen label>, cardId, accrued, future, total }`.
   - Preserve current ordering intent (accrued-first / existing order) as closely as possible; if the current order was label-driven, drive it off the first-seen `cardId`.
   - Rows in `card_due_by_month` always have a non-null `cardId` (both view surfaces require a card), so a `cardId`-keyed grouping is safe here; keep a defensive fallback for a null id (render as a plain, non-expandable row exactly as today).

   **Template needs no structural change** — `@for(...; track card.card)` + the two `@if(card.cardId; as cardId)` guards + `expandedCardId() === cardId` all stay correct once each `cardId` appears on exactly one row. (Optionally switch the `@for` `track` to `card.cardId` for a cleaner key, but it is not required for correctness.)

## Tests

- **API** — `tests/PersonalFinance.Reporting.Tests` (uses `ReportingIntegrationFixture`, the multi-context migration harness): add a fact that seeds a credit card + a payment plan whose installments are **future** (un-accrued), runs `CardDueByMonthQuery`, and asserts the returned **Future** row's `Card` equals the card's **Name** (not its GUID / not equal to `CardId`).
- **Client** — `dashboard-page.spec.ts`: feed the component `cardDueRows()` with one **Accrued** row and one **Future** row that share the same `cardId` but carry different `card` labels; assert `cycleByCard()` returns **exactly one** row for that card; assert expanding it (`toggleCardPurchases`) sets only that card's detail visible and does not reveal a second card's block. Add/adjust a case where a **future-only** card's row label is the name, not a GUID.

## Verify

- API (from `app/api/`): `dotnet test --solution PersonalFinance.sln` — green incl. `PersonalFinance.Architecture.Tests` (RNF-9; no new module edge — view + embedded query + client only). Apply the Financing migration (command above).
- Client (from `app/client/`): `CHROME_BIN=/usr/bin/brave pnpm ng test --watch=false --browsers=ChromeHeadless`, `pnpm ng lint`, `pnpm ng build`.
- Live: on a running host with a card that has future installments, the Dashboard shows the card **name** (not a GUID) and expanding that card opens **only** its own purchases.

## Out of scope

- No change to the accrued surface (`vw_card_liability_accrued`) — it is already correct.
- No change to the case-safe `CardId` join key introduced in Phase 10.
- No Ledger migration (nothing Ledger-side changes).
