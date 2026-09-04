# Slice 2 — Load Expense integration (Creditor + Account-to-Pay fields)

> **Read this whole file before touching code.** Written for a fresh session with zero prior context.
> **Dependency:** this slice requires **Slice 1** (`slice-1-creditors-crud.md`) to be fully shipped and green first — it needs real creditors (with accounts) to pick from. Do not start until Slice 1's checklist is complete.

---

## 1. Why this exists (context & intent)

We are letting an expense optionally record **who the user pays** (a Creditor) and **which of that creditor's accounts** to send money to (Account to Pay). Slice 1 built the Creditor entity + management view. **This slice wires the two new fields into the Load Expense form and persists them as metadata on the payment plan.**

Real-world driver: purchases made on *another person's* credit card, where that person later says *"pay me at this other account."* When the fields are left empty, the expense is assumed to be the user's own and only theirs — **today's behavior stays exactly as-is**.

### Locked design decisions (settled with the user — do not re-litigate)

| # | Decision | Answer |
|---|----------|--------|
| Q4 | Auto-fill | Pick a creditor → its accounts fill the dropdown, **first pre-selected**, switchable. No "default" flag. |
| Q5 | Creditor vs. existing Split | **Coexist freely** — independent optional fields, no coupling logic |
| Q6 | Scope | **Metadata only.** Store creditor + account on the expense. **Nothing posts to the ledger.** No payable balance. |

**Critical architectural note:** the existing *Split* field (Parties module) is the *mirror opposite* of Creditor — Split = "who shares this cost, they owe **me**" (receivable); Creditor = "who **I** pay" (payable-ish). They are orthogonal and may both appear on one expense. Do **not** add interaction logic between them. Storing the creditor is pure annotation — the two new columns are bare `Guid?`s, exactly like the existing opaque `SplitReferenceId` on `PaymentPlan`. No new ledger accounts, no double-entry, no settlement.

---

## 2. Behavior on the form

- A **"Different creditor"** toggle (checkbox/toggle control), **off by default**.
- **Off:** form is exactly today's (amount, card, installments, purchase date, optional Split). Creditor fields hidden and cleared; the request body omits them → persisted as `null`.
- **On:** reveals two dropdowns:
  1. **Creditor** — the existing creditors (from `GET /v1/creditors`).
  2. **Account to pay** — the *selected creditor's* accounts. On creditor change, repopulate and **auto-select the first account**; the user can switch.
- When the toggle is on, both are required (the account is auto-filled, so this is friction-free).

---

## 3. API — extend the existing CreatePaymentPlan vertical

All paths relative to `app/api/`. This is an **extension** of files that already exist — no new module surface.

### 3.1 Command (`PersonalFinance.Financing.Contracts`)
- `Commands/CreatePaymentPlanCommand.cs` — add two optional params:
  ```csharp
  public sealed record CreatePaymentPlanCommand(
      long AmountMinorUnits, Guid CardId, int InstallmentCount, DateOnly PurchaseDate,
      PaymentPlanSplitPayload? Split = null,
      Guid? CreditorId = null, Guid? CreditorAccountId = null) : ICommand<Guid>;
  ```

### 3.2 Domain + persistence
- `Domain/PaymentPlan.cs` — add `public Guid? CreditorId { get; private set; }` and `public Guid? CreditorAccountId { get; private set; }`. Thread them through `PaymentPlan.Create(...)` (add optional params, set the fields). Do **not** add navigation properties — bare Guids, like `SplitReferenceId`.
- `Infrastructure/Persistence/Configurations/PaymentPlanConfiguration.cs` — map both as nullable columns (`builder.Property(p => p.CreditorId);` / `CreditorAccountId`), mirroring how `SplitReferenceId` is mapped.

### 3.3 Handler
- `Application/Commands/CreatePaymentPlan/CreatePaymentPlanHandler.cs` — pass `command.CreditorId` / `command.CreditorAccountId` into `PaymentPlan.Create(...)`. No validation beyond "both null or both present" is required; keep it lenient (nullable). **Do not** create ledger accounts or outbox messages for the creditor — it is metadata.

### 3.4 Host DTO + mapping (`src/Bootstrap/PersonalFinance.Api`)
- `Endpoints/DTOs/CreatePaymentPlanDTO.cs` — add `Guid? CreditorId = null, Guid? CreditorAccountId = null`.
- `Endpoints/Mapping/FinancingMappingExtensions.cs` — in `ToCreatePaymentPlanCommand`, thread the two new fields through. Endpoint `Endpoints/Financing/PostPaymentPlan.cs` needs no shape change.

### 3.5 Migration
From `app/api/`:
```bash
dotnet ef migrations add AddCreditorToPaymentPlan \
  --project src/Modules/Financing/PersonalFinance.Financing \
  --startup-project src/Modules/Financing/PersonalFinance.Financing \
  --context FinancingDbContext
dotnet ef database update \
  --project src/Modules/Financing/PersonalFinance.Financing \
  --startup-project src/Modules/Financing/PersonalFinance.Financing \
  --context FinancingDbContext
```
This is an **ALTER** adding two nullable columns to `financing_payment_plans` — non-breaking for existing rows.

---

## 4. Client — extend Load Expense

All paths relative to `app/client/src/app/features/financing/`.

### 4.1 Component `pages/load-expense-page/load-expense-page.ts`
- Inject `CreditorsService` (from Slice 1) and load a `creditors` signal on init (mirror how `creditCards()` / `parties()` are loaded).
- Extend the form model:
  ```ts
  type LoadExpenseForm = FormGroup<{
    amount: FormControl<number | null>;
    cardId: FormControl<string>;
    installmentCount: FormControl<number | null>;
    purchaseDate: FormControl<string>;
    split: FormArray<SplitRow>;
    differentCreditor: FormControl<boolean>;       // NEW toggle
    creditorId: FormControl<string>;               // NEW
    creditorAccountId: FormControl<string>;        // NEW
  }>;
  ```
- **Accounts-for-selected-creditor:** a computed/derived signal from `creditorId` + `creditors()` giving the chosen creditor's accounts. On `creditorId` change (subscribe or effect): if the creditor has accounts, `setValue` `creditorAccountId` to the **first** account id; else clear it.
- **Toggle handling:** when `differentCreditor` flips off, reset `creditorId` + `creditorAccountId` to empty and clear their validators; when on, set them required. Re-run validity.
- **Submit:** include `creditorId` + `creditorAccountId` in the `CreatePaymentPlan` body **only when the toggle is on**; otherwise omit them.

### 4.2 Template `pages/load-expense-page/load-expense-page.html`
- Add the toggle + two `<select>`s near the Split section, each revealed by `@if (form.controls.differentCreditor.value)`.
- Use the existing **native-select pattern** already in this file (the Card dropdown): a `relative` wrapper, `appearance-none` select with `border-b border-rule … focus-visible:border-stamp`, and the `&#9662;` chevron span. Creditor options from `creditors()`; account options from the selected creditor's accounts. SYSTEM.md styling throughout.

### 4.3 Request type `types/create-payment-plan.ts`
- Add optional fields: `creditorId?: string; creditorAccountId?: string;`. `financing-service.ts` `createPaymentPlan(...)` needs no change (same endpoint).

---

## 5. Testing

### 5.1 API (`app/api/tests/PersonalFinance.Financing.Tests`)
Run: `dotnet test --project tests/PersonalFinance.Financing.Tests` (from `app/api/`).
- `CreatePaymentPlanHandler`: persists `CreditorId` + `CreditorAccountId` when supplied; leaves both `null` when absent (regression guard for the existing flow).
- Mapping: `CreatePaymentPlanDto.ToCreatePaymentPlanCommand()` carries the two new fields through.

### 5.2 Client (Karma/Jasmine)
Run: `CHROME_BIN=/usr/bin/brave pnpm ng test --watch=false --browsers=ChromeHeadless` (from `app/client/`).
- Extend `pages/load-expense-page/load-expense-page.spec.ts`:
  - Toggling "Different creditor" on reveals the two fields and makes them required.
  - Selecting a creditor populates the account options and **auto-selects the first**.
  - Submit body includes `creditorId` + `creditorAccountId` when on.
  - Toggle off → body omits both; **the existing 9 specs stay green** (this is the key regression bar).

---

## 6. Verification — "Slice 2 done when"

- [ ] `dotnet build` clean; `AddCreditorToPaymentPlan` migration created + applied; two nullable columns on `financing_payment_plans`.
- [ ] `dotnet test --project tests/PersonalFinance.Financing.Tests` green.
- [ ] `pnpm ng lint` + `pnpm ng build` clean; client tests green **including the untouched 9 load-expense specs**.
- [ ] **Manual end-to-end:** run API + client. Load an expense with "Different creditor" **on**, pick a creditor + account, submit → confirm the payment plan row persists `CreditorId` + `CreditorAccountId`. Load another with the toggle **off** → both columns `null`, and the whole legacy flow (amount/card/installments/split) behaves exactly as before.

---

## 7. Out of scope (deferred, per the plan)
- Payable **balances** / settlement / any ledger posting for creditors (Q6 = metadata only). *Natural future epic: mirror the Parties receivable machinery in reverse.*
- Surfacing the creditor/account on read views (statements, party timeline, transaction list) — a sensible next slice, but **not requested today**.
- Editing/deleting creditors (Q3), CBU validation / bank catalog (C-1).
