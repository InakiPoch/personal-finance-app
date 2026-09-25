# Slice 3 — Subscriptions in USD + the existing-data flip

> Read `00-overview.md`, then Slices 1–2. This slice adds currency to the **Subscriptions**
> module and performs the **one-time data migration** the user authorized: every existing
> subscription becomes USD (they were always paid in dollars); everything else stays ARS.

## Intent

- Record a subscription in ARS **or** USD from the Load Subscription form.
- Currency propagates from `SubscriptionTemplate` to the ledger entries its payments post.
- **Flip all existing subscriptions to USD** — template **and** payment history — so the
  ledger tells the truth. Accepted consequence: shared funding Bank/Cash accounts become
  poly-currency (separate ARS and USD balances).

## Key facts about subscription payments (from exploration)

- A subscription **pay** (`PaySubscriptionHandler.cs`, built by `SubscriptionChargeCalculator`)
  posts **one** double-entry transaction:
  - `Dr "{Name} Expense"` — a **dedicated per-subscription** account (isolated).
  - `Cr FundingAccount` — a **shared** Bank/Cash instrument, also used by ordinary expenses.
- The transaction is tagged: `ledger_transactions.SubscriptionReferenceId = template.Id`. This
  column is set **only** on subscription charges, and `Transaction.Reverse` **copies** it onto
  reversal (storno) rows. So subscription-originated entries are **precisely identifiable**.
- Subscriptions keep **no** payment-history table — only `LastPaidPeriod` /
  `LastPaidTransactionId` pointers on the template. The money footprint is:
  `subscriptions_templates.AmountMinorUnits` + the `AmountMinorUnits` of every entry whose
  transaction has `SubscriptionReferenceId IS NOT NULL`.
- `CreateSubscriptionTemplateHandler.cs` can also post the same charge shape once at creation
  ("assume paid"), same tag — covered by the same flip.

## API changes

### 1. Subscription template gets `CurrencyCode`
- `Domain/SubscriptionTemplate.cs` — `Amount` is `Money`; apply the two-column mapping in
  `SubscriptionTemplateConfiguration.cs` (add sibling `CurrencyCode`).
- Migration in the **Subscriptions** context: `CurrencyCode TEXT NOT NULL DEFAULT 'ARS'` on
  `subscriptions_templates`.

### 2. Thread currency through create + pay
- `CreateSubscriptionTemplateHandler.cs` (+ `CreateSubscription` DTO / endpoint): add
  `CurrencyCode`; build `Amount` with `Currency.FromCode(...)`.
- `SubscriptionChargeCalculator.cs` / `PaySubscriptionHandler.cs` — post the charge in the
  **template's** currency (both legs same currency). No math change.

### 3. THE FLIP — one-time data migration
This is the heart of the slice. It must run **after** the Ledger `Entry.CurrencyCode` column
exists (Slice 1) and the Subscriptions column exists (step 1 above). Sequence inside the
migration (or a dedicated data-migration step):

1. All money columns already backfilled to `'ARS'` by their `DEFAULT` (Slices 1–2 + step 1).
2. `UPDATE subscriptions_templates SET CurrencyCode = 'USD';` — every template becomes USD.
3. Flip the payment history — **both legs**, reversals included:
   ```sql
   UPDATE ledger_entries
      SET CurrencyCode = 'USD'
    WHERE TransactionId IN (
      SELECT Id FROM ledger_transactions WHERE SubscriptionReferenceId IS NOT NULL
    );
   ```
   Because both legs of each subscription transaction flip together, each transaction still
   **balances within USD** (`Σdebits == Σcredits` per currency holds).
4. Result: the dedicated `"{Name} Expense"` accounts are wholly USD; each shared funding
   Bank/Cash account now holds its original ARS entries **plus** the flipped USD subscription
   credits — a poly-currency account, shown as two separate balances (the model from Slice 1).

> ⚠️ Cross-module ordering: the flip touches `ledger_entries` (Ledger module) but is driven by
> subscription semantics. Put the `ledger_entries` UPDATE in a **Ledger-context** migration (it
> owns that table) that runs after Slice 1, and the template UPDATE in the Subscriptions
> migration. Keep them in the same slice, applied together, and document the run order.

### 4. Subscription read-view + Dashboard block
- `Subscriptions/.../ReadViews/vw_active_subscriptions.sql` — replace `'ARS' AS CurrencyCode`
  with the real template column.
- Dashboard subscriptions block + `subscriptions-page` display via `formatMoney(amount, code)`.

## Client changes
- `features/subscriptions/pages/subscriptions-page/subscriptions-page.{ts,html}` — add the
  `currency` control (default `'ARS'`) to the create form; send `currencyCode` on
  `CreateSubscription`. Render each subscription's amount with `formatMoney(amount,
  subscription.currencyCode)`.
- Dashboard subscriptions block (`dashboard-page`) — display per the subscription's own
  currency (post-migration these are all USD, but the code must be currency-driven, not
  hardcoded).

## Test plan

### API (xUnit)
- `CreateSubscriptionTemplateHandler` with `CurrencyCode = "USD"`: template `Amount` is USD;
  the assume-paid charge (if triggered) posts USD entries.
- `PaySubscriptionHandler`: the posted transaction's entries are the template's currency;
  reversal via `UnpaySubscription` preserves currency + `SubscriptionReferenceId`.
- **Migration test**: seed a pre-existing subscription with ARS entries, run the flip, assert
  template + all its transaction entries are USD, and that the shared funding account now
  reports **both** an ARS balance (from other activity) and a USD balance.

### Client (Karma)
- `subscriptions-page.spec.ts` — create sends `currencyCode`; USD subscription renders as USD.
- Dashboard subscriptions block spec — currency-driven rendering.

## Verification (end to end, before Slice 4)
1. Apply the migration on a DB with existing subscriptions.
2. Confirm **every** existing subscription now reads USD (card + its transactions in the
   ledger/transactions view).
3. Confirm the funding Bank/Cash account shows a distinct ARS balance and USD balance.
4. Create a new ARS subscription and a new USD subscription — both behave identically.
5. Suites green; lint + prod build clean.

## Doc-sync checklist
- `app/api/.claude/CLAUDE.md` — phase bump; subscription currency; **the one-time flip**
  (scoped by `SubscriptionReferenceId`, both legs) and the poly-currency-funding consequence.
- `app/client/.claude/CLAUDE.md` — subscription currency selector; per-currency display.
- `app/api/docs/DESIGN.md` / `PRD.md` — record the data migration as an irreversible one-time
  decision (all subscriptions → USD).
- Record both migration ids (Subscriptions column + Ledger flip).
