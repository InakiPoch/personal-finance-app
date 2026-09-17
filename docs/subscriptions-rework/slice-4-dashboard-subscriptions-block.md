# Slice 4 — Dashboard subscriptions block (read-only)

> Read `00-overview.md` first. This slice consumes the `status` field added in Slice 1 and is best shipped
> after Slice 2 so the badge it shows is actionable (the user can go to the Subscriptions page and pay).

## Goal / Why

Deliver the informational payoff the user asked for: a **read-only** Dashboard block that, for each
active subscription, shows **name, renewal date, flat cost `X`, and paid/overdue status** — and confirms
that overdue (unpaid) subscriptions are **not** part of the "Out of pocket" total. No behaviour here; the
Pay/Undo actions live on the Subscriptions page (decision 2).

## Context & current state

- The Dashboard (`app/client/src/app/features/reports/pages/dashboard-page/`) reads **no** subscription
  data today. It injects `ReportsService` + `FinancingService` and renders: header, **Out of pocket**,
  **Card debt by cycle**, and a Quick-actions nav.
- After Slice 1, `GET /v1/subscriptions/active` returns `status` (`paid | overdue | upcoming`) alongside
  `name`, `amountMinorUnits` (flat `X`), `anchorDay`, `nextDueDate`. **No new API is needed.**
- Out-of-pocket already excludes unpaid subs (Slice 1) because it derives from the ledger and unpaid
  periods have no posting — this slice only needs to *display* that truth, not compute it.

## Client changes (client-only slice)

- `dashboard-page.ts`: inject `SubscriptionsService`; add an `activeSubscriptions` signal populated on
  load via `listActive()` (mirror the existing `loadMonthlyExpenses` / `loadCardDueByMonth` pattern).
  Keep it resilient — a subscriptions load failure must not blank the rest of the Dashboard.
- `dashboard-page.html`: add a new `<section>` **after** "Card debt by cycle", as a panel matching that
  block's chrome: `rounded-card border border-rule bg-paper-raised p-6 shadow-card`, a
  `text-xs font-semibold uppercase tracking-[0.14em] text-heading` header ("Subscriptions" or
  "Active subscriptions"), and a chevron link to `/subscriptions`. Inside, a `<ul>` (or small table) with
  one row per active subscription showing:
  - **name** (`text-ink`);
  - **renewal date** — `nextDueDate` (`text-ink-faint`, `[font-variant-numeric:tabular-nums_lining-nums]`);
  - **flat cost** — `formatArs(amountMinorUnits)` (the single `X`, never a running total);
  - **status badge** — reuse the Slice-1 badge idiom: `paid` → hairline pill `color: var(--ledger)`;
    `overdue` → `text-negative` small-caps; `upcoming` → `text-ink-faint` small-caps.
- States (per `SYSTEM.md`): **loading** (`text-ink-faint`), **error** (`role="alert"`, `text-negative`),
  **empty** (no active subs → a settled/"nothing scheduled" line, `text-ledger`/`text-ink-faint`),
  **ready**. All colours must be tokens (no raw hex).
- Keep the block **read-only** — no Pay/Undo buttons here; the chevron link sends the user to
  `/subscriptions` where the actions live.

## Testing

**Client** (`CHROME_BIN=/usr/bin/brave pnpm ng test --watch=false --browsers=ChromeHeadless`):

1. `dashboard-page.spec.ts`: with active-subscription fixtures (one `paid`, one `overdue`, one
   `upcoming`), the block renders one row each with the correct badge, renewal date, and flat cost.
2. **Overdue excluded from out-of-pocket**: assert the "Out of pocket" hero total equals the sum of the
   monthly-expense fixtures only, and does **not** change when an `overdue` subscription is present in the
   subscriptions fixture (they come from independent sources — this pins that the block is purely
   informational and does not feed the total).
3. Loading, empty, and error states render correctly (subscriptions failure does not break the rest of
   the Dashboard).
4. The chevron links to `/subscriptions`.

There is no API change, so no new backend tests. (Optionally confirm the Dashboard's existing
`ReportsService`/`FinancingService` specs still pass with the added `SubscriptionsService` injection.)

## Out of scope

- Any Pay/Undo interaction on the Dashboard (decision 2 — behaviour stays in the Subscriptions view).
- Any new or changed API endpoint.
- Cadence/frequency changes.

## Verification

1. `cd app/client && pnpm ng lint && pnpm ng build --configuration production`; run client tests.
2. End-to-end: open the Dashboard — the Subscriptions block lists every active sub with its renewal date,
   flat cost, and a **Paid**/**Overdue**/**Upcoming** badge. Confirm an overdue sub is visible in the
   block but **not** reflected in the "Out of pocket" total. The chevron opens `/subscriptions`, where Pay
   turns an overdue sub Paid and the total updates on next Dashboard load.
3. This closes the initiative.
