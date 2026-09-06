# Slice 1 — Stop the reconcile loop for card splits (client only)

> Part of the **parties-card-split** initiative. Do this **first** — it removes the user-visible hang
> and is a small, isolated client change with no API impact. Read `README.md` for the full context.

## Context — what is actually broken

When a user submits a **credit-card** expense that is **split with a party**, the page confirms the
expense correctly, then kicks off a background "current-account reconciliation" poll that can never
succeed for the card path, leaving the UI stuck on *"still reconciling — the receivable is being
posted, refresh shortly."*

Confirmed facts (see `README.md` §1–2):

- The payment plan is **already persisted** — `submitStatus` becomes `'confirmed'` and the "Confirmed"
  panel renders **before** `reconcile()` is called. The expense is not lost.
- `reconcile()` polls `getBalance` with the exit condition `balance.balanceMinorUnits !== prior`, for at
  most **5 attempts × 800 ms ≈ 4 s**, then sets the participant's status to `'stalled'` (the message the
  user sees). It is called for **all** splits, with **no payment-mode check**.
- A **card** split posts no receivable synchronously; accrual happens only when the billing cycle closes
  (next month). So the balance cannot change within 4 s → the poll always stalls for cards.

**Intent:** for `mode === 'card'`, skip the balance-change poll entirely and mark participants as
*scheduled* (their receivable accrues monthly, starting the next cycle). Debit/cash and creditor-financed
splits are unchanged — their up-front postings make the poll succeed as before.

## The single file that owns the behavior

`app/client/src/app/features/financing/pages/load-expense-page/load-expense-page.ts`

Relevant anchors (line numbers approximate — match by symbol):

- `type LoadExpenseMode = 'card' | 'creditor' | 'debit'` (~line 35)
- `type ReconciliationStatus = 'reconciling' | 'reconciled' | 'stalled'` (~line 34)
- Submit subscription — on success sets `this.submitStatus.set('confirmed')` (~line 189), then:
  ```ts
  if (participants.length > 0) {
    this.reconcile(participants);
  }
  ```
  (~lines 190–192). `raw.mode` (the selected payment mode) is in scope here (`raw` at ~line 147).
- `private reconcile(participants: SplitParticipant[]): void { ... }` (~line 201), containing the
  `pollUntil(...)` loop with `{ intervalMs: 800, maxAttempts: 5 }` and exit predicate
  `balance.balanceMinorUnits !== prior`.
- `updateReconciliation(partyId, status, balanceMinorUnits)` — already used to set per-participant status.

Template: `app/client/src/app/features/financing/pages/load-expense-page/load-expense-page.html`

- Reconciliation table renders only when `reconciliations().length > 0` (~lines 360–391).
- A `@switch` on the participant status with a `@case('stalled')` showing the "still reconciling…"
  copy (~lines 371–384).

## Steps

1. **Extend the status union** (`load-expense-page.ts` ~line 34):
   ```ts
   type ReconciliationStatus = 'reconciling' | 'reconciled' | 'stalled' | 'scheduled';
   ```

2. **Pass the mode into `reconcile()`** at the call site (~line 190):
   ```ts
   if (participants.length > 0) {
     this.reconcile(participants, raw.mode);
   }
   ```

3. **Early-exit for cards** — update the signature and add the guard **before** the polling loop
   (~line 201):
   ```ts
   private reconcile(participants: SplitParticipant[], mode?: LoadExpenseMode): void {
     // seed the reconciliations list as today (status 'reconciling'), unchanged …

     if (mode === 'card') {
       // Card-split receivables accrue on each billing cycle (starting next month), so there is
       // no synchronous balance change to wait for. Mark scheduled and skip the poll.
       participants.forEach(p => this.updateReconciliation(p.partyId, 'scheduled', null));
       return;
     }

     // … existing per-participant pollUntil loop for debit/creditor stays exactly as-is …
   }
   ```
   Place the guard *after* the initial `this.reconciliations.set(...)` seeding (so the table still shows
   the participants) and *before* the `for (const participant of participants)` poll loop.

4. **Template case** (`load-expense-page.html`, inside the status `@switch` ~line 371, next to the
   `'stalled'` case):
   ```html
   @case ('scheduled') {
     <span class="text-xs text-ink-faint">scheduled — accrues monthly</span>
   }
   ```
   Keep the copy consistent with the surrounding warm-homebanking tone; no exclamation/marketing voice.

## Tests

`app/client/src/app/features/financing/pages/load-expense-page/load-expense-page.spec.ts`

- **Card split does not poll:** arrange a `partiesService.getBalance` spy; submit with `mode: 'card'` and
  one participant; assert `getBalance` is **not** called and the participant's reconciliation status is
  `'scheduled'`.
- **Debit split still polls (regression guard):** submit with `mode: 'debit'` and one participant; assert
  `getBalance` **is** called (the early-exit must not swallow the non-card paths).
- Reuse the existing spec's harness/spies and the existing "confirmed" assertions.

## Verify

From `app/client/`:
```
CHROME_BIN=/usr/bin/brave pnpm ng test --watch=false --browsers=ChromeHeadless
pnpm ng lint
pnpm ng build
```
Live: create a credit-card expense split with a party → the "Confirmed" panel appears immediately, the
participant row reads **"scheduled — accrues monthly"**, and there is **no** repeated `getBalance` call
and **no** lingering "still reconciling" state. Confirm debit/cash and creditor-financed splits still show
their reconciled balance as before.

## Out of scope

- Any API change (the server behavior is correct; this is purely the client poll).
- Showing the *amounts* of the future monthly shares — that is Slice 2b, on the Parties view.
- Changing debit/cash or creditor-financed reconciliation.
