# Dashboard items — verify-only (already implemented on this branch)

> Part of the **parties-card-split** initiative. This is **not** an implementation slice. The four
> dashboard issues the user reported are already coded on branch `chore/client-sidejobs`. The task is to
> confirm them on a **clean build**. Only if one still fails after a fresh build does it become its own
> slice.

## Why this is verify-only

The most likely reason the user still saw these misbehave is a **stale client build / cached browser
bundle**, not missing code. Investigation located the implemented fix for each item in the current tree.

## First: rebuild and hard-refresh

From `app/client/`:
```
pnpm ng build
CHROME_BIN=/usr/bin/brave pnpm ng test --watch=false --browsers=ChromeHeadless
```
Then run the app fresh and **hard-refresh** the browser (clear the cached bundle) before judging any item.

## The four items and where each is already handled

### #1 — Future expense rows showed a GUID instead of the card name
**Status: implemented.**
- `app/api/src/Modules/Financing/PersonalFinance.Financing/Infrastructure/Persistence/ReadViews/vw_card_future_schedule.sql`
  joins `financing_credit_cards` and selects `c.Name AS CardName` (only card-backed plans; `CardId IS NOT NULL`).
- `app/api/src/Reporting/PersonalFinance.Reporting/Sql/card_due_by_month.sql` emits `CardName AS Card` for the
  Future bucket and groups by it.
- Migration `...Migrations/20260906031333_UpdateCardFutureScheduleView.cs` drops/recreates the view with the join.

**Verify:** create a card purchase with future installments → the dashboard "Card Debt by Cycle" future rows
show the **card name**, not a GUID.

### #2 — Parties/split option disabled on the new-expense form
**Status: implemented.**
- `app/client/.../load-expense-page/load-expense-page.ts` `loadParties()` (~line 245) calls
  `partiesService.list()` (the `GET /v1/parties` roster), not the old `reportsService.debtSummary()`.
- The "Add participant" button `[disabled]="parties().length === 0"` (`...load-expense-page.html` ~line 320)
  enables as soon as any party exists. (Commits `e86e4dd`, `f36fc64`.)

**Verify:** with at least one party registered, the split "Add participant" control is enabled on a new expense.

### #3 — Expanding one account opened every account's details
**Status: implemented (and note the terminology).** The dashboard has a **single** expandable block,
"Card Debt by Cycle" — there is **no separate Accounts list**.
- `app/client/.../dashboard-page/dashboard-page.ts` `cycleByCard()` (~lines 76–126) groups by a **stable
  `cardId`** key; `expandedCardId: WritableSignal<string | null>` (~line 128) holds a single expanded card;
  `toggleCardPurchases(cardId)` (~line 155) sets it.
- `dashboard-page.html` `@for(card of cycleByCard(); track card.cardId ?? card.card)` (~line 86) and
  `@if(expandedCardId() === cardId)` (~line 121) reveal only the matching card.

**Verify:** with two or more cards, expanding one opens **only** that card's purchases.

### #4 — Installments showed "n of n" instead of paid-of-total
**Status: implemented.**
- `app/client/.../dashboard-page/dashboard-page.html` (~line 136) renders
  `{{ purchase.installmentCount - purchase.outstandingCount }} of {{ purchase.installmentCount }} installments paid`.
- `app/api/.../Financing/Application/Queries/GetCardPurchases/GetCardPurchasesHandler.cs` computes
  `OutstandingCount` as the count of non-paid, non-reversed installments; `CardPurchaseRow` carries both
  `installmentCount` and `outstandingCount`. (Commit `3b7a098`.)

**Verify:** a 3-installment purchase with 1 paid shows **"1 of 3 installments paid"**.

## If an item still fails on a clean build

Only then treat it as a real defect: open a dedicated slice doc in this folder
(`slice-<n>-<short-name>.md`) following the same API + client + tests vertical shape as Slice 2b, capturing
the reproduction on the fresh build and the specific divergence found.
