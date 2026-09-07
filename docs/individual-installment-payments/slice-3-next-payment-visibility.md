# Slice 3 — "Next payment for this purchase" visibility

> Read [00-overview.md](00-overview.md) and finish [slice-1](slice-1-foundation-payable-from-installments.md)
> and [slice-2](slice-2-pay-single-installment.md) (green) first. This slice is a **pure additive
> read** — no new writes, no ledger changes.

## Intent

Make each purchase legible at a glance on the **Recent Purchases** list:

> "3/12 paid · next: Nov 2026"  (or "Fully paid" when nothing remains)

"Next payment" is **derived** — the earliest un-paid, un-reversed installment's `DueCycle`.
Nothing is rescheduled; this only surfaces what already exists.

## API changes (`app/api/`)

Recent-purchases query/handler behind `GET /v1/financing/purchases/recent` (under
`src/Modules/Financing/PersonalFinance.Financing/Application/Queries/…`; the handler currently
returns `RecentPurchaseRow`-shaped rows via a `{ rows }` envelope).

For each plan, project two derived values from its installments:

- `paidInstallmentCount = count(installment.IsPaid)`.
- `nextDue = min(sequence where !IsPaid && !IsReversed)` → take that installment's `DueCycle`
  → `(Year, Month)`. **Null** when every installment is paid (or reversed).

Reuse `BillingCycle.DueCycle` (do not recompute the +1 by hand). Reuse the
`IsReversed == false` predicate shape from `GetCreditorPayablesHandler`.

Extend the response DTO row with `PaidInstallmentCount`, `NextDueYear` (nullable) and
`NextDueMonth` (nullable). `installmentCount` (total) is already present.

## Client changes (`app/client/`)

All under `src/app/features/financing/`.

### 1. Type — `types/recent-purchase-row.ts`
Add to `RecentPurchaseRow`:
```ts
paidInstallmentCount: number;
nextDueYear: number | null;
nextDueMonth: number | null;
```

### 2. Table — `pages/recent-purchases-page/.../recent-purchases-table.{ts,html}`
- Render "`{paidInstallmentCount}`/`{installmentCount}` paid".
- Render "next: `<Mon YYYY>`" from `nextDueYear`/`nextDueMonth` when non-null; otherwise show
  "Fully paid".
- Format the month label with the same locale approach already used in the client (es-AR); do
  not introduce a new date library.

## Tests

### API (`tests/PersonalFinance.Financing.Tests`)
- Mixed installments (some paid, some unpaid, one reversed) → correct `paidInstallmentCount` and
  `nextDue` = the earliest **unpaid, non-reversed** cuota's `DueCycle`.
- A reversed earliest cuota is skipped when picking `nextDue`.
- Fully-paid plan → `nextDue` is null.

### Client (`CHROME_BIN=/usr/bin/brave`)
- Row renders the "N/M paid" text.
- Row renders "next: `<month>`" when a next-due exists, and "Fully paid" when null.

## Done when

- API: `dotnet build` + `dotnet test --project tests/PersonalFinance.Financing.Tests` green.
- Client: `pnpm ng lint` + `pnpm ng test --watch=false --browsers=ChromeHeadless` green.
- **Manual E2E:** on Recent Purchases, a purchase with some cuotas paid shows an accurate paid
  count and the correct next-payment month; paying its last cuota flips it to "Fully paid".

## After this slice

The feature is complete: pay cuotas individually, statements discount them, and each purchase
shows its progress and next payment. Consider a documentation-sync pass across the project docs
(`app/api/.claude/CLAUDE.md`, `app/client/.claude/CLAUDE.md`, PRD/DESIGN) mirroring how prior
initiatives (`docs/cycle-due-month/`, `docs/dashboard-fixes/`) recorded their phases — but that
is a follow-up, not part of this slice.
