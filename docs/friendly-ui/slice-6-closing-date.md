# Slice 6 — Editable card closing dates (usual day + per-month overrides)

> Read `00-overview.md` first. Heavy API slice. API Phase 55 / client Phase 52.

## Goal

Real Argentine cards don't close on the same day every month (Visa might close the 24th in October and the 28th
in November). Today a card stores **one fixed `CutoffDay`** set at creation, with **no way to edit it**. The
user wants to fix the date **on the fly** (D9):

- **Usual closing day** — editable; applies to every month that has no specific date.
- **Per-month closing date** — "October closes on the 24th" — settable for **any month not yet closed**
  (current open month and future months). Can be cleared (falls back to the usual day).
- **Re-bucketing:** when a change moves the closing date of an open month, purchases already loaded whose
  billing month is now different **move** to the right billing month (all their installments shift together).
  Charged installments never move — if a change would move one, it's refused.

UI on **Cards and Accounts**, per card:

```
Visa                                   Usual closing day: 26  [edit]
  Next closings:  Oct 24 (set) [edit] [reset]  ·  Nov 26 [edit]  ·  Dec 26 [edit]
```

## Current state (verified 2026-09-29)

Prefixes: `F/` = `app/api/src/Modules/Financing/PersonalFinance.Financing`.

- `F/Domain/CreditCard.cs:7` — `public int CutoffDay { get; }`, getter-only, validated 1–31 in `Create`
  (`:30-32`, `FinancingErrors.InvalidCutoffDay` → 422). No update method. Naming drift: the create command/DTO
  and client call it `CutoffDate` (`CreateCreditCardValidator.cs:12`, client `instrument.ts:7 cutoffDate`),
  the list row `CutoffDay` (`Financing.Contracts/Queries/ListCreditCardsQuery.cs:5`).
- `F/Domain/BillingCycleCalculator.cs` — `ResolveCycle(purchaseDate, cutoffDay)`: purchase day ≤ (cutoff clamped
  to month length) → that month's cycle, else next month.
- `F/Domain/BillingCycle.cs` — `record BillingCycle(Year, Month)`, `DueCycle => AddMonths(1)`,
  `IsClosedAsOf(today, cutoffDay)` → `today > (Year, Month, clamped cutoffDay)`.
- **Cycles are stored per installment**, computed once at plan creation: `F/Domain/PaymentPlan.cs:68-75`
  → `firstCycle = ResolveCycle(purchaseDate, cutoffDay)`, installment *i* gets `firstCycle.AddMonths(i)`.
  `F/Domain/Installment.cs:9-10` — `CycleYear`, `CycleMonth` getter-only (required columns in
  `InstallmentConfiguration.cs:15-16`).
- Every live reader of `CutoffDay` (all must go through the new resolver):
  - `F/Application/Scheduling/AccrueInstallments.cs:57` — `installment.Cycle.IsClosedAsOf(today, card.CutoffDay)`
    decides when an installment gets charged; then finds/opens the `MonthlyStatement` for `(card, cycle, currency)`.
  - `F/Application/Commands/CreatePaymentPlan/CreatePaymentPlanHandler.cs:34` (passes to `PaymentPlan.Create`),
    `:127` + `:130` (back-dated catch-up: close check + `clampedCutoffInstant`), `:169` (due instant for the
    back-dated payment), helper `clampedCutoffInstant` `:103-106`.
  - `F/Application/Queries/GetCardPurchases/GetCardPurchasesHandler.cs:11-14, ~46` — "current cycle" for ordering.
  - `F/Application/Queries/ListCreditCards/ListCreditCardsHandler.cs:11-15` → `CreditCardRow.CutoffDay`
    → `H/Mapping/InstrumentMappingExtensions.cs:15` → `InstrumentRowDto`.
  - Creditor plans use a constant `PaymentPlan.CreditorCutoffDay = 26` — **out of scope**, unchanged.
- `MonthlyStatement` rows exist per `(CardId, CycleYear, CycleMonth, Currency)` once anything in that cycle is
  charged (`AccrueInstallments`, back-dated catch-up).
- Endpoints: only `MapPost(Instruments.Create)` and `MapGet(Instruments.List)` (`H/EndpointExtensions.cs:156,162`).
- Client: `C/features/instruments/pages/instruments-page/instruments-page.html:63-87` create form ("Cutoff day"
  input for credit), list shows `closes {{ instrument.cutoffDate }}` (~`:141`).

## Design decisions

- **"Closed" means "charged", not "date passed".** A month is locked once a `MonthlyStatement` exists for that
  card and cycle (anything charged into it). This lets the user fix a date *after* the bank closed but before the
  app charged anything (e.g. forgot to update it) and is the exact condition under which moving installments is
  safe. Month still open ⇒ editable.
- **Overrides live on the `CreditCard` aggregate** as a child collection `ClosingOverride(CycleYear, CycleMonth,
  ClosingDay)` (table `financing_card_closing_overrides`, unique `(CardId, CycleYear, CycleMonth)`). Store the
  **day** within the cycle month — the closing date of cycle (Y, M) is always in month M (simplifies resolution;
  `// ponytail:` note: a bank closing on the 1st of the next month isn't representable — add a full date if it ever
  happens).
- **One resolver replaces every `int cutoffDay`**: `CreditCard.ClosingDayOf(BillingCycle)` → override day or usual
  day, clamped to the month length. `ResolveCycle(purchaseDate)`: purchase day ≤ closing day of its own month →
  that cycle, else next. `IsClosedAsOf(cycle, today)` uses `ClosingDayOf(cycle)`. `BillingCycleCalculator` keeps
  its pure `(date, day)` shape; the card method feeds it the right day.
- **Re-bucket on every edit** (usual day or override): load the card's plans that have **any** non-reversed
  installment; for each, recompute `firstCycle = card.ResolveCycle(plan.PurchaseDate)`. If it differs from the
  stored first installment's cycle, compute the month delta and:
  - if **any** installment of that plan is charged (`IsAccrued`) → refuse the whole edit:
    `Financing.ClosingChangeMovesChargedPurchase` (409), listing nothing else changed (single `SaveChanges`);
  - else shift every installment by the delta (`Installment.MoveToCycle(BillingCycle)` — new internal method,
    `CycleYear/CycleMonth` get `private set`).
  Party splits and future-share projections read installment cycles, so they follow automatically.
- Edits to a **locked month** → `Financing.ClosingMonthLocked` (409). Usual-day edit only re-buckets purchases whose
  months are open (charged ones trigger the refusal above — the user then sets per-month overrides instead).

## API changes

1. Domain:
   - `CreditCard`: `CutoffDay` → `private set`; `ChangeUsualClosingDay(int day)` (1–31, reuses `InvalidCutoffDay`);
     `SetClosingDay(BillingCycle cycle, int day)`, `ClearClosingDay(BillingCycle cycle)`; `ClosingDayOf(cycle)`,
     `ResolveCycle(DateOnly)`, `IsClosedAsOf(cycle, today)`, `ClosingDateOf(cycle)`.
   - `ClosingOverride` child entity + configuration + migration (Financing context).
   - `Installment.MoveToCycle(BillingCycle)` (internal; fails if accrued).
   - `PaymentPlan.Create` receives the resolved `firstCycle` (or the card) instead of `int cutoffDay` — keep the
     creditor branch on `CreditorCutoffDay`.
   - Errors: `ClosingMonthLocked` (409), `ClosingChangeMovesChargedPurchase` (409), `InvalidClosingDay` or reuse
     `InvalidCutoffDay` (422) for a day outside the month. Map in `H/ErrorHttpStatusHelper.cs`.
2. Replace every `card.CutoffDay` read listed above with the card resolver (load overrides: `Include` in
   `AccrueInstallments`, `CreatePaymentPlanHandler`, `GetCardPurchasesHandler`).
3. Commands (Contracts + handlers under `F/Application/Commands/`):
   - `ChangeCardUsualClosingDayCommand(Guid CardId, int Day)`
   - `SetCardClosingDayCommand(Guid CardId, int Year, int Month, int Day)`
   - `ClearCardClosingDayCommand(Guid CardId, int Year, int Month)`
   Each: load card + overrides → apply → re-bucket (shared private/internal helper `CardRebucketer`, pure over
   plans + card so it's unit-testable) → one `SaveChangesAsync`. Lock check = `MonthlyStatements.Any(card, cycle)`.
4. Query `GetCardClosingScheduleQuery(Guid CardId, int Months = 6)` → rows from the current open month forward:
   `(Year, Month, ClosingDate, IsOverride, IsLocked)`.
5. `ListCreditCards` row gains `NextClosingDate` (closing date of the current open month) for the list.
6. Host routes (Instruments group):
   - `PUT /v1/instruments/cards/{id}/closing-day` body `{ day }`
   - `GET /v1/instruments/cards/{id}/closing-dates`
   - `PUT /v1/instruments/cards/{id}/closing-dates/{year}/{month}` body `{ day }`
   - `DELETE /v1/instruments/cards/{id}/closing-dates/{year}/{month}`
   `InstrumentRowDto` gains `nextClosingDate`.

## Client changes

- `instruments-service.ts`: the four calls; types for the schedule row.
- Cards and Accounts page, per credit card row:
  - "Usual closing day: 26 [edit]" inline number edit (1–31).
  - "Next closings" list (from the schedule query): date, "(set)" tag if override, `[edit]` date input limited to that
    month, `[reset]` if override; locked months not shown (the query starts at the current open month).
  - Errors: `ClosingChangeMovesChargedPurchase` → "Some purchases in that month are already on a card bill, so the
    closing date can't move them. Set this month's closing date only." `ClosingMonthLocked` → "That month's card bill
    is already out; its closing date can't change."
  - Replace `closes {{ cutoffDate }}` with "Next closing: Oct 24".
- Create form label "Cutoff day" → "Usual closing day" (glossary).

## Test plan

API (Financing tests):
- `CreditCard` resolver: no override → usual day; override wins for its month only; day clamped (31 in Feb).
- `ResolveCycle` with override: purchase on 25th, October override 24 → November; override 30 → October.
- Re-bucket: moving October closing earlier shifts a 3-installment plan bought on the 25th from Oct/Nov/Dec to
  Nov/Dec/Jan; later closing shifts back; plan with a charged installment → 409 and **nothing** persisted.
- Lock: a month with a `MonthlyStatement` → `ClosingMonthLocked`.
- Scheduler: `AccrueInstallments` charges on the override date, not the usual day (fake `TimeProvider`).
- Back-dated card plan creation honours overrides (existing back-dated tests stay green).
- Clear override → falls back to usual day and re-buckets.
- Host: the four routes' status codes (200/204/404/409/422).

Client:
- Service spec: the four HTTP calls.
- Instruments page spec: renders usual day and next closings; edit/reset call the service and reload; error codes map
  to the sentences above; "Next closing" text.

## Steps

- [ ] 1. API prod — domain + migration + resolver swap + commands/query + endpoints. Build clean.
- [ ] 2. API tests — as above; all existing Financing tests (cycles, back-dated, accrual) green.
- [ ] 3. Client prod — service, instruments page editing. Lint + prod build clean.
- [ ] 4. Client specs — as above. Green.
- [ ] 5. Doc-sync — API `TASK.md` Phase 55 / client Phase 52; API `CLAUDE.md` evergreen: "card closing = usual day +
      per-month overrides via `CreditCard.ClosingDayOf`; edits re-bucket open plans; months with a statement are locked";
      API `docs/DESIGN.md` billing-cycle section.
