# Slice 1 — Installments "paid of total" (Dashboard)

> Part of the **dashboard-fixes** initiative (3 slices). Implement and test **this slice alone**, get it green, let the user commit, then move to Slice 2. Client-only — no API, no migration.

## Context

On the Dashboard, the "Card Debt by Cycle" block can be expanded per card into its individual outstanding purchases (built in API Phase 16 — `GET /v1/financing/cards/{id}/purchases`). Each purchase row shows installment progress. It currently reads like **"6 of 6 installments outstanding"** when nothing has been paid yet, and **"3 of 6"** when 3 remain unpaid — i.e. it prints the *outstanding* count first. The user reads "N of N" as "how many have I paid," so the number is confusing and, on a fresh purchase, looks like everything is already paid.

The user wants it to answer the real question: **how many installments have I paid, out of the total.**

## Root cause

`CardPurchaseRow` already carries both numbers, so nothing is missing from the API:

- `app/client/src/app/features/financing/types/card-purchase-row.ts`
  ```ts
  export type CardPurchaseRow = {
    planId: string;
    description: string;
    totalMinorUnits: Money;
    installmentCount: number;    // total installments
    outstandingCount: number;    // installments still unpaid
    purchaseDate: IsoDate;
  };
  ```

The template simply binds the wrong field first:

- `app/client/src/app/features/reports/pages/dashboard-page/dashboard-page.html` (~line 136)
  ```html
  — {{ purchase.outstandingCount }} of {{ purchase.installmentCount }} installments outstanding
  ```

Paid = `installmentCount - outstandingCount`. Pure display arithmetic; no new data.

## Intent

Show **paid of total**: `{{ installmentCount - outstandingCount }} of {{ installmentCount }} installments paid`.

## Steps

1. **Edit the template** `dashboard-page.html` (the `@for(purchase of expandedPurchases(); track purchase.planId)` block, ~line 136):
   ```diff
   - — {{ purchase.outstandingCount }} of {{ purchase.installmentCount }} installments outstanding
   + — {{ purchase.installmentCount - purchase.outstandingCount }} of {{ purchase.installmentCount }} installments paid
   ```
   No `.ts`, type, or service change. `expandedPurchases()` and the row shape stay exactly as they are.

## Tests

- `app/client/src/app/features/reports/pages/dashboard-page/dashboard-page.spec.ts` — the existing purchases fixture already includes a row with `installmentCount: 6, outstandingCount: 3` (`{ planId: 'p1', description: 'New laptop', ... }`). Update/add an assertion that, once a card is expanded, the rendered purchase text contains **"3 of 6 installments paid"** (and no longer "outstanding"). If the spec currently asserts the old "outstanding" string, flip it.

## Verify (from `app/client/`)

```
CHROME_BIN=/usr/bin/brave pnpm ng test --watch=false --browsers=ChromeHeadless
pnpm ng lint
pnpm ng build
```
All green. Live check (optional): expand a card with a partially-paid plan on the running Dashboard and confirm it reads "X of N installments paid" with X = paid count.

## Out of scope

- No API/DTO/type change (both counts already arrive).
- No wording change elsewhere (statement/timeline views keep their own copy).
- Not the card-name or expand-all bug — that's Slice 2.
