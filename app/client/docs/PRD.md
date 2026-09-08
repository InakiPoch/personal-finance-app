# Client PRD — Personal Finance (Angular 20)

> Product requirements for the **web client** that consumes the PersonalFinance `.NET 10` API.
> This document defines **what the client must present to the user** and how each view maps to a
> backend capability. It is the client-side counterpart of `app/api/docs/PRD.md`.
>
> **Out of scope here:** visual/UI design (colors, spacing, components, layout). That is a separate
> task. This document commits only to *which views exist, what data they show, and which endpoints
> feed them*. The technical architecture lives in `./DESIGN.md`.

## 1. Purpose

The API is fully functional but, per the API PRD, was built "API-first" for manual testing via
Scalar/Postman. This client turns that API into a usable single-user application: register payment
instruments, load expenses (with installments and shared splits), forecast and pay card statements,
manage subscriptions, correct mistakes via reversal, and settle balances with third parties.

Every requirement below is a direct client rendering of an API user story (US-1…US-7). The client
adds **no business logic**: all accounting invariants, splits, cycles, and rounding are the API's
responsibility. The client validates input shape, formats money/dates, calls endpoints, and renders
responses and errors consistently.

## 2. User profile & access

- **Single user** (the app owner). No multi-tenant, no roles, no third-party accounts — "parties"
  are reference data, not users.
- **Authentication:** the API currently exposes **no auth endpoints** and no auth beyond
  app-level access (API decision D13). The client therefore ships **without a login flow** in this
  scope. If app-level protection is added later, it is a new cross-cutting feature, not part of any
  view below. Recorded as an open item (§7).

## 3. Views the client must present

Each view is a **container (page) component**. Presentational breakdown and styling belong to the UI
task. "Source" lists the exact endpoints (see `DESIGN.md` §9 for the full traceability table).

### 3.1 Dashboard (home) — US-1, US-2
- **Shows:** current-month spending in debit + cash grouped by category; per-card amount due this
  cycle split into **Accrued** (will hit the real bill now) vs **Future** (committed but not yet
  accrued installments).
- **Source:** `GET /v1/reports/monthly-expenses?month=YYYY-MM`, `GET /v1/reports/card-due-by-month`.
- **Notes:** monthly expenses deliberately **exclude** card purchases and third-party receivables
  (API D9) — this view answers "what actually left my pocket", not "what I owe". Month selector
  defaults to the current month.
- **Drill-down (`docs/expense-description/slice-2-card-debt-drilldown.md`, now built):** each
  "Card Debt by Cycle" row for a card expands into the individual outstanding purchases behind its
  total, each with its description (§3.3), current-cycle purchases sorted first. **Source:**
  `GET /v1/financing/cards/{id}/purchases`.
- **States:** loading, empty (no movements yet), error.

### 3.2 Instruments setup — prerequisite
- **Shows:** a form to register a payment instrument (`debit` | `credit` | `cash`); for `credit`, a
  required **cutoff day** (1–31). A list of previously registered instruments.
- **Source (write):** `POST /v1/instruments`.
- **Gap:** the API has **no list endpoint** for instruments/cards. The client maintains a local
  registry (persisted, see `DESIGN.md` §8) of instruments it created, because forms elsewhere need
  to offer cards/accounts as choices. Flagged in §7.

### 3.3 Load expense — US-3
- **Shows:** a form to load an expense. A **payment-mode** selector chooses how it was
  financed — *My credit card* (pick a card), *My debit-cash* (pick a debit/cash account + a
  required category, no installments), or *Financed by a creditor* (pick a creditor + one of
  its accounts, no card) — the three slices of `docs/expense-payment-modes/` (Slice 1 creditor,
  Slice 3 debit-cash), all built. Then: amount,
  installment count (hidden in debit-cash — a single payment), purchase date (`YYYY-MM-DD`), a
  required free-text **description** (1–120
  chars, single line — `docs/expense-description/slice-1-description-field.md`, now built), and an
  optional split across parties by integer weight — the split is available in **all** modes. The
  client never computes the billing cycle or the split cents — it submits raw inputs and the API
  allocates. On confirmation, the description headlines the panel — "payment plan created" for the
  card/creditor modes, "expense recorded" for debit-cash.
- **Back-dated card purchase (`docs/backdated-expenses/slice-1-card-backdating.md`, Slice 1 built):**
  a future purchase date is rejected client-side (and by the API, `Financing.FuturePurchaseDate`).
  When *My credit card* is chosen and the purchase date is before today, a required **"Paid from"**
  account selector appears (same debit/cash instrument list as debit-cash mode) — the API settles
  that purchase's already-elapsed installments from it, with historically-dated ledger postings, so
  the purchase reads as one you have been paying for months rather than "0/N paid". A today-dated
  card purchase shows no selector. **Creditor mode (Slice 2, built):** a back-dated
  creditor-financed purchase's elapsed cuotas are stamped paid server-side (display-only, no
  ledger, no bank picked) and Recent Purchases reads "N/M paid · next: <month>" — no client
  change, the "Paid from" selector stays card-only.
- **Category (debit-cash only):** a free-type field backed by the existing list from
  `GET /v1/expense-categories` — pick an existing category or type a new one; it is required, and
  a new name get-or-creates its `Expense` Ledger account server-side (trim + case-insensitive
  match, so "Groceries"/"groceries" never fragment the monthly breakdown). Credit and creditor
  modes have **no** category (D7).
- **Source (write):** `POST /v1/financing/payment-plans` for the card and creditor modes (`cardId`
  omitted in creditor mode — the API records a card-less plan, no billing cycle, no monthly
  statement); `POST /v1/ledger/expenses` for debit-cash — one balanced Ledger transaction
  (`Dr` category / `Cr` source), or receivables + category debit when split.
- **After submit:** if a split was included, registration of the receivable is **eventually
  consistent** (API D8) — the plan id returns before the third-party receivable is posted. The view
  confirms the plan immediately and reconciles the party balance shortly after (see `DESIGN.md` §7).
  For a **card** or **creditor-financed** split the co-borrower's receivable accrues at its due month,
  not synchronously — the view marks each participant *scheduled* rather than polling for a balance
  change that will not come in this session (`docs/parties-card-split/slice-1-reconcile-loop-fix.md` +
  `docs/cycle-due-month/slice-2-creditor-split-parity.md`, both built). Only debit/cash splits post up
  front and still reconcile live.
- **Depends on:** card choices and party choices (see gaps §7).

### 3.4 Statement detail & pay — US-4
- **Shows:** a statement's cycle, total due, paid/unpaid status, and the **itemized installments**
  that compose the total (sequence "1 of 3", purchase date, amount, a paid / reversed status chip, and
  per-row **Pay** + **Reverse** actions — Pay is disabled for a paid or reversed row). A shared bank
  account + pay-date form feeds both a per-row single-installment payment and the **"Pay full statement"**
  action (which charges only the still-unpaid, non-reversed cuotas). Optionally, the card's **future
  schedule** (not yet accrued installments) for the full-debt picture.
- **Source:** `GET /v1/financing/statements/{id}`, `POST /v1/financing/statements/{id}/pay`,
  `POST /v1/financing/installments/{id}/pay`, `GET /v1/financing/cards/{id}/future-schedule`.
- **Reversal-credit UX (API D12):** when a paid installment was reversed, a compensating card credit
  is netted against the **next** statement, not refunded as cash. The view must state this plainly.
- ~~**Gap:** no endpoint lists a card's statements.~~ **Resolved (Phase 12):** `GET
  /v1/financing/cards/{id}/statements` ships; the `Statements` view lists them per card.

### 3.5 Reverse movement — US-6
- **Shows:** confirmation and result of reversing a posted transaction (storno; and, for an
  already-paid installment, a compensating card credit). Reversal never deletes — it appends.
- **Source (write):** `POST /v1/ledger/transactions/{id}/reversal`.
- **Audit visibility:** the client surfaces reversal state where the API exposes it — the
  `isReversed` flag on statement installments (§3.4) and the party timeline (§3.7), and a per-row
  **Reverse** action on both, driven by the ledger transaction id the API threads onto those rows
  (`reversalTransactionId` / `transactionId`). The action is disabled where no live accrual backs
  the row (already reversed, or a row that is itself a reversal).
- ~~**Gap:** no general transaction/history feed exists.~~ **Resolved (Phase 12):** `GET
  /v1/ledger/transactions` ships a filterable feed; the reverse action is also reachable per-row
  from the statement-installment and party-timeline tables.

### 3.6 Subscriptions — US-5
- **Shows:** list of active subscriptions (name, amount, category, frequency, anchor day, next due
  date). Forms to create a subscription (charges the first period immediately) and to cancel one
  (stops future renewals; past charges remain).
- **Source:** `GET /v1/subscriptions/active`, `POST /v1/subscriptions`,
  `DELETE /v1/subscriptions/{id}`.
- **Notes:** renewals are scheduler-driven server-side (API D6); the client cannot trigger them and
  simply reflects the latest state on load.

### 3.7 Parties — list & detail — US-7
- **List shows:** every registered party with net balance (positive = they owe you); a party with no
  movements yet shows as settled / $0 — unless it has not-yet-accrued split installments scheduled ahead,
  in which case it reads "Nothing owed yet · N scheduled" instead of "Settled up". **Source:**
  `GET /v1/parties` for the roster, merged by id with `GET /v1/reports/parties/debt-summary` for the
  balances (the debt summary alone omits parties with zero ledger movements) and
  `GET /v1/parties/pending-shares` for the pending-schedule count.
- **Detail shows:** a party's current balance and the movement timeline (chronological, with running
  balance) that explains how the number was reached. Actions to register a shared expense and to
  register a settlement when someone pays.
- **Source:** `GET /v1/parties/{id}/balance`, `GET /v1/parties/{id}/timeline` (or the reporting
  equivalent `GET /v1/reports/parties/{id}/timeline`), `POST /v1/parties/shared-expenses`,
  `POST /v1/parties/{id}/settlements`.
- **Notes:** cross-debts net automatically server-side; the client shows the resulting net only.
- **Scheduled shares (`docs/parties-card-split/slice-2b-party-future-shares.md` +
  `docs/cycle-due-month/`, all built):** the detail view also shows a **"Scheduled"** block — what the
  party will owe per upcoming month on its card-backed **and creditor-financed** split plans, before
  each installment accrues (the timeline shows only posted movements, so a freshly split party would
  otherwise read settled / $0). **Source:** `GET /v1/parties/{id}/future-shares` — one row per
  not-yet-accrued installment share, byte-exact with what the server will post (the API reuses its
  allocator; the client renders month, source label — card or creditor name — amount, and computes
  nothing). The month shown is the **payment month** (statement-close cycle + 1; API `docs/PRD.md` §9
  decisions 9–10); creditor rows follow the same rule (their first payment is the month after purchase).

### 3.8 Creditors setup (new — not part of the original 7-view scope)
- **Shows:** a form to register a creditor (name) with optional free-text destination accounts
  (label + CBU/CVU/alias identifier — the label is required per account row, the identifier is
  optional). A list of registered creditors with their accounts.
- **Source:** `POST /v1/creditors`, `GET /v1/creditors`.
- **Notes:** distinct from **Parties** (§3.7) — a Party tracks shared-expense *debt*; a Creditor is
  just *who gets paid*, with no debt or balance of its own. This is Slice 1 of
  `docs/creditor-expense-fields/slice-1-creditors-crud.md` — CRUD only. **Slice 2** (Load-Expense
  integration, `docs/creditor-expense-fields/slice-2-load-expense-integration.md`) is now built:
  §3.3's Load-Expense form extends with an optional "Different creditor" toggle that reveals a
  creditor picker and account-to-pay selector; both fields are required when toggled on, submitted
  as optional metadata in the payment-plan request, and excluded when toggled off.
  **Superseded by `docs/expense-payment-modes/` Slice 1:** that toggle is now the *Financed by a
  creditor* option of §3.3's payment-mode selector — picking it omits `cardId` entirely and makes
  the creditor + account required (a card-less plan), rather than attaching them as metadata to a
  card plan.

### 3.9 Recent purchases (new — not part of the original 7-view scope, now built)
- **Shows:** a standalone, newest-first chronological list of every loaded expense across every
  card — each row's description (§3.3), card name, purchase date, total, a creditor-payment
  marker, and a **payment-progress line**: "N/M paid · $X pending · next: `<month>`" (the pending
  segment drops out when nothing is owed), or "Fully paid" when no installment remains. It is a
  plain browseable history, not a debt view: purchases appear whether or not they're paid off,
  and there is no card picker (unlike §3.4's Statements view).
- **Source:** `GET /v1/financing/purchases/recent` — the row carries `paidInstallmentCount`, a
  derived next-payment month (`nextDueYear` / `nextDueMonth`, null once every installment is paid
  or reversed), and `pendingAmountMinorUnits` (Σ of the un-paid, un-reversed installment amounts —
  the same population the next-payment month is drawn from). Nothing is rescheduled; this only
  surfaces what already exists.
- **Notes:** the base view is Slice 3 of `docs/expense-description/slice-3-recent-purchases-view.md`
  (Slice 1 is §3.3's description field; Slice 2 is §3.1's card-debt drill-down). The
  payment-progress line is Slice 3 of `docs/individual-installment-payments/slice-3-next-payment-visibility.md`
  — the final slice of that initiative, which also added §3.4's per-installment Pay action; the
  pending-$ segment is Slice 3 of `docs/backdated-expenses/slice-3-pending-amount.md`, which closes
  that initiative.
  Reachable from the global nav and a second Dashboard quick-action link — the client's known
  discoverability weak spot does not apply here.

### 3.10 Owed to creditors (new — not part of the original 7-view scope, now built)
- **Shows:** a standalone list of every creditor with an outstanding balance across all
  creditor-financed purchases, ordered by creditor name — each row's creditor name (+ a muted
  sub-line listing their account labels), next due date (`—` when no schedule has started), and
  **two** money figures: **"Due now"** (the focal value — Σ unpaid, non-reversed cuotas whose
  payment month is at or before the current creditor cycle, with earlier-month arrears folded in)
  and a muted **"&lt;total&gt; total"** sub-line (Σ all unpaid, non-reversed cuotas, future included).
  Paid cuotas are excluded from both.
  An empty-state note when no creditor has an outstanding balance. **Each creditor name is a link
  into its detail view.**
- **Detail view (`financing/creditor-payables/:creditorId`, now built):** a read-only drill-down
  showing one creditor's debt **grouped by purchase** — each payment plan a section with its
  description, purchase date and "`<outstanding>` outstanding of `<total>`", and an installments
  sub-table beneath (cuota N/M, payment month, amount, and a status badge: overdue / due / future /
  paid / reversed). Sections are ordered newest purchase first. An unknown creditor id renders a
  friendly "no creditor matches that link" state. No pay/undo actions yet — the table leaves a seam
  for them (Slices 3–4).
- **Source:** `GET /v1/financing/creditor-payables` (list) + `GET /v1/financing/creditor-payables/{creditorId}` (detail).
- **Notes:** not part of the original 7-view scope; traces to `docs/expense-payment-modes/slice-2-owed-to-creditors-list.md` (the list) and `docs/owed-to-creditors/` (`slice-1-current-cycle-outstanding.md` — the two-figure split, API's Phase 34; `slice-2-creditor-detail-view.md` — the detail view, API's Phase 35); Financing-only, read-only, no Ledger (D7). Paid cuotas drop out of both list money figures (`Installment.PaidOnUtc`, stamped by the back-dated creditor path and — from Slice 3 — a "pay a cuota" action). **Still pending:** `NextDueDate` is the earliest *scheduled* month and does not advance as months pass; the per-account sub-line breakdown still sums over all non-reversed cuotas, so it can exceed "Total owed" when a creditor has paid cuotas — both deferred to a later slice. Pay/undo a cuota and pay the full debt (Slices 3–4) are not built yet. Reachable from the global nav right after "Recent purchases".

### 3.11 Debit/cash expenses with categories (new — extends §3.3, now built)
- **Shows:** the third *My debit-cash* mode of §3.3's Load-Expense form. When selected, the form
  offers a **debit/cash instrument** dropdown (the account the money left — `instruments()` filtered
  to `type === 'debit' || 'cash'`), a **required category** field (existing list from
  `GET /v1/expense-categories` + free-type new), and hides the installment count (a single
  payment). Amount, purchase date, description, and the optional party split all carry over. On
  confirmation the panel headline reads "expense recorded".
- **Source:** `GET /v1/expense-categories` (category options); `POST /v1/ledger/expenses` (write).
- **Notes:** traces to `docs/expense-payment-modes/slice-3-debit-cash-categories.md`, the final
  slice of that initiative. A debit/cash purchase is money already gone — the API posts **one
  balanced Ledger transaction** (`Dr` category `Expense` account / `Cr` the source `Bank`/`Cash`
  account), not an installment plan (D3/D4). A split debit expense still posts per-party
  receivables, exactly as the card path does (D9). The category **is** the `Expense` account name —
  get-or-created by name (trim + case-insensitive) so the monthly breakdown groups cleanly; credit
  and creditor modes keep no category (D7). Subscription expense accounts also carry `Kind=Expense`
  and so appear in the category list — an accepted tradeoff.

## 4. Cross-cutting client requirements

- **Consistent error surfacing.** Every failed call yields the same typed error (from the API's
  RFC-9457 envelope): a stable `code`, a human `detail`, and an HTTP `status`. Views render errors
  from that single shape — no ad-hoc parsing per call. (See `DESIGN.md` §5.)
- **Money precision.** All amounts travel as integer **minor units** (e.g. `10050` = `100.50 ARS`).
  The client converts and formats at the edges only; it never does floating-point arithmetic on
  money. Single currency `ARS`.
- **No silent deletes.** The client never offers "edit" or "delete" for a posted movement — only
  "reverse", which appends a correcting record. Corrected items stay visible.
- **Append-only truth.** History views reflect the ledger as-is, including reversals.

## 5. Delivery phasing

The API is built across all modules, so the client covers the **full surface**. Build order follows
the API PRD roadmap:
- **Fase 1** — Dashboard, Instruments setup, Load expense, Statement detail & pay, Reverse
  (US-1/2/3/4/6). This is the standalone-valuable core.
- **Fase 2** — Subscriptions (US-5).
- **Fase 3** — Parties (US-7).

## 6. Out of scope (client)

Inherits the API's exclusions (multi-user, bank integration, multi-currency, notifications, budgets,
tax export) **plus**, for this task specifically: visual/UI design, theming, and any endpoint the API
does not expose. Login/app-protection is deferred (no backing endpoints — §2, §7).

## 7. Known API gaps (client-affecting) — flag for API

These are surfaced so the client design is honest about what a view can actually show today.

1. ~~**No list endpoint for instruments/cards/accounts** (`POST` only).~~ **Resolved (Phase 12).**
   `GET /v1/instruments` ships (Ledger debit/cash accounts + Financing credit cards merged, one
   `{ id, type, name, cutoffDate }` row each). Every form now populates its card/funding `<select>`
   from the API; the `localStorage` registry workaround was removed (`DESIGN.md` §8).
2. ~~**No statement-list per card.** US-4's pay flow needs a `statementId`, but nothing enumerates
   statements and `card-due-by-month` exposes card + amount, not `statementId`.~~ **Resolved
   (Phase 12).** `GET /v1/financing/cards/{id}/statements` ships (one summary row per statement:
   `statementId`, cycle, `amountDueMinorUnits`, `isPaid`). The `Statements` view is now a real
   card-picker list; a row opens the statement detail with its id.
3. ~~**No general transaction/history feed.** US-6's reverse flow has no list to pick from; reversal
   audit is fragmented across statement `isReversed` flags and party timelines.~~ **Resolved
   (Phase 12).** `GET /v1/ledger/transactions` ships (newest-first feed, one
   `{ transactionId, postedOnUtc, description, amountMinorUnits, isReversal, isReversed }` row each,
   optional `accountId` / `from` / `to` filter). The `Reverse` view is now a real transaction picker
   (filter → row → confirm), replacing the id-paste stopgap.
4. **No auth surface.** App-level protection (§2) has no backing endpoint yet; login is deferred.
