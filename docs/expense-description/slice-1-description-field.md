# Slice 1 — Expense description field (end-to-end + confirmation echo)

> **Read this whole file before writing any code.** It is self-contained: it assumes
> no prior context about this feature. It is the first of three vertical slices
> (tracer bullets). Build and test this slice fully before starting Slice 2.

---

## 1. Why this exists (context & intent)

In this app a user "**loads an expense**" — that means creating a Financing
`PaymentPlan` on a credit card (amount, card, number of installments, purchase date,
optional split, optional creditor). Today, after saving, the only acknowledgement the
user gets is a **raw GUID**: the load-expense confirmation panel prints *"Payment plan
`<guid>` created."* The purchase itself carries **no human-readable label** — there is
nowhere the user can record or later see *what a purchase was about*.

This slice adds a **required free-text `Description`** to the expense, threads it
through the whole create-expense write path, and closes the loop immediately by
**echoing the description back on the confirmation panel** instead of (or alongside)
the bare GUID. That makes the field provable end-to-end in one slice: type a
description, save, see it come back.

> This is the foundation. Slices 2 and 3 surface the same `Description` on a card-debt
> drill-down and a "recent purchases" list. Neither is possible until this field exists.

### Locked design decisions

| # | Decision | Answer |
|---|----------|--------|
| D1 | What is the field? | A free-text **description/label** for the purchase (e.g. "New laptop", "Groceries at Coto"). It is **independent** of the Dashboard report's `category` grouping — it is not a taxonomy/tag. |
| D2 | Required or optional? | **Required** on creation. The whole point is legibility; an optional field left blank reproduces the pain. |
| D3 | Column nullability | **`NOT NULL`**. |
| D4 | Existing rows | All existing data is **dummy**. Do a **full multi-project data-only wipe** (clear rows, keep schemas/tables) before applying the migration. SQLite cannot add a `NOT NULL` column without a default while rows exist, so the wipe is the enabler, not a nicety. |
| D5 | Validation | Server-side + form: **trimmed, required, 1–120 chars, single line (no newlines)**; any other Unicode allowed. Blank/whitespace-only is rejected. |
| D6 | Editing later | **Out of scope.** No expense field is editable in the app today; do not add an edit surface. |
| D7 | Ledger involvement | **None.** The description lives only on the Financing `PaymentPlan`. Do **not** add a column to the Ledger `Transaction`, and do **not** thread it into the ledger posting path (`AccrueInstallments` / `PostTransactionCommand`). |

### Mental model

`PaymentPlan` is the expense aggregate (Financing module). The create path is a
textbook CQRS vertical that already carries several optional fields (`Split`,
`CreditorId`, `CreditorAccountId`). Adding `Description` is the **same pattern**, except
it is required and validated. You are copying an existing groove, not inventing one.

---

## 2. Domain model

`PaymentPlan` aggregate — file:
`app/api/src/Modules/Financing/PersonalFinance.Financing/Domain/PaymentPlan.cs`

Current fields (for orientation): `Id`, `CardId`, `Total` (→ `TotalMinorUnits`),
`PurchaseDate`, `InstallmentCount`, `SplitReferenceId?`, `CreditorId?`,
`CreditorAccountId?`, child collections `Installments` + `SplitParticipants`.

**Add:** `public string Description { get; private set; }` — set inside the `Create()`
factory (trim there). It is a non-null reference type set in the factory, so a
private-setter property is the safe binding shape (mirror how `CreditorAccount.Identifier`
and `Installment.AccruedOnUtc` are private-setter, not ctor-bound).

**Table:** `financing_payment_plans` gains a `Description TEXT NOT NULL` column.

---

## 3. API — extend the existing `CreatePaymentPlan` vertical

Follow `app/api/.claude/rules/` (csharp-style, method-organization, minimal-api-structure).
`private` members are `camelCase`; `if(...)` with no space; brace on same line.

### 3.1 Domain
`Domain/PaymentPlan.cs` — add the `Description` property; add a `string description`
parameter to the `Create(...)` factory (place it with the required params, before the
optional trailing ones). Assign `Description = description.Trim();`. Validation of
emptiness/length happens in the validator (3.3), but the factory still trims.

### 3.2 Contracts (command)
`Financing.Contracts/Commands/CreatePaymentPlanCommand.cs` — add a **required**
`string Description` param. Put it among the required params (not a trailing optional),
e.g. after `PurchaseDate`:
```csharp
public sealed record CreatePaymentPlanCommand(
    long AmountMinorUnits,
    Guid CardId,
    int InstallmentCount,
    DateOnly PurchaseDate,
    string Description,
    PaymentPlanSplitPayload? Split = null,
    Guid? CreditorId = null,
    Guid? CreditorAccountId = null
) : ICommand<Guid>;
```

### 3.3 Application — validator + handler
- `Application/Commands/CreatePaymentPlan/CreatePaymentPlanValidator.cs` — add a rule:
  trim `command.Description`; reject when empty/whitespace → new error, and when the
  trimmed length is `> 120` or `< 1` → error. Add error codes to `FinancingErrors`
  (e.g. `Financing.BlankDescription`, `Financing.DescriptionTooLong`). Reject any
  newline (`\n`/`\r`) → reuse the blank/invalid code or a dedicated one. Mirror the
  existing `NonPositivePlanAmount` / `InvalidInstallmentCount` style and their HTTP
  mapping (domain-validation → 422 via `ErrorHttpStatusHelper`).
- `Application/Commands/CreatePaymentPlan/CreatePaymentPlanHandler.cs` — pass
  `command.Description` into `PaymentPlan.Create(...)`.

### 3.4 Persistence
`Infrastructure/Persistence/Configurations/PaymentPlanConfiguration.cs` — map
`Description` as a required column:
```csharp
builder.Property(plan => plan.Description).IsRequired();
```
(Column name defaults to `Description`; keep it.)

### 3.5 Migration + data wipe
**First** clear the data (data only, not schema) across the API's SQLite DB — the same
full multi-project data-only wipe done on 2026-09-04. Then create + apply the migration
(commands per `app/api/.claude/CLAUDE.md` → "EF Core migrations"):
```bash
# from app/api/
dotnet ef migrations add AddPaymentPlanDescription \
  --project src/Modules/Financing/PersonalFinance.Financing \
  --startup-project src/Modules/Financing/PersonalFinance.Financing \
  --context FinancingDbContext
dotnet ef database update \
  --project src/Modules/Financing/PersonalFinance.Financing \
  --startup-project src/Modules/Financing/PersonalFinance.Financing \
  --context FinancingDbContext
```
Confirm the generated `Up()` is `AddColumn<string>("Description", "financing_payment_plans",
nullable: false)`. If EF emits a default because rows might exist, the wipe was skipped —
redo the wipe and regenerate so the column is a clean `NOT NULL` with no default.

### 3.6 Host (DTO + mapping)
- `src/Bootstrap/PersonalFinance.Api/Endpoints/DTOs/CreatePaymentPlanDTO.cs` — add a
  required `string Description` (same position as in the command).
- `Endpoints/Mapping/FinancingMappingExtensions.cs` →
  `ToCreatePaymentPlanCommand(...)` — pass `dto.Description` through to the command.
- The endpoint `Endpoints/Financing/PostPaymentPlan.cs` and route need **no change** —
  it already delegates to the mapping extension. `POST /v1/financing/payment-plans`.

---

## 4. Client — add the field + echo it back

Angular 20, zoneless, Signals-first, reactive forms. Follow `app/client/.claude/CLAUDE.md`
and `app/client/docs/SYSTEM.md` ("warm homebanking" system) for styling.

### 4.1 Type
`src/app/features/financing/types/create-payment-plan.ts` — add `description: string;`
to `CreatePaymentPlan`.

### 4.2 Form component
`features/financing/pages/load-expense-page/load-expense-page.ts`:
- Add `description: FormControl<string>` to the `LoadExpenseForm` type and to the
  `FormBuilder.group(...)` init in `initLoadExpenseForm()`, with validators:
  `[Validators.required, Validators.maxLength(120), noBlank, noNewline]`. Reuse the
  custom-validator style in `features/financing/.../validation-helpers.ts` (where
  `positiveAmount`, `atMostTwoDecimals`, `positiveInteger`, `isoDate` live) — add
  `noBlank` (rejects whitespace-only) and `noNewline` there if not present.
- In the submit builder that constructs the `CreatePaymentPlan` body, include
  `description: this.form.controls.description.value.trim()`.

### 4.3 Template + confirmation echo
`load-expense-page.html`:
- Add a description input near the top of the form (label "Description"), styled per
  SYSTEM.md, with the usual invalid-state hint used by the other fields.
- **Confirmation panel** (currently ~lines 265–277, the `@if(submitStatus() === 'confirmed'
  && confirmedPlanId(); as planId)` block that prints *"Payment plan `<planId>` created."*):
  surface the description. Capture it on submit (e.g. a `confirmedDescription` signal set
  alongside `confirmedPlanId`) and render it prominently, e.g. *"'New laptop' — payment
  plan created."* Keep the id available (small/secondary) but the **description is the
  headline**. This is the visible proof of the slice.

### 4.4 Service
`features/financing/financing-service.ts` — `createPaymentPlan(body)` posts the body as-is;
**no change** needed once `description` is on the type and in the body.

---

## 5. Testing

### 5.1 API (xUnit v3, `tests/PersonalFinance.Financing.Tests` + `PersonalFinance.Api.Tests`)
- `CreatePaymentPlanHandlerTests` (in-memory SQLite, existing pattern) — a plan created
  with a description **persists** it; verify the stored value equals the trimmed input.
- Validator tests — blank/whitespace-only rejected; `> 120` chars rejected; a string with
  `\n` rejected; a valid 1–120 single-line string accepted. Assert the specific
  `FinancingErrors` code.
- `FinancingMappingExtensionsTests` (`Api.Tests`) — `ToCreatePaymentPlanCommand` threads
  `Description` through unchanged.

### 5.2 Client (Karma/Jasmine, zoneless — `provideZonelessChangeDetection()`)
`load-expense-page.spec.ts` (instance-based typed-view pattern already in the file):
- Form is invalid until `description` is filled (extend `fillValidForm()` accordingly).
- `description` rejects whitespace-only and a `>120` value (assert the control error).
- On submit, the `CreatePaymentPlan` body carries the trimmed `description`.
- After a confirmed submit, the confirmation panel renders the description text.

Run:
```bash
# API — from app/api/
dotnet test --solution PersonalFinance.sln
# Client — from app/client/
CHROME_BIN=/usr/bin/brave pnpm ng test --watch=false --browsers=ChromeHeadless
pnpm ng lint && pnpm ng build
```

---

## 6. Verification — Slice 1 done when

- [ ] `financing_payment_plans` has a `Description TEXT NOT NULL` column (migration applied; data wiped first).
- [ ] `POST /v1/financing/payment-plans` **rejects** a request with a missing/blank/`>120`/multiline description → `422` with a Financing description error code.
- [ ] `POST /v1/financing/payment-plans` with a valid description returns `201` and persists the trimmed value (verify in SQLite).
- [ ] The load-expense form requires a description and won't submit without one.
- [ ] After saving, the confirmation panel shows the **description**, not just a GUID.
- [ ] `dotnet test --solution` green with a higher count; `PersonalFinance.Architecture.Tests` still green (no new module edge).
- [ ] Client `ng test` + `ng lint` + `ng build` all green.

Manual end-to-end:
```bash
# from app/api/
dotnet run --project src/Bootstrap/PersonalFinance.Api
# then POST a payment plan with "description":"New laptop" and confirm the 201 body + the DB row.
```

---

## 7. Out of scope for this slice

- Any Ledger change (no `Transaction.Description`, no change to `AccrueInstallments` or
  `PostTransactionCommand`).
- The card-debt drill-down (Slice 2) and the recent-purchases view (Slice 3).
- Editing or deleting an expense's description.
- Surfacing the description on the Dashboard, statements, or the `/v1/ledger/transactions` feed.
