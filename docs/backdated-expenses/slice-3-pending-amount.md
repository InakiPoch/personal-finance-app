# Slice 3 — Pending amount (OPTIONAL)

> Read `00-overview.md` first, and ship Slices 1 & 2 green before this. **Optional** — the core
> behavior (paid counts + next month) is complete without it. Build this only if the user still
> wants a dollar figure for what's left.

## Goal

Surface **pending $** for a purchase = Σ(amount of unpaid, non-reversed installments) — so the
user can see "how much is left of the total", not just the count. Small, additive, display-only.

## Why this way

Q6 clarified the counts + next-month labels on Recent Purchases are enough on their own; the
only missing thing is being able to *see how much is pending from the total*. Per-installment
amounts already exist in the Statement view and in the ledger, so this is purely a small
read/display addition — no ledger, no schema.

## Where it lives (pick one; Recent Purchases is the default)

The purchase's pending amount is the natural companion to the existing
`"{paidInstallmentCount}/{installmentCount} paid · next: {month}"` sub-line on the Recent
Purchases row.

- **Recommended:** add pending $ to the Recent Purchases row, e.g.
  `"2/3 paid · $1000 pending · next: Sep 2026"`.
- Alternative: the statement header, or the load-expense confirmation panel. Choose based on
  where the user expects to glance for it.

## The mechanics

Pending amount = Σ over installments where `PaidOnUtc == null && !IsReversed` of `Amount`.

- **Server-computed (preferred):** extend the query that builds `RecentPurchaseRow`
  (`ListRecentPurchasesHandler`, the one that already derives `PaidInstallmentCount`,
  `NextDueYear`, `NextDueMonth`) to also project a `PendingAmountMinorUnits`. Add the field to
  the row DTO and the client `recent-purchase-row.ts` type, then render it in
  `recent-purchases-table.{ts,html}` beside the existing labels.
- Keep it consistent with the existing paid/next derivation — same filter semantics
  (non-reversed), same money units (minor units → formatted on the client).

## Steps

1. **API query.** Add `PendingAmountMinorUnits` (Σ unpaid, non-reversed `Amount`) to the
   Recent Purchases row projection in `ListRecentPurchasesHandler` and its row record.
2. **API test.** Extend the ListRecentPurchases tests: a partly-paid back-dated purchase reports
   the correct pending sum; a fully-paid one reports `0`.
3. **Client type.** Add `pendingAmountMinorUnits` to
   `features/financing/types/recent-purchase-row.ts`.
4. **Client render.** In `recent-purchases-table.{ts,html}`, add a `pendingLabel()` (format
   minor units → money) and place it in the sub-line; hide it (or show `—`) when pending is `0`.
5. **Client test.** `recent-purchases-table` spec: label renders for a partly-paid row, hidden
   for a fully-paid row.

## Gate

Run the verification gate from `00-overview.md`. Green, lint + prod build clean. This closes the
initiative.
