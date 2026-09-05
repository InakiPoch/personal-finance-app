# Slice 1 — Creditor-financed expenses (card-optional plan + mode selector)

> **Part of** the "expense payment modes" effort (`docs/expense-payment-modes/`). Do Slice 1 → 2 → 3 in
> order; each slice is tested green before the next. This doc is self-contained: a fresh session should be
> able to execute it without re-reading the others.

---

## 1. Context — the gap this slice closes

Today "Load Expense" (`/financing/load-expense`) can only record a purchase on **one of the user's own
credit cards**. It always creates a Financing `PaymentPlan` whose installments are scheduled off that
card's `cutoffDay`. A "Different creditor" checkbox exists, but it only stores the creditor as **metadata
on top of a still-mandatory `cardId`** — the debt is recorded as living on the user's card even when it
doesn't.

**Real-world case this slice fixes:** a purchase **financed directly by a creditor** (a store's own
cuotas, or a person who fronted the money). The debt is owed **to the creditor**, not billed on any card
of the user's. So the card requirement must **drop**, and the installments become a plain monthly schedule
counted from the purchase date — there is no billing cycle because there is no card.

**Relevant locked decisions** (see `plan` for the full table):
- **D2** — Creditor-financed: drop the card, plain monthly schedule from purchase date, no cutoff.
- **D5** — A **three-way mode selector** (*My credit card* / *My debit-cash* / *Financed by a creditor*)
  replaces the "Different creditor" checkbox. This slice introduces the selector with **two modes live**
  (*My credit card*, *Financed by a creditor*); *My debit-cash* arrives in Slice 3.
- **D7** — Creditor debt is **Financing-only**: it does **not** post to the Ledger. No category picker in
  this mode — the expense is identified by creditor + description.
- **D9** — The existing "split with parties" feature carries over unchanged.
- **D11** — The credit-card path is **untouched**.

**Non-goals of this slice:** no Ledger posting for creditor debt; no "Owed to creditors" list (that is
Slice 2); no debit/cash mode (Slice 3); no financial-table dashboard (deferred entirely).

---

## 2. Current state (files + shapes this slice changes)

**Create command** — `app/api/src/Modules/Financing/PersonalFinance.Financing.Contracts/Commands/CreatePaymentPlanCommand.cs`
```csharp
public sealed record CreatePaymentPlanCommand(
    long AmountMinorUnits,
    Guid CardId,                       // ← currently REQUIRED, non-nullable
    int InstallmentCount,
    DateOnly PurchaseDate,
    string Description,
    PaymentPlanSplitPayload? Split = null,
    Guid? CreditorId = null,           // ← already exist, currently metadata-only
    Guid? CreditorAccountId = null
) : ICommand<Guid>;
```

**Host DTO** — `app/api/src/Bootstrap/PersonalFinance.Api/Endpoints/DTOs/CreatePaymentPlanDTO.cs` — mirrors
the command; `CardId` is a non-nullable `Guid`, `CreditorId`/`CreditorAccountId` are nullable.

**Domain aggregate** — `app/api/src/Modules/Financing/PersonalFinance.Financing/Domain/PaymentPlan.cs` —
`CardId` is an immutable non-nullable `Guid`; `CreditorId`/`CreditorAccountId` are nullable private-setter
properties. `PaymentPlan.Create(...)` currently **requires** `cardId` and `cutoffDay` and uses the cutoff
to place installments into billing cycles.

**Persistence** — `app/api/src/Modules/Financing/PersonalFinance.Financing/Infrastructure/Persistence/Configurations/PaymentPlanConfiguration.cs`
maps `CreditorId`/`CreditorAccountId` as nullable TEXT columns already.

**Accrual** — `app/api/src/Modules/Financing/PersonalFinance.Financing/Application/Scheduling/AccrueInstallments.cs`
posts to the Ledger against `card.ExpenseAccountId` / `card.LiabilityAccountId`. This path assumes a card.

**Creditor model** — `.../Financing/Domain/Creditor.cs` + `CreditorAccount.cs`. A `CreditorAccount` is
`{ id, label, identifier }` — a free-text payee destination (e.g. "Galicia" + a CBU/alias). **Not** a card,
**no** cutoff. List endpoint: `GET /v1/creditors`.

**Client form** — `app/client/src/app/features/financing/pages/load-expense-page/load-expense-page.ts`
(+ `.html`). Current form group:
```ts
{ amount, cardId, installmentCount, purchaseDate, description,
  split: FormArray, differentCreditor: boolean, creditorId, creditorAccountId }
```
`cardId` dropdown = `creditCards()` = `instruments()` filtered to `type === 'credit'`.
`differentCreditor` toggles the `creditorId` / `creditorAccountId` fields (which currently sit **on top of**
a required card). Request type: `app/client/src/app/features/financing/types/create-payment-plan.ts`.

---

## 3. Target behavior

**Two mutually-exclusive modes this slice** (via the new selector — see UX below):

| Mode | Card | Creditor fields | Schedule | Ledger |
|------|------|-----------------|----------|--------|
| *My credit card* (unchanged) | required | hidden | billing cycle off `cutoffDay` | accrues (existing) |
| *Financed by a creditor* (new) | **omitted** | required (creditor + account-to-pay) | **plain monthly** from purchase date | **none** |

**Creditor-mode schedule:** installment *k* (1-based) is due `PurchaseDate` plus *k* months (e.g. purchase
2026-01-10, 3 installments → due 2026-02-10, 2026-03-10, 2026-04-10). No cutoff, no `MonthlyStatement`
accrual. Amounts use the same phantom-penny allocation the existing plan uses (no rounding loss).

**Validation:**
- Creditor mode: `CreditorId` **and** `CreditorAccountId` required; `CardId` must be **absent**; the account
  must belong to the creditor.
- Card mode: exactly as today (`CardId` required; creditor fields absent/ignored).
- Reject the mixed state (both a card and a creditor) with a clear domain error.

---

## 4. Step-by-step implementation flow (API → Client → Tests)

### API
1. Make the card optional across the pipeline: `CreatePaymentPlanCommand.CardId` and the DTO's `CardId`
   become `Guid?`. Keep field order stable to avoid churn.
2. `PaymentPlan.Create(...)`: overload / branch so it accepts a **card-less** creation path. When there is
   no card, build the plain monthly schedule (installment *k* due `purchaseDate.AddMonths(k)`), set
   `CreditorId`/`CreditorAccountId`, and **do not** compute cycles. Add a domain guard rejecting
   "card AND creditor both present" and "neither present".
3. `CreatePaymentPlanHandler`: resolve the creditor + account (must exist and match) in creditor mode; in
   card mode keep the existing `CardNotFound` check and cutoff lookup. Return the new plan id.
4. `AccrueInstallments`: **guard off** for card-less plans — a creditor plan must never reach the
   Ledger-posting branch. (Confirm the accrual scheduler only selects card-backed plans.)
5. Persistence needs no new columns (`CreditorId`/`CreditorAccountId` already mapped); confirm a card-less
   plan persists with a null `CardId` (may require making that column nullable + a migration).

### Client
6. Replace the `differentCreditor: boolean` control with a **`mode`** control:
   `'card' | 'creditor'` this slice (extended to `'debit'` in Slice 3). Drive field visibility off `mode`.
7. Template: a three-way selector at the top of the form (render two options now; leave room for the third).
   - `mode === 'card'`: show card dropdown + installments (today's fields).
   - `mode === 'creditor'`: hide card; show creditor dropdown (`GET /v1/creditors`) + account-to-pay
     (auto-populated from the selected creditor, first auto-selected) + installments.
8. Request builder: in creditor mode, **omit `cardId`** and send `creditorId` + `creditorAccountId`; in card
   mode, omit the creditor fields. Update `create-payment-plan.ts` so `cardId` is optional.
9. Keep the `split` FormArray available in both modes (D9).

### Tests
10. **API** (`tests/PersonalFinance.Financing.Tests`): card-less plan is created; schedule due-dates are
    `purchaseDate + k months`; "card + creditor" rejected; "neither" rejected; creditor/account mismatch
    rejected; no Ledger transaction is produced for a creditor plan.
11. **Client**: switching `mode` shows/hides the right fields; creditor-mode payload omits `cardId` and
    includes creditor + account; card-mode payload unchanged; validators fire (creditor+account required in
    creditor mode).

---

## 5. Verification

- **API** (from `app/api/`): `dotnet build`, then
  `dotnet test --project tests/PersonalFinance.Financing.Tests`
  (use `--filter "FullyQualifiedName~CreatePaymentPlan"` while iterating). Always pass `--project` (the
  MTP runner requires it).
- **Client** (from `app/client/`): `pnpm ng lint`,
  `CHROME_BIN=/usr/bin/brave pnpm ng test --watch=false --browsers=ChromeHeadless`,
  `pnpm ng build --configuration production`.
- **Manual sanity:** run the API + client, record a "Financed by a creditor" expense; confirm the plan is
  created with a null card, the installment due-dates step monthly from the purchase date, and **no** Ledger
  transaction appears for it.
- **Passing state:** all Financing tests green; a card-less plan round-trips through the DB; the form's two
  modes each submit the correct payload.

---

## 6. Open risks / accepted tradeoffs

- Making `PaymentPlan.CardId` nullable touches an aggregate that several queries assume is card-backed
  (statements, card-purchase views). **Audit every consumer** of `PaymentPlan.CardId` before shipping —
  card-less plans must be excluded from card-cycle queries, not crash them.
- **D7 accepted:** creditor debt never hits the Ledger, so it will **not** appear in Ledger-derived expense
  reports. That is intentional; the "Owed to creditors" list (Slice 2) reads Financing data directly.
- The three-way selector ships with only two options live; Slice 3 adds the third. Keep the `mode` type and
  template structured so adding `'debit'` is additive, not a rewrite.
