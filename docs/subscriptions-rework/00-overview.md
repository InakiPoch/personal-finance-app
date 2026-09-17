# Subscriptions rework — explicit-pay model + Dashboard visibility (overview)

## Why this exists

The user registers subscriptions correctly, but their behaviour on the money side is wrong:

1. **The per-subscription amount auto-multiplies.** A subscription that costs `X` starts showing
   `2X` after its renewal date passes, then `3X` a month later, and so on.
2. **That inflation flows straight into "Out of pocket".** Because each renewal posts a real ledger
   charge, every extra `X` lands in the current month's out-of-pocket total and drags it up.
3. **There is no way to say "I haven't paid this yet".** Subscriptions have no paid/overdue concept —
   they only ever accumulate.

The end state the user wants: a subscription is **due**, then **overdue** once its renewal date passes,
and it **only touches out-of-pocket when they mark it paid**. The Dashboard gets a **read-only** block
that, for each active subscription, shows: name, renewal date, the **flat cost `X`** (never a running
historic total), and whether it is **paid** or **overdue**.

## The one decision that shapes everything: the charge is no longer automatic

Today the subscription model is **auto-charge**. Root cause, grounded in code:

- Each subscription owns a dedicated `"{Name} Expense"` ledger account
  (`AccountType.Expense` / `AccountKind.Expense`).
- A background scheduler `RenewDueSubscriptions`
  (`app/api/src/Modules/Subscriptions/PersonalFinance.Subscriptions/Application/Scheduling/RenewDueSubscriptions.cs`,
  `SchedulerBase`, `Interval = 1 min`, `RunOnStartup = true`) selects every active template whose
  `NextDueDate <= today` and sends a `RenewSubscriptionCommand` **once per elapsed cycle**.
- `RenewSubscriptionHandler` posts **another full `X`** via `SubscriptionChargeCalculator.Build(...)`,
  **stamped `PostedOnUtc = now`** (the execution instant, *not* the elapsed due date), then advances
  `NextDueDate` by exactly one cycle.
- The Dashboard "Out of pocket" total reads those postings straight from the ledger:
  `Reporting/.../MonthlyExpensesQuery.cs` → `Sql/monthly_expenses.sql` over
  `Sql/vw_ledger_monthly_expenses.sql`, which sums `Expense` accounts by calendar month. The
  subscription's expense account passes that filter, so **N missed cycles = N debits, all in the
  current month = `N·X`**.

So the "multiplication" is not a display bug and not a single field — it is N repeated real charges,
all mis-dated into the current month. There is **no "paid" concept at all**: the charge *is* the
payment, and it happens on its own.

**We invert the model. Renewal no longer charges anything. A period simply becomes due, then overdue.
The real `Dr Expense / Cr Funding` charge posts only when the user marks the period paid.** Because
out-of-pocket keeps reading the ledger, unpaid periods have no posting and therefore never count —
we get the "unpaid ⇒ excluded" behaviour for free, with no parallel reporting path.

## Settled design decisions

| # | Decision | Choice |
|---|----------|--------|
| 1 | Billing model | **Explicit pay.** Kill the auto-renew charge. The `Dr Expense / Cr Funding` posting happens at pay-time, not on a schedule. |
| 2 | Dashboard | **Display-only.** The Pay/Undo behaviour lives in the Subscriptions view; the Dashboard shows a read-only summary block styled like "Card debt by cycle". |
| 3 | Scope | **Tight.** Monthly-only stays monthly. No new frequencies, no creation/cancel redesign, no fields beyond what paid/overdue needs. |
| 4 | Overdue catch-up | **One period at a time.** Paying an overdue sub posts exactly one `X` and advances the due date by one month; if still in the past it stays overdue. No silent forgiveness, no silent multiply. |
| 5 | Pay-time charge date | **Dated today.** Money leaves the pocket when the user pays, so it lands in the current month's out-of-pocket ("what actually left your pocket"). |
| 6 | Undo | **Supported, own slice.** Reverses the pay-time ledger transaction and steps the due date back one period. |
| 7 | Existing data | **One-time wipe** (see below). Delete all subscription templates, their `"{Name} Expense"` ledger accounts, and every ledger transaction tagged with a `SubscriptionReferenceId`. Out-of-pocket self-corrects because it is derived. Packaged as a run-once documented cleanup — **no** permanent destructive endpoint. |
| 8 | Registration-time charge (the "assume paid" rule) | At creation, compare the anchor day to today. **Anchor already passed this month, or is today** ⇒ assume it was paid when subscribing ⇒ **post one `X` into the current month's out-of-pocket** (dated to this month's anchor date; for "today" that is today), mark the current period **paid**. **Anchor still ahead this month** ⇒ **no charge**, period is **upcoming**; it only enters out-of-pocket when the user presses Pay. Registration is the *only* place the app posts a charge on the user's behalf. |

## The status model (formalised in Slice 1)

We repurpose the only existing temporal marker, `LastRenewalOnUtc`, into a **paid marker** and derive
status at query time. The intended shape:

- **`LastPaidPeriod : DateOnly?`** — the anchor date of the most recent period the user has paid for
  (`null` = none paid). This replaces `LastRenewalOnUtc`. It is a *period*, not a payment timestamp, so
  status math is unambiguous.
- **`NextDueDate`** (existing column, redefined) — the due date of the **next unpaid period**. It is set
  at registration, advanced one month on pay, stepped back one month on undo, and **is no longer touched
  by any scheduler** (the scheduler is deleted).

Status derived in `GetActiveSubscriptions`, with `today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime)`:

- **paid** — this calendar month's period is covered (`LastPaidPeriod` falls in the same month as
  `today`). Its charge is in the ledger ⇒ it is in out-of-pocket.
- **overdue** — `NextDueDate <= today` and this month is not paid ⇒ **excluded from out-of-pocket**.
- **upcoming** — `NextDueDate > today` and this month is not paid ⇒ not yet due, excluded until paid.

Reference implementations to mirror (Financing):
- `GetCreditorDetailHandler.statusFor(...)`
  (`.../Financing/Application/Queries/GetCreditorDetail/GetCreditorDetailHandler.cs`) — the
  `reversed/paid/overdue/due/future` derivation shape.
- `GetCreditorPayablesHandler`
  (`.../Financing/Application/Queries/GetCreditorPayables/GetCreditorPayablesHandler.cs`) — the
  `TimeProvider` injection + `Year*12 + Month` ordinal date comparison.

## The one-time wipe (Slice 1, Step 0)

Blast radius — three things, in dependency order:

1. Every ledger transaction (and its entries) carrying a `SubscriptionReferenceId` (the old auto-charges).
2. Every subscription's `"{Name} Expense"` ledger account.
3. Every row in `subscriptions_templates`.

Out-of-pocket is derived from the ledger, so once (1) is gone the current and past months self-correct.
Deliver this as a **documented, reviewable, run-once** step (a SQL script or a one-shot maintenance
routine executed deliberately), **not** a permanent endpoint. After the wipe, the user re-adds their
handful of subscriptions and they flow through the new registration rule (decision 8).

## Slice sequence (each fully green before the next)

| Slice | Title | Ships | Depends on |
|-------|-------|-------|------------|
| 1 | `slice-1-explicit-pay-foundation.md` | Kill auto-charge + wipe + registration rule + status on the Subscriptions page. Fixes the #1 pain: no more auto-inflation. | — |
| 2 | `slice-2-pay-live-period.md` | The Pay action: mark a due/overdue period paid, post `X` dated today, advance one period. | 1 |
| 3 | `slice-3-undo-payment.md` | The Undo action: reverse the pay-time ledger movement, step the due date back. | 2 |
| 4 | `slice-4-dashboard-subscriptions-block.md` | The read-only Dashboard block (the payoff): active subs with renewal date, flat cost, paid/overdue badge. | 1 (uses status); best after 2 so the badge is actionable |

Each slice is a tracer bullet: **API + Client + Testing**, verified end-to-end before the next begins.

## Shared facts & patterns (cited by every slice)

**Subscriptions module** — `app/api/src/Modules/Subscriptions/`:
- Aggregate: `PersonalFinance.Subscriptions/Domain/SubscriptionTemplate.cs` (note the type is
  `SubscriptionTemplate`, table `subscriptions_templates`). Fields: `Id`, `Name`, `Amount` (Money;
  persisted `AmountMinorUnits`, currency `Currency.Reference` = ARS), `Category`, `ExpenseAccountId`,
  `FundingAccountId`, `Frequency` (`RecurrenceFrequency`, **Monthly only**), `AnchorDay` (1–31, clamped),
  `NextDueDate` (`DateOnly`, private setter), `IsActive`, `LastRenewalOnUtc` (`DateTimeOffset?` — being
  repurposed). Value objects: `RecurrenceRule` (`.Next(after)` clamp/roll), `RenewalSchedule` (both
  `builder.Ignore`d). Cadence: `RecurrenceRule(Frequency, AnchorDay)`.
- Create: `.../Application/Commands/CreateSubscriptionTemplate/CreateSubscriptionTemplateHandler.cs`
  (today provisions `"{Name} Expense"` via `ILedgerApi.CreateAccountAsync`, sets
  `NextDueDate = recurrence.Next(today)`, and posts the first charge synchronously — this last part changes).
- **Delete:** `.../Application/Scheduling/RenewDueSubscriptions.cs` and the renew command/handler in
  `.../Application/Commands/RenewSubscription/` (dead under the new model; verify no other caller).
- Charge builder (**reuse for pay-time**): `.../Application/SubscriptionChargeCalculator.cs` —
  `Build(expenseAccountId, fundingAccountId, amount, subscriptionId, postedOnUtc)` → `PostTransactionCommand`
  with `Dr Expense / Cr Funding` lines and `SubscriptionReferenceId`.
- Read: `.../Application/Queries/GetActiveSubscriptions/`; contract row
  `PersonalFinance.Subscriptions.Contracts/Queries/GetActiveSubscriptionsQuery.cs`
  (`ActiveSubscriptionRow(Guid SubscriptionId, string Name, long AmountMinorUnits, string Category,
  RecurrenceFrequency Frequency, int AnchorDay, DateOnly NextDueDate)` — flat amount, never multiplied).
- EF config: `.../Infrastructure/Persistence/Configurations/SubscriptionTemplateConfiguration.cs`.
- Module registration: `.../PersonalFinance.Subscriptions/SubscriptionsModule.cs`.
- Cancel (unchanged): `.../Application/Commands/CancelSubscription/` flips `IsActive` false; `DELETE /v1/subscriptions/{id}`.

**Host** — `app/api/src/Bootstrap/PersonalFinance.Api/`:
`Endpoints/Subscriptions/*` (`PostSubscription.cs`, `GetActiveSubscriptions.cs`), `Endpoints/ApiRoutes.cs`,
`Endpoints/EndpointExtensions.cs` (`MapSubscriptionsEndpoints`), `Endpoints/ErrorHttpStatusHelper.cs`,
`Endpoints/DTOs/*`. Endpoints return `TypedResults.Ok(...)` for state changes (not `Created`), unwrapping
failures via `ProblemResultsHelper.From(result.Error)`.

**Error mapping** — `ErrorHttpStatusHelper.cs`: the suffix fallback maps `*NotFound`→404,
`*AlreadyPaid`/`*AlreadyAccrued`/`*AlreadyReversed`/`*NotActive`→409, `.Invalid`/`NonPositive`→422, else
400. Any new `Subscriptions.*` conflict code that does **not** end in one of those suffixes needs its own
explicit 409 line.

**TimeProvider** — `TimeProvider.System` is a singleton registered in `AddSharedInfrastructure`
(`app/api/src/Shared/PersonalFinance.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs`).
A handler just adds `TimeProvider` to its primary constructor. Canonical "today":
`DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime)`.

**Test harness** — `app/api/tests/PersonalFinance.Subscriptions.Tests/` (Subscriptions already has
EF-touching test infra: `SubscriptionsDbContext` + a design-time SQLite connection factory). Mirror the
Financing pattern: `IDisposable` class, in-memory SQLite (`Filename=:memory:`, opened, `EnsureCreated()`
builds schema from the model — not migrations), a private nested `FixedTimeProvider : TimeProvider`, a
throwing connection factory, seeding through the real domain/handlers, and a `FakeLedgerApi` that captures
every `PostTransactionCommand` in a `PostedTransactions` list for assertions
(see `app/api/tests/PersonalFinance.Financing.Tests/FakeModuleApis.cs`). **Run tests via the compiled
per-project binary** — the repo's `dotnet test --solution` reports "zero tests ran" in some shells.

**Client** — `app/client/src/app/features/`:
- Subscriptions: `subscriptions/` — `pages/subscriptions-page/{ts,html,css,spec.ts}` (single container:
  register form + active list), `subscriptions-service.ts` (`listActive()` GET `subscriptions/active`
  unwraps `{ rows }` + lowercases `frequency`; `create()` POST `subscriptions`; `cancel()` DELETE
  `subscriptions/{id}`), `types/active-subscription.ts` (`subscriptionId, name, amountMinorUnits: Money,
  category, frequency: Frequency, anchorDay: number, nextDueDate: IsoDate`), `types/frequency.ts`
  (`'monthly'`).
- Dashboard: `reports/pages/dashboard-page/{ts,html}` — injects `ReportsService` + `FinancingService`;
  `monthlyTotal()` = Σ `expensesByCategory()`; sections: header, **Out of pocket**, **Card debt by cycle**,
  Quick-actions nav. **Reads no subscription data today.**
- Status-badge idiom to reuse: `financing/pages/creditor-detail-page/creditor-purchases-table.*`
  (`@switch(row.status)` over a status union; `.status-badge` hairline pill `color: var(--ledger)`;
  temporal states as small-caps at descending ink weight) and
  `financing/pages/statement-page/installments-table.*` (`.installments__badge`).
- Design system: `app/client/docs/SYSTEM.md` — `--ledger`/`text-ledger` = settled/positive (paid),
  `--negative`/`text-negative` = error/overdue-alert, `--stamp` = blue accent (actions/hero). Every colour
  must be a token. The card panel pattern is `rounded-card border border-rule bg-paper-raised p-6
  shadow-card` with a `text-xs font-semibold uppercase tracking-[0.14em] text-heading` header + chevron
  link. Every data view needs loading / error (`role="alert"`) / empty / ready states.

**Reporting (no change needed)** — out-of-pocket derives from `Reporting/.../MonthlyExpensesQuery.cs` +
`Sql/monthly_expenses.sql` over `Sql/vw_ledger_monthly_expenses.sql`. Subscription expense accounts already
pass the `Expense` filter, so removing (wipe) or adding (pay) postings self-corrects the total with no
Reporting-side edits.

## Notes for a fresh session

- These docs are the *plan*; code happens one green-lit slice at a time, in order.
- Docs and all code artifacts are written in **English**, neutral/professional register.
- Confirm the exact EF migration needed for the `LastRenewalOnUtc` → `LastPaidPeriod` repurposing during
  Slice 1 (a column retype/rename). The data wipe is data-only; the field change is schema.
