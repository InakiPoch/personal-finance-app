# Slice 4 — Subscriptions show as "<name> Subscription"

> Read `00-overview.md` first. API Phase 53 / client Phase 50.

## Goal

The dashboard spending list (and Recent Money Movements, and later the Reverse feed) shows a
subscription as **"Claude Code Expense"**. The user wants **"Claude Code Subscription"** (D7): the
subscription's name plus a clear hint that it's a subscription, not the generic "Expense" suffix.

## Root cause (verified 2026-09-29)

It's **stored data**, not rendering:

- `A/Modules/Subscriptions/PersonalFinance.Subscriptions/Application/Commands/CreateSubscriptionTemplate/CreateSubscriptionTemplateHandler.cs:18-19`
  creates the category account with
  `new CreateAccountCommand($"{name} Expense", AccountType.Expense, AccountKind.Expense)`.
- The template stores that id: `SubscriptionTemplate.ExpenseAccountId`
  (table `subscriptions_templates`, column `ExpenseAccountId`, `SubscriptionTemplateConfiguration.cs:10,15`).
- Every read surface uses the ledger account name:
  - dashboard list ← `L/Infrastructure/Persistence/ReadViews/vw_ledger_monthly_expenses.sql` (Category = account name),
  - Recent Money Movements ← `vw_ledger_money_flow.sql` (`COALESCE(t.Description, <expense account name>, …)`;
    subscription charges have no `Description`, so the account name wins).
- `Account.Name` (`L/Domain/Account.cs:10`) is getter-only, set once at creation; **no uniqueness index**
  on name, so renaming can't collide at the DB level.
- Subscriptions never create a PaymentPlan; the charge is `Dr <name> Expense / Cr funding`
  (`SubscriptionChargeCalculator.cs`). So only this one account name matters.

Fixing it at the source (instead of a report-side join) cleans every surface at once, including
slice 7's feed.

## API changes

1. `CreateSubscriptionTemplateHandler.cs:19` → `$"{name} Subscription"`.
2. **Data migration** for existing rows, in the **Subscriptions** module's migrations (it owns the knowledge
   of which accounts are subscription accounts):
   ```sql
   UPDATE ledger_accounts
   SET Name = substr(Name, 1, length(Name) - length(' Expense')) || ' Subscription'
   WHERE Id IN (SELECT ExpenseAccountId FROM subscriptions_templates)
     AND Name LIKE '% Expense';
   ```
   - Match by **id** (the template's `ExpenseAccountId`), never by suffix alone — a user category named
     "Office Expense" must not be touched.
   - The `LIKE` guard makes it idempotent and skips accounts the user may have named differently.
   - `Down()`: the inverse (`' Subscription'` → `' Expense'`) with the same id filter.
   - **Verify before writing:** all modules share one SQLite file (`ConnectionStrings:PersonalFinanceDb`), and
     check how migrations are applied (host doesn't auto-migrate; per-context `dotnet ef database update`). If a
     fresh DB could run the Subscriptions migration before `ledger_accounts` exists, the `UPDATE` would fail at
     prepare time. If that's possible, put the migration in the **Ledger** context instead (`ledger_accounts` is
     then guaranteed; `subscriptions_templates` missing is the same problem) — or, simplest fallback, add an
     `ILedgerApi.RenameAccountAsync` + one-off idempotent startup fix. Pick whichever the migration order
     makes safe and note the choice in the step report.
   - This is a deliberate cross-module SQL touch in a one-off migration; mark it with a comment.
3. Account name max length: `name` + `" Subscription"` is 5 chars longer than before. Check the
   subscription name validator's max and the ledger account name max (`LedgerErrors.InvalidAccountName`
   rule in `Account.cs:~23`); if `maxName + 13 > accountMax`, tighten the subscription-name max.

## Client changes

None required — labels come from data. Verify by eye on the dashboard list and Recent Money Movements.
(If any client spec fixture hard-codes `'… Expense'` for a subscription row, update it for realism only.)

## Test plan

API:
- `CreateSubscriptionTemplateHandler` test: the created account name is `"Netflix Subscription"` (find the
  existing handler test / fake `ILedgerApi` that captures `CreateAccountCommand`).
- Migration: an integration test (or the Reporting fixture) seeds an account `"Netflix Expense"` referenced by a
  template and an unrelated `"Office Expense"`; after migrate, the first is `"Netflix Subscription"`, the second
  unchanged; running the UPDATE twice changes nothing.
- Name-length validator test if the max was tightened.

## Steps

- [ ] 1. API prod — handler string + migration (+ validator if needed). Build clean.
- [ ] 2. API tests — as above. All green.
- [ ] 3. Client prod — none expected; confirm by grep, report "no change".
- [ ] 4. Client specs — fixture tweaks only if any exist.
- [ ] 5. Doc-sync — API `TASK.md` Phase 53 (+ client Phase 50 line "no client change" or fold into API line);
      API `CLAUDE.md` evergreen fact: "subscription category accounts are named `<name> Subscription`".
