# Slice 3: Undo an income (+ the double-reversal guard)

> Read `00-overview.md`, then Slices 1–2. **Slice 2 must be green**: the Undo action lives on Money Flow
> income rows and uses the `transactionId` those rows already carry.

## Intent

A mistakenly recorded income can be undone from Money Flow. Undo = the **existing** ledger reversal
(`POST /v1/ledger/transactions/{id}/reversal`), which posts a mirrored storno. The ledger stays
append-only and there is no edit or delete. After undo, Slice 2's view hides the pair, and Slice 1's
monthly-incomes view nets it to zero. So both surfaces update with **no new read-side code**.

## Why the guard is part of this slice

`ReverseTransactionHandler` blocks reversing *a reversal* (`Transaction.Reverse` →
`CannotReverseAReversal`), but **nothing blocks reversing the same original twice**. The index on
`OriginalTransactionId` is not unique (`TransactionConfiguration.cs:15`). Until now that hole was only
reachable through the reverse-movement page. A one-click Undo button (a double click or a stale tab)
would post two stornos, and the income would count as **negative**. Closing the hole is the price of
the button.

## Step 1: API production

- `src/Modules/Ledger/PersonalFinance.Ledger/Application/Commands/ReverseTransaction/ReverseTransactionHandler.cs`:
  after loading `original` and **before** `Transaction.Reverse(...)`:
  ```csharp
  var alreadyReversed = await context.Transactions
      .AnyAsync(transaction => transaction.OriginalTransactionId == original.Id, cancellationToken);
  if(alreadyReversed) {
      return LedgerErrors.TransactionAlreadyReversed;
  }
  ```
  This check-then-insert has a race window, but with a single user it's negligible. Add a
  `// ponytail:` comment naming the upgrade path: a unique filtered index on
  `OriginalTransactionId` (a migration) if concurrent writers ever exist.
- `Domain/LedgerErrors.cs`: `TransactionAlreadyReversed` ("Ledger.TransactionAlreadyReversed", "This transaction has already been reversed.").
- `src/Bootstrap/PersonalFinance.Api/Endpoints/ErrorHttpStatusHelper.cs`: explicit **409** mapping (the state-conflict precedent,
  e.g. `InstallmentNotAccrued` / `NotACreditorInstallment` → 409).
- No new endpoint and no migration. The guard applies to **every** reversal (expenses too), which is the point.

## Step 2: API tests

- `tests/PersonalFinance.Ledger.Tests` (the reverse-transaction handler tests; find the existing file, or create
  `ReverseTransactionHandlerTests.cs` from the in-memory harness):
  - reversing an income once → OK, the storno posted;
  - reversing it again → `TransactionAlreadyReversed`, and **no** second storno exists;
  - reversing a debit expense twice → the same error (the guard is global);
  - the existing `CannotReverseAReversal` behavior is unchanged.
- Api (WAF): the second `POST .../reversal` → 409 ProblemDetails with code `Ledger.TransactionAlreadyReversed`.
- Reporting: an income reversed → absent from `money-flow`, and `monthly-incomes` nets it to 0 for the month.
  (Slice 1–2 tests may already cover this. Add it only if missing.)

## Step 3: client production

- `LedgerService`: reuse the existing reversal call that `reverse-movement-page` uses (look it up in `ledger-service.ts`). Don't add a new one.
- `money-flow-table`: an "Undo" `<button type="button">` **on income rows only** (`kind === 'Income'`), with an
  `output<string>()` `undo` that emits the `transactionId`. The table stays presentational.
- `money-flow-page` (container) handles `undo`:
  - `window.confirm('Undo this income? This posts a reversal.')`. Native, no dialog component (YAGNI).
  - Track the in-flight id in a signal so its button is disabled while the request runs (this prevents the double click client-side too).
  - On success → refetch the month. On 409 → refetch anyway (it's already undone elsewhere) and show the inline error text.
    On other errors → show the inline error message following the page's error-state pattern.

## Step 4: client specs

- `money-flow-table.spec.ts`: income rows render Undo, outcome rows don't; clicking it emits the row's `transactionId`.
- `money-flow-page.spec.ts`: undo confirmed → calls reversal with the id, then refetches; confirm cancelled → no call;
  the button is disabled while in flight; a 409 → refetch + message.
- Lint, prod build, test, and report the new count.

## Step 5: doc-sync, closing the initiative

- API `CLAUDE.md` Phase 47 + `TASK.md`: the double-reversal guard (global, 409) and its ponytail ceiling.
- Client `CLAUDE.md` + `TASK.md` + `DESIGN.md`: Undo on income rows.
- Mark `docs/incomes-support/` complete in the API and client CLAUDE.md files. Update the project memory.

## Verification (end to end)
1. Record an income, then undo it from Money Flow → the row disappears; the Dashboard Income side for that month drops by that amount.
2. Open two tabs and undo the same income in both → the second gets the conflict message, and the income is **not** counted negative.
3. Outcome rows show no Undo button.
4. The reverse-movement page still reverses expenses, and a second attempt is now rejected with 409.
