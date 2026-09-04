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
- **States:** loading, empty (no movements yet), error.

### 3.2 Instruments setup — prerequisite
- **Shows:** a form to register a payment instrument (`debit` | `credit` | `cash`); for `credit`, a
  required **cutoff day** (1–31). A list of previously registered instruments.
- **Source (write):** `POST /v1/instruments`.
- **Gap:** the API has **no list endpoint** for instruments/cards. The client maintains a local
  registry (persisted, see `DESIGN.md` §8) of instruments it created, because forms elsewhere need
  to offer cards/accounts as choices. Flagged in §7.

### 3.3 Load expense — US-3
- **Shows:** a form to load a credit-card expense: amount, card, installment count, purchase date
  (`YYYY-MM-DD`), and an optional split across parties by integer weight. The client never computes
  the billing cycle or the split cents — it submits raw inputs and the API allocates.
- **Source (write):** `POST /v1/financing/payment-plans`.
- **After submit:** if a split was included, registration of the receivable is **eventually
  consistent** (API D8) — the plan id returns before the third-party receivable is posted. The view
  confirms the plan immediately and reconciles the party balance shortly after (see `DESIGN.md` §7).
- **Depends on:** card choices and party choices (see gaps §7).

### 3.4 Statement detail & pay — US-4
- **Shows:** a statement's cycle, total due, paid/unpaid status, and the **itemized installments**
  that compose the total (sequence "1 of 3", purchase date, amount, reversed flag, and a per-row **Reverse**
  action for the installment's accrual). A "Pay" action
  requiring the funding bank account and pay date. Optionally, the card's **future schedule** (not
  yet accrued installments) for the full-debt picture.
- **Source:** `GET /v1/financing/statements/{id}`, `POST /v1/financing/statements/{id}/pay`,
  `GET /v1/financing/cards/{id}/future-schedule`.
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
- **List shows:** every party with net balance (positive = they owe you). **Source:**
  `GET /v1/reports/parties/debt-summary` (also the client's only way to enumerate parties).
- **Detail shows:** a party's current balance and the movement timeline (chronological, with running
  balance) that explains how the number was reached. Actions to register a shared expense and to
  register a settlement when someone pays.
- **Source:** `GET /v1/parties/{id}/balance`, `GET /v1/parties/{id}/timeline` (or the reporting
  equivalent `GET /v1/reports/parties/{id}/timeline`), `POST /v1/parties/shared-expenses`,
  `POST /v1/parties/{id}/settlements`.
- **Notes:** cross-debts net automatically server-side; the client shows the resulting net only.

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
