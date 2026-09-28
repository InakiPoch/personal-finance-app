# Slice 4 — Pay a party's part of an installment (settle + pay, reversible)

> Read `00-overview.md` and Slices 1-3 first. This is the **only** slice that crosses modules
> and touches the ledger. Everything before it is display-only. API Phase 51 / client Phase 46.
> Closes the initiative.

## Goal

When a creditor-financed expense is split with one or more parties, the installment pay dialog
gains one extra choice **per party**: **"Pay <Party>'s part ($share)"** plus a **"Received into"**
bank-account selector. Confirming does two things:

1. Records the **same settlement** the user would otherwise enter by hand on the party page:
   `Dr Bank / Cr Receivable_party`, via `IPartiesApi.SettleCurrentAccountAsync`. The party's
   balance drops by their share.
2. Records a payment of that share on the installment. The installment is partly paid, or fully
   paid if the rest was already covered.

Undoing that payment (the per-installment Undo, last-payment-first) **also reverses the
settlement's ledger transaction**, so the party owes that share again (D10).

## Rules (D8, D9, all user-confirmed)

- **Meaning**: "the party paid me, I pay the creditor". This is not "the party paid the creditor
  directly". So it's a settlement into a bank account the user picks, exactly as if they
  registered it manually in Parties.
- **Share** = that party's PhantomPenny share of **this installment** only, always the full share
  (no partial party payment).
- Offered **only on split-accrued installments** (`Installment.IsSplitAccrued`, meaning the scheduler
  has already posted `Dr Receivable / Cr CreditorPayable` for it). Future cuotas show no party
  option. That way the scheduler stays the single place that accrues, and nothing is double-posted.
- **One option per party**, hidden once that party has a payment row on this installment.
- **Disabled with an explanation** when the share is larger than the installment's remaining
  amount: "Their share ($X) is more than the $Y left on this cuota."
- No new outbox event. The existing settlement already emits `ExpenseSplitSettledIntegrationEvent`,
  which nothing consumes. A sync call through `IPartiesApi` is the pattern Financing already uses
  (`RecordSplitAccrualAsync` in `AccrueInstallments.cs`).

## API changes

### Share computation (reuse, don't re-derive)

The per-installment party share is computed today in four places, all with the same shape:
`PhantomPennyAllocator().Allocate(amount, [1, ..weights])`, participants **ordered by `PartyId`**,
index 0 = holder. The canonical one is
`F/Application/Commands/LinkPaymentPlanSplit/CreditorSplitReceivableCalculator.cs`.

Add a small sibling method there, e.g.
`IReadOnlyList<(Guid PartyId, long ShareMinorUnits)> PartyShares(Money amount, IReadOnlyList<PaymentPlanSplitParticipant> participantsOrderedByPartyId)`.
Use it for both the read query and the command, so the offered share and the settled share can't
disagree. **Always order participants by `PartyId` before calling it.** The accrual did, so the
receivable balance matches only under that order.

### Read: expose party shares on the detail row

- `CreditorInstallmentRow` + DTO gets `IReadOnlyList<CreditorInstallmentPartyShare> PartyShares`, with
  `CreditorInstallmentPartyShare(Guid PartyId, string PartyName, long ShareMinorUnits, bool IsPaid)`.
  - It's empty unless the plan has a split **and** the installment is split-accrued **and** not reversed.
  - `IsPaid` = a payment row on this installment has `PartyId == share.PartyId`.
- Party **names** live in Parties, so `GetCreditorDetailHandler` calls
  `IPartiesApi.ListPartiesAsync(new ListPartiesQuery())` once and maps `Id → Name`. It's one call per
  request, not per row. Missing name → "Unknown party".
- The client computes "disabled because share > remaining" from `ShareMinorUnits` vs
  `RemainingMinorUnits`. The server doesn't need a status enum for that.

### Command: pay a party's part

- Contracts: `PayCreditorInstallmentPartyShareCommand(Guid InstallmentId, Guid PartyId, Guid BankAccountId) : ICommand<Guid>`.
  It returns the new payment row id.
- Handler `F/Application/Commands/PayCreditorInstallmentPartyShare/…Handler.cs`, ctor
  `(FinancingDbContext, TimeProvider, IPartiesApi, ILedgerApi)`:
  1. Load the installment with `Payments` + its plan with `SplitParticipants`.
     - Not found → `InstallmentNotFound` (404).
     - Not a creditor plan → `NotACreditorInstallment` (409).
     - Reversed → `InstallmentAlreadyReversed` (409).
  2. New errors (all 409):
     - `!IsSplitAccrued` → `PartyShareNotDue`
     - party not among `SplitParticipants` → `PartyNotInSplit`
     - that party already has a payment row on this installment → `PartyShareAlreadyPaid`
  3. `share` from the shared calculator. `share > RemainingMinorUnits` → `PaymentExceedsRemaining` (400).
  4. `var settled = await partiesApi.SettleCurrentAccountAsync(new SettleCurrentAccountCommand(PartyId, share, BankAccountId, now, plan.Currency.Code))`.
     - Failure → return it unchanged. For example `SettlementExceedsBalance` means the party already
       settled their balance by hand, and `UnknownFundingAccount` means a bad bank id. Map both in the
       client.
     - `settled.Value` **is the settlement's ledger transaction id**. `SettleCurrentAccountHandler`
       returns `posting.Value`, verified.
  5. `installment.ApplyPayment(share, now, PartyId, settled.Value)` → `SaveChangesAsync`.
  6. **Compensation**: Parties and Financing are separate DbContexts, so there's no shared
     transaction. If step 5 fails *after* step 4 succeeded, call
     `ledger.ReverseTransactionAsync(new ReverseTransactionCommand(settled.Value, now))` before
     returning the error, so the party isn't left settled with no installment payment. Mark it
     `// ponytail: best-effort compensation, no outbox; move to an outbox saga if this ever flakes.`
- Host:
  - `POST /v1/financing/creditor-installments/{id}/pay-party`, body `PayCreditorInstallmentPartyShareRequestDto(Guid PartyId, Guid BankAccountId)`.
  - Map the new errors in `ErrorHttpStatusHelper.cs`, including a mapping for the Parties error codes
    passed through if they aren't mapped already.

### Undo: reverse the settlement too

`UnpayCreditorInstallmentHandler` gains `ILedgerApi`:

1. `var removed = installment.UndoLastPayment()` (Slice 1).
2. If `removed.SettlementTransactionId is { } txId`:
   - Call `ledger.ReverseTransactionAsync(new ReverseTransactionCommand(txId, now))`.
   - If that fails, return the error **without saving**, so the payment row stays.
   - `Ledger.TransactionAlreadyReversed` (409, the global double-reversal guard from incomes-support)
     is also a failure here. It means someone reversed it elsewhere, so surface it.
3. `SaveChangesAsync`.

Nothing else is needed in Parties. The reversal restores the receivable balance, and the party
page (balance + timeline) reflects it on its next load.

## Client changes

- Types:
  - `creditor-installment-row.ts` adds `partyShares: CreditorInstallmentPartyShare[]`.
  - New `pay-creditor-installment-party-share.ts` → `{ partyId, bankAccountId }`.
- `financing-service.ts`: `payCreditorInstallmentPartyShare(installmentId, body)`.
- `creditor-pay-dialog` (installment mode):
  - New inputs:
    - `partyShares` (already filtered to unpaid)
    - `remainingMinorUnits` (already there)
    - `bankAccounts: Instrument[]`
  - Each party renders a third radio, "Pay <name>'s part ($share)". It's `disabled` when
    `share > remaining`, with the explanation text visible beneath it (not just a tooltip, for
    accessibility).
  - Selecting a party reveals a required **"Received into"** `<select>` of bank accounts.
  - `confirm` output becomes a union:
    `{ kind: 'own', amountMinorUnits: Money | null } | { kind: 'party', partyId, bankAccountId }`.
    Update the Slice 1-3 callers to `kind: 'own'`.
- `creditor-detail-page`:
  - Load the instruments the same way `party-detail-page.ts` does: `bankAccounts` = instruments with
    `type === 'debit'` (~l.67). Reuse the same service call. Don't add a new endpoint.
  - Route `kind: 'party'` to the new service call → refetch.
  - Map `Parties.SettlementExceedsBalance` → "This party has no outstanding balance for this share —
    they may have already settled it on the Parties page." Also map `Parties.UnknownFundingAccount`,
    `Financing.PartyShareAlreadyPaid` and `Financing.PartyShareNotDue`.
- `creditor-purchases-table`: no change beyond passing the row (Slice 1 already emits the whole row).

## Tests

**API**:

- Calculator `PartyShares`: two parties with uneven weights, where the phantom penny lands the same
  way as `BuildLines`.
- `PayCreditorInstallmentPartyShareHandlerTests.cs`:
  - Use a fake/stub `IPartiesApi` + `ILedgerApi`, the same way existing Financing tests stub
    cross-module APIs. Check `AccrueInstallments` tests for the precedent.
  - Facts:
    - happy path → settlement called with (party, share, bank, now, currency), then a payment row
      with `PartyId` + `SettlementTransactionId`
    - the share completing the remaining → `PaidOnUtc` set
    - not accrued → `PartyShareNotDue`
    - unknown party → `PartyNotInSplit`
    - second time → `PartyShareAlreadyPaid`
    - share > remaining → `PaymentExceedsRemaining`, and the settlement is **not** called
    - settlement failure propagated, nothing persisted
- `UnpayCreditorInstallmentHandler`:
  - undo of a party row calls `ReverseTransactionAsync(txId)`
  - reversal failure → row kept
  - undo of an own row does not touch the ledger
- `GetCreditorDetailHandler`:
  - `PartyShares` present only for split-accrued, non-reversed installments
  - names mapped
  - `IsPaid` after a party payment
- Optional API-level test (`PersonalFinance.Api.Tests`): the real round trip. Pay a party's part,
  and the party balance drops by the share. Undo, and it's restored.

**Client**:

- dialog spec:
  - party radios render per unpaid share
  - disabled + explanation when share > remaining
  - bank select required when a party is chosen
  - emits the `party` union
- page spec: the party confirm calls the new service method and refetches; `SettlementExceedsBalance` message
- service spec: route + body

## Done when

- Everything is green.
- Manually, on a creditor expense split with a party, on a due cuota:
  - "Pay <party>'s part" into a bank account → the cuota shows paid $share of $amount, and the party
    page balance dropped by the share (with a settlement in the timeline)
  - Undo → both restored
- A future cuota shows no party option.

## Doc-sync (step 5, closes the initiative)

- Phase 51 (API) / 46 (client)
- api DESIGN: the party path is the one ledger-touching creditor payment; compensation note
- api PRD + client PRD / DESIGN
- memory/engram: mark the initiative complete
