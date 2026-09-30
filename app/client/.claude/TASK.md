# TASK.md — Client Implementation Guide

Phase ledger for the Angular client, sequenced by build dependency, not document order. This
file tracks **status and outcome only**. A phase that traces to a `docs/<feature-slice>/*.md`
planning doc (repo root) keeps its full task-by-task detail there — read that doc for the "how",
this file only for the "what shipped and when". Phases 0–4 trace directly to `docs/PRD.md` /
`docs/DESIGN.md` instead (no slice doc predates them).

## Conventions every step inherits

From `.claude/CLAUDE.md` and `.claude/rules/typescript-frontend-style.md`:

- Standalone components (never `standalone: true`), `OnPush`, `inject()` in field initializers,
  signals-first state, no `effect()` for data flow, no async pipe, native control flow.
- One `type` per file under a feature `types/` folder; no `I-` prefix; `Money` is a branded
  `number`, never floated.
- Services: `@Injectable({ providedIn: 'root' })`, `inject(HttpClient)`, `Observable<T>`,
  explicit HTTP generics, unwrap `{ rows: [...] }` via `map`.
- Reactive forms only, built in `initXForm(): void`; money entered in major units, converted with
  `toMinorUnits` at submit.
- Subscription cleanup: `private destroy$ = new Subject<void>()` + `takeUntil` + `ngOnDestroy`.
- Every service gets an `HttpTestingController` spec (URL, verb, body, envelope unwrap,
  `AppError` mapping).
- User-facing error messages key off the error `code`, never `detail`.

## Deviations (D1–D24)

Intentional departures from `docs/PRD.md` / `docs/DESIGN.md`, referenced by ID from other docs.
No new D-numbers were introduced after Phase 12 (D24) — every later phase is additive scope.

- **D1** — Dev API URL is `https://localhost:7095/v1` (HTTPS), not the DESIGN-documented
  `http://localhost:5000/v1`. Requires `dotnet dev-certs https --trust`.
- **D2** — `app.routes.ts` stayed `Routes = []` through Phase 0; each feature wires its own
  `loadChildren` entry in Phase 1.
- **D3** — closed by D9.
- **D4** — `problemDetailsInterceptor` trusts `HttpErrorResponse.status` as authoritative instead
  of re-deriving it from a client-side code→status table.
- **D5** — `SKIP_ERROR_MAPPING: HttpContextToken<boolean>` lets `HealthService` read a raw 503
  body while the interceptor stays globally registered.
- **D6** — `pollUntil` completes via `first(done)` + inner `retry(1)`; errors `EmptyError` when
  attempts run out.
- **D7** — interceptor/helper files use the dotted `*.interceptor.ts` form, overriding the style
  guide's hyphen convention.
- **D8** — `poll-until.spec.ts` uses RxJS `TestScheduler` marbles, not `fakeAsync`/`tick` (no
  `zone.js/testing` loaded).
- **D9** — `InstrumentType` lives in `core/types/`, not a feature — `core` must not import from a
  feature. Closes D3.
- **D10** — Statement page's future-schedule section deferred; `getFutureSchedule` implemented +
  spec-covered but unconsumed until a card-centric view needs it.
- **D11** — `LedgerService.reverse` POSTs an empty `{}` body; `postTransaction` implemented +
  tested with no dedicated view.
- **D12** — API's `RecurrenceFrequency` only defines `Monthly`; client types `Frequency =
  'monthly'` and normalises the `"Monthly"` response.
- **D13** — Subscriptions' funding-account picker offers every instrument type, not debit-only.
- **D14** — `GET /v1/parties/{id}/timeline` returns a `{ rows }` envelope (DESIGN said as-is);
  `getTimeline` unwraps it.
- **D15** — New parties were invisible until they had a ledger movement (`debt-summary`'s INNER
  JOIN). **Superseded by Phase 23** (`GET /v1/parties` roster endpoint).
- **D16** — `PartyDetailPage` renders `ReportsService.partyTimeline` (carries `currencyCode`), not
  `PartiesService.getTimeline` (implemented for parity, unconsumed — D11 precedent).
- **D17** — `expenseAccountId` on Shared Expense is hand-entered free text (no list endpoint).
- **D18** — No `pollUntil` in Phase 3 — shared-expense/settlement postings are synchronous, so the
  page plainly re-fetches after each mutation.
- **D19** — Tailwind v4 is CSS-first (`@import 'tailwindcss'` + `.postcssrc.json`); no
  `tailwind.config.js` to add.
- **D20** — Reverse buttons on `InstallmentsTable`/`TimelineTable` were blocked — neither row
  carried a ledger transaction id. **Closed by D24.**
- **D21** — `InstrumentRegistryService` (`localStorage` stopgap) retired once `GET /v1/instruments`
  shipped (Phase 12); `InstrumentsService.list()` replaces it, each picker loads its own
  `WritableSignal<Instrument[]>`.
- **D22** — `StatementIndex` (id-paste stopgap) replaced by `StatementsPage` (card picker → list)
  once `GET /v1/financing/cards/{id}/statements` shipped (Phase 12).
- **D23** — `ReverseIndex` (id-paste stopgap) replaced by `TransactionsPage` (filtered feed) once
  `GET /v1/ledger/transactions` shipped (Phase 12).
- **D24** — Per-row Reverse added to `InstallmentsTable` + `TimelineTable` once a reversible
  transaction id was threaded onto both DTOs (Phase 12 steps 7–8). Closes D20.

---

## Phase 0 — Core scaffolding & HTTP wiring

Done 2026-09-02. `docs/DESIGN.md` §2–§10. Built `core/{types,money,http,registry,health}`, env
config, interceptor chain, routing shell (`Routes = []`, D2). Build 234.89 kB; tests 37/37.
Deviations D1–D8.

## Phase 1 — Fase 1: standalone-valuable core

Done 2026-09-02. Shipped the five Fase-1 views: Dashboard (`reports`, default route), Instruments
setup, Load-expense (split + reconciliation poll), Statement detail & pay, Reverse-movement.
Build 255.50 kB; tests 99/99. Deviations D9–D11.

## Phase 2 — Fase 2: Subscriptions

Done 2026-09-03. Shipped `SubscriptionsPage` (list/create/cancel), no optimistic UI. Build
255.59 kB; tests 112/112. Deviations D12–D13.

## Phase 3 — Fase 3: Parties

Done. Shipped `PartiesPage`, `PartyDetailPage`, `SharedExpensePage` + `TimelineTable`; full
`PartiesService`. Build 263.61 kB; tests 139/139. Deviations D14–D18.

## Phase 4 — Deviations & deferred items

Tooling + API-gap catalog, worked only when a trigger lands.

- **4.1 Tailwind** — build wiring done (D19); tokens/theme are `docs/SYSTEM.md`.
- **4.2/4.3/4.4 API gaps** (`GET /v1/instruments`, `GET /v1/financing/cards/{id}/statements`,
  `GET /v1/ledger/transactions`) — closed Phase 12. D20–D24.
- **4.5 auth surface** — **still open**, no backing endpoint (`docs/PRD.md` §7.4 /
  `docs/DESIGN.md` §11.4).
- **4.6 Client CI** — done (`client-build-test` job).
- **4.7 ESLint** — done (`angular-eslint@20` flat config).

## Phase 5 — Creditors (Slice 1: CRUD)

Done 2026-09-04. `docs/creditor-expense-fields/slice-1-creditors-crud.md`. New `creditors`
feature (CRUD, styled to SYSTEM.md from the start). Tests 164/164.

## Phase 6 — Creditors (Slice 2: Load-Expense integration)

Done 2026-09-04. `docs/creditor-expense-fields/slice-2-load-expense-integration.md`. Load-expense
gained an optional creditor + destination-account toggle (later superseded by Phase 10's
payment-mode selector). Tests 168/168.

## Phase 7 — Expense description field (Slice 1)

Done. `docs/expense-description/slice-1-description-field.md`. Required `description` on
Load-Expense, echoed on the confirmation panel.

## Phase 8 — Card-debt drill-down (Slice 2)

Done 2026-09-04. `docs/expense-description/slice-2-card-debt-drilldown.md`. Dashboard's "Card
Debt by Cycle" rows expand into per-purchase detail via `cardPurchases(cardId)`. Tests 176/176.

## Phase 9 — Recent purchases view (Slice 3)

Done 2026-09-05. `docs/expense-description/slice-3-recent-purchases-view.md`. Standalone
newest-first purchase list across every card, routed at `financing/recent-purchases`. Tests
183/183.

## Phase 10 — Creditor-financed expenses (Slice 1)

Done 2026-09-05. `docs/expense-payment-modes/slice-1-creditor-financed.md`. Load-Expense's
"Different creditor" checkbox became a two-way `mode: 'card' | 'creditor'` selector; `cardId`
optional. Tests 187/187.

## Phase 20 — Owed to creditors list (Slice 2)

Done. `docs/expense-payment-modes/slice-2-owed-to-creditors-list.md`. Standalone
`creditor-payables-page`, next-due-first, `financing/creditor-payables`. Tests 194/194.

## Phase 21 — Debit/cash expenses with categories (Slice 3)

Done. `docs/expense-payment-modes/slice-3-debit-cash-categories.md`. Third *My debit-cash* mode
on Load-Expense — submits to `POST /v1/ledger/expenses` with a free-type category `<datalist>`,
not the payment-plan path. Closes `docs/expense-payment-modes/`. Tests 206/206.

## Phase 22 — Dashboard fixes: installments-paid wording + card name on future rows (Slices 1–2)

Done. `docs/dashboard-fixes/slice-1-installments-paid-of-total.md` +
`slice-2-card-name-and-expand.md`. Drill-down line reads "paid" not "outstanding"; `cycleByCard()`
regroups by stable `cardId` so a card never shows a GUID or double-expands. Tests 208/208.
Committed by the user as `3b7a098` + `979eb54`.

## Phase 23 — Dashboard fixes: Parties list endpoint wiring (Slice 3)

Done. `docs/dashboard-fixes/slice-3-parties-list-endpoint.md`. New `PartiesService.list()` (`GET
/v1/parties`) merged with `debtSummary()` so a movement-less party is visible immediately
(supersedes D15). Closes `docs/dashboard-fixes/`. Tests 210/210.

## Phase 24 — Parties card-split: reconcile-loop fix + party future shares (Slices 1 + 2b)

Done. `docs/parties-card-split/slice-1-reconcile-loop-fix.md` + `slice-2b-party-future-shares.md`.
A card split short-circuits to `'scheduled'` instead of polling a balance that never moves;
party-detail gained a "Scheduled" block (`futureShares()`) for upcoming installment shares. Tests
~212. Committed by the user as `959a3a2`, `e281437`, `09357cb`.

## Phase 25 — Billing cycle "due month" reframe + creditor-split parity (Slices 1 + 2)

Done. `docs/cycle-due-month/slice-1-card-due-month.md` + `slice-2-creditor-split-parity.md`.
Payment-facing surfaces render the API's due cycle (close + 1) verbatim; creditor splits joined
the card split's no-poll `'scheduled'` short-circuit. Tests 215/215.

## Phase 26 — Schedule-aware Parties summary (Slice 3)

Done. `docs/cycle-due-month/slice-3-schedule-aware-summary.md`. Parties list distinguishes
truly-settled from "$0 now, N scheduled" via a new `pendingShares()` merge. Closes
`docs/cycle-due-month/`. Tests 218/218.

## Phase 27 — Individual installment payments: statement-installment type wiring (Slice 1)

Done. `docs/individual-installment-payments/slice-1-foundation-payable-from-installments.md`.
`MonthlyStatementInstallment` gained `isPaid`/`paidOnUtc` (type-only, no UI change). Tests
218/218.

## Phase 28 — Individual installment payments: pay a single installment (Slice 2)

Done. `docs/individual-installment-payments/slice-2-pay-single-installment.md`. Per-row **Pay**
button on `installments-table` (`POST .../installments/{id}/pay`); full-statement action
relabelled "Pay full statement". Tests 226/226.

## Phase 29 — Individual installment payments: next-payment visibility (Slice 3)

Done. `docs/individual-installment-payments/slice-3-next-payment-visibility.md`. Recent Purchases
rows show "N/M paid · next: `<month>`" / "Fully paid". Closes
`docs/individual-installment-payments/`. Tests 228/228.

## Phase 30 — Back-dated card expenses (Slice 1)

Done. `docs/backdated-expenses/slice-1-card-backdating.md`. A past-dated **card** purchase reveals
a required "Paid from" account; a future date is rejected pre-submit (`notFuture` validator).
Tests 234/234.

## Phase 31 — Back-dated creditor cutoff (Slice 2)

Done. `docs/backdated-expenses/slice-2-creditor-cutoff.md`. No client production change — the
Slice-1 selector was already card-only; +1 spec pinning that down. Tests 235/235.

## Phase 32 — Back-dated expenses: pending $ on Recent Purchases (Slice 3, optional)

Done. `docs/backdated-expenses/slice-3-pending-amount.md`. Recent Purchases rows show
`pendingAmountMinorUnits` next to the paid count. Closes `docs/backdated-expenses/`. Tests
236/236.

## Phase 33 — Owed to creditors: current-cycle outstanding (Slice 1)

Done. `docs/owed-to-creditors/slice-1-current-cycle-outstanding.md`. `creditor-payables-table`
shows two figures — focal "Due now" + muted "Total owed" sub-line (was one historic total). Tests
237/237.

## Phase 34 — Owed to creditors: creditor detail view (Slice 2)

Done. `docs/owed-to-creditors/slice-2-creditor-detail-view.md`. New read-only
`financing/creditor-payables/:creditorId` page, debt grouped by purchase with per-cuota status
badges. Tests 248/248.

## Phase 35 — Owed to creditors: pay a creditor cuota + undo (Slice 3)

Done. `docs/owed-to-creditors/slice-3-pay-installment-and-undo.md`. Per-row **Pay**/**Undo** on
the creditor detail page (`POST/unpay .../creditor-installments/{id}`), display-only stamp, no
bank/ledger. Tests 258/258.

## Phase 36 — Owed to creditors: pay a creditor's full debt (Slice 4)

Done. `docs/owed-to-creditors/slice-4-pay-full-debt.md`. **"Pay full debt"** button with an
inline confirm, settles every remaining cuota. Closes `docs/owed-to-creditors/`. Tests 262/262.

## Phase 37 — Subscriptions rework: explicit-pay foundation (Slice 1)

Done. `docs/subscriptions-rework/slice-1-explicit-pay-foundation.md`. Status badge
(`paid`/`overdue`/`upcoming`) on the Subscriptions page; stale auto-charge copy corrected. Tests
263/263.

## Phase 38 — Subscriptions rework: pay a live period (Slice 2)

Done. `docs/subscriptions-rework/slice-2-pay-live-period.md`. Per-row **Pay** button
(`POST subscriptions/{id}/pay`), no optimistic update. Tests 268/268.

## Phase 39 — Subscriptions rework: undo a payment (Slice 3)

Done. `docs/subscriptions-rework/slice-3-undo-payment.md`. Per-row **Undo** mirroring Pay
(`POST .../unpay`). Tests 273/273.

## Phase 40 — Incomes support: record income + Dashboard toggle (Slice 1)

Done. `docs/incomes-support/slice-1-record-income-and-dashboard-toggle.md`. New
`record-income-page` (`POST ledger/incomes`) + Dashboard Out-of-pocket/Income toggle. Tests
322/322.

## Phase 41 — Incomes support: the Money Flow table (Slice 2)

Done. `docs/incomes-support/slice-2-money-flow-table.md`. New accounting-style `money-flow-page`
— every movement of the user's own money, income green / outcome red. Tests 333/333.

## Phase 42 — Incomes support: undo an income (Slice 3)

Done. `docs/incomes-support/slice-3-undo-income.md`. Per-row Undo on Money Flow income rows,
reuses `LedgerService.reverse`. Closes `docs/incomes-support/`. Tests 341/341.

## Phase 43 — Partial creditor payments: foundation (Slice 1)

Done. `docs/partial-creditor-payments/slice-1-foundation-partial-installment.md`. New
`creditor-pay-dialog` (the client's first native `<dialog>`) — pay a cuota in full or a custom
amount; Pay/Undo gate independently so a partial row shows both. Tests 353/353.

## Phase 44 — Partial creditor payments: pay-from-expense (Slice 2)

Done. `docs/partial-creditor-payments/slice-2-pay-expense.md`. "Pay expense" button per purchase
group, opens the shared dialog in `expense` mode (`POST creditor-purchases/{planId}/pay`), fills
cuotas in sequence via a waterfall. Tests 363/363.

## Phase 45 — Partial creditor payments: partial full-debt (Slice 3)

Done. `docs/partial-creditor-payments/slice-3-partial-full-debt.md`. The inline "Settle every
remaining cuota…" confirm is gone — "Pay full debt" now opens the shared `creditor-pay-dialog` in
a new `full-debt` mode. A currency `<select>` appears only when the creditor's outstanding
purchases span more than one currency; a custom amount then fills that currency's remaining
cuotas across every purchase, oldest due-month first. New `pay-creditor-full-debt.ts` request
type, `creditor-outstanding-by-currency.ts`. Tests 370/370.

## Phase 46 — Partial creditor payments: party share (Slice 4)

Done. `docs/partial-creditor-payments/slice-4-party-part.md`. `creditor-pay-dialog` (installment
mode) gains a third radio per unpaid party share — disabled with an inline explanation when the
share exceeds what remains — revealing a required "Received into" bank-account `<select>`;
`confirm` output is now a `{kind:'own',...} | {kind:'party',...}` union. `creditor-detail-page`
loads bank accounts the same way `party-detail-page` does (reuses `InstrumentsService`, no new
endpoint) and maps the new `Parties.SettlementExceedsBalance` / `Parties.UnknownFundingAccount` /
`Financing.PartyShareAlreadyPaid` / `Financing.PartyShareNotDue` errors into the existing
`payErrorMessages`. Closes `docs/partial-creditor-payments/`. Tests 377/377.

## Phase 47 — Friendly UI: grouped navbar + renames (Slice 1)

Done. `docs/friendly-ui/slice-1-navbar.md`. Client-only. `App.navGroups` replaces the flat
`navItems`: Views / Setup / Actions blocks spread across the bar; the two action links render as
filled buttons (`primary-nav__link--action`). Seven page titles renamed (routes unchanged, D3);
"Recent purchases" left the nav. Tests 382/382.

## Phase 48 — Friendly UI: ARS | USD currency toggle (Slice 2)

Done. `docs/friendly-ui/slice-2-currency-toggle.md`. Client-only. Load an Expense's currency
`<select>` is now a filled segmented radio pair beside the amount (`currencyOptions` field), and the
amount sign follows the selection (`$` / `US$`). Record Income's select untouched. Tests 385/385.

## Phase 49 — Friendly UI: remove the shared-expense page, prefill the party (Slice 3)

Done. `docs/friendly-ui/slice-3-remove-shared-expense.md`. `/parties/shared-expense` page, route, `registerSharedExpense`, and its three types deleted. Party detail links to `/financing/load-expense?party=<id>` ("Split an expense with <name>"); Load an Expense reads `?party=` once the parties list loads and pushes one weight-1 split row if the id is known (unknown → none). Tests 380/380.

## Phase 50 — Friendly UI: subscription names (Slice 4)

Done, no client change: labels come from the ledger account name, renamed API-side (API Phase 53). No spec fixture hard-coded a subscription `'… Expense'`. `docs/friendly-ui/slice-4-subscription-names.md`.

## Phase 51 — Friendly UI: dashboard rework + Due this month (Slice 5)

Done. `docs/friendly-ui/slice-5-dashboard.md`. Dashboard order: header (no "Personal ledger" eyebrow), quick actions (filled "+ Record an expense", "+ Record income" chip, "Recent Credit Card Purchases ›" link), Due this month card (per-currency total, Cards/Creditors sub-line, expandable breakdown, "Pay a card bill"/"Pay a creditor" links), flow (tabs "Money Spent" / "Money received", "Where your money went"), "Card bills by month" (Charged / Upcoming), Active subscriptions. USD purchase formatting bug fixed on expanded card purchases; creditor payables table formats per-row `currencyCode`; `FinancingService.dueThisMonth()`. A 2x2 viewport grid was tried and reverted (single column stays). Specs 396 green, lint + prod build clean.

## Phase 51.1 — Friendly UI: month-driven dashboard (follow-up to Slice 5)

Done. `docs/friendly-ui/slice-5-dashboard.md`. Month picker moved to the right end of the quick-actions row and drives every card (h1 "This month" or "<Month> <Year>"): Money Spent/Received + categories, Due card (`dueThisMonth(month)`), Card bills (filtered client-side to the month's cycle), Subscriptions (`subscriptions/by-month`; overdue first, "Future payments — not charged yet"). Late responses for a deselected month are dropped. Layout: `max-w-6xl`, header/quick actions/Due card full width, then a 2-column grid at lg+ (money flow left; Card bills over Subscriptions right). Specs 418 green, lint + prod build clean.

---

## Verification (every phase)

- **Build:** `pnpm ng build` — 0 errors, within the 500 kB warning / 1 MB error initial-JS budget.
- **Unit:** `pnpm ng test --watch=false --browsers=ChromeHeadless` — green; each new service has
  an `HttpTestingController` spec.
- **Manual:** run the API locally, `pnpm ng serve`, walk each new view through loading / empty /
  error; force a 409/422 and confirm an `AppError` renders keyed off `code`.
- **Type parity:** every `types/*.ts` field-matches the DTO tables in `docs/DESIGN.md` §3.
- **Traceability:** every phase cites a `docs/PRD.md` §/US, a `docs/DESIGN.md` §, or a
  `docs/<feature-slice>/` planning doc.
