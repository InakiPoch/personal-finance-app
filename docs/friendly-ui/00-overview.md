# Friendly UI — overview

> Planning docs only (written 2026-09-29, branch `refactor/client-uiux`). No code yet.
> Read this file first, then the slices **in order**. Each slice is a vertical tracer bullet
> (API + client + tests, or client-only where no API work exists) and must be green before
> the next one starts.

## The problem

The app grew feature by feature and the UI still speaks like the codebase: "accrued",
"statement", "liability", "the API posts…", generic "Manual entry" rows. The user (the only
user, an Argentine homebanker) wants every screen to say plainly what a number means and what
an action will do. Concretely:

1. **Dashboard** — "Out of pocket" / "Income" and the expense list below them don't explain
   themselves; there is no single "how much do I have to pay this month"; subscriptions show as
   "Claude Code Expense"; the quick actions sit at the bottom where nobody sees them.
2. **Cards and Accounts** (today "Instruments") — card closing dates move month to month in real
   life, but the app stores one fixed day and it can't be edited.
3. **Load an Expense** — the currency selector is a faint tiny `<select>`; users don't notice
   they can switch to USD.
4. **Reverse** — the nav says "Reverse", the page says "Transactions", every row says "Manual
   entry" / "Installment accrual", and nothing tells you what reversing will do.
5. **Parties** — `/parties/shared-expense?party={id}` is a stale page that asks you to paste an
   account UUID; Load Expense's split already does the same thing better.
6. **Navbar** — 11 flat links in no order; the action pages don't stand out.
7. **Copy everywhere** — developer words ("API", "client computes nothing") and accounting jargon.

## Decisions (grilled with the user, 2026-09-29)

| # | Decision |
|---|---|
| D1 | **Plain-language glossary** (below) applies to every string written by every slice, not only slice 8. |
| D2 | Client-only slices are allowed when no API work exists. Every slice is tested before the next starts. |
| D3 | **Routes/URLs never change** — only labels and titles. Avoids breaking bookmarks and route specs. |
| D4 | Dashboard numbers keep their meaning — **relabel only** (Out of pocket stays "my share, bank+cash only"). |
| D5 | Dashboard order: quick actions → Due this month → Spent/Received tabs → Card bills by month → Active subscriptions. |
| D6 | "Due this month" = **cards + creditors**, ARS and USD shown separately (never converted), overdue included. |
| D7 | Subscription category accounts are named **`<name> Subscription`** (was `<name> Expense`), with a data migration for existing ones. |
| D8 | The Parties shared-expense page **and** its `POST /v1/parties/shared-expenses` endpoint are deleted. The party page links to Load an Expense with the party prefilled in the split. |
| D9 | Closing dates: the card keeps a **usual closing day** (editable) plus **per-month closing-date overrides** for any month not yet closed. Editing re-buckets not-yet-charged installments of affected purchases; if a charged installment would move, the edit is refused. |
| D10 | Reverse: each row gets a real description, a type badge, from → to accounts, and an expandable **"If you reverse this"** impact panel. Built at read time (no backfill of old rows). |
| D11 | Currency on Load an Expense becomes a **segmented ARS \| USD toggle** matching the "Paid with" picker. |

## Glossary (D1)

| Don't say | Say |
|---|---|
| statement | card bill |
| cycle / billing cycle | billing month |
| accrued / accrual | charged to the card |
| future (installments) | upcoming |
| liability | what you owe |
| receivable | owed to you |
| reversal / storno / reversal entry | undo entry |
| cuota (in English copy) | installment |
| "the API …", "the client …", "endpoint", "UUID" | never mention — describe the outcome instead |
| "Personal ledger" eyebrow | removed |

"Installment" stays. "Reverse a Transaction" (the nav/page name) stays because the user chose it.

## Slices (in order)

| # | Doc | Scope | API phase | Client phase |
|---|---|---|---|---|
| 1 | [slice-1-navbar.md](slice-1-navbar.md) | Grouped navbar + renames | — | 47 |
| 2 | [slice-2-currency-toggle.md](slice-2-currency-toggle.md) | ARS \| USD segmented toggle | — | 48 |
| 3 | [slice-3-remove-shared-expense.md](slice-3-remove-shared-expense.md) | Delete stale page + endpoint, party prefill on Load an Expense | 52 | 49 |
| 4 | [slice-4-subscription-names.md](slice-4-subscription-names.md) | `<name> Subscription` accounts + migration | 53 | 50 |
| 5 | [slice-5-dashboard.md](slice-5-dashboard.md) | Relabel, actions on top, Due this month | 54 | 51 |
| 6 | [slice-6-closing-date.md](slice-6-closing-date.md) | Editable usual day + per-month overrides + re-bucket | 55 | 52 |
| 7 | [slice-7-reverse.md](slice-7-reverse.md) | Rich transaction feed + reverse impact | 56 | 53 |
| 8 | [slice-8-glossary-sweep.md](slice-8-glossary-sweep.md) | Apply glossary to every remaining string + guard spec | — | 54 |

Why this order: cheap, isolated client wins first (1–2) to warm up; slice 3 is a deletion; 4 is
a small API + migration; 5 depends on 4 (the dashboard list shows subscription names) and needs a
small API change; 6 and 7 are the heavy API slices; 8 goes last so it can sweep whatever copy the
earlier slices didn't touch.

## Step cadence (every slice)

The user green-lights **one step at a time**:

1. API production code — `dotnet build` clean.
2. API tests — per project: `dotnet run --project tests/<Project>` (the repo memory notes `dotnet test` is flaky here; use whichever works, both must be green).
3. Client production code — `pnpm ng lint` + `pnpm ng build --configuration production` clean.
4. Client specs — `CHROME_BIN=/usr/bin/brave pnpm ng test --watch=false --browsers=ChromeHeadless` green.
5. Doc-sync — one ledger line per phase in `app/api/.claude/TASK.md` / `app/client/.claude/TASK.md`;
   touch `app/*/.claude/CLAUDE.md` **only** for evergreen facts (new entity, new endpoint, new convention).
   Full detail stays in these docs.

Client-only slices skip steps 1–2. The user commits their own work.

## Shared facts (verified 2026-09-29)

Path prefixes used in every slice: `A/` = `app/api/src`, `F/` = `A/Modules/Financing/PersonalFinance.Financing`,
`L/` = `A/Modules/Ledger/PersonalFinance.Ledger`, `R/` = `A/Reporting/PersonalFinance.Reporting`,
`H/` = `A/Bootstrap/PersonalFinance.Api/Endpoints`, `C/` = `app/client/src/app`.

- Client never renders API error text: `C/core/http/problem-details.interceptor.ts` maps errors to
  `AppError`, and each page maps `error.code` to its own hand-written sentence. So **all copy lives in
  the client** — glossary work is client-side.
- Reporting reads cross-module data through SQL views owned by each module
  (`*/Infrastructure/Persistence/ReadViews/vw_*.sql`) queried by `R/Sql/*.sql`. New cross-module reads
  follow that pattern.
- ARS and USD are never converted anywhere; every total is per currency.
