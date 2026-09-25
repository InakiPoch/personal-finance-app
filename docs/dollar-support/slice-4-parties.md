# Slice 4 — Parties: shared expense + settle in USD (two-balance current account)

> Read `00-overview.md`, then Slices 1–3. This is the **last and heaviest** slice. It extends
> currency to the **Parties** module, where the conceptual weight lives: a party can now owe
> you **two** separate current-account balances — one in ARS, one in USD — and the settle form
> must pick which one it pays.

## Intent

- Split a **shared expense** among parties in ARS **or** USD, with the **same party
  arrangement** (weights/shares) regardless of currency.
- **Settle a current account** in a chosen currency.
- Show each party's balance **per currency, separated**; a party with debts in both currencies
  shows both.

## Why it's last

Parties is the only module where currency multiplies the **balance dimension**, not just the
display. Everywhere else an amount has one currency; here a *relationship* (what a party owes)
can span two currencies simultaneously. That two-balance model is the new complexity, so it
lands after the simpler paths are proven.

## API changes

### 1. ExpenseSplit money fields get `CurrencyCode`
- `Domain/ExpenseSplit.cs` — has **four** `Money` fields: `Total`, `HolderShare`,
  `AccruedReceivable`, `ReversedReceivable`. All share the split's **single** currency.
  In `ExpenseSplitConfiguration.cs` apply the two-column mapping to all four (they can share
  **one** `CurrencyCode` column for the split, since a split is one currency — prefer a single
  split-level `CurrencyCode` over four redundant ones).
- `Domain/ExpenseSplitParticipant.cs` — `Share` (`ExpenseSplitParticipantConfiguration.cs`)
  inherits the split currency.
- Migration in the **Parties** context: `CurrencyCode TEXT NOT NULL DEFAULT 'ARS'` on
  `parties_expense_splits` (and participants if stored separately).

### 2. Thread currency through the split + settle handlers
- Shared-expense registration (`RegisterSharedExpenseHandler` / `RecordSplitAccrualHandler` /
  `LinkPaymentPlanSplitHandler` / `CorrectExpenseSplitHandler`): the split's `CurrencyCode`
  drives every `Money` and every receivable ledger entry it posts. **Split math (weights →
  shares) is unchanged** — currency is a tag.
- `SettleCurrentAccountHandler.cs` (+ command/endpoint): add `CurrencyCode` so a settlement
  targets the party's balance **in that currency**. The receivable account is poly-currency;
  the settlement posts in the chosen currency only.

### 3. Party balance = per currency
- A party's current-account balance is a live Ledger receivable-account query. It must now
  return **one balance per currency** (reuse the poly-currency balance partitioning from
  Slice 1).
- Read-views + reporting:
  - `Ledger/.../ReadViews/vw_receivable_account_movements.sql` — real `CurrencyCode`, not the
    literal.
  - `Parties/.../ReadViews/vw_current_account_timeline.sql` — inherits `m.CurrencyCode`;
    running balance must reset **per currency** (a running total may not cross currencies).
  - `Reporting/Sql/debt_by_party.sql` (`DebtByPartyQuery.cs`) — add `CurrencyCode` to
    `SELECT`/`GROUP BY`; a party can appear once per currency.
- Loosely-typed client DTOs `future-party-share.ts` / `pending-shares-by-party-row.ts`
  (currently `string`) and the reporting `party-debt-row.ts` / `party-timeline-row.ts`
  (typed `CurrencyCode`) now carry real per-currency values.

## Client changes

### 1. Currency selectors
- `features/parties/pages/shared-expense-page/shared-expense-page.{ts,html}` — add the
  `currency` control; replace the hardcoded `Total (ARS)` label; send `currencyCode` on the
  shared-expense payload. Split rows/weights UI unchanged.
- `features/parties/pages/party-detail-page/party-detail-page.{ts,html}` — the **settle**
  form gains a currency choice (constrained to the currencies the party actually owes);
  replace the hardcoded `Amount (ARS)` label.

### 2. Two-balance display
- `parties-page/parties-page.{ts,html}` — per-party net and the totals must group by
  currency; a party can show an ARS line and a USD line. A party with no movements is
  "settled" ($0) in **no** currency (suppress empties).
- `party-detail-page.html` — current balance shows per currency; future-shares block per
  currency.
- `timeline-table.{ts,html}` — running balance column is **per currency** (do not carry a
  single running total across currencies; render separate series or a currency column).

## Test plan

### API (xUnit)
- Shared expense with `CurrencyCode = "USD"`: `Total`, every participant `Share`, and the
  accrued receivable entries are USD; **share allocation is numerically identical** to the ARS
  case (parity assertion, mirrors Slice 2's division parity).
- A party with an ARS split **and** a USD split: balance query returns **two** balances;
  settling the USD one leaves the ARS one untouched.
- `debt_by_party` / timeline: per-currency rows; running balance never crosses currencies.

### Client (Karma)
- `shared-expense-page.spec.ts` — sends `currencyCode`; label no longer hardcodes ARS.
- `party-detail-page.spec.ts` — settle form offers only owed currencies; sends the choice.
- `parties-page.spec.ts` / `timeline-table.spec.ts` — a two-currency party renders both
  balances separated; running totals are per currency.

## Verification (end to end — closes the initiative)
1. Create a **USD** shared expense split among parties; confirm shares match the ARS-equivalent
   split exactly.
2. Confirm a party who owes both ARS and USD shows **two** separated balances on the list and
   detail.
3. Settle the USD balance; confirm only the USD balance moves, ARS untouched, timeline running
   balance stays per currency.
4. Full regression: Dashboard, Financing, Subscriptions, Parties all show ARS and USD
   separated, never blended, across the whole app.
5. Suites green; lint + prod build clean.

## Doc-sync checklist
- `app/api/.claude/CLAUDE.md` — phase bump; ExpenseSplit currency; per-currency party balances;
  settle takes a currency.
- `app/client/.claude/CLAUDE.md` — shared-expense + settle selectors; two-balance current
  account; timeline per currency.
- `app/api/docs/DESIGN.md` / `app/client/docs/DESIGN.md` — the per-currency current-account
  model. Mark the USD initiative complete.
- Record the Parties migration id.
