# Slice 3 — Debit/cash expenses with categories

> **Part of** the "expense payment modes" effort. Depends on the **three-way mode selector** introduced in
> **Slice 1**. Self-contained: a fresh session should execute it from this doc alone.

---

## 1. Context — the gap this slice closes

The app is a **full monthly financial tracker** (`app/api/docs/PRD.md`: *"gastos en débito y crédito…"*),
but there is currently **no way to record a debit or cash expense**. "Load Expense" only makes credit-card
installment plans. The Ledger can *store* a transaction (`POST /v1/ledger/transactions`) but has **no UI**
and is raw double-entry with no category concept.

A debit/cash purchase is **money already gone** — a single, balanced Ledger transaction, not an installment
plan. This slice lights up the third mode of the selector (*My debit-cash*) and records that transaction,
**with a required category** so the user's future monthly breakdown ("what did I lose, and on what") is
possible.

**Relevant locked decisions:**
- **D3 / D4** — Debit and **cash** both route to a **single Ledger transaction** (source = `Bank` for
  debit, `Cash` for cash); otherwise identical.
- **D6** — **Lightweight categories**: get-or-create an `Expense`-kind Ledger account **by name**, captured
  at write time. **Required** for debit/cash.
- **D9** — Split with parties carries over (a split debit expense still posts receivables).
- **D11** — Credit-card path untouched.

**Non-goals:** no installments for debit/cash (a single payment); no financial-table dashboard (D10); no
category-management screen (the category is just a smart field on the form); creditor mode is unaffected
(it has no category — D7).

---

## 2. Current state

- **Category = an expense account's Name.** `app/api/src/Modules/Ledger/PersonalFinance.Ledger/Infrastructure/Persistence/ReadViews/vw_ledger_monthly_expenses.sql`:
  ```sql
  SELECT strftime('%Y-%m', t.PostedOnUtc) AS Month,
         a.Name AS Category,            -- category is literally the account name
         SUM(CASE WHEN e.Direction='Debit' THEN e.AmountMinorUnits ELSE -e.AmountMinorUnits END) …
  FROM ledger_entries e
  JOIN ledger_accounts a ON a.Id = e.AccountId
  JOIN ledger_transactions t ON t.Id = e.TransactionId
  WHERE a.Type='Expense' AND a.Kind NOT IN ('Receivable','CardPurchases')
  GROUP BY Month, a.Name;
  ```
  There is **no** category entity/table/enum. Expense accounts are minted **only** by cards
  (`Kind=CardPurchases`, name `"{Card} Purchases"`) and subscriptions (`Kind=Expense`, name
  `"{Sub} Expense"`). **No production endpoint** creates a generic expense account (`POST /v1/ledger/accounts`
  is dev-only).
- **Account-creation precedent** — `app/api/src/Modules/Financing/.../CreateCreditCard/CreateCreditCardHandler.cs`:
  ```csharp
  var expenseAccount = await ledger.CreateAccountAsync(
      new CreateAccountCommand($"{name} Purchases", AccountType.Expense, AccountKind.CardPurchases, OwnerReferenceId: cardId),
      cancellationToken);
  ```
  Reuse `ILedgerApi.CreateAccountAsync` with `AccountType.Expense, AccountKind.Expense` for categories.
- **Ledger post** — `app/api/src/Modules/Ledger/PersonalFinance.Ledger.Contracts/Commands/PostTransactionCommand.cs`:
  balanced `Lines` of `{ AccountId, Direction, Amount }`, `PostedOnUtc`, optional
  `Split/Installment/Subscription` reference + `Description`. Client:
  `app/client/src/app/features/ledger/ledger-service.ts#postTransaction` (scaffolded, **no UI**).
- **Instruments** — `GET /v1/instruments` returns `{ id, type:'debit'|'cash'|'credit', name, cutoffDate }`;
  debit/cash rows are Ledger `Bank`/`Cash` account ids. Client filters to `type==='credit'` today.
- **Form** — after Slice 1 the `load-expense-page` has a `mode` control (`'card' | 'creditor'`); this slice
  adds `'debit'`.

---

## 3. Target behavior

**Third mode — *My debit-cash*:**
- Instrument dropdown lists the user's **`debit` and `cash`** instruments (the account the money left).
- A **required category** field: pick an existing category or type a new one.
- **No installments** — the installment control is hidden and treated as a single payment.
- On submit, post **one balanced Ledger transaction** dated at the purchase date:

  | Leg | Account | Direction |
  |-----|---------|-----------|
  | source | selected `Bank`/`Cash` account | **Credit** (money out) |
  | category | the category `Expense` account (get-or-create by name) | **Debit** |

- **Split (D9):** if parties are split in, the holder's share debits the category account and each party's
  share debits a `Receivable`, exactly as the credit path splits today — reuse that line-building logic.

**Category get-or-create (D6):** normalize the typed name (trim + case-fold for matching), look up an
existing `Expense`-kind account by that name; if none, create one via `ILedgerApi.CreateAccountAsync(…,
AccountType.Expense, AccountKind.Expense)`. Idempotent — the same name never creates two accounts. Because
the monthly view groups by `a.Name`, the account name **is** the category label.

---

## 4. Step-by-step implementation flow (API → Client → Tests)

### API
1. **Category get-or-create + list.** Add a small query/command: `GET /v1/expense-categories` (list distinct
   `Expense`-kind, non-card account names) for the dropdown, and a get-or-create used during the debit post
   (either a dedicated command or inline in the debit handler). Reuse the `CreateCreditCardHandler` account
   pattern.
2. **Debit-expense post.** Add a handler that, given `{ amountMinorUnits, sourceInstrumentId, categoryName,
   purchaseDate, description, split? }`, resolves the category account (get-or-create), builds balanced
   lines (credit source, debit category, split → receivables), and calls the Ledger
   `PostTransactionCommand`. Validate: source instrument is `debit`/`cash`; category name non-blank; amount
   positive.
3. Expose the endpoint in the host `Endpoints/` folder under `/v1`. Decide routing: either extend the
   existing load-expense entry to branch server-side by mode, or add a sibling endpoint
   (e.g. `POST /v1/ledger/expenses`). Prefer a **dedicated debit endpoint** — it keeps the Financing
   payment-plan command clean and matches the module boundaries (debit is a Ledger concern).
4. Confirm the new category accounts satisfy the monthly-view filter (`Type='Expense'`,
   `Kind NOT IN ('Receivable','CardPurchases')`) so debit spend shows up categorized.

### Client
5. Extend the `mode` control to `'card' | 'creditor' | 'debit'`; render the third selector option.
6. `mode === 'debit'`: show a **debit/cash instrument** dropdown (`instruments()` filtered to
   `type==='debit' || type==='cash'`), a **category** field (existing list from
   `GET /v1/expense-categories` + free-type new), hide installments, keep amount/date/description/split.
7. Add a category type + service, and a `DebitExpenseService` (or extend the ledger service) calling the new
   endpoint. In debit mode the submit builds the debit payload, not a payment-plan payload.
8. Keep the credit + creditor modes exactly as they are.

### Tests
9. **API** (`tests/PersonalFinance.Ledger.Tests` and/or `...Financing.Tests` depending on where the handler
   lands): category get-or-create is idempotent (same name → same account, no duplicate); the posted
   transaction is **balanced** (debits == credits); a split debit posts the holder share + per-party
   receivables; the category appears in `vw_ledger_monthly_expenses`; a non-debit source instrument is
   rejected.
10. **Client**: debit mode shows instrument + category, hides installments; category is required; submit
    calls the debit path (not the payment-plan path); existing modes untouched.

---

## 5. Verification

- **API** (from `app/api/`): `dotnet build`, then
  `dotnet test --project tests/PersonalFinance.Ledger.Tests` **and**
  `dotnet test --project tests/PersonalFinance.Financing.Tests`
  (`--filter` to the new classes while iterating). Always pass `--project` (MTP runner).
- **Client** (from `app/client/`): `pnpm ng lint`,
  `CHROME_BIN=/usr/bin/brave pnpm ng test --watch=false --browsers=ChromeHeadless`,
  `pnpm ng build --configuration production`.
- **Manual sanity:** run API + client, register a debit instrument (`POST /v1/instruments` type=debit),
  record a debit expense with category "Groceries", and confirm the balanced transaction posts and
  "Groceries" appears as a category row in the monthly expenses view. Re-use "Groceries" on a second expense
  and confirm **no** second account is created.
- **Passing state:** Ledger + Financing tests green; a debit/cash expense round-trips as one balanced
  transaction; categories are get-or-created idempotently and surface in `vw_ledger_monthly_expenses`.

---

## 6. Open risks / accepted tradeoffs

- **Category name is the identity.** "Groceries" vs "groceries" vs "Grocery" — decide the normalization
  rule (recommend trim + case-insensitive match, store a canonical casing) so the monthly `GROUP BY a.Name`
  doesn't fragment. Document the rule in the handler.
- **Endpoint placement** (extend load-expense vs a dedicated debit endpoint) is a real fork — the doc
  recommends a dedicated Ledger-side endpoint to respect module boundaries; the executing session should
  confirm against the current host routing before wiring.
- Follow `app/client/docs/SYSTEM.md` for the form's visual treatment; the new fields must match the existing
  "warm homebanking" system.
- **D7 stays intact:** categories exist **only** for debit/cash. Creditor mode has no category; credit mode
  keeps card-based categorization. Do not add a category picker to those modes.
