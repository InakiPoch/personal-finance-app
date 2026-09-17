# Slice 1 — Explicit-pay foundation (kill auto-charge + wipe + registration rule + status)

> Read `00-overview.md` first. This slice is the trunk everything else hangs off.

## Goal / Why

Stop the auto-multiplication at the source and give every subscription a real **paid / overdue /
upcoming** status. After this slice:

- Nothing auto-charges ever again (the renewal scheduler is gone).
- Registering a subscription applies the "assume paid" rule (decision 8) — it posts **at most one** `X`,
  into the current month, and only when the anchor day is today or already passed.
- The Subscriptions page shows each subscription's status as a badge.
- Out-of-pocket reflects **only** deliberate charges (registration-time or, from Slice 2, user Pay).

This slice alone fixes the user's #1 complaint. Pay/Undo (Slices 2–3) and the Dashboard block (Slice 4)
build on the status model introduced here.

## Context & current state (grounded facts)

- Auto-charge chain (to remove): `RenewDueSubscriptions` scheduler →
  `RenewSubscriptionCommand`/`RenewSubscriptionHandler` → `SubscriptionChargeCalculator.Build(...)` posted
  `PostedOnUtc = now`, then `template.Renew(now)` advances one cycle. This is what produces `N·X` in the
  current month.
- Registration today: `CreateSubscriptionTemplateHandler` provisions the `"{Name} Expense"` ledger
  account, sets `NextDueDate = recurrence.Next(today)`, **and posts the first charge synchronously**. The
  synchronous first charge must become conditional (decision 8).
- The only paid-ish marker is `LastRenewalOnUtc : DateTimeOffset?`. It is repurposed here.
- `GetActiveSubscriptions` returns a flat `AmountMinorUnits` (the single `X`) and `NextDueDate`, no status.
- Out-of-pocket derives from the ledger; no Reporting changes are needed.

See `00-overview.md` → "Shared facts & patterns" for every file path.

## API changes

### Step 0 (run once, before re-adding data) — the wipe

A documented, reviewable, run-once cleanup (SQL script or one-shot maintenance routine — **not** a
permanent endpoint). Delete in dependency order:

1. Ledger entries + transactions carrying a `SubscriptionReferenceId` (find how the Ledger stores its
   `SubscriptionReference` — confirm the column/table during implementation).
2. Each subscription's `"{Name} Expense"` ledger account.
3. All rows in `subscriptions_templates`.

Deliver the script in the repo (e.g. under the wipe step's doc or a `scripts/` location the reviewer can
see) with an explicit "run once, then discard" note. Out-of-pocket self-corrects once (1) is gone.

### 1. Delete the auto-charge machinery

- Remove `Application/Scheduling/RenewDueSubscriptions.cs` and its hosted-service registration in
  `SubscriptionsModule.cs`.
- Remove `Application/Commands/RenewSubscription/` (command + handler) and its contract command.
  **Verify no other caller** (grep `RenewSubscription`, `Renew(` on the aggregate) before deleting.
- On the aggregate, remove/replace the `Renew(...)` method (its `NextDueDate = Recurrence.Next(NextDueDate)`
  logic moves into the new pay path in Slice 2).

### 2. Repurpose the paid marker + status shape on the aggregate

- Replace `LastRenewalOnUtc : DateTimeOffset?` with **`LastPaidPeriod : DateOnly?`** (anchor date of the
  most recent paid period; `null` = none). Update `SubscriptionTemplateConfiguration.cs` (column retype)
  and add an **EF migration** for the schema change (data is wiped, so no data migration).
- Keep `NextDueDate` but redefine it as **the due date of the next unpaid period**. It is now maintained
  only by registration (this slice) and pay/undo (Slices 2–3) — never by a scheduler.
- Add guarded domain methods mirroring `Installment.MarkPaid` / `ClearPayment`
  (`.../Financing/.../Domain/Installment.cs`), but **period-aware**. Suggested surface (finalise names in
  code review):
  - `MarkCurrentPeriodPaid(DateOnly paidPeriodAnchor)` — sets `LastPaidPeriod = paidPeriodAnchor` and
    advances `NextDueDate` one month. Used by registration (this slice) and pay (Slice 2).
  - `RevertLastPayment()` — steps `LastPaidPeriod` back one month (or null) and `NextDueDate` back one
    month. Used by undo (Slice 3).
  Keep `private set` on the mutated properties (the EF reference-nullable ctor-bind gotcha noted in the
  module's `CLAUDE.md` Phase 5 — mutate inside methods, not the constructor).

### 3. Registration rule (decision 8) in `CreateSubscriptionTemplateHandler`

Inject `TimeProvider` (primary ctor). Compute `today` and this month's anchor occurrence
(`currentAnchor`) from `AnchorDay` (reuse `RecurrenceRule` for the clamp/roll). Then:

- **`currentAnchor <= today`** (anchor passed this month, or is today):
  - Post one `X` via `SubscriptionChargeCalculator.Build(expenseAccountId, fundingAccountId, Amount, Id,
    postedOnUtc)` where `postedOnUtc` resolves to `currentAnchor` (for "today" that is today). This lands
    in the current calendar month → current month's out-of-pocket.
  - `MarkCurrentPeriodPaid(currentAnchor)` → status becomes **paid**, `NextDueDate` = next month's anchor.
- **`currentAnchor > today`** (anchor still ahead this month):
  - **Post nothing.** `LastPaidPeriod = null`, `NextDueDate = currentAnchor`. Status becomes **upcoming**.

This replaces the old unconditional first-charge posting. Provisioning the `"{Name} Expense"` account
stays exactly as today.

### 4. Extend `GetActiveSubscriptions` with status

- Inject `TimeProvider` into `GetActiveSubscriptionsHandler`; compute `today`.
- Add a **`Status`** field (string enum `paid | overdue | upcoming`) to `ActiveSubscriptionRow`
  (contract) and the host DTO. Keep `AmountMinorUnits` **flat** (the single `X`) and keep `NextDueDate`.
- Derive status per row (mirror `GetCreditorDetailHandler.statusFor`):
  - **paid** — `LastPaidPeriod` is in the same calendar month as `today`.
  - **overdue** — not paid-this-month **and** `NextDueDate <= today`.
  - **upcoming** — not paid-this-month **and** `NextDueDate > today`.

No new endpoint — `GET /v1/subscriptions/active` gains the `status` field. No error-mapping changes in
this slice (no new conflict codes yet).

## Client changes

- `types/active-subscription.ts`: add `status: SubscriptionStatus` where
  `SubscriptionStatus = 'paid' | 'overdue' | 'upcoming'` (new one-per-file type, matching the
  `frequency.ts` convention).
- `subscriptions-service.ts`: `listActive()` already unwraps `{ rows }`; ensure the `status` field maps
  through (lowercase if the API returns PascalCase, matching the existing `frequency` lowercasing).
- `pages/subscriptions-page/subscriptions-page.html`: in the active-subscriptions table, render a status
  badge per row. Reuse the creditor idiom (`creditor-purchases-table.html` `@switch` + `.status-badge`
  CSS): `paid` → hairline pill `color: var(--ledger)`; `overdue` → `text-negative` small-caps; `upcoming`
  → `text-ink-faint` small-caps. Keep the amount cell showing the flat `X` (it already does).
- Confirm loading / empty / error states remain intact per `SYSTEM.md`.

## Testing

**API** (`PersonalFinance.Subscriptions.Tests`, in-memory SQLite + private `FixedTimeProvider` +
`FakeLedgerApi`; see overview harness note). Assert:

1. **Registration, anchor already passed** (e.g. `fixedNow` = 16th, `AnchorDay` = 5): exactly **one**
   `PostTransactionCommand` captured by `FakeLedgerApi`, its posting date in the current month; status
   resolves **paid**; `NextDueDate` = next month's 5th.
2. **Registration, anchor is today** (`fixedNow` day == `AnchorDay`): exactly **one** charge, dated today;
   status **paid**.
3. **Registration, anchor still ahead** (`fixedNow` = 16th, `AnchorDay` = 20th): **zero** charges;
   status **upcoming**; `NextDueDate` = this month's 20th.
4. **No auto-charge exists**: there is no scheduler/renew path left to post extra charges (structural —
   the type is deleted; assert the module builds without it and that repeated query calls never grow the
   captured-postings list).
5. **Status derivation at boundaries**: a template whose `NextDueDate` is in the past and unpaid resolves
   **overdue**; one paid this month resolves **paid**; one due later this month, unpaid, resolves
   **upcoming**.

**Client** (`CHROME_BIN=/usr/bin/brave pnpm ng test --watch=false --browsers=ChromeHeadless`):
- `active-subscription` fixtures updated with `status`.
- `subscriptions-page.spec.ts`: renders the correct badge per status; flat amount unchanged.
- `subscriptions-service.spec.ts`: `listActive()` maps `status` through.

## Out of scope (later slices)

- The Pay action + endpoint (Slice 2).
- The Undo action + endpoint (Slice 3).
- The Dashboard block (Slice 4).

## Verification

1. `cd app/api && dotnet build`; run `PersonalFinance.Subscriptions.Tests` via its compiled binary.
2. `cd app/client && pnpm ng lint && pnpm ng build --configuration production`; run client tests.
3. End-to-end: run the API, execute the Step-0 wipe once, then re-add subscriptions:
   - past/today anchor → shows **Paid** and appears in this month's out-of-pocket (one `X`, not `N·X`);
   - future anchor → shows **Upcoming** and is **absent** from out-of-pocket.
   Leave the API running past a due date (or seed a past `NextDueDate`) → the sub shows **Overdue** and
   stays **absent** from out-of-pocket, and no extra charge appears (scheduler is gone).
4. Slice is green before starting Slice 2.
